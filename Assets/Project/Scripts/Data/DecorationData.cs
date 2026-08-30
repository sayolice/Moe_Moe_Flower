using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 정원 꾸미기 아이템 1종(화분/울타리/바닥/배경/소품). 요청 명세 작업 4의 핵심 원칙: 성능(G/s·유대·
/// 인접 효과·시듦)에 절대 관여하지 않는 순수 취향 아이템이다. 그래서 이 클래스에는 밸런스 수치가
/// goldCost 하나뿐이고, 그 값도 게임 밸런스와 무관하므로(성능 0짜리 소비처) 임의로 매겨도 안전하다.
/// 지금은 데이터 구조와 배치 기능만 있으면 되고, 실제 에셋(sprite)은 나중에 채워도 된다 —
/// sprite가 비어 있어도 GardenManager의 구매/배치 로직은 정상 동작해야 한다.
/// </summary>
[CreateAssetMenu(menuName = "Flower/DecorationData")]
public class DecorationData : ScriptableObject
{
    public string decorationId;
    public string displayName;
    public Sprite sprite;
    public long goldCost;

    [Tooltip("FlowerData.gardenShape과 동일한 방식 — (0,0) 기준 상대 좌표. 기본은 1×1.")]
    public List<Vector2Int> gardenShape = new List<Vector2Int> { Vector2Int.zero };

    public IEnumerable<Vector2Int> GetOccupiedCells(Vector2Int origin)
    {
        if (gardenShape == null || gardenShape.Count == 0)
        {
            yield return origin;
            yield break;
        }

        foreach (Vector2Int offset in gardenShape)
            yield return origin + offset;
    }
}
