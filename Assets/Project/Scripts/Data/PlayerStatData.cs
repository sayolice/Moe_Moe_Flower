using UnityEngine;

/// <summary>
/// 플레이어 전역 강화 스탯 하나(터치 애정 / 터치 골드 / 자동 애정 등)의 밸런스 데이터.
/// FlowerData와 완전히 동일한 설계 원칙: 값은 코드가 아니라 인스펙터에서 채워 넣는다.
/// 꽃 데이터/로직과는 별개의 시스템이다.
/// </summary>
[CreateAssetMenu(fileName = "NewPlayerStatData", menuName = "FlowerGirl/Player Stat Data")]
public class PlayerStatData : ScriptableObject
{
    [Header("기본 정보")]
    public string statId;      // 예: "touchAffection" (저장 데이터 키로 사용 예정)
    public string displayName; // 예: "터치 애정"
    public string valueSuffix; // 표시용 단위. 예: "/s"(자동 애정), ""(터치 애정/골드)

    [Header("시작 레벨")]
    [Tooltip("터치 애정/터치 골드처럼 처음부터 작동해야 하면 1, 자동 애정처럼 강화 전엔 꺼져 있어야 하면 0")]
    public int startingLevel = 1;

    [Header("값 (Lv.1 기준)")]
    public float baseValue;
    public float valueGrowthRate = 1.15f;

    [Header("강화 비용 (시작 레벨에서의 첫 강화 비용 기준)")]
    public long upgradeBaseCost;
    public float upgradeCostGrowthRate = 1.20f;

    // [BigNumber인 이유] FlowerData의 동일 계산 함수들과 같은 이유 — 레벨엔 상한이 없고
    // valueGrowthRate/upgradeCostGrowthRate가 매 레벨 곱해지는 지수 함수라, 결과값은 오래
    // 플레이할수록 결국 long/float의 한계에도 도달한다.

    /// <summary> 특정 레벨에서의 실제 값. Lv.0 이하는 아직 강화 전이므로 0을 반환한다. </summary>
    public BigNumber GetValue(int level)
    {
        if (level <= 0) return BigNumber.Zero;
        BigNumber growthFactor = BigNumber.PowDouble(valueGrowthRate, level - 1);
        return baseValue * growthFactor;
    }

    /// <summary>
    /// currentLevel에서 다음 레벨로 강화하는 데 드는 비용.
    /// currentLevel == startingLevel일 때 upgradeBaseCost가 그대로 나오도록,
    /// 지수 계산 기준을 startingLevel로 잡는다 (자동 애정처럼 시작 레벨이 0인 경우에도 동일한 의미 유지).
    /// </summary>
    public BigNumber GetUpgradeCost(int currentLevel)
    {
        BigNumber growthFactor = BigNumber.PowDouble(upgradeCostGrowthRate, currentLevel - startingLevel);
        return upgradeBaseCost * growthFactor;
    }

    /// <summary>
    /// fromLevel에서 시작해 levels번 연속 강화할 때 드는 총 비용 (미리보기 전용, 상태 변경 없음).
    /// FlowerData.GetLevelUpCostForLevels와 동일 원칙 — +10/MAX 버튼의 "실제 총 비용" 표시에 사용.
    /// </summary>
    public BigNumber GetUpgradeCostForLevels(int fromLevel, int levels)
    {
        if (levels <= 0) return BigNumber.Zero;

        BigNumber total = BigNumber.Zero;
        for (int i = 0; i < levels; i++)
        {
            total += GetUpgradeCost(fromLevel + i);
        }
        return total;
    }

    /// <summary>
    /// fromLevel에서 시작해 주어진 골드로 몇 레벨까지 강화할 수 있는지 계산 (미리보기 전용, 상태 변경 없음).
    /// FlowerData.GetMaxAffordableLevels와 동일 원칙 — +10(상한 계산)/MAX 버튼 표시에 사용.
    /// </summary>
    public int GetMaxAffordableLevels(int fromLevel, BigNumber gold)
    {
        const int SAFETY_CAP = 100000;

        int levels = 0;
        BigNumber remaining = gold;

        while (levels < SAFETY_CAP)
        {
            BigNumber cost = GetUpgradeCost(fromLevel + levels);
            if (cost > remaining) break;

            remaining -= cost;
            levels++;
        }

        return levels;
    }
}

public enum PlayerStatType
{
    TouchAffection,
    TouchGold,
    AutoAffection
}
