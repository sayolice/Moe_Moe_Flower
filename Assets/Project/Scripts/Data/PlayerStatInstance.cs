using System;

/// <summary>
/// 플레이어 스탯 하나(터치 애정/터치 골드/자동 애정 등)의 런타임 레벨 상태.
/// PlayerStatData(고정 데이터)와 분리 — FlowerInstance와 동일한 설계 원칙.
/// MonoBehaviour가 아닌 순수 C# 클래스.
/// </summary>
[Serializable]
public class PlayerStatInstance
{
    public int currentLevel;

    public PlayerStatInstance(int startingLevel)
    {
        currentLevel = startingLevel;
    }
}
