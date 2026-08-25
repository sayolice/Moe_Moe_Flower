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

    /// <summary> GetUpgradeCost의 다중 레벨 합산판 (+10/MAX 미리보기용). </summary>
    public long GetUpgradeCostForLevels(PlayerStatType type, int levels)
    {
        PlayerStatData data = GetData(type);
        if (data == null) return 0;
        return data.GetUpgradeCostForLevels(GetLevel(type), levels);
    }

    /// <summary> 현재 레벨 기준으로 주어진 골드로 몇 레벨까지 강화할 수 있는지 (+10/MAX 미리보기용). </summary>
    public int GetMaxAffordableLevels(PlayerStatType type, double gold)
    {
        PlayerStatData data = GetData(type);
        if (data == null) return 0;
        return data.GetMaxAffordableLevels(GetLevel(type), gold);
    }

    /// <summary>
    /// 골드를 소모해 해당 스탯을 최대 levels번 강화한다(+1/+10 버튼용). 골드가 부족한 지점에서 멈추고
    /// 실제로 오른 레벨 수를 반환한다(요청한 levels보다 적을 수 있음). FlowerManager.LevelUpLoop와 동일 원칙.
    /// </summary>
    public int TryUpgradeBy(PlayerStatType type, int levels)
    {
        PlayerStatData data = GetData(type);
        if (data == null) return 0;
        if (!instances.TryGetValue(type, out PlayerStatInstance instance)) return 0;
        if (GameManager.Instance == null) return 0;

        int gained = 0;
        while (gained < levels)
        {
            long cost = data.GetUpgradeCost(instance.currentLevel);
            if (!GameManager.Instance.TrySpendGold(cost)) break;

            instance.currentLevel++;
            gained++;
        }

        return gained;
    }

    /// <summary> 현재 골드로 가능한 최고 레벨까지 한 번에 강화한다 (MAX 버튼용). </summary>
    public int TryUpgradeToMax(PlayerStatType type) => TryUpgradeBy(type, int.MaxValue);

    /// <summary> 기존 단일 강화 API (하위 호환용). 새 코드는 TryUpgradeBy(type, 1)을 직접 사용해도 된다. </summary>
    public bool TryUpgrade(PlayerStatType type) => TryUpgradeBy(type, 1) > 0;

    /// <summary> 세이브 저장 전용: 현재 모든 스탯의 레벨을 스냅샷으로 반환한다. </summary>
    public List<PlayerStatSaveEntry> GetAllLevelsForSave()
    {
        List<PlayerStatSaveEntry> list = new List<PlayerStatSaveEntry>();
        foreach (var kvp in instances)
        {
            list.Add(new PlayerStatSaveEntry { type = kvp.Key, currentLevel = kvp.Value.currentLevel });
        }
        return list;
    }

    /// <summary>
    /// 세이브 불러오기 전용: 저장된 레벨로 덮어쓴다.
    /// savedStats에 없는 타입(예: 이후 추가된 신규 스탯)은 startingLevel 그대로 유지된다.
    /// </summary>
    public void LoadLevels(List<PlayerStatSaveEntry> savedStats)
    {
        if (savedStats == null) return;

        foreach (PlayerStatSaveEntry entry in savedStats)
        {
            if (instances.TryGetValue(entry.type, out PlayerStatInstance instance))
                instance.currentLevel = entry.currentLevel;
        }
    }
}
