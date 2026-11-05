using System.Collections.Generic;
using System.Linq;
using Unity.FPS.NewMode;
using UnityEditor;
using UnityEngine;

namespace Unity.FPS.NewModeEditor
{
    // ithappy Creative Characters FREE 의 부품 프리팹들을 모아 Resources/CharacterParts.asset 을 만든다.
    // 에셋을 새로 받거나 부품이 늘어나면 메뉴 Tools > New Mode > Rebuild Character Parts 를 다시 실행하면 된다.
    public static class CharacterPartsBuilder
    {
        const string k_Root = "Assets/ithappy/Creative_Characters_FREE/";
        const string k_Output = "Assets/NewMode/Resources/CharacterParts.asset";

        // 슬롯 정의: (id, 화면 이름, 없음 허용, 전신 코스튬 여부, 대상 렌더러, 프리팹 폴더들)
        static readonly (string id, string label, bool allowNone, bool fullBody, string renderer, string[] folders)[] k_Slots =
        {
            ("body", "몸", false, false, "Body", new[] { "Body" }),
            ("face", "얼굴", false, false, "Faces", new[] { "Faces" }),
            ("hair", "머리", true, false, "Hairstyle", new[] { "Hairstyle", "Hairstyle Single" }),
            ("hat", "모자", true, false, "Hat", new[] { "Hat", "Hat Single" }),
            ("top", "상의", true, false, "Outerwear", new[] { "Outfit", "Outwear", "Mascots" }),
            ("pants", "하의", true, false, "Pants", new[] { "Pants", "Shorts" }),
            ("shoes", "신발", true, false, "Shoes", new[] { "Shoes", "Socks" }),
            ("gloves", "장갑", true, false, "Gloves", new[] { "Gloves" }),
            ("glasses", "안경", true, false, "Glasses", new[] { "Glasses" }),
            ("acc", "얼굴 장식", true, false, "Accessories", new[] { "Face Accessories" }),
        };

        // 프리팹 이름 → 보기 좋은 한글 이름 (앞부분이 맞으면 사용, 같은 이름이 여럿이면 번호를 붙인다)
        static readonly (string prefix, string label)[] k_Names =
        {
            ("Body", "기본"),
            ("Male_emotion_usual", "보통"), ("Male_emotion_happy", "웃음"), ("Male_emotion_angry", "화남"),
            ("Hairstyle_Male_Single", "짧은 머리"), ("Hairstyle_Male", "머리"),
            ("Hat_Single", "모자"), ("Hat", "털모자"),
            ("Outfit", "한 벌 옷"), ("Outwear", "겉옷"), ("Mascot", "인형 옷"),
            ("Pants", "긴바지"), ("Shorts", "반바지"),
            ("Shoe_Slippers", "슬리퍼"), ("Shoe_Sneakers", "운동화"), ("Socks", "양말"),
            ("Gloves", "장갑"), ("Glasses", "안경"),
            ("Clown_nose", "광대 코"), ("Headphones", "헤드폰"), ("Mustache", "콧수염"), ("Pacifier", "쪽쪽이"),
        };

        [MenuItem("Tools/New Mode/Rebuild Character Parts")]
        public static void Build()
        {
            var parts = AssetDatabase.LoadAssetAtPath<CharacterParts>(k_Output);
            if (!parts)
            {
                if (!AssetDatabase.IsValidFolder("Assets/NewMode/Resources")) AssetDatabase.CreateFolder("Assets/NewMode", "Resources");
                parts = ScriptableObject.CreateInstance<CharacterParts>();
                AssetDatabase.CreateAsset(parts, k_Output);
            }

            parts.BaseModel = AssetDatabase.LoadAssetAtPath<GameObject>(k_Root + "Prefabs/Base_Mesh.prefab");
            parts.Animator = EnsureIKController();
            parts.Material = AssetDatabase.LoadAssetAtPath<Material>(k_Root + "Materials/Color.mat");

            var slots = new List<CharacterParts.Slot>();
            foreach (var def in k_Slots)
            {
                var options = new List<CharacterParts.Option>();
                foreach (string folder in def.folders)
                foreach (GameObject prefab in LoadPrefabs(k_Root + "Prefabs/" + folder))
                {
                    Mesh mesh = prefab.GetComponentInChildren<SkinnedMeshRenderer>(true)?.sharedMesh;
                    if (!mesh) continue;
                    options.Add(new CharacterParts.Option
                    {
                        Label = prefab.name,
                        Parts = new[] { new CharacterParts.Part { Renderer = def.renderer, Mesh = mesh } },
                    });
                }

                if (options.Count == 0) continue;
                NameOptions(options);
                slots.Add(new CharacterParts.Slot { Id = def.id, Label = def.label, AllowNone = def.allowNone, Options = options.ToArray() });
            }

            // 전신 코스튬: 한 벌이 여러 부위(상의 + 모자)로 나뉘어 있다
            var costumes = LoadPrefabs(k_Root + "Prefabs/Costumes")
                .GroupBy(p => p.name.Substring(0, p.name.LastIndexOf('_')))
                .Select((g, i) => new CharacterParts.Option
                {
                    Label = "코스튬 " + (i + 1),
                    Parts = g.Select((p, k) => new CharacterParts.Part
                    {
                        Renderer = k == 0 ? "Outerwear" : "Hat",
                        Mesh = p.GetComponentInChildren<SkinnedMeshRenderer>(true).sharedMesh,
                    }).ToArray(),
                }).ToArray();
            if (costumes.Length > 0)
                slots.Add(new CharacterParts.Slot { Id = "costume", Label = "코스튬", AllowNone = true, FullBody = true, Options = costumes });

            parts.Slots = slots.ToArray();
            EditorUtility.SetDirty(parts);
            AssetDatabase.SaveAssets();
            Debug.Log($"[NewMode] CharacterParts 생성: 슬롯 {parts.Slots.Length}개, 부품 {parts.Slots.Sum(s => s.Options.Length)}개");
        }

        // 에셋의 애니메이터를 복사해서 IK Pass를 켠다 (검을 휘두를 때 손을 IK로 움직이기 위해). 원본은 건드리지 않는다.
        const string k_IKController = "Assets/NewMode/Animation/NewModeCharacter.controller";

        static RuntimeAnimatorController EnsureIKController()
        {
            if (!AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(k_IKController))
            {
                if (!AssetDatabase.IsValidFolder("Assets/NewMode/Animation")) AssetDatabase.CreateFolder("Assets/NewMode", "Animation");
                AssetDatabase.CopyAsset(k_Root + "Animations/Animation_Controllers/Character_Movement.controller", k_IKController);
            }

            var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(k_IKController);
            var layers = controller.layers;
            for (int i = 0; i < layers.Length; i++) layers[i].iKPass = true;
            controller.layers = layers;
            // 점프가 바로 나가도록: 전환은 조건만 보고(Exit Time 없음) 짧게
            foreach (var layer in controller.layers)
                foreach (var cs in layer.stateMachine.states)
                    foreach (var t in cs.state.transitions)
                    {
                        t.hasExitTime = false;
                        t.hasFixedDuration = true;
                        t.duration = cs.state.name == "Jump" ? 0.15f : 0.08f;
                    }
            EditorUtility.SetDirty(controller);
            return controller;
        }

        static IEnumerable<GameObject> LoadPrefabs(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return Enumerable.Empty<GameObject>();
            return AssetDatabase.FindAssets("t:Prefab", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => System.IO.Path.GetDirectoryName(p).Replace('\\', '/') == folder) // 하위 폴더 제외
                .OrderBy(p => p)
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>);
        }

        static void NameOptions(List<CharacterParts.Option> options)
        {
            foreach (var option in options)
                foreach (var (prefix, label) in k_Names)
                    if (option.Label.StartsWith(prefix)) { option.Label = label; break; }

            foreach (var group in options.GroupBy(o => o.Label).Where(g => g.Count() > 1))
            {
                int n = 1;
                foreach (var option in group) option.Label += " " + n++;
            }
        }
    }
}
