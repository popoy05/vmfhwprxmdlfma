using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.FPS.NewMode
{
    // 3. 번개 단검 — 기믹 "연쇄 번개 · 표식"
    //  4번 맞힐 때마다 번개가 근처 적 3명에게 튄다. 돌진(Q)·오성 섬격(X)은 적에게 표식을 남기고, E로 표식을 한꺼번에 터뜨린다.
    //  평타  번개 찌르기 4연속
    //  Q     섬광 돌진           앞으로 순간 돌진, 지나간 적에게 표식
    //  E     뇌전 폭발           표식이 있는 적 모두에게 하늘에서 벼락 (없으면 앞쪽 가까운 3명)
    //  X     오성 섬격           가까운 적 다섯 사이를 별 모양 {5/2} 순서로 순간이동하며 벤다
    //  C     천둥 난무           2.4초 동안 주변 적에게 벼락이 쉬지 않고 떨어지고, 마지막에 큰 벼락
    public partial class NewModeCombat
    {
        static readonly Color k_Pink = new Color(1f, 0.45f, 0.85f);
        int m_KnifeHits;

        WeaponDef KnifeWeapon() => new WeaponDef
        {
            GimmickName = "연쇄 번개",
            GimmickInfo = "4번 맞힐 때마다 번개가 튄다 · Q/X 표식 → E로 폭발",
            Gimmick = () =>
            {
                int marked = 0;
                foreach (var d in TrainingDummy.All) if (d.Marked) marked++;
                return $"{Bar((m_KnifeHits % 4) / 4f, 4)}  <size=80%>연쇄까지 {4 - m_KnifeHits % 4}타</size>    표식 <b>{marked}</b>";
            },
            HitMultiplier = d =>
            {
                m_KnifeHits++;
                if (m_KnifeHits % 4 == 0) StartCoroutine(Chain(d, 3, 22f, 6f * S));
                return 1f;
            },
            Basic = new SkillDef { Key = "좌클릭", Name = "번개 찌르기", Formula = "y = 0,  0 ≤ x ≤ L", Run = KnifeBasic },
            Skills = new[]
            {
                new SkillDef { Key = "Q", Name = "섬광 돌진", Formula = "P(t) = P₀ + t·v", Cooldown = 3f, Run = KnifeDash,
                    Detail = () => $"P(t) = P₀ + t·v,   0 ≤ t ≤ {N(6.5f * S)}" },
                new SkillDef { Key = "E", Name = "뇌전 폭발", Formula = "벼락 y = h − ½gt²", Cooldown = 6f, Run = KnifeDetonate,
                    Detail = () => "표식 n개 → 벼락 n번" },
                new SkillDef { Key = "X", Name = "오성 섬격", Formula = "{5/2} 별 다각형", Cooldown = 8f, Run = KnifeStar,
                    Detail = () => "순서: 0 → 2 → 4 → 1 → 3 → 0  (2칸씩 건너뛰기)" },
                new SkillDef { Key = "C", Name = "천둥 난무", Formula = "tₙ = 0.1n", Cooldown = 15f, Run = KnifeStorm,
                    Detail = () => $"0.1초마다 벼락,   반경 {N(10f * S)}" },
            },
        };

        IEnumerator KnifeBasic() => Combo(4, step =>
        {
            float L = 1.9f * S;
            float side = step % 2 == 1 ? -0.18f : 0.18f;
            BodyTwist = step % 2 == 1 ? 15f : -15f;
            System.Func<float, Vector3> stab = u => Vector3.Slerp(new Vector3(side + 0.35f, 0.05f, -0.25f), new Vector3(side * 0.3f, -0.02f, 1f), Mathf.Clamp01(u)).normalized;
            return Swing(stab, Vector3.up, 0.04f, 0.07f, 0.05f, 0.6f, () =>
            {
                Fx.Play("Electro slash", World(new Vector2(L * 0.6f, 0f), 1f * S), Facing(90f), 0.18f * S, 0.1f, false);
                HitWhere(p => GeoFunctions.DistanceToSegment(p, Vector2.zero, new Vector2(L, 0f)) <= 0.6f * S, step == 3 ? 24f : 12f, step == 3, "Electro hit");
            }, 1f, 0.25f);
        });

        // Q: 앞으로 순간 돌진 — 지나간 선분 위의 적을 베고 표식
        IEnumerator KnifeDash()
        {
            float L = 6.5f * S;
            Fx.Play("Teleport", transform.position, Quaternion.identity, 0.4f * S, 0.15f, false);
            Pose(new Vector3(0.05f, -0.05f, 1f), Vector3.up);
            BodyLean = 20f;
            TrailOn = true;
            OverridesMotion = true;
            CharacterController cc = m_Player.Controller;
            Vector3 start = transform.position;
            for (float t = 0f; t < 0.14f; t += Time.deltaTime)
            {
                cc.Move(World(new Vector2(t / 0.14f * L, 0f)) - transform.position + Vector3.down * 0.05f * S);
                yield return null;
            }
            OverridesMotion = false;
            Bolt(start + Vector3.up * 0.9f * S, transform.position + Vector3.up * 0.9f * S, k_Pink, 0.05f, 0.3f);
            Vector2 end = Local(transform.position);
            foreach (var d in Targets())
            {
                if (GeoFunctions.DistanceToSegment(Local(d.transform.position), Vector2.zero, end) > 0.9f * S) continue;
                Damage(d, 36f, true, "Electro hit");
                d.Mark(8f);
            }
            BodyLean = 0f;
            yield return new WaitForSeconds(0.1f);
        }

        // E: 표식이 있는 적에게 하늘에서 벼락
        IEnumerator KnifeDetonate()
        {
            Pose(new Vector3(0.1f, 1f, 0.1f), Vector3.forward, 1f); // 단검을 하늘로
            BodyLean = -10f;
            var targets = new List<TrainingDummy>();
            foreach (var d in Targets()) if (d.Marked) targets.Add(d);
            if (targets.Count == 0) targets = InFront(9f * S, 70f, 3);
            Fx.Play("Lightning aura", transform.position, Quaternion.identity, 0.5f * S, 0.4f, false, transform);
            yield return new WaitForSeconds(0.15f);
            foreach (var d in targets)
            {
                if (!d) continue;
                Vector3 sky = d.Center + Vector3.up * 7f * S + Random.insideUnitSphere * 0.8f * S;
                Bolt(sky, d.transform.position, Color.Lerp(k_Pink, Color.white, 0.5f), 0.12f, 0.3f);
                Fx.Play("Electro slash", d.Center, Quaternion.LookRotation(Vector3.down), 0.4f * S, 0.15f, false);
                bool marked = d.Marked;
                d.ClearMark();
                m_HitTag = null;
                Damage(d, marked ? 60f : 36f, true, "Electro hit");
                if (marked) StartCoroutine(Chain(d, 2, 20f, 5f * S));
                m_Player.Shake(0.06f, 0.12f);
                yield return new WaitForSeconds(0.07f);
            }
            BodyLean = 0f;
        }

        // X: 가까운 적 다섯을 각도순으로 놓고 2칸씩 건너뛰며(별 {5/2}) 순간이동해 벤다
        IEnumerator KnifeStar()
        {
            var targets = Nearest(transform.position, 9f * S, 5);
            if (targets.Count == 0) targets = new List<TrainingDummy>();
            Vector3 home = transform.position;

            // 각도순 정렬 → {5/2} 순서 0,2,4,1,3
            Vector3 centroid = Vector3.zero;
            foreach (var d in targets) centroid += d.transform.position;
            if (targets.Count > 0) centroid /= targets.Count;
            targets.Sort((a, b) => Mathf.Atan2(a.transform.position.z - centroid.z, a.transform.position.x - centroid.x)
                .CompareTo(Mathf.Atan2(b.transform.position.z - centroid.z, b.transform.position.x - centroid.x)));
            var order = new List<TrainingDummy>();
            int n = targets.Count;
            for (int i = 0, k = 0; i < n; i++, k = (k + 2) % Mathf.Max(1, n)) order.Add(targets[k]);
            if (n % 2 == 0 && n > 0) { order.Clear(); foreach (var d in targets) order.Add(d); } // 짝수면 그냥 한 바퀴

            OverridesMotion = true;
            TrailOn = true;
            Vector3 prev = home + Vector3.up * 0.9f * S;
            foreach (var d in order)
            {
                if (!d || !d.Alive) continue;
                Vector3 side = Quaternion.Euler(0f, 90f, 0f) * (d.transform.position - centroid).normalized;
                Vector3 to = d.transform.position + (d.transform.position - centroid).normalized * 0.9f * S + side * 0.3f * S;
                Blink(to);
                FaceTo(d.transform.position);
                SetFrame();
                Bolt(prev, transform.position + Vector3.up * 0.9f * S, k_Pink, 0.05f, 0.5f);
                prev = transform.position + Vector3.up * 0.9f * S;
                Pose(new Vector3(-0.6f, 0f, 0.8f), Vector3.up);
                yield return Swing(HorizontalArc(-70f, 70f), Vector3.up, 0.02f, 0.07f, 0.02f, 0.5f, () =>
                {
                    Damage(d, 34f, true, "Electro hit");
                    d.Mark(8f);
                }, 1f, 0f);
            }
            Blink(home);
            Bolt(prev, home + Vector3.up * 0.9f * S, k_Pink, 0.05f, 0.5f);
            transform.rotation = m_Frame;
            OverridesMotion = false;
            Fx.Play("Teleport", home, Quaternion.identity, 0.4f * S, 0.1f, false);
        }

        // C: 천둥 폭풍 — 0.1초마다 주변 적에게 차례로 벼락, 끝에 내 자리에 큰 벼락
        IEnumerator KnifeStorm()
        {
            float R = 10f * S;
            Pose(new Vector3(0.1f, 1f, 0.15f), Vector3.forward, 1f);
            BodyLean = -12f;
            GameObject aura = Fx.Play("Lightning aura", transform.position, Quaternion.identity, 0.7f * S, 2.4f, false, transform);
            var targets = Nearest(transform.position, R, 20);
            int i = 0;
            for (float t = 0f; t < 2.4f; t += 0.1f)
            {
                if (targets.Count > 0)
                {
                    TrainingDummy d = targets[i++ % targets.Count];
                    if (d && d.Alive)
                    {
                        Vector3 sky = d.transform.position + Vector3.up * 8f * S + Random.insideUnitSphere * 1.5f * S;
                        Bolt(sky, d.transform.position, Color.Lerp(k_Pink, Color.white, 0.5f), 0.1f, 0.2f);
                        if (i % 3 == 0) Fx.Play("Electro hit", d.Center, Quaternion.identity, 0.5f * S, 0.1f, false);
                        Damage(d, 14f, false, null);
                    }
                }
                else
                {
                    Vector2 r = Random.insideUnitCircle * R;
                    Vector3 w = transform.position + new Vector3(r.x, 0f, r.y);
                    Bolt(w + Vector3.up * 8f * S, w, Color.Lerp(k_Pink, Color.white, 0.5f), 0.1f, 0.2f);
                }
                if (i % 4 == 0) m_Player.Shake(0.04f, 0.1f);
                yield return new WaitForSeconds(0.1f);
            }

            Pose(new Vector3(0.05f, -0.8f, 0.6f), Vector3.right, 1f);
            Crouch = 1f;
            BodyLean = 25f;
            for (int k = 0; k < 6; k++)
            {
                float a = k * 60f * Mathf.Deg2Rad;
                Vector3 p = transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 2.2f * S;
                Bolt(p + Vector3.up * 9f * S, p, Color.white, 0.14f, 0.35f);
            }
            Fx.Play("Electro slash", transform.position + Vector3.up * 0.5f * S, Quaternion.LookRotation(Vector3.down), 0.9f * S, 0.25f, false);
            Fx.Play("Plexus AoE", transform.position, Quaternion.identity, 0.5f * S, 0.3f, false);
            m_Player.Shake(0.28f, 0.5f);
            foreach (var d in Nearest(transform.position, 4.5f * S, 99)) Damage(d, 80f, true, "Electro hit");
            if (aura) aura.transform.SetParent(null, true);
            yield return new WaitForSeconds(0.3f);
            Crouch = 0f;
            BodyLean = 0f;
        }
    }
}
