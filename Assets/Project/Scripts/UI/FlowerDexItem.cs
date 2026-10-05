using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 도감 그리드의 칸 하나. 자기 flowerId만 알면 동작하며, 선택되면
/// FlowerDexPanel.ShowDetail을 통해 상세 정보를 겹쳐서 보여준다(즉시 이동/닫힘 아님).
/// 아이콘은 항상 개화(bloom) 스프라이트를 기준으로 보여준다 — 미보유/성장 중이어도 완성된 꽃의
/// 실루엣(어둡게 틴트)을 미리 보여주고, 실제로 "개화"한 뒤에야 밝은 원색으로 드러난다.
/// 씨앗을 구매(보유)한 것만으로는 공개되지 않는다 — 구매 직후에도 여전히 미개화 상태이므로
/// 실루엣 그대로 유지되는 것이 의도된 동작이다.
/// ShopItem과 동일하게 매 프레임 폴링하지 않고, Setup 시점과 관련 이벤트(구매/개화/표시전환) 발생 시에만
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

    [Header("미열람 메모리얼 뱃지 (해금됐지만 아직 안 읽은 것이 있을 때만 표시)")]
    public GameObject unreadMemorialBadge;

    private string flowerId;
    private FlowerDexPanel panel;

    public string FlowerId => flowerId;

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
        if (data == null) return;

        // 미보유 꽃은 instance가 null이다 — 이 경우도 목록에 계속 표시하고 실루엣/잠금으로 나타낸다.
        FlowerInstance instance = FlowerManager.Instance.GetInstance(flowerId);
        bool owned = instance != null;

        if (flowerNameText != null)
            flowerNameText.text = data.displayName;

        if (statusText != null)
        {
            statusText.text = !owned ? "미보유"
                : instance.isBloomed ? BuildBloomedStatusText(instance)
                : $"성장 중 {(instance.GetGrowthPercent(data.requiredAffection) * 100f):0}%";
        }

        if (iconImage != null)
        {
            // 도감은 항상 개화(bloom) 이미지를 기준으로 보여준다(성장 단계별 스프라이트 아님).
            iconImage.sprite = data.bloomSprite;
            iconImage.enabled = data.bloomSprite != null;
            // 실제로 개화하기 전(미보유 포함 성장 중 전부)은 실루엣처럼 어둡게 틴트한다 —
            // 씨앗만 구매한 상태(owned && !isBloomed)도 아직 공개된 게 아니므로 실루엣 그대로다.
            bool revealed = owned && instance.isBloomed;
            iconImage.color = revealed ? Color.white : new Color(0.08f, 0.08f, 0.08f, 1f);
        }

        if (currentHighlight != null)
            currentHighlight.SetActive(owned && FlowerManager.Instance.CurrentDisplayedFlowerId == flowerId);

        if (unreadMemorialBadge != null)
            unreadMemorialBadge.SetActive(owned && FlowerManager.Instance.HasUnreadMemorial(flowerId));
    }

    private void OnSelected()
    {
        if (panel == null || flowerId == null) return;
        panel.ShowDetail(flowerId);
    }

    /// <summary>
    /// 개화한 꽃의 그리드 셀 상태 문구에 유대(Bond) 레벨을 같이 붙인다. 목록에서 상세를 열지 않고도
    /// "누구에게 시간을 덜 썼나"가 한눈에 보여야 이 시스템의 목적(다음엔 누구에게 시간을 쓸지 결정)이
    /// 실제로 작동한다 — 상세 화면에만 있으면 매번 열어봐야 알 수 있어서 그 결정이 생기지 않는다.
    /// </summary>
    private string BuildBloomedStatusText(FlowerInstance instance)
    {
        int maxBondLevel = FlowerManager.Instance.ActiveBondData.maxBondLevel;
        string bondPart = instance.bondLevel >= maxBondLevel ? "유대MAX" : $"유대{instance.bondLevel}";
        return $"Lv.{instance.currentLevel} / {bondPart}";
    }
}
