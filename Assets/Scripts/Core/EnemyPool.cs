using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Carrega os inimigos de Resources/Enemies e monta a formação de cada combate.
/// Mesma ideia do EventPool: pareia por BiomeType, com Any como curinga.
/// </summary>
public static class EnemyPool
{
    private static List<EnemyData> allEnemies = new List<EnemyData>();
    private static bool isInitialized;

    public static void Initialize()
    {
        if (isInitialized) return;

        allEnemies.Clear();
        allEnemies.AddRange(Resources.LoadAll<EnemyData>("Enemies"));

        if (allEnemies.Count == 0)
            CreateDefaults();

        isInitialized = true;
        Debug.Log($"EnemyPool inicializado com {allEnemies.Count} inimigos");
    }

    /// <summary>
    /// Formação para um combate. Chefes vêm sozinhos; encontros normais, em grupo.
    ///
    /// <b>O grupo cresce com o quanto da rota já foi andado, não com o número do
    /// dia.</b> "Dia 5" queria dizer "perto do fim" quando toda jornada tinha
    /// sete dias. Com o plano navegável, a expedição ao Covil tem quatorze — e a
    /// régua antiga punha três inimigos em quase todo encontro a partir do quinto
    /// dia, o que sozinho levou a letalidade de 0,56 para 0,84 mortes por
    /// jornada. Pela fração, a viagem longa tem a mesma curva da curta: começa
    /// com um, dobra no meio, triplica no fim.
    /// </summary>
    /// <param name="totalDays">Dias previstos para a rota. Zero mantém a régua
    /// antiga, para quem chama sem saber o tamanho da viagem.</param>
    public static List<EnemyData> GetLineup(BiomeType biome, bool bossFight, int day, int totalDays = 0)
    {
        Initialize();

        var lineup = new List<EnemyData>();

        if (bossFight)
        {
            EnemyData boss = PickBoss(biome);
            if (boss != null) lineup.Add(boss);

            // Um capanga a partir da metade da jornada.
            if (Progresso(day, totalDays) >= 0.5f)
            {
                EnemyData minion = PickRegular(biome);
                if (minion != null) lineup.Add(minion);
            }

            return lineup;
        }

        float p = Progresso(day, totalDays);
        int count = p >= 0.66f ? 3 : p >= 0.25f ? 2 : 1;
        for (int i = 0; i < count; i++)
        {
            EnemyData enemy = PickRegular(biome);
            if (enemy != null) lineup.Add(enemy);
        }

        if (lineup.Count == 0)
            lineup.Add(CreateFallback(biome));

        return lineup;
    }

    /// <summary>
    /// Quem guarda aquela região, pelo nome. O quadro precisa disto para que a
    /// missão de selo diga contra o que o grupo vai — mapear uma região é
    /// justamente descobrir isso.
    ///
    /// Sem sorteio, ao contrário do <see cref="PickBoss"/>: o chefe da região é
    /// sempre o mesmo, e o nome muda no quadro seria o jogador aprendendo errado.
    /// Três regiões não têm chefe próprio e caem no curinga (<c>Any</c>) — é
    /// dívida de conteúdo conhecida, não falha daqui.
    /// </summary>
    public static string NomeDoChefe(BiomeType biome)
    {
        Initialize();

        var bosses = allEnemies.Where(e => e != null && e.isBoss).ToList();

        var especifico = bosses.Where(e => e.biome == biome).ToList();
        if (especifico.Count > 0) return especifico[0].enemyName;

        var curinga = bosses.Where(e => e.biome == BiomeType.Any).ToList();
        if (curinga.Count > 0) return curinga[0].enemyName;

        return null;
    }

    static EnemyData PickBoss(BiomeType biome)
    {
        var bosses = allEnemies.Where(e => e.isBoss && BiomeUtil.Matches(e.biome, biome)).ToList();
        var specific = bosses.Where(e => e.biome == biome).ToList();

        if (specific.Count > 0) return specific[Random.Range(0, specific.Count)];
        if (bosses.Count > 0) return bosses[Random.Range(0, bosses.Count)];

        return null;
    }

    /// <summary>
    /// Onde o grupo está na rota, de 0 a 1. Sem o total, cai na régua de sete
    /// dias — que era a duração de toda jornada até 11/09.
    /// </summary>
    static float Progresso(int day, int totalDays)
    {
        int total = totalDays > 0 ? totalDays : 7;
        return Mathf.Clamp01(day / (float)total);
    }

    static EnemyData PickRegular(BiomeType biome)
    {
        var pool = allEnemies.Where(e => !e.isBoss && BiomeUtil.Matches(e.biome, biome)).ToList();
        if (pool.Count == 0) return null;

        var specific = pool.Where(e => e.biome == biome).ToList();
        if (specific.Count > 0 && Random.value < 0.7f)
            pool = specific;

        return pool[Random.Range(0, pool.Count)];
    }

    static EnemyData CreateFallback(BiomeType biome)
    {
        EnemyData e = ScriptableObject.CreateInstance<EnemyData>();
        e.enemyName = "Criatura Errante";
        e.description = "Algo que vive onde não deveria.";
        e.biome = biome;
        e.maxHp = 26;
        e.attackDamage = 6;
        e.blockAmount = 4;
        e.stressDamage = 6;
        e.goldReward = 20;
        return e;
    }

    static void CreateDefaults()
    {
        // Rede de segurança: só entra em uso se Resources/Enemies estiver vazia.
        allEnemies.Add(CreateFallback(BiomeType.Any));

        EnemyData boss = ScriptableObject.CreateInstance<EnemyData>();
        boss.enemyName = "Guardião Sem Nome";
        boss.description = "Ele esperava por vocês.";
        boss.biome = BiomeType.Any;
        boss.isBoss = true;
        boss.maxHp = 80;
        boss.attackDamage = 12;
        boss.blockAmount = 8;
        boss.stressDamage = 14;
        boss.goldReward = 150;
        allEnemies.Add(boss);
    }
}
