using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum FlowerSortMode
{
    DexOrder,
    PriceAscending,
    PriceDescending,
    LevelAscending,
    LevelDescending,
    EfficiencyDescending,      // 효율순 — 지금 초당 골드 생산량이 가장 큰 꽃부터
    CostEfficiencyDescending   // 가성비순 — 다음 레벨업 비용 대비 G/s 증가분이 가장 큰 꽃부터
}

/// <summary>
/// 꽃 레벨업 목록의 "표시 순서"만 결정하는 정렬 선택 UI.
///
/// [순환 버튼 → 목록형으로 바꾼 이유] 모드가 5개일 때는 버튼 하나로 순환시켜도 괜찮았지만,
/// 6번째(효율순)에 이어 7번째(가성비순)까지 늘면서 원하는 모드로 가려면 최대 6번을 눌러야 했다.
/// 그래서 메인 버튼을 누르면 전체 모드가 목록으로 펼쳐지고, 원하는 항목을 바로 클릭해서 고르는
/// 방식으로 바꿨다 — PCLayoutBuilder.CreateSortSelector가 이 구조(토글 버튼 + 펼침 목록)를 만든다.
///
/// FlowerManager가 들고 있는 실제 보유 꽃 데이터 순서는 절대 변경하지 않는다
/// (FlowerUpgradePanel이 이 값을 읽어 표시용 리스트만 다시 정렬한다).
/// </summary>
public class FlowerSortSelector : MonoBehaviour
{
    [Header("메인 버튼 (누르면 목록이 펼쳐짐/닫힘)")]
    public Button toggleButton;
    public TMP_Text label;

    [Header("펼침 목록 (기본 비활성)")]
    public GameObject optionListRoot;

    [Tooltip("Order 배열과 1:1 순서로 대응 — PCLayoutBuilder가 같은 순서로 버튼을 만들어 연결한다.")]
    public Button[] optionButtons;

    public FlowerSortMode Current { get; private set; } = FlowerSortMode.DexOrder;

    public event Action<FlowerSortMode> OnSortModeChanged;

    /// <summary> 목록에 표시되는 순서. PCLayoutBuilder의 SortModeLabels 배열과 인덱스가 반드시 일치해야 한다. </summary>
    private static readonly FlowerSortMode[] Order =
    {
        FlowerSortMode.DexOrder,
        FlowerSortMode.PriceAscending,
        FlowerSortMode.PriceDescending,
        FlowerSortMode.LevelAscending,
        FlowerSortMode.LevelDescending,
        FlowerSortMode.EfficiencyDescending,
        FlowerSortMode.CostEfficiencyDescending
    };

    private void Start()
    {
        if (toggleButton != null)
            toggleButton.onClick.AddListener(ToggleList);

        if (optionButtons != null)
        {
            for (int i = 0; i < optionButtons.Length && i < Order.Length; i++)
            {
                if (optionButtons[i] == null) continue;
                FlowerSortMode mode = Order[i]; // 클로저 캡처용 로컬 복사
                optionButtons[i].onClick.AddListener(() => SelectMode(mode));
            }
        }

        if (optionListRoot != null) optionListRoot.SetActive(false);
        UpdateLabel();
    }

    private void ToggleList()
    {
        if (optionListRoot == null) return;
        optionListRoot.SetActive(!optionListRoot.activeSelf);
    }

    private void SelectMode(FlowerSortMode mode)
    {
        Current = mode;
        if (optionListRoot != null) optionListRoot.SetActive(false);
        UpdateLabel();
        OnSortModeChanged?.Invoke(Current);
    }

    private void UpdateLabel()
    {
        if (label != null) label.text = "정렬: " + GetModeName(Current);
    }

    private static string GetModeName(FlowerSortMode mode) => mode switch
    {
        FlowerSortMode.DexOrder => "도감순",
        FlowerSortMode.PriceAscending => "가격↑",
        FlowerSortMode.PriceDescending => "가격↓",
        FlowerSortMode.LevelAscending => "레벨↑",
        FlowerSortMode.LevelDescending => "레벨↓",
        FlowerSortMode.EfficiencyDescending => "효율순",
        FlowerSortMode.CostEfficiencyDescending => "가성비순",
        _ => ""
    };
}
