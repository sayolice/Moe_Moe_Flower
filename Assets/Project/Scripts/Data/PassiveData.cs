using System;

/// <summary>
/// 꽃 패시브 1개의 고정 데이터. FlowerData.passives 리스트에 0개 이상 담긴다.
/// 단일 클래스로 모든 패시브 종류를 표현한다(효과 타입별 서브클래스로 쪼개지 않음) —
/// 이 프로젝트가 PlayerStatData 등에서 이미 써온 "하나의 클래스 + 안 쓰는 필드는 기본값" 패턴과
/// 통일하기 위함이며, Unity 기본 Inspector로 바로 편집 가능하다(SerializeReference 불필요).
///
/// 필드 의미는 effectType마다 다르다 (아래 표 참고). 모든 패시브가 모든 필드를 쓰지는 않는다.
///
/// effectType                 | value 의미                | chance 의미           | scope 의미
/// SeedPriceDiscountPercent   | 할인율(-0.02=-2%)          | 미사용(1 고정)         | 미사용(항상 전역)
/// LevelUpCostDiscountPercent | 할인율(-0.03=-3%)          | 미사용(1 고정)         | 미사용(항상 전역)
/// GoldPerSecondBonusPercent  | 보너스율(+0.02=+2%)        | 미사용(1 고정)         | 미사용(항상 전역, 온라인·오프라인 구분 없음)
/// AutoAffectionBonusFlat     | 고정 보너스(초당, +0.25 등) | 미사용(1 고정)         | 미사용(모든 미개화 꽃에 적용)
/// TouchSupportBonusFlat      | 고정 보너스량(+1)           | 발동확률(1=100%)       | AnyFlowerTouch (자기 자신 포함)
/// LevelUpBonusLevelChance    | 미사용                     | 레벨 1개당 발동확률(0.1) | 미사용
/// LevelUpFullRefundChance    | 환급 비율(1.0=100%)        | 액션 1회당 발동확률(0.05)| 미사용
/// TouchGoldExtraChance       | 추가 지급 배수(1.0=100%)    | 발동확률(0.06)         | AnyFlowerTouch (자기 자신 포함)
/// TouchAffectionExtraChance  | 추가 지급 배수(1.0=100%)    | 발동확률(0.06)         | AnyUnbloomedTouch
/// </summary>
[Serializable]
public class PassiveData
{
    public string passiveId;
    public string displayName;
    [UnityEngine.TextArea] public string description;

    public PassiveEffectType effectType;
    public PassiveScope scope = PassiveScope.None;

    public float value;
    [UnityEngine.Range(0f, 1f)] public float chance = 1f;
}

public enum PassiveEffectType
{
    None = 0,
    SeedPriceDiscountPercent = 1,
    LevelUpCostDiscountPercent = 2,
    GoldPerSecondBonusPercent = 3, // 예전 이름: OfflineIncomeBonusPercent. "오프라인 수익 %" 자체를 폐기하고
                                    // 온라인·오프라인 구분 없는 전역 G/s 보너스로 대체(해바라기) — 오프라인이
                                    // 온라인의 100%로 확정된 이상, 오프라인 전용 보너스는 앱을 꺼두는 게 이득이
                                    // 되어버리는 모순이 생기므로 이 슬롯 자체를 재정의했다.
    TouchSupportBonusFlat = 4,
    LevelUpBonusLevelChance = 5,
    LevelUpFullRefundChance = 6,
    TouchGoldExtraChance = 7,
    TouchAffectionExtraChance = 8,
    AutoAffectionBonusFlat = 9,
}

public enum PassiveScope
{
    None = 0,
    Self = 1,               // 소유 꽃 자신을 직접 터치할 때만 발동 (현재 쓰는 패시브 없음, 향후 확장용)
    AnyFlowerTouch = 2,      // 지금 터치 중인 꽃이 무엇이든(자기 자신 포함) 적용 (수국, 나팔꽃)
    AnyUnbloomedTouch = 3,   // 지금 터치 중인 대상이 미개화 상태이기만 하면 적용, 소유자 자신 포함 무관 (팬지)
}
