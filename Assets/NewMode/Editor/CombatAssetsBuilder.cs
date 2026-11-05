using System.Collections.Generic;
using System.Linq;
using Unity.FPS.NewMode;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.Rendering.Universal;
using UnityEngine;

namespace Unity.FPS.NewModeEditor
{
    // 무기·이펙트 에셋 준비:
    //  1) Hovl Magic effects pack의 머티리얼을 URP용으로 바꾼다 (원래 Built-in 셰이더라 URP에서 분홍색으로 보임)
    //  2) 무기·이펙트 프리팹을 모아 Resources/CombatAssets.asset 을 만든다 (게임 실행 중에 이름으로 꺼내 쓰기 위해)
    public static class CombatAssetsBuilder
    {
        const string k_Output = "Assets/NewMode/Resources/CombatAssets.asset";
        const string k_Weapons = "Assets/ithappy/Weapons_FREE/Prefabs";
        static readonly string[] k_EffectFolders =
        {
            "Assets/Hovl Studio/Magic effects pack/Prefabs",
            "Assets/Eric VFX Studio/Free Game VFX/Prefab",
        };

        [MenuItem("Tools/New Mode/Setup Combat Assets")]
        public static void Build()
        {
            int converted = ConvertHovlMaterials();

            var assets = AssetDatabase.LoadAssetAtPath<CombatAssets>(k_Output);
            if (!assets)
            {
                assets = ScriptableObject.CreateInstance<CombatAssets>();
                AssetDatabase.CreateAsset(assets, k_Output);
            }

            assets.Weapons = Collect(new[] { k_Weapons }, false);
            assets.Effects = Collect(k_EffectFolders, true);
            EditorUtility.SetDirty(assets);
            AssetDatabase.SaveAssets();
            Debug.Log($"[NewMode] CombatAssets: 무기 {assets.Weapons.Length}개, 이펙트 {assets.Effects.Length}개 (URP 변환 머티리얼 {converted}개)");
        }

        static CombatAssets.Entry[] Collect(string[] folders, bool skipMeshFolder)
        {
            var list = new List<CombatAssets.Entry>();
            foreach (string folder in folders)
            {
                if (!AssetDatabase.IsValidFolder(folder)) continue;
                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (skipMeshFolder && path.Contains("/Prefab/Mesh/")) continue; // Eric: 이펙트 부품 메시
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab) list.Add(new CombatAssets.Entry { Name = prefab.name, Prefab = prefab });
                }
            }
            return list.OrderBy(e => e.Name).ToArray();
        }

        // Built-in 파티클 셰이더 → URP 파티클 셰이더 (Unity 공식 업그레이더 사용)
        static int ConvertHovlMaterials()
        {
            var upgraders = new Dictionary<string, MaterialUpgrader>
            {
                { "Particles/Standard Unlit", new ParticleUpgrader("Particles/Standard Unlit") },
                { "Particles/Standard Surface", new ParticleUpgrader("Particles/Standard Surface") },
                { "Standard", new StandardUpgrader("Standard") },
            };

            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Hovl Studio" }))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (!material || !material.shader || !upgraders.TryGetValue(material.shader.name, out var upgrader)) continue;
                MaterialUpgrader.Upgrade(material, upgrader, MaterialUpgrader.UpgradeFlags.None);
                EditorUtility.SetDirty(material);
                count++;
            }
            return count;
        }
    }
}
