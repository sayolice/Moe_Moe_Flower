using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 꽃 하나의 모든 밸런스 데이터를 담는 ScriptableObject.
/// 프로젝트 창에서 우클릭 -> Create -> FlowerGirl -> Flower Data 로 생성.
/// 값은 코드가 아니라 인스펙터에서 채워 넣는다.
/// </summary>
[CreateAssetMenu(fileName = "NewFlower", menuName = "FlowerGirl/Flower Data")]
public class FlowerData : ScriptableObject
{
    [Header("기본 정보")]
    public string flowerId;          // 예: "dandelion", "tulip" (저장 데이터 키로 사용)
    public string displayName;       // 예: "민들레"
    [TextArea] public string description;

    [Header("성장 단계 스프라이트 (4단계)")]
    public Sprite seedSprite;        // 씨앗 (0%)
    public Sprite sproutSprite;      // 발아 (0~25%)
    public Sprite growingSprite;     // 성장 (25~99%) - 스케일/이펙트로 세분화
    public Sprite bloomSprite;       // 개화 (100%) - 최종 꽃소녀 일러스트

    [Header("경제 밸런스 - 성장")]
    public int requiredAffection;    // 개화에 필요한 총 애정량
    public long seedPrice;           // 씨앗 구매 가격 (골드). 민들레는 0

    [Header("경제 밸런스 - 생산 (Lv.1 기준)")]
    public float baseGoldPerSecond;  // 개화 후 초당 골드 생산량 (Lv.1)

    [Header("레벨업 곡선")]
    public long levelUpBaseCost;     // Lv.1 -> Lv.2 비용
    public float levelUpGrowthRate = 1.20f; // 레벨업 비용 성장률 (기본 1.20)
    public float goldPerSecondGrowthRate = 1.15f; // 레벨업 시 G/s 증가율 (추후 조정)

    [Header("패시브 (0개 이상)")]
    public List<PassiveData> passives = new List<PassiveData>();

    // ===== 계산 함수 =====

    /// <summary> N레벨 레벨업 비용 계산 (1 -> 2 로 갈 때 N=1) </summary>
    public long GetLevelUpCost(int currentLevel)
    {
        double cost = levelUpBaseCost * System.Math.Pow(levelUpGrowthRate, currentLevel - 1);
        return (long)cost;
    }

    /// <summary> 특정 레벨에서의 초당 골드 생산량 </summary>
    public float GetGoldPerSecond(int level)
    {
        double gps = baseGoldPerSecond * System.Math.Pow(goldPerSecondGrowthRate, level - 1);
        return (float)gps;
    }

    /// <summary>
    /// fromLevel에서 시작해 levels번 연속 레벨업할 때 드는 총 비용 (미리보기 전용, 상태 변경 없음).
    /// +10/MAX 버튼에 표시할 "실제 총 비용" 계산에 사용.
    /// </summary>
    public long GetLevelUpCostForLevels(int fromLevel, int levels)
    {
        if (levels <= 0) return 0;

        long total = 0;
        for (int i = 0; i < levels; i++)
        {
            total += GetLevelUpCost(fromLevel + i);
        }
        return total;
    }

    /// <summary>
    /// fromLevel에서 시작해 주어진 골드로 몇 레벨까지 오를 수 있는지 계산 (미리보기 전용, 상태 변경 없음).
    /// +10(상한 계산)/MAX 버튼 표시에 사용. 밸런스 데이터 오류(성장률 1 이하 등)로 인한
    /// 무한루프를 막기 위한 안전 상한을 둔다.
    /// </summary>
    public int GetMaxAffordableLevels(int fromLevel, double gold)
    {
        const int SAFETY_CAP = 100000;

        int levels = 0;
        double remaining = gold;

        while (levels < SAFETY_CAP)
        {
            long cost = GetLevelUpCost(fromLevel + levels);
            if (cost > remaining) break;

            remaining -= cost;
            levels++;
        }

        return levels;
    }
}