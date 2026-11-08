using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Unity.FPS.NewMode
{
    // 4. 권총 — 기믹 "탄창 · 약점" (연계)
    //  탄창 12발, R로 재장전 (다 쓰면 자동). 탄창의 마지막 한 발은 피해 ×3.
    //  [약점] Q 부채 사격에 맞은 적은 약점이 드러난다 (노란 불꽃, 6초).
    //  [관통] X 관통탄은 일직선의 적을 모두 꿰뚫고, 약점이 드러난 적에겐 ×3. C 데드아이도 약점 대상 ×2.
    //  → Q로 여럿에게 약점 → 한 줄로 세워 X 관통탄이 기본 연계. 폭탄 같은 총과 상관없는 기술은 없다.
    //  평타  속사                연사 (조준 시 조준점으로)
    //  Q     부채 사격           공이치기를 훑어 6발을 부채꼴로 (탄 소모 없음) — 약점 노출
    //  E     구르기              굴러서 피하고 즉시 재장전, 다음 3발 확정 치명타
    //  X     관통탄              0.3초 겨눈 뒤 한 줄을 꿰뚫는 큰 탄 — 약점 ×3
    //  C     데드아이            시간이 느려지는 동안 적을 차례로 조준 → 한꺼번에 쏜다
    public partial class NewModeCombat
    {
        static readonly Color k_Bullet = new Color(1f, 0.85f, 0.3f);
        const int k_MagSize = 12;
        int m_Ammo = k_MagSize;
        int m_SureShots;
        bool m_LastBullet;
        SkillDef m_PistolReload;
        const string k_BulletHit = "Sparks explode yellow";

        WeaponDef PistolWeapon()
        {
            m_PistolReload = new SkillDef { Name = "재장전", Run = PistolReload };
            return new WeaponDef
            {
                FireInterval = 0.2f,
                GimmickName = "탄창 · 약점",
                GimmickInfo = "Q로 약점 노출 → X 관통탄 ×3 · 마지막 한 발 ×3",
                Gimmick = () =>
                {
                    int exposed = 0;
                    foreach (var d in TrainingDummy.All) if (d.Alive && d.Exposed) exposed++;
                    return $"{Bar(m_Ammo / (float)k_MagSize, k_MagSize)}  <b>{m_Ammo}</b> / {k_MagSize}"
                         + (m_SureShots > 0 ? $"   <color=#FF7A5C>확정 치명 {m_SureShots}</color>" : "")
                         + $"   약점 <b>{exposed}</b>   <size=80%><color=#FFFFFFAA>Q로 약점 → X 관통 ×3 · R 재장전</color></size>";
                },
                Tick = () =>
                {
                    Keyboard kb = Keyboard.current;
                    if (m_Weapon.Index == 3 && ((kb != null && kb.rKey.wasPressedThisFrame) || Unity.FPS.Gameplay.VirtualInput.WasPressed("reload")) && m_Current == null && m_Ammo < k_MagSize && !m_Player.InputBlocked)
                        StartAction(m_PistolReload);
                },
                HitMultiplier = d =>
                {
                    if (m_LastBullet) { m_HitTag = "마지막 한 발!"; return 3f; }
                    if (m_SureShots > 0) { m_HitTag = "확정 치명!"; return 1.5f; }
                    return 1f;
                },
                Basic = new SkillDef { Key = "좌클릭", Name = "속사", Desc = "3단: 한 발 → 한 발 → 두 발. 우클릭 조준 시 머리를 맞히면 ×1.8", Formula = "y = (tan θ)x", Run = PistolBasic },
                Skills = new[]
                {
                    new SkillDef { Key = "Q", Name = "부채 사격", Desc = "공이치기를 훑어 6발을 부채꼴로 — 맞은 적은 약점 노출 (6초)", Formula = "θₖ = −25° + 10°k", Cooldown = 3.5f, Run = PistolFan,
                        Detail = () => "θₖ = −25° + 10°k,   k = 0…5" },
                    new SkillDef { Key = "E", Name = "구르기", Desc = "구르며 피하고 즉시 재장전, 다음 3발 확정 치명타", Formula = "P(t) = P₀ + vt", Cooldown = 5f, Run = PistolRoll,
                        Detail = () => $"|ΔP| = {N(3.5f * S)}  ·  즉시 재장전" },
                    new SkillDef { Key = "X", Name = "관통탄", Desc = "잠깐 겨눈 뒤 한 줄을 모두 꿰뚫는 큰 탄 — 약점 노출된 적 ×3", Formula = "P(t) = P₀ + t·v", Cooldown = 6f, Run = PistolPierce,
                        Detail = () => $"P(t) = P₀ + t·v,   0 ≤ t ≤ {N(30f * S)}" },
                    new SkillDef { Key = "C", Name = "데드아이", Desc = "시간을 느리게 하고 최대 8명을 조준해 한꺼번에 사격 — 약점 대상 ×2", Formula = "t' = 0.3t", Cooldown = 14f, Run = PistolDeadeye,
                        Detail = () => "시간 × 0.3  ·  최대 8명 조준" },
                },
            };
        }

        // 평타 3단: 1단 한 발 → 2단 한 발 → 3단 두 발 연속 (더 셈). 누르고 있으면 1·2·3단이 이어짐
        IEnumerator PistolBasic() => Combo(3, PistolShot);

        IEnumerator PistolShot(int step)
        {
            LocksMovement = false; // 쏘면서 걸을 수 있음
            if (m_Ammo <= 0) { yield return PistolReload(); yield break; }
            yield return Aim(0f, 0.02f);
            int shots = step == 2 ? 2 : 1;
            for (int i = 0; i < shots && m_Ammo > 0; i++)
            {
                m_LastBullet = m_Ammo == 1;
                MuzzleFlash(m_LastBullet ? "Holy hit" : "Sparks explode yellow", m_LastBullet || step == 2 ? 0.3f : 0.18f);
                Shoot(i == 0 ? 0f : 2f, 25f * S, 0.4f * S, step == 2 ? 17f : 19f, m_LastBullet || step == 2, k_BulletHit, false, k_Bullet, m_LastBullet ? 0.08f : 0.04f);
                m_LastBullet = false;
                if (m_SureShots > 0) m_SureShots--;
                m_Ammo--;
                if (shots > 1) yield return new WaitForSeconds(0.07f);
            }
            yield return new WaitForSeconds(0.04f);
            if (m_Ammo <= 0) yield return PistolReload();
        }

        // 재장전: 총을 아래로 내리고 한 바퀴 돌린다
        IEnumerator PistolReload()
        {
            const float time = 0.6f;
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

        // Q: 공이치기를 훑어 6발을 부채꼴로 — 맞은 적은 약점 노출
        IEnumerator PistolFan()
        {
            m_ShotHook = d => d.Expose(6f);
            for (int k = 0; k < 6; k++)
            {
                float theta = -25f + 10f * k;
                yield return Aim(theta, 0.01f);
                BodyTwist = theta * 0.4f;
                MuzzleFlash("Sparks explode yellow", 0.2f);
                Shoot(theta, 18f * S, 0.5f * S, 28f, false, k_BulletHit, false, k_Bullet);
                yield return new WaitForSeconds(0.045f);
            }
            m_ShotHook = null;
            Fx.Play("Smoke puff", Muzzle, Quaternion.identity, 0.25f * S, 0.2f, false);
            BodyTwist = 0f;
        }

        // E: 입력 방향(없으면 뒤)으로 구르기 → 즉시 재장전 + 다음 3발 확정 치명
        //  위치 x(u) = D·(1 − (1 − u)³)  (빠르게 굴러 나가 부드럽게 멈춤) + 끝난 뒤 남은 관성
        IEnumerator PistolRoll()
        {
            Vector3 dir = m_Player.MoveInputWorld;
            if (dir.sqrMagnitude < 0.01f) dir = -transform.forward;
            dir.y = 0f;
            dir.Normalize();

            OverridesMotion = true;
            CharacterController cc = m_Player.Controller;
            Fx.Play("Dust puff", transform.position, Quaternion.identity, 0.3f * S, 0.1f, false);
            Pose(new Vector3(0.1f, -0.5f, 0.6f), Vector3.up, 0.7f);
            const float time = 0.34f;
            float dist = 3.5f * S, done = 0f;
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                float u = Mathf.Clamp01(t / time);
                float want = dist * (1f - Mathf.Pow(1f - u, 3f));
                Crouch = Mathf.Sin(u * Mathf.PI) * 1f;
                BodyLean = Mathf.Sin(u * Mathf.PI) * 40f;
                cc.Move(dir * (want - done) + Vector3.down * 0.05f * S);
                done = want;
                yield return null;
            }
            Crouch = 0f;
            BodyLean = 0f;
            OverridesMotion = false;
            m_Player.AddMomentum(dir * 1.5f * S);
            m_Ammo = k_MagSize;
            m_SureShots = 3;
            Fx.Play("Buff", transform.position, Quaternion.identity, 0.4f * S, 0.3f, false, transform);
        }

        // X: 관통탄 — 0.3초 동안 금빛 조준선이 겨누고, 한 줄을 모두 꿰뚫는 큰 탄. 약점 노출된 적은 ×3
        IEnumerator PistolPierce()
        {
            float length = 30f * S;
            Pose(new Vector3(0f, 0.12f, 1f), Vector3.up, 1f);
            BodyTwist = -10f;
            yield return Aim(0f, 0.05f);

            // 조준선
            Vector3 from = Muzzle;
            Vector3 dir = AimDirection(from);
            Vector3 to = from + dir * length;
            if (Physics.Raycast(from, dir, out RaycastHit wall, length, ~(1 << 2), QueryTriggerInteraction.Ignore)) to = wall.point;
            GeoFx.Line(new[] { from, to }, 0.02f * S, new Color(1f, 0.9f, 0.4f, 0.9f), 0f, 0.32f);
            Fx.Play("Sparks flashing yellow", Muzzle, Quaternion.identity, 0.3f * S, 0.3f, false);
            for (float t = 0f; t < 0.3f; t += Time.deltaTime)
            {
                Pose(new Vector3(0f, 0.12f, 1f), Vector3.up, 1f);
                BodyLean = -4f * t / 0.3f;
                yield return null;
            }

            // 탕! — 꿰뚫기
            m_ShotOverride = d =>
            {
                if (!d.Exposed) return false;
                d.ClearExpose();
                DamageBonus(d, 70f, true, "Holy hit", 3f, "약점 관통 ×3!");
                return true;
            };
            MuzzleFlash("Holy hit", 0.45f);
            Vector3 end = Shoot(0f, length, 0.6f * S, 70f, true, k_BulletHit, true, k_Bullet, 0.14f);
            m_ShotOverride = null;
            GeoFx.Line(new[] { Muzzle, end }, 0.05f * S, Color.white, 0f, 0.25f);
            Fx.Play("Smoke puff", Muzzle, Quaternion.identity, 0.35f * S, 0.25f, false);
            Fx.Play("Sparks explode yellow", end, Quaternion.identity, 0.5f * S, 0.2f, false);
            LightFlash.Spawn(Muzzle, k_Bullet, 8f, 4f * S, 0.25f);
            PostFx.Pulse(0.55f, k_Bullet);
            m_Player.Shake(0.12f, 0.2f);
            Recoil = 1f;
            m_Player.AddMomentum(-transform.forward * 3f * S); // 반동으로 살짝 밀려남
            BodyLean = -14f;
            yield return new WaitForSeconds(0.18f);
            BodyLean = 0f;
            BodyTwist = 0f;
        }

        // C: 데드아이 — 시간이 느려지는 동안 앞쪽 적을 하나씩 조준 표시 → 한꺼번에 쏜다 (약점 ×2)
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
                yield return new WaitForSecondsRealtime(0.1f);
            }
            yield return new WaitForSecondsRealtime(0.12f);

            foreach (var d in targets)
            {
                if (!d || !d.Alive) continue;
                FaceTo(d.transform.position);
                SetFrame();
                Pose(transform.InverseTransformDirection((d.Center - Muzzle).normalized), Vector3.up, 0.95f);
                MuzzleFlash("Sparks explode yellow", 0.3f);
                GeoFx.Line(new[] { Muzzle, d.Center }, 0.06f * S, Color.Lerp(k_Bullet, Color.red, 0.5f), 0f, 0.25f);
                m_HitTag = null;
                if (d.Exposed) { d.ClearExpose(); DamageBonus(d, 115f, true, k_BulletHit, 2f, "약점 ×2"); }
                else Damage(d, 115f, true, k_BulletHit);
                UltSpark(d.Center);
                RingWave.Spawn(d.transform.position + Vector3.up * 0.05f * S, 1.4f * S, new Color(1f, 0.85f, 0.3f, 0.85f), 0.3f, true);
                Recoil = 1f;
                yield return new WaitForSecondsRealtime(0.07f);
            }
            foreach (var m in marks) if (m) Destroy(m);

            // 마무리: 총을 빙글 돌려 집어넣는다 (금빛 고리 + 총구 연기, 폭발 없음)
            m_SlowMo = 1f;
            Time.timeScale = 1f;
            RingWave.Spawn(transform.position + Vector3.up * 0.05f * S, 3.5f * S, new Color(1f, 0.85f, 0.35f, 0.9f), 0.45f);
            Fx.Play("Smoke puff", Muzzle, Quaternion.identity, 0.3f * S, 0.25f, false);
            for (float t = 0f; t < 0.3f; t += Time.deltaTime)
            {
                float g = t / 0.3f * Mathf.PI * 4f;
                Pose(new Vector3(0.35f, Mathf.Sin(g) * 0.6f - 0.2f, Mathf.Cos(g) * 0.6f), new Vector3(0f, Mathf.Cos(g), -Mathf.Sin(g)), 0.8f);
                yield return null;
            }
            m_Ammo = k_MagSize;
        }
    }
}
