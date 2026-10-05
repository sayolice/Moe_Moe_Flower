using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 화면 중앙에 실제로 보이는 꽃 하나를 표시하는 컴포넌트.
/// 데이터/상태는 FlowerManager가 갖고 있고, 이 컴포넌트는 "지금 표시 중인 꽃"을 그리는 역할과
/// 확대(마우스 커서 위치 기준)/패닝(드래그 이동) 표시를 담당한다.
///
/// [클릭 처리는 여기서 하지 않는다] 예전에는 OnMouseDown()이 즉시 FlowerManager.ClickCurrentFlower()를
/// 직접 호출했는데, ScreenTouchController가 "뗄 때까지의 이동 거리로 탭/스와이프를 가리는" 별도
/// 로직으로 같은 클릭을 또 처리하고 있어서, 꽃을 한 번 탭할 때마다 애정/골드가 2배로 들어가는
/// 버그가 있었다(두 시스템이 서로 몰랐음). 지금은 ScreenTouchController 하나만 클릭 판정을 전담하고,
/// 이 클래스는 확대·패닝만 처리한다.
///
/// [패닝을 레거시 OnMouseDown/OnMouseDrag가 아니라 New Input System(Mouse.current)으로 직접 구현하는
/// 이유] 이 프로젝트는 ScreenTouchController를 포함해 전부 New Input System만 쓰는데, 패닝만 레거시
/// 물리 콜라이더 기반 콜백을 같이 쓰면 같은 마우스 입력을 서로 다른 두 파이프라인이 각자 다른 타이밍에
/// 읽어서 미묘하게 어긋나 보일 수 있다("이동 방식이 이상하다"는 원인 중 하나). 그래서 확대·패닝
/// 둘 다 이 클래스 하나가 Update()에서 Mouse.current를 직접 폴링해서 처리하도록 통일했다.
///
/// 오브젝트 간 Awake/Start 실행 순서는 유니티가 보장하지 않으므로,
/// 이벤트 구독 타이밍에만 의존하지 않고 매 프레임 변화를 감지해서 갱신한다 (폴링).
///
/// FlowerData에 실제 스프라이트가 아직 없으면(제작 전), 단계별로 색이 다른 흰 사각형
/// placeholder를 자동으로 대신 그려준다. 나중에 진짜 스프라이트를 채우면 그쪽이 자동 우선된다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class FlowerDisplayController : MonoBehaviour
{
    /// <summary> ScreenTouchController가 "지금 확대 중이라 드래그를 스와이프로 취급하면 안 되는지" 물어보는 용도. </summary>
    public static FlowerDisplayController Instance { get; private set; }

    [Header("마우스 휠 확대 (기본 크기 아래로는 축소되지 않음)")]
    [Tooltip("1보다 작게 잡지 않는다 — 축소 기능은 의도적으로 제공하지 않는다.")]
    public float minZoom = 1f;
    public float maxZoom = 2.5f;
    [Tooltip("휠 한 칸(스크롤 델타 1.0 기준)당 배율 변화량.")]
    public float zoomStep = 0.15f;

    [Header("확대 중 드래그 패닝 (특정 부분을 화면 중앙으로 이동)")]
    [Tooltip("기준 위치(확대 안 된 상태의 원래 위치)에서 이 거리(월드 유닛)의 currentZoom배보다 멀리 " +
             "이동되지 않도록 막는다 — 화면 밖으로 완전히 나가버리는 것을 방지하는 간단한 안전장치. " +
             "확대율이 높을수록 둘러볼 수 있는 범위도 비례해서 넓어진다.")]
    public float maxPanDistance = 1.5f;

    private SpriteRenderer spriteRenderer;
    private BoxCollider2D boxCollider2D;
    private static Sprite placeholderSprite;

    private string lastRenderedFlowerId;
    private GrowthStage lastRenderedStage;
    private bool hasRenderedOnce;

    private Vector3 baseScale;    // 원래(1배) 스케일 — Awake에서 한 번만 캐싱
    private Vector3 basePosition; // 원래(패닝 안 된) 위치 — Awake에서 한 번만 캐싱
    private float currentZoom = 1f;

    private bool isPanning;
    private Vector2 panLastScreenPos;

    /// <summary> 확대되어 있는지(=지금 드래그하면 스와이프가 아니라 패닝이어야 하는지). </summary>
    public bool IsZoomedIn => currentZoom > minZoom + 0.001f;

    private void Awake()
    {
        Instance = this;
        spriteRenderer = GetComponent<SpriteRenderer>();
        boxCollider2D = GetComponent<BoxCollider2D>();
        baseScale = transform.localScale;
        basePosition = transform.position;
    }

    private void Update()
    {
        if (FlowerManager.Instance == null) return;

        string currentId = FlowerManager.Instance.CurrentDisplayedFlowerId;
        FlowerData data = FlowerManager.Instance.GetCurrentData();
        FlowerInstance instance = FlowerManager.Instance.GetCurrentInstance();

        GrowthStage stage = (data != null && instance != null)
            ? instance.GetGrowthStage(data.requiredAffection)
            : GrowthStage.Seed;

        // 표시 중인 꽃이 바뀌면(스와이프/도감이동 등) 확대·패닝 상태도 전부 되돌린다 —
        // 이전 꽃에서 확대/이동해 둔 상태가 다른 꽃에도 그대로 이어지면 오히려 헷갈린다.
        if (hasRenderedOnce && currentId != lastRenderedFlowerId)
        {
            currentZoom = 1f;
            transform.localScale = baseScale;
            transform.position = basePosition;
            isPanning = false;
        }

        // 표시 중인 꽃이 바뀌었거나, 성장 단계가 바뀌었을 때만 다시 그린다 (매프레임 불필요한 갱신 방지)
        if (!hasRenderedOnce || currentId != lastRenderedFlowerId || stage != lastRenderedStage)
        {
            lastRenderedFlowerId = currentId;
            lastRenderedStage = stage;
            hasRenderedOnce = true;
            UpdateVisual(data, instance, stage);
        }

        HandleZoomInput();
        HandlePanInput();
    }

    /// <summary>
    /// 마우스 휠로 꽃을 확대해서 자세히 볼 수 있게 한다. 축소는 기본 크기(1배) 밑으로 절대
    /// 내려가지 않는다(minZoom 기본값 1) — 요청에 따라 "너무 작아지는" 것을 원천적으로 막는다.
    /// UI(사이드바 목록, 상점 등) 위에서 휠을 돌릴 때는 그쪽 스크롤과 겹치지 않도록 무시한다.
    ///
    /// [확대 위치를 마우스로 직접 정한다] 도감의 요구사항과 동일하게, 그냥 화면 중앙(오브젝트
    /// 피벗) 기준으로 확대하는 게 아니라 "지금 마우스 커서가 가리키는 지점"이 확대 전후로 계속
    /// 같은 화면 위치에 남도록 오브젝트 위치를 같이 보정한다 — 보고 싶은 부분에 커서를 놓고 휠을
    /// 굴리기만 하면 그 부분이 화면에 그대로 확대되어, "확대 → 드래그로 재조준" 하는 번거로운
    /// 2단계 과정 없이 한 번에 원하는 곳을 볼 수 있다.
    ///
    /// 이 프로젝트는 New Input System을 쓰므로(ScreenTouchController가 Mouse.current를 쓰는 것과
    /// 동일) 레거시 UnityEngine.Input 정적 API는 "Active Input Handling" 설정에 따라 런타임에
    /// 예외를 던질 수 있어 절대 쓰지 않는다. Mouse.current.scroll의 원시 델타는 OS/휠 모델에 따라
    /// 한 칸에 수십~백 단위로 들쭉날쭉하므로, 크기 대신 방향(Sign)만 취해서 "한 칸 = zoomStep만큼"
    /// 고정폭으로 움직이게 한다.
    /// </summary>
    private void HandleZoomInput()
    {
        if (Mouse.current == null) return;

        float scrollDelta = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Approximately(scrollDelta, 0f)) return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        float oldZoom = currentZoom;
        float newZoom = Mathf.Clamp(oldZoom + Mathf.Sign(scrollDelta) * zoomStep, minZoom, maxZoom);
        if (Mathf.Approximately(newZoom, oldZoom)) return; // 이미 한계치라 변화 없음

        // 확대를 적용하기 전에, 지금 커서가 가리키는 월드 좌표부터 구해둔다.
        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        float depth = cam.WorldToScreenPoint(transform.position).z;
        Vector3 cursorWorldBefore = cam.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, depth));

        currentZoom = newZoom;
        transform.localScale = baseScale * currentZoom;

        if (IsZoomedIn)
        {
            // 오브젝트 기준 확대이므로, 원점(커서 아래 그 점)에서 오브젝트까지의 거리도 같은
            // 비율(newZoom/oldZoom)로 늘어난다 — 그만큼을 위치에서 빼줘야 그 점이 커서 아래 그대로 남는다.
            float scaleRatio = newZoom / oldZoom;
            Vector3 newPosition = cursorWorldBefore + (transform.position - cursorWorldBefore) * scaleRatio;
            transform.position = ClampPan(newPosition);
        }
        else
        {
            // 축소해서 정확히 기본 배율(1배)로 돌아오면 패닝도 함께 원위치로 되돌린다.
            transform.position = basePosition;
        }
    }

    /// <summary>
    /// 확대 중일 때만 마우스 드래그로 위치를 옮긴다("특정 부분을 화면 중앙으로"). 스프라이트를
    /// 정확히 눌러야 하는 물리 콜라이더 판정 없이, 확대 중이고 UI 위가 아니면 화면 어디를 눌러도
    /// 드래그가 시작된다 — 도감의 드래그 패닝(뷰포트 안 아무 데나 눌러서 드래그)과 동일한 방식.
    /// </summary>
    private void HandlePanInput()
    {
        if (Mouse.current == null) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (!IsZoomedIn) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            isPanning = true;
            panLastScreenPos = Mouse.current.position.ReadValue();
            return;
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            isPanning = false;
            return;
        }

        if (!isPanning || !Mouse.current.leftButton.isPressed) return;
        if (!IsZoomedIn) { isPanning = false; return; } // 드래그 도중 휠로 축소해서 1배가 됐다면 중단

        Camera cam = Camera.main;
        if (cam == null) return;

        Vector2 currentScreenPos = Mouse.current.position.ReadValue();
        Vector2 screenDelta = currentScreenPos - panLastScreenPos;
        panLastScreenPos = currentScreenPos;

        // 지금 꽃의 카메라 기준 깊이(z)에서 픽셀 이동량을 월드 이동량으로 변환한다.
        float depth = cam.WorldToScreenPoint(transform.position).z;
        Vector3 worldOrigin = cam.ScreenToWorldPoint(new Vector3(0f, 0f, depth));
        Vector3 worldTarget = cam.ScreenToWorldPoint(new Vector3(screenDelta.x, screenDelta.y, depth));
        Vector3 worldDelta = worldTarget - worldOrigin;

        transform.position = ClampPan(transform.position + worldDelta);
    }

    /// <summary> basePosition에서 maxPanDistance*currentZoom보다 멀리 벗어나지 않도록 위치를 잘라낸다. </summary>
    private Vector3 ClampPan(Vector3 desiredPosition)
    {
        Vector3 offsetFromBase = desiredPosition - basePosition;
        offsetFromBase = Vector3.ClampMagnitude(offsetFromBase, maxPanDistance * currentZoom);
        return basePosition + offsetFromBase;
    }

    private void UpdateVisual(FlowerData data, FlowerInstance instance, GrowthStage stage)
    {
        if (spriteRenderer == null) return;

        if (data == null || instance == null)
        {
            spriteRenderer.sprite = null;
            return;
        }

        Sprite realSprite = data.GetSpriteForStage(stage);

        if (realSprite != null)
        {
            spriteRenderer.sprite = realSprite;
            spriteRenderer.color = Color.white;
        }
        else
        {
            spriteRenderer.sprite = GetPlaceholderSprite();
            spriteRenderer.color = GetPlaceholderColor(stage);
        }

        RefreshColliderToMatchSprite();
    }

    /// <summary>
    /// 씬에 원래 있던 BoxCollider2D의 크기가 0.0001×0.0001(사실상 점 하나)로 설정되어 있어서
    /// OnMouseDown/OnMouseDrag가 실질적으로 거의 발동하지 않았다 — 패닝이 "안 움직인다"는
    /// 증상의 원인. 여기서 스프라이트가 바뀔 때마다(꽃 전환/성장 단계 변화) 콜라이더 크기를
    /// 실제 렌더링되는 스프라이트 바운즈에 맞춰 자동으로 다시 맞춘다. 성장 단계마다,
    /// 그리고 꽃마다 스프라이트 크기가 다를 수 있으므로 한 번 고정해두는 값으로는 부족하다.
    /// (트랜스폼의 scale — 확대/축소 배율 — 은 콜라이더에도 자동으로 곱해지므로 별도 처리가 필요 없다.)
    /// </summary>
    private void RefreshColliderToMatchSprite()
    {
        if (boxCollider2D == null || spriteRenderer.sprite == null) return;

        Bounds spriteBounds = spriteRenderer.sprite.bounds;
        boxCollider2D.size = spriteBounds.size;
        boxCollider2D.offset = spriteBounds.center;
    }

    private static Sprite GetPlaceholderSprite()
    {
        if (placeholderSprite == null)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            placeholderSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
        }
        return placeholderSprite;
    }

    private static Color GetPlaceholderColor(GrowthStage stage)
    {
        switch (stage)
        {
            case GrowthStage.Seed: return new Color(0.55f, 0.4f, 0.25f);   // 갈색 (흙)
            case GrowthStage.Sprout: return new Color(0.6f, 0.85f, 0.4f);  // 연두
            case GrowthStage.Growing: return new Color(0.2f, 0.7f, 0.3f);  // 진한 초록
            case GrowthStage.Bloomed: return new Color(1f, 0.6f, 0.8f);    // 분홍 (개화)
            default: return Color.white;
        }
    }

    // ===================================================================
    // 꽃소녀 터치 상호작용 (바운스 애니메이션 & 말풍선 대사)
    // ===================================================================

    private Coroutine bounceCoroutine;

    /// <summary>
    /// 꽃소녀 터치 시 호출되는 상호작용: 통통 튀는 탄성 애니메이션 + 대사 말풍선 + 하트 연출!
    /// </summary>
    public void TriggerTouchInteraction()
    {
        if (FlowerManager.Instance == null) return;

        FlowerInstance instance = FlowerManager.Instance.GetCurrentInstance();
        FlowerData data = FlowerManager.Instance.GetCurrentData();
        if (instance == null || data == null) return;

        // 1. 통통 튀는 탄성 애니메이션 (Squash & Stretch)
        if (bounceCoroutine != null) StopCoroutine(bounceCoroutine);
        bounceCoroutine = StartCoroutine(AnimateBounce());

        // 2. 머리 위 말풍선 위치 계산 (월드 좌표)
        Bounds bounds = spriteRenderer.sprite != null ? spriteRenderer.bounds : new Bounds(transform.position, Vector3.one);
        Vector3 bubblePos = transform.position + new Vector3(0f, bounds.extents.y * currentZoom + 0.5f, 0f);

        // 3. 상황별 상호작용 대사 선택
        string quote = GetInteractionQuote(data, instance);

        if (TouchFeedbackManager.Instance != null)
        {
            TouchFeedbackManager.Instance.ShowDialogueBubble(bubblePos, quote);
            TouchFeedbackManager.Instance.SpawnHeartBurst(transform.position + new Vector3(0f, bounds.extents.y * 0.25f, 0f));
        }
    }

    private System.Collections.IEnumerator AnimateBounce()
    {
        Vector3 targetScale = baseScale * currentZoom;
        const float duration = 0.22f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;

            float squashX, squashY;
            if (t < 0.25f)
            {
                float subT = t / 0.25f;
                squashX = Mathf.Lerp(1.0f, 1.09f, subT);
                squashY = Mathf.Lerp(1.0f, 0.91f, subT);
            }
            else if (t < 0.60f)
            {
                float subT = (t - 0.25f) / 0.35f;
                squashX = Mathf.Lerp(1.09f, 0.94f, subT);
                squashY = Mathf.Lerp(0.91f, 1.07f, subT);
            }
            else
            {
                float subT = (t - 0.60f) / 0.40f;
                squashX = Mathf.Lerp(0.94f, 1.0f, subT);
                squashY = Mathf.Lerp(1.07f, 1.0f, subT);
            }

            transform.localScale = new Vector3(targetScale.x * squashX, targetScale.y * squashY, targetScale.z);
            yield return null;
        }

        transform.localScale = targetScale;
        bounceCoroutine = null;
    }

    private string GetInteractionQuote(FlowerData data, FlowerInstance instance)
    {
        if (!instance.isBloomed)
        {
            string[] unbloomedQuotes = {
                "따뜻한 손길 고마워요~ 쑥쑥 자랄게요!",
                "햇살과 당신의 사랑을 듬뿍 받는 중이에요!",
                "빨리 예쁜 꽃으로 피어나서 보답할게요~",
                "간지러워요, 헤헤~ 힘이 불끈 솟아요!"
            };
            return unbloomedQuotes[UnityEngine.Random.Range(0, unbloomedQuotes.Length)];
        }

        string id = data.flowerId != null ? data.flowerId.ToLower() : "";
        string[] quotes;

        if (id.Contains("dandelion"))
        {
            quotes = new string[] {
                "후훗, 간지러워요~",
                "바람을 타고 당신과 어디든 함께 가고 싶어요!",
                "당신의 손길은 봄바람처럼 부드러워요.",
                "날아가지 않고 여기 꼭 붙어있을게요, 헤헤."
            };
        }
        else if (id.Contains("tulip"))
        {
            quotes = new string[] {
                "헤헤, 오늘도 저 보러 와주신 건가요?",
                "사랑을 듬뿍 받는 기분이라 정말 행복해요~",
                "당신을 위해 더 예쁘게 활짝 피어날게요!",
                "오직 저만 바라봐 주실 거죠?"
            };
        }
        else if (id.Contains("cherry") || id.Contains("blossom"))
        {
            quotes = new string[] {
                "꽃잎이 흩날릴 때마다 가슴이 두근거려요...",
                "봄이 끝나도 당신 곁에 영원히 머물고 싶어요.",
                "제 벚꽃잎, 당신처럼 참 예쁘죠?"
            };
        }
        else if (id.Contains("rose") || id.Contains("roze"))
        {
            quotes = new string[] {
                "어머... 그렇게 다정하게 만져주시면 부끄러운데요?",
                "가시 조심하세요, 당신 손이 다칠까 봐 걱정돼요.",
                "오직 당신만을 위한 매혹적인 향기를 드릴게요."
            };
        }
        else if (id.Contains("sunflower"))
        {
            quotes = new string[] {
                "당신을 보면 온 세상이 반짝반짝 빛나요!",
                "오늘도 활짝 웃어볼게요, 에헤헤!",
                "당신은 저의 하나뿐인 눈부신 태양이에요!"
            };
        }
        else if (id.Contains("hydrangea"))
        {
            quotes = new string[] {
                "촉촉한 비가 내리면 당신 생각이 나요.",
                "당신의 마음에 따라 제 색도 예쁘게 물드는 것 같아요."
            };
        }
        else if (id.Contains("lavender"))
        {
            quotes = new string[] {
                "은은한 보랏빛 향기로 당신의 피로를 풀어드릴게요...",
                "편안하게 쉬어가세요. 언제나 곁에서 지켜드릴게요."
            };
        }
        else if (id.Contains("lotus"))
        {
            quotes = new string[] {
                "맑은 물 위에서 오직 당신만을 기다리고 있었어요.",
                "고결하고 맑은 마음으로 당신의 행복을 빌게요."
            };
        }
        else if (id.Contains("glory"))
        {
            quotes = new string[] {
                "좋은 아침이에요! 오늘도 활기차게 시작해봐요!",
                "당신에게 세상에서 제일 먼저 아침 인사를 건네고 싶었어요!"
            };
        }
        else if (id.Contains("pansy"))
        {
            quotes = new string[] {
                "나를 생각해 주세요, 꽃말처럼 온종일 당신 생각뿐이에요~",
                "헤헤, 자주 만져주시니까 너무 신나요!"
            };
        }
        else
        {
            quotes = new string[] {
                "오늘도 함께 있어서 정말 기뻐요!",
                "당신의 따스한 온기가 느껴져요~",
                "더 많은 시간을 당신과 함께하고 싶어요!"
            };
        }

        return quotes[UnityEngine.Random.Range(0, quotes.Length)];
    }
}
