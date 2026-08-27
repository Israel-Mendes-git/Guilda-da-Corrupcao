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

        UIUtil.ClearChildrenNow(heroSelectionContainer);

        if (GuildManager.Instance == null) return;

        foreach (var hero in GuildManager.Instance.roster)
        {
            if (hero == null || hero.isDead) continue;
            MontarFichaDeHeroi(hero);
        }
    }

    /// <summary>
    /// A ficha da fila, no molde das salas da guilda.
    ///
    /// Nasce aqui, e não de um prefab: o <c>heroSelectionButtonPrefab</c> era o
    /// botão branco padrão do Unity, e o código escrevia no primeiro
    /// <c>TMP_Text</c> que achasse — o segundo continuava com a palavra "Button"
    /// de fábrica, aparecendo por baixo do nome do herói em toda a fila.
    /// </summary>
    void MontarFichaDeHeroi(HeroData hero)
    {
        bool ativo = hero == currentHero;

        var row = new GameObject(hero.heroName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        row.transform.SetParent(heroSelectionContainer, false);
        row.GetComponent<Image>().color = ativo
            ? new Color(0.26f, 0.22f, 0.30f)
            : new Color(0.16f, 0.15f, 0.17f);

        var element = row.AddComponent<LayoutElement>();
        element.minHeight = 76;
        element.preferredHeight = 76;

        if (hero.portrait != null)
        {
            var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            portraitGo.transform.SetParent(row.transform, false);

            var portrait = portraitGo.GetComponent<Image>();
            portrait.sprite = hero.portrait;
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;

            var portraitRect = portraitGo.GetComponent<RectTransform>();
            portraitRect.anchorMin = new Vector2(0, 0.5f);
            portraitRect.anchorMax = new Vector2(0, 0.5f);
            portraitRect.sizeDelta = new Vector2(60, 60);
            portraitRect.anchoredPosition = new Vector2(42, 0);
        }

        DeckData deck = DeckRepository.GetDeck(hero);
        int cartas = deck != null && deck.cards != null ? deck.cards.Count : 0;
        int teto = deck != null ? deck.maxDeckSize : 0;

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(row.transform, false);

        var label = labelGo.AddComponent<TextMeshProUGUI>();
        label.fontSize = 18;
        label.alignment = TextAlignmentOptions.Left;
        label.raycastTarget = false;
        label.color = ativo ? new Color(0.98f, 0.92f, 0.72f) : new Color(0.88f, 0.86f, 0.82f);
        label.text = $"{PartyFormation.PreferenceIcon(hero.heroClass)} {hero.heroName}\n"
                   + $"<size=13>{GetClassName(hero.heroClass)} Nv.{hero.level}   ·   "
                   + $"baralho {cartas}/{teto}</size>";

        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(hero.portrait != null ? 80 : 14, 4);
        labelRect.offsetMax = new Vector2(-10, -4);

        HeroData capturado = hero;
        row.GetComponent<Button>().onClick.AddListener(() => SelectHero(capturado));
    }

    void SelectHero(HeroData hero)
    {
        currentHero = hero;

        // Cópia de trabalho: as edições só valem para a jornada depois de Salvar.
        currentDeck = DeckRepository.GetDeck(hero).Clone();

        // Carrega coleção de cartas do herói
        LoadHeroCollection(hero);

        // Atualiza UI
        if (heroNameText != null)
            heroNameText.text = $"{hero.heroName}  <size=18><color=#B8B0A0>"
                              + $"{GetClassName(hero.heroClass)} Nv.{hero.level}</color></size>";

        // A fila é remontada para o destaque acompanhar quem está em foco — sem
        // isso, a tela mostrava o baralho de um e a ficha acesa de outro.
        RefreshHeroList();

        RefreshDeckDisplay();
        RefreshCollectionDisplay();
        UpdateDeckStats();
    }

    void LoadHeroCollection(HeroData hero)
    {
        allOwnedCards.Clear();

        // Carrega todas as cartas disponíveis
        CardData[] allCards = Resources.LoadAll<CardData>("Cards");

        // Filtra cartas da classe do herói OU cartas curinga que ele pode usar
        foreach (var card in allCards)
        {
            if (card.requiredClass == hero.heroClass || card.requiredClass == HeroClass.Bard) // Bard = curinga
            {
                allOwnedCards.Add(card);
            }
        }

        // Adiciona cartas que já estão no deck (mesmo se não estiverem na coleção padrão)
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
            CardData capturada = card;
            GameObject cardObj = MontarCarta(card, currentDeckContainer, true);

            // Adiciona listener para remover do deck
            Button btn = cardObj.GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(() => RemoveCardFromDeck(capturada));
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
            CardData capturada = card;
            GameObject cardObj = MontarCarta(card, collectionContainer, false);

            // Adiciona listener para adicionar ao deck
            Button btn = cardObj.GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(() => AddCardToDeck(capturada));
        }

        UpdateCollectionStats();
    }

    /// <summary>
    /// O tamanho para o qual o <c>CardPrefab</c> foi desenhado. Os textos e a
    /// moldura dele são ancorados a esta medida.
    /// </summary>
    static readonly Vector2 TamanhoDaCarta = new Vector2(245, 345);

    /// <summary>Quanto a carta encolhe para caber na célula da grade.</summary>
    const float EscalaDaCarta = 0.69f;

    /// <summary>
    /// Uma carta na grade, dentro de um envelope.
    ///
    /// O <c>GridLayoutGroup</c> controla o retângulo do filho direto, e a carta
    /// tem 245×345 com tudo ancorado a esse tamanho: espremida numa célula de
    /// 170×240 ela saía com o nome cortado ("Brado de G"), a arte deformada e o
    /// rodapé fora do quadro. O envelope é quem obedece à grade; a carta vive
    /// dentro dele no tamanho certo e encolhe por <c>localScale</c>, que o layout
    /// não toca.
    /// </summary>
    GameObject MontarCarta(CardData card, Transform container, bool noDeck)
    {
        var slot = new GameObject(card.cardName, typeof(RectTransform));
        slot.transform.SetParent(container, false);

        GameObject cardObj = Instantiate(cardPrefab, slot.transform);

        var rt = (RectTransform)cardObj.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = TamanhoDaCarta;
        rt.localScale = Vector3.one * EscalaDaCarta;

        CardInDeck cardScript = GarantirCarta(cardObj);
        cardScript.Initialize(card, noDeck);

        // A descrição aqui traz as duas linhas — combate e estrada — e no corpo
        // do prefab elas não cabem: o texto saía cortado no meio da palavra
        // ("+25 de our"). O ajuste é no clone, não no prefab, que também serve ao
        // combate e à jornada, onde só uma das linhas aparece.
        if (cardScript.cardDescriptionText != null)
        {
            cardScript.cardDescriptionText.fontSize = 12;
            cardScript.cardDescriptionText.enableWordWrapping = true;
        }

        return cardObj;
    }

    /// <summary>
    /// Põe na carta recém-instanciada o componente que a preenche — e o liga aos
    /// textos do prefab.
    ///
    /// O <c>CardPrefab</c> nunca teve o <see cref="CardInDeck"/>: tem os textos,
    /// tem o Button, e mais nada. O código antigo pedia o componente e, ao não
    /// achar, **desistia em silêncio** — dentro de um <c>if (cardScript != null)</c>
    /// que também guardava o listener do clique. O resultado é a tela inteira de
    /// baralhos morta: cada carta aparecia escrita "New Text", sem custo, sem
    /// reagir ao clique e sem arrastar, e nenhum erro no console dizia por quê.
    ///
    /// Ligar por nome vale mais que arrumar o prefab à mão: o mesmo prefab serve
    /// à jornada e ao combate, que preenchem os textos por conta própria, e um
    /// componente de arrasto gravado nele iria junto para essas duas telas.
    /// </summary>
    static CardInDeck GarantirCarta(GameObject cardObj)
    {
        CardInDeck script = cardObj.GetComponent<CardInDeck>();
        if (script == null) script = cardObj.AddComponent<CardInDeck>();

        if (script.cardNameText == null)
            script.cardNameText = Achar<TMP_Text>(cardObj, "CardName");
        if (script.cardCostText == null)
            script.cardCostText = Achar<TMP_Text>(cardObj, "CostTxt");
        if (script.cardDescriptionText == null)
            script.cardDescriptionText = Achar<TMP_Text>(cardObj, "CardDescription");
        if (script.rarityBorder == null)
            script.rarityBorder = Achar<Image>(cardObj, "Border");

        return script;
    }

    static T Achar<T>(GameObject raiz, string nome) where T : Component
    {
        Transform alvo = raiz.transform.Find(nome);
        return alvo != null ? alvo.GetComponent<T>() : null;
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

    /// <summary>
    /// O que o baralho é, e não só quantas cartas tem.
    ///
    /// A contagem sozinha não ajuda a decidir o que trocar: "12/12 cartas, custo
    /// médio 1" não diz que o herói não leva nada que cure. O balanço por
    /// <see cref="CardRole"/> é a mesma conta que o <c>DeckGenerator</c> usa para
    /// montar o baralho inicial — aqui ela fica à vista de quem edita.
    /// </summary>
    void UpdateDeckStats()
    {
        if (deckStatsText == null) return;

        int cardCount = currentDeck.cards.Count;
        float avgCost = currentDeck.cards.Count > 0
            ? (float)currentDeck.cards.Average(c => c.energyCost)
            : 0f;

        int ataque = 0, defesa = 0, suporte = 0, utilidade = 0;

        foreach (CardData card in currentDeck.cards)
        {
            switch (CardRoleUtil.Of(card))
            {
                case CardRole.Ataque: ataque++; break;
                case CardRole.Defesa: defesa++; break;
                case CardRole.Suporte: suporte++; break;
                default: utilidade++; break;
            }
        }

        // O vermelho marca a falta, não a quantidade: um baralho sem ataque
        // nenhum não vence combate, e sem suporte nenhum não atravessa a estrada.
        string Parte(string rotulo, int quantas)
            => quantas == 0
                ? $"<color=#C0483E>{rotulo} 0</color>"
                : $"{rotulo} {quantas}";

        // Emoji, e não sinais tipográficos: o ✚ não existe na fonte do jogo e
        // saía como caixinha. O 🩹 já aparece no Mercado e no fim de jornada.
        deckStatsText.text = $"{cardCount}/{currentDeck.maxDeckSize} cartas   ·   "
                           + $"custo médio {avgCost:0.0}   ·   "
                           + $"{Parte("⚔️", ataque)}   {Parte("🛡️", defesa)}   "
                           + $"{Parte("🩹", suporte)}   ◆ {utilidade}";
    }

    void UpdateCollectionStats()
    {
        if (collectionStatsText == null) return;

        string quem = currentHero != null ? GetClassName(currentHero.heroClass) : "";
        collectionStatsText.text = $"{allOwnedCards.Count} cartas de {quem} no acervo da guilda";
    }

    public void SaveCurrentDeck()
    {
        if (currentHero == null)
        {
            Debug.LogError("DeckManager: Nenhum herói selecionado para salvar!");
            UIManager.Instance?.ShowMessage("Selecione um herói primeiro!", 2f);
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
            case HeroClass.Hunter: return "Caçador";
            default: return "Herói";
        }
    }
}