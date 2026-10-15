using System.Collections.Generic;
using TMPro;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace Unity.FPS.MathMode
{
    // 수학 관문: 단계 색 지붕 + 칠판(그림) + 문제 + 보기 4개의 표적.
    // 정답 표적을 쏘면 돌파, 오답을 쏘면 (보통 모드) 적이 출현 / (쉬운 모드) 힌트가 나온다.
    public class MathStation : MonoBehaviour
    {
        static readonly Color k_PanelColor = new Color(0.07f, 0.09f, 0.15f);
        static readonly Color k_BoardGreen = new Color(0.11f, 0.24f, 0.19f);
        static readonly Color k_Wood = new Color(0.55f, 0.36f, 0.2f);
        static readonly Color k_Stone = new Color(0.72f, 0.72f, 0.76f);
        static readonly Color k_CorrectColor = new Color(0.2f, 0.9f, 0.35f);
        static readonly Color k_WrongColor = new Color(0.9f, 0.2f, 0.2f);
        static readonly Color k_DoneColor = new Color(0.3f, 0.3f, 0.3f);

        public char Letter { get; private set; }
        public int Tier { get; private set; }
        public bool IsSolved { get; private set; }
        public Vector2 PlanePosition => m_Manager.World.ToPlane(transform.position);

        MathModeManager m_Manager;
        MathProblem m_Problem;
        TextMeshPro m_StatusText;
        readonly List<GameObject> m_Boards = new List<GameObject>();
        float m_LockedUntil;

        public void Init(MathModeManager manager, char letter, int tier)
        {
            m_Manager = manager;
            Letter = letter;
            Tier = tier;
            m_Problem = MathProblemGenerator.Generate(tier);
            Color tierColor = MathProblemGenerator.TierColors[MathProblemGenerator.ClampTier(tier)];
            string tierName = MathProblemGenerator.TierNames[MathProblemGenerator.ClampTier(tier)];

            // 받침: 둥근 돌 바닥 + 단계 색 테두리
            manager.CreateBlock(transform, PrimitiveType.Cylinder, new Vector3(0f, 0.06f, 0f), new Vector3(10.5f, 0.06f, 3.4f), k_Stone);
            manager.CreateBlock(transform, PrimitiveType.Cylinder, new Vector3(0f, 0.05f, 0f), new Vector3(11f, 0.05f, 3.8f), tierColor * 1.3f);

            // 양쪽 기둥 + 지붕 (단계 색)
            foreach (float x in new[] { -5.4f, 5.4f })
            {
                manager.CreateBlock(transform, PrimitiveType.Cube, new Vector3(x, 2.9f, 0.1f), new Vector3(0.35f, 5.8f, 0.35f), k_Wood);
                manager.CreateBlock(transform, PrimitiveType.Sphere, new Vector3(x, 5.95f, 0.1f), Vector3.one * 0.55f, tierColor * 1.5f);
            }

            manager.CreateBlock(transform, PrimitiveType.Cube, new Vector3(0f, 5.65f, 0.1f), new Vector3(11.3f, 0.4f, 0.6f), tierColor);
            manager.CreateText(transform, new Vector3(0f, 5.65f, -0.22f), new Vector2(10f, 0.36f),
                $"<b>관문 {letter}  ·  {tierName}</b>", Color.white, 2.2f).outlineWidth = 0.2f;

            // 문제 패널
            manager.CreateBlock(transform, PrimitiveType.Cube, new Vector3(0f, 3.85f, 0.08f), new Vector3(10.6f, 2.9f, 0.1f), k_PanelColor);

            // 칠판(그림) — 나무 테두리 + 초록 칠판
            Vector3 boardCenter = new Vector3(-3.55f, 3.85f, 0f);
            manager.CreateBlock(transform, PrimitiveType.Cube, boardCenter + new Vector3(0f, 0f, 0.02f), new Vector3(2.75f, 2.75f, 0.06f), k_Wood);
            manager.CreateBlock(transform, PrimitiveType.Cube, boardCenter + new Vector3(0f, 0f, -0.01f), new Vector3(2.5f, 2.5f, 0.04f), k_BoardGreen);
            if (m_Problem.Figure != null)
                DrawFigure(m_Problem.Figure, boardCenter + new Vector3(0f, 0f, -0.04f), 1.15f);

            Vector2 p = PlanePosition;
            manager.CreateText(transform, new Vector3(1.4f, 4.95f, -0.02f), new Vector2(6.4f, 0.35f),
                $"<color=#9FB4C8>위치 {MathProblemGenerator.Pt(p.x, p.y)}</color>", Color.white, 1.4f);
            manager.CreateText(transform, new Vector3(1.4f, 3.95f, -0.02f), new Vector2(6.4f, 1.5f),
                m_Problem.Question, Color.white, 3f);
            m_StatusText = manager.CreateText(transform, new Vector3(1.4f, 2.8f, -0.02f), new Vector2(6.4f, 0.55f),
                "정답이라고 생각하는 표적을 쏘세요!", new Color(0.7f, 0.85f, 1f), 1.8f);

            // 보기 표적 4개: 어두운 테두리 + 단계 색 판
            Color face = Color.Lerp(tierColor, new Color(0.15f, 0.25f, 0.6f), 0.45f);
            for (int i = 0; i < m_Problem.Choices.Length; i++)
            {
                float x = -3.6f + i * 2.4f;
                manager.CreateBlock(transform, PrimitiveType.Cube, new Vector3(x, 1.4f, 0.06f), new Vector3(2.2f, 1.4f, 0.12f), new Color(0.05f, 0.06f, 0.1f));
                GameObject board = manager.CreateBlock(transform, PrimitiveType.Cube, new Vector3(x, 1.4f, 0f),
                    new Vector3(2f, 1.2f, 0.2f), face, keepCollider: true);
                board.name = $"Answer_{i + 1}";

                var health = board.AddComponent<Health>();
                health.MaxHealth = 100000f;
                board.AddComponent<Damageable>();
                board.AddComponent<MathAnswerTarget>().Init(this, i, health);

                var label = manager.CreateText(transform, new Vector3(x, 1.4f, -0.12f), new Vector2(1.85f, 1.05f),
                    $"<b>{m_Problem.Choices[i]}</b>", Color.white, 3f);
                label.outlineWidth = 0.2f;
                label.outlineColor = Color.black;
                m_Boards.Add(board);
            }
        }

        // 그림을 칠판 위에 선(LineRenderer)·점·글씨로 그린다. 좌표는 [-1, 1] × scale.
        void DrawFigure(MathFigure figure, Vector3 center, float scale)
        {
            var root = new GameObject("Figure").transform;
            root.SetParent(transform, false);
            root.localPosition = center;

            foreach (var (points, color, width, loop) in figure.Lines)
            {
                LineRenderer line = m_Manager.CreateLine(root, color, width);
                line.useWorldSpace = false;
                line.loop = loop;
                line.material.SetColor("_BaseColor", Color.white); // 색은 정점 색으로만
                line.startColor = line.endColor = color * 1.4f;     // 살짝 빛나는 분필 느낌
                line.positionCount = points.Length;
                for (int i = 0; i < points.Length; i++)
                    line.SetPosition(i, new Vector3(points[i].x * scale, points[i].y * scale, 0f));
            }

            foreach (var (pos, color) in figure.Dots)
                m_Manager.CreateBlock(root, PrimitiveType.Sphere, new Vector3(pos.x * scale, pos.y * scale, -0.01f), Vector3.one * 0.12f, color * 1.4f);

            foreach (var (pos, text, color) in figure.Labels)
                m_Manager.CreateText(root, new Vector3(pos.x * scale, pos.y * scale, -0.02f), new Vector2(0.7f, 0.28f), text, color, 1.4f);
        }

        void Update()
        {
            // 플레이어가 가까우면 패널이 플레이어 쪽을 바라보도록 천천히 회전
            if (!m_Manager || !m_Manager.Player) return;
            Vector3 toStation = transform.position - m_Manager.Player.position;
            toStation.y = 0f;
            if (toStation.sqrMagnitude < 0.01f || toStation.sqrMagnitude > 40f * 40f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toStation),
                90f * Time.deltaTime);
        }

        public void OnAnswerShot(int index, GameObject board)
        {
            if (IsSolved || Time.time < m_LockedUntil) return;
            m_LockedUntil = Time.time + 0.3f; // 폭발 피해로 여러 표적이 동시에 맞는 경우 방지

            if (index == m_Problem.CorrectIndex)
            {
                IsSolved = true;
                m_Manager.SetColor(board.GetComponent<Renderer>(), k_CorrectColor);
                m_StatusText.text = $"<color=#6BFF8A>정답!</color>  {m_Problem.Explanation}";
                MathModeManager.ShowMessage($"관문 {Letter} 돌파! 정답: {m_Problem.Choices[index]}");

                foreach (GameObject b in m_Boards)
                {
                    b.GetComponent<Collider>().enabled = false;
                    if (b != board) m_Manager.SetColor(b.GetComponent<Renderer>(), k_DoneColor);
                }

                m_Manager.OnStationSolved(this);
            }
            else
            {
                m_Manager.SetColor(board.GetComponent<Renderer>(), k_WrongColor);
                board.GetComponent<Collider>().enabled = false;
                m_StatusText.text = m_Manager.ShowHints
                    ? $"<color=#FF6B6B>오답!</color>  힌트: {m_Problem.Explanation}"
                    : $"<color=#FF6B6B>오답: {m_Problem.Choices[index]}</color>  다시 생각해 보세요!";
                m_Manager.OnStationWrong(this);
            }
        }
    }

    public class MathAnswerTarget : MonoBehaviour
    {
        MathStation m_Station;
        int m_Index;

        public void Init(MathStation station, int index, Health health)
        {
            m_Station = station;
            m_Index = index;
            health.OnDamaged += OnDamaged;
        }

        void OnDamaged(float damage, GameObject source)
        {
            // 적의 총알은 무시하고 플레이어의 공격만 답으로 인정한다
            if (!source || !source.GetComponentInParent<PlayerCharacterController>()) return;
            m_Station.OnAnswerShot(m_Index, gameObject);
        }
    }
}
