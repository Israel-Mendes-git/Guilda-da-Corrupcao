using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

public class DeckManager : MonoBehaviour
{
    private static DeckManager instance;

    /// <summary>
    /// Resolvido sob demanda: o componente mora no painel do gerenciador, que
    /// nasce desativado, então o <c>Awake</c> só rodaria depois de alguém abrir
    /// a tela — e quem abre precisa do singleton antes disso.
    /// </summary>
    public static DeckManager Instance
    {
        get
        {
            if (instance != null) return instance;

            foreach (var candidate in Resources.FindObjectsOfTypeAll<DeckManager>())
            {
                if (candidate == null || candidate.gameObject.scene.rootCount == 0) continue;
                instance = candidate;
                break;
            }

            return instance;
        }
    }

    [Header("UI References")]
    public GameObject deckManagerPanel;
    public TMP_Text heroNameText;
    public Button closeButton;

    [Header("Hero Selection")]
    public Transform heroSelectionContainer;
    public GameObject heroSelectionButtonPrefab;

    [Header("Deck Display")]
    public Transform currentDeckContainer;
    public GameObject cardPrefab;
    public TMP_Text deckStatsText;

    [Header("Collection Display")]
    public Transform collectionContainer;
    public TMP_Text collectionStatsText;

    [Header("Buttons")]
    public Button saveButton;
    public Button resetButton;

    /// <summary>
    /// Teto de cópias da mesma carta no baralho. Segura o deck de virar quatro
    /// vezes a carta mais forte, que é o que um editor sem limite convida a fazer.
    /// </summary>
    public const int MaxCopiasPorCarta = 4;

    private HeroData currentHero;
    private DeckData currentDeck;
    private List<CardData> allOwnedCards = new List<CardData>();
    private bool buttonsWired;

    int CopiasNoDeck(CardData card)
    {
        return currentDeck == null || currentDeck.cards == null
            ? 0
            : currentDeck.cards.Count(c => c == card);
    }

    void Awake()
    {
        WireButtons();
    }

    /// <summary>
    /// Liga os botões uma vez. Ficava no <c>Start</c>, que só roda no frame
    /// seguinte à ativação do painel — abrir e clicar em Fechar no mesmo frame
    /// não fazia nada.
    /// </summary>
    void WireButtons()
    {
        if (buttonsWired) return;
        buttonsWired = true;

        if (closeButton != null)
            closeButton.onClick.AddListener(() => UIManager.Instance?.CloseDeckManager());

        if (saveButton != null)
            saveButton.onClick.AddListener(SaveCurrentDeck);

        if (resetButton != null)
            resetButton.onClick.AddListener(ResetToDefaultDeck);
    }

    /// <summary>
    /// Preenche a tela. Precisa ser chamada por quem abre o painel: ativar o
    /// GameObject sozinho deixava a lista de heróis, o deck e a coleção todos
    /// vazios — era o estado em que a tela vivia, já que o UIManager só ativava.
    /// </summary>
    public void OpenDeckManager()
    {
        WireButtons();

        if (deckManagerPanel != null) deckManagerPanel.SetActive(true);

        RefreshHeroList();
        UpdateCollectionStats();

        // Abre já mostrando alguém: uma tela de deck sem deck não diz ao jogador
        // que ele precisa escolher um herói primeiro.
        if (currentHero == null || currentHero.isDead || GuildManager.Instance == null
            || !GuildManager.Instance.roster.Contains(currentHero))
        {
            HeroData primeiro = GuildManager.Instance != null
                ? GuildManager.Instance.roster.FirstOrDefault(h => h != null && !h.isDead)
                : null;

            if (primeiro != null) SelectHero(primeiro);
        }
        else
        {
            SelectHero(currentHero);
        }
    }

    void RefreshHeroList()
    {
        if (heroSelectionContainer == null) return;

        foreach (Transform child in heroSelectionContainer)
            Destroy(child.gameObject);

        if (GuildManager.Instance == null) return;

        foreach (var hero in GuildManager.Instance.roster)
        {
            if (hero.isDead) continue;

            GameObject btnObj = Instantiate(heroSelectionButtonPrefab, heroSelectionContainer);
            TMP_Text btnText = btnObj.GetComponentInChildren<TMP_Text>();
            if (btnText != null) btnText.text = $"{hero.heroName}\n{GetClassName(hero.heroClass)} Nv.{hero.level}";

            Button btn = btnObj.GetComponent<Button>();
            btn.onClick.AddListener(() => SelectHero(hero));
        }
    }

    void SelectHero(HeroData hero)
    {
        currentHero = hero;

        // Cópia de trabalho: as edições só valem para a jornada depois de Salvar.
        currentDeck = DeckRepository.GetDeck(hero).Clone();

        // Carrega cole��o de cartas do her�i
        LoadHeroCollection(hero);

        // Atualiza UI
        if (heroNameText != null)
            heroNameText.text = $"{hero.heroName} - {GetClassName(hero.heroClass)} Nv.{hero.level}";

        RefreshDeckDisplay();
        RefreshCollectionDisplay();
        UpdateDeckStats();
    }

    void LoadHeroCollection(HeroData hero)
    {
        allOwnedCards.Clear();

        // Carrega todas as cartas dispon�veis
        CardData[] allCards = Resources.LoadAll<CardData>("Cards");

        // Filtra cartas da classe do her�i OU cartas curinga que ele pode usar
        foreach (var card in allCards)
        {
            if (card.requiredClass == hero.heroClass || card.requiredClass == HeroClass.Bard) // Bard = curinga
            {
                allOwnedCards.Add(card);
            }
        }

        // Adiciona cartas que j� est�o no deck (mesmo se n�o estiverem na cole��o padr�o)
        foreach (var card in currentDeck.cards)
        {
            if (!allOwnedCards.Contains(card))
                allOwnedCards.Add(card);
        }
    }

    /// <summary>
    /// Garante que os dois containers aceitem cartas soltas em cima deles.
    /// Feito em código para não depender de alguém lembrar de adicionar o
    /// componente no Inspector.
    /// </summary>
    void EnsureDropZones()
    {
        AttachZone(currentDeckContainer, DeckDropZone.Zone.Deck);
        AttachZone(collectionContainer, DeckDropZone.Zone.Collection);
    }

    static void AttachZone(Transform container, DeckDropZone.Zone zone)
    {
        if (container == null) return;

        var z = container.GetComponent<DeckDropZone>();
        if (z == null) z = container.gameObject.AddComponent<DeckDropZone>();
        z.zone = zone;

        // Sem um Graphic o raycast não alcança o container e o drop se perde.
        var img = container.GetComponent<UnityEngine.UI.Image>();
        if (img == null)
        {
            img = container.gameObject.AddComponent<UnityEngine.UI.Image>();
            img.color = new Color(1f, 1f, 1f, 0.01f);
        }
        img.raycastTarget = true;
    }

    void RefreshDeckDisplay()
    {
        if (currentDeckContainer == null) return;

        EnsureDropZones();

        foreach (Transform child in currentDeckContainer)
            Destroy(child.gameObject);

        foreach (var card in currentDeck.cards)
        {
            GameObject cardObj = Instantiate(cardPrefab, currentDeckContainer);
            CardInDeck cardScript = cardObj.GetComponent<CardInDeck>();
            if (cardScript != null)
            {
                cardScript.Initialize(card, true);
                // Adiciona listener para remover do deck
                Button btn = cardObj.GetComponent<Button>();
                if (btn != null)
                    btn.onClick.AddListener(() => RemoveCardFromDeck(card));
            }
        }

        UpdateDeckStats();
    }

    void RefreshCollectionDisplay()
    {
        if (collectionContainer == null) return;

        foreach (Transform child in collectionContainer)
            Destroy(child.gameObject);

        // Mostra o acervo inteiro da classe, inclusive o que já está no deck.
        // Escondendo as repetidas, a coleção ficava SEMPRE vazia: só existem
        // quatro cartas por classe e o deck gerado usa as quatro, com cópias.
        // Quem removesse uma carta não tinha como recolocá-la.
        foreach (var card in allOwnedCards)
        {
            GameObject cardObj = Instantiate(cardPrefab, collectionContainer);
            CardInDeck cardScript = cardObj.GetComponent<CardInDeck>();
            if (cardScript != null)
            {
                cardScript.Initialize(card, false);
                // Adiciona listener para adicionar ao deck
                Button btn = cardObj.GetComponent<Button>();
                if (btn != null)
                    btn.onClick.AddListener(() => AddCardToDeck(card));
            }
        }

        UpdateCollectionStats();
    }

    void AddCardToDeck(CardData card)
    {
        TryAddCardToDeck(card);
    }

    void RemoveCardFromDeck(CardData card)
    {
        TryRemoveCardFromDeck(card);
    }

    /// <summary>
    /// Versão pública usada pelo arrasto: devolve se a carta realmente entrou,
    /// para que a zona de drop saiba se pode consumir o gesto.
    /// </summary>
    public bool TryAddCardToDeck(CardData card)
    {
        if (card == null || currentDeck == null) return false;

        if (currentDeck.cards.Count >= currentDeck.maxDeckSize)
        {
            UIManager.Instance?.ShowMessage($"Deck cheio! Máximo {currentDeck.maxDeckSize} cartas.", 2f);
            return false;
        }

        // Cópias são permitidas: é assim que o DeckGenerator monta os baralhos,
        // e proibi-las aqui tornava a remoção de uma carta irreversível.
        if (CopiasNoDeck(card) >= MaxCopiasPorCarta)
        {
            UIManager.Instance?.ShowMessage(
                $"No máximo {MaxCopiasPorCarta} cópias de {card.cardName}.", 2f);
            return false;
        }

        currentDeck.cards.Add(card);
        RefreshDeckDisplay();
        RefreshCollectionDisplay();
        UIManager.Instance?.ShowMessage($"Carta {card.cardName} adicionada ao deck!", 2f);
        return true;
    }

    /// <summary>Contrapartida de <see cref="TryAddCardToDeck"/>.</summary>
    public bool TryRemoveCardFromDeck(CardData card)
    {
        if (card == null || currentDeck == null) return false;
        if (!currentDeck.cards.Contains(card)) return false;

        currentDeck.cards.Remove(card);
        RefreshDeckDisplay();
        RefreshCollectionDisplay();
        UIManager.Instance?.ShowMessage($"Carta {card.cardName} removida do deck!", 2f);
        return true;
    }

    void UpdateDeckStats()
    {
        if (deckStatsText == null) return;

        int cardCount = currentDeck.cards.Count;
        int avgCost = currentDeck.cards.Count > 0 ?
            (int)currentDeck.cards.Average(c => c.energyCost) : 0;

        deckStatsText.text = $"{cardCount}/{currentDeck.maxDeckSize} cartas | Custo m�dio: {avgCost}";
    }

    void UpdateCollectionStats()
    {
        if (collectionStatsText == null) return;
        collectionStatsText.text = $"Total: {allOwnedCards.Count} cartas";
    }

    public void SaveCurrentDeck()
    {
        if (currentHero == null)
        {
            Debug.LogError("DeckManager: Nenhum her�i selecionado para salvar!");
            UIManager.Instance?.ShowMessage("Selecione um her�i primeiro!", 2f);
            return;
        }

        DeckRepository.SetDeck(currentHero, currentDeck);

        UIManager.Instance?.ShowMessage($"Deck de {currentHero.heroName} salvo!", 2f);
    }

    void ResetToDefaultDeck()
    {
        if (currentHero == null) return;

        // Reseta apenas a cópia de trabalho; só vale de fato ao Salvar.
        currentDeck = DeckGenerator.GenerateDeckForHero(currentHero);

        RefreshDeckDisplay();
        RefreshCollectionDisplay();

        UIManager.Instance?.ShowMessage($"Deck de {currentHero.heroName} restaurado. Salve para confirmar.", 2f);
    }

    string GetClassName(HeroClass heroClass)
    {
        switch (heroClass)
        {
            case HeroClass.Warrior: return "Guerreiro";
            case HeroClass.Mage: return "Mago";
            case HeroClass.Healer: return "Curandeiro";
            case HeroClass.Rogue: return "Ladino";
            case HeroClass.Bard: return "Bardo";
            case HeroClass.Hunter: return "Ca�ador";
            default: return "Her�i";
        }
    }
}