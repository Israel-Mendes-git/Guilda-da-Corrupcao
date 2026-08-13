using System.Collections.Generic;
using UnityEngine;

public static class DeckGenerator
{
    private static Dictionary<HeroClass, List<CardData>> cardCache = new Dictionary<HeroClass, List<CardData>>();
    private static bool isLoaded = false;

    public static void LoadAllCards()
    {
        if (isLoaded) return;

        CardData[] allCards = Resources.LoadAll<CardData>("Cards");

        foreach (HeroClass heroClass in System.Enum.GetValues(typeof(HeroClass)))
        {
            cardCache[heroClass] = new List<CardData>();
        }

        foreach (var card in allCards)
        {
            if (cardCache.ContainsKey(card.requiredClass))
                cardCache[card.requiredClass].Add(card);
        }

        isLoaded = true;
        Debug.Log($"Carregadas {allCards.Length} cartas");

        // Log para debug - mostra cartas carregadas por classe
        foreach (var kvp in cardCache)
        {
            Debug.Log($"Classe {kvp.Key}: {kvp.Value.Count} cartas");
            foreach (var card in kvp.Value)
            {
                Debug.Log($"  - {card.cardName} (Raridade: {card.rarity})");
            }
        }
    }

    public static DeckData GenerateDeckForHero(HeroData hero)
    {
        LoadAllCards();

        DeckData deck = ScriptableObject.CreateInstance<DeckData>();
        deck.deckName = $"Deck de {hero.heroName}";
        deck.owner = hero;
        deck.cards = new List<CardData>();

        List<CardData> availableCards = GetCardsForClass(hero.heroClass);

        Debug.Log($"Gerando deck para {hero.heroName} (Classe: {hero.heroClass}). Cartas disponíveis: {availableCards.Count}");

        if (availableCards.Count == 0)
        {
            Debug.LogWarning($"Nenhuma carta encontrada para {hero.heroClass}. Criando deck padrão.");
            return CreateDefaultDeck(hero);
        }

        int deckSize = Mathf.Clamp(8 + hero.level, 8, 12);

        // Separa cartas por raridade
        List<CardData> commonCards = availableCards.FindAll(c => c.rarity == CardRarity.Common);
        List<CardData> rareCards = availableCards.FindAll(c => c.rarity == CardRarity.Rare);
        List<CardData> epicCards = availableCards.FindAll(c => c.rarity == CardRarity.Epic);
        List<CardData> legendaryCards = availableCards.FindAll(c => c.rarity == CardRarity.Legendary);

        Debug.Log($"Cartas disponíveis - Common: {commonCards.Count}, Rare: {rareCards.Count}, Epic: {epicCards.Count}, Legendary: {legendaryCards.Count}");

        // Adiciona cartas comuns
        int commonCount = Mathf.Min(deckSize - 2, commonCards.Count);
        for (int i = 0; i < commonCount && commonCards.Count > 0; i++)
        {
            CardData card = commonCards[Random.Range(0, commonCards.Count)];
            deck.cards.Add(card);
            Debug.Log($"Adicionou carta comum: {card.cardName}");
        }

        // Adiciona cartas raras
        int rareCount = Mathf.Min(1 + (hero.level / 3), rareCards.Count);
        for (int i = 0; i < rareCount && rareCards.Count > 0; i++)
        {
            CardData card = rareCards[Random.Range(0, rareCards.Count)];
            if (!deck.cards.Contains(card))
            {
                deck.cards.Add(card);
                Debug.Log($"Adicionou carta rara: {card.cardName}");
            }
        }

        // Adiciona carta épica
        if (hero.level >= 3 && epicCards.Count > 0)
        {
            CardData epicCard = epicCards[Random.Range(0, epicCards.Count)];
            deck.cards.Add(epicCard);
            Debug.Log($"Adicionou carta épica: {epicCard.cardName}");
        }

        // Adiciona carta lendária
        if (hero.level >= 5 && legendaryCards.Count > 0)
        {
            CardData legendaryCard = legendaryCards[Random.Range(0, legendaryCards.Count)];
            deck.cards.Add(legendaryCard);
            Debug.Log($"Adicionou carta lendária: {legendaryCard.cardName}");
        }

        // Garante tamanho mínimo
        while (deck.cards.Count < deckSize && commonCards.Count > 0)
        {
            deck.cards.Add(commonCards[Random.Range(0, commonCards.Count)]);
        }

        // Garante tamanho máximo
        while (deck.cards.Count > 12)
            deck.cards.RemoveAt(deck.cards.Count - 1);

        Debug.Log($"Deck final para {hero.heroName}: {deck.cards.Count} cartas");

        return deck;
    }

    private static List<CardData> GetCardsForClass(HeroClass heroClass)
    {
        if (cardCache.ContainsKey(heroClass))
            return cardCache[heroClass];
        return new List<CardData>();
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
            defaultCard.journeyEffect = JourneyEffectType.None;
            defaultCard.combatEffect = CombatEffectType.Damage;
            defaultCard.journeyEffectDescription = "Ação básica na jornada";
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