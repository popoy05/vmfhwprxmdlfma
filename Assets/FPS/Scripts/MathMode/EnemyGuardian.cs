using System.Collections.Generic;
using System.Linq;
using Unity.FPS.AI;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.AI;

namespace Unity.FPS.MathMode
{
    // 모든 적을 감시한다.
    // 1) 벽에 끼었거나, 플레이어가 걸어서 갈 수 없는 곳(벽 위·바닥 아래·고립된 구역)에 있는 적을 안전한 곳으로 옮긴다.
    //    새로 등록될 때 한 번, 이후 2초마다 다시 검사한다.
    // 2) 남은 적이 3마리 이하가 되면 머리 위에 빨간 기둥을 세워 어디서든 보이게 한다.
    // 3) 모든 적에게 힐 팩 랜덤 드롭을 설정한다.
    public class EnemyGuardian : MonoBehaviour
    {
        const float k_BodyRadius = 0.6f;
        const float k_RecheckInterval = 2f;
        const int k_RevealThreshold = 3;

        MathModeManager m_Manager;
        EnemyManager m_EnemyManager;
        readonly HashSet<EnemyController> m_Checked = new HashSet<EnemyController>();
        readonly Dictionary<EnemyController, GameObject> m_Beacons = new Dictionary<EnemyController, GameObject>();
        readonly Dictionary<EnemyController, int> m_Strikes = new Dictionary<EnemyController, int>();
        float m_NextRecheck;
        bool m_Revealed;

        public void Init(MathModeManager manager)
        {
            m_Manager = manager;
            m_EnemyManager = FindAnyObjectByType<EnemyManager>();
        }

        // 맵이 바뀐 직후처럼 모든 적을 즉시 검사해야 할 때
        public void RelocateInvalidNow()
        {
            if (!m_EnemyManager || m_EnemyManager.Enemies == null) return;
            foreach (EnemyController enemy in m_EnemyManager.Enemies.ToList())
                CheckNew(enemy);
        }

        // 쉬운 모드: 모든 적을 시작 지점 근처(배치 반경 안)의 트인 곳으로 옮긴다
        public void RelocateAllNow()
        {
            if (!m_EnemyManager || m_EnemyManager.Enemies == null) return;
            foreach (EnemyController enemy in m_EnemyManager.Enemies.ToList())
            {
                if (!enemy || !m_Checked.Add(enemy)) continue;
                SetupLoot(enemy);
                m_Manager.HighlightEnemy(enemy);
                Relocate(enemy);
            }
        }

        void Update()
        {
            if (!m_EnemyManager || m_EnemyManager.Enemies == null || !m_Manager.Player) return;

            bool recheck = Time.time >= m_NextRecheck;
            if (recheck) m_NextRecheck = Time.time + k_RecheckInterval;

            foreach (EnemyController enemy in m_EnemyManager.Enemies.ToList())
            {
                if (!enemy) continue;
                if (!m_Checked.Contains(enemy))
                    CheckNew(enemy);
                else if (recheck)
                    Recheck(enemy);
            }

            if (recheck)
            {
                m_Checked.RemoveWhere(e => !e);
                UpdateBeacons();
            }
        }

        void CheckNew(EnemyController enemy)
        {
            if (!enemy || !m_Checked.Add(enemy)) return;
            SetupLoot(enemy);
            m_Manager.HighlightEnemy(enemy);
            if (!IsValidSpot(enemy.transform.position, enemy.transform))
                Relocate(enemy);
        }

        // 두 번 연속으로 이상하면 옮긴다 (순간적으로 밀린 경우는 봐준다)
        void Recheck(EnemyController enemy)
        {
            bool ok = NavMesh.SamplePosition(enemy.transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas) &&
                      m_Manager.World.IsReachable(m_Manager.Player.position, hit.position);
            if (ok)
            {
                m_Strikes.Remove(enemy);
                return;
            }

            m_Strikes.TryGetValue(enemy, out int strikes);
            if (++strikes >= 2)
            {
                m_Strikes.Remove(enemy);
                Relocate(enemy);
            }
            else
            {
                m_Strikes[enemy] = strikes;
            }
        }

        void SetupLoot(EnemyController enemy)
        {
            if (!m_Manager.RewardPrefab) return;
            enemy.LootPrefab = m_Manager.RewardPrefab;
            enemy.DropRate = m_Manager.HealDropChance;
        }

        bool IsValidSpot(Vector3 position, Transform self)
        {
            if (!NavMesh.SamplePosition(position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                return false;
            if (Mathf.Abs(hit.position.y - position.y) > 2.5f)
                return false;
            if (!m_Manager.World.IsReachable(m_Manager.Player.position, hit.position))
                return false;
            return !OverlapsGeometry(hit.position, self);
        }

        // 몸통 부분(바닥에서 0.3m ~ 2.3m)이 벽 같은 충돌체와 겹치는지 검사
        static bool OverlapsGeometry(Vector3 groundPos, Transform self)
        {
            Vector3 bottom = groundPos + Vector3.up * (0.3f + k_BodyRadius);
            Vector3 top = groundPos + Vector3.up * (2.3f - k_BodyRadius);
            foreach (Collider col in Physics.OverlapCapsule(bottom, top, k_BodyRadius, ~0, QueryTriggerInteraction.Ignore))
            {
                if (self && col.transform.IsChildOf(self)) continue;
                if (col.GetComponentInParent<EnemyController>()) continue;
                if (col.GetComponentInParent<PlayerCharacterController>()) continue;
                return true;
            }

            return false;
        }

        void Relocate(EnemyController enemy)
        {
            var others = m_EnemyManager.Enemies.Where(e => e && e != enemy).Select(e => e.transform.position).ToList();
            Vector3 target = default;
            bool found = false;
            for (int attempt = 0; attempt < 40 && !found; attempt++)
            {
                float spacing = m_Manager.CurrentDifficulty != Difficulty.Hard ? 8f : 4f; // 쉬움·보통: 한 번에 여러 마리가 몰려오지 않게 떨어뜨린다
                if (!m_Manager.World.TryFindSpot(m_Manager.Player.position, others, 12f, spacing, 1.5f, out target))
                    break;
                found = !OverlapsGeometry(target, enemy.transform);
            }

            if (!found)
            {
                Debug.LogWarning($"[MathMode] {enemy.name}를 옮길 안전한 위치를 찾지 못했습니다.");
                return;
            }

            var agent = enemy.GetComponent<NavMeshAgent>();
            if (agent && agent.enabled)
                agent.Warp(target);
            else
                enemy.transform.position = target;

            enemy.PatrolPath = null; // 원래 순찰 경로가 닿을 수 없는 곳일 수 있으므로 제자리 경계로 바꾼다
            Debug.Log($"[MathMode] 벽에 끼었거나 닿을 수 없는 위치의 적 '{enemy.name}'을(를) {target}로 옮겼습니다.");
        }

        void UpdateBeacons()
        {
            int remaining = m_EnemyManager.Enemies.Count(e => e);
            if (remaining > k_RevealThreshold || remaining == 0) return;

            if (!m_Revealed)
            {
                m_Revealed = true;
                MathModeManager.ShowMessage($"남은 적 {remaining}마리의 위치가 빨간 기둥으로 표시됩니다");
            }

            foreach (EnemyController enemy in m_EnemyManager.Enemies)
            {
                if (!enemy || m_Beacons.ContainsKey(enemy)) continue;
                var beacon = m_Manager.CreateBlock(enemy.transform, PrimitiveType.Cylinder, Vector3.zero,
                    new Vector3(0.25f, 15f, 0.25f), new Color(1f, 0.15f, 0.1f));
                beacon.transform.position = enemy.transform.position + Vector3.up * 17f;
                beacon.transform.localScale = new Vector3(0.25f, 15f, 0.25f);
                m_Beacons[enemy] = beacon;
            }
        }
    }
}
