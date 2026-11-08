using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.FPS.NewMode
{
    public enum WeaponKind { Melee, Gun }

    // 스킬 하나: 이름·함수식·쿨타임·실행 코루틴
    public class SkillDef
    {
        public string Key, Name, Formula;
        public string Desc;                   // 캐릭터 정보 창에 보이는 한 줄 설명
        public float Cooldown, Remaining;
        public Func<IEnumerator> Run;
        public Func<string> Detail;          // 쓸 때 화면 위에 크게 뜨는 식 (실제 숫자 포함)

        // HUD
        public RectTransform Slot;
        public Image CooldownFill;
        public TextMeshProUGUI CooldownText;
        public bool WasReady = true;
    }

    // 무기 하나: 모델 + 평타 + 스킬 4개(Q·E·X·C)
    public class WeaponDef
    {
        public int Index;
        public string Name, Theme;
        public WeaponKind Kind;
        public Color Color;
        public float FireInterval = 0.3f;   // 총: 좌클릭을 누르고 있을 때 연사 간격
        public SkillDef Basic;
        public SkillDef[] Skills;

        // 무기 기믹
        public string GimmickName, GimmickInfo;      // 예: "서리 낙인", "3번 맞히면 얼어붙고, 얼어붙은 적을 치면 산산조각"
        public Func<string> Gimmick;                 // HUD에 계속 보이는 상태 (예: "탄창 9 / 12")
        public Action Tick;                          // 매 프레임 (무기 고유 입력·게이지)
        public float BasicRange = 2.5f;                 // 평타 거리 (파고들기 기준, 스케일 1)
        public Func<TrainingDummy, float> HitMultiplier; // 맞힐 때 피해 배율 (기믹 효과), 태그는 HitTag로
    }

    // 무기 목록 (모델과 기본 정보). 스킬은 NewModeCombat.<무기>.cs 에 있다.
    public static class WeaponCatalog
    {
        public struct Info
        {
            public string Name, Model, Theme;
            public string ClassName, Desc;   // 직업 이름 · 한 줄 설명 (캐릭터 꾸미기 화면)
            public WeaponKind Kind;
            public bool TwoHanded;
            public Color Color;
            public string Role;              // 역할 (근접 균형 · 탱커 · 암살자 · 원거리)
            public ClassStats Stats;
        }

        // 직업 능력치 (캐릭터 정보 창에 보이고, 실제 전투에 그대로 쓰인다)
        public struct ClassStats
        {
            public float MaxHP;        // 최대 체력
            public float Attack;       // 공격력 배율 (1 = 100%)
            public float AttackSpeed;  // 공격 속도 배율 (휘두르기·연사 빠르기)
            public float Crit;         // 치명타 확률 (0~1)
            public float CritDamage;   // 치명타 피해 배율
            public float Move;         // 이동 속도 배율
            public float Defense;      // 받는 피해 감소 (0~1)
            public float Headshot;     // 머리 명중 배율 (총)
        }

        // 직업 4개 — 각자 확실히 다른 역할
        //  서리 검사   : 근접 균형형 · 얼려서 깨뜨리는 제어
        //  대지 전사   : 느리고 단단한 탱커 · 모아 찍기 한 방
        //  번개 암살자 : 빠르고 약한 몸 · 치명타와 연쇄 번개, 순간이동
        //  황금 명사수 : 원거리 · 탄창 관리와 조준 헤드샷
        public static readonly Info[] All =
        {
            new Info { Name = "서리검", Model = null, Theme = "얼음", ClassName = "서리 검사", Role = "근접 · 제어", Desc = "3타 빙결 → 산산조각",
                Kind = WeaponKind.Melee, Color = new Color(0.45f, 0.85f, 1f),
                Stats = new ClassStats { MaxHP = 125f, Attack = 1.2f, AttackSpeed = 1.05f, Crit = 0.2f, CritDamage = 1.9f, Move = 1.02f, Defense = 0.12f, Headshot = 1f } },
            new Info { Name = "대지 도끼", Model = "axe_001", Theme = "대지", ClassName = "대지 전사", Role = "근접 · 탱커", Desc = "길게 눌러 모아 찍기",
                Kind = WeaponKind.Melee, Color = new Color(1f, 0.6f, 0.25f),
                Stats = new ClassStats { MaxHP = 170f, Attack = 1.4f, AttackSpeed = 0.95f, Crit = 0.12f, CritDamage = 2.0f, Move = 0.97f, Defense = 0.25f, Headshot = 1f } },
            new Info { Name = "번개 단검", Model = "knife_001", Theme = "번개", ClassName = "번개 암살자", Role = "근접 · 암살", Desc = "연쇄 번개 · 표식 폭발",
                Kind = WeaponKind.Melee, Color = new Color(1f, 0.45f, 0.85f),
                Stats = new ClassStats { MaxHP = 100f, Attack = 1.05f, AttackSpeed = 1.3f, Crit = 0.33f, CritDamage = 2.15f, Move = 1.15f, Defense = 0.05f, Headshot = 1f } },
            new Info { Name = "권총", Model = "pistol_001", Theme = "황금", ClassName = "황금 명사수", Role = "원거리 · 저격", Desc = "12발 탄창 · 조준 헤드샷",
                Kind = WeaponKind.Gun, Color = new Color(1f, 0.85f, 0.3f),
                Stats = new ClassStats { MaxHP = 115f, Attack = 1.2f, AttackSpeed = 1.15f, Crit = 0.25f, CritDamage = 2.0f, Move = 1.08f, Defense = 0.08f, Headshot = 1.8f } },
        };

        public static Info Current => All[Selected];

        const string k_Pref = "NewMode.Weapon";
        public static int Selected
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(k_Pref, 0), 0, All.Length - 1);
            set { PlayerPrefs.SetInt(k_Pref, Mathf.Clamp(value, 0, All.Length - 1)); PlayerPrefs.Save(); }
        }
    }

    // 무기 모델 만들기. 반환한 오브젝트의 원점 = 손잡이, +Z = 칼날(총구) 방향.
    public static class WeaponVisual
    {
        public static GameObject Create(int index, float scale, out Vector3 tipLocal)
        {
            WeaponCatalog.Info info = WeaponCatalog.All[index];
            var root = new GameObject("Weapon - " + info.Name);

            if (string.IsNullOrEmpty(info.Model))
            {
                // 손잡이 가운데(z = −0.1)가 원점이 되도록 0.1만큼 앞으로
                var holder = new GameObject("Sword").transform;
                holder.SetParent(root.transform, false);
                holder.localPosition = new Vector3(0f, 0f, 0.1f);
                BuildSword(holder);
                tipLocal = new Vector3(0f, 0f, 1.05f);
            }
            else
            {
                CombatAssets assets = CombatAssets.Load();
                GameObject prefab = assets ? assets.Weapon(info.Model) : null;
                if (prefab)
                {
                    GameObject model = UnityEngine.Object.Instantiate(prefab, root.transform, false);
                    foreach (var c in model.GetComponentsInChildren<Collider>()) UnityEngine.Object.Destroy(c);
                    // 근접 무기 모델은 +Y가 칼날 방향이라 +Z로 눕힌다 (총은 원래 +Z가 총구)
                    if (info.Kind == WeaponKind.Melee) model.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                }

                var bounds = new Bounds(Vector3.zero, Vector3.zero);
                foreach (var r in root.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds); // root가 원점·단위 회전이라 = 로컬 경계
                tipLocal = new Vector3(0f, info.Kind == WeaponKind.Gun ? bounds.center.y + bounds.extents.y * 0.5f : 0f, bounds.max.z);
            }

            root.transform.localScale = Vector3.one * scale; // tipLocal은 로컬 좌표라 스케일은 Transform이 적용
            return root;
        }

        // 얼음검: 마름모 단면 칼날 + 금색 가드·손잡이 (모두 코드로 만듦)
        static void BuildSword(Transform parent)
        {
            var blade = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Sword Blade" };
            blade.color = new Color(0.78f, 0.92f, 1f);
            blade.SetFloat("_Metallic", 0.7f);
            blade.SetFloat("_Smoothness", 0.9f);
            blade.EnableKeyword("_EMISSION");
            blade.SetColor("_EmissionColor", new Color(0.25f, 0.6f, 1f) * 1.4f);
            var gold = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(1f, 0.78f, 0.3f) };
            gold.SetFloat("_Metallic", 0.8f);
            gold.SetFloat("_Smoothness", 0.7f);
            var grip = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.15f, 0.2f, 0.4f) };

            var go = new GameObject("Blade");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = BladeMesh(0.95f, 0.075f, 0.02f);
            go.AddComponent<MeshRenderer>().sharedMaterial = blade;
            Box(parent, "Guard", Vector3.zero, new Vector3(0.28f, 0.035f, 0.05f), gold);
            Box(parent, "Grip", new Vector3(0f, 0f, -0.1f), new Vector3(0.035f, 0.035f, 0.2f), grip);
            Box(parent, "Pommel", new Vector3(0f, 0f, -0.22f), new Vector3(0.06f, 0.06f, 0.05f), gold);
        }

        static void Box(Transform parent, string name, Vector3 center, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            UnityEngine.Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
        }

        static Mesh BladeMesh(float length, float width, float thickness)
        {
            float w = width * 0.5f, t = thickness * 0.5f;
            float z0 = 0.02f, z1 = length - 0.16f;
            Vector3[] a = { new Vector3(w, 0, z0), new Vector3(0, t, z0), new Vector3(-w, 0, z0), new Vector3(0, -t, z0) };
            Vector3[] b = { new Vector3(w * 0.9f, 0, z1), new Vector3(0, t, z1), new Vector3(-w * 0.9f, 0, z1), new Vector3(0, -t, z1) };
            Vector3 tip = new Vector3(0, 0, length);

            var verts = new List<Vector3>();
            var tris = new List<int>();
            void Tri(Vector3 p, Vector3 q, Vector3 r)
            {
                int i = verts.Count;
                verts.Add(p); verts.Add(q); verts.Add(r);
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            }

            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                Tri(a[i], b[j], b[i]);
                Tri(a[i], a[j], b[j]);
                Tri(b[i], b[j], tip);
            }
            Tri(a[0], a[2], a[1]);
            Tri(a[0], a[3], a[2]);

            var mesh = new Mesh { name = "Sword Blade" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
