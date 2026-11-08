using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.FPS.NewMode
{
    // 1. 서리검 — 기믹 "서리 낙인 → 산산조각" (연계)
    //  [빙결] 평타는 서리 1단계(3단은 2단계), Q 서리 물결은 맞은 적 모두 즉시 빙결, E 결계는 0.5초마다 서리 1단계.
    //  [깨기] X 빙하 낙하·C 절대영도는 빙결된 적을 산산조각 ×3, 평타 3단은 ×2.
    //  → Q/E로 여럿을 얼리고 X로 한꺼번에 깨는 것이 기본 연계.
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
        float m_ShatterMul; // 0이면 깨지 않음 (얼음만 쌓음)

        WeaponDef SwordWeapon() => new WeaponDef
        {
            GimmickName = "서리 낙인",
            GimmickInfo = "Q·E로 빙결 → X·C로 산산조각 ×3 (평타 3단 ×2)",
            BasicRange = 3.2f,
            Gimmick = () =>
            {
                int frozen = 0;
                foreach (var d in TrainingDummy.All) if (d.Alive && d.Frozen) frozen++;
                return $"빙결된 적 <b>{frozen}</b>   <size=80%><color=#FFFFFFAA>Q·E로 얼리고 → X로 산산조각 ×3</color></size>";
            },
            HitMultiplier = SwordHit,
            Basic = new SkillDef { Key = "좌클릭", Name = "서리 베기", Desc = "3단 베기 (사거리 길어짐). 서리 1단계씩, 3단은 2단계 + 빙결된 적 산산조각 ×2", Formula = "r ≤ R,  |θ| ≤ 60°", Run = SwordBasic },
            Skills = new[]
            {
                new SkillDef { Key = "Q", Name = "서리 물결", Desc = "사인파 얼음 파도 (관통) — 맞은 적 모두 즉시 빙결", Formula = "y = A sin(2πx/λ)", Cooldown = 4f, Run = SwordWave,
                    Detail = () => $"y = {N(0.8f * S)}·sin(2πx / {N(3f * S)})" },
                new SkillDef { Key = "E", Name = "빙결 결계", Desc = "4초 서리 지대 — 0.5초마다 서리, 안에 있으면 1.5초 만에 빙결", Formula = "x² + y² ≤ r²", Cooldown = 9f, Run = SwordField,
                    Detail = () => $"x² + y² ≤ {N(2.8f * S)}²,   4초" },
                new SkillDef { Key = "X", Name = "빙하 낙하", Desc = "뛰어올라 내려찍기 — 빙결된 적은 산산조각 ×3, 나머지는 빙결", Formula = "y = −a(x − h)² + k", Cooldown = 7f, Run = SwordLeap,
                    Detail = () => $"y = −{4f * 2.4f / (36f * S):0.###}(x − {N(3f * S)})² + {N(2.4f * S)}" },
                new SkillDef { Key = "C", Name = "절대영도", Desc = "주변을 모두 얼리고 순간이동하며 하나씩 산산조각 ×3", Formula = "T → 0 K", Cooldown = 16f, Run = SwordAbsoluteZero,
                    Detail = () => $"x² + y² ≤ {N(10f * S)}²  전부 빙결" },
            },
        };

        float SwordHit(TrainingDummy d)
        {
            if (d.Frozen)
            {
                if (m_ShatterMul <= 0f) return 1f; // 깨지 않는 공격은 얼음을 유지
                d.Shatter();
                m_HitTag = m_ShatterMul >= 3f ? "산산조각 ×3!" : "산산조각!";
                Fx.Play("Crystals front attack", d.transform.position, Quaternion.LookRotation(d.transform.position - transform.position), 0.55f * S, 0.2f, false);
                Fx.Play("Sparks explode blue", d.Center, Quaternion.identity, 0.5f * S, 0.2f, false);
                if (m_ShatterMul >= 3f) { PostFx.Pulse(0.6f, k_Ice); RingWave.Spawn(d.transform.position + Vector3.up * 0.05f * S, 1.6f * S, new Color(0.7f, 0.95f, 1f, 0.85f), 0.35f); }
                return m_ShatterMul;
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
            float R = 3.2f * S;
            if (step < 2)
            {
                float from = step == 0 ? 85f : -80f, to = step == 0 ? -80f : 85f;
                Draw(u => GeoFunctions.Arc(R * 0.85f, Mathf.Lerp(from, to, u)), 40, 0.05f, k_Ice, 0.14f, 0.45f);
                return Swing(HorizontalArc(from, to), Vector3.up, 0.09f, 0.13f, 0.12f, 0.5f, () =>
                {
                    Fx.Play("Snow slash", World(new Vector2(R * 0.45f, 0f), 0.9f * S), Facing() * Quaternion.Euler(0f, 0f, step == 0 ? 0f : 180f), 0.3f * S, 0.15f, false);
                    HitWhere(p => GeoFunctions.InSector(p, R, 70f), 25f, false, "Snow hit");
                }, 1f, 0.3f);
            }
            return Swing(VerticalArc(-25f, 128f), Vector3.right, 0.16f, 0.14f, 0.2f, 0.65f, () =>
            {
                Fx.Play("Snow AOE", OnGround(new Vector2(R * 0.7f, 0f)), Quaternion.identity, 0.35f * S, 0.2f, false);
                m_FrostPerHit = 2;
                m_ShatterMul = 2f;
                HitWhere(p => GeoFunctions.InSector(p, R * 1.2f, 35f), 50f, true, "Snow hit");
                m_ShatterMul = 0f;
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
            yield return Fly(path, travel, 0.9f * S, (d, p) => { m_FrostPerHit = 3; Damage(d, 52f, true, "Snow hit"); m_FrostPerHit = 1; }, "Charge slash blue", 0.4f * S, true);
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
                HitNear(center, r, 15f, false, null);
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
            // 빙결된 적은 산산조각 ×3, 나머지는 즉시 빙결
            m_FrostPerHit = 3;
            m_ShatterMul = 3f;
            HitNear(land, 3.2f * S, 70f, true, null);
            m_ShatterMul = 0f;
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
            UltBurst(home, 10f * S, "Freeze circle", 2.2f);
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
                yield return DashTo(from, 0.07f);
                FaceTo(d.transform.position);
                SetFrame();
                float a = i % 2 == 0 ? 80f : -80f;
                yield return Swing(HorizontalArc(a, -a), Vector3.up, 0.03f, 0.08f, 0.04f, 0.6f, () => { m_ShatterMul = 3f; Damage(d, 60f, true, "Snow hit"); m_ShatterMul = 0f; UltSpark(d.Center); Fx.Play("Crystals front attack", d.transform.position, Quaternion.LookRotation(d.transform.position - transform.position), 0.5f * S, 0.2f, false); }, 1f, 0f);
                i++;
            }
            yield return DashTo(home, 0.1f);
            OverridesMotion = false;
            UltBurst(home, 5f * S, "Snow AOE", 1.4f);
            yield return new WaitForSeconds(0.2f);
        }
    }
}
