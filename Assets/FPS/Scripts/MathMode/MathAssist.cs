using System.Collections.Generic;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace Unity.FPS.MathMode
{
    // 조작 보조 (메인 메뉴에서 각각 켜고 끈다)
    //  자동 무기: 적과의 거리에 맞는 무기로 바꾸고, 탄이 떨어지면 다른 무기로 바꾼다
    //  자동 스킬: 상황에 맞춰 충격파·대시·포격·회복·궁극기를 대신 쓴다
    //  조준 보정: 화면 가운데 근처의 적 쪽으로 시선을 살짝 끌어당긴다 (쏘는 중에는 더 강하게)
    public class MathAssist : MonoBehaviour
    {
        // 무기 이름 → 사거리 역할 (0 근거리, 1 중거리, 2 원거리, -1 자동 선택 안 함)
        static readonly Dictionary<string, int> k_Roles = new Dictionary<string, int>
        {
            { "Blaster", 1 }, { "사인 블래스터", 1 }, { "지그재그 SMG", 1 }, { "삼중 나선총", 1 }, { "원뿔 샷건", 0 },
            { "감쇠 진동 저격총", 2 }, { "장미 곡선 캐논", 1 }, { "지수 레일건", 2 }, { "포물선 런처", -1 },
            { "베지에 유도탄", 1 }, { "프랙탈 분열탄", 0 }, { "극한 블랙홀포", 2 }, { "그래프 연쇄 번개", 1 },
            { "리사주 부메랑", 0 }, { "홍련 초승달 참격", 0 },
        };

        public bool AutoWeapon, AutoSkills, AimAssist;

        MathModeManager m_Manager;
        PlayerWeaponsManager m_Weapons;
        PlayerInputHandler m_Input;
        PlayerCharacterController m_Controller;
        EnemyManager m_Enemies;
        Transform m_Camera;
        float m_NextWeaponCheck, m_LastSwitch, m_NextSkillCheck;
        float m_LastHealth, m_RecentDamage;

        public void Init(MathModeManager manager)
        {
            m_Manager = manager;
            m_Weapons = manager.PlayerWeapons;
            m_Input = manager.PlayerInput;
            m_Controller = GetComponent<PlayerCharacterController>();
            m_Enemies = FindAnyObjectByType<EnemyManager>();
            m_Camera = m_Controller && m_Controller.PlayerCamera ? m_Controller.PlayerCamera.transform : Camera.main ? Camera.main.transform : null;
            AutoWeapon = GameSettings.AutoWeapon;
            AutoSkills = GameSettings.AutoSkills;
            AimAssist = GameSettings.AimAssist;
            m_LastHealth = manager.PlayerHealth ? manager.PlayerHealth.CurrentHealth : 0f;
        }

        bool Busy => !m_Input || !m_Input.CanProcessInput() || (m_Manager.Ultimate && m_Manager.Ultimate.IsPlaying) ||
                     (m_Manager.Melee && m_Manager.Melee.IsActive);

        void Update()
        {
            TrackDamage();
            if (Busy || !m_Camera) return;

            if (AimAssist) UpdateAimAssist();
            if (AutoWeapon && Time.time >= m_NextWeaponCheck)
            {
                m_NextWeaponCheck = Time.time + 0.4f;
                UpdateAutoWeapon();
            }

            if (AutoSkills && Time.time >= m_NextSkillCheck)
            {
                m_NextSkillCheck = Time.time + 0.25f;
                UpdateAutoSkills();
            }
        }

        void TrackDamage()
        {
            Health h = m_Manager.PlayerHealth;
            if (!h) return;
            if (h.CurrentHealth < m_LastHealth) m_RecentDamage += m_LastHealth - h.CurrentHealth;
            m_RecentDamage = Mathf.MoveTowards(m_RecentDamage, 0f, 15f * Time.deltaTime);
            m_LastHealth = h.CurrentHealth;
        }

        // ------------------------------------------------------------------ 적 찾기

        IEnumerable<EnemyController> Enemies()
        {
            if (!m_Enemies || m_Enemies.Enemies == null) yield break;
            foreach (EnemyController e in m_Enemies.Enemies)
                if (e) yield return e;
        }

        bool Visible(Vector3 point, EnemyController enemy)
        {
            Vector3 from = m_Camera.position, d = point - from;
            foreach (RaycastHit hit in Physics.RaycastAll(from, d.normalized, d.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<PlayerCharacterController>()) continue;
                var owner = hit.collider.GetComponentInParent<EnemyController>();
                if (owner == enemy) continue;
                if (owner) continue; // 다른 적에 가려진 것은 괜찮다
                return false;
            }

            return true;
        }

        // 화면 가운데에서 maxAngle 안에 있는 보이는 적 (각도가 가장 작은 것)
        EnemyController TargetInCone(float maxAngle, float maxDistance, out Vector3 aimPoint)
        {
            aimPoint = default;
            EnemyController best = null;
            float bestAngle = maxAngle;
            foreach (EnemyController e in Enemies())
            {
                Vector3 p = SpecialProjectile.AimPointOf(e).position;
                Vector3 to = p - m_Camera.position;
                if (to.magnitude > maxDistance) continue;
                float angle = Vector3.Angle(m_Camera.forward, to);
                if (angle >= bestAngle || !Visible(p, e)) continue;
                bestAngle = angle;
                best = e;
                aimPoint = p;
            }

            return best;
        }

        EnemyController NearestVisible(float maxDistance, out float distance)
        {
            distance = float.MaxValue;
            EnemyController best = null;
            foreach (EnemyController e in Enemies())
            {
                Vector3 p = SpecialProjectile.AimPointOf(e).position;
                float d = Vector3.Distance(m_Camera.position, p);
                if (d >= distance || d > maxDistance || !Visible(p, e)) continue;
                distance = d;
                best = e;
            }

            return best;
        }

        int CountWithin(float radius)
        {
            int n = 0;
            foreach (EnemyController e in Enemies())
                if (Vector3.Distance(e.transform.position, transform.position) < radius) n++;
            return n;
        }

        // ------------------------------------------------------------------ 조준 보정

        void UpdateAimAssist()
        {
            bool firing = m_Input.GetFireInputHeld();
            float cone = VirtualInput.Active ? 14f : 9f;
            if (!firing) cone *= 0.6f;
            EnemyController target = TargetInCone(cone, 70f, out Vector3 point);
            if (!target || !m_Controller) return;

            Vector3 to = point - m_Camera.position;
            Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            Vector3 flatTo = Vector3.ProjectOnPlane(to, Vector3.up);
            float yaw = Vector3.SignedAngle(flatForward, flatTo, Vector3.up);
            float desiredPitch = -Mathf.Asin(Mathf.Clamp(to.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
            float pitch = desiredPitch - m_Controller.CameraVerticalAngle;

            float strength = firing ? 9f : 2.5f;
            if (VirtualInput.Active) strength *= 1.5f;
            float k = 1f - Mathf.Exp(-strength * Time.deltaTime);
            m_Controller.AddLookAngles(yaw * k, pitch * k);
        }

        // ------------------------------------------------------------------ 자동 무기

        static int RoleOf(WeaponController w) => w && k_Roles.TryGetValue(w.WeaponName, out int r) ? r : 1;

        static bool HasAmmo(WeaponController w) => w && w.GetCurrentAmmo() >= 1 && !w.IsReloading;

        void UpdateAutoWeapon()
        {
            if (!m_Weapons || m_Weapons.IsAiming) return;
            WeaponController current = m_Weapons.GetActiveWeapon();
            if (current && current.IsCharging) return;

            EnemyController enemy = NearestVisible(70f, out float distance);
            bool emptyNow = current && !HasAmmo(current);
            if (!enemy && !emptyNow) return;
            if (!emptyNow && Time.time - m_LastSwitch < 1.5f) return; // 너무 자주 바꾸지 않게

            int want = !enemy ? 1 : distance < 9f ? 0 : distance < 26f ? 1 : 2;
            if (current && HasAmmo(current) && RoleOf(current) == want) return;

            int bestIndex = -1;
            float bestScore = float.MinValue;
            for (int i = 0; i < PlayerWeaponsManager.SlotCount; i++)
            {
                WeaponController w = m_Weapons.GetWeaponAtSlotIndex(i);
                int role = RoleOf(w);
                if (!HasAmmo(w) || role < 0) continue;
                float score = (role == want ? 10f : -Mathf.Abs(role - want) * 3f) + w.GetCurrentAmmo() / (float)Mathf.Max(1, w.MaxAmmo) * 2f +
                              Random.value * 0.5f; // 같은 역할이면 여러 무기를 골고루 쓴다
                if (w == current) score += 1f;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0 || m_Weapons.GetWeaponAtSlotIndex(bestIndex) == current) return;
            m_Weapons.SwitchToWeaponIndex(bestIndex);
            m_LastSwitch = Time.time;
        }

        // ------------------------------------------------------------------ 자동 스킬

        void UpdateAutoSkills()
        {
            MathSkills skills = m_Manager.Skills;
            Health h = m_Manager.PlayerHealth;
            if (!skills || !h) return;
            float hp = h.MaxHealth > 0f ? h.CurrentHealth / h.MaxHealth : 1f;

            // 회복: 체력 45% 미만
            if (hp < 0.45f && skills.Cast(skills.Heal, true)) return;

            // 대시: 체력이 낮은데 짧은 시간에 크게 맞고 있으면 무적 대시로 빠져나간다
            if (hp < 0.6f && m_RecentDamage > h.MaxHealth * 0.15f && skills.Cast(skills.Dash, true))
            {
                m_RecentDamage = 0f;
                return;
            }

            // 충격파: 가까이 붙은 적
            if ((CountWithin(7f) >= 2 || CountWithin(4f) >= 1) && skills.Cast(skills.Shockwave, true)) return;

            // 포격: 조준선 위에 멀리 있는 적
            if (skills.Strike.Ready)
            {
                EnemyController target = TargetInCone(4f, 60f, out Vector3 p);
                if (target && Vector3.Distance(p, transform.position) > 12f && skills.Cast(skills.Strike, true)) return;
            }

            // 궁극기: 적이 3마리 이상 근처에 있거나 보스가 있을 때
            MathUltimate ult = m_Manager.Ultimate;
            if (ult && ult.IsReady)
            {
                int near = 0;
                bool boss = false;
                foreach (EnemyController e in Enemies())
                {
                    if (Vector3.Distance(e.transform.position, transform.position) > 45f) continue;
                    near++;
                    var me = e.GetComponent<MathEnemy>();
                    if (me && me.Kind == EnemyKind.Boss) boss = true;
                }

                if (near >= 3 || boss) ult.TryActivate();
            }
        }
    }
}
