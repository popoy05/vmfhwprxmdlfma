using System;
using UnityEngine;

namespace Unity.FPS.NewMode
{
    // 전투에 쓰는 외부 에셋 목록 (무기 모델 · 이펙트 프리팹).
    // 에셋: Assets/NewMode/Resources/CombatAssets.asset — 메뉴 Tools/New Mode/Setup Combat Assets 로 다시 만든다.
    //  - 무기: ithappy Weapons_FREE
    //  - 이펙트: Hovl Studio Magic effects pack, Eric VFX Studio Free Game VFX
    public class CombatAssets : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string Name;
            public GameObject Prefab;
        }

        public Entry[] Weapons = Array.Empty<Entry>();
        public Entry[] Effects = Array.Empty<Entry>();
        /// <summary>몹 모델: Whale(보스) · GhostYellow/GhostBlack/GhostRed(일반 몹)</summary>
        public Entry[] Mobs = Array.Empty<Entry>();

        static CombatAssets s_Instance;
        public static CombatAssets Load() => s_Instance ? s_Instance : (s_Instance = Resources.Load<CombatAssets>("CombatAssets"));

        public GameObject Weapon(string name) => Find(Weapons, name);
        public GameObject Effect(string name) => Find(Effects, name);
        public GameObject Mob(string name) => Find(Mobs, name);

        static GameObject Find(Entry[] list, string name)
        {
            foreach (var e in list)
                if (e.Name == name) return e.Prefab;
            return null;
        }
    }
}
