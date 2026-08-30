using UnityEngine;

/// <summary>
/// "키 -> 스프라이트" 등록 한 줄. MemorialPlayerPanel이 캐릭터 초상화/배경/CG 세 종류를 각각
/// 이 타입의 리스트로 인스펙터에 등록해 두고, 태그([ENTER_C:키] [BG:키] [CG:키])가 그 키로 조회한다
/// (참고 코드의 globalCharacterSprites / GetCharacterSprite 패턴과 동일 — 캐릭터/배경/CG는 의미가
/// 달라 리스트 자체는 3개로 분리해 키 충돌을 피한다).
/// </summary>
[System.Serializable]
public class MemorialSpriteEntry
{
    public string key;
    public Sprite sprite;
}
