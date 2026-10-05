using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 설정 화면 — 도감/메모리얼과 동일한 전체 화면 오버레이(CanvasGroup으로만 보이기/숨기기 전환,
/// 절대 SetActive(false)로 자기 자신을 끄지 않음 — Awake/Start 스킵 문제 재발 방지).
///
/// [확장성] 지금은 "데이터 초기화" 하나뿐이지만, 나중에 BGM/효과음 볼륨, FPS 제한, 튜토리얼
/// 다시보기 등을 추가할 때 이 클래스나 씬 구조를 바꿀 필요가 없다 — ShopPanel/GrowthPanel과 똑같이
/// content는 그냥 세로 스크롤 영역(VerticalLayoutGroup)이라, PCLayoutBuilder.BuildSettingsPanel에서
/// 새 설정 UI(슬라이더/토글/버튼 등)를 content의 자식으로 추가하기만 하면 그대로 목록에 끼어든다.
/// 이 스크립트는 "지금 존재가 확정된 설정 항목"의 동작(데이터 초기화)만 담당하고, 항목 개수가
/// 늘어나는 것 자체에는 관여하지 않는 구조다.
/// </summary>
public class SettingsPanel : MonoBehaviour
{
    public static SettingsPanel Instance { get; private set; }

    [Header("루트 (비워두면 자기 자신 — 절대 SetActive(false)로 끄지 않음)")]
    public GameObject root;
    public Button openButton;
    public Button closeButton;

    [Header("사운드 설정 (BGM / SFX)")]
    public Slider bgmSlider;
    public TMP_Text bgmValueText;
    public Button bgmMuteButton;
    public TMP_Text bgmMuteButtonText;

    public Slider sfxSlider;
    public TMP_Text sfxValueText;
    public Button sfxMuteButton;
    public TMP_Text sfxMuteButtonText;

    [Header("데이터 초기화 — 반드시 확인 화면을 거친 뒤에만 실행된다")]
    public Button resetDataButton;
    public GameObject resetConfirmRoot; // 기본 비활성
    public Button resetConfirmYesButton;
    public Button resetConfirmNoButton;

    [Header("치트 모드 — 빌드된 게임에서도 CheatPanel을 쓸 수 있게 하되, 비밀번호(1204)를 맞혀야만 열린다")]
    public Button cheatButton;
    public GameObject cheatPasswordRoot; // 기본 비활성
    public TMP_InputField cheatPasswordInput;
    public TMP_Text cheatPasswordErrorText;
    public Button cheatPasswordConfirmButton;
    public Button cheatPasswordCancelButton;

    private const string CheatPassword = "1204";

    private CanvasGroup canvasGroup;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (root == null) root = gameObject;
        canvasGroup = root.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = root.AddComponent<CanvasGroup>();
    }

    private void Start()
    {
        if (openButton != null) openButton.onClick.AddListener(Open);
        if (closeButton != null) closeButton.onClick.AddListener(Close);

        if (resetDataButton != null) resetDataButton.onClick.AddListener(ShowResetConfirm);
        if (resetConfirmYesButton != null) resetConfirmYesButton.onClick.AddListener(ConfirmResetData);
        if (resetConfirmNoButton != null) resetConfirmNoButton.onClick.AddListener(HideResetConfirm);

        if (cheatButton != null) cheatButton.onClick.AddListener(ShowCheatPasswordPrompt);
        if (cheatPasswordConfirmButton != null) cheatPasswordConfirmButton.onClick.AddListener(ConfirmCheatPassword);
        if (cheatPasswordCancelButton != null) cheatPasswordCancelButton.onClick.AddListener(HideCheatPasswordPrompt);

        EnsureSoundSettingsUI();
        EnsureTutorialReplayUI();
        SyncSoundUI();

        SetVisible(false);
        if (resetConfirmRoot != null) resetConfirmRoot.SetActive(false);
        if (cheatPasswordRoot != null) cheatPasswordRoot.SetActive(false);
    }

    public void Open()
    {
        HideResetConfirm(); // 예전에 확인창을 띄운 채로 닫았다가 다시 열리는 경우를 방지
        HideCheatPasswordPrompt();
        SyncSoundUI();
        SetVisible(true);
    }

    public void Close() => SetVisible(false);

    private void SetVisible(bool visible)
    {
        if (canvasGroup == null) return;
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }

    private void ShowResetConfirm()
    {
        if (resetConfirmRoot != null) resetConfirmRoot.SetActive(true);
    }

    private void HideResetConfirm()
    {
        if (resetConfirmRoot != null) resetConfirmRoot.SetActive(false);
    }

    /// <summary>
    /// 실제 초기화 실행 — 되돌릴 수 없는 동작이므로 확인 화면("정말요?")을 거친 뒤에만 호출된다.
    /// 초기화 직후엔 설정 화면도 함께 닫아서, 플레이어가 바로 리셋된 메인 화면을 보게 한다.
    /// </summary>
    private void ConfirmResetData()
    {
        if (SaveManager.Instance != null)
            SaveManager.Instance.ResetAllData();

        HideResetConfirm();
        Close();
    }

    private void ShowCheatPasswordPrompt()
    {
        if (cheatPasswordInput != null) cheatPasswordInput.text = "";
        if (cheatPasswordErrorText != null) cheatPasswordErrorText.gameObject.SetActive(false);
        if (cheatPasswordRoot != null) cheatPasswordRoot.SetActive(true);
    }

    private void HideCheatPasswordPrompt()
    {
        if (cheatPasswordRoot != null) cheatPasswordRoot.SetActive(false);
    }

    /// <summary>
    /// 비밀번호(1204)를 맞혀야만 CheatPanel을 연다 — "아무나 쓰는 게 아니라"가 요청 취지라, 틀리면
    /// 조용히 무시하지 않고 에러 문구를 보여준 뒤 입력창을 비운다(재시도 유도).
    /// </summary>
    private void ConfirmCheatPassword()
    {
        string input = cheatPasswordInput != null ? cheatPasswordInput.text : "";
        if (input == CheatPassword)
        {
            HideCheatPasswordPrompt();
            if (CheatPanel.Instance != null) CheatPanel.Instance.Open();
        }
        else
        {
            if (cheatPasswordErrorText != null) cheatPasswordErrorText.gameObject.SetActive(true);
            if (cheatPasswordInput != null) cheatPasswordInput.text = "";
        }
    }

    private void EnsureTutorialReplayUI()
    {
        Transform content = root.transform.Find("Scroll View/Viewport/Content");
        if (content == null)
        {
            VerticalLayoutGroup layout = root.GetComponentInChildren<VerticalLayoutGroup>();
            if (layout != null) content = layout.transform;
        }
        if (content == null || content.Find("TutorialReplayRow") != null) return;

        GameObject row = new GameObject("TutorialReplayRow", typeof(RectTransform), typeof(LayoutElement), typeof(Image), typeof(Button));
        row.transform.SetParent(content, false);
        row.GetComponent<LayoutElement>().preferredHeight = 88f;
        Image background = row.GetComponent<Image>();
        background.color = new Color(0.24f, 0.32f, 0.43f, 1f);
        Button button = row.GetComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(StartTutorialFromSettings);

        TMP_FontAsset font = cheatPasswordErrorText != null ? cheatPasswordErrorText.font : null;
        CreateTutorialReplayText(row.transform, "Title", "튜토리얼 다시 보기", font, 19f, FontStyles.Bold,
            new Vector2(0f, 0.48f), new Vector2(1f, 1f));
        CreateTutorialReplayText(row.transform, "Description", "게임 기본 흐름과 정원 기능 안내를 다시 확인합니다.", font, 13f, FontStyles.Normal,
            new Vector2(0f, 0f), new Vector2(1f, 0.5f));
    }

    private void StartTutorialFromSettings()
    {
        Close();
        TutorialManager.EnsureInstance()?.ReplayFromSettings();
    }

    private static void CreateTutorialReplayText(Transform parent, string objectName, string value, TMP_FontAsset font,
        float fontSize, FontStyles fontStyle, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = new Vector2(18f, 4f);
        rect.offsetMax = new Vector2(-18f, -4f);

        TMP_Text text = textObject.GetComponent<TMP_Text>();
        if (font != null) text.font = font;
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Left;
        text.raycastTarget = false;
    }

    // ===================================================================
    // 사운드 설정 UI 동적 생성 및 바인딩
    // ===================================================================

    private void EnsureSoundSettingsUI()
    {
        if (bgmSlider != null && sfxSlider != null)
        {
            bgmSlider.onValueChanged.AddListener(OnBgmSliderChanged);
            sfxSlider.onValueChanged.AddListener(OnSfxSliderChanged);
            if (bgmMuteButton != null) bgmMuteButton.onClick.AddListener(ToggleBgmMute);
            if (sfxMuteButton != null) sfxMuteButton.onClick.AddListener(ToggleSfxMute);
            return;
        }

        Transform content = root.transform.Find("Scroll View/Viewport/Content");
        if (content == null)
        {
            var vlg = root.GetComponentInChildren<VerticalLayoutGroup>();
            if (vlg != null) content = vlg.transform;
        }

        if (content == null) return;

        TMP_FontAsset font = cheatPasswordErrorText != null ? cheatPasswordErrorText.font : null;

        // BGM 행 생성
        if (bgmSlider == null)
        {
            (bgmSlider, bgmValueText, bgmMuteButton, bgmMuteButtonText) =
                CreateSoundWidgetRow(content, font, "배경음악 (BGM)", "게임 배경음악의 음량을 조절합니다.", new Color(0.2f, 0.45f, 0.75f, 1f));
            bgmSlider.onValueChanged.AddListener(OnBgmSliderChanged);
            bgmMuteButton.onClick.AddListener(ToggleBgmMute);
            bgmSlider.transform.parent.SetSiblingIndex(0); // 목록 최상단 배치
        }

        // SFX 행 생성
        if (sfxSlider == null)
        {
            (sfxSlider, sfxValueText, sfxMuteButton, sfxMuteButtonText) =
                CreateSoundWidgetRow(content, font, "효과음 (SFX)", "꽃 터치, 개화, 레벨업 등 효과음 음량을 조절합니다.", new Color(0.25f, 0.65f, 0.40f, 1f));
            sfxSlider.onValueChanged.AddListener(OnSfxSliderChanged);
            sfxMuteButton.onClick.AddListener(ToggleSfxMute);
            sfxSlider.transform.parent.SetSiblingIndex(1); // 두 번째 배치
        }
    }

    private (Slider slider, TMP_Text valText, Button muteBtn, TMP_Text muteBtnText) CreateSoundWidgetRow(
        Transform parent, TMP_FontAsset font, string title, string description, Color themeColor)
    {
        GameObject row = new GameObject($"Row_{title}");
        row.transform.SetParent(parent, false);

        LayoutElement rowLE = row.AddComponent<LayoutElement>();
        rowLE.preferredHeight = 96f;

        Image rowBg = row.AddComponent<Image>();
        rowBg.color = new Color(1f, 1f, 1f, 0.05f);

        // 1. 좌측 라벨 & 설명
        GameObject titleGO = new GameObject("Title");
        titleGO.transform.SetParent(row.transform, false);
        RectTransform titleRT = titleGO.AddComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0f, 0.5f);
        titleRT.anchorMax = new Vector2(0.52f, 1f);
        titleRT.offsetMin = new Vector2(16f, 4f);
        titleRT.offsetMax = new Vector2(-8f, -8f);

        TMP_Text titleText = titleGO.AddComponent<TextMeshProUGUI>();
        if (font != null) titleText.font = font;
        titleText.text = title;
        titleText.fontSize = 20f;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = Color.white;
        titleText.alignment = TextAlignmentOptions.Left;

        GameObject descGO = new GameObject("Description");
        descGO.transform.SetParent(row.transform, false);
        RectTransform descRT = descGO.AddComponent<RectTransform>();
        descRT.anchorMin = new Vector2(0f, 0f);
        descRT.anchorMax = new Vector2(0.52f, 0.5f);
        descRT.offsetMin = new Vector2(16f, 8f);
        descRT.offsetMax = new Vector2(-8f, -4f);

        TMP_Text descText = descGO.AddComponent<TextMeshProUGUI>();
        if (font != null) descText.font = font;
        descText.text = description;
        descText.fontSize = 12f;
        descText.color = new Color(0.7f, 0.7f, 0.75f, 1f);
        descText.alignment = TextAlignmentOptions.TopLeft;

        // 2. 우측 슬라이더 컨테이너
        GameObject sliderArea = new GameObject("SliderArea");
        sliderArea.transform.SetParent(row.transform, false);
        RectTransform saRT = sliderArea.AddComponent<RectTransform>();
        saRT.anchorMin = new Vector2(0.52f, 0.2f);
        saRT.anchorMax = new Vector2(0.85f, 0.8f);
        saRT.offsetMin = Vector2.zero;
        saRT.offsetMax = Vector2.zero;

        // 슬라이더 루트
        GameObject sliderGO = new GameObject("Slider");
        sliderGO.transform.SetParent(sliderArea.transform, false);
        RectTransform sliderRT = sliderGO.AddComponent<RectTransform>();
        sliderRT.anchorMin = new Vector2(0f, 0.4f);
        sliderRT.anchorMax = new Vector2(0.82f, 0.7f);
        sliderRT.offsetMin = Vector2.zero;
        sliderRT.offsetMax = Vector2.zero;

        Slider slider = sliderGO.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;

        // 배경 트랙
        GameObject bgGO = new GameObject("Background");
        bgGO.transform.SetParent(sliderGO.transform, false);
        RectTransform bgRT = bgGO.AddComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;
        Image bgImg = bgGO.AddComponent<Image>();
        bgImg.color = new Color(0.2f, 0.22f, 0.26f, 1f);

        // 필(Fill) 영역
        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderGO.transform, false);
        RectTransform faRT = fillArea.AddComponent<RectTransform>();
        faRT.anchorMin = Vector2.zero;
        faRT.anchorMax = Vector2.one;
        faRT.offsetMin = Vector2.zero;
        faRT.offsetMax = Vector2.zero;

        GameObject fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(fillArea.transform, false);
        RectTransform fillRT = fillGO.AddComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;
        Image fillImg = fillGO.AddComponent<Image>();
        fillImg.color = themeColor;

        slider.fillRect = fillRT;
        slider.targetGraphic = fillImg;

        // 수치 텍스트 (예: "70%")
        GameObject valGO = new GameObject("ValueText");
        valGO.transform.SetParent(sliderArea.transform, false);
        RectTransform valRT = valGO.AddComponent<RectTransform>();
        valRT.anchorMin = new Vector2(0.84f, 0.2f);
        valRT.anchorMax = new Vector2(1f, 0.8f);
        valRT.offsetMin = Vector2.zero;
        valRT.offsetMax = Vector2.zero;

        TMP_Text valText = valGO.AddComponent<TextMeshProUGUI>();
        if (font != null) valText.font = font;
        valText.fontSize = 14f;
        valText.fontStyle = FontStyles.Bold;
        valText.color = Color.white;
        valText.alignment = TextAlignmentOptions.Center;
        valText.text = "50%";

        // 3. 음소거 토글 버튼
        GameObject muteBtnGO = new GameObject("MuteBtn");
        muteBtnGO.transform.SetParent(row.transform, false);
        RectTransform mbRT = muteBtnGO.AddComponent<RectTransform>();
        mbRT.anchorMin = new Vector2(0.87f, 0.25f);
        mbRT.anchorMax = new Vector2(0.98f, 0.75f);
        mbRT.offsetMin = Vector2.zero;
        mbRT.offsetMax = Vector2.zero;

        Image mbImg = muteBtnGO.AddComponent<Image>();
        mbImg.color = new Color(0.28f, 0.30f, 0.35f, 1f);
        Button muteBtn = muteBtnGO.AddComponent<Button>();
        muteBtn.targetGraphic = mbImg;

        GameObject mbTxtGO = new GameObject("Label");
        mbTxtGO.transform.SetParent(muteBtnGO.transform, false);
        RectTransform mbTxtRT = mbTxtGO.AddComponent<RectTransform>();
        mbTxtRT.anchorMin = Vector2.zero;
        mbTxtRT.anchorMax = Vector2.one;
        mbTxtRT.offsetMin = Vector2.zero;
        mbTxtRT.offsetMax = Vector2.zero;

        TMP_Text muteBtnText = mbTxtGO.AddComponent<TextMeshProUGUI>();
        if (font != null) muteBtnText.font = font;
        muteBtnText.fontSize = 12f;
        muteBtnText.fontStyle = FontStyles.Bold;
        muteBtnText.color = Color.white;
        muteBtnText.alignment = TextAlignmentOptions.Center;
        muteBtnText.text = "음소거";

        return (slider, valText, muteBtn, muteBtnText);
    }

    private void SyncSoundUI()
    {
        if (SoundManager.Instance == null) return;

        if (bgmSlider != null)
        {
            bgmSlider.SetValueWithoutNotify(SoundManager.Instance.bgmVolume);
            if (bgmValueText != null)
                bgmValueText.text = $"{Mathf.RoundToInt(SoundManager.Instance.bgmVolume * 100f)}%";
        }

        if (sfxSlider != null)
        {
            sfxSlider.SetValueWithoutNotify(SoundManager.Instance.sfxVolume);
            if (sfxValueText != null)
                sfxValueText.text = $"{Mathf.RoundToInt(SoundManager.Instance.sfxVolume * 100f)}%";
        }

        if (bgmMuteButtonText != null)
            bgmMuteButtonText.text = SoundManager.Instance.isBgmMuted ? "해제" : "음소거";

        if (sfxMuteButtonText != null)
            sfxMuteButtonText.text = SoundManager.Instance.isSfxMuted ? "해제" : "음소거";
    }

    private void OnBgmSliderChanged(float val)
    {
        if (SoundManager.Instance != null)
            SoundManager.Instance.SetBGMVolume(val);
        if (bgmValueText != null)
            bgmValueText.text = $"{Mathf.RoundToInt(val * 100f)}%";
    }

    private void OnSfxSliderChanged(float val)
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.SetSFXVolume(val);
            SoundManager.Instance.PlayTapSound(false); // 볼륨 확인용 즉각 피드백음 재생
        }
        if (sfxValueText != null)
            sfxValueText.text = $"{Mathf.RoundToInt(val * 100f)}%";
    }

    private void ToggleBgmMute()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.SetBGMMuted(!SoundManager.Instance.isBgmMuted);
            SyncSoundUI();
        }
    }

    private void ToggleSfxMute()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.SetSFXMuted(!SoundManager.Instance.isSfxMuted);
            SyncSoundUI();
        }
    }
}
