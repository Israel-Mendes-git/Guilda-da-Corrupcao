using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A preparação da jornada, em três passos: para onde ir, quem vai e com qual
/// baralho.
///
/// <b>Cada decisão mora num passo só.</b> O deck principal era escolhido em dois
/// lugares — o botão "USAR DECK" no card do passo 2 e o clique no card do passo 3
/// —, e os dois escreviam na mesma variável por caminhos diferentes: quem usava o
/// botão chegava ao passo 3 sem borda de seleção e sem a composição do baralho,
/// porque só o clique do passo 3 as produzia. Duas portas para a mesma escolha
/// não davam liberdade nenhuma; davam duas telas dizendo coisas diferentes sobre
/// o mesmo estado. A porta do passo 2 foi fechada.
///
/// Rejeitado: fechar a do passo 3 e ficar com o botão do passo 2. O passo 3 é
/// onde estão as cartas, o nível e o HP de cada herói — os números que fazem a
/// escolha do baralho ser uma escolha. No passo 2 ela seria um chute.
/// </summary>
public class QuestSelectionUI : MonoBehaviour
{
    public static QuestSelectionUI Instance;

    [Header("UI References")]
    // Step 1 - Quests
    public GameObject step1Panel;
    public Transform questListContainer;
    public GameObject questItemPrefab;
    public TMP_Text questDetailsText;
    public Button nextButton1;

    /// <summary>
    /// O mapa que substitui a lista de missões. Nasce em runtime dentro do
    /// passo 1 — ver <see cref="MontarMapaDeRegioes"/>.
    /// </summary>
    RegionMapUI regionMap;

    // Step 2 - Party (Heróis de Apoio)
    public GameObject step2Panel;
    public Transform partySelectionContainer;
    public GameObject partyMemberSelectPrefab;
    public TMP_Text partyCountText;
    public Button backButton2;
    public Button nextButton2;

    // Step 3 - Deck (Herói Principal)
    public GameObject step3Panel;
    public Transform deckSelectionContainer;
    public GameObject deckCardPrefab;
    public TMP_Text selectedDeckNameText;
    public TMP_Text teamSummaryText;
    public Button backButton3;
    public Button startJourneyButton;

    // Formação (opcional: sem estas referências a ordem de seleção ainda vale como formação)
    [Header("Formação")]
    public GameObject formationPanel;
    public Transform formationContainer;
    public TMP_Text formationHintText;

    // Provisões (opcional: se não ligado no Inspector, a jornada usa o padrão)
    [Header("Provisões")]
    public TMP_Text rationsBuyText;
    public TMP_Text torchesBuyText;
    public TMP_Text provisionsCostText;
    public Button rationsPlusButton;
    public Button rationsMinusButton;
    public Button torchesPlusButton;
    public Button torchesMinusButton;

    [Header("Provisões - Config")]
    // Dimensionadas por simulação (1000 jornadas) para a letalidade alvo: punitiva,
    // ~1 a 2 mortes a cada 3 jornadas. Com 8 rações e 4 tochas a conta não fechava
    // — 81% das jornadas passavam fome e a party morria inteira em 9% delas, o que
    // é aniquilação, não punição.
    //
    // <b>Desde 11/09 elas acompanham a viagem</b>, e o valor abaixo é só o ponto
    // de partida da tela. Com o plano navegável a duração passou a vir da
    // distância — média de 11 dias contra os 7 de antes —, e a base fixa de 10
    // rações virou fome: a letalidade medida foi de 0,56 para <b>2,59</b> mortes
    // por jornada, com 1,89 delas na estrada. A régua por dia é a mesma que foi
    // medida (10 rações e 8 tochas para 7 dias); o que mudou é que ela agora
    // multiplica pelos dias em vez de valer só para a jornada curta.
    public int baseRations = 10;
    public int baseTorches = 8;
    public int rationCost = 8;
    public int torchCost = 12;
    public int maxExtraRations = 12;
    public int maxExtraTorches = 8;

    // Bottom
    public TMP_Text goldText;
    public Button backButton;

    [Header("Raiz da tela")]
    [Tooltip("Painel que contém os passos. Se vazio, é deduzido do pai do step 1. " +
             "Nunca use o Canvas raiz aqui.")]
    public GameObject selectionRoot;

    private QuestData selectedQuest;
    private HeroData selectedMainHero;
    private List<HeroData> selectedParty = new List<HeroData>();
    private List<QuestData> availableQuests = new List<QuestData>();
    private GameObject currentSelectedDeckCard;

    private int extraRations;
    private int extraTorches;

    /// <summary>
    /// Card do passo 3 de cada herói, para conseguir marcar o baralho escolhido
    /// sem depender de o jogador ter clicado nele.
    /// </summary>
    private readonly Dictionary<HeroData, GameObject> deckCards = new Dictionary<HeroData, GameObject>();

    private int ProvisionsCost => extraRations * rationCost + extraTorches * torchCost;

    /// <summary>
    /// O que a guilda entrega para uma expedição de tantos dias.
    ///
    /// Estáticas porque o simulador as chama de fora do Play Mode, e ele precisa
    /// equipar o grupo exatamente como a tela equipa — medir uma jornada mais
    /// farta (ou mais faminta) que a real é o erro que já custou cinco
    /// conclusões invertidas neste projeto.
    /// </summary>
    public static int RacoesPara(int dias) => Mathf.CeilToInt(dias * 10f / 7f);

    public static int TochasPara(int dias) => Mathf.CeilToInt(dias * 8f / 7f);

    /// <summary>Os dias que a expedição deve durar, no meio da faixa.</summary>
    public static int DiasPrevistos(QuestData quest)
    {
        if (quest == null) return 7;
        return Mathf.Max(1, (quest.minDuration + quest.maxDuration) / 2);
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
        // Garante que todos os steps começam desativados
        if (step1Panel != null) step1Panel.SetActive(false);
        if (step2Panel != null) step2Panel.SetActive(false);
        if (step3Panel != null) step3Panel.SetActive(false);

        // Configura botões
        if (nextButton1 != null)
            nextButton1.onClick.AddListener(() => { if (selectedQuest != null) ShowStep2(); });

        if (nextButton2 != null)
            nextButton2.onClick.AddListener(() => { if (selectedParty.Count > 0) ShowStep3(); });

        if (backButton2 != null)
            backButton2.onClick.AddListener(ShowStep1);

        if (backButton3 != null)
            backButton3.onClick.AddListener(ShowStep2);

        if (startJourneyButton != null)
            startJourneyButton.onClick.AddListener(StartJourney);

        if (backButton != null)
            backButton.onClick.AddListener(Close);

        SetupProvisionButtons();
    }

    void SetupProvisionButtons()
    {
        if (rationsPlusButton != null)
            rationsPlusButton.onClick.AddListener(() => ChangeProvisions(1, 0));
        if (rationsMinusButton != null)
            rationsMinusButton.onClick.AddListener(() => ChangeProvisions(-1, 0));
        if (torchesPlusButton != null)
            torchesPlusButton.onClick.AddListener(() => ChangeProvisions(0, 1));
        if (torchesMinusButton != null)
            torchesMinusButton.onClick.AddListener(() => ChangeProvisions(0, -1));
    }

    /// <summary>Ajusta a compra respeitando o teto de carga e o ouro em caixa.</summary>
    void ChangeProvisions(int deltaRations, int deltaTorches)
    {
        int novasRacoes = Mathf.Clamp(extraRations + deltaRations, 0, maxExtraRations);
        int novasTochas = Mathf.Clamp(extraTorches + deltaTorches, 0, maxExtraTorches);

        int custo = novasRacoes * rationCost + novasTochas * torchCost;
        int ouro = GuildManager.Instance != null ? GuildManager.Instance.gold : 0;

        if (custo > ouro)
        {
            UIManager.Instance?.ShowMessage("Ouro insuficiente para essas provisões.", 2f);
            return;
        }

        extraRations = novasRacoes;
        extraTorches = novasTochas;
        UpdateProvisionsUI();
    }

    /// <summary>O que o Mercado guardou entra de graça nesta jornada.</summary>
    int MarketRations => MarketManager.Instance != null ? MarketManager.Instance.StockedRations : 0;
    int MarketTorches => MarketManager.Instance != null ? MarketManager.Instance.StockedTorches : 0;

    void UpdateProvisionsUI()
    {
        if (rationsBuyText != null)
        {
            rationsBuyText.text = $"🍖 {baseRations + extraRations + MarketRations}  (+{extraRations})"
                                + (MarketRations > 0 ? $"\n<size=13><color=#7FB069>+{MarketRations} do mercado</color></size>" : "");
        }

        if (torchesBuyText != null)
        {
            torchesBuyText.text = $"🔥 {baseTorches + extraTorches + MarketTorches}  (+{extraTorches})"
                                + (MarketTorches > 0 ? $"\n<size=13><color=#7FB069>+{MarketTorches} do mercado</color></size>" : "");
        }

        if (provisionsCostText != null)
        {
            int ouro = GuildManager.Instance != null ? GuildManager.Instance.gold : 0;
            provisionsCostText.text = ProvisionsCost == 0
                ? "Nenhuma provisão extra"
                : $"Custo: 💰 {ProvisionsCost} (restam {ouro - ProvisionsCost})";
        }
    }

    void OnEnable()
    {
        // Inscreve no evento de mudança do roster
        if (GuildManager.Instance != null)
        {
            GuildManager.Instance.onRosterChanged += OnRosterChanged;
        }

        RefreshAllData();
    }

    void OnDisable()
    {
        // Remove inscrição do evento
        if (GuildManager.Instance != null)
        {
            GuildManager.Instance.onRosterChanged -= OnRosterChanged;
        }
    }

    void OnRosterChanged()
    {
        RefreshPartySelection();
        RefreshDeckSelection();
    }

    void Update()
    {
        // F5 recarrega missões e roster sem sair da tela — atalho de desenvolvimento.
        if (Input.GetKeyDown(KeyCode.F5))
            RefreshAllData();
    }

    public void RefreshAllData()
    {
        UpdateGoldUI();

        // O que vem do quadro são só a luta de selo e a jornada final — o mapa
        // é quem oferece destino. Sem QuestManager na cena, a lista fica vazia e
        // o mapa continua inteiro: nenhuma área depende de oferta para existir.
        availableQuests = QuestManager.Instance != null
            ? QuestManager.Instance.GetQuests()
            : new List<QuestData>();

        // Zera antes de redesenhar: agora a lista de heróis é reconstruída a
        // partir de selectedParty (é o que preserva a marcação ao voltar do
        // passo 3), então limpar depois deixaria os toggles ligados sem ninguém
        // na party.
        selectedQuest = null;
        selectedMainHero = null;
        selectedParty.Clear();
        currentSelectedDeckCard = null;

        RefreshQuestList();
        RefreshPartySelection();
        RefreshDeckSelection();

        extraRations = 0;
        extraTorches = 0;
        UpdateProvisionsUI();

        ShowStep1();
        UpdateStartButtonStatus();
    }

    void UpdateGoldUI()
    {
        if (goldText != null && GuildManager.Instance != null)
            goldText.text = $"💰 {GuildManager.Instance.gold}";
    }

    int GetPlayerAverageLevel()
    {
        if (GuildManager.Instance == null || GuildManager.Instance.roster.Count == 0)
            return 1;

        int total = 0;
        foreach (var hero in GuildManager.Instance.roster)
            total += hero.level;
        return total / GuildManager.Instance.roster.Count;
    }

    #region Step 1 - Quests

    void RefreshQuestList()
    {
        if (questListContainer == null)
        {
            Debug.LogError("questListContainer é NULL!");
            return;
        }

        foreach (Transform child in questListContainer)
            Destroy(child.gameObject);

        // O destino se escolhe no mapa, e o mapa <b>não depende do quadro</b>: as
        // sete áreas existem sempre.
        //
        // Este método saía aqui quando não havia missão, e desde 11/09 não haver
        // missão é o estado normal da guilda — o quadro só guarda luta de selo e
        // jornada final. A tela de preparação abria sem nenhum destino clicável,
        // com o smoke test em 63 verificações e 0 falhas: dado certo, exibição
        // ausente, pela vigésima primeira vez.
        if (MontarMapaDeRegioes())
            return;

        // Rede de segurança: sem mapa, a lista antiga de ofertas.
        if (availableQuests == null || availableQuests.Count == 0)
        {
            Debug.LogWarning("Sem mapa e sem missão — a preparação ficou sem destino.");
            return;
        }

        foreach (var quest in availableQuests)
        {
            GameObject item = Instantiate(questItemPrefab, questListContainer);
            SetupQuestItem(item, quest);
        }
    }

    /// <summary>
    /// Constrói (ou redesenha) o mapa de regiões do passo 1.
    /// Devolve false se não houver onde montá-lo — aí a lista antiga assume.
    /// </summary>
    bool MontarMapaDeRegioes()
    {
        GameObject hospedeiro = step1Panel != null ? step1Panel
                              : (questListContainer != null ? questListContainer.gameObject : null);
        if (hospedeiro == null) return false;

        if (regionMap == null)
            regionMap = RegionMapUI.Montar(hospedeiro, this);

        if (regionMap == null) return false;

        regionMap.gameObject.SetActive(true);
        regionMap.Desenhar(availableQuests);
        EsconderListaAntiga(hospedeiro);
        return true;
    }

    /// <summary>
    /// Desliga o Scroll View da lista de missões enquanto o mapa está no ar.
    ///
    /// Ele tem fundo opaco e ocupa a mesma metade da tela: deixá-lo ligado põe
    /// uma caixa preta vazia ao lado do mapa. Desligar o objeto, e não só limpar
    /// os itens, é o que faz a área ficar realmente livre.
    /// </summary>
    void EsconderListaAntiga(GameObject hospedeiro)
    {
        if (hospedeiro == null) return;

        foreach (var scroll in hospedeiro.GetComponentsInChildren<ScrollRect>(true))
            if (scroll != null) scroll.gameObject.SetActive(false);
    }

    /// <summary>
    /// O jogador apontou um destino no mapa. Ponto de entrada do
    /// <see cref="RegionMapUI"/> — a seleção em si continua sendo a de sempre,
    /// para o resto da preparação não saber de onde veio a escolha.
    /// </summary>
    public void EscolherDestino(QuestData quest)
    {
        if (quest == null) return;
        SelectQuest(quest);
    }

    /// <summary>
    /// O jogador clicou numa área do mapa, e a expedição nasce aqui.
    ///
    /// <b>É o que substituiu o quadro de contratos como escolha de destino.</b>
    /// Antes o mapa só acendia onde havia oferta, e ir a um lugar dependia de o
    /// quadro ter sorteado aquele bioma naquele ciclo — o mundo existia, mas o
    /// jogador não podia visitá-lo. Agora as sete áreas são sempre destino, e o
    /// que a viagem custa sai da distância até lá.
    /// </summary>
    public void EscolherArea(AreaType lugar)
    {
        int nivel = QuestManager.Instance != null ? QuestManager.Instance.GetPlayerAverageLevel() : 1;

        QuestData expedicao = QuestGenerator.GerarExpedicao(lugar, nivel);
        if (expedicao == null) return;

        SelectQuest(expedicao);
    }

    void SetupQuestItem(GameObject item, QuestData quest)
    {
        // Procura pelos textos (use os nomes EXATOS do seu prefab)
        TMP_Text nameText = FindTextInChildren(item, "Name");
        TMP_Text durationText = FindTextInChildren(item, "Duration");
        TMP_Text rewardText = FindTextInChildren(item, "Reward");
        TMP_Text riskText = FindTextInChildren(item, "Risk");
        Image corruptionIcon = FindImageInChildren(item, "CorruptionIcon");

        if (nameText != null)
            nameText.text = quest.questName;
        else
            Debug.LogWarning($"Name não encontrado no QuestItem");

        if (durationText != null)
            durationText.text = $"⏱️ {quest.minDuration}-{quest.maxDuration} dias";

        if (rewardText != null)
            rewardText.text = $"💰 {quest.baseReward}+ ouro";

        if (riskText != null)
        {
            switch (quest.risk)
            {
                case QuestRisk.Low: riskText.text = "🟢 Baixo"; break;
                case QuestRisk.Medium: riskText.text = "🟡 Médio"; break;
                case QuestRisk.High: riskText.text = "🔴 Alto"; break;
            }
        }

        if (corruptionIcon != null)
            corruptionIcon.gameObject.SetActive(quest.isCorrupted);

        Button btn = item.GetComponent<Button>();
        btn.onClick.AddListener(() => SelectQuest(quest));
    }

    // Método auxiliar para encontrar textos
    TMP_Text FindTextInChildren(GameObject parent, string childName)
    {
        TMP_Text[] allTexts = parent.GetComponentsInChildren<TMP_Text>(true);
        foreach (var text in allTexts)
        {
            if (text.gameObject.name == childName)
                return text;
        }
        return null;
    }

    Image FindImageInChildren(GameObject parent, string childName)
    {
        Image[] allImages = parent.GetComponentsInChildren<Image>(true);
        foreach (var img in allImages)
        {
            if (img.gameObject.name == childName)
                return img;
        }
        return null;
    }

    string GetRiskText(QuestRisk risk)
    {
        switch (risk)
        {
            case QuestRisk.Low: return "🟢 Baixo";
            case QuestRisk.Medium: return "🟡 Médio";
            case QuestRisk.High: return "🔴 Alto";
            default: return "❓";
        }
    }

    void SelectQuest(QuestData quest)
    {
        selectedQuest = quest;

        // A mochila é da viagem, não da tela: o destino define quantos dias o
        // grupo passa fora, e é isso que diz quanta comida a guilda entrega.
        int previstos = DiasPrevistos(quest);
        baseRations = RacoesPara(previstos);
        baseTorches = TochasPara(previstos);

        AreaType lugar = AreaCatalog.Da(quest.biomeType);
        var ficha = AreaCatalog.De(lugar);
        bool eSelo = quest.isRegionBoss || quest.isFinalBoss;

        // A ficha da área no lugar do boletim de contrato.
        //
        // O que o jogador precisa para escolher é o que aquele lugar faz com as
        // regras — a etiqueta de bioma não decide nada, e dizia o mesmo que o
        // nome logo acima dela.
        string details = $"<b>{quest.questName}</b>\n\n";

        if (ficha != null)
        {
            details += $"<i>{ficha.regra}</i>\n\n";

            if (eSelo)
                details += $"<b>O selo:</b> {ficha.selo}\n\n";
            else
                details += $"<b>Dá:</b> {ficha.oQueDa}\n<b>Cobra:</b> {ficha.oQueCobra}\n\n";
        }

        details += $"⏱️ {quest.minDuration}-{quest.maxDuration} dias";
        if (ficha != null) details += $"  ·  {AreaCatalog.DiasDeIda(lugar) * 2} só de estrada";
        details += "\n";
        details += $"💰 {quest.baseReward}+ de espólio\n";
        details += $"⚠️ Risco: {GetRiskText(quest.risk)}\n";

        if (quest.isCorrupted)
            details += "\n<color=red>⚠️ LUGAR CORROMPIDO!</color>\n";

        if (quest.requirements != null && quest.requirements.Count > 0)
        {
            details += "\n<b>Requisitos:</b>\n";
            foreach (var req in quest.requirements)
                details += $"• {req.minAmount}x {req.requiredClass} (Nv.{req.minLevel}+)\n";
        }

        details += "\n" + BuildRoutePreview(quest);

        if (questDetailsText != null)
            questDetailsText.text = details;

        // A mochila mudou de tamanho junto com o destino: sem isto os contadores
        // continuam mostrando a expedição anterior.
        UpdateProvisionsUI();

        if (nextButton1 != null)
            nextButton1.interactable = true;
    }

    #endregion

    #region Step 2 - Party (Heróis de Apoio)

    void RefreshPartySelection()
    {
        if (partySelectionContainer == null)
        {
            Debug.LogError("partySelectionContainer é NULL!");
            return;
        }

        UIUtil.ClearChildrenNow(partySelectionContainer);

        if (GuildManager.Instance == null)
        {
            Debug.LogError("GuildManager.Instance é NULL!");
            return;
        }

        // A party sobrevive ao redesenho. Antes o método zerava a lista, e como
        // "Voltar" do passo 3 passa por aqui, o jogador perdia o grupo inteiro e
        // a ordem da formação só por querer conferir uma coisa no passo anterior.
        // Some daqui apenas quem não pode mais ir.
        selectedParty.RemoveAll(h => h == null || h.isDead
                                  || !GuildManager.Instance.roster.Contains(h));

        if (selectedMainHero != null && !selectedParty.Contains(selectedMainHero))
            selectedMainHero = null;

        AjustarGradeDeHerois();

        foreach (var hero in GuildManager.Instance.roster)
        {
            if (hero.isDead) continue;

            if (partyMemberSelectPrefab == null)
            {
                Debug.LogError("partyMemberSelectPrefab é NULL!");
                return;
            }

            GameObject selectObj = Instantiate(partyMemberSelectPrefab, partySelectionContainer);
            SetupPartySelectCard(selectObj, hero);
        }

        UpdatePartyCountText();
    }

    /// <summary>
    /// A grade da lista de heróis, dimensionada em execução.
    ///
    /// Em coluna única os cards ficavam empilhados numa faixa estreita da
    /// direita, com o meio da tela vazio. Em três colunas o elenco inteiro cabe
    /// de uma vez — e escolher quem vai é comparar, não rolar uma lista.
    /// </summary>
    void AjustarGradeDeHerois()
    {
        var grid = partySelectionContainer.GetComponent<GridLayoutGroup>();
        if (grid == null) return;

        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;
        grid.cellSize = new Vector2(TamanhoDoCard.x, TamanhoDoCard.y);
        grid.spacing = new Vector2(16f, 16f);
        grid.padding = new RectOffset(12, 12, 12, 12);
        grid.childAlignment = TextAnchor.UpperLeft;
    }

    /// <summary>Tamanho da célula do grid, e do card que mora nela.</summary>
    static readonly Vector2 TamanhoDoCard = new Vector2(336f, 146f);

    void SetupPartySelectCard(GameObject card, HeroData hero)
    {
        // Procura os componentes
        TMP_Text nameText = card.transform.Find("Name")?.GetComponent<TMP_Text>();
        TMP_Text classText = card.transform.Find("Class")?.GetComponent<TMP_Text>();
        TMP_Text levelText = card.transform.Find("Level")?.GetComponent<TMP_Text>();

        // Se não encontrar pelos nomes, tenta encontrar qualquer TMP_Text
        if (nameText == null)
        {
            TMP_Text[] allTexts = card.GetComponentsInChildren<TMP_Text>();
            if (allTexts.Length > 0) nameText = allTexts[0];
            if (allTexts.Length > 1) classText = allTexts[1];
            if (allTexts.Length > 2) levelText = allTexts[2];
        }

        // Quem voltou perto de quebrar não parte de novo sem cuidado. O card diz
        // isso na cara: sem o aviso, o herói simplesmente não podia ser marcado e
        // o jogador não saberia por quê.
        bool apto = hero.IsFitForJourney;

        // Os textos herdados são preenchidos assim mesmo: se um dia o card
        // voltar a usá-los, não voltam com o conteúdo de exemplo do prefab.
        if (nameText != null) nameText.text = hero.heroName;
        if (classText != null) classText.text = GetClassName(hero.heroClass);
        if (levelText != null) levelText.text = $"Nv.{hero.level}";

        // Toggle para selecionar
        Toggle toggle = card.GetComponentInChildren<Toggle>();
        if (toggle != null)
        {
            // Marcado antes de o ouvinte entrar: assim o estado restaurado não
            // dispara o callback e não mexe de novo em selectedParty.
            toggle.onValueChanged.RemoveAllListeners();
            toggle.isOn = selectedParty.Contains(hero);
            toggle.interactable = apto;
            toggle.onValueChanged.AddListener((isOn) => {
                if (isOn)
                {
                    if (!hero.IsFitForJourney)
                    {
                        toggle.isOn = false;
                        UIManager.Instance?.ShowMessage(
                            $"{hero.heroName} está {hero.UnfitReason}. Alivie o estresse no Mercado "
                            + "ou no Cemitério antes de mandá-lo de volta.", 3f);
                        return;
                    }

                    if (!selectedParty.Contains(hero))
                        selectedParty.Add(hero);
                }
                else
                {
                    selectedParty.Remove(hero);
                }
                UpdatePartyCountText();
                if (nextButton2 != null)
                    nextButton2.interactable = selectedParty.Count > 0;
            });
        }

        // O botão "USAR DECK" e o selo "⭐ PRINCIPAL" saem de cena: escolher o
        // baralho é o passo 3, e daqui a escolha era feita às cegas — sem ver
        // quantas cartas o herói tem nem em que estado ele está. O prefab é
        // compartilhado com a cena, então desligar é mais seguro que apagar.
        DesligarFilho(card, "MainButton");
        DesligarFilho(card, "MainIndicator");

        VestirCardDeSelecao(card, hero, nameText, classText, levelText, toggle);
    }

    /// <summary>
    /// Põe no card do passo 2 o que decide a escolha: rosto, vida, estresse e
    /// ferimento.
    ///
    /// <b>O card dizia nome, classe e nível — e nada disso decide quem vai.</b>
    /// Quem escolhe o grupo precisa saber quem voltou machucado da última vez e
    /// quem está a um susto de quebrar; essa informação existia no
    /// <see cref="HeroData"/> desde sempre e só aparecia na ficha do herói, a
    /// duas telas de distância. O prefab é o card herdado da cena e não tem
    /// esses campos, então eles nascem aqui — como no resto do jogo, que monta
    /// UI em execução.
    /// </summary>
    void VestirCardDeSelecao(GameObject card, HeroData hero,
                             TMP_Text nameText, TMP_Text classText, TMP_Text levelText,
                             Toggle toggle)
    {
        var rt = card.GetComponent<RectTransform>();
        if (rt == null) return;

        bool apto = hero.IsFitForJourney;

        // O retrato à esquerda, e o texto do prefab empurrado para a direita
        // dele. As posições do prefab vinham de quando o card era só três linhas
        // de texto — reaproveitar os componentes e reposicioná-los custa menos
        // que manter dois layouts vivos.
        const float MargemDoRetrato = 116f;

        if (hero.portrait != null)
        {
            // Último irmão, e não primeiro: o card tem um Background opaco
            // entre os filhos, e o retrato criado antes dele simplesmente
            // desaparecia — sem erro no console, como sempre.
            var moldura = new GameObject("Retrato", typeof(RectTransform));
            moldura.transform.SetParent(card.transform, false);
            moldura.transform.SetAsLastSibling();

            var mrt = moldura.GetComponent<RectTransform>();
            mrt.anchorMin = new Vector2(0f, 0.5f);
            mrt.anchorMax = new Vector2(0f, 0.5f);
            mrt.pivot = new Vector2(0f, 0.5f);
            mrt.anchoredPosition = new Vector2(12f, 8f);
            mrt.sizeDelta = new Vector2(92f, 92f);

            var img = moldura.AddComponent<Image>();
            img.sprite = hero.portrait;
            img.preserveAspect = true;

            // Quem não pode partir sai lavado: é a mesma leitura do nome em
            // cinza, feita onde o olho cai primeiro.
            img.color = apto ? Color.white : new Color(0.45f, 0.42f, 0.40f);
            img.raycastTarget = false;
        }

        // Os textos do prefab saem de cena e as linhas nascem aqui.
        //
        // Reposicioná-los não bastou: cada um tem pai e âncoras próprios,
        // herdados de um card menor, e "Nv.3" continuava escrito por cima de
        // "Guerreiro". Com três linhas criadas na hora, o card tem um layout
        // só — e é o mesmo que decide onde entram as barras logo abaixo.
        if (nameText != null) nameText.gameObject.SetActive(false);
        if (classText != null) classText.gameObject.SetActive(false);
        if (levelText != null) levelText.gameObject.SetActive(false);

        var nome = NovoRotulo(card, "Nome", 26f);
        nome.text = apto ? hero.heroName : $"<color=#8A7A6A>{hero.heroName}</color>";
        Ancorar(nome.rectTransform, MargemDoRetrato, -8f, 30f);

        var oficio = NovoRotulo(card, "Oficio", 20f);
        oficio.color = new Color(0.66f, 0.62f, 0.56f);
        oficio.text = $"{GetClassName(hero.heroClass)}  ·  Nv.{hero.level}";
        Ancorar(oficio.rectTransform, MargemDoRetrato, -40f, 26f);

        // Quem não pode partir diz por quê no lugar da linha de ofício: é a
        // única coisa que o jogador precisa saber sobre ele agora.
        if (!apto)
        {
            oficio.text = $"<color=#B0A040>🧠 {hero.UnfitReason}</color>";
        }

        // O toggle vai para o canto, fora do caminho do retrato e do nome.
        if (toggle != null)
        {
            var trt = toggle.GetComponent<RectTransform>();
            if (trt != null)
            {
                trt.anchorMin = trt.anchorMax = new Vector2(1f, 1f);
                trt.pivot = new Vector2(1f, 1f);
                trt.anchoredPosition = new Vector2(-10f, -10f);
            }
        }

        // As duas barras que dizem em que estado o herói volta e parte.
        float vida = hero.maxHp > 0 ? Mathf.Clamp01(hero.currentHp / (float)hero.maxHp) : 0f;
        float nervo = Mathf.Clamp01(hero.stress / 100f);

        Barra(card, "BarraVida", -94f, vida,
              Color.Lerp(new Color(0.62f, 0.20f, 0.18f), new Color(0.36f, 0.52f, 0.28f), vida),
              $"{hero.currentHp}/{hero.maxHp}");

        Barra(card, "BarraEstresse", -116f, nervo,
              Color.Lerp(new Color(0.40f, 0.40f, 0.46f), new Color(0.72f, 0.56f, 0.22f), nervo),
              $"{Mathf.RoundToInt(hero.stress)}");

        // A linha de aviso só aparece quando há o que avisar. Card sem marca é
        // herói inteiro — e num jogo de morte permanente isso também é
        // informação.
        string marca = "";
        if (hero.isInjured) marca += "<color=#B04040>🩸 ferido</color>   ";
        if (MentalStateUtil.IsAffliction(hero.mentalState))
            marca += $"<color=#B0A040>🧠 {MentalStateUtil.GetLabel(hero.mentalState)}</color>";
        else if (MentalStateUtil.IsVirtue(hero.mentalState))
            marca += $"<color=#60A060>✦ {MentalStateUtil.GetLabel(hero.mentalState)}</color>";

        if (!string.IsNullOrEmpty(marca))
        {
            var aviso = NovoRotulo(card, "Marca", 18f);
            aviso.text = marca;

            Ancorar(aviso.rectTransform, MargemDoRetrato, -66f, 22f);
        }
    }

    /// <summary>Uma linha do card: presa ao topo, começando depois do retrato.</summary>
    static void Ancorar(RectTransform rt, float x, float y, float altura)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(-x - 44f, altura);
    }

    /// <summary>
    /// Uma barra de trilho e preenchimento dentro do card.
    ///
    /// O preenchimento usa <see cref="UIUtil.Branco"/> porque
    /// <c>Image.type = Filled</c> é <b>ignorado</b> quando o Image não tem
    /// sprite: o componente desenha o retângulo inteiro, o <c>fillAmount</c> não
    /// vale nada e a barra fica sempre cheia, sem erro no console.
    /// </summary>
    static void Barra(GameObject card, string nome, float y, float fracao, Color cor, string leitura)
    {
        const float Esquerda = 116f;
        const float Altura = 14f;

        var trilho = new GameObject(nome, typeof(RectTransform));
        trilho.transform.SetParent(card.transform, false);

        var trt = trilho.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0f, 1f);
        trt.anchorMax = new Vector2(1f, 1f);
        trt.pivot = new Vector2(0f, 1f);
        trt.anchoredPosition = new Vector2(Esquerda, y);
        trt.sizeDelta = new Vector2(-Esquerda - 64f, Altura);

        var fundo = trilho.AddComponent<Image>();
        fundo.sprite = UIUtil.Branco();
        fundo.color = new Color(0.12f, 0.11f, 0.10f, 0.9f);
        fundo.raycastTarget = false;

        var cheio = new GameObject("Preenchimento", typeof(RectTransform));
        cheio.transform.SetParent(trilho.transform, false);

        var crt = cheio.GetComponent<RectTransform>();
        crt.anchorMin = Vector2.zero;
        crt.anchorMax = Vector2.one;
        crt.offsetMin = new Vector2(2f, 2f);
        crt.offsetMax = new Vector2(-2f, -2f);

        var img = cheio.AddComponent<Image>();
        img.sprite = UIUtil.Branco();
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillAmount = fracao;
        img.color = cor;
        img.raycastTarget = false;

        var numero = NovoRotulo(card, nome + "_Leitura", 16f);
        numero.text = leitura;
        numero.alignment = TextAlignmentOptions.Right;

        var nrt = numero.rectTransform;
        nrt.anchorMin = new Vector2(1f, 1f);
        nrt.anchorMax = new Vector2(1f, 1f);
        nrt.pivot = new Vector2(1f, 1f);
        nrt.anchoredPosition = new Vector2(-12f, y + 1f);
        nrt.sizeDelta = new Vector2(52f, Altura + 4f);
    }

    static TMP_Text NovoRotulo(GameObject card, string nome, float corpo)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        go.transform.SetParent(card.transform, false);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = corpo;
        tmp.color = new Color(0.86f, 0.83f, 0.78f);
        tmp.raycastTarget = false;
        tmp.richText = true;
        tmp.enableWordWrapping = false;

        return tmp;
    }

    static void DesligarFilho(GameObject card, string nome)
    {
        Transform filho = card.transform.Find(nome);
        if (filho != null) filho.gameObject.SetActive(false);
    }

    void UpdatePartyCountText()
    {
        if (partyCountText != null)
        {
            partyCountText.text = $"Heróis Selecionados: {selectedParty.Count}";

            if (selectedParty.Count == 0)
            {
                partyCountText.text += "\n<color=#D4AF37>Selecione pelo menos 1 herói</color>";
            }
            else
            {
                // Requisito não atendido avisa, mas não impede: ir despreparado
                // é uma escolha do jogador, e ela deve custar caro na jornada.
                List<string> faltando = GetUnmetRequirements(selectedQuest, selectedParty);

                if (faltando.Count > 0)
                    partyCountText.text += "\n<color=#B04040>⚠️ Requisitos não atendidos:\n• "
                                         + string.Join("\n• ", faltando) + "</color>";
                else
                    partyCountText.text += "\n<color=#4A7A4A>✓ Requisitos atendidos</color>";
            }
        }

        if (nextButton2 != null)
            nextButton2.interactable = selectedParty.Count > 0;

        RefreshFormation();
    }

    #endregion

    #region Formação

    /// <summary>
    /// Desenha a fila do grupo, da linha de frente para a retaguarda.
    ///
    /// A ordem de <see cref="selectedParty"/> é a formação — ela segue intacta
    /// para a jornada e daí para o combate. As setas reordenam essa mesma lista,
    /// então não existe estado de formação em lugar nenhum além dela.
    /// </summary>
    void RefreshFormation()
    {
        if (formationContainer == null) return;

        UIUtil.ClearChildrenNow(formationContainer);

        if (formationHintText != null)
            formationHintText.text = BuildFormationHint();

        if (selectedParty.Count == 0) return;

        for (int i = 0; i < selectedParty.Count; i++)
        {
            // Cabeçalhos no ponto em que a fileira muda, para a divisão ficar visível.
            if (i == 0)
                BuildFormationHeader("⚔️ LINHA DE FRENTE", new Color(0.85f, 0.55f, 0.35f));
            else if (i == PartyFormation.FrontSlots)
                BuildFormationHeader("🏹 RETAGUARDA", new Color(0.55f, 0.70f, 0.90f));

            BuildFormationRow(selectedParty[i], i);
        }
    }

    string BuildFormationHint()
    {
        if (selectedParty.Count == 0)
            return "As duas primeiras posições formam a linha de frente: recebem a maior parte dos golpes.";

        int malPosicionados = selectedParty.Count(h => !PartyFormation.IsWellPlaced(h, selectedParty));

        string texto = $"A retaguarda sofre {Mathf.RoundToInt((1f - PartyFormation.BackRowDamageMultiplier) * 100)}% menos dano.\n";

        // Nada impede levar mais de quatro, mas os excedentes se amontoam atrás e
        // quase nunca são atingidos — melhor dizer isso do que deixar descobrir.
        // O custo em comida também: uma boca a mais come de verdade, e o jogador
        // precisa saber disso antes de fechar a mochila, não no quarto dia.
        if (selectedParty.Count > PartyFormation.MaxSlots)
        {
            int porDia = PartyFormation.DailyRations(selectedParty.Count);
            texto += $"<color=#D4AF37>Acima de {PartyFormation.MaxSlots} heróis, os demais se abrigam na retaguarda"
                   + $" — e o grupo passa a comer {porDia} rações por dia.</color>\n";
        }

        if (malPosicionados == 0)
            texto += "<color=#4A7A4A>✓ Todos rendem onde estão.</color>";
        else
            texto += $"<color=#B04040>⚠️ {malPosicionados} fora de posição: as cartas deles saem a "
                   + $"{Mathf.RoundToInt(PartyFormation.OutOfPlaceMultiplier * 100)}%.</color>";

        return texto;
    }

    void BuildFormationHeader(string label, Color color)
    {
        var go = new GameObject("Header", typeof(RectTransform));
        go.transform.SetParent(formationContainer, false);

        var element = go.AddComponent<LayoutElement>();
        element.minHeight = 22;
        element.preferredHeight = 22;

        var text = go.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 14;
        text.color = color;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;
    }

    void BuildFormationRow(HeroData hero, int index)
    {
        bool front = index < PartyFormation.FrontSlots;
        bool bemPosicionado = PartyFormation.IsWellPlaced(hero, selectedParty);

        var row = new GameObject($"Slot_{index + 1}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        row.transform.SetParent(formationContainer, false);
        row.GetComponent<Image>().color = front
            ? new Color(0.22f, 0.17f, 0.14f)
            : new Color(0.14f, 0.16f, 0.20f);

        var element = row.AddComponent<LayoutElement>();
        element.minHeight = 42;
        element.preferredHeight = 42;

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(row.transform, false);

        var label = labelGo.AddComponent<TextMeshProUGUI>();
        label.fontSize = 15;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.raycastTarget = false;
        label.color = bemPosicionado ? new Color(0.92f, 0.90f, 0.85f) : new Color(0.85f, 0.55f, 0.50f);
        label.text = $"{index + 1}. {PartyFormation.PreferenceIcon(hero.heroClass)} {hero.heroName}"
                   + $"  <size=12>{GetClassName(hero.heroClass)}</size>"
                   + (bemPosicionado ? "" : "  <color=#B04040>⚠️</color>");

        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(8, 0);
        labelRect.offsetMax = new Vector2(-76, 0);

        // Setas: subir aproxima da linha de frente, descer afasta.
        HeroData capturado = hero;

        Button up = BuildFormationArrow(row.transform, "Btn_Up", "▲", -72);
        up.interactable = index > 0;
        up.onClick.AddListener(() => MoveInFormation(capturado, -1));

        Button down = BuildFormationArrow(row.transform, "Btn_Down", "▼", -36);
        down.interactable = index < selectedParty.Count - 1;
        down.onClick.AddListener(() => MoveInFormation(capturado, +1));
    }

    Button BuildFormationArrow(Transform parent, string name, string glyph, float right)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = new Color(0.28f, 0.25f, 0.23f);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1, 0.5f);
        rect.anchorMax = new Vector2(1, 0.5f);
        rect.sizeDelta = new Vector2(32, 32);
        rect.anchoredPosition = new Vector2(right + 16, 0);

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);

        var text = textGo.AddComponent<TextMeshProUGUI>();
        text.text = glyph;
        text.fontSize = 16;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0.94f, 0.88f, 0.72f);
        text.raycastTarget = false;

        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        return go.GetComponent<Button>();
    }

    /// <summary>Troca o herói de lugar com o vizinho, mantendo a lista como única fonte da ordem.</summary>
    void MoveInFormation(HeroData hero, int delta)
    {
        int from = selectedParty.IndexOf(hero);
        if (from < 0) return;

        int to = from + delta;
        if (to < 0 || to >= selectedParty.Count) return;

        selectedParty[from] = selectedParty[to];
        selectedParty[to] = hero;

        // UpdatePartyCountText redesenha a formação: mudar a ordem pode alterar
        // o aviso de quem está fora de posição.
        UpdatePartyCountText();
    }

    #endregion

    #region Step 3 - Deck (Herói Principal)

    void RefreshDeckSelection()
    {
        if (deckSelectionContainer == null)
        {
            Debug.LogError("deckSelectionContainer é NULL!");
            return;
        }

        UIUtil.ClearChildrenNow(deckSelectionContainer);
        deckCards.Clear();
        currentSelectedDeckCard = null;

        if (selectedParty.Count == 0)
        {
            Debug.LogWarning("Nenhum herói selecionado para mostrar decks!");
            if (selectedDeckNameText != null)
                selectedDeckNameText.text = "⚠️ Selecione heróis no passo anterior!";
            return;
        }

        foreach (var hero in selectedParty)
        {
            if (hero.isDead) continue;

            // Sempre pelo repositório: reflete o que o jogador editou no DeckManager.
            DeckData heroDeck = DeckRepository.GetDeck(hero);

            GameObject deckCard = Instantiate(deckCardPrefab, deckSelectionContainer);
            SetupDeckCard(deckCard, hero, heroDeck);
            deckCards[hero] = deckCard;
        }

        MarcarBaralhoPadrao();
    }

    /// <summary>
    /// Deixa um baralho já escolhido ao abrir o passo 3.
    ///
    /// Sem isto o passo abria sem nada marcado e com o botão de partir cinza, e
    /// nada na tela dizia que faltava um clique — era o caso em que "o jogo não
    /// deixa eu ir" não tinha explicação visível. O primeiro da formação é o
    /// padrão porque é quem lidera a fila que o jogador acabou de montar; trocar
    /// continua sendo um clique, e com um herói só não existe escolha a fazer.
    /// </summary>
    void MarcarBaralhoPadrao()
    {
        HeroData alvo = selectedMainHero != null && deckCards.ContainsKey(selectedMainHero)
            ? selectedMainHero
            : selectedParty.FirstOrDefault(h => h != null && !h.isDead);

        if (alvo == null || !deckCards.TryGetValue(alvo, out GameObject card)) return;

        SelectDeck(alvo, card);
    }

    void SetupDeckCard(GameObject card, HeroData hero, DeckData deck)
    {
        // Procura pelos textos
        TMP_Text nameText = FindTextInChildren(card, "Name");
        TMP_Text classText = FindTextInChildren(card, "Class");
        TMP_Text levelText = FindTextInChildren(card, "Level");
        TMP_Text cardCountText = FindTextInChildren(card, "CardCount");

        // Preenche os textos
        if (nameText != null)
            nameText.text = hero.heroName;
        else
            Debug.LogWarning($"Name não encontrado no DeckCard");

        if (classText != null)
            classText.text = GetClassName(hero.heroClass);

        if (levelText != null)
            levelText.text = $"Nv.{hero.level}";

        if (cardCountText != null)
            cardCountText.text = $"{deck.cards.Count} cartas";

        // O retrato trazia um "0/0" fixo do editor. É a última tela antes de
        // partir: o estado em que o herói vai é justamente o que se quer conferir.
        TMP_Text hpText = FindTextInChildren(card, "HP");
        if (hpText != null)
            hpText.text = $"❤️ {hero.currentHp}/{hero.maxHp}";

        VestirRetrato(card, hero);
        MostrarComposicaoDoBaralho(card, deck);
        LiberarCliqueDoCard(card);

        // Botão para selecionar o deck
        Button btn = card.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => SelectDeck(hero, card));
        }
    }

    /// <summary>
    /// De que é feito o baralho daquele herói, em quatro linhas.
    ///
    /// <b>O card do passo 3 tinha um vão de 150px entre a classe e a contagem de
    /// cartas.</b> "10 cartas" não ajuda a escolher o principal — dez cartas de
    /// ataque e dez de suporte ganham jornadas diferentes, e essa informação só
    /// existia entrando na tela de Baralhos, que a preparação não alcança.
    ///
    /// O papel vem do <see cref="CardRoleUtil"/>, que é o mesmo eixo pelo qual o
    /// <c>DeckGenerator</c> monta o baralho — e não de uma segunda leitura dos
    /// números da carta, que chamaria a Fúria de ataque.
    /// </summary>
    void MostrarComposicaoDoBaralho(GameObject card, DeckData deck)
    {
        if (deck == null || deck.cards == null) return;

        var contagem = new Dictionary<CardRole, int>();
        foreach (var carta in deck.cards)
        {
            if (carta == null) continue;

            CardRole papel = CardRoleUtil.Of(carta);
            contagem[papel] = contagem.ContainsKey(papel) ? contagem[papel] + 1 : 1;
        }

        var linhas = new List<string>();
        foreach (CardRole papel in new[] { CardRole.Ataque, CardRole.Defesa,
                                           CardRole.Suporte, CardRole.Utilidade })
        {
            if (!contagem.ContainsKey(papel)) continue;
            linhas.Add($"{contagem[papel]}  {NomeDoPapel(papel)}");
        }

        if (linhas.Count == 0) return;

        var go = new GameObject("Composicao", typeof(RectTransform));
        go.transform.SetParent(card.transform, false);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = string.Join("\n", linhas);
        tmp.fontSize = 19f;
        tmp.color = new Color(0.70f, 0.66f, 0.60f);
        tmp.alignment = TextAlignmentOptions.Top;
        tmp.lineSpacing = 10f;
        tmp.raycastTarget = false;

        // Abaixo da linha de classe e acima da contagem de cartas: a faixa de
        // 130px que sobrava no meio do card. Medida do topo do card, e não do
        // retrato — a primeira versão começava em -230 e escrevia por cima de
        // "Mago · Nv.2".
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = new Vector2(16f, 56f);
        rt.offsetMax = new Vector2(-16f, -360f);
    }

    static string NomeDoPapel(CardRole papel)
    {
        switch (papel)
        {
            case CardRole.Ataque: return "de ataque";
            case CardRole.Defesa: return "de defesa";
            case CardRole.Suporte: return "de suporte";
            default: return "de utilidade";
        }
    }

    /// <summary>
    /// Põe o rosto do herói no card do passo 3.
    ///
    /// O quadro do retrato é um prefab aninhado (<c>Portrait</c>), e o Image de
    /// dentro dele — <c>img_Portrait</c> — nasce sem sprite e com um cinza
    /// escuro por cor. Era a caixa vazia que aparecia no lugar de cada herói
    /// justamente na última tela antes de partir, enquanto a Taverna e a Forja
    /// mostravam o mesmo <see cref="HeroData.portrait"/> sem problema: elas
    /// criam o Image em código e atribuem o sprite; aqui ninguém atribuía.
    ///
    /// Sem retrato no herói, a cor original fica — um quadro cinza é melhor que
    /// um branco chapado no meio do card.
    /// </summary>
    static void VestirRetrato(GameObject card, HeroData hero)
    {
        if (hero == null || hero.portrait == null) return;

        foreach (var img in card.GetComponentsInChildren<Image>(true))
        {
            if (img.gameObject.name != "img_Portrait") continue;

            img.sprite = hero.portrait;
            img.color = Color.white;
            img.preserveAspect = true;
            return;
        }
    }

    /// <summary>
    /// Faz o card inteiro responder ao clique.
    ///
    /// O quadro do retrato vem com um Button próprio, herdado do prefab e sem
    /// nenhum ouvinte. Ele cobre a metade de cima do card e engolia o clique: o
    /// baralho só era escolhido quando se acertava a faixa do nome ou a contagem
    /// de cartas, e clicar no rosto do herói — o alvo óbvio — não fazia nada.
    ///
    /// Desligar o raycast dos gráficos do quadro deixa o clique atravessar até o
    /// corpo do card, de onde ele sobe para o Button da raiz. Mexer no prefab
    /// resolveria também, mas ele é o mesmo que a cena instancia, e um Button
    /// removido lá some para todo mundo.
    /// </summary>
    static void LiberarCliqueDoCard(GameObject card)
    {
        Transform quadro = card.transform.Find("Portrait");
        if (quadro == null) return;

        foreach (var grafico in quadro.GetComponentsInChildren<Graphic>(true))
            grafico.raycastTarget = false;
    }

    void SelectDeck(HeroData hero, GameObject selectedCard)
    {
        // Verifica se o herói está entre os selecionados
        if (!selectedParty.Contains(hero))
        {
            Debug.LogWarning($"Herói {hero.heroName} não está na lista de selecionados!");
            UIManager.Instance?.ShowMessage($"Selecione {hero.heroName} como apoio primeiro!", 2f);
            return;
        }

        selectedMainHero = hero;

        // Remove destaque anterior
        if (currentSelectedDeckCard != null)
        {
            Image prevBorder = currentSelectedDeckCard.transform.Find("SelectedBorder")?.GetComponent<Image>();
            if (prevBorder != null) prevBorder.gameObject.SetActive(false);
        }

        currentSelectedDeckCard = selectedCard;
        Image border = selectedCard.transform.Find("SelectedBorder")?.GetComponent<Image>();
        if (border != null) border.gameObject.SetActive(true);

        ShowDeckComposition(hero);
        UpdateStartButtonStatus();
    }

    /// <summary>
    /// Deixa explícito o que o jogador vai levar: o baralho do principal mais o
    /// que cada companheiro empresta.
    /// </summary>
    void ShowDeckComposition(HeroData main)
    {
        if (selectedDeckNameText == null) return;

        JourneyDeckBuilder.Result preview = JourneyDeckBuilder.Build(main, selectedParty);

        // Duas linhas, e não sete.
        //
        // O detalhamento por herói — "Lyra — 10 cartas (base)", "Gromm — +3
        // cartas", uma linha por companheiro — repetia o que cada card da mesma
        // tela já diz no rodapé ("10 cartas"), e transbordava da caixa por cima
        // do primeiro card do grupo. A regra do baralho híbrido cabe numa frase.
        int emprestadas = Mathf.Max(0, selectedParty.Count - 1);

        string texto = $"⭐ Deck Principal: {main.heroName}\n";
        texto += $"<b>{preview.deck.cards.Count} cartas na jornada</b>";

        if (emprestadas > 0)
            texto += $"  <size=16>— o baralho de {main.heroName} mais 3 cartas "
                   + $"de cada um dos {emprestadas} companheiros</size>";

        // A formação sai daqui: o Txt_TeamSummary, do outro lado do mesmo passo,
        // já mostra frente e retaguarda e marca quem está fora de posição. As
        // duas listas apareciam lado a lado dizendo o mesmo com palavras
        // diferentes, e a composição do baralho — que só existe nesta caixa —
        // ficava empurrada para baixo por elas.
        selectedDeckNameText.text = texto;
    }

    /// <summary>
    /// Dá ao jogador uma noção do que espera pela frente sem entregar o mapa:
    /// forma da rota e proporção de perigo, não os eventos exatos.
    /// </summary>
    string BuildRoutePreview(QuestData quest)
    {
        bool chefeNoFim = quest.isRegionBoss || quest.isFinalBoss;

        var texto = "<b>A rota:</b>\n";
        texto += $"• {quest.minDuration}-{quest.maxDuration} dias"
               + (chefeNoFim ? " até o confronto final\n" : " de ida, estrada e volta\n");
        texto += "• Cada dia oferece 2 ou 3 caminhos\n";

        switch (quest.risk)
        {
            case QuestRisk.Low:
                texto += "• <color=#4A7A4A>Poucos combates esperados</color>\n";
                break;
            case QuestRisk.Medium:
                texto += "• <color=#D4AF37>Combates frequentes</color>\n";
                break;
            case QuestRisk.High:
                texto += "• <color=#B04040>Território hostil — combates constantes</color>\n";
                break;
        }

        if (quest.isCorrupted)
            texto += "• <color=#8A4AA0>A corrupção altera os eventos</color>\n";

        // O chefe deixou de fechar toda jornada em 11/09: prometê-lo numa
        // expedição comum é prometer uma luta que não vem.
        texto += chefeNoFim
            ? "• 💀 Chefe no fim, inevitável\n"
            : "• ⚔️ Um encontro forte fecha a rota\n";
        texto += "\n<i>Batedores da Sala de Mapas revelam os caminhos adiante.</i>";

        return texto;
    }

    #endregion

    #region Requisitos da missão

    /// <summary>
    /// Confere a party contra os requisitos da missão. QuestData já os declarava,
    /// mas nada os verificava — eram texto decorativo na tela de detalhes.
    /// </summary>
    public static List<string> GetUnmetRequirements(QuestData quest, List<HeroData> party)
    {
        var faltando = new List<string>();

        if (quest == null || quest.requirements == null) return faltando;

        foreach (var req in quest.requirements)
        {
            if (req == null) continue;

            int atendem = party.Count(h => h != null && !h.isDead
                                        && h.heroClass == req.requiredClass
                                        && h.level >= req.minLevel);

            if (atendem < req.minAmount)
                faltando.Add($"{req.minAmount}x {req.requiredClass} Nv.{req.minLevel}+ (tem {atendem})");
        }

        return faltando;
    }

    #endregion

    #region Navigation

    public void ShowStep1()
    {
        if (step1Panel != null)
            step1Panel.SetActive(true);
        else
            Debug.LogError("step1Panel é NULL!");

        if (step2Panel != null) step2Panel.SetActive(false);
        if (step3Panel != null) step3Panel.SetActive(false);

        // A formação vive fora dos passos (ela é larga demais para caber dentro
        // do passo 2), então precisa ser ligada e desligada à mão.
        if (formationPanel != null) formationPanel.SetActive(false);

        if (nextButton1 != null) nextButton1.interactable = selectedQuest != null;

        // Enquanto nada está escolhido, a coluna mostra o que a guilda pede.
        //
        // É o quadro inteiro em cinco linhas, e no único instante em que ele
        // muda alguma coisa: o jogador está olhando o mapa e decidindo até onde
        // ir. Depois do clique, a mesma coluna passa a falar do lugar.
        if (selectedQuest == null && questDetailsText != null)
            questDetailsText.text = TextoDoQuadro();
    }

    /// <summary>
    /// O que a guilda pede, para ser lido em cima do mapa.
    ///
    /// A encomenda não diz onde — pede um resultado, e é o jogador que decide em
    /// que área sai mais barato consegui-lo. Sem prazo escrito o pedido vira
    /// promessa vaga, então o ciclo limite vem junto.
    /// </summary>
    string TextoDoQuadro()
    {
        var pedidos = Encomendas.Ativas();

        if (pedidos.Count == 0)
            return "<b>O quadro</b>\n\n<i>Nada pendurado neste ciclo.</i>\n\n"
                 + "Escolha um lugar no mapa.";

        int ciclo = RunManager.Existe ? RunManager.Instance.Cycle : 0;

        string texto = "<b>O quadro</b>\n\n";
        foreach (var pedido in pedidos)
        {
            int prazo = Mathf.Max(0, pedido.cicloLimite - ciclo);

            texto += $"<b>{pedido.Titulo}</b>\n";
            texto += $"<size=13>{pedido.Pedido}</size>\n";
            texto += $"<size=13>💰 {pedido.premio}  ·  "
                   + (prazo == 0 ? "<color=#B04040>último ciclo</color>" : $"{prazo} ciclos")
                   + "</size>\n\n";
        }

        texto += "<i>Escolha um lugar no mapa.</i>";
        return texto;
    }

    void ShowStep2()
    {
        if (step1Panel != null) step1Panel.SetActive(false);
        if (step2Panel != null) step2Panel.SetActive(true);
        if (step3Panel != null) step3Panel.SetActive(false);
        if (formationPanel != null) formationPanel.SetActive(true);
        RefreshPartySelection();
    }

    void ShowStep3()
    {
        if (step1Panel != null) step1Panel.SetActive(false);
        if (step2Panel != null) step2Panel.SetActive(false);
        if (step3Panel != null) step3Panel.SetActive(true);
        if (formationPanel != null) formationPanel.SetActive(false);

        // Mostra apenas os decks dos heróis selecionados
        RefreshDeckSelection();
        UpdateTeamSummary();
    }

    /// <summary>
    /// Repete a formação no passo 3, onde o painel dela já saiu de cena. É a
    /// última tela antes de partir: quem trocou a ordem no passo 2 precisa poder
    /// conferir sem voltar.
    /// </summary>
    void UpdateTeamSummary()
    {
        if (teamSummaryText == null) return;

        if (selectedParty.Count == 0)
        {
            teamSummaryText.text = "";
            return;
        }

        var frente = new List<string>();
        var retaguarda = new List<string>();

        for (int i = 0; i < selectedParty.Count; i++)
        {
            HeroData hero = selectedParty[i];
            string nome = hero.heroName;

            if (!PartyFormation.IsWellPlaced(hero, selectedParty))
                nome = $"<color=#B04040>{nome}⚠️</color>";

            if (i < PartyFormation.FrontSlots) frente.Add(nome);
            else retaguarda.Add(nome);
        }

        teamSummaryText.text = $"<color=#D98C59>⚔️ Frente:</color> {string.Join(", ", frente)}"
            + (retaguarda.Count > 0
                ? $"\n<color=#8CB3E6>🏹 Retaguarda:</color> {string.Join(", ", retaguarda)}"
                : "");
    }

    void UpdateStartButtonStatus()
    {
        if (startJourneyButton != null)
        {
            bool hasValidDeck = selectedMainHero != null && selectedParty.Contains(selectedMainHero);
            bool canStart = selectedQuest != null && selectedParty.Count > 0 && hasValidDeck;

            startJourneyButton.interactable = canStart;
        }
    }

    void StartJourney()
    {
        if (selectedQuest == null)
        {
            Debug.LogError("Nenhuma missão selecionada!");
            return;
        }

        if (selectedParty.Count == 0)
        {
            Debug.LogError("Nenhum herói de apoio selecionado!");
            return;
        }

        if (selectedMainHero == null)
        {
            Debug.LogError("Nenhum deck principal selecionado!");
            return;
        }

        // Verifica se o deck principal está entre os heróis de apoio
        if (!selectedParty.Contains(selectedMainHero))
        {
            Debug.LogError($"Herói principal {selectedMainHero.heroName} não está na lista de apoio!");
            UIManager.Instance?.ShowMessage("O herói principal deve estar entre os heróis de apoio!", 2f);
            return;
        }

        // O baralho da jornada é o do herói principal somado às cartas que os
        // companheiros emprestam — a party entra no deckbuilding, não só no combate.
        JourneyDeckBuilder.Result built = JourneyDeckBuilder.Build(selectedMainHero, selectedParty);
        DeckData selectedDeck = built.deck;

        if (selectedDeck == null || selectedDeck.cards.Count == 0)
        {
            Debug.LogError($"Deck vazio para {selectedMainHero.heroName}");
            UIManager.Instance?.ShowMessage($"{selectedMainHero.heroName} não tem cartas no deck!", 2f);
            return;
        }

        if (JourneyManager.Instance == null)
        {
            Debug.LogError("JourneyManager.Instance é NULL!");
            return;
        }

        // Cobra as provisões só agora: desistir no meio da preparação não custa nada.
        int custo = ProvisionsCost;
        if (custo > 0)
        {
            if (GuildManager.Instance == null || GuildManager.Instance.gold < custo)
            {
                UIManager.Instance?.ShowMessage("Ouro insuficiente para as provisões.", 2f);
                return;
            }

            GuildManager.Instance.SpendGold(custo);
        }

        HideSelectionScreen();

        // O estoque do Mercado é consumido só agora, junto das provisões — pela
        // mesma razão: desistir no meio da preparação não deve custar nada.
        int doMercadoRacoes = MarketManager.Instance != null ? MarketManager.Instance.ConsumeRations() : 0;
        int doMercadoTochas = MarketManager.Instance != null ? MarketManager.Instance.ConsumeTorches() : 0;

        JourneyManager.Instance.StartJourney(
            selectedQuest, selectedParty, selectedDeck,
            baseRations + extraRations + doMercadoRacoes,
            baseTorches + extraTorches + doMercadoTochas,
            built.ownership);
    }

    void Close()
    {
        HideSelectionScreen();
        if (UIManager.Instance != null)
            UIManager.Instance.ShowGuildScreen();
    }

    /// <summary>
    /// Fecha a tela de seleção.
    ///
    /// Cuidado que motivou este método: nesta cena o componente vive no Canvas
    /// raiz, então o antigo `gameObject.SetActive(false)` desligava a UI inteira
    /// — inclusive o painel da jornada que acabara de ser aberto. O sintoma era
    /// "clico em iniciar e não acontece nada".
    /// </summary>
    void HideSelectionScreen()
    {
        GameObject root = ResolveSelectionRoot();

        if (root != null && root != gameObject)
        {
            root.SetActive(false);
            return;
        }

        // Sem uma raiz própria, esconde ao menos os passos — mas nunca o Canvas.
        if (step1Panel != null) step1Panel.SetActive(false);
        if (step2Panel != null) step2Panel.SetActive(false);
        if (step3Panel != null) step3Panel.SetActive(false);

        if (GetComponent<Canvas>() == null && transform.parent != null)
            gameObject.SetActive(false);
        else
            Debug.LogWarning("QuestSelectionUI: sem selectionRoot definido e o componente está no " +
                             "Canvas raiz — apenas os passos foram escondidos.");
    }

    GameObject ResolveSelectionRoot()
    {
        if (selectionRoot != null) return selectionRoot;

        // O pai comum dos passos é, por construção, o painel da seleção.
        if (step1Panel != null && step1Panel.transform.parent != null)
            return step1Panel.transform.parent.gameObject;

        return null;
    }

    #endregion

    #region Helpers

    public void RegisterHeroDeck(HeroData hero, DeckData deck)
    {
        DeckRepository.SetDeck(hero, deck);
    }

    string GetClassName(HeroClass heroClass)
    {
        switch (heroClass)
        {
            case HeroClass.Warrior: return "⚔️ Guerreiro";
            case HeroClass.Mage: return "🔮 Mago";
            case HeroClass.Healer: return "⚕️ Curandeiro";
            case HeroClass.Rogue: return "🗡️ Ladino";
            case HeroClass.Bard: return "🎵 Bardo";
            case HeroClass.Hunter: return "🏹 Caçador";
            default: return "❓";
        }
    }

    #endregion
}
