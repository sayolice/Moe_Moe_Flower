using UnityEngine;

/// <summary>
/// 화면 중앙에 실제로 보이는 꽃 하나를 표시하는 컴포넌트.
/// 데이터/상태는 FlowerManager가 갖고 있고, 이 컴포넌트는 "지금 표시 중인 꽃"을 그리고
/// 클릭을 FlowerManager로 전달하는 역할만 한다.
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
    private SpriteRenderer spriteRenderer;
    private static Sprite placeholderSprite;

    private string lastRenderedFlowerId;
    private GrowthStage lastRenderedStage;
    private bool hasRenderedOnce;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
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

        // 표시 중인 꽃이 바뀌었거나, 성장 단계가 바뀌었을 때만 다시 그린다 (매프레임 불필요한 갱신 방지)
        if (!hasRenderedOnce || currentId != lastRenderedFlowerId || stage != lastRenderedStage)
        {
            lastRenderedFlowerId = currentId;
            lastRenderedStage = stage;
            hasRenderedOnce = true;
            UpdateVisual(data, instance, stage);
        }
    }

    private void OnMouseDown()
    {
        Debug.Log("FlowerDisplayController 클릭 감지");

        if (FlowerManager.Instance == null)
        {
            Debug.LogError("FlowerManager.Instance가 null입니다.");
            return;
        }

        FlowerManager.Instance.ClickCurrentFlower();
    }

    private void UpdateVisual(FlowerData data, FlowerInstance instance, GrowthStage stage)
    {
        if (spriteRenderer == null) return;

        if (data == null || instance == null)
        {
            spriteRenderer.sprite = null;
            return;
        }

        Sprite realSprite = stage switch
        {
            GrowthStage.Seed => data.seedSprite,
            GrowthStage.Sprout => data.sproutSprite,
            GrowthStage.Growing => data.growingSprite,
            GrowthStage.Bloomed => data.bloomSprite,
            _ => null
        };

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
}