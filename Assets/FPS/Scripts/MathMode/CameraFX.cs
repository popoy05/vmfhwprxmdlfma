using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.FPS.MathMode
{
    // 카메라 연출: 흔들림(Shake), 시야각 확대(FovKick), 화면 번쩍임(Flash), 타격 순간 멈춤(HitStop).
    // 플레이어 컨트롤러와 무기 매니저가 매 프레임 카메라 값을 정한 '뒤'에 더하고, 다음 프레임 '앞'에서 되돌린다.
    [DefaultExecutionOrder(1000)]
    public class CameraFX : MonoBehaviour
    {
        Camera m_Camera;
        Camera m_WeaponCamera;
        PlayerWeaponsManager m_Weapons;
        Image m_FlashImage;

        float m_ShakeAmp, m_ShakeEnd, m_ShakeDuration;
        float m_FovAdd, m_FovEnd, m_FovDuration;
        Color m_FlashColor;
        float m_FlashEnd, m_FlashDuration;
        float m_HitStopEnd;
        bool m_HitStopping;

        float m_AppliedFov;

        public void Init(MathModeManager manager)
        {
            var controller = GetComponent<PlayerCharacterController>();
            m_Camera = controller ? controller.PlayerCamera : Camera.main;
            m_Weapons = GetComponent<PlayerWeaponsManager>();
            m_WeaponCamera = m_Weapons ? m_Weapons.WeaponCamera : null;

            Canvas canvas = UIUtil.Canvas(transform, "CameraFX", 45);
            m_FlashImage = UIUtil.Image(canvas.transform, "Flash", Color.clear).GetComponent<Image>();
            UIUtil.Stretch((RectTransform)m_FlashImage.transform, 0f);
        }

        public void Shake(float amplitude, float duration)
        {
            if (amplitude < m_ShakeAmp * Remaining(m_ShakeEnd, m_ShakeDuration)) return;
            m_ShakeAmp = amplitude;
            m_ShakeDuration = duration;
            m_ShakeEnd = Time.unscaledTime + duration;
        }

        public void FovKick(float degrees, float duration)
        {
            m_FovAdd = degrees;
            m_FovDuration = duration;
            m_FovEnd = Time.unscaledTime + duration;
        }

        public void Flash(Color color, float duration)
        {
            m_FlashColor = color;
            m_FlashDuration = duration;
            m_FlashEnd = Time.unscaledTime + duration;
        }

        // 타격감: 아주 짧게 시간을 거의 멈춘다 (궁극기 연출 중에는 건너뜀)
        public void HitStop(float seconds)
        {
            if (Time.timeScale < 0.99f && !m_HitStopping) return;
            m_HitStopping = true;
            m_HitStopEnd = Time.unscaledTime + seconds;
            Time.timeScale = 0.05f;
        }

        static float Remaining(float end, float duration) => duration <= 0f ? 0f : Mathf.Clamp01((end - Time.unscaledTime) / duration);

        void Update()
        {
            // 지난 프레임에 더했던 값을 되돌린다
            if (m_Camera)
            {
                m_Camera.fieldOfView -= m_AppliedFov;
                if (m_WeaponCamera) m_WeaponCamera.fieldOfView = m_Camera.fieldOfView * (m_Weapons ? m_Weapons.WeaponFovMultiplier : 1f);
                m_AppliedFov = 0f;
            }

            if (m_HitStopping && Time.unscaledTime >= m_HitStopEnd)
            {
                m_HitStopping = false;
                if (Time.timeScale < 0.1f) Time.timeScale = 1f;
            }
        }

        void LateUpdate()
        {
            if (!m_Camera) return;

            // 흔들림: 시간이 지날수록 약해지는 무작위 위치·회전
            float shake = m_ShakeAmp * Remaining(m_ShakeEnd, m_ShakeDuration);
            if (shake > 0.0001f)
            {
                // 회전만 흔든다 (회전은 플레이어 컨트롤러가 매 프레임 새로 정하므로 누적되지 않음)
                m_Camera.transform.localRotation *= Quaternion.Euler(Random.Range(-1f, 1f) * shake * 2.5f, Random.Range(-1f, 1f) * shake * 1.5f, Random.Range(-1f, 1f) * shake * 3f);
            }

            // 시야각: 무기 매니저가 정한 값 위에 더한다 (다음 프레임에 무기 매니저가 원래대로 보간)
            float fov = m_FovAdd * Mathf.Sin(Remaining(m_FovEnd, m_FovDuration) * Mathf.PI * 0.5f);
            if (Mathf.Abs(fov) > 0.01f)
            {
                m_Camera.fieldOfView += fov;
                if (m_WeaponCamera) m_WeaponCamera.fieldOfView = m_Camera.fieldOfView * (m_Weapons ? m_Weapons.WeaponFovMultiplier : 1f);
                m_AppliedFov = fov;
            }

            // 화면 번쩍임
            float flash = Remaining(m_FlashEnd, m_FlashDuration);
            Color c = m_FlashColor;
            c.a *= flash;
            m_FlashImage.color = c;
            m_FlashImage.enabled = c.a > 0.001f;
        }

        void OnDisable()
        {
            if (m_HitStopping && Time.timeScale < 0.1f) Time.timeScale = 1f;
        }
    }
}
