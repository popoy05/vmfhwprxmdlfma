using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.FPS.NewMode
{
    // 3. 번개 단검 — 기믹 "연쇄 번개 · 표식"
    //  4번 맞힐 때마다 번개가 근처 적 3명에게 튄다.
    //  [표식] Q 섬광 돌진·X 오성 섬격·평타 3단이 표식(분홍 불꽃)을 남긴다 (8초).
    //  [폭발] E 뇌전 폭발이 표식을 터뜨려 ×3 + 연쇄 번개, C 천둥 난무는 표식 대상에게 ×2.
    //  → Q로 줄을 그어 표식 → E로 한꺼번에 터뜨리는 것이 기본 연계.
    //  평타  번개 찌르기 4연속
    //  Q     섬광 돌진           앞으로 순간 돌진, 지나간 적에게 표식
    //  E     뇌전 폭발           표식이 있는 적 모두에게 하늘에서 벼락 (없으면 앞쪽 가까운 3명)
    //  X     오성 섬격           가까운 적 다섯 사이를 별 모양 {5/2} 순서로 순간이동하며 벤다
    //  C     천둥 난무           주변 모든 적에게 벼락 3연타 + 큰 벼락 (0.4초에 몰아서)
    public partial class NewModeCombat
    {
        static readonly Color k_Pink = new Color(1f, 0.45f, 0.85f);
        int m_KnifeHits;

        WeaponDef KnifeWeapon() => new WeaponDef
        {
            GimmickName = "연쇄 번개",
            GimmickInfo = "Q·X·3단으로 표식 → E로 감전 폭발 ×3 · 4타마다 연쇄 번개",
            BasicRange = 3f,
            Gimmick = () =>
            {
                int marked = 0;
                foreach (var d in TrainingDummy.All) if (d.Marked) marked++;
                return $"{Bar((m_KnifeHits % 4) / 4f, 4)}  <size=80%>연쇄까지 {4 - m_KnifeHits % 4}타</size>    표식 <b>{marked}</b>  <size=80%><color=#FFFFFFAA>Q로 표식 → E로 폭발 ×3</color></size>";
            },
            HitMultiplier = d =>
            {
                m_KnifeHits++;
                if (m_KnifeHits % 4 == 0) StartCoroutine(Chain(d, 3, 28f, 6f * S));
                return 1f;
            },
            Basic = new SkillDef { Key = "좌클릭", Name = "번개 찌르기", Desc = "3단: 찌르기 → 찌르기 → X자 베기(표식). 4번 맞힐 때마다 연쇄 번개", Formula = "y = 0,  0 ≤ x ≤ L", Run = KnifeBasic },
            Skills = new[]
            {
                new SkillDef { Key = "Q", Name = "섬광 돌진", Desc = "앞으로 순간 돌진, 지나간 적에게 번개 표식", Formula = "P(t) = P₀ + t·v", Cooldown = 3f, Run = KnifeDash,
                    Detail = () => $"P(t) = P₀ + t·v,   0 ≤ t ≤ {N(6.5f * S)}" },
                new SkillDef { Key = "E", Name = "뇌전 폭발", Desc = "표식이 있는 적 모두에게 벼락 — 감전 폭발 ×3 + 연쇄 (없으면 앞쪽 3명)", Formula = "벼락 y = h − ½gt²", Cooldown = 6f, Run = KnifeDetonate,
                    Detail = () => "표식 n개 → 벼락 n번" },
                new SkillDef { Key = "X", Name = "오성 섬격", Desc = "가까운 적 다섯 사이를 별 모양으로 순간이동하며 벰", Formula = "{5/2} 별 다각형", Cooldown = 8f, Run = KnifeStar,
                    Detail = () => "순서: 0 → 2 → 4 → 1 → 3 → 0  (2칸씩 건너뛰기)" },
                new SkillDef { Key = "C", Name = "천둥 난무", Desc = "주변 모든 적에게 벼락 3연타 + 큰 벼락 (0.4초) — 표식 대상 ×2", Formula = "tₙ = 0.08n", Cooldown = 15f, Run = KnifeStorm,
                    Detail = () => $"벼락 3연타 (0.08초 간격),   반경 {N(10f * S)}" },
            },
        };

        // 평타 3단: 1단 찌르기 → 2단 반대 손 찌르기 → 3단 X자 베기 (앞으로 파고들며)
        IEnumerator KnifeBasic() => Combo(3, step =>
        {
            float L = 3f * S;
            if (step < 2)
            {
                float side = step == 1 ? -0.18f : 0.18f;
                BodyTwist = step == 1 ? 15f : -15f;
                System.Func<float, Vector3> stab = u => Vector3.Slerp(new Vector3(side + 0.35f, 0.05f, -0.25f), new Vector3(side * 0.3f, -0.02f, 1f), Mathf.Clamp01(u)).normalized;
                return Swing(stab, Vector3.up, 0.04f, 0.07f, 0.06f, 0.6f, () =>
                    HitWhere(p => GeoFunctions.DistanceToSegment(p, Vector2.zero, new Vector2(L, 0f)) <= 0.65f * S, 12f, false, "Electro hit"), 1f, 0.3f);
            }
            BodyTwist = 0f;
            return Swing(HorizontalArc(-75f, 75f, 0.15f), Vector3.up, 0.06f, 0.1f, 0.12f, 0.5f, () =>
            {
                Fx.Play("Electro slash", World(new Vector2(L * 0.6f, 0f), 1f * S), Facing(45f), 0.25f * S, 0.1f, false);
                Fx.Play("Electro slash", World(new Vector2(L * 0.6f, 0f), 1f * S), Facing(-45f), 0.25f * S, 0.1f, false);
                foreach (var d in Targets())
                {
                    if (!GeoFunctions.InSector(LocalNear(d), L * 1.2f, 75f)) continue;
                    Damage(d, 26f, true, "Electro hit");
                    d.Mark(8f);
                }
            }, 1f, 0.8f);
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
            const float time = 0.17f;
            float done = 0f;
            Vector3 fwd = m_Frame * Vector3.forward;
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                // x(u) = L·(1 − (1 − u)³): 처음에 가장 빠르고 끝에서 부드럽게 멈춤
                float u = Mathf.Clamp01(t / time);
                float want = L * (1f - Mathf.Pow(1f - u, 3f));
                cc.Move(fwd * (want - done) + Vector3.down * 0.05f * S);
                done = want;
                BodyLean = Mathf.Lerp(28f, 8f, u);
                yield return null;
            }
            cc.Move(fwd * (L - done));
            OverridesMotion = false;
            m_Player.AddMomentum(fwd * 2f * S);
            Bolt(start + Vector3.up * 0.9f * S, transform.position + Vector3.up * 0.9f * S, k_Pink, 0.05f, 0.3f);
            Vector2 end = Local(transform.position);
            foreach (var d in Targets())
            {
                if (GeoFunctions.DistanceToSegment(Local(d.transform.position), Vector2.zero, end) > 0.9f * S) continue;
                Damage(d, 46f, true, "Electro hit");
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
                if (marked)
                {
                    DamageBonus(d, 40f, true, "Electro hit", 3f, "감전 폭발 ×3!");
                    Fx.Play("Sparks explode pink", d.Center, Quaternion.identity, 0.6f * S, 0.2f, false);
                    StartCoroutine(Chain(d, 2, 20f, 5f * S));
                }
                else Damage(d, 46f, true, "Electro hit");
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
                yield return DashTo(to, 0.05f);
                FaceTo(d.transform.position);
                SetFrame();
                Bolt(prev, transform.position + Vector3.up * 0.9f * S, k_Pink, 0.05f, 0.5f);
                prev = transform.position + Vector3.up * 0.9f * S;
                Pose(new Vector3(-0.6f, 0f, 0.8f), Vector3.up);
                yield return Swing(HorizontalArc(-70f, 70f), Vector3.up, 0.02f, 0.07f, 0.02f, 0.5f, () =>
                {
                    Damage(d, 44f, true, "Electro hit");
                    d.Mark(8f);
                }, 1f, 0f);
            }
            yield return DashTo(home, 0.08f);
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
            GameObject aura = Fx.Play("Lightning aura", transform.position, Quaternion.identity, 0.7f * S, 0.5f, false, transform);
            var targets = Nearest(transform.position, R, 20);
            // 딜 압축: 주변 모든 적에게 동시에 벼락 3연타 (0.08초 간격) — 예전엔 한 명씩 1.2초 동안 돌아가며 때림
            for (int wave = 0; wave < 3; wave++)
            {
                foreach (var d in targets)
                {
                    if (!d || !d.Alive) continue;
                    Vector3 sky = d.transform.position + Vector3.up * 8f * S + Random.insideUnitSphere * 1.5f * S;
                    Bolt(sky, d.transform.position, Color.Lerp(k_Pink, Color.white, 0.4f + 0.2f * wave), 0.1f + 0.03f * wave, 0.2f);
                    if (wave == 0) LightFlash.Spawn(d.Center + Vector3.up * 0.8f * S, k_Pink, 5f, 3f * S, 0.2f);
                    if (wave == 2) Fx.Play("Electro hit", d.Center, Quaternion.identity, 0.55f * S, 0.1f, false);
                    if (d.Marked) DamageBonus(d, 90f, false, null, 2f, "표식 ×2");
                    else Damage(d, 90f, false, null);
                }
                if (targets.Count == 0)
                    for (int k = 0; k < 4; k++)
                    {
                        Vector2 r = Random.insideUnitCircle * R * 0.6f;
                        Vector3 w = transform.position + new Vector3(r.x, 0f, r.y);
                        Bolt(w + Vector3.up * 8f * S, w, Color.Lerp(k_Pink, Color.white, 0.5f), 0.1f, 0.2f);
                    }
                PostFx.Pulse(0.4f, k_Pink);
                m_Player.Shake(0.06f + 0.03f * wave, 0.1f);
                yield return new WaitForSeconds(0.08f);
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
            UltBurst(transform.position, 6f * S, "Lightning aura", 0.8f);
            Fx.Play("Plexus AoE", transform.position, Quaternion.identity, 0.5f * S, 0.3f, false);
            m_Player.Shake(0.28f, 0.5f);
            foreach (var d in targets) if (d && d.Alive) Damage(d, 130f, true, "Electro hit"); // 마무리 큰 벼락: 범위 안 모두
            if (aura) aura.transform.SetParent(null, true);
            yield return new WaitForSeconds(0.1f);
            Crouch = 0f;
            BodyLean = 0f;
        }
    }
}
