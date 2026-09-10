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

    /// <summary>
    /// A missão do Chefe Supremo — a única vitória possível de uma run.
    ///
    /// Campo no fim da classe de propósito: os assets guardam a ordem, e há
    /// missões geradas em runtime que precisam continuar carregando.
    /// </summary>
    public bool isFinalBoss;

    /// <summary>
    /// A luta que sela a região: só aparece no quadro depois que a região está
    /// inteira no mapa, e vencê-la trava a corrupção dali para sempre.
    ///
    /// É diferente do <see cref="isFinalBoss"/>, que é a jornada final — aquela
    /// só existe com três selos na mesa.
    /// </summary>
    public bool isRegionBoss;

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