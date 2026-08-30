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
    const float DEX_HEADER_HEIGHT = 64f;

    const string FLOWER_UPGRADE_ITEM_PREFAB_PATH = "Assets/Project/Prefabs/FlowerUpgradeItem.prefab";
    const string FLOWER_DEX_ITEM_PREFAB_PATH = "Assets/Project/Prefabs/FlowerDexItem.prefab";

    /// <summary>
    /// CI/커맨드라인 전용 진입점 (Unity -batchmode -executeMethod로 호출).
    /// 메뉴로 BuildLayout()을 직접 누르면 이미 열려 있는 씬을 대상으로 동작하고 저장은 사람이 하지만,
    /// 배치 모드는 씬을 스스로 열고 저장까지 마쳐야 한다는 점만 다르다.
    /// </summary>
    public static void BuildLayoutAndSave()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
        BuildLayout();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// GardenManager는 FlowerManager/GameManager처럼 씬에 전용 GameObject로 미리 존재해야 하는데,
    /// 이번 세션에 정원 시스템을 새로 추가하면서 정작 씬에 그 GameObject를 만드는 걸 빠뜨렸다 —
    /// 그래서 GardenManager.Instance가 항상 null이라 배치 UI(격자·목록)가 아무것도 못 그리고,
    /// 확장 버튼을 누르면 NullReferenceException이 나던 버그였다. FlowerManager와 똑같이 스크립트
    /// 하나만 붙은 전용 GameObject를 만들어 둔다(이미 있으면 중복 생성하지 않음).
    /// </summary>
    static void EnsureGardenManager()
    {
        if (Object.FindFirstObjectByType<GardenManager>() != null) return;

        GameObject gardenManagerGO = new GameObject("GardenManager");
        gardenManagerGO.AddComponent<GardenManager>();
    }

    [MenuItem("Tools/🌸 Build PC Layout")]
    public static void BuildLayout()
    {
        EnsureGardenManager();

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

        // 정원 선택 정보 패널(작업 1)을 ShopPanel 위 오버레이로 띄운다 — 원래는 GardenPanel 자식으로
        // 넣었는데, 격자 하이라이트와 정보 패널이 같은 화면에 동시에 떠서 좁고 불편하다는 신고가
        // 있었다. 정원을 보는 동안 상점 패널은 어차피 안 쓰이므로 그 자리를 빌려 쓴다.
        GardenFlowerInfoPanel gardenInfoPanel = CreateGardenFlowerInfoPanel(shopPanel.transform, LoadNotoFont());

        // 2. GrowthPanel -> RightSlot 자식 (꽃 성장 / 플레이어 강화 탭 전환, PanelSwitcher 기반)
        GameObject growthPanel = CreateGrowthPanel(rightSlot.transform, gardenInfoPanel);

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

        // ── 유대 메모리얼 뷰 (Canvas 최상위, 도감과 동일한 전체 오버레이 방식) ──
        BuildMemorialViewPanel(canvasGO.transform);

        // ── 메모리얼 재생기(VN) — MemorialViewPanel이 lines가 있는 항목을 열 때 이걸 재생한다 ──
        BuildMemorialPlayerPanel(canvasGO.transform);

        // ── 설정 화면 (Canvas 최상위, 동일한 전체 오버레이 방식) ──
        SettingsPanel settingsPanel = BuildSettingsPanel(canvasGO.transform);

        // ── 치트 패널 (설정 화면 위에 겹쳐서 열림, 비밀번호로 잠김) ──
        BuildCheatPanel(canvasGO.transform);

        // TopBar의 도감/설정 열기 버튼을 각 패널의 openButton 필드에 연결한다.
        // 여기서 onClick.AddListener를 직접 호출하지 않는 이유: 에디터 스크립트는 Play 모드 밖에서
        // 실행되므로 그렇게 붙인 리스너는 씬에 저장되지 않는다(런타임 전용 리스너). 필드만 연결해두면
        // 각 패널의 Start()가 실제 실행 시점에 스스로 AddListener하므로 항상 유효하다.
        Transform dexButtonTr = topBarGO.transform.Find("DexButton");
        if (dexButtonTr != null)
            dexPanel.openButton = dexButtonTr.GetComponent<Button>();

        Transform settingsButtonTr = topBarGO.transform.Find("SettingsButton");
        if (settingsButtonTr != null)
            settingsPanel.openButton = settingsButtonTr.GetComponent<Button>();

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

        // GoldPerSecondText (오른쪽 끝은 SettingsButton + DexButton 두 개 자리만큼 비워둠)
        Transform rTr = topBar.transform.Find("GoldPerSecondText");
        GameObject rGO = (rTr != null) ? rTr.gameObject : new GameObject("GoldPerSecondText");
        rGO.transform.SetParent(topBar.transform, false);
        SetupText(rGO, "골드 +0.0/s", font, 18f, TextAlignmentOptions.Right,
                  new Vector2(0.5f, 0f), new Vector2(1f, 1f), new Vector2(8f, 0f), new Vector2(-134f, 0f));

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

        // SettingsButton (설정 열기, DexButton 왼쪽에 같은 폭으로 붙임)
        Transform settingsTr = topBar.transform.Find("SettingsButton");
        GameObject settingsGO = (settingsTr != null) ? settingsTr.gameObject : new GameObject("SettingsButton");
        settingsGO.transform.SetParent(topBar.transform, false);
        RectTransform settingsRT = settingsGO.GetComponent<RectTransform>();
        if (settingsRT == null) settingsRT = settingsGO.AddComponent<RectTransform>();
        settingsRT.anchorMin = new Vector2(1f, 0f);
        settingsRT.anchorMax = new Vector2(1f, 1f);
        settingsRT.pivot = new Vector2(1f, 0.5f);
        settingsRT.sizeDelta = new Vector2(55f, 0f);
        settingsRT.anchoredPosition = new Vector2(-71f, 0f); // DexButton(-8~-63) 왼쪽에 8px 간격

        Image settingsBg = settingsGO.GetComponent<Image>();
        if (settingsBg == null) settingsBg = settingsGO.AddComponent<Image>();
        settingsBg.color = new Color(0.3f, 0.3f, 0.35f, 1f);

        Button settingsButton = settingsGO.GetComponent<Button>();
        if (settingsButton == null) settingsButton = settingsGO.AddComponent<Button>();
        settingsButton.targetGraphic = settingsBg;

        Transform settingsLabelTr = settingsGO.transform.Find("Label");
        GameObject settingsLabelGO = (settingsLabelTr != null) ? settingsLabelTr.gameObject : new GameObject("Label");
        settingsLabelGO.transform.SetParent(settingsGO.transform, false);
        SetupText(settingsLabelGO, "설정", font, 16f, TextAlignmentOptions.Center,
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

        // 개화 후에는 이 바가 유대(Bond) 게이지로 재사용된다(UIManager.UpdateFlowerStatusUI) — 탭하면
        // 지금 표시 중인 꽃의 메모리얼을 연다. 미개화 중에는 UIManager가 조건만으로 무시하므로
        // 버튼 자체는 항상 붙어 있어도 안전하다(요청 명세 5.3 진입 경로: "메인 화면의 개화 일러스트 탭"
        // 대신 유대 게이지 탭으로 대체 — 스프라이트 탭은 이미 터치/유대 획득 제스처로 쓰이고 있어
        // 겹치면 혼란스럽기 때문).
        Button barButton = barGO.GetComponent<Button>();
        if (barButton == null) barButton = barGO.AddComponent<Button>();
        barButton.targetGraphic = barTrack;
        barButton.transition = Selectable.Transition.None; // 게이지 색이 버튼 상태색으로 덮이지 않게

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

        // 2-1. UnreadBadge (안 읽은 메모리얼 표시 — 바 우측 상단에 작게 겹치는 점 하나, 기본 비활성)
        Transform badgeTr = barGO.transform.Find("UnreadBadge");
        GameObject badgeGO = (badgeTr != null) ? badgeTr.gameObject : new GameObject("UnreadBadge");
        badgeGO.transform.SetParent(barGO.transform, false);
        RectTransform badgeRT = badgeGO.GetComponent<RectTransform>();
        if (badgeRT == null) badgeRT = badgeGO.AddComponent<RectTransform>();
        badgeRT.anchorMin = new Vector2(1f, 1f);
        badgeRT.anchorMax = new Vector2(1f, 1f);
        badgeRT.pivot = new Vector2(0.5f, 0.5f);
        badgeRT.sizeDelta = new Vector2(16f, 16f);
        badgeRT.anchoredPosition = new Vector2(-2f, 2f);
        Image badgeImg = badgeGO.GetComponent<Image>();
        if (badgeImg == null) badgeImg = badgeGO.AddComponent<Image>();
        badgeImg.color = new Color(1f, 0.85f, 0.3f, 1f); // 골드빛 — 레벨업 팝업과 동일 강조색
        badgeGO.SetActive(false);

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
    static GameObject CreateGrowthPanel(Transform parent, GardenFlowerInfoPanel gardenInfoPanel)
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
        Button gardenTabButton  = CreateTabButton(tabBar.transform, "GardenTabButton", "정원", font);

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
        GameObject gardenPanelGO = CreateGardenPanel(panelContainer.transform, gardenInfoPanel);

        // ── PanelSwitcher 연결 (범용 탭 전환기 — Flower/Player/Garden을 모름) ──
        PanelSwitcher switcher = growthPanel.AddComponent<PanelSwitcher>();
        switcher.tabs = new System.Collections.Generic.List<PanelSwitcher.Tab>
        {
            new PanelSwitcher.Tab { button = flowerTabButton, panel = flowerUpgradePanelGO },
            new PanelSwitcher.Tab { button = playerTabButton, panel = playerUpgradePanelGO },
            new PanelSwitcher.Tab { button = gardenTabButton, panel = gardenPanelGO }
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

    /// <summary>
    /// 숫자만 입력받는 TMP_InputField를 만든다(도감 이미지 뷰어의 확대율 직접 입력용).
    /// TMP_InputField는 Text Area(RectMask2D) 안에 Placeholder/Text 두 개의 TextMeshProUGUI가 필요한
    /// 표준 구조라, 여기서 한 번만 조립해두고 재사용한다.
    /// </summary>
    static TMP_InputField CreateNumericInputField(Transform parent, string name, string placeholderText, TMP_FontAsset font)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();

        Image bg = go.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.14f, 1f);

        TMP_InputField input = go.AddComponent<TMP_InputField>();

        GameObject textAreaGO = new GameObject("Text Area");
        textAreaGO.transform.SetParent(go.transform, false);
        RectTransform textAreaRT = textAreaGO.AddComponent<RectTransform>();
        textAreaRT.anchorMin = Vector2.zero;
        textAreaRT.anchorMax = Vector2.one;
        textAreaRT.offsetMin = new Vector2(8f, 2f);
        textAreaRT.offsetMax = new Vector2(-8f, -2f);
        textAreaGO.AddComponent<RectMask2D>();

        GameObject placeholderGO = new GameObject("Placeholder");
        placeholderGO.transform.SetParent(textAreaGO.transform, false);
        SetupText(placeholderGO, placeholderText, font, 16f, TextAlignmentOptions.Center,
                  Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        TMP_Text placeholderTmp = placeholderGO.GetComponent<TMP_Text>();
        placeholderTmp.color = new Color(1f, 1f, 1f, 0.4f);
        placeholderTmp.fontStyle = FontStyles.Italic;

        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(textAreaGO.transform, false);
        SetupText(textGO, "", font, 16f, TextAlignmentOptions.Center,
                  Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        TMP_Text textTmp = textGO.GetComponent<TMP_Text>();

        input.textViewport = textAreaRT;
        input.textComponent = textTmp;
        input.placeholder = placeholderTmp;
        input.contentType = TMP_InputField.ContentType.DecimalNumber;
        input.characterLimit = 6;

        return input;
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

        FlowerSortSelector sortSelector = CreateSortSelector(panel.transform, header.transform, font);

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

        // 정렬 펼침 목록이 방금 만든 "Scroll View"(꽃 목록)보다 항상 위에 그려지도록 마지막 형제로
        // 옮긴다 — 그렇지 않으면 먼저 만들어진 목록이 나중에 만들어진 스크롤 뷰 밑에 깔려버린다
        // (같은 부모 안에서는 나중 형제가 위에 그려지는 유니티 UI 규칙).
        if (sortSelector.optionListRoot != null)
            sortSelector.optionListRoot.transform.SetAsLastSibling();

        return panel;
    }

    /// <summary> 레벨업단위(LevelUpAmountSelector) 선택용 순환 버튼 하나를 만들고 부착해서 반환한다. </summary>
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

        var labelField = typeof(T).GetField("label");
        labelField?.SetValue(selector, labelGO.GetComponent<TMP_Text>());

        return selector;
    }

    /// <summary>
    /// 정렬(FlowerSortSelector) 전용 — 순환 버튼 대신 "누르면 목록이 펼쳐지는" 형태로 만든다.
    /// 모드가 7개로 늘면서 순환식은 최대 6번 눌러야 원하는 모드에 닿을 수 있어 불편했다.
    /// 펼침 목록(optionListRoot)은 headerParent가 아니라 panelParent의 직계 자식으로 만든다 —
    /// header는 VerticalLayoutGroup이 자식을 순서대로 쌓기 때문에, 그 안에 넣으면 목록이 "펼쳐지는"
    /// 대신 다른 버튼을 밀어내는 레이아웃 변화가 되어버린다. panelParent에 바로 붙여야 아래 스크롤
    /// 목록 위에 겹쳐서(오버레이) 뜬다 — 실제로 맨 위에 그려지도록 형제 순서를 맞추는 것은 호출부
    /// (CreateFlowerUpgradePanel)가 SetAsLastSibling으로 마무리한다.
    ///
    /// SortModeLabels의 순서는 FlowerSortSelector.Order 배열과 반드시 인덱스가 일치해야 한다.
    /// </summary>
    static readonly string[] SortModeLabels = { "도감순", "가격↑", "가격↓", "레벨↑", "레벨↓", "효율순", "가성비순" };

    static FlowerSortSelector CreateSortSelector(Transform panelParent, Transform headerParent, TMP_FontAsset font)
    {
        // ── 메인 토글 버튼 (헤더 안, 기존 순환 버튼과 같은 자리) ──
        GameObject buttonGO = new GameObject("SortSelectorButton");
        buttonGO.transform.SetParent(headerParent, false);
        LayoutElement btnLE = buttonGO.AddComponent<LayoutElement>();
        btnLE.preferredHeight = 34f;

        Image btnImg = buttonGO.AddComponent<Image>();
        btnImg.color = new Color(0.2f, 0.22f, 0.3f, 1f);

        Button toggleButton = buttonGO.AddComponent<Button>();
        toggleButton.targetGraphic = btnImg;

        GameObject labelGO = new GameObject("Label");
        labelGO.transform.SetParent(buttonGO.transform, false);
        SetupText(labelGO, "정렬: 도감순", font, 16f, TextAlignmentOptions.Center,
                  Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // ── 펼침 목록 (panelParent 직계 자식, 기본 숨김) ──
        const float rowHeight = 32f;
        int optionCount = SortModeLabels.Length;

        GameObject listRoot = new GameObject("SortOptionList");
        listRoot.transform.SetParent(panelParent, false);
        RectTransform listRT = listRoot.AddComponent<RectTransform>();
        listRT.anchorMin = new Vector2(0f, 1f);
        listRT.anchorMax = new Vector2(1f, 1f);
        listRT.pivot = new Vector2(0.5f, 1f);
        listRT.anchoredPosition = new Vector2(0f, -76f); // 헤더(정렬+레벨업단위 두 행) 바로 아래 — 두 버튼을 가리지 않음
        listRT.sizeDelta = new Vector2(0f, rowHeight * optionCount + 8f);

        Image listBg = listRoot.AddComponent<Image>();
        listBg.color = new Color(0.12f, 0.13f, 0.18f, 0.98f);

        VerticalLayoutGroup listLayout = listRoot.AddComponent<VerticalLayoutGroup>();
        listLayout.childForceExpandWidth = true;
        listLayout.childForceExpandHeight = false;
        listLayout.childControlWidth = true;
        listLayout.childControlHeight = true;
        listLayout.spacing = 2f;
        listLayout.padding = new RectOffset(4, 4, 4, 4);

        Button[] optionButtons = new Button[optionCount];
        for (int i = 0; i < optionCount; i++)
        {
            GameObject rowGO = new GameObject($"Option_{i}_{SortModeLabels[i]}");
            rowGO.transform.SetParent(listRoot.transform, false);
            LayoutElement rowLE = rowGO.AddComponent<LayoutElement>();
            rowLE.preferredHeight = rowHeight;

            Image rowImg = rowGO.AddComponent<Image>();
            rowImg.color = new Color(1f, 1f, 1f, 0.06f);

            Button rowButton = rowGO.AddComponent<Button>();
            rowButton.targetGraphic = rowImg;

            GameObject rowLabelGO = new GameObject("Label");
            rowLabelGO.transform.SetParent(rowGO.transform, false);
            SetupText(rowLabelGO, SortModeLabels[i], font, 15f, TextAlignmentOptions.Center,
                      Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            optionButtons[i] = rowButton;
        }

        listRoot.SetActive(false);

        FlowerSortSelector selector = buttonGO.AddComponent<FlowerSortSelector>();
        selector.toggleButton = toggleButton;
        selector.label = labelGO.GetComponent<TMP_Text>();
        selector.optionListRoot = listRoot;
        selector.optionButtons = optionButtons;

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

    // ── GardenPanel 생성 (정원 탭: 1단계 최소 배치 UI — 탭 전용, 드래그 없음) ──────
    //
    // 구조: 상단 요약(총 G/s + 확장 버튼) / 가운데 격자(스크롤 가능) / 하단 배치 대상 목록(가로 스크롤).
    // 하이라이트·드래그·시듦 연출은 다음 단계에서 추가한다 — 지금은 배치를 바꿨을 때 총 G/s 숫자가
    // 실제로 움직이는지(=인접 효과 계산 경로가 살아있는지) 확인하는 것이 목적이다.
    static GameObject CreateGardenPanel(Transform parent, GardenFlowerInfoPanel infoPanel)
    {
        TMP_FontAsset font = LoadNotoFont();

        GameObject panel = new GameObject("GardenPanel");
        panel.transform.SetParent(parent, false);
        RectTransform rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // ── 상단 요약 (총 G/s + 확장 버튼) ──
        GameObject summaryRow = new GameObject("SummaryRow");
        summaryRow.transform.SetParent(panel.transform, false);
        RectTransform summaryRT = summaryRow.AddComponent<RectTransform>();
        summaryRT.anchorMin = new Vector2(0f, 1f);
        summaryRT.anchorMax = new Vector2(1f, 1f);
        summaryRT.pivot = new Vector2(0.5f, 1f);
        summaryRT.sizeDelta = new Vector2(0f, 44f);

        GameObject gpsTextGO = new GameObject("TotalGpsText");
        gpsTextGO.transform.SetParent(summaryRow.transform, false);
        SetupText(gpsTextGO, "총 0 G/s", font, 16f, TextAlignmentOptions.Left,
                  Vector2.zero, new Vector2(0.55f, 1f), new Vector2(8f, 0f), Vector2.zero);

        GameObject expandGO = new GameObject("ExpandButton");
        expandGO.transform.SetParent(summaryRow.transform, false);
        RectTransform expandRT = expandGO.AddComponent<RectTransform>();
        expandRT.anchorMin = new Vector2(0.55f, 0.1f);
        expandRT.anchorMax = new Vector2(1f, 0.9f);
        expandRT.offsetMin = new Vector2(4f, 0f);
        expandRT.offsetMax = new Vector2(-8f, 0f);
        Image expandBg = expandGO.AddComponent<Image>();
        expandBg.color = new Color(0.3f, 0.3f, 0.35f, 1f);
        Button expandButton = expandGO.AddComponent<Button>();
        expandButton.targetGraphic = expandBg;
        GameObject expandLabelGO = new GameObject("Label");
        expandLabelGO.transform.SetParent(expandGO.transform, false);
        SetupText(expandLabelGO, "확장", font, 13f, TextAlignmentOptions.Center,
                  Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // ── 하단 배치 대상 목록 (가로 스크롤) ──
        GameObject rosterSection = new GameObject("RosterSection");
        rosterSection.transform.SetParent(panel.transform, false);
        RectTransform rosterSectionRT = rosterSection.AddComponent<RectTransform>();
        rosterSectionRT.anchorMin = new Vector2(0f, 0f);
        rosterSectionRT.anchorMax = new Vector2(1f, 0f);
        rosterSectionRT.pivot = new Vector2(0.5f, 0f);
        rosterSectionRT.sizeDelta = new Vector2(0f, 160f); // 모양 미리보기 추가로 항목 키(80→120)에 맞춰 확대

        GameObject armedStatusGO = new GameObject("ArmedStatusText");
        armedStatusGO.transform.SetParent(rosterSection.transform, false);
        SetupText(armedStatusGO, "배치할 꽃을 선택하세요.", font, 13f, TextAlignmentOptions.Center,
                  new Vector2(0f, 0.78f), new Vector2(1f, 1f), new Vector2(6f, 0f), new Vector2(-6f, 0f));

        GameObject rosterScrollGO = new GameObject("RosterScroll");
        rosterScrollGO.transform.SetParent(rosterSection.transform, false);
        RectTransform rosterScrollRT = rosterScrollGO.AddComponent<RectTransform>();
        rosterScrollRT.anchorMin = new Vector2(0f, 0f);
        rosterScrollRT.anchorMax = new Vector2(1f, 0.78f);
        rosterScrollRT.offsetMin = Vector2.zero;
        rosterScrollRT.offsetMax = Vector2.zero;

        Image rosterScrollImg = rosterScrollGO.AddComponent<Image>();
        rosterScrollImg.color = new Color(0f, 0f, 0f, 0f);

        ScrollRect rosterSR = rosterScrollGO.AddComponent<ScrollRect>();
        rosterSR.horizontal = true;
        rosterSR.vertical = false;
        rosterSR.scrollSensitivity = 35f;

        GameObject rosterViewport = new GameObject("Viewport");
        rosterViewport.transform.SetParent(rosterScrollGO.transform, false);
        RectTransform rosterVpRT = rosterViewport.AddComponent<RectTransform>();
        rosterVpRT.anchorMin = Vector2.zero;
        rosterVpRT.anchorMax = Vector2.one;
        rosterVpRT.offsetMin = Vector2.zero;
        rosterVpRT.offsetMax = Vector2.zero;
        Image rosterVpImg = rosterViewport.AddComponent<Image>();
        rosterVpImg.color = new Color(1f, 1f, 1f, 0.01f);
        Mask rosterMask = rosterViewport.AddComponent<Mask>();
        rosterMask.showMaskGraphic = false;

        GameObject rosterContentGO = new GameObject("Content");
        rosterContentGO.transform.SetParent(rosterViewport.transform, false);
        RectTransform rosterContentRT = rosterContentGO.AddComponent<RectTransform>();
        rosterContentRT.anchorMin = new Vector2(0f, 0f);
        rosterContentRT.anchorMax = new Vector2(0f, 1f);
        rosterContentRT.pivot = new Vector2(0f, 0.5f);
        rosterContentRT.anchoredPosition = Vector2.zero;
        rosterContentRT.sizeDelta = Vector2.zero;

        HorizontalLayoutGroup rosterLayout = rosterContentGO.AddComponent<HorizontalLayoutGroup>();
        rosterLayout.childForceExpandWidth = false;
        rosterLayout.childForceExpandHeight = true;
        rosterLayout.childControlWidth = false;
        rosterLayout.childControlHeight = true;
        rosterLayout.spacing = 8f;
        rosterLayout.padding = new RectOffset(8, 8, 4, 4);

        ContentSizeFitter rosterCsf = rosterContentGO.AddComponent<ContentSizeFitter>();
        rosterCsf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        rosterSR.viewport = rosterVpRT;
        rosterSR.content = rosterContentRT;

        // ── 격자 (가운데 나머지 전부, 스크롤 가능 — 열 수는 런타임에 GardenPanel이 정원 크기로 맞춘다) ──
        GameObject gridArea = new GameObject("GridArea");
        gridArea.transform.SetParent(panel.transform, false);
        RectTransform gridAreaRT = gridArea.AddComponent<RectTransform>();
        gridAreaRT.anchorMin = Vector2.zero;
        gridAreaRT.anchorMax = Vector2.one;
        gridAreaRT.offsetMin = new Vector2(0f, 160f); // RosterSection 높이(160)와 반드시 일치해야 함
        gridAreaRT.offsetMax = new Vector2(0f, -44f);

        // 칸 크기를 55→80으로 키웠다(요청: "정원을 더 크게 보여줘 기본을") — 최대 크기(6×5)에서도
        // 6*80+5*4(간격)+16(패딩)=516px로 사이드바 폭(~656px)에 여유 있게 들어가고, 최소 크기(3×2)
        // 기준으로 화면이 휑해 보이던 문제가 줄어든다.
        RectTransform gridContent = BuildDexScrollGrid(gridArea.transform, new Vector2(80f, 80f));

        // 도감(BuildDexScrollGrid 기본값)은 Content를 뷰포트 폭 전체로 늘리고 위쪽에 고정한 뒤 세로만
        // PreferredSize로 맞춘다 — 항목이 많아 세로로 쭉 나열/스크롤되는 화면엔 맞지만, 정원은 격자가
        // 뷰포트보다 작을 때가 대부분이라 이 방식이면 왼쪽 위로 쏠려 보인다. 정원은 가로·세로 둘 다
        // PreferredSize로 격자 크기에 딱 맞게 줄이고, Content 자체를 뷰포트 한가운데(0.5, 0.5)에
        // 앵커/피벗으로 고정해 "width·height 모두 중앙"이 되게 한다(요청: x축·y축 둘 다).
        gridContent.anchorMin = new Vector2(0.5f, 0.5f);
        gridContent.anchorMax = new Vector2(0.5f, 0.5f);
        gridContent.pivot = new Vector2(0.5f, 0.5f);
        gridContent.anchoredPosition = Vector2.zero;

        ContentSizeFitter gridContentCsf = gridContent.GetComponent<ContentSizeFitter>();
        if (gridContentCsf != null) gridContentCsf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        GridLayoutGroup gridLayout = gridContent.GetComponent<GridLayoutGroup>();
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.spacing = new Vector2(4f, 4f);
        gridLayout.padding = new RectOffset(8, 8, 8, 8);
        gridLayout.childAlignment = TextAnchor.MiddleCenter; // Content가 격자 크기에 딱 맞으므로 지금은 무해하지만, 확장 등으로 셀 배열이 고르지 않을 때를 대비해 유지

        // 선택 정보 패널(작업 1)은 이제 이 메서드 밖(ShopPanel 위)에서 만들어져 매개변수로 들어온다 —
        // BuildLayout()의 CreateGardenFlowerInfoPanel(shopPanel.transform, ...) 호출 참고.

        // ── GardenPanel 컴포넌트 연결 ──
        GardenPanel gardenPanel = panel.AddComponent<GardenPanel>();
        gardenPanel.root              = panel;
        gardenPanel.totalGpsText      = gpsTextGO.GetComponent<TMP_Text>();
        gardenPanel.expandButton      = expandButton;
        gardenPanel.expandButtonText  = expandLabelGO.GetComponent<TMP_Text>();
        gardenPanel.gridContent       = gridContent;
        gardenPanel.gridLayout        = gridLayout;
        gardenPanel.cellPrefab        = GetOrCreateGardenCellPrefab();
        gardenPanel.rosterContent     = rosterContentRT;
        gardenPanel.rosterItemPrefab  = GetOrCreateGardenRosterItemPrefab();
        gardenPanel.armedStatusText   = armedStatusGO.GetComponent<TMP_Text>();
        gardenPanel.infoPanel         = infoPanel;

        return panel;
    }

    /// <summary>
    /// 정원 선택 정보 패널(작업 1) — OfflineSummaryPopup과 같은 "dim 배경 + 중앙 카드" 오버레이
    /// 구조다. ShopPanel의 자식으로 둔다 — 정원 탭을 보는 동안은 상점을 안 쓰므로 그 화면을 잠깐
    /// 빌려 쓰는 것("격자 하이라이트와 정보 패널이 같은 화면에 같이 떠서 좁다"는 신고로 옮김).
    /// GardenPanel(다른 사이드에 있음)은 infoPanel 필드 참조만 들고 Show/Hide를 호출한다.
    /// 내용 길이가 꽃마다 달라서(받고 있는 효과 개수 등) 고정 카드 대신 세로 스크롤 텍스트로 만든다.
    /// </summary>
    static GardenFlowerInfoPanel CreateGardenFlowerInfoPanel(Transform parent, TMP_FontAsset font)
    {
        GameObject popupRoot = new GameObject("GardenFlowerInfoPanel");
        popupRoot.transform.SetParent(parent, false);
        RectTransform rootRT = popupRoot.AddComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;

        Image dim = popupRoot.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.75f);

        GameObject card = new GameObject("Card");
        card.transform.SetParent(popupRoot.transform, false);
        RectTransform cardRT = card.AddComponent<RectTransform>();
        cardRT.anchorMin = new Vector2(0.5f, 0.5f);
        cardRT.anchorMax = new Vector2(0.5f, 0.5f);
        cardRT.pivot = new Vector2(0.5f, 0.5f);
        cardRT.anchoredPosition = Vector2.zero;
        cardRT.sizeDelta = new Vector2(560f, 620f);

        Image cardBg = card.AddComponent<Image>();
        cardBg.color = new Color(0.14f, 0.1f, 0.14f, 0.97f);

        VerticalLayoutGroup cardLayout = card.AddComponent<VerticalLayoutGroup>();
        cardLayout.childForceExpandWidth = true;
        cardLayout.childForceExpandHeight = false;
        cardLayout.childControlWidth = true;
        cardLayout.childControlHeight = true;
        cardLayout.spacing = 12f;
        cardLayout.padding = new RectOffset(20, 20, 20, 20);

        // 본문 — 길이가 꽃마다 달라 세로 스크롤로 감싼다.
        GameObject scrollGO = new GameObject("BodyScroll");
        scrollGO.transform.SetParent(card.transform, false);
        scrollGO.AddComponent<RectTransform>();
        LayoutElement scrollLE = scrollGO.AddComponent<LayoutElement>();
        scrollLE.preferredHeight = 480f;
        scrollLE.flexibleHeight = 1f;
        Image scrollBg = scrollGO.AddComponent<Image>();
        scrollBg.color = new Color(0f, 0f, 0f, 0.001f); // Mask에는 Graphic이 필요 — 사실상 투명
        ScrollRect scrollRect = scrollGO.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollGO.transform, false);
        RectTransform vpRT = viewport.AddComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero;
        vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = Vector2.zero;
        vpRT.offsetMax = Vector2.zero;
        Image vpImg = viewport.AddComponent<Image>();
        vpImg.color = new Color(1f, 1f, 1f, 0.01f);
        viewport.AddComponent<Mask>().showMaskGraphic = false;

        // Content는 ContentSizeFitter로 세로 크기가 텍스트 길이에 맞춰 자동으로 자라야 하므로,
        // SetupText(고정 앵커 박스 전제)를 쓰지 않고 위쪽 고정(anchorMin.y=anchorMax.y=1, pivot.y=1)
        // + 가로만 꽉 채우는 앵커를 직접 구성한다 — SetupText를 쓰면 앵커가 (0,0)-(0,0)으로 다시
        // 덮어써져서 이 자동 성장 레이아웃이 깨진다.
        GameObject contentGO = new GameObject("Content");
        contentGO.transform.SetParent(viewport.transform, false);
        RectTransform contentRT = contentGO.AddComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0f, 1f);
        contentRT.anchorMax = new Vector2(1f, 1f);
        contentRT.pivot = new Vector2(0.5f, 1f);
        contentRT.anchoredPosition = Vector2.zero;
        contentRT.sizeDelta = Vector2.zero;

        TMP_Text contentText = contentGO.AddComponent<TextMeshProUGUI>();
        contentText.text = "";
        contentText.font = font;
        contentText.fontSize = 17f;
        contentText.lineSpacing = 6f; // 가독성 보강 — 줄 간격을 살짝 넓혀 섹션 구분이 눈에 더 잘 들어오게
        contentText.alignment = TextAlignmentOptions.TopLeft;
        contentText.color = Color.white;
        contentText.richText = true; // GardenFlowerInfoPanel이 <b>/<color> 태그로 제목·수치를 강조한다
        contentText.enableWordWrapping = true;
        contentText.overflowMode = TextOverflowModes.Overflow;

        ContentSizeFitter contentCsf = contentGO.AddComponent<ContentSizeFitter>();
        contentCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = vpRT;
        scrollRect.content = contentRT;

        // 버튼 행 (손질하기 / 제거 / 닫기)
        GameObject buttonRow = new GameObject("ButtonRow");
        buttonRow.transform.SetParent(card.transform, false);
        LayoutElement buttonRowLE = buttonRow.AddComponent<LayoutElement>();
        buttonRowLE.preferredHeight = 52f;
        HorizontalLayoutGroup buttonRowLayout = buttonRow.AddComponent<HorizontalLayoutGroup>();
        buttonRowLayout.spacing = 12f;
        buttonRowLayout.childForceExpandWidth = true;
        buttonRowLayout.childForceExpandHeight = true;
        buttonRowLayout.childControlWidth = true;
        buttonRowLayout.childControlHeight = true;

        // 시듦 복구 터치(작업 지시 3.4) — 이 꽃이 배치된 타일을 "손질"한다. 골드는 일반 터치와 동일,
        // 유대는 절대 안 쌓인다(청소 행위). 라벨은 런타임에 GardenFlowerInfoPanel이 진행도로 갱신한다.
        Button tendButton = CreateSimpleTextButton(buttonRow.transform, "손질하기", font, new Color(0.3f, 0.45f, 0.3f, 1f));
        Button removeButton = CreateSimpleTextButton(buttonRow.transform, "제거", font, new Color(0.55f, 0.2f, 0.25f, 1f));
        Button closeButton = CreateSimpleTextButton(buttonRow.transform, "닫기", font, new Color(1f, 0.35f, 0.62f, 1f));

        GardenFlowerInfoPanel info = popupRoot.AddComponent<GardenFlowerInfoPanel>();
        info.root = popupRoot;
        info.contentText = contentText;
        info.tendButton = tendButton;
        info.tendButtonText = tendButton.GetComponentInChildren<TMP_Text>();
        if (info.tendButtonText != null)
        {
            // "손질 (0/3)" 같은 진행도 문구가 3분할된 좁은 버튼 폭을 넘어가 글씨가 밖으로 삐져나오던
            // 문제(신고됨) — 고정 폰트 크기 대신 자동 크기 조절을 켜서, 글자 수가 몇이든 버튼 안에
            // 항상 맞게 줄어들도록 한다. 한 줄 유지가 목적이라 줄바꿈은 끈다.
            info.tendButtonText.enableAutoSizing = true;
            info.tendButtonText.fontSizeMin = 10f;
            info.tendButtonText.fontSizeMax = 18f;
            info.tendButtonText.enableWordWrapping = false;
        }
        info.removeButton = removeButton;
        info.closeButton = closeButton;

        // popupRoot는 절대 SetActive(false)로 끄지 않는다 — GardenFlowerInfoPanel.Awake()가
        // CanvasGroup으로 숨김을 처리한다(OfflineSummaryPopup과 동일 원칙).
        return info;
    }

    /// <summary> 텍스트 하나만 있는 단순 버튼(배경색+라벨)을 만든다 — 정보 패널의 제거/닫기 버튼 공용. </summary>
    static Button CreateSimpleTextButton(Transform parent, string label, TMP_FontAsset font, Color bgColor)
    {
        GameObject go = new GameObject(label + "Button");
        go.transform.SetParent(parent, false);
        Image bg = go.AddComponent<Image>();
        bg.color = bgColor;
        Button button = go.AddComponent<Button>();
        button.targetGraphic = bg;

        GameObject labelGO = new GameObject("Label");
        labelGO.transform.SetParent(go.transform, false);
        SetupText(labelGO, label, font, 18f, TextAlignmentOptions.Center,
                  Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        return button;
    }

    const string GARDEN_CELL_PREFAB_PATH = "Assets/Project/Prefabs/GardenCell.prefab";
    const string GARDEN_ROSTER_ITEM_PREFAB_PATH = "Assets/Project/Prefabs/GardenRosterItem.prefab";

    static Button GetOrCreateGardenCellPrefab()
    {
        Button existing = AssetDatabase.LoadAssetAtPath<Button>(GARDEN_CELL_PREFAB_PATH);
        if (existing != null) return existing;

        TMP_FontAsset font = LoadNotoFont();

        GameObject temp = new GameObject("GardenCell");
        RectTransform rt = temp.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(80f, 80f); // 실제 크기는 GridLayoutGroup.cellSize가 정하지만(런타임에 덮어씀), 인스펙터 미리보기용으로 실제 값과 맞춰 둔다

        Image bg = temp.AddComponent<Image>();
        bg.color = new Color(1f, 1f, 1f, 0.08f);

        Button button = temp.AddComponent<Button>();
        button.targetGraphic = bg;

        GameObject labelGO = new GameObject("Label");
        labelGO.transform.SetParent(temp.transform, false);
        // 칸이 커지면서(80px) 이름+유대+배율+손질 진행도까지 최대 4줄이 들어갈 수 있어 11→12pt로 살짝 키웠다.
        SetupText(labelGO, "", font, 12f, TextAlignmentOptions.Center,
                  Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f));

        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(temp, GARDEN_CELL_PREFAB_PATH);
        Object.DestroyImmediate(temp);

        return savedPrefab != null ? savedPrefab.GetComponent<Button>() : null;
    }

    static Button GetOrCreateGardenRosterItemPrefab()
    {
        Button existing = AssetDatabase.LoadAssetAtPath<Button>(GARDEN_ROSTER_ITEM_PREFAB_PATH);
        if (existing != null) return existing;

        TMP_FontAsset font = LoadNotoFont();

        GameObject temp = new GameObject("GardenRosterItem");
        RectTransform rt = temp.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(90f, 120f);
        LayoutElement le = temp.AddComponent<LayoutElement>();
        le.preferredWidth = 90f;
        le.preferredHeight = 120f;

        Image bg = temp.AddComponent<Image>();
        bg.color = new Color(1f, 1f, 1f, 0.12f);

        Button button = temp.AddComponent<Button>();
        button.targetGraphic = bg;

        // 위쪽 45% — 이름 + 유대 레벨
        GameObject labelGO = new GameObject("Label");
        labelGO.transform.SetParent(temp.transform, false);
        SetupText(labelGO, "-", font, 13f, TextAlignmentOptions.Center,
                  new Vector2(0f, 0.55f), Vector2.one, new Vector2(4f, 2f), new Vector2(-4f, -4f));

        // 아래쪽 55% — 모양 미리보기(빈 컨테이너, 실제 칸은 GardenPanel이 꽃마다 런타임에 채운다:
        // 꽃마다 gardenShape 크기/모양이 달라 프리팹 하나로 고정해 둘 수 없기 때문).
        GameObject shapePreviewGO = new GameObject("ShapePreview");
        shapePreviewGO.transform.SetParent(temp.transform, false);
        RectTransform previewRT = shapePreviewGO.AddComponent<RectTransform>();
        previewRT.anchorMin = new Vector2(0f, 0f);
        previewRT.anchorMax = new Vector2(1f, 0.55f);
        previewRT.offsetMin = new Vector2(4f, 4f);
        previewRT.offsetMax = new Vector2(-4f, -2f);
        GridLayoutGroup previewGrid = shapePreviewGO.AddComponent<GridLayoutGroup>();
        previewGrid.cellSize = new Vector2(12f, 12f);
        previewGrid.spacing = new Vector2(2f, 2f);
        previewGrid.childAlignment = TextAnchor.MiddleCenter;
        previewGrid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        previewGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        previewGrid.constraintCount = 1; // 런타임에 GardenPanel.BuildShapePreview가 꽃 모양 너비로 덮어씀

        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(temp, GARDEN_ROSTER_ITEM_PREFAB_PATH);
        Object.DestroyImmediate(temp);

        return savedPrefab != null ? savedPrefab.GetComponent<Button>() : null;
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
                uiMgr.affectionBarButton = bar.GetComponent<Button>();
                uiMgr.unreadMemorialBadge = bar.Find("UnreadBadge")?.gameObject;
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
        cardRT.sizeDelta = new Vector2(460f, 490f); // 정원 요약(GardenSummaryText) 한 줄만큼 기존보다 키움

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

        // GardenSummaryText (정원 유대/시듦 요약 — 정원을 안 쓰면 텍스트가 비어 있어 사실상 안 보임)
        GameObject gardenGO = new GameObject("GardenSummarySection");
        gardenGO.transform.SetParent(card.transform, false);
        LayoutElement gardenLE = gardenGO.AddComponent<LayoutElement>();
        gardenLE.preferredHeight = 60f;
        SetupText(gardenGO, "", font, 15f, TextAlignmentOptions.Center,
                  Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        TMP_Text gardenText = gardenGO.GetComponent<TMP_Text>();
        gardenText.color = new Color(0.75f, 0.85f, 0.75f, 1f);

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
        popup.gardenSummaryText       = gardenText;
        popup.closeButton             = closeButton;

        // popupRoot는 절대 SetActive(false)로 끄지 않는다 — 비활성 오브젝트는 Awake/Start가 스킵되므로
        // Start()의 SaveManager 이벤트 구독이 영영 실행되지 않는다. 숨김은 OfflineSummaryPopup.Awake()가
        // CanvasGroup으로 처리한다(이 AddComponent 호출 시점에 에디터에서도 즉시 실행됨).
    }

    // ── FlowerDexPanel 생성 (전체 꽃 그리드 팝업 — 미보유 포함, 화면 전체를 꽉 채움) ──────
    // Canvas 최상위 오버레이로 둔다(사이드바 내부가 아님) — LeftSidebar/RightSidebar는
    // PCLayoutController.hideSidebarsOnMobile로 모바일에서 숨겨지므로, 사이드바 안에 두면
    // 모바일에서 도감 자체에 접근할 수 없게 된다.
    //
    // 모바일/PC 두 레이아웃을 전부 만들어 두고, 어느 쪽을 보여줄지는 런타임에
    // FlowerDexPanel.Awake()가 Application.isMobilePlatform으로 한 번만 고른다
    // (PCLayoutController가 사이드바를 켜고 끌 때 쓰는 것과 동일한 신호 — 프로젝트 전체가
    // PC/모바일을 구분하는 기준을 하나로 통일한다).
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
        dim.color = new Color(0f, 0f, 0f, 0.92f);

        // ── Header (제목 + 닫기, 모바일/PC 공용, 화면 최상단 전체 폭) ──
        GameObject header = new GameObject("Header");
        header.transform.SetParent(panelRoot.transform, false);
        RectTransform hrt = header.AddComponent<RectTransform>();
        hrt.anchorMin = new Vector2(0f, 1f);
        hrt.anchorMax = new Vector2(1f, 1f);
        hrt.pivot = new Vector2(0.5f, 1f);
        hrt.sizeDelta = new Vector2(0f, DEX_HEADER_HEIGHT);

        Image hImg = header.AddComponent<Image>();
        hImg.color = new Color(0.15f, 0.16f, 0.22f, 1f);

        GameObject title = new GameObject("Title");
        title.transform.SetParent(header.transform, false);
        SetupText(title, "꽃 도감", font, 28f, TextAlignmentOptions.Left,
                  Vector2.zero, Vector2.one, new Vector2(24f, 0f), new Vector2(-80f, 0f));

        GameObject closeGO = new GameObject("CloseButton");
        closeGO.transform.SetParent(header.transform, false);
        RectTransform closeRT = closeGO.AddComponent<RectTransform>();
        closeRT.anchorMin = new Vector2(1f, 0f);
        closeRT.anchorMax = new Vector2(1f, 1f);
        closeRT.pivot = new Vector2(1f, 0.5f);
        closeRT.sizeDelta = new Vector2(60f, 0f);
        closeRT.anchoredPosition = new Vector2(-10f, 0f);

        Image closeBg = closeGO.AddComponent<Image>();
        closeBg.color = new Color(1f, 0.35f, 0.62f, 1f);
        Button closeButton = closeGO.AddComponent<Button>();
        closeButton.targetGraphic = closeBg;

        GameObject closeLabelGO = new GameObject("Label");
        closeLabelGO.transform.SetParent(closeGO.transform, false);
        SetupText(closeLabelGO, "X", font, 22f, TextAlignmentOptions.Center,
                  Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // ── 본문 영역 (헤더 아래 전체) — 모바일/PC 레이아웃 루트 2개. 화면을 꽉 채우고,
        //    Awake() 시점에 플랫폼에 맞는 하나만 SetActive(true)로 남는다. ──
        GameObject mobileRoot = BuildDexLayoutRoot(panelRoot.transform, "MobileLayoutRoot");
        GameObject pcRoot = BuildDexLayoutRoot(panelRoot.transform, "PCLayoutRoot");

        // ── 모바일: 그리드(세로 스크롤) + 상세(아이콘 위 / 텍스트 아래, 세로 스크롤) ──
        RectTransform mobileGrid = BuildDexScrollGrid(mobileRoot.transform, new Vector2(170f, 170f));

        GameObject mobileDetail = BuildDexDetailRoot(mobileRoot.transform);

        GameObject mobileScroll = new GameObject("Scroll View");
        mobileScroll.transform.SetParent(mobileDetail.transform, false);
        RectTransform msRT = mobileScroll.AddComponent<RectTransform>();
        msRT.anchorMin = Vector2.zero;
        msRT.anchorMax = Vector2.one;
        msRT.offsetMin = Vector2.zero;
        msRT.offsetMax = new Vector2(0f, -76f);
        RectTransform mobileDetailContent = SetupVerticalScrollContent(mobileScroll, new RectOffset(20, 20, 20, 20));

        GameObject mobileIconGO = new GameObject("Icon");
        mobileIconGO.transform.SetParent(mobileDetailContent.transform, false);
        mobileIconGO.AddComponent<RectTransform>();
        LayoutElement mIconLE = mobileIconGO.AddComponent<LayoutElement>();
        mIconLE.preferredHeight = 320f;
        Image mobileDetailIconImg = mobileIconGO.AddComponent<Image>();
        mobileDetailIconImg.preserveAspect = true;

        TMP_Text mobileDetailNameText = CreateDexDetailText(mobileDetailContent.transform, font, 32f, TextAlignmentOptions.Center, true);
        TMP_Text mobileDetailDescText = CreateDexDetailText(mobileDetailContent.transform, font, 18f, TextAlignmentOptions.Center, false);
        (Image[] mobileGrowthIcons, Button[] mobileGrowthButtons) = BuildGrowthGallery(mobileDetailContent.transform);
        TMP_Text mobileDetailInfoText = CreateDexDetailText(mobileDetailContent.transform, font, 18f, TextAlignmentOptions.Left, false);
        TMP_Text mobileDetailPassiveText = CreateDexDetailText(mobileDetailContent.transform, font, 16f, TextAlignmentOptions.Left, false);

        (Button mobilePrev, Button mobileBack, Button mobileJump, Button mobileNext, Button mobileMemorial) = BuildDexDetailButtonBar(mobileDetail.transform, font);

        mobileDetail.SetActive(false);

        // ── PC: 그리드(다열, 넓게) + 상세(좌: 스탠딩 이미지 / 우: 데이터·소개말 — 마케팅용 캐릭터 소개 화면 형태) ──
        RectTransform pcGrid = BuildDexScrollGrid(pcRoot.transform, new Vector2(200f, 200f));

        GameObject pcDetail = BuildDexDetailRoot(pcRoot.transform);

        GameObject pcLeftPane = new GameObject("LeftPane");
        pcLeftPane.transform.SetParent(pcDetail.transform, false);
        RectTransform plRT = pcLeftPane.AddComponent<RectTransform>();
        plRT.anchorMin = new Vector2(0f, 0f);
        plRT.anchorMax = new Vector2(0.45f, 1f);
        plRT.offsetMin = Vector2.zero;
        plRT.offsetMax = Vector2.zero;

        // ── IconScrollView: 드래그로 패닝, 휠/버튼으로 확대·축소. ScrollRect의 scrollSensitivity를
        // 0으로 꺼서 휠의 "자체 패닝" 반응을 죽이고, 대신 ScrollWheelZoomHandler가 같은 휠 이벤트를
        // 받아 FlowerDexPanel의 확대/축소로 재활용한다(드래그 패닝 자체는 ScrollRect 그대로 동작).
        // 아이콘의 Image.preserveAspect는 FlowerDexPanel이 sizeDelta로 직접 확대율을 계산하는 로직과
        // 겹치므로 꺼둔다.
        GameObject iconScrollGO = new GameObject("IconScrollView");
        iconScrollGO.transform.SetParent(pcLeftPane.transform, false);
        RectTransform isRT = iconScrollGO.AddComponent<RectTransform>();
        isRT.anchorMin = new Vector2(0.04f, 0.28f);
        isRT.anchorMax = new Vector2(0.96f, 0.95f);
        isRT.offsetMin = Vector2.zero;
        isRT.offsetMax = Vector2.zero;

        ScrollRect iconSR = iconScrollGO.AddComponent<ScrollRect>();
        iconSR.horizontal = true;
        iconSR.vertical = true;
        iconSR.scrollSensitivity = 0f;

        GameObject iconViewportGO = new GameObject("Viewport");
        iconViewportGO.transform.SetParent(iconScrollGO.transform, false);
        RectTransform ivRT = iconViewportGO.AddComponent<RectTransform>();
        ivRT.anchorMin = Vector2.zero;
        ivRT.anchorMax = Vector2.one;
        ivRT.offsetMin = Vector2.zero;
        ivRT.offsetMax = Vector2.zero;
        Image ivImg = iconViewportGO.AddComponent<Image>();
        ivImg.color = new Color(0f, 0f, 0f, 0.15f); // 확대했을 때 이미지 바깥 경계를 눈으로 알 수 있게
        Mask ivMask = iconViewportGO.AddComponent<Mask>();
        ivMask.showMaskGraphic = true;
        ScrollWheelZoomHandler iconZoomHandler = iconViewportGO.AddComponent<ScrollWheelZoomHandler>();

        GameObject iconContentGO = new GameObject("Content");
        iconContentGO.transform.SetParent(iconViewportGO.transform, false);
        RectTransform icRT = iconContentGO.AddComponent<RectTransform>();
        icRT.anchorMin = new Vector2(0.5f, 0.5f);
        icRT.anchorMax = new Vector2(0.5f, 0.5f);
        icRT.pivot = new Vector2(0.5f, 0.5f);
        icRT.anchoredPosition = Vector2.zero;
        icRT.sizeDelta = Vector2.zero; // 런타임에 FlowerDexPanel이 확대율에 맞춰 갱신

        GameObject pcIconGO = new GameObject("StandingIcon");
        pcIconGO.transform.SetParent(iconContentGO.transform, false);
        RectTransform piRT = pcIconGO.AddComponent<RectTransform>();
        piRT.anchorMin = new Vector2(0.5f, 0.5f);
        piRT.anchorMax = new Vector2(0.5f, 0.5f);
        piRT.pivot = new Vector2(0.5f, 0.5f);
        piRT.anchoredPosition = Vector2.zero;
        Image pcDetailIconImg = pcIconGO.AddComponent<Image>();
        pcDetailIconImg.preserveAspect = false;

        iconSR.viewport = ivRT;
        iconSR.content = icRT;

        // ── 확대/축소 컨트롤 바 ("-" / 직접 입력(%) / "100%"(PPU 기준 실제 크기로 점프) / "+") ──
        GameObject zoomBarGO = new GameObject("ZoomControlBar");
        zoomBarGO.transform.SetParent(pcLeftPane.transform, false);
        RectTransform zbRT = zoomBarGO.AddComponent<RectTransform>();
        zbRT.anchorMin = new Vector2(0f, 0.18f);
        zbRT.anchorMax = new Vector2(1f, 0.28f);
        zbRT.offsetMin = Vector2.zero;
        zbRT.offsetMax = Vector2.zero;

        HorizontalLayoutGroup zoomLayout = zoomBarGO.AddComponent<HorizontalLayoutGroup>();
        zoomLayout.childForceExpandWidth = true;
        zoomLayout.childForceExpandHeight = true;
        zoomLayout.childControlWidth = true;
        zoomLayout.childControlHeight = true;
        zoomLayout.spacing = 8f;
        zoomLayout.padding = new RectOffset(8, 8, 4, 4);

        Button pcZoomOutButton = CreateTabButton(zoomBarGO.transform, "ZoomOutButton", "-", font);

        // 원하는 배율을 직접 숫자로 입력할 수 있는 필드 — +/-/휠은 25%씩만 계단식으로 움직여서
        // 정확한 값(예: 정확히 137%)을 맞추기 어렵다는 문제를 해결한다. Enter/포커스 아웃 시 적용.
        TMP_InputField pcZoomInputField = CreateNumericInputField(zoomBarGO.transform, "ZoomInputField", "%", font);
        LayoutElement zoomInputLE = pcZoomInputField.gameObject.AddComponent<LayoutElement>();
        zoomInputLE.preferredWidth = 64f;

        Button pcZoomResetButton = CreateTabButton(zoomBarGO.transform, "ZoomResetButton", "100%", font);
        Button pcZoomInButton = CreateTabButton(zoomBarGO.transform, "ZoomInButton", "+", font);

        // ── PPU 저장 버튼(에디터 전용 개발자 도구) ──────────────────────────────
        // 위 확대/축소로 화면상 크기를 원하는 만큼 맞춘 뒤 이 버튼을 누르면, 그 배율을 스프라이트의
        // 실제 Pixels Per Unit 값으로 역산해서 .meta 에셋에 영구 저장한다(FlowerDexPanel의
        // SaveCurrentZoomAsAssetPpu, #if UNITY_EDITOR 안에서만 컴파일됨). 실제 빌드에는 이 로직 자체가
        // 아예 포함되지 않으므로, FlowerDexPanel.Start()가 빌드에서는 이 버튼을 자동으로 숨긴다 —
        // 플레이어가 접근할 방법이 원천적으로 없다.
        GameObject ppuSaveGO = new GameObject("PpuSaveButton");
        ppuSaveGO.transform.SetParent(pcLeftPane.transform, false);
        RectTransform ppuSaveRT = ppuSaveGO.AddComponent<RectTransform>();
        ppuSaveRT.anchorMin = new Vector2(0.15f, 0.1f);
        ppuSaveRT.anchorMax = new Vector2(0.85f, 0.18f);
        ppuSaveRT.offsetMin = Vector2.zero;
        ppuSaveRT.offsetMax = Vector2.zero;

        Image ppuSaveBg = ppuSaveGO.AddComponent<Image>();
        ppuSaveBg.color = new Color(0.55f, 0.3f, 0.15f, 1f); // 경고성 주황빛 — 확대/축소 버튼과 구분되는 "영구 반영" 액션
        Button pcPpuSaveButton = ppuSaveGO.AddComponent<Button>();
        pcPpuSaveButton.targetGraphic = ppuSaveBg;

        GameObject ppuSaveLabelGO = new GameObject("Label");
        ppuSaveLabelGO.transform.SetParent(ppuSaveGO.transform, false);
        SetupText(ppuSaveLabelGO, "이 크기로 에셋에 저장 (에디터 전용)", font, 14f, TextAlignmentOptions.Center,
                  Vector2.zero, Vector2.one, new Vector2(4f, 0f), new Vector2(-4f, 0f));

        // 이미지 크기 + 확대율 안내 텍스트 (항상 표시 — 에셋 원본 해상도 확인용)
        GameObject iconSizeGO = new GameObject("IconSizeText");
        iconSizeGO.transform.SetParent(pcLeftPane.transform, false);
        SetupText(iconSizeGO, "-", font, 14f, TextAlignmentOptions.Center,
                  new Vector2(0f, 0f), new Vector2(1f, 0.1f), new Vector2(8f, 0f), new Vector2(-8f, 0f));
        TMP_Text pcIconSizeText = iconSizeGO.GetComponent<TMP_Text>();
        pcIconSizeText.color = new Color(0.8f, 0.78f, 0.8f, 1f);

        GameObject pcRightPane = new GameObject("RightPane");
        pcRightPane.transform.SetParent(pcDetail.transform, false);
        RectTransform prpRT = pcRightPane.AddComponent<RectTransform>();
        prpRT.anchorMin = new Vector2(0.45f, 0f);
        prpRT.anchorMax = new Vector2(1f, 1f);
        prpRT.offsetMin = Vector2.zero;
        prpRT.offsetMax = Vector2.zero;

        Image pcRightBg = pcRightPane.AddComponent<Image>();
        pcRightBg.color = new Color(0f, 0f, 0f, 0.2f);

        GameObject pcScroll = new GameObject("Scroll View");
        pcScroll.transform.SetParent(pcRightPane.transform, false);
        RectTransform psRT = pcScroll.AddComponent<RectTransform>();
        psRT.anchorMin = Vector2.zero;
        psRT.anchorMax = Vector2.one;
        psRT.offsetMin = Vector2.zero;
        psRT.offsetMax = new Vector2(0f, -84f);
        RectTransform pcDetailContent = SetupVerticalScrollContent(pcScroll, new RectOffset(28, 28, 28, 20));

        TMP_Text pcDetailNameText = CreateDexDetailText(pcDetailContent.transform, font, 36f, TextAlignmentOptions.Left, true);
        TMP_Text pcDetailDescText = CreateDexDetailText(pcDetailContent.transform, font, 19f, TextAlignmentOptions.Left, false);
        (Image[] pcGrowthIcons, Button[] pcGrowthButtons) = BuildGrowthGallery(pcDetailContent.transform);
        TMP_Text pcDetailInfoText = CreateDexDetailText(pcDetailContent.transform, font, 19f, TextAlignmentOptions.Left, false);
        TMP_Text pcDetailPassiveText = CreateDexDetailText(pcDetailContent.transform, font, 17f, TextAlignmentOptions.Left, false);

        (Button pcPrev, Button pcBack, Button pcJump, Button pcNext, Button pcMemorial) = BuildDexDetailButtonBar(pcRightPane.transform, font);

        pcDetail.SetActive(false);

        // ── FlowerDexPanel 컴포넌트 연결 ──
        // closeButton도 openButton과 동일한 이유로 필드만 연결한다(onClick.AddListener 직접 호출 금지 —
        // 에디터 스크립트에서 붙인 리스너는 씬에 저장되지 않는다). 실제 연결은 FlowerDexPanel.Start()가 한다.
        FlowerDexPanel dexPanel = panelRoot.AddComponent<FlowerDexPanel>();
        dexPanel.root = panelRoot;
        dexPanel.itemPrefab = GetOrCreateFlowerDexItemPrefab();
        dexPanel.closeButton = closeButton;

        dexPanel.mobileLayoutRoot = mobileRoot;
        dexPanel.mobileGridContent = mobileGrid;
        dexPanel.mobileDetailRoot = mobileDetail;
        dexPanel.mobileDetailIcon = mobileDetailIconImg;
        dexPanel.mobileDetailName = mobileDetailNameText;
        dexPanel.mobileDetailDescription = mobileDetailDescText;
        dexPanel.mobileGrowthStageIcons = mobileGrowthIcons;
        dexPanel.mobileGrowthStageButtons = mobileGrowthButtons;
        dexPanel.mobileDetailInfo = mobileDetailInfoText;
        dexPanel.mobileDetailPassives = mobileDetailPassiveText;
        dexPanel.mobileJumpButton = mobileJump;
        dexPanel.mobileBackButton = mobileBack;
        dexPanel.mobilePrevButton = mobilePrev;
        dexPanel.mobileNextButton = mobileNext;
        dexPanel.mobileMemorialButton = mobileMemorial;

        dexPanel.pcLayoutRoot = pcRoot;
        dexPanel.pcGridContent = pcGrid;
        dexPanel.pcDetailRoot = pcDetail;
        dexPanel.pcDetailIcon = pcDetailIconImg;
        dexPanel.pcDetailName = pcDetailNameText;
        dexPanel.pcDetailDescription = pcDetailDescText;
        dexPanel.pcGrowthStageIcons = pcGrowthIcons;
        dexPanel.pcGrowthStageButtons = pcGrowthButtons;
        dexPanel.pcDetailInfo = pcDetailInfoText;
        dexPanel.pcDetailPassives = pcDetailPassiveText;
        dexPanel.pcJumpButton = pcJump;
        dexPanel.pcBackButton = pcBack;
        dexPanel.pcPrevButton = pcPrev;
        dexPanel.pcNextButton = pcNext;
        dexPanel.pcMemorialButton = pcMemorial;
        dexPanel.pcDetailIconContent = icRT;
        dexPanel.pcDetailIconViewport = ivRT;
        dexPanel.pcDetailIconSizeText = pcIconSizeText;
        dexPanel.pcZoomInButton = pcZoomInButton;
        dexPanel.pcZoomOutButton = pcZoomOutButton;
        dexPanel.pcZoomResetButton = pcZoomResetButton;
        dexPanel.pcZoomInputField = pcZoomInputField;
        dexPanel.pcIconScrollZoomHandler = iconZoomHandler;
        dexPanel.pcPpuSaveButton = pcPpuSaveButton;

        // panelRoot는 절대 SetActive(false)로 끄지 않는다 — 비활성 오브젝트는 Awake/Start가 스킵되므로
        // Start()의 openButton.onClick.AddListener가 영영 실행되지 않는다("버튼은 눌리는데 반응 없음" 버그의
        // 원인). 숨김은 FlowerDexPanel.Awake()가 CanvasGroup으로 처리한다.
        return dexPanel;
    }

    /// <summary> 헤더 아래 전체 영역을 채우는 레이아웃 루트(모바일/PC 공용 뼈대) 하나를 만든다. </summary>
    static GameObject BuildDexLayoutRoot(Transform parent, string name)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(parent, false);
        RectTransform rt = root.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = new Vector2(0f, -DEX_HEADER_HEIGHT);

        Image bg = root.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.08f, 0.11f, 0.98f);

        return root;
    }

    /// <summary> 그리드/상세 두 상태를 담을 부모 안에, 화면 전체를 덮는 "DetailView" 오버레이 루트를 만든다. </summary>
    static GameObject BuildDexDetailRoot(Transform parent)
    {
        GameObject detail = new GameObject("DetailView");
        detail.transform.SetParent(parent, false);
        RectTransform rt = detail.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image bg = detail.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.08f, 0.11f, 1f);

        return detail;
    }

    /// <summary> 도감 그리드(스크롤+GridLayoutGroup)를 parent에 꽉 채워 만들고 Content RectTransform을 반환한다. </summary>
    static RectTransform BuildDexScrollGrid(Transform parent, Vector2 cellSize)
    {
        GameObject scrollGO = new GameObject("GridView");
        scrollGO.transform.SetParent(parent, false);
        RectTransform srt = scrollGO.AddComponent<RectTransform>();
        srt.anchorMin = Vector2.zero;
        srt.anchorMax = Vector2.one;
        srt.offsetMin = Vector2.zero;
        srt.offsetMax = Vector2.zero;

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
        grid.cellSize = cellSize;
        grid.spacing = new Vector2(14f, 14f);
        grid.padding = new RectOffset(20, 20, 20, 20);
        grid.childAlignment = TextAnchor.UpperLeft;

        ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        sr.viewport = vprt;
        sr.content = crt;

        return crt;
    }

    /// <summary>
    /// 이미 RectTransform이 배치된 scrollRoot 위에 세로 스크롤 텍스트 영역
    /// (Viewport + Content(VerticalLayoutGroup+ContentSizeFitter))을 구성하고 Content를 반환한다.
    /// 상세 화면(이름/소개/정보/패시브)처럼 내용 길이가 꽃마다 달라지는 곳에 쓴다.
    /// </summary>
    static RectTransform SetupVerticalScrollContent(GameObject scrollRoot, RectOffset padding)
    {
        Image scrollImg = scrollRoot.AddComponent<Image>();
        scrollImg.color = new Color(0f, 0f, 0f, 0f);

        ScrollRect sr = scrollRoot.AddComponent<ScrollRect>();
        sr.horizontal = false;
        sr.vertical = true;
        sr.scrollSensitivity = 35f;

        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollRoot.transform, false);
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

        VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.spacing = 14f;
        vlg.padding = padding;

        ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        sr.viewport = vprt;
        sr.content = crt;

        return crt;
    }

    /// <summary>
    /// "역대 성장 과정" 갤러리(씨앗→발아→성장→개화 4칸)를 세로 스크롤 콘텐츠 안에 가로로 만들고,
    /// 4개의 Image + 4개의 Button을 반환한다. 각 칸을 클릭하면 FlowerDexPanel이 좌측(모바일은 상단)
    /// 대표 이미지를 그 단계로 바꾼다. 실제 스프라이트/틴트 채우기는 FlowerDexPanel.UpdateGrowthGallery/
    /// SelectGrowthStage가 인덱스 기반으로 담당한다 — 나중에 칸 수를 바꾸려면 이 4를 바꾸고 그쪽
    /// stageSprites 배열만 맞춰주면 된다(필드 하나하나를 새로 연결할 필요 없음).
    /// </summary>
    static (Image[] icons, Button[] buttons) BuildGrowthGallery(Transform parent)
    {
        GameObject row = new GameObject("GrowthGallery");
        row.transform.SetParent(parent, false);
        LayoutElement le = row.AddComponent<LayoutElement>();
        le.preferredHeight = 90f;

        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.spacing = 10f;

        const int STAGE_COUNT = 4; // 씨앗/발아/성장/개화
        Image[] icons = new Image[STAGE_COUNT];
        Button[] buttons = new Button[STAGE_COUNT];
        for (int i = 0; i < STAGE_COUNT; i++)
        {
            GameObject iconGO = new GameObject($"Stage{i}");
            iconGO.transform.SetParent(row.transform, false);
            iconGO.AddComponent<RectTransform>();
            Image img = iconGO.AddComponent<Image>();
            img.preserveAspect = true;
            Button btn = iconGO.AddComponent<Button>();
            btn.targetGraphic = img;
            icons[i] = img;
            buttons[i] = btn;
        }

        return (icons, buttons);
    }

    /// <summary> 상세 화면 텍스트 한 줄(이름/소개/정보/패시브 공용)을 세로 스크롤 콘텐츠 안에 만든다. </summary>
    static TMP_Text CreateDexDetailText(Transform parent, TMP_FontAsset font, float fontSize, TextAlignmentOptions align, bool bold)
    {
        GameObject go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        SetupText(go, "-", font, fontSize, align, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        TMP_Text tmp = go.GetComponent<TMP_Text>();
        if (bold) tmp.fontStyle = FontStyles.Bold;
        tmp.color = new Color(0.92f, 0.9f, 0.92f, 1f);
        return tmp;
    }

    /// <summary> 상세 화면 하단 버튼 바("이전/다음" / "메모리얼" / "목록으로/이 꽃으로 이동")를 parent 하단에 만든다. </summary>
    static (Button prev, Button back, Button jump, Button next, Button memorial) BuildDexDetailButtonBar(Transform parent, TMP_FontAsset font)
    {
        GameObject bar = new GameObject("ButtonBar");
        bar.transform.SetParent(parent, false);
        RectTransform barRT = bar.AddComponent<RectTransform>();
        barRT.anchorMin = new Vector2(0f, 0f);
        barRT.anchorMax = new Vector2(1f, 0f);
        barRT.pivot = new Vector2(0.5f, 0f);
        barRT.sizeDelta = new Vector2(0f, 180f); // 행 3개(이전/다음, 메모리얼, 목록으로/이동) — 기존 124에서 56 늘림

        VerticalLayoutGroup barLayout = bar.AddComponent<VerticalLayoutGroup>();
        barLayout.childForceExpandWidth = true;
        barLayout.childForceExpandHeight = true;
        barLayout.childControlWidth = true;
        barLayout.childControlHeight = true;
        barLayout.spacing = 8f;
        barLayout.padding = new RectOffset(16, 16, 8, 12);

        // 1행: 이전/다음 — 목록으로 안 돌아가고 도감 순서(FlowerManager.allFlowers)대로 옆 꽃 상세로 바로 이동.
        GameObject navRow = new GameObject("NavRow");
        navRow.transform.SetParent(bar.transform, false);
        navRow.AddComponent<RectTransform>();
        LayoutElement navRowLE = navRow.AddComponent<LayoutElement>();
        navRowLE.preferredHeight = 48f;

        HorizontalLayoutGroup navLayout = navRow.AddComponent<HorizontalLayoutGroup>();
        navLayout.childForceExpandWidth = true;
        navLayout.childForceExpandHeight = true;
        navLayout.childControlWidth = true;
        navLayout.childControlHeight = true;
        navLayout.spacing = 12f;

        Button prevButton = CreateTabButton(navRow.transform, "PrevButton", "이전", font);
        Button nextButton = CreateTabButton(navRow.transform, "NextButton", "다음", font);

        // 2행: 메모리얼 보기 — 유대로 해금한 회상을 본다. 미개화 꽃은 FlowerDexPanel.ShowDetail이
        // interactable=false로 잠가둔다(유대 자체가 개화한 꽃에만 쌓이므로).
        GameObject memorialGO = new GameObject("MemorialButton");
        memorialGO.transform.SetParent(bar.transform, false);
        LayoutElement memorialLE = memorialGO.AddComponent<LayoutElement>();
        memorialLE.preferredHeight = 48f;
        Image memorialBg = memorialGO.AddComponent<Image>();
        memorialBg.color = new Color(1f, 0.85f, 0.3f, 1f); // 골드빛 — 레벨업 팝업/뱃지와 동일 강조색
        Button memorialButton = memorialGO.AddComponent<Button>();
        memorialButton.targetGraphic = memorialBg;
        GameObject memorialLabelGO = new GameObject("Label");
        memorialLabelGO.transform.SetParent(memorialGO.transform, false);
        SetupText(memorialLabelGO, "메모리얼 보기", font, 18f, TextAlignmentOptions.Center,
                  Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        memorialLabelGO.GetComponent<TMP_Text>().color = new Color(0.15f, 0.13f, 0.05f, 1f); // 밝은 배경 위 어두운 글자

        // 3행: 목록으로 / 이 꽃으로 이동 (기존과 동일)
        GameObject actionRow = new GameObject("ActionRow");
        actionRow.transform.SetParent(bar.transform, false);
        actionRow.AddComponent<RectTransform>();
        LayoutElement actionRowLE = actionRow.AddComponent<LayoutElement>();
        actionRowLE.preferredHeight = 48f;

        HorizontalLayoutGroup actionLayout = actionRow.AddComponent<HorizontalLayoutGroup>();
        actionLayout.childForceExpandWidth = true;
        actionLayout.childForceExpandHeight = true;
        actionLayout.childControlWidth = true;
        actionLayout.childControlHeight = true;
        actionLayout.spacing = 12f;

        GameObject backGO = new GameObject("BackButton");
        backGO.transform.SetParent(actionRow.transform, false);
        backGO.AddComponent<RectTransform>();
        Image backBg = backGO.AddComponent<Image>();
        backBg.color = new Color(0.3f, 0.3f, 0.35f, 1f);
        Button backButton = backGO.AddComponent<Button>();
        backButton.targetGraphic = backBg;
        GameObject backLabelGO = new GameObject("Label");
        backLabelGO.transform.SetParent(backGO.transform, false);
        SetupText(backLabelGO, "목록으로", font, 18f, TextAlignmentOptions.Center,
                  Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        GameObject jumpGO = new GameObject("JumpButton");
        jumpGO.transform.SetParent(actionRow.transform, false);
        jumpGO.AddComponent<RectTransform>();
        Image jumpBg = jumpGO.AddComponent<Image>();
        jumpBg.color = new Color(1f, 0.35f, 0.62f, 1f);
        Button jumpButton = jumpGO.AddComponent<Button>();
        jumpButton.targetGraphic = jumpBg;
        GameObject jumpLabelGO = new GameObject("Label");
        jumpLabelGO.transform.SetParent(jumpGO.transform, false);
        SetupText(jumpLabelGO, "이 꽃으로 이동", font, 18f, TextAlignmentOptions.Center,
                  Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        return (prevButton, backButton, jumpButton, nextButton, memorialButton);
    }

    // ── MemorialViewPanel 생성 (꽃 1개의 유대 메모리얼 목록/본문 — 도감과 별개의 화면 전체 오버레이) ──
    const string MEMORIAL_ENTRY_ITEM_PREFAB_PATH = "Assets/Project/Prefabs/MemorialEntryItem.prefab";

    static void BuildMemorialViewPanel(Transform canvasTransform)
    {
        Transform existing = canvasTransform.Find("MemorialViewPanel");
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        TMP_FontAsset font = LoadNotoFont();

        GameObject panelRoot = new GameObject("MemorialViewPanel");
        panelRoot.transform.SetParent(canvasTransform, false);
        RectTransform rootRT = panelRoot.AddComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;

        Image dim = panelRoot.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.92f);

        // ── Header (도감과 동일한 구조 — 제목 + 닫기) ──
        GameObject header = new GameObject("Header");
        header.transform.SetParent(panelRoot.transform, false);
        RectTransform hrt = header.AddComponent<RectTransform>();
        hrt.anchorMin = new Vector2(0f, 1f);
        hrt.anchorMax = new Vector2(1f, 1f);
        hrt.pivot = new Vector2(0.5f, 1f);
        hrt.sizeDelta = new Vector2(0f, DEX_HEADER_HEIGHT);

        Image hImg = header.AddComponent<Image>();
        hImg.color = new Color(0.15f, 0.16f, 0.22f, 1f);

        GameObject title = new GameObject("Title");
        title.transform.SetParent(header.transform, false);
        SetupText(title, "메모리얼", font, 28f, TextAlignmentOptions.Left,
                  Vector2.zero, Vector2.one, new Vector2(24f, 0f), new Vector2(-80f, 0f));

        GameObject closeGO = new GameObject("CloseButton");
        closeGO.transform.SetParent(header.transform, false);
        RectTransform closeRT = closeGO.AddComponent<RectTransform>();
        closeRT.anchorMin = new Vector2(1f, 0f);
        closeRT.anchorMax = new Vector2(1f, 1f);
        closeRT.pivot = new Vector2(1f, 0.5f);
        closeRT.sizeDelta = new Vector2(60f, 0f);
        closeRT.anchoredPosition = new Vector2(-10f, 0f);
        Image closeBg = closeGO.AddComponent<Image>();
        closeBg.color = new Color(1f, 0.35f, 0.62f, 1f);
        Button closeButton = closeGO.AddComponent<Button>();
        closeButton.targetGraphic = closeBg;
        GameObject closeLabelGO = new GameObject("Label");
        closeLabelGO.transform.SetParent(closeGO.transform, false);
        SetupText(closeLabelGO, "X", font, 22f, TextAlignmentOptions.Center,
                  Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // ── BodyRoot (헤더 아래 전체) — ListRoot/DetailRoot 두 상태가 겹쳐서 들어간다 ──
        GameObject bodyRoot = new GameObject("BodyRoot");
        bodyRoot.transform.SetParent(panelRoot.transform, false);
        RectTransform bodyRT = bodyRoot.AddComponent<RectTransform>();
        bodyRT.anchorMin = Vector2.zero;
        bodyRT.anchorMax = Vector2.one;
        bodyRT.offsetMin = Vector2.zero;
        bodyRT.offsetMax = new Vector2(0f, -DEX_HEADER_HEIGHT);

        // ── ListRoot: 세로 스크롤 목록 (해금 항목은 클릭 가능, 잠긴 항목은 제목까지 가려진 채로) ──
        GameObject listRoot = CreateStretchChild(bodyRoot.transform, "ListRoot");

        GameObject listScroll = new GameObject("Scroll View");
        listScroll.transform.SetParent(listRoot.transform, false);
        RectTransform lsRT = listScroll.AddComponent<RectTransform>();
        lsRT.anchorMin = Vector2.zero;
        lsRT.anchorMax = Vector2.one;
        lsRT.offsetMin = Vector2.zero;
        lsRT.offsetMax = Vector2.zero;
        RectTransform listContent = SetupVerticalScrollContent(listScroll, new RectOffset(20, 20, 20, 20));

        GameObject emptyGO = new GameObject("EmptyStateText");
        emptyGO.transform.SetParent(listRoot.transform, false);
        SetupText(emptyGO, "아직 메모리얼이 없습니다.", font, 18f, TextAlignmentOptions.Center,
                  Vector2.zero, Vector2.one, new Vector2(20f, 0f), new Vector2(-20f, 0f));
        emptyGO.GetComponent<TMP_Text>().color = new Color(0.85f, 0.8f, 0.85f, 1f);
        emptyGO.SetActive(false);

        // ── DetailRoot: 본문 화면 (제목 + 스크롤 가능한 본문 + 하단 뒤로가기) ──
        GameObject detailRoot = CreateStretchChild(bodyRoot.transform, "DetailRoot");
        Image detailBg = detailRoot.AddComponent<Image>();
        detailBg.color = new Color(0.1f, 0.08f, 0.11f, 1f);

        GameObject detailScroll = new GameObject("Scroll View");
        detailScroll.transform.SetParent(detailRoot.transform, false);
        RectTransform dsRT = detailScroll.AddComponent<RectTransform>();
        dsRT.anchorMin = Vector2.zero;
        dsRT.anchorMax = Vector2.one;
        dsRT.offsetMin = new Vector2(0f, 76f); // 하단 "목록으로" 버튼 자리
        dsRT.offsetMax = Vector2.zero;
        RectTransform detailContent = SetupVerticalScrollContent(detailScroll, new RectOffset(20, 20, 20, 20));

        TMP_Text detailTitleText = CreateDexDetailText(detailContent.transform, font, 26f, TextAlignmentOptions.Left, true);
        TMP_Text detailBodyText = CreateDexDetailText(detailContent.transform, font, 18f, TextAlignmentOptions.Left, false);

        GameObject backGO = new GameObject("BackButton");
        backGO.transform.SetParent(detailRoot.transform, false);
        RectTransform backRT = backGO.AddComponent<RectTransform>();
        backRT.anchorMin = new Vector2(0f, 0f);
        backRT.anchorMax = new Vector2(1f, 0f);
        backRT.pivot = new Vector2(0.5f, 0f);
        backRT.sizeDelta = new Vector2(0f, 64f);
        backRT.anchoredPosition = new Vector2(0f, 8f);
        Image backBg = backGO.AddComponent<Image>();
        backBg.color = new Color(0.3f, 0.3f, 0.35f, 1f);
        Button backButton = backGO.AddComponent<Button>();
        backButton.targetGraphic = backBg;
        GameObject backLabelGO = new GameObject("Label");
        backLabelGO.transform.SetParent(backGO.transform, false);
        SetupText(backLabelGO, "목록으로", font, 18f, TextAlignmentOptions.Center,
                  Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        detailRoot.SetActive(false);

        // ── MemorialViewPanel 컴포넌트 연결 ──
        MemorialEntryItem itemPrefab = GetOrCreateMemorialEntryItemPrefab();

        MemorialViewPanel panel = panelRoot.AddComponent<MemorialViewPanel>();
        panel.root = panelRoot;
        panel.closeButton = closeButton;
        panel.flowerNameText = title.GetComponent<TMP_Text>();
        panel.listRoot = listRoot;
        panel.listContent = listContent;
        panel.itemPrefab = itemPrefab;
        panel.emptyStateText = emptyGO.GetComponent<TMP_Text>();
        panel.detailRoot = detailRoot;
        panel.detailTitleText = detailTitleText;
        panel.detailBodyText = detailBodyText;
        panel.detailBackButton = backButton;
    }

    /// <summary> 메모리얼 목록 행 프리팹을 로드하거나, 없으면 최초 1회 생성한다(FlowerDexItem과 동일 원칙). </summary>
    static MemorialEntryItem GetOrCreateMemorialEntryItemPrefab()
    {
        MemorialEntryItem existing = AssetDatabase.LoadAssetAtPath<MemorialEntryItem>(MEMORIAL_ENTRY_ITEM_PREFAB_PATH);
        if (existing != null) return existing;

        TMP_FontAsset font = LoadNotoFont();

        GameObject temp = new GameObject("MemorialEntryItem");
        temp.AddComponent<RectTransform>();
        LayoutElement le = temp.AddComponent<LayoutElement>();
        le.preferredHeight = 64f;

        Image bg = temp.AddComponent<Image>();
        bg.color = new Color(1f, 1f, 1f, 0.06f);

        Button selectButton = temp.AddComponent<Button>();
        selectButton.targetGraphic = bg;

        GameObject titleGO = new GameObject("TitleText");
        titleGO.transform.SetParent(temp.transform, false);
        SetupText(titleGO, "-", font, 18f, TextAlignmentOptions.Left,
                  Vector2.zero, Vector2.one, new Vector2(16f, 0f), new Vector2(-70f, 0f));

        GameObject badgeGO = new GameObject("NewBadgeText");
        badgeGO.transform.SetParent(temp.transform, false);
        SetupText(badgeGO, "NEW", font, 14f, TextAlignmentOptions.Center,
                  new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-64f, 8f), new Vector2(-12f, -8f));
        TMP_Text badgeTmp = badgeGO.GetComponent<TMP_Text>();
        badgeTmp.color = new Color(1f, 0.85f, 0.3f, 1f);
        badgeTmp.fontStyle = FontStyles.Bold;

        MemorialEntryItem item = temp.AddComponent<MemorialEntryItem>();
        item.titleText = titleGO.GetComponent<TMP_Text>();
        item.newBadgeText = badgeTmp;
        item.selectButton = selectButton;

        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(temp, MEMORIAL_ENTRY_ITEM_PREFAB_PATH);
        Object.DestroyImmediate(temp);

        return savedPrefab != null ? savedPrefab.GetComponent<MemorialEntryItem>() : null;
    }

    /// <summary>
    /// 메모리얼 재생기(VN) 화면. MemorialViewPanel이 lines(태그 대본)가 있는 항목을 열 때 이 패널을
    /// 재생한다. 배경/CG/Center 캐릭터가 아래에서 위로 쌓이고, 그 위에 전체화면 투명 탭 버튼(화면
    /// 아무데나 탭하면 다음 줄로 진행)을 깔고, 다시 그 위에 대사창·상단 버튼바(로그/스킵/닫기)를
    /// 얹는다 — 대사창 텍스트와 상단 버튼들은 raycastTarget을 각각 false/true로 둬서, 화면 탭은
    /// 투명 버튼이 받고 버튼 클릭은 버튼 자신이 우선 가로채도록 한다(형제 순서상 나중에 추가된
    /// 쪽이 위에 그려지고 레이캐스트도 먼저 받는다는 uGUI 규칙을 그대로 이용).
    /// 로그 패널은 맨 나중에 추가해 항상 최상단에서 전체를 덮는다.
    /// </summary>
    static void BuildMemorialPlayerPanel(Transform canvasTransform)
    {
        Transform existing = canvasTransform.Find("MemorialPlayerPanel");
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        TMP_FontAsset font = LoadNotoFont();

        GameObject panelRoot = new GameObject("MemorialPlayerPanel");
        panelRoot.transform.SetParent(canvasTransform, false);
        RectTransform rootRT = panelRoot.AddComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;

        Image rootBg = panelRoot.AddComponent<Image>();
        rootBg.color = Color.black; // 배경 스프라이트가 없는 줄에서도 새카맣게 깔린다

        // ── 배경 / Center 캐릭터 / CG (전부 raycastTarget 꺼서 탭이 아래로 새지 않게) ──
        // [중요] CG는 배경과 캐릭터를 모두 덮는 전체화면 오버레이여야 하므로, 반드시 CenterCharacterImage
        // "다음"(형제 순서상 나중)에 만들어야 한다 — uGUI는 나중에 추가된 형제를 위에 그린다.
        GameObject bgGO = CreateStretchChild(panelRoot.transform, "BackgroundImage");
        Image backgroundImage = bgGO.AddComponent<Image>();
        backgroundImage.raycastTarget = false;
        bgGO.SetActive(false);

        GameObject centerGO = new GameObject("CenterCharacterImage");
        centerGO.transform.SetParent(panelRoot.transform, false);
        RectTransform centerRT = centerGO.AddComponent<RectTransform>();
        centerRT.anchorMin = new Vector2(0.5f, 0f);
        centerRT.anchorMax = new Vector2(0.5f, 0f);
        centerRT.pivot = new Vector2(0.5f, 0f);
        centerRT.sizeDelta = new Vector2(MOBILE_WIDTH * 0.85f, 900f);
        centerRT.anchoredPosition = new Vector2(0f, 0f);
        Image centerCharacterImage = centerGO.AddComponent<Image>();
        centerCharacterImage.preserveAspect = true;
        centerCharacterImage.raycastTarget = false;
        centerGO.SetActive(false);

        GameObject cgGO = CreateStretchChild(panelRoot.transform, "CGImage");
        Image cgImage = cgGO.AddComponent<Image>();
        cgImage.raycastTarget = false;
        cgGO.SetActive(false);

        // ── 화면 전체를 덮는 투명 탭 버튼 — 탭하면 다음 줄로 진행 ──
        GameObject tapGO = CreateStretchChild(panelRoot.transform, "ScreenTapButton");
        Image tapImg = tapGO.AddComponent<Image>();
        tapImg.color = new Color(0f, 0f, 0f, 0f); // 완전 투명이지만 raycastTarget은 유지
        Button screenTapButton = tapGO.AddComponent<Button>();
        screenTapButton.targetGraphic = tapImg;
        screenTapButton.transition = Selectable.Transition.None;

        // ── 대사창 (하단 고정) ──
        GameObject dialogueBox = new GameObject("DialogueBox");
        dialogueBox.transform.SetParent(panelRoot.transform, false);
        RectTransform boxRT = dialogueBox.AddComponent<RectTransform>();
        boxRT.anchorMin = new Vector2(0f, 0f);
        boxRT.anchorMax = new Vector2(1f, 0f);
        boxRT.pivot = new Vector2(0.5f, 0f);
        boxRT.sizeDelta = new Vector2(0f, 260f);
        boxRT.anchoredPosition = new Vector2(0f, 16f);
        Image boxBg = dialogueBox.AddComponent<Image>();
        boxBg.color = new Color(0.05f, 0.04f, 0.07f, 0.88f);
        boxBg.raycastTarget = false;

        GameObject speakerGO = new GameObject("SpeakerNameText");
        speakerGO.transform.SetParent(dialogueBox.transform, false);
        SetupText(speakerGO, "", font, 22f, TextAlignmentOptions.Left,
                  new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -44f), new Vector2(-24f, -8f));
        TMP_Text speakerNameText = speakerGO.GetComponent<TMP_Text>();
        speakerNameText.fontStyle = FontStyles.Bold;
        speakerNameText.color = new Color(1f, 0.75f, 0.85f, 1f);
        speakerNameText.raycastTarget = false;

        GameObject dialogueGO = new GameObject("DialogueText");
        dialogueGO.transform.SetParent(dialogueBox.transform, false);
        SetupText(dialogueGO, "", font, 20f, TextAlignmentOptions.TopLeft,
                  Vector2.zero, Vector2.one, new Vector2(24f, 16f), new Vector2(-24f, -48f));
        TMP_Text dialogueText = dialogueGO.GetComponent<TMP_Text>();
        dialogueText.color = Color.white;
        dialogueText.raycastTarget = false;

        // ── 상단 버튼바 (로그 / 스킵 / 닫기) — 탭 버튼보다 나중에 추가돼 클릭을 우선 가로챈다 ──
        GameObject topBar = new GameObject("TopBar");
        topBar.transform.SetParent(panelRoot.transform, false);
        RectTransform topBarRT = topBar.AddComponent<RectTransform>();
        topBarRT.anchorMin = new Vector2(1f, 1f);
        topBarRT.anchorMax = new Vector2(1f, 1f);
        topBarRT.pivot = new Vector2(1f, 1f);
        topBarRT.sizeDelta = new Vector2(280f, 56f);
        topBarRT.anchoredPosition = new Vector2(-10f, -10f);
        HorizontalLayoutGroup topBarLayout = topBar.AddComponent<HorizontalLayoutGroup>();
        topBarLayout.childForceExpandWidth = true;
        topBarLayout.childForceExpandHeight = true;
        topBarLayout.childControlWidth = true;
        topBarLayout.childControlHeight = true;
        topBarLayout.spacing = 8f;

        Button logToggleButton = CreateTabButton(topBar.transform, "LogButton", "로그", font);
        Button skipButton = CreateTabButton(topBar.transform, "SkipButton", "스킵", font);
        TMP_Text skipButtonText = skipButton.GetComponentInChildren<TMP_Text>();
        Button closeButton = CreateTabButton(topBar.transform, "CloseButton", "X", font);

        // ── 로그 패널 (맨 나중에 추가 — 항상 최상단에서 전체를 덮는다) ──
        GameObject logPanelRoot = new GameObject("LogPanelRoot");
        logPanelRoot.transform.SetParent(panelRoot.transform, false);
        RectTransform logRT = logPanelRoot.AddComponent<RectTransform>();
        logRT.anchorMin = Vector2.zero;
        logRT.anchorMax = Vector2.one;
        logRT.offsetMin = Vector2.zero;
        logRT.offsetMax = Vector2.zero;
        Image logDim = logPanelRoot.AddComponent<Image>();
        logDim.color = new Color(0f, 0f, 0f, 0.9f);

        GameObject logScroll = new GameObject("Scroll View");
        logScroll.transform.SetParent(logPanelRoot.transform, false);
        RectTransform logScrollRT = logScroll.AddComponent<RectTransform>();
        logScrollRT.anchorMin = Vector2.zero;
        logScrollRT.anchorMax = Vector2.one;
        logScrollRT.offsetMin = new Vector2(0f, 76f); // 하단 닫기 버튼 자리
        logScrollRT.offsetMax = Vector2.zero;
        RectTransform logContent = SetupVerticalScrollContent(logScroll, new RectOffset(20, 20, 20, 20));

        GameObject logCloseGO = new GameObject("LogCloseButton");
        logCloseGO.transform.SetParent(logPanelRoot.transform, false);
        RectTransform logCloseRT = logCloseGO.AddComponent<RectTransform>();
        logCloseRT.anchorMin = new Vector2(0f, 0f);
        logCloseRT.anchorMax = new Vector2(1f, 0f);
        logCloseRT.pivot = new Vector2(0.5f, 0f);
        logCloseRT.sizeDelta = new Vector2(0f, 64f);
        logCloseRT.anchoredPosition = new Vector2(0f, 8f);
        Image logCloseBg = logCloseGO.AddComponent<Image>();
        logCloseBg.color = new Color(0.3f, 0.3f, 0.35f, 1f);
        Button logCloseButton = logCloseGO.AddComponent<Button>();
        logCloseButton.targetGraphic = logCloseBg;
        GameObject logCloseLabelGO = new GameObject("Label");
        logCloseLabelGO.transform.SetParent(logCloseGO.transform, false);
        SetupText(logCloseLabelGO, "닫기", font, 18f, TextAlignmentOptions.Center,
                  Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        logPanelRoot.SetActive(false);

        // ── MemorialPlayerPanel 컴포넌트 연결 ──
        MemorialPlayerPanel player = panelRoot.AddComponent<MemorialPlayerPanel>();
        player.root = panelRoot;
        player.backgroundImage = backgroundImage;
        player.cgImage = cgImage;
        player.centerCharacterImage = centerCharacterImage;
        player.speakerNameText = speakerNameText;
        player.dialogueText = dialogueText;
        player.screenTapButton = screenTapButton;
        player.logPanelRoot = logPanelRoot;
        player.logContent = logContent.transform;
        player.logToggleButton = logToggleButton;
        player.logCloseButton = logCloseButton;
        player.skipButton = skipButton;
        player.skipButtonText = skipButtonText;
        player.closeButton = closeButton;
    }

    // ── SettingsPanel 생성 (설정 화면 — 도감/메모리얼과 동일한 전체 오버레이 방식) ──
    //
    // [확장 방법] 지금은 "데이터 초기화" 한 줄뿐이다. BGM/효과음 볼륨, FPS 제한, 튜토리얼 다시보기
    // 등을 나중에 추가할 때는: (1) 버튼형 항목이면 CreateSettingsButtonRow를 한 번 더 호출해서
    // bodyContent 밑에 추가하고, (2) 슬라이더/토글처럼 다른 위젯이 필요하면 이 함수 옆에 비슷한
    // "CreateSettingsXxxRow" 헬퍼를 하나 더 만들어 같은 방식으로 bodyContent에 추가하면 된다 —
    // SettingsPanel.cs나 이 메서드의 나머지 구조(헤더/스크롤/확인 오버레이)는 손댈 필요가 없다.
    static SettingsPanel BuildSettingsPanel(Transform canvasTransform)
    {
        Transform existing = canvasTransform.Find("SettingsPanel");
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        TMP_FontAsset font = LoadNotoFont();

        GameObject panelRoot = new GameObject("SettingsPanel");
        panelRoot.transform.SetParent(canvasTransform, false);
        RectTransform rootRT = panelRoot.AddComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;

        Image dim = panelRoot.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.92f);

        // ── Header (도감/메모리얼과 동일한 구조) ──
        GameObject header = new GameObject("Header");
        header.transform.SetParent(panelRoot.transform, false);
        RectTransform hrt = header.AddComponent<RectTransform>();
        hrt.anchorMin = new Vector2(0f, 1f);
        hrt.anchorMax = new Vector2(1f, 1f);
        hrt.pivot = new Vector2(0.5f, 1f);
        hrt.sizeDelta = new Vector2(0f, DEX_HEADER_HEIGHT);

        Image hImg = header.AddComponent<Image>();
        hImg.color = new Color(0.15f, 0.16f, 0.22f, 1f);

        GameObject title = new GameObject("Title");
        title.transform.SetParent(header.transform, false);
        SetupText(title, "설정", font, 28f, TextAlignmentOptions.Left,
                  Vector2.zero, Vector2.one, new Vector2(24f, 0f), new Vector2(-80f, 0f));

        GameObject closeGO = new GameObject("CloseButton");
        closeGO.transform.SetParent(header.transform, false);
        RectTransform closeRT = closeGO.AddComponent<RectTransform>();
        closeRT.anchorMin = new Vector2(1f, 0f);
        closeRT.anchorMax = new Vector2(1f, 1f);
        closeRT.pivot = new Vector2(1f, 0.5f);
        closeRT.sizeDelta = new Vector2(60f, 0f);
        closeRT.anchoredPosition = new Vector2(-10f, 0f);
        Image closeBg = closeGO.AddComponent<Image>();
        closeBg.color = new Color(1f, 0.35f, 0.62f, 1f);
        Button closeButton = closeGO.AddComponent<Button>();
        closeButton.targetGraphic = closeBg;
        GameObject closeLabelGO = new GameObject("Label");
        closeLabelGO.transform.SetParent(closeGO.transform, false);
        SetupText(closeLabelGO, "X", font, 22f, TextAlignmentOptions.Center,
                  Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // ── Body: 세로 스크롤 설정 목록 — 확장 전용, 앞으로 여기에 항목만 추가하면 된다 ──
        GameObject bodyScroll = new GameObject("Scroll View");
        bodyScroll.transform.SetParent(panelRoot.transform, false);
        RectTransform bodyRT = bodyScroll.AddComponent<RectTransform>();
        bodyRT.anchorMin = Vector2.zero;
        bodyRT.anchorMax = Vector2.one;
        bodyRT.offsetMin = Vector2.zero;
        bodyRT.offsetMax = new Vector2(0f, -DEX_HEADER_HEIGHT);
        RectTransform bodyContent = SetupVerticalScrollContent(bodyScroll, new RectOffset(20, 20, 20, 20));

        // ── 데이터 초기화 항목 (지금 확정된 유일한 설정) ──
        (Button resetButton, _) = CreateSettingsButtonRow(bodyContent.transform, font,
            "데이터 초기화", "저장된 진행 상황을 전부 지우고 처음부터 다시 시작합니다.\n되돌릴 수 없습니다.",
            new Color(0.55f, 0.2f, 0.2f, 1f));

        // ── 치트 모드 항목 — 비밀번호(1204)를 맞혀야만 CheatPanel이 열린다(빌드된 게임에서도 접근 가능) ──
        (Button cheatButton, _) = CreateSettingsButtonRow(bodyContent.transform, font,
            "치트 모드", "비밀번호를 입력하면 개발자용 치트 패널을 엽니다.",
            new Color(0.35f, 0.3f, 0.15f, 1f));

        // ── 확인 오버레이 (패널 직계 자식, 기본 비활성 — SetAsLastSibling으로 항상 맨 위에 그려지게 함) ──
        GameObject confirmRoot = CreateStretchChild(panelRoot.transform, "ResetConfirmOverlay");
        Image confirmDim = confirmRoot.AddComponent<Image>();
        confirmDim.color = new Color(0f, 0f, 0f, 0.85f);

        GameObject confirmBox = new GameObject("Box");
        confirmBox.transform.SetParent(confirmRoot.transform, false);
        RectTransform confirmBoxRT = confirmBox.AddComponent<RectTransform>();
        confirmBoxRT.anchorMin = new Vector2(0.5f, 0.5f);
        confirmBoxRT.anchorMax = new Vector2(0.5f, 0.5f);
        confirmBoxRT.pivot = new Vector2(0.5f, 0.5f);
        confirmBoxRT.sizeDelta = new Vector2(440f, 220f);
        Image confirmBoxBg = confirmBox.AddComponent<Image>();
        confirmBoxBg.color = new Color(0.15f, 0.13f, 0.15f, 1f);

        GameObject confirmTextGO = new GameObject("Text");
        confirmTextGO.transform.SetParent(confirmBox.transform, false);
        SetupText(confirmTextGO, "정말 초기화하시겠습니까?\n저장된 진행 상황이 전부 사라지며 되돌릴 수 없습니다.",
                  font, 18f, TextAlignmentOptions.Center,
                  new Vector2(0f, 0.4f), new Vector2(1f, 1f), new Vector2(20f, 0f), new Vector2(-20f, -20f));

        GameObject confirmButtonRow = new GameObject("ButtonRow");
        confirmButtonRow.transform.SetParent(confirmBox.transform, false);
        RectTransform confirmRowRT = confirmButtonRow.AddComponent<RectTransform>();
        confirmRowRT.anchorMin = new Vector2(0f, 0f);
        confirmRowRT.anchorMax = new Vector2(1f, 0.4f);
        confirmRowRT.offsetMin = new Vector2(20f, 20f);
        confirmRowRT.offsetMax = new Vector2(-20f, 0f);
        HorizontalLayoutGroup confirmRowLayout = confirmButtonRow.AddComponent<HorizontalLayoutGroup>();
        confirmRowLayout.childForceExpandWidth = true;
        confirmRowLayout.childForceExpandHeight = true;
        confirmRowLayout.childControlWidth = true;
        confirmRowLayout.childControlHeight = true;
        confirmRowLayout.spacing = 12f;

        GameObject noGO = new GameObject("NoButton");
        noGO.transform.SetParent(confirmButtonRow.transform, false);
        Image noBg = noGO.AddComponent<Image>();
        noBg.color = new Color(0.3f, 0.3f, 0.35f, 1f);
        Button noButton = noGO.AddComponent<Button>();
        noButton.targetGraphic = noBg;
        GameObject noLabelGO = new GameObject("Label");
        noLabelGO.transform.SetParent(noGO.transform, false);
        SetupText(noLabelGO, "취소", font, 18f, TextAlignmentOptions.Center,
                  Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        GameObject yesGO = new GameObject("YesButton");
        yesGO.transform.SetParent(confirmButtonRow.transform, false);
        Image yesBg = yesGO.AddComponent<Image>();
        yesBg.color = new Color(0.7f, 0.25f, 0.25f, 1f);
        Button yesButton = yesGO.AddComponent<Button>();
        yesButton.targetGraphic = yesBg;
        GameObject yesLabelGO = new GameObject("Label");
        yesLabelGO.transform.SetParent(yesGO.transform, false);
        SetupText(yesLabelGO, "초기화", font, 18f, TextAlignmentOptions.Center,
                  Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        confirmRoot.SetActive(false);
        confirmRoot.transform.SetAsLastSibling();

        // ── 치트 비밀번호 입력 오버레이 (패널 직계 자식, 기본 비활성 — 확인 오버레이와 동일 구조) ──
        GameObject cheatPwRoot = CreateStretchChild(panelRoot.transform, "CheatPasswordOverlay");
        Image cheatPwDim = cheatPwRoot.AddComponent<Image>();
        cheatPwDim.color = new Color(0f, 0f, 0f, 0.85f);

        GameObject cheatPwBox = new GameObject("Box");
        cheatPwBox.transform.SetParent(cheatPwRoot.transform, false);
        RectTransform cheatPwBoxRT = cheatPwBox.AddComponent<RectTransform>();
        cheatPwBoxRT.anchorMin = new Vector2(0.5f, 0.5f);
        cheatPwBoxRT.anchorMax = new Vector2(0.5f, 0.5f);
        cheatPwBoxRT.pivot = new Vector2(0.5f, 0.5f);
        cheatPwBoxRT.sizeDelta = new Vector2(440f, 260f);
        Image cheatPwBoxBg = cheatPwBox.AddComponent<Image>();
        cheatPwBoxBg.color = new Color(0.15f, 0.13f, 0.15f, 1f);

        GameObject cheatPwTitleGO = new GameObject("Text");
        cheatPwTitleGO.transform.SetParent(cheatPwBox.transform, false);
        SetupText(cheatPwTitleGO, "치트 모드 비밀번호를 입력하세요.",
                  font, 18f, TextAlignmentOptions.Center,
                  new Vector2(0f, 0.72f), new Vector2(1f, 1f), new Vector2(20f, 0f), new Vector2(-20f, -20f));

        TMP_InputField cheatPwInput = CreateNumericInputField(cheatPwBox.transform, "PasswordInput", "비밀번호", font);
        RectTransform cheatPwInputRT = cheatPwInput.GetComponent<RectTransform>();
        cheatPwInputRT.anchorMin = new Vector2(0.15f, 0.5f);
        cheatPwInputRT.anchorMax = new Vector2(0.85f, 0.68f);
        cheatPwInputRT.offsetMin = Vector2.zero;
        cheatPwInputRT.offsetMax = Vector2.zero;
        cheatPwInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        cheatPwInput.characterLimit = 8;

        GameObject cheatPwErrorGO = new GameObject("ErrorText");
        cheatPwErrorGO.transform.SetParent(cheatPwBox.transform, false);
        SetupText(cheatPwErrorGO, "비밀번호가 틀렸습니다.", font, 14f, TextAlignmentOptions.Center,
                  new Vector2(0f, 0.38f), new Vector2(1f, 0.5f), new Vector2(20f, 0f), new Vector2(-20f, 0f));
        TMP_Text cheatPwErrorText = cheatPwErrorGO.GetComponent<TMP_Text>();
        cheatPwErrorText.color = new Color(1f, 0.4f, 0.4f, 1f);
        cheatPwErrorGO.SetActive(false);

        GameObject cheatPwButtonRow = new GameObject("ButtonRow");
        cheatPwButtonRow.transform.SetParent(cheatPwBox.transform, false);
        RectTransform cheatPwRowRT = cheatPwButtonRow.AddComponent<RectTransform>();
        cheatPwRowRT.anchorMin = new Vector2(0f, 0f);
        cheatPwRowRT.anchorMax = new Vector2(1f, 0.32f);
        cheatPwRowRT.offsetMin = new Vector2(20f, 20f);
        cheatPwRowRT.offsetMax = new Vector2(-20f, 0f);
        HorizontalLayoutGroup cheatPwRowLayout = cheatPwButtonRow.AddComponent<HorizontalLayoutGroup>();
        cheatPwRowLayout.childForceExpandWidth = true;
        cheatPwRowLayout.childForceExpandHeight = true;
        cheatPwRowLayout.childControlWidth = true;
        cheatPwRowLayout.childControlHeight = true;
        cheatPwRowLayout.spacing = 12f;

        GameObject cheatPwCancelGO = new GameObject("CancelButton");
        cheatPwCancelGO.transform.SetParent(cheatPwButtonRow.transform, false);
        Image cheatPwCancelBg = cheatPwCancelGO.AddComponent<Image>();
        cheatPwCancelBg.color = new Color(0.3f, 0.3f, 0.35f, 1f);
        Button cheatPwCancelButton = cheatPwCancelGO.AddComponent<Button>();
        cheatPwCancelButton.targetGraphic = cheatPwCancelBg;
        GameObject cheatPwCancelLabelGO = new GameObject("Label");
        cheatPwCancelLabelGO.transform.SetParent(cheatPwCancelGO.transform, false);
        SetupText(cheatPwCancelLabelGO, "취소", font, 18f, TextAlignmentOptions.Center,
                  Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        GameObject cheatPwConfirmGO = new GameObject("ConfirmButton");
        cheatPwConfirmGO.transform.SetParent(cheatPwButtonRow.transform, false);
        Image cheatPwConfirmBg = cheatPwConfirmGO.AddComponent<Image>();
        cheatPwConfirmBg.color = new Color(0.65f, 0.55f, 0.2f, 1f);
        Button cheatPwConfirmButton = cheatPwConfirmGO.AddComponent<Button>();
        cheatPwConfirmButton.targetGraphic = cheatPwConfirmBg;
        GameObject cheatPwConfirmLabelGO = new GameObject("Label");
        cheatPwConfirmLabelGO.transform.SetParent(cheatPwConfirmGO.transform, false);
        SetupText(cheatPwConfirmLabelGO, "확인", font, 18f, TextAlignmentOptions.Center,
                  Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        cheatPwRoot.SetActive(false);
        cheatPwRoot.transform.SetAsLastSibling();

        SettingsPanel panel = panelRoot.AddComponent<SettingsPanel>();
        panel.root = panelRoot;
        panel.closeButton = closeButton;
        panel.resetDataButton = resetButton;
        panel.resetConfirmRoot = confirmRoot;
        panel.resetConfirmYesButton = yesButton;
        panel.resetConfirmNoButton = noButton;
        panel.cheatButton = cheatButton;
        panel.cheatPasswordRoot = cheatPwRoot;
        panel.cheatPasswordInput = cheatPwInput;
        panel.cheatPasswordErrorText = cheatPwErrorText;
        panel.cheatPasswordConfirmButton = cheatPwConfirmButton;
        panel.cheatPasswordCancelButton = cheatPwCancelButton;

        return panel;
    }

    /// <summary>
    /// 치트 패널 — SettingsPanel에서 비밀번호(1204)를 맞혀야만 열린다. 도감/설정과 동일한 전체
    /// 화면 오버레이 구조(헤더 + 세로 스크롤 본문)이고, 그 아래 항상 고정된 "결과 출력" 스크롤
    /// 박스를 하나 더 둬서 Debug.Log 대신 화면에서 바로 결과를 확인할 수 있게 한다(빌드된 게임엔
    /// Console이 없으므로).
    /// </summary>
    static void BuildCheatPanel(Transform canvasTransform)
    {
        Transform existing = canvasTransform.Find("CheatPanel");
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        TMP_FontAsset font = LoadNotoFont();

        GameObject panelRoot = new GameObject("CheatPanel");
        panelRoot.transform.SetParent(canvasTransform, false);
        RectTransform rootRT = panelRoot.AddComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;

        Image dim = panelRoot.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.94f);

        // ── Header ──
        GameObject header = new GameObject("Header");
        header.transform.SetParent(panelRoot.transform, false);
        RectTransform hrt = header.AddComponent<RectTransform>();
        hrt.anchorMin = new Vector2(0f, 1f);
        hrt.anchorMax = new Vector2(1f, 1f);
        hrt.pivot = new Vector2(0.5f, 1f);
        hrt.sizeDelta = new Vector2(0f, DEX_HEADER_HEIGHT);
        Image hImg = header.AddComponent<Image>();
        hImg.color = new Color(0.22f, 0.18f, 0.1f, 1f); // 치트 창임을 색으로도 구분(설정/도감과 다른 톤)

        GameObject title = new GameObject("Title");
        title.transform.SetParent(header.transform, false);
        SetupText(title, "치트 모드", font, 28f, TextAlignmentOptions.Left,
                  Vector2.zero, Vector2.one, new Vector2(24f, 0f), new Vector2(-80f, 0f));

        GameObject closeGO = new GameObject("CloseButton");
        closeGO.transform.SetParent(header.transform, false);
        RectTransform closeRT = closeGO.AddComponent<RectTransform>();
        closeRT.anchorMin = new Vector2(1f, 0f);
        closeRT.anchorMax = new Vector2(1f, 1f);
        closeRT.pivot = new Vector2(1f, 0.5f);
        closeRT.sizeDelta = new Vector2(60f, 0f);
        closeRT.anchoredPosition = new Vector2(-10f, 0f);
        Image closeBg = closeGO.AddComponent<Image>();
        closeBg.color = new Color(1f, 0.35f, 0.62f, 1f);
        Button closeButton = closeGO.AddComponent<Button>();
        closeButton.targetGraphic = closeBg;
        GameObject closeLabelGO = new GameObject("Label");
        closeLabelGO.transform.SetParent(closeGO.transform, false);
        SetupText(closeLabelGO, "X", font, 22f, TextAlignmentOptions.Center,
                  Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // ── 결과 출력 박스 (하단 고정, 항상 보임) ──
        const float RESULT_BOX_HEIGHT = 220f;
        GameObject resultBox = new GameObject("ResultBox");
        resultBox.transform.SetParent(panelRoot.transform, false);
        RectTransform resultBoxRT = resultBox.AddComponent<RectTransform>();
        resultBoxRT.anchorMin = new Vector2(0f, 0f);
        resultBoxRT.anchorMax = new Vector2(1f, 0f);
        resultBoxRT.pivot = new Vector2(0.5f, 0f);
        resultBoxRT.sizeDelta = new Vector2(0f, RESULT_BOX_HEIGHT);
        Image resultBoxBg = resultBox.AddComponent<Image>();
        resultBoxBg.color = new Color(0.08f, 0.08f, 0.1f, 1f);

        RectTransform resultScrollContent = SetupVerticalScrollContent(resultBox, new RectOffset(16, 16, 12, 12));
        GameObject resultTextGO = new GameObject("Text");
        resultTextGO.transform.SetParent(resultScrollContent.transform, false);
        SetupText(resultTextGO, "결과가 여기 표시됩니다.", font, 16f, TextAlignmentOptions.TopLeft,
                  Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        TMP_Text resultText = resultTextGO.GetComponent<TMP_Text>();
        resultText.color = new Color(0.85f, 0.9f, 0.85f, 1f);
        LayoutElement resultTextLE = resultTextGO.AddComponent<LayoutElement>();
        resultTextLE.flexibleWidth = 1f;

        // ── Body: 세로 스크롤 (헤더 아래 ~ 결과 박스 위) ──
        GameObject bodyScroll = new GameObject("Scroll View");
        bodyScroll.transform.SetParent(panelRoot.transform, false);
        RectTransform bodyRT = bodyScroll.AddComponent<RectTransform>();
        bodyRT.anchorMin = Vector2.zero;
        bodyRT.anchorMax = Vector2.one;
        bodyRT.offsetMin = new Vector2(0f, RESULT_BOX_HEIGHT);
        bodyRT.offsetMax = new Vector2(0f, -DEX_HEADER_HEIGHT);
        RectTransform bodyContent = SetupVerticalScrollContent(bodyScroll, new RectOffset(20, 20, 20, 20));

        // ── 1. 골드 ──
        CreateCheatSectionLabel(bodyContent.transform, font, "1. 골드");
        TMP_InputField goldInput = CreateNumericInputField(bodyContent.transform, "GoldInput", "골드 값", font);
        LayoutElement goldInputLE = goldInput.gameObject.AddComponent<LayoutElement>();
        goldInputLE.preferredHeight = 48f;
        goldInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        goldInput.characterLimit = 15;

        GameObject goldButtonRow = CreateCheatButtonRow(bodyContent.transform, 48f);
        Button goldAddButton = CreateTabButton(goldButtonRow.transform, "AddButton", "추가", font);
        Button goldSetButton = CreateTabButton(goldButtonRow.transform, "SetButton", "설정", font);

        GameObject goldPresetRow = CreateCheatButtonRow(bodyContent.transform, 44f);
        Button gold10kButton = CreateTabButton(goldPresetRow.transform, "Preset10k", "+1만", font);
        Button gold1mButton = CreateTabButton(goldPresetRow.transform, "Preset1m", "+100만", font);
        Button gold100mButton = CreateTabButton(goldPresetRow.transform, "Preset100m", "+1억", font);
        Button gold1tButton = CreateTabButton(goldPresetRow.transform, "Preset1t", "+1조", font);

        // ── 2. 시간 스킵 ──
        CreateCheatSectionLabel(bodyContent.transform, font, "2. 시간 스킵");
        GameObject skipRow1 = CreateCheatButtonRow(bodyContent.transform, 48f);
        Button skip1hButton = CreateTabButton(skipRow1.transform, "Skip1h", "1h", font);
        Button skip6hButton = CreateTabButton(skipRow1.transform, "Skip6h", "6h", font);
        Button skip12hButton = CreateTabButton(skipRow1.transform, "Skip12h", "12h", font);
        GameObject skipRow2 = CreateCheatButtonRow(bodyContent.transform, 48f);
        Button skip24hButton = CreateTabButton(skipRow2.transform, "Skip24h", "24h", font);
        Button skip48hButton = CreateTabButton(skipRow2.transform, "Skip48h", "48h", font);
        Button skip72hButton = CreateTabButton(skipRow2.transform, "Skip72h", "72h", font);

        // ── 3. 꽃 선택 (개화/레벨/유대 공용) ──
        CreateCheatSectionLabel(bodyContent.transform, font, "3. 꽃 선택 / 개화 / 레벨 / 유대");
        GameObject flowerPickRow = CreateCheatButtonRow(bodyContent.transform, 52f);
        Button prevFlowerButton = CreateTabButton(flowerPickRow.transform, "PrevButton", "◀", font);
        GameObject flowerNameGO = new GameObject("FlowerNameText");
        flowerNameGO.transform.SetParent(flowerPickRow.transform, false);
        SetupText(flowerNameGO, "-", font, 20f, TextAlignmentOptions.Center,
                  Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        TMP_Text flowerNameText = flowerNameGO.GetComponent<TMP_Text>();
        LayoutElement flowerNameLE = flowerNameGO.AddComponent<LayoutElement>();
        flowerNameLE.flexibleWidth = 2f;
        Button nextFlowerButton = CreateTabButton(flowerPickRow.transform, "NextButton", "▶", font);

        Button forceBloomButton = CreateTabButton(bodyContent.transform, "ForceBloomButton", "선택한 꽃 즉시 개화", font);
        forceBloomButton.gameObject.AddComponent<LayoutElement>().preferredHeight = 48f;

        GameObject levelRow = CreateCheatButtonRow(bodyContent.transform, 48f);
        TMP_InputField levelInput = CreateNumericInputField(levelRow.transform, "LevelInput", "레벨", font);
        levelInput.characterLimit = 6;
        Button setLevelButton = CreateTabButton(levelRow.transform, "SetLevelButton", "레벨 설정", font);

        GameObject bondRow = CreateCheatButtonRow(bodyContent.transform, 48f);
        TMP_InputField bondLevelInput = CreateNumericInputField(bondRow.transform, "BondLevelInput", "유대 Lv(0~5)", font);
        bondLevelInput.characterLimit = 1;
        Button setBondLevelButton = CreateTabButton(bondRow.transform, "SetBondLevelButton", "유대 설정", font);

        // ── 4. 정원 · 시듦 ──
        CreateCheatSectionLabel(bodyContent.transform, font, "4. 정원 · 시듦");
        GameObject wiltPickRow = CreateCheatButtonRow(bodyContent.transform, 52f);
        Button prevWiltButton = CreateTabButton(wiltPickRow.transform, "PrevWiltButton", "◀", font);
        GameObject wiltStageGO = new GameObject("WiltStageText");
        wiltStageGO.transform.SetParent(wiltPickRow.transform, false);
        SetupText(wiltStageGO, "-", font, 18f, TextAlignmentOptions.Center,
                  Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        TMP_Text wiltStageText = wiltStageGO.GetComponent<TMP_Text>();
        LayoutElement wiltStageLE = wiltStageGO.AddComponent<LayoutElement>();
        wiltStageLE.flexibleWidth = 2f;
        Button nextWiltButton = CreateTabButton(wiltPickRow.transform, "NextWiltButton", "▶", font);

        Button applyWiltStageButton = CreateTabButton(bodyContent.transform, "ApplyWiltStageButton", "선택한 시듦 단계 적용", font);
        applyWiltStageButton.gameObject.AddComponent<LayoutElement>().preferredHeight = 48f;

        Button restoreGardenButton = CreateTabButton(bodyContent.transform, "RestoreGardenButton", "정원 즉시 복구", font);
        restoreGardenButton.gameObject.AddComponent<LayoutElement>().preferredHeight = 48f;

        // ── 5. 진단 ──
        CreateCheatSectionLabel(bodyContent.transform, font, "5. 진단 출력");
        Button diagnosticsButton = CreateTabButton(bodyContent.transform, "DiagnosticsButton", "현재 상태 출력", font);
        diagnosticsButton.gameObject.AddComponent<LayoutElement>().preferredHeight = 48f;

        // ── CheatPanel 컴포넌트 연결 ──
        CheatPanel panel = panelRoot.AddComponent<CheatPanel>();
        panel.root = panelRoot;
        panel.closeButton = closeButton;

        panel.goldAmountInput = goldInput;
        panel.goldAddButton = goldAddButton;
        panel.goldSetButton = goldSetButton;
        panel.gold10kButton = gold10kButton;
        panel.gold1mButton = gold1mButton;
        panel.gold100mButton = gold100mButton;
        panel.gold1tButton = gold1tButton;

        panel.skip1hButton = skip1hButton;
        panel.skip6hButton = skip6hButton;
        panel.skip12hButton = skip12hButton;
        panel.skip24hButton = skip24hButton;
        panel.skip48hButton = skip48hButton;
        panel.skip72hButton = skip72hButton;

        panel.prevFlowerButton = prevFlowerButton;
        panel.nextFlowerButton = nextFlowerButton;
        panel.flowerNameText = flowerNameText;
        panel.forceBloomButton = forceBloomButton;
        panel.levelInput = levelInput;
        panel.setLevelButton = setLevelButton;
        panel.bondLevelInput = bondLevelInput;
        panel.setBondLevelButton = setBondLevelButton;

        panel.prevWiltButton = prevWiltButton;
        panel.nextWiltButton = nextWiltButton;
        panel.wiltStageText = wiltStageText;
        panel.applyWiltStageButton = applyWiltStageButton;
        panel.restoreGardenButton = restoreGardenButton;

        panel.diagnosticsButton = diagnosticsButton;
        panel.resultText = resultText;
    }

    /// <summary> 치트 패널 세로 스크롤 본문에 섹션 제목 한 줄을 추가한다("1. 골드" 같은). </summary>
    static void CreateCheatSectionLabel(Transform parent, TMP_FontAsset font, string text)
    {
        GameObject go = new GameObject($"Label_{text}");
        go.transform.SetParent(parent, false);
        SetupText(go, text, font, 20f, TextAlignmentOptions.Left,
                  Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        TMP_Text tmp = go.GetComponent<TMP_Text>();
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = new Color(1f, 0.85f, 0.4f, 1f);
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 32f;
    }

    /// <summary> 치트 패널 세로 스크롤 본문에 가로로 나열되는 버튼/입력창 한 줄을 추가한다. </summary>
    static GameObject CreateCheatButtonRow(Transform parent, float height)
    {
        GameObject row = new GameObject("Row");
        row.transform.SetParent(parent, false);
        LayoutElement rowLE = row.AddComponent<LayoutElement>();
        rowLE.preferredHeight = height;
        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.spacing = 8f;
        return row;
    }

    /// <summary>
    /// 설정 항목 한 줄(제목+설명 + 액션 버튼)을 만들어 parent(세로 스크롤 content)에 추가한다.
    /// 앞으로 BGM/효과음 볼륨(슬라이더), FPS 제한처럼 버튼이 아닌 위젯이 필요해지면 이 함수 옆에
    /// 비슷한 헬퍼를 하나 더 만들면 된다 — SettingsPanel.cs나 씬 구조를 바꿀 필요가 없다.
    /// </summary>
    static (Button button, TMP_Text titleText) CreateSettingsButtonRow(
        Transform parent, TMP_FontAsset font, string title, string description, Color buttonColor)
    {
        GameObject row = new GameObject($"Row_{title}");
        row.transform.SetParent(parent, false);
        LayoutElement rowLE = row.AddComponent<LayoutElement>();
        rowLE.preferredHeight = 96f;
        Image rowBg = row.AddComponent<Image>();
        rowBg.color = new Color(1f, 1f, 1f, 0.04f);

        GameObject titleGO = new GameObject("Title");
        titleGO.transform.SetParent(row.transform, false);
        SetupText(titleGO, title, font, 20f, TextAlignmentOptions.Left,
                  new Vector2(0f, 0.5f), new Vector2(0.7f, 1f), new Vector2(16f, 4f), new Vector2(-8f, -8f));
        titleGO.GetComponent<TMP_Text>().fontStyle = FontStyles.Bold;

        GameObject descGO = new GameObject("Description");
        descGO.transform.SetParent(row.transform, false);
        SetupText(descGO, description, font, 14f, TextAlignmentOptions.Left,
                  new Vector2(0f, 0f), new Vector2(0.7f, 0.5f), new Vector2(16f, 8f), new Vector2(-8f, 0f));
        descGO.GetComponent<TMP_Text>().color = new Color(0.8f, 0.75f, 0.75f, 1f);

        GameObject buttonGO = new GameObject("Button");
        buttonGO.transform.SetParent(row.transform, false);
        RectTransform buttonRT = buttonGO.AddComponent<RectTransform>();
        buttonRT.anchorMin = new Vector2(0.72f, 0.25f);
        buttonRT.anchorMax = new Vector2(0.98f, 0.75f);
        buttonRT.offsetMin = Vector2.zero;
        buttonRT.offsetMax = Vector2.zero;
        Image buttonBg = buttonGO.AddComponent<Image>();
        buttonBg.color = buttonColor;
        Button button = buttonGO.AddComponent<Button>();
        button.targetGraphic = buttonBg;
        GameObject buttonLabelGO = new GameObject("Label");
        buttonLabelGO.transform.SetParent(buttonGO.transform, false);
        SetupText(buttonLabelGO, title, font, 16f, TextAlignmentOptions.Center,
                  Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        return (button, titleGO.GetComponent<TMP_Text>());
    }

    /// <summary>
    /// FlowerDexItem 프리팹을 로드하거나, 없으면 최초 1회 생성해서 에셋으로 저장한다.
    /// FlowerUpgradeItem과 동일 원칙 — 이미 존재하면 재사용(손으로 꾸며도 재빌드 시 덮어쓰지 않음).
    /// </summary>
    static FlowerDexItem GetOrCreateFlowerDexItemPrefab()
    {
        FlowerDexItem existing = AssetDatabase.LoadAssetAtPath<FlowerDexItem>(FLOWER_DEX_ITEM_PREFAB_PATH);
        if (existing != null) return PatchUnreadMemorialBadgeIfMissing(existing);

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

        // 안 읽은 메모리얼 뱃지 — 칸 우측 상단에 작게 겹치는 점. 기본 비활성, FlowerDexItem.Refresh가 토글.
        GameObject badgeGO = new GameObject("UnreadMemorialBadge");
        badgeGO.transform.SetParent(temp.transform, false);
        RectTransform badgeRT = badgeGO.AddComponent<RectTransform>();
        badgeRT.anchorMin = new Vector2(1f, 1f);
        badgeRT.anchorMax = new Vector2(1f, 1f);
        badgeRT.pivot = new Vector2(0.5f, 0.5f);
        badgeRT.sizeDelta = new Vector2(18f, 18f);
        badgeRT.anchoredPosition = new Vector2(-6f, -6f);
        Image badgeImg = badgeGO.AddComponent<Image>();
        badgeImg.color = new Color(1f, 0.85f, 0.3f, 1f);
        badgeGO.SetActive(false);

        FlowerDexItem item = temp.AddComponent<FlowerDexItem>();
        item.iconImage = iconImg;
        item.flowerNameText = nameGO.GetComponent<TMP_Text>();
        item.statusText = statusGO.GetComponent<TMP_Text>();
        item.selectButton = selectButton;
        item.currentHighlight = highlight;
        item.unreadMemorialBadge = badgeGO;

        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(temp, FLOWER_DEX_ITEM_PREFAB_PATH);
        Object.DestroyImmediate(temp);

        return savedPrefab != null ? savedPrefab.GetComponent<FlowerDexItem>() : null;
    }

    /// <summary>
    /// 기존에 이미 저장된 FlowerDexItem 프리팹(재사용 대상이라 위 생성 분기를 안 타는 경우)에
    /// unreadMemorialBadge 필드가 비어 있으면(유대 시스템 추가 이전에 만들어진 프리팹) 뱃지 자식을
    /// 덧붙이고 필드를 채운 뒤 다시 저장한다 — "손으로 꾸며도 덮어쓰지 않는다" 원칙을 지키면서도,
    /// 새 기능이 옛 프리팹에 자동으로 반영되게 하는 최소한의 패치.
    /// </summary>
    static FlowerDexItem PatchUnreadMemorialBadgeIfMissing(FlowerDexItem existing)
    {
        if (existing.unreadMemorialBadge != null) return existing;

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(existing.gameObject);

        GameObject badgeGO = new GameObject("UnreadMemorialBadge");
        badgeGO.transform.SetParent(instance.transform, false);
        RectTransform badgeRT = badgeGO.AddComponent<RectTransform>();
        badgeRT.anchorMin = new Vector2(1f, 1f);
        badgeRT.anchorMax = new Vector2(1f, 1f);
        badgeRT.pivot = new Vector2(0.5f, 0.5f);
        badgeRT.sizeDelta = new Vector2(18f, 18f);
        badgeRT.anchoredPosition = new Vector2(-6f, -6f);
        Image badgeImg = badgeGO.AddComponent<Image>();
        badgeImg.color = new Color(1f, 0.85f, 0.3f, 1f);
        badgeGO.SetActive(false);

        FlowerDexItem item = instance.GetComponent<FlowerDexItem>();
        item.unreadMemorialBadge = badgeGO;

        PrefabUtility.SaveAsPrefabAsset(instance, FLOWER_DEX_ITEM_PREFAB_PATH);
        Object.DestroyImmediate(instance);

        return AssetDatabase.LoadAssetAtPath<FlowerDexItem>(FLOWER_DEX_ITEM_PREFAB_PATH);
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
