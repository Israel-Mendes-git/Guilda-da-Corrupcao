/// <summary>
/// Identidade estável de bioma. Antes o pareamento era feito por string entre
/// QuestData.biome ("🌲 Floresta", com emoji) e EventData.biomeTag ("Forest"),
/// o que nunca casava e jogava quase todo evento no fallback genérico.
/// </summary>
public enum BiomeType
{
    Any = 0,
    Forest = 1,
    Mountain = 2,
    Swamp = 3,
    Desert = 4,
    Tundra = 5,
    Volcano = 6,
    Ruins = 7
}

public static class BiomeUtil
{
    /// <summary>Biomas que podem ser sorteados para uma missão (exclui Any, que é curinga).</summary>
    public static readonly BiomeType[] Playable =
    {
        BiomeType.Forest, BiomeType.Mountain, BiomeType.Swamp, BiomeType.Desert,
        BiomeType.Tundra, BiomeType.Volcano, BiomeType.Ruins
    };

    public static string GetDisplayName(BiomeType biome)
    {
        switch (biome)
        {
            case BiomeType.Forest: return "🌲 Floresta";
            case BiomeType.Mountain: return "⛰️ Montanha";
            case BiomeType.Swamp: return "🏚️ Pântano";
            case BiomeType.Desert: return "🏜️ Deserto";
            case BiomeType.Tundra: return "❄️ Tundra";
            case BiomeType.Volcano: return "🌋 Vulcão";
            case BiomeType.Ruins: return "🏯 Ruínas";
            default: return "🧭 Terras Ermas";
        }
    }

    /// <summary>Um evento serve à missão se for genérico (Any) ou do mesmo bioma.</summary>
    public static bool Matches(BiomeType eventBiome, BiomeType questBiome)
    {
        return eventBiome == BiomeType.Any || eventBiome == questBiome;
    }

    public static BiomeType GetRandom()
    {
        return Playable[UnityEngine.Random.Range(0, Playable.Length)];
    }
}
