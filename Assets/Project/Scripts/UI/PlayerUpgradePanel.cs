using UnityEngine;

/// <summary>
/// 플레이어 전역 강화 탭. 꽃 레벨업(FlowerUpgradePanel/FlowerUpgradeItem)과 완전히 분리된
/// 독립 패널이며 PlayerStatManager만 참조한다 — 꽃 데이터/로직과 절대 섞이지 않는다.
/// 행 3개는 항상 고정(터치 애정/터치 골드/자동 애정)이므로 FlowerUpgradePanel과 달리
/// 동적 목록 생성이 필요 없다.
/// </summary>
public class PlayerUpgradePanel : MonoBehaviour
{
    [Header("스탯 행 3종 (Inspector에서 연결)")]
    public PlayerStatRow touchAffectionRow;
    public PlayerStatRow touchGoldRow;
    public PlayerStatRow autoAffectionRow;

    private void Start()
    {
        if (touchAffectionRow != null) touchAffectionRow.Setup(PlayerStatType.TouchAffection);
        if (touchGoldRow != null) touchGoldRow.Setup(PlayerStatType.TouchGold);
        if (autoAffectionRow != null) autoAffectionRow.Setup(PlayerStatType.AutoAffection);
    }
}
