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
            // 왼쪽 꾸미기 판과 오른쪽 직업 카드 사이(화면 가운데)에 캐릭터가 오도록 카메라를 옆으로 민다
            {
                Vector3 model = new Vector3(0f, 0.9f, 0f); // 받침대 위 캐릭터 (원점)
                Vector3 vp = cam.WorldToViewportPoint(model);
                float width = 2f * vp.z * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * cam.aspect;
                cam.transform.position += cam.transform.right * (vp.x - 0.5f) * width;
            }
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
            RefreshClassCard();
            UIAnim.Punch(m_WeaponValue.transform, 0.25f);
            m_Turntable.DOKill(true);
            m_Turntable.DOPunchScale(Vector3.one * 0.05f, 0.3f, 6, 0.5f).SetLink(m_Turntable.gameObject);
        }

        void RefreshWeapon()
        {
            if (!m_WeaponValue) return;
            WeaponCatalog.Info info = WeaponCatalog.All[WeaponCatalog.Selected];
            m_WeaponValue.text = $"<b><color=#{ColorUtility.ToHtmlStringRGB(info.Color)}>{info.ClassName}</color></b> <size=60%><color=#C8C0B0>{info.Role}</color></size>";
            m_WeaponValue.enableAutoSizing = true;
            m_WeaponValue.fontSizeMin = 14f;
        }

        // ---------------------------------------------------------------- UI

        void BuildUI()
        {
            Canvas canvas = UIUtil.Canvas(transform, "CustomizeUI", 10);

            var side = UIUtil.Image(canvas.transform, "Side", Gs.Night);
            var sideEdge = UIUtil.Image(side, "Edge", new Color(Gs.Gold.r, Gs.Gold.g, Gs.Gold.b, 0.6f));
            sideEdge.anchorMin = new Vector2(1f, 0f); sideEdge.anchorMax = new Vector2(1f, 1f); sideEdge.pivot = new Vector2(1f, 0.5f);
            sideEdge.sizeDelta = new Vector2(3f, 0f);
            side.anchorMin = new Vector2(0f, 0f);
            side.anchorMax = new Vector2(0f, 1f);
            side.pivot = new Vector2(0f, 0.5f);
            side.sizeDelta = new Vector2(680f, 0f);

            var title = UIUtil.Text(side, m_Font, "<b>캐릭터 꾸미기</b>", 64f, TextAlignmentOptions.Left);
            UIUtil.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(350f, -80f), new Vector2(600f, 90f));
            title.color = Gs.GoldBright;
            title.outlineWidth = 0.12f;
            title.outlineColor = new Color32(40, 30, 10, 255);

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
                m_WeaponValue = Selector(side, y, step - 6f, "직업", () => CycleWeapon(-1), () => CycleWeapon(1), Gs.GoldBright, 0);
                RefreshWeapon();
                y -= step;
                int row = 1;
                foreach (var slot in shown)
                {
                    var s = slot;
                    m_Rows.Add((slot, Selector(side, y, step - 6f, slot.Label, () => Cycle(s, -1), () => Cycle(s, 1), Gs.Cream, row++)));
                    y -= step;
                }
            }

            // 등장 애니메이션 (DOTween): 왼쪽 판이 미끄러져 들어오고, 제목이 튀고, 다음 버튼이 통 나타난다
            UIAnim.SlideIn(side, new Vector2(-700f, 0f), 0f, 0.5f);
            UIAnim.PopIn(title.transform, 0.25f);

            var random = Gs.Button(side, "랜덤", new Color(0.7f, 0.5f, 0.9f), Randomize, new Vector2(290f, 70f), 28f);
            UIUtil.Place((RectTransform)random.transform, new Vector2(0f, 0f), new Vector2(195f, 70f), new Vector2(290f, 70f));
            var reset = Gs.Button(side, "초기화", Gs.InkSoft, ResetAll, new Vector2(290f, 70f), 28f);
            UIUtil.Place((RectTransform)reset.transform, new Vector2(0f, 0f), new Vector2(505f, 70f), new Vector2(290f, 70f));

            BuildClassCard(canvas.transform);

            var next = Gs.Button(canvas.transform, "<b>모험 시작</b>", new Color(0.35f, 0.75f, 0.5f), StartAdventure, new Vector2(400f, 96f), 40f, true);
            UIUtil.Place((RectTransform)next.transform, new Vector2(1f, 0f), new Vector2(-330f, 80f), new Vector2(400f, 96f));
            UIAnim.PopIn(next.transform, 0.5f);
            // 다음 버튼이 은은하게 숨 쉬듯
            next.transform.GetChild(1).DOScale(1.12f, 0.8f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetLink(next.gameObject);

            var back = Gs.Button(canvas.transform, "모드 선택", Gs.InkSoft, GoBack, new Vector2(240f, 62f), 24f);
            UIUtil.Place((RectTransform)back.transform, new Vector2(1f, 1f), new Vector2(-150f, -50f), new Vector2(240f, 62f));

            var hint = Gs.Text(canvas.transform, "직업은 모험 중에 바꿀 수 없어요 · 마우스로 끌어서 돌리기 · Enter: 모험 시작 · Esc: 뒤로", 21f, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.75f), true);
            UIUtil.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(-150f, 26f), new Vector2(900f, 34f));
        }

        // ---------------------------------------------------------------- 오른쪽: 직업 정보 + 문제 난이도 (원신의 캐릭터 정보 카드처럼)

        RectTransform m_Card, m_CardBody;
        TextMeshProUGUI m_LevelValue, m_LevelInfo, m_CombatValue;

        // 전투 난이도: 보통 ↔ 어려움
        void ToggleCombat()
        {
            CombatDifficulty.Hard = !CombatDifficulty.Hard;
            RefreshLevel();
            UIAnim.Punch(m_CombatValue.transform, 0.25f);
        }

        void BuildClassCard(Transform canvas)
        {
            m_Card = Gs.Panel(canvas, "Class Card", true);
            m_Card.anchorMin = m_Card.anchorMax = new Vector2(1f, 0.5f);
            m_Card.pivot = new Vector2(1f, 0.5f);
            m_Card.anchoredPosition = new Vector2(-40f, 25f);
            m_Card.sizeDelta = new Vector2(600f, 860f);
            m_CardBody = new GameObject("Body", typeof(RectTransform)).GetComponent<RectTransform>();
            m_CardBody.SetParent(m_Card, false);
            UIUtil.Stretch(m_CardBody, 0f);
            m_CardBody.offsetMin = new Vector2(0f, 212f);

            // 아래: 전투 난이도 · 문제 난이도
            var line = UIUtil.Image(m_Card, "Line", new Color(Gs.Gold.r, Gs.Gold.g, Gs.Gold.b, 0.5f));
            UIUtil.Place(line, new Vector2(0.5f, 0f), new Vector2(0f, 210f), new Vector2(540f, 2f));
            var combatLabel = Gs.Text(m_Card, "<b>전투 난이도</b>", 26f, TextAlignmentOptions.Left, Gs.GoldBright);
            UIUtil.Place(combatLabel.rectTransform, new Vector2(0f, 0f), new Vector2(150f, 172f), new Vector2(260f, 36f));
            var cPrev = Gs.Button(m_Card, "", Gs.InkSoft, ToggleCombat, new Vector2(54f, 54f), 20f);
            UIUtil.Place((RectTransform)cPrev.transform, new Vector2(1f, 0f), new Vector2(-270f, 172f), new Vector2(54f, 54f));
            ArrowText(cPrev, "◀");
            var cNext = Gs.Button(m_Card, "", Gs.InkSoft, ToggleCombat, new Vector2(54f, 54f), 20f);
            UIUtil.Place((RectTransform)cNext.transform, new Vector2(1f, 0f), new Vector2(-50f, 172f), new Vector2(54f, 54f));
            ArrowText(cNext, "▶");
            m_CombatValue = Gs.Text(m_Card, "", 30f, TextAlignmentOptions.Center, Color.white);
            UIUtil.Place(m_CombatValue.rectTransform, new Vector2(1f, 0f), new Vector2(-160f, 172f), new Vector2(160f, 40f));
            var label = Gs.Text(m_Card, "<b>수학 문제 난이도</b>", 26f, TextAlignmentOptions.Left, Gs.GoldBright);
            UIUtil.Place(label.rectTransform, new Vector2(0f, 0f), new Vector2(150f, 112f), new Vector2(260f, 36f));
            var prev = Gs.Button(m_Card, "", Gs.InkSoft, () => CycleLevel(-1), new Vector2(54f, 54f), 20f);
            UIUtil.Place((RectTransform)prev.transform, new Vector2(1f, 0f), new Vector2(-270f, 112f), new Vector2(54f, 54f));
            ArrowText(prev, "◀");
            var nextL = Gs.Button(m_Card, "", Gs.InkSoft, () => CycleLevel(1), new Vector2(54f, 54f), 20f);
            UIUtil.Place((RectTransform)nextL.transform, new Vector2(1f, 0f), new Vector2(-50f, 112f), new Vector2(54f, 54f));
            ArrowText(nextL, "▶");
            m_LevelValue = Gs.Text(m_Card, "", 30f, TextAlignmentOptions.Center, Color.white);
            UIUtil.Place(m_LevelValue.rectTransform, new Vector2(1f, 0f), new Vector2(-160f, 112f), new Vector2(160f, 40f));
            m_LevelInfo = Gs.Text(m_Card, "", 20f, TextAlignmentOptions.Center, Gs.Cream);
            UIUtil.Place(m_LevelInfo.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(560f, 60f));

            UIAnim.SlideIn(m_Card, new Vector2(400f, 0f), 0.15f, 0.5f);
            RefreshClassCard();
            RefreshLevel();
        }

        static void ArrowText(UnityEngine.UI.Button b, string arrow)
        {
            // 작은 동그라미 버튼: 아이콘 자리를 비우고 가운데 화살표
            var icon = b.transform.Find("Icon");
            if (icon) icon.gameObject.SetActive(false);
            var t = b.GetComponentInChildren<TextMeshProUGUI>();
            t.text = arrow;
            t.rectTransform.offsetMin = new Vector2(2f, 2f);
            t.rectTransform.offsetMax = new Vector2(-2f, -2f);
        }

        void CycleLevel(int d)
        {
            MathProblem.Level = (MathProblem.Level + d + 3) % 3;
            RefreshLevel();
            UIAnim.Punch(m_LevelValue.transform, 0.25f);
        }

        void RefreshLevel()
        {
            int lv = MathProblem.Level;
            Color[] colors = { new Color(0.5f, 0.9f, 0.6f), new Color(1f, 0.85f, 0.4f), new Color(1f, 0.5f, 0.45f) };
            m_LevelValue.text = $"<b><color={Gs.Hex(colors[lv])}>{MathProblem.LevelNames[lv]}</color></b>";
            m_LevelInfo.text = MathProblem.LevelInfo[lv] + "  · 받침대 4곳 × 3문제"
                + (CombatDifficulty.Hard ? "\n<color=#FF7A6A>전투 어려움: 몹 체력 ×2.5 · 받는 피해 ×2.2 · 보스 50%부터 격노</color>" : "");
            if (m_CombatValue)
                m_CombatValue.text = CombatDifficulty.Hard ? "<b><color=#FF6A5A>어려움</color></b>" : "<b><color=#7FE59A>보통</color></b>";
        }

        void RefreshClassCard()
        {
            if (!m_CardBody) return;
            foreach (Transform c in m_CardBody) Destroy(c.gameObject);
            WeaponCatalog.Info info = WeaponCatalog.Current;
            var st = info.Stats;
            float y = -30f;

            var dot = UIUtil.RoundedImage(m_CardBody, "Element", info.Color, true);
            UIUtil.Place(dot, new Vector2(0f, 1f), new Vector2(54f, y - 22f), new Vector2(44f, 44f));
            dot.gameObject.AddComponent<UnityEngine.UI.Outline>().effectColor = Gs.Gold;
            var name = Gs.Text(m_CardBody, $"<b>{info.ClassName}</b>", 42f, TextAlignmentOptions.Left, Color.white);
            UIUtil.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(330f, y - 18f), new Vector2(500f, 54f));
            var role = Gs.Text(m_CardBody, $"<color={Gs.Hex(Gs.GoldBright)}>{info.Role}</color>  ·  {info.Name}", 22f, TextAlignmentOptions.Left, Gs.Cream);
            UIUtil.Place(role.rectTransform, new Vector2(0f, 1f), new Vector2(330f, y - 58f), new Vector2(500f, 30f));
            y -= 100f;

            (string, float, string)[] stats =
            {
                ("체력", st.MaxHP / 150f, $"{st.MaxHP:0}"),
                ("공격력", st.Attack / 1.3f, $"{st.Attack * 100f:0}%"),
                ("공격 속도", st.AttackSpeed / 1.4f, $"{st.AttackSpeed * 100f:0}%"),
                ("이동 속도", st.Move / 1.2f, $"{st.Move * 100f:0}%"),
                ("치명타", st.Crit / 0.35f, $"{st.Crit * 100f:0}% · ×{st.CritDamage:0.0}"),
                ("방어력", st.Defense / 0.3f, $"{st.Defense * 100f:0}%"),
            };
            foreach (var (label, fill, value) in stats)
            {
                var l = Gs.Text(m_CardBody, label, 22f, TextAlignmentOptions.Left, Gs.Cream);
                UIUtil.Place(l.rectTransform, new Vector2(0f, 1f), new Vector2(110f, y), new Vector2(160f, 30f));
                var bar = UIUtil.Image(m_CardBody, "Bar", new Color(1f, 1f, 1f, 0.08f));
                UIUtil.Place(bar, new Vector2(0f, 1f), new Vector2(330f, y), new Vector2(220f, 8f));
                var f = UIUtil.Image(bar, "Fill", Gs.Gold);
                f.anchorMin = Vector2.zero; f.anchorMax = new Vector2(Mathf.Clamp01(fill), 1f);
                f.offsetMin = f.offsetMax = Vector2.zero;
                var v = Gs.Text(m_CardBody, $"<b>{value}</b>", 22f, TextAlignmentOptions.Right, Color.white);
                UIUtil.Place(v.rectTransform, new Vector2(1f, 1f), new Vector2(-110f, y), new Vector2(180f, 30f));
                y -= 34f;
            }
            y -= 12f;

            // 스킬 (평타 + Q·E·X·C 이름)
            var t = Gs.Text(m_CardBody, "<b>스킬</b>", 24f, TextAlignmentOptions.Left, Gs.GoldBright);
            UIUtil.Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(300f, y), new Vector2(540f, 32f));
            y -= 36f;
            string[] keys = { "평타", "Q", "E", "X", "C" };
            string[] names = SkillNames(WeaponCatalog.Selected);
            for (int i = 0; i < keys.Length && i < names.Length; i++)
            {
                var k = Gs.Key(m_CardBody, keys[i], 28f);
                k.anchorMin = k.anchorMax = new Vector2(0f, 1f);
                k.anchoredPosition = new Vector2(60f, y);
                var n = Gs.Text(m_CardBody, names[i], 22f, TextAlignmentOptions.Left, Color.white);
                UIUtil.Place(n.rectTransform, new Vector2(0f, 1f), new Vector2(330f, y), new Vector2(460f, 30f));
                y -= 34f;
            }
            UIAnim.FadeIn(UIAnim.Group(m_CardBody), 0.25f);
        }

        // 꾸미기 화면에는 전투가 없어서 이름만 따로 적어 둔다 (NewModeCombat.<무기>.cs와 같은 이름)
        static string[] SkillNames(int index)
        {
            switch (index)
            {
                case 0: return new[] { "서리 베기 — 3단, 3타 빙결", "서리 물결", "빙결 결계", "빙하 낙하", "절대영도 (궁극기)" };
                case 1: return new[] { "대지 가르기 — 3단, 길게 누르면 모아 찍기", "회전 투척", "대지 회오리", "대지 융기", "운석 낙하 (궁극기)" };
                case 2: return new[] { "번개 찌르기 — 3단, 4타마다 연쇄 번개", "섬광 돌진", "뇌전 폭발", "오성 섬격", "천둥 난무 (궁극기)" };
                default: return new[] { "속사 — 3단, 조준 헤드샷", "부채 사격", "구르기", "점착 수류탄", "데드아이 (궁극기)" };
            }
        }

        TextMeshProUGUI Selector(RectTransform parent, float y, float height, string label,
            UnityEngine.Events.UnityAction prev, UnityEngine.Events.UnityAction next, Color labelColor, int index)
        {
            var row = UIUtil.RoundedImage(parent, label, index == 0 ? new Color(Gs.Gold.r, Gs.Gold.g, Gs.Gold.b, 0.18f) : Gs.Row);
            UIUtil.Place(row, new Vector2(0f, 1f), new Vector2(350f, y), new Vector2(600f, height));

            var name = UIUtil.Text(row, m_Font, index == 0 ? $"<b>{label}</b>" : label, 26f, TextAlignmentOptions.Left);
            UIUtil.Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(85f, 0f), new Vector2(140f, height));
            name.color = labelColor;
            name.enableAutoSizing = true;
            name.fontSizeMin = 16f;
            name.fontSizeMax = 26f;

            float buttonSize = height - 6f;
            var left = Gs.Button(row, "", Gs.InkSoft, prev, new Vector2(buttonSize, buttonSize), 22f);
            ArrowText(left, "◀");
            UIUtil.Place((RectTransform)left.transform, new Vector2(0f, 0.5f), new Vector2(200f, 0f), new Vector2(buttonSize, buttonSize));
            var right = Gs.Button(row, "", Gs.InkSoft, next, new Vector2(buttonSize, buttonSize), 22f);
            ArrowText(right, "▶");
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
            Pointer pointer = Pointer.current;
            if (pointer != null)
            {
                if (pointer.press.wasPressedThisFrame)
                {
                    bool overUI = EventSystem.current != null &&
                                  (EventSystem.current.IsPointerOverGameObject() || EventSystem.current.IsPointerOverGameObject(0));
                    m_Dragging = !overUI;
                }
                if (!pointer.press.isPressed) m_Dragging = false;
                if (m_Dragging) m_Yaw -= pointer.delta.ReadValue().x * 0.4f * 1080f / Mathf.Max(1, Screen.height);
            }

            if (m_Turntable) m_Turntable.rotation = Quaternion.Euler(0f, m_Yaw, 0f);
        }
    }
}
