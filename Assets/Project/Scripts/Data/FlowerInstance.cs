using System;
using System.Collections.Generic;
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

    /// <summary>
    /// 현재 애정 (성장 단계에서만 의미 있음).
    ///
    /// [반드시 double이어야 한다 — float이면 자동 애정이 조용히 멈춘다]
    /// float은 유효 정수 정밀도가 약 1,670만(2^24)까지다. 값이 838만(2^23)을 넘어서는 순간
    /// "표현 가능한 최소 간격(ULP)"이 1.0이 되고, 1,670만을 넘으면 2.0이 된다. 그런데 자동 애정은
    /// 매 프레임 `rate * Time.deltaTime`이라는 아주 작은 값을 더하는 방식이라 — 초당 129 기준으로
    /// 200fps면 프레임당 0.65 — 이 증가분이 ULP보다 작아지면 덧셈 결과가 원래 값으로 그대로
    /// 반올림되어 **증가가 통째로 사라진다**. 화면에 "0.0 애정/s"로 보였던 실제 원인이 이것이고,
    /// 오프라인 정산만 정상이었던 이유도 그쪽은 큰 값을 한 번에 더하기 때문이다.
    /// 후반 꽃의 requiredAffection이 수백만~수억(연꽃 3.3억)이라 이 구간은 반드시 도달한다.
    /// double은 정수 정밀도가 약 9,007조(2^53)라 이 게임의 어떤 수치에서도 같은 문제가 없다.
    /// (NumberFormatUtil이 골드 표기에서 double 경로를 유지하는 것과 정확히 같은 이유다.)
    /// </summary>
    public double currentAffection;
    public int currentLevel;       // 개화 후 레벨 (개화 전엔 0)
    public bool isBloomed;         // 개화 여부

    // ── 유대(Bond) — 개화한 꽃을 터치할 때만 오른다. FlowerManager.AddBond 참고. ──
    public double bond;            // 현재 레벨 구간에서 누적된 유대량 (레벨업 시 임계치만큼 차감되는 방식 — 누적 총량이 아님)
    public int bondLevel;          // 0 ~ BondData.maxBondLevel

    /// <summary> 이미 열람한 메모리얼의 unlockBondLevel 목록. 뱃지("안 읽음" 표시) 판정 전용. </summary>
    public List<int> readMemorialBondLevels = new List<int>();

    public FlowerInstance(string id)
    {
        flowerId = id;
        currentAffection = 0;
        currentLevel = 0;
        isBloomed = false;
        bond = 0;
        bondLevel = 0;
        readMemorialBondLevels = new List<int>();
    }

    /// <summary> 성장률 (0~1). requiredAffection은 FlowerData에서 가져와 전달. </summary>
    public float GetGrowthPercent(int requiredAffection)
    {
        if (requiredAffection <= 0) return 1f;
        // 나눗셈은 double로 하고, 결과(0~1)만 float으로 좁힌다 — 비율은 작은 수라 float으로 충분하다.
        return Mathf.Clamp01((float)(currentAffection / requiredAffection));
    }

    /// <summary>
    /// 현재 성장률에 따른 단계 판정. 0~33% Seed / 34~66% Sprout / 67~99% Growing / 100% Bloomed로
    /// 3등분(균등 33%씩)에 가깝게 나눈다 — 예전에는 0~25/26~50/51~99로 구간 폭이 들쭉날쭉했다.
    /// </summary>
    public GrowthStage GetGrowthStage(int requiredAffection)
    {
        float percent = GetGrowthPercent(requiredAffection);

        if (isBloomed || percent >= 1f) return GrowthStage.Bloomed;
        if (percent >= 0.67f) return GrowthStage.Growing;
        if (percent >= 0.34f) return GrowthStage.Sprout;
        return GrowthStage.Seed;                     // 0~33%
    }
}

public enum GrowthStage
{
    Seed,
    Sprout,
    Growing,
    Bloomed
}