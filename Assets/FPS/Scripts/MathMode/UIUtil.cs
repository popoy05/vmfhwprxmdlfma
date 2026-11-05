using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Unity.FPS.MathMode
{
    // 코드로 UI를 만들 때 쓰는 작은 도우미
    public static class UIUtil
    {
        static Sprite s_Rounded, s_Circle;

        // 모서리가 둥근 사각형 (9-slice) / 원
        public static Sprite Rounded => s_Rounded ? s_Rounded : (s_Rounded = MakeRounded(64, 20));
        public static Sprite Circle => s_Circle ? s_Circle : (s_Circle = MakeRounded(128, 64));

        static Sprite MakeRounded(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // 가장 가까운 모서리 원 중심까지의 거리로 안티앨리어싱
                float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                byte a = (byte)(Mathf.Clamp01(radius - d + 0.5f) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            float border = Mathf.Min(radius, size / 2 - 1);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        public static Canvas Canvas(Transform parent, string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static RectTransform Image(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return (RectTransform)go.transform;
        }

        public static RectTransform RoundedImage(Transform parent, string name, Color color, bool circle = false)
        {
            RectTransform rt = Image(parent, name, color);
            var image = rt.GetComponent<Image>();
            image.sprite = circle ? Circle : Rounded;
            image.type = circle ? UnityEngine.UI.Image.Type.Simple : UnityEngine.UI.Image.Type.Sliced;
            return rt;
        }

        public static TextMeshProUGUI Text(Transform parent, TMP_FontAsset font, string text, float size,
            TextAlignmentOptions alignment)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (font) tmp.font = font;
            tmp.fontSize = size;
            tmp.alignment = alignment;
            tmp.text = text;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            return tmp;
        }

        // 둥근 버튼 (마우스 클릭과 터치 모두 동작)
        public static Button Button(Transform parent, TMP_FontAsset font, string label, Color color, UnityAction onClick,
            Vector2 size, float fontSize = 30f)
        {
            RectTransform rt = RoundedImage(parent, "Button", color);
            rt.sizeDelta = size;
            var image = rt.GetComponent<Image>();
            image.raycastTarget = true;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.selectedColor = Color.white;
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(onClick);
            rt.gameObject.AddComponent<ButtonJuice>(); // 마우스 올림·누름 반응 (DOTween)

            var text = Text(rt, font, label, fontSize, TextAlignmentOptions.Center);
            Stretch(text.rectTransform, 6f);
            text.enableAutoSizing = true;
            text.fontSizeMin = 12f;
            text.fontSizeMax = fontSize;
            return button;
        }

        public static void Stretch(RectTransform rt, float padding)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padding, padding);
            rt.offsetMax = new Vector2(-padding, -padding);
        }

        public static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        // 버튼 클릭을 받으려면 EventSystem이 필요하다
        public static void EnsureEventSystem()
        {
            if (EventSystem.current) return;
            var es = Object.FindAnyObjectByType<EventSystem>();
            if (es) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }
    }
}
