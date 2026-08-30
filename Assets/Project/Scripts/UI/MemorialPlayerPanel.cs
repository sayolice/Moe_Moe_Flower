using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 메모리얼(MemorialData.lines) 재생기. 다른 프로젝트의 대화 시스템(Assets/Reference/
/// EventManager_reference.cs.txt, 참고용 — 그대로 옮기지 않음)에서 다음 패턴만 이식했다:
///   - ProcessLineDirectives의 인라인 태그 파싱 방식
///   - FadeInSlot/FadeOutSlot/SetSlotFocus/Co_FadeImage
///   - 세션 로그(currentSessionLog/ToggleVNLog/RefreshLogPanel)
///   - 스킵(Co_SkipRoutine), 탭 쿨다운(lastTapTime/tapCooldown 0.2초)
/// 그 프로젝트 전용 시스템(선택지, ResourceManager/RelationManager/DayManager/LoopManager,
/// StoryFlagManager, 채팅 버블 모드, {loop}/{LAST_DEATH} 토큰)은 전부 뺐다 — 메모리얼은 선형
/// 텍스트만 재생하는 훨씬 단순한 용도라 그 의존성들이 필요 없다.
///
/// 캐릭터 슬롯은 지금은 Center 1개뿐이지만, 참고 코드처럼 Left/Right를 나중에 추가할 때 이
/// 클래스의 구조(슬롯별 Image 필드 + Fade*/GetSlotImage 헬퍼 + ProcessLineDirectives의 태그
/// 분기)를 그대로 복제해서 확장할 수 있게 짜여 있다.
///
/// CanvasGroup으로 보이기/숨기기를 전환한다(이 프로젝트 전역 관례).
/// </summary>
public class MemorialPlayerPanel : MonoBehaviour
{
    public static MemorialPlayerPanel Instance { get; private set; }

    [Header("루트")]
    public GameObject root;

    [Header("스프라이트 데이터베이스 (키로 조회 — 참고 코드의 globalCharacterSprites 패턴)")]
    public List<MemorialSpriteEntry> characterSprites = new List<MemorialSpriteEntry>();
    public List<MemorialSpriteEntry> backgroundSprites = new List<MemorialSpriteEntry>();
    public List<MemorialSpriteEntry> cgSprites = new List<MemorialSpriteEntry>();

    [Header("화면")]
    public Image backgroundImage;
    public Image cgImage;               // 전체화면 CG — 배경/캐릭터 위를 덮는다(텍스트 박스는 그 위)
    public Image centerCharacterImage;  // Center 슬롯 1개(확장 가능한 구조 — 클래스 설명 참고)
    public TMP_Text speakerNameText;
    public TMP_Text dialogueText;
    public Button screenTapButton;      // 화면 전체를 덮는 투명 버튼 — 탭하면 다음 줄로 진행

    [Header("로그")]
    public GameObject logPanelRoot;
    public Transform logContent;
    public Button logToggleButton;
    public Button logCloseButton;

    [Header("스킵 / 닫기")]
    public Button skipButton;
    public TMP_Text skipButtonText;
    public Button closeButton; // 중간에 나가기 — 완료 처리(읽음 표시)는 하지 않는다

    [Header("연출 설정 (밸런스 값 아님 — 엔지니어링 기본값)")]
    public float fadeDuration = 0.3f;
    public float skipDelay = 0.05f;
    public Color focusColor = Color.white;
    public Color dimColor = new Color(0.55f, 0.55f, 0.55f, 1f);

    private readonly Dictionary<string, Sprite> characterDict = new Dictionary<string, Sprite>();
    private readonly Dictionary<string, Sprite> backgroundDict = new Dictionary<string, Sprite>();
    private readonly Dictionary<string, Sprite> cgDict = new Dictionary<string, Sprite>();

    private CanvasGroup canvasGroup;
    private List<string> currentLines;
    private int lineIndex;
    private bool isShowingLines;
    private Action<bool> onFinished; // true = 끝까지 재생 완료, false = 중간에 닫음

    private readonly List<string> currentSessionLog = new List<string>();
    private bool isSkipping;
    private Coroutine skipCoroutine;
    private Coroutine centerFadeCoroutine;
    private Coroutine cgFadeCoroutine;

    private float lastTapTime;
    private const float TapCooldown = 0.2f;

    /// <summary> Center 슬롯에 캐릭터가 "논리적으로" 등장해 있는지 — CG가 그 위를 덮으려고
    /// GameObject를 꺼둔 동안에도 이 값은 true로 유지된다(진짜 퇴장은 EXIT_C일 때만 false).
    /// UpdateCenterFocus/FadeOutCenter가 GameObject.activeSelf 대신 이 값으로 판단해야, CG_OFF 때
    /// [ENTER_C]를 다시 안 불러도 이전 스프라이트·색이 그대로 복귀한다. </summary>
    private bool centerHasCharacter;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (root == null) root = gameObject;
        canvasGroup = root.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = root.AddComponent<CanvasGroup>();

        BuildDict(characterSprites, characterDict);
        BuildDict(backgroundSprites, backgroundDict);
        BuildDict(cgSprites, cgDict);
    }

    private void Start()
    {
        if (screenTapButton != null) screenTapButton.onClick.AddListener(OnScreenTapped);
        if (closeButton != null) closeButton.onClick.AddListener(HandleCloseClicked);
        if (logToggleButton != null) logToggleButton.onClick.AddListener(ToggleLog);
        if (logCloseButton != null) logCloseButton.onClick.AddListener(ToggleLog);
        if (skipButton != null) skipButton.onClick.AddListener(ToggleSkip);

        SetVisible(false);
        if (logPanelRoot != null) logPanelRoot.SetActive(false);
    }

    private static void BuildDict(List<MemorialSpriteEntry> entries, Dictionary<string, Sprite> dict)
    {
        dict.Clear();
        if (entries == null) return;
        foreach (MemorialSpriteEntry e in entries)
        {
            if (e == null || string.IsNullOrEmpty(e.key) || e.sprite == null) continue;
            dict[e.key] = e.sprite;
        }
    }

    /// <summary>
    /// 재생 시작. lines가 비어 있으면 즉시 완료 콜백을 부른다(호출자가 방어적으로 체크하지만,
    /// 여기서도 한 번 더 막아 둔다). onFinished(true)는 끝까지 재생했을 때, onFinished(false)는
    /// "닫기" 버튼으로 중간에 나갔을 때 — 읽음 처리 여부는 호출자(MemorialViewPanel)가 이 값으로
    /// 결정한다.
    /// </summary>
    public void Play(List<string> lines, Action<bool> onFinished)
    {
        if (lines == null || lines.Count == 0)
        {
            onFinished?.Invoke(false);
            return;
        }

        currentLines = lines;
        lineIndex = 0;
        this.onFinished = onFinished;
        currentSessionLog.Clear();
        isShowingLines = true;
        StopSkip();

        ResetVisualState();
        SetVisible(true);
        ShowNextLine();
    }

    /// <summary> 배경/CG/캐릭터/화자 이름을 전부 빈 상태로 되돌린다 — 이전 회에서 켜 둔 CG 등이
    /// 다음 재생에 그대로 남아있지 않게 한다. </summary>
    private void ResetVisualState()
    {
        if (centerFadeCoroutine != null) { StopCoroutine(centerFadeCoroutine); centerFadeCoroutine = null; }
        if (cgFadeCoroutine != null) { StopCoroutine(cgFadeCoroutine); cgFadeCoroutine = null; }

        centerHasCharacter = false;
        if (centerCharacterImage != null) centerCharacterImage.gameObject.SetActive(false);
        if (cgImage != null) cgImage.gameObject.SetActive(false);
        if (backgroundImage != null) backgroundImage.gameObject.SetActive(false);
        if (speakerNameText != null) speakerNameText.text = "";
        if (dialogueText != null) dialogueText.text = "";
        if (logPanelRoot != null) logPanelRoot.SetActive(false);
    }

    /// <summary> 탭 쿨다운(0.2초) — 연속 탭/더블탭으로 두 줄이 한꺼번에 넘어가는 것을 막는다. </summary>
    private void OnScreenTapped()
    {
        if (Time.time - lastTapTime < TapCooldown) return;
        lastTapTime = Time.time;

        if (logPanelRoot != null && logPanelRoot.activeSelf) return; // 로그를 보는 중엔 진행 무시
        if (!isShowingLines) return;
        ShowNextLine();
    }

    private void ShowNextLine()
    {
        if (currentLines == null || lineIndex >= currentLines.Count)
        {
            Finish(true);
            return;
        }

        string raw = currentLines[lineIndex];
        lineIndex++;
        string parsed = ProcessLineDirectives(raw);

        if (dialogueText != null)
            dialogueText.text = parsed + "  <color=#FFD700>▼</color>";

        AppendLogEntry(parsed);
    }

    private void Finish(bool completed)
    {
        isShowingLines = false;
        StopSkip();

        Action<bool> callback = onFinished;
        onFinished = null;
        SetVisible(false);
        callback?.Invoke(completed);
    }

    private void HandleCloseClicked() => Finish(false);

    /// <summary>
    /// 인라인 태그 파싱 — 참고 코드 ProcessLineDirectives의 while([...]) 루프 방식을 그대로 쓰되,
    /// 이 프로젝트의 태그 집합([C]/[N]/[ENTER_C]/[EXIT_C]/[BG]/[CG]/[CG_OFF])만 처리한다. 화자 이름은
    /// 태그가 없으면 이전 줄의 화자를 그대로 유지한다(매 줄 반복 지정할 필요 없음).
    /// </summary>
    private string ProcessLineDirectives(string rawLine)
    {
        if (string.IsNullOrEmpty(rawLine)) return rawLine;

        bool speakerChanged = false;
        string speakerName = speakerNameText != null ? speakerNameText.text : "";
        bool speakerIsCenter = false;

        while (rawLine.StartsWith("[") && rawLine.Contains("]"))
        {
            int closeIdx = rawLine.IndexOf(']');
            string tag = rawLine.Substring(1, closeIdx - 1).Trim();
            rawLine = rawLine.Substring(closeIdx + 1);

            string[] parts = tag.Split(':');
            string cmd = parts[0].Trim().ToUpperInvariant();
            string arg = parts.Length > 1 ? parts[1].Trim() : "";

            switch (cmd)
            {
                case "C":
                    speakerName = arg;
                    speakerChanged = true;
                    speakerIsCenter = true;
                    break;
                case "N":
                    speakerName = "";
                    speakerChanged = true;
                    speakerIsCenter = false;
                    break;
                case "ENTER_C":
                    FadeInCenter(GetSprite(characterDict, arg));
                    break;
                case "EXIT_C":
                    FadeOutCenter();
                    break;
                case "BG":
                    SetBackground(arg);
                    break;
                case "CG":
                    ShowCG(arg);
                    break;
                case "CG_OFF":
                    HideCG();
                    break;
            }
        }

        if (speakerChanged)
        {
            if (speakerNameText != null) speakerNameText.text = speakerName;
            UpdateCenterFocus(speakerIsCenter);
        }

        return rawLine;
    }

    private static Sprite GetSprite(Dictionary<string, Sprite> dict, string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        return dict.TryGetValue(key, out Sprite s) ? s : null;
    }

    /// <summary> 슬롯이 지금 대사 중인지에 따라 밝기만 바꾼다(참고 코드의 SetSlotFocus) — 알파(페이드
    /// 진행도)는 건드리지 않고 색상만 바꿔서 페이드 애니메이션과 겹쳐도 서로 간섭하지 않는다. </summary>
    private void UpdateCenterFocus(bool isSpeaking)
    {
        if (centerCharacterImage == null || !centerHasCharacter) return;
        Color baseColor = isSpeaking ? focusColor : dimColor;
        float currentAlpha = centerCharacterImage.color.a;
        centerCharacterImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, currentAlpha);
    }

    private void FadeInCenter(Sprite sprite)
    {
        if (sprite == null || centerCharacterImage == null) return;
        if (centerFadeCoroutine != null) { StopCoroutine(centerFadeCoroutine); centerFadeCoroutine = null; }

        centerHasCharacter = true;
        centerCharacterImage.sprite = sprite;
        Color c = focusColor;

        // CG가 이미 화면을 덮고 있는 중이면(드문 순서지만 방어적으로 처리) 화면엔 보이지 않게
        // 슬롯을 꺼둔 채로 스프라이트/색만 "등장 완료" 상태로 맞춰 둔다 — 나중에 [CG_OFF]가
        // 되면 이 상태 그대로(재-ENTER_C 없이) 나타난다.
        bool cgShown = cgImage != null && cgImage.gameObject.activeSelf;
        if (cgShown)
        {
            centerCharacterImage.color = new Color(c.r, c.g, c.b, 1f);
            centerCharacterImage.gameObject.SetActive(false);
            return;
        }

        centerCharacterImage.color = new Color(c.r, c.g, c.b, 0f);
        centerCharacterImage.gameObject.SetActive(true);

        if (isSkipping || fadeDuration <= 0f)
            centerCharacterImage.color = new Color(c.r, c.g, c.b, 1f);
        else
            centerFadeCoroutine = StartCoroutine(Co_FadeImage(centerCharacterImage, 0f, 1f, fadeDuration));
    }

    private void FadeOutCenter()
    {
        if (centerCharacterImage == null || !centerHasCharacter) return;
        if (centerFadeCoroutine != null) { StopCoroutine(centerFadeCoroutine); centerFadeCoroutine = null; }

        centerHasCharacter = false;

        // CG에 가려져 애초에 화면엔 안 보이던 상태면 페이드 없이 그냥 꺼두면 된다.
        bool cgShown = cgImage != null && cgImage.gameObject.activeSelf;
        if (cgShown)
        {
            centerCharacterImage.gameObject.SetActive(false);
            return;
        }

        if (isSkipping || fadeDuration <= 0f)
        {
            Color c = centerCharacterImage.color;
            centerCharacterImage.color = new Color(c.r, c.g, c.b, 0f);
            centerCharacterImage.gameObject.SetActive(false);
        }
        else
        {
            Image img = centerCharacterImage;
            centerFadeCoroutine = StartCoroutine(Co_FadeImage(img, img.color.a, 0f, fadeDuration,
                () => img.gameObject.SetActive(false)));
        }
    }

    /// <summary> 배경은 페이드 없이 즉시 교체한다(요청 범위 — CG만 명시적으로 페이드 대상). </summary>
    private void SetBackground(string key)
    {
        Sprite sprite = GetSprite(backgroundDict, key);
        if (backgroundImage == null || sprite == null) return;
        backgroundImage.sprite = sprite;
        backgroundImage.gameObject.SetActive(true);
    }

    /// <summary> CG는 화면 전체(배경+캐릭터)를 덮고, 표시/해제 둘 다 페이드 처리한다(요청 사항).
    /// 캐릭터 슬롯은 CG 페이드인이 "완료된 뒤"에 꺼야 자연스럽다 — 페이드 도중엔 CG가 반투명이라
    /// 캐릭터가 비치는 크로스페이드처럼 보이고, 다 덮인 다음에 슬롯을 꺼야 낭비 렌더링 없이
    /// 깜빡임도 없다(sprite/color는 절대 건드리지 않으므로 CG_OFF 때 그대로 복귀한다). </summary>
    private void ShowCG(string key)
    {
        Sprite sprite = GetSprite(cgDict, key);
        if (sprite == null || cgImage == null) return;
        if (cgFadeCoroutine != null) { StopCoroutine(cgFadeCoroutine); cgFadeCoroutine = null; }

        cgImage.sprite = sprite;
        cgImage.gameObject.SetActive(true);

        if (isSkipping || fadeDuration <= 0f)
        {
            Color c = cgImage.color;
            cgImage.color = new Color(c.r, c.g, c.b, 1f);
            HideCenterUnderCG();
        }
        else
        {
            Color c = cgImage.color;
            cgImage.color = new Color(c.r, c.g, c.b, 0f);
            cgFadeCoroutine = StartCoroutine(Co_FadeImage(cgImage, 0f, 1f, fadeDuration, HideCenterUnderCG));
        }
    }

    /// <summary> sprite/color는 그대로 두고 GameObject만 끈다 — CG_OFF 때 재-ENTER_C 없이 그대로
    /// 복귀시키기 위함. </summary>
    private void HideCenterUnderCG()
    {
        if (centerCharacterImage != null && centerHasCharacter)
            centerCharacterImage.gameObject.SetActive(false);
    }

    private void HideCG()
    {
        if (cgImage == null || !cgImage.gameObject.activeSelf) return;
        if (cgFadeCoroutine != null) { StopCoroutine(cgFadeCoroutine); cgFadeCoroutine = null; }

        // CG가 걷히기 "시작"할 때 캐릭터를 먼저 되살려 둬야, CG 알파가 줄어들며 캐릭터가 자연스럽게
        // 크로스페이드로 드러난다 — 다 걷힌 뒤에 갑자기 팝인하면 부자연스럽다.
        if (centerCharacterImage != null && centerHasCharacter)
            centerCharacterImage.gameObject.SetActive(true);

        if (isSkipping || fadeDuration <= 0f)
        {
            Color c = cgImage.color;
            cgImage.color = new Color(c.r, c.g, c.b, 0f);
            cgImage.gameObject.SetActive(false);
        }
        else
        {
            Image img = cgImage;
            cgFadeCoroutine = StartCoroutine(Co_FadeImage(img, img.color.a, 0f, fadeDuration,
                () => img.gameObject.SetActive(false)));
        }
    }

    private IEnumerator Co_FadeImage(Image img, float startAlpha, float targetAlpha, float duration, Action onComplete = null)
    {
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float alpha = Mathf.Lerp(startAlpha, targetAlpha, timer / duration);
            Color c = img.color;
            img.color = new Color(c.r, c.g, c.b, alpha);
            yield return null;
        }

        Color finalC = img.color;
        img.color = new Color(finalC.r, finalC.g, finalC.b, targetAlpha);
        onComplete?.Invoke();
    }

    // ===================================================================
    // 세션 로그
    // ===================================================================

    private void AppendLogEntry(string parsedLine)
    {
        string speaker = speakerNameText != null ? speakerNameText.text : "";
        currentSessionLog.Add(string.IsNullOrEmpty(speaker) ? parsedLine : $"<b>{speaker}</b> : {parsedLine}");
    }

    public void ToggleLog()
    {
        if (logPanelRoot == null) return;

        bool willShow = !logPanelRoot.activeSelf;
        logPanelRoot.SetActive(willShow);

        if (willShow)
        {
            StopSkip();
            RefreshLogPanel();
        }
    }

    private void RefreshLogPanel()
    {
        if (logContent == null) return;

        for (int i = logContent.childCount - 1; i >= 0; i--)
            Destroy(logContent.GetChild(i).gameObject);

        TMP_FontAsset font = dialogueText != null ? dialogueText.font : null;

        foreach (string entry in currentSessionLog)
        {
            GameObject lineGO = new GameObject("LogLine", typeof(RectTransform));
            lineGO.transform.SetParent(logContent, false);

            TMP_Text text = lineGO.AddComponent<TextMeshProUGUI>();
            text.text = entry;
            text.fontSize = 20f;
            text.color = Color.white;
            text.enableWordWrapping = true;
            if (font != null) text.font = font;

            LayoutElement le = lineGO.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
        }
    }

    // ===================================================================
    // 스킵
    // ===================================================================

    public void ToggleSkip()
    {
        isSkipping = !isSkipping;
        if (skipButtonText != null) skipButtonText.text = isSkipping ? "스킵 중.." : "스킵";

        if (isSkipping)
        {
            if (skipCoroutine != null) StopCoroutine(skipCoroutine);
            skipCoroutine = StartCoroutine(Co_SkipRoutine());
        }
        else
        {
            StopSkip();
        }
    }

    public void StopSkip()
    {
        isSkipping = false;
        if (skipButtonText != null) skipButtonText.text = "스킵";
        if (skipCoroutine != null)
        {
            StopCoroutine(skipCoroutine);
            skipCoroutine = null;
        }
    }

    private IEnumerator Co_SkipRoutine()
    {
        while (isSkipping && isShowingLines)
        {
            if (logPanelRoot != null && logPanelRoot.activeSelf)
            {
                StopSkip();
                yield break;
            }

            ShowNextLine();
            yield return new WaitForSeconds(skipDelay);
        }

        StopSkip();
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup == null) return;
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }
}
