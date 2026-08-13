using System.Collections.Generic;
using UnityEngine;

public static class QuestGenerator
{
    private static string[] questPrefixes = { "Túmulo", "Caverna", "Torre", "Santuário", "Vila", "Masmorra", "Templo", "Cripta" };
    private static string[] questSuffixes = { "Antigo", "Perdido", "Abandonado", "Profano", "Assombrado", "Amaldiçoado", "Sagrado", "Esquecido" };
    private static string[] objectives = {
        "Derrote o chefe",
        "Colete recursos",
        "Resgate os prisioneiros",
        "Explore a região",
        "Sobreviva aos dias",
        "Encontre o artefato"
    };

    public static List<QuestData> GenerateQuests(int amount, int playerLevel)
    {
        List<QuestData> quests = new List<QuestData>();

        for (int i = 0; i < amount; i++)
        {
            QuestData quest = ScriptableObject.CreateInstance<QuestData>();

            // Bioma
            quest.biomeType = BiomeUtil.GetRandom();

            // Nome da quest
            string prefix = questPrefixes[Random.Range(0, questPrefixes.Length)];
            string suffix = questSuffixes[Random.Range(0, questSuffixes.Length)];
            quest.questName = $"{prefix} {suffix} - {quest.biome}";

            // Duração (baseada no nível do jogador)
            int baseDuration = Random.Range(4, 8);
            quest.minDuration = baseDuration;
            quest.maxDuration = baseDuration + Random.Range(2, 5);

            // Recompensa
            quest.baseReward = 50 + (playerLevel * 20) + Random.Range(0, 80);

            // Nível recomendado
            quest.recommendedLevel = Mathf.Max(1, playerLevel + Random.Range(-1, 2));

            // Corrupção (quanto maior, mais difícil)
            quest.corruptionLevel = Random.Range(0, 100);

            // Risco
            quest.risk = (QuestRisk)Random.Range(0, 3);

            // Objetivo
            quest.objective = objectives[Random.Range(0, objectives.Length)];

            // Requisitos aleatórios baseados na dificuldade
            quest.requirements = GenerateRequirements(quest.risk, quest.corruptionLevel);

            quests.Add(quest);
        }

        return quests;
    }

    private static List<ClassRequirement> GenerateRequirements(QuestRisk risk, int corruption)
    {
        List<ClassRequirement> requirements = new List<ClassRequirement>();

        int requirementCount = 0;

        // Corrupção alta ou risco alto = mais requisitos
        if (corruption > 70 || risk == QuestRisk.High)
            requirementCount = Random.Range(1, 3);
        else if (corruption > 40 || risk == QuestRisk.Medium)
            requirementCount = Random.Range(0, 2);
        else
            requirementCount = Random.Range(0, 1);

        for (int i = 0; i < requirementCount; i++)
        {
            HeroClass[] classes = { HeroClass.Warrior, HeroClass.Mage, HeroClass.Healer, HeroClass.Hunter };
            HeroClass requiredClass = classes[Random.Range(0, classes.Length)];

            requirements.Add(new ClassRequirement
            {
                requiredClass = requiredClass,
                minAmount = Random.Range(1, 3),
                minLevel = Random.Range(1, 4)
            });
        }

        return requirements;
    }

    public static QuestData GenerateBossQuest(int playerLevel)
    {
        QuestData bossQuest = ScriptableObject.CreateInstance<QuestData>();
        bossQuest.biomeType = BiomeUtil.GetRandom();
        bossQuest.questName = $"⚔️ CHEFE SUPREMO - {bossQuest.biome} ⚔️";
        bossQuest.minDuration = 10;
        bossQuest.maxDuration = 15;
        bossQuest.baseReward = 300 + (playerLevel * 30);
        bossQuest.recommendedLevel = playerLevel + 2;
        bossQuest.corruptionLevel = 90;
        bossQuest.risk = QuestRisk.High;
        bossQuest.objective = "Derrote o Chefe Supremo";
        bossQuest.corruptionLevel = 90;

        // Requisitos mais difíceis
        bossQuest.requirements = new List<ClassRequirement>
        {
            new ClassRequirement { requiredClass = HeroClass.Warrior, minAmount = 1, minLevel = playerLevel },
            new ClassRequirement { requiredClass = HeroClass.Healer, minAmount = 1, minLevel = playerLevel }
        };

        return bossQuest;
    }
}