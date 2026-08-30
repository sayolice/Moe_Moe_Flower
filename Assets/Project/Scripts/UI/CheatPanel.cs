using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 빌드된 게임에서도 쓸 수 있는 치트 패널 — SettingsPanel에서 비밀번호(1204)를 맞혀야 열린다.
/// 에디터 전용 Tools&gt;꽃소녀 치트(CheatMenuWindow)의 기능 중 핵심만 골라 런타임 UGUI로 옮긴 것이다:
/// 골드 추가/설정, 시간 스킵(1h~72h), 꽃 개화/레벨, 유대 레벨 설정, 정원 시듦 강제설정/즉시복구,
/// 진단 출력. "전체 일괄 적용" 계열(모든 꽃 즉시 보유/전체 레벨 설정 등)과 세이브 삭제는 여기 없다
/// — 에디터 치트 창에만 남겨둔다.
///
/// CheatMenuWindow와 동일한 원칙: 상태만 조작하고 정상 경로(AddAffection/ApplyOfflineProgress)를
/// 그대로 태운다 — FlowerManager/GardenManager의 Cheat_* 메서드를 그대로 재사용한다(이 패널이
/// 필요로 하는 것만 #if UNITY_EDITOR 밖으로 옮겨 릴리스 빌드에도 포함시켰다. Flowermanager.cs/
/// GardenManager.cs의 "핵심" 주석 섹션 참고).
///
/// CanvasGroup으로 보이기/숨기기를 전환한다(이 프로젝트 전역 관례) — SettingsPanel 위에 겹쳐서
/// 열리고, 닫으면 SettingsPanel이 다시 보인다(SettingsPanel을 같이 닫지 않음).
/// </summary>
public class CheatPanel : MonoBehaviour
{
    public static CheatPanel Instance { get; private set; }

    [Header("루트")]
    public GameObject root;
    public Button closeButton;

    [Header("1. 골드")]
    public TMP_InputField goldAmountInput;
    public Button goldAddButton;
    public Button goldSetButton;
    public Button gold10kButton;
    public Button gold1mButton;
    public Button gold100mButton;
    public Button gold1tButton;

    [Header("2. 시간 스킵")]
    public Button skip1hButton;
    public Button skip6hButton;
    public Button skip12hButton;
    public Button skip24hButton;
    public Button skip48hButton;
    public Button skip72hButton;

    [Header("3. 꽃 선택 (개화/레벨/유대 공용 — 이전/다음으로 순환)")]
    public Button prevFlowerButton;
    public Button nextFlowerButton;
    public TMP_Text flowerNameText;

    [Header("4. 개화 / 레벨 / 유대")]
    public Button forceBloomButton;
    public TMP_InputField levelInput;
    public Button setLevelButton;
    public TMP_InputField bondLevelInput;
    public Button setBondLevelButton;

    [Header("5. 정원 · 시듦")]
    public Button prevWiltButton;
    public Button nextWiltButton;
    public TMP_Text wiltStageText;
    public Button applyWiltStageButton;
    public Button restoreGardenButton;

    [Header("6. 진단")]
    public Button diagnosticsButton;

    [Header("결과 출력")]
    public TMP_Text resultText;

    private CanvasGroup canvasGroup;
    private int selectedFlowerIndex;
    private int selectedWiltIndex;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (root == null) root = gameObject;
        canvasGroup = root.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = root.AddComponent<CanvasGroup>();
        SetVisible(false);
    }

    private void Start()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Close);

        if (goldAddButton != null) goldAddButton.onClick.AddListener(() => AddGold(ParseDouble(goldAmountInput)));
        if (goldSetButton != null) goldSetButton.onClick.AddListener(() => SetGold(ParseDouble(goldAmountInput)));
        if (gold10kButton != null) gold10kButton.onClick.AddListener(() => AddGold(1e4));
        if (gold1mButton != null) gold1mButton.onClick.AddListener(() => AddGold(1e6));
        if (gold100mButton != null) gold100mButton.onClick.AddListener(() => AddGold(1e8));
        if (gold1tButton != null) gold1tButton.onClick.AddListener(() => AddGold(1e12));

        if (skip1hButton != null) skip1hButton.onClick.AddListener(() => SkipHours(1));
        if (skip6hButton != null) skip6hButton.onClick.AddListener(() => SkipHours(6));
        if (skip12hButton != null) skip12hButton.onClick.AddListener(() => SkipHours(12));
        if (skip24hButton != null) skip24hButton.onClick.AddListener(() => SkipHours(24));
        if (skip48hButton != null) skip48hButton.onClick.AddListener(() => SkipHours(48));
        if (skip72hButton != null) skip72hButton.onClick.AddListener(() => SkipHours(72));

        if (prevFlowerButton != null) prevFlowerButton.onClick.AddListener(() => StepFlower(-1));
        if (nextFlowerButton != null) nextFlowerButton.onClick.AddListener(() => StepFlower(1));
        if (forceBloomButton != null) forceBloomButton.onClick.AddListener(DoForceBloom);
        if (setLevelButton != null) setLevelButton.onClick.AddListener(DoSetLevel);
        if (setBondLevelButton != null) setBondLevelButton.onClick.AddListener(DoSetBondLevel);

        if (prevWiltButton != null) prevWiltButton.onClick.AddListener(() => StepWilt(-1));
        if (nextWiltButton != null) nextWiltButton.onClick.AddListener(() => StepWilt(1));
        if (applyWiltStageButton != null) applyWiltStageButton.onClick.AddListener(DoApplyWiltStage);
        if (restoreGardenButton != null) restoreGardenButton.onClick.AddListener(DoRestoreGarden);

        if (diagnosticsButton != null) diagnosticsButton.onClick.AddListener(DoDiagnostics);

        SetVisible(false);
    }

    /// <summary> SettingsPanel이 비밀번호(1204) 확인 후에만 호출한다. </summary>
    public void Open()
    {
        RefreshFlowerLabel();
        RefreshWiltLabel();
        SetResult("치트 패널을 열었습니다.");
        SetVisible(true);
    }

    public void Close() => SetVisible(false);

    private void SetVisible(bool visible)
    {
        if (canvasGroup == null) return;
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }

    // ===================================================================
    // 1. 골드
    // ===================================================================
    private void AddGold(double amount)
    {
        if (GameManager.Instance == null || amount == 0) return;
        GameManager.Instance.AddGold(BigNumber.FromDouble(amount));
        SetResult($"골드 +{amount:F0} 추가.\n현재 골드: {GameManager.Instance.totalGold}");
    }

    private void SetGold(double amount)
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.SetGold(BigNumber.FromDouble(amount));
        SetResult($"골드를 {amount:F0}로 설정.\n현재 골드: {GameManager.Instance.totalGold}");
    }

    // ===================================================================
    // 2. 시간 스킵 — CheatMenuWindow.SkipHours와 완전히 동일한 경로(새 시뮬레이션 코드 없음):
    // 정원의 "마지막 손질 시각"만 되감고, 기존 ApplyOfflineProgress를 그대로 호출한다.
    // ===================================================================
    private void SkipHours(double hours)
    {
        if (hours <= 0 || FlowerManager.Instance == null) return;
        double seconds = hours * 3600.0;

        string wiltBefore = GardenManager.Instance != null ? GardenManager.Instance.GetCurrentWiltStageLabel() : "(정원 없음)";
        if (GardenManager.Instance != null) GardenManager.Instance.Cheat_RewindTendedClock(seconds);

        OfflineSettlementResult result = FlowerManager.Instance.ApplyOfflineProgress(seconds);

        string wiltAfter = GardenManager.Instance != null ? GardenManager.Instance.GetCurrentWiltStageLabel() : "(정원 없음)";

        var sb = new StringBuilder();
        sb.AppendLine($"[시간 스킵 {hours:0.##}h] 경과 {result.elapsedSeconds / 3600.0:F2}h");
        sb.AppendLine($"획득 골드: {result.goldEarned:F2}");
        sb.AppendLine($"오프라인 중 개화: {(result.newlyBloomedFlowerIds.Count == 0 ? "없음" : string.Join(", ", result.newlyBloomedFlowerIds))}");
        sb.AppendLine($"정원 유대 획득(합계): {result.gardenBondEarned:F2}");
        sb.AppendLine($"정원 유대 레벨업: {(result.gardenBondLeveledFlowerIds.Count == 0 ? "없음" : string.Join(", ", result.gardenBondLeveledFlowerIds))}");
        sb.Append($"시듦 단계: {wiltBefore} → {wiltAfter}");
        SetResult(sb.ToString());

        RefreshWiltLabel();
    }

    // ===================================================================
    // 3. 꽃 선택 + 개화/레벨/유대 (도감 순서 그대로 이전/다음으로 순환)
    // ===================================================================
    private List<FlowerData> Flowers => FlowerManager.Instance != null ? FlowerManager.Instance.allFlowers : null;

    private FlowerData CurrentFlower
    {
        get
        {
            List<FlowerData> flowers = Flowers;
            if (flowers == null || flowers.Count == 0) return null;
            selectedFlowerIndex = ((selectedFlowerIndex % flowers.Count) + flowers.Count) % flowers.Count;
            return flowers[selectedFlowerIndex];
        }
    }

    private void StepFlower(int delta)
    {
        List<FlowerData> flowers = Flowers;
        if (flowers == null || flowers.Count == 0) return;
        selectedFlowerIndex = ((selectedFlowerIndex + delta) % flowers.Count + flowers.Count) % flowers.Count;
        RefreshFlowerLabel();
    }

    private void RefreshFlowerLabel()
    {
        if (flowerNameText == null) return;
        FlowerData data = CurrentFlower;
        flowerNameText.text = data != null ? data.displayName : "(꽃 없음)";
    }

    private void DoForceBloom()
    {
        FlowerData data = CurrentFlower;
        if (data == null || FlowerManager.Instance == null) return;
        bool bloomed = FlowerManager.Instance.Cheat_ForceBloom(data.flowerId);
        SetResult(bloomed ? $"{data.displayName} 즉시 개화 완료." : $"{data.displayName} 개화 실패(미보유 또는 이미 개화 상태).");
    }

    private void DoSetLevel()
    {
        FlowerData data = CurrentFlower;
        if (data == null || FlowerManager.Instance == null) return;
        int level = ParseInt(levelInput, 1);
        FlowerManager.Instance.Cheat_SetFlowerLevel(data.flowerId, level);
        SetResult($"{data.displayName} 레벨을 {level}(으)로 설정. (미개화 꽃은 무시됨)");
    }

    private void DoSetBondLevel()
    {
        FlowerData data = CurrentFlower;
        if (data == null || FlowerManager.Instance == null) return;
        int bondLevel = ParseInt(bondLevelInput, 0);
        FlowerManager.Instance.Cheat_SetFlowerBondLevel(data.flowerId, bondLevel);
        SetResult($"{data.displayName} 유대 레벨을 {bondLevel}(으)로 설정. (미보유 꽃은 무시됨)");
    }

    // ===================================================================
    // 4. 정원 · 시듦
    // ===================================================================
    private List<WiltStage> WiltStages =>
        GardenManager.Instance != null && GardenManager.Instance.ActiveGardenData != null
            ? GardenManager.Instance.ActiveGardenData.wiltStages : null;

    private void StepWilt(int delta)
    {
        List<WiltStage> stages = WiltStages;
        if (stages == null || stages.Count == 0) return;
        selectedWiltIndex = ((selectedWiltIndex + delta) % stages.Count + stages.Count) % stages.Count;
        RefreshWiltLabel();
    }

    private void RefreshWiltLabel()
    {
        if (wiltStageText == null) return;
        List<WiltStage> stages = WiltStages;
        if (stages == null || stages.Count == 0) { wiltStageText.text = "(정원 없음)"; return; }
        selectedWiltIndex = Mathf.Clamp(selectedWiltIndex, 0, stages.Count - 1);
        wiltStageText.text = stages[selectedWiltIndex].label;
    }

    private void DoApplyWiltStage()
    {
        if (GardenManager.Instance == null) return;
        List<WiltStage> stages = WiltStages;
        if (stages == null || stages.Count == 0) return;
        GardenManager.Instance.Cheat_ForceWiltStage(selectedWiltIndex);
        SetResult($"시듦 단계를 '{stages[selectedWiltIndex].label}'(으)로 강제 설정.");
    }

    private void DoRestoreGarden()
    {
        if (GardenManager.Instance == null) return;
        GardenManager.Instance.Cheat_FullyRestoreGarden();
        SetResult("정원을 즉시 완전히 복구했습니다.");
        RefreshWiltLabel();
    }

    // ===================================================================
    // 5. 진단 출력 — CheatMenuWindow.LogDiagnostics의 축약판(Console이 없는 빌드에서는 이 결과
    // 텍스트 박스가 Console을 대신한다).
    // ===================================================================
    private void DoDiagnostics()
    {
        var sb = new StringBuilder();
        sb.AppendLine("========== 진단 ==========");

        if (GameManager.Instance != null)
            sb.AppendLine($"골드: {GameManager.Instance.totalGold}   골드/초(총합): {(FlowerManager.Instance != null ? FlowerManager.Instance.GetTotalGoldPerSecond().ToString() : "-")}");

        if (FlowerManager.Instance != null)
        {
            sb.AppendLine("-- 꽃별 --");
            foreach (FlowerData data in FlowerManager.Instance.allFlowers)
            {
                if (data == null) continue;
                FlowerInstance instance = FlowerManager.Instance.GetInstance(data.flowerId);
                if (instance == null) { sb.AppendLine($"  {data.displayName}: 미보유"); continue; }
                if (!instance.isBloomed) { sb.AppendLine($"  {data.displayName}: 보유·미개화"); continue; }
                BigNumber gps = FlowerManager.Instance.GetEffectiveGoldPerSecond(data, instance.currentLevel, instance.bondLevel, data.flowerId);
                sb.AppendLine($"  {data.displayName}: Lv.{instance.currentLevel}, 유대 Lv.{instance.bondLevel}, G/s={gps}");
            }
        }

        if (GardenManager.Instance != null)
        {
            GardenManager gm = GardenManager.Instance;
            sb.AppendLine($"-- 정원: {gm.Width}×{gm.Height}, 시듦 단계: {gm.GetCurrentWiltStageLabel()} (손질 후 경과 {gm.GetElapsedSecondsSinceTended():F0}초)");
        }

        SetResult(sb.ToString());
    }

    private void SetResult(string text)
    {
        if (resultText != null) resultText.text = text;
    }

    private static double ParseDouble(TMP_InputField field) =>
        field != null && double.TryParse(field.text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : 0;

    private static int ParseInt(TMP_InputField field, int fallback) =>
        field != null && int.TryParse(field.text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : fallback;
}
