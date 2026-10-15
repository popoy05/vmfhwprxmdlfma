using System.Collections.Generic;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace Unity.FPS.UI
{
    public class WeaponHUDManager : MonoBehaviour
    {
        [Tooltip("UI panel containing the layoutGroup for displaying weapon ammo")]
        public RectTransform AmmoPanel;

        [Tooltip("Prefab for displaying weapon ammo")]
        public GameObject AmmoCounterPrefab;

        PlayerWeaponsManager m_PlayerWeaponsManager;
        List<AmmoCounter> m_AmmoCounters = new List<AmmoCounter>();

        void Start()
        {
            m_PlayerWeaponsManager = FindAnyObjectByType<PlayerWeaponsManager>();
            DebugUtility.HandleErrorIfNullFindObject<PlayerWeaponsManager, WeaponHUDManager>(m_PlayerWeaponsManager,
                this);

            // 플레이어가 이 HUD보다 먼저 무기를 받았을 수 있으므로, 들고 있는 무기 전부를 칸으로 만든다
            // (원래는 현재 무기 하나만 만들어서 1번 무기만 보이는 문제가 있었다)
            for (int i = 0; i < PlayerWeaponsManager.SlotCount; i++)
            {
                WeaponController weapon = m_PlayerWeaponsManager.GetWeaponAtSlotIndex(i);
                if (weapon) AddWeapon(weapon, i);
            }

            WeaponController activeWeapon = m_PlayerWeaponsManager.GetActiveWeapon();
            if (activeWeapon)
                ChangeWeapon(activeWeapon);

            m_PlayerWeaponsManager.OnAddedWeapon += AddWeapon;
            m_PlayerWeaponsManager.OnRemovedWeapon += RemoveWeapon;
            m_PlayerWeaponsManager.OnSwitchedToWeapon += ChangeWeapon;
        }

        void AddWeapon(WeaponController newWeapon, int weaponIndex)
        {
            // 같은 칸이 두 번 만들어지지 않도록
            foreach (AmmoCounter counter in m_AmmoCounters)
                if (counter && counter.WeaponCounterIndex == weaponIndex)
                    return;

            GameObject ammoCounterInstance = Instantiate(AmmoCounterPrefab, AmmoPanel);
            AmmoCounter newAmmoCounter = ammoCounterInstance.GetComponent<AmmoCounter>();
            DebugUtility.HandleErrorIfNullGetComponent<AmmoCounter, WeaponHUDManager>(newAmmoCounter, this,
                ammoCounterInstance.gameObject);

            newAmmoCounter.Initialize(newWeapon, weaponIndex);

            m_AmmoCounters.Add(newAmmoCounter);
            ChangeWeapon(newWeapon);
        }

        void RemoveWeapon(WeaponController newWeapon, int weaponIndex)
        {
            int foundCounterIndex = -1;
            for (int i = 0; i < m_AmmoCounters.Count; i++)
            {
                if (m_AmmoCounters[i].WeaponCounterIndex == weaponIndex)
                {
                    foundCounterIndex = i;
                    Destroy(m_AmmoCounters[i].gameObject);
                }
            }

            if (foundCounterIndex >= 0)
            {
                m_AmmoCounters.RemoveAt(foundCounterIndex);
                ChangeWeapon(null);
            }
        }

        void ChangeWeapon(WeaponController weapon)
        {
            // 무기가 9개를 넘으면 목록이 지도를 가리지 않도록 작게 줄인다
            m_AmmoCounters.RemoveAll(c => !c);
            float scale = Mathf.Min(1f, 9f / Mathf.Max(1, m_AmmoCounters.Count));
            AmmoPanel.localScale = new Vector3(scale, scale, 1f);
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(AmmoPanel);
        }
    }
}