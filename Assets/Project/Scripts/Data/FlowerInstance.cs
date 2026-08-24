using System;
using UnityEngine;

/// <summary>
/// 플레이 중 변하는 꽃의 실제 상태.
/// FlowerData(고정 데이터)와 분리해서, 저장/불러오기와 여러 꽃 동시 관리를 쉽게 한다.
/// MonoBehaviour가 아닌 순수 C# 클래스 (직렬화 가능) - 저장 시스템에서 그대로 JSON 변환 가능.
/// </summary>
[Serializable]
public class FlowerInstance
{
    public string flowerId;        // 어떤 FlowerData인지 (id로 참조, 저장 데이터 키)
    public float currentAffection; // 현재 애정 (성장 단계에서만 의미 있음)
    public int currentLevel;       // 개화 후 레벨 (개화 전엔 0)
    public bool isBloomed;         // 개화 여부

    public FlowerInstance(string id)
    {
        flowerId = id;
        currentAffection = 0f;
        currentLevel = 0;
        isBloomed = false;
    }

    /// <summary> 성장률 (0~1). requiredAffection은 FlowerData에서 가져와 전달. </summary>
    public float GetGrowthPercent(int requiredAffection)
    {
        if (requiredAffection <= 0) return 1f;
        return Mathf.Clamp01(currentAffection / requiredAffection);
    }

    /// <summary> 현재 성장률에 따른 단계 판정. </summary>
    public GrowthStage GetGrowthStage(int requiredAffection)
    {
        float percent = GetGrowthPercent(requiredAffection);

        if (isBloomed || percent >= 1f) return GrowthStage.Bloomed;
        if (percent >= 0.50f) return GrowthStage.Growing;
        if (percent >= 0.25f) return GrowthStage.Sprout;
        return GrowthStage.Seed;                     // 0%
    }
}

public enum GrowthStage
{
    Seed,
    Sprout,
    Growing,
    Bloomed
}