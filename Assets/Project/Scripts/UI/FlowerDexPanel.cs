using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 도감 그리드 팝업. 보유 여부와 무관하게 FlowerManager.allFlowers 전체를 나열하고
/// (미보유는 FlowerDexItem이 어둡게/잠금으로 표시), 항목을 선택하면 화면 전체를 덮는 상세 정보
/// (DetailPanel)를 보여준다. 실제로 메인 화면을 그 꽃으로 전환하는 것은 상세 화면의
/// "이 꽃으로 이동" 버튼을 눌러야만 일어난다.
///
/// 모바일/PC 화면 꽉 채우기: Awake() 시점에 Application.isMobilePlatform으로 딱 한 번만 판단해서
/// (프로젝트 전역에서 PCLayoutController가 사이드바를 켜고 끄는 것과 동일한 신호) 두 레이아웃 루트 중
/// 하나만 활성화한다. 이후 모든 로직(RebuildList/ShowDetail 등)은 "지금 활성화된 쪽"의 참조만 가리키는
/// 사설 필드(content/detailRoot/...)만 사용하므로, 모바일/PC 어느 쪽이든 코드 경로가 하나로 통일된다.
///
/// [중요] 이 컴포넌트가 붙은 GameObject(root)는 절대 SetActive(false)로 끄지 않는다.
/// Unity는 비활성 오브젝트의 Awake/Start를 스킵하므로, 여기서 자기 자신을 끄면 다음 씬 로드 때
/// Start()의 openButton.onClick.AddListener가 영영 실행되지 않아 "버튼은 눌리는데 아무 반응 없음"
/// 버그가 된다. 대신 CanvasGroup(alpha/interactable/blocksRaycasts)으로 보이기/숨기기만 전환한다.
/// </summary>
public class FlowerDexPanel : MonoBehaviour
{
    [Header("루트 (표시/숨김 대상, 비워두면 자기 자신 — 절대 SetActive(false)로 끄지 않음)")]
    public GameObject root;

    [Header("아이템 프리팹 (모바일/PC 그리드 공용)")]
    public FlowerDexItem itemPrefab;

    [Header("버튼 (Inspector/빌더에서 필드만 연결 — 리스너는 런타임에 스스로 붙인다)")]
    [Tooltip("이 도감을 여는 버튼(TopBar 등 외부 오브젝트). 에디터 스크립트에서 onClick.AddListener를 " +
             "직접 호출하면 Play 모드 밖이라 씬에 저장되지 않으므로, 필드 참조만 받아 Start()에서 스스로 연결한다.")]
    public Button openButton;
    public Button closeButton;

    [Header("모바일 레이아웃 (Application.isMobilePlatform == true일 때 사용)")]
    public GameObject mobileLayoutRoot;
    public Transform mobileGridContent;
    public GameObject mobileDetailRoot;
    public Image mobileDetailIcon;
    public TMP_Text mobileDetailName;
    public TMP_Text mobileDetailDescription;
    public Image[] mobileGrowthStageIcons;
    public Button[] mobileGrowthStageButtons;
    public TMP_Text mobileDetailInfo;
    public TMP_Text mobileDetailPassives;
    public Button mobileJumpButton;
    public Button mobileBackButton;
    [Tooltip("목록으로 안 돌아가고 도감 순서대로 옆 꽃 상세로 바로 이동한다.")]
    public Button mobilePrevButton;
    public Button mobileNextButton;
    public Button mobileMemorialButton;

    [Header("PC 레이아웃 (좌: 스탠딩 이미지 / 우: 데이터·소개말)")]
    public GameObject pcLayoutRoot;
    public Transform pcGridContent;
    public GameObject pcDetailRoot;
    public Image pcDetailIcon;
    public TMP_Text pcDetailName;
    public TMP_Text pcDetailDescription;
    public Image[] pcGrowthStageIcons;
    public Button[] pcGrowthStageButtons;
    public TMP_Text pcDetailInfo;
    public TMP_Text pcDetailPassives;
    public Button pcJumpButton;
    public Button pcBackButton;
    public Button pcPrevButton;
    public Button pcNextButton;
    public Button pcMemorialButton;

    [Header("PC 전용: 좌측 이미지 뷰어 (드래그로 패닝, 휠/버튼으로 확대·축소 — 모바일엔 '좌측' 개념이 없어 없음)")]
    public RectTransform pcDetailIconContent;
    public RectTransform pcDetailIconViewport;
    public TMP_Text pcDetailIconSizeText;
    public Button pcZoomInButton;
    public Button pcZoomOutButton;
    [Tooltip("클릭 시 확대율을 100%(=Image.SetNativeSize 기준, 스프라이트의 Pixels Per Unit을 반영한 실제 크기)로 리셋한다.")]
    public Button pcZoomResetButton;
    [Tooltip("확대율을 %로 직접 입력한다 — +/-/휠은 25%씩 계단식이라 정확한 값을 맞추기 어려운 문제를 해결.")]
    public TMP_InputField pcZoomInputField;
    public ScrollWheelZoomHandler pcIconScrollZoomHandler;

    [Tooltip("개발자 전용 도구 — 지금 확대/축소로 맞춘 화면 크기를 스프라이트의 실제 Pixels Per Unit 값으로 " +
             "역산해서 .meta 에셋에 영구 저장한다. #if UNITY_EDITOR 안에서만 동작하므로 실제 빌드에서는 " +
             "자동으로 숨겨진다(플레이어 악용 불가).")]
    public Button pcPpuSaveButton;

    private readonly List<FlowerDexItem> items = new List<FlowerDexItem>();
    private CanvasGroup canvasGroup;
    private string detailFlowerId;

    // ── Awake()가 모바일/PC 중 하나를 골라 채우는 "활성" 참조. 이후 로직은 전부 이것만 쓴다. ──
    private Transform content;
    private GameObject detailRoot;
    private Image detailIconImage;
    private TMP_Text detailNameText;
    private TMP_Text detailDescriptionText;
    private Image[] growthStageIcons;
    private Button[] growthStageButtons;
    private TMP_Text detailInfoText;
    private TMP_Text detailPassivesText;
    private Button jumpButton;
    private Button detailBackButton;
    private Button prevButton;
    private Button nextButton;
    private Button memorialButton;

    // PC 전용(모바일은 계속 null — 아래 로직들이 null 체크로 자연히 건너뛴다).
    private RectTransform detailIconContent;
    private RectTransform detailIconViewport;
    private TMP_Text detailIconSizeText;
    private Button zoomInButton;
    private Button zoomOutButton;
    private Button zoomResetButton;
    private TMP_InputField zoomInputField;
    private ScrollWheelZoomHandler iconScrollZoomHandler;
    private Button ppuSaveButton;

    // 지금 상세화면에 열려 있는 꽃(성장 갤러리 클릭 핸들러가 매번 다시 조회하지 않도록 캐시).
    private FlowerData currentDetailData;
    private FlowerInstance currentDetailInstance; // 미보유면 null

    private const float ZoomStepMultiplier = 1.25f;
    private const float MinZoomFactor = 0.02f;
    private const float MaxZoomFactor = 20f;
    private Vector2 detailIconBaseSize; // Image.SetNativeSize 기준 "100%"(PPU 반영) 크기
    private float iconZoomFactor = 1f;  // 1 = 100%

    private void Awake()
    {
        if (root == null) root = gameObject;

        canvasGroup = root.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = root.AddComponent<CanvasGroup>();

        bool useMobile = Application.isMobilePlatform;
        if (mobileLayoutRoot != null) mobileLayoutRoot.SetActive(useMobile);
        if (pcLayoutRoot != null) pcLayoutRoot.SetActive(!useMobile);

        if (useMobile)
        {
            content = mobileGridContent;
            detailRoot = mobileDetailRoot;
            detailIconImage = mobileDetailIcon;
            detailNameText = mobileDetailName;
            detailDescriptionText = mobileDetailDescription;
            growthStageIcons = mobileGrowthStageIcons;
            growthStageButtons = mobileGrowthStageButtons;
            detailInfoText = mobileDetailInfo;
            detailPassivesText = mobileDetailPassives;
            jumpButton = mobileJumpButton;
            detailBackButton = mobileBackButton;
            prevButton = mobilePrevButton;
            nextButton = mobileNextButton;
            memorialButton = mobileMemorialButton;
        }
        else
        {
            content = pcGridContent;
            detailRoot = pcDetailRoot;
            detailIconImage = pcDetailIcon;
            detailNameText = pcDetailName;
            detailDescriptionText = pcDetailDescription;
            growthStageIcons = pcGrowthStageIcons;
            growthStageButtons = pcGrowthStageButtons;
            detailInfoText = pcDetailInfo;
            detailPassivesText = pcDetailPassives;
            jumpButton = pcJumpButton;
            detailBackButton = pcBackButton;
            prevButton = pcPrevButton;
            nextButton = pcNextButton;
            memorialButton = pcMemorialButton;

            detailIconContent = pcDetailIconContent;
            detailIconViewport = pcDetailIconViewport;
            detailIconSizeText = pcDetailIconSizeText;
            zoomInButton = pcZoomInButton;
            zoomOutButton = pcZoomOutButton;
            zoomResetButton = pcZoomResetButton;
            zoomInputField = pcZoomInputField;
            iconScrollZoomHandler = pcIconScrollZoomHandler;
            ppuSaveButton = pcPpuSaveButton;
        }

        if (detailRoot != null) detailRoot.SetActive(false);
        SetVisible(false);
    }

    private void Start()
    {
        if (openButton != null) openButton.onClick.AddListener(Open);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (jumpButton != null) jumpButton.onClick.AddListener(ConfirmJumpToDetailFlower);
        if (detailBackButton != null) detailBackButton.onClick.AddListener(HideDetail);
        if (prevButton != null) prevButton.onClick.AddListener(() => NavigateDetail(-1));
        if (nextButton != null) nextButton.onClick.AddListener(() => NavigateDetail(1));
        if (memorialButton != null) memorialButton.onClick.AddListener(OpenMemorialForDetailFlower);

        if (zoomInButton != null) zoomInButton.onClick.AddListener(() => AdjustZoom(ZoomStepMultiplier));
        if (zoomOutButton != null) zoomOutButton.onClick.AddListener(() => AdjustZoom(1f / ZoomStepMultiplier));
        if (zoomResetButton != null) zoomResetButton.onClick.AddListener(ResetZoomToNative);
        if (zoomInputField != null) zoomInputField.onEndEdit.AddListener(OnZoomInputSubmitted);
        if (iconScrollZoomHandler != null) iconScrollZoomHandler.onScroll.AddListener(OnIconScroll);

        // 개발자 전용 도구 — 실제 빌드(플레이어)에는 SaveCurrentZoomAsAssetPpu 자체가 컴파일되지 않으므로,
        // 버튼도 그쪽에서는 그냥 숨겨서 죽은 버튼이 화면에 남지 않게 한다.
        if (ppuSaveButton != null)
        {
#if UNITY_EDITOR
            ppuSaveButton.onClick.AddListener(SaveCurrentZoomAsAssetPpu);
#else
            ppuSaveButton.gameObject.SetActive(false);
#endif
        }

        if (growthStageButtons != null)
        {
            for (int i = 0; i < growthStageButtons.Length; i++)
            {
                if (growthStageButtons[i] == null) continue;
                int stageIndex = i; // 클로저 캡처용 로컬 복사 — 반복 변수 그대로 캡처하면 전부 마지막 값이 됨
                growthStageButtons[i].onClick.AddListener(() => SelectGrowthStage(stageIndex));
            }
        }

        if (FlowerManager.Instance != null)
        {
            FlowerManager.Instance.OnOwnedFlowersChanged += RefreshItems;
            FlowerManager.Instance.OnFlowerBloomed += HandleFlowerBloomed;
            FlowerManager.Instance.OnDisplayedFlowerChanged += HandleDisplayedFlowerChanged;
        }
    }

    private void OnDestroy()
    {
        if (FlowerManager.Instance != null)
        {
            FlowerManager.Instance.OnOwnedFlowersChanged -= RefreshItems;
            FlowerManager.Instance.OnFlowerBloomed -= HandleFlowerBloomed;
            FlowerManager.Instance.OnDisplayedFlowerChanged -= HandleDisplayedFlowerChanged;
        }
    }

    private void HandleFlowerBloomed(string _) => RefreshItems(); // 개화 시 상태(레벨/성장%) 갱신
    private void HandleDisplayedFlowerChanged(string _) => RefreshItems(); // 현재 표시 중 강조 갱신

    /// <summary> 도감 열기. 열 때마다 최신 목록으로 다시 그리고, 상세 화면은 항상 닫힌 상태로 시작한다. </summary>
    public void Open()
    {
        RebuildList();
        HideDetail();
        SetVisible(true);
    }

    public void Close()
    {
        SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup == null) return;
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }

    /// <summary>
    /// FlowerDexItem이 선택됐을 때 호출: 즉시 이동하지 않고 상세 정보만 화면 전체에 겹쳐서 보여준다.
    /// 미보유 꽃도 호출은 가능하지만, 콘텐츠(소개말/이미지/성장 갤러리/패시브)는 잠그고
    /// "구매 시 해금됩니다" 안내로 대체한다 — 이름과 기본 스탯(필요 애정/초당 골드, 상점에서 볼 수 있는
    /// 수준의 정보)만 미리보기로 남겨서 구매 판단에는 도움을 준다.
    /// </summary>
    public void ShowDetail(string flowerId)
    {
        FlowerData data = FlowerManager.Instance != null ? FlowerManager.Instance.GetFlowerData(flowerId) : null;
        if (data == null) return;

        detailFlowerId = flowerId;
        FlowerInstance instance = FlowerManager.Instance.GetInstance(flowerId); // 미보유면 null
        bool owned = instance != null;

        currentDetailData = data;
        currentDetailInstance = instance;

        if (detailNameText != null)
            detailNameText.text = data.displayName;

        if (owned)
            ShowUnlockedContent(data, instance);
        else
            ShowLockedContent(data);

        if (jumpButton != null)
            jumpButton.interactable = owned;

        // 메모리얼은 유대로만 해금되고, 유대는 개화한 꽃만 쌓을 수 있으므로 미개화 꽃은 버튼을 비활성화한다.
        if (memorialButton != null)
            memorialButton.interactable = owned && instance.isBloomed;

        if (detailRoot != null) detailRoot.SetActive(true);
    }

    private void OpenMemorialForDetailFlower()
    {
        if (string.IsNullOrEmpty(detailFlowerId) || MemorialViewPanel.Instance == null) return;
        MemorialViewPanel.Instance.Open(detailFlowerId);
    }

    /// <summary> 보유한 꽃: 기존과 동일하게 소개말/성장 갤러리/스탯/패시브를 전부 보여준다. </summary>
    private void ShowUnlockedContent(FlowerData data, FlowerInstance instance)
    {
        if (detailDescriptionText != null)
            detailDescriptionText.text = string.IsNullOrEmpty(data.description) ? "" : data.description;

        SetGrowthGalleryInteractable(true);
        UpdateGrowthGallery(data, instance, true);

        string bloomStatus = instance.isBloomed ? "개화 완료" : $"성장 중 ({(instance.GetGrowthPercent(data.requiredAffection) * 100f):0}%)";
        BigNumber gps = instance.isBloomed
            ? FlowerManager.Instance.GetEffectiveGoldPerSecond(data, instance.currentLevel, instance.bondLevel)
            : BigNumber.FromDouble(data.baseGoldPerSecond);

        // 유대 레벨: "어느 아이와 시간을 덜 보냈는지"를 도감에서 한눈에 보여주는 게 이 시스템의 핵심
        // 목적이므로 생략하지 않는다. 미개화 꽃은 애초에 유대를 쌓을 수 없으므로(터치 대상이
        // 아니라 애정만 오르는 상태) 표시하지 않는다.
        string bondLine = instance.isBloomed
            ? $"유대 : Lv.{instance.bondLevel}{(instance.bondLevel >= FlowerManager.Instance.ActiveBondData.maxBondLevel ? " (MAX)" : "")}\n"
            : "";

        if (detailInfoText != null)
        {
            detailInfoText.text =
                $"필요 애정 : {NumberFormatUtil.Format(data.requiredAffection)}\n" +
                $"초당 골드 : {NumberFormatUtil.FormatPrecise(gps)} G/s\n" +
                $"레벨 : {instance.currentLevel}\n" +
                bondLine +
                $"개화 여부 : {bloomStatus}";
        }

        if (detailPassivesText != null)
            detailPassivesText.text = BuildPassiveText(data);

        SetZoomControlsInteractable(true);

        // 대표 이미지는 기본으로 최종 개화(bloom) 모습부터 보여준다. 성장 갤러리의 각 칸을 클릭하면
        // SelectGrowthStage가 이 이미지를 그 단계로 바꿔준다 — "역대 성장 과정 중 어느 것을 눌러도
        // 그 모습으로 바뀐다"는 요구가 여기서 실현된다.
        SelectGrowthStage((int)GrowthStage.Bloomed);
    }

    /// <summary>
    /// 미보유 꽃: 소개말/이미지/성장 갤러리/패시브를 전부 잠그고 "구매 시 해금됩니다"로 대체한다.
    /// 필요 애정/초당 골드(Lv.1 기준) 정도만 남겨서 상점 미리보기 수준의 정보는 유지한다.
    /// </summary>
    private void ShowLockedContent(FlowerData data)
    {
        if (detailDescriptionText != null)
            detailDescriptionText.text = "구매 시 해금됩니다.";

        if (detailInfoText != null)
        {
            detailInfoText.text =
                $"필요 애정 : {NumberFormatUtil.Format(data.requiredAffection)}\n" +
                $"초당 골드 : {NumberFormatUtil.FormatPrecise(data.baseGoldPerSecond)} G/s (Lv.1)\n" +
                $"레벨 : -\n" +
                $"개화 여부 : 미보유";
        }

        if (detailPassivesText != null)
            detailPassivesText.text = "";

        SetGrowthGalleryInteractable(false);
        HideGrowthGallery(); // 실루엣조차 보여주지 않고 완전히 숨긴다(대표 이미지와 동일한 수준으로 잠금)

        if (detailIconImage != null)
            detailIconImage.enabled = false; // 대표 이미지 자체를 숨긴다 — 실루엣도 보여주지 않는다

        SetZoomControlsInteractable(false);
        if (detailIconSizeText != null) detailIconSizeText.text = "";
    }

    /// <summary> 성장 갤러리 버튼들의 클릭 가능 여부를 한 번에 켜고 끈다(잠긴 꽃은 클릭해도 반응 없게). </summary>
    private void SetGrowthGalleryInteractable(bool interactable)
    {
        if (growthStageButtons == null) return;
        foreach (Button btn in growthStageButtons)
        {
            if (btn != null) btn.interactable = interactable;
        }
    }

    /// <summary> 성장 갤러리 아이콘을 실루엣조차 없이 완전히 숨긴다(미보유 잠금 화면 전용). </summary>
    private void HideGrowthGallery()
    {
        if (growthStageIcons == null) return;
        foreach (Image icon in growthStageIcons)
        {
            if (icon != null) icon.enabled = false;
        }
    }

    /// <summary> PC 전용 확대/축소 컨트롤의 클릭 가능 여부를 한 번에 켜고 끈다(잠긴 꽃은 볼 이미지가 없으므로). </summary>
    private void SetZoomControlsInteractable(bool interactable)
    {
        if (zoomInButton != null) zoomInButton.interactable = interactable;
        if (zoomOutButton != null) zoomOutButton.interactable = interactable;
        if (zoomResetButton != null) zoomResetButton.interactable = interactable;
    }

    /// <summary>
    /// 성장 갤러리의 stageIndex번째 칸을 클릭했을 때(또는 상세화면을 처음 열 때 기본값 bloom으로) 호출.
    /// 좌측(모바일은 상단) 대표 이미지를 그 단계의 스프라이트로 바꾼다. 실제로 그 단계까지 도달하지
    /// 못했으면(미보유 포함) 갤러리 칸과 동일하게 실루엣으로 표시해서 스포일러를 막는다 — "구매만
    /// 해도 최종 모습이 보이면 안 된다"는 원칙을 여기서도 그대로 지킨다.
    /// </summary>
    private void SelectGrowthStage(int stageIndex)
    {
        if (currentDetailData == null || detailIconImage == null) return;

        Sprite[] stageSprites =
        {
            currentDetailData.seedSprite, currentDetailData.sproutSprite,
            currentDetailData.growingSprite, currentDetailData.bloomSprite
        };
        if (stageIndex < 0 || stageIndex >= stageSprites.Length) return;

        bool owned = currentDetailInstance != null;
        int reachedIndex = owned ? (int)currentDetailInstance.GetGrowthStage(currentDetailData.requiredAffection) : -1;

        detailIconImage.sprite = stageSprites[stageIndex];
        detailIconImage.enabled = stageSprites[stageIndex] != null;
        detailIconImage.color = (stageIndex <= reachedIndex) ? Color.white : new Color(0.08f, 0.08f, 0.08f, 1f);

        RefreshIconBaseSizeAndFit();
    }

    /// <summary>
    /// PC 전용: 지금 표시 중인 스프라이트의 Image.SetNativeSize 기준 "100%" 크기(스프라이트의
    /// Pixels Per Unit을 반영한 실제 렌더링 크기 — 같은 텍스처 픽셀 수여도 PPU가 다르면 다르게 나온다)를
    /// 다시 계산하고, 확대율을 100%(=PPU 기준 실제 크기)로 리셋한다. 새 스프라이트로 바뀔 때(단계 클릭,
    /// 새 꽃 열기)마다 호출한다.
    ///
    /// [기본값이 "화면에 맞춤"이 아니라 "100%"인 이유] 뷰포트에 맞춰 자동으로 줄이거나 늘리면
    /// 어떤 꽃이든 화면상 크기가 항상 비슷해 보여서, 정작 비교하고 싶은 "실제 크기 차이"가 가려진다.
    /// 100%(PPU 반영 실제 크기)로 고정해야 꽃마다/단계마다 크기가 다르면 다른 만큼 화면에 다르게
    /// 보이고, 그 차이를 보고 에셋 크기를 조정할지 판단할 수 있다. 뷰포트보다 커져서 잘리는 부분은
    /// 드래그 패닝으로 마저 확인하면 된다 — 화면에 맞춰 축소해버리면 오히려 비교가 안 된다.
    /// detailIconViewport 등이 전부 PC 전용이라 모바일에서는 아무 것도 하지 않고 끝난다.
    /// </summary>
    private void RefreshIconBaseSizeAndFit()
    {
        if (detailIconImage == null || detailIconImage.sprite == null || detailIconViewport == null) return;

        detailIconImage.SetNativeSize();
        detailIconBaseSize = detailIconImage.rectTransform.sizeDelta;

        iconZoomFactor = 1f; // 항상 PPU 기준 실제 크기(100%)로 시작 — "화면에 맞춤"으로 자동 축소/확대하지 않는다
        ApplyIconZoom();
    }

    private void AdjustZoom(float multiplier)
    {
        iconZoomFactor = Mathf.Clamp(iconZoomFactor * multiplier, MinZoomFactor, MaxZoomFactor);
        ApplyIconZoom();
    }

    /// <summary> "100%" 버튼 전용: PPU 기준 실제 크기로 정확히 되돌린다(맞춤 크기 배율과 무관하게). </summary>
    private void ResetZoomToNative()
    {
        iconZoomFactor = 1f;
        ApplyIconZoom();
    }

    /// <summary>
    /// 확대율 입력 필드에서 Enter를 누르거나 포커스를 벗어나면 호출된다(TMP_InputField.onEndEdit).
    /// +/-/휠은 25%씩 계단식으로만 움직여서 "정확히 137%" 같은 임의의 값을 맞추기 어렵다는 문제를
    /// 해결한다 — 원하는 숫자를 직접 쳐서 정확히 그 배율로 맞출 수 있다.
    /// 파싱 실패/0 이하/범위를 벗어난 값은 무시하고, 어느 경우든 필드를 실제 적용된 값으로 다시
    /// 맞춰서(ApplyIconZoom이 처리) 사용자가 항상 "지금 실제로 적용된 숫자"를 보게 한다.
    /// </summary>
    private void OnZoomInputSubmitted(string text)
    {
        if (float.TryParse(text, out float percent) && percent > 0f)
        {
            iconZoomFactor = Mathf.Clamp(percent / 100f, MinZoomFactor, MaxZoomFactor);
        }

        ApplyIconZoom();
    }

    private void OnIconScroll(float scrollDeltaY)
    {
        if (Mathf.Approximately(scrollDeltaY, 0f)) return;
        AdjustZoom(scrollDeltaY > 0f ? ZoomStepMultiplier : 1f / ZoomStepMultiplier);
    }

#if UNITY_EDITOR
    /// <summary>
    /// 개발자 전용: 드래그/휠/버튼으로 지금 화면에 맞춰 놓은 확대율을 스프라이트의 실제 Pixels Per
    /// Unit(PPU) 값으로 역산해서 .meta 에셋에 영구 저장한다("게임 실행 → 도감 확인 → 게임 종료 →
    /// 에셋 열어서 PPU 수동 조절 → 다시 게임 실행 → 도감 재확인"을 반복하지 않아도 되게 하는 것이 목적).
    /// 저장 직후 메인 화면 등 이 스프라이트를 쓰는 다른 곳에도(재생 중이면 다음 프레임부터) 그대로 반영된다.
    ///
    /// 계산: SetNativeSize()가 만드는 크기는 spriteRect / (spritePpu / canvasReferencePpu)에 비례한다.
    /// "지금 화면에 보이는 크기"(=baseSize * iconZoomFactor)를 100%(zoomFactor=1)에서도 그대로
    /// 보이게 만드는 새 PPU는 newPpu = oldPpu / iconZoomFactor로 역산된다.
    ///
    /// [플레이어 악용 방지] UnityEditor.TextureImporter 등은 UNITY_EDITOR 심볼이 정의된 에디터
    /// 컴파일에서만 존재하는 API라, 이 메서드 자체가 실제 빌드(플레이어)에는 통째로 포함되지 않는다.
    /// Start()도 빌드에서는 이 메서드를 참조하는 대신 버튼을 숨기므로, 플레이어가 접근할 방법이 없다.
    /// </summary>
    private void SaveCurrentZoomAsAssetPpu()
    {
        if (detailIconImage == null || detailIconImage.sprite == null) return;

        if (Mathf.Approximately(iconZoomFactor, 1f))
        {
            Debug.Log("[FlowerDexPanel] 이미 100%(현재 저장된 크기 그대로)라서 저장할 변경 사항이 없습니다.");
            return;
        }

        Sprite sprite = detailIconImage.sprite;
        string path = UnityEditor.AssetDatabase.GetAssetPath(sprite);
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogWarning($"[FlowerDexPanel] '{sprite.name}'의 에셋 경로를 찾을 수 없어 저장하지 못했습니다.");
            return;
        }

        if (!(UnityEditor.AssetImporter.GetAtPath(path) is UnityEditor.TextureImporter importer))
        {
            Debug.LogWarning($"[FlowerDexPanel] TextureImporter를 찾을 수 없습니다: {path}");
            return;
        }

        float oldPpu = sprite.pixelsPerUnit;
        float newPpu = Mathf.Max(1f, oldPpu / iconZoomFactor);

        importer.spritePixelsPerUnit = newPpu;
        importer.SaveAndReimport();

        Debug.Log($"[FlowerDexPanel] '{sprite.name}' PPU 저장됨: {oldPpu:0} → {newPpu:0} ({path})");

        // 리임포트로 스프라이트가 갱신됐으니, 지금 보이던 크기 그대로 다시 100%로 맞춰준다 —
        // 그냥 두면 이전 배율이 새 실제 크기에 다시 곱해져서 화면 크기가 튀어 보인다.
        RefreshIconBaseSizeAndFit();
    }
#endif

    /// <summary>
    /// detailIconBaseSize(PPU 기준 100%)에 iconZoomFactor를 곱해 실제 표시 크기를 반영하고,
    /// 뷰포트보다 커지면 IconScrollView가 스크롤(드래그 패닝)로 구석구석 볼 수 있도록 Content 크기도
    /// 맞춰 키운다. 크기 안내 텍스트에 원본 해상도 + 현재 확대율을 함께 보여줘서 "에셋 크기 조정의
    /// 근거"로 쓸 수 있게 한다.
    /// </summary>
    private void ApplyIconZoom()
    {
        if (detailIconImage == null) return;

        Vector2 targetSize = detailIconBaseSize * iconZoomFactor;
        detailIconImage.rectTransform.sizeDelta = targetSize;

        if (detailIconContent != null)
        {
            Vector2 viewportSize = detailIconViewport != null ? detailIconViewport.rect.size : targetSize;
            detailIconContent.sizeDelta = new Vector2(
                Mathf.Max(targetSize.x, viewportSize.x),
                Mathf.Max(targetSize.y, viewportSize.y));
        }

        if (detailIconSizeText != null && detailIconImage.sprite != null)
        {
            // 이제 기본값 자체가 100%(PPU 반영 실제 크기)이므로, 숫자를 따로 안 읽어도 화면에 보이는
            // 크기 자체가 곧 비교 기준이다. 확대율은 사용자가 +/-나 휠로 직접 조절했을 때만 100%에서
            // 벗어난다.
            Rect spriteRect = detailIconImage.sprite.rect;
            detailIconSizeText.text =
                $"이미지 크기 : {(int)spriteRect.width} x {(int)spriteRect.height} px  (확대율 {iconZoomFactor * 100f:0}%)";
        }

        // +/-, 휠, "100%" 등 어떤 경로로 확대율이 바뀌었든 입력 필드도 항상 최신값으로 맞춰둔다
        // (SetTextWithoutNotify라 여기서 다시 OnZoomInputSubmitted가 재귀 호출되지 않는다).
        if (zoomInputField != null)
            zoomInputField.SetTextWithoutNotify($"{iconZoomFactor * 100f:0}");
    }

    /// <summary>
    /// "역대 성장 과정" 갤러리: 씨앗 → 발아 → 성장 → 개화 스프라이트를 순서대로 보여주고,
    /// 이미 거쳐 간 단계까지는 원색, 아직 안 거친 단계는 실루엣으로 어둡게 표시한다.
    ///
    /// [향후 확장 포인트] 이 배열(growthStageIcons)과 stageSprites는 둘 다 인덱스 기반이라,
    /// FlowerData에 성장 단계를 하나 더 추가하고 싶으면 (1) FlowerData에 스프라이트 필드 추가,
    /// (2) 아래 stageSprites 배열에 한 줄 추가, (3) PCLayoutBuilder.BuildGrowthGallery가 만드는
    /// 아이콘 개수(현재 4개)만 맞춰주면 끝난다 — 필드 하나하나를 새로 연결할 필요가 없다.
    /// GrowthStage enum 값(Seed=0/Sprout=1/Growing=2/Bloomed=3)이 배열 인덱스와 그대로 대응한다.
    /// </summary>
    private void UpdateGrowthGallery(FlowerData data, FlowerInstance instance, bool owned)
    {
        if (growthStageIcons == null) return;

        Sprite[] stageSprites = { data.seedSprite, data.sproutSprite, data.growingSprite, data.bloomSprite };

        // 미보유 꽃은 아직 어떤 단계도 거치지 않았으므로 전부 실루엣으로 표시한다(reachedIndex = -1).
        int reachedIndex = owned ? (int)instance.GetGrowthStage(data.requiredAffection) : -1;

        for (int i = 0; i < growthStageIcons.Length; i++)
        {
            Image icon = growthStageIcons[i];
            if (icon == null) continue;

            Sprite sprite = i < stageSprites.Length ? stageSprites[i] : null;
            icon.sprite = sprite;
            icon.enabled = sprite != null;
            icon.color = (i <= reachedIndex) ? Color.white : new Color(0.08f, 0.08f, 0.08f, 1f);
        }
    }

    /// <summary>
    /// 꽃의 패시브 목록을 "이름 : 설명" 줄바꿈 나열로 만든다. 패시브가 없으면 빈 문자열.
    /// effectType이 None인 항목(인스펙터에 자리만 만들어두고 아직 안 채운 빈 슬롯, 예: 튤립)은
    /// 빈 줄로 나오지 않도록 건너뛴다.
    /// </summary>
    private string BuildPassiveText(FlowerData data)
    {
        if (data.passives == null || data.passives.Count == 0) return "";

        var sb = new StringBuilder();
        bool isFirst = true;
        foreach (PassiveData p in data.passives)
        {
            if (p == null || p.effectType == PassiveEffectType.None) continue;

            string name = string.IsNullOrEmpty(p.displayName) ? p.passiveId : p.displayName;
            if (string.IsNullOrEmpty(name)) continue;

            if (!isFirst) sb.Append('\n');
            isFirst = false;
            sb.Append(string.IsNullOrEmpty(p.description) ? name : $"{name} : {p.description}");
        }
        return sb.ToString();
    }

    private void HideDetail()
    {
        if (detailRoot != null) detailRoot.SetActive(false);
        detailFlowerId = null;
    }

    /// <summary>
    /// 도감이 열려 있고, 상세 화면(그리드가 아니라)을 보고 있을 때만 방향키(←/→)로도
    /// 이전/다음 버튼과 동일하게 동작한다. 확대율 직접 입력 칸(pcZoomInputField)에 커서가 가 있을
    /// 때는 방향키가 "커서 이동" 목적이므로 가로채지 않는다.
    /// </summary>
    private void Update()
    {
        if (Keyboard.current == null) return;
        if (canvasGroup == null || canvasGroup.alpha <= 0f) return; // 도감이 닫혀 있음
        if (detailRoot == null || !detailRoot.activeSelf) return;   // 그리드를 보고 있음 (상세 아님)
        if (IsTextInputFieldFocused()) return;

        if (Keyboard.current.leftArrowKey.wasPressedThisFrame) NavigateDetail(-1);
        else if (Keyboard.current.rightArrowKey.wasPressedThisFrame) NavigateDetail(1);
    }

    private static bool IsTextInputFieldFocused()
    {
        if (EventSystem.current == null) return false;
        GameObject selected = EventSystem.current.currentSelectedGameObject;
        if (selected == null) return false;
        return selected.GetComponent<TMP_InputField>() != null;
    }

    /// <summary>
    /// "이전"/"다음" 버튼 전용: 목록으로 돌아가지 않고 도감 순서(FlowerManager.allFlowers, 그리드와
    /// 동일한 순서) 기준으로 옆 꽃의 상세로 곧장 이동한다. 보유 여부와 무관하게(잠긴 꽃도 포함) 전체
    /// 목록을 그대로 순회하고, 끝에서는 반대쪽 끝으로 돌아간다(순환).
    /// </summary>
    private void NavigateDetail(int direction)
    {
        if (FlowerManager.Instance == null || string.IsNullOrEmpty(detailFlowerId)) return;

        List<FlowerData> allFlowers = FlowerManager.Instance.allFlowers;
        int count = allFlowers.Count;
        if (count <= 1) return;

        int currentIndex = allFlowers.FindIndex(f => f != null && f.flowerId == detailFlowerId);
        if (currentIndex < 0) return;

        int nextIndex = (currentIndex + direction + count) % count;
        FlowerData nextData = allFlowers[nextIndex];
        if (nextData != null) ShowDetail(nextData.flowerId);
    }

    /// <summary> 상세 화면의 "이 꽃으로 이동" 버튼 전용: 실제로 메인 화면을 전환하고 도감을 닫는다. </summary>
    private void ConfirmJumpToDetailFlower()
    {
        if (string.IsNullOrEmpty(detailFlowerId)) return;
        if (FlowerManager.Instance != null) FlowerManager.Instance.TryJumpTo(detailFlowerId);
        Close();
    }

    private void RebuildList()
    {
        if (content == null || itemPrefab == null || FlowerManager.Instance == null)
            return;

        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);
        items.Clear();

        // 도감순 = FlowerManager.allFlowers 순서. 보유 여부와 무관하게 전부 표시(미보유는 실루엣/잠금).
        foreach (FlowerData data in FlowerManager.Instance.allFlowers)
        {
            if (data == null) continue;

            FlowerDexItem item = Instantiate(itemPrefab, content);
            item.Setup(data.flowerId, this);
            items.Add(item);
        }
    }

    private void RefreshItems()
    {
        foreach (FlowerDexItem item in items)
        {
            if (item != null) item.Refresh();
        }
    }
}
