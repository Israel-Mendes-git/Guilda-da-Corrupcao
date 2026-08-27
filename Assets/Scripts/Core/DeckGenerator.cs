using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Monta o baralho inicial de um herói.
///
/// <b>O que estava errado.</b> A montagem antiga colocava duas comuns, as raras,
/// a épica, e depois completava o baralho sorteando comuns até chegar ao tamanho.
/// Com duas cartas comuns por classe, isso dava oito cópias de duas cartas num
/// baralho de onze — o baralho de todo Guerreiro do mesmo nível era o mesmo
/// baralho, e o "montar deck" não decidia nada. Pior: o laço de completar
/// ignorava o teto de <see cref="DeckManager.MaxCopiasPorCarta"/>, de modo que o
/// gerador entregava um baralho que o próprio editor de baralhos recusaria
/// montar, e o Curandeiro saía para a jornada sem nenhuma carta que ferisse.
///
/// <b>O que ficou.</b> Duas camadas. Primeiro as cartas de recompensa, na mesma
/// progressão de antes — é ela que faz subir de nível valer a pena. Depois o
/// corpo do baralho, distribuído por <see cref="CardRole"/> em fatias fixas, com
/// as cópias espalhadas em rodízio dentro de cada papel. O teto de cópias é o
/// mesmo do editor e da biblioteca.
///
/// <b>Rejeitado.</b> Sortear com peso por raridade: continua concentrando o
/// baralho na carta mais forte e não garante que exista defesa nele. E fixar a
/// composição carta a carta por classe: vira uma segunda tabela para manter em dia
/// toda vez que uma carta nova entra em <c>Resources/Cards</c>.
/// </summary>
public static class DeckGenerator
{
    private static readonly Dictionary<HeroClass, List<CardData>> cardCache
        = new Dictionary<HeroClass, List<CardData>>();
    private static bool isLoaded = false;

    /// <summary>
    /// Fatia do corpo do baralho reservada a cada papel.
    ///
    /// Os números vêm do que a montagem antiga produzia por acaso: com duas
    /// comuns por classe, uma de ataque e uma de defesa, o baralho saía perto de
    /// metade ataque. Foram mantidos para que dobrar o acervo de cartas mude a
    /// variedade do baralho sem mudar o que ele faz por turno.
    ///
    /// <b>São a régua da letalidade.</b> Se o smoke test sair fora de 0,33–0,67
    /// mortes por jornada, é aqui que se mexe: cada ponto de <see cref="FatiaAtaque"/>
    /// encurta o combate, cada ponto de <see cref="FatiaDefesa"/> o alonga. Mexer
    /// no valor das cartas para o mesmo fim é o caminho da inflação.
    /// </summary>
    const float FatiaAtaque = 0.45f;
    const float FatiaDefesa = 0.25f;
    const float FatiaSuporte = 0.20f;
    const float FatiaUtilidade = 0.10f;

    public static void LoadAllCards()
    {
        if (isLoaded) return;

        CardData[] allCards = Resources.LoadAll<CardData>("Cards");

        foreach (HeroClass heroClass in System.Enum.GetValues(typeof(HeroClass)))
            cardCache[heroClass] = new List<CardData>();

        foreach (var card in allCards)
        {
            if (card != null && cardCache.ContainsKey(card.requiredClass))
                cardCache[card.requiredClass].Add(card);
        }

        isLoaded = true;
    }

    /// <summary>
    /// Esquece o acervo em memória. Só serve ao Editor: sem isto, uma carta
    /// criada pelo Card Creator não aparece em baralho nenhum até reiniciar o
    /// Unity, e a ferramenta parece não ter funcionado.
    /// </summary>
    public static void Recarregar()
    {
        cardCache.Clear();
        isLoaded = false;
    }

    public static DeckData GenerateDeckForHero(HeroData hero)
    {
        DeckData deck = ScriptableObject.CreateInstance<DeckData>();
        deck.deckName = $"Deck de {hero.heroName}";
        deck.owner = hero;
        deck.cards = new List<CardData>();

        List<CardData> pool = GetCardsForClass(hero.heroClass);
        if (pool.Count == 0)
        {
            Debug.LogWarning($"Nenhuma carta encontrada para {hero.heroClass}. Criando deck padrão.");
            return CreateDefaultDeck(hero);
        }

        int deckSize = Mathf.Clamp(8 + hero.level, 8, 12);

        var copias = new Dictionary<CardData, int>();

        // Devolve false quando a carta não coube — teto de cópias ou baralho cheio.
        bool Por(CardData card, bool ignorarTeto = false)
        {
            if (card == null || deck.cards.Count >= deckSize) return false;

            copias.TryGetValue(card, out int quantas);
            if (!ignorarTeto && quantas >= DeckManager.MaxCopiasPorCarta) return false;

            deck.cards.Add(card);
            copias[card] = quantas + 1;
            return true;
        }

        // ── 1. Cartas de recompensa ────────────────────────────────────────────
        // Mesma progressão de antes, de propósito: é o que o jogador ganha por
        // subir de nível, e mexer nela mexeria no ritmo da guilda inteira.
        if (hero.level >= 5) Por(Sortear(pool, CardRarity.Legendary));
        if (hero.level >= 3) Por(Sortear(pool, CardRarity.Epic));

        int raras = 1 + hero.level / 3;
        foreach (var rara in Embaralhado(pool.Where(c => c.rarity == CardRarity.Rare)).Take(raras))
            Por(rara);

        // ── 2. Corpo do baralho, por papel ─────────────────────────────────────
        int corpo = deckSize - deck.cards.Count;
        int[] vagas = Repartir(corpo);

        Preencher(pool, hero, CardRole.Ataque, vagas[0], Por);
        Preencher(pool, hero, CardRole.Defesa, vagas[1], Por);
        Preencher(pool, hero, CardRole.Suporte, vagas[2], Por);
        Preencher(pool, hero, CardRole.Utilidade, vagas[3], Por);

        // ── 3. O que sobrou ────────────────────────────────────────────────────
        // Papel que a classe não tem devolve as vagas. Elas não podem evaporar:
        // um baralho abaixo do alvo é uma mão a menos na jornada, e o smoke test
        // cobra o tamanho.
        Completar(deck, pool, hero, deckSize, Por);

        return deck;
    }

    /// <summary>
    /// Reparte as vagas do corpo do baralho entre os quatro papéis, na ordem
    /// Ataque, Defesa, Suporte, Utilidade.
    ///
    /// Arredondar cada fatia por conta própria perdia vagas: num corpo de oito,
    /// 45/25/20 já somava oito e a Utilidade ficava com zero — a carta de compra
    /// e a de energia nunca entravam em baralho nenhum. Aqui as sobras vão para
    /// quem tem a maior parte fracionária, e a soma fecha sempre.
    /// </summary>
    static int[] Repartir(int corpo)
    {
        if (corpo <= 0) return new[] { 0, 0, 0, 0 };

        float[] exatos =
        {
            corpo * FatiaAtaque, corpo * FatiaDefesa,
            corpo * FatiaSuporte, corpo * FatiaUtilidade
        };

        var vagas = new int[4];
        int somadas = 0;

        for (int i = 0; i < 4; i++)
        {
            vagas[i] = Mathf.FloorToInt(exatos[i]);
            somadas += vagas[i];
        }

        // Maior sobra primeiro; empate resolve pela ordem dos papéis.
        var ordem = Enumerable.Range(0, 4)
                              .OrderByDescending(i => exatos[i] - Mathf.FloorToInt(exatos[i]))
                              .ThenBy(i => i)
                              .ToList();

        for (int k = 0; somadas < corpo; k++, somadas++)
            vagas[ordem[k % 4]]++;

        return vagas;
    }

    /// <summary>
    /// Enche as vagas de um papel em rodízio entre as cartas que o exercem.
    ///
    /// Rodízio, e não sorteio: sorteio com poucas cartas volta a concentrar o
    /// baralho numa só, que é exatamente o defeito que esta classe existe para
    /// corrigir.
    /// </summary>
    static void Preencher(List<CardData> pool, HeroData hero,
                          CardRole papel, int vagas, System.Func<CardData, bool, bool> Por)
    {
        if (vagas <= 0) return;

        var candidatos = Embaralhado(Base(pool, hero).Where(c => CardRoleUtil.Of(c) == papel)).ToList();
        if (candidatos.Count == 0) return;

        int postas = 0;
        while (postas < vagas)
        {
            bool algumaEntrou = false;

            foreach (var carta in candidatos)
            {
                if (postas >= vagas) break;
                if (!Por(carta, false)) continue;

                postas++;
                algumaEntrou = true;
            }

            // Todas no teto de cópias (ou baralho cheio): insistir seria laço eterno.
            if (!algumaEntrou) return;
        }
    }

    /// <summary>Fecha o tamanho do baralho com o que ainda couber, papel nenhum em especial.</summary>
    static void Completar(DeckData deck, List<CardData> pool, HeroData hero,
                          int deckSize, System.Func<CardData, bool, bool> Por)
    {
        var sobras = Embaralhado(Base(pool, hero)).ToList();
        if (sobras.Count == 0) sobras = Embaralhado(pool).ToList();
        if (sobras.Count == 0) return;

        while (deck.cards.Count < deckSize)
        {
            bool algumaEntrou = false;

            foreach (var carta in sobras)
            {
                if (deck.cards.Count >= deckSize) break;
                if (Por(carta, false)) algumaEntrou = true;
            }

            if (algumaEntrou) continue;

            // Acervo pequeno demais para o tamanho do baralho: melhor estourar o
            // teto de cópias do que entregar um herói com meia mão. Acontece com
            // classe que ainda tem poucas cartas escritas.
            if (!Por(sobras[Random.Range(0, sobras.Count)], true)) return;
        }
    }

    /// <summary>
    /// O corpo do baralho é feito de cartas comuns: são elas que o jogador tem em
    /// quantidade. Se a classe não tiver nenhuma, cai para o que o nível já abriu
    /// — sem isso, uma classe só de cartas raras sairia com baralho vazio.
    /// </summary>
    static IEnumerable<CardData> Base(List<CardData> pool, HeroData hero)
    {
        var comuns = pool.Where(c => c.rarity == CardRarity.Common).ToList();
        if (comuns.Count > 0) return comuns;

        return pool.Where(c => RaridadeLiberada(c.rarity, hero.level));
    }

    static bool RaridadeLiberada(CardRarity rarity, int level)
    {
        switch (rarity)
        {
            case CardRarity.Epic: return level >= 3;
            case CardRarity.Legendary: return level >= 5;
            default: return true;
        }
    }

    static CardData Sortear(List<CardData> pool, CardRarity rarity)
    {
        var candidatos = pool.Where(c => c.rarity == rarity).ToList();
        return candidatos.Count == 0 ? null : candidatos[Random.Range(0, candidatos.Count)];
    }

    static IEnumerable<CardData> Embaralhado(IEnumerable<CardData> cartas)
    {
        var lista = cartas.Where(c => c != null).ToList();

        for (int i = lista.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (lista[i], lista[j]) = (lista[j], lista[i]);
        }

        return lista;
    }

    /// <summary>
    /// As cartas que este herói pode levar.
    ///
    /// <see cref="HeroClass.Bard"/> já valia como curinga no editor de baralhos e
    /// na biblioteca, e só o gerador ignorava: uma carta neutra apareceria à venda
    /// e jamais num baralho inicial. Hoje não existe nenhuma — a costura fica
    /// pronta para quando o autor decidir que existem cartas de qualquer classe.
    /// </summary>
    private static List<CardData> GetCardsForClass(HeroClass heroClass)
    {
        LoadAllCards();

        var pool = new List<CardData>();

        if (cardCache.TryGetValue(heroClass, out var proprias))
            pool.AddRange(proprias);

        if (heroClass != HeroClass.Bard && cardCache.TryGetValue(HeroClass.Bard, out var curingas))
            pool.AddRange(curingas);

        return pool;
    }

    private static DeckData CreateDefaultDeck(HeroData hero)
    {
        DeckData deck = ScriptableObject.CreateInstance<DeckData>();
        deck.deckName = $"Deck de {hero.heroName}";
        deck.owner = hero;
        deck.cards = new List<CardData>();

        // Cria cartas padrão
        for (int i = 0; i < 8; i++)
        {
            CardData defaultCard = ScriptableObject.CreateInstance<CardData>();
            defaultCard.cardName = GetDefaultCardName(hero.heroClass);
            defaultCard.cardDescription = $"Ataque básico de {GetClassName(hero.heroClass)}";
            defaultCard.requiredClass = hero.heroClass;
            defaultCard.rarity = CardRarity.Common;
            defaultCard.energyCost = 2;
            defaultCard.combatDamage = 8;
            defaultCard.journeyEffect = JourneyEffectType.RemoveObstacle;
            defaultCard.combatEffect = CombatEffectType.Damage;
            defaultCard.journeyEffectDescription = "Abre caminho na jornada";
            defaultCard.combatEffectDescription = $"{defaultCard.combatDamage} de dano";

            deck.cards.Add(defaultCard);
        }

        return deck;
    }

    private static string GetDefaultCardName(HeroClass heroClass)
    {
        switch (heroClass)
        {
            case HeroClass.Warrior: return "Ataque de Espada";
            case HeroClass.Mage: return "Centelha Mágica";
            case HeroClass.Healer: return "Toque Curativo";
            case HeroClass.Hunter: return "Flecha Básica";
            default: return "Ataque";
        }
    }

    private static string GetClassName(HeroClass heroClass)
    {
        switch (heroClass)
        {
            case HeroClass.Warrior: return "Guerreiro";
            case HeroClass.Mage: return "Mago";
            case HeroClass.Healer: return "Curandeiro";
            case HeroClass.Hunter: return "Caçador";
            default: return "Herói";
        }
    }
}
