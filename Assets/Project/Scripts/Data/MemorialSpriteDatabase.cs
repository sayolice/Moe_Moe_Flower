using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 메모리얼 재생기(MemorialPlayerPanel)에서 태그([ENTER_C:키], [BG:키], [CG:키])로 조회하는
/// 스프라이트 데이터베이스 ScriptableObject.
/// 
/// 씬(Scene)의 GameObject 인스펙터에 직접 등록하는 방식은 씬 재생성/빌드 시 유실될 위험이 있으므로,
/// 독립적인 .asset 파일로 프로젝트에 영구 저장하고 MemorialPlayerPanel이 참조하도록 한다.
/// </summary>
[CreateAssetMenu(fileName = "MemorialSpriteDatabase", menuName = "🌸 Moe Moe Flower/Memorial Sprite Database")]
public class MemorialSpriteDatabase : ScriptableObject
{
    [Header("캐릭터 초상화 ([ENTER_C:키] 로 조회)")]
    public List<MemorialSpriteEntry> characterSprites = new List<MemorialSpriteEntry>();

    [Header("배경 ([BG:키] 로 조회)")]
    public List<MemorialSpriteEntry> backgroundSprites = new List<MemorialSpriteEntry>();

    [Header("전체화면 CG ([CG:키] 로 조회)")]
    public List<MemorialSpriteEntry> cgSprites = new List<MemorialSpriteEntry>();
}
