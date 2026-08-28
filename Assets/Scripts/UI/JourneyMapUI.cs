using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// O mapa da jornada, visto de cima, ocupando a tela — e a ficha do grupo
/// andando por ele.
///
/// <b>O que mudou em relação ao diagrama de nós.</b> Antes a rota era uma grade
/// de caixinhas no alto da tela: todos os pontos desenhados de saída, os
/// desconhecidos como quadrados com "?", e a topologia inteira visível desde o
/// primeiro dia. Isso entrega ao jogador a informação que ele deveria comprar
/// com batedores, e faz a viagem parecer um formulário.
///
/// Agora o mapa é o cenário: pontos espalhados por um terreno, trilhas ligando
/// uns aos outros, e <b>névoa cobrindo o que ninguém foi ver</b>. O alcance da
/// vista é o do <see cref="MapRoomManager"/> — contratar batedor passa a ter
/// efeito visível, em vez de acrescentar uma linha de texto.
///
/// O posicionamento é feito à mão (RectTransform absoluto) em vez de layout
/// automático: as trilhas precisam saber onde cada ponto ficou, e um LayoutGroup
/// só resolve isso no fim do frame.
/// </summary>
public class JourneyMapUI : MonoBehaviour
{
    public static JourneyMapUI Instance;

    [Header("Pontos")]
    public RectTransform nodeContainer;

    [Tooltip("Ignorado: os pontos do mapa são desenhados por código.")]
    public GameObject nodePrefab;

    [Header("Trilhas")]
    [Tooltip("Deixe vazio para gerar uma imagem simples em runtime.")]
    public GameObject edgePrefab;
    public Color edgeColor = new Color(0.35f, 0.30f, 0.24f, 0.75f);
    public Color edgeAvailableColor = new Color(0.83f, 0.69f, 0.36f, 1f);
    public Color edgeTraveledColor = new Color(0.55f, 0.50f, 0.42f, 1f);
    public float edgeThickness = 4f;

    [Header("Detalhe")]
    public TMP_Text nodeDetailText;

    [Header("A ficha do grupo")]
    public TrailRoadUI partyToken;

    [Tooltip("Segundos que o grupo leva para atravessar um trecho.")]
    public float duracaoDaCaminhada = 1.1f;

    [Header("Layout")]
    [Tooltip("Distância mínima entre dois pontos vizinhos na horizontal.")]
    public float layerSpacing = 300f;
    public float slotSpacing = 185f;
    public float leftMargin = 130f;
    public float rightMargin = 130f;

    [Header("Cores")]
    public Color pastColor = new Color(0.30f, 0.28f, 0.25f);
    public Color currentColor = new Color(0.85f, 0.72f, 0.42f);
    public Color availableColor = new Color(0.42f, 0.60f, 0.38f);
    public Color revealedColor = new Color(0.34f, 0.40f, 0.52f);

    /// <summary>Raio do marcador, em pixels de canvas.</summary>
    const float RaioPonto = 26f;
    const float RaioChefe = 34f;

    readonly Dictionary<int, GameObject> nodeViews = new Dictionary<int, GameObject>();
    readonly List<GameObject> edgeViews = new List<GameObject>();
    JourneyMap map;
    int revealedCount;
    RectTransform terreno;

    /// <summary>
    /// Onde o grupo fica na janela do mapa, como fração da largura.
    ///
    /// Não é o centro: a caixa do evento ocupa o terço esquerdo da tela, e um
    /// grupo centrado ficaria com metade do caminho à frente escondido atrás
    /// dela. À direita da caixa, o que se vê adiante é o que interessa decidir.
    /// </summary>
    /// <remarks>
    /// Subiu de 0,58 para 0,70 quando o mapa passou a ser o fundo da travessia —
    /// para o caminho já andado caber na tela em vez de sair pela borda esquerda.
    /// <b>Voltou para 0,52 em 28/08</b>, a pedido do autor: "mapa e personagens
    /// muito à direita, no canto da tela". A 0,70 o grupo andava a 1.316px de uma
    /// janela de 1.880 e ficava colado na borda, com os pontos seguintes cortados
    /// pelo recorte — e o que vem pela frente é justamente o que se decide.
    ///
    /// 0,52 e não 0,50: a caixa do evento ocupa até 35% da largura, e o grupo
    /// precisa parar à direita dela, não em cima.
    /// </remarks>
    const float AncoraDoGrupo = 0.52f;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    /// <summary>Reconstrói o mapa inteiro. Chamar quando a jornada começa.</summary>
    public void BuildMap(JourneyMap journeyMap, int revealed)
    {
        map = journeyMap;
        revealedCount = revealed;

        if (nodeContainer == null || map == null) return;

        GarantirPapel();
        GarantirTerreno();
        LimparDesenho();

        for (int layer = 0; layer < map.layers.Count; layer++)
        {
            var row = map.layers[layer];
            for (int i = 0; i < row.Count; i++)
                nodeViews[row[i].id] = CriarPonto(row[i], row.Count);
        }

        BuildEdges();
        DecorarTerreno();

        // A ficha começa na borda de onde o grupo veio: a guilda fica atrás da
        // primeira escolha, e não em cima dela.
        PosicionarFicha(PosicaoDePartida(), true);
        CentrarNoGrupo();

        Refresh(map, revealedCount);
    }

    /// <summary>
    /// O terreno: a folha que carrega pontos, trilhas e a ficha, e que desliza
    /// sob a janela do mapa.
    ///
    /// <b>Por que a rota não cabe parada na tela.</b> Com a névoa, o que está
    /// desenhado é só a vizinhança do grupo — dois ou três pontos. Em
    /// coordenadas fixas, isso amontoa tudo no canto de onde a rota começa e
    /// deixa a tela inteira vazia à direita, ficando pior quanto mais o grupo
    /// avança. Deslizando o terreno, o grupo fica sempre no mesmo lugar e é o
    /// mundo que passa — que é como um mapa maior que a tela se lê.
    /// </summary>
    /// <summary>
    /// O pergaminho por baixo da rota.
    ///
    /// <b>A travessia acontece dentro do mesmo mapa em que o destino foi
    /// escolhido.</b> Antes, a rota era um punhado de círculos flutuando sobre a
    /// arte do bioma: nada dizia que aquilo era um mapa, nem que era o mesmo
    /// mundo do passo da preparação. O papel amarra as duas telas — escolher a
    /// região e atravessá-la passam a ser a mesma folha.
    ///
    /// Fica **parado** enquanto o terreno desliza: é a mesa em que o mapa está
    /// aberto, não um dos objetos desenhados nele. E é filho do container, atrás
    /// de tudo, dentro do recorte que já existia.
    ///
    /// Sem o pacote de arte, nada é criado e a tela continua como era.
    /// </summary>
    void GarantirPapel()
    {
        if (nodeContainer == null) return;

        MapArtCatalog arte = MapArtCatalog.Carregar();
        Sprite papel = arte != null ? arte.papel : null;
        if (papel == null) return;

        Transform achado = nodeContainer.Find("PapelDoMapa");
        GameObject go;

        if (achado != null)
        {
            go = achado.gameObject;
        }
        else
        {
            go = new GameObject("PapelDoMapa", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(nodeContainer, false);
        }

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = go.GetComponent<Image>();
        img.sprite = papel;
        img.type = Image.Type.Simple;
        img.raycastTarget = false;

        // Apagado e puxado para a sombra: a 30% de alfa e cor cheia, o
        // pergaminho clareava a tela inteira e apagava o clima da estrada — o
        // jogo é escuro, e a jornada acontece à noite tanto quanto de dia. O
        // papel precisa dizer "isto é um mapa" sem virar o assunto.
        img.color = new Color(0.72f, 0.66f, 0.58f, 0.20f);

        go.transform.SetAsFirstSibling();
    }

    void GarantirTerreno()
    {
        if (terreno != null) return;

        Transform achado = nodeContainer.Find("Terreno");
        if (achado != null)
        {
            terreno = achado as RectTransform;
        }
        else
        {
            var go = new GameObject("Terreno", typeof(RectTransform));
            go.transform.SetParent(nodeContainer, false);
            terreno = go.GetComponent<RectTransform>();
        }

        // Mesmo referencial dos pontos: canto esquerdo, meia altura.
        terreno.anchorMin = terreno.anchorMax = new Vector2(0f, 0.5f);
        terreno.pivot = new Vector2(0f, 0.5f);
        terreno.sizeDelta = Vector2.zero;
        terreno.anchoredPosition = Vector2.zero;

        // A ficha nasce filha do container (é o setup da cena que a cria) e
        // precisa viajar junto com o terreno, senão o mapa desliza por baixo do
        // grupo e a distância entre ele e os pontos muda sozinha.
        if (partyToken != null && partyToken.transform.parent != terreno)
            partyToken.transform.SetParent(terreno, false);
    }

    /// <summary>
    /// Tira do container só o que este mapa desenhou.
    ///
    /// A ficha do grupo também é filha daqui, e um <c>ClearChildren</c> a levaria
    /// junto — com ela some a janela do palco, e a jornada seguinte abriria sem
    /// grupo nenhum na tela e sem erro no console.
    /// </summary>
    void LimparDesenho()
    {
        foreach (var view in nodeViews.Values)
            if (view != null) { view.transform.SetParent(null, false); Destroy(view); }

        foreach (var edge in edgeViews)
            if (edge != null) { edge.transform.SetParent(null, false); Destroy(edge); }

        foreach (var enfeite in enfeites)
            if (enfeite != null) { enfeite.transform.SetParent(null, false); Destroy(enfeite); }

        nodeViews.Clear();
        edgeViews.Clear();
        enfeites.Clear();
    }

    readonly List<GameObject> enfeites = new List<GameObject>();

    /// <summary>
    /// Espalha terreno pelo mapa: montanhas, matas, água.
    ///
    /// <b>O papel estava vazio.</b> Uma rota de círculos ligados por linhas sobre
    /// uma folha em branco lê como diagrama, não como travessia — o mundo entre
    /// um ponto e outro simplesmente não existia. O que enche esse vazio é o
    /// mesmo pacote que desenha o mapa da região: as dez montanhas, as dez
    /// árvores e as ondas.
    ///
    /// <b>Duas regras.</b> Primeira: o que nasce onde vem de uma função do
    /// índice, nunca de <c>Random</c> — o mapa precisa ficar torto como terreno,
    /// mas igual a cada redesenho, e a rota é redesenhada a cada dia. Segunda:
    /// nada nasce perto da trilha; o enfeite que cobre um ponto ou uma ligação
    /// tirou informação da tela para pôr decoração no lugar.
    ///
    /// O bioma escolhe o repertório: a Tundra tem pinheiros, o Pântano tem água,
    /// a Montanha tem serra. É o mesmo mundo do mapa da região, visto de perto.
    /// </summary>
    void DecorarTerreno()
    {
        MapArtCatalog arte = MapArtCatalog.Carregar();
        if (arte == null || terreno == null || map == null) return;

        List<Sprite> repertorio = RepertorioDoBioma(arte);
        if (repertorio.Count == 0) return;

        // Densidade: dois por camada, dos dois lados da rota. Mais que isso vira
        // mato tapando o caminho; menos, e o papel continua vazio.
        //
        // <b>Começa antes do começo.</b> A rota nasce em leftMargin, mas a tela
        // mostra o mundo à esquerda dela — de onde o grupo veio. Decorar só a
        // partir do primeiro dia deixava metade do pergaminho em branco, que era
        // exatamente a queixa: mapa vazio.
        int camadas = Mathf.Max(1, map.layers.Count);
        int primeiro = -6;
        int ultimo = Mathf.Clamp(camadas * 2 + 4, 10, 60);

        for (int i = primeiro; i < ultimo; i++)
        {
            Sprite sprite = repertorio[Mathf.Abs((int)(Torto(i, 17) * 1000f)) % repertorio.Count];

            float x = leftMargin + (i * 0.5f) * EffectiveLayerSpacing() + Torto(i, 23) * 130f;

            // <b>A faixa sai da janela, não da rota.</b> Medindo pela rota, os
            // enfeites nasciam a 400px do eixo numa janela de 644 e caíam todos
            // fora do papel — decoração invisível, que é pior que nenhuma.
            //
            // Aqui eles ocupam a margem entre a última fileira de pontos e a
            // borda rasgada: perto o bastante para preencher, longe o bastante
            // para não cobrir ponto nenhum.
            float metadeDaJanela = nodeContainer.rect.height * 0.5f;
            float bordaDaRota = (map.layers.Count > 0 ? map.layers[0].Count - 1 : 0) * slotSpacing * 0.5f;
            float livre = Mathf.Max(40f, metadeDaJanela - bordaDaRota - 70f);

            float y = (Torto(i, 41) > 0f ? 1f : -1f)
                    * (bordaDaRota + 50f + Mathf.Abs(Torto(i, 29)) * livre * 0.6f);

            float tamanho = 44f + Mathf.Abs(Torto(i, 53)) * 38f;

            var go = new GameObject($"Enfeite_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(terreno, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(tamanho, tamanho);
            rt.anchoredPosition = new Vector2(x, y);

            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;

            // Tinta desbotada: é cenário, e precisa ficar atrás da rota também
            // no peso visual, não só na ordem de desenho. Mas não tão apagada a
            // ponto de sumir — a 55% sobre papel escuro, o terreno mal existia.
            img.color = new Color(0.34f, 0.27f, 0.20f, 0.78f);

            go.transform.SetAsFirstSibling();
            enfeites.Add(go);
        }
    }

    /// <summary>O que se desenha no chão desta região.</summary>
    List<Sprite> RepertorioDoBioma(MapArtCatalog arte)
    {
        var lista = new List<Sprite>();
        if (arte == null) return lista;

        BiomeType bioma = JourneyManager.Instance != null
            ? JourneyManager.Instance.CurrentBiome
            : BiomeType.Forest;

        if (bioma == BiomeType.Any) bioma = BiomeType.Forest;

        // O símbolo da própria região entra sempre: é ele que amarra a travessia
        // ao lugar escolhido no mapa do mundo.
        Sprite proprio = arte.IconeDe(bioma);
        if (proprio != null) { lista.Add(proprio); lista.Add(proprio); }

        // Mais dois vizinhos, para o terreno não virar carimbo repetido.
        Sprite mata = arte.IconeDe(BiomeType.Forest);
        Sprite serra = arte.IconeDe(BiomeType.Mountain);
        Sprite agua = arte.IconeDe(BiomeType.Swamp);

        switch (bioma)
        {
            case BiomeType.Swamp:
            case BiomeType.Tundra:
            case BiomeType.Forest:
                if (mata != null) lista.Add(mata);
                if (agua != null) lista.Add(agua);
                break;

            default:
                if (serra != null) lista.Add(serra);
                if (mata != null) lista.Add(mata);
                break;
        }

        return lista;
    }

    #region Desenho

    GameObject CriarPonto(MapNode node, int rowCount)
    {
        float raio = node.isBoss ? RaioChefe : RaioPonto;

        var go = new GameObject($"Node_{node.id}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(terreno, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(raio * 2f, raio * 2f);
        rt.anchoredPosition = PositionFor(node, rowCount);

        var img = go.GetComponent<Image>();
        img.sprite = UIUtil.Circulo();
        img.type = Image.Type.Simple;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        int captured = node.id;
        btn.onClick.AddListener(() => OnNodeClicked(captured));

        // O ícone diz o que é o ponto; o rótulo, embaixo, dá o nome. Os dois
        // ficam fora do círculo para não brigar com a cor de estado.
        //
        // Com o pacote de mapa no projeto, o símbolo desenhado entra no lugar do
        // emoji — espadas cruzadas, baú, telhado. O emoji continua como reserva:
        // ele nunca falhou, e trocar arte não pode apagar informação.
        var icone = new GameObject("Icon", typeof(RectTransform));
        icone.transform.SetParent(go.transform, false);

        // GetComponent, não AddComponent: o RectTransform já veio no construtor
        // do GameObject. Pedir outro lança exceção no meio da montagem do mapa —
        // uma por ponto, a cada redesenho — e o que se vê é o Editor travando,
        // não um erro de UI.
        var iconeRt = icone.GetComponent<RectTransform>();
        iconeRt.anchorMin = Vector2.zero;
        iconeRt.anchorMax = Vector2.one;
        iconeRt.offsetMin = Vector2.zero;
        iconeRt.offsetMax = Vector2.zero;

        Sprite simbolo = SimboloDe(node);
        TextMeshProUGUI iconeTxt = null;

        if (simbolo != null)
        {
            var img2 = icone.AddComponent<Image>();
            img2.sprite = simbolo;
            img2.preserveAspect = true;
            img2.raycastTarget = false;

            // <b>Cresce para fora do ponto, não para dentro.</b> Sem o disco, o
            // que se vê é só o desenho: encolhido a 70% de um círculo de 26px,
            // a cabana virava um borrão de dez pixels. O ponto continua sendo a
            // área de clique; a ilustração é maior que ela.
            //
            // A correção de tamanho vem do catálogo, medida nos pixels opacos:
            // a torre ocupa quase todo o quadro, a pegada ocupa um terço.
            float folga = raio * (EscalaDoSimbolo(node) - 1f) + raio * 0.55f;
            iconeRt.offsetMin = -Vector2.one * folga;
            iconeRt.offsetMax = Vector2.one * folga;
        }
        else
        {
            iconeTxt = icone.AddComponent<TextMeshProUGUI>();
            iconeTxt.fontSize = node.isBoss ? 30 : 24;
            iconeTxt.alignment = TextAlignmentOptions.Center;
            iconeTxt.raycastTarget = false;
        }

        var label = new GameObject("Label", typeof(RectTransform));
        label.transform.SetParent(go.transform, false);
        var labelTxt = label.AddComponent<TextMeshProUGUI>();
        labelTxt.fontSize = 15;
        labelTxt.alignment = TextAlignmentOptions.Top;
        labelTxt.raycastTarget = false;
        labelTxt.color = new Color(0.86f, 0.83f, 0.76f);
        var labelRt = label.GetComponent<RectTransform>();
        labelRt.anchorMin = new Vector2(0.5f, 0f);
        labelRt.anchorMax = new Vector2(0.5f, 0f);
        labelRt.pivot = new Vector2(0.5f, 1f);
        labelRt.sizeDelta = new Vector2(190f, 22f);
        labelRt.anchoredPosition = new Vector2(0f, -4f);

        return go;
    }

    /// <summary>
    /// Onde o ponto cai no terreno.
    ///
    /// As camadas avançam da esquerda para a direita; dentro de cada uma, os
    /// pontos se distribuem na vertical. O deslocamento extra vem de uma função
    /// do <c>id</c>, e não de <c>Random</c>: o mapa precisa ficar torto como
    /// terreno, mas <b>igual a cada redesenho</b> — sorteio aqui faria os pontos
    /// pularem de lugar toda vez que a tela se atualizasse.
    /// </summary>
    Vector2 PositionFor(MapNode node, int rowCount)
    {
        float x = leftMargin + node.layer * EffectiveLayerSpacing();
        float y = -(node.slot - (rowCount - 1) * 0.5f) * slotSpacing;

        // O chefe fica sempre no eixo, para o fim da rota se ler de longe.
        if (!node.isBoss)
        {
            x += Torto(node.id, 31) * 34f;
            y += Torto(node.id, 57) * 30f;
        }

        return new Vector2(x, y);
    }

    /// <summary>Ruído estável de -1 a 1, derivado do id do ponto.</summary>
    static float Torto(int id, int tempero)
    {
        float s = Mathf.Sin((id + 1) * tempero * 0.7213f) * 43758.5453f;
        return (s - Mathf.Floor(s)) * 2f - 1f;
    }

    /// <summary>
    /// Distância entre camadas — fixa.
    ///
    /// Ela era encolhida para a rota inteira caber na largura da tela, o que
    /// espremia jornadas de nove dias até os pontos se encostarem. Com o terreno
    /// deslizando, o mapa pode ser maior que a janela: a distância entre dois
    /// pontos passa a significar quanto chão há entre eles, e não quantos dias
    /// sobraram para desenhar.
    /// </summary>
    float EffectiveLayerSpacing() => layerSpacing;

    void BuildEdges()
    {
        foreach (var kv in nodeViews)
        {
            MapNode node = map.GetNode(kv.Key);
            if (node == null) continue;

            foreach (int nextId in node.next)
            {
                if (!nodeViews.TryGetValue(nextId, out GameObject targetView)) continue;
                edgeViews.Add(CreateEdge(kv.Value, targetView, node.id, nextId));
            }
        }

        // As trilhas ficam atrás dos pontos.
        foreach (var edge in edgeViews)
            if (edge != null) edge.transform.SetAsFirstSibling();
    }

    GameObject CreateEdge(GameObject fromView, GameObject toView, int fromId, int toId)
    {
        GameObject edge = edgePrefab != null
            ? Instantiate(edgePrefab, terreno)
            : new GameObject("Edge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));

        if (edgePrefab == null)
            edge.transform.SetParent(terreno, false);

        edge.name = $"Edge_{fromId}_{toId}";

        var rt = edge.GetComponent<RectTransform>();
        var a = fromView.GetComponent<RectTransform>().anchoredPosition;
        var b = toView.GetComponent<RectTransform>().anchoredPosition;

        Vector2 delta = b - a;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = a;
        rt.sizeDelta = new Vector2(delta.magnitude, edgeThickness);
        rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

        var img = edge.GetComponent<Image>();
        if (img != null)
        {
            img.color = edgeColor;
            img.raycastTarget = false;
        }

        return edge;
    }

    #endregion

    #region Estado e névoa

    /// <summary>Repinta estados sem recriar nada.</summary>
    public void Refresh(JourneyMap journeyMap, int revealed)
    {
        if (journeyMap != null) map = journeyMap;
        revealedCount = revealed;

        if (map == null) return;

        foreach (var kv in nodeViews)
        {
            MapNode node = map.GetNode(kv.Key);
            if (node != null) PaintNode(kv.Value, node);
        }

        RefreshEdges();
    }

    void PaintNode(GameObject view, MapNode node)
    {
        if (view == null) return;

        NodeState state = GetState(node);

        // Névoa: o que está fora do alcance simplesmente não está no mapa. Um
        // quadrado com "?" ainda conta quantos caminhos existem e onde eles se
        // juntam — que é exatamente o que os batedores vendem.
        bool visivel = state != NodeState.Hidden;
        if (view.activeSelf != visivel) view.SetActive(visivel);
        if (!visivel) return;

        var icon = view.transform.Find("Icon")?.GetComponent<TMP_Text>();
        var label = view.transform.Find("Label")?.GetComponent<TMP_Text>();
        var background = view.GetComponent<Image>();
        var btn = view.GetComponent<Button>();

        if (label != null)
        {
            label.text = DescribeType(node);

            // Sobre o pergaminho, o bege claro do rótulo some. O nome do ponto é
            // metade da informação da rota — a outra metade é o símbolo.
            bool temPapel = nodeContainer != null && nodeContainer.Find("PapelDoMapa") != null;
            label.color = temPapel
                ? new Color(0.14f, 0.11f, 0.09f)
                : new Color(0.86f, 0.83f, 0.76f);

            label.fontStyle = temPapel ? TMPro.FontStyles.Bold : TMPro.FontStyles.Normal;
        }
        if (icon != null) icon.text = state == NodeState.Past ? "✓" : GetIcon(node.eventData);

        // Com símbolo desenhado não há texto para trocar por "✓": o que marca o
        // ponto já percorrido é o próprio símbolo apagando, junto com o fundo.
        // Sem o disco por trás, é a tinta do desenho que diz o estado: o lugar já
        // visitado desbota, o alcançável é tinta cheia, e o que os batedores
        // apenas avistaram fica pálido, como coisa vista de longe.
        var iconImg = view.transform.Find("Icon")?.GetComponent<Image>();
        if (iconImg != null)
        {
            switch (state)
            {
                case NodeState.Past:
                    iconImg.color = new Color(0.32f, 0.26f, 0.20f, 0.45f);
                    break;
                case NodeState.Current:
                case NodeState.Available:
                    iconImg.color = new Color(0.16f, 0.12f, 0.08f, 1f);
                    break;
                default:
                    iconImg.color = new Color(0.30f, 0.25f, 0.20f, 0.60f);
                    break;
            }
        }

        if (background != null)
        {
            bool comSimbolo = iconImg != null && iconImg.sprite != null;

            // <b>Com o lugar desenhado, o disco sai de cena.</b> O ponto passa a
            // ser a própria cabana, a aldeia, a caverna — carimbada no papel,
            // como num mapa de verdade. Um círculo colorido atrás de cada uma
            // devolveria a leitura de diagrama que a arte veio desfazer.
            //
            // O estado, que era a cor do disco, passa a ser dito pela tinta do
            // desenho e por um halo fraco só embaixo do ponto atual: é onde o
            // jogador precisa olhar antes de escolher.
            if (comSimbolo)
            {
                background.color = state == NodeState.Current
                    ? new Color(0.90f, 0.78f, 0.45f, 0.55f)
                    : new Color(0f, 0f, 0f, 0f);
            }
            else
            {
                switch (state)
                {
                    case NodeState.Past: background.color = pastColor; break;
                    case NodeState.Current: background.color = currentColor; break;
                    case NodeState.Available: background.color = availableColor; break;
                    default: background.color = revealedColor; break;
                }
            }
        }

        // Alcançável não basta: o mapa só aceita cliques na janela em que a
        // jornada está de fato esperando a escolha da rota.
        if (btn != null)
        {
            bool aceitandoEscolha = JourneyManager.Instance == null || JourneyManager.Instance.IsChoosingRoute;
            btn.interactable = state == NodeState.Available && aceitandoEscolha;
        }
    }

    NodeState GetState(MapNode node)
    {
        if (node.visited)
            return map.Current != null && map.Current.id == node.id
                ? NodeState.Current
                : NodeState.Past;

        if (map.IsReachable(node.id)) return NodeState.Available;

        // Batedores enxergam além do alcance imediato. Sem nenhum contratado, o
        // grupo vê só o passo à frente — que é o que ele alcança com os olhos.
        int currentLayer = map.CurrentLayer;
        if (node.layer <= currentLayer + 1 + revealedCount) return NodeState.Revealed;

        return NodeState.Hidden;
    }

    void RefreshEdges()
    {
        MapNode current = map.Current;

        foreach (var edge in edgeViews)
        {
            if (edge == null) continue;

            var img = edge.GetComponent<Image>();
            if (img == null) continue;

            // Trilha que leva ao desconhecido some junto com o ponto: uma linha
            // que termina no nada entrega que há algo ali.
            bool visivel = PontasVisiveis(edge.name);
            if (edge.activeSelf != visivel) edge.SetActive(visivel);
            if (!visivel) continue;

            bool fromCurrent = current != null && edge.name.StartsWith($"Edge_{current.id}_");
            bool traveled = IsTraveledEdge(edge.name);

            img.color = fromCurrent ? edgeAvailableColor
                      : traveled ? edgeTraveledColor
                                 : edgeColor;

            // Sobre o pergaminho, o percorrido precisa ser o traço mais forte do
            // mapa: é ele que responde "por onde viemos". A 1,5× e cinza-claro,
            // ele se confundia com as ligações que ninguém tomou — e o caminho
            // andado sumia no meio das alternativas descartadas.
            bool temPapel = nodeContainer != null && nodeContainer.Find("PapelDoMapa") != null;

            if (traveled && temPapel) img.color = new Color(0.24f, 0.17f, 0.11f, 1f);

            var rt = edge.GetComponent<RectTransform>();
            if (rt != null)
                rt.sizeDelta = new Vector2(rt.sizeDelta.x,
                    fromCurrent ? edgeThickness * 2f
                    : traveled ? edgeThickness * (temPapel ? 2.6f : 1.5f)
                    : edgeThickness);
        }
    }

    bool PontasVisiveis(string edgeName)
    {
        if (!Pontas(edgeName, out int from, out int to)) return false;

        MapNode a = map.GetNode(from);
        MapNode b = map.GetNode(to);

        return a != null && b != null
            && GetState(a) != NodeState.Hidden
            && GetState(b) != NodeState.Hidden;
    }

    bool IsTraveledEdge(string edgeName)
    {
        if (!Pontas(edgeName, out int from, out int to)) return false;

        MapNode a = map.GetNode(from);
        MapNode b = map.GetNode(to);

        return a != null && b != null && a.visited && b.visited;
    }

    /// <summary>Formato: Edge_{from}_{to}</summary>
    static bool Pontas(string edgeName, out int from, out int to)
    {
        from = to = -1;

        var partes = edgeName.Split('_');
        if (partes.Length < 3) return false;

        return int.TryParse(partes[1], out from) && int.TryParse(partes[2], out to);
    }

    #endregion

    #region A ficha do grupo

    /// <summary>Onde a ficha espera antes da primeira escolha.</summary>
    Vector2 PosicaoDePartida()
    {
        return new Vector2(leftMargin - EffectiveLayerSpacing() * 0.6f, 0f);
    }

    /// <summary>A posição de um ponto no mapa, para a ficha ir até lá.</summary>
    public Vector2 PosicaoDoNo(int nodeId)
    {
        if (!nodeViews.TryGetValue(nodeId, out GameObject view) || view == null)
            return PosicaoDePartida();

        return view.GetComponent<RectTransform>().anchoredPosition;
    }

    void PosicionarFicha(Vector2 onde, bool ligar = false)
    {
        if (partyToken == null) return;

        var rt = partyToken.GetComponent<RectTransform>();
        if (rt == null) return;

        // Mesmo referencial dos pontos, senão "ir até o ponto" leva a ficha para
        // um lugar que não é o dele — foi o que pôs o grupo no rodapé da tela.
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.anchoredPosition = onde;

        if (ligar) partyToken.transform.SetAsLastSibling();
    }

    /// <summary>
    /// Desliza o terreno para o grupo ficar sempre no mesmo ponto da janela.
    /// </summary>
    /// <summary>
    /// Traz o trecho de rota em volta do grupo para dentro do quadro.
    ///
    /// <b>Só no eixo horizontal.</b> Antes o terreno também subia e descia para
    /// pôr o grupo na altura do meio, e o efeito era o oposto do pretendido: a
    /// rota inteira balançava a cada escolha, e os pontos da fileira de baixo
    /// saíam pela borda rasgada do papel. A rota cabe de pé na janela — o que
    /// precisa rolar é o comprimento dela, que cresce com os dias.
    /// </summary>
    void CentrarNoGrupo()
    {
        if (terreno == null || partyToken == null || nodeContainer == null) return;

        var ficha = partyToken.GetComponent<RectTransform>();
        if (ficha == null) return;

        float alvoX = nodeContainer.rect.width * AncoraDoGrupo;
        terreno.anchoredPosition = new Vector2(alvoX - ficha.anchoredPosition.x, 0f);
    }

    /// <summary>
    /// O mesmo, mas alcançando o alvo aos poucos.
    ///
    /// Usado <b>enquanto o grupo caminha</b>: recentrar a cada quadro faz o
    /// terreno andar exatamente o que o grupo andou, e o resultado na tela é um
    /// grupo parado com o mapa escorregando por baixo — ninguém vê ninguém
    /// caminhar. Deixando a câmera atrasada, o grupo <i>sai</i> do ponto de
    /// origem, percorre a trilha e é alcançado depois.
    /// </summary>
    void SeguirGrupoDeLonge(float suavidade)
    {
        if (terreno == null || partyToken == null || nodeContainer == null) return;

        var ficha = partyToken.GetComponent<RectTransform>();
        if (ficha == null) return;

        float alvoX = nodeContainer.rect.width * AncoraDoGrupo;
        var alvo = new Vector2(alvoX - ficha.anchoredPosition.x, 0f);

        terreno.anchoredPosition = Vector2.Lerp(terreno.anchoredPosition, alvo,
                                                Mathf.Clamp01(suavidade));
    }

    /// <summary>
    /// Leva a ficha até o ponto, no tempo da caminhada.
    ///
    /// Quem chama espera esta corrotina terminar antes de abrir o evento: é isso
    /// que dá à jornada o ritmo de andar e parar, em vez de teleportar o grupo e
    /// jogar uma tela de texto na cara do jogador.
    /// </summary>
    public IEnumerator Caminhar(int nodeId)
    {
        if (partyToken == null) yield break;

        var rt = partyToken.GetComponent<RectTransform>();
        if (rt == null) yield break;

        Vector2 origem = rt.anchoredPosition;
        Vector2 destino = PosicaoDoNo(nodeId);
        float duracao = Mathf.Max(0.05f, duracaoDaCaminhada);

        partyToken.Andar(true);

        for (float t = 0f; t < duracao; t += Time.deltaTime)
        {
            // Suavizado nas pontas: sair e chegar com solavanco é o que denuncia
            // interpolação linear numa tela que quer parecer uma viagem.
            float p = Mathf.SmoothStep(0f, 1f, t / duracao);
            rt.anchoredPosition = Vector2.Lerp(origem, destino, p);

            // A câmera vem atrás, não junto: é o que faz o grupo percorrer a
            // trilha na tela em vez de ficar pregado no mesmo pixel enquanto o
            // mapa desliza — a viagem existia no código e não aparecia.
            SeguirGrupoDeLonge(Time.deltaTime * 2.2f);
            yield return null;
        }

        rt.anchoredPosition = destino;

        // Fecha a distância que sobrou, ainda em movimento: cortar para a
        // posição final devolveria o solavanco que a suavização evitou.
        for (float t = 0f; t < 0.35f; t += Time.deltaTime)
        {
            SeguirGrupoDeLonge(Time.deltaTime * 6f);
            yield return null;
        }

        CentrarNoGrupo();
        partyToken.Andar(false);
    }

    #endregion

    /// <summary>
    /// Os pontos que aceitam clique agora.
    ///
    /// Existe para o teste não precisar saber onde os pontos moram na
    /// hierarquia. Quando o terreno entrou entre o container e os pontos, o
    /// probe — que varria os filhos diretos — parou de achar qualquer um, e a
    /// jornada rodou 600 iterações sem escolher rota nenhuma. Perguntar ao mapa
    /// quem está clicável é estável; adivinhar a árvore não é.
    /// </summary>
    public IEnumerable<Button> PontosClicaveis()
    {
        foreach (var kv in nodeViews)
        {
            GameObject view = kv.Value;
            if (view == null || !view.activeInHierarchy) continue;

            var btn = view.GetComponent<Button>();
            if (btn != null && btn.interactable) yield return btn;
        }
    }

    void OnNodeClicked(int nodeId)
    {
        if (map == null) return;

        MapNode node = map.GetNode(nodeId);
        if (node == null) return;

        ShowNodeDetail(node);

        // Clicar num ponto alcançável é a forma de escolher a rota.
        if (map.IsReachable(nodeId))
            JourneyManager.Instance?.OnNodeChosen(nodeId);
    }

    void ShowNodeDetail(MapNode node)
    {
        if (nodeDetailText == null) return;

        NodeState state = GetState(node);
        string dia = $"Dia {node.layer + 1}";

        switch (state)
        {
            case NodeState.Hidden:
                nodeDetailText.text = $"{dia}: território desconhecido.\nContrate batedores na Sala de Mapas.";
                break;

            case NodeState.Past:
                nodeDetailText.text = $"{dia}: {node.eventData?.eventTitle} — já resolvido.";
                break;

            default:
                nodeDetailText.text = $"{dia}: {node.eventData?.eventTitle}\n{node.eventData?.description}";
                break;
        }
    }

    #region Rótulos

    enum NodeState { Past, Current, Available, Revealed, Hidden }

    static string DescribeType(MapNode node)
    {
        if (node == null) return "";
        if (node.isBoss) return "Chefe";
        if (node.eventData == null) return "?";

        switch (node.eventData.eventType)
        {
            case JourneyEventType.Combat: return "Combate";
            case JourneyEventType.Treasure: return "Tesouro";
            case JourneyEventType.Trap: return "Perigo";
            case JourneyEventType.Rest: return "Descanso";
            case JourneyEventType.Shop: return "Mercador";
            case JourneyEventType.Story: return "História";
            default: return "Jornada";
        }
    }

    /// <summary>
    /// O símbolo desenhado deste ponto, ou null quando o pacote de mapa não está
    /// no projeto — aí vale o emoji de <see cref="GetIcon"/>.
    /// </summary>
    static Sprite SimboloDe(MapNode node)
    {
        MapArtCatalog arte = MapArtCatalog.Carregar();
        if (arte == null || node == null) return null;

        if (node.isBoss) return arte.marcoDeChefe;
        if (node.eventData == null) return null;

        return arte.IconeDe(node.eventData.eventType);
    }

    /// <summary>Quanto este desenho precisa crescer para ter presença no mapa.</summary>
    static float EscalaDoSimbolo(MapNode node)
    {
        MapArtCatalog arte = MapArtCatalog.Carregar();
        if (arte == null || node == null) return 1f;

        if (node.isBoss) return arte.escalaDoChefe > 0f ? arte.escalaDoChefe : 1f;
        if (node.eventData == null) return 1f;

        return arte.EscalaDe(node.eventData.eventType);
    }

    /// <summary>
    /// Glifos que a fonte do jogo tem — os emoji entram pelo fallback. Nada de
    /// símbolos exóticos: já houve caso de ✦ virar caixinha em cinco telas.
    /// </summary>
    static string GetIcon(EventData data)
    {
        if (data == null) return "🚶";
        if (data.isBossEvent) return "💀";

        switch (data.eventType)
        {
            case JourneyEventType.Combat: return "⚔️";
            case JourneyEventType.Treasure: return "💰";
            case JourneyEventType.Trap: return "⚠️";
            case JourneyEventType.Rest: return "🔥";
            case JourneyEventType.Shop: return "🛒";
            case JourneyEventType.Story: return "📜";
            default: return "🚶";
        }
    }

    #endregion
}
