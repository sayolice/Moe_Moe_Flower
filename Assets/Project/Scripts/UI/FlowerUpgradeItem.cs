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
/// 변화가 없으면 캐시된 값을 그대로 표시한다. 골드/비용이 BigNumber(FloorForCache 참고)라
/// 정수 캐스팅은 못 쓰지만, 정수부만 남기는 것과 동일한 효과를 double 경로로 재현해서
/// 자동 생산으로 매 프레임 미세하게 오르는 소수점 변화는 무시하고, 실제로 살 수 있는 레벨
/// 수가 바뀔 수 있는 순간에만 재계산한다.
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
    private BigNumber cachedGoldFloor = BigNumber.Zero; // 정수부까지만 반영 — FloorForCache 참고
    private int cachedLevel = -1;
    private int cachedBondLevel = -1;
    private LevelUpAmount cachedMode = (LevelUpAmount)(-1);
    private float cachedCostMultiplier = float.NaN; // 장미(전역 레벨업비용 할인)가 뒤늦게 켜지는 경우 대비
    private float cachedGpsMultiplier = float.NaN;  // 해바라기(전역 G/s 보너스)가 뒤늦게 켜지는 경우 대비

    public void Setup(string id, LevelUpAmountSelector selector)
    {
        flowerId = id;
        amountSelector = selector;
        hasCache = false; // 새로 배치된 행이므로 다음 프레임에 강제로 재계산

        // Execute 연결은 holdRepeatButton 하나로만 한다 — actionButton.onClick까지 같이 걸면
        // 짧게 한 번 눌러도 "누르는 순간"(HoldRepeatButton.OnPointerDown)과 "떼는 순간"(Button.onClick)
        // 두 번 발동해서 1회 클릭에 레벨업이 2번 일어나는 버그가 된다. Button은 interactable
        // 상태(회색 처리 등) 표시/판정용으로만 남겨두고, 리스너는 비워둔다.
        if (actionButton != null)
            actionButton.onClick.RemoveAllListeners();

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
            SetPreBloomState(data, instance);
            return;
        }

        LevelUpAmount mode = amountSelector != null ? amountSelector.Current : LevelUpAmount.One;
        BigNumber goldFloor = FloorForCache(GameManager.Instance.totalGold);
        float costMultiplier = PassiveManager.Instance != null
            ? PassiveManager.Instance.GetTotalMultiplier(PassiveEffectType.LevelUpCostDiscountPercent)
            : 1f;
        float gpsMultiplier = PassiveManager.Instance != null
            ? PassiveManager.Instance.GetTotalMultiplier(PassiveEffectType.GoldPerSecondBonusPercent)
            : 1f;

        bool needsRecalculate = !hasCache
            || goldFloor != cachedGoldFloor
            || instance.currentLevel != cachedLevel
            || instance.bondLevel != cachedBondLevel
            || mode != cachedMode
            || !Mathf.Approximately(costMultiplier, cachedCostMultiplier)
            || !Mathf.Approximately(gpsMultiplier, cachedGpsMultiplier);

        if (needsRecalculate)
        {
            hasCache = true;
            cachedGoldFloor = goldFloor;
            cachedLevel = instance.currentLevel;
            cachedBondLevel = instance.bondLevel;
            cachedMode = mode;
            cachedCostMultiplier = costMultiplier;
            cachedGpsMultiplier = gpsMultiplier;

            int levelsToApply = CalculateLevelsForMode(data, instance.currentLevel, mode, GameManager.Instance.totalGold);
            ApplyPreview(data, instance.currentLevel, instance.bondLevel, levelsToApply, mode);
        }

        if (holdRepeatButton != null)
            holdRepeatButton.allowRepeat = (mode != LevelUpAmount.Max);
    }

    /// <summary>
    /// 캐시 비교 전용 — 원래 (long)totalGold로 정수부만 남겨 "레벨업 비용(정수)에 영향 없는 소수점
    /// 미세 변화"를 무시했었다. BigNumber는 정수 캐스팅이 없으므로, double 범위 안에서는 같은 방식
    /// (버림)을 재현하고, 그 범위를 넘는(사실상 도달 불가능한) 값은 그냥 있는 그대로 비교한다 —
    /// 그 경우는 애초에 이 캐시 최적화보다 훨씬 큰 자릿수라 프레임마다 재계산돼도 체감상 문제없다.
    /// </summary>
    private static BigNumber FloorForCache(BigNumber gold)
    {
        double asDouble = gold.ToDouble();
        return double.IsInfinity(asDouble) ? gold : BigNumber.FromDouble(System.Math.Floor(asDouble));
    }

    private int CalculateLevelsForMode(FlowerData data, int currentLevel, LevelUpAmount mode, BigNumber gold)
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

    private void ApplyPreview(FlowerData data, int currentLevel, int bondLevel, int levelsToApply, LevelUpAmount mode)
    {
        BigNumber currentGps = FlowerManager.Instance.GetEffectiveGoldPerSecond(data, currentLevel, bondLevel, flowerId);

        // FormatPrecise인 이유: 저레벨 G/s(예: 민들레 Lv.1 = 1)나 레벨업 1회당 증가분은 1 미만인 게
        // 정상인데, Format()으로 찍으면 정수로 뭉개져서 "0"이 되어 레벨업해도 안 오르는 것처럼 보였다.
        if (levelGpsText != null)
            levelGpsText.text = $"Lv.{currentLevel}   G/s {NumberFormatUtil.FormatPrecise(currentGps)}";

        if (levelsToApply <= 0)
        {
            // 지금 가능한 레벨이 0이어도, 참고용으로 "다음 1레벨" 비용은 계속 보여주고 버튼만 비활성화
            BigNumber nextCost = FlowerManager.Instance.GetEffectiveLevelUpCost(data, currentLevel);
            if (actionText != null)
                actionText.text = $"{NumberFormatUtil.FormatGold(nextCost)} → +0 G/s";
            if (actionButton != null)
                actionButton.interactable = false;
            return;
        }

        BigNumber totalCost = FlowerManager.Instance.GetEffectiveLevelUpCostForLevels(data, currentLevel, levelsToApply);
        BigNumber gpsAfter = FlowerManager.Instance.GetEffectiveGoldPerSecond(data, currentLevel + levelsToApply, bondLevel, flowerId);
        BigNumber gpsDelta = gpsAfter - currentGps;

        string suffix = mode == LevelUpAmount.Max ? $" / Lv.{currentLevel + levelsToApply}" : "";

        if (actionText != null)
            actionText.text = $"{NumberFormatUtil.FormatGold(totalCost)} → +{NumberFormatUtil.FormatPrecise(gpsDelta)} G/s{suffix}";

        if (actionButton != null)
            actionButton.interactable = true;
    }

    /// <summary>
    /// 미개화 꽃의 행 표시: 애정 진행도 + 예상 개화 시간을 함께 보여준다(각 꽃마다 독립 표시).
    /// 이 목록은 화면에 표시 중이 아닌 꽃도 전부 나열하므로, 터치 애정은 반영할 수 없고
    /// FlowerManager.GetEffectiveAutoAffectionRate()(모든 미개화 꽃에 동일하게 적용되는 자동 애정)만 사용한다.
    /// 속도가 TimeFormatUtil.MinDisplayRatePerSecond 이하(자동 애정이 사실상 0)면 나눗셈 자체를
    /// 하지 않고 "예측 불가"라고 명시한다 — Uimanager.BuildBloomEtaSuffix와 동일한 판단 기준.
    /// </summary>
    private void SetPreBloomState(FlowerData data, FlowerInstance instance)
    {
        if (levelGpsText != null)
        {
            // Uimanager와 동일하게 축약 표기 — 후반 꽃의 필요 애정이 수백만~수억이라 그대로는 못 읽는다.
            string current = NumberFormatUtil.Format(instance.currentAffection);
            string required = NumberFormatUtil.Format(data.requiredAffection);
            double rate = FlowerManager.Instance.GetEffectiveAutoAffectionRate();

            string etaSuffix;
            if (rate <= TimeFormatUtil.MinDisplayRatePerSecond)
            {
                etaSuffix = " / 개화까지 예측 불가";
            }
            else
            {
                double remaining = System.Math.Max(0, data.requiredAffection - instance.currentAffection);
                etaSuffix = $" / 개화까지 {TimeFormatUtil.Format(remaining / rate)}";
            }

            levelGpsText.text = $"애정 {current}/{required}{etaSuffix}";
        }

        if (actionText != null) actionText.text = "개화 후 레벨업 가능";
        if (actionButton != null) actionButton.interactable = false;
        hasCache = false; // 개화하는 순간 다음 프레임에 즉시 재계산되도록 캐시 무효화
    }

    private void Execute()
    {
        if (FlowerManager.Instance == null || flowerId == null) return;

        LevelUpAmount mode = amountSelector != null ? amountSelector.Current : LevelUpAmount.One;

        int gained = 0;
        switch (mode)
        {
            case LevelUpAmount.One:
                gained = FlowerManager.Instance.TryLevelUpFlowerBy(flowerId, 1);
                break;
            case LevelUpAmount.Ten:
                gained = FlowerManager.Instance.TryLevelUpFlowerBy(flowerId, 10);
                break;
            case LevelUpAmount.Max:
                gained = FlowerManager.Instance.TryLevelUpFlowerToMax(flowerId);
                break;
        }

        if (gained > 0)
        {
            if (SoundManager.Instance != null)
                SoundManager.Instance.PlayLevelUpSound();
        }

        // 실제 레벨업이 성공하면 골드/레벨이 반드시 바뀌므로 다음 프레임에 자연히 재계산되지만,
        // 안전하게 캐시를 명시적으로 무효화해서 즉시 최신 상태가 반영되도록 한다.
        hasCache = false;
    }
}
