using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mercado: converte ouro em suprimento e em cuidado.
///
/// Divide-se em duas coisas que o resto do jogo já entende. Rações e tochas vão
/// para um estoque que a próxima jornada consome — é o mesmo caminho dos batedores
/// da Sala de Mapas. Tratamento, bandagem e vinho agem na hora, sobre o herói do
/// roster que mais precisa, porque a jornada só devolve 60% do HP e nada apaga um
/// ferimento ou o estresse acumulado.
///
/// <b>Por que a sala deixou de ser uma lista.</b> Era a última das sete assim:
/// dez linhas com um "COMPRAR" cada, sobre 60% de tela vazia. O jogador via o
/// preço e não via em quem a compra ia pegar — o alívio de 18 de estresse era uma
/// frase no rodapé <i>depois</i> do clique, e quem pagava não tinha como ligar o
/// gasto ao efeito. Agora são três faixas, no molde da Forja: a <b>carroça</b> à
/// esquerda, o <b>balcão</b> no meio com um item de cada vez, e à direita
/// <b>em quem a compra pega</b>, com a barra andando no mesmo clique em que o
/// ouro sai.
/// </summary>
public class MarketManager : MonoBehaviour
{
    public static MarketManager Instance;

    [Header("Cabeçalho")]
    public TMP_Text goldText;
    public TMP_Text hintText;

    [Header("A carroça")]
    public Transform itemContainer;

    [Header("O balcão")]
    public GameObject counterRoot;
    public Image counterIcon;
    public TMP_Text counterEmoji;
    public TMP_Text counterName;
    public TMP_Text counterDesc;
    public TMP_Text counterStock;
    public Button buyButton;

    [Header("O efeito")]
    public TMP_Text effectTitle;
    public Transform effectContainer;

    [Header("Rodapé")]
    public TMP_Text feedbackText;
    public Button closeButton;

    [Header("Preços")]
    public int rationCost = 8;
    public int torchCost = 12;
    public int potionCost = 70;
    public int bandageCost = 90;
    public int wineCost = 55;

    [Header("Efeitos")]
    public int potionHeal = 15;
    public float wineStressRelief = 18f;

    /// <summary>
    /// Quantos dos quatro frascos de combate o mercador traz por volta. Metade:
    /// menos do que isso e o jogador não consegue planejar; os quatro sempre à
    /// mão são o que fazia a sala não mudar nunca.
    /// </summary>
    const int FrascosPorCiclo = 2;

    // Comprado aqui, gasto na próxima jornada.
    private int stockedRations;
    private int stockedTorches;

    public int StockedRations => stockedRations;
    public int StockedTorches => stockedTorches;

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
            closeButton.onClick.AddListener(() => UIManager.Instance?.CloseMarket());

        if (buyButton != null)
            buyButton.onClick.AddListener(ComprarOEmFoco);
    }

    public void RefreshMarket()
    {
        // Uma relíquia por vez na prateleira, sorteada na primeira vez que a
        // sala abre e reposta depois de vendida. Sortear a cada abertura deixaria
        // o jogador rolando o dado até sair a que ele quer, o que é o contrário
        // de escolher.
        //
        // A virada de ciclo repõe a prateleira mesmo sem venda: é a carroça nova
        // chegando. Sem isso, quem não comprasse a relíquia da primeira visita
        // veria a mesma pelo resto da partida.
        if (reliquiaDoDia == null || cicloDoEstoque != CycleStock.CicloAtual)
        {
            RenovarReliquia();
            cicloDoEstoque = CycleStock.CicloAtual;
        }

        catalogo = BuildCatalog();

        // O item em foco é procurado pelo nome, e não guardado por referência: o
        // catálogo é reconstruído a cada compra, e a instância anterior morre com
        // ele. Sem isto, comprar uma ração jogava o balcão de volta ao primeiro
        // item e o jogador perdia o lugar onde estava.
        MarketItem foco = catalogo.FirstOrDefault(i => i.nome == nomeEmFoco) ?? catalogo.FirstOrDefault();
        nomeEmFoco = foco != null ? foco.nome : null;

        AtualizarCabecalho();
        MontarCarroca();
        AtualizarBalcao();
        MontarEfeito();
    }

    /// <summary>Em que ciclo a prateleira foi montada. -1 antes da primeira visita.</summary>
    int cicloDoEstoque = -1;

    /// <summary>O que está no balcão. Guardado pelo nome — ver <see cref="RefreshMarket"/>.</summary>
    string nomeEmFoco;

    List<MarketItem> catalogo = new List<MarketItem>();

    void AtualizarCabecalho()
    {
        int gold = GuildManager.Instance != null ? GuildManager.Instance.gold : 0;

        if (goldText != null)
            goldText.text = $"💰 {gold}";

        // O aviso da carroça fica no cabeçalho porque é a mesma pergunta: o que
        // eu levo desta vez. Sem ele, a prateleira muda sozinha entre uma visita
        // e outra e parece bug.
        if (hintText != null)
            hintText.text = "O mercador troca a carroça a cada volta da guilda — "
                          + $"<color=#B8B0A0>ciclo {CycleStock.CicloAtual}</color>.   "
                          + "Escolha à esquerda; à direita, em quem a compra pega.";
    }

    void SetFeedback(string message)
    {
        if (feedbackText != null)
            feedbackText.text = message;
    }

    #region O catálogo

    /// <summary>Onde a compra vai pegar. Decide o que a faixa da direita mostra.</summary>
    enum EfeitoEm { Vida, Ferimento, Estresse, Racoes, Tochas, Pocoes, Reliquias }

    class MarketItem
    {
        public string emoji;
        public string nome;
        public string descricao;
        public int cost;

        /// <summary>Id do catálogo de itens, quando houver ícone desenhado.</summary>
        public string iconId;

        public EfeitoEm efeito;

        /// <summary>Quem recebe, para os itens que agem sobre um herói.</summary>
        public System.Func<HeroData> alvo;

        /// <summary>O que dizer quando não há em quem usar.</summary>
        public string semAlvo;

        public System.Action onBought;

        public bool TemAlvo => alvo == null || alvo() != null;
    }

    List<MarketItem> BuildCatalog()
    {
        var catalog = new List<MarketItem>();

        catalog.Add(new MarketItem
        {
            emoji = "🍖",
            nome = "Ração",
            descricao = "Duas refeições a mais na próxima jornada. Grupo sem comida perde vida e ganha estresse todo dia.",
            cost = rationCost,
            efeito = EfeitoEm.Racoes,
            onBought = () =>
            {
                stockedRations += 2;
                SetFeedback("Rações guardadas para a próxima partida.");
            }
        });

        catalog.Add(new MarketItem
        {
            emoji = "🔥",
            nome = "Tocha",
            descricao = "Uma tocha a mais na próxima jornada. No escuro o grupo apanha mais dos eventos.",
            cost = torchCost,
            efeito = EfeitoEm.Tochas,
            onBought = () =>
            {
                stockedTorches += 1;
                SetFeedback("Tocha guardada para a próxima partida.");
            }
        });

        // "Tratamento", e não "Poção de cura": o catálogo de itens tem uma "Poção
        // de Cura" que vai para a mochila e é bebida em combate, e as duas
        // apareciam lado a lado na mesma prateleira, com o mesmo emoji e nomes que
        // só diferiam por uma maiúscula. Esta age aqui, some no ato e não entra na
        // bagagem de ninguém — o rótulo passa a dizer isso. O nome definitivo é
        // decisão do autor.
        catalog.Add(new MarketItem
        {
            emoji = "🩺",
            nome = "Tratamento",
            descricao = $"O boticário atende aqui mesmo: restaura {potionHeal} de vida a quem estiver pior. Não vai na bagagem.",
            cost = potionCost,
            efeito = EfeitoEm.Vida,
            alvo = MostWounded,
            semAlvo = "Ninguém está machucado.",
            onBought = () =>
            {
                HeroData alvo = MostWounded();
                if (alvo == null) return;

                int curado = Mathf.Min(potionHeal, alvo.maxHp - alvo.currentHp);
                alvo.currentHp += curado;
                SetFeedback($"{alvo.heroName} recupera {curado} de vida.");
            }
        });

        catalog.Add(new MarketItem
        {
            emoji = "🩹",
            nome = "Bandagem",
            descricao = "Trata o ferimento de um herói. Ferimento não sara sozinho — quem volta ferido parte ferido.",
            cost = bandageCost,
            efeito = EfeitoEm.Ferimento,
            alvo = FirstInjured,
            semAlvo = "Ninguém está ferido.",
            onBought = () =>
            {
                HeroData alvo = FirstInjured();
                if (alvo == null) return;

                alvo.isInjured = false;
                SetFeedback($"{alvo.heroName} não está mais ferido.");
            }
        });

        catalog.Add(new MarketItem
        {
            emoji = "🍷",
            nome = "Vinho",
            descricao = $"Alivia {Mathf.RoundToInt(wineStressRelief)} de estresse de quem mais sofre. Acima de 85 o herói recusa partir.",
            cost = wineCost,
            efeito = EfeitoEm.Estresse,
            alvo = MostStressed,
            semAlvo = "Ninguém carrega estresse.",
            onBought = () =>
            {
                HeroData alvo = MostStressed();
                if (alvo == null) return;

                alvo.stress = Mathf.Max(0f, alvo.stress - wineStressRelief);
                SetFeedback($"{alvo.heroName} bebe e respira melhor.");
            }
        });

        // Os frascos de combate, comprados antes de partir. Vão para a
        // prateleira da guilda; quem os leva na estrada se decide na ficha do
        // herói, que é onde se vê quem precisa de quê.
        //
        // Só metade dos frascos aparece por ciclo, e são sempre os mesmos dentro
        // da mesma volta. Com os quatro sempre à mão, o Mercado era uma lista de
        // compras: nada mudava entre uma visita e a próxima, e pular a sala não
        // custava nada.
        foreach (var pocao in CycleStock.Escolher(ItemCatalog.Pocoes, FrascosPorCiclo, "mercado-frascos"))
        {
            PotionDef def = pocao;
            catalog.Add(new MarketItem
            {
                emoji = "🧪",
                nome = def.nome,
                descricao = $"{def.descricao} Vai para a prateleira: quem a leva na estrada se decide na ficha do herói.",
                cost = def.preco,
                iconId = def.id,
                efeito = EfeitoEm.Pocoes,
                onBought = () =>
                {
                    GuildManager.Instance?.GuardarPocao(def.id);
                    SetFeedback($"{def.nome} guardada na prateleira da guilda.");
                }
            });
        }

        // Uma relíquia por visita, sorteada: a loja com o catálogo inteiro
        // sempre à mão transforma escolha em lista de compras, e o preço dela
        // já é alto o bastante para a decisão doer.
        if (reliquiaDoDia != null)
        {
            RelicDef def = reliquiaDoDia;
            catalog.Add(new MarketItem
            {
                emoji = "🏺",
                nome = def.nome,
                descricao = $"{def.descricao} Fica com o herói que a equipar, e se perde com ele.",
                cost = def.preco,
                iconId = def.id,
                efeito = EfeitoEm.Reliquias,
                onBought = () =>
                {
                    GuildManager.Instance?.GuardarReliquia(def.id);
                    reliquiaDoDia = null;
                    SetFeedback($"{def.nome} está na prateleira — equipe alguém na ficha dele.");
                }
            });
        }

        return catalog;
    }

    /// <summary>
    /// A relíquia que este mercador tem hoje. Nula depois de comprada, até a
    /// próxima renovação.
    /// </summary>
    RelicDef reliquiaDoDia;

    /// <summary>
    /// Sorteia a relíquia da vez, pulando o que a guilda já tem — vender de novo
    /// o que está na prateleira faria a loja parecer quebrada.
    ///
    /// O sorteio vem do ciclo (<see cref="CycleStock"/>), e não do gerador
    /// global: com <c>Random.Range</c>, recarregar o save trocava a relíquia da
    /// vitrine, e bastava recarregar até sair a que se queria. Agora a volta 4
    /// oferece a mesma relíquia em qualquer sessão.
    /// </summary>
    public void RenovarReliquia()
    {
        var guilda = GuildManager.Instance;
        var candidatas = new List<RelicDef>();

        foreach (var r in ItemCatalog.Reliquias)
        {
            bool naPrateleira = guilda != null && guilda.relicStock.Contains(r.id);
            if (!naPrateleira) candidatas.Add(r);
        }

        // Repetir a da volta anterior é o mesmo que não trocar a carroça — só
        // vale quando não sobrou outra para oferecer.
        if (candidatas.Count > 1 && ultimaReliquiaOferecida != null)
            candidatas.RemoveAll(r => r.id == ultimaReliquiaOferecida);

        var escolhida = CycleStock.Escolher(candidatas, 1, "mercado-reliquia");

        reliquiaDoDia = escolhida.Count > 0 ? escolhida[0] : null;
        if (reliquiaDoDia != null) ultimaReliquiaOferecida = reliquiaDoDia.id;
    }

    /// <summary>O que a vitrine mostrou por último, para a carroça nova não repetir.</summary>
    string ultimaReliquiaOferecida;

    #endregion

    #region A carroça

    void MontarCarroca()
    {
        if (itemContainer == null) return;

        UIUtil.ClearChildrenNow(itemContainer);

        foreach (var item in catalogo)
            MontarFicha(item);
    }

    /// <summary>
    /// A ficha da carroça. Ela não compra nada: só põe o item no balcão. O que
    /// ela carrega é o motivo para escolher — o preço, e o aviso de quando não dá
    /// para levar, que antes só aparecia como um botão apagado sem explicação.
    /// </summary>
    void MontarFicha(MarketItem item)
    {
        int gold = GuildManager.Instance != null ? GuildManager.Instance.gold : 0;
        bool emFoco = item.nome == nomeEmFoco;
        bool temAlvo = item.TemAlvo;
        bool paga = gold >= item.cost;

        var row = new GameObject(item.nome, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        row.transform.SetParent(itemContainer, false);
        row.GetComponent<Image>().color = emFoco
            ? new Color(0.26f, 0.22f, 0.30f)
            : new Color(0.16f, 0.15f, 0.17f);

        var element = row.AddComponent<LayoutElement>();
        element.minHeight = 72;
        element.preferredHeight = 72;

        Sprite icone = ItemCatalog.Icone(item.iconId);
        if (icone != null)
        {
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconGo.transform.SetParent(row.transform, false);

            var img = iconGo.GetComponent<Image>();
            img.sprite = icone;
            img.preserveAspect = true;
            img.raycastTarget = false;

            var iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0, 0.5f);
            iconRect.anchorMax = new Vector2(0, 0.5f);
            iconRect.sizeDelta = new Vector2(52, 52);
            iconRect.anchoredPosition = new Vector2(38, 0);
        }

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(row.transform, false);

        var label = labelGo.AddComponent<TextMeshProUGUI>();
        label.fontSize = 18;
        label.alignment = TextAlignmentOptions.Left;
        label.raycastTarget = false;
        label.color = emFoco ? new Color(0.98f, 0.92f, 0.72f) : new Color(0.88f, 0.86f, 0.82f);

        string segunda = !temAlvo
            ? $"<color=#A0603C>{item.semAlvo}</color>"
            : !paga
                ? "<color=#A0603C>ouro insuficiente</color>"
                : $"<color=#D4AF37>{item.cost}💰</color>";

        label.text = $"{(icone != null ? "" : item.emoji + " ")}{item.nome}\n<size=14>{segunda}</size>";

        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(icone != null ? 74 : 14, 4);
        labelRect.offsetMax = new Vector2(-10, -4);

        string capturado = item.nome;
        row.GetComponent<Button>().onClick.AddListener(() => PorNoBalcao(capturado));
    }

    /// <summary>Traz o item para o balcão. É o único efeito do clique na carroça.</summary>
    public void PorNoBalcao(string nome)
    {
        nomeEmFoco = nome;

        MontarCarroca();
        AtualizarBalcao();
        MontarEfeito();
    }

    #endregion

    #region O balcão

    MarketItem EmFoco => catalogo.FirstOrDefault(i => i.nome == nomeEmFoco);

    void AtualizarBalcao()
    {
        MarketItem item = EmFoco;
        bool tem = item != null;

        if (counterRoot != null) counterRoot.SetActive(tem);
        if (!tem) return;

        Sprite icone = ItemCatalog.Icone(item.iconId);

        if (counterIcon != null)
        {
            counterIcon.sprite = icone;
            counterIcon.enabled = icone != null;
            counterIcon.preserveAspect = true;
        }

        // O emoji é o desenho de sete dos nove itens: só frascos e relíquias têm
        // arte em Resources/ItemIcons. Onde há sprite, o emoji sai de cena — os
        // dois juntos seriam o mesmo item desenhado duas vezes.
        if (counterEmoji != null)
        {
            counterEmoji.text = icone != null ? "" : item.emoji;
            counterEmoji.enabled = icone == null;
        }

        if (counterName != null)
            counterName.text = item.nome;

        if (counterDesc != null)
            counterDesc.text = item.descricao;

        if (counterStock != null)
            counterStock.text = $"A próxima jornada leva   🍖 {stockedRations}   🔥 {stockedTorches}";

        AtualizarBotao(item);
    }

    void AtualizarBotao(MarketItem item)
    {
        if (buyButton == null) return;

        int gold = GuildManager.Instance != null ? GuildManager.Instance.gold : 0;
        bool temAlvo = item.TemAlvo;
        bool paga = gold >= item.cost;

        buyButton.interactable = temAlvo && paga;

        var label = buyButton.GetComponentInChildren<TMP_Text>(true);
        if (label == null) return;

        // O botão diz por que está travado. Um "COMPRAR" apagado obriga o jogador
        // a adivinhar entre "não tenho ouro" e "não há quem precise" — e as duas
        // pedem coisas opostas dele.
        //
        // O motivo vai em caixa normal: o rótulo de um botão desligado é uma
        // frase, não um comando, e em versal ele gritava mais que o "COMPRAR" das
        // compras que o jogador pode fazer.
        label.text = !temAlvo ? item.semAlvo
                   : !paga ? $"FALTAM {item.cost - gold}💰"
                   : $"COMPRAR   {item.cost}💰";
    }

    void ComprarOEmFoco()
    {
        MarketItem item = EmFoco;
        if (item == null) return;

        if (!item.TemAlvo)
        {
            SetFeedback(item.semAlvo);
            return;
        }

        if (GuildManager.Instance == null || !GuildManager.Instance.SpendGold(item.cost))
        {
            SetFeedback("Ouro insuficiente.");
            return;
        }

        item.onBought();
        RefreshMarket();
    }

    #endregion

    #region Em quem a compra pega

    /// <summary>
    /// A faixa da direita: o efeito do que está no balcão, antes de o ouro sair.
    ///
    /// É a resposta à queixa que abriu esta frente — o efeito da compra não era
    /// perceptível. A barra mostra o "depois" em tom apagado atrás do "agora", e
    /// o número em texto ao lado; comprar move a barra na mesma tela.
    /// </summary>
    void MontarEfeito()
    {
        if (effectContainer == null) return;

        UIUtil.ClearChildrenNow(effectContainer);

        MarketItem item = EmFoco;
        if (item == null) return;

        switch (item.efeito)
        {
            case EfeitoEm.Vida:
                MontarRoster(item, Medida.Vida, potionHeal, "Quem seria atendido:");
                break;

            case EfeitoEm.Ferimento:
                MontarRoster(item, Medida.Ferimento, 0, "Quem seria tratado:");
                break;

            case EfeitoEm.Estresse:
                MontarRoster(item, Medida.Estresse, Mathf.RoundToInt(wineStressRelief), "Quem beberia:");
                break;

            case EfeitoEm.Racoes:
                Titulo("O que a próxima jornada leva:");
                LinhaDeEstoque("🍖 Rações", stockedRations, stockedRations + 2);
                LinhaDeEstoque("🔥 Tochas", stockedTorches, stockedTorches);
                break;

            case EfeitoEm.Tochas:
                Titulo("O que a próxima jornada leva:");
                LinhaDeEstoque("🍖 Rações", stockedRations, stockedRations);
                LinhaDeEstoque("🔥 Tochas", stockedTorches, stockedTorches + 1);
                break;

            case EfeitoEm.Pocoes:
            case EfeitoEm.Reliquias:
                Titulo("O que já está na prateleira da guilda:");
                MontarPrateleira();
                break;
        }
    }

    enum Medida { Vida, Estresse, Ferimento }

    void Titulo(string texto)
    {
        if (effectTitle != null) effectTitle.text = texto;
    }

    /// <summary>
    /// O grupo inteiro, com quem receberia em destaque.
    ///
    /// Mostrar só o alvo deixava a faixa com uma linha e o resto vazio — e, no
    /// caso mais comum logo depois de uma jornada boa, com nenhuma. Listando o
    /// roster, a sala responde à pergunta anterior à compra: <i>alguém precisa
    /// disto?</i> A resposta "ninguém" passa a ser visível em vez de dita.
    /// </summary>
    void MontarRoster(MarketItem item, Medida medida, int delta, string tituloComAlvo)
    {
        HeroData alvo = item.alvo != null ? item.alvo() : null;

        Titulo(alvo != null ? tituloComAlvo : $"{item.semAlvo}   Como o grupo está:");

        var vivos = LivingRoster().ToList();
        if (vivos.Count == 0)
        {
            LinhaDeTexto("A guilda não tem ninguém de pé.", new Color(0.66f, 0.63f, 0.58f));
            return;
        }

        foreach (HeroData hero in vivos)
            LinhaDeHeroi(hero, medida, hero == alvo ? delta : 0, hero == alvo);
    }

    /// <summary>
    /// Um herói e a barra da medida que o item mexe. Só o alvo mostra o "depois";
    /// os outros estão ali para dizer que não precisam.
    /// </summary>
    void LinhaDeHeroi(HeroData hero, Medida medida, int delta, bool destaque)
    {
        if (hero == null) return;

        var row = new GameObject(hero.heroName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        row.transform.SetParent(effectContainer, false);
        row.GetComponent<Image>().color = destaque
            ? new Color(0.26f, 0.22f, 0.30f)
            : new Color(0.17f, 0.16f, 0.17f);
        row.GetComponent<Image>().raycastTarget = false;

        var element = row.AddComponent<LayoutElement>();
        element.minHeight = 88;
        element.preferredHeight = 88;

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

        float max = medida == Medida.Vida ? hero.maxHp : 100f;
        float agora = medida == Medida.Vida ? hero.currentHp : hero.stress;
        float depois = medida == Medida.Vida
            ? Mathf.Min(hero.maxHp, hero.currentHp + delta)
            : Mathf.Max(0f, hero.stress - delta);

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(row.transform, false);

        var label = labelGo.AddComponent<TextMeshProUGUI>();
        label.fontSize = 17;
        label.alignment = TextAlignmentOptions.TopLeft;
        label.raycastTarget = false;
        label.color = new Color(0.90f, 0.88f, 0.84f);

        string linha2 = medida == Medida.Ferimento
            ? "<color=#B04040>🩸 Ferido</color>  →  <color=#5FA85F>tratado</color>"
            : medida == Medida.Vida
                ? $"❤️ {Mathf.RoundToInt(agora)}/{Mathf.RoundToInt(max)}  →  <color=#5FA85F>{Mathf.RoundToInt(depois)}/{Mathf.RoundToInt(max)}</color>"
                : $"😰 {Mathf.RoundToInt(agora)}/100  →  <color=#5FA85F>{Mathf.RoundToInt(depois)}/100</color>";

        label.text = $"{hero.heroName}\n<size=15>{linha2}</size>";

        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0, 1);
        labelRect.anchorMax = new Vector2(1, 1);
        labelRect.offsetMin = new Vector2(hero.portrait != null ? 80 : 14, -52);
        labelRect.offsetMax = new Vector2(-12, -8);

        if (medida == Medida.Ferimento) return;

        Color cor = medida == Medida.Vida
            ? new Color(0.65f, 0.22f, 0.22f)
            : new Color(0.45f, 0.35f, 0.60f);

        BarraComparativa(row.transform, agora / max, depois / max, cor,
                         hero.portrait != null ? 80 : 14);
    }

    /// <summary>
    /// Trilho, o "depois" atrás e o "agora" na frente.
    ///
    /// Quem desenha primeiro é o maior dos dois, sempre: assim a diferença sobra
    /// à vista como uma faixa mais clara, tanto para a barra que cresce (vida)
    /// quanto para a que encolhe (estresse). Com o menor atrás, o efeito ficaria
    /// escondido debaixo do outro preenchimento.
    /// </summary>
    void BarraComparativa(Transform pai, float agora, float depois, Color cor, float margemEsquerda)
    {
        var trilho = new GameObject("Bar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        trilho.transform.SetParent(pai, false);
        trilho.GetComponent<Image>().color = new Color(0.10f, 0.09f, 0.11f);
        trilho.GetComponent<Image>().raycastTarget = false;

        var trilhoRect = trilho.GetComponent<RectTransform>();
        trilhoRect.anchorMin = new Vector2(0, 0);
        trilhoRect.anchorMax = new Vector2(1, 0);
        trilhoRect.offsetMin = new Vector2(margemEsquerda, 14);
        trilhoRect.offsetMax = new Vector2(-12, 32);

        float maior = Mathf.Max(agora, depois);
        float menor = Mathf.Min(agora, depois);

        Preenchimento(trilhoRect, maior, Color.Lerp(cor, Color.white, 0.45f));
        Preenchimento(trilhoRect, menor, cor);
    }

    void Preenchimento(RectTransform trilho, float fracao, Color cor)
    {
        var go = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(trilho, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var img = go.GetComponent<Image>();
        img.color = cor;
        img.raycastTarget = false;

        // Sem sprite, o Filled é ignorado e a barra sai sempre cheia — ver
        // UIUtil.Branco.
        img.sprite = UIUtil.Branco();
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillAmount = Mathf.Clamp01(fracao);
    }

    void LinhaDeEstoque(string rotulo, int agora, int depois)
    {
        string texto = agora == depois
            ? $"{rotulo}   {agora}"
            : $"{rotulo}   {agora}  →  <color=#5FA85F>{depois}</color>";

        LinhaDeTexto(texto, new Color(0.90f, 0.88f, 0.84f));
    }

    /// <summary>
    /// O que a guilda já guardou. Sem isto, a segunda relíquia igual era comprada
    /// sem o jogador ter como saber que já tinha a primeira.
    /// </summary>
    void MontarPrateleira()
    {
        var guilda = GuildManager.Instance;
        if (guilda == null) return;

        var linhas = new List<string>();

        foreach (var grupo in guilda.relicStock.GroupBy(id => id))
        {
            RelicDef def = ItemCatalog.Reliquia(grupo.Key);
            if (def != null) linhas.Add($"🏺 {def.nome}{(grupo.Count() > 1 ? $" ×{grupo.Count()}" : "")}");
        }

        foreach (var grupo in guilda.potionStock.GroupBy(id => id))
        {
            PotionDef def = ItemCatalog.Pocao(grupo.Key);
            if (def != null) linhas.Add($"🧪 {def.nome}{(grupo.Count() > 1 ? $" ×{grupo.Count()}" : "")}");
        }

        if (linhas.Count == 0)
        {
            LinhaDeTexto("A prateleira está vazia.", new Color(0.66f, 0.63f, 0.58f));
            return;
        }

        foreach (string linha in linhas)
            LinhaDeTexto(linha, new Color(0.90f, 0.88f, 0.84f));
    }

    void LinhaDeTexto(string texto, Color cor)
    {
        var row = new GameObject("Linha", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        row.transform.SetParent(effectContainer, false);
        row.GetComponent<Image>().color = new Color(0.17f, 0.16f, 0.17f);
        row.GetComponent<Image>().raycastTarget = false;

        var element = row.AddComponent<LayoutElement>();
        element.minHeight = 42;
        element.preferredHeight = 42;

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(row.transform, false);

        var label = labelGo.AddComponent<TextMeshProUGUI>();
        label.fontSize = 17;
        label.alignment = TextAlignmentOptions.Left;
        label.raycastTarget = false;
        label.color = cor;
        label.text = texto;

        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(14, 0);
        labelRect.offsetMax = new Vector2(-12, 0);
    }

    #endregion

    #region Alvos

    HeroData MostWounded()
    {
        return LivingRoster()
            .Where(h => h.currentHp < h.maxHp)
            .OrderBy(h => h.maxHp > 0 ? (float)h.currentHp / h.maxHp : 1f)
            .FirstOrDefault();
    }

    HeroData FirstInjured()
    {
        return LivingRoster().FirstOrDefault(h => h.isInjured);
    }

    HeroData MostStressed()
    {
        return LivingRoster()
            .Where(h => h.stress > 0f)
            .OrderByDescending(h => h.stress)
            .FirstOrDefault();
    }

    static IEnumerable<HeroData> LivingRoster()
    {
        if (GuildManager.Instance == null) return Enumerable.Empty<HeroData>();
        return GuildManager.Instance.roster.Where(h => h != null && !h.isDead);
    }

    #endregion

    #region Consumo pela jornada

    /// <summary>Rações compradas aqui, entregues à próxima jornada e zeradas.</summary>
    public int ConsumeRations()
    {
        int total = stockedRations;
        stockedRations = 0;
        return total;
    }

    /// <summary>Tochas compradas aqui, entregues à próxima jornada e zeradas.</summary>
    public int ConsumeTorches()
    {
        int total = stockedTorches;
        stockedTorches = 0;
        return total;
    }

    #endregion
}
