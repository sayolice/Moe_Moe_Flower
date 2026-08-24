using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// GameManager / FlowerManager의 값을 실제 UI(Text, 게이지)에 매 프레임 반영한다.
/// GameCanvas 오브젝트에 붙이고, Inspector에서 각 필드에 하이어라키의 UI 요소를 드래그해서 연결.
/// FlowerManager가 "현재 표시 중인 꽃"을 알려주므로, 이 스크립트는 특정 꽃을 직접 참조하지 않는다.
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("TopBar")]
    public TMP_Text goldText;
    public TMP_Text goldPerSecondText;

    [Header("FlowerStatusUI")]
    public TMP_Text affectionPerSecondText;
    public TMP_Text affectionValueText;

    [Header("AffectionBar - Background를 게이지로 사용")]
    public Image affectionBarFillImage;

    [Header("속도 표시 스무딩 (초 단위 반응 시간)")]
    public float rateSmoothingTime = 0.5f;

    private double lastGold;
    private float lastAffection;
    private string lastTrackedFlowerId;
    private float displayedGoldPerSecond;
    private float displayedAffectionPerSecond;
    private bool initialized;

    private void Update()
    {
        if (GameManager.Instance == null || FlowerManager.Instance == null) return;

        FlowerInstance instance = FlowerManager.Instance.GetCurrentInstance();
        FlowerData data = FlowerManager.Instance.GetCurrentData();

        if (!initialized)
        {
            lastGold = GameManager.Instance.totalGold;
            lastAffection = instance != null ? instance.currentAffection : 0f;
            lastTrackedFlowerId = FlowerManager.Instance.CurrentDisplayedFlowerId;
            initialized = true;
            return;
        }

        // 표시 중인 꽃이 바뀌면(스와이프/도감이동/신규구매) 애정 속도 측정 기준을 리셋
        if (lastTrackedFlowerId != FlowerManager.Instance.CurrentDisplayedFlowerId)
        {
            lastAffection = instance != null ? instance.currentAffection : 0f;
            displayedAffectionPerSecond = 0f;
            lastTrackedFlowerId = FlowerManager.Instance.CurrentDisplayedFlowerId;
        }

        UpdateRates(instance);
        UpdateGoldUI();
        UpdateFlowerStatusUI(instance, data);
    }

    private void UpdateRates(FlowerInstance instance)
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        float alpha = 1f - Mathf.Exp(-dt / rateSmoothingTime);

        // 골드 속도: 표시 중인 꽃과 무관하게 전체 골드 총량의 실제 변화 속도 (자동생산+클릭 모두 포함)
        double goldNow = GameManager.Instance.totalGold;
        float instantGoldRate = (float)((goldNow - lastGold) / dt);
        displayedGoldPerSecond = Mathf.Lerp(displayedGoldPerSecond, instantGoldRate, alpha);
        lastGold = goldNow;

        // 애정 속도: 현재 표시 중인 꽃 기준
        if (instance != null && !instance.isBloomed)
        {
            float affectionNow = instance.currentAffection;
            float instantAffectionRate = (affectionNow - lastAffection) / dt;
            displayedAffectionPerSecond = Mathf.Lerp(displayedAffectionPerSecond, instantAffectionRate, alpha);
            lastAffection = affectionNow;
        }
        else
        {
            displayedAffectionPerSecond = 0f;
        }
    }

    private void UpdateGoldUI()
    {
        if (goldText != null)
            goldText.text = $"골드 {FormatGold(GameManager.Instance.totalGold)}";

        if (goldPerSecondText != null)
            goldPerSecondText.text = $"골드 +{Mathf.Max(0f, displayedGoldPerSecond):0.0}/s";
    }

    private void UpdateFlowerStatusUI(FlowerInstance instance, FlowerData data)
    {
        if (instance == null || data == null)
        {
            if (affectionValueText != null) affectionValueText.text = "-";
            if (affectionPerSecondText != null) affectionPerSecondText.text = "";
            SetBarFill(0f);
            return;
        }

        if (instance.isBloomed)
        {
            if (affectionPerSecondText != null)
                affectionPerSecondText.text = "개화 완료";
            if (affectionValueText != null)
                affectionValueText.text = ""; // 꽃 레벨은 오른쪽 꽃 탭(FlowerUpgradePanel)에서만 표시
            SetBarFill(1f);
            return;
        }

        float percent = instance.GetGrowthPercent(data.requiredAffection);
        int current = Mathf.FloorToInt(instance.currentAffection);
        int required = data.requiredAffection;

        if (affectionValueText != null)
            affectionValueText.text = $"{current} / {required} ({(percent * 100f):0}%)";
        if (affectionPerSecondText != null)
            affectionPerSecondText.text = $"+{Mathf.Max(0f, displayedAffectionPerSecond):0.0} 애정/s";

        SetBarFill(percent);
    }

    private void SetBarFill(float percent)
    {
        if (affectionBarFillImage == null) return;
        affectionBarFillImage.fillAmount = Mathf.Clamp01(percent);
    }

    private string FormatGold(double value)
    {
        return Mathf.FloorToInt((float)value).ToString();
    }
}