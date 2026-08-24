using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 오른쪽 FlowerManagementPanel(레벨업 UI) 담당.
/// UIManager와 동일하게 매 프레임 FlowerManager/GameManager 값을 폴링해서 갱신한다
/// (골드 자동 생산으로 totalGold가 계속 바뀌므로, 레벨업 버튼의 interactable 상태도
/// 매 프레임 다시 계산해야 즉시 반영된다).
/// </summary>
public class FlowerManagementPanelController : MonoBehaviour
{
    [Header("텍스트")]
    public TMP_Text flowerNameText;
    public TMP_Text levelText;
    public TMP_Text currentGpsText;
    public TMP_Text nextGpsText;
    public TMP_Text levelUpCostText;

    [Header("버튼")]
    public Button levelUpButton;

    private void Start()
    {
        if (levelUpButton != null)
            levelUpButton.onClick.AddListener(OnLevelUpClicked);
    }

    private void Update()
    {
        if (FlowerManager.Instance == null || GameManager.Instance == null) return;

        FlowerData data = FlowerManager.Instance.GetCurrentData();
        FlowerInstance instance = FlowerManager.Instance.GetCurrentInstance();

        if (data == null || instance == null)
        {
            SetEmptyState();
            return;
        }

        if (flowerNameText != null)
            flowerNameText.text = data.displayName;

        if (!instance.isBloomed)
        {
            SetPreBloomState();
            return;
        }

        int currentLevel = instance.currentLevel;
        float currentGps = data.GetGoldPerSecond(currentLevel);
        float nextGps = data.GetGoldPerSecond(currentLevel + 1);
        long cost = data.GetLevelUpCost(currentLevel);

        if (levelText != null)
            levelText.text = $"Lv.{currentLevel}";
        if (currentGpsText != null)
            currentGpsText.text = $"현재 G/s: {currentGps:0.0}";
        if (nextGpsText != null)
            nextGpsText.text = $"다음 Lv G/s: {nextGps:0.0}";
        if (levelUpCostText != null)
            levelUpCostText.text = $"레벨업 비용: {cost:N0} G";

        if (levelUpButton != null)
            levelUpButton.interactable = GameManager.Instance.totalGold >= cost;
    }

    private void SetEmptyState()
    {
        if (flowerNameText != null) flowerNameText.text = "-";
        if (levelText != null) levelText.text = "-";
        if (currentGpsText != null) currentGpsText.text = "";
        if (nextGpsText != null) nextGpsText.text = "";
        if (levelUpCostText != null) levelUpCostText.text = "";
        if (levelUpButton != null) levelUpButton.interactable = false;
    }

    private void SetPreBloomState()
    {
        if (levelText != null) levelText.text = "미개화";
        if (currentGpsText != null) currentGpsText.text = "";
        if (nextGpsText != null) nextGpsText.text = "";
        if (levelUpCostText != null) levelUpCostText.text = "개화 후 레벨업 가능";
        if (levelUpButton != null) levelUpButton.interactable = false;
    }

    private void OnLevelUpClicked()
    {
        if (FlowerManager.Instance == null) return;
        FlowerManager.Instance.TryLevelUpFlower(FlowerManager.Instance.CurrentDisplayedFlowerId);
    }
}
