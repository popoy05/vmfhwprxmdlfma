using DG.Tweening;
using TMPro;
using Unity.FPS.MathMode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Unity.FPS.NewMode
{
    // 수학 문제 (받침대 가까이에서 F)
    //  · 받침대마다 3문제를 차례로 — 객관식(숫자키 1~4·클릭)과 주관식(직접 입력 → Enter)
    //  · 틀리면 창이 흔들리고 힌트가 나온다 (다시 풀 수 있음). 중간에 닫아도 푼 만큼은 남는다
    //  · 3문제를 다 맞히면: 모든 스킬 재사용 + 15초 동안 공격력 ×2
    // 원신 풍: 크림색 창 · 금색 장식 제목 · 알약 버튼 / 상호작용 안내는 화면 오른쪽 가운데 "F" 알약
    public partial class NewModeCombat
    {
        const float k_BuffTime = 15f;
        float m_BuffUntil;
        int m_Solved;
        bool PowerBuff => Time.time < m_BuffUntil;

        RectTransform m_PromptRoot;
        TextMeshProUGUI m_Prompt, m_Status;
        Canvas m_QuizCanvas;
        QuizStation m_QuizStation;
        Button[] m_QuizButtons;
        RectTransform m_QuizPanel, m_QuizBody;
        TMP_InputField m_QuizInput;
        TextMeshProUGUI m_QuizHint;
        bool m_QuizLocked;
        /// <summary>[F] 수학 문제 풀기 안내 (터치 조작이 그 위를 누를 수 있게)</summary>
        public RectTransform QuizPrompt => m_PromptRoot;

        void BuildQuizHUD()
        {
            // 상호작용 안내 (원신처럼 화면 오른쪽 가운데): [F] 수학 문제 풀기
            m_PromptRoot = UIUtil.RoundedImage(m_Canvas.transform, "Prompt", new Color(0.1f, 0.11f, 0.17f, 0.8f));
            m_PromptRoot.GetComponent<Image>().sprite = Gs.Pill;
            UIUtil.Place(m_PromptRoot, new Vector2(0.5f, 0.5f), new Vector2(330f, -40f), new Vector2(380f, 58f));
            var key = Gs.Key(m_PromptRoot, "F", 40f);
            key.anchorMin = key.anchorMax = new Vector2(0f, 0.5f);
            key.anchoredPosition = new Vector2(34f, 0f);
            m_Prompt = Gs.Text(m_PromptRoot, "", 26f, TextAlignmentOptions.Left, Gs.Cream, true);
            UIUtil.Stretch(m_Prompt.rectTransform, 0f);
            m_Prompt.rectTransform.offsetMin = new Vector2(72f, 0f);
            m_PromptRoot.gameObject.SetActive(false);

            // 오른쪽 위: 버프
            m_Status = Gs.Text(m_Canvas.transform, "", 26f, TextAlignmentOptions.Right, Gs.Cream, true);
            UIUtil.Place(m_Status.rectTransform, new Vector2(1f, 1f), new Vector2(-230f, -60f), new Vector2(420f, 80f));
        }

        void UpdateQuiz()
        {
            if (!m_PromptRoot) return;

            string status = "";
            if (PowerBuff) status = $"<color=#FF8A6A><b>공격력 ×2</b></color>  {Mathf.CeilToInt(m_BuffUntil - Time.time)}초";
            m_Status.text = status;

            if (m_QuizCanvas) { QuizInput(); return; }

            QuizStation near = null;
            float best = 2.6f * S;
            foreach (var st in QuizStation.All)
            {
                if (st.Solved) continue;
                Vector3 d = st.transform.position - transform.position;
                d.y = 0f;
                if (d.magnitude < best) { best = d.magnitude; near = st; }
            }
            bool show = near && !m_InCutscene && m_Current == null && !m_Player.InputBlocked;
            if (show != m_PromptRoot.gameObject.activeSelf)
            {
                m_PromptRoot.gameObject.SetActive(show);
                if (show) UIAnim.SlideIn(m_PromptRoot, new Vector2(60f, 0f), 0f, 0.25f);
            }
            if (show) m_Prompt.text = $"수학 문제 풀기  <size=80%><color={Gs.Hex(Gs.Gold)}>{near.Progress}/{near.Problems.Length}</color></size>";
            Keyboard kb = Keyboard.current;
            Gamepad pad = Gamepad.current;
            if (show && !m_Player.Paused && ((kb != null && kb.fKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame)
                                              || Unity.FPS.Gameplay.VirtualInput.WasPressed("interact")))
                OpenQuiz(near);
        }

        void OpenQuiz(QuizStation station)
        {
            if (m_QuizCanvas) return; // 이미 열려 있음
            m_QuizStation = station;
            m_Player.InputBlocked = true;
            m_Player.Aiming = false;
            m_PromptRoot.gameObject.SetActive(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            m_QuizCanvas = UIUtil.Canvas(transform, "Math Quiz", 25);
            UIUtil.EnsureEventSystem();
            UIAnim.FadeIn(UIAnim.Group(Gs.Dim(m_QuizCanvas.transform, 0.55f)), 0.2f);

            m_QuizPanel = Gs.Panel(m_QuizCanvas.transform, "Panel", false);
            UIUtil.Place(m_QuizPanel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1060f, 720f));
            // 닫기 (휴대폰엔 Esc가 없으니 버튼으로)
            var close = Gs.Button(m_QuizPanel, "닫기", new Color(0.8f, 0.35f, 0.3f), CloseQuiz, new Vector2(150f, 60f), 26f);
            UIUtil.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(-95f, -50f), new Vector2(150f, 60f));
            UIAnim.PopIn(m_QuizPanel, 0f, 0.35f);
            ShowProblem();
        }

        // 지금 문제 (받침대의 Progress번째) 화면을 새로 그린다
        void ShowProblem()
        {
            m_QuizLocked = false;
            if (m_QuizBody) Destroy(m_QuizBody.gameObject);
            m_QuizBody = new GameObject("Body", typeof(RectTransform)).GetComponent<RectTransform>();
            m_QuizBody.SetParent(m_QuizPanel, false);
            UIUtil.Stretch(m_QuizBody, 0f);
            m_QuizInput = null;
            m_QuizButtons = null;

            QuizStation st = m_QuizStation;
            MathProblem problem = st.Problem;
            string level = MathProblem.LevelNames[MathProblem.Level];
            Gs.Header(m_QuizBody, $"수학 문제 · {level}", 40f, new Vector2(0f, -55f), 900f, Gs.Ink);

            // 진행: ● ● ○
            for (int i = 0; i < st.Problems.Length; i++)
            {
                var pip = UIUtil.RoundedImage(m_QuizBody, "Pip", i < st.Progress ? Gs.Gold : i == st.Progress ? Gs.Ink : Gs.CreamDark, true);
                UIUtil.Place(pip, new Vector2(0.5f, 1f), new Vector2((i - (st.Problems.Length - 1) * 0.5f) * 34f, -112f), new Vector2(18f, 18f));
            }
            var kind = Gs.Text(m_QuizBody, problem.Typed ? "<b>주관식</b> — 답을 직접 입력하세요" : "<b>객관식</b> — 보기에서 고르세요", 22f, TextAlignmentOptions.Center, Gs.InkSoft);
            UIUtil.Place(kind.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -146f), new Vector2(900f, 34f));

            var question = Gs.Text(m_QuizBody, $"<b>{problem.Question}</b>", 64f, TextAlignmentOptions.Center, Gs.Ink);
            UIUtil.Place(question.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -260f), new Vector2(940f, 180f));
            question.enableAutoSizing = true;
            question.fontSizeMin = 30f;
            question.fontSizeMax = 64f;

            if (problem.Typed) BuildTyped();
            else BuildChoices(problem);

            m_QuizHint = Gs.Text(m_QuizBody, "", 24f, TextAlignmentOptions.Center, new Color32(170, 110, 40, 255));
            UIUtil.Place(m_QuizHint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(940f, 40f));

            var foot = Gs.Text(m_QuizBody, (problem.Typed ? "숫자를 입력하고 Enter" : "숫자키 1~4 또는 클릭") + "  ·  Esc 닫기 (푼 문제는 남아요)\n" +
                $"<color={Gs.Hex(new Color32(170, 120, 40, 255))}>3문제 모두 맞히면: 모든 스킬 재사용 + 15초 동안 공격력 ×2</color>", 21f, TextAlignmentOptions.Center, Gs.InkSoft);
            UIUtil.Place(foot.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 52f), new Vector2(940f, 70f));
            UIAnim.FadeIn(UIAnim.Group(m_QuizBody), 0.2f);
        }

        void BuildChoices(MathProblem problem)
        {
            m_QuizButtons = new Button[4];
            Color[] icons = { new Color(0.35f, 0.6f, 0.95f), new Color(0.35f, 0.75f, 0.5f), new Color(0.95f, 0.6f, 0.3f), new Color(0.7f, 0.5f, 0.9f) };
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                var button = Gs.Button(m_QuizBody, $"<size=65%><color={Gs.Hex(Gs.InkSoft)}>{i + 1}</color></size>   {problem.Choices[i]}", icons[i], () => Answer(index), new Vector2(440f, 96f), 44f);
                var rt = (RectTransform)button.transform;
                UIUtil.Place(rt, new Vector2(0.5f, 0.5f), new Vector2(i % 2 == 0 ? -235f : 235f, i < 2 ? -60f : -175f), new Vector2(440f, 96f));
                UIAnim.SlideIn(rt, new Vector2(0f, -80f), 0.05f + 0.05f * i, 0.3f);
                m_QuizButtons[i] = button;
            }
        }

        void BuildTyped()
        {
            // TMP 입력칸 (정수만, 음수 가능)
            var go = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
            go.transform.SetParent(m_QuizBody, false);
            var rt = (RectTransform)go.transform;
            UIUtil.Place(rt, new Vector2(0.5f, 0.5f), new Vector2(-110f, -110f), new Vector2(420f, 100f));
            var bg = go.GetComponent<Image>();
            bg.sprite = Gs.Pill;
            bg.type = Image.Type.Sliced;
            bg.color = Color.white;
            go.AddComponent<Outline>().effectColor = Gs.Gold;

            m_QuizInput = go.GetComponent<TMP_InputField>();
            m_QuizInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            m_QuizInput.characterLimit = 7;
            m_QuizInput.textComponent.font = Gs.Font;
            m_QuizInput.textComponent.fontSize = 52f;
            m_QuizInput.textComponent.color = Gs.Ink;
            m_QuizInput.textComponent.alignment = TextAlignmentOptions.Center;
            if (m_QuizInput.placeholder is TextMeshProUGUI ph)
            {
                ph.font = Gs.Font;
                ph.text = "답 입력";
                ph.fontSize = 40f;
                ph.alignment = TextAlignmentOptions.Center;
                ph.color = new Color(Gs.InkSoft.r, Gs.InkSoft.g, Gs.InkSoft.b, 0.5f);
            }
            m_QuizInput.onSubmit.AddListener(_ => SubmitTyped());

            var ok = Gs.Button(m_QuizBody, "확인", new Color(0.35f, 0.75f, 0.5f), SubmitTyped, new Vector2(220f, 96f), 38f, true);
            UIUtil.Place((RectTransform)ok.transform, new Vector2(0.5f, 0.5f), new Vector2(240f, -110f), new Vector2(220f, 96f));
            FocusInput();
        }

        void FocusInput()
        {
            if (!m_QuizInput) return;
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(m_QuizInput.gameObject);
            m_QuizInput.ActivateInputField();
        }

        void QuizInput()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null || m_QuizLocked) return;
            if (kb.escapeKey.wasPressedThisFrame) { CloseQuiz(); return; }
            if (m_QuizButtons != null)
                for (int i = 0; i < 4; i++)
                    if (kb[Key.Digit1 + i].wasPressedThisFrame || kb[Key.Numpad1 + i].wasPressedThisFrame)
                        Answer(i);
        }

        void SubmitTyped()
        {
            if (m_QuizLocked || !m_QuizInput) return;
            string text = m_QuizInput.text.Trim();
            if (text.Length == 0) { FocusInput(); return; }
            if (int.TryParse(text, out int value) && value == m_QuizStation.Problem.Value) Correct(m_QuizInput.transform);
            else
            {
                Wrong();
                m_QuizInput.text = "";
                FocusInput();
            }
        }

        void Answer(int index)
        {
            if (m_QuizLocked || !m_QuizStation || m_QuizButtons == null || !m_QuizButtons[index].interactable) return;
            if (index == m_QuizStation.Problem.Answer)
            {
                var image = m_QuizButtons[index].GetComponent<Image>();
                image.DOKill();
                image.color = new Color32(170, 225, 170, 255);
                Correct(m_QuizButtons[index].transform);
            }
            else
            {
                // 오답: 그 보기는 막히고 창이 흔들린다 — 다시 고를 수 있다
                m_QuizButtons[index].interactable = false;
                var image = m_QuizButtons[index].GetComponent<Image>();
                image.DOKill();
                image.color = new Color32(225, 170, 160, 255);
                Wrong();
            }
        }

        void Wrong()
        {
            m_QuizStation.Wrong++;
            m_QuizPanel.DOKill(true);
            m_QuizPanel.DOShakeAnchorPos(0.35f, 18f, 24).SetLink(m_QuizPanel.gameObject);
            string hint = m_QuizStation.Problem.Hint;
            m_QuizHint.text = string.IsNullOrEmpty(hint) ? "다시 한 번 천천히 계산해 보세요" : $"<b>힌트</b>  {hint}";
            UIAnim.Punch(m_QuizHint.transform, 0.15f, 0.3f);
        }

        void Correct(Transform clicked)
        {
            m_QuizLocked = true;
            UIAnim.Punch(clicked, 0.2f, 0.35f);
            var ok = Gs.Text(m_QuizBody, "<b>정답!</b>", 110f, TextAlignmentOptions.Center, new Color32(70, 150, 90, 255));
            UIUtil.Place(ok.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(800f, 150f));
            ok.outlineWidth = 0.15f;
            ok.outlineColor = Color.white;
            UIAnim.PopIn(ok.transform, 0f, 0.3f);

            QuizStation station = m_QuizStation;
            station.Progress++;
            station.RefreshLabel();
            bool done = station.Progress >= station.Problems.Length;
            DOVirtual.DelayedCall(0.8f, () =>
            {
                if (!m_QuizCanvas) return;
                if (done)
                {
                    CloseQuiz();
                    Reward(station);
                }
                else ShowProblem();
            }).SetLink(gameObject);
        }

        void CloseQuiz()
        {
            if (m_QuizCanvas) Destroy(m_QuizCanvas.gameObject);
            m_QuizCanvas = null;
            m_QuizStation = null;
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
            // 같은 프레임의 Esc가 일시정지로 이어지지 않게 한 박자 뒤에 풀어 준다
            DOVirtual.DelayedCall(0.05f, () => { if (m_Player) m_Player.InputBlocked = false; }, true).SetLink(gameObject);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void Reward(QuizStation station)
        {
            if (station) station.MarkSolved();
            m_Solved++;
            m_BuffUntil = Time.time + k_BuffTime;
            foreach (var weapon in m_Weapons)
                foreach (var skill in weapon.Skills)
                    skill.Remaining = 0f;

            Fx.Play("Buff", transform.position, Quaternion.identity, 0.9f * S, 0.6f, false, transform);
            Fx.Play("Star aura", transform.position + Vector3.up * 1f * S, Quaternion.identity, 0.7f * S, 1.2f, false, transform);
            if (station) Fx.Play("Sparks explode yellow", station.transform.position + Vector3.up * 2.3f * S, Quaternion.identity, 0.9f * S, 0.2f, false);
            m_Player.Shake(0.08f, 0.3f);
            PostFx.Pulse(0.4f, Gs.Gold);
            ShowBanner($"<b>3문제 모두 정답!</b>\n<size=45%><color={Gs.Hex(Gs.GoldBright)}>모든 스킬 재사용 · 15초 동안 공격력 ×2</color></size>");
        }
    }
}
