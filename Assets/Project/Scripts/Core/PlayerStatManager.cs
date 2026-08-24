using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 플레이어 전역 강화 스탯(터치 애정 / 터치 골드 / 자동 애정 등)을 관리하는 싱글턴.
/// FlowerData/FlowerInstance/FlowerManager와 동일한 패턴이지만, 꽃 시스템과는
/// 완전히 독립된 별도 시스템이다 — 꽃 레벨업 로직과 이 클래스는 서로를 모른다.
/// (반대로 FlowerManager가 이 클래스를 참조해서 터치/자동 애정 값을 읽어간다.)
/// </summary>
public class PlayerStatManager : MonoBehaviour
{
    public static PlayerStatManager Instance { get; private set; }

    [Serializable]
    public class StatEntry
    {
        public PlayerStatType type;
        public PlayerStatData data;
    }

    [Header("스탯 목록 (타입-데이터 쌍, Inspector에서 3종 연결)")]
    public List<StatEntry> statEntries = new List<StatEntry>();

    private readonly Dictionary<PlayerStatType, PlayerStatInstance> instances = new Dictionary<PlayerStatType, PlayerStatInstance>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        foreach (StatEntry entry in statEntries)
        {
            if (entry.data == null) continue;
            instances[entry.type] = new PlayerStatInstance(entry.data.startingLevel);
        }
    }

    public PlayerStatData GetData(PlayerStatType type) =>
        statEntries.FirstOrDefault(e => e.type == type)?.data;

    public int GetLevel(PlayerStatType type) =>
        instances.TryGetValue(type, out PlayerStatInstance inst) ? inst.currentLevel : 0;

    /// <summary> 해당 스탯의 현재 실제 값 (예: 클릭 1회당 애정량, 초당 자동 애정량). </summary>
    public float GetCurrentValue(PlayerStatType type)
    {
        PlayerStatData data = GetData(type);
        if (data == null) return 0f;
        return data.GetValue(GetLevel(type));
    }

    public long GetUpgradeCost(PlayerStatType type)
    {
        PlayerStatData data = GetData(type);
        if (data == null) return 0;
        return data.GetUpgradeCost(GetLevel(type));
    }

    /// <summary> 골드를 소모해 해당 스탯을 1레벨 강화한다. 골드가 부족하면 실패한다. </summary>
    public bool TryUpgrade(PlayerStatType type)
    {
        PlayerStatData data = GetData(type);
        if (data == null) return false;
        if (!instances.TryGetValue(type, out PlayerStatInstance instance)) return false;
        if (GameManager.Instance == null) return false;

        long cost = data.GetUpgradeCost(instance.currentLevel);
        if (!GameManager.Instance.TrySpendGold(cost)) return false;

        instance.currentLevel++;
        return true;
    }
}
