using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TutorialManager : MonoBehaviour
{
    private enum TutorialHighlightTarget
    {
        None,
        Flower,
        AffectionGauge,
        TouchAffectionUpgrade,
        PlayerUpgradeTab,
        GoldDisplay,
        Shop,
        FlowerDex,
        DandelionDexItem,
        FlowerDexClose,
        Garden
    }

    private struct TutorialStep
    {
        public string title;
        public string description;
        public TutorialHighlightTarget highlightTarget;
        public bool waitForFlowerTap;
        public bool waitForTouchAffectionUpgrade;

        public TutorialStep(string title, string description,
            TutorialHighlightTarget highlightTarget = TutorialHighlightTarget.None,
            bool waitForFlowerTap = false, bool waitForTouchAffectionUpgrade = false)
        {
            this.title = title;
            this.description = description;
            this.highlightTarget = highlightTarget;
            this.waitForFlowerTap = waitForFlowerTap;
            this.waitForTouchAffectionUpgrade = waitForTouchAffectionUpgrade;
        }
    }

    private static readonly TutorialStep[] Steps =
    {
        new TutorialStep("꽃을 터치해 보세요", "꽃을 터치하면 골드를 얻습니다. 아직 피지 않은 꽃은 애정도 함께 쌓입니다. 꽃을 한 번 터치하면 다음 설명으로 넘어갈 수 있습니다.", TutorialHighlightTarget.Flower, true),
        new TutorialStep("애정과 개화", "애정 게이지는 꽃이 피기까지의 진행도입니다. 터치 애정은 터치 성장량을, 자동 애정은 보유한 미개화 꽃들의 성장 속도를 높입니다.", TutorialHighlightTarget.AffectionGauge),
        new TutorialStep("터치 애정 강화 체험", "성장 메뉴의 플레이어 탭에서 터치 애정 강화 버튼을 눌러 보세요. 강화하면 꽃을 한 번 터치할 때 얻는 애정이 증가합니다. 버튼을 누르면 실제 골드가 사용됩니다.", TutorialHighlightTarget.TouchAffectionUpgrade, false, true),
        new TutorialStep("골드와 레벨 강화", "꽃이 개화하면 초당 골드를 생산합니다. 꽃 레벨을 올리면 그 꽃의 초당 생산량이 증가합니다. 터치 골드 스탯은 꽃 상태와 관계없이 터치로 얻는 골드를 늘립니다.", TutorialHighlightTarget.GoldDisplay),
        new TutorialStep("상점과 도감", "상점에서 골드로 씨앗을 구매해 새 꽃을 만날 수 있습니다. 도감에서는 만난 꽃과 성장 정보를 확인할 수 있습니다.", TutorialHighlightTarget.Shop),
        new TutorialStep("도감에서 꽃 확인", "상단의 도감 버튼을 눌러 꽃의 정보와 성장 단계를 확인해 보세요.", TutorialHighlightTarget.FlowerDex),
        new TutorialStep("패시브 효과", "특정 꽃의 패시브는 그 꽃이 개화하면 활성화됩니다. 활성 패시브는 씨앗·레벨업 비용 할인, 골드 생산 보너스 등 전역 효과를 줄 수 있습니다."),
        new TutorialStep("유대와 메모리얼", "개화한 꽃을 터치하면 골드와 함께 유대가 쌓입니다. 유대 레벨은 해당 꽃의 생산량과 정원 인접 효과를 강화하며, 일부 레벨에서는 메모리얼을 해금합니다."),
        new TutorialStep("정원", "성장 패널의 정원 탭에서 개화한 꽃을 배치해 인접 효과를 얻고 유대를 쌓을 수 있습니다. 방치하면 인접 보너스가 시들 수 있으며, 타일을 손질해 회복할 수 있습니다.", TutorialHighlightTarget.Garden),
        new TutorialStep("준비 완료", "꽃을 터치하고 키우며 골드를 모으세요. 개화한 꽃을 늘리고, 스탯·패시브·유대·정원을 활용해 정원을 발전시킬 수 있습니다.")
    };

    private static TutorialManager instance;

    private Canvas targetCanvas;
    private CanvasGroup canvasGroup;
    private RectTransform overlayRect;
    private RectTransform highlightRect;
    private RectTransform currentHighlightTarget;
    private TMP_Text titleText;
    private TMP_Text descriptionText;
    private TMP_Text progressText;
    private TMP_Text hintText;
    private RectTransform previousButtonRect;
    private Button nextButton;
    private Button previousButton;
    private TMP_Text nextButtonText;

    private int currentStepIndex;
    private bool isFirstRunSession;
    private readonly System.Collections.Generic.HashSet<int> completedFlowerTapSteps = new System.Collections.Generic.HashSet<int>();
    private bool isReplaySession;
    private bool touchAffectionUpgradeCompleted;
    private int touchAffectionLevelAtStepStart = -1;
    private bool flowerDexEnteredDuringStep;
    private bool isVisible;

    public static bool IsRunning => instance != null && instance.isVisible;

    public static TutorialManager EnsureInstance()
    {
        if (instance != null) return instance;

        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null) return null;

        TutorialManager manager = canvas.GetComponent<TutorialManager>();
        return manager != null ? manager : canvas.gameObject.AddComponent<TutorialManager>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;
        targetCanvas = GetComponent<Canvas>();
        if (targetCanvas == null) targetCanvas = GetComponentInParent<Canvas>();
        if (targetCanvas != null) BuildOverlay();
    }

    private void OnDestroy()
    {
        if (FlowerManager.Instance != null)
        {
            FlowerManager.Instance.OnCurrentFlowerClicked -= HandleCurrentFlowerClicked;
            FlowerManager.Instance.OnCurrentFlowerClicked -= HandleTutorialFlowerClicked;
        }

        if (instance == this) instance = null;
    }

    private void Update()
    {
        if (!isVisible) return;
        overlayRect.SetAsLastSibling();

        TutorialStep step = Steps[currentStepIndex];
        if (step.waitForTouchAffectionUpgrade && !touchAffectionUpgradeCompleted)
            UpdateTouchAffectionExperience();

        FlowerDexPanel openDex = Object.FindAnyObjectByType<FlowerDexPanel>(FindObjectsInactive.Include);
        TutorialHighlightTarget highlightTarget;
        if (openDex != null && openDex.IsOpen)
        {
            flowerDexEnteredDuringStep = true;
            if (step.highlightTarget == TutorialHighlightTarget.FlowerDex && !openDex.IsDetailOpen
                && openDex.GetFlowerItemRect("dandelion") != null)
                highlightTarget = TutorialHighlightTarget.DandelionDexItem;
            else
                highlightTarget = TutorialHighlightTarget.FlowerDexClose;
        }
        else
        {
            highlightTarget = step.waitForTouchAffectionUpgrade
                ? GetTouchAffectionExperienceHighlight()
                : step.highlightTarget;

        }
        currentHighlightTarget = GetHighlightRect(highlightTarget);
        UpdateCurrentHighlight(highlightTarget);
    }

    public void StartFirstRunTutorial()
    {
        StartTutorial(true);
    }

    public void ReplayFromSettings()
    {
        bool firstRunIncomplete = SaveManager.Instance != null && !SaveManager.Instance.IsTutorialCompleted;
        StartTutorial(firstRunIncomplete, !firstRunIncomplete);
    }

    private void StartTutorial(bool firstRunSession, bool replaySession = false)
    {
        if (targetCanvas == null || canvasGroup == null) return;

        UnsubscribeFromFlowerClick();
        isFirstRunSession = firstRunSession;
        isReplaySession = replaySession;
        completedFlowerTapSteps.Clear();
        flowerDexEnteredDuringStep = false;
        currentStepIndex = 0;
        touchAffectionUpgradeCompleted = false;
        touchAffectionLevelAtStepStart = -1;
        isVisible = true;

        if (FlowerManager.Instance != null)
        {
            FlowerManager.Instance.OnCurrentFlowerClicked += HandleCurrentFlowerClicked;
            FlowerManager.Instance.OnCurrentFlowerClicked += HandleTutorialFlowerClicked;
        }

        overlayRect.SetAsLastSibling();
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        ShowCurrentStep();
    }

    private void BuildOverlay()
    {
        GameObject overlay = new GameObject("TutorialOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(GraphicRaycaster));
        overlay.transform.SetParent(targetCanvas.rootCanvas.transform, false);
        overlayRect = overlay.GetComponent<RectTransform>();
        StretchToParent(overlayRect);
        Canvas overlayCanvas = overlay.GetComponent<Canvas>();
        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder = short.MaxValue;
        overlay.GetComponent<GraphicRaycaster>().ignoreReversedGraphics = true;
        canvasGroup = overlay.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        GameObject highlight = new GameObject("TutorialHighlight", typeof(RectTransform), typeof(Image), typeof(Outline));
        highlight.transform.SetParent(overlay.transform, false);
        highlightRect = highlight.GetComponent<RectTransform>();
        Image highlightImage = highlight.GetComponent<Image>();
        highlightImage.color = new Color(1f, 0.8f, 0.1f, 0.12f);
        highlightImage.raycastTarget = false;
        Outline outline = highlight.GetComponent<Outline>();
        outline.effectColor = new Color(1f, 0.82f, 0.1f, 1f);
        outline.effectDistance = new Vector2(6f, 6f);
        RectTransform highlightTransform = highlight.GetComponent<RectTransform>();
        highlightTransform.anchorMin = new Vector2(0.5f, 0.5f);
        highlightTransform.anchorMax = new Vector2(0.5f, 0.5f);
        highlightTransform.pivot = new Vector2(0.5f, 0.5f);
        highlight.SetActive(false);

        GameObject card = new GameObject("TutorialCard", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(overlay.transform, false);
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        PositionCardOverGameArea(cardRect);
        card.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.13f, 0.96f);
        card.GetComponent<Image>().raycastTarget = true;

        TMP_FontAsset font = GetAvailableFont();
        titleText = CreateText(card.transform, "Title", font, 24f, FontStyles.Bold,
            new Vector2(0.04f, 0.72f), new Vector2(0.96f, 0.98f), TextAlignmentOptions.Left);
        descriptionText = CreateText(card.transform, "Description", font, 18f, FontStyles.Normal,
            new Vector2(0.04f, 0.30f), new Vector2(0.96f, 0.72f), TextAlignmentOptions.TopLeft);
        progressText = CreateText(card.transform, "Progress", font, 16f, FontStyles.Normal,
            new Vector2(0.04f, 0.04f), new Vector2(0.22f, 0.22f), TextAlignmentOptions.Left);
        hintText = CreateText(card.transform, "Hint", font, 16f, FontStyles.Bold,
            new Vector2(0.23f, 0.04f), new Vector2(0.50f, 0.22f), TextAlignmentOptions.Left);

        previousButton = CreateButton(card.transform, "PreviousButton", "이전", font,
            new Vector2(0.52f, 0.04f), new Vector2(0.66f, 0.22f), new Color(0.32f, 0.34f, 0.4f, 1f), GoToPreviousStep);
        previousButtonRect = previousButton.GetComponent<RectTransform>();
        CreateButton(card.transform, "SkipButton", "전체 건너뛰기", font,
            new Vector2(0.68f, 0.04f), new Vector2(0.82f, 0.22f), new Color(0.32f, 0.34f, 0.4f, 1f), SkipTutorial);
        Button next = CreateButton(card.transform, "NextButton", "다음", font,
            new Vector2(0.84f, 0.04f), new Vector2(0.96f, 0.22f), new Color(0.85f, 0.35f, 0.58f, 1f), AdvanceStep);
        nextButton = next;
        nextButtonText = next.GetComponentInChildren<TMP_Text>();

    }

    private void PositionCardOverGameArea(RectTransform cardRect)
    {
        RectTransform gameArea = FindTransformRecursive(targetCanvas.rootCanvas.transform, "MobileGameArea") as RectTransform;
        if (gameArea == null)
        {
            cardRect.anchoredPosition = Vector2.zero;
            cardRect.sizeDelta = new Vector2(overlayRect.rect.width * 0.92f, overlayRect.rect.height * 0.23f);
            return;
        }

        Vector3[] corners = new Vector3[4];
        gameArea.GetWorldCorners(corners);
        Camera uiCamera = targetCanvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : targetCanvas.rootCanvas.worldCamera;
        Vector2 bottomLeft;
        Vector2 topRight;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayRect,
            RectTransformUtility.WorldToScreenPoint(uiCamera, corners[0]), uiCamera, out bottomLeft);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayRect,
            RectTransformUtility.WorldToScreenPoint(uiCamera, corners[2]), uiCamera, out topRight);

        Vector2 areaSize = topRight - bottomLeft;
        cardRect.anchoredPosition = bottomLeft + new Vector2(areaSize.x * 0.5f, areaSize.y * 0.70f);
        cardRect.sizeDelta = new Vector2(areaSize.x * 0.92f, areaSize.y * 0.23f);
    }

    private static Transform FindTransformRecursive(Transform parent, string objectName)
    {
        if (parent == null) return null;
        if (parent.name == objectName) return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindTransformRecursive(parent.GetChild(i), objectName);
            if (found != null) return found;
        }

        return null;
    }

    private void ShowCurrentStep()
    {
        TutorialStep step = Steps[currentStepIndex];
        titleText.text = step.title;
        descriptionText.text = step.description;
        progressText.text = $"튜토리얼 {currentStepIndex + 1} / {Steps.Length}";
        bool flowerTapped = !step.waitForFlowerTap || completedFlowerTapSteps.Contains(currentStepIndex);

        if (step.waitForTouchAffectionUpgrade && touchAffectionLevelAtStepStart < 0)
            touchAffectionLevelAtStepStart = PlayerStatManager.Instance != null
                ? PlayerStatManager.Instance.GetLevel(PlayerStatType.TouchAffection)
                : 0;

        if (step.waitForTouchAffectionUpgrade && isReplaySession && PlayerStatManager.Instance != null)
        {
            PlayerStatData data = PlayerStatManager.Instance.GetData(PlayerStatType.TouchAffection);
            if (data != null && PlayerStatManager.Instance.GetLevel(PlayerStatType.TouchAffection) > data.startingLevel
                && !touchAffectionUpgradeCompleted)
            {
                touchAffectionUpgradeCompleted = true;
                touchAffectionLevelAtStepStart = PlayerStatManager.Instance.GetLevel(PlayerStatType.TouchAffection);
            }
        }

        nextButton.interactable = (!step.waitForFlowerTap || flowerTapped)
            && (!step.waitForTouchAffectionUpgrade || touchAffectionUpgradeCompleted);
        if (step.waitForFlowerTap)
            hintText.text = flowerTapped ? "꽃을 터치해 주세요." : "꽃을 한 번 터치해 주세요.";
        else if (step.waitForTouchAffectionUpgrade && touchAffectionUpgradeCompleted)
            hintText.text = isReplaySession ? "이미 강화된 능력입니다. 버튼을 확인하고 다음으로 진행하세요." : "강화 성공! 다음을 눌러 계속하세요.";
        else if (step.waitForTouchAffectionUpgrade)
            hintText.text = "골드를 모으고 플레이어 강화 버튼을 직접 눌러 보세요.";
        else
            hintText.text = "다음을 눌러 계속하세요.";

        previousButton.interactable = currentStepIndex > 0;
        nextButtonText.text = currentStepIndex == Steps.Length - 1 ? "완료" : "다음";
        previousButtonRect.gameObject.SetActive(currentStepIndex > 0);
        if (step.waitForTouchAffectionUpgrade && !touchAffectionUpgradeCompleted)
            UpdateTouchAffectionExperience();
        flowerDexEnteredDuringStep = false;
        TutorialHighlightTarget highlightTarget = step.waitForTouchAffectionUpgrade
            ? GetTouchAffectionExperienceHighlight()
            : step.highlightTarget;
        currentHighlightTarget = GetHighlightRect(highlightTarget);
        UpdateCurrentHighlight(highlightTarget);
    }

    private void HandleCurrentFlowerClicked()
    {
        if (!isVisible || !Steps[currentStepIndex].waitForFlowerTap) return;

        completedFlowerTapSteps.Add(currentStepIndex);
        hintText.text = "좋아요! 이제 다음 설명을 확인하세요.";
        nextButton.interactable = true;
    }

    private void HandleTutorialFlowerClicked()
    {
        if (isVisible && Steps[currentStepIndex].waitForTouchAffectionUpgrade)
            UpdateTouchAffectionExperience();
    }

    private void UpdateTouchAffectionExperience()
    {
        if (PlayerStatManager.Instance == null || GameManager.Instance == null)
        {
            hintText.text = "강화 정보를 불러올 수 없습니다. 전체 건너뛰기로 계속할 수 있습니다.";
            nextButton.interactable = true;
            return;
        }

        int currentLevel = PlayerStatManager.Instance.GetLevel(PlayerStatType.TouchAffection);
        if (touchAffectionLevelAtStepStart < 0)
            touchAffectionLevelAtStepStart = currentLevel;

        if (!isReplaySession && !touchAffectionUpgradeCompleted && currentLevel > touchAffectionLevelAtStepStart)
        {
            touchAffectionUpgradeCompleted = true;
            hintText.text = "강화 성공! 다음을 눌러 계속하세요.";
            nextButton.interactable = true;
            return;
        }

        BigNumber cost = PlayerStatManager.Instance.GetUpgradeCost(PlayerStatType.TouchAffection);
        BigNumber gold = GameManager.Instance.totalGold;
        if (gold < cost)
        {
            BigNumber remaining = cost - gold;
            hintText.text = $"골드가 {NumberFormatUtil.FormatGold(remaining)} 부족합니다. 꽃을 터치해 골드를 모아 보세요.";
            nextButton.interactable = false;
            return;
        }

        hintText.text = "필요한 골드를 모았습니다. 성장 패널의 플레이어 탭에서 터치 애정 강화 버튼을 눌러 보세요.";
        nextButton.interactable = false;
    }

    private TutorialHighlightTarget GetTouchAffectionExperienceHighlight()
    {
        if (touchAffectionUpgradeCompleted)
            return isReplaySession
                ? TutorialHighlightTarget.TouchAffectionUpgrade
                : TutorialHighlightTarget.None;
        if (PlayerStatManager.Instance == null || GameManager.Instance == null) return TutorialHighlightTarget.None;

        BigNumber cost = PlayerStatManager.Instance.GetUpgradeCost(PlayerStatType.TouchAffection);
        if (GameManager.Instance.totalGold < cost)
            return TutorialHighlightTarget.Flower;

        PlayerUpgradePanel playerPanel = Object.FindAnyObjectByType<PlayerUpgradePanel>(FindObjectsInactive.Include);
        if (playerPanel == null) return TutorialHighlightTarget.None;
        return playerPanel.gameObject.activeInHierarchy
            ? TutorialHighlightTarget.TouchAffectionUpgrade
            : TutorialHighlightTarget.PlayerUpgradeTab;
    }

    private bool IsTouchAffectionUpgradeVisible()
    {
        PlayerUpgradePanel playerPanel = Object.FindAnyObjectByType<PlayerUpgradePanel>(FindObjectsInactive.Include);
        return playerPanel != null && playerPanel.gameObject.activeInHierarchy
            && playerPanel.touchAffectionRow != null && playerPanel.touchAffectionRow.actionButton != null
            && playerPanel.touchAffectionRow.actionButton.gameObject.activeInHierarchy;
    }

    private void UpdateFlowerHighlight()
    {
        if (FlowerDisplayController.Instance == null || Camera.main == null)
        {
            highlightRect.gameObject.SetActive(false);
            return;
        }

        SpriteRenderer renderer = FlowerDisplayController.Instance.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            highlightRect.gameObject.SetActive(false);
            return;
        }

        Bounds bounds = renderer.bounds;
        Vector3 bottomLeft = Camera.main.WorldToScreenPoint(new Vector3(bounds.min.x, bounds.min.y, bounds.center.z));
        Vector3 topRight = Camera.main.WorldToScreenPoint(new Vector3(bounds.max.x, bounds.max.y, bounds.center.z));
        Camera uiCamera = targetCanvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : targetCanvas.rootCanvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayRect, bottomLeft, uiCamera, out Vector2 localBottomLeft);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayRect, topRight, uiCamera, out Vector2 localTopRight);

        highlightRect.anchoredPosition = (localBottomLeft + localTopRight) * 0.5f;
        highlightRect.sizeDelta = new Vector2(Mathf.Abs(localTopRight.x - localBottomLeft.x), Mathf.Abs(localTopRight.y - localBottomLeft.y)) + new Vector2(24f, 24f);
        highlightRect.gameObject.SetActive(true);
    }

    private void UpdateCurrentHighlight(TutorialHighlightTarget target)
    {
        RectTransform targetRect = currentHighlightTarget;
        if (targetRect == null || !targetRect.gameObject.activeInHierarchy)
        {
            if (target == TutorialHighlightTarget.Flower)
                UpdateFlowerHighlight();
            else
                highlightRect.gameObject.SetActive(false);
            return;
        }

        Vector3[] corners = new Vector3[4];
        targetRect.GetWorldCorners(corners);
        Camera uiCamera = targetCanvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : targetCanvas.rootCanvas.worldCamera;
        Vector2 screenMin = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[0]);
        Vector2 screenMax = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[2]);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayRect, screenMin, uiCamera, out Vector2 localMin);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayRect, screenMax, uiCamera, out Vector2 localMax);
        highlightRect.anchoredPosition = (localMin + localMax) * 0.5f;
        highlightRect.sizeDelta = new Vector2(Mathf.Abs(localMax.x - localMin.x), Mathf.Abs(localMax.y - localMin.y)) + new Vector2(8f, 8f);
        highlightRect.gameObject.SetActive(true);
    }

    private RectTransform GetHighlightRect(TutorialHighlightTarget target)
    {
        switch (target)
        {
            case TutorialHighlightTarget.AffectionGauge:
                UIManager uiManager = Object.FindAnyObjectByType<UIManager>();
                return uiManager != null && uiManager.affectionBarFillImage != null
                    ? uiManager.affectionBarFillImage.rectTransform
                    : null;
            case TutorialHighlightTarget.TouchAffectionUpgrade:
                PlayerUpgradePanel playerPanel = Object.FindAnyObjectByType<PlayerUpgradePanel>(FindObjectsInactive.Include);
                return playerPanel != null && playerPanel.touchAffectionRow != null && playerPanel.touchAffectionRow.actionButton != null
                    ? playerPanel.touchAffectionRow.actionButton.GetComponent<RectTransform>()
                    : null;
            case TutorialHighlightTarget.PlayerUpgradeTab:
                PlayerUpgradePanel upgradePanel = Object.FindAnyObjectByType<PlayerUpgradePanel>(FindObjectsInactive.Include);
                return GetTabButtonRect(upgradePanel);
            case TutorialHighlightTarget.GoldDisplay:
                UIManager goldUi = Object.FindAnyObjectByType<UIManager>();
                return goldUi != null && goldUi.goldText != null
                    ? goldUi.goldText.rectTransform
                    : null;
            case TutorialHighlightTarget.Shop:
                ShopManager shop = Object.FindAnyObjectByType<ShopManager>(FindObjectsInactive.Include);
                return shop != null && shop.gameObject.activeInHierarchy
                    ? shop.transform as RectTransform
                    : null;
            case TutorialHighlightTarget.FlowerDex:
                FlowerDexPanel dex = Object.FindAnyObjectByType<FlowerDexPanel>(FindObjectsInactive.Include);
                return dex != null && dex.openButton != null && dex.openButton.gameObject.activeInHierarchy
                    ? dex.openButton.GetComponent<RectTransform>()
                    : null;
            case TutorialHighlightTarget.DandelionDexItem:
                FlowerDexPanel dandelionDex = Object.FindAnyObjectByType<FlowerDexPanel>(FindObjectsInactive.Include);
                return dandelionDex != null && dandelionDex.IsOpen
                    ? dandelionDex.GetFlowerItemRect("dandelion")
                    : null;
            case TutorialHighlightTarget.FlowerDexClose:
                FlowerDexPanel openDex = Object.FindAnyObjectByType<FlowerDexPanel>(FindObjectsInactive.Include);
                return openDex != null && openDex.IsOpen && openDex.closeButton != null
                    ? openDex.closeButton.GetComponent<RectTransform>()
                    : null;
            case TutorialHighlightTarget.Garden:
                GardenPanel garden = Object.FindAnyObjectByType<GardenPanel>(FindObjectsInactive.Include);
                return GetTabButtonRect(garden);
            default:
                return null;
        }
    }

    private RectTransform GetTabButtonRect(MonoBehaviour tabPanel)
    {
        if (tabPanel == null) return null;
        PanelSwitcher switcher = tabPanel.GetComponentInParent<PanelSwitcher>(true);
        if (switcher == null) return null;

        for (int i = 0; i < switcher.tabs.Count; i++)
        {
            PanelSwitcher.Tab tab = switcher.tabs[i];
            if (tab.panel == tabPanel.gameObject && tab.button != null)
                return tab.button.GetComponent<RectTransform>();
        }

        return null;
    }

    private void AdvanceStep()
    {
        TutorialStep step = Steps[currentStepIndex];
        bool flowerTapped = completedFlowerTapSteps.Contains(currentStepIndex);
        if (!flowerTapped && step.waitForFlowerTap) return;
        if (step.waitForTouchAffectionUpgrade && !touchAffectionUpgradeCompleted) return;

        if (currentStepIndex >= Steps.Length - 1)
        {
            FinishTutorial();
            return;
        }

        currentStepIndex++;
        flowerDexEnteredDuringStep = false;
        ShowCurrentStep();
    }

    private void GoToPreviousStep()
    {
        if (currentStepIndex <= 0) return;
        currentStepIndex--;
        flowerDexEnteredDuringStep = false;
        ShowCurrentStep();
    }

    private void SkipTutorial() => FinishTutorial();

    private void FinishTutorial()
    {
        isVisible = false;
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        UnsubscribeFromFlowerClick();

        if (isFirstRunSession && SaveManager.Instance != null)
            SaveManager.Instance.MarkTutorialCompleted();
    }

    private void UnsubscribeFromFlowerClick()
    {
        if (FlowerManager.Instance != null)
        {
            FlowerManager.Instance.OnCurrentFlowerClicked -= HandleCurrentFlowerClicked;
            FlowerManager.Instance.OnCurrentFlowerClicked -= HandleTutorialFlowerClicked;
        }
    }

    private static TMP_Text CreateText(Transform parent, string name, TMP_FontAsset font, float fontSize,
        FontStyles fontStyle, Vector2 anchorMin, Vector2 anchorMax, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = new Vector2(10f, 2f);
        rect.offsetMax = new Vector2(-10f, -2f);

        TMP_Text text = textObject.GetComponent<TMP_Text>();
        if (font != null) text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = Color.white;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateButton(Transform parent, string name, string label, TMP_FontAsset font,
        Vector2 anchorMin, Vector2 anchorMax, Color color, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = buttonObject.GetComponent<Image>();
        image.color = color;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        TMP_Text text = CreateText(buttonObject.transform, "Label", font, 17f, FontStyles.Bold,
            Vector2.zero, Vector2.one, TextAlignmentOptions.Center);
        text.text = label;
        return button;
    }

    private static TMP_FontAsset GetAvailableFont()
    {
        UIManager uiManager = Object.FindAnyObjectByType<UIManager>();
        if (uiManager != null && uiManager.affectionValueText != null && uiManager.affectionValueText.font != null)
            return uiManager.affectionValueText.font;

        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        TMP_Text sample = canvas != null ? canvas.GetComponentInChildren<TMP_Text>(true) : null;
        return sample != null ? sample.font : TMP_Settings.defaultFontAsset;
    }

    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
