using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 정원 화면. 탭과 드래그 두 가지 방식으로 배치한다:
///   [탭] 아래 "배치 대상" 목록에서 꽃을 탭하면 "선택(armed)" 상태가 되고, 격자의 빈 칸을 탭하면
///        그 칸을 원점으로 배치된다(모양대로 나머지 칸도 점유). 이미 꽃이 있는 칸을 탭하면 그 꽃의
///        정보 패널을 연다(작업 1).
///   [드래그] 로스터 항목을 격자로 끌면 배치, 격자 안에서 다른 칸으로 끌면 이동, 격자 밖(로스터 등)에
///        놓으면 제거된다 — 정보 패널이 열려서 탭만으로는 배치를 취소할 방법이 마땅치 않다는 문제를
///        드래그로 해결한다. 둘 다 armed/드래그 중인 꽃을 격자 위에 올리면(호버) 그 자리에 놓았을
///        때의 결과를 미리 보여준다(작업 4 — GardenManager.Simulate*TotalGoldPerSecond, 실제 배치/
///        이동과 동일 계산 경로).
/// 배치가 안 되면(격자 밖으로 나가거나 겹침) 조용히 실패하고 원래 상태가 유지되어 다시 시도할 수 있다.
///
/// 정보 패널이 열려 있는 동안 격자는 그 꽃이 "주는 효과"(초록)/"받는 효과"(파랑)/인접 판정 칸(옅게)을
/// 색으로 구분해 보여주고(작업 2), 모든 배치 칸에는 항상 현재 인접 배율이 작게 표시된다. 상단 요약은
/// 정원 총 G/s와 "인접 효과 +N%"(인접 효과를 전부 껐을 때와 비교한 값, 작업 3)를 보여준다.
///
/// 모든 인접 효과 수치(하이라이트 대상·배율·미리보기)는 GardenManager가 배치 변경 시에만 재계산해
/// 캐시해 둔 값을 읽기만 한다 — 이 패널이 별도로 인접 효과를 계산하지 않는다.
/// </summary>
public class GardenPanel : MonoBehaviour
{
    [Header("루트")]
    public GameObject root;

    [Header("상단 요약")]
    public TMP_Text totalGpsText;
    public Button expandButton;
    public TMP_Text expandButtonText;

    [Header("격자")]
    public Transform gridContent;
    public GridLayoutGroup gridLayout;
    public Button cellPrefab;

    [Header("배치 대상 목록 (가로 스크롤)")]
    public Transform rosterContent;
    public Button rosterItemPrefab;
    public TMP_Text armedStatusText;

    [Header("선택 정보 패널 (작업 1 — 배치된 꽃 탭 시 오픈. 상점 패널 위에 뜨는 별도 오브젝트 참조)")]
    public GardenFlowerInfoPanel infoPanel;

    private const string ShapePreviewChildName = "ShapePreview";

    // 격자 하이라이트 색(작업 2). 선택 > 서로 주고받음 > 주는 대상(초록) > 받는 출처(파랑) >
    // 인접 판정 칸(옅게) > 기본 순으로 우선한다.
    private static readonly Color SelectedColor = new Color(1f, 0.85f, 0.2f, 1f);
    private static readonly Color GivingColor = new Color(0.3f, 0.9f, 0.4f, 0.9f);
    private static readonly Color ReceivingColor = new Color(0.3f, 0.6f, 1f, 0.9f);
    private static readonly Color BothColor = new Color(0.55f, 0.8f, 0.35f, 0.9f);
    private static readonly Color AdjacencyCheckTint = new Color(1f, 1f, 1f, 0.18f);
    // 시든(아직 다 손질되지 않은) 타일을 탁한 갈색조로 살짝 덮어서 손질된 타일과 구분한다(작업 지시
    // 3.4 UI 요구 — "시든 타일과 복구된 타일이 구분되어야 함"). 텍스트("손질N/M")와 함께 이중으로
    // 표시해서 색맹 등으로 색 구분이 어려운 경우에도 진행도를 알 수 있게 한다.
    private static readonly Color WiltedTint = new Color(0.35f, 0.3f, 0.22f, 1f);
    private static readonly Color OccupiedColor = new Color(1f, 0.55f, 0.75f, 0.9f);
    private static readonly Color EmptyColor = new Color(1f, 1f, 1f, 0.08f);

    private static readonly Color PreviewValidColor = new Color(0.3f, 1f, 0.4f, 0.85f);
    private static readonly Color PreviewInvalidColor = new Color(1f, 0.3f, 0.3f, 0.85f);

    private readonly List<Button> spawnedCells = new List<Button>();
    private readonly List<Button> spawnedRosterItems = new List<Button>();
    private string armedFlowerId;
    private string selectedFlowerId; // 정보 패널에 지금 보여주는 꽃(작업 1·2 공용 — 격자 하이라이트 기준)

    // RebuildGrid가 채워 두고 RepaintCellVisual(드래그 미리보기를 지울 때)이 재사용하는 하이라이트
    // 컨텍스트 — 매번 새로 계산하지 않기 위한 캐시(성능: 호버할 때마다 GardenManager 캐시를 다시
    // 훑지 않는다).
    private int cachedGridWidth;
    private HashSet<Vector2Int> cachedSelectedCells;
    private HashSet<Vector2Int> cachedAdjacencyCheckCells;
    private HashSet<string> cachedGivingTargetIds;
    private HashSet<string> cachedReceivingSourceIds;
    // 여러 칸을 차지하는(직사각형) 꽃이 점유한 칸들 — 이 칸들은 개별 렌더링을 비우고 하나의 병합
    // 오버레이(spawnedMergeOverlays)가 대신 그린다("칸과 칸 사이 간격을 없애 하나의 블록처럼" 요청).
    private readonly HashSet<Vector2Int> cachedMergedCells = new HashSet<Vector2Int>();
    // 비직사각형(L자·십자 등) 다칸 꽃에서 라벨을 생략할 칸들 — 대표 칸(맨 위→맨 왼쪽) 하나만 이름/유대/
    // 배율을 보여주고 나머지는 비워서, 사각형 병합 블록처럼 "하나의 꽃"으로 읽히게 한다.
    private readonly HashSet<Vector2Int> cachedLabelBlankCells = new HashSet<Vector2Int>();
    private readonly List<Image> spawnedMergeOverlays = new List<Image>();

    // 드래그/호버 중 격자에 직접 칠하는 "모양+가능여부" 미리보기(성능: RebuildGrid 전체 재실행 없이
    // 해당 칸들만 다시 칠한다).
    private readonly List<Vector2Int> previewPaintedCells = new List<Vector2Int>();

    // ── 드래그 앤 드롭 ──────────────────────────────────────────────────
    // "탭해서 배치"만 있으면 이미 배치된 꽃을 탭했을 때 정보 패널이 열려버려서(작업 1) 배치를
    // 취소할 직관적인 방법이 없다는 문제가 보고돼 추가했다: 로스터에서 격자로 끌면 배치, 격자
    // 안에서 다른 칸으로 끌면 이동, 격자 밖(로스터 등)으로 끌어다 놓으면 제거된다.
    private string draggingFlowerId;
    private Vector2Int? draggingFromCell; // null이면 로스터에서 시작한 새 배치, 값이 있으면 기존 배치 이동
    private bool dragWasHandledByDrop;    // 유효한 칸에 드롭됐는지 — false면 EndDrag에서 제거 처리
    private RectTransform dragGhostRT;
    private TMP_Text dragGhostText;

    private void Awake()
    {
        if (root == null) root = gameObject;
    }

    private void Start()
    {
        if (expandButton != null) expandButton.onClick.AddListener(HandleExpandClicked);

        if (GardenManager.Instance != null)
            GardenManager.Instance.OnGardenChanged += HandleGardenChanged;
        if (FlowerManager.Instance != null)
        {
            FlowerManager.Instance.OnOwnedFlowersChanged += HandleGardenChanged;
            FlowerManager.Instance.OnFlowerBloomed += HandleFlowerBloomed;
            FlowerManager.Instance.OnBondLevelUp += HandleBondLevelUp;
        }
        // infoPanel이 이제 다른 사이드(상점 패널) 위에 뜨므로, 격자 쪽에서 그 패널이 닫혔는지 스스로
        // 알 방법이 없다 — 이 이벤트로 알려받아 하이라이트(selectedFlowerId)를 함께 해제한다.
        if (infoPanel != null) infoPanel.OnClosed += HandleInfoPanelClosed;

        RebuildAll();
    }

    private void OnDestroy()
    {
        if (GardenManager.Instance != null)
            GardenManager.Instance.OnGardenChanged -= HandleGardenChanged;
        if (FlowerManager.Instance != null)
        {
            FlowerManager.Instance.OnOwnedFlowersChanged -= HandleGardenChanged;
            FlowerManager.Instance.OnFlowerBloomed -= HandleFlowerBloomed;
            FlowerManager.Instance.OnBondLevelUp -= HandleBondLevelUp;
        }
        if (infoPanel != null) infoPanel.OnClosed -= HandleInfoPanelClosed;
    }

    private void HandleInfoPanelClosed()
    {
        selectedFlowerId = null;
        RebuildGrid();
    }

    // 이 패널은 GrowthPanel의 3번째 탭이라 PanelSwitcher가 SetActive로 켜고 끈다 — 탭을 다시 열
    // 때마다 최신 상태로 보이도록 OnEnable에서도 다시 그린다.
    private void OnEnable() => RebuildAll();

    private void HandleGardenChanged() => RebuildAll();
    private void HandleFlowerBloomed(string _) => RebuildAll();
    private void HandleBondLevelUp(string _, int __) => RebuildAll(); // 매 레벨업마다 인접 효과 위력이 바뀔 수 있음(유대 레벨 비례)

    private void RebuildAll()
    {
        if (GardenManager.Instance == null || !gameObject.activeInHierarchy) return;

        // 선택된 꽃이 그 사이 제거됐으면(정보 패널의 "제거" 버튼 등) 하이라이트도 함께 해제한다.
        if (!string.IsNullOrEmpty(selectedFlowerId) && !GardenManager.Instance.IsPlaced(selectedFlowerId))
            selectedFlowerId = null;

        RebuildGrid();
        RebuildRoster();
        UpdateSummary();
        UpdateArmedStatusText();
    }

    /// <summary>
    /// 격자 칸 버튼을 재사용한다(크기가 그대로면 Destroy+Instantiate를 다시 하지 않음) — 매번 전부
    /// 새로 만들면 드래그 앤 드롭 직후처럼 재계산이 잦은 상황에서 눈에 띄는 끊김이 생긴다는 신고가
    /// 있었다. 격자 크기(확장 등)가 바뀔 때만 다시 만든다.
    /// </summary>
    private void RebuildGrid()
    {
        if (gridContent == null || cellPrefab == null) return;

        int width = GardenManager.Instance.Width;
        int height = GardenManager.Instance.Height;
        int needed = width * height;

        if (gridLayout != null) gridLayout.constraintCount = width;

        if (spawnedCells.Count != needed)
        {
            foreach (Button cell in spawnedCells)
                if (cell != null) Destroy(cell.gameObject);
            spawnedCells.Clear();

            for (int i = 0; i < needed; i++)
                spawnedCells.Add(Instantiate(cellPrefab, gridContent));
        }

        cachedGridWidth = width;

        // 작업 2 하이라이트 준비 — 선택된 꽃이 있을 때만 채운다. 전부 GardenManager가 이미 계산해 둔
        // 캐시(GetOutgoingEffectInfo/GetIncomingContributions/GetAdjacencyCheckCells)를 읽기만 한다.
        cachedSelectedCells = null;
        cachedAdjacencyCheckCells = null;
        cachedGivingTargetIds = null;
        cachedReceivingSourceIds = null;

        if (!string.IsNullOrEmpty(selectedFlowerId) && GardenManager.Instance.IsPlaced(selectedFlowerId))
        {
            FlowerData selectedData = FlowerManager.Instance != null ? FlowerManager.Instance.GetFlowerData(selectedFlowerId) : null;
            Vector2Int? origin = GardenManager.Instance.GetPlacementOrigin(selectedFlowerId);
            if (selectedData != null && origin.HasValue)
                cachedSelectedCells = new HashSet<Vector2Int>(selectedData.GetOccupiedCells(origin.Value));

            cachedAdjacencyCheckCells = GardenManager.Instance.GetAdjacencyCheckCells(selectedFlowerId);

            OutgoingEffectInfo outgoing = GardenManager.Instance.GetOutgoingEffectInfo(selectedFlowerId);
            cachedGivingTargetIds = outgoing != null ? new HashSet<string>(outgoing.targetFlowerIds) : new HashSet<string>();

            cachedReceivingSourceIds = new HashSet<string>(
                GardenManager.Instance.GetIncomingContributions(selectedFlowerId).Select(c => c.sourceFlowerId));
        }

        // 여러 칸짜리 폴리오미노가 점유한 칸들을 미리 분류해 둔다.
        //   - 직사각형(칸 수 == 바운딩 박스 넓이): 아래 개별 렌더링을 생략하고 RebuildMergeOverlays가
        //     만드는 사각형 하나짜리 오버레이가 통째로 대신 그린다.
        //   - 비직사각형(L자·십자 등): 배경은 칸별로 그대로 두되(이미 같은 색이라 이질감 없음), 라벨만
        //     대표 칸(맨 위→맨 왼쪽) 하나에 몰아준다 — 칸과 칸 사이 간격은 RebuildMergeOverlays가
        //     인접한 두 칸 사이의 틈만 잇는 작은 "이음매" 오버레이로 메운다(바운딩 박스로 통째로
        //     덮으면 원래 비어 있어야 할 칸까지 가려지므로, 사각형처럼 하나로 덮을 수 없다).
        cachedMergedCells.Clear();
        cachedLabelBlankCells.Clear();
        foreach (string flowerId in GardenManager.Instance.GetPlacedFlowerIds())
        {
            FlowerData data = FlowerManager.Instance != null ? FlowerManager.Instance.GetFlowerData(flowerId) : null;
            Vector2Int? placedOrigin = GardenManager.Instance.GetPlacementOrigin(flowerId);
            if (data == null || !placedOrigin.HasValue) continue;

            List<Vector2Int> occupied = data.GetOccupiedCells(placedOrigin.Value).ToList();
            if (occupied.Count <= 1) continue; // 1칸이면 이어붙일 게 없음

            if (occupied.Count == GetShapeBoundingArea(data))
            {
                foreach (Vector2Int c in occupied) cachedMergedCells.Add(c);
            }
            else
            {
                Vector2Int primary = occupied.OrderBy(c => c.y).ThenBy(c => c.x).First();
                foreach (Vector2Int c in occupied)
                    if (c != primary) cachedLabelBlankCells.Add(c);
            }
        }

        // y=0(모양 편집기의 좌상단 기준과 동일)을 화면 위쪽 행으로 그린다.
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                Button cellButton = spawnedCells[y * width + x];

                ApplyCellVisual(cellButton, cell);

                Vector2Int capturedCell = cell; // 클로저 캡처용 로컬 복사
                cellButton.onClick.RemoveAllListeners();
                cellButton.onClick.AddListener(() => HandleCellClicked(capturedCell));

                WireCellEvents(cellButton, capturedCell);
            }
        }

        // [버그 수정] 재시작 직후(세이브 로드로 칸 버튼을 전부 새로 Instantiate하는 경우) GridLayoutGroup의
        // 실제 배치 계산은 그 프레임이 끝날 때 지연 실행되므로, 바로 다음 줄에서 각 칸의
        // RectTransform.anchoredPosition/sizeDelta를 읽으면 아직 갱신 전(기본값 0 등)일 수 있다 —
        // 그 상태로 병합 오버레이 위치를 계산하면 죄다 어긋나 보인다("재시작했더니 정원이 망가졌다"는
        // 신고의 원인). 오버레이 계산 직전에 레이아웃을 강제로 즉시 완료시켜 항상 최신 위치를 읽게 한다.
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(gridContent as RectTransform);

        RebuildMergeOverlays();
    }

    /// <summary> data.gardenShape의 바운딩 박스 칸 수(가로×세로). occupied.Count와 같으면 빈틈없는
    /// 직사각형이라는 뜻 — 병합 오버레이로 안전하게 덮을 수 있다. </summary>
    private static int GetShapeBoundingArea(FlowerData data)
    {
        List<Vector2Int> shape = (data.gardenShape != null && data.gardenShape.Count > 0)
            ? data.gardenShape
            : new List<Vector2Int> { Vector2Int.zero };

        int w = 0, h = 0;
        foreach (Vector2Int c in shape)
        {
            if (c.x + 1 > w) w = c.x + 1;
            if (c.y + 1 > h) h = c.y + 1;
        }
        return w * h;
    }

    /// <summary>
    /// 다칸 꽃마다 칸과 칸 사이 간격이 안 보이게 만든다(요청: "2×2 꽃을 배치하면 칸과 칸 사이 간격이
    /// 보이는데... 하나의 블록처럼", 그리고 후속 요청: "ㄱ자나 + 모양은 블록 처리가 안 돼"). 직사각형은
    /// 통짜 사각형 오버레이 하나로, 비직사각형(L자·십자 등)은 인접한 두 칸 사이의 틈만 잇는 작은
    /// "이음매" 오버레이 여러 개로 처리한다 — 바운딩 박스 하나로 덮으면 비직사각형은 원래 비어 있어야
    /// 할 칸(예: L자의 빈 모서리)까지 가려지기 때문이다. 둘 다 실제 칸의 RectTransform(GridLayoutGroup이
    /// 이미 정확히 배치해 둔 값)을 기준으로 위치를 계산해서, 격자 전체의 좌표 공식을 몰라도 항상
    /// 정확히 이어붙는다. 클릭/드래그는 여전히 개별 칸 버튼이 처리한다(오버레이는 raycastTarget=false).
    /// </summary>
    private void RebuildMergeOverlays()
    {
        foreach (Image overlay in spawnedMergeOverlays)
            if (overlay != null) Destroy(overlay.gameObject);
        spawnedMergeOverlays.Clear();

        if (gridLayout == null || gridContent == null || GardenManager.Instance == null || FlowerManager.Instance == null) return;

        foreach (string flowerId in GardenManager.Instance.GetPlacedFlowerIds())
        {
            FlowerData data = FlowerManager.Instance.GetFlowerData(flowerId);
            Vector2Int? originOpt = GardenManager.Instance.GetPlacementOrigin(flowerId);
            if (data == null || !originOpt.HasValue) continue;

            Vector2Int origin = originOpt.Value;
            List<Vector2Int> occupied = data.GetOccupiedCells(origin).ToList();
            if (occupied.Count <= 1) continue;

            if (occupied.Count == GetShapeBoundingArea(data))
                CreateRectangularBlockOverlay(flowerId, data, origin);
            else
                CreateSeamOverlaysForShape(flowerId, occupied);
        }
    }

    /// <summary> 직사각형 다칸 꽃 — 통짜 사각형 오버레이 하나로 전체를 덮는다(원점 칸 기준 좌상단 +
    /// 바운딩 박스 전체 크기). </summary>
    private void CreateRectangularBlockOverlay(string flowerId, FlowerData data, Vector2Int origin)
    {
        int shapeW = 0, shapeH = 0;
        foreach (Vector2Int c in data.gardenShape)
        {
            if (c.x + 1 > shapeW) shapeW = c.x + 1;
            if (c.y + 1 > shapeH) shapeH = c.y + 1;
        }

        int originIndex = origin.y * cachedGridWidth + origin.x;
        if (originIndex < 0 || originIndex >= spawnedCells.Count) return;
        RectTransform originRT = spawnedCells[originIndex].GetComponent<RectTransform>();
        if (originRT == null) return;

        Vector2 size = new Vector2(
            shapeW * gridLayout.cellSize.x + (shapeW - 1) * gridLayout.spacing.x,
            shapeH * gridLayout.cellSize.y + (shapeH - 1) * gridLayout.spacing.y);

        Image overlayImg = CreateOverlay($"MergedOverlay_{flowerId}", originRT, GetCellTopLeft(originRT), size,
            ApplyWiltTint(ResolveCellColor(origin, flowerId), flowerId));

        FlowerInstance instance = FlowerManager.Instance.GetInstance(flowerId);
        string bondSuffix = instance != null ? $"\n유대{instance.bondLevel}" : "";
        float multiplier = GardenManager.Instance.GetGoldMultiplierForFlower(flowerId);
        AddOverlayLabel(overlayImg.gameObject, $"{data.displayName}{bondSuffix}\n×{multiplier:0.00}{GetTendingSuffix(flowerId)}");
    }

    /// <summary> 시듦 복구 진행도 표시(작업 지시 3.4) — 이 타일이 아직 다 손질되지 않았으면
    /// "손질{터치수}/{필요수}"를 라벨에 덧붙인다. 이미 다 손질된 타일은 굳이 표시하지 않아
    /// 깔끔하게 둔다. </summary>
    private string GetTendingSuffix(string flowerId)
    {
        if (GardenManager.Instance == null || !GardenManager.Instance.IsCurrentlyWilted()) return "";
        int required = GardenManager.Instance.ActiveGardenData.touchesPerPlacedFlowerToFullyRestore;
        int touches = GardenManager.Instance.GetTileTouchCount(flowerId);
        return touches >= required ? "" : $"\n손질{touches}/{required}";
    }

    /// <summary> 아직 다 손질되지 않은 타일이면 색을 살짝 탁하게 섞어서(WiltedTint) 시각적으로 구분한다
    /// (작업 지시 3.4 UI 요구). 격자 칸/직사각형 병합 오버레이/이음매 오버레이 셋 다 공유한다.
    /// GardenManager.IsCurrentlyWilted()로 먼저 걸러서, 정원이 지금 실제로는 멀쩡한데(양호 유예
    /// 기간이거나 방금 전체 복구를 마쳐 손질 카운트만 막 비워진 직후) 터치 수가 0이라는 이유만으로
    /// 탁한 색이 다시 나타나는 것("복구했더니 시듦이 도로 생겼다"는 신고)을 막는다. </summary>
    private Color ApplyWiltTint(Color baseColor, string flowerId)
    {
        if (GardenManager.Instance == null || !GardenManager.Instance.IsCurrentlyWilted()) return baseColor;
        int required = GardenManager.Instance.ActiveGardenData.touchesPerPlacedFlowerToFullyRestore;
        bool needsTending = GardenManager.Instance.GetTileTouchCount(flowerId) < required;
        return needsTending ? Color.Lerp(baseColor, WiltedTint, 0.35f) : baseColor;
    }

    /// <summary> 비직사각형 다칸 꽃 — 서로 4방향으로 붙어 있는 칸 쌍마다, 그 사이의 간격(spacing)만
    /// 딱 채우는 작은 이음매 오버레이를 만든다. 각 칸에서 "오른쪽"과 "아래쪽" 이웃만 검사하면 모든
    /// 인접 쌍을 정확히 한 번씩만 처리한다. 라벨은 여기서 붙이지 않는다 — 대표 칸(cachedLabelBlankCells로
    /// 나머지가 비워진 칸)이 자기 자신의 평범한 칸 라벨을 그대로 보여준다. </summary>
    private void CreateSeamOverlaysForShape(string flowerId, List<Vector2Int> occupied)
    {
        HashSet<Vector2Int> occupiedSet = new HashSet<Vector2Int>(occupied);
        Color color = ApplyWiltTint(ResolveCellColor(occupied[0], flowerId), flowerId); // 이 꽃의 모든 칸이 기본적으로 같은 색이라 대표 1칸으로 충분

        foreach (Vector2Int cell in occupied)
        {
            int index = cell.y * cachedGridWidth + cell.x;
            if (index < 0 || index >= spawnedCells.Count) continue;
            RectTransform cellRT = spawnedCells[index].GetComponent<RectTransform>();
            if (cellRT == null) continue;
            Vector2 topLeft = GetCellTopLeft(cellRT);

            if (occupiedSet.Contains(cell + new Vector2Int(1, 0)) && gridLayout.spacing.x > 0f)
            {
                CreateOverlay($"Seam_{flowerId}_{cell.x}_{cell.y}_H", cellRT,
                    topLeft + new Vector2(gridLayout.cellSize.x, 0f),
                    new Vector2(gridLayout.spacing.x, gridLayout.cellSize.y), color);
            }

            if (occupiedSet.Contains(cell + new Vector2Int(0, 1)) && gridLayout.spacing.y > 0f)
            {
                CreateOverlay($"Seam_{flowerId}_{cell.x}_{cell.y}_V", cellRT,
                    topLeft + new Vector2(0f, -gridLayout.cellSize.y),
                    new Vector2(gridLayout.cellSize.x, gridLayout.spacing.y), color);
            }
        }
    }

    /// <summary> 칸의 실제 RectTransform(피벗이 무엇이든 상관없이)에서 좌상단 모서리 좌표를 구한다 —
    /// GridLayoutGroup이 기본으로 주는 (0.5, 0.5) 중앙 피벗 그대로도 항상 정확하다. </summary>
    private static Vector2 GetCellTopLeft(RectTransform rt)
    {
        return new Vector2(
            rt.anchoredPosition.x - rt.sizeDelta.x * rt.pivot.x,
            rt.anchoredPosition.y + rt.sizeDelta.y * (1f - rt.pivot.y));
    }

    /// <summary>
    /// gridContent 아래에 순수 시각 전용(raycastTarget=false) 오버레이 하나를 만든다. gridContent엔
    /// GridLayoutGroup이 붙어 있어서, 그냥 자식으로 넣으면 격자가 "다음 칸"인 것처럼 강제로 재배치해
    /// 버려 방금 계산해 둔 위치·크기가 곧바로 덮어써진다(신고된 "블록이 투명해진다/어긋난다" 버그의
    /// 원인이었다) — LayoutElement.ignoreLayout=true로 모든 레이아웃 그룹의 제어 대상에서 뺀다.
    /// anchorMin/Max는 기준 칸(referenceCellRT)의 것을 그대로 복사해 같은 좌표계를 쓰고, 피벗은 항상
    /// 좌상단(0,1)으로 고정해 topLeft 위치에서 오른쪽·아래로만 정확히 자라나게 한다.
    /// </summary>
    private Image CreateOverlay(string name, RectTransform referenceCellRT, Vector2 topLeft, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(gridContent, false);

        LayoutElement le = go.AddComponent<LayoutElement>();
        le.ignoreLayout = true;

        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = referenceCellRT.anchorMin;
        rt.anchorMax = referenceCellRT.anchorMax;
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = topLeft;
        rt.sizeDelta = size;
        rt.SetAsLastSibling(); // 격자 칸 버튼들보다 항상 위에 그려지게(안 그러면 뒤쪽 칸에 가려짐)

        Image img = go.GetComponent<Image>();
        img.raycastTarget = false;
        img.color = color;

        spawnedMergeOverlays.Add(img);
        return img;
    }

    private void AddOverlayLabel(GameObject overlayGO, string text)
    {
        GameObject labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(overlayGO.transform, false);
        RectTransform labelRT = (RectTransform)labelGO.transform;
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;

        TMP_Text overlayLabel = labelGO.AddComponent<TextMeshProUGUI>();
        overlayLabel.text = text;
        overlayLabel.alignment = TextAlignmentOptions.Center;
        overlayLabel.fontSize = 13f;
        overlayLabel.color = Color.black;
        overlayLabel.raycastTarget = false;
        if (totalGpsText != null) overlayLabel.font = totalGpsText.font;
    }

    /// <summary> 칸 하나의 텍스트+색을 "정상 상태"(호버/드래그 미리보기 미적용)로 그린다. RebuildGrid의
    /// 메인 루프와, 미리보기를 지울 때(RepaintCellVisual)가 공유한다. </summary>
    private void ApplyCellVisual(Button cellButton, Vector2Int cell)
    {
        TMP_Text label = cellButton.GetComponentInChildren<TMP_Text>();
        Image bg = cellButton.targetGraphic as Image;

        if (cachedMergedCells.Contains(cell))
        {
            // 이 칸은 RebuildMergeOverlays가 그린 병합 오버레이가 대신 보여준다 — 자기 배경/글자는
            // 투명하게 비운다(클릭 판정용 Button 자체는 그대로 살아있다).
            if (label != null) label.text = "";
            if (bg != null) bg.color = new Color(0f, 0f, 0f, 0f);
            return;
        }

        string occupyingId = GardenManager.Instance.GetFlowerIdAt(cell);

        if (occupyingId != null)
        {
            FlowerData data = FlowerManager.Instance != null ? FlowerManager.Instance.GetFlowerData(occupyingId) : null;
            FlowerInstance instance = FlowerManager.Instance != null ? FlowerManager.Instance.GetInstance(occupyingId) : null;
            string name = data != null ? data.displayName : occupyingId;
            string bondSuffix = instance != null ? $"\n유대{instance.bondLevel}" : "";
            // 작업 2: 각 타일 위에 지금 받고 있는 인접 배율을 항상 작게 표시한다. 단, 비직사각형
            // 다칸 꽃(L자·십자 등)의 대표 칸이 아닌 나머지 칸은 라벨을 비운다(cachedLabelBlankCells) —
            // 그래야 여러 칸에 이름이 중복 표시되지 않고 "하나의 꽃"처럼 읽힌다.
            float multiplier = GardenManager.Instance.GetGoldMultiplierForFlower(occupyingId);
            string multiplierSuffix = $"\n×{multiplier:0.00}";
            string tendingSuffix = GetTendingSuffix(occupyingId);

            if (label != null) label.text = cachedLabelBlankCells.Contains(cell) ? "" : (name + bondSuffix + multiplierSuffix + tendingSuffix);
            if (bg != null) bg.color = ApplyWiltTint(ResolveCellColor(cell, occupyingId), occupyingId);
        }
        else
        {
            if (label != null) label.text = "";
            if (bg != null)
                bg.color = (cachedAdjacencyCheckCells != null && cachedAdjacencyCheckCells.Contains(cell))
                    ? Color.Lerp(EmptyColor, AdjacencyCheckTint, 0.6f)
                    : EmptyColor;
        }
    }

    /// <summary> RebuildGrid를 다시 돌리지 않고 칸 하나만 "정상 상태"로 되돌린다(드래그 미리보기 해제 전용,
    /// 성능: 매 호버 이탈마다 격자 전체를 다시 그리지 않기 위함). </summary>
    private void RepaintCellVisual(Vector2Int cell)
    {
        if (cachedGridWidth <= 0) return;
        int index = cell.y * cachedGridWidth + cell.x;
        if (index < 0 || index >= spawnedCells.Count) return;
        ApplyCellVisual(spawnedCells[index], cell);
    }

    /// <summary> 작업 2 — 선택 상태에 따른 칸 색 우선순위: 선택된 꽃 자신 > 서로 주고받음 >
    /// 주는 대상(초록) > 받는 출처(파랑) > 인접 판정 칸(옅게) > 기본 점유색. </summary>
    private Color ResolveCellColor(Vector2Int cell, string occupyingId)
    {
        if (cachedSelectedCells != null && cachedSelectedCells.Contains(cell)) return SelectedColor;

        bool isGivingTarget = cachedGivingTargetIds != null && cachedGivingTargetIds.Contains(occupyingId);
        bool isReceivingSource = cachedReceivingSourceIds != null && cachedReceivingSourceIds.Contains(occupyingId);
        if (isGivingTarget && isReceivingSource) return BothColor;
        if (isGivingTarget) return GivingColor;
        if (isReceivingSource) return ReceivingColor;

        if (cachedAdjacencyCheckCells != null && cachedAdjacencyCheckCells.Contains(cell))
            return Color.Lerp(OccupiedColor, AdjacencyCheckTint, 0.35f);

        return OccupiedColor;
    }

    /// <summary>
    /// 셀 하나에 필요한 이벤트를 전부 건다: (1) 호버 시 배치 전 미리보기(작업 4), (2) 드래그 시작/
    /// 이동/끝(격자 안에서 다른 칸으로 옮기기), (3) 드롭(로스터 또는 다른 칸에서 드래그해 온 꽃을
    /// 이 칸에 놓기). 클릭(HandleCellClicked)은 Button.onClick이 별도로 처리하며, Unity의 드래그
    /// 임계값 덕분에 실제로 끌지 않은 짧은 탭은 여전히 클릭으로만 처리된다.
    /// </summary>
    private void WireCellEvents(Button cellButton, Vector2Int cell)
    {
        EventTrigger trigger = cellButton.gameObject.GetComponent<EventTrigger>();
        if (trigger == null) trigger = cellButton.gameObject.AddComponent<EventTrigger>();
        trigger.triggers.Clear();

        AddTrigger(trigger, EventTriggerType.PointerEnter, _ => ShowPlacementPreview(cell));
        AddTrigger(trigger, EventTriggerType.PointerExit, _ => ClearPlacementPreview());

        AddTrigger(trigger, EventTriggerType.BeginDrag, _ => BeginDragFromCell(cell));
        AddTrigger(trigger, EventTriggerType.Drag, data => DragMove((PointerEventData)data));
        AddTrigger(trigger, EventTriggerType.EndDrag, _ => EndDragFromCell());
        AddTrigger(trigger, EventTriggerType.Drop, _ => DropOnCell(cell));
    }

    /// <summary> 로스터 항목에서 격자로 끌어다 놓으면 배치되도록 드래그 이벤트를 건다(클릭=선택(arm)은
    /// Button.onClick이 그대로 처리하며, 드래그 임계값 덕분에 둘이 충돌하지 않는다). </summary>
    private void WireRosterDrag(Button item, string flowerId)
    {
        EventTrigger trigger = item.gameObject.GetComponent<EventTrigger>();
        if (trigger == null) trigger = item.gameObject.AddComponent<EventTrigger>();
        trigger.triggers.Clear();

        AddTrigger(trigger, EventTriggerType.BeginDrag, _ => BeginDragFromRoster(flowerId));
        AddTrigger(trigger, EventTriggerType.Drag, data => DragMove((PointerEventData)data));
        AddTrigger(trigger, EventTriggerType.EndDrag, _ => EndDragFromRoster());
    }

    private static void AddTrigger(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> action)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(action);
        trigger.triggers.Add(entry);
    }

    /// <summary>
    /// 작업 4(배치 전 미리보기) — 셀에 마우스 포인터가 올라오면(호버), 지금 선택(armed) 또는 드래그
    /// 중인 꽃을 이 칸에 놓았을 때 "실제로 어느 칸들을 차지하는지"를 격자에 초록(가능)/빨강(불가능)
    /// 으로 직접 칠하고("모양과 들어갈 때를 가정한 미리보기" 요청), G/s 변화도 함께 보여준다.
    /// 자리 계산(PreviewResolvedOrigin)과 G/s 계산(Simulate*TotalGoldPerSecond) 둘 다 실제 배치/이동
    /// 경로가 쓰는 것과 동일하므로, 이 미리보기는 실제로 놓았을 때의 결과와 항상 정확히 같다(검증 6번).
    /// RebuildGrid를 다시 돌리지 않고 필요한 칸만 직접 칠해서 호버할 때마다 격자 전체가 다시 그려지는
    /// 비용(드래그가 버벅인다는 신고의 원인 중 하나)을 없앤다.
    /// </summary>
    private void ShowPlacementPreview(Vector2Int cell)
    {
        string previewFlowerId = !string.IsNullOrEmpty(draggingFlowerId) ? draggingFlowerId : armedFlowerId;
        ClearShapePaint();

        if (string.IsNullOrEmpty(previewFlowerId) || GardenManager.Instance == null) return;

        FlowerData data = FlowerManager.Instance != null ? FlowerManager.Instance.GetFlowerData(previewFlowerId) : null;
        if (data == null) return;

        Vector2Int? origin = GardenManager.Instance.PreviewResolvedOrigin(previewFlowerId, cell);
        bool valid = origin.HasValue;

        if (valid)
            foreach (Vector2Int occupied in data.GetOccupiedCells(origin.Value))
                PaintShapeCell(occupied, PreviewValidColor);
        else
            PaintShapeCell(cell, PreviewInvalidColor); // 놓을 자리가 없으면 호버한 칸만 "안 됨" 표시

        if (armedStatusText == null) return;

        if (!valid)
        {
            armedStatusText.text = "여기엔 놓을 수 없어요.";
            return;
        }

        BigNumber before = FlowerManager.Instance != null ? FlowerManager.Instance.GetTotalGoldPerSecond() : BigNumber.Zero;
        BigNumber after = draggingFromCell.HasValue
            ? GardenManager.Instance.SimulateMoveTotalGoldPerSecond(previewFlowerId, cell)
            : GardenManager.Instance.SimulatePlacementTotalGoldPerSecond(previewFlowerId, cell);

        double beforeD = before.ToDouble();
        double afterD = after.ToDouble();
        string deltaText = beforeD > 0 && !double.IsInfinity(beforeD) && !double.IsInfinity(afterD)
            ? $" ({(afterD >= beforeD ? "+" : "")}{(afterD / beforeD - 1.0) * 100.0:0.#}%)"
            : "";

        armedStatusText.text = $"여기에 놓으면: 정원 총 G/s {NumberFormatUtil.FormatPrecise(before)} → {NumberFormatUtil.FormatPrecise(after)}{deltaText}";
    }

    /// <summary> 호버/드래그가 그 칸을 벗어나면 미리보기 색을 지우고 안내 문구도 원래대로 되돌린다. </summary>
    private void ClearPlacementPreview()
    {
        ClearShapePaint();
        UpdateArmedStatusText();
    }

    private void PaintShapeCell(Vector2Int cell, Color tint)
    {
        if (cachedGridWidth <= 0 || !GardenManager.Instance.IsInsideGrid(cell)) return;
        int index = cell.y * cachedGridWidth + cell.x;
        if (index < 0 || index >= spawnedCells.Count) return;

        Button btn = spawnedCells[index];
        Image img = btn != null ? btn.targetGraphic as Image : null;
        if (img == null) return;

        img.color = tint;
        previewPaintedCells.Add(cell);
    }

    private void ClearShapePaint()
    {
        foreach (Vector2Int cell in previewPaintedCells)
            RepaintCellVisual(cell);
        previewPaintedCells.Clear();
    }

    // ===================================================================
    // 드래그 앤 드롭
    // ===================================================================

    private void BeginDragFromRoster(string flowerId)
    {
        draggingFlowerId = flowerId;
        draggingFromCell = null;
        dragWasHandledByDrop = false;
        ShowDragGhost(flowerId);
    }

    private void BeginDragFromCell(Vector2Int cell)
    {
        string occupyingId = GardenManager.Instance != null ? GardenManager.Instance.GetFlowerIdAt(cell) : null;
        if (occupyingId == null) return; // 빈 칸에서는 드래그가 시작되지 않는다

        draggingFlowerId = occupyingId;
        draggingFromCell = cell;
        dragWasHandledByDrop = false;
        ShowDragGhost(occupyingId);
    }

    private void DragMove(PointerEventData eventData)
    {
        if (dragGhostRT == null || string.IsNullOrEmpty(draggingFlowerId) || root == null) return;

        RectTransform parentRT = root.GetComponent<RectTransform>();
        if (parentRT != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRT, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
        {
            dragGhostRT.anchoredPosition = localPoint;
        }
    }

    private void EndDragFromRoster()
    {
        HideDragGhost();
        draggingFlowerId = null;
        draggingFromCell = null;
    }

    /// <summary>
    /// 배치 취소 경로 — 격자 안 칸에서 드래그를 시작했는데(draggingFromCell.HasValue) 유효한 칸에
    /// 드롭되지 않았으면(dragWasHandledByDrop == false, 즉 격자 밖이나 로스터 위에서 손을 뗐다는 뜻)
    /// 그 꽃을 정원에서 제거한다. Unity 이벤트 순서상 Drop은 항상 EndDrag보다 먼저 호출되므로,
    /// 유효한 칸에 드롭됐다면 이 시점에 이미 dragWasHandledByDrop이 true로 설정돼 있다.
    /// </summary>
    private void EndDragFromCell()
    {
        if (!dragWasHandledByDrop && draggingFromCell.HasValue && !string.IsNullOrEmpty(draggingFlowerId) && GardenManager.Instance != null)
            GardenManager.Instance.RemoveFlower(draggingFlowerId);

        HideDragGhost();
        draggingFlowerId = null;
        draggingFromCell = null;
        RebuildAll();
    }

    private void DropOnCell(Vector2Int cell)
    {
        if (string.IsNullOrEmpty(draggingFlowerId) || GardenManager.Instance == null) return;
        dragWasHandledByDrop = true;

        if (draggingFromCell.HasValue)
            GardenManager.Instance.TryMoveFlowerAtCell(draggingFlowerId, cell);
        else
            GardenManager.Instance.TryPlaceFlowerAtCell(draggingFlowerId, cell);
        // 성공하면 OnGardenChanged가, 실패해도 EndDragFromCell/EndDragFromRoster의 RebuildAll이
        // 화면을 다시 정상 상태로 그린다.
    }

    private void ShowDragGhost(string flowerId)
    {
        EnsureDragGhost();
        if (dragGhostRT == null) return;

        FlowerData data = FlowerManager.Instance != null ? FlowerManager.Instance.GetFlowerData(flowerId) : null;
        if (dragGhostText != null) dragGhostText.text = data != null ? data.displayName : flowerId;

        dragGhostRT.gameObject.SetActive(true);
        dragGhostRT.SetAsLastSibling();
    }

    private void HideDragGhost()
    {
        if (dragGhostRT != null) dragGhostRT.gameObject.SetActive(false);
    }

    /// <summary>
    /// 드래그 중인 꽃 이름을 보여주며 포인터를 따라다니는 작은 표식. 프리팹 없이 런타임에 한 번만
    /// 만들어 재사용한다(에디터 빌더 변경 없이 순수 코드로 추가하기 위함). 폰트는 이미 한글 폰트로
    /// 설정돼 있는 totalGpsText의 것을 그대로 빌려 쓴다 — 런타임 스크립트는 에디터 전용 LoadNotoFont를
    /// 쓸 수 없기 때문.
    /// </summary>
    private void EnsureDragGhost()
    {
        if (dragGhostRT != null || root == null) return;

        GameObject ghost = new GameObject("DragGhost", typeof(RectTransform), typeof(Image));
        ghost.transform.SetParent(root.transform, false);
        dragGhostRT = ghost.GetComponent<RectTransform>();
        dragGhostRT.sizeDelta = new Vector2(96f, 40f);
        dragGhostRT.pivot = new Vector2(0.5f, 0.5f);

        Image img = ghost.GetComponent<Image>();
        img.color = new Color(1f, 0.85f, 0.3f, 0.92f);
        img.raycastTarget = false; // 고스트 자신이 드롭 대상 판정을 가로채면 안 된다

        GameObject labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(ghost.transform, false);
        RectTransform labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;

        dragGhostText = labelGO.AddComponent<TextMeshProUGUI>();
        dragGhostText.alignment = TextAlignmentOptions.Center;
        dragGhostText.fontSize = 14f;
        dragGhostText.color = Color.black;
        dragGhostText.raycastTarget = false;
        if (totalGpsText != null) dragGhostText.font = totalGpsText.font;

        ghost.SetActive(false);
    }

    private void RebuildRoster()
    {
        if (rosterContent == null || rosterItemPrefab == null) return;

        foreach (Button item in spawnedRosterItems)
            if (item != null) Destroy(item.gameObject);
        spawnedRosterItems.Clear();

        if (FlowerManager.Instance == null) return;

        foreach (string flowerId in FlowerManager.Instance.GetOwnedIdsInDexOrder())
        {
            FlowerInstance instance = FlowerManager.Instance.GetInstance(flowerId);
            if (instance == null || !instance.isBloomed) continue; // 배치 대상은 개화한 꽃만
            if (GardenManager.Instance.IsPlaced(flowerId)) continue; // 이미 정원에 있으면 목록에서 제외

            FlowerData data = FlowerManager.Instance.GetFlowerData(flowerId);
            string name = data != null ? data.displayName : flowerId;

            Button item = Instantiate(rosterItemPrefab, rosterContent);
            TMP_Text label = item.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = $"{name}\n유대{instance.bondLevel}";
            BuildShapePreview(item.transform.Find(ShapePreviewChildName), data);

            Image bg = item.targetGraphic as Image;
            bool isArmed = flowerId == armedFlowerId;
            if (bg != null) bg.color = isArmed ? new Color(1f, 0.85f, 0.3f, 1f) : new Color(1f, 1f, 1f, 0.12f);

            string capturedId = flowerId; // 클로저 캡처용 로컬 복사
            item.onClick.RemoveAllListeners();
            item.onClick.AddListener(() => ArmFlower(capturedId));
            WireRosterDrag(item, capturedId);

            spawnedRosterItems.Add(item);
        }
    }

    /// <summary>
    /// 배치 대상 목록의 각 항목 아래에 그 꽃의 폴리오미노 모양을 작은 격자로 미리 보여준다
    /// (요청: "아래에서 모양을 미리 보여줬으면 좋겠어"). container는 프리팹에 미리 만들어 둔 빈
    /// GridLayoutGroup 컨테이너("ShapePreview") — 꽃마다 모양이 달라 칸은 항상 런타임에 새로
    /// 채운다. FlowerDataEditor의 5×5 정규화 규칙과 마찬가지로 gardenShape은 이미 좌상단 (0,0)
    /// 기준으로 저장돼 있으므로 그대로 바운딩 박스 크기만 구해서 채우면 된다.
    /// </summary>
    private static void BuildShapePreview(Transform container, FlowerData data)
    {
        if (container == null) return;

        for (int i = container.childCount - 1; i >= 0; i--)
            Destroy(container.GetChild(i).gameObject);

        List<Vector2Int> shape = (data != null && data.gardenShape != null && data.gardenShape.Count > 0)
            ? data.gardenShape
            : new List<Vector2Int> { Vector2Int.zero };

        int width = 1, height = 1;
        foreach (Vector2Int cell in shape)
        {
            if (cell.x + 1 > width) width = cell.x + 1;
            if (cell.y + 1 > height) height = cell.y + 1;
        }

        HashSet<Vector2Int> occupied = new HashSet<Vector2Int>(shape);

        GridLayoutGroup grid = container.GetComponent<GridLayoutGroup>();
        if (grid != null) grid.constraintCount = width;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                GameObject cellGO = new GameObject("Cell", typeof(RectTransform), typeof(Image));
                cellGO.transform.SetParent(container, false);
                Image img = cellGO.GetComponent<Image>();
                bool isOn = occupied.Contains(new Vector2Int(x, y));
                img.color = isOn ? new Color(1f, 0.55f, 0.75f, 0.9f) : new Color(1f, 1f, 1f, 0.06f);
            }
        }
    }

    private void HandleCellClicked(Vector2Int cell)
    {
        string occupyingId = GardenManager.Instance.GetFlowerIdAt(cell);

        if (occupyingId != null)
        {
            // 작업 1: 배치된 꽃을 탭하면 즉시 해제가 아니라 정보 패널을 연다. 제거는 그 패널의
            // "제거" 버튼으로 옮겼다 — 정보를 보지 않고 실수로 지우는 일이 없게.
            selectedFlowerId = occupyingId;
            if (infoPanel != null) infoPanel.Show(occupyingId);
            RebuildGrid(); // 하이라이트만 다시 그리면 되지만, 간단히 격자 전체를 다시 그린다
            return;
        }

        if (string.IsNullOrEmpty(armedFlowerId))
        {
            // 선택(정보 패널이 열려 하이라이트 중)돼 있는데 빈 칸을 탭하면 "빈 곳을 눌러서 선택 해제"로
            // 처리한다 — "하이라이트를 끄는 방법이 없다"는 신고에 대한 대응. infoPanel.Hide()가
            // OnClosed를 쏘고, GardenPanel.HandleInfoPanelClosed가 selectedFlowerId 정리와 다시
            // 그리기까지 알아서 처리한다.
            if (!string.IsNullOrEmpty(selectedFlowerId) && infoPanel != null)
                infoPanel.Hide();
            return;
        }

        bool placed = GardenManager.Instance.TryPlaceFlowerAtCell(armedFlowerId, cell);
        if (placed)
            armedFlowerId = null;
        // 실패하면 선택 상태를 유지한다 — 다른 칸에 다시 시도할 수 있게.
        RebuildAll();
    }

    private void ArmFlower(string flowerId)
    {
        // 정보 패널을 보다가 다른 꽃을 배치하려고 로스터를 탭하면, 격자 하이라이트가 남아 헷갈리지
        // 않도록 정보 패널을 먼저 닫는다(OnClosed가 selectedFlowerId도 함께 정리한다).
        if (!string.IsNullOrEmpty(selectedFlowerId) && infoPanel != null)
            infoPanel.Hide();

        armedFlowerId = flowerId;
        RebuildRoster(); // 선택 강조만 갱신하면 되지만, 간단히 목록 전체를 다시 그린다(꽃 수가 적어 비용 미미)
        UpdateArmedStatusText();
    }

    private void UpdateArmedStatusText()
    {
        if (armedStatusText == null) return;

        if (string.IsNullOrEmpty(armedFlowerId))
        {
            armedStatusText.text = "배치할 꽃을 아래에서 선택하거나 격자로 끌어다 놓으세요.";
            return;
        }

        FlowerData data = FlowerManager.Instance != null ? FlowerManager.Instance.GetFlowerData(armedFlowerId) : null;
        string name = data != null ? data.displayName : armedFlowerId;
        armedStatusText.text = $"선택됨: {name} — 격자의 빈 칸을 탭하세요. (배치된 꽃은 드래그로 이동/제거 가능)";
    }

    /// <summary>
    /// 작업 3 — 정원 총 G/s + "인접 효과 +N%"(핵심 지표: 인접 효과를 전부 제거했을 때와 지금 배치를
    /// 비교한 값. GardenManager.OnGardenChanged/FlowerManager 이벤트로 RebuildAll이 호출될 때마다
    /// 다시 계산되므로 배치를 바꾸면 실시간으로 갱신된다 — 매 프레임 폴링하지 않는다).
    /// </summary>
    private void UpdateSummary()
    {
        if (totalGpsText != null && FlowerManager.Instance != null)
        {
            BigNumber withAdjacency = FlowerManager.Instance.GetTotalGoldPerSecond(); // 시듦까지 반영된 실제 값
            BigNumber withRawAdjacency = FlowerManager.Instance.GetTotalGoldPerSecondWithRawAdjacency(); // 시듦 적용 전
            BigNumber withoutAdjacency = FlowerManager.Instance.GetTotalGoldPerSecondWithoutGardenAdjacency(); // 인접 효과 자체가 없을 때

            double withD = withAdjacency.ToDouble();
            double rawD = withRawAdjacency.ToDouble();
            double withoutD = withoutAdjacency.ToDouble();

            string adjacencyPercentText = "";
            if (withoutD > 0 && !double.IsInfinity(withD) && !double.IsInfinity(rawD) && !double.IsInfinity(withoutD))
            {
                double prePercent = (rawD / withoutD - 1.0) * 100.0;
                double postPercent = (withD / withoutD - 1.0) * 100.0;
                float wiltSeverity = GardenManager.Instance != null ? GardenManager.Instance.GetCurrentGlobalWiltMultiplier() : 1f;

                // 시듦이 실제로 뭔가를 깎고 있을 때만 "시듦 적용 전 → 후"로 나눠 보여준다 — 시듦이
                // 없으면(양호) 두 값이 같아서 나눠 보여줘 봐야 같은 숫자만 두 번 찍힌다.
                adjacencyPercentText = wiltSeverity < 0.999f && Math.Abs(prePercent - postPercent) > 0.05
                    ? $"  (인접 효과 {(prePercent >= 0 ? "+" : "")}{prePercent:0.#}% → 시듦 적용 후 {(postPercent >= 0 ? "+" : "")}{postPercent:0.#}% (×{wiltSeverity:0.00}))"
                    : $"  (인접 효과 {(postPercent >= 0 ? "+" : "")}{postPercent:0.#}%)";
            }

            string placementLine = "";
            string tendingLine = "";
            if (GardenManager.Instance != null)
            {
                int totalCells = GardenManager.Instance.Width * GardenManager.Instance.Height;
                // "N/M칸"은 칸 수여야 한다(폴리오미노 크기 반영) — 배치된 종 수(PlacedFlowerCount)와
                // 다르다. 해바라기(1×3) 하나만 놓아도 종은 1이지만 칸은 3.
                placementLine = $"\n배치 {GardenManager.Instance.OccupiedCellCount} / {totalCells}칸   정원 상태: {GardenManager.Instance.GetCurrentWiltStageLabel()}";

                // 시듦 복구 진행도(작업 지시 3.4) — 배치된 꽃이 있고 실제로 시들어 있을 때만 보여준다.
                // 안 그러면 전체 복구를 막 끝낸 직후(터치 카운트가 방금 비워짐)에 "0/N"이 다시 나타나
                // "시듦이 도로 생겼다"는 오해를 준다 — 지금은 페널티가 없으니 진행도 자체가 무의미하다.
                if (GardenManager.Instance.PlacedFlowerCount > 0 && GardenManager.Instance.IsCurrentlyWilted())
                {
                    (int done, int needed) = GardenManager.Instance.GetTendingProgress();
                    tendingLine = $"\n정원 손질 {done} / {needed}";
                }
            }

            totalGpsText.text = $"정원 총 {NumberFormatUtil.FormatPrecise(withAdjacency)} G/s{adjacencyPercentText}{placementLine}{tendingLine}";
        }

        if (expandButtonText != null && GardenManager.Instance != null)
        {
            List<GardenData.GardenSize> sizes = GardenManager.Instance.ActiveGardenData.sizes;
            int next = GardenManager.Instance.CurrentSizeIndex + 1;
            if (sizes != null && next < sizes.Count)
            {
                GardenData.GardenSize nextSize = sizes[next];
                expandButtonText.text = $"확장 ({nextSize.width}×{nextSize.height}) — {NumberFormatUtil.FormatGold(nextSize.unlockCost)}";
                if (expandButton != null) expandButton.interactable = true;
            }
            else
            {
                expandButtonText.text = "최대 크기";
                if (expandButton != null) expandButton.interactable = false;
            }
        }
    }

    private void HandleExpandClicked()
    {
        GardenManager.Instance.TryExpandGarden(); // 성공/실패 모두 OnGardenChanged 또는 무반응으로 자연히 처리됨
    }
}
