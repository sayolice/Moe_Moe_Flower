using System.Collections.Generic;

public sealed class MiniArenaFlowerProfile
{
    public string flowerId;
    public string displayName;
    public string passiveName;
    public string passiveDescription;
    public string[] treeIds;

    public MiniArenaFlowerProfile(string flowerId, string displayName, string passiveName, string passiveDescription, params string[] treeIds)
    {
        this.flowerId = flowerId;
        this.displayName = displayName;
        this.passiveName = passiveName;
        this.passiveDescription = passiveDescription;
        this.treeIds = treeIds;
    }
}

public static class MiniArenaFlowerProfiles
{
    private static readonly Dictionary<string, MiniArenaFlowerProfile> Profiles = new Dictionary<string, MiniArenaFlowerProfile>
    {
        {
            "Dandelion",
            new MiniArenaFlowerProfile(
                "Dandelion",
                "민들레",
                "홀씨의 축복",
                "적을 처치할 때마다 홀씨를 남깁니다. 홀씨를 5개 모으면 바람의 파동이 자동 발동해 주변 적을 밀쳐내고 피해를 줍니다.",
                "seed", "wind", "bloom", "seed_wind", "seed_bloom", "wind_bloom")
        }
    };

    public static MiniArenaFlowerProfile Find(string flowerId)
    {
        return flowerId != null && Profiles.TryGetValue(flowerId, out MiniArenaFlowerProfile profile) ? profile : null;
    }
}
