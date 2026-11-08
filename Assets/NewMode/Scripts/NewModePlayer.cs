using TMPro;
using Unity.FPS.MathMode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Unity.FPS.NewMode
{
    // 새 모드의 3인칭 플레이어: 꾸민 캐릭터로 맵 안을 돌아다닌다.
    //  WASD/방향키 이동(카메라 기준) · 마우스 시점 · 휠 줌 · Shift 달리기 · Space 점프 · Esc 일시정지
    //  좌클릭 평타 · Q/E/X/C 스킬 (NewModeCombat)
    //  게임패드: 왼쪽 스틱 이동 · 오른쪽 스틱 시점 · A 점프 · 왼쪽 스틱 누르기 달리기 · Start 일시정지
    //
    // 크기: Player 오브젝트의 Scale을 줄이면(예: 미니 맵에서 0.5) 캐릭터·속도·점프·카메라·스킬 범위가 모두 같은 비율로 줄어든다.
    [RequireComponent(typeof(CharacterController))]
    public partial class NewModePlayer : MonoBehaviour
    {
        [Header("이동 (Scale 1 기준)")]
        public float WalkSpeed = 3.2f;
        public float RunSpeed = 7f;
        public float JumpHeight = 1.4f;
        public float Gravity = -20f;
        public float TurnSpeed = 720f;

        [Header("카메라 (3인칭, Scale 1 기준)")]
        public float MouseSensitivity = 0.12f;
        public float GamepadLookSpeed = 160f;
        public float CameraDistance = 4.5f;
        public float MinDistance = 1.5f;
        public float MaxDistance = 9f;
        public float CameraHeight = 1.5f;

        [Tooltip("이 높이보다 아래로 떨어지면 시작 위치로 되돌린다")]
        public float FallLimitY = -50f;

        // 이전 버전(1인칭) 씬에 저장된 값 — 쓰지 않지만 직렬화 호환을 위해 남겨 둠
        [HideInInspector] public float EyeHeight = 1.65f;

        CharacterController m_Controller;
        Camera m_Camera;
        Animator m_Animator;
        NewModeCombat m_Combat;
        float m_Yaw, m_Pitch = 20f;
        float m_VerticalVelocity;
        float m_AnimSpeed, m_AnimState;
        float m_AirTime;
        float m_Coyote, m_JumpBuffer;
        float m_JumpPose; // 점프를 누른 순간부터 바로 점프 동작 (땅을 떠날 때까지 기다리지 않음)
        Vector3 m_SpawnPosition;
        Quaternion m_SpawnRotation;
        bool m_Paused;
        GameObject m_PausePanel;

        static readonly int k_Vert = Animator.StringToHash("Vert");
        static readonly int k_Hor = Animator.StringToHash("Hor");
        static readonly int k_State = Animator.StringToHash("State");
        static readonly int k_IsJump = Animator.StringToHash("IsJump");

        /// <summary>캐릭터 크기 배율 (Player 오브젝트의 Scale)</summary>
        public float Scale => transform.lossyScale.y;
        /// <summary>지금 누르고 있는 이동 방향 (카메라 기준을 월드로 바꾼 것, 안 누르면 0)</summary>
        public Vector3 MoveInputWorld { get; private set; }
        public float Yaw => m_Yaw;
        public bool Paused => m_Paused;
        public bool Grounded => m_Controller.isGrounded;
        public Animator Animator => m_Animator;
        public CharacterController Controller => m_Controller;
        public Camera Camera => m_Camera;
        /// <summary>true면 컷신이 카메라를 직접 움직인다</summary>
        public bool CameraOverride { get; set; }
        /// <summary>true면 이동·시점 입력을 받지 않는다 (수학 문제 창 등)</summary>
        public bool InputBlocked { get; set; }
        /// <summary>총 조준 중 (어깨 너머 확대 시점)</summary>
        public bool Aiming { get; set; }
        public float AimBlend => m_AimBlend;
        /// <summary>화면 한가운데(조준점)로 나가는 광선</summary>
        public Ray AimRay => m_Camera ? m_Camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)) : new Ray(transform.position, transform.forward);
        float m_AimBlend, m_BaseFov = 60f;
        Vector3 m_Follow;
        bool m_FollowInit;
        Vector3 m_MoveVel; // 지금 수평 이동 속도 (부드럽게 가속·감속)

        float m_ShakeAmp, m_ShakeTime, m_ShakeDuration;
        /// <summary>화면 흔들기 (amp: 캐릭터 크기 기준 m)</summary>
        public void Shake(float amp, float duration)
        {
            if (m_ShakeDuration > 0f && amp < m_ShakeAmp * m_ShakeTime / m_ShakeDuration) return; // 더 센 흔들림이 진행 중
            m_ShakeAmp = amp; m_ShakeDuration = m_ShakeTime = duration;
        }

        public Vector3 ShakeOffset()
        {
            if (m_ShakeTime <= 0f) return Vector3.zero;
            m_ShakeTime -= Time.unscaledDeltaTime;
            float k = Mathf.Max(0f, m_ShakeTime / m_ShakeDuration);
            float t = Time.unscaledTime * 38f;
            return new Vector3(Mathf.PerlinNoise(t, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, t) - 0.5f, Mathf.PerlinNoise(t, t * 0.5f) - 0.5f) * 2f * m_ShakeAmp * k * k * Scale;
        }

        void Awake()
        {
            m_Controller = GetComponent<CharacterController>();
            m_SpawnPosition = transform.position;
            m_SpawnRotation = transform.rotation;
            m_Yaw = transform.eulerAngles.y;
            gameObject.layer = 2; // Ignore Raycast: 스킬이 땅 높이를 잴 때 플레이어 몸에 걸리지 않도록

            // 카메라는 플레이어를 따라다니되 회전은 따로 하도록 떼어낸다 (씬에 있던 카메라 설정은 그대로 사용)
            m_Camera = GetComponentInChildren<Camera>();
            if (!m_Camera)
            {
                m_Camera = new GameObject("Player Camera") { tag = "MainCamera" }.AddComponent<Camera>();
                m_Camera.gameObject.AddComponent<AudioListener>();
            }
            m_Camera.transform.SetParent(null, true);
            m_Camera.nearClipPlane = Mathf.Min(0.1f, 0.1f * Scale);
            m_BaseFov = m_Camera.fieldOfView;

            GameObject model = PlayerAppearance.Spawn(transform);
            if (model) m_Animator = model.GetComponentInChildren<Animator>();
            // 캐릭터 컨트롤러는 바닥과 skinWidth만큼 띄워 두므로, 모델을 그만큼 내려 발이 땅에 닿게 한다
            if (model) model.transform.localPosition = new Vector3(0f, -m_Controller.skinWidth, 0f);

            m_Combat = GetComponent<NewModeCombat>();
            if (!m_Combat) m_Combat = gameObject.AddComponent<NewModeCombat>();
            InitHealth();
        }

        void Start()
        {
            Time.timeScale = 1f;
            UIUtil.EnsureEventSystem();
            BuildUI();
            SetPaused(false);
            UpdateCamera();
            NewModeTouch.Attach(this); // 휴대폰: 터치 조작 화면
        }

        void OnDestroy()
        {
            if (m_Camera) Destroy(m_Camera.gameObject);
        }

        void Update()
        {
            Keyboard kb = Keyboard.current;
            Gamepad pad = Gamepad.current;

            UpdateHealth();
            if (InputBlocked && !m_Paused)
            {
                Move(null, null); // 입력 없이 중력만
                return;
            }
            if ((kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.startButton.wasPressedThisFrame)
                || Unity.FPS.Gameplay.VirtualInput.WasPressed("pause"))
                SetPaused(!m_Paused);
            if (m_Paused) return;

            // 창 밖을 눌렀다가 돌아오면 다시 커서를 잠근다
            if (!NewModeTouch.Enabled && Cursor.lockState != CursorLockMode.Locked && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                LockCursor(true);

            Look(pad);
            Move(kb, pad);

            if (transform.position.y < FallLimitY) FallRecover();
        }

        void LateUpdate()
        {
            if (!m_Paused && !CameraOverride) UpdateCamera();
        }

        void Look(Gamepad pad)
        {
            Vector2 look = Vector2.zero;
            Mouse mouse = Mouse.current;
            if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
            {
                look += mouse.delta.ReadValue() * MouseSensitivity;
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                    CameraDistance = Mathf.Clamp(CameraDistance - Mathf.Sign(scroll) * 0.5f, MinDistance, MaxDistance);
            }
            if (pad != null)
                look += pad.rightStick.ReadValue() * GamepadLookSpeed * Time.deltaTime;
            // 터치: 오른쪽 화면을 끈 만큼 (화면 높이 기준이라 기기 해상도와 상관없이 같은 느낌)
            if (Unity.FPS.Gameplay.VirtualInput.Active)
                look += Unity.FPS.Gameplay.VirtualInput.LookThisFrame * (0.2f * 1080f / Mathf.Max(1, Screen.height)) * GameSettings.Sensitivity;

            look *= Mathf.Lerp(1f, 0.45f, m_AimBlend); // 조준 중엔 천천히
            m_Yaw += look.x;
            m_Pitch = Mathf.Clamp(m_Pitch - look.y, -35f, 70f);
        }

        void Move(Keyboard kb, Gamepad pad)
        {
            // 도약·회전 같은 스킬이 캐릭터를 직접 움직이는 중이면 이동 입력과 중력을 쉰다
            if (m_Combat && m_Combat.OverridesMotion)
            {
                m_VerticalVelocity = 0f;
                m_MoveVel = Vector3.zero;
                Animate(0f, false, m_Controller.isGrounded);
                return;
            }

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
            if (Unity.FPS.Gameplay.VirtualInput.Active)
            {
                input += Unity.FPS.Gameplay.VirtualInput.Move;
                run |= Unity.FPS.Gameplay.VirtualInput.IsHeld("sprint");
                jump |= Unity.FPS.Gameplay.VirtualInput.WasPressed("jump");
            }

            input = Vector2.ClampMagnitude(input, 1f);
            MoveInputWorld = Quaternion.Euler(0f, m_Yaw, 0f) * new Vector3(input.x, 0f, input.y);

            // 공격하는 동안은 제자리에서 휘두른다 (이동은 천천히만)
            bool attacking = m_Combat && m_Combat.LocksMovement;
            if (attacking)
            {
                input *= 0.35f; // 공격 중에도 조금은 움직일 수 있게 (뻑뻑하지 않게)
                jump = false;
            }

            // 카메라가 보는 방향 기준으로 움직이고, 캐릭터는 가는 방향을 바라본다
            Quaternion camYaw = Quaternion.Euler(0f, m_Yaw, 0f);
            Vector3 direction = camYaw * new Vector3(input.x, 0f, input.y);
            if (Aiming)
            {
                // 조준 중: 몸은 항상 조준 방향, 걷기만
                transform.rotation = Quaternion.Euler(0f, m_Yaw, 0f);
                run = false;
                input *= 0.6f;
                direction = camYaw * new Vector3(input.x, 0f, input.y);
            }
            else if (!attacking && direction.sqrMagnitude > 0.001f)
            {
                Quaternion target = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, TurnSpeed * Time.deltaTime);
            }

            float s = Scale;
            float speed = (run ? RunSpeed : WalkSpeed) * s * WeaponCatalog.Current.Stats.Move;
            Vector3 wantVel = direction * speed;
            float accel = m_Controller.isGrounded ? (wantVel.sqrMagnitude > m_MoveVel.sqrMagnitude ? 14f : 18f) : 5f;
            m_MoveVel = Vector3.Lerp(m_MoveVel, wantVel, 1f - Mathf.Exp(-accel * Time.deltaTime));
            Vector3 move = m_MoveVel + m_KnockVel;
            m_KnockVel = Vector3.MoveTowards(m_KnockVel, Vector3.zero, 20f * s * Time.deltaTime);

            bool grounded = m_Controller.isGrounded;
            float gravity = Gravity * s;
            if (grounded) m_Coyote = 0.12f; else m_Coyote -= Time.deltaTime;   // 낭떠러지 끝에서 살짝 늦게 눌러도 점프
            if (jump) m_JumpBuffer = 0.15f; else m_JumpBuffer -= Time.deltaTime; // 착지 직전에 눌러도 점프
            if (grounded && m_VerticalVelocity < 0f) m_VerticalVelocity = -2f * s; // 경사면에 붙어 있게
            if (m_Coyote > 0f && m_JumpBuffer > 0f && m_VerticalVelocity <= 0.1f * s)
            {
                m_VerticalVelocity = Mathf.Sqrt(JumpHeight * s * -2f * gravity);
                m_JumpPose = 0.25f;
                m_Coyote = 0f;
                m_JumpBuffer = 0f;
            }
            // 떨어질 때는 중력을 더 세게 — 둥둥 뜨지 않고 깔끔하게 착지
            float g = m_VerticalVelocity < 0f ? gravity * 1.7f : gravity;
            m_VerticalVelocity += g * Time.deltaTime;
            move.y = m_VerticalVelocity;

            m_Controller.Move(move * Time.deltaTime);

            Animate(attacking ? 0f : input.magnitude, run, m_Controller.isGrounded);
        }

        void Animate(float amount, bool run, bool grounded)
        {
            if (!m_Animator) return;

            m_AirTime = grounded ? 0f : m_AirTime + Time.deltaTime;
            m_JumpPose -= Time.deltaTime;
            m_AnimSpeed = Mathf.MoveTowards(m_AnimSpeed, amount, 6f * Time.deltaTime);
            m_AnimState = Mathf.MoveTowards(m_AnimState, run && amount > 0.1f ? 1f : 0f, 4f * Time.deltaTime);

            m_Animator.SetFloat(k_Vert, m_AnimSpeed);
            m_Animator.SetFloat(k_Hor, 0f);
            m_Animator.SetFloat(k_State, m_AnimState);
            m_Animator.SetBool(k_IsJump, m_JumpPose > 0f || m_AirTime > 0.15f); // 계단·경사에서 잠깐 뜬 건 점프로 치지 않음
        }

        void UpdateCamera()
        {
            if (!m_Camera) return;
            float s = Scale;
            m_AimBlend = Mathf.MoveTowards(m_AimBlend, Aiming ? 1f : 0f, Time.unscaledDeltaTime * 7f);
            float aim = Mathf.SmoothStep(0f, 1f, m_AimBlend);
            Quaternion rotation = Quaternion.Euler(m_Pitch, m_Yaw, 0f);
            // 따라가는 점: 옆으로는 빠르게, 위아래(점프·착지·계단)는 조금 더 느긋하게 — e^(−kt) 보간
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            Vector3 body = transform.position;
            if (!m_FollowInit || (body - m_Follow).sqrMagnitude > 36f * s * s) { m_Follow = body; m_FollowInit = true; }
            float kh = 1f - Mathf.Exp(-dt * Mathf.Lerp(16f, 40f, aim));
            float kv = 1f - Mathf.Exp(-dt * Mathf.Lerp(9f, 40f, aim));
            m_Follow = new Vector3(Mathf.Lerp(m_Follow.x, body.x, kh), Mathf.Lerp(m_Follow.y, body.y, kv), Mathf.Lerp(m_Follow.z, body.z, kh));
            // 조준: 어깨 너머(오른쪽)로 붙고, 가까이, 화각을 좁힌다
            Vector3 pivot = m_Follow + Vector3.up * Mathf.Lerp(CameraHeight, 1.6f, aim) * s
                          + (rotation * Vector3.right) * 0.55f * s * aim;
            Vector3 back = rotation * Vector3.back;
            float wanted = Mathf.Lerp(CameraDistance, 1.7f, aim) * s;
            m_Camera.fieldOfView = Mathf.Lerp(m_BaseFov, m_BaseFov * 0.6f, aim);

            // 카메라와 캐릭터 사이에 벽·지형이 있으면 앞으로 당긴다
            float distance = wanted;
            if (Physics.SphereCast(pivot, 0.25f * s, back, out RaycastHit hit, wanted, ~(1 << 2), QueryTriggerInteraction.Ignore))
                distance = Mathf.Max(0.3f * s, hit.distance - 0.1f * s);

            m_Camera.transform.SetPositionAndRotation(pivot + back * distance + rotation * ShakeOffset(), rotation);
        }

        void Respawn()
        {
            m_Controller.enabled = false;
            transform.SetPositionAndRotation(m_SpawnPosition, m_SpawnRotation);
            m_Controller.enabled = true;
            m_VerticalVelocity = 0f;
            m_Yaw = m_SpawnRotation.eulerAngles.y;
            m_FollowInit = false;
        }

        /// <summary>대시·구르기가 끝난 뒤 남는 관성 (마찰로 부드럽게 줄어든다)</summary>
        public void AddMomentum(Vector3 v)
        {
            v.y = 0f;
            m_KnockVel += v;
        }

        /// <summary>공격할 때 카메라가 보는 쪽으로 몸을 돌린다</summary>
        public void FaceCameraDirection()
        {
            transform.rotation = Quaternion.Euler(0f, m_Yaw, 0f);
        }

        // ---------------------------------------------------------------- UI

        void BuildUI()
        {
            TMP_FontAsset font = MathModeAssets.Load() ? MathModeAssets.Load().Font : null;
            Canvas canvas = UIUtil.Canvas(transform, "NewMode HUD", 10);

            // 왼쪽 아래 작은 안내 (자세한 조작은 Tab → 조작)
            var help = Gs.Text(canvas.transform, "<color=#FFE196>Tab</color> 캐릭터   <color=#FFE196>T</color> 훈련장   <color=#FFE196>Esc</color> 메뉴", 20f, TextAlignmentOptions.Left, new Color(1f, 1f, 1f, 0.8f), true);
            if (NewModeTouch.Enabled) help.gameObject.SetActive(false); // 휴대폰은 화면 버튼
            UIUtil.Place(help.rectTransform, new Vector2(0f, 0f), new Vector2(240f, 28f), new Vector2(440f, 30f));

            // 일시정지 메뉴 (원신 풍 크림색 창)
            var pause = new GameObject("Pause", typeof(RectTransform)).GetComponent<RectTransform>();
            pause.SetParent(canvas.transform, false);
            UIUtil.Stretch(pause, 0f);
            m_PausePanel = pause.gameObject;
            Gs.Dim(pause, 0.55f);
            var panel = Gs.Panel(pause, "Panel", false);
            bool touch = NewModeTouch.Enabled;
            UIUtil.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, touch ? 880f : 760f));
            Gs.Header(panel, "메뉴", 46f, new Vector2(0f, -60f), 540f, Gs.Ink);

            string[] labels = { "<b>계속하기</b>", NewModeTouch.Enabled ? "캐릭터 정보" : "캐릭터 정보 (Tab)", NewModeTouch.Enabled ? "훈련장 ↔ 모험" : "훈련장 ↔ 모험 (T)", "처음부터 다시", "메인 메뉴" };
            Color[] icons = { new Color(0.35f, 0.75f, 0.5f), new Color(0.95f, 0.75f, 0.35f), new Color(0.35f, 0.6f, 0.95f), new Color(0.7f, 0.5f, 0.9f), Gs.InkSoft };
            UnityEngine.Events.UnityAction[] actions =
            {
                () => SetPaused(false),
                () => { SetPaused(false); if (m_Combat) CharacterSheet.Open(m_Combat); },
                () => { SetPaused(false); TrainingZone.Toggle(); },
                () => LeaveTo(SceneManager.GetActiveScene().name, false),
                () => LeaveTo(AppBootstrap.NewModeCharacterScene, false), // 새 모드의 메인 메뉴 (캐릭터·직업 고르기)
            };
            for (int i = 0; i < labels.Length; i++)
            {
                var b = Gs.Button(panel, labels[i], icons[i], actions[i], new Vector2(480f, 86f), 32f, i == 0);
                UIUtil.Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0f, -170f - i * 112f), new Vector2(480f, 86f));
            }
            if (touch)
            {
                // 휴대폰: 화면 고정 (가로 한 방향 고정 ↔ 가로 자동 회전)
                TextMeshProUGUI lockLabel = null;
                System.Func<string> lockText = () => AppBootstrap.ScreenLocked ? "화면 고정: <b>켜짐</b>" : "화면 고정: <b>꺼짐</b> (자동 회전)";
                var lockButton = Gs.Button(panel, lockText(), new Color(0.4f, 0.75f, 0.8f), () =>
                {
                    AppBootstrap.ScreenLocked = !AppBootstrap.ScreenLocked;
                    if (lockLabel) lockLabel.text = lockText();
                }, new Vector2(480f, 86f), 28f);
                UIUtil.Place((RectTransform)lockButton.transform, new Vector2(0.5f, 1f), new Vector2(0f, -170f - labels.Length * 112f), new Vector2(480f, 86f));
                lockLabel = lockButton.GetComponentInChildren<TextMeshProUGUI>();
            }
            m_PausePanel.transform.SetAsLastSibling();
            BuildHealthUI(canvas.transform, font);
        }

        void SetPaused(bool paused)
        {
            m_Paused = paused;
            Time.timeScale = paused ? 0f : 1f;
            if (m_PausePanel)
            {
                m_PausePanel.SetActive(paused);
                if (paused)
                {
                    // DOTween (timeScale 0이어도 움직이도록 SetUpdate): 판이 서서히, 버튼은 차례로 튀어나옴
                    UIAnim.FadeIn(UIAnim.Group(m_PausePanel.transform), 0.2f);
                    UIAnim.PopIn(m_PausePanel.transform.GetChild(1), 0f, 0.3f);
                }
            }
            LockCursor(!paused);
        }

        static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void LeaveTo(string scene, bool mapSelect)
        {
            Time.timeScale = 1f;
            AppBootstrap.ClassicMenu = false;
            AppBootstrap.NewModeMapSelect = mapSelect;
            SceneManager.LoadScene(scene);
        }
    }
}
