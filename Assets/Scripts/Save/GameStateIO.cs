using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A ponte entre o jogo vivo e o arquivo de save.
///
/// É o único lugar que conhece as duas pontas: sabe quais managers guardam
/// estado e sabe o formato do <see cref="SaveGame"/>. O <see cref="SaveSystem"/>
/// só escreve e lê bytes; os managers não sabem que save existe.
///
/// <b>O que este arquivo deliberadamente não salva:</b> o meio de uma jornada.
/// Mapa, dia, mão, descarte, mitigação acumulada e combate em andamento vivem em
/// campos privados do <c>JourneyManager</c> e do <c>CombatManager</c>, e
/// reconstruí-los seria um projeto por si só. O ponto de save é a guilda, entre
/// jornadas — que é também onde o autosave cai naturalmente, no fim do ciclo.
/// </summary>
public static class GameStateIO
{
    #region Capturar

    /// <summary>Fotografa o estado de agora.</summary>
    public static SaveGame Capturar()
    {
        var dados = new SaveGame();

        var guilda = GuildManager.Instance;
        if (guilda != null)
        {
            dados.guild = new GuildSave
            {
                gold = guilda.gold,
                reputation = guilda.reputation,
                maxRosterSize = guilda.maxRosterSize,
                relicStock = new List<string>(guilda.relicStock ?? new List<string>()),
                potionStock = new List<string>(guilda.potionStock ?? new List<string>())
            };

            foreach (var heroi in guilda.roster)
                if (heroi != null) dados.roster.Add(DeHeroi(heroi));

            foreach (var caido in guilda.fallenHeroes)
                if (caido != null) dados.fallen.Add(DeHeroi(caido));
        }

        var run = RunManager.Instance;
        if (run != null)
        {
            dados.run = new RunSave
            {
                cycle = run.Cycle,
                corruption = run.Corruption,
                state = (int)run.State,
                endReason = (int)run.EndReason,
                fallen = run.Fallen,
                regionCorruption = RegionMap.Serializar(),
                regionMapping = RegionMap.SerializarMapeamento(),
                regionSealed = RegionMap.SerializarSelos(),
                writingsOnShelf = Escritos.SerializarEstante(),
                writingsTranslated = Escritos.SerializarLidos(),
                lastTranslationCycle = Escritos.CicloDaUltimaTraducao
            };
        }

        var quests = QuestManager.Instance;
        if (quests != null)
            foreach (var missao in quests.GetQuests())
                if (missao != null) dados.quests.Add(DeMissao(missao));

        // Só os decks de quem está no roster: um deck órfão sobreviveria a run
        // inteira crescendo o arquivo sem que nada volte a lê-lo.
        dados.decks = DeckRepository.Exportar(dados.roster);

        return dados;
    }

    static HeroSave DeHeroi(HeroData h) => new HeroSave
    {
        heroId = h.GetId(),
        heroName = h.heroName,
        heroClass = (int)h.heroClass,
        level = h.level,
        maxHp = h.maxHp,
        currentHp = h.currentHp,
        salary = h.salary,
        personality = (int)h.personality,
        trait = (int)h.trait,
        loyalty = h.loyalty,
        morale = h.morale,
        isInjured = h.isInjured,
        isDead = h.isDead,
        corruptionExposure = h.corruptionExposure,
        xp = h.xp,
        xpToNextLevel = h.xpToNextLevel,
        stress = h.stress,
        mentalState = (int)h.mentalState,
        isOnDeathsDoor = h.isOnDeathsDoor,
        weaponLevel = h.weaponLevel,
        armorLevel = h.armorLevel,

        // Cópias, não as listas do herói: o save não pode ficar apontando para
        // dentro de um asset vivo, ou salvar vira uma foto que continua mudando.
        relics = new List<string>(h.relics ?? new List<string>()),
        potions = new List<string>(h.potions ?? new List<string>())
    };


    static QuestSave DeMissao(QuestData q) => new QuestSave
    {
        questName = q.questName,
        description = q.description,
        biomeType = (int)q.biomeType,
        minDuration = q.minDuration,
        maxDuration = q.maxDuration,
        baseReward = q.baseReward,
        recommendedLevel = q.recommendedLevel,
        corruptionLevel = q.corruptionLevel,
        risk = (int)q.risk,
        objective = q.objective,
        isFinalBoss = q.isFinalBoss,
        isRegionBoss = q.isRegionBoss,
        requirements = q.requirements ?? new List<ClassRequirement>()
    };

    #endregion

    #region Aplicar

    /// <summary>
    /// Reconstrói o jogo a partir do save.
    ///
    /// Chamado logo depois de a cena carregar e antes dos <c>Start</c> — é o que
    /// faz o <c>GuildManager</c> encontrar o roster já cheio e não criar o elenco
    /// inicial por cima. Devolve false quando o save não serve.
    /// </summary>
    public static bool Aplicar(SaveGame dados)
    {
        if (dados == null) return false;

        var guilda = GuildManager.Instance;
        if (guilda == null)
        {
            Debug.LogError("GameStateIO: não há GuildManager para receber o save.");
            return false;
        }

        // Os decks entram antes dos heróis porque o repositório é indexado por
        // heroId, e o herói reconstruído já chega perguntando pelo dele.
        DeckRepository.Importar(dados.decks);

        guilda.roster.Clear();
        guilda.fallenHeroes.Clear();

        if (dados.guild != null)
        {
            guilda.gold = dados.guild.gold;
            guilda.reputation = dados.guild.reputation;

            // Um save feito antes de os alojamentos existirem traz 0 aqui; nesse
            // caso vale o que o Inspector diz, não um roster de tamanho zero.
            if (dados.guild.maxRosterSize > 0)
                guilda.maxRosterSize = dados.guild.maxRosterSize;

            guilda.relicStock = new List<string>(dados.guild.relicStock ?? new List<string>());
            guilda.potionStock = new List<string>(dados.guild.potionStock ?? new List<string>());
        }

        foreach (var salvo in dados.roster)
            if (salvo != null) guilda.roster.Add(ParaHeroi(salvo));

        foreach (var salvo in dados.fallen)
            if (salvo != null) guilda.fallenHeroes.Add(ParaHeroi(salvo));

        var run = RunManager.Instance;
        if (run != null && dados.run != null)
        {
            run.Restaurar(dados.run.cycle, dados.run.corruption,
                          (RunState)dados.run.state, (RunEndReason)dados.run.endReason,
                          dados.run.fallen);

            RegionMap.Restaurar(dados.run.regionCorruption, dados.run.regionMapping, dados.run.regionSealed);

            Escritos.Restaurar(dados.run.writingsOnShelf, dados.run.writingsTranslated,
                               dados.run.lastTranslationCycle);
        }

        var quests = QuestManager.Instance;
        if (quests != null)
        {
            var missoes = new List<QuestData>();
            foreach (var salva in dados.quests)
                if (salva != null) missoes.Add(ParaMissao(salva));

            quests.SetQuests(missoes);
        }

        guilda.NotificarTudoMudou();
        return true;
    }

    static HeroData ParaHeroi(HeroSave s)
    {
        var h = ScriptableObject.CreateInstance<HeroData>();

        h.AtribuirId(s.heroId);
        h.heroName = s.heroName;
        h.heroClass = (HeroClass)s.heroClass;
        h.level = s.level;
        h.maxHp = s.maxHp;
        h.currentHp = s.currentHp;
        h.salary = s.salary;
        h.personality = (Personality)s.personality;
        h.trait = (Trait)s.trait;
        h.loyalty = s.loyalty;
        h.morale = s.morale;
        h.isInjured = s.isInjured;
        h.isDead = s.isDead;
        h.corruptionExposure = s.corruptionExposure;
        h.xp = s.xp;
        h.xpToNextLevel = s.xpToNextLevel > 0 ? s.xpToNextLevel : HeroData.XpNecessarioPara(s.level);
        h.stress = s.stress;
        h.mentalState = (MentalState)s.mentalState;
        h.isOnDeathsDoor = s.isOnDeathsDoor;
        h.weaponLevel = s.weaponLevel;
        h.armorLevel = s.armorLevel;

        // Save anterior aos itens não traz as listas: o herói volta desequipado,
        // que é o estado certo para quem jogou antes de eles existirem.
        h.relics = new List<string>(s.relics ?? new List<string>());
        h.potions = new List<string>(s.potions ?? new List<string>());

        h.name = string.IsNullOrEmpty(s.heroName) ? "Herói" : s.heroName;

        // O retrato sai do nome, não do arquivo: é a mesma regra da HeroFactory,
        // e é ela que faz o mesmo herói manter a cara entre sessões sem que o
        // save precise carregar referência de sprite nenhuma.
        if (PortraitCatalog.Instance != null)
            h.portrait = PortraitCatalog.Instance.Para(h.heroName);

        return h;
    }

    static QuestData ParaMissao(QuestSave s)
    {
        var q = ScriptableObject.CreateInstance<QuestData>();

        q.questName = s.questName;
        q.description = s.description;
        q.biomeType = (BiomeType)s.biomeType;
        q.minDuration = s.minDuration;
        q.maxDuration = s.maxDuration;
        q.baseReward = s.baseReward;
        q.recommendedLevel = s.recommendedLevel;
        q.corruptionLevel = s.corruptionLevel;
        q.risk = (QuestRisk)s.risk;
        q.objective = s.objective;
        q.isFinalBoss = s.isFinalBoss;
        q.isRegionBoss = s.isRegionBoss;
        q.requirements = s.requirements ?? new List<ClassRequirement>();

        q.name = string.IsNullOrEmpty(s.questName) ? "Missão" : s.questName;

        if (BiomeArtCatalog.Instance != null)
            q.biomeIcon = BiomeArtCatalog.Instance.Para(q.biomeType);

        return q;
    }

    #endregion
}
