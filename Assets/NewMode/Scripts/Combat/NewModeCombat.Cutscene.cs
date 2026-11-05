using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using Unity.FPS.MathMode;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.FPS.NewMode
{
    // 궁극기(C) 컷신 — 무기마다 다른 연출 5가지
    //  0 궤도   : 앞쪽 아래에서 올려다보며 반 바퀴 (홈런 배트)
    //  1 돌진 줌 : 뒤쪽 멀리서 → 얼굴 앞으로 확 다가가며 "임팩트 프레임"(시간 정지 + 색 반전 섬광) (대지 도끼·산탄총)
    //  2 하늘 시점: 머리 위에서 마법진을 내려다보며 돌다가 어깨 너머로 떨어진다 (서리검)
    //  3 옆 추적 : 옆에서 나란히 달리듯 지나가고, 화면엔 속도선 (번개 단검·권총)
    //  4 조준경 : 어깨 너머에서 멀리 확대 → 조준경 테두리 (저격총)
    public partial class NewModeCombat
    {
        static readonly int[] k_CutStyle = { 2, 1, 3, 0, 3, 1, 4 };

        IEnumerator Ultimate(SkillDef skill)
        {
            Theme th = Th;
            Color color = C;
            int style = k_CutStyle[Mathf.Clamp(m_Weapon.Index, 0, k_CutStyle.Length - 1)];
            m_InCutscene = true;
            if (m_HitstopOn) { m_HitstopOn = false; Time.timeScale = m_SlowMo; }

            Camera cam = m_Player.Camera;
            m_Player.CameraOverride = true;
            m_Player.Aiming = false;
            float fov0 = cam ? cam.fieldOfView : 60f;

            CanvasGroup hud = UIAnim.Group(m_Canvas);
            hud.DOKill();
            hud.DOFade(0f, 0.2f).SetLink(hud.gameObject);
            var ui = new CutUI(this, skill, color, style);

            // 자세
            if (style == 4) Pose(new Vector3(0.05f, 0.02f, 1f), Vector3.up, 0.95f);
            else if (m_Weapon.Kind == WeaponKind.Gun) Pose(new Vector3(0.1f, 0.75f, 0.65f), Vector3.back, 0.95f);
            else Pose(new Vector3(0.15f, 1f, 0.12f), Vector3.forward, 1f);
            BodyLean = style == 4 ? 0f : -10f;
            TrailOn = true;

            // 이펙트: 무기의 마법진 + 오라 (겹치지 않게 두 개만)
            Fx.Play(th.Circle, Feet, Facing(), th.CircleSize * S, 1.9f, false);
            Fx.Play(th.Aura, transform.position + Vector3.up * 0.9f * S, Quaternion.identity, th.AuraSize * S, 1.7f, false, transform);

            Vector3 center = transform.position;
            Quaternion facing = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            Vector3 fwd = facing * Vector3.forward, right = facing * Vector3.right;
            // 오른쪽이 벽·바위로 막혀 있으면 카메라를 왼쪽으로
            if (Physics.Linecast(center + Vector3.up * 1.2f * S, center + right * 3.6f * S + Vector3.up * 1.2f * S, ~(1 << 2), QueryTriggerInteraction.Ignore))
                right = -right;
            const float duration = 1.8f;
            float nextCharge = 0.2f;
            bool cardShown = false, impact = false;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float u = t / duration;
                float e = Mathf.SmoothStep(0f, 1f, u);
                if (cam)
                {
                    Shot(style, u, e, center, fwd, right, out Vector3 eye, out Vector3 look, out float fovMul);
                    // 카메라가 벽·지형 속으로 들어가지 않게: 보는 곳 → 눈 사이가 막히면 그 앞까지 당긴다
                    Vector3 back = eye - look;
                    if (Physics.SphereCast(look, 0.2f * S, back.normalized, out RaycastHit block, back.magnitude, ~(1 << 2), QueryTriggerInteraction.Ignore))
                        eye = look + back.normalized * Mathf.Max(0.6f * S, block.distance - 0.15f * S);
                    cam.transform.SetPositionAndRotation(eye + m_Player.ShakeOffset(), Quaternion.LookRotation(look - eye));
                    cam.fieldOfView = fov0 * fovMul;
                }

                if (t >= nextCharge)
                {
                    nextCharge += 0.5f;
                    Vector3 tip = m_Rig ? m_Rig.TipPosition : Chest + Vector3.up * 0.8f * S;
                    Fx.Play(th.Charge, tip, Quaternion.identity, 0.3f * S, 0.15f, false);
                }
                if (!cardShown && u >= (style == 1 ? 0.72f : 0.15f))
                {
                    cardShown = true;
                    ui.ShowTitle();
                    m_Player.Shake(0.05f, 1f);
                }
                // 돌진 줌: 얼굴 앞에 닿는 순간 "임팩트 프레임"
                if (style == 1 && !impact && u >= 0.72f)
                {
                    impact = true;
                    yield return ui.ImpactFrame();
                }
                yield return null;
            }

            // 섬광 → 해방
            yield return ui.FlashIn();
            if (cam) cam.fieldOfView = fov0;
            m_Player.CameraOverride = false;
            m_InCutscene = false;
            ui.Close();
            hud.DOFade(1f, 0.4f).SetDelay(0.2f).SetLink(hud.gameObject);

            Fx.Play(th.Release, Feet, Facing(), 0.7f * S, 0.3f, false);
            m_Player.Shake(0.22f, 0.45f);
            BodyLean = 0f;

            m_Player.FaceCameraDirection();
            SetFrame();
            m_Ultimate = true;
            yield return skill.Run();
            m_Ultimate = false;
        }

        // 카메라 길 (u: 0→1 진행, e: 부드럽게 한 u)
        void Shot(int style, float u, float e, Vector3 c, Vector3 fwd, Vector3 right, out Vector3 eye, out Vector3 look, out float fov)
        {
            float s = S;
            Vector3 up = Vector3.up;
            switch (style)
            {
                case 1: // 돌진 줌
                {
                    Vector3 far = c - fwd * 7f * s + up * 3.2f * s + right * 2.2f * s + right * e * 0.8f * s;
                    Vector3 near = c + fwd * 1.5f * s + up * 1.45f * s + right * 0.3f * s;
                    float k = Mathf.InverseLerp(0.5f, 0.72f, u);
                    k = k * k * k; // 처음엔 천천히, 끝에 확
                    eye = Vector3.Lerp(far, near, k);
                    look = c + up * Mathf.Lerp(1f, 1.4f, k) * s;
                    fov = Mathf.Lerp(0.9f, 0.55f, k);
                    break;
                }
                case 2: // 하늘 시점 → 어깨 너머
                {
                    float a = Mathf.Lerp(0f, 200f, e) * Mathf.Deg2Rad;
                    Vector3 orbit = (Mathf.Cos(a) * right + Mathf.Sin(a) * fwd) * 1.6f * s;
                    Vector3 sky = c + up * Mathf.Lerp(8f, 5f, e) * s + orbit;
                    Vector3 shoulder = c - fwd * 3f * s + up * 2f * s + right * 0.8f * s;
                    float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.75f, 1f, u));
                    eye = Vector3.Lerp(sky, shoulder, k);
                    look = Vector3.Lerp(c, c + fwd * 4f * s + up * 1f * s, k);
                    fov = Mathf.Lerp(0.85f, 1f, k);
                    break;
                }
                case 3: // 옆 추적
                {
                    eye = c + right * 3.4f * s + up * 1.15f * s + fwd * Mathf.Lerp(-3f, 3f, e) * s;
                    look = c + up * 1f * s + fwd * Mathf.Lerp(-0.6f, 1.2f, e) * s;
                    fov = 0.8f;
                    break;
                }
                case 4: // 조준경
                {
                    eye = c - fwd * Mathf.Lerp(2.4f, 1.6f, e) * s + right * 0.6f * s + up * 1.6f * s;
                    look = c + fwd * 20f * s + up * 1f * s;
                    fov = Mathf.Lerp(1f, 0.32f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 0.7f, u)));
                    break;
                }
                default: // 0 궤도
                {
                    float angle = Mathf.Lerp(40f, -55f, e) * Mathf.Deg2Rad;
                    Vector3 dir = Mathf.Cos(angle) * fwd + Mathf.Sin(angle) * right;
                    eye = c + dir * Mathf.Lerp(3.6f, 4.8f, e) * s + up * Mathf.Lerp(0.6f, 1.6f, e) * s;
                    Vector3 side = Vector3.Cross(up, c - eye).normalized;
                    look = c + up * Mathf.Lerp(0.85f, 0.75f, e) * s - side * 1.1f * s;
                    fov = Mathf.Lerp(0.8f, 0.7f, e);
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ 컷신 화면 (스타일마다 다른 타이틀)
        class CutUI
        {
            readonly NewModeCombat m_C;
            readonly SkillDef m_Skill;
            readonly Color m_Color;
            readonly int m_Style;
            readonly Canvas m_Canvas;
            readonly RectTransform m_Root, m_Top, m_Bottom;
            readonly Image m_Flash;
            RectTransform m_Title;
            readonly List<RectTransform> m_Extra = new List<RectTransform>();

            public CutUI(NewModeCombat c, SkillDef skill, Color color, int style)
            {
                m_C = c;
                m_Skill = skill;
                m_Color = color;
                m_Style = style;
                m_Canvas = UIUtil.Canvas(c.transform, "Ultimate Cutscene", 30);
                m_Root = (RectTransform)m_Canvas.transform;

                if (style == 4) BuildScope();
                else
                {
                    const float barH = 120f;
                    m_Top = Bar(true, barH);
                    m_Bottom = Bar(false, barH);
                }
                if (style == 3) BuildSpeedLines();
                if (style == 2) BuildRing();

                m_Flash = UIUtil.Image(m_Root, "Flash", new Color(1f, 1f, 1f, 0f)).GetComponent<Image>();
                UIUtil.Stretch(m_Flash.rectTransform, 0f);
                m_Flash.raycastTarget = false;
            }

            RectTransform Bar(bool top, float h)
            {
                var bar = UIUtil.Image(m_Root, top ? "Bar Top" : "Bar Bottom", Color.black);
                bar.anchorMin = new Vector2(0f, top ? 1f : 0f);
                bar.anchorMax = new Vector2(1f, top ? 1f : 0f);
                bar.pivot = new Vector2(0.5f, top ? 1f : 0f);
                bar.sizeDelta = new Vector2(0f, h);
                bar.anchoredPosition = new Vector2(0f, top ? h : -h);
                bar.DOAnchorPosY(0f, 0.25f).SetEase(Ease.OutCubic).SetLink(bar.gameObject);
                return bar;
            }

            string Name => $"<b>{m_Skill.Name}</b>";
            string Sub => $"<b>궁극기</b>  ·  {WeaponCatalog.All[m_C.m_Weapon.Index].ClassName}";
            string Formula => FxFormula.Rich(m_Skill.Detail != null ? m_Skill.Detail() : m_Skill.Formula);

            public void ShowTitle()
            {
                switch (m_Style)
                {
                    case 1: TitleCenter(); break;
                    case 2: TitleVertical(); break;
                    case 4: TitleScope(); break;
                    default: TitleBand(); break;
                }
            }

            // 0·3: 기울어진 띠가 오른쪽에서 확 들어옴
            void TitleBand()
            {
                m_Title = new GameObject("Title", typeof(RectTransform)).GetComponent<RectTransform>();
                m_Title.SetParent(m_Root, false);
                UIUtil.Place(m_Title, new Vector2(0.5f, 0.5f), new Vector2(2400f, -300f), new Vector2(1300f, 190f));
                var band = UIUtil.Image(m_Title, "Band", new Color(m_Color.r * 0.22f, m_Color.g * 0.22f, m_Color.b * 0.3f, 0.85f));
                UIUtil.Stretch(band, 0f);
                band.localRotation = Quaternion.Euler(0f, 0f, 2f);
                var edge = UIUtil.Image(band, "Edge", Color.Lerp(m_Color, Color.white, 0.5f));
                edge.anchorMin = new Vector2(0f, 1f); edge.anchorMax = new Vector2(1f, 1f); edge.pivot = new Vector2(0.5f, 1f);
                edge.sizeDelta = new Vector2(0f, 8f);
                Label(m_Title, Sub, 30f, new Vector2(0f, 52f), TextAlignmentOptions.Left, Color.Lerp(m_Color, Color.white, 0.6f));
                var name = Label(m_Title, Name, 104f, new Vector2(0f, -14f), TextAlignmentOptions.Left, Color.white);
                name.characterSpacing = 6f;
                Label(m_Title, Formula, 22f, new Vector2(0f, -68f), TextAlignmentOptions.Right, new Color(1f, 1f, 1f, 0.6f));
                DOTween.Sequence()
                    .Append(m_Title.DOAnchorPosX(-260f, 0.22f).SetEase(Ease.OutCubic))
                    .Append(m_Title.DOAnchorPosX(-330f, 1.2f).SetEase(Ease.Linear))
                    .SetLink(m_Title.gameObject);
                m_Title.localScale = new Vector3(1.25f, 0.6f, 1f);
                m_Title.DOScale(1f, 0.3f).SetEase(Ease.OutBack).SetLink(m_Title.gameObject);
            }

            // 1: 화면 가운데에 거대한 글자가 쾅
            void TitleCenter()
            {
                m_Title = new GameObject("Title", typeof(RectTransform)).GetComponent<RectTransform>();
                m_Title.SetParent(m_Root, false);
                UIUtil.Place(m_Title, new Vector2(0.5f, 0.5f), new Vector2(0f, -220f), new Vector2(1600f, 260f));
                var name = Label(m_Title, Name, 150f, new Vector2(0f, 10f), TextAlignmentOptions.Center, Color.white);
                name.outlineWidth = 0.25f;
                name.outlineColor = (Color32)Color.Lerp(m_Color, Color.black, 0.5f);
                Label(m_Title, Sub, 34f, new Vector2(0f, 110f), TextAlignmentOptions.Center, Color.Lerp(m_Color, Color.white, 0.5f));
                Label(m_Title, Formula, 24f, new Vector2(0f, -90f), TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.6f));
                m_Title.localScale = Vector3.one * 2.4f;
                m_Title.DOScale(1f, 0.18f).SetEase(Ease.InCubic).SetUpdate(true).SetLink(m_Title.gameObject);
                m_Title.DOShakeAnchorPos(0.4f, 25f, 30).SetDelay(0.18f).SetUpdate(true).SetLink(m_Title.gameObject);
            }

            // 2: 가운데 아래, 고리 안에서 글자가 한 자씩
            void TitleVertical()
            {
                m_Title = new GameObject("Title", typeof(RectTransform)).GetComponent<RectTransform>();
                m_Title.SetParent(m_Root, false);
                UIUtil.Place(m_Title, new Vector2(0.5f, 0.5f), new Vector2(0f, -250f), new Vector2(1200f, 220f));
                var name = Label(m_Title, Name, 96f, Vector2.zero, TextAlignmentOptions.Center, Color.white);
                name.characterSpacing = 30f;
                name.maxVisibleCharacters = 0;
                int count = m_Skill.Name.Length;
                DOTween.To(() => name.maxVisibleCharacters, v => name.maxVisibleCharacters = v, count, 0.08f * count).SetLink(name.gameObject);
                DOTween.To(() => name.characterSpacing, v => name.characterSpacing = v, 8f, 1.2f).SetEase(Ease.OutCubic).SetLink(name.gameObject);
                Label(m_Title, Sub, 30f, new Vector2(0f, 90f), TextAlignmentOptions.Center, Color.Lerp(m_Color, Color.white, 0.5f));
                Label(m_Title, Formula, 22f, new Vector2(0f, -80f), TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.6f));
            }

            // 4: 조준경 아래쪽 글자
            void TitleScope()
            {
                m_Title = new GameObject("Title", typeof(RectTransform)).GetComponent<RectTransform>();
                m_Title.SetParent(m_Root, false);
                UIUtil.Place(m_Title, new Vector2(0.5f, 0f), new Vector2(0f, 160f), new Vector2(1400f, 200f));
                Label(m_Title, "<b>조준 완료</b>", 30f, new Vector2(0f, 60f), TextAlignmentOptions.Center, new Color(1f, 0.35f, 0.3f));
                Label(m_Title, Name, 84f, new Vector2(0f, -5f), TextAlignmentOptions.Center, Color.white);
                Label(m_Title, Formula, 22f, new Vector2(0f, -65f), TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.6f));
                UIAnim.FadeIn(UIAnim.Group(m_Title), 0.3f);
            }

            TextMeshProUGUI Label(RectTransform parent, string text, float size, Vector2 pos, TextAlignmentOptions align, Color color)
            {
                var t = UIUtil.Text(parent, m_C.m_Font, text, size, align);
                UIUtil.Place(t.rectTransform, new Vector2(0.5f, 0.5f), pos, new Vector2(parent.sizeDelta.x - 100f, size * 1.4f));
                t.color = color;
                t.outlineWidth = Mathf.Max(t.outlineWidth, 0.15f);
                t.outlineColor = new Color(0.05f, 0.05f, 0.1f);
                t.textWrappingMode = TextWrappingModes.NoWrap;
                return t;
            }

            // 3: 무기 색 속도선이 오른쪽에서 왼쪽으로 쉭쉭
            void BuildSpeedLines()
            {
                for (int i = 0; i < 16; i++)
                {
                    var line = UIUtil.Image(m_Root, "Speed", new Color(m_Color.r, m_Color.g, m_Color.b, Random.Range(0.25f, 0.6f)));
                    line.anchorMin = line.anchorMax = new Vector2(0.5f, 0.5f);
                    float y = Random.Range(-480f, 480f);
                    line.sizeDelta = new Vector2(Random.Range(300f, 900f), Random.Range(3f, 8f));
                    line.anchoredPosition = new Vector2(1500f, y);
                    line.DOAnchorPosX(-1500f, Random.Range(0.25f, 0.5f)).SetDelay(Random.Range(0f, 0.4f)).SetLoops(-1).SetEase(Ease.Linear).SetLink(line.gameObject);
                    m_Extra.Add(line);
                }
            }

            // 2: 화면 가운데 커지며 도는 고리
            void BuildRing()
            {
                var ring = UIUtil.RoundedImage(m_Root, "Ring", new Color(m_Color.r, m_Color.g, m_Color.b, 0.25f), true);
                UIUtil.Place(ring, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 300f));
                ring.GetComponent<Image>().raycastTarget = false;
                ring.DOSizeDelta(new Vector2(1600f, 1600f), 1.8f).SetEase(Ease.OutCubic).SetLink(ring.gameObject);
                ring.GetComponent<Image>().DOFade(0f, 1.8f).SetEase(Ease.InCubic).SetLink(ring.gameObject);
                m_Extra.Add(ring);
            }

            // 4: 조준경 — 가운데 원만 뚫린 검은 화면 + 십자선
            void BuildScope()
            {
                var tex = new Texture2D(256, 256, TextureFormat.RGBA32, false);
                for (int y = 0; y < 256; y++)
                    for (int x = 0; x < 256; x++)
                    {
                        float r = new Vector2(x - 127.5f, y - 127.5f).magnitude / 128f;
                        float a = r > 0.96f ? 1f : r > 0.93f ? 0.85f : 0f;
                        tex.SetPixel(x, y, new Color(0f, 0f, 0f, a));
                    }
                tex.Apply();
                var sprite = Sprite.Create(tex, new Rect(0, 0, 256, 256), new Vector2(0.5f, 0.5f));
                var scope = new GameObject("Scope", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
                scope.SetParent(m_Root, false);
                UIUtil.Stretch(scope, 0f);
                var circle = new GameObject("Circle", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                circle.SetParent(scope, false);
                circle.anchorMin = circle.anchorMax = new Vector2(0.5f, 0.5f);
                circle.sizeDelta = new Vector2(1000f, 1000f);
                circle.GetComponent<Image>().sprite = sprite;
                foreach (float side in new[] { -1f, 1f })
                {
                    var black = UIUtil.Image(scope, "Side", Color.black);
                    black.anchorMin = new Vector2(side < 0f ? 0f : 0.5f, 0f);
                    black.anchorMax = new Vector2(side < 0f ? 0.5f : 1f, 1f);
                    black.offsetMin = new Vector2(side < 0f ? 0f : 499f, 0f);
                    black.offsetMax = new Vector2(side < 0f ? -499f : 0f, 0f);
                    var cap = UIUtil.Image(scope, "Cap", Color.black);
                    cap.anchorMin = new Vector2(0f, side < 0f ? 0f : 0.5f);
                    cap.anchorMax = new Vector2(1f, side < 0f ? 0.5f : 1f);
                    cap.offsetMin = new Vector2(0f, side < 0f ? 0f : 499f);
                    cap.offsetMax = new Vector2(0f, side < 0f ? -499f : 0f);
                }
                var h = UIUtil.Image(scope, "H", new Color(0f, 0f, 0f, 0.8f)); h.anchorMin = h.anchorMax = new Vector2(0.5f, 0.5f); h.sizeDelta = new Vector2(900f, 3f);
                var v = UIUtil.Image(scope, "V", new Color(0f, 0f, 0f, 0.8f)); v.anchorMin = v.anchorMax = new Vector2(0.5f, 0.5f); v.sizeDelta = new Vector2(3f, 900f);
                var dot = UIUtil.RoundedImage(scope, "Dot", new Color(1f, 0.2f, 0.2f), true); UIUtil.Place(dot, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 10f));
                var group = scope.GetComponent<CanvasGroup>();
                group.alpha = 0f;
                group.DOFade(1f, 0.4f).SetDelay(0.5f).SetLink(scope.gameObject);
                m_Extra.Add(scope);
            }

            // 임팩트 프레임: 시간 정지 + 무기 색 반전 섬광 두 번
            public IEnumerator ImpactFrame()
            {
                float before = Time.timeScale;
                Time.timeScale = 0f;
                Color inv = new Color(1f - m_Color.r * 0.5f, 1f - m_Color.g * 0.5f, 1f - m_Color.b * 0.5f, 0.85f);
                m_Flash.color = inv;
                yield return new WaitForSecondsRealtime(0.05f);
                m_Flash.color = new Color(0f, 0f, 0f, 0.7f);
                yield return new WaitForSecondsRealtime(0.05f);
                m_Flash.color = new Color(m_Color.r, m_Color.g, m_Color.b, 0.6f);
                yield return new WaitForSecondsRealtime(0.08f);
                m_Flash.color = new Color(1f, 1f, 1f, 0f);
                Time.timeScale = before;
                m_C.m_Player.Shake(0.2f, 0.4f);
            }

            public IEnumerator FlashIn()
            {
                m_Flash.DOKill();
                m_Flash.color = new Color(1f, 1f, 1f, 0f);
                m_Flash.DOFade(1f, 0.06f).SetLink(m_Flash.gameObject);
                yield return new WaitForSeconds(0.07f);
            }

            public void Close()
            {
                if (m_Top) m_Top.DOAnchorPosY(m_Top.sizeDelta.y, 0.3f).SetEase(Ease.InQuad).SetLink(m_Top.gameObject);
                if (m_Bottom) m_Bottom.DOAnchorPosY(-m_Bottom.sizeDelta.y, 0.3f).SetEase(Ease.InQuad).SetLink(m_Bottom.gameObject);
                if (m_Title) UIAnim.Group(m_Title).DOFade(0f, 0.2f).SetLink(m_Title.gameObject);
                foreach (var x in m_Extra) if (x) UIAnim.Group(x).DOFade(0f, 0.2f).SetLink(x.gameObject);
                m_Flash.DOFade(0f, 0.45f).SetLink(m_Flash.gameObject);
                Object.Destroy(m_Canvas.gameObject, 1f);
            }
        }
    }
}
