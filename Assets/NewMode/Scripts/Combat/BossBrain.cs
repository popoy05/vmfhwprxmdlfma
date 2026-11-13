using System.Collections;
using UnityEngine;

namespace Unity.FPS.NewMode
{
    // 보스 "하늘 고래" — 가까이 붙으면 몸통 박치기(TrainingDummy AI), 그 밖에 특수 패턴을 번갈아 쓴다
    //  1 물기둥     : 플레이어 발밑·주변에 빨간 원(예고) → 물기둥 폭발. 원 밖으로 나가면 피함 (2단계부터 여러 번 따라옴)
    //  2 해일 고리  : 고래에서 퍼지는 주황 고리. 땅에 있으면 맞음 → 점프로 넘기 (2단계부터 두 겹)
    //  3 해일 돌진  : 플레이어 쪽으로 빨간 길(예고) → 그 길을 따라 돌진. 옆으로 비켜서 피함 (3단계: 되돌아 한 번 더)
    //  4 물방울 탄막: 부채꼴로 물방울을 뿜음. 사이로 빠지거나 점프 (3단계: 두 번)
    //  6 유령 소환  : 체력 60%·30%에서 유령을 부른다
    // 단계: 1(체력 60% 이상) → 2(60% 아래: 패턴 추가·빨라짐) → 3(30% 아래: 격노 — 더 빠르고 연속 패턴)
    // 어려움 모드: 처음부터 2단계, 50% 아래 격노, 패턴 간격·예고가 짧고 물기둥·물방울·돌진이 더 많다
    public class BossBrain : MonoBehaviour
    {
        TrainingDummy m_Boss;
        float m_Next = 3.5f;
        int m_Summons;
        int m_Turn;
        bool m_Casting;
        bool m_Enraged;

        public void Init(TrainingDummy boss) => m_Boss = boss;

        float S => m_Boss.Scale;
        float B => m_Boss.Size;
        float HealthRatio => m_Boss.Health / m_Boss.MaxHealth;
        static bool Hard => CombatDifficulty.Hard;
        float EnrageAt => Hard ? 0.5f : 0.3f;
        int Phase => HealthRatio < EnrageAt ? 3 : (Hard || HealthRatio < 0.6f) ? 2 : 1;
        static float W => CombatDifficulty.BossWarn; // 예고 시간 배율

        void Update()
        {
            if (!m_Boss || !m_Boss.Alive)
            {
                if (m_Boss) { m_Boss.Hold = false; m_Boss.Invulnerable = false; }
                return;
            }
            NewModePlayer p = NewModePlayer.Instance;
            if (m_Casting || !p || p.Dead || !m_Boss.Engaged || m_Boss.Busy || m_Boss.Frozen) return;

            // 체력 60%·30%: 유령 소환 (30%에선 격노 연출도)
            if ((m_Summons == 0 && HealthRatio < 0.6f) || (m_Summons == 1 && HealthRatio < EnrageAt))
            {
                m_Summons++;
                Run(Summon(m_Summons == 1 ? 2 : 3));
                return;
            }

            m_Next -= Time.deltaTime;
            if (m_Next > 0f) return;
            int phase = Phase;
            m_Next = (phase == 3 ? Random.Range(1.6f, 2.3f) : phase == 2 ? Random.Range(2.4f, 3.2f) : Random.Range(3.4f, 4.4f)) * CombatDifficulty.BossTempo;

            // 단계마다 쓸 수 있는 패턴 순서 (같은 패턴이 연달아 나오지 않게 돌아가며)
            int[] order = phase == 1 ? new[] { 0, 2, 1, 3 } : new[] { 0, 2, 1, 3, 2, 0, 3 };
            int pick = order[m_Turn++ % order.Length];
            switch (pick)
            {
                case 0: Run(Geysers(p)); break;
                case 1: Run(Shockwave(m_Boss.transform.position, phase >= 2 ? 2 : 1)); break;
                case 2: Run(Charge(p, phase == 3 ? (Hard ? 3 : 2) : 1)); break;
                case 3: Run(Bubbles(p, phase == 3 ? (Hard ? 3 : 2) : 1)); break;
            }
        }

        void Run(IEnumerator pattern) => StartCoroutine(Cast(pattern));

        IEnumerator Cast(IEnumerator pattern)
        {
            m_Casting = true;
            m_Boss.Hold = true; // 패턴 쓰는 동안은 걸어오거나 박치기하지 않음
            yield return pattern;
            if (m_Boss)
            {
                m_Boss.Hold = false;
                m_Boss.Invulnerable = false;
            }
            m_Casting = false;
        }

        bool Gone => !m_Boss || !m_Boss.Alive;

        void Face(Vector3 at)
        {
            Vector3 d = at - m_Boss.transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.0001f) m_Boss.transform.rotation = Quaternion.LookRotation(d);
        }

        static bool PlayerOk(NewModePlayer p) => p && !p.Dead;

        // ------------------------------------------------------------------ 1 물기둥

        IEnumerator Geysers(NewModePlayer p)
        {
            int phase = Phase;
            m_Boss.PlayAttack(0.9f);
            Face(p.transform.position);
            int waves = phase == 1 ? 1 : phase == 2 ? 2 : 3;
            for (int w = 0; w < waves && !Gone; w++)
            {
                if (!PlayerOk(p)) yield break;
                // 플레이어 자리 하나 + 주변 몇 개 (뒤 단계일수록 많고 빠르게)
                int extra = (phase == 1 ? 3 : 4) + (Hard ? 2 : 0);
                float warn = (phase == 3 ? 0.75f : 1f) * W;
                Vector3 at = p.transform.position;
                StartCoroutine(Geyser(at, 2f * S, warn));
                for (int i = 0; i < extra; i++)
                {
                    Vector2 r = Random.insideUnitCircle.normalized * Random.Range(2.5f, 5.5f) * S;
                    StartCoroutine(Geyser(at + new Vector3(r.x, 0f, r.y), 1.6f * S, warn + Random.Range(0.1f, 0.35f)));
                }
                yield return new WaitForSeconds(phase == 3 ? 0.5f : 0.75f);
            }
            yield return new WaitForSeconds(0.6f);
        }

        IEnumerator Geyser(Vector3 at, float r, float warn)
        {
            at.y = GeoFx.GroundY(at, S);
            Telegraph.Disc(at, r, warn, new Color(1f, 0.2f, 0.15f));
            yield return new WaitForSeconds(warn);
            if (Gone) yield break;
            Fx.Play("Sparks explode blue", at, Quaternion.identity, 0.8f * r, 0.2f, false);
            Fx.Play("Smoke AOE explosion", at, Quaternion.identity, 0.45f * r, 0.15f, false);
            NewModePlayer p = NewModePlayer.Instance;
            if (!PlayerOk(p)) yield break;
            Vector3 d = p.transform.position - at;
            float h = d.y;
            d.y = 0f;
            if (d.magnitude < r && h < 1.6f * S) p.TakeDamage(16f, at, 6f);
            p.Shake(0.08f, 0.2f);
        }

        // ------------------------------------------------------------------ 2 해일 고리 (점프로 넘기)

        IEnumerator Shockwave(Vector3 c, int rings)
        {
            m_Boss.PlayAttack(0.8f);
            Telegraph.Disc(c, 1.2f * B * 0.5f, 0.8f * W, new Color(1f, 0.5f, 0.1f));
            yield return new WaitForSeconds(0.8f * W);
            for (int i = 0; i < rings && !Gone; i++)
            {
                StartCoroutine(Ring(c, 7.5f * S, 14f * S));
                yield return new WaitForSeconds(0.7f);
            }
            yield return new WaitForSeconds(0.4f);
        }

        IEnumerator Ring(Vector3 c, float speed, float max)
        {
            Fx.Play("Smoke AOE explosion", c, Quaternion.identity, 0.5f * S * 2f, 0.2f, false);
            // r(t) = v·t 로 퍼지는 고리 — 고리가 지나갈 때 땅에 있으면 맞음
            GameObject ring = Telegraph.Ring(c, new Color(1f, 0.55f, 0.15f));
            bool hit = false;
            for (float t = 0f; t * speed < max; t += Time.deltaTime)
            {
                if (Gone) break;
                float r = t * speed;
                ring.transform.localScale = new Vector3(r * 2f, r * 2f, 1f);
                NewModePlayer p = NewModePlayer.Instance;
                if (!hit && PlayerOk(p))
                {
                    Vector3 d = p.transform.position - c;
                    float h = d.y;
                    d.y = 0f;
                    if (Mathf.Abs(d.magnitude - r) < 0.45f * S && p.Grounded && h < 1.5f * S)
                    {
                        hit = true;
                        p.TakeDamage(12f, c, 5f);
                    }
                }
                yield return null;
            }
            Destroy(ring);
        }

        // ------------------------------------------------------------------ 3 해일 돌진

        IEnumerator Charge(NewModePlayer p, int times)
        {
            for (int n = 0; n < times && !Gone; n++)
            {
                if (!PlayerOk(p)) yield break;
                Vector3 from = m_Boss.transform.position;
                Vector3 dir = p.transform.position - from;
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.01f) dir = m_Boss.transform.forward;
                dir.Normalize();
                Face(from + dir);

                // 길이: 플레이어를 지나 조금 더 (최대 18), 땅이 끊기거나 벽이면 그 앞까지
                float want = Mathf.Min((p.transform.position - from).magnitude + 5f * S, 18f * S);
                float len = 0f;
                for (float l = S; l <= want; l += S)
                {
                    Vector3 q = from + dir * l;
                    float gy = GeoFx.GroundY(q, S);
                    if (Mathf.Abs(gy - from.y) > 1.2f * S) break;
                    len = l;
                }
                // 벽·건물 앞에서 멈춤
                if (Physics.SphereCast(m_Boss.Center, m_Boss.Radius * 0.5f, dir, out RaycastHit wall, len + m_Boss.Radius, ~(1 << 2), QueryTriggerInteraction.Ignore))
                    len = Mathf.Min(len, wall.distance - m_Boss.Radius * 0.5f);
                // 집에서 너무 멀어지지 않게 (돌아갈 수 있게)
                for (int k = 0; k < 20 && len > 0f; k++)
                {
                    Vector3 end = from + dir * len - m_Boss.Home;
                    end.y = 0f;
                    if (end.magnitude <= 18f * S) break;
                    len -= S;
                }
                len = Mathf.Max(len, 0f);
                float width = 2.2f * m_Boss.Radius * 0.75f;
                float warn = (Phase == 3 ? 0.75f : 1f) * W;
                Telegraph.Lane(from, dir, len, width, warn, new Color(1f, 0.2f, 0.15f));
                m_Boss.PlayAttack(warn + 0.6f);
                // 예고 동안 뒤로 살짝 물러났다가
                for (float t = 0f; t < warn; t += Time.deltaTime)
                {
                    if (Gone) yield break;
                    m_Boss.SetBodyOffset(-Vector3.forward * 0.25f * B * Mathf.Sin(Mathf.Clamp01(t / warn) * Mathf.PI * 0.5f));
                    yield return null;
                }

                // 돌진: 0.45초에 길 끝까지 (ease-in-out), 지나가는 길 위에 있으면 크게 맞음
                Vector3 to = from + dir * len;
                to.y = GeoFx.GroundY(to, S);
                bool hit = false;
                float time = 0.45f;
                Fx.Play("Sparks explode blue", m_Boss.Center, Quaternion.identity, 0.6f * B, 0.15f, false);
                for (float t = 0f; t < time; t += Time.deltaTime)
                {
                    if (Gone) yield break;
                    float u = Mathf.SmoothStep(0f, 1f, t / time);
                    Vector3 pos = Vector3.Lerp(from, to, u);
                    pos.y = Mathf.Lerp(from.y, to.y, u);
                    m_Boss.transform.position = pos;
                    m_Boss.SetBodyOffset(Vector3.zero);
                    if (!hit && PlayerOk(p))
                    {
                        Vector3 d = p.transform.position - pos;
                        float h = d.y;
                        d.y = 0f;
                        if (d.magnitude < m_Boss.Radius + 0.6f * S && h < m_Boss.Height)
                        {
                            hit = true;
                            p.TakeDamage(24f, pos - dir * S, 11f);
                            p.Shake(0.2f, 0.3f);
                        }
                    }
                    yield return null;
                }
                if (Gone) yield break;
                m_Boss.transform.position = to;
                Fx.Play("Dust ground", to, Quaternion.identity, 0.6f * B, 0.2f, false);
                yield return new WaitForSeconds(n + 1 < times ? 0.35f : 0.7f);
            }
        }

        // ------------------------------------------------------------------ 4 물방울 탄막

        static Material s_Bubble;

        IEnumerator Bubbles(NewModePlayer p, int volleys)
        {
            for (int v = 0; v < volleys && !Gone; v++)
            {
                if (!PlayerOk(p)) yield break;
                Face(p.transform.position);
                m_Boss.PlayAttack(0.9f);
                yield return new WaitForSeconds(0.55f); // 입을 벌리는 동안 (피할 준비)
                if (Gone) yield break;
                Vector3 mouth = m_Boss.Head;
                Vector3 aim = p.transform.position - m_Boss.transform.position;
                aim.y = 0f;
                aim.Normalize();
                int count = (Phase == 1 ? 5 : 7) + (Hard ? 2 : 0);
                float spread = Phase == 1 ? 50f : 70f;
                float offset = v % 2 == 1 ? spread / (count - 1) * 0.5f : 0f; // 두 번째는 사이사이로
                for (int i = 0; i < count; i++)
                {
                    float a = -spread * 0.5f + spread * i / (count - 1) + offset;
                    StartCoroutine(Bubble(mouth, Quaternion.Euler(0f, a, 0f) * aim));
                }
                yield return new WaitForSeconds(0.7f);
            }
            yield return new WaitForSeconds(0.4f);
        }

        IEnumerator Bubble(Vector3 from, Vector3 dir)
        {
            if (!s_Bubble) s_Bubble = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.45f, 0.8f, 1f, 0.65f) };
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(go.GetComponent<Collider>());
            go.layer = 2;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = s_Bubble;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            float size = 0.9f * S;
            go.transform.localScale = Vector3.one * size;
            Fx.Play("Sparks blue", from, Quaternion.identity, 0.25f * S, 2.5f, false, go.transform);

            // 입에서 나와 땅 높이(허리쯤)로 내려앉은 뒤 곧게 날아감
            float speed = 7f * S, life = 2.6f;
            Vector3 pos = from;
            for (float t = 0f; t < life; t += Time.deltaTime)
            {
                pos += dir * speed * Time.deltaTime;
                float ground = GeoFx.GroundY(pos, S) + 0.55f * S;
                pos.y = Mathf.Lerp(pos.y, ground, 1f - Mathf.Exp(-6f * Time.deltaTime));
                go.transform.position = pos;
                go.transform.localScale = Vector3.one * size * (1f + 0.08f * Mathf.Sin(t * 14f));
                NewModePlayer p = NewModePlayer.Instance;
                if (PlayerOk(p))
                {
                    Vector3 c = p.transform.position + Vector3.up * 0.8f * p.Scale;
                    if ((c - pos).magnitude < size * 0.5f + 0.35f * S)
                    {
                        p.TakeDamage(9f, pos - dir, 4f);
                        Fx.Play("Sparks explode blue", pos, Quaternion.identity, 0.35f * S, 0.1f, false);
                        break;
                    }
                }
                yield return null;
            }
            Destroy(go);
        }

        // ------------------------------------------------------------------ 6 유령 소환

        IEnumerator Summon(int count)
        {
            m_Boss.PlayAttack(1f);
            Telegraph.Disc(m_Boss.transform.position, 1.2f * B, 0.9f, new Color(0.7f, 0.3f, 1f));
            if (count >= 3 && !m_Enraged)
            {
                m_Enraged = true;
                GeoFx.Popup(m_Boss.Center + Vector3.up * B, "<b>격노!</b>", new Color(1f, 0.35f, 0.3f), S, 2.2f);
                Fx.Play("Red energy explosion", m_Boss.Center, Quaternion.identity, 0.7f * B, 0.3f, false);
                NewModePlayer pl = NewModePlayer.Instance;
                if (pl) pl.Shake(0.25f, 0.5f);
            }
            yield return new WaitForSeconds(0.9f);
            for (int i = 0; i < count && !Gone; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                Vector3 p = m_Boss.transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (m_Boss.Radius + 2f * S);
                p.y = GeoFx.GroundY(p, S);
                if (Mathf.Abs(p.y - m_Boss.transform.position.y) > 0.6f * S) p = m_Boss.transform.position + m_Boss.transform.right * (i - 1) * 3f * S;
                var mob = TrainingDummy.Create(p, m_Boss.transform.rotation, S, MobKind.Hopper);
                mob.Hostile = true;
                mob.Respawns = false;
                mob.MaxHealth = 6000f;
                mob.ResetHealth();
                Fx.Play("Teleport", p, Quaternion.identity, 0.4f * S, 0.15f, false);
            }
        }
    }

    // 땅 위의 공격 예고 표시 (반투명 빨간 원·고리·길)
    public static class Telegraph
    {
        static Texture2D s_Disc, s_Ring, s_Box;
        static Material s_Material;

        static Texture2D Make(bool ring)
        {
            var tex = new Texture2D(128, 128, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 128; x++)
                {
                    float r = new Vector2(x - 63.5f, y - 63.5f).magnitude / 64f;
                    float a = ring
                        ? Mathf.Clamp01(1f - Mathf.Abs(r - 0.93f) / 0.07f)
                        : (r > 1f ? 0f : r > 0.9f ? 1f : 0.35f + 0.25f * r);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            return tex;
        }

        // 길 (가장자리 진하게, 안쪽 옅게)
        static Texture2D MakeBox()
        {
            var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    bool edge = x < 2 || x > 29;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, edge ? 1f : 0.4f));
                }
            tex.Apply();
            return tex;
        }

        static GameObject Quad(Vector3 center, Texture2D tex, Color color)
        {
            if (!s_Material) s_Material = new Material(Shader.Find("Sprites/Default"));
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.Destroy(go.GetComponent<Collider>());
            go.layer = 2;
            go.transform.position = center + Vector3.up * 0.06f;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var r = go.GetComponent<Renderer>();
            r.material = new Material(s_Material) { mainTexture = tex, color = color };
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>반지름 r 원 — time초 동안 안쪽이 차오른다</summary>
        public static void Disc(Vector3 center, float radius, float time, Color color)
        {
            if (!s_Disc) s_Disc = Make(false);
            if (!s_Ring) s_Ring = Make(true);
            var edge = Quad(center, s_Ring, color);
            edge.transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);
            var fill = Quad(center + Vector3.up * 0.01f, s_Disc, new Color(color.r, color.g, color.b, 0.6f));
            fill.AddComponent<TelegraphFill>().Init(radius, time, edge);
        }

        public static GameObject Ring(Vector3 center, Color color)
        {
            if (!s_Ring) s_Ring = Make(true);
            return Quad(center, s_Ring, color);
        }

        /// <summary>from에서 dir 쪽으로 길이 length, 폭 width인 길 — time초 동안 앞으로 차오른다</summary>
        public static void Lane(Vector3 from, Vector3 dir, float length, float width, float time, Color color)
        {
            if (!s_Box) s_Box = MakeBox();
            dir.y = 0f;
            dir.Normalize();
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            var edge = Quad(from + dir * length * 0.5f, s_Box, new Color(color.r, color.g, color.b, 0.45f));
            edge.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
            edge.transform.localScale = new Vector3(width, length, 1f);
            var fill = Quad(from, s_Box, new Color(color.r, color.g, color.b, 0.75f));
            fill.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
            fill.AddComponent<TelegraphLane>().Init(from, dir, length, width, time, edge);
        }
    }

    public class TelegraphFill : MonoBehaviour
    {
        float m_Radius, m_Time, m_Age;
        GameObject m_Edge;

        public void Init(float radius, float time, GameObject edge)
        {
            m_Radius = radius;
            m_Time = time;
            m_Edge = edge;
            transform.localScale = Vector3.zero;
        }

        void Update()
        {
            m_Age += Time.deltaTime;
            float u = Mathf.Clamp01(m_Age / m_Time);
            transform.localScale = new Vector3(m_Radius * 2f * u, m_Radius * 2f * u, 1f);
            if (m_Age >= m_Time + 0.1f)
            {
                if (m_Edge) Destroy(m_Edge);
                Destroy(gameObject);
            }
        }
    }

    public class TelegraphLane : MonoBehaviour
    {
        Vector3 m_From, m_Dir;
        float m_Length, m_Width, m_Time, m_Age;
        GameObject m_Edge;

        public void Init(Vector3 from, Vector3 dir, float length, float width, float time, GameObject edge)
        {
            m_From = from + Vector3.up * 0.07f;
            m_Dir = dir;
            m_Length = length;
            m_Width = width;
            m_Time = time;
            m_Edge = edge;
            transform.localScale = Vector3.zero;
        }

        void Update()
        {
            m_Age += Time.deltaTime;
            float u = Mathf.Clamp01(m_Age / m_Time);
            float l = m_Length * u;
            transform.position = m_From + m_Dir * l * 0.5f;
            transform.localScale = new Vector3(m_Width, Mathf.Max(0.001f, l), 1f);
            if (m_Age >= m_Time + 0.35f) // 돌진이 지나갈 때까지 조금 더 남김
            {
                if (m_Edge) Destroy(m_Edge);
                Destroy(gameObject);
            }
        }
    }
}
