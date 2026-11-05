using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using Unity.FPS.MathMode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Unity.FPS.NewMode
{
    // 새 모드 → 캐릭터 꾸미기 화면 (직업 고르기 → 중세 아레나 모험 시작).
    // 왼쪽: 부위별 ◀ ▶ 선택 / 오른쪽: 캐릭터 미리보기 (마우스로 끌어서 돌리기)
    public class CharacterCustomizeScreen : MonoBehaviour
    {
        // 캐릭터가 화면 오른쪽(왼쪽 패널 옆)에 오도록 카메라를 옆으로 비켜 둔다
        static readonly Vector3 k_CameraPosition = new Vector3(0.85f, 1.2f, 4.9f);
        static readonly Vector3 k_CameraLookAt = new Vector3(0.85f, 0.9f, 0f);

        CharacterParts m_Parts;
        TMP_FontAsset m_Font;
        Transform m_Turntable;
        GameObject m_Model;
        readonly List<(CharacterParts.Slot slot, TextMeshProUGUI value)> m_Rows = new List<(CharacterParts.Slot, TextMeshProUGUI)>();
        float m_Yaw = -20f; // 카메라 쪽(정면)에서 살짝 비스듬히
        bool m_Dragging;

        void Start()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            UIUtil.EnsureEventSystem();
            m_Parts = CharacterParts.Load();
            m_Font = MathModeAssets.Load() ? MathModeAssets.Load().Font : null;

            BuildStage();
            BuildUI();
            Refresh();
        }

        // ---------------------------------------------------------------- 무대 (카메라·조명·바닥·캐릭터)

        void BuildStage()
        {
            Camera cam = Camera.main;
            if (!cam) cam = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            cam.transform.position = k_CameraPosition;
            cam.transform.LookAt(k_CameraLookAt);
            cam.fieldOfView = 35f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.55f, 0.72f, 0.9f);

            if (!FindAnyObjectByType<Light>())
            {
                var sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1.3f;
                sun.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(40f, 150f, 0f);
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.85f, 0.9f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.7f, 0.72f, 0.75f);
            RenderSettings.ambientGroundColor = new Color(0.45f, 0.45f, 0.5f);

            // 둥근 받침대
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            floor.name = "Pedestal";
            floor.transform.position = new Vector3(0f, -0.05f, 0f);
            floor.transform.localScale = new Vector3(1.8f, 0.05f, 1.8f);
            var floorRenderer = floor.GetComponent<Renderer>();
            floorRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.95f, 0.85f, 0.55f) };

            m_Turntable = new GameObject("Turntable").transform;
            m_Model = PlayerAppearance.Spawn(m_Turntable);
            m_Turntable.rotation = Quaternion.Euler(0f, m_Yaw, 0f);

            // 고른 무기를 손에 쥐여 준다 (전투 없이 들고 있는 자세만)
            Animator animator = m_Model ? m_Model.GetComponentInChildren<Animator>() : null;
            if (animator)
            {
                m_Rig = animator.gameObject.AddComponent<WeaponRig>();
                m_Rig.Init(m_Turntable, 1f, null);
                m_Rig.Equip(WeaponCatalog.Selected);
            }

            // 등장: 받침대 위로 통 튀어 오른다
            m_Turntable.localScale = Vector3.one * 0.6f;
            m_Turntable.DOScale(1f, 0.6f).SetEase(Ease.OutBack).SetLink(m_Turntable.gameObject);
        }

        WeaponRig m_Rig;
        TextMeshProUGUI m_WeaponValue;

        void CycleWeapon(int direction)
        {
            int count = WeaponCatalog.All.Length;
            WeaponCatalog.Selected = (WeaponCatalog.Selected + direction + count) % count;
            if (m_Rig) m_Rig.Equip(WeaponCatalog.Selected);
            RefreshWeapon();
            UIAnim.Punch(m_WeaponValue.transform, 0.25f);
            m_Turntable.DOKill(true);
            m_Turntable.DOPunchScale(Vector3.one * 0.05f, 0.3f, 6, 0.5f).SetLink(m_Turntable.gameObject);
        }

        void RefreshWeapon()
        {
            if (!m_WeaponValue) return;
            WeaponCatalog.Info info = WeaponCatalog.All[WeaponCatalog.Selected];
            m_WeaponValue.text = $"<b><color=#{ColorUtility.ToHtmlStringRGB(info.Color)}>{info.ClassName}</color></b> <size=60%><color=#AAB4C8>{info.Name} · {info.Desc}</color></size>";
            m_WeaponValue.enableAutoSizing = true;
            m_WeaponValue.fontSizeMin = 14f;
        }

        // ---------------------------------------------------------------- UI

        void BuildUI()
        {
            Canvas canvas = UIUtil.Canvas(transform, "CustomizeUI", 10);

            var side = UIUtil.Image(canvas.transform, "Side", new Color(0.02f, 0.04f, 0.1f, 0.78f));
            side.anchorMin = new Vector2(0f, 0f);
            side.anchorMax = new Vector2(0f, 1f);
            side.pivot = new Vector2(0f, 0.5f);
            side.sizeDelta = new Vector2(680f, 0f);

            var title = UIUtil.Text(side, m_Font, "<b>캐릭터 꾸미기</b>", 64f, TextAlignmentOptions.Left);
            UIUtil.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(350f, -80f), new Vector2(600f, 90f));
            title.color = new Color(1f, 0.84f, 0.3f);
            title.outlineWidth = 0.15f;
            title.outlineColor = new Color(0.4f, 0.25f, 0f);

            if (m_Parts == null || m_Parts.Slots.Length == 0)
            {
                var error = UIUtil.Text(side, m_Font, "캐릭터 부품을 찾을 수 없어요.\n(Tools > New Mode > Rebuild Character Parts)", 26f, TextAlignmentOptions.Left);
                UIUtil.Place(error.rectTransform, new Vector2(0f, 1f), new Vector2(350f, -220f), new Vector2(600f, 120f));
            }
            else
            {
                // 맨 위: 직업(무기) — 모험을 시작하면 바꿀 수 없다
                // 고를 게 하나뿐인 슬롯(예: 몸)은 보여주지 않는다
                var shown = System.Array.FindAll(m_Parts.Slots, s => s.AllowNone || s.Options.Length > 1);
                float y = -165f;
                float step = Mathf.Min(68f, 780f / Mathf.Max(1, shown.Length + 1));
                m_WeaponValue = Selector(side, y, step - 6f, "직업", () => CycleWeapon(-1), () => CycleWeapon(1), new Color(1f, 0.84f, 0.3f), 0);
                RefreshWeapon();
                y -= step;
                int row = 1;
                foreach (var slot in shown)
                {
                    var s = slot;
                    m_Rows.Add((slot, Selector(side, y, step - 6f, slot.Label, () => Cycle(s, -1), () => Cycle(s, 1), new Color(0.7f, 0.8f, 0.95f), row++)));
                    y -= step;
                }
            }

            // 등장 애니메이션 (DOTween): 왼쪽 판이 미끄러져 들어오고, 제목이 튀고, 다음 버튼이 통 나타난다
            UIAnim.SlideIn(side, new Vector2(-700f, 0f), 0f, 0.5f);
            UIAnim.PopIn(title.transform, 0.25f);

            var random = UIUtil.Button(side, m_Font, "랜덤", new Color(0.55f, 0.35f, 0.75f), Randomize, new Vector2(290f, 70f), 30f);
            UIUtil.Place((RectTransform)random.transform, new Vector2(0f, 0f), new Vector2(195f, 70f), new Vector2(290f, 70f));
            var reset = UIUtil.Button(side, m_Font, "초기화", new Color(0.4f, 0.42f, 0.5f), ResetAll, new Vector2(290f, 70f), 30f);
            UIUtil.Place((RectTransform)reset.transform, new Vector2(0f, 0f), new Vector2(505f, 70f), new Vector2(290f, 70f));

            var next = UIUtil.Button(canvas.transform, m_Font, "<b>모험 시작  ▶</b>", new Color(0.2f, 0.75f, 0.4f), StartAdventure, new Vector2(420f, 100f), 44f);
            UIUtil.Place((RectTransform)next.transform, new Vector2(1f, 0f), new Vector2(-260f, 90f), new Vector2(420f, 100f));
            UIAnim.PopIn(next.transform, 0.5f);
            // 다음 버튼이 은은하게 숨 쉬듯 커졌다 작아졌다
            next.transform.GetChild(0).DOScale(1.06f, 0.8f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetLink(next.gameObject);

            var back = UIUtil.Button(canvas.transform, m_Font, "◀ 모드 선택", new Color(0.25f, 0.35f, 0.55f), GoBack, new Vector2(240f, 60f), 26f);
            UIUtil.Place((RectTransform)back.transform, new Vector2(1f, 1f), new Vector2(-150f, -50f), new Vector2(240f, 60f));

            var hint = UIUtil.Text(canvas.transform, m_Font, "직업은 모험 중에 바꿀 수 없어요 · 마우스로 끌어서 돌리기 · Enter: 모험 시작 · Esc: 뒤로", 22f, TextAlignmentOptions.Center);
            UIUtil.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(120f, 30f), new Vector2(900f, 36f));
            hint.color = new Color(1f, 1f, 1f, 0.7f);
            hint.outlineWidth = 0.2f;
            hint.outlineColor = new Color(0f, 0f, 0f, 0.6f);
        }

        TextMeshProUGUI Selector(RectTransform parent, float y, float height, string label,
            UnityEngine.Events.UnityAction prev, UnityEngine.Events.UnityAction next, Color labelColor, int index)
        {
            var row = UIUtil.RoundedImage(parent, label, new Color(1f, 1f, 1f, index == 0 ? 0.14f : 0.08f));
            UIUtil.Place(row, new Vector2(0f, 1f), new Vector2(350f, y), new Vector2(600f, height));

            var name = UIUtil.Text(row, m_Font, index == 0 ? $"<b>{label}</b>" : label, 26f, TextAlignmentOptions.Left);
            UIUtil.Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(85f, 0f), new Vector2(140f, height));
            name.color = labelColor;
            name.enableAutoSizing = true;
            name.fontSizeMin = 16f;
            name.fontSizeMax = 26f;

            float buttonSize = height - 6f;
            var left = UIUtil.Button(row, m_Font, "◀", new Color(0.25f, 0.35f, 0.55f), prev, new Vector2(buttonSize, buttonSize), 26f);
            UIUtil.Place((RectTransform)left.transform, new Vector2(0f, 0.5f), new Vector2(200f, 0f), new Vector2(buttonSize, buttonSize));
            var right = UIUtil.Button(row, m_Font, "▶", new Color(0.25f, 0.35f, 0.55f), next, new Vector2(buttonSize, buttonSize), 26f);
            UIUtil.Place((RectTransform)right.transform, new Vector2(1f, 0.5f), new Vector2(-40f, 0f), new Vector2(buttonSize, buttonSize));

            var value = UIUtil.Text(row, m_Font, "", 28f, TextAlignmentOptions.Center);
            UIUtil.Place(value.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(80f, 0f), new Vector2(300f, height));
            value.enableAutoSizing = true;
            value.fontSizeMin = 14f;
            value.fontSizeMax = 28f;

            UIAnim.SlideIn(row, new Vector2(-120f, 0f), 0.15f + 0.035f * index, 0.35f); // 한 줄씩 차례로
            return value;
        }

        // ---------------------------------------------------------------- 선택 변경

        void Cycle(CharacterParts.Slot slot, int direction)
        {
            int min = slot.AllowNone ? -1 : 0;
            int count = slot.Options.Length - min;
            int index = PlayerAppearance.Get(slot);
            index = ((index - min + direction) % count + count) % count + min;
            Select(slot, index);
        }

        void Select(CharacterParts.Slot slot, int index)
        {
            PlayerAppearance.Set(slot, index);

            // 옷을 하나라도 고르면 전신 코스튬은 벗는다 (코스튬을 고르면 다른 옷은 가려진다)
            if (!slot.FullBody && index >= 0 && slot.Id != "body" && slot.Id != "face")
                foreach (var other in m_Parts.Slots)
                    if (other.FullBody) PlayerAppearance.Set(other, -1);

            PlayerAppearance.Save();
            Refresh();
            foreach (var (s, value) in m_Rows)
                if (s == slot) UIAnim.Punch(value.transform, 0.25f);
            m_Turntable.DOKill(true);
            m_Turntable.DOPunchScale(Vector3.one * 0.04f, 0.25f, 6, 0.5f).SetLink(m_Turntable.gameObject);
        }

        void Randomize()
        {
            foreach (var slot in m_Parts.Slots)
            {
                int index;
                if (slot.FullBody) index = Random.value < 0.15f ? Random.Range(0, slot.Options.Length) : -1;
                else if (!slot.AllowNone) index = Random.Range(0, slot.Options.Length);
                else index = Random.value < 0.35f ? -1 : Random.Range(0, slot.Options.Length);
                PlayerAppearance.Set(slot, index);
            }

            PlayerAppearance.Save();
            Refresh();
            // 모든 줄이 차례로 콩콩 튄다 + 캐릭터가 한 바퀴
            for (int i = 0; i < m_Rows.Count; i++)
            {
                Transform t = m_Rows[i].value.transform;
                DOVirtual.DelayedCall(0.03f * i, () => UIAnim.Punch(t, 0.3f)).SetLink(t.gameObject);
            }
            DOTween.To(() => m_Yaw, v => m_Yaw = v, m_Yaw + 360f, 0.6f).SetEase(Ease.OutCubic).SetLink(m_Turntable.gameObject);
        }

        void ResetAll()
        {
            PlayerAppearance.ResetAll(m_Parts);
            PlayerAppearance.Save();
            Refresh();
        }

        void Refresh()
        {
            PlayerAppearance.Apply(m_Model);
            if (!m_Parts) return;

            bool costume = false;
            foreach (var slot in m_Parts.Slots)
                if (slot.FullBody && PlayerAppearance.Get(slot) >= 0) costume = true;

            foreach (var (slot, value) in m_Rows)
            {
                int index = PlayerAppearance.Get(slot);
                string text = index < 0 ? "<color=#8C96AA>없음</color>" : $"<b>{slot.Options[index].Label}</b>";
                bool hidden = costume && !slot.FullBody && slot.Id != "body" && slot.Id != "face";
                value.text = hidden ? "<color=#8C96AA>코스튬에 가려짐</color>" : text;
            }
        }

        // ---------------------------------------------------------------- 이동·입력

        // 맵은 중세 아레나 하나 — 바로 모험 시작
        void StartAdventure()
        {
            PlayerAppearance.Save();
            AppBootstrap.ClassicMenu = false;
            SceneManager.LoadScene(AppBootstrap.NewModeArenaScene);
        }

        void GoBack()
        {
            PlayerAppearance.Save();
            AppBootstrap.ClassicMenu = false;
            AppBootstrap.NewModeMapSelect = false;
            SceneManager.LoadScene(AppBootstrap.MenuScene);
        }

        void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) { StartAdventure(); return; }
                if (kb.escapeKey.wasPressedThisFrame) { GoBack(); return; }
                if (kb.qKey.isPressed || kb.leftArrowKey.isPressed) m_Yaw += 120f * Time.deltaTime;
                if (kb.eKey.isPressed || kb.rightArrowKey.isPressed) m_Yaw -= 120f * Time.deltaTime;
            }

            // 화면(버튼이 아닌 곳)을 끌어서 캐릭터 돌리기
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame)
                    m_Dragging = EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject();
                if (!mouse.leftButton.isPressed) m_Dragging = false;
                if (m_Dragging) m_Yaw -= mouse.delta.ReadValue().x * 0.4f;
            }

            if (m_Turntable) m_Turntable.rotation = Quaternion.Euler(0f, m_Yaw, 0f);
        }
    }
}
