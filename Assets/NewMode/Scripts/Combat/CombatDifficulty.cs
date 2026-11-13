using UnityEngine;

namespace Unity.FPS.NewMode
{
    // 전투 난이도 (수학 문제 난이도와 따로) — 캐릭터 화면에서 고른다
    //  어려움: 몹이 훨씬 단단하고 아프게 때리고 빠르다. 보스는 처음부터 2단계 패턴, 체력 50% 아래면 격노.
    //          회복은 늦고 느리며, 맞은 뒤 무적 시간도 짧다. (훈련장은 그대로)
    public static class CombatDifficulty
    {
        public static bool Hard
        {
            get => PlayerPrefs.GetInt("combat.hard", 0) == 1;
            set { PlayerPrefs.SetInt("combat.hard", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static string Name => Hard ? "어려움" : "보통";
        public static string Info => Hard
            ? "몹 체력 ×2.5 · 받는 피해 ×2.2 · 몹이 빠르고 공격이 잦음\n보스: 처음부터 강한 패턴, 체력 50%부터 격노 · 회복 느림"
            : "기본 전투";

        /// <summary>몹이 받는 피해 배율 (적대 몹만)</summary>
        public static float MobDamageTaken => Hard ? 0.4f : 1f;
        /// <summary>플레이어가 받는 피해 배율</summary>
        public static float PlayerDamageTaken => Hard ? 2.2f : 1f;
        /// <summary>몹 이동 속도 배율</summary>
        public static float MobSpeed => Hard ? 1.55f : 1f;
        /// <summary>몹 공격 예고·재사용 시간 배율 (작을수록 빠름)</summary>
        public static float MobTempo => Hard ? 0.6f : 1f;
        /// <summary>몹이 알아채는 거리 배율</summary>
        public static float MobAggro => Hard ? 1.4f : 1f;
        /// <summary>보스 패턴 사이 간격 · 예고 시간 배율</summary>
        public static float BossTempo => Hard ? 0.65f : 1f;
        public static float BossWarn => Hard ? 0.75f : 1f;
        /// <summary>회복: 마지막으로 맞은 뒤 기다리는 시간 · 초당 회복량</summary>
        public static float RegenDelay => Hard ? 6f : 3f;
        public static float RegenRate => Hard ? 3f : 8f;
        /// <summary>맞은 뒤 무적 시간</summary>
        public static float IFrames => Hard ? 0.25f : 0.4f;
    }
}
