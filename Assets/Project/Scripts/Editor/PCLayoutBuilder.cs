#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Tools > 🌸 Build PC Layout 메뉴로 실행.
/// 
/// 원인 완벽 해결:
/// 1. Content RectTransform의 default sizeDelta.x=100 & pivot.x=0.5 문제로 인해
///    좌측으로 50px 확장되며 '민' 글자가 화면 밖(-X)으로 밀려나 있던 원인 완전 제거!
///    -> Content의 sizeDelta = Vector2.zero, pivot = (0, 1)로 설정하여 좌측 경계(0px)에 완벽 고정.
/// 2. LeftSidebar 및 RightSidebar에 RectMask2D 컴포넌트 추가하여
///    사이드바 자식 요소가 사이드바 영역 밖으로 절대로 이탈 불가능하도록 경계 마스킹 보장.
/// 3. 구형 고아 요소 자동 정리 및 9:16 세로 모바일 중앙 화면 정밀 연산 적용.
/// </summary>
public static class PCLayoutBuilder
{
    const float MOBILE_WIDTH     = 607.5f; // 1080 * 9 / 16 = 607.5px (9:16 세로 모바일)
    const float HALF_MOBILE_W    = 303.75f;
    const float TOPBAR_HEIGHT    = 70f;
    const float FLOWER_UI_HEIGHT = 100f;

    const string FLOWER_UPGRADE_ITEM_PREFAB_PATH = "Assets/Project/Prefabs/FlowerUpgradeItem.prefab";
    const string FLOWER_DEX_ITEM_PREFAB_PATH = "Assets/Project/Prefabs/FlowerDexItem.prefab";

    [MenuItem("Tools/🌸 Build PC Layout")]
    public static void BuildLayout()
    {
        // 1. Canvas 찾기 및 Scaler 설정 (1920x1080 기준)
        Canvas gameCanvas = Object.FindFirstObjectByType<Canvas>();
        if (gameCanvas == null)
        {
            EditorUtility.DisplayDialog("오류", "씬에서 Canvas를 찾을 수 없습니다.", "확인");
            return;
        }
        GameObject canvasGO = gameCanvas.gameObject;

        CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode          = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution  = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight   = 0.5f;
        }

        // 2. 기존 씬 요소 위치 보존 및 레거시 삭제
        Transform existingTopBar       = FindChildRecursive(canvasGO.transform, "TopBar");
        Transform existingFlowerStatus = FindChildRecursive(canvasGO.transform, "FlowerStatusUI");

        // GameCanvas 직계 자식 중 구형 ShopPanel 또는 고아 PCLayout 삭제
        for (int i = canvasGO.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = canvasGO.transform.GetChild(i);
            if (child.name == "ShopPanel" || child.name == "PCLayout")
            {
                if (existingTopBar != null && existingTopBar.IsChildOf(child))
                    existingTopBar.SetParent(canvasGO.transform, false);
                if (existingFlowerStatus != null && existingFlowerStatus.IsChildOf(child))
                    existingFlowerStatus.SetParent(canvasGO.transform, false);

                Object.DestroyImmediate(child.gameObject);
            }
        }

        // ── 3열 레이아웃 루트 ──────────────────────────────────────────────
        GameObject pcLayout = new GameObject("PCLayout");
        pcLayout.transform.SetParent(canvasGO.transform, false);
        RectTransform pcLayoutRT = pcLayout.AddComponent<RectTransform>();
        pcLayoutRT.anchorMin = Vector2.zero;
        pcLayoutRT.anchorMax = Vector2.one;
        pcLayoutRT.offsetMin = Vector2.zero;
        pcLayoutRT.offsetMax = Vector2.zero;

        // [LeftSidebar] - 화면 좌측 끝(0) ~ 중앙 모바일 화면 좌측 경계(-303.75)
        GameObject leftSidebar = new GameObject("LeftSidebar");
        leftSidebar.transform.SetParent(pcLayout.transform, false);
        RectTransform leftRT = leftSidebar.AddComponent<RectTransform>();
        leftRT.anchorMin = new Vector2(0f, 0f);
        leftRT.anchorMax = new Vector2(0.5f, 1f);
        leftRT.offsetMin = Vector2.zero;
        leftRT.offsetMax = new Vector2(-HALF_MOBILE_W, 0f); // 중앙 경계까지 밀착

        Image leftBg = leftSidebar.AddComponent<Image>();
        leftBg.color = new Color(0.1f, 0.11f, 0.15f, 1f); // 다크 패널
        
        // RectMask2D 추가: 사이드바 외부로 요소가 절대 이탈하지 못하도록 경계 캡슐화 마스킹!
        leftSidebar.AddComponent<RectMask2D>();

        GameObject leftSlot = CreateStretchChild(leftSidebar.transform, "LeftSlot");

        // [MobileGameArea] - 중앙 9:16 고정 화면 (Width = 607.5px)
        GameObject mobileGameArea = new GameObject("MobileGameArea");
        mobileGameArea.transform.SetParent(pcLayout.transform, false);
        RectTransform mobileRT = mobileGameArea.AddComponent<RectTransform>();
        mobileRT.anchorMin        = new Vector2(0.5f, 0f);
        mobileRT.anchorMax        = new Vector2(0.5f, 1f);
        mobileRT.pivot            = new Vector2(0.5f, 0.5f);
        mobileRT.anchoredPosition = Vector2.zero;
        mobileRT.sizeDelta        = new Vector2(MOBILE_WIDTH, 0f); // 607.5px x 1080px (9:16)

        // 배경 Image는 일부러 붙이지 않는다: Canvas가 Screen Space - Overlay라서 그 안의 Graphic은
        // sortingOrder/하이어라키 순서와 무관하게 항상 씬의 모든 카메라 출력보다 앞에 그려진다.
        // 즉 여기 Image를 달면 "꽃 뒤로 보내기"가 구조적으로 불가능하고, 알파를 아무리 낮춰도
        // 중앙의 꽃 스프라이트(월드 스페이스 SpriteRenderer) 전체에 뿌연 틴트로 얹힌다.
        // PC 사이드바 경계를 보여주는 디자인 타임용 틴트였지만 실제 게임 화면을 흐리게 만드는 게
        // 더 큰 손해라 제거했다 — 경계는 좌/우 사이드바의 어두운 배경만으로도 충분히 구분된다.

        // [RightSidebar] - 모바일 화면 우측 경계(+303.75) ~ 화면 우측 끝(1)
        GameObject rightSidebar = new GameObject("RightSidebar");
        rightSidebar.transform.SetParent(pcLayout.transform, false);
        RectTransform rightRT = rightSidebar.AddComponent<RectTransform>();
        rightRT.anchorMin = new Vector2(0.5f, 0f);
        rightRT.anchorMax = new Vector2(1f, 1f);
        rightRT.offsetMin = new Vector2(HALF_MOBILE_W, 0f); // 우측 끝까지 밀착
        rightRT.offsetMax = Vector2.zero;

        Image rightBg = rightSidebar.AddComponent<Image>();
        rightBg.color = new Color(0.1f, 0.11f, 0.15f, 1f); // 다크 패널

        // RectMask2D 추가: 우측 사이드바 경계 마스킹
        rightSidebar.AddComponent<RectMask2D>();

        GameObject rightSlot = CreateStretchChild(rightSidebar.transform, "RightSlot");

        // ── MobileGameArea 내부: TopBar + FlowerStatusUI 재배치 ──────────
        // TopBar (골드 표시)
        GameObject topBarGO = (existingTopBar != null) ? existingTopBar.gameObject : new GameObject("TopBar");
        topBarGO.transform.SetParent(mobileRT, false);
        SetupTopBar(topBarGO);

        // FlowerStatusUI (애정 게이지 & 속도/수치)
        GameObject statusUIGO = (existingFlowerStatus != null) ? existingFlowerStatus.gameObject : new GameObject("FlowerStatusUI");
        statusUIGO.transform.SetParent(mobileRT, false);
        SetupFlowerStatusUI(statusUIGO);

        // ── 패널 3개 생성 및 슬롯 배치 ──────────────────────────────────
        // 1. ShopPanel -> LeftSlot 자식
        GameObject shopPanel = CreateShopPanel(leftSlot.transform);

        // 2. GrowthPanel -> RightSlot 자식 (꽃 성장 / 플레이어 강화 탭 전환, PanelSwitcher 기반)
        GameObject growthPanel = CreateGrowthPanel(rightSlot.transform);

        // ── PCLayoutController 연결 ──────────────────────────────────────
        PCLayoutController ctrl = pcLayout.AddComponent<PCLayoutController>();
        ctrl.leftSidebar  = leftSidebar;
        ctrl.rightSidebar = rightSidebar;
        ctrl.leftPanel    = shopPanel;
        ctrl.rightPanel   = growthPanel;
        ctrl.hideSidebarsOnMobile = true;

        // ── UIManager 연결 ───────────────────────────────────────────────
        ConnectUIManager(canvasGO, mobileRT.transform);

        // ── 꽃 도감 팝업 (Canvas 최상위, 화면 전체 오버레이 — 사이드바가 숨는 모바일에서도 접근 가능) ──
        FlowerDexPanel dexPanel = BuildFlowerDexPanel(canvasGO.transform);

        // TopBar의 도감 열기 버튼을 FlowerDexPanel.openButton 필드에 연결한다.
        // 여기서 onClick.AddListener를 직접 호출하지 않는 이유: 에디터 스크립트는 Play 모드 밖에서
        // 실행되므로 그렇게 붙인 리스너는 씬에 저장되지 않는다(런타임 전용 리스너). 필드만 연결해두면
        // FlowerDexPanel.Start()가 실제 실행 시점에 스스로 AddListener하므로 항상 유효하다.
        Transform dexButtonTr = topBarGO.transform.Find("DexButton");
        if (dexButtonTr != null)
            dexPanel.openButton = dexButtonTr.GetComponent<Button>();

        // ── 오프라인 정산 팝업 (Canvas 최상위 마지막 자식 = 항상 맨 위에 그려짐) ──
        BuildOfflineSummaryPopup(canvasGO.transform);

        // 씬 저장 마킹
        EditorSceneManager.MarkSceneDirty(canvasGO.scene);

        Debug.Log("[PCLayoutBuilder] 사이드바 경계 마스킹 및 Content -X 오프셋 원천 해결 완료!");
    }

    // ── TopBar 설정 ───────────────────────────────────────────────────
    static void SetupTopBar(GameObject topBar)
    {
        RectTransform rt = topBar.GetComponent<RectTransform>();
        if (rt == null) rt = topBar.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 1f);
        rt.anchorMax        = new Vector2(1f, 1f);
        rt.pivot            = new Vector2(0.5f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = new Vector2(0f, TOPBAR_HEIGHT);

        Image img = topBar.GetComponent<Image>();
        if (img == null) img = topBar.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.65f); // 어두운 상단 바

        TMP_FontAsset font = LoadNotoFont();

        // GoldText
        Transform gTr = topBar.transform.Find("GoldText");
        GameObject gGO = (gTr != null) ? gTr.gameObject : new GameObject("GoldText");
        gGO.transform.SetParent(topBar.transform, false);
        SetupText(gGO, "골드 0", font, 24f, TextAlignmentOptions.Left,
                  new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Vector2(16f, 0f), new Vector2(-8f, 0f));

        // GoldPerSecondText (오른쪽 끝은 DexButton 자리만큼 비워둠)
        Transform rTr = topBar.transform.Find("GoldPerSecondText");
        GameObject rGO = (rTr != null) ? rTr.gameObject : new GameObject("GoldPerSecondText");
        rGO.transform.SetParent(topBar.transform, false);
        SetupText(rGO, "골드 +0.0/s", font, 18f, TextAlignmentOptions.Right,
                  new Vector2(0.5f, 0f), new Vector2(1f, 1f), new Vector2(8f, 0f), new Vector2(-70f, 0f));

        // DexButton (도감 열기, 우측 끝 고정폭)
        Transform dexTr = topBar.transform.Find("DexButton");
        GameObject dexGO = (dexTr != null) ? dexTr.gameObject : new GameObject("DexButton");
        dexGO.transform.SetParent(topBar.transform, false);
        RectTransform dexRT = dexGO.GetComponent<RectTransform>();
        if (dexRT == null) dexRT = dexGO.AddComponent<RectTransform>();
        dexRT.anchorMin = new Vector2(1f, 0f);
        dexRT.anchorMax = new Vector2(1f, 1f);
        dexRT.pivot = new Vector2(1f, 0.5f);
        dexRT.sizeDelta = new Vector2(55f, 0f);
        dexRT.anchoredPosition = new Vector2(-8f, 0f);

        Image dexBg = dexGO.GetComponent<Image>();
        if (dexBg == null) dexBg = dexGO.AddComponent<Image>();
        dexBg.color = new Color(1f, 0.35f, 0.62f, 1f);

        Button dexButton = dexGO.GetComponent<Button>();
        if (dexButton == null) dexButton = dexGO.AddComponent<Button>();
        dexButton.targetGraphic = dexBg;

        Transform dexLabelTr = dexGO.transform.Find("Label");
        GameObject dexLabelGO = (dexLabelTr != null) ? dexLabelTr.gameObject : new GameObject("Label");
        dexLabelGO.transform.SetParent(dexGO.transform, false);
        SetupText(dexLabelGO, "도감", font, 16f, TextAlignmentOptions.Center,
                  Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }

    // ── FlowerStatusUI (애정 수치 & 게이지) 설정 ───────────────────────
    static void SetupFlowerStatusUI(GameObject statusUI)
    {
        RectTransform rt = statusUI.GetComponent<RectTransform>();
        if (rt == null) rt = statusUI.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 0f);
        rt.anchorMax        = new Vector2(1f, 0f);
        rt.pivot            = new Vector2(0.5f, 0f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = new Vector2(0f, FLOWER_UI_HEIGHT);

        Image bg = statusUI.GetComponent<Image>();
        if (bg == null) bg = statusUI.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.08f, 0.12f, 0.85f); // 어두운 하단 패널

        TMP_FontAsset font = LoadNotoFont();

        // 1. AffectionPerSecondText (우측은 FlowerIndicatorText 자리만큼 비워둠)
        Transform rateTr = statusUI.transform.Find("AffectionPerSecondText");
        GameObject rateGO = (rateTr != null) ? rateTr.gameObject : new GameObject("AffectionPerSecondText");
        rateGO.transform.SetParent(statusUI.transform, false);
        SetupText(rateGO, "+0.0 애정/s", font, 18f, TextAlignmentOptions.Center,
                  new Vector2(0f, 0.62f), new Vector2(0.8f, 1f), new Vector2(10f, 0f), new Vector2(-6f, 0f));

        // 1-1. FlowerIndicatorText ("3 / 5" — 보유 꽃 중 현재 몇 번째인지)
        Transform indicatorTr = statusUI.transform.Find("FlowerIndicatorText");
        GameObject indicatorGO = (indicatorTr != null) ? indicatorTr.gameObject : new GameObject("FlowerIndicatorText");
        indicatorGO.transform.SetParent(statusUI.transform, false);
        SetupText(indicatorGO, "1 / 1", font, 16f, TextAlignmentOptions.Right,
                  new Vector2(0.8f, 0.62f), new Vector2(1f, 1f), new Vector2(6f, 0f), new Vector2(-10f, 0f));

        // 2. AffectionBar
        Transform barTr = statusUI.transform.Find("AffectionBar");
        GameObject barGO = (barTr != null) ? barTr.gameObject : new GameObject("AffectionBar");
        barGO.transform.SetParent(statusUI.transform, false);

        Transform oldBg = barGO.transform.Find("Backgoround");
        if (oldBg != null) Object.DestroyImmediate(oldBg.gameObject);

        RectTransform barRT = barGO.GetComponent<RectTransform>();
        if (barRT == null) barRT = barGO.AddComponent<RectTransform>();
        barRT.anchorMin        = new Vector2(0.05f, 0.38f);
        barRT.anchorMax        = new Vector2(0.95f, 0.58f);
        barRT.pivot            = new Vector2(0.5f, 0.5f);
        barRT.anchoredPosition = Vector2.zero;
        barRT.sizeDelta        = Vector2.zero;

        Image barTrack = barGO.GetComponent<Image>();
        if (barTrack == null) barTrack = barGO.AddComponent<Image>();
        barTrack.color = new Color(0f, 0f, 0f, 0.6f);
        barTrack.sprite = null;
        barTrack.type = Image.Type.Simple;

        Transform fillTr = barGO.transform.Find("Fill");
        GameObject fillGO = (fillTr != null) ? fillTr.gameObject : new GameObject("Fill");
        fillGO.transform.SetParent(barGO.transform, false);

        RectTransform fillRT = fillGO.GetComponent<RectTransform>();
        if (fillRT == null) fillRT = fillGO.AddComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;

        Image fillImg = fillGO.GetComponent<Image>();
        if (fillImg == null) fillImg = fillGO.AddComponent<Image>();
        fillImg.sprite = GetWhiteSprite(); // Image.Type.Filled 호환 White Sprite
        fillImg.color = new Color(1f, 0.35f, 0.62f, 1f); // 핑크빛 게이지
        fillImg.type  = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillAmount = 0.01f;

        // 3. AffectionValueText
        Transform valTr = statusUI.transform.Find("AffectionValueText");
        GameObject valGO = (valTr != null) ? valTr.gameObject : new GameObject("AffectionValueText");
        valGO.transform.SetParent(statusUI.transform, false);
        SetupText(valGO, "0 / 100 (0%)", font, 18f, TextAlignmentOptions.Center,
                  new Vector2(0f, 0f), new Vector2(1f, 0.38f), new Vector2(10f, 0f), new Vector2(-10f, 0f));
    }

    // ── ShopPanel 생성 ────────────────────────────────────────────────
    static GameObject CreateShopPanel(Transform parent)
    {
        TMP_FontAsset font = LoadNotoFont();

        GameObject shopPanel = new GameObject("ShopPanel");
        shopPanel.transform.SetParent(parent, false);
        RectTransform rt = shopPanel.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // Header
        GameObject header = new GameObject("ShopHeader");
        header.transform.SetParent(shopPanel.transform, false);
        RectTransform hrt = header.AddComponent<RectTransform>();
        hrt.anchorMin = new Vector2(0f, 1f);
        hrt.anchorMax = new Vector2(1f, 1f);
        hrt.pivot     = new Vector2(0.5f, 1f);
        hrt.sizeDelta = new Vector2(0f, 55f);

        Image hImg = header.AddComponent<Image>();
        hImg.color = new Color(0.15f, 0.16f, 0.22f, 1f);

        GameObject title = new GameObject("Title");
        title.transform.SetParent(header.transform, false);
        SetupText(title, "상점", font, 24f, TextAlignmentOptions.Left,
                  Vector2.zero, Vector2.one, new Vector2(20f, 0f), new Vector2(-20f, 0f));

        // Scroll View
        GameObject scrollGO = new GameObject("Scroll View");
        scrollGO.transform.SetParent(shopPanel.transform, false);
        RectTransform srt = scrollGO.AddComponent<RectTransform>();
        srt.anchorMin = Vector2.zero;
        srt.anchorMax = Vector2.one;
        srt.offsetMin = Vector2.zero;
        srt.offsetMax = new Vector2(0f, -55f);

        Image scrollImg = scrollGO.AddComponent<Image>();
        scrollImg.color = new Color(0f, 0f, 0f, 0f);

        ScrollRect sr = scrollGO.AddComponent<ScrollRect>();
        sr.horizontal        = false;
        sr.vertical          = true;
        sr.scrollSensitivity = 35f;

        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollGO.transform, false);
        RectTransform vprt = viewport.AddComponent<RectTransform>();
        vprt.anchorMin = Vector2.zero;
        vprt.anchorMax = Vector2.one;
        vprt.offsetMin = Vector2.zero;
        vprt.offsetMax = Vector2.zero;
        Image vpImg = viewport.AddComponent<Image>();
        vpImg.color = new Color(1f, 1f, 1f, 0.01f);
        Mask mask = viewport.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        // Content
        GameObject content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        RectTransform crt = content.AddComponent<RectTransform>();
        // ★ 핵심 수정: pivot=(0, 1), sizeDelta=Vector2.zero 설정으로 좌측 0px 경계에 완벽 고정!
        crt.anchorMin        = new Vector2(0f, 1f);
        crt.anchorMax        = new Vector2(1f, 1f);
        crt.pivot            = new Vector2(0f, 1f);
        crt.anchoredPosition = Vector2.zero;
        crt.sizeDelta        = Vector2.zero;

        VerticalLayoutGroup cvlg = content.AddComponent<VerticalLayoutGroup>();
        cvlg.childForceExpandWidth  = true;
        cvlg.childForceExpandHeight = false;
        cvlg.childControlWidth      = true;
        cvlg.childControlHeight     = true;
        cvlg.spacing = 8f;
        cvlg.padding = new RectOffset(16, 16, 16, 16);

        ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        sr.viewport = vprt;
        sr.content  = crt;

        // EmptyState ("모든 꽃을 만났습니다" — 구매 가능한 꽃이 하나도 없을 때, Scroll View와 같은 영역에 겹쳐서 표시)
        GameObject emptyState = new GameObject("EmptyState");
        emptyState.transform.SetParent(shopPanel.transform, false);
        RectTransform emptyRT = emptyState.AddComponent<RectTransform>();
        emptyRT.anchorMin = Vector2.zero;
        emptyRT.anchorMax = Vector2.one;
        emptyRT.offsetMin = Vector2.zero;
        emptyRT.offsetMax = new Vector2(0f, -55f);

        GameObject emptyLabelGO = new GameObject("Label");
        emptyLabelGO.transform.SetParent(emptyState.transform, false);
        SetupText(emptyLabelGO, "모든 꽃을 만났습니다", font, 20f, TextAlignmentOptions.Center,
                  Vector2.zero, Vector2.one, new Vector2(20f, 0f), new Vector2(-20f, 0f));
        emptyLabelGO.GetComponent<TMP_Text>().color = new Color(0.85f, 0.8f, 0.85f, 1f);

        emptyState.SetActive(false);

        // ShopManager 연결
        ShopManager sm = shopPanel.AddComponent<ShopManager>();
        var contentField = typeof(ShopManager).GetField("content",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        contentField?.SetValue(sm, crt);

        var prefabField = typeof(ShopManager).GetField("shopItemPrefab",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        ShopItem itemPrefab = AssetDatabase.LoadAssetAtPath<ShopItem>("Assets/Project/Prefabs/ShopItem.prefab");
        if (itemPrefab != null) prefabField?.SetValue(sm, itemPrefab);

        var emptyRootField = typeof(ShopManager).GetField("emptyStateRoot",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        emptyRootField?.SetValue(sm, emptyState);

        var emptyTextField = typeof(ShopManager).GetField("emptyStateText",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        emptyTextField?.SetValue(sm, emptyLabelGO.GetComponent<TMP_Text>());

        return shopPanel;
    }

    // ── GrowthPanel 생성 (꽃 성장 / 플레이어 강화, PanelSwitcher로 탭 전환) ─────
    static GameObject CreateGrowthPanel(Transform parent)
    {
        TMP_FontAsset font = LoadNotoFont();

        GameObject growthPanel = new GameObject("GrowthPanel");
        growthPanel.transform.SetParent(parent, false);
        RectTransform rt = growthPanel.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image bg = growthPanel.AddComponent<Image>();
        bg.color = new Color(0.12f, 0.18f, 0.15f, 0.95f);

        // ── TabBar ("꽃" / "플레이어") ──
        GameObject tabBar = new GameObject("TabBar");
        tabBar.transform.SetParent(growthPanel.transform, false);
        RectTransform tabRT = tabBar.AddComponent<RectTransform>();
        tabRT.anchorMin = new Vector2(0f, 1f);
        tabRT.anchorMax = new Vector2(1f, 1f);
        tabRT.pivot     = new Vector2(0.5f, 1f);
        tabRT.sizeDelta = new Vector2(0f, 55f);

        Image tabBg = tabBar.AddComponent<Image>();
        tabBg.color = new Color(0.15f, 0.16f, 0.22f, 1f);

        HorizontalLayoutGroup tabLayout = tabBar.AddComponent<HorizontalLayoutGroup>();
        tabLayout.childForceExpandWidth  = true;
        tabLayout.childForceExpandHeight = true;
        tabLayout.childControlWidth      = true;
        tabLayout.childControlHeight     = true;
        tabLayout.spacing = 4f;
        tabLayout.padding = new RectOffset(4, 4, 4, 4);

        Button flowerTabButton  = CreateTabButton(tabBar.transform, "FlowerTabButton", "꽃", font);
        Button playerTabButton  = CreateTabButton(tabBar.transform, "PlayerTabButton", "플레이어", font);

        // ── PanelContainer (TabBar 아래 전체 영역) ──
        GameObject panelContainer = new GameObject("PanelContainer");
        panelContainer.transform.SetParent(growthPanel.transform, false);
        RectTransform pcRT = panelContainer.AddComponent<RectTransform>();
        pcRT.anchorMin = Vector2.zero;
        pcRT.anchorMax = Vector2.one;
        pcRT.offsetMin = Vector2.zero;
        pcRT.offsetMax = new Vector2(0f, -55f);

        GameObject flowerUpgradePanelGO = CreateFlowerUpgradePanel(panelContainer.transform);
        GameObject playerUpgradePanelGO = CreatePlayerUpgradePanel(panelContainer.transform);

        // ── PanelSwitcher 연결 (범용 탭 전환기 — Flower/Player를 모름) ──
        PanelSwitcher switcher = growthPanel.AddComponent<PanelSwitcher>();
        switcher.tabs = new System.Collections.Generic.List<PanelSwitcher.Tab>
        {
            new PanelSwitcher.Tab { button = flowerTabButton, panel = flowerUpgradePanelGO },
            new PanelSwitcher.Tab { button = playerTabButton, panel = playerUpgradePanelGO }
        };
        switcher.defaultTabIndex = 0;
        // 아래 두 색상은 PanelSwitcher의 공개 필드에 넣어주는 "초기값"일 뿐이며, 이후 Inspector에서 자유롭게 바꿀 수 있다.
        switcher.activeTabColor   = new Color(1f, 0.35f, 0.62f, 1f);  // 핑크 (AffectionBar 등 기존 강조색과 동일 계열)
        switcher.inactiveTabColor = new Color(0.25f, 0.27f, 0.35f, 1f); // 기존 탭 기본 배경색

        return growthPanel;
    }

    static Button CreateTabButton(Transform parent, string name, string label, TMP_FontAsset font)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();

        Image img = go.AddComponent<Image>();
        img.color = new Color(0.25f, 0.27f, 0.35f, 1f);

        Button button = go.AddComponent<Button>();
        button.targetGraphic = img;

        GameObject labelGO = new GameObject("Label");
        labelGO.transform.SetParent(go.transform, false);
        SetupText(labelGO, label, font, 20f, TextAlignmentOptions.Center,
                  Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        return button;
    }

    // ── FlowerUpgradePanel 생성 (꽃 탭: 정렬/단위 선택 + 보유 꽃 전체 스크롤 목록) ──
    static GameObject CreateFlowerUpgradePanel(Transform parent)
    {
        TMP_FontAsset font = LoadNotoFont();

        GameObject panel = new GameObject("FlowerUpgradePanel");
        panel.transform.SetParent(parent, false);
        RectTransform rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // ── ListHeader (정렬 선택 + 레벨업 단위 선택, 순환 버튼 2개) ──
        GameObject header = new GameObject("ListHeader");
        header.transform.SetParent(panel.transform, false);
        RectTransform hrt = header.AddComponent<RectTransform>();
        hrt.anchorMin = new Vector2(0f, 1f);
        hrt.anchorMax = new Vector2(1f, 1f);
        hrt.pivot     = new Vector2(0.5f, 1f);
        hrt.sizeDelta = new Vector2(0f, 76f);

        VerticalLayoutGroup headerLayout = header.AddComponent<VerticalLayoutGroup>();
        headerLayout.childForceExpandWidth  = true;
        headerLayout.childForceExpandHeight = false;
        headerLayout.childControlWidth      = true;
        headerLayout.childControlHeight     = true;
        headerLayout.spacing = 4f;
        headerLayout.padding = new RectOffset(8, 8, 6, 6);

        FlowerSortSelector sortSelector = CreateSelectorButton<FlowerSortSelector>(
            header.transform, "SortSelectorButton", "정렬: 도감순", font);

        LevelUpAmountSelector amountSelector = CreateSelectorButton<LevelUpAmountSelector>(
            header.transform, "AmountSelectorButton", "레벨업 단위: +1", font);

        // ── Scroll View (꽃 목록만 스크롤 — 꽃 수가 늘어도 플레이어 탭에는 영향 없음) ──
        GameObject scrollGO = new GameObject("Scroll View");
        scrollGO.transform.SetParent(panel.transform, false);
        RectTransform srt = scrollGO.AddComponent<RectTransform>();
        srt.anchorMin = Vector2.zero;
        srt.anchorMax = Vector2.one;
        srt.offsetMin = Vector2.zero;
        srt.offsetMax = new Vector2(0f, -76f);

        Image scrollImg = scrollGO.AddComponent<Image>();
        scrollImg.color = new Color(0f, 0f, 0f, 0f);

        ScrollRect sr = scrollGO.AddComponent<ScrollRect>();
        sr.horizontal        = false;
        sr.vertical          = true;
        sr.scrollSensitivity = 35f;

        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollGO.transform, false);
        RectTransform vprt = viewport.AddComponent<RectTransform>();
        vprt.anchorMin = Vector2.zero;
        vprt.anchorMax = Vector2.one;
        vprt.offsetMin = Vector2.zero;
        vprt.offsetMax = Vector2.zero;
        Image vpImg = viewport.AddComponent<Image>();
        vpImg.color = new Color(1f, 1f, 1f, 0.01f);
        Mask mask = viewport.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        GameObject content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        RectTransform crt = content.AddComponent<RectTransform>();
        crt.anchorMin        = new Vector2(0f, 1f);
        crt.anchorMax        = new Vector2(1f, 1f);
        crt.pivot            = new Vector2(0f, 1f);
        crt.anchoredPosition = Vector2.zero;
        crt.sizeDelta        = Vector2.zero;

        VerticalLayoutGroup cvlg = content.AddComponent<VerticalLayoutGroup>();
        cvlg.childForceExpandWidth  = true;
        cvlg.childForceExpandHeight = false;
        cvlg.childControlWidth      = true;
        cvlg.childControlHeight     = true;
        cvlg.spacing = 8f;
        cvlg.padding = new RectOffset(12, 12, 12, 12);

        ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        sr.viewport = vprt;
        sr.content  = crt;

        // ── FlowerUpgradePanel 연결 ──
        FlowerUpgradePanel upgradePanel = panel.AddComponent<FlowerUpgradePanel>();
        upgradePanel.content        = crt;
        upgradePanel.itemPrefab     = GetOrCreateFlowerUpgradeItemPrefab();
        upgradePanel.amountSelector = amountSelector;
        upgradePanel.sortSelector   = sortSelector;

        return panel;
    }

    /// <summary> 정렬/레벨업단위 선택용 순환 버튼 하나를 만들고 T 컴포넌트를 부착해서 반환한다. </summary>
    static T CreateSelectorButton<T>(Transform parent, string name, string initialLabel, TMP_FontAsset font) where T : Component
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 34f;

        Image img = go.AddComponent<Image>();
        img.color = new Color(0.2f, 0.22f, 0.3f, 1f);

        Button button = go.AddComponent<Button>();
        button.targetGraphic = img;

        GameObject labelGO = new GameObject("Label");
        labelGO.transform.SetParent(go.transform, false);
        SetupText(labelGO, initialLabel, font, 16f, TextAlignmentOptions.Center,
                  Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        T selector = go.AddComponent<T>();

        // FlowerSortSelector/LevelUpAmountSelector 둘 다 public TMP_Text label 필드를 가짐
        var labelField = typeof(T).GetField("label");
        labelField?.SetValue(selector, labelGO.GetComponent<TMP_Text>());

        return selector;
    }

    /// <summary>
    /// FlowerUpgradeItem 프리팹을 로드하거나, 없으면 최초 1회 생성해서 에셋으로 저장한다.
    /// 이미 존재하면 그대로 재사용 — 나중에 프리팹을 손으로 꾸며도 재빌드 시 덮어쓰지 않는다.
    /// </summary>
    static FlowerUpgradeItem GetOrCreateFlowerUpgradeItemPrefab()
    {
        FlowerUpgradeItem existing = AssetDatabase.LoadAssetAtPath<FlowerUpgradeItem>(FLOWER_UPGRADE_ITEM_PREFAB_PATH);
        if (existing != null) return existing;

        TMP_FontAsset font = LoadNotoFont();

        GameObject temp = new GameObject("FlowerUpgradeItem");
        RectTransform rt = temp.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0f, 90f);

        LayoutElement le = temp.AddComponent<LayoutElement>();
        le.preferredHeight = 90f;

        Image bg = temp.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.25f);

        GameObject nameGO = new GameObject("FlowerNameText");
        nameGO.transform.SetParent(temp.transform, false);
        SetupText(nameGO, "-", font, 20f, TextAlignmentOptions.Left,
                  new Vector2(0f, 0.62f), new Vector2(1f, 1f), new Vector2(12f, 0f), new Vector2(-12f, 0f));

        GameObject levelGpsGO = new GameObject("LevelGpsText");
        levelGpsGO.transform.SetParent(temp.transform, false);
        SetupText(levelGpsGO, "Lv.0   G/s 0.00", font, 16f, TextAlignmentOptions.Left,
                  new Vector2(0f, 0.34f), new Vector2(1f, 0.62f), new Vector2(12f, 0f), new Vector2(-12f, 0f));

        GameObject actionGO = new GameObject("ActionButton");
        actionGO.transform.SetParent(temp.transform, false);
        RectTransform art = actionGO.AddComponent<RectTransform>();
        art.anchorMin = new Vector2(0f, 0.04f);
        art.anchorMax = new Vector2(1f, 0.32f);
        art.offsetMin = new Vector2(12f, 0f);
        art.offsetMax = new Vector2(-12f, 0f);

        Image actionBg = actionGO.AddComponent<Image>();
        actionBg.color = new Color(1f, 0.35f, 0.62f, 1f); // 핑크 (AffectionBar와 동일 계열)

        Button actionButton = actionGO.AddComponent<Button>();
        actionButton.targetGraphic = actionBg;

        HoldRepeatButton holdRepeat = actionGO.AddComponent<HoldRepeatButton>();

        GameObject actionLabelGO = new GameObject("ActionText");
        actionLabelGO.transform.SetParent(actionGO.transform, false);
        SetupText(actionLabelGO, "-", font, 16f, TextAlignmentOptions.Center,
                  Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        FlowerUpgradeItem item = temp.AddComponent<FlowerUpgradeItem>();
        item.flowerNameText   = nameGO.GetComponent<TMP_Text>();
        item.levelGpsText     = levelGpsGO.GetComponent<TMP_Text>();
        item.actionText       = actionLabelGO.GetComponent<TMP_Text>();
        item.actionButton     = actionButton;
        item.holdRepeatButton = holdRepeat;

        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(temp, FLOWER_UPGRADE_ITEM_PREFAB_PATH);
        Object.DestroyImmediate(temp);

        return savedPrefab != null ? savedPrefab.GetComponent<FlowerUpgradeItem>() : null;
    }

    // ── PlayerUpgradePanel 생성 (플레이어 탭: 스켈레톤, 꽃 레벨업과 완전 분리) ──
    static GameObject CreatePlayerUpgradePanel(Transform parent)
    {
        TMP_FontAsset font = LoadNotoFont();

        GameObject panel = new GameObject("PlayerUpgradePanel");
        panel.transform.SetParent(parent, false);
        RectTransform rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        VerticalLayoutGroup vlg = panel.AddComponent<VerticalLayoutGroup>();
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = true;
        vlg.spacing = 10f;
        vlg.padding = new RectOffset(16, 16, 16, 16);

        // 레벨업 단위 선택 (+1/+10/MAX) — 꽃 탭(FlowerUpgradePanel)의 AmountSelectorButton과 같은
        // 컴포넌트지만 별도 인스턴스. 이 패널만의 강화 단위를 독립적으로 갖는다.
        LevelUpAmountSelector amountSelector = CreateSelectorButton<LevelUpAmountSelector>(
            panel.transform, "AmountSelectorButton", "레벨업 단위: +1", font);

        PlayerUpgradePanel controller = panel.AddComponent<PlayerUpgradePanel>();
        controller.amountSelector    = amountSelector;
        controller.touchAffectionRow = CreatePlayerStatRow(panel.transform, "TouchAffectionRow", font);
        controller.touchGoldRow      = CreatePlayerStatRow(panel.transform, "TouchGoldRow", font);
        controller.autoAffectionRow  = CreatePlayerStatRow(panel.transform, "AutoAffectionRow", font);

        return panel;
    }

    /// <summary>
    /// 스탯 행 UI만 생성/배선한다 (PlayerStatType 연결은 하지 않음 — 그건 런타임에
    /// PlayerUpgradePanel.Start()가 담당). PlayerStatData/PlayerStatManager는 여기서
    /// 전혀 참조하지 않는다 — UI Builder와 게임 데이터/매니저 생성 책임을 분리하기 위함.
    /// </summary>
    static PlayerStatRow CreatePlayerStatRow(Transform parent, string rowGoName, TMP_FontAsset font)
    {
        GameObject row = new GameObject(rowGoName);
        row.transform.SetParent(parent, false);
        LayoutElement le = row.AddComponent<LayoutElement>();
        le.preferredHeight = 100f;

        Image bg = row.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.25f);

        GameObject nameGO = new GameObject("StatNameText");
        nameGO.transform.SetParent(row.transform, false);
        SetupText(nameGO, "-", font, 20f, TextAlignmentOptions.Left,
                  new Vector2(0f, 0.62f), new Vector2(1f, 1f), new Vector2(12f, 0f), new Vector2(-12f, 0f));

        GameObject valueGO = new GameObject("StatValueText");
        valueGO.transform.SetParent(row.transform, false);
        SetupText(valueGO, "Lv.0   현재 +0.00", font, 16f, TextAlignmentOptions.Left,
                  new Vector2(0f, 0.34f), new Vector2(1f, 0.62f), new Vector2(12f, 0f), new Vector2(-12f, 0f));

        // ActionButton (강화 버튼 — FlowerUpgradeItem의 ActionButton과 동일한 구조/팔레트)
        GameObject actionGO = new GameObject("ActionButton");
        actionGO.transform.SetParent(row.transform, false);
        RectTransform art = actionGO.AddComponent<RectTransform>();
        art.anchorMin = new Vector2(0f, 0.04f);
        art.anchorMax = new Vector2(1f, 0.32f);
        art.offsetMin = new Vector2(12f, 0f);
        art.offsetMax = new Vector2(-12f, 0f);

        Image actionBg = actionGO.AddComponent<Image>();
        actionBg.color = new Color(1f, 0.35f, 0.62f, 1f); // 핑크 (AffectionBar 등 기존 강조색과 동일 계열)

        Button actionButton = actionGO.AddComponent<Button>();
        actionButton.targetGraphic = actionBg;

        HoldRepeatButton holdRepeat = actionGO.AddComponent<HoldRepeatButton>();

        GameObject actionLabelGO = new GameObject("ActionText");
        actionLabelGO.transform.SetParent(actionGO.transform, false);
        SetupText(actionLabelGO, "-", font, 16f, TextAlignmentOptions.Center,
                  Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        PlayerStatRow statRow = row.AddComponent<PlayerStatRow>();
        statRow.nameText        = nameGO.GetComponent<TMP_Text>();
        statRow.valueText       = valueGO.GetComponent<TMP_Text>();
        statRow.actionText      = actionLabelGO.GetComponent<TMP_Text>();
        statRow.actionButton    = actionButton;
        statRow.holdRepeatButton = holdRepeat;

        return statRow;
    }

    // ── Placeholder 카드 패널 생성 ─────────────────────────────────────────
    static GameObject CreatePlaceholderPanel(Transform parent, string name, string titleStr, string descStr, Color bgColor)
    {
        TMP_FontAsset font = LoadNotoFont();

        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = go.AddComponent<Image>();
        img.color = bgColor;

        // 카드 뷰 (중앙 배치)
        GameObject card = new GameObject("Card");
        card.transform.SetParent(go.transform, false);
        RectTransform crt = card.AddComponent<RectTransform>();
        crt.anchorMin        = new Vector2(0.5f, 0.5f);
        crt.anchorMax        = new Vector2(0.5f, 0.5f);
        crt.pivot            = new Vector2(0.5f, 0.5f);
        crt.anchoredPosition = Vector2.zero;
        crt.sizeDelta        = new Vector2(400f, 240f);

        Image cardBg = card.AddComponent<Image>();
        cardBg.color = new Color(0f, 0f, 0f, 0.35f);

        // Title
        GameObject titleGO = new GameObject("Title");
        titleGO.transform.SetParent(card.transform, false);
        SetupText(titleGO, titleStr, font, 26f, TextAlignmentOptions.Center,
                  new Vector2(0f, 0.55f), new Vector2(1f, 0.95f), new Vector2(12f, 0f), new Vector2(-12f, 0f));

        // Desc
        GameObject descGO = new GameObject("Desc");
        descGO.transform.SetParent(card.transform, false);
        SetupText(descGO, descStr, font, 18f, TextAlignmentOptions.Center,
                  new Vector2(0f, 0.05f), new Vector2(1f, 0.55f), new Vector2(12f, 0f), new Vector2(-12f, 0f));

        return go;
    }

    // ── UIManager 매핑 ─────────────────────────────────────────────────
    static void ConnectUIManager(GameObject canvasGO, Transform mobileRT)
    {
        UIManager uiMgr = canvasGO.GetComponent<UIManager>();
        if (uiMgr == null) uiMgr = canvasGO.AddComponent<UIManager>();

        Transform topBar = mobileRT.Find("TopBar");
        if (topBar != null)
        {
            uiMgr.goldText          = topBar.Find("GoldText")?.GetComponent<TMP_Text>();
            uiMgr.goldPerSecondText = topBar.Find("GoldPerSecondText")?.GetComponent<TMP_Text>();
        }

        Transform statusUI = mobileRT.Find("FlowerStatusUI");
        if (statusUI != null)
        {
            uiMgr.affectionPerSecondText = statusUI.Find("AffectionPerSecondText")?.GetComponent<TMP_Text>();
            uiMgr.affectionValueText     = statusUI.Find("AffectionValueText")?.GetComponent<TMP_Text>();
            uiMgr.flowerIndicatorText    = statusUI.Find("FlowerIndicatorText")?.GetComponent<TMP_Text>();

            Transform bar = statusUI.Find("AffectionBar");
            if (bar != null)
            {
                Image fill = bar.Find("Fill")?.GetComponent<Image>();
                uiMgr.affectionBarFillImage = fill;
            }
        }
    }

    // ── OfflineSummaryPopup 생성 (오프라인 정산 결과 팝업, 개화 강조) ──────
    static void BuildOfflineSummaryPopup(Transform canvasTransform)
    {
        // 재실행 시 항상 새로 만든다 (ShopPanel/GrowthPanel과 동일 원칙 — 손으로 꾸민 적 없는 신규 요소)
        Transform existing = canvasTransform.Find("OfflineSummaryPopup");
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        TMP_FontAsset font = LoadNotoFont();

        GameObject popupRoot = new GameObject("OfflineSummaryPopup");
        popupRoot.transform.SetParent(canvasTransform, false);
        RectTransform rootRT = popupRoot.AddComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;

        Image dim = popupRoot.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.75f);

        // Card (중앙 고정 카드)
        GameObject card = new GameObject("Card");
        card.transform.SetParent(popupRoot.transform, false);
        RectTransform cardRT = card.AddComponent<RectTransform>();
        cardRT.anchorMin = new Vector2(0.5f, 0.5f);
        cardRT.anchorMax = new Vector2(0.5f, 0.5f);
        cardRT.pivot = new Vector2(0.5f, 0.5f);
        cardRT.anchoredPosition = Vector2.zero;
        cardRT.sizeDelta = new Vector2(460f, 420f);

        Image cardBg = card.AddComponent<Image>();
        cardBg.color = new Color(0.14f, 0.1f, 0.14f, 0.97f);

        VerticalLayoutGroup vlg = card.AddComponent<VerticalLayoutGroup>();
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = true;
        vlg.spacing = 14f;
        vlg.padding = new RectOffset(24, 24, 28, 24);
        vlg.childAlignment = TextAnchor.UpperCenter;

        // ElapsedTimeText (작게, 맨 위)
        GameObject elapsedGO = new GameObject("ElapsedTimeText");
        elapsedGO.transform.SetParent(card.transform, false);
        LayoutElement elapsedLE = elapsedGO.AddComponent<LayoutElement>();
        elapsedLE.preferredHeight = 30f;
        SetupText(elapsedGO, "-", font, 18f, TextAlignmentOptions.Center,
                  Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        elapsedGO.GetComponent<TMP_Text>().color = new Color(0.85f, 0.8f, 0.85f, 1f);

        // BloomedFlowersSection (개화 강조 — 골드보다 크고 위에 배치, 개화가 없으면 비활성화)
        GameObject bloomSection = new GameObject("BloomedFlowersSection");
        bloomSection.transform.SetParent(card.transform, false);
        LayoutElement bloomLE = bloomSection.AddComponent<LayoutElement>();
        bloomLE.preferredHeight = 110f;

        Image bloomBg = bloomSection.AddComponent<Image>();
        bloomBg.color = new Color(1f, 0.35f, 0.62f, 0.18f); // 핑크 강조 (AffectionBar 등과 동일 계열)

        GameObject bloomTextGO = new GameObject("BloomedFlowersText");
        bloomTextGO.transform.SetParent(bloomSection.transform, false);
        SetupText(bloomTextGO, "-", font, 26f, TextAlignmentOptions.Center,
                  Vector2.zero, Vector2.one, new Vector2(16f, 8f), new Vector2(-16f, -8f));
        TMP_Text bloomText = bloomTextGO.GetComponent<TMP_Text>();
        bloomText.color = new Color(1f, 0.55f, 0.75f, 1f);
        bloomText.fontStyle = FontStyles.Bold;

        // GoldEarnedText (개화 강조보다 작게)
        GameObject goldGO = new GameObject("GoldEarnedText");
        goldGO.transform.SetParent(card.transform, false);
        LayoutElement goldLE = goldGO.AddComponent<LayoutElement>();
        goldLE.preferredHeight = 44f;
        SetupText(goldGO, "-", font, 22f, TextAlignmentOptions.Center,
                  Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // CloseButton
        GameObject closeGO = new GameObject("CloseButton");
        closeGO.transform.SetParent(card.transform, false);
        LayoutElement closeLE = closeGO.AddComponent<LayoutElement>();
        closeLE.preferredHeight = 52f;

        Image closeBg = closeGO.AddComponent<Image>();
        closeBg.color = new Color(1f, 0.35f, 0.62f, 1f);

        Button closeButton = closeGO.AddComponent<Button>();
        closeButton.targetGraphic = closeBg;

        GameObject closeLabelGO = new GameObject("Label");
        closeLabelGO.transform.SetParent(closeGO.transform, false);
        SetupText(closeLabelGO, "확인", font, 20f, TextAlignmentOptions.Center,
                  Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // ── OfflineSummaryPopup 컴포넌트 연결 ──
        OfflineSummaryPopup popup = popupRoot.AddComponent<OfflineSummaryPopup>();
        popup.root                    = popupRoot;
        popup.elapsedTimeText         = elapsedGO.GetComponent<TMP_Text>();
        popup.goldEarnedText          = goldGO.GetComponent<TMP_Text>();
        popup.bloomedFlowersText      = bloomText;
        popup.bloomedFlowersSection   = bloomSection;
        popup.closeButton             = closeButton;

        popupRoot.SetActive(false); // Awake에서도 꺼지지만, 재생성 직후에도 안 보이도록 명시
    }

    // ── FlowerDexPanel 생성 (보유 꽃 그리드 팝업, 화면 전체 오버레이) ──────
    // Canvas 최상위 오버레이로 둔다(사이드바 내부가 아님) — LeftSidebar/RightSidebar는
    // PCLayoutController.hideSidebarsOnMobile로 모바일에서 숨겨지므로, 사이드바 안에 두면
    // 모바일에서 도감 자체에 접근할 수 없게 된다.
    static FlowerDexPanel BuildFlowerDexPanel(Transform canvasTransform)
    {
        Transform existing = canvasTransform.Find("FlowerDexPanel");
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        TMP_FontAsset font = LoadNotoFont();

        GameObject panelRoot = new GameObject("FlowerDexPanel");
        panelRoot.transform.SetParent(canvasTransform, false);
        RectTransform rootRT = panelRoot.AddComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;

        Image dim = panelRoot.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.75f);

        // Card (중앙 고정 카드)
        GameObject card = new GameObject("Card");
        card.transform.SetParent(panelRoot.transform, false);
        RectTransform cardRT = card.AddComponent<RectTransform>();
        cardRT.anchorMin = new Vector2(0.5f, 0.5f);
        cardRT.anchorMax = new Vector2(0.5f, 0.5f);
        cardRT.pivot = new Vector2(0.5f, 0.5f);
        cardRT.anchoredPosition = Vector2.zero;
        cardRT.sizeDelta = new Vector2(560f, 700f);

        Image cardBg = card.AddComponent<Image>();
        cardBg.color = new Color(0.14f, 0.1f, 0.14f, 0.97f);

        // Header (제목 + 닫기 버튼)
        GameObject header = new GameObject("Header");
        header.transform.SetParent(card.transform, false);
        RectTransform hrt = header.AddComponent<RectTransform>();
        hrt.anchorMin = new Vector2(0f, 1f);
        hrt.anchorMax = new Vector2(1f, 1f);
        hrt.pivot = new Vector2(0.5f, 1f);
        hrt.sizeDelta = new Vector2(0f, 55f);

        Image hImg = header.AddComponent<Image>();
        hImg.color = new Color(0.15f, 0.16f, 0.22f, 1f);

        GameObject title = new GameObject("Title");
        title.transform.SetParent(header.transform, false);
        SetupText(title, "꽃 도감", font, 24f, TextAlignmentOptions.Left,
                  Vector2.zero, Vector2.one, new Vector2(20f, 0f), new Vector2(-70f, 0f));

        GameObject closeGO = new GameObject("CloseButton");
        closeGO.transform.SetParent(header.transform, false);
        RectTransform closeRT = closeGO.AddComponent<RectTransform>();
        closeRT.anchorMin = new Vector2(1f, 0f);
        closeRT.anchorMax = new Vector2(1f, 1f);
        closeRT.pivot = new Vector2(1f, 0.5f);
        closeRT.sizeDelta = new Vector2(55f, 0f);
        closeRT.anchoredPosition = new Vector2(-8f, 0f);

        Image closeBg = closeGO.AddComponent<Image>();
        closeBg.color = new Color(1f, 0.35f, 0.62f, 1f);
        Button closeButton = closeGO.AddComponent<Button>();
        closeButton.targetGraphic = closeBg;

        GameObject closeLabelGO = new GameObject("Label");
        closeLabelGO.transform.SetParent(closeGO.transform, false);
        SetupText(closeLabelGO, "X", font, 20f, TextAlignmentOptions.Center,
                  Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // Scroll View + GridLayoutGroup (보유 꽃 그리드)
        GameObject scrollGO = new GameObject("Scroll View");
        scrollGO.transform.SetParent(card.transform, false);
        RectTransform srt = scrollGO.AddComponent<RectTransform>();
        srt.anchorMin = Vector2.zero;
        srt.anchorMax = Vector2.one;
        srt.offsetMin = Vector2.zero;
        srt.offsetMax = new Vector2(0f, -55f);

        Image scrollImg = scrollGO.AddComponent<Image>();
        scrollImg.color = new Color(0f, 0f, 0f, 0f);

        ScrollRect sr = scrollGO.AddComponent<ScrollRect>();
        sr.horizontal = false;
        sr.vertical = true;
        sr.scrollSensitivity = 35f;

        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollGO.transform, false);
        RectTransform vprt = viewport.AddComponent<RectTransform>();
        vprt.anchorMin = Vector2.zero;
        vprt.anchorMax = Vector2.one;
        vprt.offsetMin = Vector2.zero;
        vprt.offsetMax = Vector2.zero;
        Image vpImg = viewport.AddComponent<Image>();
        vpImg.color = new Color(1f, 1f, 1f, 0.01f);
        Mask mask = viewport.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        GameObject content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        RectTransform crt = content.AddComponent<RectTransform>();
        crt.anchorMin = new Vector2(0f, 1f);
        crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(0f, 1f);
        crt.anchoredPosition = Vector2.zero;
        crt.sizeDelta = Vector2.zero;

        GridLayoutGroup grid = content.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(160f, 160f);
        grid.spacing = new Vector2(12f, 12f);
        grid.padding = new RectOffset(16, 16, 16, 16);
        grid.childAlignment = TextAnchor.UpperLeft;

        ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        sr.viewport = vprt;
        sr.content = crt;

        // ── FlowerDexPanel 컴포넌트 연결 ──
        // closeButton도 openButton과 동일한 이유로 필드만 연결한다(onClick.AddListener 직접 호출 금지 —
        // 에디터 스크립트에서 붙인 리스너는 씬에 저장되지 않는다). 실제 연결은 FlowerDexPanel.Start()가 한다.
        FlowerDexPanel dexPanel = panelRoot.AddComponent<FlowerDexPanel>();
        dexPanel.root = panelRoot;
        dexPanel.content = crt;
        dexPanel.itemPrefab = GetOrCreateFlowerDexItemPrefab();
        dexPanel.closeButton = closeButton;

        panelRoot.SetActive(false);
        return dexPanel;
    }

    /// <summary>
    /// FlowerDexItem 프리팹을 로드하거나, 없으면 최초 1회 생성해서 에셋으로 저장한다.
    /// FlowerUpgradeItem과 동일 원칙 — 이미 존재하면 재사용(손으로 꾸며도 재빌드 시 덮어쓰지 않음).
    /// </summary>
    static FlowerDexItem GetOrCreateFlowerDexItemPrefab()
    {
        FlowerDexItem existing = AssetDatabase.LoadAssetAtPath<FlowerDexItem>(FLOWER_DEX_ITEM_PREFAB_PATH);
        if (existing != null) return existing;

        TMP_FontAsset font = LoadNotoFont();

        GameObject temp = new GameObject("FlowerDexItem");
        RectTransform rt = temp.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(160f, 160f);

        Image bg = temp.AddComponent<Image>();
        bg.color = new Color(1f, 1f, 1f, 0.06f);

        Button selectButton = temp.AddComponent<Button>();
        selectButton.targetGraphic = bg;

        // 현재 메인에 표시 중인 꽃 강조 테두리 (기본 비활성)
        GameObject highlight = new GameObject("CurrentHighlight");
        highlight.transform.SetParent(temp.transform, false);
        RectTransform hlRT = highlight.AddComponent<RectTransform>();
        hlRT.anchorMin = Vector2.zero;
        hlRT.anchorMax = Vector2.one;
        hlRT.offsetMin = Vector2.zero;
        hlRT.offsetMax = Vector2.zero;
        Image hlImg = highlight.AddComponent<Image>();
        hlImg.color = new Color(1f, 0.35f, 0.62f, 0.35f);
        highlight.SetActive(false);

        GameObject iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(temp.transform, false);
        RectTransform iconRT = iconGO.AddComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0.2f, 0.35f);
        iconRT.anchorMax = new Vector2(0.8f, 0.95f);
        iconRT.offsetMin = Vector2.zero;
        iconRT.offsetMax = Vector2.zero;
        Image iconImg = iconGO.AddComponent<Image>();
        iconImg.preserveAspect = true;

        GameObject nameGO = new GameObject("NameText");
        nameGO.transform.SetParent(temp.transform, false);
        SetupText(nameGO, "-", font, 16f, TextAlignmentOptions.Center,
                  new Vector2(0f, 0.16f), new Vector2(1f, 0.35f), new Vector2(4f, 0f), new Vector2(-4f, 0f));

        GameObject statusGO = new GameObject("StatusText");
        statusGO.transform.SetParent(temp.transform, false);
        SetupText(statusGO, "-", font, 13f, TextAlignmentOptions.Center,
                  new Vector2(0f, 0f), new Vector2(1f, 0.16f), new Vector2(4f, 0f), new Vector2(-4f, 0f));

        FlowerDexItem item = temp.AddComponent<FlowerDexItem>();
        item.iconImage = iconImg;
        item.flowerNameText = nameGO.GetComponent<TMP_Text>();
        item.statusText = statusGO.GetComponent<TMP_Text>();
        item.selectButton = selectButton;
        item.currentHighlight = highlight;

        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(temp, FLOWER_DEX_ITEM_PREFAB_PATH);
        Object.DestroyImmediate(temp);

        return savedPrefab != null ? savedPrefab.GetComponent<FlowerDexItem>() : null;
    }

    // ── 헬퍼 메소드들 ─────────────────────────────────────────────────
    static GameObject CreateStretchChild(Transform parent, string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return go;
    }

    static void SetupText(GameObject go, string content, TMP_FontAsset font, float fontSize,
                          TextAlignmentOptions align, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.offsetMin = oMin;
        rt.offsetMax = oMax;

        TMP_Text tmp = go.GetComponent<TextMeshProUGUI>();
        if (tmp == null) tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text                  = content;
        tmp.font                  = font;
        tmp.fontSize              = fontSize;
        tmp.alignment             = align;
        tmp.color                 = Color.white;
        tmp.enableWordWrapping    = true;
        tmp.overflowMode          = TextOverflowModes.Overflow;
    }

    static Sprite GetWhiteSprite()
    {
        Texture2D tex = Texture2D.whiteTexture;
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
    }

    static TMP_FontAsset LoadNotoFont()
    {
        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Fonts/NotoSansKR-Bold SDF.asset");
    }

    static Transform FindChildRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform found = FindChildRecursive(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
#endif
