using System.Collections;
using UnityEngine;

namespace Unity.FPS.NewMode
{
    // 성의 수호자 (보스) — 근접 공격(TrainingDummy AI) 말고도 특수 공격을 번갈아 쓴다
    //  1 대지 강타 : 플레이어 발밑에 빨간 원(1.1초 예고) → 폭발. 원 밖으로 나가면 피함
    //  2 충격파    : 보스에서 퍼져 나가는 고리. 땅에 있으면 맞음 → 점프로 넘기
    //  3 부하 소환 : 체력이 절반이 되면 깡총이 둘 (한 번)
    //  체력이 절반 아래면 더 자주
    public class BossBrain : MonoBehaviour
    {
        TrainingDummy m_Boss;
        float m_Next = 4f;
        bool m_Summoned;
        int m_Turn;

        public void Init(TrainingDummy boss) => m_Boss = boss;

        void Update()
        {
            if (!m_Boss || !m_Boss.Alive) return;
            NewModePlayer p = NewModePlayer.Instance;
            if (!p || p.Dead || !m_Boss.Engaged || m_Boss.Busy || m_Boss.Frozen) return;

            float half = m_Boss.Health / m_Boss.MaxHealth;
            if (!m_Summoned && half < 0.5f)
            {
                m_Summoned = true;
                StartCoroutine(Summon());
                return;
            }

            m_Next -= Time.deltaTime;
            if (m_Next > 0f) return;
            m_Next = half < 0.5f ? Random.Range(3f, 4f) : Random.Range(4.5f, 6f);
            if (m_Turn++ % 2 == 0) StartCoroutine(Slam(p));
            else StartCoroutine(Shockwave());
        }

        float S => m_Boss.Scale;

        IEnumerator Slam(NewModePlayer p)
        {
            Vector3 at = p.transform.position;
            at.y = GeoFx.GroundY(at, S);
            float r = 2.2f * S;
            Telegraph.Disc(at, r, 1.1f, new Color(1f, 0.2f, 0.15f));
            yield return new WaitForSeconds(1.1f);
            if (!m_Boss || !m_Boss.Alive) yield break;
            Fx.Play("Ground AOE explosion", at, Quaternion.identity, 0.6f * S, 0.25f, false);
            Fx.Play("Stones hit", at, Quaternion.LookRotation(Vector3.up), 0.6f * S, 0.15f, false);
            if (p && !p.Dead)
            {
                Vector3 d = p.transform.position - at;
                d.y = 0f;
                if (d.magnitude < r && p.transform.position.y - at.y < 1.2f * S) p.TakeDamage(16f, at, 7f);
                p.Shake(0.15f, 0.3f);
            }
        }

        IEnumerator Shockwave()
        {
            Vector3 c = m_Boss.transform.position;
            // 예고: 보스가 웅크리고 발밑이 빨갛게
            Telegraph.Disc(c, 1.6f * S, 0.8f, new Color(1f, 0.5f, 0.1f));
            yield return new WaitForSeconds(0.8f);
            if (!m_Boss || !m_Boss.Alive) yield break;
            Fx.Play("Smoke AOE explosion", c, Quaternion.identity, 0.5f * S, 0.2f, false);

            // r(t) = v·t 로 퍼지는 고리 — 고리가 지나갈 때 땅에 있으면 맞음
            float v = 7f * S, max = 10f * S;
            GameObject ring = Telegraph.Ring(c, new Color(1f, 0.55f, 0.15f));
            bool hit = false;
            for (float t = 0f; t * v < max; t += Time.deltaTime)
            {
                float r = t * v;
                ring.transform.localScale = new Vector3(r * 2f, r * 2f, 1f);
                NewModePlayer p = NewModePlayer.Instance;
                if (!hit && p && !p.Dead)
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

        IEnumerator Summon()
        {
            Telegraph.Disc(m_Boss.transform.position, 3f * S, 0.8f, new Color(0.7f, 0.3f, 1f));
            yield return new WaitForSeconds(0.8f);
            for (int i = 0; i < 2; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                Vector3 p = m_Boss.transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 2.5f * S;
                p.y = GeoFx.GroundY(p, S);
                if (Mathf.Abs(p.y - m_Boss.transform.position.y) > 0.6f * S) p = m_Boss.transform.position + Vector3.right * (i == 0 ? 2f : -2f) * S;
                var mob = TrainingDummy.Create(p, Quaternion.identity, S, MobKind.Hopper);
                mob.Hostile = true;
                mob.Respawns = false;
                mob.MaxHealth = 5000f;
                mob.ResetHealth();
                Fx.Play("Teleport", p, Quaternion.identity, 0.4f * S, 0.15f, false);
            }
        }
    }

    // 땅 위의 공격 예고 표시 (반투명 빨간 원·고리)
    public static class Telegraph
    {
        static Texture2D s_Disc, s_Ring;
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
}
