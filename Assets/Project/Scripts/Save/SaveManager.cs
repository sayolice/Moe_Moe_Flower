using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>
/// 게임 진행 상태를 로컬 파일(JSON)로 저장/불러오는 싱글턴.
/// GameManager(골드) / FlowerManager(꽃) / PlayerStatManager(플레이어 스탯)를 읽고 써서
/// 하나의 SaveData로 직렬화한다. 이 클래스는 세 매니저의 필드를 직접 알지 않고,
/// 각 매니저가 노출하는 저장 전용 공개 API(GetAllOwnedInstances/LoadOwnedFlowers 등)만 사용한다.
///
/// 오프라인 정산 규칙 (사용자 확정):
///   - 획득률 100%(감산 없음), 시간 상한 없음 — 앱을 켜두든 말든 결과가 같다.
///   - 골드와 애정 둘 다 정산한다(터치 애정은 제외 — 클릭이 없으므로).
///   - 실제 정산 계산(개화 시점을 나눠 구간별로 처리)은 FlowerManager.ApplyOfflineProgress가 담당한다.
///     이 클래스는 "경과 시간이 얼마인지"만 UTC 기준으로 계산해서 넘겨줄 뿐, 꽃 상태를 직접 건드리지 않는다.
/// </summary>
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    [Header("자동 저장 주기 (초) - 밸런스 값 아님, 엔지니어링 기본값")]
    public float autoSaveInterval = 30f;

    /// <summary> 불러오기 시 오프라인 정산이 끝나면 발생. UI(OfflineSummaryPopup)가 구독. </summary>
    public event Action<OfflineSettlementResult> OnOfflineSettlementApplied;

    private const string SaveFileName = "savedata.json";
    private string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

    private float autoSaveTimer;

    /// <summary> Load() 진행 중(오프라인 정산 포함)에는 true — 아래 이벤트 기반 저장이 이 창에서 스스로
    /// 다시 저장을 트리거하는 것을 막는다(불러오는 중간 상태를 저장해버리는 것을 방지). </summary>
    private bool isLoading;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // 모든 싱글턴의 Awake가 끝난 뒤 호출되는 것이 Unity의 보장 순서이므로,
        // GameManager/FlowerManager/PlayerStatManager.Instance는 여기서 이미 non-null이다.
        // FlowerManager.Start()(튜토리얼 자동 지급)와의 실행 순서는 상관없다 —
        // 저장 파일이 있으면 LoadOwnedFlowers가 목록을 통째로 덮어쓰고,
        // 없으면 FlowerManager 쪽 최초 지급 로직이 정상 진행된다.
        Load();

        // 구매/개화처럼 "잃으면 아까운" 중요한 순간에는 30초를 기다리지 않고 즉시 저장한다.
        // isLoading 가드 덕분에 Load() 안에서(튜토리얼 최초 지급, 오프라인 정산 중 개화 등)
        // 같은 이벤트가 발생해도 여기서 다시 저장을 트리거하지 않는다.
        if (FlowerManager.Instance != null)
        {
            FlowerManager.Instance.OnFlowerBloomed += HandleFlowerBloomed;
            FlowerManager.Instance.OnOwnedFlowersChanged += HandleOwnedFlowersChanged;
        }
    }

    private void OnDestroy()
    {
        if (FlowerManager.Instance != null)
        {
            FlowerManager.Instance.OnFlowerBloomed -= HandleFlowerBloomed;
            FlowerManager.Instance.OnOwnedFlowersChanged -= HandleOwnedFlowersChanged;
        }
    }

    private void HandleFlowerBloomed(string _) => SaveUnlessLoading();
    private void HandleOwnedFlowersChanged() => SaveUnlessLoading();

    private void SaveUnlessLoading()
    {
        if (isLoading) return;
        Save();
    }

    private void Update()
    {
        autoSaveTimer += Time.deltaTime;
        if (autoSaveTimer < autoSaveInterval) return;

        autoSaveTimer = 0f;
        Save();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus) Save(); // 모바일: 백그라운드 전환 시 저장
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        // PC: 창 포커스를 잃을 때(다른 창 클릭 등)도 저장 시각을 갱신해야
        // "오프라인 정산"이 실제 방치 시작 시점부터 정확히 계산된다.
        if (!hasFocus) Save();
    }

    private void OnApplicationQuit()
    {
        Save(); // PC: 종료 시 저장
    }

    public void Save()
    {
        if (GameManager.Instance == null || FlowerManager.Instance == null) return;

        SaveData data = new SaveData
        {
            totalGold = GameManager.Instance.totalGold,
            currentDisplayedFlowerId = FlowerManager.Instance.CurrentDisplayedFlowerId,
            lastSaveTimeTicksUtc = DateTime.UtcNow.Ticks
        };

        foreach (FlowerInstance instance in FlowerManager.Instance.GetAllOwnedInstances())
            data.flowers.Add(new FlowerSaveEntry(instance));

        if (PlayerStatManager.Instance != null)
            data.playerStats = PlayerStatManager.Instance.GetAllLevelsForSave();

        try
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(SavePath, json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] 저장 실패: {e}");
        }
    }

    public void Load()
    {
        if (!File.Exists(SavePath)) return; // 최초 실행 - 저장 파일 없음, 정상 상태

        SaveData data;
        try
        {
            string json = File.ReadAllText(SavePath);
            data = JsonUtility.FromJson<SaveData>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] 불러오기 실패: {e}");
            return;
        }

        if (data == null) return;

        isLoading = true;
        try
        {
            if (GameManager.Instance != null)
                GameManager.Instance.SetGold(data.totalGold);

            List<FlowerInstance> loadedFlowers = data.flowers.Select(f => f.ToInstance()).ToList();
            if (FlowerManager.Instance != null)
                FlowerManager.Instance.LoadOwnedFlowers(loadedFlowers, data.currentDisplayedFlowerId);

            if (PlayerStatManager.Instance != null)
                PlayerStatManager.Instance.LoadLevels(data.playerStats);

            double elapsedSeconds = ComputeElapsedSecondsSinceLastSave(data.lastSaveTimeTicksUtc);

            OfflineSettlementResult result = FlowerManager.Instance != null
                ? FlowerManager.Instance.ApplyOfflineProgress(elapsedSeconds)
                : new OfflineSettlementResult { elapsedSeconds = elapsedSeconds };

            OnOfflineSettlementApplied?.Invoke(result);
        }
        finally
        {
            isLoading = false;
        }
    }

    /// <summary>
    /// 마지막 저장 시각(UTC) 이후 경과 시간(초). 기기 로컬 타임존이 바뀌어도 영향받지 않도록
    /// 항상 UTC 기준으로 계산하고, 시스템 시계가 되돌아간 경우 등 음수가 나오면 0으로 처리한다.
    /// </summary>
    private double ComputeElapsedSecondsSinceLastSave(long lastSaveTicksUtc)
    {
        if (lastSaveTicksUtc <= 0) return 0;

        DateTime lastSaveUtc = new DateTime(lastSaveTicksUtc, DateTimeKind.Utc);
        double elapsedSeconds = (DateTime.UtcNow - lastSaveUtc).TotalSeconds;
        return elapsedSeconds > 0 ? elapsedSeconds : 0;
    }
}
