using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 보유한 모든 꽃(FlowerInstance)을 관리하는 중앙 매니저.
/// 화면엔 한 번에 하나의 꽃만 표시되지만(FlowerDisplayController가 담당),
/// 골드 생산은 화면에 안 보이는 개화 꽃도 전부 계속 진행되어야 하므로 여기서 일괄 처리한다.
/// </summary>
public class FlowerManager : MonoBehaviour
{
    public static FlowerManager Instance { get; private set; }

    [Header("도감 순서 (전체 꽃 목록, 이 순서가 도감/스와이프 순서가 됨)")]
    public List<FlowerData> allFlowers = new List<FlowerData>();

    [Header("현재 메인 화면에 표시 중인 꽃 (읽기 전용 확인용)")]
    [SerializeField] private string currentDisplayedFlowerId;

    // 실제로 구매(심음)된 꽃만 여기 존재. key = flowerId
    private Dictionary<string, FlowerInstance> ownedFlowers = new Dictionary<string, FlowerInstance>();

    /// <summary> 화면 표시가 바뀔 때(스와이프/도감이동/신규구매/애정변화) 발생. UI/디스플레이가 구독. </summary>
    public event Action<string> OnDisplayedFlowerChanged;
    /// <summary> 특정 꽃이 개화했을 때 발생. id 전달. </summary>
    public event Action<string> OnFlowerBloomed;
    /// <summary> 보유 목록 자체가 바뀔 때(구매 등). 상점/도감 UI 갱신용. </summary>
    public event Action OnOwnedFlowersChanged;

    public string CurrentDisplayedFlowerId => currentDisplayedFlowerId;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // 튜토리얼: 아무것도 보유하지 않은 최초 상태면 목록의 첫 꽃(민들레)을 자동 지급
        if (ownedFlowers.Count == 0 && allFlowers.Count > 0)
        {
            TryPurchaseSeed(allFlowers[0].flowerId);
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        // 화면 표시 여부와 무관하게, 개화한 모든 꽃이 계속 골드를 생산한다. (기존 로직, 변경 없음)
        foreach (var kvp in ownedFlowers)
        {
            FlowerInstance instance = kvp.Value;
            if (!instance.isBloomed) continue;

            FlowerData data = GetFlowerData(instance.flowerId);
            if (data == null) continue;

            float gps = data.GetGoldPerSecond(instance.currentLevel);
            GameManager.Instance.AddGold(gps * dt);
        }

        // 자동 애정: 화면 표시 여부와 무관하게, 보유한 모든 "미개화" 꽃에 동시에 적용된다.
        // (터치 애정과 달리 현재 표시 중인 꽃 하나로 한정하지 않음 — PlayerStatManager 요구사항)
        if (PlayerStatManager.Instance != null)
        {
            float autoAffection = PlayerStatManager.Instance.GetCurrentValue(PlayerStatType.AutoAffection);
            if (autoAffection > 0f)
            {
                foreach (var kvp in ownedFlowers)
                {
                    FlowerInstance instance = kvp.Value;
                    if (instance.isBloomed) continue;

                    FlowerData data = GetFlowerData(instance.flowerId);
                    if (data == null) continue;

                    AddAffection(instance, data, autoAffection * dt); // 기존 성장/개화 판정 그대로 재사용
                }
            }
        }
    }

    public FlowerData GetFlowerData(string id) => allFlowers.FirstOrDefault(f => f.flowerId == id);
    public bool IsOwned(string id) => ownedFlowers.ContainsKey(id);
    public FlowerInstance GetInstance(string id) => ownedFlowers.TryGetValue(id, out var inst) ? inst : null;

    public FlowerData GetCurrentData() => GetFlowerData(currentDisplayedFlowerId);
    public FlowerInstance GetCurrentInstance() => GetInstance(currentDisplayedFlowerId);

    /// <summary> 씨앗 구매. 성공 시 자동으로 그 꽃이 메인 화면에 표시된다. </summary>
    public bool TryPurchaseSeed(string id)
    {
        if (ownedFlowers.ContainsKey(id)) return false; // 이미 보유중

        FlowerData data = GetFlowerData(id);
        if (data == null) return false;

        if (!GameManager.Instance.TrySpendGold(data.seedPrice)) return false;

        ownedFlowers[id] = new FlowerInstance(id);
        OnOwnedFlowersChanged?.Invoke();

        SetDisplayedFlower(id);
        return true;
    }

    /// <summary>
    /// 지정한 꽃을 최대 levels번 레벨업한다 (+1 / +10 버튼용). 개화한 꽃만 가능하며,
    /// 골드가 부족한 지점에서 멈추고 실제로 오른 레벨 수를 반환한다 (요청한 levels보다 적을 수 있음).
    /// 화면에 표시 중이 아닌 꽃도 id만 알면 레벨업 가능 (ownedFlowers 딕셔너리를 직접 수정하므로
    /// 화면 밖 꽃의 G/s 자동 생산 루프(Update)에도 다음 프레임부터 즉시 반영된다).
    /// </summary>
    public int TryLevelUpFlowerBy(string id, int levels) => LevelUpLoop(id, levels);

    /// <summary> 현재 골드로 가능한 최고 레벨까지 한 번에 레벨업한다 (MAX 버튼용). </summary>
    public int TryLevelUpFlowerToMax(string id) => LevelUpLoop(id, int.MaxValue);

    /// <summary>
    /// 기존 단일 레벨업 API (하위 호환용). 새 코드는 TryLevelUpFlowerBy(id, 1)을 직접 사용해도 된다.
    /// </summary>
    public bool TryLevelUpFlower(string id) => TryLevelUpFlowerBy(id, 1) > 0;

    private int LevelUpLoop(string id, int maxLevels)
    {
        FlowerInstance instance = GetInstance(id);
        FlowerData data = GetFlowerData(id);
        if (instance == null || data == null) return 0;
        if (!instance.isBloomed) return 0; // 개화한 꽃만 레벨업 가능

        int gained = 0;
        while (gained < maxLevels)
        {
            long cost = data.GetLevelUpCost(instance.currentLevel);
            if (!GameManager.Instance.TrySpendGold(cost)) break; // 골드 부족 시 여기서 중단

            instance.currentLevel++;
            gained++;
        }

        if (gained > 0 && id == currentDisplayedFlowerId)
            OnDisplayedFlowerChanged?.Invoke(id); // 현재 표시 중인 꽃이면 UI 갱신 트리거

        return gained;
    }

    /// <summary> 현재 표시 중인 꽃을 클릭했을 때 호출. </summary>
    public void ClickCurrentFlower()
    {
        FlowerInstance instance = GetCurrentInstance();
        FlowerData data = GetCurrentData();
        if (instance == null || data == null) return;

        // 골드는 조건 없이 클릭 시 항상 증가
        float touchGold = PlayerStatManager.Instance != null
            ? PlayerStatManager.Instance.GetCurrentValue(PlayerStatType.TouchGold)
            : 0f;
        GameManager.Instance.AddGold(touchGold);

        if (!instance.isBloomed)
        {
            float touchAffection = PlayerStatManager.Instance != null
                ? PlayerStatManager.Instance.GetCurrentValue(PlayerStatType.TouchAffection)
                : 0f;
            AddAffection(instance, data, touchAffection);
        }
    }

    private void AddAffection(FlowerInstance instance, FlowerData data, float amount)
    {
        if (instance.isBloomed) return;

        instance.currentAffection += amount;

        if (instance.currentAffection >= data.requiredAffection)
        {
            instance.isBloomed = true;
            instance.currentLevel = 1;
            OnFlowerBloomed?.Invoke(instance.flowerId);
            // TODO: 조합 보너스 체크, 메모리얼 조건 체크 훅 연결
        }

        if (instance.flowerId == currentDisplayedFlowerId)
            OnDisplayedFlowerChanged?.Invoke(currentDisplayedFlowerId);
    }

    // ===== 화면 전환 (스와이프 / 도감 이동) =====

    /// <summary> 보유(구매)한 꽃들을 도감 순서대로 정렬한 id 리스트. 스와이프가 이 순서를 따른다. </summary>
    public List<string> GetOwnedIdsInDexOrder()
    {
        return allFlowers
            .Select(f => f.flowerId)
            .Where(id => ownedFlowers.ContainsKey(id))
            .ToList();
    }

    public void SwipeNext() => Swipe(1);
    public void SwipePrevious() => Swipe(-1);

    private void Swipe(int direction)
    {
        List<string> owned = GetOwnedIdsInDexOrder();
        if (owned.Count == 0) return;

        int currentIndex = owned.IndexOf(currentDisplayedFlowerId);
        if (currentIndex < 0) currentIndex = 0;

        int nextIndex = (currentIndex + direction + owned.Count) % owned.Count;
        SetDisplayedFlower(owned[nextIndex]);
    }

    /// <summary> 도감에서 "이동하기" 선택 시 호출. 보유(구매)한 꽃만 이동 가능. </summary>
    public bool TryJumpTo(string id)
    {
        if (!ownedFlowers.ContainsKey(id)) return false;
        SetDisplayedFlower(id);
        return true;
    }

    private void SetDisplayedFlower(string id)
    {
        currentDisplayedFlowerId = id;
        OnDisplayedFlowerChanged?.Invoke(id);
    }
}