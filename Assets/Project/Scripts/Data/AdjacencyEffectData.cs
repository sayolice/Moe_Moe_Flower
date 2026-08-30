using UnityEngine;

/// <summary>
/// 인접 효과 종류. 하드코딩(flowerId별 분기)을 피하기 위해 효과의 "형태"를 데이터로 정의한다 —
/// 어떤 꽃이 어떤 타입인지는 FlowerData.adjacencyEffect(인스펙터)에서만 정해진다.
/// </summary>
public enum AdjacencyEffectType
{
    None,
    BoostAllNeighborsFlat,      // 민들레/팬지/벚꽃: 인접한 꽃소녀 전원 G/s +primaryValue
    SelfPlusPerNeighbor,        // 튤립/나팔꽃: 자신 +primaryValue, 인접 1칸(고유 이웃 1명)당 +secondaryValue 추가
    BoostHighestBondNeighbor,   // 장미: 인접 중 유대 레벨 최고 1명의 G/s ×primaryValue
    BoostLowestBondNeighbor,    // 라벤더: 인접 중 유대 레벨 최저 1명의 G/s ×primaryValue
    SelfBoostPerEmptyNeighbor,  // 해바라기: 인접한 빈 칸 1개당 자신 G/s +primaryValue
    AmplifyNeighborsAdjacency   // 수국/연꽃: 인접한 꽃소녀들의 "인접 효과 보너스"를 (1+primaryValue)배 증폭
}

/// <summary>
/// 인접 효과 하나의 수치 정의. primaryValue/secondaryValue의 의미는 type에 따라 다르다(주석 참고).
/// 전부 % 기준의 "보너스 비율"(예: 0.08 = +8%)로 넣는다 — 배수형(장미/라벤더의 ×1.5)도
/// GardenManager 내부에서 (배수 - 1)로 환산해 같은 "보너스 비율" 체계로 합산한다.
/// </summary>
[System.Serializable]
public class AdjacencyEffectData
{
    public AdjacencyEffectType type = AdjacencyEffectType.None;

    [Tooltip("타입별 의미: BoostAllNeighborsFlat/SelfBoostPerEmptyNeighbor=보너스 비율, " +
             "SelfPlusPerNeighbor=자신 기본 보너스 비율, BoostHighest/LowestBondNeighbor=배수(예: 1.5), " +
             "AmplifyNeighborsAdjacency=증폭 비율(예: 0.2=20% 증폭)")]
    public float primaryValue;

    [Tooltip("SelfPlusPerNeighbor 전용 — 고유 인접 꽃 1명당 추가되는 보너스 비율. 다른 타입에서는 사용하지 않음.")]
    public float secondaryValue;

    /// <summary>
    /// type/primaryValue/secondaryValue로부터 사람이 읽는 설명 문구를 생성한다(작업 지시: "정원 인접
    /// 효과 시각화 UI" 작업 5). 도감 상세 화면과 정원 정보 패널이 이 메서드 하나만 공유하도록 해서,
    /// 두 화면의 설명이 서로 어긋날 여지를 없앤다 — SO에 별도 텍스트 필드를 두지 않는 이유이기도 하다
    /// (밸런스 수치를 고치면 설명도 자동으로 맞게 바뀐다).
    /// </summary>
    public string GetDescription()
    {
        switch (type)
        {
            case AdjacencyEffectType.BoostAllNeighborsFlat:
                return $"인접한 꽃 전원의 G/s +{primaryValue * 100f:0.#}%";
            case AdjacencyEffectType.SelfPlusPerNeighbor:
                return $"자신 G/s +{primaryValue * 100f:0.#}%, 고유 인접 꽃 1개체당 추가로 +{secondaryValue * 100f:0.#}%";
            case AdjacencyEffectType.BoostHighestBondNeighbor:
                return $"인접한 꽃 중 유대 레벨이 가장 높은 1명의 G/s ×{primaryValue:0.##}";
            case AdjacencyEffectType.BoostLowestBondNeighbor:
                return $"인접한 꽃 중 유대 레벨이 가장 낮은 1명의 G/s ×{primaryValue:0.##}";
            case AdjacencyEffectType.SelfBoostPerEmptyNeighbor:
                return $"인접한 빈 칸 1개당 자신 G/s +{primaryValue * 100f:0.#}%";
            case AdjacencyEffectType.AmplifyNeighborsAdjacency:
                return $"인접한 꽃들이 받고 있는 인접 효과를 {primaryValue * 100f:0.#}% 증폭 (증폭끼리는 서로 증폭하지 않음)";
            default:
                return "인접 효과 없음";
        }
    }
}
