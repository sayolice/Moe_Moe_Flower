using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopItem : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text flowerNameText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private Button buyButton;

    private FlowerData flowerData;
    private ShopManager shopManager;

    public void Setup(FlowerData data, ShopManager manager)
    {
        flowerData = data;
        shopManager = manager;

        flowerNameText.text = data.displayName;

        if (data.seedPrice == 0)
            priceText.text = "무료";
        else
            priceText.text = $"{data.seedPrice:N0} G";

        buyButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(OnBuyClicked);

        Refresh();
    }

    public void Refresh()
    {
        if (flowerData == null || FlowerManager.Instance == null)
            return;

        bool owned = FlowerManager.Instance.IsOwned(flowerData.flowerId);

        // 이미 구매한 꽃은 구매 버튼 비활성화
        buyButton.interactable = !owned;
    }

    private void OnBuyClicked()
    {
        if (shopManager == null || flowerData == null)
            return;

        shopManager.BuyFlower(flowerData.flowerId);
    }
}