using System.Collections.Generic;

/// <summary>
/// 정원 인접 효과 시각화 UI(작업 지시)를 위한 표시 전용 데이터. GardenManager.RecomputeAdjacencyCache가
/// 배치가 바뀔 때만 채우고, UI는 이 값을 읽기만 한다 — "표시용 계산과 실제 계산이 분리되면 안 된다"는
/// 지시서 요구를 지키기 위해, 실제 G/s에 쓰이는 adjacencyBonusFractionCache와 정확히 같은 계산 루프
/// 안에서 함께 만들어진다.
/// </summary>

/// <summary> "받고 있는 효과" 1건 — 남이 나에게 준 보너스 하나. </summary>
public class AdjacencyContribution
{
    public string sourceFlowerId;
    /// <summary> 표시용 문구, 예: "민들레 인접 보너스" / "수국 증폭". </summary>
    public string label;
    /// <summary>
    /// 유대 위력(FlowerManager.GetAdjacencyPower) 적용 "후"의 최종 값 — 실제로 baseFraction 계산에
    /// 쓰인 바로 그 값이다. isAmplification이 false면 더해진 양(예: 0.104 = +10.4%p), true면 곱해진
    /// 배율 자체(예: 1.13 = ×1.13). 두 종류를 같은 필드에 섞어 담는 대신 의미를 분리해 둬야 UI가
    /// "+10.4%"와 "×1.13"을 헷갈리지 않는다.
    /// </summary>
    public float amount;
    /// <summary> 유대 위력 적용 "전" 원본 값 — 소스 꽃이 유대 Lv.5(풀파워)였다면 나왔을 값과 같다.
    /// amount = rawAmount * power(가산형) 또는 1+(rawAmount-1)*power(증폭형)의 관계. </summary>
    public float rawAmount;
    /// <summary> 이 기여를 만든 소스 꽃의 유대 레벨(표시용). </summary>
    public int sourceBondLevel;
    /// <summary> FlowerManager.GetAdjacencyPower(sourceBondLevel) — 표시용으로 그대로 들고 있는다. </summary>
    public float power;
    public bool isAmplification;
}

/// <summary> "주는 효과" — 이 꽃 자신이 남에게 발동 중인 효과 정보(내 인접 효과 vs 받고 있는 효과 구분용). </summary>
public class OutgoingEffectInfo
{
    /// <summary> AdjacencyEffectData.GetDescription() 결과 — 타입/값에 관계없이 항상 채워짐. </summary>
    public string description = "";
    /// <summary> 유대 위력(FlowerManager.GetAdjacencyPower)이 0이면 false — 보통 유대 Lv.0일 때만
    /// 해당하며, 그 외(Lv.1 이상)에는 위력이 낮더라도 어느 정도는 발동 중이므로 true다. </summary>
    public bool isActive;
    /// <summary> 지금 이 순간 실제로 이 효과를 받고 있는 대상 꽃 id 목록(없으면 빈 리스트). </summary>
    public List<string> targetFlowerIds = new List<string>();
}
