using System;
using UnityEngine;

/// <summary>
/// 전역 골드 관리자. 골드 총량과 증감만 담당한다.
/// 터치애정/터치골드/자동애정은 PlayerStatManager로 분리되어 있다 (이 클래스는 그 시스템을 모른다).
///
/// [BigNumber인 이유] 골드는 유일하게 상한이 없는 값이다 — G/s와 레벨업 비용이 전부 "레벨"에
/// 지수적으로 비례하는데 레벨엔 상한이 없으므로, 오래 플레이할수록 골드는 결국 double의 한계
/// (약 10^308)에도 도달한다. int→float→double로 이어진 오버플로우를 이번엔 근본적으로 끝내기
/// 위해 BigNumber(가수×10^지수, 사실상 무한 범위)를 쓴다. 자세한 설계 배경은 BigNumber.cs 참고.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public BigNumber totalGold = BigNumber.Zero;

    /// <summary>
    /// 골드 잔액이 바뀔 때마다 발생 — AddGold/TrySpendGold/SetGold 모두 이 이벤트를 발생시킨다.
    /// UIManager는 매 프레임 totalGold를 폴링하는 대신 이 이벤트를 구독해서 갱신한다.
    /// 인자: 변경 후 새 골드 잔액.
    /// </summary>
    public event Action<BigNumber> OnGoldChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void AddGold(BigNumber amount)
    {
        totalGold += amount;
        OnGoldChanged?.Invoke(totalGold);
    }

    /// <summary>
    /// 세이브 불러오기 전용: 골드를 저장된 값으로 직접 덮어쓴다.
    /// 불러오기 후 UI 동기화를 위해 OnGoldChanged도 발생시킨다.
    /// </summary>
    public void SetGold(BigNumber amount)
    {
        totalGold = amount;
        OnGoldChanged?.Invoke(totalGold);
    }

    public bool TrySpendGold(BigNumber amount)
    {
        if (amount < BigNumber.Zero) return false;
        if (totalGold < amount) return false;
        totalGold -= amount;
        OnGoldChanged?.Invoke(totalGold);
        return true;
    }
}
