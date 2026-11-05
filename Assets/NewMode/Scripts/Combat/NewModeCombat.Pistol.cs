using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Unity.FPS.NewMode
{
    // 5. 권총 — 기믹 "탄창"
    //  탄창 12발, R로 재장전 (다 쓰면 자동). 탄창의 마지막 한 발은 피해 ×3.
    //  평타  속사                연사 (조준점으로)
    //  Q     부채 사격           공이치기를 훑어 6발을 부채꼴로 (탄 소모 없음)
    //  E     구르기              굴러서 피하고 즉시 재장전, 다음 3발 확정 치명타
    //  X     점착 수류탄         포물선으로 던져 닿은 적에게 붙고 1초 뒤 폭발
    //  C     데드아이            시간이 느려지는 동안 적을 차례로 조준 → 한꺼번에 쏜다
    public partial class NewModeCombat
    {
        static readonly Color k_Bullet = new Color(1f, 0.85f, 0.3f);
        const int k_MagSize = 12;
        int m_Ammo = k_MagSize;
        int m_SureShots;
        bool m_LastBullet;
        SkillDef m_PistolReload;

        WeaponDef PistolWeapon()
        {
            m_PistolReload = new SkillDef { Name = "재장전", Run = PistolReload };
            return new WeaponDef
            {
                FireInterval = 0.16f,
                GimmickName = "탄창",
                GimmickInfo = "12발 · R 재장전 · 마지막 한 발은 피해 ×3",
                Gimmick = () => $"{Bar(m_Ammo / (float)k_MagSize, k_MagSize)}  <b>{m_Ammo}</b> / {k_MagSize}"
                                + (m_SureShots > 0 ? $"   <color=#FF7A5C>확정 치명 {m_SureShots}</color>" : "")
                                + "   <size=80%><color=#FFFFFFAA>R 재장전</color></size>",
                Tick = () =>
                {
                    Keyboard kb = Keyboard.current;
                    if (m_Weapon.Index == 4 && kb != null && kb.rKey.wasPressedThisFrame && m_Current == null && m_Ammo < k_MagSize && !m_Player.InputBlocked)
                        StartAction(m_PistolReload);
                },
                HitMultiplier = d =>
                {
                    if (m_LastBullet) { m_HitTag = "마지막 한 발!"; return 3f; }
                    if (m_SureShots > 0) { m_HitTag = "확정 치명!"; return 1.5f; }
                    return 1f;
                },
                Basic = new SkillDef { Key = "좌클릭", Name = "속사", Formula = "y = (tan θ)x", Run = PistolBasic },
                Skills = new[]
                {
                    new SkillDef { Key = "Q", Name = "부채 사격", Formula = "θₖ = −25° + 10°k", Cooldown = 4f, Run = PistolFan,
                        Detail = () => "θₖ = −25° + 10°k,   k = 0…5" },
                    new SkillDef { Key = "E", Name = "구르기", Formula = "P(t) = P₀ + vt", Cooldown = 5f, Run = PistolRoll,
                        Detail = () => $"|ΔP| = {N(3.5f * S)}  ·  즉시 재장전" },
                    new SkillDef { Key = "X", Name = "점착 수류탄", Formula = "y = v sinθ t − ½gt²", Cooldown = 7f, Run = PistolGrenade,
                        Detail = () => $"x = {N(7f * S)}cos40°·t,   y = {N(7f * S)}sin40°·t − ½·{N(9.8f * S)}t²" },
                    new SkillDef { Key = "C", Name = "데드아이", Formula = "t' = 0.3t", Cooldown = 15f, Run = PistolDeadeye,
                        Detail = () => "시간 × 0.3  ·  최대 8명 조준" },
                },
            };
        }

        IEnumerator PistolBasic()
        {
            if (m_Ammo <= 0) { yield return PistolReload(); yield break; }
            yield return Aim(0f, 0.02f);
            m_LastBullet = m_Ammo == 1;
            MuzzleFlash(m_LastBullet ? "Holy hit" : "Sparks explode yellow", m_LastBullet ? 0.3f : 0.18f);
            Shoot(0f, 25f * S, 0.45f * S, 16f, m_LastBullet, "Electro hit", false, k_Bullet, m_LastBullet ? 0.08f : 0.04f);
            m_LastBullet = false;
            if (m_SureShots > 0) m_SureShots--;
            m_Ammo--;
            yield return new WaitForSeconds(0.04f);
            if (m_Ammo <= 0) yield return PistolReload();
        }

        // 재장전: 총을 아래로 내리고 한 바퀴 돌린다
        IEnumerator PistolReload()
        {
            const float time = 0.85f;
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                float u = t / time;
                float spin = u * Mathf.PI * 2f;
                Pose(new Vector3(0.2f + 0.3f * Mathf.Sin(spin), -0.6f + 0.3f * Mathf.Cos(spin), 0.5f), Vector3.up, 0.75f);
                BodyLean = 8f;
                yield return null;
            }
            BodyLean = 0f;
            m_Ammo = k_MagSize;
            Fx.Play("Sparks flashing yellow", Muzzle, Quaternion.identity, 0.2f * S, 0.1f, false);
        }

        // Q: 공이치기를 훑어 6발을 부채꼴로
        IEnumerator PistolFan()
        {
            for (int k = 0; k < 6; k++)
            {
                float theta = -25f + 10f * k;
                yield return Aim(theta, 0.01f);
                BodyTwist = theta * 0.4f;
                MuzzleFlash("Sparks explode yellow", 0.2f);
                Shoot(theta, 18f * S, 0.5f * S, 20f, false, "Electro hit", false, k_Bullet);
                yield return new WaitForSeconds(0.045f);
            }
            BodyTwist = 0f;
        }

        // E: 입력 방향(없으면 뒤)으로 구르기 → 즉시 재장전 + 다음 3발 확정 치명
        IEnumerator PistolRoll()
        {
            Keyboard kb = Keyboard.current;
            Vector2 input = Vector2.zero;
            if (kb != null)
            {
                if (kb.wKey.isPressed) input.y += 1f;
                if (kb.sKey.isPressed) input.y -= 1f;
                if (kb.dKey.isPressed) input.x += 1f;
                if (kb.aKey.isPressed) input.x -= 1f;
            }
            if (input.sqrMagnitude < 0.01f) input = Vector2.down;
            Vector3 dir = Quaternion.Euler(0f, m_Player.Yaw, 0f) * new Vector3(input.x, 0f, input.y).normalized;

            OverridesMotion = true;
            CharacterController cc = m_Player.Controller;
            Fx.Play("Dust puff", transform.position, Quaternion.identity, 0.3f * S, 0.1f, false);
            Pose(new Vector3(0.1f, -0.5f, 0.6f), Vector3.up, 0.7f);
            const float time = 0.32f;
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                float u = t / time;
                Crouch = Mathf.Sin(u * Mathf.PI) * 1f;
                BodyLean = Mathf.Sin(u * Mathf.PI) * 40f;
                cc.Move(dir * 3.5f * S / time * Time.deltaTime + Vector3.down * 0.05f * S);
                yield return null;
            }
            Crouch = 0f;
            BodyLean = 0f;
            OverridesMotion = false;
            m_Ammo = k_MagSize;
            m_SureShots = 3;
            Fx.Play("Buff", transform.position, Quaternion.identity, 0.4f * S, 0.3f, false, transform);
        }

        // X: 점착 수류탄 — 포물선, 닿은 적에게 붙고 1초 뒤 폭발 (넉백)
        IEnumerator PistolGrenade()
        {
            float v = 7f * S, angle = 40f, g = 9.8f * S;
            Pose(new Vector3(-0.3f, 0.6f, -0.4f), Vector3.up, 0.9f);
            BodyTwist = -20f;
            yield return new WaitForSeconds(0.12f);
            Pose(new Vector3(-0.1f, 0.3f, 1f), Vector3.up, 1f);
            BodyTwist = 15f;
            LocksMovement = false;

            GameObject nade = Ball(0.18f * S, new Color(0.3f, 0.35f, 0.2f));
            Vector3 start = Chest + transform.forward * 0.3f * S;
            Vector3 vel = m_Frame * Vector3.forward * v * Mathf.Cos(angle * Mathf.Deg2Rad) + Vector3.up * v * Mathf.Sin(angle * Mathf.Deg2Rad);
            TrainingDummy stuck = null;
            Vector3 stuckOffset = Vector3.zero;
            float flight = 0f;
            while (flight < 3f)
            {
                flight += Time.deltaTime;
                Vector3 p = start + vel * flight + Vector3.down * 0.5f * g * flight * flight;
                nade.transform.position = p;
                foreach (var d in Targets())
                    if ((d.Center - p).magnitude < 0.7f * d.Scale + 0.3f * S) { stuck = d; stuckOffset = p - d.transform.position; break; }
                if (stuck || p.y <= GeoFx.GroundY(p, S) + 0.05f * S) break;
                yield return null;
            }
            BodyTwist = 0f;
            SwordActive = false;

            // 깜빡이며 1초
            var r = nade.GetComponent<Renderer>();
            for (float t = 0f; t < 1f; t += Time.deltaTime)
            {
                if (stuck) nade.transform.position = stuck.transform.position + stuckOffset;
                r.material.SetColor("_EmissionColor", (Mathf.Repeat(t * 8f, 1f) < 0.5f ? Color.red * 3f : Color.black));
                yield return null;
            }
            Vector3 at = nade.transform.position;
            Destroy(nade);
            Fx.Play("Explosion", at, Quaternion.identity, 0.4f * S, 0.2f, false);
            m_Player.Shake(0.15f, 0.3f);
            foreach (var d in Nearest(at, 2.6f * S, 99))
            {
                Damage(d, d == stuck ? 80f : 45f, true, null);
                d.Knock((d.transform.position - at).normalized * 9f * S);
            }
        }

        // C: 데드아이 — 시간이 느려지는 동안 앞쪽 적을 하나씩 조준 표시 → 한꺼번에 쏜다
        IEnumerator PistolDeadeye()
        {
            var targets = InFront(22f * S, 75f, 8);
            m_SlowMo = 0.3f;
            Time.timeScale = m_SlowMo;
            var marks = new List<GameObject>();
            yield return Aim(0f, 0f);
            foreach (var d in targets)
            {
                marks.Add(Fx.Play("Sparks flashing yellow", d.Head + Vector3.up * 0.3f * S, Quaternion.identity, 0.3f * S, 3f, false, d.transform));
                FaceTo(d.transform.position);
                yield return new WaitForSecondsRealtime(0.12f);
            }
            yield return new WaitForSecondsRealtime(0.15f);

            foreach (var d in targets)
            {
                if (!d || !d.Alive) continue;
                FaceTo(d.transform.position);
                SetFrame();
                Pose(transform.InverseTransformDirection((d.Center - Muzzle).normalized), Vector3.up, 0.95f);
                MuzzleFlash("Sparks explode yellow", 0.3f);
                GeoFx.Line(new[] { Muzzle, d.Center }, 0.06f * S, Color.Lerp(k_Bullet, Color.red, 0.5f), 0f, 0.25f);
                m_HitTag = null;
                Damage(d, 70f, true, "Sparks explode yellow");
                Recoil = 1f;
                yield return new WaitForSecondsRealtime(0.07f);
            }
            foreach (var m in marks) if (m) Destroy(m);
            m_SlowMo = 1f;
            Time.timeScale = 1f;
            m_Ammo = k_MagSize;
        }
    }
}
