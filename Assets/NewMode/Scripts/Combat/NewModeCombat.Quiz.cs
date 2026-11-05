using DG.Tweening;
using TMPro;
using Unity.FPS.MathMode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Unity.FPS.NewMode
{
    // 수학 문제 자리 가까이에서 F → 문제 창 (숫자키 1~4 또는 클릭, Esc 닫기)
    // 맞히면: 모든 스킬 쿨타임 초기화 + 15초 동안 공격력 2배
    public partial class NewModeCombat
    {
        const float k_BuffTime = 15f;
        float m_BuffUntil;
        int m_Solved;
        bool PowerBuff => Time.time < m_BuffUntil;

        TextMeshProUGUI m_Prompt, m_Status;
        Canvas m_QuizCanvas;
        QuizStation m_QuizStation;
        Button[] m_QuizButtons;
        RectTransform m_QuizPanel;
        bool m_QuizLocked;

        void BuildQuizHUD()
        {
            m_Prompt = UIUtil.Text(m_Canvas.transform, m_Font, "", 34f, TextAlignmentOptions.Center);
            UIUtil.Place(m_Prompt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -170f), new Vector2(800f, 60f));
            m_Prompt.outlineWidth = 0.2f;
            m_Prompt.outlineColor = new Color(0.1f, 0.05f, 0f);

            m_Status = UIUtil.Text(m_Canvas.transform, m_Font, "", 26f, TextAlignmentOptions.Right);
            UIUtil.Place(m_Status.rectTransform, new Vector2(1f, 1f), new Vector2(-230f, -110f), new Vector2(420f, 80f));
            m_Status.outlineWidth = 0.2f;
            m_Status.outlineColor = new Color(0.05f, 0.05f, 0.15f);
        }

        void UpdateQuiz()
        {
            if (!m_Prompt) return;

            // 오른쪽 위: 푼 문제 수 + 공격력 버프 남은 시간
            string status = m_Solved > 0 ? $"<color=#FFD866>푼 문제</color>  {m_Solved}" : "";
            if (PowerBuff) status += $"\n<color=#FF7A5C><b>공격력 ×2</b></color>  {Mathf.CeilToInt(m_BuffUntil - Time.time)}초";
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
            bool show = near && !m_InCutscene && m_Current == null;
            m_Prompt.text = show ? "<color=#FFD866><b>F</b></color>  수학 문제 풀기" : "";
            Keyboard kb = Keyboard.current;
            Gamepad pad = Gamepad.current;
            if (show && !m_Player.Paused && ((kb != null && kb.fKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame)))
                OpenQuiz(near);
        }

        void OpenQuiz(QuizStation station)
        {
            m_QuizStation = station;
            m_QuizLocked = false;
            m_Player.InputBlocked = true;
            m_Player.Aiming = false;
            m_Prompt.text = "";
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            MathProblem problem = station.Problem;
            m_QuizCanvas = UIUtil.Canvas(transform, "Math Quiz", 25);
            var dim = UIUtil.Image(m_QuizCanvas.transform, "Dim", new Color(0.02f, 0.03f, 0.08f, 0.6f));
            UIUtil.Stretch(dim, 0f);
            UIAnim.FadeIn(UIAnim.Group(dim), 0.2f);

            m_QuizPanel = UIUtil.RoundedImage(m_QuizCanvas.transform, "Panel", new Color(0.06f, 0.08f, 0.18f, 0.96f));
            UIUtil.Place(m_QuizPanel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 680f));
            var stripe = UIUtil.Image(m_QuizPanel, "Stripe", new Color(1f, 0.8f, 0.3f));
            stripe.anchorMin = new Vector2(0.06f, 1f); stripe.anchorMax = new Vector2(0.94f, 1f); stripe.pivot = new Vector2(0.5f, 1f);
            stripe.sizeDelta = new Vector2(0f, 6f);
            stripe.anchoredPosition = new Vector2(0f, -8f);

            var title = UIUtil.Text(m_QuizPanel, m_Font, "<b>수학 문제</b>", 40f, TextAlignmentOptions.Center);
            UIUtil.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(900f, 60f));
            title.color = new Color(1f, 0.84f, 0.3f);

            var question = UIUtil.Text(m_QuizPanel, m_Font, $"<b>{problem.Question}</b>", 60f, TextAlignmentOptions.Center);
            UIUtil.Place(question.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -200f), new Vector2(920f, 200f));
            question.enableAutoSizing = true;
            question.fontSizeMin = 30f;
            question.fontSizeMax = 60f;

            m_QuizButtons = new Button[4];
            Color[] colors = { new Color(0.25f, 0.45f, 0.85f), new Color(0.2f, 0.65f, 0.45f), new Color(0.75f, 0.45f, 0.2f), new Color(0.6f, 0.35f, 0.75f) };
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                var button = UIUtil.Button(m_QuizPanel, m_Font, $"<size=70%>{i + 1}</size>   <b>{problem.Choices[i]}</b>", colors[i], () => Answer(index), new Vector2(420f, 110f), 46f);
                var rt = (RectTransform)button.transform;
                Vector2 pos = new Vector2(i % 2 == 0 ? -225f : 225f, i < 2 ? -50f : -180f);
                UIUtil.Place(rt, new Vector2(0.5f, 0.5f), pos, new Vector2(420f, 110f));
                UIAnim.SlideIn(rt, new Vector2(0f, -120f), 0.08f + 0.05f * i, 0.35f);
                m_QuizButtons[i] = button;
            }

            var hint = UIUtil.Text(m_QuizPanel, m_Font, "숫자키 1~4 또는 클릭  ·  Esc 닫기\n<color=#FFD866>정답 보상: 모든 스킬 재사용 + 15초 동안 공격력 ×2</color>", 22f, TextAlignmentOptions.Center);
            UIUtil.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(900f, 70f));
            hint.color = new Color(1f, 1f, 1f, 0.75f);

            UIAnim.PopIn(m_QuizPanel, 0f, 0.35f);
        }

        void QuizInput()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null || m_QuizLocked) return;
            if (kb.escapeKey.wasPressedThisFrame) { CloseQuiz(); return; }
            for (int i = 0; i < 4; i++)
                if (kb[Key.Digit1 + i].wasPressedThisFrame || kb[Key.Numpad1 + i].wasPressedThisFrame)
                    Answer(i);
        }

        void Answer(int index)
        {
            if (m_QuizLocked || !m_QuizStation || !m_QuizButtons[index].interactable) return;
            var rt = (RectTransform)m_QuizButtons[index].transform;
            var image = m_QuizButtons[index].GetComponent<Image>();

            if (index == m_QuizStation.Problem.Answer)
            {
                m_QuizLocked = true;
                image.DOKill();
                image.color = new Color(0.25f, 0.85f, 0.4f);
                UIAnim.Punch(rt, 0.25f, 0.4f);
                var ok = UIUtil.Text(m_QuizPanel, m_Font, "<b>정답!</b>", 120f, TextAlignmentOptions.Center);
                UIUtil.Place(ok.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(800f, 160f));
                ok.color = new Color(0.5f, 1f, 0.6f);
                ok.outlineWidth = 0.2f;
                ok.outlineColor = new Color(0f, 0.2f, 0.05f);
                UIAnim.PopIn(ok.transform, 0f, 0.3f);

                QuizStation station = m_QuizStation;
                DOVirtual.DelayedCall(0.9f, () =>
                {
                    CloseQuiz();
                    Reward(station);
                }).SetLink(gameObject);
            }
            else
            {
                // 오답: 그 보기는 빨갛게 막히고 창이 흔들린다 — 다시 고를 수 있다
                m_QuizButtons[index].interactable = false;
                m_QuizStation.Wrong++;
                image.DOKill();
                image.color = new Color(0.55f, 0.15f, 0.15f);
                m_QuizPanel.DOKill(true);
                m_QuizPanel.DOShakeAnchorPos(0.35f, 18f, 24).SetLink(m_QuizPanel.gameObject);
            }
        }

        void CloseQuiz()
        {
            if (m_QuizCanvas) Destroy(m_QuizCanvas.gameObject);
            m_QuizCanvas = null;
            m_QuizStation = null;
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
            ShowBanner("<b>정답!</b>\n<size=45%><color=#FFD866>모든 스킬 재사용 · 15초 동안 공격력 ×2</color></size>");
        }
    }
}
