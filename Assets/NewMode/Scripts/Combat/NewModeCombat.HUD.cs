using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using Unity.FPS.MathMode;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.FPS.NewMode
{
    // 전투 HUD — 원신 풍
    //  · 오른쪽 아래: 둥근 스킬 버튼 (C 궁극기가 가장 크고, Q·E·X가 그 둘레, 평타는 작게) — 쿨타임은 부채꼴로 걷힘
    //  · 평타 단계 표시: 마름모 3개 (1단·2단·3단)
    //  · 가운데 위: 알림 띠 (가운데 진하고 양끝이 옅은 남색 띠 + 금색 선)
    //  · 판을 깔지 않고 그림자 있는 글씨, 금색 포인트
    public partial class NewModeCombat
    {
        TMP_FontAsset m_Font;
        Canvas m_Canvas;
        RectTransform m_SkillRoot, m_BannerRoot;
        TextMeshProUGUI m_Banner;
        RectTransform[] m_ComboPips;
        RectTransform m_TargetMark;

        void BuildHUD()
        {
            m_Canvas = UIUtil.Canvas(transform, "Combat HUD", 9);

            m_SkillRoot = new GameObject("Skills", typeof(RectTransform)).GetComponent<RectTransform>();
            m_SkillRoot.SetParent(m_Canvas.transform, false);
            m_SkillRoot.anchorMin = m_SkillRoot.anchorMax = new Vector2(1f, 0f);
            m_SkillRoot.sizeDelta = Vector2.zero;
            m_SkillRoot.anchoredPosition = Vector2.zero;

            m_BannerRoot = Gs.Banner(m_Canvas.transform, out m_Banner, 300f, 130f);
            m_BannerRoot.GetComponent<CanvasGroup>().alpha = 0f;

            // 평타 단계: ◆ ◆ ◆ (평타 버튼 위)
            m_ComboPips = new RectTransform[3];
            for (int i = 0; i < 3; i++)
            {
                var pip = Gs.Diamond(m_SkillRoot, new Color(1f, 1f, 1f, 0.25f), 16f);
                pip.anchorMin = pip.anchorMax = new Vector2(0.5f, 0.5f);
                pip.anchoredPosition = new Vector2(-500f + i * 30f, 175f);
                m_ComboPips[i] = pip;
            }

            // 타겟 표시: 다음 공격이 향할 적 머리 위 ▼ (금색 마름모 + 고리)
            m_TargetMark = new GameObject("Target", typeof(RectTransform)).GetComponent<RectTransform>();
            m_TargetMark.SetParent(m_Canvas.transform, false);
            m_TargetMark.anchorMin = m_TargetMark.anchorMax = new Vector2(0.5f, 0.5f);
            m_TargetMark.sizeDelta = new Vector2(44f, 44f);
            var ring = UIUtil.Image(m_TargetMark, "Ring", new Color(1f, 0.45f, 0.35f, 0.9f));
            ring.GetComponent<Image>().sprite = Gs.Ring;
            UIUtil.Stretch(ring, 0f);
            var dia = Gs.Diamond(m_TargetMark, Gs.GoldBright, 12f);
            dia.anchorMin = dia.anchorMax = new Vector2(0.5f, 0.5f);
            dia.anchoredPosition = Vector2.zero;
            m_TargetMark.gameObject.SetActive(false);

            BuildQuizHUD();
        }

        void UpdateTargetMark()
        {
            if (!m_TargetMark) return;
            Camera cam = m_Player ? m_Player.Camera : null;
            TrainingDummy t = null;
            if (cam && !m_InCutscene && !m_Player.InputBlocked && !(m_Weapon.Kind == WeaponKind.Gun && m_Player.Aiming))
                t = FindTarget(m_Weapon.Kind == WeaponKind.Gun ? 14f : k_MeleeLock);
            if (!t) { m_TargetMark.gameObject.SetActive(false); return; }
            Vector3 sp = cam.WorldToScreenPoint(t.Head + Vector3.up * t.HeadRadius * 2.4f);
            if (sp.z < 0f) { m_TargetMark.gameObject.SetActive(false); return; }
            var canvasRect = (RectTransform)m_Canvas.transform;
            float k = canvasRect.rect.width / Screen.width;
            m_TargetMark.gameObject.SetActive(true);
            m_TargetMark.anchoredPosition = new Vector2(sp.x * k, sp.y * k) - canvasRect.rect.size * 0.5f;
            m_TargetMark.localScale = Vector3.one * (0.9f + 0.1f * Mathf.Sin(Time.unscaledTime * 8f));
            m_TargetMark.localRotation = Quaternion.Euler(0f, 0f, Time.unscaledTime * 90f);
        }

        // 버튼 하나: 어두운 원 + 색 고리 + 이름 두 글자 + 키 + 쿨타임 부채꼴
        RectTransform SkillButton(SkillDef skill, Vector2 pos, float size, Color ring, string key)
        {
            var slot = UIUtil.RoundedImage(m_SkillRoot, skill.Name, new Color(0.08f, 0.09f, 0.14f, 0.82f), true);
            slot.anchorMin = slot.anchorMax = new Vector2(0.5f, 0.5f);
            slot.anchoredPosition = pos;
            slot.sizeDelta = new Vector2(size, size);

            var r = UIUtil.Image(slot, "Ring", ring);
            r.GetComponent<Image>().sprite = Gs.Ring;
            UIUtil.Stretch(r, 0f);

            string shortName = skill.Name.Replace(" ", "");
            if (shortName.Length > 2) shortName = shortName.Substring(0, 2);
            var n = Gs.Text(slot, $"<b>{shortName}</b>", size * 0.3f, TextAlignmentOptions.Center, Color.white, true);
            UIUtil.Stretch(n.rectTransform, 0f);
            n.rectTransform.offsetMin = new Vector2(size * 0.1f, size * 0.2f);
            n.rectTransform.offsetMax = new Vector2(-size * 0.1f, -size * 0.1f);

            var cover = UIUtil.RoundedImage(slot, "Cooldown", new Color(0f, 0f, 0f, 0.62f), true);
            UIUtil.Stretch(cover, size * 0.06f);
            var coverImage = cover.GetComponent<Image>();
            coverImage.type = Image.Type.Filled;
            coverImage.fillMethod = Image.FillMethod.Radial360;
            coverImage.fillOrigin = (int)Image.Origin360.Top;
            coverImage.fillClockwise = false;
            coverImage.fillAmount = skill.Cooldown > 0f ? skill.Remaining / skill.Cooldown : 0f;
            skill.CooldownFill = coverImage;

            var cd = Gs.Text(slot, "", size * 0.36f, TextAlignmentOptions.Center, Color.white, true);
            UIUtil.Stretch(cd.rectTransform, 0f);
            cd.fontStyle = FontStyles.Bold;
            skill.CooldownText = cd;
            skill.WasReady = skill.Remaining <= 0f;

            var k = Gs.Key(slot, key, Mathf.Max(24f, size * 0.26f));
            k.anchorMin = k.anchorMax = new Vector2(0.5f, 0f);
            k.anchoredPosition = new Vector2(0f, 4f);

            var label = Gs.Text(slot, skill.Name, 17f, TextAlignmentOptions.Center, Gs.Cream, true);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            label.rectTransform.sizeDelta = new Vector2(170f, 24f);
            label.rectTransform.anchoredPosition = new Vector2(0f, -24f);
            label.textWrappingMode = TextWrappingModes.NoWrap;

            skill.Slot = slot;
            UIAnim.PopIn(slot, 0.05f, 0.35f);
            return slot;
        }

        void RebuildSkillBar()
        {
            if (!m_SkillRoot) return;
            foreach (Transform child in m_SkillRoot)
                if (System.Array.IndexOf(m_ComboPips, child) < 0) Destroy(child.gameObject);

            Color c = m_Weapon.Color;
            if (NewModeTouch.Enabled)
            {
                // 휴대폰: 큰 공격 버튼을 중심으로 Q · E · X · C가 둘레에 (터치 영역과 같은 자리)
                SkillButton(m_Weapon.Basic, NewModeTouch.AttackPos, NewModeTouch.AttackSize, new Color(1f, 1f, 1f, 0.75f), "공격");
                m_Weapon.Basic.CooldownFill.fillAmount = 0f;
                for (int i = 0; i < 4; i++)
                    SkillButton(m_Weapon.Skills[i], NewModeTouch.SkillPos(i), i == 3 ? NewModeTouch.UltSize : NewModeTouch.SkillSize,
                        i == 3 ? Gs.GoldBright : c, m_Weapon.Skills[i].Key);
                for (int i = 0; i < m_ComboPips.Length; i++)
                {
                    m_ComboPips[i].anchoredPosition = new Vector2(-205f + i * 30f, 285f);
                    m_ComboPips[i].SetAsLastSibling();
                }
                return;
            }
            // C(궁극기)를 중심으로, 왼쪽에서 위로 호를 그리며 Q · E · X  (원신의 원소 스킬·원소 폭발 배치처럼)
            Vector2 ult = new Vector2(-140f, 160f);
            SkillButton(m_Weapon.Skills[3], ult, 150f, Gs.GoldBright, "C");
            float R = 180f;
            float[] angles = { 182f, 142f, 102f };
            for (int i = 0; i < 3; i++)
            {
                float a = angles[i] * Mathf.Deg2Rad;
                SkillButton(m_Weapon.Skills[i], ult + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * R, 96f, c, m_Weapon.Skills[i].Key);
            }
            SkillButton(m_Weapon.Basic, new Vector2(-470f, 110f), 78f, new Color(1f, 1f, 1f, 0.6f), "좌클릭");
            m_Weapon.Basic.CooldownFill.fillAmount = 0f;
            for (int i = 0; i < m_ComboPips.Length; i++)
            {
                m_ComboPips[i].anchoredPosition = new Vector2(-500f + i * 30f, 172f);
                m_ComboPips[i].SetAsLastSibling();
            }
        }

        void Punch(SkillDef skill)
        {
            if (!skill.Slot) return;
            UIAnim.Punch(skill.Slot, 0.14f, 0.3f);
        }

        void ShowBanner(string text)
        {
            if (!m_Banner) return;
            m_Banner.text = FxFormula.Rich(text);
            Gs.Show(m_BannerRoot, 1.2f);
        }

        void UpdateHUD()
        {
            if (m_Weapon == null) return;
            UpdateTargetMark();
            foreach (var skill in m_Weapon.Skills)
            {
                if (!skill.CooldownFill) continue;
                skill.CooldownFill.fillAmount = skill.Cooldown > 0f ? skill.Remaining / skill.Cooldown : 0f;
                skill.CooldownText.text = skill.Remaining > 0f ? Mathf.CeilToInt(skill.Remaining).ToString() : "";

                bool ready = skill.Remaining <= 0f;
                if (ready && !skill.WasReady) UIAnim.Punch(skill.Slot, 0.18f, 0.35f);
                skill.WasReady = ready;
            }
            // 궁극기가 준비되면 은은하게 숨쉬기
            var ultSlot = m_Weapon.Skills[3].Slot;
            if (ultSlot && m_Weapon.Skills[3].Remaining <= 0f && !DOTween.IsTweening(ultSlot))
                ultSlot.localScale = Vector3.one * (1f + 0.04f * Mathf.Sin(Time.unscaledTime * 4f));

            // 평타 단계: 이어진 단 수만큼 금색
            int step = ComboStep;
            bool live = (m_Current != null && m_CurrentIsBasic) || m_ComboWindow > 0f;
            for (int i = 0; i < m_ComboPips.Length; i++)
            {
                var img = m_ComboPips[i].GetComponent<Image>();
                Color want = live && i < step ? Gs.GoldBright : new Color(1f, 1f, 1f, 0.22f);
                img.color = Color.Lerp(img.color, want, Mathf.Clamp01(Time.unscaledDeltaTime * 18f));
                float sc = live && i == step - 1 ? 1.4f : 1f;
                m_ComboPips[i].localScale = Vector3.Lerp(m_ComboPips[i].localScale, Vector3.one * sc, Mathf.Clamp01(Time.unscaledDeltaTime * 14f));
            }
        }
    }
}
