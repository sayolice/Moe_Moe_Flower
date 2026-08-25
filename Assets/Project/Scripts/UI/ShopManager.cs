using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ShopManager : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private Transform content;
    [SerializeField] private ShopItem shopItemPrefab;

    [Header("빈 상태 안내 (구매 가능한 꽃이 하나도 남지 않았을 때)")]
    [SerializeField] private GameObject emptyStateRoot;
    [SerializeField] private TMP_Text emptyStateText;

    private readonly List<ShopItem> shopItems = new List<ShopItem>();

    private void Start()
    {
        BuildShop();

        if (FlowerManager.Instance != null)
        {
            // 구매(보유 목록 변경)가 일어나면 그 꽃이 상점 목록에서 빠져야 하므로 전체를 다시 만든다.
            FlowerManager.Instance.OnOwnedFlowersChanged += BuildShop;
            // 씨앗가 전역 할인 패시브(튤립)는 "개화 후"에만 켜지므로, 구매 이벤트만으론
            // 개화 시점에 상점 가격 표시가 갱신되지 않는다 — 개화한 꽃은 이미 보유 중이라
            // 상점 목록 자체는 안 바뀌므로, 가격 텍스트만 새로고침하면 된다(전체 재생성 불필요).
            FlowerManager.Instance.OnFlowerBloomed += OnAnyFlowerBloomed;
        }
    }

    private void OnDestroy()
    {
        if (FlowerManager.Instance != null)
        {
            FlowerManager.Instance.OnOwnedFlowersChanged -= BuildShop;
            FlowerManager.Instance.OnFlowerBloomed -= OnAnyFlowerBloomed;
        }
    }

    private void OnAnyFlowerBloomed(string flowerId) => RefreshShopItems();

    private void BuildShop()
    {
        if (content == null || shopItemPrefab == null)
        {
            Debug.LogError("[ShopManager] Content 또는 ShopItem Prefab이 연결되지 않았습니다.");
            return;
        }

        ClearShop();

        if (FlowerManager.Instance == null)
        {
            Debug.LogError("[ShopManager] FlowerManager.Instance가 없습니다.");
            return;
        }

        foreach (FlowerData flower in FlowerManager.Instance.allFlowers)
        {
            if (flower == null)
                continue;

            // 이미 구매(보유)한 꽃은 상점 목록에서 제외한다 — "비활성 구매 버튼"으로 남겨두지 않는다.
            if (FlowerManager.Instance.IsOwned(flower.flowerId))
                continue;

            ShopItem item = Instantiate(shopItemPrefab, content);
            item.Setup(flower, this);
            shopItems.Add(item);
        }

        UpdateEmptyState();
    }

    public void BuyFlower(string flowerId)
    {
        if (FlowerManager.Instance == null)
            return;

        bool success = FlowerManager.Instance.TryPurchaseSeed(flowerId);

        if (!success)
        {
            Debug.Log($"[ShopManager] 구매 실패: {flowerId}");
            return;
        }

        Debug.Log($"[ShopManager] 구매 성공: {flowerId}");
        // TryPurchaseSeed가 내부에서 OnOwnedFlowersChanged를 이미 동기 호출했으므로
        // BuildShop()이 그 안에서 실행되어 이 항목은 이 시점에 이미 목록에서 빠져 있다.
    }

    private void RefreshShopItems()
    {
        foreach (ShopItem item in shopItems)
        {
            if (item != null)
                item.Refresh();
        }
    }

    private void ClearShop()
    {
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Destroy(content.GetChild(i).gameObject);
        }

        shopItems.Clear();
    }

    /// <summary> 구매 가능한 꽃이 하나도 남지 않았을 때만 안내 문구를 보여준다. </summary>
    private void UpdateEmptyState()
    {
        bool isEmpty = shopItems.Count == 0;

        if (emptyStateRoot != null)
            emptyStateRoot.SetActive(isEmpty);

        if (isEmpty && emptyStateText != null)
            emptyStateText.text = "모든 꽃을 만났습니다";
    }
}
