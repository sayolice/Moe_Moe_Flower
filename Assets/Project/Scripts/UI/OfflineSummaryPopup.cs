using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 오프라인 정산 결과를 요약해서 보여주는 팝업. SaveManager.OnOfflineSettlementApplied를 구독해서
/// 자동으로 뜬다. 개화한 꽃이 있으면 골드보다 위쪽에 더 크게 강조해서 보여준다
/// (개화가 이 게임에서 가장 중요한 사건이므로).
///
/// [중요] 이 컴포넌트가 붙은 GameObject(root)는 절대 SetActive(false)로 끄지 않는다.
/// Unity는 비활성 오브젝트의 Awake/Start를 스킵하므로, 여기서 자기 자신을 끄면 다음 씬 로드 때
/// Start()의 SaveManager 이벤트 구독이 영영 실행되지 않아 팝업이 조용히 죽어버린다.
/// 대신 CanvasGroup(alpha/interactable/blocksRaycasts)으로 보이기/숨기기만 전환한다.
/// </summary>
public class OfflineSummaryPopup : MonoBehaviour
{
    [Header("최소 표시 기준 (너무 짧은 방치엔 팝업을 띄우지 않음 — UX 기본값, 밸런스 아님)")]
    public double minSecondsToShow = 60;

    [Header("루트 (표시/숨김 대상, 절대 SetActive(false)로 끄지 않음)")]
    public GameObject root;

    [Header("텍스트")]
    public TMP_Text elapsedTimeText;
    public TMP_Text goldEarnedText;
    public TMP_Text bloomedFlowersText;

    [Tooltip("정원에서 얻은 유대/시듦 상태 요약. 정원을 안 쓰면(배치가 없으면) 빈 문자열로 남아 사실상 안 보인다.")]
    public TMP_Text gardenSummaryText;

    [Header("개화 강조 섹션 (개화한 꽃이 있을 때만 활성화)")]
    public GameObject bloomedFlowersSection;

    [Header("닫기 버튼")]
    public Button closeButton;

    private CanvasGroup canvasGroup;

    private void Awake()
    {
        if (root == null) root = gameObject;

        canvasGroup = root.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = root.AddComponent<CanvasGroup>();

        SetVisible(false);
    }

    private void Start()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Hide);

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
            elapsedTimeText.text = $"게임을 쉬는 동안 {TimeFormatUtil.Format(result.elapsedSeconds)}이 지났습니다.";

        if (goldEarnedText != null)
            goldEarnedText.text = BuildGoldEarnedText(result.goldEarned);

        if (bloomedFlowersSection != null)
            bloomedFlowersSection.SetActive(hasBloom);

        if (hasBloom && bloomedFlowersText != null)
            bloomedFlowersText.text = BuildBloomedFlowerText(result.newlyBloomedFlowerIds);

        if (gardenSummaryText != null)
            gardenSummaryText.text = BuildGardenSummaryText(result);

        SetVisible(true);
    }

    /// <summary>
    /// 정원에서 얻은 유대량 + 오프라인 중 유대 레벨이 오른 꽃 + 현재 시듦 상태를 요약한다(요청
    /// 명세 5.3). 정원을 아예 안 쓰는 플레이어(GardenManager가 없거나 배치가 0)에게는 빈 문자열을
    /// 반환해서, 정원과 무관한 팝업 모양이 예전과 똑같이 보이게 한다.
    /// </summary>
    private string BuildGardenSummaryText(OfflineSettlementResult result)
    {
        if (GardenManager.Instance == null || GardenManager.Instance.PlacedFlowerCount == 0) return "";

        var lines = new List<string>();

        if (result.gardenBondEarned > 0)
            lines.Add($"정원에서 유대 {NumberFormatUtil.Format(result.gardenBondEarned)} 획득");

        if (result.gardenBondLeveledFlowerIds.Count > 0)
        {
            var uniqueNames = new List<string>();
            foreach (string id in result.gardenBondLeveledFlowerIds)
            {
                FlowerData data = FlowerManager.Instance != null ? FlowerManager.Instance.GetFlowerData(id) : null;
                string name = data != null ? data.displayName : id;
                if (!uniqueNames.Contains(name)) uniqueNames.Add(name);
            }
            lines.Add($"정원에서 유대 레벨업: {string.Join(", ", uniqueNames)}");
        }

        string wiltLabel = GardenManager.Instance.GetCurrentWiltStageLabel();
        float wiltMultiplier = GardenManager.Instance.GetCurrentGlobalWiltMultiplier();
        if (wiltMultiplier < 1f)
            lines.Add($"정원 상태: {wiltLabel} (인접 효과 {(wiltMultiplier * 100f):0}%) — 타일을 터치해서 손질해 주세요.");

        return string.Join("\n", lines);
    }

    /// <summary>
    /// "이전 골드 → 현재 골드 (+획득량)" 형식. 이 이벤트가 발생하는 시점(SaveManager.Load 안,
    /// 아직 Update()가 한 번도 돌지 않은 시점)에는 GameManager.totalGold에 이미 오프라인 수익이
    /// 더해져 있으므로, "이전 골드"는 (현재 골드 - 획득량)으로 역산해도 정확하다.
    /// </summary>
    private string BuildGoldEarnedText(double goldEarned)
    {
        // GameManager.totalGold는 BigNumber지만, 이 팝업은 표시 전용이라 double로 좁혀도 안전하다
        // (goldEarned 자체도 Flowermanager.ApplyOfflineProgress에서 이미 double로 좁혀 온 값).
        double goldAfter = GameManager.Instance != null ? GameManager.Instance.totalGold.ToDouble() : goldEarned;
        double goldBefore = goldAfter - goldEarned;

        return $"{NumberFormatUtil.FormatGold(goldBefore)} → {NumberFormatUtil.FormatGold(goldAfter)} (+{NumberFormatUtil.FormatGold(goldEarned)})";
    }

    private string BuildBloomedFlowerText(List<string> flowerIds)
    {
        var lines = new List<string>();
        foreach (string id in flowerIds)
        {
            FlowerData data = FlowerManager.Instance != null ? FlowerManager.Instance.GetFlowerData(id) : null;
            string name = data != null ? data.displayName : id;
            lines.Add($"{KoreanUtil.WithSubjectParticle(name)} 개화했습니다!");
        }
        return string.Join("\n", lines);
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup == null) return;
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }

    private void Hide()
    {
        SetVisible(false);
    }
}
