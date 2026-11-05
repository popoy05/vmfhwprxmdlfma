using UnityEngine;
using UnityEngine.UI;

namespace Unity.FPS.NewMode
{
    // 총 조준: 우클릭을 누르고 있으면 어깨 너머로 확대(NewModePlayer), 화면 가운데 조준선을 향해 쏜다.
    //  조준선은 총을 들었을 때만 보이고, 쏘면 벌어졌다 모이며, 허수아비 위에 있으면 빨갛게 바뀐다.
    public partial class NewModeCombat
    {
        RectTransform m_Crosshair;
        Image[] m_CrossBars;
        Image m_CrossDot;
        CanvasGroup m_CrossGroup;
        float m_CrosshairKick;

        /// <summary>총구에서 조준점(화면 가운데가 가리키는 곳)으로 가는 방향</summary>
        Vector3 AimDirection(Vector3 from)
        {
            Vector3 flat = m_Frame * Vector3.forward;
            if (!m_Player || !m_Player.Camera) return flat;

            Ray ray = m_Player.AimRay;
            float max = 150f * S;
            float dist = max;
            if (Physics.Raycast(ray, out RaycastHit hit, max, ~(1 << 2), QueryTriggerInteraction.Ignore)) dist = hit.distance;
            TrainingDummy target = DummyOnRay(ray, dist, out float along);
            if (target) dist = along;

            Vector3 point = ray.GetPoint(dist);
            Vector3 dir = point - from;
            // 조준점이 총구 뒤쪽이거나 너무 가까우면 그냥 앞으로
            if (dir.sqrMagnitude < 0.25f * S * S || Vector3.Dot(dir, flat) <= 0f) return flat;
            return dir.normalized;
        }

        static TrainingDummy DummyOnRay(Ray ray, float maxDist, out float along)
        {
            TrainingDummy best = null;
            along = maxDist;
            foreach (var d in TrainingDummy.All)
            {
                if (!d.Alive) continue;
                Vector3 to = d.Center - ray.origin;
                float a = Vector3.Dot(to, ray.direction);
                if (a <= 0f || a >= along) continue;
                Vector3 side = to - ray.direction * a;
                side.y *= 0.5f; // 위아래는 넉넉하게 (몸통이 길쭉함)
                if (side.magnitude <= 0.45f * d.Scale) { best = d; along = a; }
            }
            return best;
        }

        void BuildCrosshair()
        {
            m_Crosshair = new GameObject("Crosshair", typeof(RectTransform)).GetComponent<RectTransform>();
            m_Crosshair.SetParent(m_Canvas.transform, false);
            m_Crosshair.anchorMin = m_Crosshair.anchorMax = new Vector2(0.5f, 0.5f);
            m_Crosshair.sizeDelta = new Vector2(120f, 120f);
            m_CrossGroup = m_Crosshair.gameObject.AddComponent<CanvasGroup>();
            m_CrossGroup.blocksRaycasts = false;
            m_CrossGroup.alpha = 0f;

            m_CrossBars = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                var bar = new GameObject("Bar", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                bar.SetParent(m_Crosshair, false);
                bar.sizeDelta = i % 2 == 0 ? new Vector2(4f, 16f) : new Vector2(16f, 4f);
                var img = bar.GetComponent<Image>();
                img.raycastTarget = false;
                var outline = bar.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.6f);
                m_CrossBars[i] = img;
            }
            var dot = new GameObject("Dot", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            dot.SetParent(m_Crosshair, false);
            dot.sizeDelta = new Vector2(5f, 5f);
            m_CrossDot = dot.GetComponent<Image>();
            m_CrossDot.raycastTarget = false;
            dot.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.6f);
        }

        void UpdateCrosshair()
        {
            if (!m_Canvas) return;
            if (!m_Crosshair) BuildCrosshair();

            bool gun = m_Weapon != null && m_Weapon.Kind == WeaponKind.Gun && !m_InCutscene;
            m_CrossGroup.alpha = Mathf.MoveTowards(m_CrossGroup.alpha, gun ? 1f : 0f, Time.unscaledDeltaTime * 8f);
            if (m_CrossGroup.alpha <= 0f) return;

            m_CrosshairKick = Mathf.MoveTowards(m_CrosshairKick, 0f, Time.unscaledDeltaTime * 3f);
            float aim = m_Player.AimBlend;
            float gap = Mathf.Lerp(16f, 7f, aim) + 26f * m_CrosshairKick;
            Vector2[] dirs = { Vector2.up, Vector2.right, Vector2.down, Vector2.left };

            bool onTarget = DummyOnRay(m_Player.AimRay, 150f * S, out _);
            Color color = onTarget ? new Color(1f, 0.3f, 0.25f) : Color.Lerp(Color.white, C, 0.35f);
            for (int i = 0; i < 4; i++)
            {
                m_CrossBars[i].rectTransform.anchoredPosition = dirs[i] * (gap + 8f);
                m_CrossBars[i].color = color;
            }
            m_CrossDot.color = color;
            m_Crosshair.localScale = Vector3.one * Mathf.Lerp(1f, 1.15f, aim);
        }
    }
}
