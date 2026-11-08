using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Unity.FPS.MathMode
{
    // DOTween으로 만든 UI 움직임 모음 (메뉴·HUD 공통).
    // 일시정지(timeScale = 0) 중에도 움직이도록 모두 SetUpdate(true), 오브젝트가 사라지면 함께 정리되도록 SetLink.
    public static class UIAnim
    {
        public static CanvasGroup Group(Component c)
        {
            var group = c.GetComponent<CanvasGroup>();
            return group ? group : c.gameObject.AddComponent<CanvasGroup>();
        }

        /// <summary>offset만큼 떨어진 곳에서 미끄러져 들어오며 나타난다</summary>
        public static Tween SlideIn(RectTransform rt, Vector2 offset, float delay = 0f, float duration = 0.45f)
        {
            Vector2 target = rt.anchoredPosition;
            rt.anchoredPosition = target + offset;
            var group = Group(rt);
            group.alpha = 0f;
            DOTween.Kill(rt);
            // 어떤 이유로 중간에 끊겨도(Kill) 마지막 모습(제자리·불투명)으로 맞춘다
            return DOTween.Sequence()
                .Append(rt.DOAnchorPos(target, duration).SetEase(Ease.OutBack))
                .Join(group.DOFade(1f, duration * 0.6f))
                .SetDelay(delay).SetUpdate(true).SetLink(rt.gameObject).SetTarget(rt)
                .OnKill(() => { if (rt) { rt.anchoredPosition = target; group.alpha = 1f; } });
        }

        /// <summary>작게 시작해서 통통 튀며 커진다</summary>
        public static Tween PopIn(Transform t, float delay = 0f, float duration = 0.4f)
        {
            t.localScale = Vector3.one * 0.5f;
            var group = Group(t);
            group.alpha = 0f;
            return DOTween.Sequence().OnKill(() => { if (t) { t.localScale = Vector3.one; group.alpha = 1f; } })
                .Append(t.DOScale(1f, duration).SetEase(Ease.OutBack))
                .Join(group.DOFade(1f, duration * 0.5f))
                .SetDelay(delay).SetUpdate(true).SetLink(t.gameObject);
        }

        /// <summary>한 번 콩 튀기</summary>
        public static void Punch(Transform t, float strength = 0.15f, float duration = 0.3f)
        {
            if (!t) return;
            t.DOKill(true);
            t.localScale = Vector3.one;
            t.DOPunchScale(Vector3.one * strength, duration, 6, 0.6f).SetUpdate(true).SetLink(t.gameObject);
        }

        /// <summary>이미지가 번쩍였다가 원래 색으로</summary>
        public static void Flash(Graphic g, Color flash, float duration = 0.35f)
        {
            if (!g) return;
            Color original = g.color;
            g.DOKill(true);
            g.color = flash;
            g.DOColor(original, duration).SetUpdate(true).SetLink(g.gameObject);
        }

        public static Tween FadeIn(CanvasGroup group, float duration = 0.25f)
        {
            group.alpha = 0f;
            return group.DOFade(1f, duration).SetUpdate(true).SetLink(group.gameObject);
        }
    }

    // 버튼에 마우스를 올리면 살짝 커지고, 누르면 눌리는 느낌 (UIUtil.Button이 자동으로 붙인다)
    public class ButtonJuice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public float HoverScale = 1.06f;
        bool m_Over;

        // 자기 크기 트윈만 끊는다 — transform.DOKill()은 같은 대상의 등장 애니메이션(SlideIn)까지 끊어서
        // 카드가 투명한 채로 멈추는 문제가 있었다 (모드 선택으로 돌아왔을 때 "새 모드" 카드가 안 보이는데 클릭은 되던 버그)
        void To(float scale, float duration)
        {
            DOTween.Kill(this);
            transform.DOScale(scale, duration).SetEase(Ease.OutQuad).SetUpdate(true).SetLink(gameObject).SetTarget(this);
        }

        public void OnPointerEnter(PointerEventData e) { m_Over = true; To(HoverScale, 0.15f); }
        public void OnPointerExit(PointerEventData e) { m_Over = false; To(1f, 0.15f); }
        public void OnPointerDown(PointerEventData e) => To(0.93f, 0.08f);
        public void OnPointerUp(PointerEventData e) => To(m_Over ? HoverScale : 1f, 0.2f);

        void OnDisable()
        {
            DOTween.Kill(this);
            transform.localScale = Vector3.one;
        }
    }
}
