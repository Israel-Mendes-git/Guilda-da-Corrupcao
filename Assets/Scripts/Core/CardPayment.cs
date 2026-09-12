using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// As salas aceitam carta no lugar de ouro — o primeiro dos três usos do
/// baralho fora da estrada escolhidos pelo autor em 09/09.
///
/// <b>A regra.</b> Uma compra que o ouro não cobre pode ser paga com <b>uma</b>
/// carta cujo valor cubra o preço. O valor é o mesmo que a Biblioteca cobra por
/// ela (comum 100, rara 250, épica 500, lendária 1000) — sem troco: o que sobra
/// é o preço de não ter o ouro. A carta sai do baralho guardado do dono, para
/// sempre. Sem troco também não há arbitragem: comprar uma carta na Biblioteca
/// e pagar com ela devolve exatamente o que custou.
///
/// <b>Só quando falta ouro.</b> Com ouro na mesa, enfraquecer o baralho para
/// poupar moeda não é decisão que alguém tome; sem ouro, é a pergunta inteira:
/// o que eu abro mão para ter isto agora? O botão da compra já diz, antes do
/// clique, que a carta serve — "faltam 30 · ou uma carta rara".
///
/// <b>O baralho tem piso.</b> Um herói não paga com carta se o baralho dele
/// ficar abaixo de <see cref="PisoDoBaralho"/>: a mão da estrada é de cinco, e
/// um baralho de seis não é baralho. As cartas de escrito não se vendem — elas
/// são o que se leva à luta final.
///
/// O painel é montado em execução, como o <see cref="AfflictionMoment"/>: não
/// há prefab nem objeto dormindo na cena para ligar no Inspector.
/// </summary>
public static class CardPayment
{
    /// <summary>Abaixo disto o baralho deixa de ser um baralho.</summary>
    public const int PisoDoBaralho = 8;

    /// <summary>
    /// O valor de cada raridade. Espelha os preços da Biblioteca
    /// (<c>LibraryManager.commonCardPrice</c> e irmãos); o smoke test tranca os
    /// dois juntos.
    /// </summary>
    public static int Valor(CardRarity raridade)
    {
        switch (raridade)
        {
            case CardRarity.Rare: return 250;
            case CardRarity.Epic: return 500;
            case CardRarity.Legendary: return 1000;
            default: return 100;
        }
    }

    public static int Valor(CardData carta) => carta == null ? 0 : Valor(carta.rarity);

    /// <summary>Uma carta com dono, pronta para pagar.</summary>
    public struct Candidata
    {
        public HeroData dono;
        public CardData carta;
    }

    /// <summary>
    /// Quem pode pagar este preço com uma carta: heróis vivos, com baralho acima
    /// do piso, e as cartas deles que valem o preço. Uma linha por carta
    /// distinta de cada herói — duas cópias são a mesma escolha.
    /// </summary>
    public static List<Candidata> Candidatas(int preco)
    {
        var lista = new List<Candidata>();
        var guilda = GuildManager.Instance;
        if (guilda == null) return lista;

        foreach (HeroData heroi in guilda.roster)
        {
            if (heroi == null || !heroi.IsAlive) continue;

            DeckData baralho = DeckRepository.GetDeck(heroi);
            if (baralho?.cards == null || baralho.cards.Count <= PisoDoBaralho) continue;

            foreach (CardData carta in baralho.cards.Where(c => c != null).Distinct())
            {
                if (Escritos.EhEscrito(carta)) continue;
                if (Valor(carta) < preco) continue;

                lista.Add(new Candidata { dono = heroi, carta = carta });
            }
        }

        // As mais baratas primeiro: é a que menos custa que se oferece antes.
        return lista.OrderBy(c => Valor(c.carta)).ThenBy(c => c.dono.heroName).ToList();
    }

    public static bool PodePagar(int preco) => Candidatas(preco).Count > 0;

    /// <summary>
    /// A raridade mais barata que cobre o preço — "ou uma carta rara".
    /// Vazio quando ninguém na guilda tem carta que sirva.
    /// </summary>
    public static string Rotulo(int preco)
    {
        var candidatas = Candidatas(preco);
        if (candidatas.Count == 0) return "";

        return $"ou uma carta {NomeDaRaridade(candidatas[0].carta.rarity)}";
    }

    public static string NomeDaRaridade(CardRarity raridade)
    {
        switch (raridade)
        {
            case CardRarity.Rare: return "rara";
            case CardRarity.Epic: return "épica";
            case CardRarity.Legendary: return "lendária";
            default: return "comum";
        }
    }

    /// <summary>
    /// Tira a carta do baralho guardado do dono, sem volta. Devolve false se ela
    /// já não estava lá.
    /// </summary>
    public static bool Queimar(HeroData dono, CardData carta)
    {
        if (dono == null || carta == null) return false;

        DeckData baralho = DeckRepository.GetDeck(dono);
        if (baralho?.cards == null) return false;

        bool saiu = baralho.cards.Remove(carta);
        if (saiu) GuildManager.Instance?.onRosterChanged?.Invoke();
        return saiu;
    }

    /// <summary>
    /// Tenta cobrar em ouro; sem ouro, oferece a carta. <c>aoPagar</c> roda
    /// quando o preço foi pago por qualquer dos dois caminhos.
    ///
    /// É o ponto único que as salas chamam no lugar de <c>SpendGold</c>. Devolve
    /// true se o ouro pagou na hora — a sala então segue em frente; false
    /// significa que o painel abriu (ou que não havia nem ouro nem carta, caso
    /// em que <c>semComoPagar</c> é chamado).
    /// </summary>
    public static bool Cobrar(int preco, string oQue, Action aoPagar, Action semComoPagar = null)
    {
        var guilda = GuildManager.Instance;
        if (guilda == null) { semComoPagar?.Invoke(); return false; }

        if (guilda.SpendGold(preco))
        {
            aoPagar?.Invoke();
            return true;
        }

        if (!PodePagar(preco))
        {
            semComoPagar?.Invoke();
            return false;
        }

        Oferecer(preco, oQue, aoPagar);
        return false;
    }

    /// <summary>Quem foi a última carta a pagar alguma coisa — o relatório lê.</summary>
    public static string UltimoPagamento { get; private set; } = "";

    // ---------------------------------------------------------- o selo cobra

    /// <summary>
    /// O que o selo cobra em cartas — o terceiro uso do baralho fora da estrada.
    ///
    /// As candidatas são as cartas do papel pedido pela área, entre as do
    /// baralho da jornada cujo dono está vivo e ainda as tem no baralho
    /// guardado; o jogador escolhe quais no balanço. Se faltar carta do papel,
    /// o selo completa <b>sem escolha</b> com as mais raras do resto do
    /// baralho: ir despreparado não bloqueia, custa caro — que é a regra do jogo
    /// para todo requisito. As cartas de escrito ficam de fora: são o que se
    /// leva à luta final.
    ///
    /// Estático e aqui, não no JourneyManager, para o smoke test conferir a
    /// conta sem Play Mode.
    /// </summary>
    public static JourneyReport.Queima MontarQueimaDoSelo(AreaCatalog.Ficha ficha, DeckData deckDaJornada,
                                                         CardOwnership ownership, IList<HeroData> party)
    {
        if (ficha == null || deckDaJornada?.cards == null || party == null) return null;

        var queima = new JourneyReport.Queima { papel = ficha.seloPede, quantas = ficha.seloCobra };

        var vistas = new HashSet<(HeroData, CardData)>();
        var todas = new List<JourneyReport.Queima.Candidata>();

        foreach (CardData carta in deckDaJornada.cards)
        {
            if (carta == null || Escritos.EhEscrito(carta)) continue;

            HeroData dono = ownership != null ? ownership.BestOwner(carta, party) : party.FirstOrDefault();
            if (dono == null || !dono.IsAlive) continue;
            if (!vistas.Add((dono, carta))) continue;

            DeckData guardado = DeckRepository.GetDeck(dono);
            if (guardado?.cards == null || !guardado.cards.Contains(carta)) continue;

            todas.Add(new JourneyReport.Queima.Candidata { dono = dono, carta = carta });
        }

        queima.candidatas = todas.Where(c => CardRoleUtil.Of(c.carta) == ficha.seloPede).ToList();

        int faltam = ficha.seloCobra - queima.candidatas.Count;
        if (faltam > 0)
        {
            var sobras = todas.Where(c => CardRoleUtil.Of(c.carta) != ficha.seloPede)
                              .OrderByDescending(c => (int)c.carta.rarity)
                              .Take(faltam)
                              .ToList();

            foreach (var c in sobras)
            {
                Queimar(c.dono, c.carta);
                queima.queimadasSemEscolha.Add($"{c.carta.cardName} de {c.dono.heroName}");
            }

            queima.quantas = queima.candidatas.Count;
        }

        return queima;
    }

    // ---------------------------------------------------------------- painel

    const string NomeDoPainel = "CardPaymentPanel";
    const int Ordem = 1050;

    static readonly Color Fundo = new Color(0.03f, 0.02f, 0.02f, 0.90f);
    static readonly Color Caixa = new Color(0.14f, 0.12f, 0.10f, 0.98f);
    static readonly Color Creme = new Color(0.97f, 0.94f, 0.86f);
    static readonly Color Dourado = new Color(0.85f, 0.72f, 0.35f);
    static readonly Color Cinza = new Color(0.62f, 0.58f, 0.52f);

    /// <summary>O painel está aberto? O teste de Play Mode pergunta.</summary>
    public static bool Aberto
    {
        get
        {
            Canvas canvas = UIUtil.CanvasPrincipal();
            return canvas != null && canvas.transform.Find(NomeDoPainel) != null;
        }
    }

    /// <summary>
    /// Abre a escolha da carta. Um botão por candidata, e Voltar. Escolher
    /// queima a carta e chama <c>aoPagar</c>.
    /// </summary>
    public static void Oferecer(int preco, string oQue, Action aoPagar)
    {
        Canvas canvas = UIUtil.CanvasPrincipal();
        if (canvas == null) return;

        Fechar();

        var candidatas = Candidatas(preco);
        if (candidatas.Count == 0) return;

        var raiz = new GameObject(NomeDoPainel, typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        raiz.transform.SetParent(canvas.transform, false);
        raiz.transform.SetAsLastSibling();
        Esticar(raiz.GetComponent<RectTransform>());

        var proprio = raiz.GetComponent<Canvas>();
        proprio.overrideSorting = true;
        proprio.sortingOrder = Ordem;

        // O véu segura o clique: por baixo dele a sala continua desenhada, e um
        // clique que atravessasse compraria outra coisa.
        var veu = Imagem(raiz.transform, "Veu", Fundo);
        Esticar(veu.GetComponent<RectTransform>());
        veu.GetComponent<Image>().raycastTarget = true;

        float altura = Mathf.Min(820f, 250f + Mathf.Min(candidatas.Count, 8) * 58f);
        var caixa = Imagem(raiz.transform, "Caixa", Caixa);
        var crt = caixa.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(760f, altura);

        Texto(caixa.transform, "Txt_Titulo", $"Pagar {oQue} com uma carta", 30, Dourado,
              new Vector2(0.5f, 1f), new Vector2(0, -44), new Vector2(720, 44));

        Texto(caixa.transform, "Txt_Regra",
              $"Vale {preco} de ouro. A carta sai do baralho para sempre, e não há troco.",
              18, Cinza, new Vector2(0.5f, 1f), new Vector2(0, -86), new Vector2(720, 30));

        var lista = new GameObject("Lista", typeof(RectTransform), typeof(VerticalLayoutGroup));
        lista.transform.SetParent(caixa.transform, false);
        var lrt = lista.GetComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0f, 0f);
        lrt.anchorMax = new Vector2(1f, 1f);
        lrt.offsetMin = new Vector2(30f, 90f);
        lrt.offsetMax = new Vector2(-30f, -112f);

        var layout = lista.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = true;
        layout.childControlWidth = true;

        int preco0 = preco;
        foreach (Candidata c in candidatas.Take(8))
        {
            Candidata capturada = c;
            string rotulo = $"{capturada.dono.heroName}  ·  {capturada.carta.cardName}"
                          + $"   <size=70%><color=#B8B0A0>{NomeDaRaridade(capturada.carta.rarity)}, vale "
                          + $"{Valor(capturada.carta)}</color></size>";

            Botao(lista.transform, $"Btn_{capturada.carta.name}", rotulo, 50f, () =>
            {
                if (!Queimar(capturada.dono, capturada.carta)) return;

                UltimoPagamento = $"{capturada.carta.cardName} de {capturada.dono.heroName} pagou {preco0}";
                UIManager.Instance?.ShowMessage(
                    $"🃏 {capturada.carta.cardName} sai do baralho de {capturada.dono.heroName}.", 2.5f);

                Fechar();
                aoPagar?.Invoke();
            });
        }

        Botao(caixa.transform, "Btn_Voltar", "Voltar", 0f, Fechar, ancoraEmbaixo: true);
    }

    public static void Fechar()
    {
        Canvas canvas = UIUtil.CanvasPrincipal();
        if (canvas == null) return;

        Transform velho = canvas.transform.Find(NomeDoPainel);
        if (velho != null) UnityEngine.Object.Destroy(velho.gameObject);
    }

    // ------------------------------------------------------------ construção

    static GameObject Imagem(Transform pai, string nome, Color cor)
    {
        var go = new GameObject(nome, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(pai, false);
        var img = go.GetComponent<Image>();
        img.color = cor;
        img.raycastTarget = false;
        return go;
    }

    static void Texto(Transform pai, string nome, string conteudo, int corpo, Color cor,
                      Vector2 ancora, Vector2 posicao, Vector2 tamanho)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        go.transform.SetParent(pai, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = ancora;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = posicao;
        rt.sizeDelta = tamanho;

        var texto = go.AddComponent<TextMeshProUGUI>();
        texto.text = conteudo;
        texto.fontSize = corpo;
        texto.color = cor;
        texto.alignment = TextAlignmentOptions.Center;
        texto.richText = true;
        texto.raycastTarget = false;
    }

    static void Botao(Transform pai, string nome, string rotulo, float altura, Action acao,
                      bool ancoraEmbaixo = false)
    {
        var go = new GameObject(nome, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(pai, false);

        var rt = go.GetComponent<RectTransform>();
        if (ancoraEmbaixo)
        {
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0, 24f);
            rt.sizeDelta = new Vector2(220f, 50f);
        }
        else
        {
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = altura;
            le.minHeight = altura;
        }

        var img = go.GetComponent<Image>();
        img.color = new Color(0.24f, 0.21f, 0.17f, 1f);

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => acao?.Invoke());

        var t = new GameObject("Text (TMP)", typeof(RectTransform));
        t.transform.SetParent(go.transform, false);
        Esticar(t.GetComponent<RectTransform>());

        var texto = t.AddComponent<TextMeshProUGUI>();
        texto.text = rotulo;
        texto.fontSize = 20;
        texto.color = Creme;
        texto.alignment = TextAlignmentOptions.Center;
        texto.richText = true;
        texto.raycastTarget = false;
    }

    static void Esticar(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }
}
