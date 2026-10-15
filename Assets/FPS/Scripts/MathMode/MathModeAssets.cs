using System.Collections.Generic;
using TMPro;
using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.MathMode
{
    // 수학 모드가 쓰는 에셋 묶음. Resources 폴더에 두어 어느 씬(메뉴·결과 화면 포함)에서도, 빌드된 앱에서도 불러올 수 있다.
    // Tools > Math Mode > 씬에 수학 모드 설치 가 자동으로 만들고 채운다.
    [CreateAssetMenu(menuName = "Math Mode/Assets")]
    public class MathModeAssets : ScriptableObject
    {
        public const string ResourcePath = "MathModeAssets";

        public TMP_FontAsset Font;
        public Material UnlitMaterial;
        public Material LineMaterial;
        public Material LitMaterial;
        public Material[] ThemeSkyboxes;
        public GameObject RewardPrefab;
        public GameObject PenaltyEnemyPrefab;
        public GameObject ExplosionVfx;
        [Tooltip("플레이어에게 항상 지급할 무기 (기본 무기 + 수학 무기)")]
        public List<WeaponController> Weapons = new List<WeaponController>();

        static MathModeAssets s_Cached;

        public static MathModeAssets Load()
        {
            if (!s_Cached) s_Cached = Resources.Load<MathModeAssets>(ResourcePath);
            return s_Cached;
        }
    }
}
