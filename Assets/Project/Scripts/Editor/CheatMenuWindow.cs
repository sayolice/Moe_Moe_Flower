#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 개발자 치트 메뉴 (에디터 전용) — Tools > 꽃소녀 치트.
///
/// 유대 Lv.5(꽃 1종당 집중 클릭 5.6시간)·정원 인접 효과(유대 레벨에 비례해 단계적으로 강해짐)·후반 꽃 개화(최대
/// 12시간)·시듦 단계 전환(12h/24h/48h)·오프라인 정산은 실시간으로는 사실상 검증이 불가능해서
/// 이 창이 필요하다.
///
/// [절대 원칙]
/// - 이 파일은 Editor/ 폴더에 있어 릴리스 빌드에서 자동 제외된다.
/// - 런타임 스크립트(FlowerManager/GardenManager)에 추가한 Cheat_* 훅은 전부 #if UNITY_EDITOR로
///   감싸여 있다 — DEVELOPMENT_BUILD가 아니라 에디터 전용이다.
/// - 이 창은 상태만 조작한다. 개화는 AddAffection을, 유대 추가는 AddBond를, 시간 스킵은 기존
///   ApplyOfflineProgress를 그대로 재사용한다 — 별도 시뮬레이션 코드를 만들지 않는다.
/// </summary>
public class CheatMenuWindow : EditorWindow
{
    [MenuItem("Tools/꽃소녀 치트")]
    public static void Open() => GetWindow<CheatMenuWindow>("꽃소녀 치트");

    private Vector2 scroll;

    private string goldAmountInput = "1000000";
    private int selectedFlowerIndex;
    private string levelInput = "10";
    private string bondLevelInput = "5";
    private string bondAmountInput = "500";
    private string customHoursInput = "1";
    private int selectedGardenSizeIndex;
    private int selectedWiltStageIndex;

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        if (!EditorApplication.isPlaying)
            EditorGUILayout.HelpBox("Play 모드에서만 대부분의 기능을 쓸 수 있습니다. 먼저 Play를 눌러주세요.", MessageType.Info);

        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
        {
            DrawGoldSection();
            Space();
            DrawFlowerStateSection();
            Space();
            DrawBondSection();
            Space();
            DrawTimeSkipSection();
            Space();
            DrawGardenSection();
            Space();
            DrawDiagnosticsSection();
        }

        Space();
        DrawSaveSection(); // Play 모드가 아니어도 경로 확인/폴더 열기는 가능해서 별도로 뺀다

        EditorGUILayout.EndScrollView();
    }

    private static void Space() => EditorGUILayout.Space(10);

    // ===================================================================
    // 1. 재화
    // ===================================================================
    private void DrawGoldSection()
    {
        EditorGUILayout.LabelField("1. 재화", EditorStyles.boldLabel);
        if (GameManager.Instance == null)
        {
            EditorGUILayout.HelpBox("GameManager.Instance 없음", MessageType.Warning);
            return;
        }

        goldAmountInput = EditorGUILayout.TextField("골드 값 (지수 표기 가능, 예: 1e12)", goldAmountInput);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("골드 추가")) AddGold(ParseDouble(goldAmountInput));
        if (GUILayout.Button("골드 설정")) GameManager.Instance.SetGold(BigNumber.FromDouble(ParseDouble(goldAmountInput)));
        if (GUILayout.Button("골드 초기화")) GameManager.Instance.SetGold(BigNumber.Zero);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField("프리셋");
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+1만")) AddGold(1e4);
        if (GUILayout.Button("+100만")) AddGold(1e6);
        if (GUILayout.Button("+1억")) AddGold(1e8);
        if (GUILayout.Button("+1조")) AddGold(1e12);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField($"현재 골드: {GameManager.Instance.totalGold}");
    }

    private void AddGold(double amount)
    {
        if (GameManager.Instance == null || amount == 0) return;
        GameManager.Instance.AddGold(BigNumber.FromDouble(amount));
    }

    // ===================================================================
    // 2. 꽃 상태
    // ===================================================================
    private void DrawFlowerStateSection()
    {
        EditorGUILayout.LabelField("2. 꽃 상태", EditorStyles.boldLabel);
        if (FlowerManager.Instance == null)
        {
            EditorGUILayout.HelpBox("FlowerManager.Instance 없음", MessageType.Warning);
            return;
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("모든 꽃 즉시 보유")) FlowerManager.Instance.Cheat_OwnAllFlowers();
        if (GUILayout.Button("모든 꽃 즉시 개화")) FlowerManager.Instance.Cheat_ForceBloomAllOwned();
        EditorGUILayout.EndHorizontal();

        DrawFlowerPicker();
        if (GUILayout.Button("선택한 꽃 즉시 개화") && CurrentFlowerId != null)
            FlowerManager.Instance.Cheat_ForceBloom(CurrentFlowerId);

        EditorGUILayout.Space(4);
        levelInput = EditorGUILayout.TextField("레벨", levelInput);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("모든 꽃 레벨 설정")) FlowerManager.Instance.Cheat_SetAllLevels(ParseInt(levelInput, 1));
        if (GUILayout.Button("선택한 꽃 레벨 설정") && CurrentFlowerId != null)
            FlowerManager.Instance.Cheat_SetFlowerLevel(CurrentFlowerId, ParseInt(levelInput, 1));
        EditorGUILayout.EndHorizontal();
    }

    private string CurrentFlowerId
    {
        get
        {
            if (FlowerManager.Instance == null) return null;
            List<FlowerData> flowers = FlowerManager.Instance.allFlowers;
            if (flowers == null || selectedFlowerIndex < 0 || selectedFlowerIndex >= flowers.Count) return null;
            return flowers[selectedFlowerIndex] != null ? flowers[selectedFlowerIndex].flowerId : null;
        }
    }

    private void DrawFlowerPicker()
    {
        if (FlowerManager.Instance == null) return;
        List<FlowerData> flowers = FlowerManager.Instance.allFlowers;
        if (flowers == null || flowers.Count == 0)
        {
            EditorGUILayout.HelpBox("allFlowers가 비어 있습니다.", MessageType.Warning);
            return;
        }

        string[] names = flowers.Select(f => f != null ? f.displayName : "(null)").ToArray();
        selectedFlowerIndex = EditorGUILayout.Popup("꽃 선택", Mathf.Clamp(selectedFlowerIndex, 0, names.Length - 1), names);
    }

    // ===================================================================
    // 3. 유대
    // ===================================================================
    private void DrawBondSection()
    {
        EditorGUILayout.LabelField("3. 유대", EditorStyles.boldLabel);
        if (FlowerManager.Instance == null)
        {
            EditorGUILayout.HelpBox("FlowerManager.Instance 없음", MessageType.Warning);
            return;
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("모든 꽃 유대 Lv.5")) FlowerManager.Instance.Cheat_SetAllBondLevels(5);
        if (GUILayout.Button("모든 꽃 유대 Lv.0")) FlowerManager.Instance.Cheat_SetAllBondLevels(0);
        EditorGUILayout.EndHorizontal();

        DrawFlowerPicker();

        EditorGUILayout.BeginHorizontal();
        bondLevelInput = EditorGUILayout.TextField("유대 레벨 (0~5)", bondLevelInput);
        if (GUILayout.Button("설정") && CurrentFlowerId != null)
            FlowerManager.Instance.Cheat_SetFlowerBondLevel(CurrentFlowerId, ParseInt(bondLevelInput, 0));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        bondAmountInput = EditorGUILayout.TextField("유대 추가량", bondAmountInput);
        if (GUILayout.Button("선택한 꽃에 유대 추가") && CurrentFlowerId != null)
        {
            FlowerInstance instance = FlowerManager.Instance.GetInstance(CurrentFlowerId);
            if (instance != null)
                FlowerManager.Instance.AddBond(instance, ParseDouble(bondAmountInput)); // AddBond를 그대로 호출
        }
        EditorGUILayout.EndHorizontal();
    }

    // ===================================================================
    // 4. 시간 스킵 (가장 중요)
    // ===================================================================
    private void DrawTimeSkipSection()
    {
        EditorGUILayout.LabelField("4. 시간 스킵 (가장 중요)", EditorStyles.boldLabel);
        if (FlowerManager.Instance == null)
        {
            EditorGUILayout.HelpBox("FlowerManager.Instance 없음", MessageType.Warning);
            return;
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("1h")) SkipHours(1);
        if (GUILayout.Button("6h")) SkipHours(6);
        if (GUILayout.Button("12h")) SkipHours(12);
        if (GUILayout.Button("24h")) SkipHours(24);
        if (GUILayout.Button("48h")) SkipHours(48);
        if (GUILayout.Button("72h")) SkipHours(72);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        customHoursInput = EditorGUILayout.TextField("직접 입력(시간)", customHoursInput);
        if (GUILayout.Button("스킵")) SkipHours(ParseDouble(customHoursInput));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox("결과는 Console에 로그로 출력됩니다.", MessageType.None);
    }

    /// <summary>
    /// System.DateTime.UtcNow는 건드리지 않는다. 대신 (1) 정원의 "마지막 손질 시각"만 스킵한
    /// 시간만큼 과거로 되돌려서(GardenManager.Cheat_RewindTendedClock) 시듦 계산이 실제로 그만큼
    /// 시간이 흐른 것처럼 보이게 하고, (2) 기존 FlowerManager.ApplyOfflineProgress(elapsedSeconds)를
    /// 그대로 호출한다 — 실제 오프라인 복귀와 완전히 동일한 경로다. 새 시뮬레이션 코드는 없다.
    /// </summary>
    private void SkipHours(double hours)
    {
        if (hours <= 0 || FlowerManager.Instance == null) return;
        double seconds = hours * 3600.0;

        string wiltBefore = GardenManager.Instance != null ? GardenManager.Instance.GetCurrentWiltStageLabel() : "(정원 없음)";

        if (GardenManager.Instance != null)
            GardenManager.Instance.Cheat_RewindTendedClock(seconds);

        OfflineSettlementResult result = FlowerManager.Instance.ApplyOfflineProgress(seconds);

        string wiltAfter = GardenManager.Instance != null ? GardenManager.Instance.GetCurrentWiltStageLabel() : "(정원 없음)";

        var sb = new StringBuilder();
        sb.AppendLine($"[치트] 시간 스킵 {hours:0.##}h 정산 결과");
        sb.AppendLine($"  경과 시간: {result.elapsedSeconds:F0}초 ({result.elapsedSeconds / 3600.0:F2}h)");
        sb.AppendLine($"  획득 골드: {result.goldEarned:F2}");
        sb.AppendLine($"  오프라인 중 개화: {(result.newlyBloomedFlowerIds.Count == 0 ? "없음" : string.Join(", ", result.newlyBloomedFlowerIds))}");
        sb.AppendLine($"  정원 유대 획득(합계): {result.gardenBondEarned:F2}");
        sb.AppendLine($"  정원 유대 레벨업: {(result.gardenBondLeveledFlowerIds.Count == 0 ? "없음" : string.Join(", ", result.gardenBondLeveledFlowerIds))}");
        sb.AppendLine($"  시듦 단계: {wiltBefore} → {wiltAfter}");
        Debug.Log(sb.ToString());
    }

    // ===================================================================
    // 5. 정원 · 시듦
    // ===================================================================
    private void DrawGardenSection()
    {
        EditorGUILayout.LabelField("5. 정원 · 시듦", EditorStyles.boldLabel);
        if (GardenManager.Instance == null)
        {
            EditorGUILayout.HelpBox("GardenManager.Instance 없음", MessageType.Warning);
            return;
        }

        List<GardenData.GardenSize> sizes = GardenManager.Instance.ActiveGardenData.sizes;
        if (sizes != null && sizes.Count > 0)
        {
            string[] sizeLabels = sizes.Select(s => $"{s.width * s.height}칸 ({s.width}×{s.height})").ToArray();
            EditorGUILayout.BeginHorizontal();
            selectedGardenSizeIndex = EditorGUILayout.Popup("정원 확장 단계",
                Mathf.Clamp(selectedGardenSizeIndex, 0, sizeLabels.Length - 1), sizeLabels);
            if (GUILayout.Button("적용", GUILayout.Width(60)))
                GardenManager.Instance.Cheat_SetGardenSizeIndex(selectedGardenSizeIndex);
            EditorGUILayout.EndHorizontal();
        }

        if (GUILayout.Button("정원 배치 전체 해제")) GardenManager.Instance.Cheat_ClearAllPlacements();

        EditorGUILayout.Space(4);

        List<WiltStage> stages = GardenManager.Instance.ActiveGardenData.wiltStages;
        if (stages != null && stages.Count > 0)
        {
            string[] stageLabels = stages.Select(s => s.label).ToArray();
            EditorGUILayout.BeginHorizontal();
            selectedWiltStageIndex = EditorGUILayout.Popup("시듦 단계 강제 설정",
                Mathf.Clamp(selectedWiltStageIndex, 0, stageLabels.Length - 1), stageLabels);
            if (GUILayout.Button("적용", GUILayout.Width(60)))
                GardenManager.Instance.Cheat_ForceWiltStage(selectedWiltStageIndex);
            EditorGUILayout.EndHorizontal();
        }

        if (GUILayout.Button("정원 즉시 복구")) GardenManager.Instance.Cheat_FullyRestoreGarden();

        EditorGUILayout.Space(4);
        if (GUILayout.Button("인접 효과 재계산"))
        {
            GardenManager.Instance.RecomputeNow();
            Debug.Log("[치트] 인접 효과 캐시를 다시 계산했습니다.");
        }
        EditorGUILayout.HelpBox(
            "Reload Domain을 끈 채(Enter Play Mode Options) Play 모드를 오래 켜두고 스크립트만 여러 번 " +
            "고친 경우, 코드는 최신이어도 이미 배치된 꽃의 인접 효과 캐시는 마지막 배치 변경 시점 값 " +
            "그대로일 수 있습니다. 값이 이상해 보이면 이 버튼으로 강제 재계산하거나, Play 모드를 " +
            "껐다 켜서 깨끗한 상태에서 다시 확인하세요.", MessageType.Info);
    }

    // ===================================================================
    // 6. 진단 출력
    // ===================================================================
    private void DrawDiagnosticsSection()
    {
        EditorGUILayout.LabelField("6. 진단 출력", EditorStyles.boldLabel);
        if (GUILayout.Button("현재 상태 출력")) LogDiagnostics();
        EditorGUILayout.HelpBox("결과는 Console에 로그로 출력됩니다.", MessageType.None);
    }

    private void LogDiagnostics()
    {
        var sb = new StringBuilder();
        sb.AppendLine("========== [치트] 현재 상태 진단 ==========");

        if (GameManager.Instance != null)
            sb.AppendLine($"골드: {GameManager.Instance.totalGold}   골드/초(총합): {(FlowerManager.Instance != null ? FlowerManager.Instance.GetTotalGoldPerSecond().ToString() : "-")}");

        if (PlayerStatManager.Instance != null)
        {
            sb.AppendLine("-- 플레이어 스탯 --");
            foreach (PlayerStatType type in Enum.GetValues(typeof(PlayerStatType)))
            {
                int lvl = PlayerStatManager.Instance.GetLevel(type);
                BigNumber val = PlayerStatManager.Instance.GetCurrentValue(type);
                sb.AppendLine($"  {type}: Lv.{lvl}, 값={val}");
            }
        }

        if (FlowerManager.Instance != null)
        {
            sb.AppendLine("-- 꽃별 --");
            foreach (FlowerData data in FlowerManager.Instance.allFlowers)
            {
                if (data == null) continue;
                AppendFlowerDiagnostics(sb, data);
            }
        }

        if (GardenManager.Instance != null)
        {
            GardenManager gm = GardenManager.Instance;
            sb.AppendLine("-- 정원 --");
            sb.AppendLine($"  격자: {gm.Width}×{gm.Height} (확장 단계 {gm.CurrentSizeIndex})");
            foreach (string id in gm.GetPlacedFlowerIds())
            {
                Vector2Int? origin = gm.GetPlacementOrigin(id);
                sb.AppendLine($"  배치: {id} @ {origin}");
            }
            sb.AppendLine($"  시듦 단계: {gm.GetCurrentWiltStageLabel()} (손질 후 경과 {gm.GetElapsedSecondsSinceTended():F0}초)");
        }
        else
        {
            sb.AppendLine("-- 정원: 없음 --");
        }

        AppendSaveDiagnostics(sb);

        sb.AppendLine("============================================");
        Debug.Log(sb.ToString());
    }

    /// <summary>
    /// 꽃 1종의 G/s를 단계별로 나눠 출력한다 — 최종 G/s만 보면 어느 단계에서 계산이 어긋났는지
    /// 알 수 없다는 지시서 요구사항. "기본 G/s × 패시브 배율 × 유대 배율 × 정원 배율(최종)"의 곱이
    /// FlowerManager.GetEffectiveGoldPerSecond의 실제 결과와 정확히 같아야 한다(검증 6번) —
    /// 인접 원본 보너스/시듦 심각도는 정원 배율이 "왜" 그 값인지 보여주는 참고용 분해치이며, 그
    /// 둘의 곱은 정원 배율 자체와 다르다(공식이 1+fraction×wilt이지 (1+fraction)×wilt가 아니므로) —
    /// 그래서 곱연산 체인에는 정원 배율(최종) 하나만 넣는다.
    /// </summary>
    private void AppendFlowerDiagnostics(StringBuilder sb, FlowerData data)
    {
        FlowerInstance instance = FlowerManager.Instance.GetInstance(data.flowerId);
        bool owned = instance != null;
        bool bloomed = owned && instance.isBloomed;

        if (!owned)
        {
            sb.AppendLine($"  {data.displayName}({data.flowerId}): 미보유");
            return;
        }

        if (!bloomed)
        {
            sb.AppendLine($"  {data.displayName}({data.flowerId}): 보유·미개화 (애정 {instance.currentAffection:F1}/{data.requiredAffection})");
            return;
        }

        int level = instance.currentLevel;
        int bondLevel = instance.bondLevel;

        BigNumber baseGps = data.GetGoldPerSecond(level);
        float bondMult = FlowerManager.Instance.GetBondGoldMultiplier(bondLevel);
        float passiveMult = PassiveManager.Instance != null
            ? PassiveManager.Instance.GetTotalMultiplier(PassiveEffectType.GoldPerSecondBonusPercent)
            : 1f;
        float gardenMult = GardenManager.Instance != null
            ? GardenManager.Instance.GetGoldMultiplierForFlower(data.flowerId)
            : 1f;

        BigNumber finalGps = FlowerManager.Instance.GetEffectiveGoldPerSecond(data, level, bondLevel, data.flowerId);
        BigNumber computedCheck = baseGps * passiveMult * bondMult * gardenMult;

        sb.AppendLine($"  {data.displayName}({data.flowerId}): Lv.{level}, 유대 Lv.{bondLevel}({instance.bond:F1})");
        sb.AppendLine($"    기본 G/s={baseGps} × 패시브={passiveMult:F3} × 유대={bondMult:F3} × 정원={gardenMult:F3} = {computedCheck} (실제 최종 G/s={finalGps})");

        if (GardenManager.Instance != null && GardenManager.Instance.IsPlaced(data.flowerId))
        {
            float rawAdjacency = GardenManager.Instance.GetRawAdjacencyMultiplierForFlower(data.flowerId);
            float wiltSeverity = GardenManager.Instance.GetWiltSeverityForFlower(data.flowerId);
            sb.AppendLine($"    (참고: 정원 배율 분해 — 인접 원본 보너스={rawAdjacency:F3}, 시듦 심각도={wiltSeverity:F3}, 정원배율=1+({rawAdjacency:F3}-1)×{wiltSeverity:F3})");
        }
    }

    // ===================================================================
    // 7. 세이브
    // ===================================================================
    private const string SaveFileName = "savedata.json"; // SaveManager.SaveFileName과 동일 문자열(진단 전용 참조)

    private void DrawSaveSection()
    {
        EditorGUILayout.LabelField("7. 세이브", EditorStyles.boldLabel);

        string path = Path.Combine(Application.persistentDataPath, SaveFileName);

        if (GUILayout.Button("세이브 파일 경로 출력")) Debug.Log($"[치트] 세이브 파일 경로: {path}");
        if (GUILayout.Button("세이브 폴더 열기")) EditorUtility.RevealInFinder(Application.persistentDataPath);

        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying || SaveManager.Instance == null))
        {
            if (GUILayout.Button("즉시 저장")) SaveManager.Instance.Save();

            if (GUILayout.Button("세이브 삭제 후 재시작"))
            {
                bool confirmed = EditorUtility.DisplayDialog(
                    "세이브 삭제 후 재시작",
                    "세이브 파일을 삭제하고 모든 진행 상태를 초기 상태로 되돌립니다.\n되돌릴 수 없습니다. 계속할까요?",
                    "삭제", "취소");
                if (confirmed)
                {
                    PlayerPrefs.DeleteAll(); // 이 프로젝트는 현재 PlayerPrefs를 쓰지 않지만, 지시서에 명시돼 있어 방어적으로 함께 지운다
                    SaveManager.Instance.ResetAllData();
                    Debug.Log("[치트] 세이브 삭제 후 초기 상태로 재시작했습니다. " +
                              "'세이브 삭제 후 첫 실행 시 오프라인 수익 0' 자체를 검증하려면, " +
                              "Play 모드를 껐다가 다시 켜서 SaveManager.Load()가 '파일 없음' 경로를 타는지 확인하세요.");
                }
            }
        }
    }

    private void AppendSaveDiagnostics(StringBuilder sb)
    {
        string path = Path.Combine(Application.persistentDataPath, SaveFileName);
        sb.AppendLine($"세이브 파일 경로: {path}");

        if (!File.Exists(path))
        {
            sb.AppendLine("마지막 저장 시각: (세이브 파일 없음)");
            return;
        }

        try
        {
            string json = File.ReadAllText(path);
            SaveData data = JsonUtility.FromJson<SaveData>(json);
            if (data != null && data.lastSaveTimeTicksUtc > 0)
            {
                DateTime lastSave = new DateTime(data.lastSaveTimeTicksUtc, DateTimeKind.Utc);
                sb.AppendLine($"마지막 저장 시각(UTC): {lastSave:yyyy-MM-dd HH:mm:ss}");
            }
            else
            {
                sb.AppendLine("마지막 저장 시각: 0 (신규 저장 또는 손상된 값)");
            }
        }
        catch (Exception e)
        {
            sb.AppendLine($"마지막 저장 시각: 세이브 파일 읽기 실패 ({e.Message})");
        }
    }

    // ===================================================================
    // 파싱 유틸
    // ===================================================================
    private static double ParseDouble(string text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : 0;

    private static int ParseInt(string text, int fallback) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;
}
#endif
