using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.FPS.NewMode
{
    // 4. 홈런 배트 — 기믹 "핀볼"
    //  맞은 적은 멀리 밀려나 미끄러지고, 다른 적과 부딪치면 둘 다 다친다. 날린 적이 떨어지면 비거리를 잰다.
    //  평타  풀스윙 2연타        (맞은 적을 밀어냄)
    //  Q     홈런                앞의 적을 포물선으로 멀리 — 떨어진 자리에 충격
    //  E     번트 볼             공을 띄워 쳐서 가까운 적 사이를 4번 튕긴다 (튈 때마다 높이 ½)
    //  X     진공 스윙           주변 적을 끌어당긴 뒤 사방으로 날려 버린다
    //  C     만루 홈런           주변 적을 모두 앞에 줄 세운 뒤 한 번에 날려 보낸다
    public partial class NewModeCombat
    {
        static readonly Color k_Yellow = new Color(1f, 0.9f, 0.35f);
        float m_BestDistance;

        WeaponDef BatWeapon() => new WeaponDef
        {
            GimmickName = "핀볼",
            GimmickInfo = "날아간 적이 다른 적과 부딪치면 둘 다 피해 · 홈런 비거리 기록",
            Gimmick = () => $"최장 비거리 <b>{m_BestDistance / Mathf.Max(0.01f, S):0.0}m</b>   <size=80%><color=#FFFFFFAA>날린 적끼리 부딪치면 추가 피해</color></size>",
            Basic = new SkillDef { Key = "좌클릭", Name = "풀스윙", Formula = "|θ| ≤ 100°", Run = BatBasic },
            Skills = new[]
            {
                new SkillDef { Key = "Q", Name = "홈런", Formula = "y = v sinθ t − ½gt²", Cooldown = 4f, Run = BatHomeRun,
                    Detail = () => $"x = {N(10f * S)}cos45°·t,   y = {N(10f * S)}sin45°·t − ½·{N(9.8f * S)}t²" },
                new SkillDef { Key = "E", Name = "번트 볼", Formula = "hₙ = H·(½)ⁿ⁻¹", Cooldown = 6f, Run = BatBuntBall,
                    Detail = () => $"hₙ = {N(2.4f * S)}·(½)ⁿ⁻¹,   n = 1…4" },
                new SkillDef { Key = "X", Name = "진공 스윙", Formula = "v(t) = v₀e^(−kt)", Cooldown = 8f, Run = BatVacuum,
                    Detail = () => "끌어당김 → v₀ = 14,  v(t) = 14e^(−3.2t)" },
                new SkillDef { Key = "C", Name = "만루 홈런", Formula = "θₖ = 30° + 6°k", Cooldown = 16f, Run = BatGrandSlam,
                    Detail = () => "k번째 적의 발사각 θₖ = 30° + 6°k" },
            },
        };

        IEnumerator BatBasic() => Combo(2, step =>
        {
            float R = 2.3f * S;
            float from = step == 0 ? 115f : -115f;
            return Swing(HorizontalArc(from, -from, -0.05f), Vector3.up, 0.14f, 0.17f, 0.16f, 0.5f, () =>
            {
                foreach (var d in Targets())
                {
                    if (!GeoFunctions.InSector(Local(d.transform.position), R, 100f)) continue;
                    Damage(d, 24f, step == 1, "Star hit");
                    // 휘두른 방향(옆) + 바깥쪽으로 밀어냄
                    Vector3 side = transform.right * (step == 0 ? -1f : 1f);
                    Vector3 away = (d.transform.position - transform.position).normalized;
                    d.Knock((away * 0.6f + side * 0.6f).normalized * (step == 1 ? 11f : 7f) * S);
                }
            }, 1f, 0.4f);
        });

        // Q: 앞의 적을 포물선으로 날린다 — 떨어진 자리에 충격 + 비거리
        IEnumerator BatHomeRun()
        {
            float v = 10f * S, angle = 45f, g = 9.8f * S;
            BodyTwist = 30f; // 크게 비틀었다가
            yield return Swing(HorizontalArc(130f, -70f, 0.1f), Vector3.up, 0.3f, 0.15f, 0.25f, 0.55f, () =>
            {
                BodyTwist = -30f;
                Vector3 forward = m_Frame * Vector3.forward;
                Vector3 velocity = forward * v * Mathf.Cos(angle * Mathf.Deg2Rad) + Vector3.up * v * Mathf.Sin(angle * Mathf.Deg2Rad);
                m_Player.Shake(0.12f, 0.2f);
                foreach (var d in InFront(2.6f * S, 55f, 3))
                {
                    Damage(d, 55f, true, "Holy hit");
                    LaunchHomeRun(d, velocity, g);
                }
            }, 1.05f, 0.3f);
        }

        void LaunchHomeRun(TrainingDummy d, Vector3 velocity, float g)
        {
            Vector3 from = d.transform.position;
            System.Action<TrainingDummy> landed = null;
            landed = t =>
            {
                t.Landed -= landed;
                float dist = Vector3.Distance(new Vector3(from.x, 0f, from.z), new Vector3(t.transform.position.x, 0f, t.transform.position.z));
                bool record = dist > m_BestDistance;
                if (record) m_BestDistance = dist;
                GeoFx.Popup(t.Center + Vector3.up * 1.2f * S, $"<size=60%>{(record ? "신기록!" : "비거리")}</size>\n{dist / Mathf.Max(0.01f, S):0.0}m", k_Yellow, S, 1.6f);
                Fx.Play("Star hit", t.transform.position, Quaternion.identity, 0.6f * S, 0.15f, false);
                foreach (var o in Nearest(t.transform.position, 2f * S, 99, new[] { t }))
                {
                    DamageTagged(o, 30f, true, "착지 충격!");
                    o.Knock((o.transform.position - t.transform.position).normalized * 6f * S);
                }
            };
            d.Landed += landed;
            d.Launch(velocity, g);
        }

        // E: 공을 띄워 쳐서 가까운 적 사이를 튕긴다 — 튈 때마다 높이가 ½
        IEnumerator BatBuntBall()
        {
            GameObject ball = Ball(0.22f * S, Color.white);
            Vector3 toss = transform.position + transform.forward * 0.5f * S + Vector3.up * 1.2f * S;
            Pose(new Vector3(0.6f, 0.2f, -0.4f), Vector3.up, 0.95f);
            BodyTwist = 25f;
            for (float t = 0f; t < 0.3f; t += Time.deltaTime)
            {
                ball.transform.position = toss + Vector3.up * Mathf.Sin(t / 0.3f * Mathf.PI) * 0.8f * S;
                yield return null;
            }
            yield return Swing(HorizontalArc(100f, -40f), Vector3.up, 0f, 0.1f, 0.1f, 0.5f, null, 1f, 0.2f);
            LocksMovement = false;
            SwordActive = false;
            Fx.Play("Star hit", ball.transform.position, Quaternion.identity, 0.35f * S, 0.1f, false);

            var hit = new HashSet<TrainingDummy>();
            Vector3 pos = ball.transform.position;
            float H = 2.4f * S;
            GameObject trail = Fx.Play("Sparks flashing yellow", pos, Quaternion.identity, 0.25f * S, 3f, false, ball.transform);
            for (int n = 1; n <= 4; n++)
            {
                var next = Nearest(pos, 9f * S, 1, hit);
                Vector3 to = next.Count > 0 ? next[0].Center : pos + transform.forward * 4f * S;
                float h = H * Mathf.Pow(0.5f, n - 1);
                float time = 0.28f;
                for (float t = 0f; t < time; t += Time.deltaTime)
                {
                    float u = t / time;
                    ball.transform.position = Vector3.Lerp(pos, to, u) + Vector3.up * 4f * h * u * (1f - u);
                    yield return null;
                }
                pos = to;
                if (next.Count == 0) break;
                TrainingDummy d = next[0];
                hit.Add(d);
                Damage(d, 40f * Mathf.Pow(0.85f, n - 1), true, "Star hit");
                d.Knock((d.transform.position - transform.position).normalized * 5f * S);
            }
            if (trail) trail.transform.SetParent(null, true);
            Fx.Play("Sparks explode yellow", pos, Quaternion.identity, 0.4f * S, 0.1f, false);
            Destroy(ball);
        }

        // X: 배트를 한 바퀴 돌려 주변 적을 끌어당긴 뒤, 사방으로 날린다
        IEnumerator BatVacuum()
        {
            float R = 6f * S;
            var targets = Nearest(transform.position, R, 12);
            Fx.Play("Dust ground", transform.position, Quaternion.identity, 0.45f * S, 0.4f, false);
            Pose(new Vector3(1f, -0.1f, 0f), Vector3.up);
            TrailOn = true;
            var starts = new List<Vector3>();
            foreach (var d in targets) starts.Add(d.transform.position);
            Quaternion rot = transform.rotation;
            OverridesMotion = true;
            for (float t = 0f; t < 0.55f; t += Time.deltaTime)
            {
                float u = t / 0.55f;
                transform.rotation = rot * Quaternion.Euler(0f, -360f * u, 0f);
                for (int i = 0; i < targets.Count; i++)
                {
                    if (!targets[i] || !targets[i].Alive) continue;
                    Vector3 dir = (starts[i] - transform.position);
                    dir.y = 0f;
                    Vector3 to = transform.position + dir.normalized * 1.4f * S;
                    targets[i].MoveTo(Vector3.Lerp(starts[i], to, u * u));
                }
                yield return null;
            }
            transform.rotation = rot;
            OverridesMotion = false;
            yield return Swing(HorizontalArc(160f, -160f), Vector3.up, 0.08f, 0.22f, 0.15f, 0.4f, () =>
            {
                m_Player.Shake(0.16f, 0.3f);
                Fx.Play("Sparks explode yellow", Chest, Quaternion.identity, 0.6f * S, 0.15f, false);
                foreach (var d in targets)
                {
                    if (!d || !d.Alive) continue;
                    Damage(d, 46f, true, "Star hit");
                    d.Knock((d.transform.position - transform.position).normalized * 14f * S);
                }
            }, 1.05f, 0f);
        }

        // C: 주변 적을 앞쪽 호 위에 줄 세우고, 한 번에 날린다 (k번째 적의 발사각 30° + 6°k)
        IEnumerator BatGrandSlam()
        {
            float R = 10f * S;
            var targets = Nearest(transform.position, R, 10);
            var starts = new List<Vector3>();
            var slots = new List<Vector3>();
            for (int i = 0; i < targets.Count; i++)
            {
                starts.Add(targets[i].transform.position);
                float a = targets.Count == 1 ? 0f : Mathf.Lerp(-60f, 60f, i / (targets.Count - 1f));
                Vector2 p = GeoFunctions.Arc(1.8f * S, a);
                slots.Add(World(p));
            }
            Fx.Play("Star aura", transform.position, Quaternion.identity, 0.7f * S, 1f, false, transform);
            Pose(new Vector3(0.7f, 0.5f, -0.5f), Vector3.up, 0.95f);
            BodyTwist = 40f;
            Crouch = 0.6f;
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                float u = GeoFunctions.EaseOutCubic(t / 0.5f);
                for (int i = 0; i < targets.Count; i++)
                    if (targets[i] && targets[i].Alive) targets[i].MoveTo(Vector3.Lerp(starts[i], slots[i], u));
                yield return null;
            }
            yield return new WaitForSeconds(0.15f);
            Crouch = 0f;
            yield return Swing(HorizontalArc(150f, -120f, 0.05f), Vector3.up, 0.05f, 0.2f, 0.3f, 0.45f, () =>
            {
                BodyTwist = -40f;
                m_Player.Shake(0.3f, 0.5f);
                Fx.Play("Sparks explode yellow", Chest + transform.forward * 1f * S, Quaternion.identity, 1f * S, 0.2f, false);
                Fx.Play("FX_PinkCross_Up", transform.position + transform.forward * 1.5f * S, Quaternion.identity, 0.5f * S, 0.2f, false);
                for (int k = 0; k < targets.Count; k++)
                {
                    TrainingDummy d = targets[k];
                    if (!d || !d.Alive) continue;
                    float angle = 30f + 6f * k, v = 12f * S, g = 9.8f * S;
                    Vector3 dir = (d.transform.position - transform.position);
                    dir.y = 0f;
                    dir = dir.normalized;
                    Damage(d, 95f, true, "Holy hit");
                    LaunchHomeRun(d, dir * v * Mathf.Cos(angle * Mathf.Deg2Rad) + Vector3.up * v * Mathf.Sin(angle * Mathf.Deg2Rad), g);
                }
            }, 1.05f, 0f);
            BodyTwist = 0f;
        }
    }
}
