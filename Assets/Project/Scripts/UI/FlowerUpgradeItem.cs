using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 꽃 1개에 대한 레벨업 행. FlowerUpgradePanel이 Setup()으로 flowerId와
/// 공용 LevelUpAmountSelector를 주입한다. 자기 flowerId만 알면 동작하므로
/// 현재 중앙 화면에 어떤 꽃이 떠 있는지와 완전히 무관하게 레벨업할 수 있다.
///
/// [재계산 최소화] 미리보기(비용/증가 G/s) 계산은 매 프레임 무조건 다시 하지 않는다.
/// "골드(정수부) / 현재 레벨 / 레벨업 단위"가 실제로 바뀐 프레임에만 재계산하고,
/// 변화가 없으면 캐시된 값을 그대로 표시한다. 레벨업 비용은 전부 정수(long)이므로
/// 골드를 정수부로 floor해서 비교하면 자동 생산으로 매 프레임 미세하게 오르는 소수점
/// 변화는 무시되고, 실제로 살 수 있는 레벨 수가 바뀔 수 있는 순간에만 재계산된다.
/// 특히 MAX 모드의 GetMaxAffordableLevels 루프가 꽃 수가 늘어나도 매 프레임 반복 실행되지 않도록 하는 것이 핵심.
/// </summary>
public class FlowerUpgradeItem : MonoBehaviour
{
    [Header("텍스트")]
    public TMP_Text flowerNameText;
    public TMP_Text levelGpsText;
    public TMP_Text actionText; // 버튼 라벨: "29G → +0.35 G/s" 등

    [Header("버튼")]
    public Button actionButton;
    public HoldRepeatButton holdRepeatButton;

    private string flowerId;
    private LevelUpAmountSelector amountSelector;

    // ── 재계산 스킵용 캐시 ──────────────────────────────────────────
    private bool hasCache;
    private long cachedGoldFloor = long.MinValue;
    private int cachedLevel = -1;
    private LevelUpAmount cachedMode = (LevelUpAmount)(-1);
    private float cachedCostMultiplier = float.NaN; // 장미(전역 레벨업비용 할인)가 뒤늦게 켜지는 경우 대비
    private float cachedGpsMultiplier = float.NaN;  // 해바라기(전역 G/s 보너스)가 뒤늦게 켜지는 경우 대비

    public void Setup(string id, LevelUpAmountSelector selector)
    {
        flowerId = id;
        amountSelector = selector;
        hasCache = false; // 새로 배치된 행이므로 다음 프레임에 강제로 재계산

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
        if (FlowerManager.Instance == null || GameManager.Instance == null || flowerId == null)
            return;

        FlowerData data = FlowerManager.Instance.GetFlowerData(flowerId);
        FlowerInstance instance = FlowerManager.Instance.GetInstance(flowerId);
        if (data == null || instance == null) return;

        if (flowerNameText != null)
            flowerNameText.text = data.displayName;

        if (!instance.isBloomed)
        {
            SetPreBloomState();
            return;
        }

        LevelUpAmount mode = amountSelector != null ? amountSelector.Current : LevelUpAmount.One;
        long goldFloor = (long)GameManager.Instance.totalGold;
        float costMultiplier = PassiveManager.Instance != null
            ? PassiveManager.Instance.GetTotalMultiplier(PassiveEffectType.LevelUpCostDiscountPercent)
            : 1f;
        float gpsMultiplier = PassiveManager.Instance != null
            ? PassiveManager.Instance.GetTotalMultiplier(PassiveEffectType.GoldPerSecondBonusPercent)
            : 1f;

        bool needsRecalculate = !hasCache
            || goldFloor != cachedGoldFloor
            || instance.currentLevel != cachedLevel
            || mode != cachedMode
            || !Mathf.Approximately(costMultiplier, cachedCostMultiplier)
            || !Mathf.Approximately(gpsMultiplier, cachedGpsMultiplier);

        if (needsRecalculate)
        {
            hasCache = true;
            cachedGoldFloor = goldFloor;
            cachedLevel = instance.currentLevel;
            cachedMode = mode;
            cachedCostMultiplier = costMultiplier;
            cachedGpsMultiplier = gpsMultiplier;

            int levelsToApply = CalculateLevelsForMode(data, instance.currentLevel, mode, GameManager.Instance.totalGold);
            ApplyPreview(data, instance.currentLevel, levelsToApply, mode);
        }

        if (holdRepeatButton != null)
            holdRepeatButton.allowRepeat = (mode != LevelUpAmount.Max);
    }

    private int CalculateLevelsForMode(FlowerData data, int currentLevel, LevelUpAmount mode, double gold)
    {
        switch (mode)
        {
            case LevelUpAmount.One:
                return FlowerManager.Instance.GetEffectiveLevelUpCost(data, currentLevel) <= gold ? 1 : 0;
            case LevelUpAmount.Ten:
                return Mathf.Min(10, FlowerManager.Instance.GetEffectiveMaxAffordableLevels(data, currentLevel, gold));
            case LevelUpAmount.Max:
                return FlowerManager.Instance.GetEffectiveMaxAffordableLevels(data, currentLevel, gold);
            default:
                return 0;
        }
    }

    private void ApplyPreview(FlowerData data, int currentLevel, int levelsToApply, LevelUpAmount mode)
    {
        float currentGps = FlowerManager.Instance.GetEffectiveGoldPerSecond(data, currentLevel);

        if (levelGpsText != null)
            levelGpsText.text = $"Lv.{currentLevel}   G/s {currentGps:0.00}";

        if (levelsToApply <= 0)
        {
            // 지금 가능한 레벨이 0이어도, 참고용으로 "다음 1레벨" 비용은 계속 보여주고 버튼만 비활성화
            long nextCost = FlowerManager.Instance.GetEffectiveLevelUpCost(data, currentLevel);
            if (actionText != null)
                actionText.text = $"{nextCost:N0}G → +0.00 G/s";
            if (actionButton != null)
                actionButton.interactable = false;
            return;
        }

        long totalCost = FlowerManager.Instance.GetEffectiveLevelUpCostForLevels(data, currentLevel, levelsToApply);
        float gpsAfter = FlowerManager.Instance.GetEffectiveGoldPerSecond(data, currentLevel + levelsToApply);
        float gpsDelta = gpsAfter - currentGps;

        string suffix = mode == LevelUpAmount.Max ? $" / Lv.{currentLevel + levelsToApply}" : "";

        if (actionText != null)
            actionText.text = $"{totalCost:N0}G → +{gpsDelta:0.00} G/s{suffix}";

        if (actionButton != null)
            actionButton.interactable = true;
    }

    private void SetPreBloomState()
    {
        if (levelGpsText != null) levelGpsText.text = "성장 중";
        if (actionText != null) actionText.text = "개화 후 레벨업 가능";
        if (actionButton != null) actionButton.interactable = false;
        hasCache = false; // 개화하는 순간 다음 프레임에 즉시 재계산되도록 캐시 무효화
    }

    private void Execute()
    {
        if (FlowerManager.Instance == null || flowerId == null) return;

        LevelUpAmount mode = amountSelector != null ? amountSelector.Current : LevelUpAmount.One;

        switch (mode)
        {
            case LevelUpAmount.One:
                FlowerManager.Instance.TryLevelUpFlowerBy(flowerId, 1);
                break;
            case LevelUpAmount.Ten:
                FlowerManager.Instance.TryLevelUpFlowerBy(flowerId, 10);
                break;
            case LevelUpAmount.Max:
                FlowerManager.Instance.TryLevelUpFlowerToMax(flowerId);
                break;
        }

        // 실제 레벨업이 성공하면 골드/레벨이 반드시 바뀌므로 다음 프레임에 자연히 재계산되지만,
        // 안전하게 캐시를 명시적으로 무효화해서 즉시 최신 상태가 반영되도록 한다.
        hasCache = false;
    }
}
