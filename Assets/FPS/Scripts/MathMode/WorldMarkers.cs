using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Unity.FPS.MathMode
{
    // 벽 너머로도 보이는 위치 마커 (◆ + 이름 + 거리).
    // 무기 카메라는 다른 물체에 가려지지 않고 맨 위에 그려지므로, 마커를 무기 레이어에 두면 항상 보인다.
    // 거리에 비례해 크기를 키워 화면에서 늘 비슷한 크기로 보이게 한다.
    public class WorldMarkers : MonoBehaviour
    {
        class Marker
        {
            public Func<Vector3?> Position;
            public Func<string> Label;
            public Transform Root;
            public TextMeshPro Text;
            public float Size;
        }

        MathModeManager m_Manager;
        Camera m_Camera;
        int m_Layer;
        readonly List<Marker> m_Markers = new List<Marker>();

        public void Init(MathModeManager manager)
        {
            m_Manager = manager;
            m_Camera = manager.PlayerWeapons && manager.PlayerWeapons.WeaponCamera ? manager.PlayerWeapons.WeaponCamera : Camera.main;
            m_Layer = MathCombat.WeaponLayer(manager.PlayerWeapons);
            // 무기 카메라는 원래 10m까지만 그리므로 멀리 있는 마커도 보이도록 늘린다
            if (m_Camera) m_Camera.farClipPlane = Mathf.Max(m_Camera.farClipPlane, 500f);
        }

        // position이 null을 돌려주면 그 동안 마커를 숨긴다
        public void Add(Func<Vector3?> position, Color color, Func<string> label, float size = 1f)
        {
            var root = new GameObject("Marker").transform;
            root.SetParent(transform, false);

            // ◆: 카메라를 향한 정사각형을 45° 돌린 모양 + 검은 테두리
            var outline = m_Manager.CreateBlock(root, PrimitiveType.Cube, Vector3.zero, new Vector3(0.62f, 0.62f, 0.02f), Color.black);
            outline.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var diamond = m_Manager.CreateBlock(root, PrimitiveType.Cube, new Vector3(0f, 0f, -0.02f), new Vector3(0.48f, 0.48f, 0.02f), color);
            diamond.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            TextMeshPro text = m_Manager.CreateText(root, new Vector3(0f, 0.75f, -0.02f), new Vector2(4f, 0.6f), "", color, 3f);
            text.outlineWidth = 0.25f;
            text.outlineColor = Color.black;

            MathCombat.SetLayerRecursively(root.gameObject, m_Layer);
            m_Markers.Add(new Marker { Position = position, Label = label, Root = root, Text = text, Size = size });
        }

        void LateUpdate()
        {
            if (!m_Camera) return;
            Vector3 eye = m_Camera.transform.position;

            foreach (Marker marker in m_Markers)
            {
                Vector3? p = marker.Position();
                bool visible = p.HasValue;
                if (marker.Root.gameObject.activeSelf != visible) marker.Root.gameObject.SetActive(visible);
                if (!visible) continue;

                Vector3 pos = p.Value;
                float distance = Vector3.Distance(eye, pos);
                marker.Root.position = pos;
                marker.Root.rotation = Quaternion.LookRotation(pos - eye);
                marker.Root.localScale = Vector3.one * (Mathf.Clamp(distance * 0.045f, 0.6f, 8f) * marker.Size);
                marker.Text.text = $"{marker.Label()}  {distance:0}m";
            }
        }
    }
}
