using System.Collections.Generic;
using TMPro;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace Unity.FPS.MathMode
{
    // 수학 모드 전체를 관리한다: 맵 생성, 난이도 적용, 좌표평면 HUD, 수학 관문, 좌표 탐색, 함수 그래프 레이저,
    // 스킬·단검·궁극기, 모바일 조작, 결과 기록.
    public class MathModeManager : MonoBehaviour
    {
        [Header("Assets (비어 있으면 Resources/MathModeAssets 에서 자동으로 불러옴)")]
        public TMP_FontAsset Font;
        public Material UnlitMaterial;
        public Material LineMaterial;
        public GameObject RewardPrefab;
        public GameObject PenaltyEnemyPrefab;
        public Material LitMaterial;
        public GameObject ExplosionVfx;
        [Tooltip("테마 순서: 좌표 도시, 기하 사막, 설원 요새 (네 번째 네온 우주는 쓰지 않음)")]
        public Material[] ThemeSkyboxes;

        [Header("플레이어 조작감")]
        [Tooltip("점프 초속도. 최고 높이 = v² / 2g")]
        public float JumpForce = 10f;
        [Tooltip("중력 가속도 (클수록 빨리 떨어짐)")]
        public float Gravity = 40f;
        public float WeaponSwitchDelay = 0.12f;

        [Header("좌표평면")]
        [Tooltip("좌표평면 1칸이 몇 미터인지")]
        public float UnitSize = 4f;

        [Header("가시성")]
        public Color EnemyColor = new Color(1f, 0.32f, 0.38f);

        // 난이도에 따라 정해지는 값 (게임 시작 시 DifficultyPreset 에서 채움)
        [HideInInspector] public int StationCount = 3;
        [HideInInspector] public int BeaconCount = 1;
        [HideInInspector] public int LaserCount = 1;
        [HideInInspector] public int PenaltyEnemiesPerWrongAnswer;
        [HideInInspector] public float LaserDamagePerSecond = 6f;
        [HideInInspector] public float HealDropChance = 1f;

        public static MathModeManager Instance { get; private set; }
        public static bool CreatingPreview; // 메뉴·결과 화면의 배경 맵을 만들 때만 true
        public bool PreviewOnly { get; private set; }

        public MathWorld World { get; private set; }
        public Transform Player { get; private set; }
        public Health PlayerHealth { get; private set; }
        public List<MathStation> Stations { get; } = new List<MathStation>();
        public List<GraphLaser> Lasers { get; } = new List<GraphLaser>();
        public CoordinateHuntObjective Hunt { get; private set; }
        public PlayerMeleeMode Melee { get; private set; }
        public PlayerWeaponsManager PlayerWeapons { get; private set; }
        public PlayerInputHandler PlayerInput { get; private set; }
        public MathSkills Skills { get; private set; }
        public MathUltimate Ultimate { get; private set; }
        public CameraFX CameraFX { get; private set; }
        public EnemyGuardian Guardian { get; private set; }
        public WorldMarkers Markers { get; private set; }
        public MathAssist Assist { get; private set; }
        public MapTheme CurrentTheme { get; private set; } = MapTheme.GridCity;
        public int CurrentMapChoice { get; private set; }
        public Difficulty CurrentDifficulty { get; private set; } = Difficulty.Easy;
        public DifficultyPreset Preset { get; private set; } = DifficultyPreset.For(Difficulty.Easy);
        public bool EasyMode => CurrentDifficulty == Difficulty.Easy;
        public bool ShowHints => Preset.ShowHints;
        public bool Started { get; private set; }

        MathStationObjective m_StationObjective;
        PlayerCharacterController m_PlayerController;
        readonly List<Vector3> m_Occupied = new List<Vector3>();

        void Awake()
        {
            PreviewOnly = CreatingPreview;
            if (!PreviewOnly) Instance = this;
            LoadMissingAssets();
            // 한글은 글자 종류가 많아 아틀라스 한 장이 금방 차서 네모(□)로 보인다 → 아틀라스를 여러 장 쓰도록
            if (Font)
                Font.isMultiAtlasTexturesEnabled = true;
        }

        void LoadMissingAssets()
        {
            MathModeAssets assets = MathModeAssets.Load();
            if (!assets) return;
            if (!Font) Font = assets.Font;
            if (!UnlitMaterial) UnlitMaterial = assets.UnlitMaterial;
            if (!LineMaterial) LineMaterial = assets.LineMaterial;
            if (!LitMaterial) LitMaterial = assets.LitMaterial;
            if (ThemeSkyboxes == null || ThemeSkyboxes.Length == 0) ThemeSkyboxes = assets.ThemeSkyboxes;
            if (!RewardPrefab) RewardPrefab = assets.RewardPrefab;
            if (!PenaltyEnemyPrefab) PenaltyEnemyPrefab = assets.PenaltyEnemyPrefab;
            if (!ExplosionVfx) ExplosionVfx = assets.ExplosionVfx;
        }

        void Start()
        {
            if (PreviewOnly) return;

            m_PlayerController = FindAnyObjectByType<PlayerCharacterController>();
            if (!m_PlayerController)
            {
                Debug.LogError("[MathMode] 플레이어를 찾을 수 없습니다.");
                enabled = false;
                return;
            }

            Player = m_PlayerController.transform;
            PlayerHealth = m_PlayerController.GetComponent<Health>();
            PlayerWeapons = m_PlayerController.GetComponent<PlayerWeaponsManager>();
            PlayerInput = m_PlayerController.GetComponent<PlayerInputHandler>();
            ApplyPlayerTuning();

            EventManager.AddListener<EnemyKillEvent>(OnEnemyKilled);
            EventManager.AddListener<AllObjectivesCompletedEvent>(OnGameWon);
            EventManager.AddListener<PlayerDeathEvent>(OnPlayerDied);

            // 안내창에서 '시작'을 누르면 BeginGame이 호출된다
            gameObject.AddComponent<GameIntro>().Init(this, PlayerInput);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (PreviewOnly) return;
            EventManager.RemoveListener<EnemyKillEvent>(OnEnemyKilled);
            EventManager.RemoveListener<AllObjectivesCompletedEvent>(OnGameWon);
            EventManager.RemoveListener<PlayerDeathEvent>(OnPlayerDied);
            VirtualInput.ResetAll();
        }

        void ApplyPlayerTuning()
        {
            // 점프: 기존 v=9, g=20 → 높이 2.0m, 체공 0.9초 / 변경 v=10, g=40 → 높이 1.25m, 체공 0.5초
            m_PlayerController.JumpForce = JumpForce;
            m_PlayerController.GravityDownForce = Gravity;
            if (PlayerWeapons) PlayerWeapons.WeaponSwitchDelay = WeaponSwitchDelay;
            if (PlayerInput) PlayerInput.LookSensitivity = GameSettings.Sensitivity;
            VirtualInput.Active = GameSettings.UseMobileControls;

            // C는 스킬에 쓰므로 앉기를 왼쪽 Ctrl로 옮긴다
            var crouch = InputSystem.actions?.FindAction("Player/Crouch");
            if (crouch != null)
            {
                for (int i = 0; i < crouch.bindings.Count; i++)
                    if (crouch.bindings[i].path == "<Keyboard>/c")
                        crouch.ApplyBindingOverride(i, "<Keyboard>/leftCtrl");
            }

            if (GameSettings.IsMobileDevice)
                QualitySettings.shadowDistance = Mathf.Min(QualitySettings.shadowDistance, 35f);
        }

        // ------------------------------------------------------------------ 게임 시작

        public void BeginGame(int mapChoice, Difficulty difficulty)
        {
            Started = true;
            CurrentMapChoice = mapChoice;
            CurrentDifficulty = difficulty;
            Preset = DifficultyPreset.For(difficulty);

            int seed;
            if (mapChoice == GameSettings.RandomMap)
            {
                CurrentTheme = (MapTheme)Random.Range(0, GameSettings.ThemeCount);
                seed = Random.Range(1, 100000);
            }
            else
            {
                CurrentTheme = (MapTheme)Mathf.Clamp(mapChoice, 0, GameSettings.ThemeCount - 1);
                seed = 1000 + (int)CurrentTheme * 77;
            }

            MapGenerator map = MapGenerator.Build(this, CurrentTheme, seed, buildNavMesh: true);
            TeleportPlayer(map.SpawnPoint);
            GameStats.Reset(CurrentTheme, seed, difficulty);

            World = new MathWorld(UnitSize);
            if (!World.HasNavMesh)
            {
                Debug.LogError("[MathMode] NavMesh가 없어 수학 모드 오브젝트를 배치할 수 없습니다.");
                return;
            }

            // 도달 가능 여부의 기준점 = 시작 지점 (플레이어가 상자 위에 올라가도 판정이 흔들리지 않도록)
            if (NavMesh.SamplePosition(Player.position, out NavMeshHit hub, 5f, NavMesh.AllAreas))
                World.Hub = hub.position;

            m_Occupied.Add(Player.position);
            MathEnemy.ResetAnnouncements();
            Markers = gameObject.AddComponent<WorldMarkers>();
            Markers.Init(this);
            ApplyDifficulty();

            Guardian = gameObject.AddComponent<EnemyGuardian>();
            Guardian.Init(this);
            if (difficulty >= Difficulty.Hard)
                Guardian.RelocateInvalidNow();
            else
                Guardian.RelocateAllNow(); // 남긴 적을 모두 시작 지점 근처의 잘 보이는 곳으로
            RelocatePickups();

            SpawnStations();
            SpawnLasers();
            SpawnHunt();
            SpawnBossObjective();
            AddObjectiveMarkers();

            EnsureAllWeapons();
            CameraFX = Player.gameObject.AddComponent<CameraFX>();
            CameraFX.Init(this);
            Melee = Player.gameObject.AddComponent<PlayerMeleeMode>();
            Melee.Init(this);
            Skills = Player.gameObject.AddComponent<MathSkills>();
            Skills.Init(this);
            Skills.HealDisabled = Preset.NoHeal;
            foreach (MathSkills.Skill skill in Skills.All)
                skill.Cooldown *= Preset.SkillCooldown;
            Ultimate = Player.gameObject.AddComponent<MathUltimate>();
            Ultimate.Init(this);
            Assist = Player.gameObject.AddComponent<MathAssist>();
            Assist.Init(this);
            FitConeCrosshairs();
            if (Preset.WrongAnswerKills) HideCheats();

            gameObject.AddComponent<CoordinatePlaneHUD>().Init(this);
            if (GameSettings.UseMobileControls)
                gameObject.AddComponent<MobileControls>().Init(this);

            ShowMessage($"{MapGenerator.DisplayName(CurrentTheme)} · {GameSettings.DifficultyName(difficulty)} - 임무 시작!");
            if (Preset.WrongAnswerKills)
                ShowMessage("극한 난이도: 관문 오답 = 즉사, 회복 없음. 신중하게!");
        }

        // ------------------------------------------------------------------ 난이도

        void ApplyDifficulty()
        {
            DifficultyPreset p = Preset;
            StationCount = p.Stations;
            BeaconCount = p.Beacons;
            LaserCount = p.Lasers;
            PenaltyEnemiesPerWrongAnswer = p.PenaltyEnemies;
            LaserDamagePerSecond = p.LaserDamage;
            HealDropChance = p.HealDrop;
            MathProblemGenerator.Easy = EasyMode;
            World.MaxFromPlayer = p.PlacementRadius;

            if (PlayerHealth)
            {
                PlayerHealth.MaxHealth = p.PlayerHealth;
                PlayerHealth.CurrentHealth = p.PlayerHealth;
            }

            var damageable = Player.GetComponent<Damageable>();
            if (damageable) damageable.DamageMultiplier = p.IncomingDamage;

            var enemyManager = FindAnyObjectByType<EnemyManager>();
            if (!enemyManager || enemyManager.Enemies == null) return;

            // 포탑 수와 전체 적 수를 난이도에 맞게 줄인다
            var kept = new List<EnemyController>();
            int turrets = 0;
            foreach (EnemyController enemy in enemyManager.Enemies.ToArray())
            {
                if (!enemy) continue;
                bool isTurret = enemy.GetComponent<EnemyTurret>();
                bool keep = kept.Count < p.EnemyCount && (!isTurret || turrets < p.MaxTurrets);
                if (!keep)
                {
                    // 처치 이벤트 없이 목록에서만 빼고 제거 (처치 수에 포함되지 않도록)
                    enemyManager.Enemies.Remove(enemy);
                    Destroy(enemy.gameObject);
                    continue;
                }

                if (isTurret) turrets++;
                TuneEnemy(enemy);
                if (!isTurret) MathEnemy.Apply(enemy, KindFor(kept.Count - turrets), this);
                kept.Add(enemy);
            }

            var killObjective = FindAnyObjectByType<ObjectiveKillEnemies>();
            if (killObjective && killObjective.MustKillAllEnemies)
            {
                killObjective.KillsToCompleteObjective = kept.Count;
                killObjective.UpdateObjective(string.Empty, $"0 / {kept.Count}", string.Empty);
            }
        }

        // 난이도별 적 구성: 앞에서부터 차례로 배정 (쉬움은 단순한 적 위주)
        EnemyKind KindFor(int index)
        {
            EnemyKind[] order = CurrentDifficulty switch
            {
                Difficulty.Easy => new[] { EnemyKind.Drone, EnemyKind.Rusher, EnemyKind.Tank, EnemyKind.Splitter, EnemyKind.Drone },
                Difficulty.Normal => new[] { EnemyKind.Drone, EnemyKind.Rusher, EnemyKind.Tank, EnemyKind.Splitter, EnemyKind.Sniper,
                    EnemyKind.Healer, EnemyKind.Blinker, EnemyKind.Orbiter, EnemyKind.Drone },
                _ => new[] { EnemyKind.Drone, EnemyKind.Rusher, EnemyKind.Tank, EnemyKind.Sniper, EnemyKind.Splitter, EnemyKind.Healer,
                    EnemyKind.Blinker, EnemyKind.Orbiter, EnemyKind.Rusher, EnemyKind.Drone },
            };
            return order[index % order.Length];
        }

        // 기본 호버봇을 복제해 원하는 종류의 적을 만든다 (오답 벌칙·분열·보스 소환에 사용)
        public EnemyController SpawnEnemy(EnemyKind kind, Vector3 near, float searchRadius)
        {
            if (!PenaltyEnemyPrefab) return null;
            if (!NavMesh.SamplePosition(near, out NavMeshHit hit, searchRadius, NavMesh.AllAreas)) return null;
            Vector3 look = Player ? Vector3.ProjectOnPlane(Player.position - hit.position, Vector3.up) : Vector3.forward;
            GameObject go = Instantiate(PenaltyEnemyPrefab, hit.position, look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.identity);
            var enemy = go.GetComponent<EnemyController>();
            if (!enemy) return null;
            TuneEnemy(enemy);
            MathEnemy.Apply(enemy, kind, this);
            return enemy;
        }

        public void TuneEnemy(EnemyController enemy)
        {
            DifficultyPreset p = Preset;
            var health = enemy.GetComponent<Health>();
            if (health)
            {
                health.MaxHealth *= p.EnemyHealth;
                health.CurrentHealth = health.MaxHealth;
            }

            foreach (WeaponController weapon in enemy.GetComponentsInChildren<WeaponController>(true))
            {
                weapon.DelayBetweenShots *= p.EnemyFireDelay;
                weapon.BulletSpreadAngle = Mathf.Max(weapon.BulletSpreadAngle, p.EnemySpread);
            }

            MathEnemy.ScaleSpeed(enemy, p.EnemySpeed);
        }

        // 씬에 저장된 시작 무기와 상관없이, 에셋에 등록된 무기를 모두 지급한다 (총이 한 종류만 뜨는 문제 방지)
        void EnsureAllWeapons()
        {
            MathModeAssets assets = MathModeAssets.Load();
            if (!PlayerWeapons || !assets) return;

            foreach (WeaponController prefab in assets.Weapons)
                if (prefab && !PlayerWeapons.HasWeapon(prefab))
                    PlayerWeapons.AddWeapon(prefab);

            // 특수 기믹 무기 (기본 Blaster를 복제해 만든다)
            if (Preset.SpecialWeapons && assets.Weapons.Count > 0)
                foreach (WeaponController special in MathSpecialWeapons.CreateTemplates(this, assets.Weapons[0]))
                    PlayerWeapons.AddWeapon(special);

            // 극한 난이도: 앞의 몇 개만 남기고 무기를 뺀다
            for (int i = Preset.WeaponLimit; i < PlayerWeaponsManager.SlotCount; i++)
            {
                WeaponController extra = PlayerWeapons.GetWeaponAtSlotIndex(i);
                if (extra) PlayerWeapons.RemoveWeapon(extra);
            }

            if (!PlayerWeapons.GetActiveWeapon() && PlayerWeapons.GetWeaponAtSlotIndex(0))
                PlayerWeapons.SwitchToWeaponIndex(0, true);
        }

        // 극한 난이도에서는 일시정지 메뉴의 무적 토글을 숨긴다
        void HideCheats()
        {
            var menu = FindAnyObjectByType<Unity.FPS.UI.InGameMenuManager>(FindObjectsInactive.Include);
            if (!menu || !menu.InvincibilityToggle) return;
            menu.InvincibilityToggle.isOn = false;
            menu.InvincibilityToggle.gameObject.SetActive(false);
            if (PlayerHealth) PlayerHealth.Invincible = false;
        }

        // ------------------------------------------------------------------ 기록 / 이벤트

        void OnEnemyKilled(EnemyKillEvent evt)
        {
            GameStats.Kills++;
            if (Ultimate) Ultimate.AddCharge(34f);
        }

        void OnGameWon(AllObjectivesCompletedEvent evt) => GameStats.EndTime = Time.time;
        void OnPlayerDied(PlayerDeathEvent evt) => GameStats.EndTime = Time.time;

        public void OnBeaconFound()
        {
            GameStats.BeaconsFound++;
            if (Ultimate) Ultimate.AddCharge(40f);
        }

        // ------------------------------------------------------------------ 가시성

        // 적 몸체를 종류별 색으로 칠하고, 화면에서 늘 같은 두께로 보이는 테두리(벽 너머로는 반투명 실루엣)를 씌운다
        public void HighlightEnemy(EnemyController enemy)
        {
            var kind = enemy.GetComponent<MathEnemy>();
            Color body = kind ? kind.Color : EnemyColor;
            foreach (Renderer r in enemy.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                Material[] shared = r.sharedMaterials;
                bool hasBody = false;
                foreach (Material m in shared)
                    if (m && m == enemy.BodyMaterial) hasBody = true;
                if (!hasBody) continue;

                Material[] mats = r.materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (shared[i] != enemy.BodyMaterial) continue;
                    mats[i].SetColor("_BaseColor", body);
                    mats[i].SetColor("_Color", body);
                }

                r.materials = mats;
            }

            bool isBoss = kind && kind.Kind == EnemyKind.Boss;
            Color outline = Color.Lerp(body, Color.white, 0.15f);
            outline.a = 1f;
            if (!EnemyOutline.Apply(enemy.gameObject, outline, isBoss ? 5f : 3.5f) && Markers)
            {
                // 테두리 셰이더를 쓸 수 없는 기기에서는 예전처럼 ◆ 마커로 대신한다
                Markers.Add(() => enemy ? enemy.transform.position + Vector3.up * 2.6f : (Vector3?)null,
                    new Color(1f, 0.25f, 0.2f), () => "적", 0.8f);
            }
        }

        void AddObjectiveMarkers()
        {
            foreach (MathStation station in Stations)
            {
                MathStation s = station;
                Markers.Add(() => s && !s.IsSolved ? s.transform.position + Vector3.up * 6.6f : (Vector3?)null,
                    new Color(1f, 0.85f, 0.2f), () => $"관문 {s.Letter}");
            }

            if (Hunt)
                Markers.Add(() => Hunt && !Hunt.IsCompleted && Hunt.HasTarget && Hunt.ShowMarker ? Hunt.TargetWorld + Vector3.up * 3f : (Vector3?)null,
                    new Color(0.3f, 1f, 1f), () => Hunt.ShowMarker ? $"좌표 ({Hunt.CurrentTarget.x}, {Hunt.CurrentTarget.y})" : "좌표 목표");
        }

        void TeleportPlayer(Vector3 position)
        {
            var cc = Player.GetComponent<CharacterController>();
            if (cc) cc.enabled = false;
            Player.SetPositionAndRotation(position, Quaternion.identity);
            if (cc) cc.enabled = true;
        }

        // 기존 픽업은 시작 위치를 기억해 그 자리에서 떠다니므로, 복제해서 새 맵 위에 다시 놓는다
        void RelocatePickups()
        {
            var pickups = new List<Pickup>();
            foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                pickups.AddRange(root.GetComponentsInChildren<Pickup>());

            foreach (Pickup pickup in pickups)
            {
                if (Preset.NoHeal && pickup is HealthPickup)
                {
                    Destroy(pickup.gameObject);
                    continue;
                }

                if (World.TryFindSpot(Player.position, m_Occupied, 8f, 6f, 1f, out Vector3 spot))
                    Instantiate(pickup.gameObject, spot + Vector3.up, Quaternion.identity);
                Destroy(pickup.gameObject);
            }
        }

        // 원뿔 샷건: 조준 원의 크기를 실제 탄이 퍼지는 원뿔 반각 α에 맞춘다
        void FitConeCrosshairs()
        {
            var crosshairs = FindAnyObjectByType<Unity.FPS.UI.CrosshairManager>();
            Canvas canvas = crosshairs ? crosshairs.GetComponentInParent<Canvas>() : null;
            Camera cam = PlayerWeapons ? PlayerWeapons.WeaponCamera : null;
            if (!canvas || !cam || !PlayerWeapons) return;

            for (int i = 0; i < 9; i++)
            {
                WeaponController weapon = PlayerWeapons.GetWeaponAtSlotIndex(i);
                var trajectory = weapon && weapon.ProjectilePrefab ? weapon.ProjectilePrefab.GetComponent<MathTrajectory>() : null;
                if (!trajectory || trajectory.Type != MathTrajectory.Function.Cone) continue;

                // 화면상 지름(px) = 화면 높이 × tanα / tan(시야각/2)
                float diameterPx = Screen.height * Mathf.Tan(trajectory.Amplitude * Mathf.Deg2Rad) /
                                   Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                int size = Mathf.RoundToInt(diameterPx / canvas.scaleFactor * 1.1f);
                var data = weapon.CrosshairDataDefault;
                data.CrosshairSize = size;
                weapon.CrosshairDataDefault = data;
                data = weapon.CrosshairDataTargetInSight;
                data.CrosshairSize = size;
                weapon.CrosshairDataTargetInSight = data;
            }
        }

        // ------------------------------------------------------------------ 배치

        void SpawnStations()
        {
            var spots = new List<Vector3>();
            for (int i = 0; i < StationCount; i++)
            {
                if (!World.TryFindSpot(Player.position, m_Occupied, 14f, 18f, 6f, out Vector3 spot))
                    break;
                m_Occupied.Add(spot);
                spots.Add(spot);
            }

            // 가까운 관문부터 A, B, C... 단계도 쉬운 것부터: 기초 도형 → 좌표평면 → 그래프 → 도전
            spots.Sort((a, b) => Vector3.Distance(Player.position, a).CompareTo(Vector3.Distance(Player.position, b)));
            int placed = 0;
            foreach (Vector3 spot in spots)
            {
                char letter = (char)('A' + placed);
                var go = new GameObject($"MathStation_{letter}");
                go.transform.SetParent(transform, false);
                go.transform.position = spot;
                var station = go.AddComponent<MathStation>();
                station.Init(this, letter, Preset.StationTiers[Mathf.Min(placed, Preset.StationTiers.Length - 1)]);
                Stations.Add(station);
                placed++;
            }

            if (placed == 0) return;

            var objGo = new GameObject("Objective_MathStations");
            objGo.transform.SetParent(transform, false);
            m_StationObjective = objGo.AddComponent<MathStationObjective>();
            m_StationObjective.Setup(placed);
        }

        void SpawnLasers()
        {
            for (int i = 0; i < LaserCount; i++)
            {
                bool found = false;
                for (int attempt = 0; attempt < 30 && !found; attempt++)
                {
                    if (!World.TryFindSpot(Player.position, m_Occupied, 12f, 14f, 0f, out Vector3 spot))
                        break;

                    // 가장 넓게 트인 방향으로 그래프 평면을 세운다
                    float bestHalfWidth = 0f;
                    Vector3 bestRight = Vector3.right;
                    for (int a = 0; a < 4; a++)
                    {
                        Vector3 right = Quaternion.Euler(0f, a * 45f, 0f) * Vector3.right;
                        float half = Mathf.Min(FreeDistance(spot, right), FreeDistance(spot, -right), 8f) - 0.3f;
                        if (half > bestHalfWidth)
                        {
                            bestHalfWidth = half;
                            bestRight = right;
                        }
                    }

                    if (bestHalfWidth < 3f) continue;

                    m_Occupied.Add(spot);
                    var go = new GameObject($"GraphLaser_{i + 1}");
                    go.transform.SetParent(transform, false);
                    var laser = go.AddComponent<GraphLaser>();
                    laser.Init(this, spot, bestRight, bestHalfWidth, (GraphLaser.FunctionType)(i % 3));
                    Lasers.Add(laser);
                    found = true;
                }
            }
        }

        static float FreeDistance(Vector3 from, Vector3 dir)
        {
            return Physics.Raycast(from + Vector3.up * 1.2f, dir, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore)
                ? hit.distance
                : 10f;
        }

        void SpawnBossObjective()
        {
            var go = new GameObject("Objective_FinalBoss");
            go.transform.SetParent(transform, false);
            go.AddComponent<BossObjective>().Setup(this);
        }

        void SpawnHunt()
        {
            if (BeaconCount <= 0) return;
            var go = new GameObject("Objective_CoordinateHunt");
            go.transform.SetParent(transform, false);
            Hunt = go.AddComponent<CoordinateHuntObjective>();
            Hunt.Setup(this, BeaconCount, m_Occupied);
        }

        // ------------------------------------------------------------------ 관문 이벤트

        public void OnStationSolved(MathStation station)
        {
            GameStats.Correct++;
            if (Ultimate) Ultimate.AddCharge(50f);
            if (CameraFX) CameraFX.Flash(new Color(0.4f, 1f, 0.5f, 0.25f), 0.35f);
            if (RewardPrefab && !Preset.NoHeal)
                Instantiate(RewardPrefab, station.transform.position + Vector3.up * 1.2f, Quaternion.identity);
            if (m_StationObjective)
                m_StationObjective.OnStationSolved();
        }

        public void OnStationWrong(MathStation station)
        {
            GameStats.Wrong++;
            if (Preset.WrongAnswerKills)
            {
                ShowMessage($"오답! 관문 {station.Letter}: 극한 난이도에서는 찍으면 즉사...");
                if (CameraFX) CameraFX.Flash(new Color(1f, 0f, 0f, 0.6f), 0.8f);
                if (PlayerHealth)
                {
                    PlayerHealth.Invincible = false;
                    PlayerHealth.Kill();
                }

                return;
            }

            ShowMessage(PenaltyEnemiesPerWrongAnswer > 0
                ? $"오답! 관문 {station.Letter}에서 적이 출현했다"
                : $"오답! 관문 {station.Letter}의 힌트를 보고 다른 표적을 쏘세요");
            if (!PenaltyEnemyPrefab) return;

            for (int i = 0; i < PenaltyEnemiesPerWrongAnswer; i++)
            {
                Vector2 offset = Random.insideUnitCircle.normalized * Random.Range(5f, 9f);
                SpawnEnemy(Random.value < 0.5f ? EnemyKind.Rusher : EnemyKind.Drone,
                    station.transform.position + new Vector3(offset.x, 0f, offset.y), 4f);
            }
        }

        public static void ShowMessage(string message)
        {
            DisplayMessageEvent evt = Events.DisplayMessageEvent;
            evt.Message = message;
            evt.DelayBeforeDisplay = 0f;
            EventManager.Broadcast(evt);
        }

        // ------------------------------------------------------------------ 생성 도우미

        public TextMeshPro CreateText(Transform parent, Vector3 localPos, Vector2 size, string text, Color color,
            float maxFontSize = 8f)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var tmp = go.AddComponent<TextMeshPro>();
            if (Font) tmp.font = Font;
            tmp.rectTransform.sizeDelta = size;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.5f;
            tmp.fontSizeMax = maxFontSize;
            tmp.color = color;
            tmp.text = text;
            return tmp;
        }

        public GameObject CreateBlock(Transform parent, PrimitiveType type, Vector3 localPos, Vector3 scale, Color color,
            bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            if (!keepCollider)
                DestroyImmediate(go.GetComponent<Collider>());
            SetColor(go.GetComponent<Renderer>(), color);
            return go;
        }

        // 조명을 받는 장식 블록 (충돌체 없음)
        public GameObject CreateLitBlock(Transform parent, Vector3 localPos, Vector3 scale, Color color, float smoothness,
            PrimitiveType type = PrimitiveType.Cube)
        {
            var go = GameObject.CreatePrimitive(type);
            DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<Renderer>();
            if (LitMaterial) renderer.material = LitMaterial;
            renderer.material.SetColor("_BaseColor", color);
            if (renderer.material.HasProperty("_Smoothness")) renderer.material.SetFloat("_Smoothness", smoothness);
            if (renderer.material.HasProperty("_Metallic")) renderer.material.SetFloat("_Metallic", smoothness > 0.7f ? 0.8f : 0f);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        public void SetColor(Renderer renderer, Color color)
        {
            if (UnlitMaterial)
                renderer.material = UnlitMaterial;
            renderer.material.SetColor("_BaseColor", color);
            renderer.material.SetColor("_Color", color);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public LineRenderer CreateLine(Transform parent, Color color, float width)
        {
            var go = new GameObject("Line");
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.widthMultiplier = width;
            line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (LineMaterial) line.material = LineMaterial;
            line.startColor = line.endColor = color;
            line.material.SetColor("_BaseColor", color);
            return line;
        }

        // 메뉴·결과 화면용: 플레이어 없이 맵만 만드는 관리자
        public static MathModeManager CreatePreviewHost()
        {
            CreatingPreview = true;
            var go = new GameObject("MathMode Preview Host");
            var host = go.AddComponent<MathModeManager>();
            CreatingPreview = false;
            return host;
        }
    }
}
