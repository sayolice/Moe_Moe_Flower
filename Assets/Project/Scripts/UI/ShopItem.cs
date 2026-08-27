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

        // 씨앗가 전역 할인 패시브(튤립)가 있으면 매 프레임 최신 할인가로 갱신될 수 있으므로 여기서 표시
        BigNumber effectivePrice = FlowerManager.Instance.GetEffectiveSeedPrice(flowerData);
        priceText.text = effectivePrice == BigNumber.Zero ? "무료" : NumberFormatUtil.FormatGold(effectivePrice);
    }

    private void OnBuyClicked()
    {
        if (shopManager == null || flowerData == null)
            return;

        shopManager.BuyFlower(flowerData.flowerId);
    }
}