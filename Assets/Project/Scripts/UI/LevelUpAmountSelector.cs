using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum LevelUpAmount
{
    One,
    Ten,
    Max
}

/// <summary>
/// 레벨업 단위(+1 / +10 / MAX) 선택 상태를 보관하는 순환 버튼.
/// 클릭할 때마다 다음 단위로 순환하고, 변경 시 OnAmountChanged를 발생시킨다.
/// 특정 패널에 종속되지 않음 — FlowerUpgradePanel/FlowerUpgradeItem이 이 컴포넌트의
/// 현재 값(Current)을 읽어서 미리보기/레벨업 실행에 사용한다.
/// </summary>
public class LevelUpAmountSelector : MonoBehaviour
{
    public TMP_Text label;

    public LevelUpAmount Current { get; private set; } = LevelUpAmount.One;

    public event Action<LevelUpAmount> OnAmountChanged;

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
            LevelUpAmount.One => LevelUpAmount.Ten,
            LevelUpAmount.Ten => LevelUpAmount.Max,
            _ => LevelUpAmount.One
        };

        UpdateLabel();
        OnAmountChanged?.Invoke(Current);
    }

    private void UpdateLabel()
    {
        if (label == null) return;

        label.text = Current switch
        {
            LevelUpAmount.One => "레벨업 단위: +1",
            LevelUpAmount.Ten => "레벨업 단위: +10",
            LevelUpAmount.Max => "레벨업 단위: MAX",
            _ => ""
        };
    }
}
