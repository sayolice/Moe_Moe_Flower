using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 도감 그리드의 칸 하나. 자기 flowerId만 알면 동작하며, 선택되면
/// FlowerDexPanel.SelectAndClose를 통해 그 꽃을 메인에 띄우고 도감을 닫는다.
/// ShopItem과 동일하게 매 프레임 폴링하지 않고, Setup 시점과 관련 이벤트(개화/표시전환) 발생 시에만
/// Refresh한다 — 그리드 값(레벨/성장%)이 골드처럼 매 프레임 바뀌는 값이 아니기 때문.
/// </summary>
public class FlowerDexItem : MonoBehaviour
{
    [Header("UI")]
    public Image iconImage;
    public TMP_Text flowerNameText;
    public TMP_Text statusText;
    public Button selectButton;

    [Header("현재 메인에 표시 중인 꽃 강조")]
    public GameObject currentHighlight;

    private string flowerId;
    private FlowerDexPanel panel;

    public void Setup(string id, FlowerDexPanel owner)
    {
        flowerId = id;
        panel = owner;

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(OnSelected);
        }

        Refresh();
    }

    public void Refresh()
    {
        if (flowerId == null || FlowerManager.Instance == null) return;

        FlowerData data = FlowerManager.Instance.GetFlowerData(flowerId);
        FlowerInstance instance = FlowerManager.Instance.GetInstance(flowerId);
        if (data == null || instance == null) return;

        if (flowerNameText != null)
            flowerNameText.text = data.displayName;

        if (statusText != null)
        {
            statusText.text = instance.isBloomed
                ? $"Lv.{instance.currentLevel}"
                : $"성장 중 {(instance.GetGrowthPercent(data.requiredAffection) * 100f):0}%";
        }

        if (iconImage != null)
        {
            Sprite sprite = instance.GetGrowthStage(data.requiredAffection) switch
            {
                GrowthStage.Bloomed => data.bloomSprite,
                GrowthStage.Growing => data.growingSprite,
                GrowthStage.Sprout => data.sproutSprite,
                _ => data.seedSprite
            };
            iconImage.sprite = sprite;
            iconImage.enabled = sprite != null;
        }

        if (currentHighlight != null)
            currentHighlight.SetActive(FlowerManager.Instance.CurrentDisplayedFlowerId == flowerId);
    }

    private void OnSelected()
    {
        if (panel == null || flowerId == null) return;
        panel.SelectAndClose(flowerId);
    }
}
