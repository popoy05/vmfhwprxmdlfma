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
            HP = MaxHP;
            m_Checkpoint = transform.position;
            m_CheckpointYaw = transform.eulerAngles.y;
        }

        public void SetCheckpoint(Vector3 position, float yaw)
        {
            m_Checkpoint = position;
            m_CheckpointYaw = yaw;
        }

        public void Teleport(Vector3 position, float yaw)
        {
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
            m_HpFill.color = fill > 0.5f ? new Color(0.35f, 0.9f, 0.45f) : fill > 0.25f ? new Color(1f, 0.8f, 0.3f) : new Color(1f, 0.3f, 0.3f);
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

            TMP_FontAsset font = MathModeAssets.Load() ? MathModeAssets.Load().Font : null;
            var canvas = UIUtil.Canvas(transform, "Defeat", 45);
            m_DeathPanel = canvas.gameObject;
            var dim = UIUtil.Image(canvas.transform, "Dim", new Color(0.25f, 0.02f, 0.05f, 0.6f));
            UIUtil.Stretch(dim, 0f);
            UIAnim.FadeIn(UIAnim.Group(dim), 0.5f);
            var title = UIUtil.Text(canvas.transform, font, "<b>쓰러졌어요…</b>", 96f, TextAlignmentOptions.Center);
            UIUtil.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(1200f, 130f));
            title.color = new Color(1f, 0.55f, 0.5f);
            UIAnim.PopIn(title.transform, 0.1f, 0.5f);
            var sub = UIUtil.Text(canvas.transform, font, "적의 빨간 공격 표시를 피하고, 점프로 충격파를 넘어 보세요", 30f, TextAlignmentOptions.Center);
            UIUtil.Place(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 50f), new Vector2(1200f, 50f));

            var again = UIUtil.Button(canvas.transform, font, "<b>마지막 지점에서 다시</b>", new Color(0.2f, 0.72f, 0.42f), Revive, new Vector2(460f, 100f), 34f);
            UIUtil.Place((RectTransform)again.transform, new Vector2(0.5f, 0.5f), new Vector2(-250f, -100f), new Vector2(460f, 100f));
            var restart = UIUtil.Button(canvas.transform, font, "처음부터", new Color(0.35f, 0.35f, 0.5f),
                () => SceneManager.LoadScene(SceneManager.GetActiveScene().name), new Vector2(360f, 100f), 34f);
            UIUtil.Place((RectTransform)restart.transform, new Vector2(0.5f, 0.5f), new Vector2(250f, -100f), new Vector2(360f, 100f));
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

            m_HpBar = UIUtil.RoundedImage(canvas, "HP", new Color(0.03f, 0.05f, 0.12f, 0.8f));
            UIUtil.Place(m_HpBar, new Vector2(0.5f, 0f), new Vector2(0f, 222f), new Vector2(560f, 34f));
            m_HpLag = UIUtil.RoundedImage(m_HpBar, "Lag", new Color(1f, 0.9f, 0.9f, 0.7f)).GetComponent<Image>();
            UIUtil.Stretch(m_HpLag.rectTransform, 4f);
            m_HpLag.type = Image.Type.Filled;
            m_HpLag.fillMethod = Image.FillMethod.Horizontal;
            m_HpFill = UIUtil.RoundedImage(m_HpBar, "Fill", new Color(0.35f, 0.9f, 0.45f)).GetComponent<Image>();
            UIUtil.Stretch(m_HpFill.rectTransform, 4f);
            m_HpFill.type = Image.Type.Filled;
            m_HpFill.fillMethod = Image.FillMethod.Horizontal;
            m_HpText = UIUtil.Text(m_HpBar, font, "", 22f, TextAlignmentOptions.Center);
            UIUtil.Stretch(m_HpText.rectTransform, 0f);
            m_HpText.outlineWidth = 0.25f;
            m_HpText.outlineColor = new Color(0f, 0f, 0f, 0.8f);
        }
    }
}
