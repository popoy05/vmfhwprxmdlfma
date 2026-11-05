using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.FPS.NewMode
{
    // 6. 산탄총 — 기믹 "근접 사격 · 펌프"
    //  가까울수록 세다 (피해 ×(2 − d/8), 최대 ×2) + 맞은 적을 밀어낸다. 2발 쏘면 펌프질(장전).
    //  평타  산탄                8발 원뿔
    //  Q     반동 도약           땅에 쏘고 그 반동으로 뒤로 높이 뛴다
    //  E     화염 방사           1.6초 동안 앞쪽 원뿔에 불길
    //  X     슬러그탄            한 발짜리 무거운 탄 — 한 줄로 꿰뚫고 멀리 밀어낸다
    //  C     용의 숨결           앞으로 세 번 커지는 불의 원뿔 폭발
    public partial class NewModeCombat
    {
        static readonly Color k_Fire = new Color(1f, 0.45f, 0.2f);
        int m_Shells = 2;

        WeaponDef ShotgunWeapon() => new WeaponDef
        {
            FireInterval = 0.25f,
            GimmickName = "근접 사격",
            GimmickInfo = "가까울수록 최대 ×2 · 맞은 적을 밀어냄 · 2발마다 펌프",
            Gimmick = () => $"장탄 <b>{new string('■', m_Shells)}<color=#FFFFFF44>{new string('■', 2 - m_Shells)}</color></b>   <size=80%><color=#FFFFFFAA>붙어서 쏘면 ×2</color></size>",
            HitMultiplier = d =>
            {
                float dist = Vector3.Distance(d.transform.position, transform.position);
                float m = Mathf.Lerp(2f, 1f, Mathf.Clamp01(dist / (8f * S)));  // 2 − d/8
                if (m > 1.7f) m_HitTag = "근접!";
                Vector3 away = d.transform.position - transform.position;
                d.Knock(away.normalized * 2.5f * S * m);
                return m;
            },
            Basic = new SkillDef { Key = "좌클릭", Name = "산탄", Formula = "|θ| ≤ 14°,  피해 × (2 − d/8)", Run = ShotgunBasic },
            Skills = new[]
            {
                new SkillDef { Key = "Q", Name = "반동 도약", Formula = "F = −ma", Cooldown = 5f, Run = ShotgunRecoil,
                    Detail = () => $"뒤로 {N(4f * S)},  높이 {N(2.2f * S)}" },
                new SkillDef { Key = "E", Name = "화염 방사", Formula = "r ≤ 5,  |θ| ≤ 30°", Cooldown = 8f, Run = ShotgunFlame,
                    Detail = () => $"r ≤ {N(5f * S)},   |θ| ≤ 30°,   1.6초" },
                new SkillDef { Key = "X", Name = "슬러그탄", Formula = "P(t) = P₀ + t·v", Cooldown = 6f, Run = ShotgunSlug,
                    Detail = () => $"관통 직선,  밀어내기 v₀ = {N(14f * S)}" },
                new SkillDef { Key = "C", Name = "용의 숨결", Formula = "rₖ = 3k,  k = 1, 2, 3", Cooldown = 15f, Run = ShotgunDragon,
                    Detail = () => $"rₖ = {N(3f * S)}·k,   반각 20° + 8°k" },
            },
        };

        IEnumerator ShotgunBasic()
        {
            if (m_Shells <= 0) { yield return ShotgunPump(); yield break; }
            yield return Aim(0f, 0.03f);
            MuzzleFlash("Sparks explode red", 0.35f);
            Fx.Play("Smoke puff", Muzzle, Quaternion.identity, 0.2f * S, 0.1f, false);
            for (int i = 0; i < 8; i++)
                Shoot(Random.Range(-14f, 14f), 9f * S, 0.4f * S, 9f, false, i == 0 ? "Sparks explode red" : null, false, k_Fire, 0.025f);
            Recoil = 1f;
            m_Player.Shake(0.07f, 0.15f);
            m_Shells--;
            yield return new WaitForSeconds(0.12f);
            if (m_Shells <= 0) yield return ShotgunPump();
        }

        // 펌프: 왼손으로 당겼다 미는 느낌 (총을 앞뒤로)
        IEnumerator ShotgunPump()
        {
            for (float t = 0f; t < 0.4f; t += Time.deltaTime)
            {
                float u = Mathf.Sin(t / 0.4f * Mathf.PI);
                Pose(new Vector3(0.1f, 0.15f + 0.15f * u, 1f), Vector3.up, 0.95f - 0.25f * u);
                BodyLean = -6f * u;
                yield return null;
            }
            BodyLean = 0f;
            m_Shells = 2;
        }

        // Q: 발밑 앞쪽에 쏘고, 그 반동으로 뒤로 높이 뛴다
        IEnumerator ShotgunRecoil()
        {
            Pose(new Vector3(0.05f, -0.7f, 0.7f), Vector3.up, 0.95f);
            yield return new WaitForSeconds(0.08f);
            MuzzleFlash("Red energy explosion", 0.25f);
            Fx.Play("Dust ground", OnGround(new Vector2(1.2f * S, 0f)), Quaternion.identity, 0.4f * S, 0.15f, false);
            foreach (var d in InFront(4f * S, 45f, 99)) Damage(d, 34f, true, "Sparks explode red");
            m_Player.Shake(0.12f, 0.25f);
            Recoil = 1f;
            BodyLean = -25f;
            yield return Leap(-4f * S, 2.2f * S, 0.5f, null);
            BodyLean = 0f;
            m_Shells = 2;
        }

        // E: 1.6초 동안 화염 방사 (0.15초마다 원뿔 안 적에게 피해)
        IEnumerator ShotgunFlame()
        {
            const float time = 1.6f;
            float r = 5f * S;
            float nextTick = 0f, nextFx = 0f;
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                float sway = Mathf.Sin(t * 9f) * 12f;
                yield return Aim(sway, 0f);
                BodyTwist = sway * 0.4f;
                Recoil = 0.25f;
                if (t >= nextFx)
                {
                    nextFx += 0.07f;
                    Vector3 dir = Facing(sway) * Vector3.forward;
                    float d = Random.Range(0.8f, r);
                    Fx.Play(Random.value < 0.5f ? "Sparks flashing red" : "Smoke puff", Muzzle + dir * d + Random.insideUnitSphere * 0.3f * S, Quaternion.LookRotation(dir), (0.2f + 0.1f * d / r) * S, 0.1f, false);
                }
                if (t >= nextTick)
                {
                    nextTick += 0.15f;
                    foreach (var dmy in InFront(r, 30f, 99)) Damage(dmy, 6f, false, null);
                }
                yield return null;
            }
            BodyTwist = 0f;
        }

        // X: 슬러그탄 — 한 줄로 꿰뚫고 멀리 밀어낸다 (핀볼처럼 부딪침)
        IEnumerator ShotgunSlug()
        {
            yield return Aim(0f, 0.12f);
            Crouch = 0.5f;
            MuzzleFlash("Red energy explosion", 0.3f);
            Vector3 end = Shoot(0f, 18f * S, 0.55f * S, 50f, true, "Sparks explode red", true, k_Fire, 0.12f);
            Vector3 dir = (end - Muzzle).normalized;
            foreach (var d in Targets())
            {
                Vector3 to = d.Center - Muzzle;
                float along = Vector3.Dot(to, dir);
                if (along < 0f || (to - dir * along).magnitude > 0.7f * S) continue;
                d.Knock(dir * 14f * S);
            }
            Recoil = 1f;
            BodyLean = -15f;
            m_Player.Shake(0.15f, 0.25f);
            yield return new WaitForSeconds(0.25f);
            Crouch = 0f;
            BodyLean = 0f;
        }

        // C: 용의 숨결 — 앞으로 세 번, 점점 크고 넓은 불의 원뿔
        IEnumerator ShotgunDragon()
        {
            Pose(new Vector3(0.1f, 0.2f, 1f), Vector3.up, 0.95f);
            Crouch = 0.8f;
            Fx.Play("Charge slash red", Muzzle, Facing(), 0.4f * S, 0.4f, false);
            yield return new WaitForSeconds(0.3f);
            for (int k = 1; k <= 3; k++)
            {
                float r = 3f * S * k, half = 20f + 8f * k;
                MuzzleFlash("Red energy explosion", 0.25f + 0.08f * k);
                for (int i = 0; i < 2 + k; i++)
                {
                    float a = Mathf.Lerp(-half, half, (i + 0.5f) / (2 + k));
                    Vector3 w = OnGround(GeoFunctions.Arc(r * 0.85f, a));
                    Fx.Play(i % 2 == 0 ? "Explosion" : "Sparks explode red", w + Vector3.up * 0.4f * S, Quaternion.identity, (0.2f + 0.05f * k) * S, 0.15f, false);
                }
                foreach (var d in InFront(r, half, 99))
                {
                    if (Local(d.transform.position).magnitude < r - 3f * S) continue; // 이번 고리에 있는 적만
                    Damage(d, 40f + 15f * k, true, null);
                    d.Launch(Vector3.up * (3f + k) * S + (d.transform.position - transform.position).normalized * 3f * S, 18f * S);
                }
                Recoil = 1f;
                BodyLean = -10f * k;
                m_Player.Shake(0.08f * k, 0.25f);
                yield return new WaitForSeconds(0.28f);
            }
            Crouch = 0f;
            BodyLean = 0f;
            m_Shells = 2;
        }
    }
}
