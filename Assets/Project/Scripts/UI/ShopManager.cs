using System.Collections.Generic;
using UnityEngine;

public class ShopManager : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private Transform content;
    [SerializeField] private ShopItem shopItemPrefab;

    private readonly List<ShopItem> shopItems = new List<ShopItem>();

    private void Start()
    {
        BuildShop();

        if (FlowerManager.Instance != null)
        {
            FlowerManager.Instance.OnOwnedFlowersChanged += RefreshShopItems;
        }
    }

    private void OnDestroy()
    {
        if (FlowerManager.Instance != null)
        {
            FlowerManager.Instance.OnOwnedFlowersChanged -= RefreshShopItems;
        }
    }

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

            ShopItem item = Instantiate(shopItemPrefab, content);
            item.Setup(flower, this);
            shopItems.Add(item);
        }
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
        RefreshShopItems();
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
}