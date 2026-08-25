using UnityEngine;

/// <summary>
/// 플레이어 전역 강화 탭. 꽃 레벨업(FlowerUpgradePanel/FlowerUpgradeItem)과 완전히 분리된
/// 독립 패널이며 PlayerStatManager만 참조한다 — 꽃 데이터/로직과 절대 섞이지 않는다.
/// 행 3개는 항상 고정(터치 애정/터치 골드/자동 애정)이므로 FlowerUpgradePanel과 달리
/// 동적 목록 생성이 필요 없다.
///
/// amountSelector(+1/+10/MAX)는 꽃 탭의 것과 같은 컴포넌트 타입이지만 별도 인스턴스다 —
/// "완전히 분리된 독립 패널"이라는 설계 원칙대로, 두 탭은 서로의 강화 단위 상태를 공유하지 않는다.
/// </summary>
public class PlayerUpgradePanel : MonoBehaviour
{
    [Header("스탯 행 3종 (Inspector에서 연결)")]
    public PlayerStatRow touchAffectionRow;
    public PlayerStatRow touchGoldRow;
    public PlayerStatRow autoAffectionRow;

    [Header("레벨업 단위 선택 (+1/+10/MAX)")]
    public LevelUpAmountSelector amountSelector;

    private void Start()
    {
        if (touchAffectionRow != null) touchAffectionRow.Setup(PlayerStatType.TouchAffection, amountSelector);
        if (touchGoldRow != null) touchGoldRow.Setup(PlayerStatType.TouchGold, amountSelector);
        if (autoAffectionRow != null) autoAffectionRow.Setup(PlayerStatType.AutoAffection, amountSelector);
    }
}
