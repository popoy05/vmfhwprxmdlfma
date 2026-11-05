using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.FPS.NewMode
{
    // 7. 저격총 — 기믹 "집중 · 헤드샷"
    //  조준(우클릭)하고 있으면 집중이 1.2초에 걸쳐 차오른다: 피해 ×(1 + 1.5·집중). 머리를 맞히면 ×2 "헤드샷!".
    //  평타  저격                조준점으로 한 발 (관통)
    //  Q     도탄                맞은 적에서 가까운 적으로 세 번 튕긴다
    //  E     레이저 드론         6초 동안 곁에 떠서 가까운 적에게 레이저를 쏜다
    //  X     백스텝 사격         뒤로 뛰며 집중 100%로 한 발
    //  C     궤도 폭격           조준한 곳에 하늘에서 거대한 빛기둥 + 둘레 육각형에 작은 기둥 여섯
    public partial class NewModeCombat
    {
        static readonly Color k_Purple = new Color(0.75f, 0.5f, 1f);
        float m_Focus;
        bool m_Headshot, m_FullFocus;

        WeaponDef SniperWeapon() => new WeaponDef
        {
            FireInterval = 0.9f,
            GimmickName = "집중",
            GimmickInfo = "조준하면 집중이 차오름 (최대 ×2.5) · 머리 ×2",
            Gimmick = () => $"{Bar(m_Focus, 10)}  <b>{Mathf.RoundToInt(m_Focus * 100f)}%</b>   <size=80%><color=#FFFFFFAA>우클릭 조준 · 머리를 노려라</color></size>",
            Tick = () =>
            {
                // 조준 중이면 1.2초에 걸쳐 차오르고, 풀면 빨리 빠진다
                bool aiming = m_Player.Aiming && m_Weapon.Index == 6;
                m_Focus = Mathf.MoveTowards(m_Focus, aiming ? 1f : 0f, Time.deltaTime * (aiming ? 1f / 1.2f : 2.5f));
            },
            HitMultiplier = d =>
            {
                float focus = m_FullFocus ? 1f : m_Focus;
                float m = 1f + 1.5f * focus;
                if (m_Headshot) { m *= 2f; m_HitTag = "헤드샷!"; }
                return m;
            },
            Basic = new SkillDef { Key = "좌클릭", Name = "저격", Formula = "피해 × (1 + 1.5·집중)", Run = SniperBasic },
            Skills = new[]
            {
                new SkillDef { Key = "Q", Name = "도탄", Formula = "입사각 = 반사각", Cooldown = 5f, Run = SniperRicochet,
                    Detail = () => "입사각 = 반사각,   3번 튕김" },
                new SkillDef { Key = "E", Name = "레이저 드론", Formula = "P(t) = (r cos ωt, h, r sin ωt)", Cooldown = 10f, Run = SniperDrone,
                    Detail = () => $"P(t) = ({N(1.2f * S)} cos 2t, {N(2f * S)}, {N(1.2f * S)} sin 2t)" },
                new SkillDef { Key = "X", Name = "백스텝 사격", Formula = "P' = P − d·û", Cooldown = 6f, Run = SniperBackstep,
                    Detail = () => $"P' = P − {N(3.5f * S)}·û,  집중 100%" },
                new SkillDef { Key = "C", Name = "궤도 폭격", Formula = "정육각형 Pₖ = r(cos 60°k, sin 60°k)", Cooldown = 16f, Run = SniperOrbital,
                    Detail = () => $"Pₖ = {N(3f * S)}(cos 60°k, sin 60°k),   k = 0…5" },
            },
        };

        /// <summary>조준점으로 한 발. 맞은 적(첫 번째)을 돌려준다. 머리 높이면 헤드샷.</summary>
        TrainingDummy SniperShot(float amount, bool fullFocus, float width = 0.06f)
        {
            Ray ray = m_Player.AimRay;
            float max = 120f * S, dist = max;
            if (Physics.Raycast(ray, out RaycastHit wall, max, ~(1 << 2), QueryTriggerInteraction.Ignore)) dist = wall.distance;
            TrainingDummy target = DummyOnRay(ray, dist, out float along);
            Vector3 point = ray.GetPoint(target ? along : dist);

            Vector3 from = Muzzle;
            GeoFx.Line(new[] { from, point }, width * S, Color.Lerp(k_Purple, Color.white, 0.3f), 0f, 0.3f);
            MuzzleFlash("FX_Purple_Hit_02", 0.3f);
            Recoil = 1f;
            m_Player.Shake(0.05f, 0.12f);
            if (!target) { Fx.Play("Sparks explode pink", point, Quaternion.identity, 0.25f * S, 0.1f, false); return null; }

            m_Headshot = point.y >= target.Head.y - 0.22f * target.Scale * (target.Kind == MobKind.Brute ? 1.7f : 1f);
            m_FullFocus = fullFocus;
            Damage(target, amount, true, "FX_Purple_Hit_02", m_Headshot ? 0.7f : 0.45f);
            target.Knock(ray.direction * 3f * S);
            m_Headshot = false;
            m_FullFocus = false;
            return target;
        }

        IEnumerator SniperBasic()
        {
            yield return Aim(0f, 0.04f);
            SniperShot(40f, false);
            m_Focus *= 0.3f; // 쏘면 집중이 흐트러짐
            yield return new WaitForSeconds(0.15f);
        }

        // Q: 첫 적에게서 가까운 적으로 세 번 튕긴다
        IEnumerator SniperRicochet()
        {
            yield return Aim(0f, 0.05f);
            TrainingDummy current = SniperShot(30f, false);
            if (!current) yield break;
            var hit = new HashSet<TrainingDummy> { current };
            for (int i = 0; i < 3 && current; i++)
            {
                yield return new WaitForSeconds(0.08f);
                var next = Nearest(current.transform.position, 8f * S, 1, hit);
                if (next.Count == 0) break;
                GeoFx.Line(new[] { current.Center, next[0].Center }, 0.05f * S, k_Purple, 0f, 0.3f);
                Fx.Play("Sparks explode pink", current.Center, Quaternion.identity, 0.25f * S, 0.1f, false);
                DamageTagged(next[0], 34f, true, "도탄!", "FX_Purple_Hit_02");
                hit.Add(next[0]);
                current = next[0];
            }
        }

        // E: 레이저 드론 — 6초 동안 내 둘레를 돌며 0.5초마다 가장 가까운 적에게 레이저
        IEnumerator SniperDrone()
        {
            Pose(new Vector3(0.1f, 0.6f, 0.5f), Vector3.up, 0.9f);
            yield return new WaitForSeconds(0.1f);
            StartCoroutine(Drone(6f));
        }

        IEnumerator Drone(float life)
        {
            GameObject drone = Ball(0.25f * S, k_Purple);
            GameObject shield = Fx.Play("Magic shield pink", drone.transform.position, Quaternion.identity, 0.18f * S, life, false, drone.transform);
            float next = 0.3f;
            for (float t = 0f; t < life && this; t += Time.deltaTime)
            {
                drone.transform.position = transform.position + new Vector3(Mathf.Cos(t * 2f) * 1.2f, 2f + 0.15f * Mathf.Sin(t * 5f), Mathf.Sin(t * 2f) * 1.2f) * S;
                if (t >= next)
                {
                    next += 0.5f;
                    var target = Nearest(transform.position, 11f * S, 1);
                    if (target.Count > 0)
                    {
                        GeoFx.Line(new[] { drone.transform.position, target[0].Center }, 0.05f * S, Color.Lerp(k_Purple, Color.white, 0.4f), 0f, 0.2f);
                        DamageTagged(target[0], 14f, false, null, "FX_Purple_Hit_02");
                    }
                }
                yield return null;
            }
            if (shield) shield.transform.SetParent(null, true);
            Fx.Play("Sparks explode pink", drone.transform.position, Quaternion.identity, 0.3f * S, 0.1f, false);
            Destroy(drone);
        }

        // X: 뒤로 뛰며 집중 100%로 한 발
        IEnumerator SniperBackstep()
        {
            Fx.Play("Sparks explode pink", transform.position, Quaternion.identity, 0.3f * S, 0.2f, false);
            yield return Aim(0f, 0f);
            BodyLean = -15f;
            yield return Leap(-3.5f * S, 1.2f * S, 0.35f, null);
            BodyLean = 0f;
            Crouch = 0.6f;
            SniperShot(55f, true, 0.1f);
            yield return new WaitForSeconds(0.2f);
            Crouch = 0f;
        }

        // C: 궤도 폭격 — 조준한 땅에 표적 → 큰 빛기둥 + 정육각형 꼭짓점에 작은 기둥 여섯
        IEnumerator SniperOrbital()
        {
            // 컷신 직후엔 카메라가 아직 컷신 자리에 있어서 조준선이 엉뚱한 곳을 가리킨다 →
            // 앞쪽 25 안에서 적이 가장 많이 모인 곳(둘레 3 안의 적 수가 최대)을 자동으로 노린다. 없으면 앞 8.
            Vector3 target = OnGround(new Vector2(8f * S, 0f));
            int best = 0;
            foreach (var d in InFront(25f * S, 75f, 20))
            {
                int n = Nearest(d.transform.position, 3f * S, 99).Count;
                if (n > best) { best = n; target = d.transform.position; }
            }
            target.y = GeoFx.GroundY(target, S);
            Pose(new Vector3(0.1f, 0.9f, 0.3f), Vector3.up, 0.95f);
            BodyLean = -12f;
            Fx.Play("Laser AOE", target, Quaternion.identity, 0.5f * S, 1f, false);
            yield return new WaitForSeconds(0.7f);

            Fx.Play("FX_LightPillar", target, Quaternion.identity, 1.2f * S, 0.5f, false);
            Fx.Play("Explosion", target + Vector3.up * 0.4f * S, Quaternion.identity, 0.6f * S, 0.2f, false);
            m_Player.Shake(0.25f, 0.5f);
            foreach (var d in Nearest(target, 3f * S, 99))
            {
                Damage(d, 110f, true, "FX_Purple_Hit_02");
                d.Launch(Vector3.up * 6f * S, 16f * S);
            }
            for (int k = 0; k < 6; k++)
            {
                yield return new WaitForSeconds(0.08f);
                float a = k * 60f * Mathf.Deg2Rad;
                Vector3 p = target + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 3f * S;
                p.y = GeoFx.GroundY(p, S);
                Fx.Play("FX_LightPillar", p, Quaternion.identity, 0.5f * S, 0.3f, false);
                foreach (var d in Nearest(p, 1.5f * S, 99)) Damage(d, 45f, true, null);
            }
            BodyLean = 0f;
        }
    }
}
