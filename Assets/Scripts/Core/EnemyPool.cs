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
    /// <param name="partySize">Quantos heróis saíram da guilda. Abaixo de quatro
    /// o encontro encolhe na mesma proporção; acima, fica como está.</param>
    /// <param name="area">A área da jornada: a Torre e a Cripta põem gente a
    /// mais do outro lado (<see cref="DaArea"/>). <c>None</c> é o encontro
    /// puro do bestiário.</param>
    /// <param name="party">Quem está na estrada — a Torre copia um deles.</param>
    public static List<EnemyData> GetLineup(BiomeType biome, bool bossFight, int day, int totalDays = 0,
                                            int partySize = PartyFormation.MaxSlots,
                                            AreaType area = AreaType.None, IList<HeroData> party = null)
    {
        Initialize();

        var lineup = new List<EnemyData>();

        if (bossFight)
        {
            EnemyData boss = PickBoss(biome);
            if (boss != null) lineup.Add(boss);

            // Um capanga a partir da metade da jornada — para um grupo que tem
            // gente para dividir a atenção. Dois heróis contra chefe e capanga
            // é a conta que a Torre faz de propósito, não a de um selo comum.
            if (Progresso(day, totalDays) >= 0.5f && partySize >= 3)
            {
                EnemyData minion = PickRegular(biome);
                if (minion != null) lineup.Add(minion);
            }

            DaArea(lineup, area, party);
            return lineup;
        }

        float p = Progresso(day, totalDays);
        int count = ParaOGrupo(p >= TerceiroInimigoAPartirDe ? 3 : p >= SegundoInimigoAPartirDe ? 2 : 1, partySize);
        for (int i = 0; i < count; i++)
        {
            EnemyData enemy = PickRegular(biome);
            if (enemy != null) lineup.Add(enemy);
        }

        if (lineup.Count == 0)
            lineup.Add(CreateFallback(biome));

        DaArea(lineup, area, party);
        return lineup;
    }

    /// <summary>
    /// Em que fração da rota o encontro comum passa a dois e a três inimigos.
    ///
    /// Campos, e não literais, para a varredura de balanceamento medir "e se o
    /// terceiro só viesse no último quinto?" sem tocar no código. A curva foi
    /// medida para 3,4 lutas por jornada; com metade dos pontos da rota em luta
    /// (13/09) ela passou a ser a régua que decide a letalidade.
    /// </summary>
    public static float SegundoInimigoAPartirDe = 0.25f;
    public static float TerceiroInimigoAPartirDe = 0.66f;

    /// <summary>
    /// O que a área põe do outro lado além do bestiário — desde 13/09.
    ///
    /// <b>A Torre</b> copia um herói vivo do grupo, com o nível, a arma e a
    /// armadura dele: é a ideia do Campeão em pequeno, no meio do jogo. A cópia
    /// <b>toma o lugar</b> de um inimigo comum quando há mais de um — somada,
    /// ela fazia a Torre custar 2,7 mortes por jornada contra 0,3 da Mata. <b>A
    /// Cripta</b> levanta um dos mortos da guilda que ficou sem tributo no
    /// Cemitério — a dívida do jogador entra aqui, e é o que dá função ao
    /// monumento; essa soma, porque é dívida. Uma cópia por combate, sorteada.
    /// </summary>
    static void DaArea(List<EnemyData> lineup, AreaType area, IList<HeroData> party)
    {
        if (area == AreaType.Torre)
        {
            var vivos = party?.Where(h => h != null && h.IsAlive).ToList();
            if (vivos != null && vivos.Count > 0)
            {
                int comuns = lineup.Count(e => e != null && !e.isBoss);
                if (comuns >= 2) lineup.RemoveAt(lineup.FindLastIndex(e => e != null && !e.isBoss));
                lineup.Add(CopiaDoHeroi(vivos[Random.Range(0, vivos.Count)], erguido: false));
            }
        }
        else if (area == AreaType.Cripta)
        {
            var mortos = AreaRules.MortosSemTributo();
            if (mortos.Count > 0)
                lineup.Add(CopiaDoHeroi(mortos[Random.Range(0, mortos.Count)], erguido: true));
        }
    }

    /// <summary>
    /// Um inimigo montado de um herói — a cópia da Torre ou o morto erguido da
    /// Cripta. Criado em memória a cada luta: não é asset, e não deve ser.
    ///
    /// Os números vêm do que o herói é: a vida dele, o dano da arma forjada e
    /// o nível, a guarda da armadura. O rótulo diz de quem é a cara.
    /// </summary>
    public static EnemyData CopiaDoHeroi(HeroData heroi, bool erguido)
    {
        EnemyData e = ScriptableObject.CreateInstance<EnemyData>();
        e.enemyName = erguido ? $"{heroi.heroName}, erguido" : $"O reflexo de {heroi.heroName}";
        e.description = erguido
            ? "Enterrado sem tributo. Voltou com o que tinha."
            : "Tem o seu rosto, o seu aço e nenhuma hesitação.";
        e.biome = BiomeType.Any;
        e.isBoss = false;
        e.maxHp = Mathf.Max(10, heroi.maxHp);
        e.attackDamage = 4 + heroi.level * 2 + ForgeManager.WeaponBonus(heroi);
        e.blockAmount = 3 + heroi.armorLevel * 2;
        e.stressDamage = erguido ? 12 : 8;
        e.attackWeight = 60;
        e.defendWeight = 15;
        e.stressWeight = erguido ? 20 : 10;
        e.attackAllWeight = erguido ? 5 : 15;
        e.goldReward = erguido ? 0 : 20;
        e.portrait = heroi.portrait;
        e.portraitTint = erguido ? new Color(0.62f, 0.70f, 0.66f) : new Color(0.80f, 0.72f, 0.90f);
        e.name = e.enemyName;
        return e;
    }

    /// <summary>Este inimigo foi montado de um herói? (a cópia da Torre ou o erguido)</summary>
    public static bool EhCopiaDeHeroi(EnemyData e) =>
        e != null && !e.isBoss && e.biome == BiomeType.Any
        && (e.enemyName.StartsWith("O reflexo de ") || e.enemyName.EndsWith(", erguido"));

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
    /// O encontro no tamanho do grupo.
    ///
    /// A curva de um, dois e três inimigos foi medida para quatro heróis. Desde
    /// 12/09 a guilda nasce com dois, e dois contra três no fim da Mata não é
    /// punição, é aniquilação: o encontro encolhe na proporção do grupo,
    /// arredondando para cima — dois heróis veem um inimigo até o meio da rota
    /// e dois no fim. Grupo maior que quatro não engorda o encontro: o preço de
    /// levar mais gente já é comida e experiência diluída.
    /// </summary>
    static int ParaOGrupo(int count, int partySize)
    {
        int grupo = Mathf.Clamp(partySize, 1, PartyFormation.MaxSlots);
        return Mathf.Clamp(Mathf.CeilToInt(count * grupo / (float)PartyFormation.MaxSlots), 1, 3);
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
