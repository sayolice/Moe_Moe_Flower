using System.Collections.Generic;

/// <summary>
/// 오프라인 정산 결과 요약 (일회성 런타임 데이터, 저장되지 않음).
/// FlowerManager.ApplyOfflineProgress()가 만들고, SaveManager가 OnOfflineSettlementApplied로
/// 흘려보내면 UI(OfflineSummaryPopup)가 구독해서 화면에 보여준다.
/// </summary>
public class OfflineSettlementResult
{
    public double elapsedSeconds;
    public double goldEarned;
    public List<string> newlyBloomedFlowerIds = new List<string>();
}
