using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using Unity.FPS.MathMode;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.FPS.NewMode
{
    // 전투 HUD (DOTween 애니메이션)
    //  - 아래: 스킬 바 (평타 + Q·E·X·C) — 무기를 바꾸면 차례로 튀어 올라온다, 쓰면 콩 튀고, 쿨타임이 끝나면 반짝
    //  - 왼쪽: 무기 목록 (1~7) — 고른 무기가 커지고 색이 바뀐다
    //  - 위: 스킬 함수식 배너 — 커지며 나타났다가 흐려진다
    public partial class NewModeCombat
    {
        TMP_FontAsset m_Font;
        Canvas m_Canvas;
        RectTransform m_SkillBar;
        TextMeshProUGUI m_Banner;
        Sequence m_BannerSeq;
        readonly List<(RectTransform row, Image bg, TextMeshProUGUI label)> m_WeaponRows = new List<(RectTransform, Image, TextMeshProUGUI)>();

        void BuildHUD()
        {
            m_Canvas = UIUtil.Canvas(transform, "Combat HUD", 9);

            m_SkillBar = new GameObject("Skill Bar", typeof(RectTransform)).GetComponent<RectTransform>();
            m_SkillBar.SetParent(m_Canvas.transform, false);
            UIUtil.Place(m_SkillBar, new Vector2(0.5f, 0f), new Vector2(0f, 82f), new Vector2(1300f, 130f));

            // 스킬 함수식 배너
            m_Banner = UIUtil.Text(m_Canvas.transform, m_Font, "", 54f, TextAlignmentOptions.Center);
            UIUtil.Place(m_Banner.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(1400f, 140f));
            m_Banner.textWrappingMode = TextWrappingModes.NoWrap;
            m_Banner.enableAutoSizing = true;
            m_Banner.fontSizeMin = 28f;
            m_Banner.fontSizeMax = 54f;
            m_Banner.outlineWidth = 0.2f;
            m_Banner.outlineColor = new Color(0.05f, 0.1f, 0.25f);
            m_Banner.alpha = 0f;

            BuildQuizHUD();
        }

        void RebuildSkillBar()
        {
            if (!m_SkillBar) return;
            foreach (Transform child in m_SkillBar) Destroy(child.gameObject);

            var all = new List<SkillDef> { m_Weapon.Basic };
            all.AddRange(m_Weapon.Skills);
            float width = 236f, gap = 12f;
            float startX = -(all.Count - 1) * (width + gap) * 0.5f;
            for (int i = 0; i < all.Count; i++)
            {
                SkillDef skill = all[i];
                var slot = UIUtil.RoundedImage(m_SkillBar, skill.Name, new Color(0.03f, 0.06f, 0.14f, 0.85f));
                UIUtil.Place(slot, new Vector2(0.5f, 0.5f), new Vector2(startX + i * (width + gap), 0f), new Vector2(width, 120f));
                skill.Slot = slot;

                // 무기 색 테두리 느낌의 윗줄
                var stripe = UIUtil.Image(slot, "Stripe", m_Weapon.Color);
                stripe.anchorMin = new Vector2(0.08f, 1f);
                stripe.anchorMax = new Vector2(0.92f, 1f);
                stripe.pivot = new Vector2(0.5f, 1f);
                stripe.sizeDelta = new Vector2(0f, 4f);
                stripe.anchoredPosition = new Vector2(0f, -3f);

                var key = UIUtil.RoundedImage(slot, "Key", i == 0 ? new Color(0.35f, 0.4f, 0.55f) : Color.Lerp(m_Weapon.Color, Color.black, 0.35f));
                float keyWidth = i == 0 ? (m_Weapon.Kind == WeaponKind.Gun ? 120f : 74f) : 46f;
                UIUtil.Place(key, new Vector2(0f, 1f), new Vector2(keyWidth * 0.5f + 5f, -4f), new Vector2(keyWidth, 36f));
                var keyText = UIUtil.Text(key, m_Font, $"<b>{(i == 0 ? (m_Weapon.Kind == WeaponKind.Gun ? "좌클릭 연사" : "좌클릭") : skill.Key)}</b>", 20f, TextAlignmentOptions.Center);
                UIUtil.Stretch(keyText.rectTransform, 2f);
                keyText.enableAutoSizing = true;
                keyText.fontSizeMin = 10f;
                keyText.fontSizeMax = 20f;

                var name = UIUtil.Text(slot, m_Font, $"<b>{skill.Name}</b>", 25f, TextAlignmentOptions.Center);
                UIUtil.Place(name.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(width - 16f, 36f));
                name.enableAutoSizing = true;
                name.fontSizeMin = 14f;
                name.fontSizeMax = 25f;

                var formula = UIUtil.Text(slot, m_Font, FxFormula.Rich(skill.Formula), 20f, TextAlignmentOptions.Center);
                UIUtil.Place(formula.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(width - 16f, 30f));
                formula.color = new Color(1f, 1f, 1f, 0.45f);
                formula.enableAutoSizing = true;
                formula.fontSizeMin = 11f;
                formula.fontSizeMax = 15f;

                var cover = UIUtil.RoundedImage(slot, "Cooldown", new Color(0f, 0f, 0f, 0.6f));
                UIUtil.Stretch(cover, 0f);
                var coverImage = cover.GetComponent<Image>();
                coverImage.type = Image.Type.Filled;
                coverImage.fillMethod = Image.FillMethod.Vertical;
                coverImage.fillOrigin = (int)Image.OriginVertical.Bottom;
                coverImage.fillAmount = skill.Cooldown > 0f ? skill.Remaining / skill.Cooldown : 0f;
                skill.CooldownFill = coverImage;

                var cd = UIUtil.Text(slot, m_Font, "", 40f, TextAlignmentOptions.Center);
                UIUtil.Stretch(cd.rectTransform, 0f);
                cd.fontStyle = FontStyles.Bold;
                skill.CooldownText = cd;
                skill.WasReady = skill.Remaining <= 0f;

                UIAnim.SlideIn(slot, new Vector2(0f, -160f), 0.04f * i, 0.4f);
            }

            for (int i = 0; i < m_WeaponRows.Count; i++)
            {
                var (row, bg, label) = m_WeaponRows[i];
                bool on = i == m_Weapon.Index;
                // 등장 애니메이션(SlideIn)은 살려 두고, 강조용 크기 트윈만 바꾼다
                DOTween.Kill("weapon-row-" + i);
                row.DOScale(on ? 1.08f : 1f, 0.25f).SetEase(Ease.OutBack).SetId("weapon-row-" + i).SetLink(row.gameObject);
                bg.DOKill();
                bg.DOColor(on ? new Color(m_Weapons[i].Color.r, m_Weapons[i].Color.g, m_Weapons[i].Color.b, 0.55f) : new Color(1f, 1f, 1f, 0.06f), 0.25f).SetLink(bg.gameObject);
                label.color = on ? Color.white : new Color(0.75f, 0.8f, 0.9f);
            }
        }

        void Punch(SkillDef skill)
        {
            if (!skill.Slot) return;
            UIAnim.Punch(skill.Slot, 0.12f, 0.3f);
            var image = skill.Slot.GetComponent<Image>();
            UIAnim.Flash(image, Color.Lerp(m_Weapon.Color, Color.white, 0.3f), 0.35f);
        }

        void ShowBanner(string text)
        {
            if (!m_Banner) return;
            m_Banner.text = FxFormula.Rich(text);
            m_BannerSeq?.Kill();
            m_Banner.alpha = 0f;
            m_Banner.rectTransform.localScale = Vector3.one * 0.7f;
            m_BannerSeq = DOTween.Sequence()
                .Append(m_Banner.DOFade(1f, 0.15f))
                .Join(m_Banner.rectTransform.DOScale(1f, 0.35f).SetEase(Ease.OutBack))
                .AppendInterval(1.1f)
                .Append(m_Banner.DOFade(0f, 0.4f))
                .SetLink(m_Banner.gameObject);
        }

        void UpdateHUD()
        {
            if (m_Weapon == null) return;
            foreach (var skill in m_Weapon.Skills)
            {
                if (!skill.CooldownFill) continue;
                skill.CooldownFill.fillAmount = skill.Cooldown > 0f ? skill.Remaining / skill.Cooldown : 0f;
                skill.CooldownText.text = skill.Remaining > 0f ? Mathf.CeilToInt(skill.Remaining).ToString() : "";

                // 쿨타임이 막 끝나면 반짝
                bool ready = skill.Remaining <= 0f;
                if (ready && !skill.WasReady)
                {
                    UIAnim.Punch(skill.Slot, 0.1f, 0.35f);
                    UIAnim.Flash(skill.Slot.GetComponent<Image>(), new Color(1f, 1f, 1f, 0.9f), 0.4f);
                }
                skill.WasReady = ready;
            }
        }
    }
}
