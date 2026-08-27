using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 꽃 레벨업 탭의 콘텐츠. 현재 중앙 화면에 표시 중인 꽃과 완전히 무관하게
/// "보유한 모든 꽃"을 나열하고, 각 행(FlowerUpgradeItem)에서 직접 레벨업한다.
/// 스와이프나 메인 화면 전환 없이 오른쪽에서 바로 강화할 수 있게 하는 것이 핵심.
/// </summary>
public class FlowerUpgradePanel : MonoBehaviour
{
    [Header("참조")]
    public Transform content;
    public FlowerUpgradeItem itemPrefab;
    public LevelUpAmountSelector amountSelector;
    public FlowerSortSelector sortSelector;

    private readonly Dictionary<string, FlowerUpgradeItem> items = new Dictionary<string, FlowerUpgradeItem>();

    private void Start()
    {
        if (sortSelector != null)
            sortSelector.OnSortModeChanged += HandleSortModeChanged;

        if (FlowerManager.Instance != null)
        {
            FlowerManager.Instance.OnOwnedFlowersChanged += RebuildList;
            FlowerManager.Instance.OnFlowerBloomed += HandleFlowerBloomed;
        }

        RebuildList();
    }

    private void OnDestroy()
    {
        if (sortSelector != null)
            sortSelector.OnSortModeChanged -= HandleSortModeChanged;

        if (FlowerManager.Instance != null)
        {
            FlowerManager.Instance.OnOwnedFlowersChanged -= RebuildList;
            FlowerManager.Instance.OnFlowerBloomed -= HandleFlowerBloomed;
        }
    }

    private void HandleSortModeChanged(FlowerSortMode _) => Resort();
    private void HandleFlowerBloomed(string _) => RebuildList(); // 개화 시 카드 상태(레벨/비용 등) 갱신을 위해 목록 재구성

    /// <summary> 보유 목록 자체가 바뀌었을 때(구매 등) 전체를 다시 만든다. </summary>
    private void RebuildList()
    {
        if (content == null || itemPrefab == null || FlowerManager.Instance == null)
            return;

        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);
        items.Clear();

        List<string> sortedIds = GetSortedOwnedIds();

        foreach (string id in sortedIds)
        {
            FlowerUpgradeItem item = Instantiate(itemPrefab, content);
            item.Setup(id, amountSelector);
            items[id] = item;
        }
    }

    /// <summary> 목록을 새로 만들지 않고, 정렬 기준에 맞춰 기존 행의 순서(SiblingIndex)만 바꾼다. </summary>
    private void Resort()
    {
        if (items.Count == 0) return;

        List<string> sortedIds = GetSortedOwnedIds();
        for (int i = 0; i < sortedIds.Count; i++)
        {
            if (items.TryGetValue(sortedIds[i], out FlowerUpgradeItem item))
                item.transform.SetSiblingIndex(i);
        }
    }

    /// <summary>
    /// 현재 정렬 모드에 맞춰 "표시 순서"만 계산한다.
    /// FlowerManager.allFlowers / ownedFlowers의 실제 저장 순서는 변경하지 않는다.
    /// </summary>
    private List<string> GetSortedOwnedIds()
    {
        FlowerManager fm = FlowerManager.Instance;
        List<string> ownedInDexOrder = fm.GetOwnedIdsInDexOrder(); // 도감순 = FlowerManager.allFlowers 순서

        FlowerSortMode mode = sortSelector != null ? sortSelector.Current : FlowerSortMode.DexOrder;

        switch (mode)
        {
            case FlowerSortMode.PriceAscending:
                return ownedInDexOrder.OrderBy(id => fm.GetFlowerData(id)?.seedPrice ?? 0).ToList();
            case FlowerSortMode.PriceDescending:
                return ownedInDexOrder.OrderByDescending(id => fm.GetFlowerData(id)?.seedPrice ?? 0).ToList();
            case FlowerSortMode.LevelAscending:
                return ownedInDexOrder.OrderBy(id => fm.GetInstance(id)?.currentLevel ?? 0).ToList();
            case FlowerSortMode.LevelDescending:
                return ownedInDexOrder.OrderByDescending(id => fm.GetInstance(id)?.currentLevel ?? 0).ToList();
            case FlowerSortMode.EfficiencyDescending:
                return ownedInDexOrder.OrderByDescending(id => GetCurrentGoldPerSecond(fm, id)).ToList();
            case FlowerSortMode.CostEfficiencyDescending:
                return ownedInDexOrder.OrderByDescending(id => GetCostEfficiency(fm, id)).ToList();
            default:
                return ownedInDexOrder;
        }
    }

    /// <summary>
    /// "가성비순" 정렬 전용 — "다음 레벨업 1회에 드는 골드 대비, 그걸로 늘어나는 G/s"가 큰 꽃부터.
    /// 효율순(지금 당장의 절대 생산량)과 다르게, "지금 골드를 어디에 써야 회수가 빠른가"를 보여준다.
    /// 미개화 꽃은 레벨업 자체가 불가능하므로 0으로 취급해 목록 맨 뒤로 보낸다.
    /// </summary>
    private static BigNumber GetCostEfficiency(FlowerManager fm, string id)
    {
        FlowerInstance instance = fm.GetInstance(id);
        FlowerData data = fm.GetFlowerData(id);
        if (instance == null || data == null || !instance.isBloomed) return BigNumber.Zero;

        BigNumber cost = fm.GetEffectiveLevelUpCost(data, instance.currentLevel);
        if (cost <= BigNumber.Zero) return BigNumber.Zero;

        BigNumber gpsNow = fm.GetEffectiveGoldPerSecond(data, instance.currentLevel, instance.bondLevel);
        BigNumber gpsAfter = fm.GetEffectiveGoldPerSecond(data, instance.currentLevel + 1, instance.bondLevel);

        return (gpsAfter - gpsNow) / cost;
    }

    /// <summary>
    /// "효율순" 정렬 전용 — 지금 이 순간 실제로 벌어들이는 G/s(유대 배율 포함)를 기준으로 삼는다.
    /// 미개화 꽃(currentLevel==0)은 GetGoldPerSecond(0)이 의미 없는 값(레벨-1 지수가 음수가 됨)을
    /// 낼 수 있으므로, 개화 전에는 0으로 취급해서 자연스럽게 목록 맨 뒤로 보낸다.
    /// </summary>
    private static BigNumber GetCurrentGoldPerSecond(FlowerManager fm, string id)
    {
        FlowerInstance instance = fm.GetInstance(id);
        FlowerData data = fm.GetFlowerData(id);
        if (instance == null || data == null || !instance.isBloomed) return BigNumber.Zero;

        return fm.GetEffectiveGoldPerSecond(data, instance.currentLevel, instance.bondLevel);
    }
}
