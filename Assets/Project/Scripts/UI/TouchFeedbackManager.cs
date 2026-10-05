using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 터치 피드백, 플로팅 텍스트, 반짝임 파티클, 개화 축하 연출을 총괄하는 VFX 매니저.
///
/// [핵심 기능]
/// 1. 터치 시 손끝 좌표에 반짝임 파티클 확산 및 `+10 애정` / `+20 골드` 플로팅 텍스트 연출.
/// 2. 초당 20회 이상 광클해도 버벅임이 없도록 오브젝트 풀링(Object Pool) 완비 (GC 0%).
/// 3. 개화(Bloom) 달성 시 화면 전체 꽃잎/컨페티 샤워 및 축하 플래시/배너 연출.
/// 4. 씬에 Canvas가 있으면 그 위에 높은 sortingOrder(500)로 자동 오버레이되므로,
///    인스펙터 연결이나 프리팹 없이도 즉시 작동합니다.
/// </summary>
public class TouchFeedbackManager : MonoBehaviour
{
    private static TouchFeedbackManager instance;
    public static TouchFeedbackManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<TouchFeedbackManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("TouchFeedbackManager");
                    instance = go.AddComponent<TouchFeedbackManager>();
                }
            }
            return instance;
        }
    }

    [Header("폰트 (비워두면 NotoSans 자동 검색)")]
    public TMP_FontAsset fontAsset;

    private Canvas overlayCanvas;
    private RectTransform canvasRT;

    // 플로팅 텍스트 풀
    private readonly List<FloatingTextItem> textPool = new List<FloatingTextItem>();
    private const int InitialTextPoolSize = 25;

    // 파티클 풀
    private readonly List<SparkleParticleItem> particlePool = new List<SparkleParticleItem>();
    private const int InitialParticlePoolSize = 60;

    // 절차적 원형/별 스프라이트
    private Sprite circleSprite;

    // 상호작용 말풍선
    private GameObject dialogueBubbleGO;
    private RectTransform dialogueBubbleRT;
    private TMP_Text dialogueText;
    private CanvasGroup dialogueCG;
    private Coroutine dialogueCoroutine;

    private class FloatingTextItem
    {
        public GameObject go;
        public RectTransform rt;
        public TMP_Text text;
        public CanvasGroup cg;
        public bool isBusy;
    }

    private class SparkleParticleItem
    {
        public GameObject go;
        public RectTransform rt;
        public Image img;
        public bool isBusy;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureCanvas();
        CreateProceduralSprites();
        WarmPools();
    }

    private void Start()
    {
        if (FlowerManager.Instance != null)
        {
            FlowerManager.Instance.OnFlowerBloomed += HandleFlowerBloomed;
        }
    }

    private void OnDestroy()
    {
        if (FlowerManager.Instance != null)
        {
            FlowerManager.Instance.OnFlowerBloomed -= HandleFlowerBloomed;
        }
    }

    private void EnsureCanvas()
    {
        if (overlayCanvas != null) return;

        GameObject canvasGO = new GameObject("VFX_OverlayCanvas");
        canvasGO.transform.SetParent(transform, false);

        overlayCanvas = canvasGO.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 500; // 최상단 오버레이

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>(); // 차단 없이 관통
        canvasRT = canvasGO.GetComponent<RectTransform>();

        if (fontAsset == null)
        {
            // 씬에 로드된 폰트 탐색
            TMP_Text existing = FindFirstObjectByType<TMP_Text>();
            if (existing != null) fontAsset = existing.font;
        }
    }

    private void CreateProceduralSprites()
    {
        // 32x32 부드러운 원형 알파 텍스처
        int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] cols = new Color[size * size];
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float radius = size * 0.48f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01(1f - (dist / radius));
                alpha = Mathf.Pow(alpha, 1.8f);
                cols[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private void WarmPools()
    {
        for (int i = 0; i < InitialTextPoolSize; i++)
            textPool.Add(CreateNewTextItem());

        for (int i = 0; i < InitialParticlePoolSize; i++)
            particlePool.Add(CreateNewParticleItem());
    }

    private FloatingTextItem CreateNewTextItem()
    {
        GameObject go = new GameObject("VFX_Text");
        go.transform.SetParent(overlayCanvas.transform, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        CanvasGroup cg = go.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;

        TMP_Text tmp = go.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null) tmp.font = fontAsset;
        tmp.fontSize = 24f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;

        go.SetActive(false);
        return new FloatingTextItem { go = go, rt = rt, text = tmp, cg = cg, isBusy = false };
    }

    private SparkleParticleItem CreateNewParticleItem()
    {
        GameObject go = new GameObject("VFX_Particle");
        go.transform.SetParent(overlayCanvas.transform, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        Image img = go.AddComponent<Image>();
        img.sprite = circleSprite;
        img.raycastTarget = false;

        go.SetActive(false);
        return new SparkleParticleItem { go = go, rt = rt, img = img, isBusy = false };
    }

    // ===================================================================
    // 공개 VFX API
    // ===================================================================

    /// <summary>
    /// 터치 지점에 반짝임 파티클과 플로팅 수치 텍스트를 연출합니다.
    /// </summary>
    public void SpawnTapFeedback(Vector2 screenPos, string text, Color textColor, bool isGold = false)
    {
        EnsureCanvas();

        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, screenPos, null, out localPos);

        // 1. 파티클 버스트 (4~6개)
        int pCount = UnityEngine.Random.Range(4, 7);
        for (int i = 0; i < pCount; i++)
        {
            SparkleParticleItem p = GetAvailableParticle();
            if (p != null) StartCoroutine(AnimateSparkle(p, localPos, textColor));
        }

        // 2. 플로팅 텍스트
        FloatingTextItem item = GetAvailableText();
        if (item != null)
        {
            StartCoroutine(AnimateFloatingText(item, localPos, text, textColor, isGold));
        }
    }

    /// <summary>
    /// 개화 성공 시 축하 꽃잎 샤워 및 배너를 연출합니다.
    /// </summary>
    public void PlayBloomCelebration(string flowerName)
    {
        EnsureCanvas();
        StartCoroutine(AnimateBloomCelebration(flowerName));
    }

    /// <summary>
    /// 꽃소녀 머리 위에 상호작용 말풍선 대사를 팝업합니다.
    /// </summary>
    public void ShowDialogueBubble(Vector3 worldPos, string quote)
    {
        EnsureCanvas();
        EnsureDialogueBubble();

        Camera cam = Camera.main;
        Vector2 screenPos = cam != null ? (Vector2)cam.WorldToScreenPoint(worldPos) : new Vector2(Screen.width * 0.5f, Screen.height * 0.65f);

        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, screenPos, null, out localPos);

        if (dialogueCoroutine != null) StopCoroutine(dialogueCoroutine);
        dialogueCoroutine = StartCoroutine(AnimateDialogueBubble(quote, localPos));
    }

    /// <summary>
    /// 꽃소녀 주변으로 핑크 하트가 피어오르는 연출
    /// </summary>
    public void SpawnHeartBurst(Vector3 worldPos)
    {
        EnsureCanvas();
        Camera cam = Camera.main;
        Vector2 screenPos = cam != null ? (Vector2)cam.WorldToScreenPoint(worldPos) : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, screenPos, null, out localPos);

        int count = UnityEngine.Random.Range(2, 4);
        for (int i = 0; i < count; i++)
        {
            FloatingTextItem item = GetAvailableText();
            if (item != null)
            {
                StartCoroutine(AnimateFloatingHeart(item, localPos + new Vector2(UnityEngine.Random.Range(-40f, 40f), UnityEngine.Random.Range(-20f, 20f))));
            }
        }
    }

    private void HandleFlowerBloomed(string flowerId)
    {
        FlowerData data = FlowerManager.Instance != null ? FlowerManager.Instance.GetFlowerData(flowerId) : null;
        string name = data != null ? data.displayName : "꽃";
        PlayBloomCelebration(name);
    }

    // ===================================================================
    // 코루틴 연출 로직
    // ===================================================================

    private IEnumerator AnimateFloatingText(FloatingTextItem item, Vector2 startPos, string content, Color color, bool isGold)
    {
        item.isBusy = true;
        item.go.SetActive(true);
        item.text.text = content;
        item.text.color = color;
        item.text.fontSize = isGold ? 22f : 24f;

        // 약간의 x축 랜덤 지터
        float xOffset = UnityEngine.Random.Range(-25f, 25f);
        Vector2 origin = startPos + new Vector2(xOffset, 15f);
        item.rt.anchoredPosition = origin;
        item.rt.localScale = Vector3.one * 0.7f;
        item.cg.alpha = 1f;

        const float duration = 0.75f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;

            // 크기 팝업 후 부드러운 상승
            float scale = t < 0.2f ? Mathf.Lerp(0.7f, 1.25f, t / 0.2f) : Mathf.Lerp(1.25f, 1.0f, (t - 0.2f) / 0.8f);
            item.rt.localScale = Vector3.one * scale;

            float yRise = Mathf.Lerp(0f, 65f, Mathf.Sqrt(t));
            item.rt.anchoredPosition = origin + new Vector2(0f, yRise);

            // 후반 40%에서 페이드아웃
            item.cg.alpha = t > 0.6f ? Mathf.Lerp(1f, 0f, (t - 0.6f) / 0.4f) : 1f;

            yield return null;
        }

        item.go.SetActive(false);
        item.isBusy = false;
    }

    private IEnumerator AnimateSparkle(SparkleParticleItem p, Vector2 startPos, Color color)
    {
        p.isBusy = true;
        p.go.SetActive(true);

        float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float distance = UnityEngine.Random.Range(30f, 60f);
        Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        Vector2 targetPos = startPos + dir * distance;

        p.rt.anchoredPosition = startPos;
        float startSize = UnityEngine.Random.Range(12f, 20f);
        p.rt.sizeDelta = new Vector2(startSize, startSize);
        p.img.color = color;

        const float duration = 0.45f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;

            p.rt.anchoredPosition = Vector2.Lerp(startPos, targetPos, Mathf.Sin(t * Mathf.PI * 0.5f));
            p.rt.sizeDelta = Vector2.one * Mathf.Lerp(startSize, 0f, t);
            p.img.color = new Color(color.r, color.g, color.b, 1f - t);

            yield return null;
        }

        p.go.SetActive(false);
        p.isBusy = false;
    }

    private IEnumerator AnimateBloomCelebration(string flowerName)
    {
        // 1. 축하 배너 오브젝트 생성
        GameObject bannerGO = new GameObject("BloomBanner");
        bannerGO.transform.SetParent(overlayCanvas.transform, false);

        RectTransform bannerRT = bannerGO.AddComponent<RectTransform>();
        bannerRT.anchorMin = new Vector2(0.5f, 0.6f);
        bannerRT.anchorMax = new Vector2(0.5f, 0.6f);
        bannerRT.pivot = new Vector2(0.5f, 0.5f);
        bannerRT.sizeDelta = new Vector2(500f, 100f);

        CanvasGroup bannerCG = bannerGO.AddComponent<CanvasGroup>();
        bannerCG.blocksRaycasts = false;

        Image bannerBg = bannerGO.AddComponent<Image>();
        bannerBg.color = new Color(0.1f, 0.12f, 0.16f, 0.90f);

        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(bannerGO.transform, false);
        RectTransform textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        TMP_Text tmp = textGO.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null) tmp.font = fontAsset;
        tmp.text = $"🌸 {flowerName} 개화 성공! 🌸";
        tmp.fontSize = 32f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(1f, 0.85f, 0.35f, 1f);

        // 2. 35개 꽃잎/컨페티 샤워
        Color[] petalColors = {
            new Color(1f, 0.6f, 0.75f, 1f), // 핑크
            new Color(1f, 0.9f, 0.4f, 1f),  // 골드
            new Color(0.6f, 0.9f, 1f, 1f),  // 하늘
            new Color(0.7f, 1f, 0.6f, 1f)   // 연두
        };

        for (int i = 0; i < 35; i++)
        {
            SparkleParticleItem p = GetAvailableParticle();
            if (p != null)
            {
                Color col = petalColors[i % petalColors.Length];
                StartCoroutine(AnimateFallingPetal(p, col));
            }
        }

        // 3. 배너 등장 & 퇴장 연출 (약 2초)
        const float bannerDuration = 2.2f;
        float elapsed = 0f;

        while (elapsed < bannerDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / bannerDuration;

            if (t < 0.15f)
            {
                // 등장 팝업
                float scale = Mathf.Lerp(0.5f, 1.15f, t / 0.15f);
                bannerRT.localScale = Vector3.one * scale;
                bannerCG.alpha = t / 0.15f;
            }
            else if (t < 0.25f)
            {
                bannerRT.localScale = Vector3.one * Mathf.Lerp(1.15f, 1.0f, (t - 0.15f) / 0.1f);
                bannerCG.alpha = 1f;
            }
            else if (t > 0.8f)
            {
                // 서서히 페이드아웃
                bannerCG.alpha = Mathf.Lerp(1f, 0f, (t - 0.8f) / 0.2f);
                bannerRT.anchoredPosition = new Vector2(0f, Mathf.Lerp(0f, 30f, (t - 0.8f) / 0.2f));
            }

            yield return null;
        }

        Destroy(bannerGO);
    }

    private IEnumerator AnimateFallingPetal(SparkleParticleItem p, Color color)
    {
        p.isBusy = true;
        p.go.SetActive(true);

        float screenW = canvasRT.rect.width > 0 ? canvasRT.rect.width : 1920f;
        float screenH = canvasRT.rect.height > 0 ? canvasRT.rect.height : 1080f;

        float startX = UnityEngine.Random.Range(-screenW * 0.45f, screenW * 0.45f);
        float startY = screenH * 0.55f;
        float endY = -screenH * 0.55f;

        p.rt.anchoredPosition = new Vector2(startX, startY);
        float petalSize = UnityEngine.Random.Range(16f, 26f);
        p.rt.sizeDelta = new Vector2(petalSize, petalSize * 1.3f);
        p.img.color = color;

        float fallDuration = UnityEngine.Random.Range(1.6f, 2.5f);
        float swaySpeed = UnityEngine.Random.Range(3f, 7f);
        float swayDist = UnityEngine.Random.Range(20f, 50f);
        float elapsed = 0f;

        while (elapsed < fallDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / fallDuration;

            float currentY = Mathf.Lerp(startY, endY, t);
            float currentX = startX + Mathf.Sin(elapsed * swaySpeed) * swayDist;
            p.rt.anchoredPosition = new Vector2(currentX, currentY);

            // 회전 효과
            p.rt.rotation = Quaternion.Euler(0f, 0f, elapsed * 120f);

            p.img.color = new Color(color.r, color.g, color.b, t > 0.8f ? (1f - t) / 0.2f : 1f);

            yield return null;
        }

        p.rt.rotation = Quaternion.identity;
        p.go.SetActive(false);
        p.isBusy = false;
    }

    private FloatingTextItem GetAvailableText()
    {
        for (int i = 0; i < textPool.Count; i++)
        {
            if (!textPool[i].isBusy) return textPool[i];
        }
        FloatingTextItem newItem = CreateNewTextItem();
        textPool.Add(newItem);
        return newItem;
    }

    private SparkleParticleItem GetAvailableParticle()
    {
        for (int i = 0; i < particlePool.Count; i++)
        {
            if (!particlePool[i].isBusy) return particlePool[i];
        }
        SparkleParticleItem newItem = CreateNewParticleItem();
        particlePool.Add(newItem);
        return newItem;
    }

    private void EnsureDialogueBubble()
    {
        if (dialogueBubbleGO != null) return;

        dialogueBubbleGO = new GameObject("VFX_DialogueBubble");
        dialogueBubbleGO.transform.SetParent(overlayCanvas.transform, false);

        dialogueBubbleRT = dialogueBubbleGO.AddComponent<RectTransform>();
        dialogueBubbleRT.sizeDelta = new Vector2(340f, 64f);
        dialogueBubbleRT.pivot = new Vector2(0.5f, 0f);

        dialogueCG = dialogueBubbleGO.AddComponent<CanvasGroup>();
        dialogueCG.blocksRaycasts = false;
        dialogueCG.interactable = false;

        Image bubbleBg = dialogueBubbleGO.AddComponent<Image>();
        bubbleBg.color = new Color(0.10f, 0.12f, 0.16f, 0.94f);

        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(dialogueBubbleGO.transform, false);
        RectTransform textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(16f, 6f);
        textRT.offsetMax = new Vector2(-16f, -6f);

        dialogueText = textGO.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null) dialogueText.font = fontAsset;
        dialogueText.fontSize = 17f;
        dialogueText.fontStyle = FontStyles.Bold;
        dialogueText.alignment = TextAlignmentOptions.Center;
        dialogueText.color = new Color(1f, 0.94f, 0.80f, 1f);

        dialogueBubbleGO.SetActive(false);
    }

    private IEnumerator AnimateDialogueBubble(string quote, Vector2 targetLocalPos)
    {
        dialogueBubbleGO.SetActive(true);
        dialogueBubbleRT.anchoredPosition = targetLocalPos;
        dialogueText.text = quote;
        dialogueCG.alpha = 0f;
        dialogueBubbleRT.localScale = Vector3.one * 0.7f;

        const float duration = 2.0f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;

            if (t < 0.12f)
            {
                // 팝업 등장
                float subT = t / 0.12f;
                dialogueBubbleRT.localScale = Vector3.one * Mathf.Lerp(0.7f, 1.05f, subT);
                dialogueCG.alpha = subT;
            }
            else if (t < 0.20f)
            {
                float subT = (t - 0.12f) / 0.08f;
                dialogueBubbleRT.localScale = Vector3.one * Mathf.Lerp(1.05f, 1.0f, subT);
                dialogueCG.alpha = 1f;
            }
            else if (t > 0.80f)
            {
                // 부드러운 페이드아웃
                float subT = (t - 0.80f) / 0.20f;
                dialogueCG.alpha = Mathf.Lerp(1f, 0f, subT);
                dialogueBubbleRT.anchoredPosition = targetLocalPos + new Vector2(0f, Mathf.Lerp(0f, 15f, subT));
            }
            else
            {
                dialogueCG.alpha = 1f;
            }

            yield return null;
        }

        dialogueBubbleGO.SetActive(false);
        dialogueCoroutine = null;
    }

    private IEnumerator AnimateFloatingHeart(FloatingTextItem item, Vector2 startPos)
    {
        item.isBusy = true;
        item.go.SetActive(true);
        item.text.text = "♥";
        item.text.color = new Color(1f, 0.45f, 0.68f, 0.95f);
        item.text.fontSize = UnityEngine.Random.Range(22f, 30f);

        item.rt.anchoredPosition = startPos;
        item.rt.localScale = Vector3.one * 0.6f;
        item.cg.alpha = 1f;

        float duration = UnityEngine.Random.Range(0.65f, 0.95f);
        float swaySpeed = UnityEngine.Random.Range(4f, 8f);
        float swayDist = UnityEngine.Random.Range(10f, 25f);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;

            float yRise = Mathf.Lerp(0f, 65f, t);
            float xSway = Mathf.Sin(elapsed * swaySpeed) * swayDist;
            item.rt.anchoredPosition = startPos + new Vector2(xSway, yRise);

            float scale = Mathf.Lerp(0.6f, 1.15f, Mathf.Sin(t * Mathf.PI));
            item.rt.localScale = Vector3.one * scale;

            item.cg.alpha = t > 0.6f ? Mathf.Lerp(1f, 0f, (t - 0.6f) / 0.4f) : 1f;

            yield return null;
        }

        item.go.SetActive(false);
        item.isBusy = false;
    }
}
