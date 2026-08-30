using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 정원 시스템의 전역 밸런스 데이터. BondData/PlayerStatData와 동일한 원칙 — 프로젝트에 단 1개만
/// 존재하고, 값은 코드가 아니라 인스펙터에서 조정한다. 초안 수치는 전부 미확정이므로 여기서
/// 쉽게 바꿀 수 있어야 한다(요청 명세 1.4).
/// </summary>
[CreateAssetMenu(menuName = "Flower/GardenData")]
public class GardenData : ScriptableObject
{
    [System.Serializable]
    public class GardenSize
    {
        public int width;
        public int height;
        public double unlockCost;
    }

    [Header("확장 단계 (0번째가 초기 크기, unlockCost는 그 단계로 확장하는 비용 — 0번째는 무료)")]
    public List<GardenSize> sizes = new List<GardenSize>
    {
        new GardenSize { width = 3, height = 2, unlockCost = 0 },
        new GardenSize { width = 5, height = 3, unlockCost = 50000 },
        new GardenSize { width = 6, height = 5, unlockCost = 5000000 },
    };

    [Header("정원 유대 (절대 배율을 곱하지 말 것 — 항상 이 고정값 그대로, BondData와 동일한 원칙)")]
    public double bondPerSecondInGarden = 1.0;

    [Header("시듦 — 인접 효과 보너스에만 곱해지는 배율(기본 G/s·개화·유대는 절대 건드리지 않음)")]
    [Tooltip("이 시간(초) 미만 방치는 시들지 않는다 — 수면 중에는 시들지 않아야 하므로 12시간 유예를 둔다.")]
    public double wiltGracePeriodSeconds = 43200; // 12h

    [Tooltip("afterSeconds 오름차순으로 정렬되어 있어야 한다. 마지막 손질(lastTendedTime) 이후 경과 시간이 " +
             "이 값 이상이면 이 단계가 적용된다(더 큰 afterSeconds를 만족하는 단계가 우선).")]
    public List<WiltStage> wiltStages = new List<WiltStage>
    {
        new WiltStage { afterSeconds = 0,      adjacencyMultiplier = 1.00f, label = "양호" },
        new WiltStage { afterSeconds = 43200,  adjacencyMultiplier = 0.75f, label = "약간 시듦" },  // 12h
        new WiltStage { afterSeconds = 86400,  adjacencyMultiplier = 0.50f, label = "시듦" },        // 24h
        new WiltStage { afterSeconds = 172800, adjacencyMultiplier = 0.25f, label = "많이 시듦" },   // 48h
    };

    [Tooltip("시든 타일 1개를 완전히 회복하는 데 필요한 터치 횟수.")]
    public int touchesPerPlacedFlowerToFullyRestore = 3;

    /// <summary> elapsedSeconds에 해당하는 시듦 배율. wiltStages가 비어 있으면 항상 1(시듦 없음). </summary>
    public float GetWiltMultiplier(double elapsedSeconds)
    {
        if (wiltStages == null || wiltStages.Count == 0) return 1f;

        float multiplier = 1f;
        foreach (WiltStage stage in wiltStages)
        {
            if (elapsedSeconds >= stage.afterSeconds) multiplier = stage.adjacencyMultiplier;
        }
        return multiplier;
    }

    /// <summary> elapsedSeconds에 해당하는 시듦 단계 이름(UI 표시용). </summary>
    public string GetWiltStageLabel(double elapsedSeconds)
    {
        if (wiltStages == null || wiltStages.Count == 0) return "";

        string label = "";
        foreach (WiltStage stage in wiltStages)
        {
            if (elapsedSeconds >= stage.afterSeconds) label = stage.label;
        }
        return label;
    }

    /// <summary>
    /// elapsedSeconds 시점 기준으로 "다음 시듦 단계 경계까지 남은 시간"(초). 이미 마지막 단계를
    /// 지났으면 더 이상 경계가 없으므로 double.PositiveInfinity를 반환한다 — 오프라인 정산이 이
    /// 값으로 구간을 쪼개다가, 더 쪼갤 경계가 없으면(무한대) 자연히 나머지 시간 전체를 한 구간으로
    /// 처리하게 된다.
    /// </summary>
    public double GetTimeToNextWiltStageBoundary(double elapsedSeconds)
    {
        if (wiltStages == null || wiltStages.Count == 0) return double.PositiveInfinity;

        double next = double.PositiveInfinity;
        foreach (WiltStage stage in wiltStages)
        {
            if (stage.afterSeconds > elapsedSeconds && stage.afterSeconds < next)
                next = stage.afterSeconds;
        }
        return next == double.PositiveInfinity ? double.PositiveInfinity : next - elapsedSeconds;
    }
}

[System.Serializable]
public class WiltStage
{
    public double afterSeconds;
    public float adjacencyMultiplier;
    public string label;
}
