using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Quest", menuName = "Game/Quest")]
public class QuestData : ScriptableObject
{
    public string questName;
    public string description;
    public BiomeType biomeType = BiomeType.Forest;
    public Sprite biomeIcon;

    /// <summary>Nome de exibição do bioma. O pareamento com eventos usa biomeType.</summary>
    public string biome => BiomeUtil.GetDisplayName(biomeType);
    public int minDuration;
    public int maxDuration;
    public int baseReward;
    public int recommendedLevel;
    public int corruptionLevel;
    public QuestRisk risk;
    public string objective; // Adicione este campo
    public bool isCorrupted => corruptionLevel >= 50;
    public List<ClassRequirement> requirements;

    public int GetActualDuration()
    {
        return Random.Range(minDuration, maxDuration + 1);
    }

    public int GetTotalReward(int duration)
    {
        return baseReward + (duration * 20);
    }
}

[System.Serializable]
public class ClassRequirement
{
    public HeroClass requiredClass;
    public int minAmount;
    public int minLevel;
}

public enum QuestRisk { Low, Medium, High }