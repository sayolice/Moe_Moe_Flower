/// <summary>
/// 유대 레벨 달성 시 해금되는 회상/에피소드 1편. FlowerData.memorialEntries에 꽃마다 리스트로 들어간다.
/// 지금은 꽃당 Lv.1~3 세 편을 기준으로 하고(Lv.4·5는 메모리얼 없이 G/s 배율만 준다), 나중에 편수를
/// 늘릴 수 있도록 리스트+unlockBondLevel 구조로 둔다.
///
/// 텍스트는 아직 사람이 작성하지 않았다 — title/body가 빈 문자열이어도 레벨업/배율 계산 등 시스템
/// 동작에는 전혀 영향이 없어야 한다(이 클래스를 참조하는 쪽은 항상 null 가능성과 빈 문자열 둘 다
/// 대비해야 한다).
/// </summary>
[System.Serializable]
public class MemorialData
{
    public int unlockBondLevel; // 1, 2, 3 (지금 기준. Lv.4·5는 이 값을 가진 항목을 두지 않으면 됨)
    public string title;
    [UnityEngine.TextArea(5, 20)]
    public string body;
}
