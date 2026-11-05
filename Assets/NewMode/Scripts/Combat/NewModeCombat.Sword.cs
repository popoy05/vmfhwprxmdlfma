using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.FPS.NewMode
{
    // 1. 서리검 — 기믹 "서리 낙인"
    //  맞힐 때마다 서리가 1단계 쌓이고, 3단계가 되면 얼어붙는다. 얼어붙은 적을 치면 산산조각 (피해 ×2.5).
    //  평타  서리 베기 3연격     (가로 → 가로 → 내려찍기)
    //  Q     서리 물결           앞으로 퍼져 나가는 얼음 파도 (사인파 궤적)
    //  E     빙결 결계           땅에 칼을 꽂아 4초 동안 서리 지대를 만든다
    //  X     빙하 낙하           포물선 도약 후 내려찍기 — 맞은 적은 즉시 빙결
    //  C     절대영도            주변을 모두 얼린 뒤 얼음 사이를 순간이동하며 하나씩 깨부순다
    public partial class NewModeCombat
    {
        static readonly Color k_Ice = new Color(0.45f, 0.85f, 1f);
        static readonly Color k_Violet = new Color(0.7f, 0.5f, 1f);
        static readonly Color k_Gold = new Color(1f, 0.82f, 0.35f);

        int m_FrostPerHit = 1;

        WeaponDef SwordWeapon() => new WeaponDef
        {
            GimmickName = "서리 낙인",
            GimmickInfo = "3번 맞히면 빙결 · 빙결된 적을 치면 산산조각 ×2.5",
            Gimmick = () =>
            {
                int frozen = 0;
                foreach (var d in TrainingDummy.All) if (d.Frozen) frozen++;
                return $"빙결된 적 <b>{frozen}</b>   <size=80%><color=#FFFFFFAA>3타 빙결 → 치면 산산조각 ×2.5</color></size>";
            },
            HitMultiplier = SwordHit,
            Basic = new SkillDef { Key = "좌클릭", Name = "서리 베기", Formula = "r ≤ R,  |θ| ≤ 60°", Run = SwordBasic },
            Skills = new[]
            {
                new SkillDef { Key = "Q", Name = "서리 물결", Formula = "y = A sin(2πx/λ)", Cooldown = 4f, Run = SwordWave,
                    Detail = () => $"y = {N(0.8f * S)}·sin(2πx / {N(3f * S)})" },
                new SkillDef { Key = "E", Name = "빙결 결계", Formula = "x² + y² ≤ r²", Cooldown = 9f, Run = SwordField,
                    Detail = () => $"x² + y² ≤ {N(2.8f * S)}²,   4초" },
                new SkillDef { Key = "X", Name = "빙하 낙하", Formula = "y = −a(x − h)² + k", Cooldown = 7f, Run = SwordLeap,
                    Detail = () => $"y = −{4f * 2.4f / (36f * S):0.###}(x − {N(3f * S)})² + {N(2.4f * S)}" },
                new SkillDef { Key = "C", Name = "절대영도", Formula = "T → 0 K", Cooldown = 16f, Run = SwordAbsoluteZero,
                    Detail = () => $"x² + y² ≤ {N(10f * S)}²  전부 빙결" },
            },
        };

        float SwordHit(TrainingDummy d)
        {
            if (d.Frozen)
            {
                d.Shatter();
                m_HitTag = "산산조각!";
                Fx.Play("Crystals front attack", d.transform.position, Quaternion.LookRotation(d.transform.position - transform.position), 0.45f * S, 0.2f, false);
                return 2.5f;
            }
            if (d.AddFrost(m_FrostPerHit))
            {
                m_HitTag = "빙결!";
                Fx.Play("Snow hit", d.Center, Quaternion.identity, 0.5f * S, 0.2f, false);
            }
            return 1f;
        }

        IEnumerator SwordBasic() => Combo(3, step =>
        {
            float R = 2.1f * S;
            if (step < 2)
            {
                float from = step == 0 ? 85f : -80f, to = step == 0 ? -80f : 85f;
                Draw(u => GeoFunctions.Arc(R * 0.85f, Mathf.Lerp(from, to, u)), 40, 0.05f, k_Ice, 0.14f, 0.45f);
                return Swing(HorizontalArc(from, to), Vector3.up, 0.09f, 0.13f, 0.12f, 0.5f, () =>
                {
                    Fx.Play("Snow slash", World(new Vector2(R * 0.45f, 0f), 0.9f * S), Facing() * Quaternion.Euler(0f, 0f, step == 0 ? 0f : 180f), 0.3f * S, 0.15f, false);
                    HitWhere(p => GeoFunctions.InSector(p, R, 70f), 20f, false, "Snow hit");
                }, 1f, 0.3f);
            }
            return Swing(VerticalArc(-25f, 128f), Vector3.right, 0.16f, 0.14f, 0.2f, 0.65f, () =>
            {
                Fx.Play("Snow AOE", OnGround(new Vector2(R * 0.7f, 0f)), Quaternion.identity, 0.35f * S, 0.2f, false);
                m_FrostPerHit = 2;
                HitWhere(p => GeoFunctions.InSector(p, R * 1.2f, 30f), 40f, true, "Snow hit");
                m_FrostPerHit = 1;
            }, 1f, 0.5f);
        });

        // Q: 칼을 크게 휘둘러 얼음 파도를 날린다 — 파도는 사인파를 그리며 앞으로 (관통)
        IEnumerator SwordWave()
        {
            float A = 0.8f * S, lambda = 3f * S, L = 13f * S, travel = 0.7f;
            yield return Swing(HorizontalArc(100f, -70f), Vector3.up, 0.12f, 0.12f, 0f, 1f, null, 1f, 0.4f);
            Draw(u => new Vector2(u * L, GeoFunctions.Sine(u * L, A, lambda)), 90, 0.05f, k_Ice, travel, travel + 0.5f);
            LocksMovement = false;
            SwordActive = false;
            Vector3 origin = m_Origin;
            Quaternion frame = m_Frame;
            System.Func<float, Vector3> path = t =>
            {
                float x = t / travel * L;
                Vector3 w = origin + frame * new Vector3(GeoFunctions.Sine(x, A, lambda), 0f, x);
                w.y = GeoFx.GroundY(w, S) + 0.6f * S;
                return w;
            };
            yield return Fly(path, travel, 0.9f * S, (d, p) => Damage(d, 46f, true, "Snow hit"), "Charge slash blue", 0.4f * S, true);
        }

        // E: 칼을 땅에 꽂아 서리 지대 — 0.5초마다 안에 있는 적에게 서리 + 피해
        IEnumerator SwordField()
        {
            float r = 2.8f * S;
            Pose(new Vector3(0.1f, -1f, 0.35f), Vector3.forward, 0.9f);
            Crouch = 1f;
            BodyLean = 20f;
            yield return new WaitForSeconds(0.18f);
            Vector3 center = OnGround(new Vector2(2.6f * S, 0f));
            Fx.Play("Freeze circle", center, Quaternion.identity, 1.1f * S, 4f, false);
            Fx.Play("Snow AOE", center, Quaternion.identity, 0.6f * S, 4f, false);
            m_Player.Shake(0.06f, 0.2f);
            yield return new WaitForSeconds(0.15f);
            Crouch = 0f;
            BodyLean = 0f;
            LocksMovement = false;
            SwordActive = false;
            StartCoroutine(FieldTicks(center, r, 4f));
        }

        IEnumerator FieldTicks(Vector3 center, float r, float time)
        {
            for (float t = 0f; t < time; t += 0.5f)
            {
                HitNear(center, r, 9f, false, null);
                yield return new WaitForSeconds(0.5f);
            }
        }

        IEnumerator SwordLeap()
        {
            float D = 6f * S, H = 2.4f * S, flight = 0.55f;
            Pose(new Vector3(0.1f, 1f, -0.25f), Vector3.forward, 0.95f);
            Crouch = 1f;
            yield return new WaitForSeconds(0.12f);
            Crouch = 0f;
            Fx.Play("Snow hit", transform.position, Quaternion.identity, 0.5f * S, 0.1f, false);
            yield return Leap(D, H, flight, VerticalArc(-15f, 130f));

            Vector3 land = transform.position;
            Crouch = 1f;
            Fx.Play("Sparks explode blue", land, Quaternion.identity, 0.6f * S, 0.25f, false);
            Fx.Play("Freeze circle", land, Quaternion.identity, 0.9f * S, 0.4f, false);
            m_Player.Shake(0.14f, 0.3f);
            m_FrostPerHit = 3; // 바로 빙결
            HitNear(land, 2.8f * S, 60f, true, null);
            m_FrostPerHit = 1;
            yield return new WaitForSeconds(0.3f);
            Crouch = 0f;
        }

        // C: 반경 10 안을 모두 얼린 뒤, 얼음 사이를 순간이동하며 하나씩 깨부순다
        IEnumerator SwordAbsoluteZero()
        {
            float R = 10f * S;
            Vector3 home = transform.position;
            Fx.Play("Snow AOE", home, Quaternion.identity, 2.2f * S, 0.8f, false);
            var targets = Nearest(home, R, 8);
            foreach (var d in targets) { d.AddFrost(3, 5f); }
            m_Player.Shake(0.12f, 0.5f);
            yield return new WaitForSeconds(0.45f);

            OverridesMotion = true;
            TrailOn = true;
            int i = 0;
            foreach (var d in targets)
            {
                if (!d || !d.Alive) continue;
                Vector3 from = d.transform.position + (transform.position - d.transform.position).normalized * 1.1f * S;
                Fx.Play("Teleport", transform.position, Quaternion.identity, 0.35f * S, 0.1f, false);
                Blink(from);
                FaceTo(d.transform.position);
                SetFrame();
                float a = i % 2 == 0 ? 80f : -80f;
                yield return Swing(HorizontalArc(a, -a), Vector3.up, 0.03f, 0.08f, 0.04f, 0.6f, () => Damage(d, 70f, true, "Snow hit"), 1f, 0f);
                i++;
            }
            Blink(home);
            OverridesMotion = false;
            Fx.Play("Freeze circle", home, Quaternion.identity, 1.6f * S, 0.4f, false);
            yield return new WaitForSeconds(0.2f);
        }
    }
}
