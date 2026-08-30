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

    /// <summary> 정원에서 오프라인 중 획득한 유대 총량(배치된 꽃 전체 합, 표시 전용). </summary>
    public double gardenBondEarned;
    /// <summary> 오프라인 중 정원에서 유대 레벨이 오른 꽃 id 목록(중복 가능 — 여러 번 올랐어도 그때마다 추가됨). </summary>
    public List<string> gardenBondLeveledFlowerIds = new List<string>();
}
