using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 유대(Bond) 시스템의 전역 밸런스 데이터. 꽃마다 만들지 않고 프로젝트에 단 1개만 존재한다 —
/// 임계치/배율은 모든 꽃이 동일하게 공유하므로, 곡선을 조정할 때 꽃 에셋을 하나씩 열 필요가 없다.
///
/// [절대 인플레이션하지 않는다 — 설계 원칙 1.1/1.2]
/// bondPerClick / bondPerSecondInGarden은 플레이어 스탯 레벨, 패시브, 그 어떤 배율과도 무관하게
/// 항상 이 고정값 그대로다. FlowerManager.AddBond를 포함해 유대를 더하는 어떤 코드도 이 값에
/// 무언가를 곱해서는 안 된다. 획득 "속도"의 상한이 사람 손가락(초당 약 5회)이라는 물리적 한계이기
/// 때문에, 이 값이 절대 오르지 않아야만 유대 임계치를 꽃 30종이 전부 공유할 수 있고, 꽃이 늘어나도
/// 유대 축에서는 밸런싱할 것이 없어진다. 이 성질이 깨지면(획득 속도가 오르면) 유대도 결국
/// 인플레이션하는 재화가 되어, 이 프로젝트에서 과거 3회 반복된 "인플레이션 재화 × 고정 콘텐츠
/// 게이트" 버그 패턴을 그대로 재현하게 된다.
///
/// 절대 만들지 말아야 할 것 (요청 명세 1.2):
///   - 유대 획득 속도를 올리는 업그레이드
///   - 유대 획득량이 TouchAffection 등 플레이어 스탯 레벨에 비례하는 것
///   - 정원 레벨업으로 유대 획득 속도를 올리는 것
///   - 자동 애정이 유대를 쌓는 것
///   - 유대 획득에 배율을 곱하는 모든 형태
/// 유대를 늘리는 방향은 오직 "폭"(동시에 몇 명에게 쌓이는가, 향후 정원 슬롯 확장)뿐이다.
/// </summary>
[CreateAssetMenu(menuName = "Flower/BondData")]
public class BondData : ScriptableObject
{
    [Header("Lv.1~5 도달에 필요한 유대량 — 누적 총량이 아니라 '그 레벨 구간' 안에서의 요구량이다.")]
    [Tooltip("예: Lv.3 도달에 필요한 총 유대는 500+2000+8000=10,500 (thresholds[2]가 아니라 앞 원소들의 합).")]
    public List<double> thresholds = new List<double> { 500, 2000, 8000, 30000, 100000 };

    [Header("각 레벨에서 적용되는 G/s 곱연산 배율 (그 배율은 해당 꽃에만 적용된다)")]
    public List<float> goldMultipliers = new List<float> { 1.1f, 1.25f, 1.5f, 2.0f, 3.0f };

    [Header("획득량 — 절대 배율을 곱하지 말 것. 항상 이 고정값 그대로 적용되어야 한다.")]
    public int bondPerClick = 1;
    [Tooltip("정원 시스템(이번 작업 범위 밖)이 배치된 꽃에 매초 AddBond(flower, bondPerSecondInGarden)을 " +
             "호출하는 형태로 쓸 값. 지금은 아무도 참조하지 않는다.")]
    public double bondPerSecondInGarden = 1.0;

    [Tooltip("이 레벨에서 유대 성장이 멈춘다(설계 원칙 1.3) — 무한 성장 금지, 수집 게임에서 '전부를 " +
             "Lv.5로'가 유일한 최적 전략이 되도록 하는 핵심 장치.")]
    public int maxBondLevel = 5;
}
