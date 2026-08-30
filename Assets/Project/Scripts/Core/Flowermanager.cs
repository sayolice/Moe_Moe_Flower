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

    [Header("유대(Bond) 전역 밸런스 — 비워두면 BondData의 기본값으로 자동 대체됨 (필수 아님)")]
    public BondData bondData;
    private BondData runtimeDefaultBondData; // bondData를 안 채웠을 때 쓰는 기본값 인스턴스 (지연 생성)

    // 실제로 구매(심음)된 꽃만 여기 존재. key = flowerId
    private Dictionary<string, FlowerInstance> ownedFlowers = new Dictionary<string, FlowerInstance>();

    /// <summary> 화면 표시가 바뀔 때(스와이프/도감이동/신규구매/애정변화) 발생. UI/디스플레이가 구독. </summary>
    public event Action<string> OnDisplayedFlowerChanged;
    /// <summary> 특정 꽃이 개화했을 때 발생. id 전달. </summary>
    public event Action<string> OnFlowerBloomed;
    /// <summary> 보유 목록 자체가 바뀔 때(구매 등). 상점/도감 UI 갱신용. </summary>
    public event Action OnOwnedFlowersChanged;
    /// <summary> 특정 꽃의 유대 레벨이 올랐을 때 발생 (flowerId, newBondLevel). 연출/메모리얼 뱃지용. </summary>
    public event Action<string, int> OnBondLevelUp;
    /// <summary>
    /// 터치로 골드를 얻을 때마다 발생 (얻은 양). UIManager가 "골드 +n/s" 표시에 잠깐 반영해 애정처럼
    /// 터치에 반응하는 느낌을 준다 — 단, totalGold를 직접 측정하지 않고 이 이벤트로만 반영하므로
    /// 레벨업 등 골드 소비 이벤트가 이 표시를 오염시키지 않는다(예전 "0/s에 고정" 버그가 바로
    /// totalGold 자체를 측정해서 생겼던 문제라 같은 실수를 반복하지 않기 위함).
    /// </summary>
    public event Action<BigNumber> OnTouchGoldGranted;

    /// <summary>
    /// 지금 적용 중인 유대 밸런스 데이터. bondData를 인스펙터에서 비워둬도(에셋 미배치) BondData
    /// 클래스 자체의 필드 기본값으로 즉시 정상 동작하도록, 런타임에 CreateInstance로 기본 인스턴스를
    /// 하나 만들어 대신 쓴다 — 씬 재구성 없이도 유대 시스템이 처음부터 작동해야 하기 때문이다.
    /// </summary>
    public BondData ActiveBondData
    {
        get
        {
            if (bondData != null) return bondData;
            if (runtimeDefaultBondData == null) runtimeDefaultBondData = ScriptableObject.CreateInstance<BondData>();
            return runtimeDefaultBondData;
        }
    }

    public string CurrentDisplayedFlowerId => currentDisplayedFlowerId;

    /// <summary>
    /// 인디케이터 UI용("3 / 5" 등): 보유 꽃(도감순) 중 현재 표시 중인 꽃의 0-based 순번.
    /// currentDisplayedFlowerId를 유일한 소스로 삼아 매번 계산한다 — 별도 인덱스 필드를 두면
    /// 구매/스와이프/도감이동 등 표시 대상이 바뀌는 모든 경로마다 그 필드도 손으로 맞춰줘야 해서
    /// 어긋날 여지가 생긴다(GetEffectiveGoldPerSecond가 온라인/오프라인 경로를 하나로 합친 것과 같은 이유).
    /// 보유 꽃이 없거나 표시 대상이 없으면 -1.
    /// </summary>
    public int CurrentDisplayIndex => GetOwnedIdsInDexOrder().IndexOf(currentDisplayedFlowerId);

    /// <summary> 인디케이터 UI용: 현재 보유한 꽃 총 개수. </summary>
    public int OwnedFlowerCount => ownedFlowers.Count;

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

            BigNumber gps = GetEffectiveGoldPerSecond(data, instance.currentLevel, instance.bondLevel, instance.flowerId);
            GameManager.Instance.AddGold(gps * dt);
        }

        // 정원 유대: 배치된 꽃은 매초 bondPerSecondInGarden만큼 유대가 쌓인다(배율 없이 그 값 그대로,
        // 설계 원칙 0.2/0.3). ApplyOfflineProgress(오프라인)에는 이미 반영돼 있었는데 이 온라인 Update
        // 루프에는 빠져 있었다 — 그래서 정원에 배치해 둬도 실시간으로는 유대가 안 쌓이던 버그였다.
        // AddBond를 그대로 재사용해 while 루프 다중 레벨업/Lv.5 상한을 동일하게 적용받는다.
        if (GardenManager.Instance != null)
        {
            double gardenBondRate = GardenManager.Instance.ActiveGardenData.bondPerSecondInGarden;
            if (gardenBondRate > 0)
            {
                foreach (string flowerId in GardenManager.Instance.GetPlacedFlowerIds())
                {
                    if (ownedFlowers.TryGetValue(flowerId, out FlowerInstance placedInstance))
                        AddBond(placedInstance, gardenBondRate * dt);
                }
            }
        }

        // 자동 애정: 화면 표시 여부와 무관하게, 보유한 모든 "미개화" 꽃에 동시에 적용된다.
        // (터치 애정과 달리 현재 표시 중인 꽃 하나로 한정하지 않음 — PlayerStatManager 요구사항)
        // 연꽃(AutoAffectionBonusFlat)이 있으면 PlayerStat과 별개로 고정치를 더한다.
        {
            double autoAffection = GetEffectiveAutoAffectionRate();

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

    /// <summary>
    /// 자동 애정 스탯 + 연꽃(AutoAffectionBonusFlat) 고정 보너스를 합친 "실효" 자동 애정 속도(초당).
    /// Update()(온라인)와 ApplyOfflineProgress()(오프라인), 그리고 UI의 개화 예상 시간 계산까지
    /// 전부 이 메서드 하나만 호출하도록 통일해서, 세 곳의 계산식이 서로 어긋날 여지를 없앤다.
    /// </summary>
    public double GetEffectiveAutoAffectionRate()
    {
        // PlayerStatManager.GetCurrentValue는 BigNumber를 반환한다(터치 골드와 같은 경로를 공유하기
        // 때문 — 터치 골드는 총 골드에 더해지므로 상한이 없어야 한다). 자동 애정은 currentAffection
        // (개화하면 더 안 자라는, 설계상 상한이 있는 값)에만 쓰이므로 여기서 double로 좁혀도 안전하다.
        double rate = PlayerStatManager.Instance != null
            ? PlayerStatManager.Instance.GetCurrentValue(PlayerStatType.AutoAffection).ToDouble()
            : 0;
        if (PassiveManager.Instance != null)
            rate += PassiveManager.Instance.GetFlatBonusTotal(PassiveEffectType.AutoAffectionBonusFlat);
        return rate;
    }

    // ===== 패시브 반영 "실효" 비용 계산 (전역 할인은 FlowerData 혼자 모르므로 여기서 감싼다) =====

    /// <summary> 씨앗가에 전역 할인 패시브(튤립)를 반영한 실제 구매 가격. </summary>
    public BigNumber GetEffectiveSeedPrice(FlowerData data)
    {
        float multiplier = PassiveManager.Instance != null
            ? PassiveManager.Instance.GetTotalMultiplier(PassiveEffectType.SeedPriceDiscountPercent)
            : 1f;
        return BigNumber.FromDouble(data.seedPrice) * multiplier;
    }

    /// <summary> 레벨업 비용에 전역 할인 패시브(장미)를 반영한 실제 비용(레벨 1개). </summary>
    public BigNumber GetEffectiveLevelUpCost(FlowerData data, int level)
    {
        float multiplier = PassiveManager.Instance != null
            ? PassiveManager.Instance.GetTotalMultiplier(PassiveEffectType.LevelUpCostDiscountPercent)
            : 1f;
        return data.GetLevelUpCost(level) * multiplier;
    }

    /// <summary> GetEffectiveLevelUpCost의 다중 레벨 합산판 (+10/MAX 미리보기용). </summary>
    public BigNumber GetEffectiveLevelUpCostForLevels(FlowerData data, int fromLevel, int levels)
    {
        if (levels <= 0) return BigNumber.Zero;

        BigNumber total = BigNumber.Zero;
        for (int i = 0; i < levels; i++)
            total += GetEffectiveLevelUpCost(data, fromLevel + i);
        return total;
    }

    /// <summary>
    /// G/s에 전역 보너스 패시브(해바라기)와 유대(Bond) 배율을 반영한 실제 생산량. 온라인(Update),
    /// 오프라인(ApplyOfflineProgress), UI 프리뷰(FlowerUpgradeItem/FlowerDexPanel/UIManager) 전부
    /// 반드시 이 메서드를 통해서만 G/s를 계산해야 세 경로가 어긋날 여지가 없다 — 유대 배율을 여기
    /// 한 곳에만 곱하는 이유도 동일하다(다른 곳에서 별도로 곱하지 말 것).
    ///
    /// bondLevel은 호출자가 "그 꽃 인스턴스"의 현재 유대 레벨을 직접 넘긴다(FlowerData 혼자서는
    /// 알 수 없는 값이라 인자로 받는다) — 패시브 전역 배율과 달리 유대 배율은 그 꽃 하나에만
    /// 적용되므로 FlowerManager가 전역으로 조회할 수 없다.
    ///
    /// flowerId는 정원 배율(인접 효과 × 시듦) 조회에 쓴다 — GardenManager가 없으면(아직 씬에
    /// 없거나 미배치) 자동으로 1(영향 없음)이 된다. "정원은 생산 게이트가 아니다" 원칙이 이
    /// 기본값 1로 구조적으로 보장된다.
    ///
    /// gardenElapsedOverride는 오프라인 정산 전용이다 — 라이브 시각(DateTime.UtcNow) 대신 "그
    /// 구간 시점"의 경과 시간을 명시해서 시듦 단계를 정확히 재현해야 하기 때문이다(온라인/UI
    /// 프리뷰는 null로 두면 GardenManager가 알아서 지금 시각 기준으로 계산한다).
    /// </summary>
    public BigNumber GetEffectiveGoldPerSecond(FlowerData data, int level, int bondLevel, string flowerId, double? gardenElapsedOverride = null)
    {
        float passiveMultiplier = PassiveManager.Instance != null
            ? PassiveManager.Instance.GetTotalMultiplier(PassiveEffectType.GoldPerSecondBonusPercent)
            : 1f;

        float gardenMultiplier = 1f;
        if (GardenManager.Instance != null)
        {
            gardenMultiplier = gardenElapsedOverride.HasValue
                ? GardenManager.Instance.GetGoldMultiplierForFlower(flowerId, gardenElapsedOverride.Value)
                : GardenManager.Instance.GetGoldMultiplierForFlower(flowerId);
        }

        return data.GetGoldPerSecond(level) * passiveMultiplier * GetBondGoldMultiplier(bondLevel) * gardenMultiplier;
    }

    /// <summary>
    /// 유대 레벨에 대응하는 G/s 곱연산 배율. Lv.0(유대 없음)은 1.0(배율 없음), 그 외에는
    /// BondData.goldMultipliers[bondLevel-1]. 인덱스 범위를 벗어나면(밸런스 데이터 오류 방어) 1.0.
    /// </summary>
    public float GetBondGoldMultiplier(int bondLevel)
    {
        if (bondLevel <= 0) return 1f;

        List<float> multipliers = ActiveBondData.goldMultipliers;
        int index = bondLevel - 1;
        if (multipliers == null || index < 0 || index >= multipliers.Count) return 1f;

        return multipliers[index];
    }

    /// <summary>
    /// 유대 레벨에 대응하는 정원 인접 효과 "위력"(0~1). GardenManager.RecomputeAdjacencyCache가
    /// 각 꽃이 내는 인접 효과의 기본 보너스에 이 값을 곱해서 실제 기여량을 정한다 — 유대 Lv.5에서만
    /// 발동(0 또는 100%)하던 것을 유대 레벨에 비례해 단계적으로 강해지도록 바꾼 것(요청 사항).
    /// 인덱스는 bondLevel 그대로(0=Lv.0)라 GetBondGoldMultiplier와 인덱싱 규칙이 다르다는 점에 주의 —
    /// 그쪽은 "Lv.1부터 배열이 시작"이지만 이쪽은 "Lv.0부터 배열이 시작"이다(Lv.0에서 위력이 반드시
    /// 0이어야 하므로 배열에 그 칸이 필요하다).
    /// </summary>
    public float GetAdjacencyPower(int bondLevel)
    {
        List<float> table = ActiveBondData.adjacencyPowerByBondLevel;
        if (table == null || table.Count == 0) return bondLevel >= ActiveBondData.maxBondLevel ? 1f : 0f; // 밸런스 데이터 누락 시 구 동작(Lv.5부터 100%)으로 안전하게 대체

        int index = Mathf.Clamp(bondLevel, 0, table.Count - 1);
        return table[index];
    }

    /// <summary>
    /// 지금 이 순간 개화한 모든 보유 꽃의 초당 골드 생산량 합 — UI의 "골드 +n/s" 표시 전용.
    /// [버그 배경] 예전 UIManager는 이 값을 totalGold의 프레임간 변화량(goldNow-lastGold)/dt로
    /// "측정"해서 보여줬는데, 레벨업(골드 소모)·씨앗 구매·터치 골드 등 골드가 순간적으로 오르내리는
    /// 모든 이벤트가 전부 이 측정치를 오염시켰다. 특히 연속 레벨업(꾹 누르기)처럼 소모가 생산보다
    /// 훨씬 빠른 구간에서는 측정치가 크게 음수로 떨어지고, 그 뒤 지수 스무딩으로 되돌아오는 데
    /// 오래 걸리거나(체감상 "0에 고정") 재실행 전까지 회복이 안 되는 것처럼 보였다.
    /// 이 메서드는 골드 잔액 변화를 전혀 보지 않고 "꽃 상태(개화 여부·레벨)"만으로 매번 새로 계산하므로,
    /// 골드가 어떻게 오르내리든 절대 왜곡되지 않는다 — Update()의 실제 골드 지급 루프와 동일한 계산.
    /// </summary>
    public BigNumber GetTotalGoldPerSecond()
    {
        BigNumber total = BigNumber.Zero;
        foreach (var kvp in ownedFlowers)
        {
            FlowerInstance instance = kvp.Value;
            if (!instance.isBloomed) continue;

            FlowerData data = GetFlowerData(instance.flowerId);
            if (data == null) continue;

            total += GetEffectiveGoldPerSecond(data, instance.currentLevel, instance.bondLevel, instance.flowerId);
        }
        return total;
    }

    /// <summary>
    /// 작업 3(정원 전체 요약 "인접 효과 +N%") 전용. GetTotalGoldPerSecond와 완전히 같은 합산이되
    /// gardenMultiplier만 빼서 계산한다 — 그래서 (GetTotalGoldPerSecond() / 이 값 - 1)이 정확히
    /// "지금 배치가 정원 배율(인접 효과×시듦)로 인해 기여하는 몫"이 된다. 새 계산식을 만드는 게
    /// 아니라 기존 식에서 인수 하나를 1로 고정한 것뿐이라, 두 값이 어긋날 여지가 없다.
    /// </summary>
    public BigNumber GetTotalGoldPerSecondWithoutGardenAdjacency()
    {
        BigNumber total = BigNumber.Zero;
        float passiveMultiplier = PassiveManager.Instance != null
            ? PassiveManager.Instance.GetTotalMultiplier(PassiveEffectType.GoldPerSecondBonusPercent)
            : 1f;

        foreach (var kvp in ownedFlowers)
        {
            FlowerInstance instance = kvp.Value;
            if (!instance.isBloomed) continue;

            FlowerData data = GetFlowerData(instance.flowerId);
            if (data == null) continue;

            total += data.GetGoldPerSecond(instance.currentLevel) * passiveMultiplier * GetBondGoldMultiplier(instance.bondLevel);
        }
        return total;
    }

    /// <summary>
    /// 작업 3(정원 요약 "인접 효과 +N% → 시듦 적용 후 +M%") 전용 — 시듦을 적용하기 "전"의 총 G/s.
    /// GetTotalGoldPerSecond와 완전히 같은 합산이되 gardenMultiplier 대신 GardenManager의
    /// GetRawAdjacencyMultiplierForFlower(시듦 미반영, 1+fraction)를 쓴다. 이 값과
    /// GetTotalGoldPerSecondWithoutGardenAdjacency()를 비교하면 "인접 효과 자체가 얼마나 센지"가,
    /// GetTotalGoldPerSecond()와 비교하면 "시듦 적용 후 실제로 얼마나 남았는지"가 나온다.
    /// </summary>
    public BigNumber GetTotalGoldPerSecondWithRawAdjacency()
    {
        BigNumber total = BigNumber.Zero;
        float passiveMultiplier = PassiveManager.Instance != null
            ? PassiveManager.Instance.GetTotalMultiplier(PassiveEffectType.GoldPerSecondBonusPercent)
            : 1f;

        foreach (var kvp in ownedFlowers)
        {
            FlowerInstance instance = kvp.Value;
            if (!instance.isBloomed) continue;

            FlowerData data = GetFlowerData(instance.flowerId);
            if (data == null) continue;

            float rawGardenMultiplier = GardenManager.Instance != null
                ? GardenManager.Instance.GetRawAdjacencyMultiplierForFlower(instance.flowerId)
                : 1f;

            total += data.GetGoldPerSecond(instance.currentLevel) * passiveMultiplier
                * GetBondGoldMultiplier(instance.bondLevel) * rawGardenMultiplier;
        }
        return total;
    }

    /// <summary> GetEffectiveLevelUpCost 기준으로 주어진 골드로 몇 레벨까지 오를 수 있는지 (+10/MAX 미리보기용). </summary>
    public int GetEffectiveMaxAffordableLevels(FlowerData data, int fromLevel, BigNumber gold)
    {
        const int SAFETY_CAP = 100000;

        int levels = 0;
        BigNumber remaining = gold;

        while (levels < SAFETY_CAP)
        {
            BigNumber cost = GetEffectiveLevelUpCost(data, fromLevel + levels);
            if (cost > remaining) break;

            remaining -= cost;
            levels++;
        }

        return levels;
    }

    public FlowerData GetFlowerData(string id) => allFlowers.FirstOrDefault(f => f.flowerId == id);
    public bool IsOwned(string id) => ownedFlowers.ContainsKey(id);
    public FlowerInstance GetInstance(string id) => ownedFlowers.TryGetValue(id, out var inst) ? inst : null;

    /// <summary> 세이브 저장 전용: 현재 보유한 모든 꽃 인스턴스를 반환한다 (읽기 전용 스냅샷 용도). </summary>
    public IEnumerable<FlowerInstance> GetAllOwnedInstances() => ownedFlowers.Values;

    /// <summary>
    /// 세이브 불러오기 전용: 보유 꽃 목록을 저장된 상태로 통째로 교체한다.
    /// Start()의 최초 튜토리얼 지급 로직(ownedFlowers.Count == 0 체크)과는 실행 순서 상관없이
    /// 안전하다 — 이 메서드가 먼저 실행되면 Count가 0이 아니게 되어 튜토리얼 지급이 자동으로 스킵되고,
    /// 나중에 실행되면 튜토리얼 지급 결과를 그대로 덮어쓴다.
    /// </summary>
    public void LoadOwnedFlowers(List<FlowerInstance> flowers, string displayedFlowerId)
    {
        ownedFlowers.Clear();
        if (flowers != null)
        {
            foreach (FlowerInstance instance in flowers)
            {
                if (string.IsNullOrEmpty(instance.flowerId)) continue;
                ownedFlowers[instance.flowerId] = instance;
            }
        }
        OnOwnedFlowersChanged?.Invoke();

        if (!string.IsNullOrEmpty(displayedFlowerId) && ownedFlowers.ContainsKey(displayedFlowerId))
        {
            SetDisplayedFlower(displayedFlowerId);
        }
        else
        {
            List<string> owned = GetOwnedIdsInDexOrder();
            if (owned.Count > 0) SetDisplayedFlower(owned[0]);
        }
    }

    /// <summary>
    /// 데이터 초기화(설정 화면 전용) — 보유 꽃을 전부 비우고, Start()의 최초 튜토리얼 지급과 동일한
    /// 로직(TryPurchaseSeed)으로 도감 첫 꽃만 다시 지급한다. 재시작 없이 즉시 초반부터 다시 테스트할
    /// 수 있어야 하므로, 세이브 파일 삭제와 별개로 지금 실행 중인 상태 자체를 여기서 되돌린다.
    /// </summary>
    public void ResetToFreshStart()
    {
        ownedFlowers.Clear();
        currentDisplayedFlowerId = null;
        OnOwnedFlowersChanged?.Invoke();

        if (allFlowers.Count > 0)
            TryPurchaseSeed(allFlowers[0].flowerId);
    }

    public FlowerData GetCurrentData() => GetFlowerData(currentDisplayedFlowerId);
    public FlowerInstance GetCurrentInstance() => GetInstance(currentDisplayedFlowerId);

    /// <summary>
    /// 오프라인 경과 시간(elapsedSeconds)만큼 골드/애정/정원 유대를 정산한다 (세이브 불러오기 전용).
    /// 정책: 온라인의 100%, 상한 없음. 터치 애정/터치 골드는 포함하지 않는다(클릭이 없으므로).
    ///
    /// [구간별(이벤트 기반) 시뮬레이션] 오프라인 구간 전체를 한 번에 계산하지 않고, 다음 중 가장
    /// 먼저 오는 시점까지를 한 구간으로 쪼갠다: (1) 다음 개화, (2) 다음 시듦 단계 경계(12h/24h/48h).
    /// 이유는 둘 다 같다 — G/s에 영향을 주는 무언가가 "그 순간부터" 바뀌는데, 한 번에 계산하면
    /// 정산 시작 시점의 배율이 구간 전체에 잘못 적용된다(정원 명세 5.2가 시듦에 대해 이 문제를
    /// 명시적으로 지적한다). 구간 경계마다 다시 계산하면, 그 순간부터 바로 새 배율이 반영된다 —
    /// 온라인에서 매 프레임 다시 계산하는 것과 원리가 같다.
    /// </summary>
    public OfflineSettlementResult ApplyOfflineProgress(double elapsedSeconds)
    {
        var result = new OfflineSettlementResult { elapsedSeconds = elapsedSeconds };
        if (elapsedSeconds <= 0 || GameManager.Instance == null) return result;

        BigNumber totalGold = BigNumber.Zero;
        double remainingTime = elapsedSeconds;

        // 정원이 아직 씬에 없으면(구현 전/미배치) 전부 건너뛴다 — "정원을 안 쓰면 영향 0" 원칙.
        bool hasGarden = GardenManager.Instance != null;
        double gardenElapsedSinceTended = hasGarden ? GardenManager.Instance.GetElapsedSecondsSinceTended() : 0;
        List<string> placedFlowerIds = hasGarden ? GardenManager.Instance.GetPlacedFlowerIds().ToList() : new List<string>();
        double gardenBondRate = hasGarden ? GardenManager.Instance.ActiveGardenData.bondPerSecondInGarden : 0;

        // 반복 횟수는 이론상 "이번 정산 중 개화하는 꽃의 수 + 지나가는 시듦 단계 수"만큼만 필요하므로,
        // 넉넉한 상한을 둬서 부동소수점 오차로 인한 무한루프 가능성을 원천 차단한다.
        const int SAFETY_CAP = 10000;
        for (int iteration = 0; remainingTime > 0 && iteration < SAFETY_CAP; iteration++)
        {
            double autoAffectionRate = GetEffectiveAutoAffectionRate();

            // 이번 구간의 길이 = 남은 시간, 다음 개화까지의 시간, 다음 시듦 단계 경계까지의 시간 중 최솟값.
            double segmentDuration = remainingTime;
            if (autoAffectionRate > 0f)
            {
                foreach (FlowerInstance instance in ownedFlowers.Values)
                {
                    if (instance.isBloomed) continue;
                    FlowerData data = GetFlowerData(instance.flowerId);
                    if (data == null) continue;

                    double remainingAffection = Math.Max(0, data.requiredAffection - instance.currentAffection);
                    double timeToBloom = remainingAffection / autoAffectionRate;
                    if (timeToBloom < segmentDuration) segmentDuration = timeToBloom;
                }
            }

            if (hasGarden && placedFlowerIds.Count > 0)
            {
                double timeToWiltBoundary = GardenManager.Instance.ActiveGardenData
                    .GetTimeToNextWiltStageBoundary(gardenElapsedSinceTended);
                if (timeToWiltBoundary < segmentDuration) segmentDuration = timeToWiltBoundary;
            }

            bool anyGrowingFlower = false;

            foreach (FlowerInstance instance in ownedFlowers.Values)
            {
                FlowerData data = GetFlowerData(instance.flowerId);
                if (data == null) continue;

                if (instance.isBloomed)
                {
                    // 이 구간 동안 현재 레벨 G/s로 정산 (전역 G/s 보너스·정원 배율 모두 구간 시작
                    // 시점 최신값 반영 — gardenElapsedSinceTended를 명시적으로 넘겨서, "지금"이
                    // 아니라 "그 구간 시점"의 시듦 단계로 정확히 계산한다).
                    totalGold += GetEffectiveGoldPerSecond(
                        data, instance.currentLevel, instance.bondLevel, instance.flowerId, gardenElapsedSinceTended)
                        * segmentDuration;
                    continue;
                }

                anyGrowingFlower = true;
                if (autoAffectionRate <= 0f) continue; // 자동 애정이 꺼져 있으면 미개화 꽃은 애초에 자라지 않음

                // 기존 개화 로직(AddAffection)을 그대로 태운다 — 임계치를 넘기면 그 안에서 isBloomed=true로
                // 전환되고, 다음 반복의 GetEffectiveAutoAffectionRate()/GetEffectiveGoldPerSecond() 호출이
                // PassiveManager를 통해 이 변화를 즉시 반영한다.
                AddAffection(instance, data, autoAffectionRate * segmentDuration);
                if (instance.isBloomed)
                    result.newlyBloomedFlowerIds.Add(instance.flowerId);
            }

            // 정원 유대 정산 — 배치된 꽃만, 기존 AddBond 경로 재사용(while 루프로 여러 레벨 처리 유지).
            // 절대 배율을 곱하지 않는다(설계 원칙 0.2/0.3) — bondPerSecondInGarden 그대로.
            if (hasGarden)
            {
                foreach (string flowerId in placedFlowerIds)
                {
                    FlowerInstance placedInstance = GetInstance(flowerId);
                    if (placedInstance == null) continue;

                    int levelBefore = placedInstance.bondLevel;
                    AddBond(placedInstance, gardenBondRate * segmentDuration);
                    if (placedInstance.bondLevel != levelBefore)
                        result.gardenBondLeveledFlowerIds.Add(flowerId);
                }
                result.gardenBondEarned += gardenBondRate * segmentDuration * placedFlowerIds.Count;
            }

            remainingTime -= segmentDuration;
            gardenElapsedSinceTended += segmentDuration;

            // 더 진행할 이유가 없으면(자동 애정도 꺼져 있고, 성장 중인 꽃도 없고, 정원에 배치된 것도
            // 없어서 시듦 경계도 의미가 없음) 여기서 종료 — 그렇지 않으면 매번 segmentDuration이
            // remainingTime과 같아져 다음 반복에서 자연히 끝나므로 무한 반복은 아니지만, 굳이 도는
            // 것을 막는 조기 종료다.
            if (autoAffectionRate <= 0f && !anyGrowingFlower && (!hasGarden || placedFlowerIds.Count == 0))
                break;
        }

        // 여기서 별도의 "오프라인 배율"을 다시 곱하지 않는다 — 오프라인은 온라인의 100%로 확정됐고,
        // GetEffectiveGoldPerSecond가 이미 온라인과 동일한 계산식(해바라기 G/s 보너스 포함)을 썼으므로
        // totalGold는 그 자체로 정확한 오프라인 정산 결과다. 여기서 또 배율을 곱하면 100%를 초과해서
        // "앱을 꺼두는 게 이득"이라는 모순이 재발한다.
        if (totalGold > BigNumber.Zero)
        {
            GameManager.Instance.AddGold(totalGold);
            // goldEarned는 팝업 표시 전용(계산에 재사용되지 않음)이라 double로 좁혀도 안전하다 —
            // 정말 double 한계를 넘는 극단적인 경우엔 큰 수로 보이거나 무한대로 보일 뿐, 골드
            // 자체(totalGold, BigNumber)는 이미 정확히 더해진 뒤라 게임 진행엔 영향이 없다.
            result.goldEarned = totalGold.ToDouble();
        }

        return result;
    }

    /// <summary> 씨앗 구매. 성공 시 자동으로 그 꽃이 메인 화면에 표시된다. </summary>
    public bool TryPurchaseSeed(string id)
    {
        if (ownedFlowers.ContainsKey(id)) return false; // 이미 보유중

        FlowerData data = GetFlowerData(id);
        if (data == null) return false;

        if (!GameManager.Instance.TrySpendGold(GetEffectiveSeedPrice(data))) return false;

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

    /// <summary>
    /// maxLevels는 "실제로 골드를 지불하는 레벨 수"의 상한이다(+1=1, +10=10, MAX=무제한).
    /// 벚꽃(무료 레벨)은 이 상한과 별개로 추가되므로, +10을 눌러도 요청한 10레벨은
    /// 항상 정상적으로 구매되고 그 위에 벚꽃 보너스가 더 붙는 구조다(Lv10→20 정상, 발동 시 →21).
    /// </summary>
    private int LevelUpLoop(string id, int maxLevels)
    {
        FlowerInstance instance = GetInstance(id);
        FlowerData data = GetFlowerData(id);
        if (instance == null || data == null) return 0;
        if (!instance.isBloomed) return 0; // 개화한 꽃만 레벨업 가능

        int paidGained = 0;
        int freeGained = 0;
        BigNumber totalGoldSpent = BigNumber.Zero;

        while (paidGained < maxLevels)
        {
            BigNumber cost = GetEffectiveLevelUpCost(data, instance.currentLevel);
            if (!GameManager.Instance.TrySpendGold(cost)) break; // 골드 부족 시 여기서 중단

            totalGoldSpent += cost;
            instance.currentLevel++;
            paidGained++;

            // 벚꽃: 지금 막 구매한 이 레벨 1개에 대해서만 독립 판정한다.
            // (액션 1회가 아니라 "레벨 1개"마다 판정하므로 +1/+10/MAX 어느 것으로 사도 기대값이 동일하다)
            if (PassiveManager.Instance != null && PassiveManager.Instance.RollBonusFreeLevel())
            {
                instance.currentLevel++;
                freeGained++;
            }
        }

        // 라벤더: 이번 레벨업 "작업" 전체(실제 지불한 골드 총액)에 대해 1회 환급 판정.
        if (paidGained > 0 && PassiveManager.Instance != null)
        {
            BigNumber refund = PassiveManager.Instance.RollLevelUpRefund(totalGoldSpent);
            if (refund > BigNumber.Zero) GameManager.Instance.AddGold(refund);
        }

        int totalGained = paidGained + freeGained;

        if (totalGained > 0 && id == currentDisplayedFlowerId)
            OnDisplayedFlowerChanged?.Invoke(id); // 현재 표시 중인 꽃이면 UI 갱신 트리거

        return totalGained;
    }

    /// <summary>
    /// 현재 표시 중인 꽃을 클릭했을 때 호출.
    /// 패시브 3종이 여기 관여한다 (셋 다 "지금 터치한 꽃이 누구인지"와 무관하게 적용됨 — 나팔꽃/수국을
    /// 화면에 띄울 필요가 없다):
    ///   나팔꽃(AnyFlowerTouch)  - 6% 확률로 TouchGold 1회 추가
    ///   팬지(AnyUnbloomedTouch) - 지금 터치한 꽃이 미개화 상태면(누구든) 6% 확률로 TouchAffection 1회 추가
    ///   수국(AnyFlowerTouch)    - 애정+1(미개화)/골드+1(개화), 자기 자신 터치 시에도 적용
    /// </summary>
    public void ClickCurrentFlower()
    {
        FlowerInstance instance = GetCurrentInstance();
        FlowerData data = GetCurrentData();
        if (instance == null || data == null) return;

        // 골드는 조건 없이 클릭 시 항상 증가. BigNumber인 이유는 GetCurrentValue와 동일
        // (터치 골드도 레벨에 지수적으로 비례해 상한 없이 커지고, 총 골드에 더해지기 때문).
        BigNumber touchGold = PlayerStatManager.Instance != null
            ? PlayerStatManager.Instance.GetCurrentValue(PlayerStatType.TouchGold)
            : BigNumber.Zero;

        if (PassiveManager.Instance != null)
            touchGold += PassiveManager.Instance.RollAnyFlowerTouchBonus(PassiveEffectType.TouchGoldExtraChance, touchGold);

        GameManager.Instance.AddGold(touchGold);
        OnTouchGoldGranted?.Invoke(touchGold);

        if (!instance.isBloomed)
        {
            // 터치 애정은 currentAffection(개화하면 더 안 자라는, 상한 있는 값)에만 쓰이므로
            // double로 좁혀도 안전하다(GetEffectiveAutoAffectionRate와 동일한 이유).
            double touchAffection = PlayerStatManager.Instance != null
                ? PlayerStatManager.Instance.GetCurrentValue(PlayerStatType.TouchAffection).ToDouble()
                : 0;

            if (PassiveManager.Instance != null)
                touchAffection += PassiveManager.Instance.RollAnyUnbloomedTouchBonus(instance, PassiveEffectType.TouchAffectionExtraChance, touchAffection);

            AddAffection(instance, data, touchAffection);
        }
        else
        {
            // 유대(Bond): 개화한 꽃을 터치하면 +1. 어떤 스탯/패시브/배율도 절대 곱하지 않는다 —
            // BondData.bondPerClick을 그대로 AddBond에 넘긴다(설계 원칙 1.1/1.2, ActiveBondData 주석 참고).
            AddBond(instance, ActiveBondData.bondPerClick);
        }

        // 수국: 지원 보너스 — 대상(=지금 터치한 꽃, 자기 자신 포함)의 상태에 따라 애정 또는 골드로 적용
        if (PassiveManager.Instance != null)
        {
            float supportBonus = PassiveManager.Instance.RollSupportTouchBonus(instance);
            if (supportBonus > 0f)
            {
                if (instance.isBloomed)
                {
                    GameManager.Instance.AddGold(supportBonus);
                    OnTouchGoldGranted?.Invoke(supportBonus);
                }
                else AddAffection(instance, data, supportBonus);
            }
        }
    }

    /// <summary>
    /// 정원의 시듦 복구 터치(GardenManager.TouchTile)처럼, "이 터치는 유대를 쌓지 않지만 골드는
    /// 일반 터치와 동일하게 받아야 하는" 외부 이벤트가 재사용하는 공개 경로. ClickCurrentFlower의
    /// 골드 지급 부분과 같은 계산을 쓰되, 절대 AddBond를 호출하지 않는다는 것이 핵심이다
    /// (시듦 복구는 청소 행위이지 관계 형성이 아니라는 설계 원칙 3.4).
    /// </summary>
    public void GrantExternalTouchGold()
    {
        BigNumber touchGold = PlayerStatManager.Instance != null
            ? PlayerStatManager.Instance.GetCurrentValue(PlayerStatType.TouchGold)
            : BigNumber.Zero;

        if (PassiveManager.Instance != null)
            touchGold += PassiveManager.Instance.RollAnyFlowerTouchBonus(PassiveEffectType.TouchGoldExtraChance, touchGold);

        GameManager.Instance.AddGold(touchGold);
        OnTouchGoldGranted?.Invoke(touchGold);
    }

    /// <summary>
    /// amount가 double인 것이 중요하다 — 자동 애정은 매 프레임 `rate * deltaTime`이라는 아주 작은
    /// 값을 더하는데, 여기서 float으로 좁히면 currentAffection이 커진 뒤(수백만 이상)에는 그 증가분이
    /// 통째로 반올림되어 사라진다(FlowerInstance.currentAffection 주석의 정밀도 설명 참고).
    /// </summary>
    private void AddAffection(FlowerInstance instance, FlowerData data, double amount)
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

    /// <summary>
    /// 유대(Bond)를 더한다. 지금은 터치(ClickCurrentFlower, 개화한 꽃 +1)만 이 메서드를 호출하지만,
    /// 정원 시스템이 붙으면 배치된 꽃마다 매초 AddBond(flower, BondData.bondPerSecondInGarden)을
    /// 호출하는 형태로 그대로 재사용된다(오프라인 정산도 이 경로를 재사용하게 될 것) — 그래서
    /// public으로 미리 열어둔다. amount는 호출자가 이미 확정한 "정확한 유대량"이며, 이 메서드는
    /// 어떤 경우에도 amount에 배율을 곱하지 않는다(설계 원칙 1.1/1.2 — 유대는 절대 인플레이션하지 않음).
    ///
    /// [Lv.5에서 반드시 멈춘다 — 설계 원칙 1.3] 이미 최대 레벨이면 아예 더하지 않고 조용히 반환한다.
    /// 무한 성장을 허용하면 "한 명에게 몰아주기"가 최적 전략이 되어 나머지 꽃이 방치되는데, 수집
    /// 게임에서는 그게 치명적이라 상한을 절대적인 것으로 취급한다.
    /// </summary>
    public void AddBond(FlowerInstance flower, double amount)
    {
        if (flower == null || amount <= 0) return;

        BondData bd = ActiveBondData;
        if (flower.bondLevel >= bd.maxBondLevel) return; // Lv.5 도달 후에는 더 쌓지 않는다

        flower.bond += amount;

        // while로 처리 — 임계치 조정이 잦은 프로젝트라, 한 번의 호출로 여러 레벨이 한꺼번에
        // 오르는 경우(정원의 초당 누적 등)를 방어해 둔다.
        while (flower.bondLevel < bd.maxBondLevel &&
               flower.bondLevel < bd.thresholds.Count &&
               flower.bond >= bd.thresholds[flower.bondLevel])
        {
            flower.bond -= bd.thresholds[flower.bondLevel];
            flower.bondLevel++;
            OnBondLevelUp?.Invoke(flower.flowerId, flower.bondLevel);
        }

        // 최대 레벨에 도달한 뒤 남은 잉여분은 어차피 다시는 쓰이지 않는다(다음 호출은 위의 이른
        // 반환으로 막힘) — UI가 "MAX" 대신 어중간한 잔여 숫자를 보여주지 않도록 여기서 비워둔다.
        if (flower.bondLevel >= bd.maxBondLevel) flower.bond = 0;

        if (flower.flowerId == currentDisplayedFlowerId)
            OnDisplayedFlowerChanged?.Invoke(currentDisplayedFlowerId);
    }

    /// <summary>
    /// 해당 꽃에 "읽지 않은, 이미 해금된" 메모리얼이 하나라도 있는지 — 도감 그리드 뱃지/메인화면
    /// 뱃지 판정용. 미보유·미개화 꽃은 애초에 유대를 쌓을 수 없으므로 항상 false.
    /// </summary>
    public bool HasUnreadMemorial(string flowerId)
    {
        FlowerInstance instance = GetInstance(flowerId);
        FlowerData data = GetFlowerData(flowerId);
        if (instance == null || data == null || !instance.isBloomed) return false;

        for (int level = 1; level <= instance.bondLevel; level++)
        {
            MemorialData memorial = data.GetMemorialForBondLevel(level);
            if (memorial == null) continue; // 이 레벨엔 애초에 메모리얼이 없음(Lv.4·5 등)

            bool alreadyRead = instance.readMemorialBondLevels != null &&
                                instance.readMemorialBondLevels.Contains(level);
            if (!alreadyRead) return true;
        }
        return false;
    }

    /// <summary> 메모리얼 1편을 읽음 처리한다(MemorialViewPanel이 본문을 열 때 호출). </summary>
    public void MarkMemorialRead(string flowerId, int bondLevel)
    {
        FlowerInstance instance = GetInstance(flowerId);
        if (instance == null) return;

        if (instance.readMemorialBondLevels == null) instance.readMemorialBondLevels = new List<int>();
        if (instance.readMemorialBondLevels.Contains(bondLevel)) return;

        instance.readMemorialBondLevels.Add(bondLevel);
        OnOwnedFlowersChanged?.Invoke(); // 도감 그리드 뱃지 등 구독자 갱신
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

    // ===================================================================
    // 개발자 치트 — 핵심 3개(개화/레벨/유대 레벨)는 CheatPanel(런타임, 설정 화면에서 비밀번호로
    // 잠금)에서도 써야 해서 항상 컴파일된다. 상태만 조작하고, 정상 경로(AddAffection)를 우회하지
    // 않는다 — 특히 개화는 반드시 AddAffection을 태워서 패시브 활성화 등 부수 처리가 누락되지
    // 않게 한다. 이 셋 외의 "전체 일괄" 계열은 여전히 에디터 전용(CheatMenuWindow)이다.
    // ===================================================================

    /// <summary>
    /// 치트 — 지정한 보유·미개화 꽃의 애정을 요구치까지 채워 정상 개화 경로(AddAffection)를 그대로
    /// 태운다. isBloomed를 직접 대입하지 않는 이유는 클래스 상단 요청 지시서 참고 — 패시브 활성화
    /// 등 부수 처리가 누락되면 안 되기 때문이다.
    /// </summary>
    public bool Cheat_ForceBloom(string flowerId)
    {
        FlowerInstance instance = GetInstance(flowerId);
        FlowerData data = GetFlowerData(flowerId);
        if (instance == null || data == null || instance.isBloomed) return false;

        double remaining = Math.Max(1, data.requiredAffection - instance.currentAffection);
        AddAffection(instance, data, remaining);
        return instance.isBloomed;
    }

    /// <summary> 치트 — 개화한 꽃의 레벨을 직접 설정한다(골드 소모 없음). 미개화 꽃은 무시. </summary>
    public void Cheat_SetFlowerLevel(string flowerId, int level)
    {
        FlowerInstance instance = GetInstance(flowerId);
        if (instance == null || !instance.isBloomed) return;

        instance.currentLevel = Math.Max(1, level);
        if (flowerId == currentDisplayedFlowerId) OnDisplayedFlowerChanged?.Invoke(flowerId);
    }

    /// <summary>
    /// 치트 — 유대 레벨을 직접 설정한다. bond(현재 레벨 구간 누적치)를 0으로 초기화해 다음 레벨업
    /// 판정이 어긋나지 않게 한다(요청 지시서 명시 사항). 메모리얼 해금은 bondLevel만으로 판정되므로
    /// (FlowerManager.HasUnreadMemorial 참고) 별도 플래그 갱신이 필요 없다. OnBondLevelUp을 그대로
    /// 발생시켜야 GardenManager의 인접 효과 캐시도 함께 갱신된다(인접 효과 검증이 이 치트의 목적이므로
    /// 이 이벤트를 생략하면 안 된다).
    /// </summary>
    public void Cheat_SetFlowerBondLevel(string flowerId, int bondLevel)
    {
        FlowerInstance instance = GetInstance(flowerId);
        if (instance == null) return;

        int clamped = Mathf.Clamp(bondLevel, 0, ActiveBondData.maxBondLevel);
        instance.bondLevel = clamped;
        instance.bond = 0;
        OnBondLevelUp?.Invoke(flowerId, clamped);
        if (flowerId == currentDisplayedFlowerId) OnDisplayedFlowerChanged?.Invoke(flowerId);
    }

#if UNITY_EDITOR
    // ===================================================================
    // 개발자 치트 (에디터 전용, 나머지) — Tools > 꽃소녀 치트(CheatMenuWindow)에서만 호출한다.
    // 전부 #if UNITY_EDITOR로 감싸 릴리스 빌드에는 포함되지 않는다.
    // ===================================================================

    /// <summary> 치트 — 씨앗 비용을 무시하고 미보유 꽃을 전부 보유 상태로 만든다. </summary>
    public void Cheat_OwnAllFlowers()
    {
        foreach (FlowerData data in allFlowers)
        {
            if (data == null || ownedFlowers.ContainsKey(data.flowerId)) continue;
            ownedFlowers[data.flowerId] = new FlowerInstance(data.flowerId);
        }
        OnOwnedFlowersChanged?.Invoke();

        if (string.IsNullOrEmpty(currentDisplayedFlowerId))
        {
            List<string> owned = GetOwnedIdsInDexOrder();
            if (owned.Count > 0) SetDisplayedFlower(owned[0]);
        }
    }

    /// <summary> 치트 — 보유한 모든 미개화 꽃을 Cheat_ForceBloom으로 개화시킨다. </summary>
    public void Cheat_ForceBloomAllOwned()
    {
        foreach (string id in GetOwnedIdsInDexOrder().ToList())
            Cheat_ForceBloom(id);
    }

    /// <summary> 치트 — 보유한 모든 개화 꽃의 레벨을 일괄 설정한다. </summary>
    public void Cheat_SetAllLevels(int level)
    {
        foreach (string id in GetOwnedIdsInDexOrder())
            Cheat_SetFlowerLevel(id, level);
    }

    /// <summary> 치트 — 보유한 모든 꽃의 유대 레벨을 일괄 설정한다. </summary>
    public void Cheat_SetAllBondLevels(int bondLevel)
    {
        foreach (string id in GetOwnedIdsInDexOrder())
            Cheat_SetFlowerBondLevel(id, bondLevel);
    }
#endif
}