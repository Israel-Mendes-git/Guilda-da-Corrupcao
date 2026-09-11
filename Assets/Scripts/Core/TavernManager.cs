using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Taverna: a sala onde a guilda ganha gente.
///
/// É a decisão de mais peso do jogo — o ouro sai agora, o corpo pode não voltar
/// da estrada, e quem morre não volta nunca. A tela precisava carregar esse peso,
/// e não carregava.
///
/// <b>Por que a sala deixou de ser três fichas lado a lado.</b> A versão anterior
/// mostrava os candidatos como cards iguais, com nome, classe e dois números cada
/// um; contratar era clicar no card e ver um aviso preto subir por cima da tela.
/// Nada ali dizia o que a guilda ganhava: dois guerreiros e um curandeiro pareciam
/// a mesma compra, e as cartas que o recruta empresta ao baralho — que é o efeito
/// real da contratação — só apareciam na jornada seguinte, quando ninguém mais
/// liga uma coisa à outra.
///
/// Agora a taverna tem <b>um candidato na mesa por vez</b>: à esquerda, quem
/// espera para ser chamado; no meio, o escolhido, com o rosto grande, o preço e o
/// que ele carrega de bom e de ruim; à direita, as cartas que ele traz e como ele
/// se compara a quem já está no roster. É o desenho da Forja, pelo mesmo motivo
/// que o autor deu: quanto mais interativa, mais retorno percebido.
///
/// <b>O baralho é sorteado antes da contratação, não depois.</b> Se a prateleira
/// mostrasse um baralho de exemplo, o herói chegaria com outro e a vitrine estaria
/// mentindo. O baralho que está na tela é o que vai para o
/// <see cref="DeckRepository"/> no clique.
///
/// <b>Os avisos saíram do popup.</b> O aviso preto do <see cref="UIManager"/>
/// nasce no alto do Canvas e cobria o que estivesse embaixo — na captura, o botão
/// de voltar. O retorno da sala agora é uma linha da própria sala, como na Forja,
/// e o ganho aparece subindo sobre o retrato.
/// </summary>
public class TavernManager : MonoBehaviour
{
    public static TavernManager Instance;

    [Header("Cabeçalho")]
    public TMP_Text goldText;
    public TMP_Text hintText;
    public TMP_Text feedbackText;

    [Header("Quem espera na porta")]
    public Transform recruitContainer;
    public Button refreshButton;
    public TMP_Text refreshCostText;
    public Button closeButton;

    [Header("A mesa")]
    public GameObject tableRoot;
    public Image portraitImage;
    public TMP_Text heroNameText;
    public TMP_Text heroStatsText;
    public TMP_Text heroTraitsText;
    public TMP_Text hireEffectText;
    public Button hireButton;

    [Header("O que ele traz")]
    public TMP_Text cardShelfTitle;
    public TMP_Text compareText;
    public Transform cardShelf;
    public GameObject cardPrefab;

    [Header("Configuração")]
    public int refreshCost = 50;
    public int minLevel = 1;
    public int maxLevel = 3;

    /// <summary>Quantos esperam na porta a cada leva.</summary>
    const int TamanhoDaLeva = 3;

    /// <summary>Quantas cartas do candidato cabem na prateleira sem virar mosaico.</summary>
    const int MaxCartasNaPrateleira = 4;

    /// <summary>A carta do prefab é 245×345; quatro delas cabem na prateleira assim.</summary>
    const float EscalaDaCarta = 0.85f;

    readonly List<HeroData> currentRecruits = new List<HeroData>();

    /// <summary>
    /// O baralho já sorteado de cada candidato. É o que a prateleira mostra e o
    /// que o herói leva ao ser contratado — a promessa e a entrega são o mesmo
    /// objeto, de propósito.
    /// </summary>
    readonly Dictionary<HeroData, DeckData> baralhoDoCandidato = new Dictionary<HeroData, DeckData>();

    HeroData naMesa;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        if (refreshButton != null)
        {
            refreshButton.onClick.RemoveListener(PayToRefresh);
            refreshButton.onClick.AddListener(PayToRefresh);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CloseTavern);
            closeButton.onClick.AddListener(CloseTavern);
        }

        if (hireButton != null)
        {
            hireButton.onClick.RemoveListener(ContratarQuemEstaNaMesa);
            hireButton.onClick.AddListener(ContratarQuemEstaNaMesa);
        }

        AtualizarSala();
    }

    void OnEnable()
    {
        // Só redesenha o que já existe. Sortear a primeira leva aqui era seguro
        // enquanto o componente morava no painel — agora ele mora num objeto
        // sempre ativo, e este OnEnable roda no carregamento da cena, antes de o
        // QuestManager ter acordado. Quem abre a sala é que pede a leva: o
        // UIManager chama RefreshRecruits ao mostrar a taverna.
        if (currentRecruits.Count > 0)
            AtualizarSala();
    }

    /// <summary>Sorteia candidatos novos sem cobrar. Quem cobra é <see cref="PayToRefresh"/>.</summary>
    public void RefreshRecruits()
    {
        GenerateRecruits();
        AtualizarSala();
        EnsureQuestsExist();
    }

    /// <summary>
    /// Renova a leva por ouro. O botão existe desde sempre no script, mas a
    /// cobrança estava comentada e o botão não existia na cena — dava para
    /// rolar candidatos infinitamente de graça, ou não rolar de jeito nenhum.
    /// </summary>
    public void PayToRefresh()
    {
        if (GuildManager.Instance == null) return;

        if (GuildManager.Instance.gold < refreshCost)
        {
            SetFeedback($"<color=#B04040>Ouro insuficiente: a leva nova custa {refreshCost}💰.</color>");
            return;
        }

        if (!GuildManager.Instance.SpendGold(refreshCost)) return;

        RefreshRecruits();
        SetFeedback("A taverna se enche de caras novas.");
    }

    void SetFeedback(string mensagem)
    {
        if (feedbackText != null)
            feedbackText.text = mensagem;
    }

    #region A leva de candidatos

    void GenerateRecruits()
    {
        DescartarBaralhosNaoAdotados();
        currentRecruits.Clear();

        for (int i = 0; i < TamanhoDaLeva; i++)
        {
            HeroData novo = HeroFactory.CreateRandomHero(minLevel, maxLevel);
            currentRecruits.Add(novo);

            // Sorteado aqui, e não na contratação: é este baralho que a
            // prateleira mostra, e ele precisa ser o mesmo que o herói leva.
            baralhoDoCandidato[novo] = DeckGenerator.GenerateDeckForHero(novo);
        }

        naMesa = currentRecruits.FirstOrDefault();
    }

    /// <summary>
    /// Os baralhos de quem não foi contratado são instâncias soltas de
    /// ScriptableObject: sem isto, cada renovação deixaria três para trás pelo
    /// resto da partida. O de quem entrou na guilda já saiu deste dicionário.
    /// </summary>
    void DescartarBaralhosNaoAdotados()
    {
        foreach (var par in baralhoDoCandidato)
            if (par.Value != null) Destroy(par.Value);

        baralhoDoCandidato.Clear();
    }

    /// <summary>
    /// Garante que haja missões no quadro. Antes a taverna regerava as três missões
    /// a cada abertura, o que apagava a missão que o jogador tinha acabado de escolher.
    /// </summary>
    void EnsureQuestsExist()
    {
        if (QuestManager.Instance == null)
        {
            Debug.LogError("QuestManager.Instance é NULL! Certifique-se de que o QuestManager está na cena.");
            return;
        }

        QuestManager.Instance.GarantirQuadro();
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

    #endregion

    #region A sala inteira

    void AtualizarSala()
    {
        int ouro = GuildManager.Instance != null ? GuildManager.Instance.gold : 0;
        int noRoster = GuildManager.Instance != null ? GuildManager.Instance.roster.Count : 0;
        int limite = GuildManager.Instance != null ? GuildManager.Instance.maxRosterSize : 0;

        if (goldText != null)
            goldText.text = $"💰 {ouro}";

        if (hintText != null)
            hintText.text = "Contratar custa o salário do herói e vale para sempre: quem morre na estrada não volta.   "
                          + $"Guilda: {noRoster}/{limite}.";

        // Quem foi contratado saiu da leva, e quem chegou agora precisa de alguém
        // na mesa — a sala nunca fica com o meio vazio tendo candidato na porta.
        if (naMesa == null || !currentRecruits.Contains(naMesa))
            naMesa = currentRecruits.FirstOrDefault();

        MontarFila();
        AtualizarMesa();
        AtualizarBotaoDeRenovar();
    }

    void AtualizarBotaoDeRenovar()
    {
        bool podePagar = GuildManager.Instance != null && GuildManager.Instance.gold >= refreshCost;

        if (refreshCostText != null)
            refreshCostText.text = podePagar
                ? $"Renovar por {refreshCost}💰"
                : $"<color=#B04040>Renovar por {refreshCost}💰</color>";

        if (refreshButton != null)
            refreshButton.interactable = podePagar;
    }

    #endregion

    #region A fila à esquerda

    void MontarFila()
    {
        if (recruitContainer == null) return;

        UIUtil.ClearChildrenNow(recruitContainer);

        foreach (HeroData candidato in currentRecruits)
            MontarFicha(candidato);
    }

    /// <summary>
    /// A ficha da fila. Ela não contrata ninguém: só chama o candidato à mesa. O
    /// botão que cobra mora no meio, onde se vê o que o dinheiro compra — a
    /// versão antiga contratava no clique do card, e era possível gastar 30 de
    /// ouro sem nunca ter visto o herói.
    /// </summary>
    void MontarFicha(HeroData hero)
    {
        bool ativo = hero == naMesa;

        var row = new GameObject(hero.heroName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        row.transform.SetParent(recruitContainer, false);
        row.GetComponent<Image>().color = ativo
            ? new Color(0.30f, 0.24f, 0.16f)
            : new Color(0.17f, 0.15f, 0.14f);

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

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(row.transform, false);

        var label = labelGo.AddComponent<TextMeshProUGUI>();
        label.fontSize = 18;
        label.alignment = TextAlignmentOptions.Left;
        label.raycastTarget = false;
        label.color = ativo ? new Color(0.98f, 0.92f, 0.72f) : new Color(0.88f, 0.86f, 0.82f);

        // Classe e nível na mesma linha, um depois do outro: na versão anterior
        // eram dois textos ancorados no mesmo canto do card e o "Nv.1" saía
        // impresso por cima do nome da classe.
        label.text = $"{PartyFormation.PreferenceIcon(hero.heroClass)} {hero.heroName}\n"
                   + $"<size=13>{GetClassName(hero.heroClass)} Nv.{hero.level}   {PrecoNaFicha(hero)}</size>";

        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(hero.portrait != null ? 80 : 14, 4);
        labelRect.offsetMax = new Vector2(-10, -4);

        HeroData capturado = hero;
        row.GetComponent<Button>().onClick.AddListener(() => PorNaMesa(capturado));
    }

    string PrecoNaFicha(HeroData hero)
    {
        return PodePagar(hero.salary)
            ? $"💰 {hero.salary}"
            : $"<color=#B04040>💰 {hero.salary}</color>";
    }

    /// <summary>Chama o candidato à mesa. É o único efeito do clique na fila.</summary>
    public void PorNaMesa(HeroData hero)
    {
        if (hero == null || !currentRecruits.Contains(hero)) return;

        naMesa = hero;
        MontarFila();
        AtualizarMesa();
    }

    #endregion

    #region A mesa e a prateleira

    void AtualizarMesa()
    {
        bool temAlguem = naMesa != null;

        if (tableRoot != null) tableRoot.SetActive(temAlguem);

        if (!temAlguem)
        {
            MontarCartas();
            if (compareText != null)
                compareText.text = $"Renovar a leva custa {refreshCost}💰.";
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

        if (heroStatsText != null)
            heroStatsText.text = $"❤️ {naMesa.maxHp} de vida   "
                               + $"{PartyFormation.PreferenceIcon(naMesa.heroClass)} {FileiraDe(naMesa)}";

        if (heroTraitsText != null)
            heroTraitsText.text = Bagagem(naMesa);

        if (compareText != null)
            compareText.text = ComparacaoComORoster(naMesa);

        AtualizarBotaoDeContratar();
        MontarCartas();
    }

    /// <summary>Onde o candidato rende. O bardo não tem fileira preferida, e dizer
    /// que ele é de retaguarda seria inventar uma regra que o combate não aplica.</summary>
    static string FileiraDe(HeroData hero)
    {
        FormationRow? preferida = PartyFormation.PreferredRow(hero.heroClass);
        return preferida == null ? "serve em qualquer fileira" : PartyFormation.RowLabel(preferida.Value);
    }

    /// <summary>
    /// O que o candidato carrega de bom e de ruim, com o efeito escrito ao lado.
    /// Sem o efeito, personalidade e traço eram enfeite: "Egoísta" e "Corajoso"
    /// pareciam a mesma informação, e nenhuma das duas mudava a escolha.
    /// </summary>
    static string Bagagem(HeroData hero)
    {
        var linhas = new List<string>();

        string personalidade = GetPersonalityIcon(hero.personality);
        string efeitoPersonalidade = EfeitoDaPersonalidade(hero.personality);
        linhas.Add(efeitoPersonalidade.Length > 0
            ? $"{personalidade} — {efeitoPersonalidade}"
            : personalidade);

        if (hero.trait != Trait.None)
        {
            string traco = GetTraitText(hero.trait);
            string efeitoTraco = EfeitoDoTraco(hero.trait);
            linhas.Add(efeitoTraco.Length > 0 ? $"{traco} — {efeitoTraco}" : traco);
        }

        return string.Join("\n", linhas);
    }

    void AtualizarBotaoDeContratar()
    {
        if (hireButton == null) return;

        bool temVaga = GuildManager.Instance != null && GuildManager.Instance.CanRecruit();
        bool temOuro = PodePagar(naMesa.salary);

        hireButton.interactable = temVaga && temOuro;

        // Incluindo inativos: a sala se atualiza também com o painel fechado —
        // depois da jornada, por exemplo — e aí o rótulo do botão não seria achado.
        var texto = hireButton.GetComponentInChildren<TMP_Text>(true);
        if (texto != null)
        {
            if (!temVaga)
                texto.text = "GUILDA CHEIA";
            else
                texto.text = temOuro
                    ? $"🍺 CONTRATAR   {naMesa.salary}💰"
                    : $"<color=#B04040>🍺 CONTRATAR   {naMesa.salary}💰</color>";
        }

        if (hireEffectText != null)
            hireEffectText.text = OQueMuda();
    }

    /// <summary>
    /// A frase que diz o que o clique faz. É o pedido do autor sobre o botão de
    /// compra: dizer o preço e o que muda, e não só "CONTRATAR".
    /// </summary>
    string OQueMuda()
    {
        if (GuildManager.Instance == null) return "";

        int noRoster = GuildManager.Instance.roster.Count;
        int limite = GuildManager.Instance.maxRosterSize;
        int ouro = GuildManager.Instance.gold;

        if (!GuildManager.Instance.CanRecruit())
            return $"A guilda está cheia: {noRoster}/{limite} heróis.";

        if (!PodePagar(naMesa.salary))
            return $"Faltam {naMesa.salary - ouro}💰 para contratar {naMesa.heroName}.";

        int cartas = BaralhoDe(naMesa)?.cards.Count ?? 0;

        return $"A guilda fica com {noRoster + 1} de {limite} heróis   ·   "
             + $"traz {cartas} cartas ao baralho dele   ·   sobram {ouro - naMesa.salary}💰";
    }

    /// <summary>
    /// As cartas que o candidato traz. É aqui que a contratação deixa de ser um
    /// nome e vira uma promessa concreta: são estas cartas que vão para a mão no
    /// combate, e não outras.
    /// </summary>
    void MontarCartas()
    {
        if (cardShelf == null) return;
        UIUtil.ClearChildrenNow(cardShelf);

        if (naMesa == null)
        {
            if (cardShelfTitle != null)
                cardShelfTitle.text = "Ninguém mais espera na porta.";
            return;
        }

        DeckData deck = BaralhoDe(naMesa);
        List<CardData> distintas = deck?.cards == null
            ? new List<CardData>()
            : deck.cards.Where(c => c != null).Distinct().Take(MaxCartasNaPrateleira).ToList();

        if (cardShelfTitle != null)
        {
            cardShelfTitle.text = distintas.Count == 0
                ? $"Não há cartas de {GetClassName(naMesa.heroClass)} no projeto — ele chegaria sem baralho."
                : $"O que {naMesa.heroName} traz para o baralho ({deck.cards.Count} cartas):";
        }

        if (cardPrefab == null) return;

        for (int i = 0; i < distintas.Count; i++)
        {
            CardData card = distintas[i];
            GameObject view = Instantiate(cardPrefab, cardShelf);

            // Grade posicionada à mão, como na Forja: um LayoutGroup ignoraria a
            // escala e deixaria buracos do tamanho da carta inteira entre elas.
            var rect = view.transform as RectTransform;
            if (rect != null)
            {
                const float Largura = 245f * EscalaDaCarta;
                const float Altura = 345f * EscalaDaCarta;

                int colunas = Mathf.Min(2, distintas.Count);
                int linhas = Mathf.CeilToInt(distintas.Count / (float)colunas);
                int coluna = i % colunas;
                int linha = i / colunas;

                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.localScale = Vector3.one * EscalaDaCarta;
                rect.anchoredPosition = new Vector2(
                    (coluna - (colunas - 1) / 2f) * (Largura + 24f),
                    ((linhas - 1) / 2f - linha) * (Altura + 20f));
            }

            // Quantas cópias daquela carta vêm no baralho. Duas iguais mudam o
            // que o herói faz num turno, e a prateleira mostra cada desenho uma
            // vez só — sem esta nota, a repetição ficaria invisível.
            int copias = deck.cards.Count(c => c == card);
            string nota = copias > 1 ? $"<color=#B8B0A0>◆ ×{copias} no baralho dele</color>" : "";

            var cardUI = view.GetComponent<CardUI>();
            if (cardUI != null)
            {
                cardUI.Bind(card, journeyMode: false);
                if (nota.Length > 0) cardUI.AppendNote(nota);
            }
            else
            {
                // O prefab da carta não tem CardUI: o combate preenche os filhos
                // pelo nome, e a taverna precisa do mesmo caminho.
                string descricao = card.GetDescription(false);
                SetTextoDoFilho(view, "CardName", card.cardName);
                SetTextoDoFilho(view, "CardDescription",
                    nota.Length > 0 ? descricao + "\n" + nota : descricao);
                SetTextoDoFilho(view, "CostTxt", $"⚡ {card.energyCost}");
                CardUI.AplicarArte(view, card);
            }

            // A carta aqui é mostruário: arrastar ou clicar não faz nada.
            var drag = view.GetComponent<CardDragHandler>();
            if (drag != null) Destroy(drag);

            var botao = view.GetComponent<Button>();
            if (botao != null) botao.interactable = false;
        }
    }

    /// <summary>Preenche um texto do prefab da carta pelo nome do filho.</summary>
    static void SetTextoDoFilho(GameObject root, string nomeDoFilho, string valor)
    {
        TMP_Text alvo = root.transform.Find(nomeDoFilho)?.GetComponent<TMP_Text>();
        if (alvo != null) alvo.text = valor;
    }

    /// <summary>
    /// O candidato medido contra quem já está no quadro. Sem isto, o terceiro
    /// guerreiro e o primeiro curandeiro custam o mesmo e parecem valer o mesmo.
    /// </summary>
    static string ComparacaoComORoster(HeroData hero)
    {
        if (GuildManager.Instance == null) return "";

        List<HeroData> vivos = GuildManager.Instance.roster
            .Where(h => h != null && !h.isDead)
            .ToList();

        if (vivos.Count == 0)
            return "A guilda está vazia: ele seria o primeiro nome do quadro.";

        int mesmaClasse = vivos.Count(h => h.heroClass == hero.heroClass);
        string linhaClasse = mesmaClasse == 0
            ? $"A guilda ainda não tem nenhum {GetClassName(hero.heroClass)}."
            : $"Mais um {GetClassName(hero.heroClass)}: a guilda já tem {mesmaClasse}.";

        int frente = vivos.Count(h => PartyFormation.PreferredRow(h.heroClass) == FormationRow.Front);
        int retaguarda = vivos.Count(h => PartyFormation.PreferredRow(h.heroClass) == FormationRow.Back);

        return linhaClasse + $"\nHoje são {frente} de frente e {retaguarda} de retaguarda.";
    }

    DeckData BaralhoDe(HeroData hero)
    {
        if (hero == null) return null;
        return baralhoDoCandidato.TryGetValue(hero, out DeckData deck) ? deck : null;
    }

    #endregion

    #region Contratar

    static bool PodePagar(int custo)
    {
        return GuildManager.Instance != null && GuildManager.Instance.gold >= custo;
    }

    /// <summary>Ligado ao botão da mesa. Sem parâmetro para poder ser removido pelo nome.</summary>
    void ContratarQuemEstaNaMesa()
    {
        TryRecruitHero(naMesa);
    }

    void TryRecruitHero(HeroData hero)
    {
        if (hero == null) return;

        if (GuildManager.Instance == null || !GuildManager.Instance.CanRecruit())
        {
            SetFeedback("<color=#B04040>A guilda está cheia.</color>");
            return;
        }

        if (!PodePagar(hero.salary))
        {
            SetFeedback($"<color=#B04040>Ouro insuficiente: {hero.heroName} pede {hero.salary}💰.</color>");
            return;
        }

        GuildManager.Instance.RecruitHero(hero);

        if (!GuildManager.Instance.roster.Contains(hero))
        {
            SetFeedback($"<color=#B04040>{hero.heroName} não entrou na guilda.</color>");
            return;
        }

        // O baralho que estava na prateleira é o que ele leva. Gerar outro agora
        // faria a vitrine mentir.
        DeckData baralho = BaralhoDe(hero) ?? DeckGenerator.GenerateDeckForHero(hero);
        DeckRepository.SetDeck(hero, baralho);
        baralhoDoCandidato.Remove(hero);

        // O ganho sobe sobre o rosto antes da sala se refazer: o número flutuante
        // é filho do Canvas, então ele sobrevive à troca de quem está na mesa.
        if (portraitImage != null)
            CombatFeedback.Get().ShowText(portraitImage.gameObject,
                $"+{baralho.cards.Count} cartas", new Color(0.50f, 0.78f, 0.41f));

        currentRecruits.Remove(hero);
        naMesa = currentRecruits.FirstOrDefault();

        SetFeedback($"{hero.heroName} entrou na guilda: {GuildManager.Instance.roster.Count}"
                  + $"/{GuildManager.Instance.maxRosterSize} heróis, {baralho.cards.Count} cartas novas no baralho dele.");

        AtualizarSala();
    }

    void CloseTavern()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.CloseTavern();
        }
        else if (recruitContainer != null)
        {
            // O manager deixou de morar dentro do painel — sem UIManager, o
            // fallback antigo (`gameObject.SetActive(false)`) desligaria o
            // próprio manager, e `TavernManager.Instance` continuaria de pé
            // apontando para um objeto inativo: a sala nunca mais abriria e nada
            // apareceria no console. Quem fecha é o painel.
            Transform painel = recruitContainer.root.Find("Canvas/Panel_Tavern")
                            ?? recruitContainer.parent;
            if (painel != null) painel.gameObject.SetActive(false);
        }

        // REATIVA O BOTÃO DA JORNADA
        MapManager mapManager = FindObjectOfType<MapManager>();
        if (mapManager != null)
        {
            mapManager.EnableJourneyButton();
        }
    }

    #endregion

    #region Rótulos

    /// <summary>
    /// O nome da classe sem ícone. O ícone que acompanha o herói vem do
    /// <see cref="PartyFormation.PreferenceIcon"/>, que diz em que fileira ele
    /// rende — informação que a taverna precisa dar antes da contratação, não a
    /// classe duas vezes.
    /// </summary>
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
            default: return "Andarilho";
        }
    }

    static string GetPersonalityIcon(Personality personality)
    {
        switch (personality)
        {
            case Personality.Brave: return "🦁 Corajoso";
            case Personality.Coward: return "🐔 Covarde";
            case Personality.Ambitious: return "⭐ Ambicioso";
            case Personality.Loyal: return "🤝 Leal";
            case Personality.Stubborn: return "🪨 Teimoso";
            case Personality.Selfish: return "👑 Egoísta";
            default: return "❓";
        }
    }

    /// <summary>O que a personalidade faz de fato. Vazio quando ainda não faz nada.</summary>
    static string EfeitoDaPersonalidade(Personality personality)
    {
        switch (personality)
        {
            case Personality.Brave: return "sofre 25% menos estresse";
            case Personality.Coward: return "sofre 35% mais estresse";
            case Personality.Loyal: return "sofre mais quando um aliado cai";
            case Personality.Selfish: return "sofre menos quando um aliado cai";
            default: return "";
        }
    }

    static string GetTraitText(Trait trait)
    {
        switch (trait)
        {
            case Trait.Drunkard: return "🍺 Bêbado";
            case Trait.Lucky: return "🍀 Sortudo";
            case Trait.Scarred: return "⚡ Cicatrizado";
            case Trait.FastHealer: return "💚 Cura Rápida";
            case Trait.Cursed: return "💀 Amaldiçoado";
            default: return "";
        }
    }

    /// <summary>O que o traço faz de fato. Vazio quando ainda não faz nada.</summary>
    static string EfeitoDoTraco(Trait trait)
    {
        switch (trait)
        {
            case Trait.Lucky: return "menos estresse e menos chance de morrer";
            case Trait.Cursed: return "mais estresse e mais chance de morrer";
            case Trait.FastHealer: return "sara ferimentos durante a jornada";
            default: return "";
        }
    }

    #endregion
}
