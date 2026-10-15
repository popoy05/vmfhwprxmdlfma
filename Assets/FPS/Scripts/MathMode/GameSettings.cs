using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Unity.FPS.MathMode
{
    public enum Difficulty { Easy, Normal, Hard, Extreme }
    public enum ControlScheme { Auto, PC, Mobile }

    // 실행 파일(Windows/Android)에서 메뉴로 바꾸고 기기에 저장되는 설정 (PlayerPrefs)
    public static class GameSettings
    {
        public const int ThemeCount = 3;  // 좌표 도시, 기하 사막, 설원 요새 (네온 우주정거장은 눈이 아파서 뺐다)
        public const int RandomMap = ThemeCount; // 맵 선택: 0~2 테마, 3 랜덤

        public static Difficulty Difficulty
        {
            get => (Difficulty)PlayerPrefs.GetInt("math.difficulty", (int)Difficulty.Easy);
            set { PlayerPrefs.SetInt("math.difficulty", (int)value); PlayerPrefs.Save(); }
        }

        public static int Map
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt("math.map", 0), 0, RandomMap);
            set { PlayerPrefs.SetInt("math.map", value); PlayerPrefs.Save(); }
        }

        public static ControlScheme Controls
        {
            get => (ControlScheme)PlayerPrefs.GetInt("math.controls", (int)ControlScheme.Auto);
            set { PlayerPrefs.SetInt("math.controls", (int)value); PlayerPrefs.Save(); }
        }

        // 조작 보조 (메인 메뉴에서 켜고 끈다)
        public static bool AutoWeapon
        {
            get => PlayerPrefs.GetInt("math.autoWeapon", 1) == 1;
            set { PlayerPrefs.SetInt("math.autoWeapon", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool AutoSkills
        {
            get => PlayerPrefs.GetInt("math.autoSkills", 1) == 1;
            set { PlayerPrefs.SetInt("math.autoSkills", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool AimAssist
        {
            get => PlayerPrefs.GetInt("math.aimAssist", 1) == 1;
            set { PlayerPrefs.SetInt("math.aimAssist", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static float Sensitivity
        {
            get => PlayerPrefs.GetFloat("math.sensitivity", 1f);
            set { PlayerPrefs.SetFloat("math.sensitivity", Mathf.Clamp(value, 0.2f, 3f)); PlayerPrefs.Save(); }
        }

        public static bool UseMobileControls => Controls == ControlScheme.Mobile ||
                                                (Controls == ControlScheme.Auto && (Application.isMobilePlatform || Touchscreen.current != null && Mouse.current == null));

        public static bool IsMobileDevice => Application.isMobilePlatform;

        public static string DifficultyName(Difficulty d) => d switch
        {
            Difficulty.Easy => "쉬움",
            Difficulty.Normal => "보통",
            Difficulty.Hard => "어려움",
            _ => "극한",
        };

        public static string DifficultyColor(Difficulty d) => d switch
        {
            Difficulty.Easy => "#6BFF8A",
            Difficulty.Normal => "#FFD54A",
            Difficulty.Hard => "#FF6B6B",
            _ => "#D36BFF",
        };

        public static string DifficultyDescription(Difficulty d) => d switch
        {
            Difficulty.Easy => "처음 해보는 사람용 · 약 3분\n적 5마리, 관문 3곳(도형→좌표→그래프), 좌표 1곳",
            Difficulty.Normal => "익숙해졌다면 · 약 6분\n적 9마리, 관문 4곳(+도전 문제), 좌표 2곳(수식 단서)",
            _ => "도전! · 10분 이상\n적 전부(포탑 포함), 관문 5곳, 좌표 3곳, 오답 시 적 출현",
        };

        public static string MapName(int map) => map == RandomMap ? "랜덤 맵" : MapGenerator.DisplayName((MapTheme)map);
        public static string OnOff(bool on) => on ? "<color=#6BFF8A><b>켜짐</b></color>" : "<color=#FF8A8A>꺼짐</color>";
        public static string ControlsName(ControlScheme c) => c switch
        {
            ControlScheme.PC => "PC (키보드·마우스)",
            ControlScheme.Mobile => "모바일 (터치)",
            _ => "자동",
        };
    }

    // 난이도별 수치
    public class DifficultyPreset
    {
        public int EnemyCount, MaxTurrets, Beacons, Lasers, PenaltyEnemies;
        public int[] StationTiers;      // 관문마다의 문제 단계 (MathProblemGenerator.TierNames 참고)
        public int Stations => StationTiers.Length;
        public int WeaponLimit = 99;    // 지급할 무기 수 (극한: 3)
        public bool SpecialWeapons = true;
        public bool WrongAnswerKills;   // 극한: 오답이면 즉사
        public bool NoHeal;             // 극한: 회복 수단 없음
        public float BossHealth;
        public float PlayerHealth, IncomingDamage, EnemyHealth, EnemyFireDelay, EnemySpread, EnemySpeed;
        public float PlacementRadius, LaserDamage, HealDrop, SkillCooldown, UltimateChargeRate;
        public float HintDelay, MarkerDelay;
        public bool DirectHuntClue, ShowHints;

        public static DifficultyPreset For(Difficulty d) => d switch
        {
            Difficulty.Easy => new DifficultyPreset
            {
                EnemyCount = 5, MaxTurrets = 0, StationTiers = new[] { 0, 0, 0 }, BossHealth = 700f, Beacons = 1, Lasers = 1, PenaltyEnemies = 0,
                PlayerHealth = 250f, IncomingDamage = 0.3f, EnemyHealth = 0.55f, EnemyFireDelay = 2.5f, EnemySpread = 5f, EnemySpeed = 0.6f,
                PlacementRadius = 45f, LaserDamage = 6f, HealDrop = 1f, SkillCooldown = 0.5f, UltimateChargeRate = 1.6f,
                HintDelay = 0f, MarkerDelay = 0f, DirectHuntClue = true, ShowHints = true,
            },
            Difficulty.Normal => new DifficultyPreset
            {
                EnemyCount = 9, MaxTurrets = 1, StationTiers = new[] { 1, 2, 3, 3 }, BossHealth = 1300f, Beacons = 2, Lasers = 2, PenaltyEnemies = 0,
                PlayerHealth = 150f, IncomingDamage = 0.6f, EnemyHealth = 0.8f, EnemyFireDelay = 1.6f, EnemySpread = 3f, EnemySpeed = 0.85f,
                PlacementRadius = 65f, LaserDamage = 12f, HealDrop = 0.6f, SkillCooldown = 0.75f, UltimateChargeRate = 1.2f,
                HintDelay = 45f, MarkerDelay = 90f, DirectHuntClue = false, ShowHints = true,
            },
            Difficulty.Hard => new DifficultyPreset
            {
                EnemyCount = 20, MaxTurrets = 3, StationTiers = new[] { 2, 3, 4, 4, 4 }, BossHealth = 2200f, Beacons = 3, Lasers = 3, PenaltyEnemies = 1,
                PlayerHealth = 100f, IncomingDamage = 1f, EnemyHealth = 1f, EnemyFireDelay = 1f, EnemySpread = 0f, EnemySpeed = 1f,
                PlacementRadius = float.MaxValue, LaserDamage = 30f, HealDrop = 0.35f, SkillCooldown = 1f, UltimateChargeRate = 1f,
                HintDelay = 60f, MarkerDelay = 120f, DirectHuntClue = false, ShowHints = false,
            },
            _ => new DifficultyPreset
            {
                EnemyCount = 20, MaxTurrets = 3, StationTiers = new[] { 5, 5, 5, 5, 5 }, BossHealth = 3200f, Beacons = 3,
                Lasers = 3, PenaltyEnemies = 0, WeaponLimit = 3, SpecialWeapons = false, WrongAnswerKills = true, NoHeal = true,
                PlayerHealth = 100f, IncomingDamage = 1.25f, EnemyHealth = 1.3f, EnemyFireDelay = 0.85f, EnemySpread = 0f, EnemySpeed = 1.15f,
                PlacementRadius = float.MaxValue, LaserDamage = 35f, HealDrop = 0f, SkillCooldown = 1f, UltimateChargeRate = 0.8f,
                HintDelay = 90f, MarkerDelay = 150f, DirectHuntClue = false, ShowHints = false,
            },
        };
    }

    // 결과 화면에 보여줄 이번 판 기록
    public static class GameStats
    {
        public static float StartTime, EndTime;
        public static int Kills, Correct, Wrong, BeaconsFound, Ultimates;
        public static MapTheme Theme;
        public static int Seed;
        public static Difficulty Difficulty;

        public static float Duration => Mathf.Max(0f, EndTime - StartTime);

        public static void Reset(MapTheme theme, int seed, Difficulty difficulty)
        {
            StartTime = Time.time;
            EndTime = 0f;
            Kills = Correct = Wrong = BeaconsFound = Ultimates = 0;
            Theme = theme;
            Seed = seed;
            Difficulty = difficulty;
        }

        // 시간과 오답 수로 등급 매기기
        public static string Grade(bool win)
        {
            if (!win) return "-";
            float minutes = Duration / 60f;
            float target = Difficulty switch { Difficulty.Easy => 4f, Difficulty.Normal => 7f, Difficulty.Hard => 11f, _ => 15f };
            int score = (minutes <= target * 0.7f ? 2 : minutes <= target ? 1 : 0) + (Wrong == 0 ? 2 : Wrong <= 2 ? 1 : 0);
            return score >= 4 ? "S" : score >= 3 ? "A" : score >= 2 ? "B" : "C";
        }
    }

    // 키보드와 화면 버튼(모바일)을 함께 읽는 도우미
    public static class MathInput
    {
        public static bool Pressed(Key key, string virtualId)
        {
            Keyboard kb = Keyboard.current;
            return (kb != null && kb[key].wasPressedThisFrame) || VirtualInput.WasPressed(virtualId);
        }

        public static bool AttackPressed()
        {
            Mouse mouse = Mouse.current;
            return (mouse != null && mouse.leftButton.wasPressedThisFrame && !VirtualInput.Active) || VirtualInput.WasPressed("fire");
        }

        public static bool AttackHeld()
        {
            Mouse mouse = Mouse.current;
            return (mouse != null && mouse.leftButton.isPressed && !VirtualInput.Active) || VirtualInput.IsHeld("fire");
        }
    }
}
