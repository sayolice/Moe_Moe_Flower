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

    [Header("유대(Bond) 메모리얼 — 꽃마다 다름 (임계치/배율은 여기 없음, BondData 하나로 전 꽃 공통 관리)")]
    [Tooltip("텍스트는 아직 비어 있을 수 있다 — 비어 있어도 레벨업/배율 시스템은 정상 동작해야 한다.")]
    public List<MemorialData> memorialEntries = new List<MemorialData>();

    // ===== 계산 함수 =====
    // [전부 BigNumber를 반환하는 이유] 레벨엔 상한이 없고 성장률은 매 레벨 곱해지는 지수 함수라,
    // 결과값(비용/생산량)은 오래 플레이할수록 결국 long/double의 한계에도 도달한다. levelUpBaseCost/
    // baseGoldPerSecond 자체(디자이너가 손으로 넣는 Lv.1 기준값)는 작은 수라 double로 충분하지만,
    // "그 값에 성장률을 몇십~몇백 제곱한 결과"는 그렇지 않다 — BigNumber.PowDouble이 그 제곱 자체를
    // Math.Pow로 직접 계산하지 않고 로그로 처리해서, 결과가 아무리 커져도 Infinity가 되지 않는다.

    /// <summary> N레벨 레벨업 비용 계산 (1 -> 2 로 갈 때 N=1) </summary>
    public BigNumber GetLevelUpCost(int currentLevel)
    {
        BigNumber growthFactor = BigNumber.PowDouble(levelUpGrowthRate, currentLevel - 1);
        return levelUpBaseCost * growthFactor;
    }

    /// <summary> 특정 레벨에서의 초당 골드 생산량 </summary>
    public BigNumber GetGoldPerSecond(int level)
    {
        BigNumber growthFactor = BigNumber.PowDouble(goldPerSecondGrowthRate, level - 1);
        return baseGoldPerSecond * growthFactor;
    }

    /// <summary>
    /// fromLevel에서 시작해 levels번 연속 레벨업할 때 드는 총 비용 (미리보기 전용, 상태 변경 없음).
    /// +10/MAX 버튼에 표시할 "실제 총 비용" 계산에 사용.
    /// </summary>
    public BigNumber GetLevelUpCostForLevels(int fromLevel, int levels)
    {
        if (levels <= 0) return BigNumber.Zero;

        BigNumber total = BigNumber.Zero;
        for (int i = 0; i < levels; i++)
        {
            total += GetLevelUpCost(fromLevel + i);
        }
        return total;
    }

    /// <summary>
    /// 성장 단계에 대응하는 스프라이트를 반환한다 (FlowerDisplayController/FlowerDexItem/FlowerDexPanel이
    /// 각자 따로 switch문을 두지 않도록 단일 소스로 통일).
    /// </summary>
    public Sprite GetSpriteForStage(GrowthStage stage)
    {
        return stage switch
        {
            GrowthStage.Bloomed => bloomSprite,
            GrowthStage.Growing => growingSprite,
            GrowthStage.Sprout => sproutSprite,
            _ => seedSprite
        };
    }

    /// <summary> 지정한 유대 레벨에 해금되는 메모리얼(없으면 null). memorialEntries가 비어 있어도 안전. </summary>
    public MemorialData GetMemorialForBondLevel(int bondLevel)
    {
        if (memorialEntries == null) return null;
        return memorialEntries.Find(m => m != null && m.unlockBondLevel == bondLevel);
    }

    /// <summary>
    /// fromLevel에서 시작해 주어진 골드로 몇 레벨까지 오를 수 있는지 계산 (미리보기 전용, 상태 변경 없음).
    /// +10(상한 계산)/MAX 버튼 표시에 사용. 밸런스 데이터 오류(성장률 1 이하 등)로 인한
    /// 무한루프를 막기 위한 안전 상한을 둔다.
    /// </summary>
    public int GetMaxAffordableLevels(int fromLevel, BigNumber gold)
    {
        const int SAFETY_CAP = 100000;

        int levels = 0;
        BigNumber remaining = gold;

        while (levels < SAFETY_CAP)
        {
            BigNumber cost = GetLevelUpCost(fromLevel + levels);
            if (cost > remaining) break;

            remaining -= cost;
            levels++;
        }

        return levels;
    }
}