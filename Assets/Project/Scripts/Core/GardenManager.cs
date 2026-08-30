using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 정원(격자 배치 + 인접 효과 + 시듦)을 관리하는 싱글턴. FlowerManager와 동일한 패턴 —
/// FlowerManager.GetEffectiveGoldPerSecond가 이 클래스에 "이 꽃의 정원 배율이 얼마인지"만
/// 물어보고, 격자/인접/시듦 로직을 직접 알지 않는다(단일 책임 분리).
///
/// [설계 원칙 — 반드시 지킬 것]
/// 1. 정원은 생산 게이트가 아니다 — 배치 안 한 꽃도 기본 G/s는 정상 생산한다. 이 클래스가 제공하는
///    배율은 "곱해서 늘려주는" 보너스일 뿐, 배치 여부가 기본값에 곱셈 이하로 작용해서는 안 된다
///    (미배치 = 배율 1.0, 절대 1.0 미만이 아님).
/// 2. 유대 획득 속도는 절대 올리지 않는다 — bondPerSecondInGarden은 고정값이고, 여기에 어떤
///    배율도 곱하지 않는다(BondData/AddBond와 동일 원칙).
/// 3. 시듦은 인접 효과 "보너스분"에만 곱해진다 — 기본 G/s, 레벨업 G/s, 유대 배율, 개화 진행,
///    유대 획득 그 무엇에도 관여하지 않는다. GetGoldMultiplierForFlower의 수식(1 + fraction*wilt)이
///    이걸 구조적으로 보장한다(fraction=0이면 wilt를 얼마를 곱하든 결과는 항상 1).
/// </summary>
public class GardenManager : MonoBehaviour
{
    public static GardenManager Instance { get; private set; }

    [Header("정원 밸런스 데이터 — 비워두면 GardenData 기본값으로 자동 대체됨 (필수 아님)")]
    public GardenData gardenData;
    private GardenData runtimeDefaultGardenData;

    [Header("꾸미기 — 전체 장식 목록 (에셋 0개여도 정상 동작해야 함)")]
    public List<DecorationData> allDecorations = new List<DecorationData>();

    /// <summary> 정원 상태(배치/확장/시듦 회복 등)가 바뀔 때 발생. UI 갱신용. </summary>
    public event Action OnGardenChanged;

    public const int MaxPresetSlots = 3;
    public List<GardenPreset> presets = new List<GardenPreset>();

    private int currentSizeIndex;
    private readonly Dictionary<string, Vector2Int> flowerPlacements = new Dictionary<string, Vector2Int>();
    private readonly Dictionary<string, int> tileTouchCounts = new Dictionary<string, int>();
    private long lastTendedTimeTicksUtc;

    private readonly HashSet<string> ownedDecorationIds = new HashSet<string>();
    private readonly Dictionary<string, Vector2Int> decorationPlacements = new Dictionary<string, Vector2Int>();

    /// <summary> flowerId -> "인접 효과 보너스 비율"(예: 0.18 = +18%). 배치 변경 시에만 재계산되는 캐시.
    /// GetGoldMultiplierForFlower(실제 G/s 계산)의 유일한 소스 — UI 표시용 계산은 이 값과
    /// incomingContributionsCache/outgoingEffectCache를 "같은 재계산 루프"에서 함께 만들어진 것만
    /// 읽는다(정원 인접 효과 시각화 UI 지시서 — 표시용 계산과 실제 계산 분리 금지). </summary>
    private readonly Dictionary<string, float> adjacencyBonusFractionCache = new Dictionary<string, float>();

    /// <summary> flowerId -> "받고 있는 효과" 내역(표시 전용). </summary>
    private readonly Dictionary<string, List<AdjacencyContribution>> incomingContributionsCache = new Dictionary<string, List<AdjacencyContribution>>();
    /// <summary> flowerId -> "주는 효과" 정보(표시 전용). </summary>
    private readonly Dictionary<string, OutgoingEffectInfo> outgoingEffectCache = new Dictionary<string, OutgoingEffectInfo>();

    private static readonly Vector2Int[] FourDirections =
    {
        new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1)
    };

    public GardenData ActiveGardenData
    {
        get
        {
            if (gardenData != null) return gardenData;
            if (runtimeDefaultGardenData == null) runtimeDefaultGardenData = ScriptableObject.CreateInstance<GardenData>();
            return runtimeDefaultGardenData;
        }
    }

    public int Width => GetSizeOrDefault().width;
    public int Height => GetSizeOrDefault().height;
    public int CurrentSizeIndex => currentSizeIndex;

    private GardenData.GardenSize GetSizeOrDefault()
    {
        List<GardenData.GardenSize> sizes = ActiveGardenData.sizes;
        if (sizes != null && currentSizeIndex >= 0 && currentSizeIndex < sizes.Count) return sizes[currentSizeIndex];
        return new GardenData.GardenSize { width = 3, height = 2, unlockCost = 0 };
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // 세이브 불러오기(LoadFromSaveData)와 실행 순서가 어느 쪽이든 안전하다 — 이미 로드된 값(0보다
        // 큼)이면 건드리지 않고, 아직 한 번도 안 채워졌으면(최초 실행) 지금 시각으로 "방금 손질함"
        // 상태를 만든다. FlowerManager의 튜토리얼 지급 로직과 동일한 순서 무관 패턴.
        if (lastTendedTimeTicksUtc <= 0)
            lastTendedTimeTicksUtc = DateTime.UtcNow.Ticks;

        // [Awake/OnEnable이 아니라 Start에서 구독하는 이유] Unity는 "모든 오브젝트의 Awake가 끝난
        // 뒤에야 Start가 호출된다"만 보장하고, 서로 다른 오브젝트 간 Awake/OnEnable의 순서는
        // 보장하지 않는다. 만약 OnEnable에서 구독했다면 FlowerManager.Awake()가 아직 Instance를
        // 채우기 전에 이 OnEnable이 먼저 실행되어 조용히 구독을 건너뛸 수 있다 — SaveManager 등
        // 이 프로젝트의 다른 모든 이벤트 구독이 Start()를 쓰는 것과 같은 이유다.
        if (FlowerManager.Instance != null)
            FlowerManager.Instance.OnBondLevelUp += HandleBondLevelUp;
    }

    private void OnDestroy()
    {
        if (FlowerManager.Instance != null)
            FlowerManager.Instance.OnBondLevelUp -= HandleBondLevelUp;
    }

    // ===================================================================
    // 배치 (작업 1.2)
    // ===================================================================

    public bool IsInsideGrid(Vector2Int cell) => cell.x >= 0 && cell.x < Width && cell.y >= 0 && cell.y < Height;

    public bool IsPlaced(string flowerId) => flowerPlacements.ContainsKey(flowerId);
    public Vector2Int? GetPlacementOrigin(string flowerId) =>
        flowerPlacements.TryGetValue(flowerId, out Vector2Int origin) ? origin : (Vector2Int?)null;
    public IEnumerable<string> GetPlacedFlowerIds() => flowerPlacements.Keys;
    /// <summary> 배치된 "종" 수(폴리오미노 크기와 무관 — flowerPlacements의 키 개수). 정원 요약의
    /// "N/M칸" 표시에는 쓰지 않는다(칸 수와 종 수가 다르기 때문) — OccupiedCellCount를 대신 쓴다. </summary>
    public int PlacedFlowerCount => flowerPlacements.Count;

    /// <summary>
    /// 지금 격자에서 실제로 차 있는 칸의 총 개수(폴리오미노 크기 반영) — 정원 요약의 "N/M칸" 표시가
    /// 이 값을 써야 한다. PlacedFlowerCount(종 수)와 혼동하지 말 것 — 해바라기(1×3) 하나만 배치돼도
    /// PlacedFlowerCount는 1이지만 이 값은 3이다.
    /// </summary>
    public int OccupiedCellCount
    {
        get
        {
            int total = 0;
            foreach (var kvp in flowerPlacements)
            {
                FlowerData data = FlowerManager.Instance != null ? FlowerManager.Instance.GetFlowerData(kvp.Key) : null;
                if (data == null) { total += 1; continue; }
                total += data.GetOccupiedCells(kvp.Value).Count();
            }
            return total;
        }
    }

    /// <summary> origin에 data를 놓을 수 있는지 — 격자 밖이거나 이미 다른 꽃이 있으면 false. </summary>
    public bool CanPlaceAt(FlowerData data, Vector2Int origin)
    {
        if (data == null) return false;
        foreach (Vector2Int cell in data.GetOccupiedCells(origin))
        {
            if (!IsInsideGrid(cell)) return false;
            if (GetFlowerIdAtCell(cell) != null) return false;
        }
        return true;
    }

    /// <summary> 해당 칸을 점유 중인 꽃의 id(없으면 null) — 정원 UI가 격자를 그릴 때 쓴다. </summary>
    public string GetFlowerIdAt(Vector2Int cell) => GetFlowerIdAtCell(cell);

    private string GetFlowerIdAtCell(Vector2Int cell)
    {
        foreach (var kvp in flowerPlacements)
        {
            FlowerData data = FlowerManager.Instance != null ? FlowerManager.Instance.GetFlowerData(kvp.Key) : null;
            if (data == null) continue;
            foreach (Vector2Int occ in data.GetOccupiedCells(kvp.Value))
                if (occ == cell) return kvp.Key;
        }
        return null;
    }

    /// <summary>
    /// 개화한 꽃만, 한 꽃은 1개체만, 쿨다운·비용 없이 자유롭게 배치한다(요청 명세 1.2).
    /// 이미 배치된 꽃을 다른 자리로 옮기려면 TryMoveFlower를 쓴다.
    /// </summary>
    public bool TryPlaceFlower(string flowerId, Vector2Int origin)
    {
        if (flowerPlacements.ContainsKey(flowerId)) return false;

        FlowerInstance instance = FlowerManager.Instance != null ? FlowerManager.Instance.GetInstance(flowerId) : null;
        FlowerData data = FlowerManager.Instance != null ? FlowerManager.Instance.GetFlowerData(flowerId) : null;
        if (instance == null || data == null || !instance.isBloomed) return false;
        if (!CanPlaceAt(data, origin)) return false;

        flowerPlacements[flowerId] = origin;
        if (!tileTouchCounts.ContainsKey(flowerId)) tileTouchCounts[flowerId] = 0;

        RecomputeAdjacencyCache();
        OnGardenChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// UI에서 격자의 "칸 하나"를 클릭했을 때 쓰는 진입점. TryPlaceFlower(flowerId, origin)은 origin을
    /// 곧 모양의 (0,0) 기준 칸으로 그대로 쓰기 때문에, 세로/가로로 긴 모양은 그 기준 칸(보통 맨
    /// 위/맨 왼쪽)을 정확히 눌러야만 배치되고, 칸이 남아도는데도 다른 칸을 누르면 실패하는 문제가
    /// 있었다(신고된 버그: 해바라기 세로 3칸 중 2·3번째 칸을 누르면 공간이 있어도 안 들어감).
    ///
    /// 그래서 클릭한 칸이 모양의 "어느 칸"이 되어도 되도록, 모양에 저장된 칸 순서대로(정규화 때
    /// 위→아래, 왼→오 순으로 정렬돼 있음) 후보 원점을 하나씩 시도해 처음으로 유효한 배치를 채택한다.
    /// 즉 클릭한 칸을 그대로 원점으로 쓰는 시도(기존 동작과 동일)를 가장 먼저 해보고, 그게 격자
    /// 밖으로 나가거나 겹치면 위/왼쪽으로 한 칸씩 당겨보며 "클릭한 칸을 포함하면서 들어갈 수 있는"
    /// 자리를 찾는다 — 이론상 들어갈 수 있는 자리가 있으면 어디를 눌러도 배치되게 하기 위함.
    /// </summary>
    public bool TryPlaceFlowerAtCell(string flowerId, Vector2Int clickedCell)
    {
        FlowerData data = FlowerManager.Instance != null ? FlowerManager.Instance.GetFlowerData(flowerId) : null;
        if (data == null) return false;

        Vector2Int? origin = ResolveOriginForCell(data, clickedCell);
        return origin.HasValue && TryPlaceFlower(flowerId, origin.Value);
    }

    /// <summary>
    /// 클릭/드롭한 칸이 모양의 "어느 칸"이 되어도 되도록, 모양에 저장된 칸 순서대로(정규화 때
    /// 위→아래, 왼→오 순으로 정렬돼 있음) 후보 원점을 하나씩 시도해 처음으로 유효한 원점을 반환한다.
    /// TryPlaceFlowerAtCell/TryMoveFlowerAtCell/Simulate*TotalGoldPerSecond가 전부 공유한다 —
    /// 배치 판정 로직이 여러 곳에 따로 복제되면 그중 하나만 고치고 잊어버리는 실수가 생기기 쉽다.
    /// </summary>
    private Vector2Int? ResolveOriginForCell(FlowerData data, Vector2Int cell)
    {
        List<Vector2Int> shape = (data.gardenShape != null && data.gardenShape.Count > 0)
            ? data.gardenShape
            : new List<Vector2Int> { Vector2Int.zero };

        foreach (Vector2Int offset in shape)
        {
            Vector2Int candidate = cell - offset;
            if (CanPlaceAt(data, candidate)) return candidate;
        }
        return null;
    }

    public bool RemoveFlower(string flowerId)
    {
        if (!flowerPlacements.Remove(flowerId)) return false;

        tileTouchCounts.Remove(flowerId);
        RecomputeAdjacencyCache();
        OnGardenChanged?.Invoke();
        return true;
    }

    /// <summary> 배치된 꽃을 다른 칸으로 옮긴다(실패 시 원래 자리 그대로 유지). </summary>
    public bool TryMoveFlower(string flowerId, Vector2Int newOrigin)
    {
        if (!flowerPlacements.TryGetValue(flowerId, out Vector2Int oldOrigin))
            return TryPlaceFlower(flowerId, newOrigin);

        FlowerData data = FlowerManager.Instance != null ? FlowerManager.Instance.GetFlowerData(flowerId) : null;
        if (data == null) return false;

        flowerPlacements.Remove(flowerId); // 자기 자신과 겹침 판정이 안 나게 임시로 빼둔다
        if (!CanPlaceAt(data, newOrigin))
        {
            flowerPlacements[flowerId] = oldOrigin;
            return false;
        }

        flowerPlacements[flowerId] = newOrigin;
        RecomputeAdjacencyCache();
        OnGardenChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 드래그 앤 드롭 전용 진입점(TryPlaceFlowerAtCell의 이동판) — 드롭한 칸이 모양의 정확한
    /// 기준 칸이 아니어도, ResolveOriginForCell로 후보를 찾아 그 자리로 옮긴다. 실패하면(놓을 자리가
    /// 없으면) 원래 자리 그대로 유지한다.
    /// </summary>
    public bool TryMoveFlowerAtCell(string flowerId, Vector2Int droppedCell)
    {
        if (!flowerPlacements.ContainsKey(flowerId)) return TryPlaceFlowerAtCell(flowerId, droppedCell);

        FlowerData data = FlowerManager.Instance != null ? FlowerManager.Instance.GetFlowerData(flowerId) : null;
        if (data == null) return false;

        Vector2Int oldOrigin = flowerPlacements[flowerId];
        flowerPlacements.Remove(flowerId); // 자기 자신과 겹침 판정이 안 나게 임시로 빼둔다

        Vector2Int? origin = ResolveOriginForCell(data, droppedCell);
        if (!origin.HasValue)
        {
            flowerPlacements[flowerId] = oldOrigin;
            return false;
        }

        flowerPlacements[flowerId] = origin.Value;
        RecomputeAdjacencyCache();
        OnGardenChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 작업 4(배치 전 미리보기) 전용. flowerId를 실제로 그 칸에 놓아본 뒤(TryPlaceFlowerAtCell과 완전히
    /// 같은 경로 → RecomputeAdjacencyCache → 총 G/s 조회) 그 결과를 얻고, 즉시 원래 상태로 되돌린다.
    /// 같은 프레임 안에서 동기적으로 끝나므로 다른 시스템이 이 임시 상태를 관측하지 않고, OnGardenChanged도
    /// 발생시키지 않는다(진짜로 바뀐 게 아니므로 다른 UI가 갱신될 이유가 없다). "표시용 계산과 실제 계산
    /// 분리 금지" 원칙을 지키기 위해 새 산식을 만들지 않고 실제 배치 경로를 그대로 재사용한다 — 그래서
    /// 이 미리보기 값은 실제로 배치했을 때의 값과 항상 정확히 같다. 놓을 수 없는 자리면(격자 밖/겹침/
    /// 미개화 등) 변화 없음을 뜻하는 "지금 총 G/s"를 그대로 반환한다.
    /// </summary>
    public BigNumber SimulatePlacementTotalGoldPerSecond(string flowerId, Vector2Int clickedCell)
    {
        if (FlowerManager.Instance == null) return BigNumber.Zero;

        BigNumber currentTotal = FlowerManager.Instance.GetTotalGoldPerSecond();

        FlowerData data = FlowerManager.Instance.GetFlowerData(flowerId);
        if (data == null || flowerPlacements.ContainsKey(flowerId)) return currentTotal;

        Vector2Int? origin = ResolveOriginForCell(data, clickedCell);
        if (!origin.HasValue) return currentTotal;

        bool hadTouchEntry = tileTouchCounts.ContainsKey(flowerId);

        flowerPlacements[flowerId] = origin.Value;
        if (!hadTouchEntry) tileTouchCounts[flowerId] = 0;
        RecomputeAdjacencyCache();

        BigNumber simulated = FlowerManager.Instance.GetTotalGoldPerSecond();

        flowerPlacements.Remove(flowerId);
        if (!hadTouchEntry) tileTouchCounts.Remove(flowerId); // 시뮬레이션 이전 상태로 정확히 복원
        RecomputeAdjacencyCache();

        return simulated;
    }

    /// <summary>
    /// 드래그 앤 드롭 전용 — SimulatePlacementTotalGoldPerSecond의 이동판. 이미 배치된 꽃을 다른
    /// 칸으로 옮겼을 때의 결과를 미리 계산한다(TryMoveFlowerAtCell과 완전히 같은 경로). 아직 배치되지
    /// 않은 꽃이면 일반 배치 시뮬레이션과 동일하게 동작한다.
    /// </summary>
    public BigNumber SimulateMoveTotalGoldPerSecond(string flowerId, Vector2Int droppedCell)
    {
        if (FlowerManager.Instance == null) return BigNumber.Zero;

        BigNumber currentTotal = FlowerManager.Instance.GetTotalGoldPerSecond();

        if (!flowerPlacements.TryGetValue(flowerId, out Vector2Int oldOrigin))
            return SimulatePlacementTotalGoldPerSecond(flowerId, droppedCell);

        FlowerData data = FlowerManager.Instance.GetFlowerData(flowerId);
        if (data == null) return currentTotal;

        flowerPlacements.Remove(flowerId);
        Vector2Int? origin = ResolveOriginForCell(data, droppedCell);
        if (!origin.HasValue)
        {
            flowerPlacements[flowerId] = oldOrigin;
            return currentTotal;
        }

        flowerPlacements[flowerId] = origin.Value;
        RecomputeAdjacencyCache();
        BigNumber simulated = FlowerManager.Instance.GetTotalGoldPerSecond();

        flowerPlacements[flowerId] = oldOrigin;
        RecomputeAdjacencyCache();
        return simulated;
    }

    /// <summary>
    /// 표시 전용(드래그 중 모양+가능여부 미리보기) — hoveredCell에 flowerId를 놓으면 실제로 어느
    /// 원점에 놓이는지 계산한다. 이미 배치된 꽃(이동 중)이면 자기 자신과의 겹침 판정을 잠깐 빼고
    /// 계산한다 — TryMoveFlowerAtCell이 실제로 쓰는 것과 정확히 같은 ResolveOriginForCell을
    /// 재사용하므로, 이 미리보기가 가리키는 자리와 실제로 드롭했을 때 놓이는 자리가 항상 일치한다.
    /// 어댑턴시 캐시를 건드리지 않는 순수 기하 계산이라 매 호버마다 불러도 가볍다.
    /// </summary>
    public Vector2Int? PreviewResolvedOrigin(string flowerId, Vector2Int hoveredCell)
    {
        FlowerData data = FlowerManager.Instance != null ? FlowerManager.Instance.GetFlowerData(flowerId) : null;
        if (data == null) return null;

        if (!flowerPlacements.TryGetValue(flowerId, out Vector2Int oldOrigin))
            return ResolveOriginForCell(data, hoveredCell);

        flowerPlacements.Remove(flowerId);
        Vector2Int? resolved = ResolveOriginForCell(data, hoveredCell);
        flowerPlacements[flowerId] = oldOrigin;
        return resolved;
    }

    /// <summary> 다음 확장 단계로 골드를 지불하고 넓힌다. 이미 최대 크기면 실패. </summary>
    public bool TryExpandGarden()
    {
        List<GardenData.GardenSize> sizes = ActiveGardenData.sizes;
        if (sizes == null || currentSizeIndex + 1 >= sizes.Count) return false;
        if (GameManager.Instance == null) return false;

        GardenData.GardenSize next = sizes[currentSizeIndex + 1];
        if (!GameManager.Instance.TrySpendGold(next.unlockCost)) return false;

        currentSizeIndex++;
        RecomputeAdjacencyCache(); // 새로 생긴 빈칸이 해바라기류(SelfBoostPerEmptyNeighbor)에 영향을 줌
        OnGardenChanged?.Invoke();
        return true;
    }

    // ===================================================================
    // 인접 효과 (작업 2)
    // ===================================================================

    /// <summary>
    /// 배치가 바뀔 때만 호출된다(요청 명세 2.5 — 매 프레임 격자 전체를 순회하지 않는다).
    /// 계산 순서(명세 2.4): (1) 증폭 미적용 기본값 산출 → (2) 증폭 적용 → (3) 증폭끼리는 서로
    /// 증폭하지 않음 → (4) 결과를 캐시에 저장, GetGoldMultiplierForFlower가 소비한다.
    /// </summary>
    private void RecomputeAdjacencyCache()
    {
        // 세 캐시 전부 여기서 먼저 비운다 — 이전 값이 남아있으면 다음 배치 변경 때 값이 누적되는
        // 클래스의 버그가 생긴다(지시서가 명시적으로 경고한 문제 유형). 재계산 때마다 항상 빈
        // 상태에서 처음부터 다시 쌓아 올린다.
        adjacencyBonusFractionCache.Clear();
        incomingContributionsCache.Clear();
        outgoingEffectCache.Clear();
        if (FlowerManager.Instance == null || flowerPlacements.Count == 0) return;

        // 각 배치된 꽃의 "고유 이웃 꽃 집합"과 "인접한 빈 칸 수"를 미리 구해둔다 — 같은 이웃이 여러
        // 칸에서 맞닿아도 1회만 세기 위함(HashSet), 그리고 빈 칸도 중복 없이 세기 위함.
        Dictionary<string, HashSet<string>> neighborsOf = new Dictionary<string, HashSet<string>>();
        Dictionary<string, int> emptyNeighborCountOf = new Dictionary<string, int>();

        foreach (var kvp in flowerPlacements)
        {
            string flowerId = kvp.Key;
            FlowerData data = FlowerManager.Instance.GetFlowerData(flowerId);
            if (data == null) continue;

            HashSet<string> neighborIds = new HashSet<string>();
            HashSet<Vector2Int> emptyNeighborCells = new HashSet<Vector2Int>();

            foreach (Vector2Int cell in data.GetOccupiedCells(kvp.Value))
            {
                foreach (Vector2Int dir in FourDirections)
                {
                    Vector2Int nCell = cell + dir;
                    if (!IsInsideGrid(nCell)) continue;

                    string nId = GetFlowerIdAtCell(nCell);
                    if (nId == null) emptyNeighborCells.Add(nCell);
                    else if (nId != flowerId) neighborIds.Add(nId);
                }
            }

            neighborsOf[flowerId] = neighborIds;
            emptyNeighborCountOf[flowerId] = emptyNeighborCells.Count;
        }

        // ── 1단계: 증폭 미적용 기본 보너스 비율 (+ 표시용 breakdown을 같은 루프에서 함께 기록) ──
        Dictionary<string, float> baseFraction = new Dictionary<string, float>();
        foreach (string id in flowerPlacements.Keys)
        {
            baseFraction[id] = 0f;
            incomingContributionsCache[id] = new List<AdjacencyContribution>();
        }

        List<string> amplifierIds = new List<string>();

        foreach (string sourceId in flowerPlacements.Keys)
        {
            FlowerInstance sourceInstance = FlowerManager.Instance.GetInstance(sourceId);
            FlowerData sourceData = FlowerManager.Instance.GetFlowerData(sourceId);
            if (sourceInstance == null || sourceData == null) continue;

            AdjacencyEffectData effect = sourceData.adjacencyEffect;
            // 유대 위력(0~1) — 예전엔 "Lv.5 도달 시에만 발동"(0 또는 100%)이었지만, 유대 레벨에
            // 비례해 단계적으로 강해지도록 바뀌었다(요청 사항). 아래에서 "기본 보너스"에만 이 값을
            // 곱한다 — 절대 최종 배율(1+보너스)에 곱하지 않는다(그러면 1 미만이 되어 기본 G/s까지
            // 깎이므로, "시듦은 보너스분에만" 원칙과 동일한 이유로 반드시 지켜야 한다).
            float power = FlowerManager.Instance.GetAdjacencyPower(sourceInstance.bondLevel);
            OutgoingEffectInfo outgoing = new OutgoingEffectInfo
            {
                description = effect != null ? effect.GetDescription() : "",
                isActive = effect != null && effect.type != AdjacencyEffectType.None && power > 0f
            };
            outgoingEffectCache[sourceId] = outgoing;

            if (!outgoing.isActive) continue; // 위력이 0(보통 유대 Lv.0)이면 아직 아무 효과도 없음

            HashSet<string> neighbors = neighborsOf.TryGetValue(sourceId, out var n) ? n : new HashSet<string>();
            string sourceLabel = sourceData.displayName;

            // neighborId/sourceId/targetId는 전부 flowerPlacements.Keys에서 나온 값이라, 이 시점에
            // baseFraction/emptyNeighborCountOf는 이미 모든 배치된 꽃에 대해 0(또는 실제 빈칸 수)으로
            // 채워져 있다(위 초기화 루프 참고) — 그래서 GetValueOrDefault 없이 바로 인덱서로 읽고
            // 쓴다(이 API의 .NET 버전 가용성을 따질 필요가 없어 더 안전하다).
            switch (effect.type)
            {
                case AdjacencyEffectType.BoostAllNeighborsFlat:
                    foreach (string neighborId in neighbors)
                    {
                        float scaled = effect.primaryValue * power;
                        baseFraction[neighborId] = baseFraction[neighborId] + scaled;
                        incomingContributionsCache[neighborId].Add(new AdjacencyContribution
                        {
                            sourceFlowerId = sourceId, label = $"{sourceLabel} 인접 보너스",
                            amount = scaled, rawAmount = effect.primaryValue,
                            sourceBondLevel = sourceInstance.bondLevel, power = power, isAmplification = false
                        });
                        outgoing.targetFlowerIds.Add(neighborId);
                    }
                    break;

                case AdjacencyEffectType.SelfPlusPerNeighbor:
                {
                    float rawAmount = effect.primaryValue + effect.secondaryValue * neighbors.Count;
                    float scaled = rawAmount * power;
                    baseFraction[sourceId] = baseFraction[sourceId] + scaled;
                    incomingContributionsCache[sourceId].Add(new AdjacencyContribution
                    {
                        sourceFlowerId = sourceId, label = "자가 보너스",
                        amount = scaled, rawAmount = rawAmount,
                        sourceBondLevel = sourceInstance.bondLevel, power = power, isAmplification = false
                    });
                    outgoing.targetFlowerIds.Add(sourceId);
                    break;
                }

                case AdjacencyEffectType.BoostHighestBondNeighbor:
                case AdjacencyEffectType.BoostLowestBondNeighbor:
                {
                    string targetId = null;
                    bool wantHighest = effect.type == AdjacencyEffectType.BoostHighestBondNeighbor;
                    int bestBond = wantHighest ? int.MinValue : int.MaxValue;

                    foreach (string neighborId in neighbors)
                    {
                        FlowerInstance ni = FlowerManager.Instance.GetInstance(neighborId);
                        if (ni == null) continue;
                        bool better = wantHighest ? ni.bondLevel > bestBond : ni.bondLevel < bestBond;
                        if (better) { bestBond = ni.bondLevel; targetId = neighborId; }
                    }

                    if (targetId != null)
                    {
                        float rawAmount = effect.primaryValue - 1f;
                        float scaled = rawAmount * power;
                        baseFraction[targetId] = baseFraction[targetId] + scaled;
                        incomingContributionsCache[targetId].Add(new AdjacencyContribution
                        {
                            sourceFlowerId = sourceId, label = $"{sourceLabel} 인접 보너스",
                            amount = scaled, rawAmount = rawAmount,
                            sourceBondLevel = sourceInstance.bondLevel, power = power, isAmplification = false
                        });
                        outgoing.targetFlowerIds.Add(targetId);
                    }
                    break;
                }

                case AdjacencyEffectType.SelfBoostPerEmptyNeighbor:
                {
                    int emptyCount = emptyNeighborCountOf.TryGetValue(sourceId, out var e) ? e : 0;
                    float rawAmount = effect.primaryValue * emptyCount;
                    float scaled = rawAmount * power;
                    baseFraction[sourceId] = baseFraction[sourceId] + scaled;
                    incomingContributionsCache[sourceId].Add(new AdjacencyContribution
                    {
                        sourceFlowerId = sourceId, label = $"빈 칸 {emptyCount}개 보너스",
                        amount = scaled, rawAmount = rawAmount,
                        sourceBondLevel = sourceInstance.bondLevel, power = power, isAmplification = false
                    });
                    outgoing.targetFlowerIds.Add(sourceId);
                    break;
                }

                case AdjacencyEffectType.AmplifyNeighborsAdjacency:
                    amplifierIds.Add(sourceId); // 증폭은 2단계에서 처리(targetFlowerIds도 그때 확정)
                    break;
            }
        }

        // ── 2단계: 증폭 적용. 3단계 규칙(증폭끼리는 서로 증폭 안 함)은 amplifierIds 제외로 구현 ──
        Dictionary<string, float> finalFraction = new Dictionary<string, float>(baseFraction);

        foreach (string ampId in amplifierIds)
        {
            FlowerInstance ampInstance = FlowerManager.Instance.GetInstance(ampId);
            FlowerData ampData = FlowerManager.Instance.GetFlowerData(ampId);
            if (ampInstance == null || ampData == null) continue;

            float ampPower = FlowerManager.Instance.GetAdjacencyPower(ampInstance.bondLevel);
            if (ampPower <= 0f) continue; // 증폭 소스 자신의 유대 위력이 0이면 증폭도 발동하지 않는다

            float rawAmplifyMultiplier = 1f + ampData.adjacencyEffect.primaryValue;
            float amplifyMultiplier = 1f + ampData.adjacencyEffect.primaryValue * ampPower;
            string ampLabel = $"{ampData.displayName} 증폭";
            HashSet<string> neighbors = neighborsOf.TryGetValue(ampId, out var n) ? n : new HashSet<string>();
            List<string> actualTargets = new List<string>();

            foreach (string neighborId in neighbors)
            {
                if (amplifierIds.Contains(neighborId)) continue; // 증폭끼리는 서로 증폭하지 않는다

                finalFraction[neighborId] = finalFraction[neighborId] * amplifyMultiplier;
                incomingContributionsCache[neighborId].Add(new AdjacencyContribution
                {
                    sourceFlowerId = ampId, label = ampLabel,
                    amount = amplifyMultiplier, rawAmount = rawAmplifyMultiplier,
                    sourceBondLevel = ampInstance.bondLevel, power = ampPower, isAmplification = true
                });
                actualTargets.Add(neighborId);
            }

            if (outgoingEffectCache.TryGetValue(ampId, out OutgoingEffectInfo ampOutgoing))
                ampOutgoing.targetFlowerIds = actualTargets;
        }

        foreach (var kvp in finalFraction)
            adjacencyBonusFractionCache[kvp.Key] = kvp.Value;
    }

    /// <summary> 표시 전용 — 이 꽃이 "받고 있는 효과" 내역(없으면 빈 리스트, null 아님). </summary>
    public IReadOnlyList<AdjacencyContribution> GetIncomingContributions(string flowerId) =>
        incomingContributionsCache.TryGetValue(flowerId, out var list) ? list : EmptyContributions;
    private static readonly List<AdjacencyContribution> EmptyContributions = new List<AdjacencyContribution>();

    /// <summary> 표시 전용 — 이 꽃이 "주는 효과" 정보(배치 안 됐거나 캐시에 없으면 null). </summary>
    public OutgoingEffectInfo GetOutgoingEffectInfo(string flowerId) =>
        outgoingEffectCache.TryGetValue(flowerId, out var info) ? info : null;

    /// <summary>
    /// 표시 전용 — flowerId가 점유한 모든 칸 기준 4방향 인접 판정 칸 전체(격자 밖 제외, 점유 여부 무관).
    /// 격자 하이라이트(작업 2)가 "이 칸들이 인접으로 계산된다"는 것을 옅은 표시로 보여줄 때 쓴다.
    /// 배치와 무관한 순수 기하 계산이라 캐시하지 않고 그때그때 구해도 안전하다(선택 시 1회만 호출됨).
    /// </summary>
    public HashSet<Vector2Int> GetAdjacencyCheckCells(string flowerId)
    {
        var result = new HashSet<Vector2Int>();
        if (!flowerPlacements.TryGetValue(flowerId, out Vector2Int origin)) return result;

        FlowerData data = FlowerManager.Instance != null ? FlowerManager.Instance.GetFlowerData(flowerId) : null;
        if (data == null) return result;

        foreach (Vector2Int cell in data.GetOccupiedCells(origin))
            foreach (Vector2Int dir in FourDirections)
            {
                Vector2Int nCell = cell + dir;
                if (IsInsideGrid(nCell)) result.Add(nCell);
            }
        return result;
    }

    /// <summary>
    /// 지금 이 순간(라이브)의 정원 배율 — 온라인 계산·메인화면 표시용.
    /// GetEffectiveGoldPerSecond가 이 값을 곱해서 세 경로(온라인/오프라인/프리뷰) 전부에 반영한다.
    /// 배치 안 된 꽃은 항상 1(보너스도 페널티도 없음) — "정원은 생산 게이트가 아니다" 원칙.
    /// </summary>
    public float GetGoldMultiplierForFlower(string flowerId) =>
        GetGoldMultiplierForFlower(flowerId, GetElapsedSecondsSinceTended());

    /// <summary>
    /// elapsedSecondsSinceTended를 명시적으로 받는 오버로드 — 오프라인 정산이 "그 구간 시점"의
    /// 시듦 단계로 정확히 계산하기 위해 라이브 시각(DateTime.UtcNow) 대신 이 값을 쓴다.
    /// </summary>
    public float GetGoldMultiplierForFlower(string flowerId, double elapsedSecondsSinceTended)
    {
        if (!adjacencyBonusFractionCache.TryGetValue(flowerId, out float fraction) || fraction == 0f)
            return 1f;

        // 시듦은 "보너스분(fraction)"에만 곱한다 — 기본값 1은 절대 시듦의 영향을 받지 않는다.
        return 1f + fraction * GetGardenWiltSeverity(elapsedSecondsSinceTended);
    }

    private bool IsFlowerTileFullyTended(string flowerId)
    {
        int touches = tileTouchCounts.TryGetValue(flowerId, out int t) ? t : 0;
        return touches >= ActiveGardenData.touchesPerPlacedFlowerToFullyRestore;
    }

    /// <summary>
    /// 시듦 복구 사양(GARDEN_WILT_IMPLEMENTATION.md 3.4절) — 부분 복구를 "타일 전부를 채워야만
    /// 효과가 돌아오는" 전부-아니면-전무 방식 대신, 경과 시간 기준 배율(timeSeverity)에서 1.0(완전
    /// 회복)까지 "복구된 타일 수 / 전체 타일 수"에 비례해 선형으로 되돌아오게 한다 — 큰 정원일수록
    /// 전부-아니면-전무 방식의 부담이 커지기 때문이다(권장안 채택). 정원 전체에 동일하게 적용된다 —
    /// "손질"은 특정 꽃이 아니라 정원이라는 환경 전체를 대상으로 하는 행위이기 때문이다(작업 지시
    /// 3번의 대전제). GetGoldMultiplierForFlower/GetWiltSeverityForFlower/GetCurrentGlobalWiltMultiplier
    /// 셋 다 이 메서드 하나만 쓰도록 통일해서 세 표시가 서로 어긋나지 않게 한다.
    /// </summary>
    private float GetGardenWiltSeverity(double elapsedSecondsSinceTended)
    {
        float timeSeverity = ActiveGardenData.GetWiltMultiplier(elapsedSecondsSinceTended);
        float recoveredFraction = GetRecoveredTileFraction();
        return timeSeverity + (1f - timeSeverity) * recoveredFraction;
    }

    /// <summary> 부분 복구 비율 — 터치 요구치를 채운("완전히 복구된") 타일 수 / 배치된 타일 총수.
    /// 배치된 타일이 하나도 없으면 시들 것도 없으므로 1(완전 회복 취급)을 반환한다. </summary>
    public float GetRecoveredTileFraction()
    {
        if (flowerPlacements.Count == 0) return 1f;

        int recovered = 0;
        foreach (string flowerId in flowerPlacements.Keys)
            if (IsFlowerTileFullyTended(flowerId)) recovered++;

        return (float)recovered / flowerPlacements.Count;
    }

    /// <summary> 표시 전용 — 이 타일의 현재 손질(복구) 터치 누적 횟수. 배치 안 된 꽃은 0. </summary>
    public int GetTileTouchCount(string flowerId) => tileTouchCounts.TryGetValue(flowerId, out int t) ? t : 0;

    /// <summary>
    /// UI 진행도 표시("정원 손질 12/18") 전용 — 실제 터치 누적 횟수 합계 / 정원 전체를 완전히
    /// 복구하는 데 필요한 총 횟수(배치된 타일 수 × 타일당 필요 횟수). GetRecoveredTileFraction과
    /// 달리 "타일이 완전히 끝났는지"가 아니라 "지금까지 실제로 몇 번 터치했는지"를 그대로 보여줘서,
    /// 타일 하나를 절반쯤 손질한 진행도도 사라지지 않고 보인다.
    /// </summary>
    public (int done, int needed) GetTendingProgress()
    {
        int required = ActiveGardenData.touchesPerPlacedFlowerToFullyRestore;
        int needed = flowerPlacements.Count * required;

        int done = 0;
        foreach (string flowerId in flowerPlacements.Keys)
            done += Mathf.Min(GetTileTouchCount(flowerId), required);

        return (done, needed);
    }

    /// <summary> 표시 전용 — 시듦 적용 전의 순수 인접 효과 배율(1+fraction). 미배치/캐시 없음이면 1.
    /// 정원 요약의 "인접 효과 +N% → 시듦 적용 후 +M%" 표시(작업 지시 3번)가 "시듦 적용 전" 쪽을
    /// 계산할 때 쓴다 — GetGoldMultiplierForFlower는 이미 시듦이 반영된 값이라 따로 필요하다. </summary>
    public float GetRawAdjacencyMultiplierForFlower(string flowerId) =>
        adjacencyBonusFractionCache.TryGetValue(flowerId, out float fraction) ? 1f + fraction : 1f;

    /// <summary>
    /// 표시 전용 — 지금 이 순간 적용되는 시듦 심각도(0~1, 1=시듦 없음). 부분 복구가 반영된
    /// GetGardenWiltSeverity를 그대로 쓴다(정원 전체에 동일하게 적용됨 — 더 이상 "이 타일만" 완전히
    /// 손질됐는지로 갈리지 않는다). 배치 안 된 꽃은 애초에 시듦 대상이 아니므로 1.
    /// </summary>
    public float GetWiltSeverityForFlower(string flowerId)
    {
        if (!flowerPlacements.ContainsKey(flowerId)) return 1f;
        return GetGardenWiltSeverity(GetElapsedSecondsSinceTended());
    }

    /// <summary>
    /// 인접 효과 캐시를 지금 즉시 다시 계산한다. 평소엔 배치가 바뀔 때 자동으로 호출되므로 직접 부를
    /// 일이 없지만, "Reload Domain 비활성화" 상태로 Play 모드를 오래 켜둔 채 스크립트만 여러 번
    /// 고친 경우(이번 세션에서 반복됐던 상황) 코드는 최신이어도 이미 배치된 꽃들의 캐시는 마지막으로
    /// 배치가 바뀐 시점의 계산 결과 그대로 남아 있을 수 있다 — Play 모드를 새로 시작하지 않고도
    /// 최신 로직으로 다시 계산해보고 싶을 때 이 메서드(치트 메뉴의 "인접 효과 재계산" 버튼)를 쓴다.
    /// </summary>
    public void RecomputeNow() => RecomputeAdjacencyCache();

    // ===================================================================
    // 시듦 (작업 3)
    // ===================================================================

    /// <summary> 마지막으로 정원 전체가 손질 완료된 시각 이후 경과 초. 한 번도 안 썼으면 0(시들 것도 없음). </summary>
    public double GetElapsedSecondsSinceTended()
    {
        if (lastTendedTimeTicksUtc <= 0) return 0;
        DateTime last = new DateTime(lastTendedTimeTicksUtc, DateTimeKind.Utc);
        double elapsed = (DateTime.UtcNow - last).TotalSeconds;
        return elapsed > 0 ? elapsed : 0;
    }

    public string GetCurrentWiltStageLabel() => ActiveGardenData.GetWiltStageLabel(GetElapsedSecondsSinceTended());
    /// <summary> 부분 복구가 반영된 실제 시듦 배율(GetGardenWiltSeverity) — GetCurrentWiltStageLabel과
    /// 달리 이 값은 손질 진행도에 따라 "양호" 단계보다 실제로 더 나을 수 있다(그 반대는 없음 —
    /// 부분 복구는 항상 시간 기준 배율보다 같거나 낫게만 작용한다). </summary>
    public float GetCurrentGlobalWiltMultiplier() => GetGardenWiltSeverity(GetElapsedSecondsSinceTended());

    /// <summary>
    /// 지금 이 순간 정원에 실제 시듦 페널티가 있는지(배율&lt;1). 양호 유예 기간이거나, 방금 전체
    /// 복구를 완료해 손질 주기가 막 초기화된 직후라면 false다 — 이 시점엔 GetTendingProgress()가
    /// "0/N"을 돌려주는데(터치 카운트가 방금 비워졌으니까), 이걸 "다시 시들기 시작했다"로 오해하지
    /// 않도록 UI(정원 손질 진행도 표시, 칸의 손질N/M 표시, 시든 타일 색조)는 전부 이 값이 true일
    /// 때만 보여준다. 지금은 손질할 게 없다는 뜻이므로 진행도 UI 자체를 숨기는 게 맞다.
    /// </summary>
    public bool IsCurrentlyWilted() => GetCurrentGlobalWiltMultiplier() < 0.999f;

    /// <summary>
    /// 시든 타일(=배치된 꽃 1개체) 터치 — 요청 명세 3.4. 골드는 일반 터치와 동일하게 받지만
    /// 유대는 절대 쌓지 않는다(청소 행위이지 관계 형성이 아님). 배치 안 된 flowerId는 무시.
    /// </summary>
    public void TouchTile(string flowerId)
    {
        if (!flowerPlacements.ContainsKey(flowerId)) return;

        int required = ActiveGardenData.touchesPerPlacedFlowerToFullyRestore;
        int current = tileTouchCounts.TryGetValue(flowerId, out int t) ? t : 0;
        tileTouchCounts[flowerId] = Mathf.Min(current + 1, required);

        if (FlowerManager.Instance != null)
            FlowerManager.Instance.GrantExternalTouchGold();

        CheckIfFullyTendedAndResetCycle();
        OnGardenChanged?.Invoke();
    }

    /// <summary>
    /// 배치된 모든 타일이 필요 횟수만큼 손질됐으면, 정원 전체를 "방금 손질함" 상태로 되돌리고
    /// 다음 시듦 주기를 새로 시작한다. 배치가 하나도 없으면 항상 참(시들 게 없으므로).
    /// </summary>
    private void CheckIfFullyTendedAndResetCycle()
    {
        int required = ActiveGardenData.touchesPerPlacedFlowerToFullyRestore;
        foreach (string flowerId in flowerPlacements.Keys)
        {
            int touches = tileTouchCounts.TryGetValue(flowerId, out int t) ? t : 0;
            if (touches < required) return;
        }

        lastTendedTimeTicksUtc = DateTime.UtcNow.Ticks;
        tileTouchCounts.Clear();
        RecomputeAdjacencyCache();
    }

    // ===================================================================
    // 유대 레벨 변화 반영 (레벨이 오를 때마다 인접 효과 위력(GetAdjacencyPower)이 바뀌므로 재계산
    // 필요 — 예전엔 Lv.5 경계에서만 바뀌었지만, 이제 매 레벨업마다 위력이 달라진다)
    // 구독/해제 자체는 위 Start()/OnDestroy()에서 처리한다.
    // ===================================================================

    private void HandleBondLevelUp(string flowerId, int newBondLevel)
    {
        // 배치돼 있지 않은 꽃의 유대가 올라도 인접 효과 캐시엔 영향이 없으므로 조용히 무시한다.
        if (flowerPlacements.ContainsKey(flowerId))
            RecomputeAdjacencyCache();
    }

    // ===================================================================
    // 꾸미기 (작업 4 — 데이터 구조와 배치 기능만. 성능에 절대 관여하지 않음)
    // ===================================================================

    public DecorationData GetDecorationData(string id) => allDecorations.Find(d => d != null && d.decorationId == id);
    public bool IsDecorationOwned(string id) => ownedDecorationIds.Contains(id);
    public IEnumerable<string> GetPlacedDecorationIds() => decorationPlacements.Keys;
    public Vector2Int? GetDecorationOrigin(string id) =>
        decorationPlacements.TryGetValue(id, out Vector2Int origin) ? origin : (Vector2Int?)null;

    public bool TryPurchaseDecoration(string decorationId)
    {
        if (ownedDecorationIds.Contains(decorationId)) return false;

        DecorationData data = GetDecorationData(decorationId);
        if (data == null || GameManager.Instance == null) return false;
        if (!GameManager.Instance.TrySpendGold(data.goldCost)) return false;

        ownedDecorationIds.Add(decorationId);
        OnGardenChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 장식은 꽃 배치와 칸을 다투지 않는다 — 성능에 관여하지 않는 별도 레이어라, 화분/바닥/배경처럼
    /// 꽃 밑이나 주변에 자연스럽게 깔리는 것들이 대부분이기 때문이다. 격자 범위 안인지만 확인한다.
    /// </summary>
    public bool TryPlaceDecoration(string decorationId, Vector2Int origin)
    {
        if (!ownedDecorationIds.Contains(decorationId)) return false;

        DecorationData data = GetDecorationData(decorationId);
        if (data == null) return false;
        foreach (Vector2Int cell in data.GetOccupiedCells(origin))
            if (!IsInsideGrid(cell)) return false;

        decorationPlacements[decorationId] = origin;
        OnGardenChanged?.Invoke();
        return true;
    }

    public void RemoveDecoration(string decorationId)
    {
        if (decorationPlacements.Remove(decorationId))
            OnGardenChanged?.Invoke();
    }

    // ===================================================================
    // 배치 프리셋 (작업 6.4 — 구조만. UI는 이후 작업)
    // ===================================================================

    public void SaveCurrentAsPreset(int slotIndex, string presetName)
    {
        if (slotIndex < 0 || slotIndex >= MaxPresetSlots) return;
        while (presets.Count <= slotIndex) presets.Add(new GardenPreset());

        GardenPreset preset = presets[slotIndex];
        preset.presetName = presetName;
        preset.placements.Clear();
        foreach (var kvp in flowerPlacements)
            preset.placements.Add(new GardenFlowerPlacement { flowerId = kvp.Key, x = kvp.Value.x, y = kvp.Value.y });
    }

    /// <summary>
    /// 프리셋을 그대로 재현한다. 그 사이 조건이 안 맞게 된(개화 취소는 없지만 방어적으로) 꽃은
    /// 조용히 건너뛴다 — 하나가 안 맞는다고 나머지 배치까지 막을 이유가 없다.
    /// </summary>
    public void ApplyPreset(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= presets.Count) return;

        foreach (string id in flowerPlacements.Keys.ToList())
            RemoveFlower(id);

        foreach (GardenFlowerPlacement p in presets[slotIndex].placements)
            TryPlaceFlower(p.flowerId, new Vector2Int(p.x, p.y));
    }

    /// <summary> 데이터 초기화(설정 화면 전용) — 정원을 완전히 빈 상태로 되돌린다. </summary>
    public void ResetToFreshStart()
    {
        flowerPlacements.Clear();
        tileTouchCounts.Clear();
        decorationPlacements.Clear();
        ownedDecorationIds.Clear();
        presets.Clear();
        adjacencyBonusFractionCache.Clear();

        currentSizeIndex = 0;
        lastTendedTimeTicksUtc = DateTime.UtcNow.Ticks;

        OnGardenChanged?.Invoke();
    }

    // ===================================================================
    // 세이브 (작업 7)
    // ===================================================================

    public GardenSaveData CaptureSaveData()
    {
        GardenSaveData data = new GardenSaveData
        {
            sizeIndex = currentSizeIndex,
            lastTendedTimeTicksUtc = lastTendedTimeTicksUtc
        };

        foreach (var kvp in flowerPlacements)
            data.flowerPlacements.Add(new GardenFlowerPlacement { flowerId = kvp.Key, x = kvp.Value.x, y = kvp.Value.y });

        foreach (var kvp in tileTouchCounts)
            data.tileTouches.Add(new GardenTileTouchEntry { flowerId = kvp.Key, touchCount = kvp.Value });

        data.ownedDecorationIds.AddRange(ownedDecorationIds);

        foreach (var kvp in decorationPlacements)
            data.decorationPlacements.Add(new GardenDecorationPlacement { decorationId = kvp.Key, x = kvp.Value.x, y = kvp.Value.y });

        data.presets = presets;

        return data;
    }

    /// <summary>
    /// data는 SaveData.garden의 필드 초기화식 덕분에 항상 non-null이다(정원 필드가 없는 예전
    /// 세이브를 읽어도 "빈 GardenSaveData"가 만들어진다) — 그래도 방어적으로 null 체크는 해 둔다.
    /// </summary>
    public void LoadFromSaveData(GardenSaveData data)
    {
        flowerPlacements.Clear();
        tileTouchCounts.Clear();
        decorationPlacements.Clear();
        ownedDecorationIds.Clear();
        presets.Clear();

        if (data == null) data = new GardenSaveData();

        currentSizeIndex = Mathf.Max(0, data.sizeIndex);
        lastTendedTimeTicksUtc = data.lastTendedTimeTicksUtc;

        if (data.flowerPlacements != null)
            foreach (GardenFlowerPlacement p in data.flowerPlacements)
                if (!string.IsNullOrEmpty(p.flowerId))
                    flowerPlacements[p.flowerId] = new Vector2Int(p.x, p.y);

        if (data.tileTouches != null)
            foreach (GardenTileTouchEntry t in data.tileTouches)
                if (!string.IsNullOrEmpty(t.flowerId))
                    tileTouchCounts[t.flowerId] = t.touchCount;

        if (data.ownedDecorationIds != null)
            foreach (string id in data.ownedDecorationIds)
                if (!string.IsNullOrEmpty(id)) ownedDecorationIds.Add(id);

        if (data.decorationPlacements != null)
            foreach (GardenDecorationPlacement p in data.decorationPlacements)
                if (!string.IsNullOrEmpty(p.decorationId))
                    decorationPlacements[p.decorationId] = new Vector2Int(p.x, p.y);

        if (data.presets != null)
            presets = data.presets;

        RecomputeAdjacencyCache();
    }

    // ===================================================================
    // 개발자 치트 — 핵심 3개(시듦 강제/즉시 복구/시간 되감기)는 CheatPanel(런타임, 설정 화면에서
    // 비밀번호로 잠금)에서도 써야 해서 항상 컴파일된다. 이 셋 외의 계열은 여전히 에디터
    // 전용(CheatMenuWindow)이다.
    // ===================================================================

    /// <summary>
    /// 치트 — 시듦 단계를 강제로 지정한다(연출 확인용). wiltStages[stageIndex]의 afterSeconds보다
    /// 살짝 더 지난 시각으로 lastTendedTimeTicksUtc를 되돌리되, 다음 단계 경계는 넘지 않게 해서
    /// 정확히 그 단계가 되도록 한다 — 새 시뮬레이션 코드 없이 기존 GetWiltMultiplier/
    /// GetWiltStageLabel 계산 경로를 그대로 탄다.
    /// </summary>
    public void Cheat_ForceWiltStage(int stageIndex)
    {
        List<WiltStage> stages = ActiveGardenData.wiltStages;
        if (stages == null || stageIndex < 0 || stageIndex >= stages.Count) return;

        double targetElapsed = stages[stageIndex].afterSeconds + 1;
        if (stageIndex + 1 < stages.Count)
            targetElapsed = Math.Min(targetElapsed, stages[stageIndex + 1].afterSeconds - 1);

        lastTendedTimeTicksUtc = DateTime.UtcNow.AddSeconds(-targetElapsed).Ticks;
        OnGardenChanged?.Invoke();
    }

    /// <summary> 치트 — 정원 전체를 "방금 손질 완료" 상태로 되돌린다(CheckIfFullyTendedAndResetCycle과
    /// 동일한 상태 전이를 즉시 강제한다). </summary>
    public void Cheat_FullyRestoreGarden()
    {
        lastTendedTimeTicksUtc = DateTime.UtcNow.Ticks;
        tileTouchCounts.Clear();
        OnGardenChanged?.Invoke();
    }

    /// <summary>
    /// 치트 — 시간 스킵 기능 전용. 실제로 seconds만큼 시간이 흘렀다면 lastTendedTimeTicksUtc는
    /// 그대로인데(손질 안 했으니) DateTime.UtcNow만 미래로 이동해 GetElapsedSecondsSinceTended()가
    /// 자연히 커진다 — 이 치트에선 UtcNow를 조작할 수 없으므로 대신 lastTendedTimeTicksUtc를 그만큼
    /// 과거로 되돌려 동일한 효과를 낸다. FlowerManager.ApplyOfflineProgress를 호출하기 "전에" 먼저
    /// 호출해야, 그 메서드가 구간 분할 계산의 시작점으로 읽는 GetElapsedSecondsSinceTended()가
    /// 이미 스킵을 반영한 값이 된다.
    /// </summary>
    public void Cheat_RewindTendedClock(double seconds)
    {
        if (lastTendedTimeTicksUtc <= 0) return; // 정원이 아직 한 번도 초기화되지 않았으면 손댈 게 없음
        lastTendedTimeTicksUtc = new DateTime(lastTendedTimeTicksUtc, DateTimeKind.Utc).AddSeconds(-seconds).Ticks;
    }

#if UNITY_EDITOR
    // ===================================================================
    // 개발자 치트 (에디터 전용, 나머지) — Tools > 꽃소녀 치트(CheatMenuWindow)에서만 호출한다.
    // 전부 #if UNITY_EDITOR로 감싸 릴리스 빌드에는 포함되지 않는다.
    // ===================================================================

    /// <summary> 치트 — 정원 확장 단계를 비용 없이 직접 설정한다. 격자가 줄어들며 밖으로 나가는
    /// 배치가 생기면 안전하게 제거한다(치트 전용 방어 — 실제 플레이 경로에는 축소가 없어 발생하지
    /// 않는 상황이지만, 치트로 강제 축소했을 때 잘못된 상태가 남지 않도록 한다). </summary>
    public void Cheat_SetGardenSizeIndex(int index)
    {
        List<GardenData.GardenSize> sizes = ActiveGardenData.sizes;
        if (sizes == null || sizes.Count == 0) return;

        currentSizeIndex = Mathf.Clamp(index, 0, sizes.Count - 1);

        foreach (string id in flowerPlacements.Keys.ToList())
        {
            FlowerData data = FlowerManager.Instance != null ? FlowerManager.Instance.GetFlowerData(id) : null;
            bool fits = data != null && data.GetOccupiedCells(flowerPlacements[id]).All(IsInsideGrid);
            if (!fits) RemoveFlower(id);
        }

        RecomputeAdjacencyCache();
        OnGardenChanged?.Invoke();
    }

    /// <summary> 치트 — 정원에 배치된 꽃을 전부 해제한다(장식·확장 단계·시듦 주기는 그대로 유지). </summary>
    public void Cheat_ClearAllPlacements()
    {
        flowerPlacements.Clear();
        tileTouchCounts.Clear();
        RecomputeAdjacencyCache();
        OnGardenChanged?.Invoke();
    }

#endif
}
