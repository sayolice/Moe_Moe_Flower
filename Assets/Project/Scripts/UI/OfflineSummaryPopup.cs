using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 오프라인 정산 결과를 요약해서 보여주는 팝업. SaveManager.OnOfflineSettlementApplied를 구독해서
/// 자동으로 뜬다. 개화한 꽃이 있으면 골드보다 위쪽에 더 크게 강조해서 보여준다
/// (개화가 이 게임에서 가장 중요한 사건이므로).
/// </summary>
public class OfflineSummaryPopup : MonoBehaviour
{
    [Header("최소 표시 기준 (너무 짧은 방치엔 팝업을 띄우지 않음 — UX 기본값, 밸런스 아님)")]
    public double minSecondsToShow = 60;

    [Header("루트 (Show/Hide 대상)")]
    public GameObject root;

    [Header("텍스트")]
    public TMP_Text elapsedTimeText;
    public TMP_Text goldEarnedText;
    public TMP_Text bloomedFlowersText;

    [Header("개화 강조 섹션 (개화한 꽃이 있을 때만 활성화)")]
    public GameObject bloomedFlowersSection;

    [Header("닫기 버튼")]
    public Button closeButton;

    private void Awake()
    {
        if (root != null) root.SetActive(false);
        if (closeButton != null) closeButton.onClick.AddListener(Hide);
    }

    private void Start()
    {
        if (SaveManager.Instance != null)
            SaveManager.Instance.OnOfflineSettlementApplied += HandleSettlement;
    }

    private void OnDestroy()
    {
        if (SaveManager.Instance != null)
            SaveManager.Instance.OnOfflineSettlementApplied -= HandleSettlement;
    }

    private void HandleSettlement(OfflineSettlementResult result)
    {
        if (result == null) return;

        bool hasBloom = result.newlyBloomedFlowerIds.Count > 0;
        bool worthShowing = hasBloom || result.goldEarned > 0 || result.elapsedSeconds >= minSecondsToShow;
        if (!worthShowing) return;

        if (elapsedTimeText != null)
            elapsedTimeText.text = $"{FormatDuration(result.elapsedSeconds)} 동안 자리를 비웠어요";

        if (goldEarnedText != null)
            goldEarnedText.text = $"+{Mathf.FloorToInt((float)result.goldEarned):N0} G";

        if (bloomedFlowersSection != null)
            bloomedFlowersSection.SetActive(hasBloom);

        if (hasBloom && bloomedFlowersText != null)
            bloomedFlowersText.text = BuildBloomedFlowerText(result.newlyBloomedFlowerIds);

        if (root != null) root.SetActive(true);
    }

    private string BuildBloomedFlowerText(List<string> flowerIds)
    {
        var names = new List<string>();
        foreach (string id in flowerIds)
        {
            FlowerData data = FlowerManager.Instance != null ? FlowerManager.Instance.GetFlowerData(id) : null;
            names.Add(data != null ? data.displayName : id);
        }
        return "🌸 " + string.Join(", ", names) + " 개화!";
    }

    private string FormatDuration(double totalSeconds)
    {
        int seconds = Mathf.FloorToInt((float)totalSeconds);
        int hours = seconds / 3600;
        int minutes = (seconds % 3600) / 60;

        if (hours > 0) return $"{hours}시간 {minutes}분";
        if (minutes > 0) return $"{minutes}분";
        return $"{seconds}초";
    }

    private void Hide()
    {
        if (root != null) root.SetActive(false);
    }
}
