using System.Collections.Generic;
using UnityEngine;

public static class QuestGenerator
{
    /// <summary>
    /// Uma expedição a uma área sorteada — o que o simulador e os testes usam
    /// quando precisam de uma jornada qualquer.
    ///
    /// Substituiu o <c>GenerateQuests</c>, que fabricava contratos com nome
    /// sorteado ("Cripta Esquecida") e destino sorteado. O quadro deixou de
    /// oferecer destino em 11/09, e manter um gerador de contratos vivo só para
    /// os testes faria o simulador medir um jogo que não existe mais — que é
    /// exatamente o erro que já custou cinco conclusões invertidas neste projeto.
    /// </summary>
    public static QuestData ExpedicaoQualquer(int playerLevel)
    {
        var area = AreaCatalog.Todas[Random.Range(0, AreaCatalog.Todas.Length)];
        return GerarExpedicao(area, playerLevel);
    }

    /// <summary>
    /// A expedição a uma área — o que nasce de clicar no mapa, desde 11/09.
    ///
    /// Substitui o contrato do quadro como forma de escolher destino. O que muda
    /// não é o formato (continua sendo um <see cref="QuestData"/>, e a jornada
    /// inteira a jusante não sabe de onde ela veio), e sim de onde vêm os
    /// números: <b>a distância cobra os dias</b>, e o lugar cobra o resto.
    ///
    /// Ida e volta pela rota, mais dois a quatro dias dentro da área. É por isso
    /// que a Mata sai por 6 a 8 dias e o Covil por 14 a 16 — e é o que faz o
    /// mapa ser uma decisão em vez de uma lista.
    /// </summary>
    public static QuestData GerarExpedicao(AreaType area, int playerLevel)
    {
        var ficha = AreaCatalog.De(area);
        if (ficha == null) return null;

        QuestData quest = ScriptableObject.CreateInstance<QuestData>();
        quest.biomeType = ficha.aspecto;
        quest.questName = ficha.NomeComIcone;
        quest.objective = ficha.oQueDa;
        quest.description = ficha.regra;

        int estrada = AreaCatalog.DiasDeIda(area) * 2;
        quest.minDuration = estrada + 2;
        quest.maxDuration = estrada + 4;

        // A corrupção é a da área, com a variação pequena do ponto exato da
        // rota — a mesma regra que o contrato já usava.
        quest.corruptionLevel = Mathf.Clamp(
            Mathf.RoundToInt(RegionMap.Corrupcao(ficha.aspecto)) + Random.Range(-5, 6), 0, 100);

        // O espólio cresce com a distância: é o único jeito de o fundo do mapa
        // valer o risco de atravessar tudo o que está no caminho.
        quest.baseReward = 40 + AreaCatalog.Saltos(area) * 25 + playerLevel * 10;
        quest.recommendedLevel = Mathf.Max(1, playerLevel + (AreaCatalog.Saltos(area) >= 3 ? 1 : 0));

        quest.risk = quest.corruptionLevel > 65 || AreaCatalog.Saltos(area) >= 4 ? QuestRisk.High
                   : quest.corruptionLevel > 35 || AreaCatalog.Saltos(area) >= 2 ? QuestRisk.Medium
                   : QuestRisk.Low;

        quest.requirements = GenerateRequirements(quest.risk, quest.corruptionLevel);

        return quest;
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
        string lugar = AreaCatalog.Nome(AreaCatalog.Da(regiao));

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
            ? $"⚔️ A PASSAGEM — {AreaCatalog.Nome(AreaCatalog.Da(passagem))} ⚔️"
            : "⚔️ CHEFE SUPREMO ⚔️";
        bossQuest.minDuration = 10;
        bossQuest.maxDuration = 15;
        bossQuest.baseReward = 300 + (playerLevel * 30);
        bossQuest.recommendedLevel = playerLevel + 2;
        bossQuest.risk = QuestRisk.High;
        bossQuest.objective = passagem != BiomeType.Any
            ? $"O último selo abriu a passagem em {AreaCatalog.Nome(AreaCatalog.Da(passagem))}. Vá até ela."
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