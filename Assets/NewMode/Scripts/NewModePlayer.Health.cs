using System;
using DG.Tweening;
using TMPro;
using Unity.FPS.MathMode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Unity.FPS.NewMode
{
    // 플레이어 체력 — 쉽게, 하지만 방심하면 쓰러질 수 있게
    //  · 최대 100, 맞으면 0.4초 무적, 3초 동안 안 맞으면 초당 8씩 회복
    //  · 쓰러지면: 마지막 지점에서 다시 / 처음부터
    //  · 맵 밖으로 떨어지면 마지막 지점으로 (체력 −10)
    public partial class NewModePlayer
    {
        public static NewModePlayer Instance { get; private set; }

        public float MaxHP = 100f;
        public float HP { get; private set; }
        public bool Dead { get; private set; }
        /// <summary>컷신 중 등 — 피해를 받지 않음</summary>
        public bool Invulnerable { get; set; }
        public event Action Revived;

        Vector3 m_Checkpoint;
        float m_CheckpointYaw;
        float m_LastHurt = -99f, m_IFrames;
        Vector3 m_KnockVel;

        RectTransform m_HpBar;
        Image m_HpFill, m_HpLag, m_Hurt;
        TextMeshProUGUI m_HpText;
        GameObject m_DeathPanel;

        void InitHealth()
        {
            Instance = this;
            MaxHP = WeaponCatalog.Current.Stats.MaxHP; // 직업마다 다른 최대 체력
            HP = MaxHP;
            m_Checkpoint = transform.position;
            m_CheckpointYaw = transform.eulerAngles.y;
        }

        public void SetCheckpoint(Vector3 position, float yaw)
        {
            m_FollowInit = false;
            m_Checkpoint = position;
            m_CheckpointYaw = yaw;
        }

        public void Teleport(Vector3 position, float yaw)
        {
            m_FollowInit = false;
            m_MoveVel = Vector3.zero;
            m_Controller.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            m_Controller.enabled = true;
            m_VerticalVelocity = 0f;
            m_KnockVel = Vector3.zero;
            m_Yaw = yaw;
        }

        /// <summary>맞았다. knock: 밀려나는 세기 (캐릭터 크기 기준)</summary>
        public void TakeDamage(float amount, Vector3 from, float knock = 0f)
        {
            if (Dead || Invulnerable || m_IFrames > 0f || m_Paused) return;
            amount *= 1f - WeaponCatalog.Current.Stats.Defense; // 직업 방어력
            HP = Mathf.Max(0f, HP - amount);
            m_IFrames = 0.4f;
            m_LastHurt = Time.time;

            Vector3 away = transform.position - from;
            away.y = 0f;
            if (away.sqrMagnitude > 0.0001f) m_KnockVel = away.normalized * knock * Scale;
            Shake(0.08f + amount * 0.006f, 0.25f);
            GeoFx.Popup(transform.position + Vector3.up * 2f * Scale, $"-{Mathf.RoundToInt(amount)}", new Color(1f, 0.3f, 0.3f), Scale, 1.1f);
            if (m_Hurt)
            {
                m_Hurt.DOKill();
                m_Hurt.color = new Color(1f, 0.1f, 0.1f, 0.35f);
                m_Hurt.DOFade(0f, 0.5f).SetLink(m_Hurt.gameObject);
            }
            if (m_HpBar) UIAnim.Punch(m_HpBar, 0.08f, 0.25f);
            if (HP <= 0f) Die();
        }

        void UpdateHealth()
        {
            m_IFrames -= Time.deltaTime;
            if (!Dead && HP < MaxHP && Time.time - m_LastHurt > 3f)
                HP = Mathf.Min(MaxHP, HP + 8f * Time.deltaTime);

            if (!m_HpFill) return;
            float fill = HP / MaxHP;
            m_HpFill.fillAmount = fill;
            m_HpLag.fillAmount = Mathf.MoveTowards(m_HpLag.fillAmount, fill, Time.unscaledDeltaTime * 0.5f);
            if (m_HpLag.fillAmount < fill) m_HpLag.fillAmount = fill;
            m_HpFill.color = fill > 0.5f ? new Color32(140, 215, 90, 255) : fill > 0.25f ? new Color32(240, 200, 80, 255) : new Color32(235, 90, 80, 255);
            m_HpText.text = $"<b>HP</b>  {Mathf.CeilToInt(HP)} / {Mathf.RoundToInt(MaxHP)}";
        }

        void FallRecover()
        {
            Teleport(m_Checkpoint, m_CheckpointYaw);
            if (!Dead) { HP = Mathf.Max(1f, HP - 10f); m_LastHurt = Time.time; }
        }

        void Die()
        {
            Dead = true;
            InputBlocked = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (m_Animator) m_Animator.SetFloat("Vert", 0f);
            if (m_DeathPanel) Destroy(m_DeathPanel);
            var canvas = UIUtil.Canvas(transform, "Defeat", 45);
            m_DeathPanel = canvas.gameObject;
            var dim = Gs.Dim(canvas.transform, 0.6f);
            dim.GetComponent<Image>().color = new Color(0.2f, 0.03f, 0.06f, 0.55f);
            UIAnim.FadeIn(UIAnim.Group(dim), 0.5f);
            var panel = Gs.Panel(canvas.transform, "Panel", false);
            UIUtil.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 460f));
            UIAnim.PopIn(panel, 0.1f, 0.45f);
            Gs.Header(panel, "쓰러졌어요", 56f, new Vector2(0f, -70f), 760f, new Color32(170, 70, 60, 255));
            var sub = Gs.Text(panel, "적이 빨갛게 깜빡이면 공격이 와요. 보스의 빨간 원은 피하고, 주황 고리는 점프!", 26f, TextAlignmentOptions.Center, Gs.Ink);
            UIUtil.Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(800f, 80f));

            var again = Gs.Button(panel, "<b>마지막 지점에서 다시</b>", new Color(0.35f, 0.75f, 0.5f), Revive, new Vector2(400f, 90f), 32f, true);
            UIUtil.Place((RectTransform)again.transform, new Vector2(0.5f, 0f), new Vector2(-215f, 80f), new Vector2(400f, 90f));
            var restart = Gs.Button(panel, "처음부터", Gs.InkSoft,
                () => SceneManager.LoadScene(SceneManager.GetActiveScene().name), new Vector2(320f, 90f), 32f);
            UIUtil.Place((RectTransform)restart.transform, new Vector2(0.5f, 0f), new Vector2(225f, 80f), new Vector2(320f, 90f));
        }

        void Revive()
        {
            if (m_DeathPanel) Destroy(m_DeathPanel);
            Dead = false;
            HP = MaxHP;
            m_IFrames = 2f; // 되살아난 직후 잠깐 무적
            Teleport(m_Checkpoint, m_CheckpointYaw);
            InputBlocked = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Revived?.Invoke();
        }

        void BuildHealthUI(Transform canvas, TMP_FontAsset font)
        {
            m_Hurt = UIUtil.Image(canvas, "Hurt", new Color(1f, 0.1f, 0.1f, 0f)).GetComponent<Image>();
            UIUtil.Stretch(m_Hurt.rectTransform, 0f);
            m_Hurt.raycastTarget = false;
            m_Hurt.transform.SetAsFirstSibling();

            m_HpBar = UIUtil.RoundedImage(canvas, "HP", new Color(0.05f, 0.06f, 0.1f, 0.75f));
            m_HpBar.GetComponent<Image>().sprite = Gs.Pill;
            UIUtil.Place(m_HpBar, new Vector2(0.5f, 0f), new Vector2(0f, 58f), new Vector2(560f, 24f));
            m_HpBar.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.5f);
            m_HpLag = UIUtil.RoundedImage(m_HpBar, "Lag", new Color(1f, 0.9f, 0.9f, 0.7f)).GetComponent<Image>();
            UIUtil.Stretch(m_HpLag.rectTransform, 3f);
            m_HpLag.sprite = Gs.Pill;
            m_HpLag.type = Image.Type.Filled;
            m_HpLag.fillMethod = Image.FillMethod.Horizontal;
            m_HpFill = UIUtil.RoundedImage(m_HpBar, "Fill", new Color(0.35f, 0.9f, 0.45f)).GetComponent<Image>();
            UIUtil.Stretch(m_HpFill.rectTransform, 3f);
            m_HpFill.sprite = Gs.Pill;
            m_HpFill.type = Image.Type.Filled;
            m_HpFill.fillMethod = Image.FillMethod.Horizontal;
            m_HpText = Gs.Text(m_HpBar, "", 20f, TextAlignmentOptions.Center, Color.white, true);
            UIUtil.Stretch(m_HpText.rectTransform, 0f);
            var cls = Gs.Text(m_HpBar, $"<b>{WeaponCatalog.Current.ClassName}</b>", 22f, TextAlignmentOptions.Left, Gs.Cream, true);
            cls.rectTransform.anchorMin = new Vector2(0f, 1f);
            cls.rectTransform.anchorMax = new Vector2(1f, 1f);
            cls.rectTransform.pivot = new Vector2(0.5f, 0f);
            cls.rectTransform.sizeDelta = new Vector2(0f, 30f);
            cls.rectTransform.anchoredPosition = new Vector2(6f, 2f);
            m_HpText.outlineWidth = 0.25f;
            m_HpText.outlineColor = new Color(0f, 0f, 0f, 0.8f);
        }
    }
}
