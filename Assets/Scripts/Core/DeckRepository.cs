using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dono único dos decks de cada herói.
/// Antes existiam três cópias independentes (QuestSelectionUI, DeckManager e a criada
/// pela Taverna): editar o deck não afetava a jornada, e o "salvar" escrevia em
/// PlayerPrefs sem que nada jamais lesse de volta.
///
/// <b>Onde os decks moram agora:</b> dentro do save da partida, junto do herói
/// dono de cada um — é lá que eles fazem sentido, porque um baralho sem o herói
/// não é nada. O PlayerPrefs continua sendo <i>lido</i> como último recurso, para
/// que quem já jogava antes do sistema de save não perca o que montou; escrito,
/// não é mais. A consequência a saber: um deck editado só fica gravado quando a
/// partida for salva (autosave de fim de jornada, ou o botão da pausa).
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
        {
            // O deck vindo do save chega sem dono: os baralhos são reconstruídos
            // antes dos heróis, porque é o herói que pergunta pelo seu.
            if (cached.owner == null) cached.owner = hero;
            return cached;
        }

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

    /// <summary>Substitui o deck do herói. Fica gravado quando a partida for salva.</summary>
    public static void SetDeck(HeroData hero, DeckData deck)
    {
        if (hero == null || deck == null) return;

        decks[hero.GetId()] = deck;
    }

    /// <summary>
    /// Mantido pela compatibilidade com quem já chamava. Não escreve mais em
    /// disco por conta própria: quem grava é o save da partida.
    /// </summary>
    public static void Save(HeroData hero)
    {
    }

    /// <summary>Descarta o deck de um herói morto ou dispensado.</summary>
    public static void Remove(HeroData hero)
    {
        if (hero == null) return;

        string id = hero.GetId();
        decks.Remove(id);
        PlayerPrefs.DeleteKey(PrefsPrefix + id);
    }

    /// <summary>
    /// Esquece todos os decks. Chamado ao trocar de partida — sem isto, a guilda
    /// nova herdaria em memória os baralhos da anterior, e como o índice é por
    /// heroId eles ficariam ali para sempre sem nunca voltar a ser usados.
    /// </summary>
    public static void Limpar()
    {
        decks.Clear();
    }

    /// <summary>Regenera o deck a partir das regras padrão, descartando as edições.</summary>
    public static DeckData ResetToDefault(HeroData hero)
    {
        if (hero == null) return null;

        DeckData fresh = DeckGenerator.GenerateDeckForHero(hero);
        SetDeck(hero, fresh);
        return fresh;
    }

    #region Persistência no save da partida

    /// <summary>
    /// Os decks dos heróis informados, prontos para o arquivo.
    ///
    /// Recebe a lista de heróis já convertida em vez de exportar o dicionário
    /// inteiro: assim um deck de herói morto há dez ciclos não fica engordando
    /// todo save daqui em diante.
    /// </summary>
    public static List<DeckSave> Exportar(List<HeroSave> herois)
    {
        var saida = new List<DeckSave>();
        if (herois == null) return saida;

        foreach (var heroi in herois)
        {
            if (heroi == null || string.IsNullOrEmpty(heroi.heroId)) continue;
            if (!decks.TryGetValue(heroi.heroId, out DeckData deck) || deck == null) continue;

            var registro = new DeckSave { heroId = heroi.heroId };

            foreach (var carta in deck.cards)
                if (carta != null) registro.cardNames.Add(carta.name);

            saida.Add(registro);
        }

        return saida;
    }

    /// <summary>Repõe os decks vindos de um save, descartando o que estava em memória.</summary>
    public static void Importar(List<DeckSave> salvos)
    {
        decks.Clear();
        if (salvos == null) return;

        EnsureCardIndex();

        foreach (var registro in salvos)
        {
            if (registro == null || string.IsNullOrEmpty(registro.heroId)) continue;

            DeckData deck = ScriptableObject.CreateInstance<DeckData>();
            deck.deckName = "Deck salvo";
            deck.cards = new List<CardData>();

            foreach (string nome in registro.cardNames)
                if (cardsByName.TryGetValue(nome, out CardData carta))
                    deck.cards.Add(carta);

            // Um deck que chegou vazio porque as cartas sumiram do projeto não
            // vale a pena guardar: o GetDeck gera um novo e o herói volta jogável.
            if (deck.cards.Count > 0) decks[registro.heroId] = deck;
        }
    }

    #endregion

    #region Leitura do formato antigo (PlayerPrefs)

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

    /// <summary>
    /// O formato antigo, só de leitura. Quem preenche é o JsonUtility, por
    /// reflexão — daí o valor inicial, que existe para o compilador não avisar
    /// de um campo que "nunca é atribuído".
    /// </summary>
    [System.Serializable]
    private class SerializableDeck
    {
        public string[] cardNames = new string[0];
    }

    #endregion
}
