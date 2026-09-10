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

    [Header("A estrada")]
    /// <summary>
    /// A ficha do grupo no mapa. Opcional de propósito: sem a arte do SPUM a
    /// jornada continua jogável, só que sem os corpos.
    /// </summary>
    public TrailRoadUI trailRoad;

    /// <summary>
    /// A caixa do evento, que flutua sobre o mapa. Só aparece quando o grupo
    /// pára em algum lugar — enquanto ele anda, o mapa fica limpo.
    /// </summary>
    public GameObject eventBox;

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

    [Header("Experiência")]
    [Tooltip("XP por concluir a missão, antes dos ajustes.")]
    public int xpBaseDaJornada = 60;

    [Tooltip("XP somado por trecho percorrido.")]
    public int xpPorDia = 8;

    [Tooltip("Quanto a corrupção da região soma ao XP, no máximo (0,5 = +50% em corrupção 100).")]
    public float xpBonusMaximoDeCorrupcao = 0.5f;

    [Tooltip("Fração do XP recebida quando a jornada fracassa.")]
    [Range(0f, 1f)] public float xpDoFracasso = 0.4f;

    [Tooltip("Fração do XP recebida pelos heróis além dos quatro primeiros da formação.")]
    [Range(0f, 1f)] public float xpDosHeroisExtras = 0.5f;

    [Header("Retorno")]
    [Tooltip("Estresse aliviado por jornada nos heróis que ficaram na guilda.")]
    public float descansoNaGuilda = 20f;

    /// <summary>Tamanho de grupo em que o XP começa a render menos — o mesmo
    /// limite a partir do qual a party passa a comer uma ração a mais por dia.</summary>
    private const int PartySemPenalidade = 4;

    private QuestData currentQuest;

    /// <summary>
    /// A região que o grupo está atravessando. <c>Any</c> quando não há jornada
    /// em curso — quem desenha o mapa precisa saber o terreno, e a missão em si
    /// continua privada.
    /// </summary>
    public BiomeType CurrentBiome => currentQuest != null ? currentQuest.biomeType : BiomeType.Any;
    private List<HeroData> currentParty;
    private JourneyMap journeyMap;
    private int currentDay = 0;
    private int totalDays = 0;
    private bool isWaitingForChoice = false;
    private EventData currentEvent;

    // Entre resolver um evento e entrar no próximo, o grupo escolhe por onde seguir.
    private bool isChoosingRoute = false;

    /// <summary>A dica de bifurcação já foi dada nesta jornada.</summary>
    private bool avisouDaBifurcacao = false;

    /// <summary>O grupo está atravessando um trecho neste instante.</summary>
    private bool caminhando = false;

    /// <summary>
    /// O grupo está a caminho de algum lugar — nada a decidir por enquanto.
    ///
    /// Existe para quem dirige a jornada de fora (o teste) saber a diferença
    /// entre "esperando o jogador" e "no meio de uma animação". Sem isso, o
    /// probe gastava o orçamento de iterações dele nos frames da caminhada e
    /// acusava travamento numa jornada que estava andando normalmente.
    /// </summary>
    public bool EmTravessia => caminhando;

    /// <summary>O mapa só aceita cliques enquanto a rota está sendo escolhida.</summary>
    public bool IsChoosingRoute => isChoosingRoute && !journeyEnded;

    /// <summary>
    /// O grupo está na estrada?
    ///
    /// É o que o save consulta antes de gravar. O estado de dentro da jornada —
    /// mapa, dia, mão, descarte, mitigação — vive em campos privados daqui e não
    /// é salvo por ninguém; gravar no meio devolveria o jogador à guilda com a
    /// missão sumida do quadro e a party fora de casa.
    /// </summary>
    public bool EmJornada => currentQuest != null && !journeyEnded;

    /// <summary>
    /// Quem está na estrada, na ordem da formação. Só leitura: quem quiser
    /// mexer na party passa pelos caminhos que atualizam a interface junto.
    /// </summary>
    public IReadOnlyList<HeroData> PartyAtual => currentParty;

    // Sem isto, as corrotinas de transição já agendadas continuam produzindo
    // eventos depois que a jornada acabou — a jornada nunca fechava.
    private bool journeyEnded = false;

    // Preparo acumulado com cartas antes de decidir: reduz o dano do desfecho escolhido.
    private float currentMitigation = 0f;
    private const float MaxMitigation = 0.75f;

    /// <summary>
    /// Efeitos de carta jogados neste trecho. É o que destrava as opções com
    /// requisito — a ponte entre o baralho e a decisão.
    /// </summary>
    private readonly HashSet<JourneyEffectType> playedThisEvent = new HashSet<JourneyEffectType>();

    // Baixas desta jornada, para o relatório final.
    private List<HeroData> journeyCasualties = new List<HeroData>();

    // Ouro tirado dos inimigos, separado do pagamento do contrato.
    private int combatGold = 0;

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
        GameAudio.Tocar(MusicContext.Journey);

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
        combatGold = 0;
        currentMitigation = 0f;
        journeyEnded = false;
        isChoosingRoute = false;
        caminhando = false;
        avisouDaBifurcacao = false;
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

        // Os corpos entram na ordem da formação — a mesma fila que decide quem
        // apanha no combate é a que o jogador vê marchando.
        if (trailRoad != null) trailRoad.Preparar(currentParty);

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

        if (resolutionLogText != null)
            resolutionLogText.text = "";

        UpdateDetourUI();
        JourneyMapUI.Instance?.Refresh(journeyMap, revealedEvents);

        // Escolher a rota é olhar o mapa: a caixa do evento sai da frente, e com
        // ela as cartas. O que o jogador precisa comparar são os pontos, e eles
        // ficavam metade cobertos pela caixa que falava do lugar anterior.
        MostrarParada(false);

        // Só na primeira bifurcação da jornada. O aviso ensina uma coisa que o
        // mapa já diz sozinho — os pontos alcançáveis são os únicos clicáveis —,
        // e repeti-lo em cada um dos seis ou sete cruzamentos da viagem é um
        // popup atravessando o mapa justamente quando se quer olhar para ele.
        //
        // Curto pelo mesmo motivo de sempre: a caminhada até o próximo ponto leva
        // mais de um segundo, e um aviso de 2,5s ainda estava na tela quando o
        // evento seguinte abria.
        if (!avisouDaBifurcacao)
        {
            avisouDaBifurcacao = true;
            UIManager.Instance?.ShowMessage("A rota se divide — escolha para onde o grupo segue.", 1.5f);
        }
    }

    /// <summary>Chamado pelo mapa quando o jogador escolhe um nó alcançável.</summary>
    public void OnNodeChosen(int nodeId)
    {
        if (journeyEnded || !isChoosingRoute) return;

        EnterNode(nodeId);
    }

    void EnterNode(int nodeId)
    {
        if (journeyMap == null || journeyEnded) return;

        // Uma travessia de cada vez. A caminhada leva mais de um segundo, e
        // nesse intervalo o fluxo continua vivo: um segundo pedido de rota
        // chegando no meio moveria o grupo duas vezes e abriria dois eventos
        // para o mesmo dia.
        if (caminhando) return;

        if (!journeyMap.MoveTo(nodeId))
        {
            // Sair calado daqui trava a jornada para sempre: ninguém mais chama
            // NextEvent, e a tela fica esperando um clique que não resolve nada.
            Debug.LogWarning($"JourneyManager: nó {nodeId} não é alcançável a partir daqui — refazendo a escolha de rota.");
            ShowRouteChoice();
            return;
        }

        isChoosingRoute = false;
        StartCoroutine(IrAte());
    }

    /// <summary>
    /// A travessia de um trecho: o grupo anda até o ponto e só então o que há
    /// lá aparece.
    ///
    /// É aqui que a jornada deixa de ser uma sequência de telas de texto. O
    /// evento não abre no clique — abre na chegada, e enquanto se anda o mapa
    /// fica sem caixa e sem cartas na frente.
    /// </summary>
    IEnumerator IrAte()
    {
        caminhando = true;

        // A jornada em que esta travessia nasceu. Se outra começar durante a
        // caminhada, o mapa é outro e o grupo é outro — seguir em frente aqui
        // faria o grupo novo entrar num ponto do mapa antigo, e o sintoma seria
        // um "nó do mapa sem evento associado" sem causa aparente.
        JourneyMap mapaDaVez = journeyMap;
        int noDaVez = journeyMap != null ? journeyMap.currentNodeId : -1;

        MostrarParada(false);

        if (JourneyMapUI.Instance != null)
        {
            JourneyMapUI.Instance.Refresh(journeyMap, revealedEvents);
            yield return JourneyMapUI.Instance.Caminhar(noDaVez);
        }

        caminhando = false;

        // A jornada pode ter acabado no meio do caminho (o jogador abandonou,
        // ou uma corrotina de transição fechou tudo): entrar no nó agora
        // reabriria uma jornada encerrada.
        if (journeyEnded || journeyMap != mapaDaVez) yield break;

        EnterNodeInternal();
    }

    /// <summary>
    /// O grupo está parado em algum lugar? A caixa do evento e a mão de cartas
    /// aparecem juntas, porque as cartas só têm efeito quando há o que resolver.
    /// </summary>
    void MostrarParada(bool parado)
    {
        if (eventBox != null && eventBox.activeSelf != parado)
            eventBox.SetActive(parado);

        if (handContainer != null && handContainer.gameObject.activeSelf != parado)
            handContainer.gameObject.SetActive(parado);

        // Quem está andando não descansa nem desvia. O EndTurn já era inócuo
        // fora da parada, mas um botão que aceita clique e não faz nada é pior
        // que um desligado: o jogador conclui que o jogo travou.
        if (endTurnButton != null) endTurnButton.interactable = parado;

        if (detourButton != null)
        {
            if (parado) UpdateDetourUI();
            else detourButton.interactable = false;
        }
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
        playedThisEvent.Clear();

        if (dayText != null)
            dayText.text = $"Dia {currentDay} / {(journeyMap != null ? journeyMap.LayerCount : totalDays)}";

        if (eventTitleText != null)
            eventTitleText.text = eventData.eventTitle;

        if (eventDescriptionText != null)
            StartCoroutine(TypeText(eventData.description, eventDescriptionText));

        if (resolutionLogText != null)
            resolutionLogText.text = "";

        // Chegou a algum lugar: o grupo pára para resolver o que encontrou.
        //
        // A parada é ligada <b>antes</b> de montar a mão: o leque se refaz no
        // OnEnable do container, e criar as cartas com ele desligado deixaria a
        // primeira leva sem posição até o frame seguinte.
        if (trailRoad != null) trailRoad.Andar(false);
        MostrarParada(true);

        // Atualiza UI das cartas e as opções deste evento
        UpdateCardUI();
        BuildChoices(eventData);
        UpdateResourceUI();
        UpdateDetourUI();
        UpdateUpcomingEvents();
        JourneyMapUI.Instance?.Refresh(journeyMap, revealedEvents);
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

            if (!captured.RequiresCard)
            {
                CreateChoiceButton(captured.optionText, () => ChooseOutcome(captured));
                continue;
            }

            bool destravada = playedThisEvent.Contains(captured.requiredEffect);
            string requisito = JourneyEffectUtil.GetLabel(captured.requiredEffect);

            if (destravada)
            {
                string rotulo = $"<color=#D9B85A>◆</color> {captured.optionText}";
                if (!string.IsNullOrEmpty(captured.empoweredText))
                    rotulo += $"\n<size=80%><color=#D9B85A>{captured.empoweredText}</color></size>";

                CreateChoiceButton(rotulo, () => ChooseOutcome(captured));
            }
            else
            {
                // A opção travada não some: o jogador precisa ver o que perdeu
                // por não ter trazido a carta certa. É o que faz a preparação
                // pesar na próxima jornada.
                string rotulo = $"<color=#7A756B>🔒 {captured.optionText}\n"
                              + $"<size=80%>Precisa de uma carta capaz de {requisito}</size></color>";

                CreateChoiceButton(rotulo, () => UIManager.Instance?.ShowMessage(
                    $"Sem uma carta capaz de {requisito}, esse caminho está fechado.", 2.5f), false);
            }
        }

        // O layout só redistribui a altura no frame seguinte; sem forçar agora,
        // um evento de quatro opções aparece por um quadro transbordando sobre
        // o status do grupo.
        var rect = choiceContainer as RectTransform;
        if (rect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
    }

    /// <param name="habilitada">
    /// Opções travadas continuam clicáveis de propósito: o clique explica o que
    /// falta, em vez de o botão ficar cinza e mudo.
    /// </param>
    void CreateChoiceButton(string label, UnityEngine.Events.UnityAction action, bool habilitada = true)
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

            if (!habilitada)
            {
                var cores = btn.colors;
                cores.normalColor = new Color(0.16f, 0.15f, 0.14f);
                cores.highlightedColor = new Color(0.20f, 0.19f, 0.17f);
                btn.colors = cores;
            }
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
        // Espólio das lutas, para o balanço final poder discriminá-lo do
        // pagamento do contrato.
        if (victory && CombatManager.Instance != null)
            combatGold += CombatManager.Instance.LastCombatReward;

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

        // Guarda contra o botão travado: a opção com requisito só resolve se a
        // carta tiver sido jogada.
        if (outcome.RequiresCard && !playedThisEvent.Contains(outcome.requiredEffect))
        {
            UIManager.Instance?.ShowMessage(
                $"Sem uma carta capaz de {JourneyEffectUtil.GetLabel(outcome.requiredEffect)}, "
                + "esse caminho está fechado.", 2.5f);
            return;
        }

        isWaitingForChoice = false;

        EventOutcome aplicado = ComDesfechoReforcado(outcome);
        EventResolver.Resolution resolution = EventResolver.Resolve(aplicado, currentParty, currentMitigation);

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

        ProcurarAchado();

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

        StartCoroutine(DepoisDoEvento(resolution));
    }

    /// <summary>
    /// A pausa entre um evento e o próximo, com o momento da quebra no meio.
    ///
    /// A quebra vem antes do próximo evento, e não junto: é a estrada parando
    /// para mostrar o que acabou de acontecer com alguém do grupo. Emendar o
    /// evento seguinte por cima devolveria o que havia — uma linha de log que
    /// some antes de ser lida.
    /// </summary>
    IEnumerator DepoisDoEvento(EventResolver.Resolution resolution)
    {
        var quebrados = new List<HeroData>();

        if (resolution != null)
            quebrados.AddRange(resolution.newlyAfflicted);

        foreach (var doTrecho in quebradosNoTrecho)
            if (!quebrados.Contains(doTrecho)) quebrados.Add(doTrecho);

        quebradosNoTrecho.Clear();

        if (quebrados.Count > 0)
            yield return AfflictionMoment.MostrarTodos(quebrados);

        yield return DelayedNextEvent();
    }

    /// <summary>
    /// O que se acha no chão depois de um evento resolvido.
    ///
    /// <b>Frascos, nunca relíquias.</b> A relíquia é permanente e está presa ao
    /// risco: ela vem do chefe, do despojo escolhido ou do Mercado pago. Um
    /// evento de estrada acontece muitas vezes por jornada, e largar relíquia
    /// aqui encheria os slots de todo mundo antes do segundo ciclo, apagando a
    /// escolha de quem leva o quê.
    ///
    /// Vai para a prateleira, e não para a mochila de alguém: no meio da estrada
    /// não há tela para escolher quem carrega, e o frasco achado no dia 3 só
    /// serviria à jornada seguinte de qualquer forma.
    /// </summary>
    void ProcurarAchado()
    {
        // Um a cada oito eventos. Com ~22 eventos resolvidos por jornada no
        // teste, é da ordem de dois frascos por viagem — bem menos do que se
        // gasta, que é o que mantém o frasco valendo alguma coisa.
        if (Random.value > 0.12f) return;

        var guilda = GuildManager.Instance;
        if (guilda == null) return;

        var achado = ItemCatalog.Pocoes[Random.Range(0, ItemCatalog.Pocoes.Count)];
        guilda.GuardarPocao(achado.id);

        if (resolutionLogText != null)
            resolutionLogText.text += $"\n🧪 Entre os destroços: {achado.nome}.";
    }

    /// <summary>
    /// Troca as consequências pelo desfecho reforçado quando a carta exigida foi
    /// jogada. Devolve o próprio desfecho quando não há versão reforçada — a
    /// carta então apenas destrava a opção, sem melhorá-la.
    /// </summary>
    EventOutcome ComDesfechoReforcado(EventOutcome outcome)
    {
        if (outcome == null || !outcome.RequiresCard) return outcome;
        if (outcome.empoweredConsequences == null) return outcome;
        if (!playedThisEvent.Contains(outcome.requiredEffect)) return outcome;

        // <b>Reforço vazio não substitui nada.</b> O campo é uma classe
        // serializada: o Unity a instancia sempre, então "não preenchido" chega
        // aqui como um desfecho zerado — e a troca cega apagava as consequências
        // boas da opção. Medido em 21/08: as 25 opções que exigem carta estavam
        // assim, e jogar a carta trocava, por exemplo, +10 de vida no grupo
        // inteiro por absolutamente nada.
        //
        // Enquanto os desfechos reforçados não forem escritos, a carta faz o que
        // sempre disse fazer: destrava o caminho, sem piorá-lo.
        if (Vazio(outcome.empoweredConsequences)) return outcome;

        // Cópia rasa: o asset do evento não pode ser alterado em runtime, ou a
        // mudança gruda no ScriptableObject e vaza para a próxima jornada.
        return new EventOutcome
        {
            optionText = outcome.optionText,
            consequences = outcome.empoweredConsequences,
            extraDays = outcome.extraDays,
            triggersCorruption = outcome.triggersCorruption,
            requiredEffect = outcome.requiredEffect,
            empoweredText = outcome.empoweredText
        };
    }

    /// <summary>
    /// Este desfecho não faz nada com ninguém?
    ///
    /// Serve para distinguir "reforço não escrito" de "reforço que existe": o
    /// primeiro precisa ser ignorado, o segundo é o prêmio da carta.
    /// </summary>
    static bool Vazio(EventConsequences c)
    {
        if (c == null) return true;

        if (c.goldChange != 0 || c.reputationChange != 0) return false;

        if (c.heroEffects != null)
            foreach (var e in c.heroEffects)
                if (e != null && (e.hpChange != 0 || e.addInjury || e.addTrait)) return false;

        if (c.moraleChanges != null)
            foreach (var m in c.moraleChanges)
                if (m != null && m.moraleChange != 0) return false;

        return true;
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

            case JourneyEffectType.Revive:
            {
                // Prioridade para quem está na Beira da Morte: é lá que a carta
                // decide se alguém volta para casa. Sem ninguém à beira, socorre
                // o mais ferido.
                HeroData alvo = currentParty.FirstOrDefault(h => h.IsAlive && h.isOnDeathsDoor)
                             ?? currentParty.Where(h => h.IsAlive)
                                            .OrderBy(h => h.currentHp / (float)Mathf.Max(1, h.maxHp))
                                            .FirstOrDefault();

                if (alvo == null) break;

                bool estavaNaBeira = alvo.isOnDeathsDoor;
                int cura = Mathf.Max(1, Mathf.RoundToInt(alvo.maxHp * 0.5f));

                alvo.isOnDeathsDoor = false;
                alvo.currentHp = Mathf.Min(alvo.maxHp, alvo.currentHp + cura);
                alvo.stress = Mathf.Max(0f, alvo.stress - 20f);

                UIManager.Instance?.ShowMessage(
                    estavaNaBeira
                        ? $"{alvo.heroName} foi trazido de volta da beira da morte!"
                        : $"{alvo.heroName} recupera o fôlego (+{cura} HP).", 2.5f);
                break;
            }

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

            // Find("Image") pegava o primeiro filho com esse nome, e o prefab tem
            // quatro — a arte ia parar num enfeite de canto do texto.
            CardUI.AplicarArte(cardObj, card);
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

        // O que foi jogado pode destravar uma opção do evento, então as escolhas
        // são redesenhadas: é aqui que o baralho entra na decisão.
        bool destravouAlgo = playedThisEvent.Add(card.journeyEffect);

        cardManager.PlayCard(card);

        UpdatePartyStatus();
        UpdateCardUI();
        UpdateResourceUI();

        if (destravouAlgo && currentEvent != null)
            BuildChoices(currentEvent);
    }

    /// <summary>Teto de preparo acumulável antes de decidir.</summary>
    public static float MaxMitigationValue => MaxMitigation;

    /// <summary>
    /// Quanto cada tipo de carta protege o grupo do desfecho do evento.
    ///
    /// Estático e público para o simulador de balanceamento chamar esta regra em
    /// vez de manter uma cópia: foi exatamente esse tipo de duplicata que fez o
    /// simulador de combate medir um jogo que não existia.
    /// </summary>
    public static float GetMitigationFor(CardData card)
    {
        if (card == null) return 0f;

        switch (card.journeyEffect)
        {
            case JourneyEffectType.RemoveObstacle:
            case JourneyEffectType.ProtectFromWeather:
            case JourneyEffectType.Intimidate:
                return 0.35f;

            case JourneyEffectType.Purify:
            case JourneyEffectType.HealInjury:
            case JourneyEffectType.Teleport:
            case JourneyEffectType.Revive:
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

    /// <summary>
    /// Primeira opção SEM requisito de carta, ou uma saída neutra. A saída de
    /// emergência não pode escolher um caminho que o jogador não destravou.
    /// </summary>
    EventOutcome GetFallbackOutcome(EventData eventData)
    {
        if (eventData != null && eventData.outcomes != null)
        {
            EventOutcome livre = eventData.outcomes.FirstOrDefault(o => o != null && !o.RequiresCard);
            if (livre != null) return livre;
        }

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

        // A barra pode ter enchido aqui, e não num evento. Sem esta linha, quem
        // chegava aos 100 andando no escuro terminava a jornada em "estresse 100,
        // Estável": a quebra simplesmente não acontecia por este caminho.
        EventResolver.ResolveStressBreakpoints(currentParty, upkeep);

        foreach (var deadHero in upkeep.died)
            journeyCasualties.Add(deadHero);

        // Guardado para o momento da quebra, que só pode rodar quando a estrada
        // parar — aqui ainda estamos no meio do consumo de um trecho.
        foreach (var quebrado in upkeep.newlyAfflicted)
            if (!quebradosNoTrecho.Contains(quebrado)) quebradosNoTrecho.Add(quebrado);

        if (upkeep.lines.Count > 0)
            Debug.Log($"[Manutenção diária]\n{upkeep.ToText()}");
    }

    /// <summary>
    /// Quem quebrou na manutenção diária desde o último evento resolvido.
    ///
    /// A manutenção roda no meio do avanço — às vezes várias vezes seguidas, num
    /// desvio de rota — e não é hora de parar a tela. O momento sai quando o
    /// trecho termina.
    /// </summary>
    readonly List<HeroData> quebradosNoTrecho = new List<HeroData>();

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
        caminhando = false;

        // A estrada acabou: o palco sai junto, com a câmera e a textura dele.
        if (trailRoad != null) trailRoad.Desmontar();

        int survivors = currentParty.Count(h => !h.isDead);
        int contrato = success ? currentQuest.GetTotalReward(totalDays) : currentQuest.baseReward / 2;
        int porSobreviventes = survivors * 25;

        int reward = contrato + porSobreviventes;
        int bonusBiblioteca = 0;

        // Aplica bônus de ouro da biblioteca.
        //
        // Sem popup: o aviso disparava um instante antes da tela de balanço, que
        // discrimina "Biblioteca: +N" na lista de recompensas. Era a mesma linha
        // duas vezes, e a segunda por cima da primeira.
        if (goldBonus > 0)
        {
            int bonusReward = Mathf.RoundToInt(reward * goldBonus);
            bonusBiblioteca = bonusReward;
            reward += bonusReward;
        }

        GuildManager.Instance.AddGold(reward);
        GuildManager.Instance.AddReputation(success ? 10 : -5);

        int xpDaJornada = CalcularXpDaJornada(success);
        var promovidos = new List<string>();

        // O balanço é montado aqui porque é o último instante em que a party
        // ainda existe inteira: logo abaixo os mortos saem do roster.
        var report = new JourneyReport
        {
            success = success,
            questName = currentQuest != null ? currentQuest.questName : "Jornada",
            daysTraveled = currentDay,
            sobreviventes = survivors,
            mortos = currentParty.Count(h => h.isDead),
            recompensaBase = contrato,
            recompensaSobreviventes = porSobreviventes,
            recompensaCombates = combatGold,
            recompensaBonus = bonusBiblioteca,
            recompensaTotal = reward + combatGold,
            reputacao = success ? 10 : -5
        };

        for (int i = 0; i < currentParty.Count; i++)
        {
            HeroData hero = currentParty[i];

            var linha = new JourneyReport.HeroLine
            {
                nome = hero.heroName,
                maxHp = hero.maxHp,
                nivelAntes = hero.level,
                morreu = hero.isDead
            };

            if (hero.isDead)
            {
                linha.nivelDepois = hero.level;
                linha.estadoMental = MentalStateUtil.GetLabel(hero.mentalState);
                report.herois.Add(linha);

                GuildManager.Instance.RegisterDeath(hero);
                continue;
            }

            // O retorno alivia o corpo, mas não apaga o que a jornada deixou na
            // cabeça. Quem voltou melhor que 60% não é rebaixado a 60%: o descanso
            // é piso de recuperação, não teto.
            hero.isOnDeathsDoor = false;
            hero.currentHp = Mathf.Clamp(
                Mathf.Max(hero.currentHp, Mathf.RoundToInt(hero.maxHp * 0.6f)),
                1, hero.maxHp);
            hero.stress = Mathf.Max(0f, hero.stress - 15f);
            hero.morale = Mathf.Min(hero.morale + (success ? 20f : 5f), 100f);

            // Ferimento não sara mais por sorteio. Quem tem Recuperação Rápida se
            // vira sozinho; o resto volta ferido e precisa de bandagem no Mercado.
            // Antes, 40% de chance apagava o ferimento no caminho de volta — o
            // machucado sumia sem que ninguém cuidasse dele, e a Forja e o
            // Mercado ficavam sem razão de existir entre uma jornada e outra.
            if (hero.isInjured && hero.trait == Trait.FastHealer)
                hero.isInjured = false;

            // Luto: quem viu companheiro cair volta pior do que os números de
            // combate sozinhos explicariam.
            if (journeyCasualties.Count > 0)
            {
                hero.morale = Mathf.Max(0f, hero.morale - journeyCasualties.Count * 8f);
                EventResolver.AddStress(hero, journeyCasualties.Count * 6f,
                                        new EventResolver.Resolution());
            }

            // A ordem da party é a formação: os quatro primeiros são a expedição
            // de fato, e quem vai além disso divide a experiência com a multidão.
            int xpDoHeroi = i < PartySemPenalidade
                ? xpDaJornada
                : Mathf.RoundToInt(xpDaJornada * xpDosHeroisExtras);

            int niveis = hero.AddXp(xpDoHeroi);
            if (niveis > 0)
                promovidos.Add($"{hero.heroName} → Nv.{hero.level}");

            linha.hp = hero.currentHp;
            linha.maxHp = hero.maxHp;
            linha.estresse = Mathf.RoundToInt(hero.stress);
            linha.ferido = hero.isInjured;
            linha.nivelDepois = hero.level;
            linha.xpGanho = xpDoHeroi;
            linha.xpAtual = hero.xp;
            linha.xpMeta = hero.XpMetaAtual;
            linha.xpProgresso = hero.XpProgress;
            linha.estadoMental = MentalStateUtil.GetLabel(hero.mentalState);
            linha.aflicao = MentalStateUtil.IsAffliction(hero.mentalState);
            linha.virtude = MentalStateUtil.IsVirtue(hero.mentalState);

            report.herois.Add(linha);
        }

        // Quem ficou na guilda descansou enquanto os outros apanhavam.
        //
        // Sem isto, o esgotamento seria um beco sem saída: o herói acima do
        // limite não pode partir, e só partir aliviava o estresse. A guilda
        // trabalhando é o que faz o tempo passar para quem está em casa.
        DescansarQuemFicou();

        // A missão sai do quadro e o quadro se repõe.
        if (QuestManager.Instance != null)
            QuestManager.Instance.CompleteQuest(currentQuest);

        if (TavernManager.Instance != null)
            TavernManager.Instance.RefreshRecruits();

        // O pulso da run: a jornada acabou, então o mundo apodrece um pouco e as
        // condições de fim são conferidas. Vencer o Chefe Supremo é a única
        // vitória — e é conferido antes de avançar o ciclo, para a run não
        // terminar por Corrupção no mesmo instante em que foi ganha.
        var run = RunManager.Instance;
        if (run != null)
        {
            if (success && currentQuest != null && currentQuest.isFinalBoss)
                run.ReportBossDefeated();
            else
                run.AdvanceCycle(journeyCasualties.Count);

            // O que a expedição traz de volta, na ordem em que importa.
            //
            // Selar antes de corromper não é detalhe: a região selada não
            // apodrece mais, e a visita que a selou seria a última a sujá-la —
            // o grupo cobraria o preço da travessia depois de ter fechado o
            // lugar.
            if (currentQuest != null)
            {
                var regiao = currentQuest.biomeType;

                if (success && currentQuest.isRegionBoss)
                    report.regiaoSelada = RegionMap.Selar(regiao);

                report.mapaCompletado = RegionMap.Mapear(
                    regiao,
                    success ? RegionMap.MapeamentoPorExpedicao : RegionMap.MapeamentoPorFracasso);

                report.mapeamento = RegionMap.FracaoMapeada(regiao);
                report.regiao = regiao;

                // A região atravessada fica pior do que estava. É o que faz o
                // mapa responder ao que o jogador fez, e não só ao tempo
                // passando: voltar sempre ao mesmo lugar seguro cobra um preço
                // visível ali.
                RegionMap.Corromper(regiao, RegionMap.CorrupcaoPorVisita);
            }
        }

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

        if (promovidos.Count > 0)
        {
            resultMessage += "\n\n📈 <color=#60A060>Subiram de nível:</color>";
            foreach (var promocao in promovidos)
                resultMessage += $"\n• {promocao}";
        }

        if (success)
            MontarRecompensas(report, reward);

        System.Action voltarParaGuilda = () =>
        {
            if (journeyPanel != null) journeyPanel.SetActive(false);
            UIManager.Instance?.ShowGuildScreen();
        };

        // A tela de balanço é a saída preferida; o popup de texto continua como
        // rede de segurança para cenas montadas antes dela existir.
        if (JourneyResultUI.Instance != null)
            JourneyResultUI.Instance.Mostrar(report, voltarParaGuilda);
        else
            UIManager.Instance?.ShowResult(
                success ? "🏆 Vitória!" : "💀 Derrota",
                resultMessage,
                voltarParaGuilda);

        OnJourneyComplete?.Invoke(success, reward);
       // LibraryManager.Instance?.ClearAllKnowledges();
    }

    /// <summary>
    /// O despojo que o jogador escolhe ao voltar.
    ///
    /// Cada opção conversa com um sistema que já existe e cobra caro em outro
    /// lugar: ouro é o que compra tudo; o descanso é o que o vinho do Mercado
    /// vende a 55 por herói; o tratamento é a bandagem a 90. Escolher uma é
    /// dizer qual dívida da jornada dói mais — e é isso que faz a decisão pesar.
    /// </summary>
    void MontarRecompensas(JourneyReport report, int ouroDaMissao)
    {
        var sobreviventes = currentParty.Where(h => h.IsAlive).ToList();
        if (sobreviventes.Count == 0) return;

        // 1. Bolso cheio: sempre disponível, é a régua contra a qual as outras
        //    opções são medidas.
        int extra = Mathf.Max(40, Mathf.RoundToInt(ouroDaMissao * 0.35f));
        report.recompensas.Add(new JourneyReport.Reward
        {
            titulo = $"💰 Espólio extra (+{extra})",
            descricao = "Vender o que deu para carregar.",
            confirmacao = $"A guilda leva mais {extra} de ouro.",
            aplicar = () => GuildManager.Instance?.AddGold(extra)
        });

        // 2. Descanso: vale mais quanto pior o grupo voltou.
        int estressados = sobreviventes.Count(h => h.stress >= 40f);
        if (estressados > 0)
        {
            const float alivio = 30f;
            report.recompensas.Add(new JourneyReport.Reward
            {
                titulo = $"🍷 Noite na taverna (−{Mathf.RoundToInt(alivio)} de estresse)",
                descricao = estressados == 1
                    ? "Um herói bebe até esquecer."
                    : $"Alivia {estressados} heróis abalados.",
                confirmacao = "A bebida corre solta — e a estrada fica para amanhã.",
                aplicar = () =>
                {
                    foreach (var h in sobreviventes)
                        h.stress = Mathf.Max(0f, h.stress - alivio);
                }
            });
        }

        // 3. Tratamento: só aparece se alguém precisa, e é a única coisa que
        //    apaga um ferimento de graça agora que ele não sara sozinho.
        var feridos = sobreviventes.Where(h => h.isInjured).ToList();
        if (feridos.Count > 0)
        {
            report.recompensas.Add(new JourneyReport.Reward
            {
                titulo = $"⚕️ Cuidados do curandeiro ({feridos.Count} ferido(s))",
                descricao = "Trata os ferimentos que voltaram da estrada.",
                confirmacao = "Os ferimentos foram tratados.",
                aplicar = () =>
                {
                    foreach (var h in feridos)
                    {
                        h.isInjured = false;
                        h.currentHp = Mathf.Min(h.maxHp, h.currentHp + Mathf.RoundToInt(h.maxHp * 0.25f));
                    }
                }
            });
        }

        // 4. Reputação: o caminho lento, para quem pensa na guilda e não na bolsa.
        report.recompensas.Add(new JourneyReport.Reward
        {
            titulo = "⭐ Contar a história (+15 de reputação)",
            descricao = "O feito corre as tavernas do reino.",
            confirmacao = "A fama da guilda cresce.",
            aplicar = () => GuildManager.Instance?.AddReputation(15)
        });

        // 5. O achado da estrada: uma relíquia, escolhida contra o ouro que ela
        //    valeria. É a opção que constrói o herói em vez de pagar as contas
        //    da guilda — a única aqui cujo efeito atravessa jornadas.
        var achado = ItemCatalog.Reliquias[Random.Range(0, ItemCatalog.Reliquias.Count)];
        report.recompensas.Add(new JourneyReport.Reward
        {
            titulo = $"🏺 {achado.nome}",
            descricao = achado.descricao,
            confirmacao = $"{achado.nome} vai para a prateleira da guilda.",
            aplicar = () => GuildManager.Instance?.GuardarReliquia(achado.id)
        });
    }

    /// <summary>
    /// Alívio para os heróis do roster que não foram nesta jornada.
    ///
    /// Descansar em casa cura menos que o cuidado pago do Mercado ou a vigília
    /// do Cemitério — a diferença é que é de graça e sempre acontece. É a
    /// válvula que impede a guilda de travar com todo mundo esgotado.
    /// </summary>
    void DescansarQuemFicou()
    {
        if (GuildManager.Instance == null) return;

        foreach (var hero in GuildManager.Instance.roster)
        {
            if (hero == null || hero.isDead) continue;
            if (currentParty != null && currentParty.Contains(hero)) continue;

            hero.stress = Mathf.Max(0f, hero.stress - descansoNaGuilda);
            hero.morale = Mathf.Min(100f, hero.morale + 5f);

            // Ferimento tratado com tempo, não com sorte: só cicatriza quem
            // passou uma jornada inteira fora da estrada.
            if (hero.isInjured && hero.stress < 40f && Random.value < 0.5f)
                hero.isInjured = false;
        }
    }

    /// <summary>
    /// Experiência que a jornada rende a cada sobrevivente da linha de frente.
    ///
    /// Paga pelo que foi enfrentado, não pelo relógio: cada trecho percorrido
    /// conta, e a corrupção da região multiplica o total — é ela que separa uma
    /// caminhada tranquila de uma expedição ao coração da praga. Fracassar ainda
    /// ensina alguma coisa, só que pouco.
    /// </summary>
    int CalcularXpDaJornada(bool success)
    {
        float total = xpBaseDaJornada + currentDay * xpPorDia;

        int corrupcao = currentQuest != null ? currentQuest.corruptionLevel : 0;
        total *= 1f + (Mathf.Clamp01(corrupcao / 100f) * xpBonusMaximoDeCorrupcao);

        if (!success)
            total *= xpDoFracasso;

        return Mathf.Max(1, Mathf.RoundToInt(total));
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
        // A região tinha nome e emoji, e mais nada: Pântano e Tundra eram a mesma
        // tela cinza. A arte da missão vem primeiro; o catálogo é o padrão do
        // bioma. Deserto e Vulcão não têm arte em pacote nenhum, e aí a imagem
        // some em vez de aparecer vazia.
        if (biomeIcon != null)
        {
            Sprite arte = currentQuest.biomeIcon;

            if (arte == null && BiomeArtCatalog.Instance != null)
                arte = BiomeArtCatalog.Instance.Para(currentQuest.biomeType);

            biomeIcon.sprite = arte;
            biomeIcon.enabled = arte != null;
        }
    }

    void UpdatePartyStatus()
    {
        // As barras sobre a cabeça leem o mesmo estado dos cards do rodapé, e
        // por isso se atualizam no mesmo lugar: dois caminhos separados para a
        // mesma informação acabam divergindo, e aí a tela mostra duas verdades.
        if (trailRoad != null) trailRoad.AtualizarEstado();

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

        // O rosto de quem está na estrada, atrás dos números e translúcido: o card
        // é pequeno e a informação vem primeiro. Morto esmaece — a fileira é o
        // elenco, e a baixa precisa saltar aos olhos sem depender de ler o texto.
        Image retrato = card.transform.Find("Portrait")?.GetComponent<Image>();
        if (retrato != null && hero.portrait != null)
        {
            retrato.sprite = hero.portrait;
            retrato.color = hero.isDead
                ? new Color(0.45f, 0.35f, 0.35f, 0.20f)
                : new Color(1f, 1f, 1f, 0.34f);
        }

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
        {
            hpBar.fillAmount = hero.maxHp > 0 ? (float)hero.currentHp / hero.maxHp : 0f;

            // A barra veste o sprite vermelho do kit: sangue é a cor dela. Só a
            // Beira da Morte escurece — ver a mesma regra no CombatManager.
            hpBar.color = hero.isOnDeathsDoor ? new Color(0.55f, 0.15f, 0.15f) : Color.white;
        }

        if (stressText != null) stressText.text = $"🧠 {Mathf.RoundToInt(hero.stress)}";

        if (stressBar != null)
        {
            stressBar.fillAmount = Mathf.Clamp01(hero.stress / 100f);
            stressBar.color = hero.stress >= 100f ? new Color(0.80f, 0.10f, 0.10f)
                            : hero.stress >= 75f ? new Color(0.85f, 0.65f, 0.25f)
                                                 : new Color(0.55f, 0.52f, 0.45f);
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