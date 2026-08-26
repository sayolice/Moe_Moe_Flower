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

    [Header("꽃 전환 인디케이터 (예: \"3 / 5\")")]
    public TMP_Text flowerIndicatorText;

    [Header("AffectionBar - Background를 게이지로 사용")]
    public Image affectionBarFillImage;

    [Header("유대 게이지 탭 → 메모리얼 열기 (개화 후에만 의미 있음, AffectionBar에 Button으로 붙는다)")]
    public Button affectionBarButton;
    public GameObject unreadMemorialBadge;

    [Header("애정 속도 표시 스무딩 (초 단위 반응 시간 — 골드는 스무딩 없이 직접 계산해서 표시함)")]
    public float rateSmoothingTime = 0.5f;

    private float lastAffection;
    private string lastTrackedFlowerId;
    private float displayedAffectionPerSecond;
    private bool initialized;

    private void Start()
    {
        if (FlowerManager.Instance != null)
            FlowerManager.Instance.OnBondLevelUp += HandleBondLevelUp;

        if (affectionBarButton != null)
            affectionBarButton.onClick.AddListener(OpenMemorialForCurrentFlower);
    }

    /// <summary>
    /// 유대 게이지(개화 후 애정 게이지 자리를 대신 쓰는 그 바)를 탭하면 지금 표시 중인 꽃의
    /// 메모리얼을 연다. 미개화 상태(애정 게이지가 떠 있을 때)는 버튼이 눌려도 조용히 무시한다 —
    /// 별도 GameObject를 껐다 켰다 하지 않고 조건만 검사하는 이유는, 이 버튼이 AffectionBar
    /// 자체에 붙어 있어(레이아웃 재사용) 개화 여부와 무관하게 항상 같은 자리에 존재하기 때문이다.
    /// </summary>
    private void OpenMemorialForCurrentFlower()
    {
        if (FlowerManager.Instance == null || MemorialViewPanel.Instance == null) return;

        FlowerInstance instance = FlowerManager.Instance.GetCurrentInstance();
        if (instance == null || !instance.isBloomed) return;

        MemorialViewPanel.Instance.Open(FlowerManager.Instance.CurrentDisplayedFlowerId);
    }

    private void OnDestroy()
    {
        if (FlowerManager.Instance != null)
            FlowerManager.Instance.OnBondLevelUp -= HandleBondLevelUp;
    }

    /// <summary>
    /// 유대 레벨업 연출(요청 명세 5.4) — 짧은 텍스트 피드백만 넣는다(파티클/게이지 채워짐은 이미
    /// UpdateFlowerStatusUI가 매 프레임 실제 진행도를 그대로 그려주므로 별도 애니메이션 없이도
    /// "채워지는" 느낌은 자연히 생긴다). 연타를 끊으면 안 되므로 절대 모달/블로킹 연출을 쓰지 않고,
    /// 화면 한 켠에 잠깐 떴다 사라지는 텍스트 하나로 처리한다. 지금 화면에 표시 중이지 않은 꽃의
    /// 유대가 올라도(정원 시스템 도입 후) 여기서는 조용히 무시한다 — 안 보이는 꽃 때문에 화면에
    /// 갑자기 텍스트가 뜨면 오히려 혼란스럽다.
    /// </summary>
    private void HandleBondLevelUp(string flowerId, int newBondLevel)
    {
        if (FlowerManager.Instance == null || flowerId != FlowerManager.Instance.CurrentDisplayedFlowerId) return;
        if (affectionValueText == null) return;

        SpawnBondLevelUpPopup(newBondLevel);
    }

    private void SpawnBondLevelUpPopup(int newBondLevel)
    {
        Transform parent = affectionValueText.transform.parent;
        if (parent == null) return;

        GameObject popupGO = new GameObject("BondLevelUpPopup");
        popupGO.transform.SetParent(parent, false);

        RectTransform rt = popupGO.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0.38f); // AffectionValueText와 같은 영역 위에 겹쳐서 시작
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        TMP_Text text = popupGO.AddComponent<TextMeshProUGUI>();
        text.text = $"유대 Lv.{newBondLevel} 달성!";
        text.font = affectionValueText.font; // 이미 검증된(네모 박스 안 깨지는) Noto 폰트를 그대로 재사용
        text.fontSize = 22f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(1f, 0.85f, 0.3f, 1f); // 골드빛 — 게이지 핑크색과 구분되는 강조색

        StartCoroutine(AnimateBondLevelUpPopup(rt, text));
    }

    /// <summary>
    /// 위로 살짝 떠오르며 페이드아웃 — 약 1초. 코루틴 하나로 끝나므로 연속으로 여러 번 레벨업해도
    /// (한 터치에 여러 레벨이 한꺼번에 오르는 경우는 없지만, 정원 도입 후를 대비) 팝업이 각자
    /// 독립적으로 뜨고 사라질 뿐 서로 간섭하거나 입력을 막지 않는다.
    /// </summary>
    private System.Collections.IEnumerator AnimateBondLevelUpPopup(RectTransform rt, TMP_Text text)
    {
        const float duration = 1f;
        const float riseDistance = 40f;
        float elapsed = 0f;
        Color startColor = text.color;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            rt.anchoredPosition = new Vector2(0f, riseDistance * t);
            text.color = new Color(startColor.r, startColor.g, startColor.b, 1f - t);

            yield return null;
        }

        if (rt != null) Destroy(rt.gameObject);
    }

    private void Update()
    {
        if (GameManager.Instance == null || FlowerManager.Instance == null) return;

        FlowerInstance instance = FlowerManager.Instance.GetCurrentInstance();
        FlowerData data = FlowerManager.Instance.GetCurrentData();

        if (!initialized)
        {
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
        UpdateIndicator();
    }

    /// <summary> "3 / 5" 형태로 현재 표시 중인 꽃이 보유 꽃 중 몇 번째인지 표시한다. </summary>
    private void UpdateIndicator()
    {
        if (flowerIndicatorText == null) return;

        int index = FlowerManager.Instance.CurrentDisplayIndex;
        int count = FlowerManager.Instance.OwnedFlowerCount;

        flowerIndicatorText.text = (index >= 0 && count > 0) ? $"{index + 1} / {count}" : "";
    }

    private void UpdateRates(FlowerInstance instance)
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        float alpha = 1f - Mathf.Exp(-dt / rateSmoothingTime);

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

    /// <summary>
    /// [예전 버그] "골드 +n/s" 표시를 totalGold의 프레임간 변화량으로 "측정"했었는데, 레벨업(골드
    /// 소모)·씨앗 구매·터치 골드처럼 골드가 순간적으로 오르내리는 모든 이벤트가 그 측정치를
    /// 오염시켰다 — 특히 연속 레벨업(꾹 누르기)으로 소모가 생산을 크게 앞지르면 측정치가 깊이
    /// 음수로 떨어져서, 이후 지수 스무딩으로 회복되는 데 오래 걸리거나 재실행 전까지 "0/s"에
    /// 갇힌 것처럼 보였다. 지금은 골드 잔액을 아예 보지 않고 FlowerManager.GetTotalGoldPerSecond()
    /// (꽃의 개화 여부·레벨만으로 계산)를 그대로 쓰므로, 골드가 어떻게 소모되든 절대 왜곡되지 않는다.
    /// </summary>
    private void UpdateGoldUI()
    {
        if (goldText != null)
            goldText.text = $"골드 {NumberFormatUtil.Format(GameManager.Instance.totalGold)}";

        if (goldPerSecondText != null)
        {
            float gps = FlowerManager.Instance != null ? FlowerManager.Instance.GetTotalGoldPerSecond() : 0f;
            goldPerSecondText.text = $"골드 +{NumberFormatUtil.Format(gps)}/s";
        }
    }

    private void UpdateFlowerStatusUI(FlowerInstance instance, FlowerData data)
    {
        if (instance == null || data == null)
        {
            if (affectionValueText != null) affectionValueText.text = "-";
            if (affectionPerSecondText != null) affectionPerSecondText.text = "";
            if (unreadMemorialBadge != null) unreadMemorialBadge.SetActive(false);
            SetBarFill(0f);
            return;
        }

        if (instance.isBloomed)
        {
            // 개화한 꽃은 애정 게이지 자리를 유대(Bond) 게이지가 대신 쓴다 — 레이아웃(텍스트 2줄 +
            // 게이지 바 1개)을 그대로 재사용하되 내용만 바꾼다. 레벨업 자체는 여전히 오른쪽 꽃 탭
            // (FlowerUpgradePanel)에서만 가능 — 여기는 표시 전용.
            BondData bondData = FlowerManager.Instance.ActiveBondData;
            float gps = FlowerManager.Instance.GetEffectiveGoldPerSecond(data, instance.currentLevel, instance.bondLevel);
            float bondMultiplier = FlowerManager.Instance.GetBondGoldMultiplier(instance.bondLevel);

            // 유대 배율이 걸려 있을 때만 "(×1.25)"처럼 드러낸다 — Lv.0(배율 없음)에서까지 "(×1)"을
            // 붙이면 불필요한 소음이라 생략한다.
            string multiplierSuffix = instance.bondLevel > 0 ? $" (×{bondMultiplier:0.##})" : "";

            if (unreadMemorialBadge != null)
                unreadMemorialBadge.SetActive(FlowerManager.Instance.HasUnreadMemorial(instance.flowerId));

            if (affectionPerSecondText != null)
                affectionPerSecondText.text = $"Lv.{instance.currentLevel}   G/s {NumberFormatUtil.Format(gps)}{multiplierSuffix}";

            bool isBondMaxed = instance.bondLevel >= bondData.maxBondLevel;
            if (isBondMaxed)
            {
                // 실패가 아니라 완성이므로 회색 비활성처럼 보이면 안 된다 — SetBarFill(1f)로 가득 채운
                // 상태를 그대로 유지하고, 텍스트만 "MAX"로 명시한다.
                if (affectionValueText != null) affectionValueText.text = "유대 MAX";
                SetBarFill(1f);
            }
            else
            {
                // bondLevel은 항상 0 <= bondLevel < maxBondLevel <= thresholds.Count 여야 정상 데이터이지만,
                // 밸런스 데이터가 잘못 채워진 경우(thresholds 원소 부족)까지 방어해서 인덱스 예외를 막는다.
                double threshold = (instance.bondLevel < bondData.thresholds.Count)
                    ? bondData.thresholds[instance.bondLevel]
                    : 1d;

                if (affectionValueText != null)
                    affectionValueText.text = $"유대 Lv.{instance.bondLevel}   {NumberFormatUtil.Format(instance.bond)} / {NumberFormatUtil.Format(threshold)}";

                SetBarFill(threshold > 0d ? Mathf.Clamp01((float)(instance.bond / threshold)) : 0f);
            }
            return;
        }

        float percent = instance.GetGrowthPercent(data.requiredAffection);
        int current = Mathf.FloorToInt(instance.currentAffection);
        int required = data.requiredAffection;

        if (unreadMemorialBadge != null) unreadMemorialBadge.SetActive(false); // 미개화 상태는 애초에 유대/메모리얼이 없음

        if (affectionValueText != null)
            affectionValueText.text = $"{current} / {required} ({(percent * 100f):0}%){BuildBloomEtaSuffix(instance, data)}";
        if (affectionPerSecondText != null)
            affectionPerSecondText.text = $"+{Mathf.Max(0f, displayedAffectionPerSecond):0.0} 애정/s";

        SetBarFill(percent);
    }

    /// <summary>
    /// 미개화 꽃의 예상 개화 시간 문구(짧게 " 개화까지 3시간 20분" 형태)를 만든다.
    ///
    /// [문구를 짧게 유지하는 이유] affectionValueText는 한 줄짜리 고정 높이 박스라서, 여기에 긴
    /// 문구를 붙이면 두 줄로 줄바꿈되어 바로 위 애정 게이지 바와 겹쳐 보인다("글씨가 깨져 보인다"는
    /// 원인 중 하나였다). 그래서 FlowerUpgradeItem과 동일하게 "개화까지 OO" 짧은 형태로 통일한다.
    ///
    /// [rate: 터치+자동을 합친 실측 속도를 그대로 쓴다] "지금 터치하면 이 숫자가 줄어야 한다"가
    /// 당연한 기대치이므로, displayedAffectionPerSecond(터치 버스트 + 자동 애정을 섞은 실측치)를
    /// 그대로 쓴다 — 자동 애정 전용 값으로 바꿨더니 터치 중에도 "개화까지 무한"이 떠버리는 퇴행이
    /// 있었다.
    ///
    /// [속도가 사실상 0일 때: 나누지 않고 "예측 불가"로 명시] 예전엔 속도에 최소값을 깔고 억지로
    /// 나눠서 "27시간 뒤 개화"처럼 그럴듯한 숫자를 만들었는데, 정말 아무 진행이 없는 상태(터치도
    /// 안 하고 자동 애정도 0)에서 그런 숫자는 "가만히 둬도 언젠가 된다"는 잘못된 인상을 준다.
    /// 그래서 속도가 TimeFormatUtil.MinDisplayRatePerSecond 이하면 나눗셈 자체를 하지 않고
    /// "예측 불가"라고 밝힌다. 이 기준보다 살짝만 커도(터치 한 번이면 충분히 넘는다) 바로 실제
    /// 숫자로 전환되므로 "터치하면 반영돼야 한다"는 요구와는 충돌하지 않는다.
    /// </summary>
    private string BuildBloomEtaSuffix(FlowerInstance instance, FlowerData data)
    {
        float rate = displayedAffectionPerSecond;
        if (rate <= TimeFormatUtil.MinDisplayRatePerSecond) return " 개화까지 예측 불가";

        double remaining = System.Math.Max(0, data.requiredAffection - instance.currentAffection);
        double etaSeconds = remaining / rate;

        return $" 개화까지 {TimeFormatUtil.Format(etaSeconds)}";
    }

    private void SetBarFill(float percent)
    {
        if (affectionBarFillImage == null) return;
        affectionBarFillImage.fillAmount = Mathf.Clamp01(percent);
    }
}