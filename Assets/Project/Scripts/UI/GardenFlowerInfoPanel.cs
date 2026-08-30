using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 정원에서 배치된 꽃을 탭했을 때 여는 정보 패널(작업 지시: "정원 인접 효과 시각화 UI" 작업 1).
/// "인접 효과"(내가 남에게 주는 것)와 "받고 있는 효과"(남이 나에게 주는 것)를 반드시 분리해서 보여주고,
/// G/s 계산을 기본→레벨→유대→인접→최종 단계별로 전부 표시한다 — 최종값만 보이면 어느 배율에서
/// 계산이 어긋났는지 알 수 없다는 것이 지시서의 핵심 요구.
///
/// 표시는 전부 GardenManager의 캐시(GetOutgoingEffectInfo/GetIncomingContributions/
/// GetGoldMultiplierForFlower)를 읽기만 한다 — 배치가 바뀔 때 GardenManager가 이미 계산해 둔 값을
/// 그대로 보여줄 뿐, 이 패널이 별도로 인접 효과를 재계산하지 않는다("표시용 계산과 실제 계산 분리
/// 금지" 원칙).
///
/// CanvasGroup으로 보이기/숨기기를 전환한다(이 프로젝트 전역 관례 — SetActive(false)는 Start()의
/// 리스너 등록을 다음 씬 로드 때 건너뛰게 만들 수 있어서 쓰지 않는다).
/// </summary>
public class GardenFlowerInfoPanel : MonoBehaviour
{
    public GameObject root;
    public TMP_Text contentText;
    public Button removeButton;
    public Button closeButton;
    public Button tendButton;
    public TMP_Text tendButtonText;

    /// <summary> 닫힐 때(닫기·제거 버튼 모두) 발생 — GardenPanel이 이걸 구독해서 격자 하이라이트
    /// (선택 상태)를 함께 해제한다. 패널이 이제 정원 사이드 패널이 아니라 상점 패널 자리에 뜨기
    /// 때문에, 격자 쪽에서 "닫혔다"는 걸 스스로 알 방법이 없어서 이벤트로 알려줘야 한다. </summary>
    public event Action OnClosed;

    private CanvasGroup canvasGroup;
    private string shownFlowerId;

    private void Awake()
    {
        if (root == null) root = gameObject;
        canvasGroup = root.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = root.AddComponent<CanvasGroup>();
    }

    private void Start()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Hide);
        if (removeButton != null) removeButton.onClick.AddListener(HandleRemoveClicked);
        if (tendButton != null) tendButton.onClick.AddListener(HandleTendClicked);

        if (GardenManager.Instance != null) GardenManager.Instance.OnGardenChanged += HandleGardenChanged;
        if (FlowerManager.Instance != null) FlowerManager.Instance.OnBondLevelUp += HandleBondLevelUp;

        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (GardenManager.Instance != null) GardenManager.Instance.OnGardenChanged -= HandleGardenChanged;
        if (FlowerManager.Instance != null) FlowerManager.Instance.OnBondLevelUp -= HandleBondLevelUp;
    }

    private void HandleGardenChanged()
    {
        // 배치가 바뀌어서 지금 보여주던 꽃이 제거됐으면(예: 다른 경로로 제거) 패널을 닫는다.
        if (!string.IsNullOrEmpty(shownFlowerId) && GardenManager.Instance != null && !GardenManager.Instance.IsPlaced(shownFlowerId))
        {
            Hide();
            return;
        }
        Refresh();
    }

    private void HandleBondLevelUp(string flowerId, int newBondLevel) => Refresh();

    public void Show(string flowerId)
    {
        shownFlowerId = flowerId;
        Refresh();
        SetVisible(true);
    }

    public void Hide()
    {
        bool wasVisible = isVisible;
        shownFlowerId = null;
        SetVisible(false);
        if (wasVisible) OnClosed?.Invoke();
    }

    public bool IsShowing(string flowerId) => isVisible && shownFlowerId == flowerId;
    private bool isVisible;

    private void HandleRemoveClicked()
    {
        if (string.IsNullOrEmpty(shownFlowerId) || GardenManager.Instance == null) return;
        GardenManager.Instance.RemoveFlower(shownFlowerId);
        Hide();
    }

    /// <summary>
    /// 시듦 복구 터치(작업 지시 3.4) — 골드는 일반 터치와 동일하게 받지만 유대는 절대 쌓지 않는다
    /// (청소 행위이지 관계 형성이 아님, GardenManager.TouchTile이 이미 보장한다). 패널을 닫지 않고
    /// Refresh()만 다시 불러서, 눌렀을 때 배율/진행도가 그 자리에서 바로 바뀌는 게 보이게 한다.
    /// </summary>
    private void HandleTendClicked()
    {
        if (string.IsNullOrEmpty(shownFlowerId) || GardenManager.Instance == null) return;
        GardenManager.Instance.TouchTile(shownFlowerId);
        Refresh();
    }

    private void Refresh()
    {
        if (contentText == null || string.IsNullOrEmpty(shownFlowerId)) return;
        contentText.text = BuildContent(shownFlowerId);
        UpdateTendButton();
    }

    private void UpdateTendButton()
    {
        if (tendButton == null || GardenManager.Instance == null || string.IsNullOrEmpty(shownFlowerId)) return;

        // 정원이 지금 실제로 시들어 있지 않으면(양호 유예 기간이거나 방금 전체 복구를 마쳐 손질
        // 카운트만 막 비워진 직후) 손질 자체가 무의미하다 — 그런데도 "손질하기 (0/3)"을 보여주면
        // "방금 다 했는데 왜 다시 0이지"로 오해하기 딱 좋다. 그런 상태에선 버튼을 비활성화하고
        // "정원 양호"로 보여준다.
        if (!GardenManager.Instance.IsCurrentlyWilted())
        {
            tendButton.interactable = false;
            if (tendButtonText != null) tendButtonText.text = "정원 양호";
            return;
        }

        int required = GardenManager.Instance.ActiveGardenData.touchesPerPlacedFlowerToFullyRestore;
        int touches = GardenManager.Instance.GetTileTouchCount(shownFlowerId);
        bool done = touches >= required;

        tendButton.interactable = !done;
        if (tendButtonText != null)
            tendButtonText.text = done ? "손질 완료" : $"손질 ({touches}/{required})";
    }

    private void SetVisible(bool visible)
    {
        isVisible = visible;
        if (canvasGroup == null) return;
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }

    private static string NameOf(string flowerId)
    {
        FlowerData data = FlowerManager.Instance != null ? FlowerManager.Instance.GetFlowerData(flowerId) : null;
        return data != null ? data.displayName : flowerId;
    }

    /// <summary>
    /// 표시 문구 조립. 전부 GardenManager/FlowerManager가 이미 계산해 둔 값을 읽기만 하고, 여기서
    /// 인접 효과나 G/s를 새로 계산하지 않는다 — "인접" 배율은 GetGoldMultiplierForFlower(실제 G/s
    /// 계산에 쓰이는 바로 그 값)를 그대로 쓰고, 시듦은 그 값에 이미 반영돼 있으므로 별도 곱셈 줄로
    /// 두지 않고 상태 문구로만 덧붙인다 — 그래야 "기본×레벨×유대×인접=최종"이 항상 정확히 성립한다
    /// (시듦까지 별도 줄로 쪼개면 1+fraction×wilt ≠ (1+fraction)×wilt라서 곱이 어긋나 버린다).
    /// </summary>
    // 가독성 보강용 색상 — TMP 리치 텍스트 태그로 직접 삽입한다(별도 폰트/스타일 에셋 없이 이 텍스트
    // 블록 하나만으로 위계를 드러내기 위함: 제목/구분선/수치를 톤으로 구분).
    private const string HeaderColor = "#FF9EC4";   // 섹션 제목(연분홍 — 이 프로젝트 강조색 계열)
    private const string PositiveColor = "#8CF29A"; // 증가/이득
    private const string NegativeColor = "#FF8A8A"; // 감소/불가
    private const string MutedColor = "#B9B4C0";    // 보조 설명(회색빛)
    private const string FinalColor = "#FFD966";    // 최종 결과 강조(금색)

    /// <summary>
    /// 기여 1건을 "기본 인접 보너스 → 유대 Lv.n → 적용" 3줄로 풀어서 보여준다(요청 예시 형식 그대로).
    /// c.rawAmount/c.power/c.amount는 전부 GardenManager가 이미 계산해 캐시해 둔 값을 읽기만 한다 —
    /// 여기서 유대 위력을 다시 계산하지 않는다.
    /// </summary>
    private static void AppendContributionBreakdown(StringBuilder sb, AdjacencyContribution c)
    {
        sb.AppendLine($"    {c.label}");
        if (c.isAmplification)
        {
            string appliedColor = c.amount >= 1f ? PositiveColor : MutedColor;
            sb.AppendLine($"        기본 증폭         ×{c.rawAmount:0.00}");
            sb.AppendLine($"        유대 Lv.{c.sourceBondLevel}         ×{c.power:0.00}");
            sb.AppendLine($"        → 적용            <color={appliedColor}>×{c.amount:0.00}</color>");
        }
        else
        {
            string rawColor = c.rawAmount >= 0 ? PositiveColor : NegativeColor;
            string appliedColor = c.amount >= 0 ? PositiveColor : NegativeColor;
            sb.AppendLine($"        기본 인접 보너스   <color={rawColor}>{(c.rawAmount >= 0 ? "+" : "")}{c.rawAmount * 100f:0.#}%</color>");
            sb.AppendLine($"        유대 Lv.{c.sourceBondLevel}          ×{c.power:0.00}");
            sb.AppendLine($"        → 적용             <color={appliedColor}>{(c.amount >= 0 ? "+" : "")}{c.amount * 100f:0.#}%</color>");
        }
    }

    private string BuildContent(string flowerId)
    {
        FlowerData data = FlowerManager.Instance != null ? FlowerManager.Instance.GetFlowerData(flowerId) : null;
        FlowerInstance instance = FlowerManager.Instance != null ? FlowerManager.Instance.GetInstance(flowerId) : null;
        if (data == null || instance == null) return "";

        var sb = new StringBuilder();
        sb.AppendLine($"<size=124%><b>{data.displayName}</b></size>   <color={FinalColor}>유대 Lv.{instance.bondLevel}</color>");
        sb.AppendLine();

        // ── 인접 효과 (내가 남에게 주는 것) ──
        OutgoingEffectInfo outgoing = GardenManager.Instance != null ? GardenManager.Instance.GetOutgoingEffectInfo(flowerId) : null;
        bool outgoingActive = outgoing != null && outgoing.isActive;
        // 예전엔 유대 Lv.5 미만이면 완전 비활성(0%)이었지만, 이제 유대 레벨에 비례해 단계적으로
        // 강해진다 — 그래서 "Lv.5 필요" 대신 "지금 위력이 몇 %인지"를 보여준다.
        float outgoingPower = FlowerManager.Instance != null ? FlowerManager.Instance.GetAdjacencyPower(instance.bondLevel) : 0f;
        string outgoingHeaderSuffix = outgoing != null && !string.IsNullOrEmpty(outgoing.description)
            ? (outgoingActive
                ? $"  <color={MutedColor}>(위력 {outgoingPower * 100f:0.#}%)</color>"
                : $"  <color={MutedColor}>(위력 0% — 유대 Lv.1부터 발동)</color>")
            : "";
        sb.AppendLine($"<b><color={HeaderColor}>▸ 인접 효과</color></b> <color={MutedColor}>(내가 남에게)</color>{outgoingHeaderSuffix}");
        if (outgoing != null && !string.IsNullOrEmpty(outgoing.description))
        {
            sb.AppendLine($"    {outgoing.description}");
            if (outgoingActive)
            {
                sb.AppendLine(outgoing.targetFlowerIds.Count == 0
                    ? $"    <color={MutedColor}>→ 지금은 적용 대상 없음</color>"
                    : $"    → 적용 대상: <b>{string.Join(", ", outgoing.targetFlowerIds.Select(NameOf))}</b>");
            }
        }
        else
        {
            sb.AppendLine($"    <color={MutedColor}>이 꽃은 인접 효과가 없습니다.</color>");
        }

        sb.AppendLine();

        // ── 받고 있는 효과 (남이 나에게 주는 것) ──
        sb.AppendLine($"<b><color={HeaderColor}>▸ 받고 있는 효과</color></b> <color={MutedColor}>(남이 나에게)</color>");
        IReadOnlyList<AdjacencyContribution> incoming = GardenManager.Instance != null
            ? GardenManager.Instance.GetIncomingContributions(flowerId)
            : new List<AdjacencyContribution>();

        if (incoming.Count == 0)
        {
            sb.AppendLine($"    <color={MutedColor}>없음</color>");
        }
        else
        {
            // 기여 하나마다 "기본 → 유대 위력 → 적용" 3줄로 풀어서 보여준다(요청: "단계를 표시해주세요") —
            // 기여마다 소스가 다르고(장미 Lv.3, 튤립 Lv.5 등) 소스마다 유대 레벨이 다를 수 있어서,
            // 하나의 합계 줄에 "유대 Lv.n"을 표시하면 여러 소스가 섞였을 때 부정확해진다.
            bool first = true;
            foreach (AdjacencyContribution c in incoming)
            {
                if (!first) sb.AppendLine();
                first = false;
                AppendContributionBreakdown(sb, c);
            }
        }

        float adjacencyMultiplier = GardenManager.Instance != null
            ? GardenManager.Instance.GetGoldMultiplierForFlower(flowerId)
            : 1f;
        float rawAdjacencyMultiplier = GardenManager.Instance != null
            ? GardenManager.Instance.GetRawAdjacencyMultiplierForFlower(flowerId)
            : 1f;
        float wiltSeverity = GardenManager.Instance != null
            ? GardenManager.Instance.GetWiltSeverityForFlower(flowerId)
            : 1f;

        sb.AppendLine($"    <color={MutedColor}>─────────────────────</color>");
        // 시듦이 실제로 뭔가를 깎고 있을 때만 "시듦 전 → 시듦 적용 → 최종" 3줄로 풀어서 보여준다 —
        // 최종값만 보이면 계산이 맞는지 확인할 수 없다는 요청. 시듦이 없으면(양호) 셋 다 같은 값이라
        // 굳이 나눌 필요 없이 한 줄만 보여준다.
        if (wiltSeverity < 0.999f)
        {
            sb.AppendLine($"    인접 배율 합계 (시듦 전)   ×{rawAdjacencyMultiplier:0.000}");
            sb.AppendLine($"    시듦 적용             ×{wiltSeverity:0.00}  <color={MutedColor}>(보너스에만)</color>");
            sb.AppendLine($"    인접 배율 최종         <b><color={FinalColor}>×{adjacencyMultiplier:0.000}</color></b>");
        }
        else
        {
            sb.AppendLine($"    인접 배율 합계   <b><color={FinalColor}>×{adjacencyMultiplier:0.00}</color></b>");
        }

        sb.AppendLine();

        // ── G/s 계산 (단계별 전부 표시 — 핵심 요구사항) ──
        sb.AppendLine($"<b><color={HeaderColor}>▸ G/s 계산</color></b>");
        if (!instance.isBloomed)
        {
            sb.AppendLine($"    <color={MutedColor}>아직 개화하지 않았습니다.</color>");
        }
        else
        {
            BigNumber baseGpsLv1 = data.GetGoldPerSecond(1);
            double levelMultiplier = Math.Pow(data.goldPerSecondGrowthRate, Math.Max(0, instance.currentLevel - 1));
            float bondMultiplier = FlowerManager.Instance.GetBondGoldMultiplier(instance.bondLevel);
            float passiveMultiplier = PassiveManager.Instance != null
                ? PassiveManager.Instance.GetTotalMultiplier(PassiveEffectType.GoldPerSecondBonusPercent)
                : 1f;
            BigNumber finalGps = FlowerManager.Instance.GetEffectiveGoldPerSecond(
                data, instance.currentLevel, instance.bondLevel, flowerId);

            string wiltNote = GardenManager.Instance != null && GardenManager.Instance.IsPlaced(flowerId)
                ? $"  <color={MutedColor}>(시듦: {GardenManager.Instance.GetCurrentWiltStageLabel()} — 위 인접 배율에 이미 반영됨)</color>"
                : "";

            sb.AppendLine($"    기본            {NumberFormatUtil.FormatPrecise(baseGpsLv1)}");
            sb.AppendLine($"    레벨 (Lv.{instance.currentLevel})    ×{levelMultiplier:0.00}");
            if (Math.Abs(passiveMultiplier - 1f) > 0.0001f)
                sb.AppendLine($"    패시브          ×{passiveMultiplier:0.00}");
            sb.AppendLine($"    유대 (Lv.{instance.bondLevel})     ×{bondMultiplier:0.00}");
            sb.AppendLine($"    인접            ×{adjacencyMultiplier:0.00}{wiltNote}");
            sb.AppendLine($"    <color={MutedColor}>─────────────────────</color>");
            sb.AppendLine($"    <size=115%>최종   <b><color={FinalColor}>{NumberFormatUtil.FormatPrecise(finalGps)}</color></b></size>");
        }

        return sb.ToString();
    }
}
