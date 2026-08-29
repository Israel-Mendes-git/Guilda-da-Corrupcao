using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Biblioteca: vende cartas para o baralho de um herói.
///
/// <b>Por que a sala deixou de ser uma lista.</b> A versão anterior era uma faixa
/// com três cartas e um "COMPRAR" embaixo de cada uma. O preço aparecia, o efeito
/// não: pagar 100 de ouro tirava a carta da faixa e mais nada acontecia na tela —
/// nem no jogo, porque a carta comprada não ia para lugar nenhum. A tela de
/// Baralhos, que é grátis, já oferece todas as cartas da classe de cada herói.
///
/// <b>A compra tem dono.</b> Agora há sempre um herói <i>na mesa</i>, escolhido na
/// fila à esquerda, e a carta comprada entra no baralho dele no mesmo clique: o
/// contador da mesa sobe, a carta aparece na lista do baralho e o ouro sai. É a
/// resposta à pergunta que faltava na hora de pagar — <b>para quem serve esta
/// carta</b> —, e é o que separa esta sala da tela de Baralhos: aqui se paga para
/// acrescentar, lá se rearranja o que já existe.
///
/// <b>Cada carta da estante é julgada contra esse baralho.</b> Na própria carta,
/// onde o combate escreve o dano real, a biblioteca escreve o veredito: se a carta
/// não serve àquele herói, se ele já a leva, ou quanto ela bate a melhor que ele
/// tem daquele tipo. Sem isso o jogador escolhia por preço, que é a única coisa
/// que a sala dizia.
///
/// <b>O estoque não é re-sorteado a cada abertura.</b> Antes o sorteio rodava em
/// todo <see cref="RefreshLibrary"/>, inclusive depois de uma compra — a estante
/// inteira trocava e a carta recém-comprada podia reaparecer à venda. Aqui o
/// sorteio acontece uma vez e ao mudar de nível. O que muda por ciclo é decisão
/// de outra frente e entra em <see cref="GenerateCardsByLevel"/>, que ficou como
/// a única costura do estoque.
/// </summary>
public class LibraryManager : MonoBehaviour
{
    public static LibraryManager Instance;

    [Header("Configuração")]
    public int libraryLevel = 1;
    public List<CardData> availableCards = new List<CardData>();

    [Header("Cabeçalho")]
    public TMP_Text levelText;
    public TMP_Text goldText;
    public TMP_Text hintText;
    public TMP_Text feedbackText;

    [Header("Quem está na fila")]
    public Transform heroContainer;

    [Header("A mesa de leitura")]
    public GameObject deskRoot;
    public Image portraitImage;
    public TMP_Text heroNameText;
    public TMP_Text heroStatsText;
    public TMP_Text deckTitleText;
    public Transform deckContainer;

    [Header("A estante")]
    public Transform cardsContainer;
    public TMP_Text shelfTitleText;
    public GameObject cardPrefab;

    /// <summary>Moldura do nicho. Vem do Editor: os nichos nascem em execução.</summary>
    public Sprite nicheSprite;

    [Header("Buttons")]
    public Button upgradeButton;
    public Button closeButton;

    [Header("Preços")]
    public int upgradeBaseCost = 500;
    public int commonCardPrice = 100;
    public int rareCardPrice = 250;
    public int epicCardPrice = 500;
    public int legendaryCardPrice = 1000;

    /// <summary>Nichos da estante: três por duas. É o que cabe sem encolher a carta
    /// a ponto de a descrição não poder mais ser lida.</summary>
    const int NichosNaEstante = 6;

    /// <summary>Acima disto não há raridade nova para liberar.</summary>
    const int NivelMaximo = 4;

    const float LarguraDoNicho = 224f;
    const float AlturaDoNicho = 340f;
    const float EspacoEntreNichos = 18f;
    const int ColunasDaEstante = 3;

    /// <summary>A carta do prefab é 245×345; seis delas cabem na estante assim.</summary>
    const float EscalaDaCarta = 0.78f;

    HeroData naMesa;
    bool estoqueSorteado;
    int nivelDoEstoque;
    string classesDoEstoque;

    /// <summary>A mesa foi escolhida na fila, e não pela sala. Quem escolheu manda.</summary>
    bool mesaEscolhidaPeloJogador;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(() => UIManager.Instance?.CloseLibrary());

        if (upgradeButton != null)
            upgradeButton.onClick.AddListener(UpgradeLibrary);
    }

    /// <summary>Quem recebe a próxima carta comprada. Nulo enquanto a guilda não tem ninguém vivo.</summary>
    public HeroData NaMesa => naMesa;

    public void RefreshLibrary()
    {
        int gold = GuildManager.Instance != null ? GuildManager.Instance.gold : 0;

        if (goldText != null)
            goldText.text = $"💰 {gold}";

        if (levelText != null)
            levelText.text = $"Nível {libraryLevel} · {RaridadesLiberadas()}";

        if (hintText != null)
            hintText.text = "A carta comprada entra direto no baralho de quem está na mesa — "
                          + $"no máximo {DeckManager.MaxCopiasPorCarta} cópias de cada. "
                          + "Melhorar a sala traz raridades melhores para a estante.";

        // A estante antes da mesa: quem senta é escolhido pelo que há para lhe
        // vender, e para isso o estoque de hoje já precisa estar sorteado.
        GarantirEstoque();

        // Quem morreu na última jornada não pode continuar na mesa, e quem entrou
        // agora precisa aparecer na fila.
        //
        // A última condição é a sala se corrigindo: se quem sentou foi a própria
        // sala e não há mais nada para lhe vender, ela cede o lugar. Escolha do
        // jogador não é desfeita — comprar a última carta útil de um herói não
        // pode tirá-lo da mesa nas costas de quem o pôs ali.
        if (naMesa == null || naMesa.isDead || !EstaNoRoster(naMesa)
            || (!mesaEscolhidaPeloJogador && !PodeComprarAlgo(naMesa)))
        {
            naMesa = PrimeiroQuePodeComprar() ?? PrimeiroVivo();
            mesaEscolhidaPeloJogador = false;
        }

        AtualizarBotaoDeMelhora();

        BuildHeroes();
        AtualizarMesa();
        MontarEstante();
    }

    static bool EstaNoRoster(HeroData hero)
    {
        return GuildManager.Instance != null && GuildManager.Instance.roster.Contains(hero);
    }

    static HeroData PrimeiroVivo()
    {
        if (GuildManager.Instance == null) return null;
        return GuildManager.Instance.roster.FirstOrDefault(h => h != null && !h.isDead);
    }

    /// <summary>
    /// Quem a sala põe na mesa sozinha: o primeiro herói vivo que <b>pode</b>
    /// receber alguma carta da estante de hoje.
    ///
    /// Era o primeiro vivo, e mais nada. Com o baralho dele cheio em 12/12 — que
    /// é o estado comum de quem está na guilda há alguns ciclos —, a Biblioteca
    /// abria com todos os botões desligados e parecia quebrada; o remédio era um
    /// clique numa ficha da fila, e nada na tela dizia isso.
    ///
    /// O ouro não entra na conta: um herói que o jogador ainda não pode pagar
    /// continua sendo o certo para mostrar.
    /// </summary>
    HeroData PrimeiroQuePodeComprar()
    {
        if (GuildManager.Instance == null) return null;
        return GuildManager.Instance.roster.FirstOrDefault(PodeComprarAlgo);
    }

    /// <summary>Há na estante de hoje alguma carta que entra no baralho deste herói?</summary>
    bool PodeComprarAlgo(HeroData hero)
    {
        if (hero == null || hero.isDead) return false;

        DeckData deck = DeckRepository.GetDeck(hero);
        if (deck == null || deck.cards == null) return false;
        if (deck.cards.Count >= deck.maxDeckSize) return false;

        return availableCards.Any(c => ServeA(c, hero)
            && Copias(deck, c) < DeckManager.MaxCopiasPorCarta);
    }

    void SetFeedback(string message)
    {
        if (feedbackText != null)
            feedbackText.text = message;
    }

    #region A fila à esquerda

    void BuildHeroes()
    {
        if (heroContainer == null) return;

        UIUtil.ClearChildrenNow(heroContainer);

        if (GuildManager.Instance == null) return;

        foreach (var hero in GuildManager.Instance.roster.Where(h => h != null && !h.isDead))
            BuildHeroRow(hero);
    }

    /// <summary>
    /// A ficha da fila. Ela não vende nada: só troca quem está na mesa. O que ela
    /// carrega é o motivo para trocar — quantas das cartas de hoje servem àquele
    /// herói e quanto espaço ainda há no baralho dele.
    /// </summary>
    void BuildHeroRow(HeroData hero)
    {
        bool ativo = hero == naMesa;

        var row = new GameObject(hero.heroName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        row.transform.SetParent(heroContainer, false);
        row.GetComponent<Image>().color = ativo
            ? new Color(0.26f, 0.22f, 0.30f)
            : new Color(0.16f, 0.15f, 0.17f);

        var element = row.AddComponent<LayoutElement>();
        element.minHeight = 76;
        element.preferredHeight = 76;

        // Retrato pequeno: é como o jogador reconhece o herói em todas as outras
        // telas, e sem ele a fila voltaria a ser uma lista de nomes.
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
        int servem = availableCards.Count(c => ServeA(c, hero));

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(row.transform, false);

        var label = labelGo.AddComponent<TextMeshProUGUI>();
        label.fontSize = 18;
        label.alignment = TextAlignmentOptions.Left;
        label.raycastTarget = false;
        label.color = ativo ? new Color(0.98f, 0.92f, 0.72f) : new Color(0.88f, 0.86f, 0.82f);
        label.text = $"{PartyFormation.PreferenceIcon(hero.heroClass)} {hero.heroName}\n"
                   + $"<size=13>baralho {cartas}/{teto}   ·   "
                   + $"{servem} de {availableCards.Count} à venda servem</size>";

        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(hero.portrait != null ? 80 : 14, 4);
        labelRect.offsetMax = new Vector2(-10, -4);

        HeroData capturado = hero;
        row.GetComponent<Button>().onClick.AddListener(() => PorNaMesa(capturado));
    }

    /// <summary>Traz o herói para a mesa. É o único efeito do clique na fila.</summary>
    public void PorNaMesa(HeroData hero)
    {
        if (hero == null || hero.isDead) return;

        naMesa = hero;
        mesaEscolhidaPeloJogador = true;
        SetFeedback($"O que for comprado vai para o baralho de {hero.heroName}.");

        BuildHeroes();
        AtualizarMesa();

        // Os vereditos da estante são todos relativos a quem está na mesa: trocar
        // de herói sem remontá-los deixaria a estante mentindo sobre o dono novo.
        MontarEstante();
    }

    #endregion

    #region A mesa e o baralho que ela mostra

    void AtualizarMesa()
    {
        bool temAlguem = naMesa != null;

        if (deskRoot != null) deskRoot.SetActive(temAlguem);
        if (!temAlguem)
        {
            MontarBaralho();
            return;
        }

        if (portraitImage != null)
        {
            portraitImage.sprite = naMesa.portrait;
            portraitImage.enabled = naMesa.portrait != null;
            portraitImage.preserveAspect = true;
        }

        if (heroNameText != null)
            heroNameText.text = $"{PartyFormation.PreferenceIcon(naMesa.heroClass)} {naMesa.heroName}"
                              + $"  <size=18><color=#B8B0A0>{GetClassName(naMesa.heroClass)} Nv.{naMesa.level}</color></size>";

        DeckData deck = DeckRepository.GetDeck(naMesa);
        int cartas = deck != null && deck.cards != null ? deck.cards.Count : 0;
        int teto = deck != null ? deck.maxDeckSize : 0;

        if (heroStatsText != null)
            heroStatsText.text = $"❤️ {naMesa.currentHp}/{naMesa.maxHp}   ·   baralho {cartas}/{teto}";

        if (deckTitleText != null)
            deckTitleText.text = cartas == 0
                ? $"{naMesa.heroName} não leva carta nenhuma."
                : $"O que {naMesa.heroName} já leva:";

        MontarBaralho();
    }

    /// <summary>
    /// O baralho do herói na mesa, uma linha por carta. É o "antes" contra o qual
    /// a compra se mede — e é ele que muda no clique, à vista de quem pagou.
    /// </summary>
    void MontarBaralho()
    {
        if (deckContainer == null) return;

        UIUtil.ClearChildrenNow(deckContainer);

        if (naMesa == null) return;

        DeckData deck = DeckRepository.GetDeck(naMesa);
        if (deck == null || deck.cards == null) return;

        // Cópias agrupadas: quatro "Corte Duplo" em quatro linhas empurrariam as
        // outras cartas para fora da mesa sem dizer nada a mais.
        var ordem = new List<CardData>();
        var copias = new Dictionary<CardData, int>();

        foreach (CardData card in deck.cards)
        {
            if (card == null) continue;

            if (copias.ContainsKey(card)) { copias[card]++; continue; }

            copias[card] = 1;
            ordem.Add(card);
        }

        foreach (CardData card in ordem)
        {
            Genero genero = GeneroDe(card);
            bool destaque = genero != Genero.Nenhum
                         && ValorDe(card, genero) >= MelhorDoBaralho(deck, genero);

            MontarLinhaDoBaralho(card, copias[card], destaque);
        }
    }

    void MontarLinhaDoBaralho(CardData card, int copias, bool destaque)
    {
        var row = new GameObject(card.cardName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        row.transform.SetParent(deckContainer, false);

        var fundo = row.GetComponent<Image>();
        fundo.raycastTarget = false;
        fundo.color = destaque
            ? new Color(0.24f, 0.21f, 0.28f)
            : new Color(0.15f, 0.14f, 0.16f);

        var element = row.AddComponent<LayoutElement>();
        element.minHeight = 34;
        element.preferredHeight = 34;

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(row.transform, false);

        var label = labelGo.AddComponent<TextMeshProUGUI>();
        label.fontSize = 16;
        label.alignment = TextAlignmentOptions.Left;
        label.raycastTarget = false;
        label.color = destaque ? new Color(0.98f, 0.92f, 0.72f) : new Color(0.86f, 0.84f, 0.80f);

        // O ◆ marca a melhor do tipo. É o número que o veredito da estante cita,
        // e sem a marca o jogador teria de varrer a lista para conferir.
        label.text = $"{(destaque ? "◆" : "·")} {card.cardName}{(copias > 1 ? $" ×{copias}" : "")}"
                   + $"<pos=58%><color=#B8B0A0>{Resumo(card)}</color>";

        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(10, 0);
        labelRect.offsetMax = new Vector2(-10, 0);
    }

    #endregion

    #region A estante

    /// <summary>
    /// As cartas à venda, cada uma num nicho da estante, com o veredito escrito na
    /// própria carta e o preço no botão logo abaixo.
    /// </summary>
    void MontarEstante()
    {
        if (cardsContainer == null) return;

        UIUtil.ClearChildrenNow(cardsContainer);

        int total = Mathf.Min(availableCards.Count, NichosNaEstante);

        if (shelfTitleText != null)
        {
            shelfTitleText.text = total == 0
                ? "A estante está vazia. Melhorar a sala renova o acervo."
                : naMesa == null
                    ? "À venda — não há ninguém vivo para receber carta."
                    : $"À venda — para o baralho de {naMesa.heroName}:";
        }

        if (cardPrefab == null || total == 0) return;

        for (int i = 0; i < total; i++)
            MontarNicho(availableCards[i], i, total);
    }

    void MontarNicho(CardData card, int indice, int total)
    {
        var nicho = new GameObject(card.cardName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        nicho.transform.SetParent(cardsContainer, false);

        // Grade posicionada à mão, como na prateleira da Forja: um LayoutGroup
        // ignora a escala das cartas e abre buracos do tamanho da carta inteira.
        int colunas = Mathf.Min(ColunasDaEstante, total);
        int linhas = Mathf.CeilToInt(total / (float)colunas);
        int coluna = indice % colunas;
        int linha = indice / colunas;

        var rect = (RectTransform)nicho.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(LarguraDoNicho, AlturaDoNicho);
        rect.anchoredPosition = new Vector2(
            (coluna - (colunas - 1) / 2f) * (LarguraDoNicho + EspacoEntreNichos),
            ((linhas - 1) / 2f - linha) * (AlturaDoNicho + EspacoEntreNichos));

        // A moldura do nicho acende com a raridade: é a única coisa que distingue
        // uma lendária de uma comum antes de ler o preço. Sem sprite ela vira uma
        // caixa escura em vez de um retângulo colorido — cor chapada de raridade
        // num quadrado grande lê como erro de UI, não como acabamento.
        var moldura = nicho.GetComponent<Image>();
        moldura.raycastTarget = false;

        if (nicheSprite != null)
        {
            moldura.sprite = nicheSprite;
            moldura.type = Image.Type.Sliced;
            moldura.color = CorDaRaridade(card.rarity);
        }
        else
        {
            moldura.color = new Color(0.13f, 0.12f, 0.14f);
        }

        MontarCartaNoNicho(nicho, card);
        MontarBotaoDeCompra(nicho, card);
    }

    void MontarCartaNoNicho(GameObject nicho, CardData card)
    {
        GameObject view = Instantiate(cardPrefab, nicho.transform);

        var rect = view.transform as RectTransform;
        if (rect != null)
        {
            // Pivô no topo: a escala encolhe a carta para baixo e a borda de cima
            // fica onde foi posta, o que mantém as duas fileiras alinhadas.
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
            rect.localScale = Vector3.one * EscalaDaCarta;
            rect.anchoredPosition = new Vector2(0, -8f);
        }

        // O prefab da carta não tem CardUI: o combate preenche os filhos pelo
        // nome, e a biblioteca precisa do mesmo caminho. Só com o Bind, a carta
        // aparecia com o "New Text" do editor.
        var cardUI = view.GetComponent<CardUI>();
        string descricao = card.GetDescription(false);
        string veredito = Veredito(card);

        if (cardUI != null)
        {
            cardUI.Bind(card, journeyMode: false);
            cardUI.AppendNote(veredito);
        }
        else
        {
            SetTextoDoFilho(view, "CardName", card.cardName);
            SetTextoDoFilho(view, "CardDescription",
                string.IsNullOrEmpty(descricao) ? veredito : descricao + "\n" + veredito);
            SetTextoDoFilho(view, "CostTxt", $"⚡ {card.energyCost}");
            CardUI.AplicarArte(view, card);
        }

        // A carta aqui é mostruário: quem paga é o botão. Arrastá-la para fora do
        // nicho ou clicá-la por engano não pode gastar ouro.
        var drag = view.GetComponent<CardDragHandler>();
        if (drag != null) Destroy(drag);

        // Destruído, e não desligado: um Button desligado continua sendo um
        // botão para quem conta a tela, e seis mostruários faziam a auditoria
        // anunciar dez controles indisponíveis numa sala que tem dois.
        var botao = view.GetComponent<Button>();
        if (botao != null) Destroy(botao);
    }

    void MontarBotaoDeCompra(GameObject nicho, CardData card)
    {
        var go = new GameObject("Btn_Buy", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(nicho.transform, false);
        go.GetComponent<Image>().color = new Color(0.20f, 0.17f, 0.16f);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 0);
        rect.anchorMax = new Vector2(1, 0);
        rect.offsetMin = new Vector2(10, 10);
        rect.offsetMax = new Vector2(-10, 52);

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);

        var text = textGo.AddComponent<TextMeshProUGUI>();
        text.fontSize = 15;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0.94f, 0.88f, 0.72f);
        text.raycastTarget = false;

        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Estado estado = Avaliar(card);
        int preco = GetCardPrice(card.rarity);

        text.text = RotuloDoBotao(estado, preco);

        var botao = go.GetComponent<Button>();
        botao.interactable = estado == Estado.Pode;

        CardData capturada = card;
        botao.onClick.AddListener(() => Comprar(capturada));
    }

    #endregion

    #region Comprar

    /// <summary>Por que a compra pode ou não acontecer. Serve ao botão e ao veredito.</summary>
    enum Estado { SemHeroi, NaoServe, BaralhoCheio, CopiasDemais, SemOuro, Pode }

    Estado Avaliar(CardData card)
    {
        if (card == null || naMesa == null) return Estado.SemHeroi;
        if (!ServeA(card, naMesa)) return Estado.NaoServe;

        DeckData deck = DeckRepository.GetDeck(naMesa);
        if (deck == null || deck.cards == null) return Estado.SemHeroi;

        if (deck.cards.Count >= deck.maxDeckSize) return Estado.BaralhoCheio;
        if (Copias(deck, card) >= DeckManager.MaxCopiasPorCarta) return Estado.CopiasDemais;

        int preco = GetCardPrice(card.rarity);
        if (GuildManager.Instance == null || GuildManager.Instance.gold < preco) return Estado.SemOuro;

        return Estado.Pode;
    }

    static string RotuloDoBotao(Estado estado, int preco)
    {
        switch (estado)
        {
            case Estado.SemHeroi: return "SEM HERÓI NA MESA";
            case Estado.NaoServe: return "NÃO SERVE";
            case Estado.BaralhoCheio: return "BARALHO CHEIO";
            case Estado.CopiasDemais: return $"JÁ TEM {DeckManager.MaxCopiasPorCarta}";
            case Estado.SemOuro: return $"<color=#B04040>COMPRAR   {preco}💰</color>";
            default: return $"COMPRAR   {preco}💰";
        }
    }

    void Comprar(CardData card)
    {
        Estado estado = Avaliar(card);
        int preco = GetCardPrice(card.rarity);

        switch (estado)
        {
            case Estado.SemHeroi:
                SetFeedback("Escolha na fila quem vai receber a carta.");
                return;
            case Estado.NaoServe:
                SetFeedback($"{card.cardName} é carta de {GetClassName(card.requiredClass)}: "
                          + $"{naMesa.heroName} não saberia jogá-la.");
                return;
            case Estado.BaralhoCheio:
                SetFeedback($"O baralho de {naMesa.heroName} está cheio. "
                          + "Tire uma carta em Baralhos para abrir espaço.");
                return;
            case Estado.CopiasDemais:
                SetFeedback($"{naMesa.heroName} já leva {DeckManager.MaxCopiasPorCarta} cópias de {card.cardName}.");
                return;
            case Estado.SemOuro:
                SetFeedback($"Ouro insuficiente: {card.cardName} custa {preco}.");
                return;
        }

        if (GuildManager.Instance == null || !GuildManager.Instance.SpendGold(preco))
        {
            SetFeedback("Ouro insuficiente.");
            return;
        }

        // GetDeck devolve o baralho vivo do repositório, e não uma cópia: mexer
        // nesta lista já é mexer no baralho que a jornada vai usar.
        DeckData deck = DeckRepository.GetDeck(naMesa);
        deck.cards.Add(card);

        availableCards.Remove(card);

        SetFeedback($"{card.cardName} entra no baralho de {naMesa.heroName}: "
                  + $"{deck.cards.Count}/{deck.maxDeckSize} cartas.");

        RefreshLibrary();

        // O ganho sobe sobre o retrato de quem recebeu — é ali, e não na estante,
        // que a compra mudou alguma coisa.
        if (portraitImage != null && portraitImage.gameObject.activeInHierarchy)
            CombatFeedback.Get().ShowText(portraitImage.gameObject,
                $"+ {card.cardName}", new Color(0.50f, 0.78f, 0.41f));
    }

    #endregion

    #region O veredito: para quem serve, e se é melhor

    /// <summary>
    /// A linha que a biblioteca escreve na própria carta. Responde, nesta ordem:
    /// esta carta serve ao herói da mesa? ele já a tem? e ela bate a melhor que
    /// ele leva daquele tipo?
    /// </summary>
    string Veredito(CardData card)
    {
        if (naMesa == null)
            return "<color=#B8B0A0>Escolha na fila quem vai receber.</color>";

        if (!ServeA(card, naMesa))
            return $"<color=#B04040>Não serve a {naMesa.heroName} — carta de {GetClassName(card.requiredClass)}.</color>";

        DeckData deck = DeckRepository.GetDeck(naMesa);
        int copias = Copias(deck, card);

        // O baralho cheio vem antes das cópias e do valor: em 12/12 nenhuma carta
        // entra, e a saída é a tela de Baralhos. A frase que ensina isso existia
        // só dentro de Comprar — e o botão desligado nunca deixava chegar lá.
        if (deck != null && deck.cards != null && deck.cards.Count >= deck.maxDeckSize)
            return $"<color=#B04040>Baralho cheio ({deck.cards.Count}/{deck.maxDeckSize}) — "
                 + "tire uma carta em Baralhos para abrir espaço.</color>";

        if (copias > 0)
        {
            string quantas = copias == 1 ? "esta carta" : $"{copias} cópias desta";
            return $"<color=#B8B0A0>◇ {naMesa.heroName} já leva {quantas}.</color>";
        }

        Genero genero = GeneroDe(card);
        if (genero == Genero.Nenhum)
            return $"<color=#7FB069>◆ {naMesa.heroName} não tem nada assim.</color>";

        int valor = ValorDe(card, genero);
        int melhor = MelhorDoBaralho(deck, genero);
        string tipo = NomeDoGenero(genero);

        if (melhor <= 0)
            return $"<color=#7FB069>◆ {valor} de {tipo} — {naMesa.heroName} não tem nenhuma.</color>";

        if (valor > melhor)
            return $"<color=#7FB069>◆ {valor} de {tipo} — a melhor de {naMesa.heroName} faz {melhor}.</color>";

        return $"<color=#B8B0A0>◇ {valor} de {tipo} — {naMesa.heroName} já tem uma de {melhor}.</color>";
    }

    /// <summary>
    /// Bardo é curinga: é a mesma regra que a tela de Baralhos usa para montar a
    /// coleção de cada herói, e divergir dela faria a biblioteca vender carta que
    /// o editor de deck recusa.
    /// </summary>
    static bool ServeA(CardData card, HeroData hero)
    {
        return card != null && hero != null
            && (card.requiredClass == hero.heroClass || card.requiredClass == HeroClass.Bard);
    }

    static int Copias(DeckData deck, CardData card)
    {
        return deck == null || deck.cards == null ? 0 : deck.cards.Count(c => c == card);
    }

    /// <summary>O que a carta faz de mensurável. Só serve para comparar duas cartas do mesmo tipo.</summary>
    enum Genero { Nenhum, Dano, Bloqueio, Cura }

    static Genero GeneroDe(CardData card)
    {
        if (card == null) return Genero.Nenhum;
        if (card.combatDamage > 0) return Genero.Dano;
        if (card.combatBlock > 0) return Genero.Bloqueio;
        if (card.combatHeal > 0) return Genero.Cura;
        return Genero.Nenhum;
    }

    static int ValorDe(CardData card, Genero genero)
    {
        if (card == null) return 0;

        switch (genero)
        {
            case Genero.Dano: return card.combatDamage;
            case Genero.Bloqueio: return card.combatBlock;
            case Genero.Cura: return card.combatHeal;
            default: return 0;
        }
    }

    static string NomeDoGenero(Genero genero)
    {
        switch (genero)
        {
            case Genero.Dano: return "dano";
            case Genero.Bloqueio: return "bloqueio";
            case Genero.Cura: return "cura";
            default: return "";
        }
    }

    static int MelhorDoBaralho(DeckData deck, Genero genero)
    {
        if (deck == null || deck.cards == null || genero == Genero.Nenhum) return 0;

        int melhor = 0;
        foreach (CardData card in deck.cards)
        {
            if (card == null || GeneroDe(card) != genero) continue;
            melhor = Mathf.Max(melhor, ValorDe(card, genero));
        }

        return melhor;
    }

    /// <summary>A linha da direita na lista do baralho: o que a carta faz e o que custa.</summary>
    static string Resumo(CardData card)
    {
        Genero genero = GeneroDe(card);
        string efeito = genero == Genero.Nenhum
            ? ""
            : $"{ValorDe(card, genero)} de {NomeDoGenero(genero)}   ";

        return $"{efeito}⚡ {card.energyCost}";
    }

    /// <summary>Preenche um texto do prefab da carta pelo nome do filho.</summary>
    static void SetTextoDoFilho(GameObject root, string nomeDoFilho, string valor)
    {
        TMP_Text alvo = root.transform.Find(nomeDoFilho)?.GetComponent<TMP_Text>();
        if (alvo != null) alvo.text = valor;
    }

    #endregion

    #region O estoque e o nível da sala

    void GarantirEstoque()
    {
        string classes = ClassesDaGuilda();

        if (estoqueSorteado && nivelDoEstoque == libraryLevel && classesDoEstoque == classes) return;

        GenerateCardsByLevel();

        estoqueSorteado = true;
        nivelDoEstoque = libraryLevel;
        classesDoEstoque = classes;
    }

    /// <summary>
    /// As classes vivas da guilda, em texto estável — a chave do estoque.
    ///
    /// A estante é sorteada para elas, e precisa ser sorteada de novo quando
    /// entra uma classe que a guilda não tinha: senão o recruta chega e a sala
    /// não tem uma única carta dele. Só quando o <i>conjunto</i> muda, e não a
    /// cada abertura — re-sortear em todo <see cref="RefreshLibrary"/> devolvia à
    /// estante a carta recém-comprada, que é o defeito que fechou esse caminho.
    /// </summary>
    string ClassesDaGuilda()
    {
        if (GuildManager.Instance == null) return "";

        return string.Join(",", GuildManager.Instance.roster
            .Where(h => h != null && h.IsAlive)
            .Select(h => (int)h.heroClass)
            .Distinct()
            .OrderBy(c => c));
    }

    /// <summary>
    /// O que a estante oferece. É a única costura do estoque: quem for fazer o
    /// acervo mudar por ciclo troca o miolo daqui e nada mais precisa saber.
    ///
    /// <b>A estante é sorteada para quem está na guilda.</b> Antes o sorteio
    /// varria o acervo inteiro com uma moeda por carta e ficava com o que caísse.
    /// O roster não entrava na conta: com quatro classes, a maior parte da
    /// estante era carta de classe que ninguém tinha — a captura da sala mostrou
    /// cinco nichos e cinco botões dizendo "NÃO SERVE", e o teste de cliques
    /// anotou "comprar carta: nenhuma disponível". Uma sala de compra que não
    /// vende nada é uma porta a menos, e não uma escolha.
    ///
    /// Agora só entram cartas que servem a <i>alguém</i> do roster, e cada classe
    /// presente tem um nicho reservado antes de o resto ser preenchido. Bardo é
    /// curinga e serve a todos — a mesma regra do <see cref="ServeA"/>.
    /// </summary>
    void GenerateCardsByLevel()
    {
        availableCards.Clear();

        CardData[] allCards = Resources.LoadAll<CardData>("Cards");

        if (allCards.Length == 0)
        {
            Debug.LogWarning("Nenhuma carta encontrada em Resources/Cards! Execute Tools -> Card Creator primeiro.");
            return;
        }

        List<HeroData> guilda = GuildManager.Instance != null
            ? GuildManager.Instance.roster.Where(h => h != null && h.IsAlive).ToList()
            : new List<HeroData>();

        List<CardData> liberadas = allCards.Where(c => c != null && RaridadeLiberada(c.rarity)).ToList();

        // Sem ninguém vivo não há a quem servir, e a estante volta a ser o acervo
        // inteiro — é o que a sala mostra enquanto a guilda não tem heróis.
        List<CardData> servem = guilda.Count == 0
            ? liberadas
            : liberadas.Where(c => guilda.Any(h => ServeA(c, h))).ToList();

        if (servem.Count == 0) servem = liberadas;

        // Um nicho reservado por classe presente: toda ficha da fila precisa ter
        // o que comprar quando o jogador a põe na mesa.
        foreach (HeroClass classe in guilda.Select(h => h.heroClass).Distinct())
        {
            if (availableCards.Count >= NichosNaEstante) break;

            List<CardData> daClasse = servem
                .Where(c => c.requiredClass == classe && !availableCards.Contains(c)).ToList();

            if (daClasse.Count > 0)
                availableCards.Add(daClasse[Random.Range(0, daClasse.Count)]);
        }

        // O resto é sorteio livre dentro do mesmo bolo, sem repetir nicho.
        List<CardData> sobra = servem.Where(c => !availableCards.Contains(c)).ToList();

        while (availableCards.Count < NichosNaEstante && sobra.Count > 0)
        {
            int i = Random.Range(0, sobra.Count);
            availableCards.Add(sobra[i]);
            sobra.RemoveAt(i);
        }
    }

    bool RaridadeLiberada(CardRarity rarity)
    {
        switch (rarity)
        {
            case CardRarity.Common: return libraryLevel >= 1;
            case CardRarity.Rare: return libraryLevel >= 2;
            case CardRarity.Epic: return libraryLevel >= 3;
            case CardRarity.Legendary: return libraryLevel >= 4;
            default: return false;
        }
    }

    string RaridadesLiberadas()
    {
        if (libraryLevel >= 4) return "até lendárias";
        if (libraryLevel >= 3) return "até épicas";
        if (libraryLevel >= 2) return "comuns e raras";
        return "só cartas comuns";
    }

    void AtualizarBotaoDeMelhora()
    {
        if (upgradeButton == null) return;

        bool noMaximo = libraryLevel >= NivelMaximo;
        int cost = upgradeBaseCost * libraryLevel;
        bool temOuro = GuildManager.Instance != null && GuildManager.Instance.gold >= cost;

        upgradeButton.interactable = !noMaximo && temOuro;

        var texto = upgradeButton.GetComponentInChildren<TMP_Text>(true);
        if (texto == null) return;

        texto.text = noMaximo
            ? "ACERVO COMPLETO"
            : temOuro ? $"MELHORAR   {cost}💰"
                      : $"<color=#B04040>MELHORAR   {cost}💰</color>";
    }

    void UpgradeLibrary()
    {
        if (libraryLevel >= NivelMaximo)
        {
            SetFeedback("Esta biblioteca já guarda tudo o que sabe guardar.");
            return;
        }

        int cost = upgradeBaseCost * libraryLevel;

        if (GuildManager.Instance == null || !GuildManager.Instance.SpendGold(cost))
        {
            SetFeedback($"Ouro insuficiente: melhorar a sala custa {cost}.");
            return;
        }

        libraryLevel++;

        // O nível mudou, então o estoque é sorteado de novo — e é justamente esse
        // acervo novo o que o jogador acabou de comprar.
        SetFeedback($"Biblioteca no nível {libraryLevel}. Na estante agora: {RaridadesLiberadas()}.");
        RefreshLibrary();
    }

    #endregion

    int GetCardPrice(CardRarity rarity)
    {
        switch (rarity)
        {
            case CardRarity.Common: return commonCardPrice;
            case CardRarity.Rare: return rareCardPrice;
            case CardRarity.Epic: return epicCardPrice;
            case CardRarity.Legendary: return legendaryCardPrice;
            default: return commonCardPrice;
        }
    }

    static Color CorDaRaridade(CardRarity rarity)
    {
        switch (rarity)
        {
            case CardRarity.Rare: return new Color(0.36f, 0.46f, 0.70f);
            case CardRarity.Epic: return new Color(0.55f, 0.35f, 0.72f);
            case CardRarity.Legendary: return new Color(0.85f, 0.70f, 0.32f);
            default: return new Color(0.42f, 0.40f, 0.38f);
        }
    }

    static string GetClassName(HeroClass heroClass)
    {
        switch (heroClass)
        {
            case HeroClass.Warrior: return "Guerreiro";
            case HeroClass.Mage: return "Mago";
            case HeroClass.Healer: return "Curandeiro";
            case HeroClass.Rogue: return "Ladino";
            case HeroClass.Bard: return "Bardo";
            case HeroClass.Hunter: return "Caçador";
            default: return heroClass.ToString();
        }
    }
}
