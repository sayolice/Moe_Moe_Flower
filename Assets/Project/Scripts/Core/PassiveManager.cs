using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 꽃 패시브 계산 전담 싱글턴. FlowerData.passives(정적 데이터)를 읽어서
///   (1) 정적 수정자 총량 조회 — GetTotalMultiplier / GetFlatBonusTotal
///   (2) 터치/레벨업 이벤트 발동 판정 — RollXxx
/// 를 제공한다. FlowerManager/GameManager/SaveManager는 이 클래스에 "지금 얼마인지"만
/// 물어보고, 패시브 리스트를 직접 순회하지 않는다(단일 책임 분리).
///
/// 공통 규칙: 모든 패시브는 소유 꽃이 개화(isBloomed) 상태일 때만 활성화된다.
/// </summary>
public class PassiveManager : MonoBehaviour
{
    public static PassiveManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary> 개화한 보유 꽃들의 패시브 중, 지정한 효과 타입만 순회한다. </summary>
    private IEnumerable<(FlowerInstance owner, PassiveData passive)> GetActivePassives(PassiveEffectType type)
    {
        if (FlowerManager.Instance == null) yield break;

        foreach (FlowerInstance instance in FlowerManager.Instance.GetAllOwnedInstances())
        {
            if (!instance.isBloomed) continue; // 패시브는 개화 후에만 활성화

            FlowerData data = FlowerManager.Instance.GetFlowerData(instance.flowerId);
            if (data == null || data.passives == null) continue;

            foreach (PassiveData p in data.passives)
            {
                if (p.effectType == type) yield return (instance, p);
            }
        }
    }

    // ===== 1. 정적 수정자 (튤립 씨앗가 / 장미 레벨업비용 / 해바라기 G/s) =====

    /// <summary> 지정 효과 타입의 현재 전역 배율. 예: -2%와 -3%가 동시에 있으면 0.95 반환. </summary>
    public float GetTotalMultiplier(PassiveEffectType type)
    {
        float totalDelta = 0f;
        foreach (var (_, passive) in GetActivePassives(type))
            totalDelta += passive.value;
        return 1f + totalDelta;
    }

    /// <summary> 지정 효과 타입의 현재 전역 고정치 합. 예: 연꽃(+0.25/s)이 2종이면 0.5 반환. </summary>
    public float GetFlatBonusTotal(PassiveEffectType type)
    {
        float total = 0f;
        foreach (var (_, passive) in GetActivePassives(type))
            total += passive.value;
        return total;
    }

    // ===== 2. 자기 자신 한정 터치형 (현재 쓰는 패시브 없음 — 향후 확장용으로 남겨둠) =====

    /// <summary>
    /// touchedInstance(=지금 터치한 꽃)가 소유자 자신일 때만 발동하는 Self 스코프 확률형 보너스.
    /// 지금 확정 패시브 중 이 스코프를 쓰는 것은 없다(나팔꽃은 AnyFlowerTouch로 변경됨) —
    /// 나중에 "자기 자신을 직접 터치해야만 발동" 하는 패시브가 생기면 그때 사용한다.
    /// </summary>
    public float RollSelfTouchBonus(FlowerInstance touchedInstance, PassiveEffectType type, float baseAmount)
    {
        if (touchedInstance == null || !touchedInstance.isBloomed) return 0f;

        FlowerData data = FlowerManager.Instance != null ? FlowerManager.Instance.GetFlowerData(touchedInstance.flowerId) : null;
        if (data == null || data.passives == null) return 0f;

        float bonus = 0f;
        foreach (PassiveData p in data.passives)
        {
            if (p.effectType != type || p.scope != PassiveScope.Self) continue;
            if (Random.value < p.chance) bonus += baseAmount * p.value;
        }
        return bonus;
    }

    /// <summary>
    /// 지금 터치 중인 대상이 무엇이든(자기 자신 포함, 개화/미개화 무관) 발동하는 확률형 보너스 합
    /// (나팔꽃: TouchGoldExtraChance). "지금 터치한 꽃이 나팔꽃이어야 한다"는 조건이 없으므로,
    /// 나팔꽃을 화면에 띄우지 않고 다른 꽃을 보고 있어도 계속 적용된다.
    /// </summary>
    public float RollAnyFlowerTouchBonus(PassiveEffectType type, float baseAmount)
    {
        float bonus = 0f;
        foreach (var (_, passive) in GetActivePassives(type))
        {
            if (passive.scope != PassiveScope.AnyFlowerTouch) continue;
            if (Random.value < passive.chance) bonus += baseAmount * passive.value;
        }
        return bonus;
    }

    // ===== 3. 미개화 대상 터치형 (팬지: TouchAffectionExtraChance) =====

    /// <summary> 지금 터치 중인 대상이 미개화 상태일 때, 보유한 AnyUnbloomedTouch 스코프 패시브를 판정한다. </summary>
    public float RollAnyUnbloomedTouchBonus(FlowerInstance touchedInstance, PassiveEffectType type, float baseAmount)
    {
        if (touchedInstance == null || touchedInstance.isBloomed) return 0f;

        float bonus = 0f;
        foreach (var (_, passive) in GetActivePassives(type))
        {
            if (passive.scope != PassiveScope.AnyUnbloomedTouch) continue;
            if (Random.value < passive.chance) bonus += baseAmount * passive.value;
        }
        return bonus;
    }

    // ===== 4. 지원형 터치 보너스 (수국: TouchSupportBonusFlat) =====

    /// <summary>
    /// 지금 터치 중인 꽃이 무엇이든(자기 자신 포함) 발동하는 고정 보너스 합.
    /// chance=1(100%)이면 매번 발동 — 확률형과 완전히 같은 코드 경로를 재사용한다
    /// (수국은 "터치되는 게 100%라고 생각"하면 되는 이유가 이것).
    /// </summary>
    public float RollSupportTouchBonus(FlowerInstance touchedInstance)
    {
        if (touchedInstance == null) return 0f;

        float bonus = 0f;
        foreach (var (_, passive) in GetActivePassives(PassiveEffectType.TouchSupportBonusFlat))
        {
            if (passive.scope != PassiveScope.AnyFlowerTouch) continue;
            if (Random.value < passive.chance) bonus += passive.value;
        }
        return bonus;
    }

    // ===== 5. 레벨업 이벤트형 (벚꽃: 레벨당 독립 판정 / 라벤더: 액션당 환급) =====

    /// <summary>
    /// 레벨업 1개 구매마다 독립 판정한다(벚꽃 10%). +1/+10/MAX 어느 버튼으로 사든
    /// "실제로 구매한 레벨 수"만큼만 판정 기회가 생기므로, 액션 횟수로 기대값을 부풀릴 수 없다.
    /// </summary>
    public bool RollBonusFreeLevel()
    {
        bool triggered = false;
        foreach (var (_, passive) in GetActivePassives(PassiveEffectType.LevelUpBonusLevelChance))
        {
            if (Random.value < passive.chance) triggered = true;
        }
        return triggered;
    }

    /// <summary>
    /// 레벨업 액션 1회(그 액션에서 실제로 지불한 골드 전체) 기준 환급 판정(라벤더 5%×100%).
    /// 환급액이 액션 규모에 비례하므로 +1로 나눠 사든 MAX로 한 번에 사든 기대값은 항상 동일하다.
    /// </summary>
    public long RollLevelUpRefund(long goldSpentThisAction)
    {
        if (goldSpentThisAction <= 0) return 0;

        long refund = 0;
        foreach (var (_, passive) in GetActivePassives(PassiveEffectType.LevelUpFullRefundChance))
        {
            if (Random.value < passive.chance)
                refund += (long)(goldSpentThisAction * passive.value);
        }
        return refund;
    }
}
