using DG.Tweening;
using TMPro;
using Unity.FPS.MathMode;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Unity.FPS.NewMode
{
    // 원신(Genshin Impact) 풍 UI 도구 — 참고: gamedesignernawe.tistory.com/94 (원신 UI/UX 스크린샷 모음)
    //  · 메뉴 창: 크림색 바탕(#ECE5D8) + 진한 남색 글씨(#3B4255) + 금색(#D3BC8E) 테두리·장식선
    //  · 게임 화면(HUD): 판을 깔지 않고 반투명 남색 + 그림자 있는 흰 글씨, 금색 포인트
    //  · 버튼: 둥근 알약 모양 크림 버튼, 왼쪽에 색 동그라미 아이콘
    //  · 제목: 양옆에 금색 마름모 장식선
    public static class Gs
    {
        public static readonly Color Cream = new Color32(236, 229, 216, 255);
        public static readonly Color CreamDark = new Color32(219, 208, 189, 255);
        public static readonly Color Ink = new Color32(59, 66, 85, 255);
        public static readonly Color InkSoft = new Color32(110, 115, 130, 255);
        public static readonly Color Gold = new Color32(211, 188, 142, 255);
        public static readonly Color GoldBright = new Color32(255, 225, 150, 255);
        public static readonly Color Night = new Color32(28, 32, 46, 220);
        public static readonly Color NightSoft = new Color32(28, 32, 46, 150);
        public static readonly Color Row = new Color(1f, 1f, 1f, 0.05f);

        static TMP_FontAsset s_Font;
        static Sprite s_Ring, s_Pill;
        public static TMP_FontAsset Font => s_Font ? s_Font : (s_Font = MathModeAssets.Load() ? MathModeAssets.Load().Font : null);

        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        // 고리 모양 (스킬 버튼 테두리·쿨타임)
        public static Sprite Ring => s_Ring ? s_Ring : (s_Ring = MakeRing(128, 0.86f));
        // 알약 모양 (둥근 버튼)
        public static Sprite Pill => s_Pill ? s_Pill : (s_Pill = MakePill());

        static Sprite MakeRing(int size, float inner)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float r = new Vector2(x + 0.5f - half, y + 0.5f - half).magnitude / half;
                    float a = Mathf.Clamp01((1f - r) * half) * Mathf.Clamp01((r - inner) * half);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        static Sprite MakePill()
        {
            const int size = 64, radius = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                    float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(radius - d + 0.5f)));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(31, 31, 31, 31));
        }

        public static TextMeshProUGUI Text(Transform parent, string text, float size, TextAlignmentOptions align, Color color, bool shadow = false)
        {
            var t = UIUtil.Text(parent, Font, text, size, align);
            t.color = color;
            if (shadow)
            {
                // HUD 글씨: 얇은 어두운 외곽선 + 아래 그림자 느낌
                t.outlineWidth = 0.18f;
                t.outlineColor = new Color32(20, 22, 32, 200);
            }
            return t;
        }

        /// <summary>메뉴 창 (dark면 반투명 남색, 아니면 크림색) + 금색 테두리</summary>
        public static RectTransform Panel(Transform parent, string name, bool dark)
        {
            var rt = UIUtil.RoundedImage(parent, name, dark ? Night : Cream);
            var outline = rt.gameObject.AddComponent<Outline>();
            outline.effectColor = dark ? new Color(Gold.r, Gold.g, Gold.b, 0.55f) : Gold;
            outline.effectDistance = new Vector2(2f, -2f);
            return rt;
        }

        /// <summary>금색 마름모 아이콘</summary>
        public static RectTransform Diamond(Transform parent, Color color, float size)
        {
            var rt = UIUtil.Image(parent, "Diamond", color);
            rt.sizeDelta = new Vector2(size, size);
            rt.localRotation = Quaternion.Euler(0f, 0f, 45f);
            return rt;
        }

        /// <summary>제목: 가운데 글씨 + 양옆 금색 선과 마름모</summary>
        public static TextMeshProUGUI Header(Transform parent, string text, float size, Vector2 pos, float width, Color textColor)
        {
            var root = new GameObject("Header", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(parent, false);
            UIUtil.Place(root, new Vector2(0.5f, 1f), pos, new Vector2(width, size * 1.4f));
            var t = Text(root, $"<b>{text}</b>", size, TextAlignmentOptions.Center, textColor);
            UIUtil.Stretch(t.rectTransform, 0f);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            float textW = Mathf.Min(width * 0.6f, t.GetPreferredValues().x + 30f);
            foreach (float side in new[] { -1f, 1f })
            {
                var line = UIUtil.Image(root, "Line", new Color(Gold.r, Gold.g, Gold.b, 0.8f));
                line.anchorMin = line.anchorMax = new Vector2(0.5f, 0.5f);
                float len = (width - textW) * 0.5f - 24f;
                line.sizeDelta = new Vector2(Mathf.Max(10f, len), 2f);
                line.anchoredPosition = new Vector2(side * (textW * 0.5f + 12f + len * 0.5f), 0f);
                var d = Diamond(root, Gold, 9f);
                d.anchorMin = d.anchorMax = new Vector2(0.5f, 0.5f);
                d.anchoredPosition = new Vector2(side * (textW * 0.5f + 8f), 0f);
            }
            return t;
        }

        /// <summary>얇은 금색 구분선</summary>
        public static RectTransform Divider(Transform parent, Vector2 pos, float width)
        {
            var line = UIUtil.Image(parent, "Divider", new Color(Gold.r, Gold.g, Gold.b, 0.6f));
            UIUtil.Place(line, new Vector2(0.5f, 1f), pos, new Vector2(width, 2f));
            return line;
        }

        /// <summary>원신 풍 알약 버튼: 크림 바탕, 진한 글씨, 왼쪽 색 동그라미</summary>
        public static Button Button(Transform parent, string label, Color icon, UnityAction onClick, Vector2 size, float fontSize = 30f, bool primary = false)
        {
            var b = UIUtil.Button(parent, Font, "", primary ? new Color32(245, 222, 165, 255) : Cream, onClick, size, fontSize);
            var img = b.GetComponent<Image>();
            img.sprite = Pill;
            img.type = UnityEngine.UI.Image.Type.Sliced;
            var outline = b.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(Gold.r, Gold.g, Gold.b, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);

            var dot = UIUtil.RoundedImage(b.transform, "Icon", Ink, true);
            UIUtil.Place(dot, new Vector2(0f, 0.5f), new Vector2(size.y * 0.5f + 4f, 0f), new Vector2(size.y * 0.56f, size.y * 0.56f));
            var inner = UIUtil.RoundedImage(dot, "Inner", icon, true);
            UIUtil.Stretch(inner, size.y * 0.09f);

            var text = b.GetComponentInChildren<TextMeshProUGUI>();
            text.text = label;
            text.color = Ink;
            text.fontStyle = FontStyles.Bold;
            text.rectTransform.offsetMin = new Vector2(size.y * 0.95f, 6f);
            text.rectTransform.offsetMax = new Vector2(-size.y * 0.35f, -6f);
            return b;
        }

        /// <summary>키 표시 (예: "F", "Q") — 작은 남색 둥근 칸에 크림 글씨</summary>
        public static RectTransform Key(Transform parent, string key, float height = 30f)
        {
            var rt = UIUtil.RoundedImage(parent, "Key", new Color32(40, 45, 62, 235));
            float w = Mathf.Max(height, key.Length * height * 0.45f + 14f);
            rt.sizeDelta = new Vector2(w, height);
            rt.gameObject.AddComponent<Outline>().effectColor = new Color(Gold.r, Gold.g, Gold.b, 0.7f);
            var t = Text(rt, $"<b>{key}</b>", height * 0.6f, TextAlignmentOptions.Center, Cream);
            UIUtil.Stretch(t.rectTransform, 1f);
            t.enableAutoSizing = true;
            t.fontSizeMin = 8f;
            t.fontSizeMax = height * 0.6f;
            return rt;
        }

        /// <summary>화면 전체 어둡게 (메뉴 뒤)</summary>
        public static RectTransform Dim(Transform parent, float alpha = 0.6f)
        {
            var dim = UIUtil.Image(parent, "Dim", new Color(0.04f, 0.05f, 0.09f, alpha));
            UIUtil.Stretch(dim, 0f);
            dim.GetComponent<Image>().raycastTarget = true;
            return dim;
        }

        /// <summary>원신 풍 알림 띠: 화면 가운데 위, 가로로 옅어지는 남색 띠 + 금색 선</summary>
        public static RectTransform Banner(Transform parent, out TextMeshProUGUI text, float y, float height = 120f)
        {
            var root = new GameObject("Banner", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
            root.SetParent(parent, false);
            UIUtil.Place(root, new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(1100f, height));
            // 가운데가 진하고 양끝이 투명한 띠: 3조각
            for (int i = -1; i <= 1; i++)
            {
                var part = UIUtil.Image(root, "Band", new Color(0.08f, 0.09f, 0.14f, i == 0 ? 0.7f : 0.35f));
                part.anchorMin = new Vector2(0.5f, 0f);
                part.anchorMax = new Vector2(0.5f, 1f);
                part.sizeDelta = new Vector2(i == 0 ? 560f : 260f, 0f);
                part.anchoredPosition = new Vector2(i * 410f, 0f);
            }
            foreach (float yy in new[] { 0f, 1f })
            {
                var line = UIUtil.Image(root, "Line", new Color(Gold.r, Gold.g, Gold.b, 0.75f));
                line.anchorMin = new Vector2(0.15f, yy);
                line.anchorMax = new Vector2(0.85f, yy);
                line.sizeDelta = new Vector2(0f, 2f);
            }
            text = Text(root, "", 54f, TextAlignmentOptions.Center, Cream, true);
            UIUtil.Stretch(text.rectTransform, 8f);
            text.enableAutoSizing = true;
            text.fontSizeMin = 20f;
            text.fontSizeMax = 54f;
            return root;
        }

        public static void Show(RectTransform banner, float hold = 1.6f)
        {
            var group = banner.GetComponent<CanvasGroup>();
            group.DOKill();
            banner.DOKill();
            group.alpha = 0f;
            banner.localScale = new Vector3(1.2f, 0.6f, 1f);
            DOTween.Sequence()
                .Append(group.DOFade(1f, 0.2f))
                .Join(banner.DOScale(1f, 0.35f).SetEase(Ease.OutBack))
                .AppendInterval(hold)
                .Append(group.DOFade(0f, 0.45f))
                .SetLink(banner.gameObject).SetTarget(banner);
        }
    }
}
