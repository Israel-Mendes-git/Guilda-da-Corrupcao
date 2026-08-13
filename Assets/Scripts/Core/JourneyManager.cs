using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class JourneyManager : MonoBehaviour
{
    public static JourneyManager Instance;

    [Header("UI References")]
    public GameObject journeyPanel;
    public TMP_Text dayText;
    public TMP_Text questNameText;
    public TMP_Text biomeText;
    public Image biomeIcon;
    public TMP_Text eventTitleText;
    public TMP_Text eventDescriptionText;

    [Header("Card UI")]
    public Transform handContainer;
    public GameObject cardPrefab;
    public TMP_Text deckCountText;
    public TMP_Text handCountText;
    public TMP_Text discardCountText;

    [Header("Party Status")]
    public Transform partyStatusContainer;
    public GameObject partyStatusPrefab;

    [Header("Escolhas do Evento")]
    public Transform choiceContainer;
    public GameObject choiceButtonPrefab;
    public TMP_Text resolutionLogText;

    [Header("Recursos - UI")]
    public TMP_Text rationsText;
    public TMP_Text torchesText;
    public TMP_Text energyText;

    [Header("Sala de Mapas")]
    public Button detourButton;
    public TMP_Text detourCountText;
    public TMP_Text upcomingEventsText;

    [Header("Buttons")]
    public Button abortButton;
    public Button endTurnButton;

    [Header("Config")]
    public float textTypeSpeed = 0.03f;
    public float eventTransitionDelay = 0.5f;

    [Header("Recursos")]
    public int rations = 10;
    public int torches = 5;
    public int currentEnergy = 3;
    public int maxEnergy = 5;

    [Header("Desgaste da estrada")]
    [Tooltip("Dano por herói a cada trecho sem ração.")]
    // Mantido em 5: a fome é a punição mais dura da estrada e deve doer. O que
    // mudou foi a quantidade de ração, não o preço de ficar sem ela.
    public int starvationDamage = 5;

    [Tooltip("Estresse por herói a cada trecho sem tocha.")]
    // 8 empilhava rápido demais: eram 6,6 trechos no escuro por jornada, o que
    // sozinho enchia a barra de estresse e levava 26% dos heróis à aflição.
    public float darknessStress = 5f;

    private QuestData currentQuest;
    private List<HeroData> currentParty;
    private JourneyMap journeyMap;
    private int currentDay = 0;
    private int totalDays = 0;
    private bool isWaitingForChoice = false;
    private EventData currentEvent;

    // Entre resolver um evento e entrar no próximo, o grupo escolhe por onde seguir.
    private bool isChoosingRoute = false;

    /// <summary>O mapa só aceita cliques enquanto a rota está sendo escolhida.</summary>
    public bool IsChoosingRoute => isChoosingRoute && !journeyEnded;

    // Sem isto, as corrotinas de transição já agendadas continuam produzindo
    // eventos depois que a jornada acabou — a jornada nunca fechava.
    private bool journeyEnded = false;

    // Preparo acumulado com cartas antes de decidir: reduz o dano do desfecho escolhido.
    private float currentMitigation = 0f;
    private const float MaxMitigation = 0.75f;

    // Baixas desta jornada, para o relatório final.
    private List<HeroData> journeyCasualties = new List<HeroData>();

    // Um descanso por evento, para o botão não virar fonte infinita de energia.
    private bool hasRestedThisEvent = false;

    // Efeitos de carta que duram além do evento em que foram jogadas.
    private bool skipNextCombat = false;      // Intimidate
    private int weatherProtectionDays = 0;    // ProtectFromWeather

    // Comprado na Sala de Mapas e consumido aqui.
    private int revealedEvents = 0;
    private int detoursRemaining = 0;

    // Card system
    private CardManager cardManager;
    private DeckData currentDeck;

    // Quem emprestou cada carta do baralho desta jornada. Segue para o combate,
    // onde a posição do dono decide a potência da carta.
    private CardOwnership currentOwnership;
    private int extraStartingCards = 0;
    private int maxEnergyBonus = 0;
    private float goldBonus = 0f;

    public void AddExtraStartingCard(int amount)
    {
        extraStartingCards += amount;
    }

    public void AddMaxEnergy(int amount)
    {
        maxEnergyBonus += amount;
        maxEnergy = 5 + maxEnergyBonus;
        currentEnergy = maxEnergy;
    }

    public void AddGoldBonus(float bonus)
    {
        goldBonus += bonus;
    }


    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        if (journeyPanel != null)
            journeyPanel.SetActive(false);

        if (abortButton != null)
            abortButton.onClick.AddListener(ConfirmAbortJourney);

        if (endTurnButton != null)
            endTurnButton.onClick.AddListener(EndTurn);

        if (detourButton != null)
            detourButton.onClick.AddListener(TakeDetour);

        // Inicializa CardManager
        cardManager = GetComponent<CardManager>();
        if (cardManager == null)
            cardManager = gameObject.AddComponent<CardManager>();
    }

    /// <param name="startingRations">-1 mantém o sorteio padrão; caso contrário, é o que foi comprado na preparação.</param>
    /// <param name="startingTorches">Idem.</param>
    /// <param name="cardOwnership">Quem emprestou cada carta, para a formação valer no combate.</param>
    public void StartJourney(QuestData quest, List<HeroData> party, DeckData deck,
                             int startingRations = -1, int startingTorches = -1,
                             CardOwnership cardOwnership = null)
    {
        currentQuest = quest;

        // A ordem da lista é a formação escolhida na preparação — copiar preservando-a
        // é o que faz as duas primeiras posições valerem como linha de frente.
        currentParty = new List<HeroData>(party);
        currentOwnership = cardOwnership;
        currentDeck = deck;  // Adicione esta variável na classe
        currentDay = 0;
        totalDays = quest.GetActualDuration();
        currentEnergy = maxEnergy;
        journeyCasualties.Clear();
        currentMitigation = 0f;
        journeyEnded = false;
        isChoosingRoute = false;
        skipNextCombat = false;
        weatherProtectionDays = 0;

        // O que foi comprado na Sala de Mapas vale para esta jornada.
        revealedEvents = MapRoomManager.Instance != null ? MapRoomManager.Instance.ConsumeScoutingForJourney() : 0;
        detoursRemaining = MapRoomManager.Instance != null ? MapRoomManager.Instance.ConsumeDetoursForJourney() : 0;

        // Cada jornada começa com a cabeça limpa: aflições e Death's Door não transitam.
        foreach (var hero in currentParty)
        {
            hero.mentalState = MentalState.Normal;
            hero.isOnDeathsDoor = false;
        }

        // Provisões vêm da preparação quando o jogador as comprou.
        rations = startingRations >= 0 ? startingRations : 10 + Random.Range(0, 5);
        torches = startingTorches >= 0 ? startingTorches : 5 + Random.Range(0, 3);
        maxEnergy = 5 + maxEnergyBonus;
        currentEnergy = maxEnergy;

        // StartJourney vem de fora e pode chegar antes do Start() deste componente.
        if (cardManager == null)
        {
            cardManager = GetComponent<CardManager>();
            if (cardManager == null)
                cardManager = gameObject.AddComponent<CardManager>();
        }

        cardManager.handSize = 5 + extraStartingCards;
        cardManager.maxHandSize = 7 + extraStartingCards;
        cardManager.InitializeDeck(currentDeck);

        // Gera a rota ramificada desta jornada.
        journeyMap = JourneyMapGenerator.Generate(quest, totalDays);
        JourneyMapUI.Instance?.BuildMap(journeyMap, revealedEvents);

        // A jornada assume a tela: sem isto, os prédios da guilda continuavam
        // desenhados atrás do mapa e das cartas.
        if (journeyPanel != null)
        {
            if (UIManager.Instance != null)
                UIManager.Instance.EnterJourneyScreen();
            else
                journeyPanel.SetActive(true);

            journeyPanel.transform.SetAsLastSibling();
        }
        else
        {
            Debug.LogError("JourneyManager: journeyPanel não está atribuído no Inspector!");
        }
        UpdateQuestInfo();
        UpdatePartyStatus();
        UpdateCardUI();

        // Primeiro evento
        NextEvent();

        Debug.Log($"Jornada iniciada: {quest.questName} - {totalDays} dias");
    }

    /// <summary>
    /// Fim de um trecho: ou o chefe caiu e a jornada acabou, ou o grupo
    /// precisa decidir por qual caminho seguir.
    /// </summary>
    void NextEvent()
    {
        if (journeyEnded) return;

        // Estar no último nó só acontece depois de vencer o chefe.
        if (journeyMap != null && journeyMap.Current != null && journeyMap.IsAtEnd)
        {
            EndJourney(true);
            return;
        }

        ShowRouteChoice();
    }

    /// <summary>Devolve o controle ao mapa: nada avança até o jogador escolher um nó.</summary>
    void ShowRouteChoice()
    {
        if (journeyMap == null) return;

        isWaitingForChoice = false;
        isChoosingRoute = true;

        ClearChoices();

        List<MapNode> options = journeyMap.GetChoices();
        if (options.Count == 0)
        {
            // Rota sem saída não deveria existir; encerrar é melhor que travar.
            Debug.LogWarning("JourneyManager: nó sem continuação — encerrando a jornada.");
            EndJourney(true);
            return;
        }

        // Caminho único não é escolha: entra direto.
        if (options.Count == 1)
        {
            EnterNode(options[0].id);
            return;
        }

        if (eventTitleText != null)
            eventTitleText.text = "Escolha o caminho";

        if (eventDescriptionText != null)
            eventDescriptionText.text = "A rota se divide. Selecione no mapa por onde o grupo segue.";

        if (resolutionLogText != null)
            resolutionLogText.text = "";

        UpdateDetourUI();
        JourneyMapUI.Instance?.Refresh(journeyMap, revealedEvents);
        UIManager.Instance?.ShowMessage("A rota se divide — escolha no mapa.", 2.5f);
    }

    /// <summary>Chamado pelo mapa quando o jogador escolhe um nó alcançável.</summary>
    public void OnNodeChosen(int nodeId)
    {
        if (journeyEnded || !isChoosingRoute) return;
        if (journeyMap == null || !journeyMap.MoveTo(nodeId)) return;

        isChoosingRoute = false;
        EnterNodeInternal();
    }

    void EnterNode(int nodeId)
    {
        if (journeyMap == null || !journeyMap.MoveTo(nodeId)) return;

        isChoosingRoute = false;
        EnterNodeInternal();
    }

    void EnterNodeInternal()
    {
        currentDay++;
        currentEvent = journeyMap.Current?.eventData;

        if (currentEvent == null)
        {
            Debug.LogError("JourneyManager: nó do mapa sem evento associado.");
            EndJourney(false);
            return;
        }

        ShowEvent(currentEvent);
    }

    void ShowEvent(EventData eventData)
    {
        isWaitingForChoice = true;
        currentMitigation = 0f;
        hasRestedThisEvent = false;

        if (dayText != null)
            dayText.text = $"Dia {currentDay} / {(journeyMap != null ? journeyMap.LayerCount : totalDays)}";

        if (eventTitleText != null)
            eventTitleText.text = eventData.eventTitle;

        if (eventDescriptionText != null)
            StartCoroutine(TypeText(eventData.description, eventDescriptionText));

        if (resolutionLogText != null)
            resolutionLogText.text = "";

        // Atualiza UI das cartas e as opções deste evento
        UpdateCardUI();
        BuildChoices(eventData);
        UpdateResourceUI();
        UpdateDetourUI();
        UpdateUpcomingEvents();
        JourneyMapUI.Instance?.Refresh(journeyMap, revealedEvents);

        UIManager.Instance?.ShowMessage("Prepare-se com cartas e escolha como agir.", 3f);
    }

    /// <summary>Cria um botão para cada desfecho possível do evento.</summary>
    void BuildChoices(EventData eventData)
    {
        if (choiceContainer == null || choiceButtonPrefab == null)
        {
            Debug.LogWarning("JourneyManager: choiceContainer/choiceButtonPrefab não configurados — as escolhas do evento não serão exibidas.");
            return;
        }

        ClearContainerNow(choiceContainer);

        EventOutcome[] outcomes = eventData.outcomes;

        // Evento sem opções ainda precisa de uma saída.
        if (outcomes == null || outcomes.Length == 0)
        {
            outcomes = new[]
            {
                new EventOutcome { optionText = "Seguir em frente", consequences = new EventConsequences() }
            };
        }

        // Eventos de combate ganham a opção de resolver na mesa, e não pela narrativa.
        if (IsCombatEvent(eventData) && CombatManager.Instance != null)
            CreateChoiceButton("⚔️ Enfrentar em combate", StartCombatForCurrentEvent);

        foreach (var outcome in outcomes)
        {
            EventOutcome captured = outcome; // evita capturar a variável do laço
            CreateChoiceButton(outcome.optionText, () => ChooseOutcome(captured));
        }

        // O layout só redistribui a altura no frame seguinte; sem forçar agora,
        // um evento de quatro opções aparece por um quadro transbordando sobre
        // o status do grupo.
        var rect = choiceContainer as RectTransform;
        if (rect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
    }

    void CreateChoiceButton(string label, UnityEngine.Events.UnityAction action)
    {
        GameObject btnObj = Instantiate(choiceButtonPrefab, choiceContainer);

        TMP_Text text = btnObj.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
            text.text = label;

        Button btn = btnObj.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(action);
        }
    }

    static bool IsCombatEvent(EventData eventData)
    {
        return eventData != null && (eventData.eventType == JourneyEventType.Combat || eventData.isBossEvent);
    }

    /// <summary>Abre o combate por turnos e devolve o resultado para a jornada.</summary>
    void StartCombatForCurrentEvent()
    {
        if (!isWaitingForChoice || currentEvent == null) return;

        isWaitingForChoice = false;
        ClearChoices();

        // Intimidação: o combate é evitado e conta como vitória. O chefe não
        // se intimida — do contrário a carta venceria a jornada sozinha.
        if (skipNextCombat && !currentEvent.isBossEvent)
        {
            skipNextCombat = false;
            UIManager.Instance?.ShowMessage(
                "Os inimigos recuam diante do grupo — não há luta.", 2.5f);
            OnCombatFinished(true);
            return;
        }

        List<EnemyData> lineup = EnemyPool.GetLineup(
            currentQuest.biomeType,
            currentEvent.isBossEvent,
            currentDay
        );

        CombatManager.Instance.StartCombat(currentParty, currentDeck, lineup, OnCombatFinished, currentOwnership);
    }

    void OnCombatFinished(bool victory)
    {
        // Quem morreu no combate entra no relatório da jornada.
        foreach (var hero in currentParty)
        {
            if (hero.isDead && !journeyCasualties.Contains(hero))
                journeyCasualties.Add(hero);
        }

        ConsumeDailyResources();

        UpdatePartyStatus();
        UpdateCardUI();
        UpdateResourceUI();

        if (IsPartyDead())
        {
            EndJourney(false);
            return;
        }

        // Perder para o chefe encerra a jornada; perder um encontro comum só cobra caro.
        if (!victory && currentEvent != null && currentEvent.isBossEvent)
        {
            EndJourney(false);
            return;
        }

        StartCoroutine(DelayedNextEvent());
    }

    /// <summary>Resolve o evento com a opção escolhida e avança o dia.</summary>
    void ChooseOutcome(EventOutcome outcome)
    {
        if (!isWaitingForChoice) return;
        isWaitingForChoice = false;

        EventResolver.Resolution resolution = EventResolver.Resolve(outcome, currentParty, currentMitigation);

        // Desvios de rota custam dias — e dias custam mantimentos.
        for (int i = 0; i < resolution.extraDays; i++)
            ConsumeDailyResources();

        ConsumeDailyResources();

        foreach (var deadHero in resolution.died)
            journeyCasualties.Add(deadHero);

        if (resolutionLogText != null)
            resolutionLogText.text = resolution.ToText();

        if (resolution.lines.Count > 0)
            Debug.Log($"[Evento] {currentEvent.eventTitle}\n{resolution.ToText()}");

        ClearChoices();
        UpdatePartyStatus();
        UpdateCardUI();
        UpdateResourceUI();
        UpdateDetourUI();

        if (IsPartyDead())
        {
            EndJourney(false);
            return;
        }

        StartCoroutine(DelayedNextEvent());
    }

    void ClearChoices()
    {
        ClearContainerNow(choiceContainer);
    }

    /// <summary>
    /// Esvazia o container imediatamente.
    ///
    /// `Destroy` só remove o objeto no fim do frame. Quando dois eventos são
    /// processados no mesmo frame — o que acontece sempre que a rota tem um
    /// caminho único e o nó é resolvido em seguida — os botões do evento
    /// anterior continuavam na tela junto com os novos. O jogador via uma pilha
    /// de opções que não pertenciam ao evento à sua frente, e o primeiro botão
    /// da lista podia ser sobra do evento passado.
    /// </summary>
    static void ClearContainerNow(Transform container)
    {
        if (container == null) return;

        for (int i = container.childCount - 1; i >= 0; i--)
        {
            Transform child = container.GetChild(i);
            child.SetParent(null, false);   // sai da lista agora, não no fim do frame
            Destroy(child.gameObject);
        }
    }

    /// <summary>
    /// Recusa o evento atual e toma outro caminho. Custa um desvio comprado na
    /// Sala de Mapas e um dia extra de mantimentos — evitar tem preço.
    /// </summary>
    void TakeDetour()
    {
        if (!isWaitingForChoice) return;

        if (detoursRemaining <= 0)
        {
            UIManager.Instance?.ShowMessage("Nenhum desvio disponível. Compre rotas na Sala de Mapas.", 2f);
            return;
        }

        // O chefe final não pode ser contornado.
        if (currentEvent != null && currentEvent.isBossEvent)
        {
            UIManager.Instance?.ShowMessage("Não há como contornar o que espera vocês aqui.", 2.5f);
            return;
        }

        detoursRemaining--;

        EventData replacement = EventPool.GetRandomEvent(
            currentQuest.biomeType,
            currentQuest.corruptionLevel,
            currentDay
        );

        // O rodeio consome o dia.
        ConsumeDailyResources();

        if (IsPartyDead())
        {
            EndJourney(false);
            return;
        }

        // Troca o que acontece neste nó, sem redesenhar a rota.
        if (journeyMap?.Current != null)
            journeyMap.ReplaceEvent(journeyMap.Current.id, replacement);

        currentEvent = replacement;

        UIManager.Instance?.ShowMessage("O grupo toma outro caminho.", 2f);
        ShowEvent(replacement);
    }

    void UpdateDetourUI()
    {
        if (detourCountText != null)
            detourCountText.text = $"🧭 {detoursRemaining}";

        if (detourButton != null)
            detourButton.interactable = detoursRemaining > 0 && isWaitingForChoice
                && currentEvent != null && !currentEvent.isBossEvent;
    }

    /// <summary>
    /// Títulos do que os batedores identificaram adiante. Numa rota ramificada
    /// isso são os caminhos possíveis a partir daqui, não "os próximos dias".
    /// </summary>
    public List<string> GetRevealedEventTitles()
    {
        var titles = new List<string>();

        if (journeyMap == null || revealedEvents <= 0)
            return titles;

        foreach (var node in journeyMap.GetChoices())
        {
            if (titles.Count >= revealedEvents) break;
            titles.Add($"Dia {node.layer + 1}: {node.eventData?.eventTitle}");
        }

        return titles;
    }

    void UpdateUpcomingEvents()
    {
        if (upcomingEventsText == null) return;

        List<string> known = GetRevealedEventTitles();

        upcomingEventsText.text = known.Count == 0
            ? "🔭 Nenhum batedor à frente."
            : "🔭 Adiante:\n" + string.Join("\n", known);
    }

    void UpdateResourceUI()
    {
        if (rationsText != null) rationsText.text = $"🍖 {rations}";
        if (torchesText != null) torchesText.text = $"🔥 {torches}";
        if (energyText != null) energyText.text = $"⚡ {currentEnergy}/{maxEnergy}";
    }

    void ApplyCardEffectOnJourney(CardData card)
    {
        Debug.Log($"Usando carta: {card.cardName} - Efeito: {card.journeyEffect}");

        switch (card.journeyEffect)
        {
            case JourneyEffectType.RemoveObstacle:
                // Obstáculo removido significa atravessar o evento ileso: em vez
                // de só somar mitigação, leva o preparo ao teto.
                currentMitigation = MaxMitigation;
                UIManager.Instance?.ShowMessage(
                    $"{card.cardName} abriu caminho — o grupo passa ileso.", 2.5f);
                break;

            case JourneyEffectType.HealInjury:
                var injuredHero = currentParty.FirstOrDefault(h => h.isInjured && !h.isDead);
                if (injuredHero != null)
                {
                    injuredHero.isInjured = false;
                    UIManager.Instance?.ShowMessage($"{card.cardName} curou {injuredHero.heroName}!", 2f);
                }
                break;

            case JourneyEffectType.GainFood:
                rations += card.journeyEffectValue;
                UIManager.Instance?.ShowMessage($"{card.cardName} rendeu +{card.journeyEffectValue} comida!", 2f);
                break;

            case JourneyEffectType.GainGold:
                GuildManager.Instance.AddGold(card.journeyEffectValue);
                UIManager.Instance?.ShowMessage($"{card.cardName} rendeu +{card.journeyEffectValue} ouro!", 2f);
                break;

            case JourneyEffectType.RevealNextEvent:
                revealedEvents = Mathf.Max(revealedEvents, 1) + card.journeyEffectValue;
                var ahead = journeyMap?.GetChoices();
                if (ahead != null && ahead.Count > 0)
                {
                    string nomes = string.Join(" / ", ahead.Select(n => n.eventData?.eventTitle));
                    UIManager.Instance?.ShowMessage($"Adiante: {nomes}", 3f);
                }
                UpdateUpcomingEvents();
                JourneyMapUI.Instance?.Refresh(journeyMap, revealedEvents);
                break;

            case JourneyEffectType.SkipDay:
                SkipDays(1);
                break;

            case JourneyEffectType.Intimidate:
                skipNextCombat = true;
                UIManager.Instance?.ShowMessage(
                    "Inimigos intimidados — o próximo combate será evitado.", 2.5f);
                break;

            case JourneyEffectType.Purify:
                foreach (var hero in currentParty)
                {
                    hero.isInjured = false;
                }
                UIManager.Instance?.ShowMessage($"Maldições e doenças foram removidas!", 2f);
                break;

            case JourneyEffectType.Teleport:
                SkipDays(2);
                break;

            case JourneyEffectType.ProtectFromWeather:
                weatherProtectionDays += Mathf.Max(2, card.journeyEffectValue);
                UIManager.Instance?.ShowMessage(
                    $"Grupo abrigado — fome e escuridão não os atingem por {weatherProtectionDays} dias.", 2.5f);
                break;

            case JourneyEffectType.RestoreMorale:
                foreach (var hero in currentParty)
                {
                    if (!hero.isDead)
                        hero.morale = Mathf.Min(100, hero.morale + card.journeyEffectValue);
                }
                UIManager.Instance?.ShowMessage($"Moral do grupo aumentou em {card.journeyEffectValue}!", 2f);
                break;

            case JourneyEffectType.ExtraRations:
                rations += 5;
                UIManager.Instance?.ShowMessage($"Encontrou rações extras! +5 comida", 2f);
                break;

            default:
                UIManager.Instance?.ShowMessage($"{card.cardName} usado com sucesso!", 2f);
                break;
        }
    }

    void UpdateCardUI()
    {
        if (handContainer == null || cardManager == null || cardPrefab == null) return;

        UIUtil.ClearChildrenNow(handContainer);

        // Mesmo leque do combate: mantém as cartas legíveis e dentro da faixa
        // reservada à mão, em vez de espremidas por um layout horizontal.
        var fan = handContainer.GetComponent<HandFanLayout>();
        if (fan == null) fan = handContainer.gameObject.AddComponent<HandFanLayout>();

        var legacy = handContainer.GetComponent<LayoutGroup>();
        if (legacy != null) Destroy(legacy);

        // Mostra cartas da mão
        foreach (var card in cardManager.hand)
        {
            GameObject cardObj = Instantiate(cardPrefab, handContainer);
            SetupCardUI(cardObj, card);
        }

        fan.Rebuild();

        // Atualiza contadores
        if (deckCountText != null)
            deckCountText.text = $"📚 {cardManager.drawPile.Count}";
        if (handCountText != null)
            handCountText.text = $"🃏 {cardManager.hand.Count}/{cardManager.maxHandSize}";
        if (discardCountText != null)
            discardCountText.text = $"🗑️ {cardManager.discardPile.Count}";
    }

    /// <summary>Escreve num filho pelo nome, procurando em qualquer profundidade.</summary>
    static void SetCardText(GameObject root, string childName, string value)
    {
        foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
            if (t.gameObject.name == childName) { t.text = value; return; }
    }

    void SetupCardUI(GameObject cardObj, CardData card)
    {
        // O prefab traz um CardUI com as referências ligadas. Procurar filhos
        // por "Name"/"Description"/"Cost" não achava nada — o prefab usa
        // "CardName"/"CardDescription"/"CostTxt" — e as cartas ficavam exibindo
        // o "New Text" que veio do editor.
        var cardUI = cardObj.GetComponent<CardUI>();
        if (cardUI != null)
        {
            cardUI.Bind(card, journeyMode: true);
        }
        else
        {
            SetCardText(cardObj, "CardName", card.cardName);
            SetCardText(cardObj, "CardDescription", card.GetDescription(true));
            SetCardText(cardObj, "CostTxt", $"⚡ {card.energyCost}");

            Image cardImage = cardObj.transform.Find("Image")?.GetComponent<Image>();
            if (cardImage != null && card.cardImage != null)
                cardImage.sprite = card.cardImage;
        }

        // Fundo por raridade
        Image background = cardObj.GetComponent<Image>();
        if (background != null)
        {
            switch (card.rarity)
            {
                case CardRarity.Common:
                    background.color = new Color(0.5f, 0.5f, 0.5f);
                    break;
                case CardRarity.Rare:
                    background.color = new Color(0.2f, 0.4f, 0.8f);
                    break;
                case CardRarity.Epic:
                    background.color = new Color(0.6f, 0.2f, 0.8f);
                    break;
                case CardRarity.Legendary:
                    background.color = new Color(0.9f, 0.7f, 0.1f);
                    break;
            }
        }

        // Botão para jogar a carta
        Button btn = cardObj.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.AddListener(() => PlayCard(card));

            // Desabilita se não tem energia suficiente
            btn.interactable = currentEnergy >= card.energyCost;
        }
    }

    /// <summary>
    /// Jogar uma carta não resolve mais o evento sozinha: ela aplica seu efeito e
    /// prepara o grupo, reduzindo o dano da opção que o jogador escolher em seguida.
    /// </summary>
    void PlayCard(CardData card)
    {
        if (!isWaitingForChoice) return;

        if (currentEnergy < card.energyCost)
        {
            UIManager.Instance?.ShowMessage($"Energia insuficiente! Precisa de {card.energyCost} energia.", 2f);
            return;
        }

        currentEnergy -= card.energyCost;

        ApplyCardEffectOnJourney(card);
        currentMitigation = Mathf.Min(MaxMitigation, currentMitigation + GetMitigationFor(card));

        cardManager.PlayCard(card);

        UpdatePartyStatus();
        UpdateCardUI();
        UpdateResourceUI();
    }

    /// <summary>Quanto cada tipo de carta protege o grupo do desfecho do evento.</summary>
    float GetMitigationFor(CardData card)
    {
        switch (card.journeyEffect)
        {
            case JourneyEffectType.RemoveObstacle:
            case JourneyEffectType.ProtectFromWeather:
            case JourneyEffectType.Intimidate:
                return 0.35f;

            case JourneyEffectType.Purify:
            case JourneyEffectType.HealInjury:
            case JourneyEffectType.Teleport:
                return 0.20f;

            default:
                return 0.10f;
        }
    }

    /// <summary>
    /// Descanso: recupera energia e compra uma carta, mas gasta mantimentos de um dia.
    /// Serve também como saída de emergência caso a UI de escolhas não esteja montada,
    /// para que a jornada nunca fique sem uma forma de avançar.
    /// </summary>
    void EndTurn()
    {
        if (!isWaitingForChoice) return;

        bool choicesAvailable = choiceContainer != null && choiceButtonPrefab != null;
        if (!choicesAvailable)
        {
            ChooseOutcome(GetFallbackOutcome(currentEvent));
            return;
        }

        if (hasRestedThisEvent)
        {
            UIManager.Instance?.ShowMessage("O grupo já descansou neste trecho.", 2f);
            return;
        }

        hasRestedThisEvent = true;

        currentEnergy = Mathf.Min(maxEnergy, currentEnergy + 2);
        cardManager.DrawCard();
        ConsumeDailyResources();

        UpdatePartyStatus();
        UpdateCardUI();
        UpdateResourceUI();

        if (IsPartyDead())
        {
            EndJourney(false);
            return;
        }

        UIManager.Instance?.ShowMessage("O grupo descansa: ⚡+2 e uma carta — ao custo de mantimentos.", 2.5f);
    }

    /// <summary>Primeira opção do evento, ou uma saída neutra se ele não tiver nenhuma.</summary>
    EventOutcome GetFallbackOutcome(EventData eventData)
    {
        if (eventData != null && eventData.outcomes != null && eventData.outcomes.Length > 0)
            return eventData.outcomes[0];

        return new EventOutcome { optionText = "Seguir em frente", consequences = new EventConsequences() };
    }

    /// <summary>
    /// Contadores da última jornada, para o relatório de balanceamento saber de
    /// onde veio o desgaste. Sem isto, "a party morreu" não distingue fome de
    /// combate — e as duas causas pedem correções opostas.
    /// </summary>
    public int UpkeepTicks { get; private set; }
    public int StarvationTicks { get; private set; }
    public int StarvationDamage { get; private set; }
    public int DarknessTicks { get; private set; }
    public int DaysElapsed => currentDay;
    public int PlannedDays => totalDays;

    /// <summary>Rações consumidas por dia, conforme o tamanho do grupo vivo.</summary>
    public int DailyRationCost()
    {
        int vivos = currentParty != null
            ? currentParty.Count(h => h != null && h.IsAlive)
            : 0;

        return PartyFormation.DailyRations(vivos);
    }

    void ConsumeDailyResources()
    {
        UpkeepTicks++;

        var upkeep = new EventResolver.Resolution();

        // Abrigo comprado com carta: o consumo acontece, mas as penalidades não.
        bool abrigado = weatherProtectionDays > 0;
        if (abrigado) weatherProtectionDays--;

        rations -= DailyRationCost();
        if (rations <= 0)
        {
            rations = 0;

            if (!abrigado)
            {
                // Fome: dano real, capaz de levar alguém à beira da morte.
                StarvationTicks++;

                foreach (var hero in currentParty.Where(h => h.IsAlive).ToList())
                {
                    EventResolver.DealDamage(hero, starvationDamage, currentParty, upkeep);
                    StarvationDamage += starvationDamage;
                }

                UIManager.Instance?.ShowMessage("🍖 Sem rações! O grupo passa fome.", 2f);
            }
            else
            {
                UIManager.Instance?.ShowMessage("O abrigo protege o grupo apesar da falta de rações.", 2f);
            }
        }

        torches--;
        if (torches <= 0)
        {
            torches = 0;

            // Escuridão não fere o corpo, corrói a mente.
            if (!abrigado)
            {
                DarknessTicks++;

                foreach (var hero in currentParty.Where(h => h.IsAlive))
                    EventResolver.AddStress(hero, darknessStress, upkeep);
            }
        }

        foreach (var deadHero in upkeep.died)
            journeyCasualties.Add(deadHero);

        if (upkeep.lines.Count > 0)
            Debug.Log($"[Manutenção diária]\n{upkeep.ToText()}");
    }

    /// <summary>
    /// Avança pela rota sem resolver os eventos do caminho.
    ///
    /// Antes isto apenas chamava `ConsumeDailyResources()` em laço, ou seja:
    /// a carta que prometia "pular um dia" gastava mantimentos e não saía do
    /// lugar — punia quem a usasse. Pular um dia é andar no mapa de graça.
    /// </summary>
    void SkipDays(int days)
    {
        if (journeyEnded || journeyMap == null) return;

        int pulados = 0;

        for (int i = 0; i < days; i++)
        {
            // O confronto final não se contorna.
            var opcoes = journeyMap.GetChoices().Where(n => !n.isBoss).ToList();
            if (opcoes.Count == 0) break;

            journeyMap.MoveTo(opcoes[Random.Range(0, opcoes.Count)].id);
            currentDay++;
            pulados++;
        }

        if (pulados == 0)
        {
            UIManager.Instance?.ShowMessage("Não há como contornar o que espera adiante.", 2.5f);
            return;
        }

        UIManager.Instance?.ShowMessage(
            pulados == 1 ? "O grupo atravessa o dia sem incidentes."
                         : $"O grupo atravessa {pulados} dias sem incidentes.", 2.5f);

        isWaitingForChoice = false;
        ClearChoices();
        UpdateResourceUI();
        JourneyMapUI.Instance?.Refresh(journeyMap, revealedEvents);

        StartCoroutine(DelayedNextEvent());
    }

    bool IsPartyDead()
    {
        return currentParty.All(h => h.isDead);
    }

    void EndJourney(bool success)
    {
        // Vários caminhos levam aqui (party morta, chefe vencido, derrota para o
        // chefe) e corrotinas de transição podem estar em voo. Sem esta guarda a
        // jornada era encerrada repetidas vezes, pagando recompensa a cada volta.
        if (journeyEnded) return;
        journeyEnded = true;
        isWaitingForChoice = false;
        isChoosingRoute = false;

        int survivors = currentParty.Count(h => !h.isDead);
        int reward = success ? currentQuest.GetTotalReward(totalDays) : currentQuest.baseReward / 2;
        reward += survivors * 25;

        // Aplica bônus de ouro da biblioteca
        if (goldBonus > 0)
        {
            int bonusReward = Mathf.RoundToInt(reward * goldBonus);
            reward += bonusReward;
            UIManager.Instance?.ShowMessage($"Bônus da Biblioteca: +{bonusReward} ouro!", 2f);
        }

        GuildManager.Instance.AddGold(reward);
        GuildManager.Instance.AddReputation(success ? 10 : -5);

        foreach (var hero in currentParty)
        {
            if (hero.isDead)
            {
                GuildManager.Instance.RegisterDeath(hero);
                continue;
            }

            // O retorno alivia o corpo, mas não apaga o que a jornada deixou na cabeça.
            hero.isOnDeathsDoor = false;
            hero.currentHp = Mathf.Max(1, Mathf.RoundToInt(hero.maxHp * 0.6f));
            hero.stress = Mathf.Max(0f, hero.stress - 15f);
            hero.morale = Mathf.Min(hero.morale + (success ? 20f : 5f), 100f);

            // Ferimentos leves saram sozinhos; os demais exigem cuidado posterior.
            if (hero.isInjured && (hero.trait == Trait.FastHealer || Random.value < 0.4f))
                hero.isInjured = false;
        }

        // A missão sai do quadro e o quadro se repõe.
        if (QuestManager.Instance != null)
            QuestManager.Instance.CompleteQuest(currentQuest);

        if (TavernManager.Instance != null)
            TavernManager.Instance.RefreshRecruits();

        string resultMessage = success
            ? $"Missão concluída!\n{survivors} heróis sobreviveram\n+{reward} ouro"
            : $"Missão fracassada!\n{survivors} heróis sobreviveram\n+{reward} ouro";

        if (journeyCasualties.Count > 0)
        {
            resultMessage += "\n\n⚰️ <color=#B04040>Perdas:</color>";
            foreach (var fallen in journeyCasualties)
                resultMessage += $"\n• {fallen.heroName}";
        }

        var afflicted = currentParty
            .Where(h => h.IsAlive && MentalStateUtil.IsAffliction(h.mentalState))
            .ToList();

        if (afflicted.Count > 0)
        {
            resultMessage += "\n\n🧠 <color=#B0A040>Abalados:</color>";
            foreach (var hero in afflicted)
                resultMessage += $"\n• {hero.heroName} — {MentalStateUtil.GetLabel(hero.mentalState)}";
        }

        UIManager.Instance?.ShowResult(
            success ? "🏆 Vitória!" : "💀 Derrota",
            resultMessage,
            () => {
                journeyPanel.SetActive(false);
                UIManager.Instance?.ShowGuildScreen();
            }
        );

        OnJourneyComplete?.Invoke(success, reward);
       // LibraryManager.Instance?.ClearAllKnowledges();
    }

    void ConfirmAbortJourney()
    {
        UIManager.Instance?.ShowConfirm(
            "Abandonar Jornada",
            "Tem certeza? Receberá recompensa reduzida.",
            () => EndJourney(false),
            null
        );
    }

    void UpdateQuestInfo()
    {
        if (questNameText != null) questNameText.text = currentQuest.questName;
        if (biomeText != null) biomeText.text = currentQuest.biome;
        if (biomeIcon != null && currentQuest.biomeIcon != null)
            biomeIcon.sprite = currentQuest.biomeIcon;
    }

    void UpdatePartyStatus()
    {
        if (partyStatusContainer == null || partyStatusPrefab == null) return;

        UIUtil.ClearChildrenNow(partyStatusContainer);

        foreach (var hero in currentParty)
        {
            GameObject statusObj = Instantiate(partyStatusPrefab, partyStatusContainer);
            SetupPartyStatusCard(statusObj, hero);
        }
    }

    void SetupPartyStatusCard(GameObject card, HeroData hero)
    {
        TMP_Text nameText = card.transform.Find("Name")?.GetComponent<TMP_Text>();
        TMP_Text hpText = card.transform.Find("HP")?.GetComponent<TMP_Text>();
        Image hpBar = card.transform.Find("HPBar/Fill")?.GetComponent<Image>();
        TMP_Text stressText = card.transform.Find("Stress")?.GetComponent<TMP_Text>();
        Image stressBar = card.transform.Find("StressBar/Fill")?.GetComponent<Image>();
        TMP_Text stateText = card.transform.Find("State")?.GetComponent<TMP_Text>();

        // A posição na formação acompanha o grupo pela jornada inteira: é a mesma
        // ordem que o combate vai usar, e o jogador precisa vê-la antes da luta.
        if (nameText != null)
        {
            if (hero.isDead)
            {
                nameText.text = hero.heroName;
            }
            else
            {
                int position = PartyFormation.GetPosition(hero, currentParty) + 1;
                bool front = PartyFormation.GetRow(hero, currentParty) == FormationRow.Front;
                string aviso = PartyFormation.IsWellPlaced(hero, currentParty) ? "" : " <color=#B04040>⚠️</color>";

                nameText.text = $"<color=#8CB8F0>{position}{(front ? "⚔️" : "🏹")}</color> {hero.heroName}{aviso}";
            }
        }

        if (hero.isDead)
        {
            if (hpText != null) hpText.text = "💀 MORTO";
            if (hpBar != null) hpBar.fillAmount = 0;
            if (stressBar != null) stressBar.fillAmount = 0;
            if (stressText != null) stressText.text = "";
            if (stateText != null) stateText.text = "";
            return;
        }

        if (hpText != null)
        {
            hpText.text = hero.isOnDeathsDoor
                ? "☠️ BEIRA DA MORTE"
                : $"{hero.currentHp}/{hero.maxHp}";
        }

        if (hpBar != null)
            hpBar.fillAmount = hero.maxHp > 0 ? (float)hero.currentHp / hero.maxHp : 0f;

        if (stressText != null) stressText.text = $"🧠 {Mathf.RoundToInt(hero.stress)}";

        if (stressBar != null)
        {
            stressBar.fillAmount = hero.stress / 100f;
            stressBar.color = hero.stress >= 100f ? new Color(0.8f, 0.1f, 0.1f)
                            : hero.stress >= 50f ? new Color(0.85f, 0.6f, 0.1f)
                            : new Color(0.4f, 0.5f, 0.7f);
        }

        if (stateText != null)
        {
            if (hero.mentalState == MentalState.Normal)
            {
                stateText.text = hero.isInjured ? "🩸 Ferido" : "";
            }
            else
            {
                bool virtue = MentalStateUtil.IsVirtue(hero.mentalState);
                string color = virtue ? "#5FA85F" : "#B04040";
                stateText.text = $"<color={color}>{MentalStateUtil.GetLabel(hero.mentalState)}</color>";
            }
        }
    }

    IEnumerator TypeText(string text, TMP_Text target)
    {
        target.text = "";
        foreach (char c in text)
        {
            target.text += c;
            yield return new WaitForSeconds(textTypeSpeed);
        }
    }

    IEnumerator DelayedNextEvent()
    {
        yield return new WaitForSeconds(eventTransitionDelay);

        // A jornada pode ter acabado durante a espera.
        if (journeyEnded) yield break;

        NextEvent();
    }

    public System.Action<bool, int> OnJourneyComplete;
}