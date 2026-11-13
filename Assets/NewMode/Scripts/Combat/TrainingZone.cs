using TMPro;
using Unity.FPS.MathMode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Unity.FPS.NewMode
{
    // 훈련장: 섬 바깥 하늘에 떠 있는 넓은 연습장.
    //  T(또는 일시정지 메뉴)로 모험 ↔ 훈련장 워프. 훈련장의 몹은 움직이지도 공격하지도 않고, 쓰러져도 다시 일어난다.
    //  훈련장에 있는 동안 모험 시간은 멈춘다.
    public class TrainingZone : MonoBehaviour
    {
        public static TrainingZone Instance { get; private set; }
        public static bool Inside => Instance && Instance.m_Inside;

        NewModePlayer m_Player;
        Vector3 m_Center;
        float m_Half;
        bool m_Inside;
        Vector3 m_ReturnPos;
        float m_ReturnYaw;

        public static TrainingZone Create(NewModePlayer player, Vector3 center)
        {
            var go = new GameObject("Training Zone");
            var zone = go.AddComponent<TrainingZone>();
            zone.m_Player = player;
            zone.m_Center = center;
            zone.Build();
            Instance = zone;
            return zone;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        float S => m_Player ? m_Player.Scale : 1f;

        void Build()
        {
            float s = S;
            m_Half = 16f * s;
            transform.position = m_Center;

            var floorMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.32f, 0.46f, 0.3f) };
            var lineMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.24f, 0.34f, 0.22f) };
            var stoneMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.6f, 0.58f, 0.65f) };

            Box("Floor", new Vector3(0f, -0.5f * s, 0f), new Vector3(m_Half * 2f, 1f * s, m_Half * 2f), floorMat, true);
            // 바닥 줄무늬 (4칸마다)
            for (int i = -3; i <= 3; i++)
            {
                Box("Line", new Vector3(i * 4f * s, 0.005f, 0f), new Vector3(0.08f * s, 0.01f, m_Half * 2f), lineMat, false);
                Box("Line", new Vector3(0f, 0.005f, i * 4f * s), new Vector3(m_Half * 2f, 0.01f, 0.08f * s), lineMat, false);
            }
            // 테두리 낮은 돌담 + 보이지 않는 높은 벽 (떨어지지 않게)
            for (int side = 0; side < 4; side++)
            {
                Vector3 dir = Quaternion.Euler(0f, side * 90f, 0f) * Vector3.forward;
                Vector3 across = Quaternion.Euler(0f, side * 90f + 90f, 0f) * Vector3.forward;
                Vector3 size = new Vector3(Mathf.Abs(across.x) * m_Half * 2f + Mathf.Abs(dir.x) * 0.6f * s, 0.6f * s, Mathf.Abs(across.z) * m_Half * 2f + Mathf.Abs(dir.z) * 0.6f * s);
                Box("Rim", dir * m_Half + Vector3.up * 0.3f * s, size, stoneMat, true);
                var wall = new GameObject("Invisible Wall");
                wall.transform.SetParent(transform, false);
                wall.transform.localPosition = dir * (m_Half + 0.3f * s) + Vector3.up * 3f * s;
                wall.AddComponent<BoxCollider>().size = new Vector3(size.x, 6f * s, size.z);
            }

            // 앞줄 유령 5, 꼬마 유령 3, 붉은 유령 1, 보스 고래 하나 (모두 제자리 — 움직이거나 공격하지 않음)
            Mob(MobKind.Dummy, new Vector3(-6f, 0f, 6f));
            Mob(MobKind.Dummy, new Vector3(-3f, 0f, 6.5f));
            Mob(MobKind.Dummy, new Vector3(0f, 0f, 7f));
            Mob(MobKind.Dummy, new Vector3(3f, 0f, 6.5f));
            Mob(MobKind.Dummy, new Vector3(6f, 0f, 6f));
            Mob(MobKind.Hopper, new Vector3(-8f, 0f, -2f));
            Mob(MobKind.Hopper, new Vector3(-6f, 0f, -5f));
            Mob(MobKind.Hopper, new Vector3(-9f, 0f, -7f));
            Mob(MobKind.Brute, new Vector3(8f, 0f, -3f));
            Mob(MobKind.Boss, new Vector3(7.5f, 0f, -9f));

            // 표지판
            TMP_FontAsset font = MathModeAssets.Load() ? MathModeAssets.Load().Font : null;
            var sign = new GameObject("Sign");
            sign.transform.SetParent(transform, false);
            sign.transform.localPosition = new Vector3(0f, 4.5f * s, 13f * s);
            sign.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * Quaternion.Euler(0f, 180f, 0f);
            var tmp = sign.AddComponent<TextMeshPro>();
            if (font) tmp.font = font;
            tmp.text = "<b>훈련장</b>\n<size=50%>마음껏 연습하세요 · T: 모험으로 돌아가기</size>";
            tmp.fontSize = 10f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(1f, 0.85f, 0.35f);
            tmp.outlineWidth = 0.2f;
            tmp.outlineColor = new Color32(40, 25, 0, 255);
            tmp.rectTransform.sizeDelta = new Vector2(30f, 10f);
            sign.transform.localScale = Vector3.one * s;
            sign.transform.rotation = Quaternion.LookRotation(sign.transform.position - (m_Center + Vector3.back * 10f * s));

            QuizStation.Persistent("Portal blue", transform, new Vector3(0f, 1.2f * s, -12f * s), 0.9f * s);
            QuizStation.Persistent("Magic circle", transform, new Vector3(0f, 0.03f, -12f * s), 1.2f * s);
        }

        void Box(string name, Vector3 local, Vector3 size, Material mat, bool collider)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            if (!collider) Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localPosition = local;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        void Mob(MobKind kind, Vector3 local)
        {
            Vector3 p = m_Center + local * S;
            Vector3 look = (m_Center + Vector3.back * 12f * S) - p;
            look.y = 0f;
            var mob = TrainingDummy.Create(p, Quaternion.LookRotation(look), S, kind);
            mob.Hostile = false;
            mob.Respawns = true;
            mob.Stationary = true; // 훈련장 몹은 제자리에
        }

        // ------------------------------------------------------------------ 워프

        public static void Toggle()
        {
            if (Instance) Instance.Warp();
        }

        void Warp()
        {
            if (!m_Player || m_Player.Dead) return;
            Fx.Play("Teleport", m_Player.transform.position, Quaternion.identity, 0.5f * S, 0.2f, false);
            if (!m_Inside)
            {
                m_ReturnPos = m_Player.transform.position;
                m_ReturnYaw = m_Player.transform.eulerAngles.y;
                m_Player.Teleport(m_Center + new Vector3(0f, 0.1f, -10f) * S, 0f);
                m_Inside = true;
            }
            else
            {
                m_Player.Teleport(m_ReturnPos + Vector3.up * 0.1f * S, m_ReturnYaw);
                m_Inside = false;
            }
            Fx.Play("Teleport", m_Player.transform.position, Quaternion.identity, 0.5f * S, 0.2f, false);
        }

        void Update()
        {
            if (!m_Player) return;
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.tKey.wasPressedThisFrame && !m_Player.Paused && !m_Player.InputBlocked && !m_Player.Dead)
                Warp();
            // 훈련장에서 혹시 떨어지면 가운데로
            if (m_Inside && m_Player.transform.position.y < m_Center.y - 4f * S)
                m_Player.Teleport(m_Center + new Vector3(0f, 0.1f, -10f) * S, 0f);
        }
    }
}
