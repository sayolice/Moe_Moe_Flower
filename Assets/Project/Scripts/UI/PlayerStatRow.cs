using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 플레이어 강화 스탯 1개(터치 애정/터치 골드/자동 애정)에 대한 표시/강화 행.
/// 꽃 레벨업(FlowerUpgradeItem)과 완전히 분리된 별도 시스템이며 PlayerStatManager만 참조하지만,
/// +1/+10/MAX 단위 선택과 "비용 → 증가량" 미리보기 구조는 FlowerUpgradeItem과 동일하게 맞춘다.
///
/// [재계산 최소화] FlowerUpgradeItem과 동일한 원칙: 골드(정수부)/레벨/단위가 실제로 바뀐
/// 프레임에만 비용·증가량을 다시 계산하고, 변화가 없으면 캐시를 그대로 표시한다.
/// </summary>
public class PlayerStatRow : MonoBehaviour
{
    [Header("텍스트")]
    public TMP_Text nameText;
    public TMP_Text valueText;
    public TMP_Text actionText; // 버튼 라벨: "50G → +0.35/s" 등

    [Header("버튼")]
    public Button actionButton;
    public HoldRepeatButton holdRepeatButton;

    private PlayerStatType statType;
    private LevelUpAmountSelector amountSelector;
    private bool isSetUp;

    // ── 재계산 스킵용 캐시 ──────────────────────────────────────────
    private bool hasCache;
    private long cachedGoldFloor = long.MinValue;
    private int cachedLevel = -1;
    private LevelUpAmount cachedMode = (LevelUpAmount)(-1);

    public void Setup(PlayerStatType type, LevelUpAmountSelector selector)
    {
        statType = type;
        amountSelector = selector;
        isSetUp = true;
        hasCache = false; // 새로 연결됐으니 다음 프레임에 강제로 재계산

        if (actionButton != null)
        {
            actionButton.onClick.RemoveAllListeners();
            actionButton.onClick.AddListener(Execute);
        }

        if (holdRepeatButton != null)
        {
            holdRepeatButton.OnTrigger -= Execute;
            holdRepeatButton.OnTrigger += Execute;
        }
    }

    private void Update()
    {
        if (!isSetUp || PlayerStatManager.Instance == null || GameManager.Instance == null)
            return;

        PlayerStatData data = PlayerStatManager.Instance.GetData(statType);
        if (data == null) return;

        if (nameText != null)
            nameText.text = data.displayName;

        int level = PlayerStatManager.Instance.GetLevel(statType);
        LevelUpAmount mode = amountSelector != null ? amountSelector.Current : LevelUpAmount.One;
        long goldFloor = (long)GameManager.Instance.totalGold;

        bool needsRecalculate = !hasCache
            || goldFloor != cachedGoldFloor
            || level != cachedLevel
            || mode != cachedMode;

        if (needsRecalculate)
        {
            hasCache = true;
            cachedGoldFloor = goldFloor;
            cachedLevel = level;
            cachedMode = mode;

            int levelsToApply = CalculateLevelsForMode(level, mode, GameManager.Instance.totalGold);
            ApplyPreview(data, level, levelsToApply, mode);
        }

        if (holdRepeatButton != null)
            holdRepeatButton.allowRepeat = (mode != LevelUpAmount.Max);
    }

    private int CalculateLevelsForMode(int currentLevel, LevelUpAmount mode, double gold)
    {
        switch (mode)
        {
            case LevelUpAmount.One:
                return PlayerStatManager.Instance.GetUpgradeCost(statType) <= gold ? 1 : 0;
            case LevelUpAmount.Ten:
                return Mathf.Min(10, PlayerStatManager.Instance.GetMaxAffordableLevels(statType, gold));
            case LevelUpAmount.Max:
                return PlayerStatManager.Instance.GetMaxAffordableLevels(statType, gold);
            default:
                return 0;
        }
    }

    private void ApplyPreview(PlayerStatData data, int currentLevel, int levelsToApply, LevelUpAmount mode)
    {
        float currentValue = data.GetValue(currentLevel);

        if (valueText != null)
            valueText.text = $"Lv.{currentLevel}   현재 +{currentValue:0.00}{data.valueSuffix}";

        if (levelsToApply <= 0)
        {
            // 지금 가능한 레벨이 0이어도, 참고용으로 "다음 1레벨" 비용은 계속 보여주고 버튼만 비활성화
            long nextCost = data.GetUpgradeCost(currentLevel);
            if (actionText != null)
                actionText.text = $"{nextCost:N0}G → +0.00{data.valueSuffix}";
            if (actionButton != null)
                actionButton.interactable = false;
            return;
        }

        long totalCost = PlayerStatManager.Instance.GetUpgradeCostForLevels(statType, levelsToApply);
        float valueAfter = data.GetValue(currentLevel + levelsToApply);
        float valueDelta = valueAfter - currentValue;

        string suffix = mode == LevelUpAmount.Max ? $" / Lv.{currentLevel + levelsToApply}" : "";

        if (actionText != null)
            actionText.text = $"{totalCost:N0}G → +{valueDelta:0.00}{data.valueSuffix}{suffix}";

        if (actionButton != null)
            actionButton.interactable = true;
    }

    private void Execute()
    {
        if (PlayerStatManager.Instance == null) return;

        LevelUpAmount mode = amountSelector != null ? amountSelector.Current : LevelUpAmount.One;

        switch (mode)
        {
            case LevelUpAmount.One:
                PlayerStatManager.Instance.TryUpgradeBy(statType, 1);
                break;
            case LevelUpAmount.Ten:
                PlayerStatManager.Instance.TryUpgradeBy(statType, 10);
                break;
            case LevelUpAmount.Max:
                PlayerStatManager.Instance.TryUpgradeToMax(statType);
                break;
        }

        // 실제 강화가 성공하면 골드/레벨이 반드시 바뀌므로 다음 프레임에 자연히 재계산되지만,
        // 안전하게 캐시를 명시적으로 무효화해서 즉시 최신 상태가 반영되도록 한다.
        hasCache = false;
    }
}
