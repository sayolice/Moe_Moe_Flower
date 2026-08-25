using System;
using System.Collections.Generic;

/// <summary>
/// 꽃 1개의 저장용 스냅샷. FlowerInstance(런타임 상태)와 분리된 순수 데이터 클래스.
/// JsonUtility는 기본(무인자) 생성자를 요구하므로 반드시 남겨둔다.
/// </summary>
[Serializable]
public class FlowerSaveEntry
{
    public string flowerId;
    public float currentAffection;
    public int currentLevel;
    public bool isBloomed;

    public FlowerSaveEntry() { }

    public FlowerSaveEntry(FlowerInstance instance)
    {
        flowerId = instance.flowerId;
        currentAffection = instance.currentAffection;
        currentLevel = instance.currentLevel;
        isBloomed = instance.isBloomed;
    }

    /// <summary> 저장된 값을 바탕으로 런타임 FlowerInstance를 새로 만든다. </summary>
    public FlowerInstance ToInstance()
    {
        FlowerInstance instance = new FlowerInstance(flowerId)
        {
            currentAffection = currentAffection,
            currentLevel = currentLevel,
            isBloomed = isBloomed
        };
        return instance;
    }
}

/// <summary> 플레이어 스탯 1개(터치 애정/터치 골드/자동 애정)의 저장용 레벨 스냅샷. </summary>
[Serializable]
public class PlayerStatSaveEntry
{
    public PlayerStatType type;
    public int currentLevel;
}

/// <summary>
/// 세이브 파일 전체 구조. JsonUtility로 그대로 직렬화한다 (Dictionary 미지원이라 List만 사용).
/// </summary>
[Serializable]
public class SaveData
{
    public int saveVersion = 1;

    public double totalGold;
    public string currentDisplayedFlowerId;

    public List<FlowerSaveEntry> flowers = new List<FlowerSaveEntry>();
    public List<PlayerStatSaveEntry> playerStats = new List<PlayerStatSaveEntry>();

    /// <summary> 오프라인 수익 계산 기준 시각 (UTC, DateTime.Ticks). </summary>
    public long lastSaveTimeTicksUtc;
}
