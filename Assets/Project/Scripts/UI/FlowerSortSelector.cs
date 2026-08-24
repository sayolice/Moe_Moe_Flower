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
    LevelDescending
}

/// <summary>
/// 꽃 레벨업 목록의 "표시 순서"만 결정하는 정렬 선택 순환 버튼.
/// FlowerManager가 들고 있는 실제 보유 꽃 데이터 순서는 절대 변경하지 않는다
/// (FlowerUpgradePanel이 이 값을 읽어 표시용 리스트만 다시 정렬한다).
/// </summary>
public class FlowerSortSelector : MonoBehaviour
{
    public TMP_Text label;

    public FlowerSortMode Current { get; private set; } = FlowerSortMode.DexOrder;

    public event Action<FlowerSortMode> OnSortModeChanged;

    private void Start()
    {
        Button button = GetComponent<Button>();
        if (button != null)
            button.onClick.AddListener(CycleNext);

        UpdateLabel();
    }

    private void CycleNext()
    {
        Current = Current switch
        {
            FlowerSortMode.DexOrder => FlowerSortMode.PriceAscending,
            FlowerSortMode.PriceAscending => FlowerSortMode.PriceDescending,
            FlowerSortMode.PriceDescending => FlowerSortMode.LevelAscending,
            FlowerSortMode.LevelAscending => FlowerSortMode.LevelDescending,
            _ => FlowerSortMode.DexOrder
        };

        UpdateLabel();
        OnSortModeChanged?.Invoke(Current);
    }

    private void UpdateLabel()
    {
        if (label == null) return;

        label.text = Current switch
        {
            FlowerSortMode.DexOrder => "정렬: 도감순",
            FlowerSortMode.PriceAscending => "정렬: 가격↑",
            FlowerSortMode.PriceDescending => "정렬: 가격↓",
            FlowerSortMode.LevelAscending => "정렬: 레벨↑",
            FlowerSortMode.LevelDescending => "정렬: 레벨↓",
            _ => ""
        };
    }
}
