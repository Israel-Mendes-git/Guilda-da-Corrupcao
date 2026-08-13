using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Fileira que um herói ocupa na formação do grupo.</summary>
public enum FormationRow
{
    Front,
    Back
}

/// <summary>
/// Regras da formação do grupo.
///
/// A ordem da party deixa de ser incidental e passa a ser uma decisão: as duas
/// primeiras posições formam a linha de frente, as demais a retaguarda. Quem está
/// à frente atrai a maior parte dos golpes e os recebe por inteiro; quem está atrás
/// é poupado. Em troca, cada classe só rende de verdade no lugar que lhe cabe — um
/// mago empurrado para a frente continua lançando magias, mas bem mais fracas.
///
/// As posições são contadas apenas entre os vivos: quando o da frente cai, quem
/// vinha atrás assume o lugar dele.
/// </summary>
public static class PartyFormation
{
    /// <summary>
    /// Desliga as três consequências da formação (alvo, dano e potência de carta)
    /// sem remover a UI. Serve para comparar duas runs de teste mudando uma coisa
    /// só, e para desligar a mecânica caso ela não agrade.
    /// </summary>
    public static bool Enabled = true;

    /// <summary>Quantas posições formam a linha de frente.</summary>
    public const int FrontSlots = 2;

    /// <summary>Tamanho máximo pensado para a formação.</summary>
    public const int MaxSlots = 4;

    /// <summary>
    /// Rações que um grupo desse tamanho come por dia.
    ///
    /// Não há teto para a party, mas uma boca a mais come de verdade: cada bloco
    /// de <see cref="MaxSlots"/> heróis custa uma ração diária. Levar reservas
    /// deixa de ser grátis sem que a regra precise proibir nada — que é como o
    /// resto do jogo trata escolha do jogador.
    /// </summary>
    public static int DailyRations(int partySize)
    {
        if (partySize <= MaxSlots) return 1;
        return Mathf.CeilToInt(partySize / (float)MaxSlots);
    }

    /// <summary>
    /// Potência de uma carta cujo dono está na fileira errada. Enfraquece em vez
    /// de bloquear: uma mão inteira de cartas proibidas viraria um turno perdido,
    /// e ir mal posicionado deve custar caro sem tirar a jogada do jogador.
    /// </summary>
    public const float OutOfPlaceMultiplier = 0.6f;

    /// <summary>Dano que a retaguarda recebe do que a alcança.</summary>
    public const float BackRowDamageMultiplier = 0.75f;

    // Peso relativo de virar alvo. Com dois na frente e dois atrás, dá cerca de
    // 75% dos ataques na linha de frente.
    const float FrontTargetWeight = 3f;
    const float BackTargetWeight = 1f;

    /// <summary>
    /// Com que frequência o golpe ignora quem está na Beira da Morte.
    ///
    /// Não é um peso, é um sorteio à parte, e a diferença importa: um peso baixo
    /// ainda deixa o moribundo da linha de frente como alvo mais provável que um
    /// aliado inteiro da retaguarda, e o combate passa a matar por azar imediato
    /// em vez de por acúmulo. Este é o número que o combate usava antes de existir
    /// formação, preservado de propósito.
    /// </summary>
    const float SpareDeathsDoorChance = 0.8f;

    /// <summary>Heróis vivos na ordem da formação. É esta lista que define as posições.</summary>
    public static List<HeroData> LivingOrder(IList<HeroData> party)
    {
        if (party == null) return new List<HeroData>();
        return party.Where(h => h != null && h.IsAlive).ToList();
    }

    /// <summary>Posição do herói entre os vivos, começando em 0. -1 se não estiver na formação.</summary>
    public static int GetPosition(HeroData hero, IList<HeroData> party)
    {
        if (hero == null) return -1;
        return LivingOrder(party).IndexOf(hero);
    }

    /// <summary>Fileira ocupada agora. Quem não está na formação conta como retaguarda.</summary>
    public static FormationRow GetRow(HeroData hero, IList<HeroData> party)
    {
        int position = GetPosition(hero, party);
        return position >= 0 && position < FrontSlots ? FormationRow.Front : FormationRow.Back;
    }

    /// <summary>
    /// Onde a classe rende. Nulo significa que ela se vira em qualquer lugar —
    /// é o caso do bardo, que trabalha o grupo inteiro.
    /// </summary>
    public static FormationRow? PreferredRow(HeroClass heroClass)
    {
        switch (heroClass)
        {
            case HeroClass.Warrior:
            case HeroClass.Rogue:
                return FormationRow.Front;

            case HeroClass.Mage:
            case HeroClass.Hunter:
            case HeroClass.Healer:
                return FormationRow.Back;

            default:
                return null;
        }
    }

    /// <summary>O herói está numa fileira que serve à classe dele?</summary>
    public static bool IsWellPlaced(HeroData hero, IList<HeroData> party)
    {
        if (hero == null) return false;

        FormationRow? preferred = PreferredRow(hero.heroClass);
        if (preferred == null) return true;

        return GetRow(hero, party) == preferred.Value;
    }

    /// <summary>Fração do dano que chega ao herói por causa da posição dele.</summary>
    public static float DamageTakenMultiplier(HeroData hero, IList<HeroData> party)
    {
        if (!Enabled) return 1f;
        return GetRow(hero, party) == FormationRow.Back ? BackRowDamageMultiplier : 1f;
    }

    /// <summary>Potência das cartas de um herói conforme ele esteja bem ou mal posicionado.</summary>
    public static float PowerMultiplier(HeroData hero, IList<HeroData> party)
    {
        if (!Enabled) return 1f;
        return IsWellPlaced(hero, party) ? 1f : OutOfPlaceMultiplier;
    }

    /// <summary>
    /// Sorteia quem o inimigo ataca. A linha de frente concentra os golpes;
    /// é o que dá sentido a colocar quem aguenta na frente.
    /// </summary>
    public static HeroData PickTarget(IList<HeroData> party)
    {
        List<HeroData> alive = LivingOrder(party);
        if (alive.Count == 0) return null;

        // Primeiro a proteção de quem está caído, depois a posição. A ordem é o
        // que mantém a letalidade onde estava: quase sempre o golpe procura quem
        // ainda está de pé, e só entre esses a linha de frente atrai mais.
        List<HeroData> pool = alive.Where(h => !h.isOnDeathsDoor).ToList();
        if (pool.Count == 0 || Random.value >= SpareDeathsDoorChance)
            pool = alive;

        if (!Enabled)
            return pool[Random.Range(0, pool.Count)];

        // O peso vem da posição na formação inteira, não do índice dentro do pool:
        // tirar um moribundo da lista não promove ninguém à linha de frente.
        var weights = new float[pool.Count];
        float total = 0f;

        for (int i = 0; i < pool.Count; i++)
        {
            weights[i] = alive.IndexOf(pool[i]) < FrontSlots ? FrontTargetWeight : BackTargetWeight;
            total += weights[i];
        }

        if (total <= 0f)
            return pool[Random.Range(0, pool.Count)];

        float roll = Random.value * total;
        for (int i = 0; i < pool.Count; i++)
        {
            roll -= weights[i];
            if (roll <= 0f) return pool[i];
        }

        return pool[pool.Count - 1];
    }

    /// <summary>Aplica um multiplicador a um valor de carta sem nunca zerar um efeito que existia.</summary>
    public static int Scale(int amount, float multiplier)
    {
        if (amount <= 0) return amount;
        return Mathf.Max(1, Mathf.RoundToInt(amount * multiplier));
    }

    #region Rótulos

    public static string RowLabel(FormationRow row)
    {
        return row == FormationRow.Front ? "Frente" : "Retaguarda";
    }

    /// <summary>Ícone da fileira onde a classe rende, para marcar as fichas de herói.</summary>
    public static string PreferenceIcon(HeroClass heroClass)
    {
        FormationRow? preferred = PreferredRow(heroClass);
        if (preferred == null) return "🎭";
        return preferred.Value == FormationRow.Front ? "⚔️" : "🏹";
    }

    /// <summary>Uma linha explicando a posição do herói, para a tela de preparação.</summary>
    public static string DescribePlacement(HeroData hero, IList<HeroData> party)
    {
        if (hero == null) return string.Empty;

        int position = GetPosition(hero, party);
        FormationRow row = GetRow(hero, party);
        string label = $"{position + 1}. {hero.heroName} — {RowLabel(row)}";

        if (IsWellPlaced(hero, party))
            return $"<color=#4A7A4A>{label}</color>";

        FormationRow? preferred = PreferredRow(hero.heroClass);
        string quer = preferred != null ? RowLabel(preferred.Value).ToLower() : "qualquer fileira";

        return $"<color=#B04040>{label} ⚠️ rende na {quer}</color>";
    }

    #endregion
}
