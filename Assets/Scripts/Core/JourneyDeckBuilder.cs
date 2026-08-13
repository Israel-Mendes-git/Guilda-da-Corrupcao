using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Monta o baralho que vai para a jornada.
///
/// A regra do jogo: um herói principal define o baralho base e os companheiros
/// entram como cartas de apoio dentro dele. É o que faz a party importar no
/// deckbuilding — levar um mago muda as cartas que você compra, mesmo jogando
/// com um guerreiro.
/// </summary>
public static class JourneyDeckBuilder
{
    /// <summary>Quantas cartas cada companheiro empresta ao baralho.</summary>
    public const int DefaultSupportCards = 3;

    public class Result
    {
        public DeckData deck;

        /// <summary>Uma linha por herói, para a tela de preparação mostrar a composição.</summary>
        public readonly List<string> breakdown = new List<string>();

        /// <summary>
        /// Quem trouxe cada carta. O combate consulta isto para saber se o dono
        /// está bem posicionado na formação.
        /// </summary>
        public readonly CardOwnership ownership = new CardOwnership();
    }

    public static Result Build(HeroData main, IEnumerable<HeroData> party, int supportCards = DefaultSupportCards)
    {
        var result = new Result();

        if (main == null)
        {
            Debug.LogError("JourneyDeckBuilder: herói principal nulo.");
            result.deck = ScriptableObject.CreateInstance<DeckData>();
            result.deck.cards = new List<CardData>();
            return result;
        }

        DeckData baseDeck = DeckRepository.GetDeck(main);

        // Clone: a jornada não deve alterar o baralho guardado do herói.
        DeckData journeyDeck = baseDeck != null
            ? baseDeck.Clone()
            : ScriptableObject.CreateInstance<DeckData>();

        if (journeyDeck.cards == null)
            journeyDeck.cards = new List<CardData>();

        journeyDeck.deckName = $"Jornada de {main.heroName}";
        journeyDeck.owner = main;

        result.breakdown.Add($"⭐ {main.heroName} — {journeyDeck.cards.Count} cartas (base)");
        result.ownership.RegisterAll(journeyDeck.cards, main);

        foreach (var support in party ?? Enumerable.Empty<HeroData>())
        {
            if (support == null || support == main || support.isDead) continue;

            List<CardData> contribution = TakeContribution(support, supportCards);
            if (contribution.Count == 0)
            {
                result.breakdown.Add($"   {support.heroName} — sem cartas a emprestar");
                continue;
            }

            // Direto na lista: AddCard recusa repetidas, e aqui a repetição é
            // legítima — dois heróis da mesma classe emprestam a mesma carta.
            journeyDeck.cards.AddRange(contribution);
            result.ownership.RegisterAll(contribution, support);
            result.breakdown.Add($"   {support.heroName} — +{contribution.Count} cartas");
        }

        // O limite do baralho guardado não vale para o baralho da jornada.
        journeyDeck.maxDeckSize = Mathf.Max(journeyDeck.maxDeckSize, journeyDeck.cards.Count);

        result.deck = journeyDeck;
        return result;
    }

    /// <summary>
    /// Cartas que um companheiro empresta. Sorteia sem repetir dentro do próprio
    /// baralho, para o apoio não virar três cópias da mesma carta.
    /// </summary>
    public static List<CardData> TakeContribution(HeroData support, int count)
    {
        var picked = new List<CardData>();

        DeckData deck = DeckRepository.GetDeck(support);
        if (deck == null || deck.cards == null || deck.cards.Count == 0)
            return picked;

        var pool = deck.cards.Where(c => c != null).Distinct().ToList();

        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            int index = Random.Range(0, pool.Count);
            picked.Add(pool[index]);
            pool.RemoveAt(index);
        }

        return picked;
    }

    /// <summary>Prévia do tamanho final, sem montar o baralho.</summary>
    public static int PreviewSize(HeroData main, IEnumerable<HeroData> party, int supportCards = DefaultSupportCards)
    {
        if (main == null) return 0;

        DeckData baseDeck = DeckRepository.GetDeck(main);
        int total = baseDeck?.cards?.Count ?? 0;

        foreach (var support in party ?? Enumerable.Empty<HeroData>())
        {
            if (support == null || support == main || support.isDead) continue;

            DeckData d = DeckRepository.GetDeck(support);
            int disponiveis = d?.cards?.Where(c => c != null).Distinct().Count() ?? 0;
            total += Mathf.Min(supportCards, disponiveis);
        }

        return total;
    }
}
