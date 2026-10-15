using System.Collections.Generic;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace Unity.FPS.MathMode
{
    public static class MathCombat
    {
        // 무기 이름 → 한 줄 설명 (무기 교체 시 화면에 표시)
        static readonly Dictionary<string, string> k_WeaponDescriptions = new Dictionary<string, string>
        {
            { "Blaster", "기본 연사총. 곧게 날아가는 직선 탄도" },
            { "사인 블래스터", "연사형. 탄이 사인 곡선을 그리며 좌우로 흔들린다" },
            { "지그재그 SMG", "초고속 연사, 약한 탄. 삼각파 모양으로 꺾이며 날아간다" },
            { "삼중 나선총", "한 번에 3발이 120°씩 어긋나 나선을 그리며 회전" },
            { "원뿔 샷건", "근거리 한 방. 조준 원 안에 7발이 원뿔 모양으로 퍼진다" },
            { "감쇠 진동 저격총", "강한 한 발. 처음엔 흔들리다 멀리 갈수록 정확해진다" },
            { "장미 곡선 캐논", "느리고 강한 탄이 장미 꽃잎 모양으로 회전하며 날아간다" },
            { "지수 레일건", "느리게 출발해 지수함수처럼 폭발적으로 가속하는 관통급 탄" },
            { "포물선 런처", "꾹 눌러 충전 후 발사. 중력을 받는 포물선 폭발탄" },
            { "베지에 유도탄", "특수: 4발이 흩어졌다가 베지에 곡선을 그리며 조준한 적에게 휘어 들어간다" },
            { "프랙탈 분열탄", "특수: 6m마다 3갈래로 갈라진다 (1 → 3 → 9발)" },
            { "극한 블랙홀포", "특수: 맞은 자리에 특이점을 만들어 적을 빨아들인 뒤 폭발" },
            { "그래프 연쇄 번개", "특수: 맞힌 적에서 가까운 적으로 번개가 최대 5번 튄다" },
            { "리사주 부메랑", "특수: 8자 곡선을 그리며 갔다가 돌아오고, 닿는 적을 모두 관통" },
            { "홍련 초승달 참격", "특수: 붉은 초승달 검기가 날아가며 닿는 모든 적을 벤다" },
        };

        public static string Describe(WeaponController weapon)
        {
            if (!weapon) return "";
            return k_WeaponDescriptions.TryGetValue(weapon.WeaponName, out string d) ? d : "";
        }

        public static string Formula(WeaponController weapon)
        {
            if (weapon && weapon.ProjectilePrefab is SpecialProjectile special) return special.Formula;
            var trajectory = weapon && weapon.ProjectilePrefab ? weapon.ProjectilePrefab.GetComponent<MathTrajectory>() : null;
            return trajectory ? trajectory.Formula : "y = 0 (직선)";
        }

        // 반지름 안의 모든 피격 가능 대상에게 한 번씩 피해를 준다 (플레이어 자신 제외).
        // damageByDistance: 중심에서의 거리 → 피해량
        public static int DamageInRadius(Vector3 center, float radius, System.Func<float, float> damageByDistance, GameObject source)
        {
            var damaged = new HashSet<Health>();
            foreach (Collider col in Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Collide))
            {
                if (col.GetComponentInParent<PlayerCharacterController>()) continue;
                if (col.GetComponent<MathAnswerTarget>()) continue; // 범위 공격으로 관문 답이 선택되지 않도록
                var damageable = col.GetComponent<Damageable>();
                if (!damageable || !damageable.Health || !damaged.Add(damageable.Health)) continue;

                float distance = Vector3.Distance(center, col.ClosestPoint(center));
                damageable.InflictDamage(damageByDistance(distance), true, source);
            }

            return damaged.Count;
        }

        public static int WeaponLayer(PlayerWeaponsManager weapons)
        {
            int mask = weapons ? weapons.FpsWeaponLayer.value : 0;
            return mask > 0 ? Mathf.RoundToInt(Mathf.Log(mask & -mask, 2)) : 0;
        }

        public static void SetLayerRecursively(GameObject go, int layer)
        {
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = layer;
        }
    }
}
