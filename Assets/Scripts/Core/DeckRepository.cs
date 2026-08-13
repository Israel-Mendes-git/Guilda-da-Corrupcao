using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Dono único dos decks de cada herói.
/// Antes existiam três cópias independentes (QuestSelectionUI, DeckManager e a criada
/// pela Taverna): editar o deck não afetava a jornada, e o "salvar" escrevia em
/// PlayerPrefs sem que nada jamais lesse de volta.
/// </summary>
public static class DeckRepository
{
    private const string PrefsPrefix = "Deck_";

    private static readonly Dictionary<string, DeckData> decks = new Dictionary<string, DeckData>();
    private static Dictionary<string, CardData> cardsByName;

    /// <summary>Deck do herói: o salvo, o já carregado, ou um novo gerado na hora.</summary>
    public static DeckData GetDeck(HeroData hero)
    {
        if (hero == null) return null;

        string id = hero.GetId();

        if (decks.TryGetValue(id, out DeckData cached) && cached != null)
            return cached;

        DeckData loaded = LoadFromPrefs(hero);
        if (loaded != null && loaded.cards.Count > 0)
        {
            decks[id] = loaded;
            return loaded;
        }

        DeckData generated = DeckGenerator.GenerateDeckForHero(hero);
        decks[id] = generated;
        return generated;
    }

    /// <summary>Substitui o deck do herói e persiste.</summary>
    public static void SetDeck(HeroData hero, DeckData deck)
    {
        if (hero == null || deck == null) return;

        decks[hero.GetId()] = deck;
        SaveToPrefs(hero, deck);
    }

    public static void Save(HeroData hero)
    {
        if (hero == null) return;

        if (decks.TryGetValue(hero.GetId(), out DeckData deck))
            SaveToPrefs(hero, deck);
    }

    /// <summary>Descarta o deck de um herói morto ou dispensado.</summary>
    public static void Remove(HeroData hero)
    {
        if (hero == null) return;

        string id = hero.GetId();
        decks.Remove(id);
        PlayerPrefs.DeleteKey(PrefsPrefix + id);
    }

    /// <summary>Regenera o deck a partir das regras padrão, descartando as edições.</summary>
    public static DeckData ResetToDefault(HeroData hero)
    {
        if (hero == null) return null;

        DeckData fresh = DeckGenerator.GenerateDeckForHero(hero);
        SetDeck(hero, fresh);
        return fresh;
    }

    #region Persistência

    static void SaveToPrefs(HeroData hero, DeckData deck)
    {
        var payload = new SerializableDeck
        {
            cardNames = deck.cards.Where(c => c != null).Select(c => c.name).ToArray()
        };

        PlayerPrefs.SetString(PrefsPrefix + hero.GetId(), JsonUtility.ToJson(payload));
        PlayerPrefs.Save();
    }

    static DeckData LoadFromPrefs(HeroData hero)
    {
        string key = PrefsPrefix + hero.GetId();
        if (!PlayerPrefs.HasKey(key)) return null;

        SerializableDeck payload;
        try
        {
            payload = JsonUtility.FromJson<SerializableDeck>(PlayerPrefs.GetString(key));
        }
        catch
        {
            Debug.LogWarning($"DeckRepository: deck salvo de {hero.heroName} está corrompido; gerando um novo.");
            PlayerPrefs.DeleteKey(key);
            return null;
        }

        if (payload?.cardNames == null) return null;

        EnsureCardIndex();

        DeckData deck = ScriptableObject.CreateInstance<DeckData>();
        deck.deckName = $"Deck de {hero.heroName}";
        deck.owner = hero;
        deck.cards = new List<CardData>();

        foreach (string cardName in payload.cardNames)
        {
            if (cardsByName.TryGetValue(cardName, out CardData card))
                deck.cards.Add(card);
        }

        return deck;
    }

    /// <summary>Índice nome-do-asset → carta, para reconstruir decks salvos.</summary>
    static void EnsureCardIndex()
    {
        if (cardsByName != null) return;

        cardsByName = new Dictionary<string, CardData>();
        foreach (var card in Resources.LoadAll<CardData>("Cards"))
        {
            if (card != null && !cardsByName.ContainsKey(card.name))
                cardsByName[card.name] = card;
        }
    }

    [System.Serializable]
    private class SerializableDeck
    {
        public string[] cardNames;
    }

    #endregion
}
