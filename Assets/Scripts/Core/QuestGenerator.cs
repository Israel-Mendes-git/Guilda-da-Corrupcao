using System.Collections.Generic;
using UnityEngine;

public static class QuestGenerator
{
    /// <summary>
    /// O lugar da missão, com o gênero do substantivo. O gênero mora aqui porque
    /// é dele que o estado depende: com as duas listas sorteadas à parte, o
    /// quadro oferecia "Vila Antigo", "Cripta Sagrado" e "Masmorra Antigo".
    /// </summary>
    private static readonly (string nome, bool feminino)[] lugares =
    {
        ("Túmulo", false), ("Caverna", true), ("Torre", true), ("Santuário", false),
        ("Vila", true), ("Masmorra", true), ("Templo", false), ("Cripta", true)
    };

    /// <summary>O estado do lugar, nas duas formas. Quem escolhe é o lugar sorteado.</summary>
    private static readonly (string masculino, string feminino)[] estados =
    {
        ("Antigo", "Antiga"), ("Perdido", "Perdida"), ("Abandonado", "Abandonada"),
        ("Profano", "Profana"), ("Assombrado", "Assombrada"), ("Amaldiçoado", "Amaldiçoada"),
        ("Sagrado", "Sagrada"), ("Esquecido", "Esquecida")
    };
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

            // Nome da quest. Sem o bioma: todo lugar que mostra o nome mostra a
            // região ao lado — o quadro tem o cabeçalho da região, a ficha tem a
            // linha "📍 Bioma:" e o HUD da jornada tem o Txt_Biome. Escrito nos
            // dois, saía "Santuário Amaldiçoado - 🏯 Ruínas" embaixo de "🏯 Ruínas".
            var lugar = lugares[Random.Range(0, lugares.Length)];
            var estado = estados[Random.Range(0, estados.Length)];
            quest.questName = $"{lugar.nome} {(lugar.feminino ? estado.feminino : estado.masculino)}";

            // Duração (baseada no nível do jogador)
            int baseDuration = Random.Range(4, 8);
            quest.minDuration = baseDuration;
            quest.maxDuration = baseDuration + Random.Range(2, 5);

            // Recompensa
            quest.baseReward = 50 + (playerLevel * 20) + Random.Range(0, 80);

            // Nível recomendado
            quest.recommendedLevel = Mathf.Max(1, playerLevel + Random.Range(-1, 2));

            // Corrupção: é a da região, não um sorteio.
            //
            // Era Random.Range(0, 100) — e por isso o mundo nunca piorava. Depois
            // virou o medidor global mais um sorteio, o que fazia o mundo andar,
            // mas ainda deixava duas missões no mesmo bioma saírem uma limpa e
            // outra podre. Agora vem do RegionMap: o estado do lugar é do lugar,
            // e é o que o mapa mostra antes de o jogador escolher o destino.
            //
            // A variação pequena que sobrou é da missão em si — um ponto mais
            // exposto dentro da mesma região —, não do mundo.
            quest.corruptionLevel = Mathf.Clamp(
                Mathf.RoundToInt(RegionMap.Corrupcao(quest.biomeType)) + Random.Range(-5, 6), 0, 100);

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

    /// <summary>
    /// A luta que sela uma região. Nasce da região, não de sorteio: o bioma vem
    /// mapeado de fora, e o nome traz quem guarda o lugar, porque é isso que
    /// mapear revelou.
    ///
    /// Duração e recompensa ficam acima do contrato comum e abaixo da jornada
    /// final — é uma expedição a um lugar que a guilda já conhece inteiro.
    /// </summary>
    public static QuestData GenerateRegionBossQuest(BiomeType regiao, int playerLevel)
    {
        if (regiao == BiomeType.Any) return null;

        QuestData quest = ScriptableObject.CreateInstance<QuestData>();
        quest.biomeType = regiao;

        string chefe = EnemyPool.NomeDoChefe(regiao);
        string lugar = BiomeUtil.GetDisplayName(regiao);

        quest.questName = string.IsNullOrEmpty(chefe)
            ? $"🔒 Selar {lugar}"
            : $"🔒 Selar {lugar} — {chefe}";
        quest.objective = string.IsNullOrEmpty(chefe)
            ? "Derrube o que guarda a região e sele o lugar"
            : $"Derrube {chefe} e sele a região";

        quest.minDuration = 7;
        quest.maxDuration = 10;
        quest.baseReward = 200 + (playerLevel * 20);
        quest.recommendedLevel = playerLevel + 1;

        // A corrupção da missão é a da própria região, e não um número fixo: o
        // preço de demorar para selar é a luta ficar pior, que é a mesma regra
        // que já vale para voltar ao mesmo lugar.
        quest.corruptionLevel = Mathf.RoundToInt(RegionMap.Corrupcao(regiao));
        quest.risk = QuestRisk.High;
        quest.isRegionBoss = true;

        quest.requirements = new List<ClassRequirement>
        {
            new ClassRequirement { requiredClass = HeroClass.Warrior, minAmount = 1, minLevel = playerLevel }
        };

        return quest;
    }

    /// <summary>
    /// A jornada final.
    ///
    /// <b>O lugar não é sorteado desde 09/09:</b> a passagem se abre na região
    /// que foi selada por último, e é para lá que o grupo marcha. Enquanto não
    /// houver selo nenhum — caso que só acontece se alguém chamar isto fora de
    /// hora — cai no sorteio antigo em vez de devolver missão sem lugar.
    /// </summary>
    public static QuestData GenerateBossQuest(int playerLevel)
    {
        BiomeType passagem = RegionMap.UltimoSelo;

        QuestData bossQuest = ScriptableObject.CreateInstance<QuestData>();
        bossQuest.biomeType = passagem != BiomeType.Any ? passagem : BiomeUtil.GetRandom();
        bossQuest.questName = passagem != BiomeType.Any
            ? $"⚔️ A PASSAGEM — {BiomeUtil.GetDisplayName(passagem)} ⚔️"
            : "⚔️ CHEFE SUPREMO ⚔️";
        bossQuest.minDuration = 10;
        bossQuest.maxDuration = 15;
        bossQuest.baseReward = 300 + (playerLevel * 30);
        bossQuest.recommendedLevel = playerLevel + 2;
        bossQuest.risk = QuestRisk.High;
        bossQuest.objective = passagem != BiomeType.Any
            ? $"O último selo abriu a passagem em {BiomeUtil.GetDisplayName(passagem)}. Vá até ela."
            : "Derrote o Chefe Supremo";
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