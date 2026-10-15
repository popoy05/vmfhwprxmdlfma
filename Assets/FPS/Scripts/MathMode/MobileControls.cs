using System;
using System.Collections.Generic;
using TMPro;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Unity.FPS.MathMode
{
    // 모바일(터치) 전용 조작 화면.
    //  왼쪽 절반: 떠오르는 조이스틱(이동, 끝까지 밀면 달리기)   오른쪽: 끌어서 바라보기
    //  버튼: 발사(끌면 조준 이동), 점프, 조준, 장전, 무기 ◀▶, 단검, 스킬 Z X C V(쿨타임 표시), 궁극기 Σ(게이지), Ⅱ ? 지도
    // 모든 입력은 VirtualInput 으로 들어가 PC 키보드·마우스 입력과 똑같이 처리된다.
    public class MobileControls : MonoBehaviour
    {
        MathModeManager m_Manager;
        Canvas m_Canvas;
        TMP_FontAsset m_Font;
        Unity.FPS.UI.InGameMenuManager m_PauseMenu;
        readonly List<(MathSkills.Skill skill, Image cover)> m_SkillCovers = new List<(MathSkills.Skill, Image)>();
        Image m_UltFill;
        Image m_UltRing;

        public void Init(MathModeManager manager)
        {
            m_Manager = manager;
            m_Font = manager.Font;
            m_PauseMenu = FindAnyObjectByType<Unity.FPS.UI.InGameMenuManager>();
            VirtualInput.Active = true;
            UIUtil.EnsureEventSystem();

            // 손가락 드래그가 마우스 시선 입력(<Pointer>/delta)으로도 들어가지 않도록 마우스 전용으로 바꾼다
            InputAction look = InputSystem.actions?.FindAction("Player/Look");
            if (look != null)
                for (int i = 0; i < look.bindings.Count; i++)
                    if (look.bindings[i].path == "<Pointer>/delta")
                        look.ApplyBindingOverride(i, "<Mouse>/delta");

            Build();
        }

        void OnDestroy()
        {
            VirtualInput.ResetAll();
        }

        void Build()
        {
            m_Canvas = UIUtil.Canvas(transform, "MobileControls", 15);
            Transform c = m_Canvas.transform;

            // 바라보기 영역 (오른쪽 55%) — 버튼보다 먼저 만들어 버튼 뒤에 깔리게
            var lookArea = UIUtil.Image(c, "LookArea", new Color(0f, 0f, 0f, 0.001f));
            lookArea.anchorMin = new Vector2(0.45f, 0f);
            lookArea.anchorMax = new Vector2(1f, 1f);
            lookArea.offsetMin = lookArea.offsetMax = Vector2.zero;
            lookArea.GetComponent<Image>().raycastTarget = true;
            lookArea.gameObject.AddComponent<TouchLookArea>();

            // 이동 조이스틱 (왼쪽 45%)
            var moveArea = UIUtil.Image(c, "MoveArea", new Color(0f, 0f, 0f, 0.001f));
            moveArea.anchorMin = Vector2.zero;
            moveArea.anchorMax = new Vector2(0.45f, 0.85f);
            moveArea.offsetMin = moveArea.offsetMax = Vector2.zero;
            moveArea.GetComponent<Image>().raycastTarget = true;
            var joyBase = UIUtil.RoundedImage(moveArea, "Base", new Color(1f, 1f, 1f, 0.18f), circle: true);
            var joyKnob = UIUtil.RoundedImage(joyBase, "Knob", new Color(1f, 1f, 1f, 0.55f), circle: true);
            joyBase.sizeDelta = new Vector2(260f, 260f);
            joyKnob.sizeDelta = new Vector2(120f, 120f);
            moveArea.gameObject.AddComponent<TouchJoystick>().Init(joyBase, joyKnob);

            Vector2 br = new Vector2(1f, 0f);
            // 발사 (끌면 바라보기도 된다)
            var fire = RoundButton(c, br, new Vector2(-200f, 200f), 230f, "발사", new Color(0.9f, 0.25f, 0.25f, 0.75f), "fire", hold: true);
            fire.gameObject.AddComponent<TouchLookArea>().PassThrough = true;

            // 스킬: 발사 버튼 주위에 원호로
            MathSkills.Skill[] skills = m_Manager.Skills ? m_Manager.Skills.All : new MathSkills.Skill[0];
            Vector2[] arc = { new Vector2(-256f, 515f), new Vector2(-383f, 462f), new Vector2(-477f, 360f), new Vector2(-519f, 228f) };
            for (int i = 0; i < skills.Length && i < arc.Length; i++)
            {
                MathSkills.Skill skill = skills[i];
                Color col = skill.Color;
                col.a = 0.8f;
                RectTransform b = RoundButton(c, br, arc[i], 108f, $"<b>{skill.Key}</b>\n<size=60%>{skill.Name}</size>", col * 0.75f, skill.VirtualId, hold: false);
                var cover = UIUtil.RoundedImage(b, "Cooldown", new Color(0f, 0f, 0f, 0.6f), circle: true).GetComponent<Image>();
                UIUtil.Stretch((RectTransform)cover.transform, 0f);
                cover.type = Image.Type.Filled;
                cover.fillMethod = Image.FillMethod.Radial360;
                cover.fillOrigin = (int)Image.Origin360.Top;
                cover.fillClockwise = false;
                m_SkillCovers.Add((skill, cover));
            }

            RoundButton(c, br, new Vector2(-470f, 90f), 100f, "조준", new Color(0.3f, 0.4f, 0.6f, 0.7f), "aim", hold: true);
            RoundButton(c, br, new Vector2(-70f, 410f), 120f, "점프", new Color(0.3f, 0.55f, 0.9f, 0.75f), "jump", hold: true);
            RoundButton(c, br, new Vector2(-70f, 545f), 95f, "장전", new Color(0.35f, 0.4f, 0.5f, 0.7f), "reload", hold: false);
            RoundButton(c, br, new Vector2(-640f, 110f), 100f, "단검", new Color(0.4f, 0.6f, 0.75f, 0.75f), "dagger", hold: false);
            RoundButton(c, br, new Vector2(-330f, 650f), 90f, "◀", new Color(0.3f, 0.35f, 0.45f, 0.7f), "prevWeapon", hold: false);
            RoundButton(c, br, new Vector2(-200f, 650f), 90f, "▶", new Color(0.3f, 0.35f, 0.45f, 0.7f), "nextWeapon", hold: false);
            var weaponLabel = UIUtil.Text(c, m_Font, "무기", 22f, TextAlignmentOptions.Center);
            UIUtil.Place(weaponLabel.rectTransform, br, new Vector2(-265f, 710f), new Vector2(120f, 30f));

            // 궁극기
            RectTransform ult = RoundButton(c, br, new Vector2(-690f, 270f), 140f, "<b>Σ</b>", new Color(0.35f, 0.25f, 0.05f, 0.85f), "ultimate", hold: false);
            m_UltFill = UIUtil.RoundedImage(ult, "Charge", new Color(1f, 0.75f, 0.2f, 0.65f), circle: true).GetComponent<Image>();
            UIUtil.Stretch((RectTransform)m_UltFill.transform, 0f);
            m_UltFill.type = Image.Type.Filled;
            m_UltFill.fillMethod = Image.FillMethod.Vertical;
            m_UltFill.transform.SetAsFirstSibling();
            ult.GetComponentInChildren<TextMeshProUGUI>().transform.SetAsLastSibling();
            m_UltRing = ult.GetComponent<Image>();

            // 위쪽 오른쪽: 일시정지 / 도움말 / 지도
            Vector2 tr = new Vector2(1f, 1f);
            RoundButton(c, tr, new Vector2(-70f, -60f), 84f, "||", new Color(0.2f, 0.2f, 0.25f, 0.8f), null, hold: false,
                onTap: () => { if (m_PauseMenu) m_PauseMenu.TogglePauseMenu(); });
            RoundButton(c, tr, new Vector2(-170f, -60f), 84f, "?", new Color(0.2f, 0.2f, 0.25f, 0.8f), "help", hold: false);
            RoundButton(c, tr, new Vector2(-270f, -60f), 84f, "지도", new Color(0.2f, 0.2f, 0.25f, 0.8f), "map", hold: false);
        }

        RectTransform RoundButton(Transform parent, Vector2 anchor, Vector2 pos, float size, string label, Color color,
            string virtualId, bool hold, Action onTap = null)
        {
            RectTransform rt = UIUtil.RoundedImage(parent, label, color, circle: true);
            UIUtil.Place(rt, anchor, pos, new Vector2(size, size));
            rt.GetComponent<Image>().raycastTarget = true;
            var outline = rt.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.35f);
            outline.effectDistance = new Vector2(2f, -2f);

            var text = UIUtil.Text(rt, m_Font, label, size * 0.28f, TextAlignmentOptions.Center);
            UIUtil.Stretch(text.rectTransform, 6f);
            text.enableAutoSizing = true;
            text.fontSizeMin = 10f;
            text.fontSizeMax = size * 0.3f;

            var touch = rt.gameObject.AddComponent<TouchButton>();
            touch.VirtualId = virtualId;
            touch.Hold = hold;
            touch.OnTap = onTap;
            return rt;
        }

        void Update()
        {
            // 일시정지 메뉴·브리핑·궁극기 컷신 중에는 터치 버튼을 숨겨서 그 화면의 버튼이 눌리게 한다
            bool hide = Time.timeScale <= 0.001f || (m_PauseMenu && m_PauseMenu.IsOpen) ||
                        (m_Manager.Ultimate && m_Manager.Ultimate.IsPlaying);
            if (m_Canvas.enabled == hide)
            {
                m_Canvas.enabled = !hide;
                if (hide) VirtualInput.ResetAll();
            }

            foreach (var (skill, cover) in m_SkillCovers)
                cover.fillAmount = skill.Cooldown > 0f ? skill.Remaining / skill.Cooldown : 0f;

            if (m_Manager.Ultimate)
            {
                m_UltFill.fillAmount = m_Manager.Ultimate.Charge01;
                float pulse = m_Manager.Ultimate.IsReady ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f) : 0f;
                m_UltRing.color = Color.Lerp(new Color(0.35f, 0.25f, 0.05f, 0.85f), new Color(1f, 0.8f, 0.2f, 0.95f), pulse);
            }
        }
    }

    // 누르고 있는 동안 / 한 번 탭
    public class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public string VirtualId;
        public bool Hold;
        public Action OnTap;
        Vector3 m_Scale = Vector3.one;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!string.IsNullOrEmpty(VirtualId)) VirtualInput.Press(VirtualId);
            OnTap?.Invoke();
            transform.localScale = m_Scale * 0.9f;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!string.IsNullOrEmpty(VirtualId)) VirtualInput.Release(VirtualId);
            transform.localScale = m_Scale;
        }

        void OnDisable()
        {
            if (!string.IsNullOrEmpty(VirtualId)) VirtualInput.Release(VirtualId);
            transform.localScale = m_Scale;
        }
    }

    // 끌어서 바라보기 (PassThrough: 발사 버튼처럼 누름과 동시에 끌기도 받는 경우)
    public class TouchLookArea : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public bool PassThrough;
        int m_PointerId = int.MinValue;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (m_PointerId == int.MinValue) m_PointerId = eventData.pointerId;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != m_PointerId) return;
            // 위로 끌면 위를 보도록 (마우스 바인딩이 y를 뒤집는 것과 맞춘다)
            VirtualInput.AddLook(new Vector2(eventData.delta.x, -eventData.delta.y));
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == m_PointerId) m_PointerId = int.MinValue;
        }

        void OnDisable() => m_PointerId = int.MinValue;
    }

    // 왼쪽 화면 어디든 누르면 그 자리에 생기는 조이스틱
    public class TouchJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        RectTransform m_Base, m_Knob, m_Area;
        Vector2 m_RestPos = new Vector2(260f, 260f);
        int m_PointerId = int.MinValue;
        const float k_Radius = 110f;

        public void Init(RectTransform joyBase, RectTransform knob)
        {
            m_Base = joyBase;
            m_Knob = knob;
            m_Area = (RectTransform)transform;
            m_Base.anchorMin = m_Base.anchorMax = Vector2.zero;
            m_Base.anchoredPosition = m_RestPos;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (m_PointerId != int.MinValue) return;
            m_PointerId = eventData.pointerId;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(m_Area, eventData.position, eventData.pressEventCamera, out Vector2 local))
                m_Base.anchoredPosition = local - m_Area.rect.min;
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != m_PointerId) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(m_Base, eventData.position, eventData.pressEventCamera, out Vector2 local))
                return;
            Vector2 offset = Vector2.ClampMagnitude(local, k_Radius);
            m_Knob.anchoredPosition = offset;
            VirtualInput.Move = offset / k_Radius;
            if (offset.magnitude >= k_Radius * 0.95f) VirtualInput.Press("sprint");
            else VirtualInput.Release("sprint");
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != m_PointerId) return;
            Reset();
        }

        void Reset()
        {
            m_PointerId = int.MinValue;
            if (m_Knob) m_Knob.anchoredPosition = Vector2.zero;
            if (m_Base) m_Base.anchoredPosition = m_RestPos;
            VirtualInput.Move = Vector2.zero;
            VirtualInput.Release("sprint");
        }

        void OnDisable() => Reset();
    }
}
