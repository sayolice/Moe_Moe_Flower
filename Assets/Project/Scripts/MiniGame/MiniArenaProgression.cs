using System;
using System.Collections.Generic;
using UnityEngine;

public enum MiniArenaDifficulty
{
    Relaxed,
    Standard,
    Expert,
    Endless
}

public enum MiniArenaWeaponStyle
{
    Balanced,
    Rapid,
    Heavy
}

public enum MiniArenaUpgradeKind
{
    Universal,
    Seed,
    Wind,
    Bloom,
    Fusion
}

[Serializable]
public sealed class MiniArenaSaveData
{
    public int version = 1;
    public int credits;
    public int damageLevel;
    public int fireRateLevel;
    public int maxHealthLevel;
    public int experienceLevel;
    public int pickupLevel;
    public int bestSurvivalSeconds;
    public int bestKills;
    public int wins;
}

public static class MiniArenaUpgradeCatalog
{
    public const int MaxTrees = 2;
    private static readonly float FusionOfferMin = 0.05f;
    private static readonly float FusionOfferMax = 0.70f;
    public static readonly MiniArenaUpgradeDefinition[] All =
    {
        new MiniArenaUpgradeDefinition("power", "범용 화력 증폭", "모든 공격 피해량 +10%", MiniArenaUpgradeKind.Universal, 5, 0.10f, null, true, 0),
        new MiniArenaUpgradeDefinition("cadence", "범용 사격 가속", "자동 공격 속도 +8%", MiniArenaUpgradeKind.Universal, 5, 0.08f, null, true, 0),
        new MiniArenaUpgradeDefinition("vitality", "범용 장갑 보강", "최대 체력 +1", MiniArenaUpgradeKind.Universal, 5, 1f, null, true, 0),
        new MiniArenaUpgradeDefinition("insight", "범용 경험 훈련", "경험치 획득량 +15%", MiniArenaUpgradeKind.Universal, 5, 0.15f, null, true, 0),
        new MiniArenaUpgradeDefinition("seed", "민들레 씨앗", "홀씨 파동 발동 조건 감소", MiniArenaUpgradeKind.Seed, 5, 1f, null, false, 1),
        new MiniArenaUpgradeDefinition("seedBurst", "홀씨 폭발", "홀씨 파동 피해와 범위 증가", MiniArenaUpgradeKind.Seed, 5, 0.25f, "seed", false, 2),
        new MiniArenaUpgradeDefinition("seedGermination", "연쇄 발아", "파동 피해 추가 강화", MiniArenaUpgradeKind.Seed, 5, 0.2f, "seedBurst", false, 3),
        new MiniArenaUpgradeDefinition("seedRain", "홀씨 비", "파동마다 추가 탄환 발사", MiniArenaUpgradeKind.Seed, 3, 1f, "seedGermination", false, 4),
        new MiniArenaUpgradeDefinition("wind", "순풍", "이동 속도와 밀쳐내기 강화", MiniArenaUpgradeKind.Wind, 5, 0.08f, null, false, 1),
        new MiniArenaUpgradeDefinition("tailwind", "바람의 길", "이동 중 바람 칼날 자동 발사", MiniArenaUpgradeKind.Wind, 5, 0.15f, "wind", false, 2),
        new MiniArenaUpgradeDefinition("crosswind", "측풍", "바람 칼날 피해 강화", MiniArenaUpgradeKind.Wind, 5, 0.2f, "tailwind", false, 3),
        new MiniArenaUpgradeDefinition("gale", "질풍", "이동과 발사 빈도 강화", MiniArenaUpgradeKind.Wind, 3, 0.25f, "crosswind", false, 4),
        new MiniArenaUpgradeDefinition("bloom", "만개", "주변을 공격하는 꽃잎 생성", MiniArenaUpgradeKind.Bloom, 5, 0.35f, null, false, 1),
        new MiniArenaUpgradeDefinition("petalShield", "꽃잎 방벽", "꽃잎 범위와 피해 강화", MiniArenaUpgradeKind.Bloom, 5, 0.2f, "bloom", false, 2),
        new MiniArenaUpgradeDefinition("pollen", "꽃가루 폭발", "꽃잎 처치 시 추가 폭발", MiniArenaUpgradeKind.Bloom, 5, 0.2f, "petalShield", false, 3),
        new MiniArenaUpgradeDefinition("everbloom", "영원한 개화", "꽃잎 주기와 폭발 범위 강화", MiniArenaUpgradeKind.Bloom, 3, 0.25f, "pollen", false, 4),
        new MiniArenaUpgradeDefinition("fusionSeedWind", "씨앗바람: 파종 폭풍", "홀씨 파동과 바람 칼날 동시 발사", MiniArenaUpgradeKind.Fusion, 3, 0.35f, null, false, 5, new[] { "seed", "wind" }, new[] { "seed", "wind" }),
        new MiniArenaUpgradeDefinition("fusionSeedBloom", "씨앗개화: 생명의 순환", "파동에 꽃잎 연계", MiniArenaUpgradeKind.Fusion, 3, 0.35f, null, false, 5, new[] { "seed", "bloom" }, new[] { "seed", "bloom" }),
        new MiniArenaUpgradeDefinition("fusionWindBloom", "바람개화: 꽃바람", "꽃잎 공격에 바람 범위 피해 추가", MiniArenaUpgradeKind.Fusion, 3, 0.35f, null, false, 5, new[] { "wind", "bloom" }, new[] { "wind", "bloom" })
    };

    public static MiniArenaUpgradeDefinition Find(string id) => Array.Find(All, item => item.id == id);
    public static int GetLevel(Dictionary<string, int> levels, string id) => levels.TryGetValue(id, out int level) ? level : 0;

    public static List<MiniArenaUpgradeDefinition> GetAvailable(Dictionary<string, int> levels, string flowerId)
    {
        List<MiniArenaUpgradeDefinition> result = new List<MiniArenaUpgradeDefinition>();
        HashSet<MiniArenaUpgradeKind> ownedTrees = GetOwnedTrees(levels);
        MiniArenaFlowerProfile profile = MiniArenaFlowerProfiles.Find(flowerId);
        foreach (MiniArenaUpgradeDefinition definition in All)
        {
            if (GetLevel(levels, definition.id) >= definition.maxLevel || !AvailableForFlower(profile, definition)) continue;
            if (!string.IsNullOrEmpty(definition.prerequisite))
            {
                MiniArenaUpgradeDefinition prerequisite = Find(definition.prerequisite);
                if (prerequisite == null || GetLevel(levels, prerequisite.id) < prerequisite.maxLevel) continue;
            }
            if (definition.prerequisites != null && !PrerequisitesMaxed(levels, definition.prerequisites)) continue;
            if (definition.tier == 1 && definition.kind != MiniArenaUpgradeKind.Universal
                && !ownedTrees.Contains(definition.kind) && ownedTrees.Count >= MaxTrees) continue;
            result.Add(definition);
        }
        return result;
    }

    public static HashSet<MiniArenaUpgradeKind> GetOwnedTrees(Dictionary<string, int> levels)
    {
        HashSet<MiniArenaUpgradeKind> trees = new HashSet<MiniArenaUpgradeKind>();
        foreach (MiniArenaUpgradeDefinition definition in All)
            if (definition.tier == 1 && GetLevel(levels, definition.id) > 0) trees.Add(definition.kind);
        return trees;
    }

    public static float GetFusionOfferChance(MiniArenaUpgradeDefinition fusion, Dictionary<string, int> levels)
    {
        float current = 0f;
        float maximum = 0f;
        foreach (MiniArenaUpgradeDefinition definition in All)
        {
            if (definition.tier != 2) continue;
            if (definition.kind != MiniArenaUpgradeKind.Seed && definition.kind != MiniArenaUpgradeKind.Wind
                && definition.kind != MiniArenaUpgradeKind.Bloom) continue;
            if (!SharesTree(definition, fusion)) continue;
            current += GetLevel(levels, definition.id);
            maximum += definition.maxLevel;
        }
        float progress = maximum > 0f ? Mathf.Clamp01(current / maximum) : 1f;
        return FusionOfferMin + (FusionOfferMax - FusionOfferMin) * progress * progress;
    }

    private static bool AvailableForFlower(MiniArenaFlowerProfile profile, MiniArenaUpgradeDefinition definition)
    {
        if (definition.kind == MiniArenaUpgradeKind.Universal) return true;
        if (profile == null || profile.treeIds == null) return false;
        if (definition.kind == MiniArenaUpgradeKind.Fusion)
        {
            foreach (string tree in definition.treeIds)
                if (Array.IndexOf(profile.treeIds, tree) < 0) return false;
            return true;
        }
        string treeId = TreeId(definition.kind);
        return treeId != null && Array.IndexOf(profile.treeIds, treeId) >= 0;
    }

    private static bool PrerequisitesMaxed(Dictionary<string, int> levels, string[] prerequisites)
    {
        foreach (string id in prerequisites)
        {
            MiniArenaUpgradeDefinition prerequisite = Find(id);
            if (prerequisite == null || GetLevel(levels, id) < prerequisite.maxLevel) return false;
        }
        return true;
    }

    private static bool SharesTree(MiniArenaUpgradeDefinition definition, MiniArenaUpgradeDefinition fusion)
    {
        if (fusion == null || fusion.treeIds == null) return false;
        string treeId = TreeId(definition.kind);
        return treeId != null && Array.IndexOf(fusion.treeIds, treeId) >= 0;
    }

    private static string TreeId(MiniArenaUpgradeKind kind)
    {
        switch (kind)
        {
            case MiniArenaUpgradeKind.Seed: return "seed";
            case MiniArenaUpgradeKind.Wind: return "wind";
            case MiniArenaUpgradeKind.Bloom: return "bloom";
            default: return null;
        }
    }
}

public static class MiniArenaProgression
{
    private const string SaveKey = "mini_arena_progress_v1";
    private static MiniArenaSaveData data;

    public static MiniArenaSaveData Data
    {
        get
        {
            if (data == null) Load();
            return data;
        }
    }

    public static readonly string[] UpgradeIds = { "damage", "fireRate", "health", "experience", "pickup" };
    public static readonly string[] UpgradeNames = { "공격 훈련", "연사 훈련", "생명력 훈련", "경험 훈련", "수집 범위 훈련" };
    public static readonly int[] UpgradeBaseCosts = { 25, 30, 35, 30, 25 };
    public static readonly float[] UpgradeCostScales = { 1.55f, 1.55f, 1.6f, 1.6f, 1.55f };
    public static readonly int[] UpgradeMaxLevels = { 10, 10, 8, 8, 8 };

    public static int GetUpgradeLevel(int index)
    {
        switch (index)
        {
            case 0: return Data.damageLevel;
            case 1: return Data.fireRateLevel;
            case 2: return Data.maxHealthLevel;
            case 3: return Data.experienceLevel;
            case 4: return Data.pickupLevel;
            default: return 0;
        }
    }

    public static int GetUpgradeCost(int index)
    {
        int level = GetUpgradeLevel(index);
        return Mathf.RoundToInt(UpgradeBaseCosts[index] * Mathf.Pow(UpgradeCostScales[index], level));
    }

    public static bool BuyUpgrade(int index)
    {
        if (index < 0 || index >= UpgradeIds.Length || GetUpgradeLevel(index) >= UpgradeMaxLevels[index]) return false;
        int cost = GetUpgradeCost(index);
        if (Data.credits < cost) return false;
        Data.credits -= cost;
        switch (index)
        {
            case 0: Data.damageLevel++; break;
            case 1: Data.fireRateLevel++; break;
            case 2: Data.maxHealthLevel++; break;
            case 3: Data.experienceLevel++; break;
            case 4: Data.pickupLevel++; break;
        }
        Save();
        return true;
    }

    public static void CompleteRun(int seconds, int kills, bool victory)
    {
        int reward = Mathf.Max(1, seconds / 8 + kills / 3 + (victory ? 50 : 0));
        Data.credits += reward;
        Data.bestSurvivalSeconds = Mathf.Max(Data.bestSurvivalSeconds, seconds);
        Data.bestKills = Mathf.Max(Data.bestKills, kills);
        if (victory) Data.wins++;
        Save();
    }

    public static void Save()
    {
        if (data == null) return;
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    private static void Load()
    {
        if (!PlayerPrefs.HasKey(SaveKey))
        {
            data = new MiniArenaSaveData();
            return;
        }

        try
        {
            data = JsonUtility.FromJson<MiniArenaSaveData>(PlayerPrefs.GetString(SaveKey));
            if (data == null || data.version != 1) data = new MiniArenaSaveData();
        }
        catch
        {
            data = new MiniArenaSaveData();
        }
    }
}

public sealed class MiniArenaUpgradeDefinition
{
    public string id;
    public string name;
    public string description;
    public MiniArenaUpgradeKind kind;
    public int maxLevel;
    public float valuePerLevel;
    public string prerequisite;
    public bool universal;
    public int tier;
    public string[] treeIds;
    public string[] prerequisites;

    public MiniArenaUpgradeDefinition(string id, string name, string description, MiniArenaUpgradeKind kind,
        int maxLevel, float valuePerLevel, string prerequisite = null, bool universal = false,
        int tier = 1, string[] treeIds = null, string[] prerequisites = null)
    {
        this.id = id;
        this.name = name;
        this.description = description;
        this.kind = kind;
        this.maxLevel = maxLevel;
        this.valuePerLevel = valuePerLevel;
        this.prerequisite = prerequisite;
        this.universal = universal;
        this.tier = tier;
        this.treeIds = treeIds;
        this.prerequisites = prerequisites;
    }
}
