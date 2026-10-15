using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Unity.FPS.Gameplay
{
    public class PlayerInputHandler : MonoBehaviour
    {
        [Tooltip("Sensitivity multiplier for moving the camera around")]
        public float LookSensitivity = 1f;

        [Tooltip("Additional sensitivity multiplier for WebGL")]
        public float WebglLookSensitivityMultiplier = 0.25f;

        [Tooltip("Limit to consider an input when using a trigger on a controller")]
        public float TriggerAxisThreshold = 0.4f;

        [Tooltip("Used to flip the vertical input axis")]
        public bool InvertYAxis = false;

        [Tooltip("Used to flip the horizontal input axis")]
        public bool InvertXAxis = false;

        GameFlowManager m_GameFlowManager;
        PlayerCharacterController m_PlayerCharacterController;
        bool m_FireInputWasHeld;

        private InputAction m_MoveAction;
        private InputAction m_LookAction;
        private InputAction m_JumpAction;
        private InputAction m_FireAction;
        private InputAction m_AimAction;
        private InputAction m_SprintAction;
        private InputAction m_CrouchAction;
        private InputAction m_ReloadAction;
        private InputAction m_NextWeaponAction;

        void Start()
        {
            m_PlayerCharacterController = GetComponent<PlayerCharacterController>();
            DebugUtility.HandleErrorIfNullGetComponent<PlayerCharacterController, PlayerInputHandler>(
                m_PlayerCharacterController, this, gameObject);
            m_GameFlowManager = FindAnyObjectByType<GameFlowManager>();
            DebugUtility.HandleErrorIfNullFindObject<GameFlowManager, PlayerInputHandler>(m_GameFlowManager, this);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            m_MoveAction = InputSystem.actions.FindAction("Player/Move");
            m_LookAction = InputSystem.actions.FindAction("Player/Look");
            m_JumpAction = InputSystem.actions.FindAction("Player/Jump");
            m_FireAction = InputSystem.actions.FindAction("Player/Fire");
            m_AimAction = InputSystem.actions.FindAction("Player/Aim");
            m_SprintAction = InputSystem.actions.FindAction("Player/Sprint");
            m_CrouchAction = InputSystem.actions.FindAction("Player/Crouch");
            m_ReloadAction = InputSystem.actions.FindAction("Player/Reload");
            m_NextWeaponAction = InputSystem.actions.FindAction("Player/NextWeapon");

            m_MoveAction.Enable();
            m_LookAction.Enable();
            m_JumpAction.Enable();
            m_FireAction.Enable();
            m_AimAction.Enable();
            m_SprintAction.Enable();
            m_CrouchAction.Enable();
            m_ReloadAction.Enable();
            m_NextWeaponAction.Enable();
        }

        void LateUpdate()
        {
            m_FireInputWasHeld = GetFireInputHeld();
        }

        // 안내창이 떠 있는 동안 등 플레이어 입력 전체를 막을 때 사용
        public bool InputBlocked { get; set; }

        // 근접 공격 모드 등에서 총기 사격 입력을 막을 때 사용
        public bool FireBlocked { get; set; }

        public bool CanProcessInput()
        {
            // 모바일 조작(터치)일 때는 커서 잠금이 없으므로 잠금 여부를 보지 않는다
            bool cursorOk = Cursor.lockState == CursorLockMode.Locked || VirtualInput.Active;
            return cursorOk && Time.timeScale > 0f && !m_GameFlowManager.GameIsEnding && !InputBlocked;
        }

        public Vector3 GetMoveInput()
        {
            if (CanProcessInput())
            {
                var input = m_MoveAction.ReadValue<Vector2>() + VirtualInput.Move;
                Vector3 move = new Vector3(input.x, 0f, input.y);

                // constrain move input to a maximum magnitude of 1, otherwise diagonal movement might exceed the max move speed defined
                move = Vector3.ClampMagnitude(move, 1);

                return move;
            }

            return Vector3.zero;
        }

        public float GetLookInputsHorizontal()
        {
            if (!CanProcessInput())
                return 0.0f;

            float input = m_LookAction.ReadValue<Vector2>().x;

#if UNITY_WEBGL
            // Mouse tends to be even more sensitive in WebGL due to mouse acceleration, so reduce it even more
            input *= WebglLookSensitivityMultiplier;
#endif
            input += VirtualInput.LookThisFrame.x * VirtualInput.LookScale;

            if (InvertXAxis)
                input *= -1;

            return input * LookSensitivity;
        }

        public float GetLookInputsVertical()
        {
            if (!CanProcessInput())
                return 0.0f;

            float input = m_LookAction.ReadValue<Vector2>().y;

#if UNITY_WEBGL
            // Mouse tends to be even more sensitive in WebGL due to mouse acceleration, so reduce it even more
            input *= WebglLookSensitivityMultiplier;
#endif
            input += VirtualInput.LookThisFrame.y * VirtualInput.LookScale;

            if (InvertYAxis)
                input *= -1;

            return input * LookSensitivity;
        }

        public bool GetJumpInputDown()
        {
            if (CanProcessInput())
            {
                return m_JumpAction.WasPressedThisFrame() || VirtualInput.WasPressed("jump");
            }

            return false;
        }

        public bool GetJumpInputHeld()
        {
            if (CanProcessInput())
            {
                return m_JumpAction.IsPressed() || VirtualInput.IsHeld("jump");
            }

            return false;
        }

        public bool GetFireInputDown()
        {
            return GetFireInputHeld() && !m_FireInputWasHeld;
        }

        public bool GetFireInputReleased()
        {
            return !GetFireInputHeld() && m_FireInputWasHeld;
        }

        public bool GetFireInputHeld()
        {
            if (CanProcessInput() && !FireBlocked)
            {
                return m_FireAction.IsPressed() || VirtualInput.IsHeld("fire");
            }

            return false;
        }

        public bool GetAimInputHeld()
        {
            if (CanProcessInput())
            {
                return m_AimAction.IsPressed() || VirtualInput.IsHeld("aim");
            }

            return false;
        }

        public bool GetSprintInputHeld()
        {
            if (CanProcessInput())
            {
                return m_SprintAction.IsPressed() || VirtualInput.IsHeld("sprint");
            }

            return false;
        }

        public bool GetCrouchInputDown()
        {
            if (CanProcessInput())
            {
                return m_CrouchAction.WasPressedThisFrame() || VirtualInput.WasPressed("crouch");
            }

            return false;
        }

        public bool GetCrouchInputReleased()
        {
            if (CanProcessInput())
            {
                return m_CrouchAction.WasReleasedThisFrame();
            }

            return false;
        }

        public bool GetReloadButtonDown()
        {
            if (CanProcessInput())
            {
                return m_ReloadAction.WasPressedThisFrame() || VirtualInput.WasPressed("reload");
            }

            return false;
        }

        public int GetSwitchWeaponInput()
        {
            if (CanProcessInput())
            {
                if (VirtualInput.WasPressed("prevWeapon"))
                    return -1;
                if (VirtualInput.WasPressed("nextWeapon"))
                    return 1;

                // Q = 이전 무기, E = 다음 무기 (누를 때 한 번만 전환)
                Keyboard keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    if (keyboard.qKey.wasPressedThisFrame)
                        return -1;
                    if (keyboard.eKey.wasPressedThisFrame)
                        return 1;
                    if (keyboard.qKey.isPressed || keyboard.eKey.isPressed)
                        return 0;
                }

                var input = m_NextWeaponAction.ReadValue<float>();

                if (input > 0f)
                    return -1;

                if (input < 0f)
                    return 1;
            }

            return 0;
        }

        public int GetSelectWeaponInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (CanProcessInput() && keyboard != null)
            {
                Key[] keys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9, Key.Digit0 };
                for (int i = 0; i < keys.Length; i++)
                    if (keyboard[keys[i]].wasPressedThisFrame)
                        return i + 1;
            }

            return 0;
        }
    }
}
