using System;
using UnityEngine;

namespace Unity.FPS.NewMode
{
    // 새 모드 캐릭터 꾸미기에 쓰는 부품 목록 (ithappy Creative Characters FREE).
    // 에셋: Assets/NewMode/Resources/CharacterParts.asset — 메뉴 Tools/New Mode/Rebuild Character Parts 로 다시 만들 수 있다.
    //
    // 캐릭터 구조: Base_Mesh 프리팹 안에 부위별 SkinnedMeshRenderer(Hat, Hairstyle, Outerwear ...)가 있고
    // 모두 같은 뼈대를 쓰므로, 부위 렌더러의 mesh만 바꿔 끼우면 옷이 바뀐다.
    public class CharacterParts : ScriptableObject
    {
        public GameObject BaseModel;
        public RuntimeAnimatorController Animator;
        public Material Material;
        public Slot[] Slots = Array.Empty<Slot>();

        [Serializable]
        public class Slot
        {
            public string Id;                 // 저장 키 (예: "hat")
            public string Label;              // 화면에 보이는 이름 (예: "모자")
            public bool AllowNone = true;     // "없음" 선택 가능 여부
            [Tooltip("이 슬롯을 고르면 몸·얼굴을 뺀 다른 슬롯이 모두 가려진다 (전신 코스튬)")]
            public bool FullBody;
            public Option[] Options = Array.Empty<Option>();
        }

        [Serializable]
        public class Option
        {
            public string Label;
            public Part[] Parts = Array.Empty<Part>();
        }

        [Serializable]
        public struct Part
        {
            public string Renderer;           // Base_Mesh 안의 렌더러 이름 (예: "Hat")
            public Mesh Mesh;
        }

        static CharacterParts s_Instance;
        public static CharacterParts Load() => s_Instance ? s_Instance : (s_Instance = Resources.Load<CharacterParts>("CharacterParts"));
    }

    // 플레이어가 고른 모습. 슬롯마다 "선택 번호"를 저장한다 (-1 = 없음). PlayerPrefs에 보관되어 껐다 켜도 유지된다.
    public static class PlayerAppearance
    {
        const string k_Prefix = "NewMode.Appearance.";

        // 처음 시작할 때의 기본 모습
        static int DefaultIndex(CharacterParts.Slot slot)
        {
            string label;
            switch (slot.Id)
            {
                case "face": label = "보통"; break;
                case "hair": label = "머리 1"; break;
                case "top": label = "겉옷 1"; break;
                case "pants": label = "긴바지 1"; break;
                case "shoes": label = "운동화"; break;
                default: return slot.AllowNone ? -1 : 0;
            }

            int index = Array.FindIndex(slot.Options, o => o.Label == label);
            return index >= 0 ? index : 0;
        }

        public static int Get(CharacterParts.Slot slot)
        {
            int value = PlayerPrefs.GetInt(k_Prefix + slot.Id, DefaultIndex(slot));
            int min = slot.AllowNone ? -1 : 0;
            return Mathf.Clamp(value, min, slot.Options.Length - 1);
        }

        public static void Set(CharacterParts.Slot slot, int index)
        {
            PlayerPrefs.SetInt(k_Prefix + slot.Id, index);
        }

        public static void Save() => PlayerPrefs.Save();

        public static void ResetAll(CharacterParts parts)
        {
            foreach (var slot in parts.Slots) PlayerPrefs.DeleteKey(k_Prefix + slot.Id);
        }

        // 캐릭터 모델을 만들어 parent 아래에 붙인다 (발 위치 = parent 원점)
        public static GameObject Spawn(Transform parent)
        {
            CharacterParts parts = CharacterParts.Load();
            if (!parts || !parts.BaseModel) return null;

            GameObject model = UnityEngine.Object.Instantiate(parts.BaseModel, parent, false);
            model.name = "Character Model";
            Animator animator = model.GetComponentInChildren<Animator>();
            if (animator)
            {
                animator.runtimeAnimatorController = parts.Animator;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            Apply(model);
            return model;
        }

        // 저장된 선택대로 부위 렌더러의 mesh를 바꾼다
        public static void Apply(GameObject model)
        {
            CharacterParts parts = CharacterParts.Load();
            if (!parts || !model) return;

            var renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            // 일단 몸을 뺀 모든 부위를 끈다 (Base_Mesh의 기본 mesh는 꼭짓점 4개짜리 빈 자리)
            foreach (var r in renderers) r.enabled = false;

            CharacterParts.Slot fullBody = null;
            foreach (var slot in parts.Slots)
                if (slot.FullBody && Get(slot) >= 0) fullBody = slot;

            foreach (var slot in parts.Slots)
            {
                bool basic = slot.Id == "body" || slot.Id == "face";
                if (fullBody != null && slot != fullBody && !basic) continue; // 전신 코스튬이면 다른 옷은 숨김

                int index = Get(slot);
                if (index < 0) continue;
                foreach (var part in slot.Options[index].Parts)
                {
                    SkinnedMeshRenderer target = Array.Find(renderers, r => r.name == part.Renderer);
                    if (!target || !part.Mesh) continue;
                    target.sharedMesh = part.Mesh;
                    target.sharedMaterial = parts.Material;
                    target.localBounds = part.Mesh.bounds;
                    target.updateWhenOffscreen = true; // 애니메이션 중 부위가 잘려 사라지지 않도록
                    target.enabled = true;
                }
            }
        }
    }
}
