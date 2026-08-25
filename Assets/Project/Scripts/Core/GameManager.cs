using UnityEngine;

/// <summary>
/// 전역 골드 관리자. 골드 총량과 증감만 담당한다.
/// 터치애정/터치골드/자동애정은 PlayerStatManager로 분리되어 있다 (이 클래스는 그 시스템을 모른다).
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("골드")]
    public double totalGold = 0;

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

    public void AddGold(double amount)
    {
        totalGold += amount;
        // TODO: UI 골드 표시 갱신 이벤트 연결
    }

    /// <summary> 세이브 불러오기 전용: 골드를 저장된 값으로 직접 덮어쓴다. </summary>
    public void SetGold(double amount) => totalGold = amount;

    public bool TrySpendGold(double amount)
    {
        if (totalGold < amount) return false;
        totalGold -= amount;
        return true;
    }
}