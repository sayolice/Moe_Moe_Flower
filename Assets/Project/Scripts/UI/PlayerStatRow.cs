using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 플레이어 강화 스탯 1개(터치 애정/터치 골드/자동 애정)에 대한 표시/강화 행.
/// 꽃 레벨업(FlowerUpgradeItem)과 완전히 분리된 별도 시스템이며 PlayerStatManager만 참조한다.
///
/// [재계산 최소화] FlowerUpgradeItem과 동일한 원칙: 골드(정수부)나 레벨이 실제로 바뀐
/// 프레임에만 비용/미리보기를 다시 계산하고, 변화가 없으면 캐시를 그대로 표시한다.
/// </summary>
public class PlayerStatRow : MonoBehaviour
{
    [Header("텍스트")]
    public TMP_Text nameText;
    public TMP_Text valueText;
    public TMP_Text actionText; // 버튼 라벨: "50G → 강화" 등

    [Header("버튼")]
    public Button actionButton;
    public HoldRepeatButton holdRepeatButton;

    private PlayerStatType statType;
    private bool isSetUp;

    // ── 재계산 스킵용 캐시 ──────────────────────────────────────────
    private bool hasCache;
    private long cachedGoldFloor = long.MinValue;
    private int cachedLevel = -1;

    public void Setup(PlayerStatType type)
    {
        statType = type;
        isSetUp = true;
        hasCache = false; // 새로 연결됐으니 다음 프레임에 강제로 재계산

        if (actionButton != null)
        {
            actionButton.onClick.RemoveAllListeners();
            actionButton.onClick.AddListener(Execute);
        }

        if (holdRepeatButton != null)
        {
            holdRepeatButton.allowRepeat = true; // 항상 1레벨씩 강화이므로 홀드 반복 허용
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
        long goldFloor = (long)GameManager.Instance.totalGold;

        bool needsRecalculate = !hasCache || goldFloor != cachedGoldFloor || level != cachedLevel;

        if (needsRecalculate)
        {
            hasCache = true;
            cachedGoldFloor = goldFloor;
            cachedLevel = level;

            ApplyPreview(data, level, GameManager.Instance.totalGold);
        }
    }

    private void ApplyPreview(PlayerStatData data, int level, double gold)
    {
        float currentValue = data.GetValue(level);

        if (valueText != null)
            valueText.text = $"Lv.{level}   현재 +{currentValue:0.00}{data.valueSuffix}";

        long cost = data.GetUpgradeCost(level);
        bool affordable = cost <= gold;

        if (actionText != null)
            actionText.text = $"{cost:N0}G → 강화";

        if (actionButton != null)
            actionButton.interactable = affordable;
    }

    private void Execute()
    {
        if (PlayerStatManager.Instance == null) return;

        PlayerStatManager.Instance.TryUpgrade(statType);

        // 강화 성공 시 골드/레벨이 반드시 바뀌므로 다음 프레임에 자연히 재계산되지만,
        // 안전하게 캐시를 명시적으로 무효화해서 즉시 최신 상태가 반영되도록 한다.
        hasCache = false;
    }
}
