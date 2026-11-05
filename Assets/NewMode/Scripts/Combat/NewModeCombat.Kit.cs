using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.FPS.MathMode;
using UnityEngine;

namespace Unity.FPS.NewMode
{
    // 무기 키트 공용 도구: 기믹 HUD · 핀볼 충돌 · 연쇄 번개 · 날아가는 투사체 · 도약 · 회전
    public partial class NewModeCombat
    {
        TextMeshProUGUI m_GimmickLabel;

        void UpdateGimmickHUD()
        {
            if (!m_Canvas || m_Weapon == null) return;
            if (!m_GimmickLabel)
            {
                m_GimmickLabel = UIUtil.Text(m_Canvas.transform, m_Font, "", 26f, TextAlignmentOptions.Center);
                UIUtil.Place(m_GimmickLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 172f), new Vector2(900f, 40f));
                m_GimmickLabel.outlineWidth = 0.25f;
                m_GimmickLabel.outlineColor = new Color(0.03f, 0.05f, 0.12f);
            }
            string state = m_Weapon.Gimmick != null ? m_Weapon.Gimmick() : "";
            m_GimmickLabel.text = $"<color=#{ColorUtility.ToHtmlStringRGB(Color.Lerp(C, Color.white, 0.35f))}><b>{m_Weapon.GimmickName}</b></color>   {state}";
        }

        // 게이지 막대 글자: ■■■□□
        static string Bar(float fill, int cells = 10)
        {
            int on = Mathf.Clamp(Mathf.RoundToInt(fill * cells), 0, cells);
            return "<mspace=0.55em>" + new string('■', on) + "<color=#FFFFFF44>" + new string('■', cells - on) + "</color></mspace>";
        }

        // 핀볼: 밀려난 몹이 다른 몹과 부딪치면 둘 다 다친다 (속도에 비례)
        void OnBumped(TrainingDummy a, TrainingDummy b, float speed)
        {
            if (!this || m_Weapon == null) return;
            float amount = Mathf.Clamp(speed / S * 2.2f, 8f, 40f);
            m_HitTag = null;
            Fx.Play(Th.Impact, (a.Center + b.Center) * 0.5f, Quaternion.identity, 0.45f * S, 0.15f, false);
            DamageTagged(a, amount * 0.6f, false, "충돌!");
            DamageTagged(b, amount, true, "충돌!");
        }

        void DamageTagged(TrainingDummy d, float amount, bool big, string tag, string fx = null)
        {
            // Damage 안에서 HitMultiplier가 m_HitTag를 덮어쓸 수 있으므로, 기믹 배율 없이 직접 계산
            if (!d || !d.Alive) return;
            bool crit = m_Ultimate || UnityEngine.Random.value < (big ? 0.45f : 0.15f);
            float dmg = amount * k_DamageScale * UnityEngine.Random.Range(0.85f, 1.15f) * (crit ? 1.9f : 1f) * (m_Ultimate ? 2f : 1f) * (PowerBuff ? 2f : 1f);
            d.Hit(dmg, transform.position, big, crit, tag);
            if (fx != null) Fx.Play(fx, d.Center, Quaternion.identity, 0.45f * S, 0.2f, false);
            OnImpact(d, big, crit);
        }

        // ---------------------------------------------------------------- 찾기

        List<TrainingDummy> Nearest(Vector3 from, float radius, int max, ICollection<TrainingDummy> exclude = null)
        {
            var list = new List<(TrainingDummy d, float dist)>();
            foreach (var d in Targets())
            {
                if (exclude != null && exclude.Contains(d)) continue;
                Vector3 diff = d.transform.position - from;
                diff.y = 0f;
                float m = diff.magnitude;
                if (m <= radius) list.Add((d, m));
            }
            list.Sort((a, b) => a.dist.CompareTo(b.dist));
            var result = new List<TrainingDummy>();
            for (int i = 0; i < list.Count && i < max; i++) result.Add(list[i].d);
            return result;
        }

        /// <summary>앞쪽 부채꼴(반각 halfDeg) 안에서 가장 가까운 순으로</summary>
        List<TrainingDummy> InFront(float radius, float halfDeg, int max)
        {
            var list = new List<TrainingDummy>();
            foreach (var d in Nearest(transform.position, radius, 99))
                if (GeoFunctions.InSector(Local(d.transform.position), radius, halfDeg)) { list.Add(d); if (list.Count >= max) break; }
            return list;
        }

        // ---------------------------------------------------------------- 번개

        /// <summary>a에서 b로 지그재그 번개 (선분을 n등분하고 수직으로 흔듦)</summary>
        void Bolt(Vector3 a, Vector3 b, Color color, float width = 0.06f, float life = 0.22f)
        {
            const int n = 9;
            var pts = new Vector3[n + 1];
            Vector3 dir = b - a;
            Vector3 side = Vector3.Cross(dir, Vector3.up).normalized;
            if (side.sqrMagnitude < 0.01f) side = Vector3.right;
            Vector3 up2 = Vector3.Cross(side, dir).normalized;
            for (int i = 0; i <= n; i++)
            {
                float u = i / (float)n;
                float jitter = (i == 0 || i == n) ? 0f : 0.25f * S;
                pts[i] = a + dir * u + side * UnityEngine.Random.Range(-jitter, jitter) + up2 * UnityEngine.Random.Range(-jitter, jitter);
            }
            GeoFx.Line(pts, width * S, color, 0f, life);
        }

        /// <summary>연쇄 번개: start에서 가까운 적으로 jumps번 튄다 (튈 때마다 피해 × falloff)</summary>
        IEnumerator Chain(TrainingDummy start, int jumps, float amount, float range, float falloff = 0.8f)
        {
            var hit = new HashSet<TrainingDummy> { start };
            TrainingDummy current = start;
            for (int i = 0; i < jumps && current; i++)
            {
                var next = Nearest(current.transform.position, range, 1, hit);
                if (next.Count == 0) yield break;
                TrainingDummy d = next[0];
                Bolt(current.Center, d.Center, Color.Lerp(k_Pink, Color.white, 0.4f));
                amount *= falloff;
                DamageTagged(d, amount, false, "연쇄", "Electro hit");
                hit.Add(d);
                current = d;
                yield return new WaitForSeconds(0.06f);
            }
        }

        // ---------------------------------------------------------------- 투사체

        /// <summary>
        /// path(t) (0 ≤ t ≤ time)를 따라 날아가는 이펙트. radius 안에 들어온 적에게 onHit을 한 번씩.
        /// pierce가 아니면 첫 적에서 멈추고 그 적을 돌려준다.
        /// </summary>
        IEnumerator Fly(Func<float, Vector3> path, float time, float radius, Action<TrainingDummy, Vector3> onHit,
            string fx, float fxSize, bool pierce, GameObject model = null)
        {
            var carrier = new GameObject("Projectile");
            carrier.transform.position = path(0f);
            GameObject trail = fx != null ? Fx.Play(fx, carrier.transform.position, Quaternion.identity, fxSize, time + 0.1f, false, carrier.transform) : null;
            if (model) model.transform.SetParent(carrier.transform, true);
            var done = new HashSet<TrainingDummy>();
            Vector3 last = carrier.transform.position;
            for (float t = 0f; t <= time; t += Time.deltaTime)
            {
                Vector3 p = path(Mathf.Min(t, time));
                carrier.transform.position = p;
                if ((p - last).sqrMagnitude > 0.0001f) carrier.transform.rotation = Quaternion.LookRotation(p - last);
                last = p;
                foreach (var d in Targets())
                {
                    if (done.Contains(d)) continue;
                    if ((d.Center - p).magnitude > radius + d.Radius && (d.transform.position - p).magnitude > radius + d.Radius) continue;
                    done.Add(d);
                    onHit?.Invoke(d, p);
                    if (!pierce) { Cleanup(carrier, trail); yield break; }
                }
                yield return null;
            }
            Cleanup(carrier, trail);
        }

        static void Cleanup(GameObject carrier, GameObject trail)
        {
            if (trail) trail.transform.SetParent(null, true);
            Destroy(carrier);
        }

        static GameObject Ball(float size, Color color)
        {
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(ball.GetComponent<Collider>());
            ball.layer = 2;
            ball.transform.localScale = Vector3.one * size;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * 1.5f);
            ball.GetComponent<Renderer>().material = mat;
            return ball;
        }

        // ---------------------------------------------------------------- 몸 움직이기

        /// <summary>포물선 y = −(4H/D²)(x − D/2)² + H 를 따라 앞으로 D만큼 뛴다</summary>
        IEnumerator Leap(float D, float H, float flight, Func<float, Vector3> slam)
        {
            OverridesMotion = true;
            CharacterController cc = m_Player.Controller;
            for (float t = 0f; t < flight; t += Time.deltaTime)
            {
                float x = Mathf.Clamp01(t / flight) * D;
                cc.Move(World(new Vector2(x, 0f), GeoFunctions.Parabola(x, D, H)) - transform.position);
                if (slam != null)
                {
                    float fall = Mathf.InverseLerp(0.55f, 1f, t / flight);
                    BladeDirLocal = slam(GeoFunctions.EaseOutCubic(fall));
                    TrailOn = fall > 0f;
                    BodyLean = Mathf.Lerp(-12f, 25f, fall);
                }
                yield return null;
            }
            BodyLean = 0f;
            OverridesMotion = false;
        }

        IEnumerator SpinFor(float time, int turns)
        {
            OverridesMotion = true;
            Quaternion start = transform.rotation;
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                transform.rotation = start * Quaternion.Euler(0f, -360f * turns * t / time, 0f);
                yield return null;
            }
            transform.rotation = start;
            OverridesMotion = false;
        }

        /// <summary>순간이동 (지형 위로)</summary>
        void Blink(Vector3 to)
        {
            CharacterController cc = m_Player.Controller;
            to = SafeGround(to);
            cc.enabled = false;
            transform.position = to;
            cc.enabled = true;
        }

        /// <summary>
        /// 순간이동·착지할 수 있는 안전한 땅: 내비메시 위(걸을 수 있는 곳)로 붙이고,
        /// 지금 높이와 너무 다르면(지붕·낭떠러지 아래) 지금 자리에 머문다.
        /// </summary>
        Vector3 SafeGround(Vector3 p)
        {
            Vector3 here = transform.position;
            if (UnityEngine.AI.NavMesh.SamplePosition(p, out UnityEngine.AI.NavMeshHit hit, 1.5f * S, UnityEngine.AI.NavMesh.AllAreas)
                && Mathf.Abs(hit.position.y - here.y) < 2.5f * S)
                return hit.position + Vector3.up * 0.05f * S;
            float y = GeoFx.GroundY(p, S);
            if (Mathf.Abs(y - here.y) < 1f * S) return new Vector3(p.x, y + 0.05f * S, p.z);
            return here;
        }

        void FaceTo(Vector3 world)
        {
            Vector3 d = world - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(d);
        }

        bool LmbHeld()
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            var pad = UnityEngine.InputSystem.Gamepad.current;
            return (mouse != null && mouse.leftButton.isPressed) || (pad != null && pad.buttonWest.isPressed);
        }
    }
}
