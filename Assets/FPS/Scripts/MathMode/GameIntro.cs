using TMPro;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Unity.FPS.MathMode
{
    // 게임 시작 전 '임무 브리핑' 창: 조작 방법 + 해야 할 임무 + 시작 버튼.
    // 맵·난이도는 메인 메뉴에서 고른다 (여기서는 보여주기만 하고, 바꾸려면 '메인 메뉴'로).
    // 마우스 클릭·터치·키보드(Enter/Space) 모두 된다. 게임 중에는 [H] 또는 화면의 ? 버튼으로 다시 연다.
    public class GameIntro : MonoBehaviour
    {
        MathModeManager m_Manager;
        PlayerInputHandler m_Input;
        GameObject m_Root;
        TMP_FontAsset m_Font;
        TextMeshProUGUI m_Header, m_Missions, m_DiffInfo, m_StartLabel;
        int m_Map;
        Difficulty m_Difficulty;
        bool m_Started;
        bool m_Open;

        public void Init(MathModeManager manager, PlayerInputHandler input)
        {
            m_Manager = manager;
            m_Input = input;
            m_Font = manager.Font;
            m_Map = GameSettings.Map;
            m_Difficulty = GameSettings.Difficulty;
            UIUtil.EnsureEventSystem();
            Build();
            Open();
        }

        void Build()
        {
            Canvas canvas = UIUtil.Canvas(transform, "IntroCanvas", 50);
            m_Root = canvas.gameObject;

            var dim = UIUtil.Image(canvas.transform, "Dim", new Color(0f, 0.01f, 0.04f, 0.8f));
            UIUtil.Stretch(dim, 0f);
            dim.GetComponent<Image>().raycastTarget = true; // 뒤의 게임 화면이 눌리지 않도록

            var panel = UIUtil.RoundedImage(canvas.transform, "Panel", new Color(0.05f, 0.08f, 0.15f, 0.97f));
            UIUtil.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1320f, 820f));

            m_Header = UIUtil.Text(panel, m_Font, "", 44f, TextAlignmentOptions.Center);
            UIUtil.Place(m_Header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -55f), new Vector2(1200f, 70f));

            // 왼쪽: 조작 방법 (기기에 맞게)
            var keysBox = UIUtil.RoundedImage(panel, "Keys", new Color(1f, 1f, 1f, 0.05f));
            UIUtil.Place(keysBox, new Vector2(0f, 1f), new Vector2(330f, -310f), new Vector2(600f, 340f));
            var keys = UIUtil.Text(keysBox, m_Font, ControlsText(), 24f, TextAlignmentOptions.TopLeft);
            UIUtil.Stretch(keys.rectTransform, 18f);
            keys.enableAutoSizing = true;
            keys.fontSizeMin = 14f;
            keys.fontSizeMax = 24f;

            // 오른쪽: 임무
            var missionBox = UIUtil.RoundedImage(panel, "Missions", new Color(1f, 1f, 1f, 0.05f));
            UIUtil.Place(missionBox, new Vector2(1f, 1f), new Vector2(-330f, -310f), new Vector2(600f, 340f));
            m_Missions = UIUtil.Text(missionBox, m_Font, "", 24f, TextAlignmentOptions.TopLeft);
            UIUtil.Stretch(m_Missions.rectTransform, 18f);
            m_Missions.enableAutoSizing = true;
            m_Missions.fontSizeMin = 14f;
            m_Missions.fontSizeMax = 24f;

            // 난이도 설명 (선택은 메인 메뉴에서)
            m_DiffInfo = UIUtil.Text(panel, m_Font, "", 24f, TextAlignmentOptions.Center);
            UIUtil.Place(m_DiffInfo.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -540f), new Vector2(1200f, 70f));
            m_DiffInfo.color = new Color(0.75f, 0.82f, 0.95f);
            m_DiffInfo.enableAutoSizing = true;
            m_DiffInfo.fontSizeMin = 14f;
            m_DiffInfo.fontSizeMax = 24f;

            // 시작 버튼 + 메인 메뉴 버튼
            var start = UIUtil.Button(panel, m_Font, "", new Color(0.2f, 0.75f, 0.4f), Confirm, new Vector2(480f, 110f), 44f);
            UIUtil.Place((RectTransform)start.transform, new Vector2(0.5f, 0f), new Vector2(-140f, 100f), new Vector2(480f, 110f));
            m_StartLabel = start.GetComponentInChildren<TextMeshProUGUI>();

            var menu = UIUtil.Button(panel, m_Font, "메인 메뉴\n<size=65%>맵·난이도 바꾸기</size>", new Color(0.3f, 0.32f, 0.5f),
                GoToMainMenu, new Vector2(240f, 110f), 30f);
            UIUtil.Place((RectTransform)menu.transform, new Vector2(0.5f, 0f), new Vector2(250f, 100f), new Vector2(240f, 110f));
        }

        static string ControlsText()
        {
            if (GameSettings.UseMobileControls)
                return "<size=115%><b><color=#66E6FF>터치 조작</color></b></size>\n" +
                       "<b>왼쪽 화면</b> 끌기 = 이동 (끝까지 밀면 달리기)\n" +
                       "<b>오른쪽 화면</b> 끌기 = 바라보기\n" +
                       "<b>빨간 큰 버튼</b> 쏘기 (누른 채 끌면 조준도 이동)\n" +
                       "<b>점프 · 조준 · 장전</b> 버튼, <b>◀ ▶</b> 무기 바꾸기 (자동 무기를 켜면 알아서)\n" +
                       "<b>Z X C V</b> 수학 스킬, <b>Σ</b> 궁극기, <b>단검</b>\n" +
                       "<b>||</b> 일시정지·메인 메뉴  <b>?</b> 이 화면  <b>지도</b> 좌표평면";

            string auto = $"<size=90%>자동 무기 {GameSettings.OnOff(GameSettings.AutoWeapon)} · 자동 스킬 {GameSettings.OnOff(GameSettings.AutoSkills)} · 조준 보정 {GameSettings.OnOff(GameSettings.AimAssist)}</size>\n";
            bool easy = GameSettings.AutoWeapon && GameSettings.AutoSkills;
            return "<size=115%><b><color=#66E6FF>꼭 알아야 할 키 (이것만 있으면 OK)</color></b></size>\n" +
                   "<b>WASD</b> 이동   <b>마우스</b> 바라보기\n<b>좌클릭</b> 쏘기   <b>Space</b> 점프\n" +
                   auto +
                   (easy
                       ? "<size=90%><color=#AAB4C8>무기 교체·스킬은 자동! 직접 하고 싶으면 Q/E, Z X C V G</color></size>\n"
                       : "<b>Q / E</b> 무기  <b>Z</b> 충격파 <b>X</b> 대시 <b>C</b> 포격 <b>V</b> 회복 <b>G</b> 궁극기\n") +
                   "<size=90%><color=#AAB4C8>R 재장전 · Shift 달리기 · F 단검 · M 지도 · H 이 화면 · Tab 일시정지</color></size>";
        }

        void Refresh()
        {
            DifficultyPreset p = DifficultyPreset.For(m_Difficulty);
            string diffColor = GameSettings.DifficultyColor(m_Difficulty);
            m_Header.text = $"<b>임무 브리핑</b>  <size=70%><color=#AAB4C8>{GameSettings.MapName(m_Map)} · </color><color={diffColor}>{GameSettings.DifficultyName(m_Difficulty)}</color></size>";

            var tierNames = new System.Collections.Generic.List<string>();
            foreach (int tier in p.StationTiers)
            {
                string name = MathProblemGenerator.TierNames[MathProblemGenerator.ClampTier(tier)];
                string hex = ColorUtility.ToHtmlStringRGB(MathProblemGenerator.TierColors[MathProblemGenerator.ClampTier(tier)]);
                string item = $"<color=#{hex}>{name}</color>";
                if (!tierNames.Contains(item)) tierNames.Add(item);
            }

            string stations = string.Join(" → ", tierNames) + (p.WrongAnswerKills ? "  <color=#FF5050><b>오답 = 즉사</b></color>" : "");
            string hunt = p.DirectHuntClue ? "목표 좌표로 걸어가기" : "수식 단서를 풀어 좌표 찾기";
            m_Missions.text =
                "<size=115%><b><color=#7FFFD4>임무 (모두 하면 승리)</color></b></size>\n" +
                $"<b>1. 적 처치</b> <color=#FF6B6B>테두리가 빛나는 적</color> <size=85%>(최대 {p.EnemyCount}마리, 종류마다 특기가 다름)</size>\n" +
                $"<b>2. 수학 관문 {p.Stations}곳</b> <color=#FFD54A>노란 ◆</color>\n   <size=85%>{stations}\n   정답이 적힌 표적을 쏘기</size>\n" +
                $"<b>3. 좌표 지점 {p.Beacons}곳</b> <color=#4DFFFF>하늘색 ◆</color>\n   <size=85%>{hunt}</size>\n" +
                $"<b>4. 최종 보스</b> <color=#FFC830>{MathEnemy.NameOf(EnemyKind.Boss)}</color> <size=85%>(1~3을 끝내면 등장)</size>\n" +
                "<color=#6BFF8A><b>화면 위 '다음 목표'를 따라가세요!</b></color>";
            m_DiffInfo.text = GameSettings.DifficultyDescription(m_Difficulty).Replace("\n", "  ·  ");
            m_StartLabel.text = m_Started ? "<b>계속하기</b>" : "<b>▶  시작</b>";
        }

        void Open()
        {
            m_Open = true;
            if (m_Started)
            {
                m_Map = m_Manager.CurrentMapChoice;
                m_Difficulty = m_Manager.CurrentDifficulty;
            }

            m_Root.SetActive(true);
            Time.timeScale = 0f;
            if (m_Input) m_Input.InputBlocked = true;
            Cursor.lockState = CursorLockMode.None; // 버튼을 마우스로 누를 수 있도록
            Cursor.visible = true;
            Refresh();
        }

        void Close()
        {
            m_Open = false;
            m_Root.SetActive(false);
            Time.timeScale = 1f;
            if (m_Input) m_Input.InputBlocked = false;
            if (!VirtualInput.Active)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        void Confirm()
        {
            if (m_Started)
            {
                Close();
                return;
            }

            Close();
            m_Started = true;
            m_Manager.BeginGame(m_Map, m_Difficulty);
        }

        static void GoToMainMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(AppBootstrap.MenuScene);
        }

        void Update()
        {
            Keyboard kb = Keyboard.current;

            if (!m_Open)
            {
                if (m_Started && ((kb != null && kb.hKey.wasPressedThisFrame) || VirtualInput.WasPressed("help")) &&
                    !(m_Manager.Ultimate && m_Manager.Ultimate.IsPlaying))
                    Open();
                return;
            }

            if (kb == null) return;
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
                Confirm();
            else if (m_Started && (kb.hKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame))
                Close();
        }
    }
}
