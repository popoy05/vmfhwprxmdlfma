using TMPro;
using Unity.FPS.MathMode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Unity.FPS.NewMode
{
    // 새 모드의 1인칭 플레이어: 맵 안을 걸어다니기만 하는 간단한 조작.
    //  WASD/방향키 이동 · 마우스 시점 · Shift 달리기 · Space 점프 · Esc 일시정지
    //  게임패드: 왼쪽 스틱 이동 · 오른쪽 스틱 시점 · A 점프 · 왼쪽 스틱 누르기 달리기 · Start 일시정지
    [RequireComponent(typeof(CharacterController))]
    public class NewModePlayer : MonoBehaviour
    {
        [Header("이동")]
        public float WalkSpeed = 6f;
        public float RunSpeed = 12f;
        public float JumpHeight = 1.6f;
        public float Gravity = -20f;

        [Header("시점")]
        public float MouseSensitivity = 0.12f;
        public float GamepadLookSpeed = 160f;
        public float EyeHeight = 1.65f;

        [Tooltip("이 높이보다 아래로 떨어지면 시작 위치로 되돌린다")]
        public float FallLimitY = -50f;

        CharacterController m_Controller;
        Camera m_Camera;
        float m_Pitch;
        float m_VerticalVelocity;
        Vector3 m_SpawnPosition;
        Quaternion m_SpawnRotation;
        bool m_Paused;
        GameObject m_PausePanel;

        void Awake()
        {
            m_Controller = GetComponent<CharacterController>();
            m_Camera = GetComponentInChildren<Camera>();
            if (!m_Camera)
            {
                var cam = new GameObject("Player Camera") { tag = "MainCamera" };
                cam.transform.SetParent(transform, false);
                m_Camera = cam.AddComponent<Camera>();
                cam.AddComponent<AudioListener>();
            }

            m_Camera.transform.localPosition = new Vector3(0f, EyeHeight, 0f);
            m_Camera.transform.localRotation = Quaternion.identity;
            m_SpawnPosition = transform.position;
            m_SpawnRotation = transform.rotation;
        }

        void Start()
        {
            Time.timeScale = 1f;
            UIUtil.EnsureEventSystem();
            BuildUI();
            SetPaused(false);
        }

        void Update()
        {
            Keyboard kb = Keyboard.current;
            Gamepad pad = Gamepad.current;

            if ((kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.startButton.wasPressedThisFrame))
                SetPaused(!m_Paused);
            if (m_Paused) return;

            // 창 밖을 눌렀다가 돌아오면 다시 커서를 잠근다
            if (Cursor.lockState != CursorLockMode.Locked && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                LockCursor(true);

            Look(pad);
            Move(kb, pad);

            if (transform.position.y < FallLimitY) Respawn();
        }

        void Look(Gamepad pad)
        {
            Vector2 look = Vector2.zero;
            if (Mouse.current != null && Cursor.lockState == CursorLockMode.Locked)
                look += Mouse.current.delta.ReadValue() * MouseSensitivity;
            if (pad != null)
                look += pad.rightStick.ReadValue() * GamepadLookSpeed * Time.deltaTime;

            transform.Rotate(0f, look.x, 0f, Space.Self);
            m_Pitch = Mathf.Clamp(m_Pitch - look.y, -85f, 85f);
            m_Camera.transform.localRotation = Quaternion.Euler(m_Pitch, 0f, 0f);
        }

        void Move(Keyboard kb, Gamepad pad)
        {
            Vector2 input = Vector2.zero;
            bool run = false, jump = false;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.y -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x -= 1f;
                run |= kb.leftShiftKey.isPressed;
                jump |= kb.spaceKey.wasPressedThisFrame;
            }

            if (pad != null)
            {
                input += pad.leftStick.ReadValue();
                run |= pad.leftStickButton.isPressed;
                jump |= pad.buttonSouth.wasPressedThisFrame;
            }

            input = Vector2.ClampMagnitude(input, 1f);
            float speed = run ? RunSpeed : WalkSpeed;
            Vector3 move = (transform.right * input.x + transform.forward * input.y) * speed;

            bool grounded = m_Controller.isGrounded;
            if (grounded && m_VerticalVelocity < 0f) m_VerticalVelocity = -2f; // 경사면에 붙어 있게
            if (grounded && jump) m_VerticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
            m_VerticalVelocity += Gravity * Time.deltaTime;
            move.y = m_VerticalVelocity;

            m_Controller.Move(move * Time.deltaTime);
        }

        void Respawn()
        {
            m_Controller.enabled = false;
            transform.SetPositionAndRotation(m_SpawnPosition, m_SpawnRotation);
            m_Controller.enabled = true;
            m_VerticalVelocity = 0f;
        }

        // ---------------------------------------------------------------- UI

        void BuildUI()
        {
            TMP_FontAsset font = MathModeAssets.Load() ? MathModeAssets.Load().Font : null;
            Canvas canvas = UIUtil.Canvas(transform, "NewMode HUD", 10);

            var crosshair = UIUtil.RoundedImage(canvas.transform, "Crosshair", new Color(1f, 1f, 1f, 0.8f), circle: true);
            UIUtil.Place(crosshair, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(8f, 8f));

            var help = UIUtil.Text(canvas.transform, font,
                "WASD 이동 · 마우스 시점 · Shift 달리기 · Space 점프 · Esc 메뉴", 22f, TextAlignmentOptions.Left);
            UIUtil.Place(help.rectTransform, new Vector2(0f, 0f), new Vector2(440f, 36f), new Vector2(820f, 40f));
            help.color = new Color(1f, 1f, 1f, 0.75f);
            help.outlineWidth = 0.2f;
            help.outlineColor = new Color(0f, 0f, 0f, 0.8f);

            // 일시정지 메뉴
            var panel = UIUtil.Image(canvas.transform, "Pause", new Color(0.02f, 0.04f, 0.1f, 0.7f));
            UIUtil.Stretch(panel, 0f);
            panel.GetComponent<Image>().raycastTarget = true;
            m_PausePanel = panel.gameObject;

            var title = UIUtil.Text(panel, font, "<b>일시정지</b>", 80f, TextAlignmentOptions.Center);
            UIUtil.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 200f), new Vector2(800f, 110f));

            var resume = UIUtil.Button(panel, font, "<b>계속하기</b>", new Color(0.2f, 0.72f, 0.42f), () => SetPaused(false), new Vector2(420f, 90f), 38f);
            UIUtil.Place((RectTransform)resume.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(420f, 90f));
            var menu = UIUtil.Button(panel, font, "모드 선택으로", new Color(0.25f, 0.35f, 0.6f), BackToMenu, new Vector2(420f, 90f), 34f);
            UIUtil.Place((RectTransform)menu.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(420f, 90f));
        }

        void SetPaused(bool paused)
        {
            m_Paused = paused;
            Time.timeScale = paused ? 0f : 1f;
            if (m_PausePanel) m_PausePanel.SetActive(paused);
            LockCursor(!paused);
        }

        static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void BackToMenu()
        {
            Time.timeScale = 1f;
            AppBootstrap.ClassicMenu = false;
            SceneManager.LoadScene(AppBootstrap.MenuScene);
        }
    }
}
