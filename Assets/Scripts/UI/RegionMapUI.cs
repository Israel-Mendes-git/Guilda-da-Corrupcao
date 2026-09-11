using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// O mapa-múndi: uma folha maior que a tela, que o jogador percorre para
/// procurar destino.
///
/// <b>É a interface de mapa de um jogo — arrastar e aproximar —, com outra
/// função.</b> Em <i>Hollow Knight</i> ou <i>Ori</i> o mapa serve para o jogador
/// se localizar; aqui ele serve para escolher para onde a expedição vai. O que
/// se percorre é o mundo, e o clique é a decisão.
///
/// <b>Por que não é mais um diagrama.</b> A primeira versão punha sete símbolos
/// em posições fixas, com três linhas de texto embaixo de cada um e retas
/// ligando ponto a ponto — um grafo sobre pergaminho. Agora cada área é um
/// <b>lugar com extensão</b>, o caminho entre elas serpenteia, o rio e a serra
/// dizem por que uma área é longe, e o texto que sobra na folha é só o nome.
///
/// Monta-se por código, em runtime, como antes: a cena é grande e montada por
/// ferramenta de Editor, e um painel novo dependendo de hierarquia exata seria
/// mais uma coisa para sair de sincronia. Tudo o que a tela mostra nasce do
/// <see cref="AreaCatalog"/> e do <see cref="RegionMap"/>.
/// </summary>
public class RegionMapUI : MonoBehaviour,
    IDragHandler, IBeginDragHandler, IEndDragHandler, IScrollHandler
{
    // ── o papel ────────────────────────────────────────────────────────────

    /// <summary>
    /// Tamanho da folha, em pixels. Deitada, e bem maior que a janela: é o que
    /// obriga a percorrer.
    /// </summary>
    static readonly Vector2 TamanhoDoPapel = new Vector2(2400f, 1400f);

    /// <summary>
    /// O quanto se vê ao abrir.
    ///
    /// Nem tudo (aí não haveria o que procurar), nem colado (aí o jogador não
    /// saberia que há mundo além). A 0,8 a janela mostra o sul inteiro e o
    /// começo do norte — e o fundo do mapa exige arrastar.
    /// </summary>
    const float EscalaInicial = 0.8f;

    /// <summary>Afastar até ver a folha inteira; aproximar até ler de perto.</summary>
    const float EscalaMin = 0.52f;
    const float EscalaMax = 1.6f;

    /// <summary>Raio de um território, em fração da largura do papel.</summary>
    const float RaioDoTerritorio = 0.082f;

    RectTransform janela;
    RectTransform papel;
    float escala = EscalaInicial;

    /// <summary>
    /// Houve arrasto desde que o botão desceu?
    ///
    /// O arrasto e o clique usam o mesmo botão, e o UGUI entrega o clique mesmo
    /// quando o ponteiro andou — então, sem isto, todo arrasto que terminasse
    /// sobre um território escolheria aquele destino.
    /// </summary>
    bool arrastou;

    readonly List<GameObject> desenho = new List<GameObject>();
    QuestSelectionUI dono;
    AreaType selecionada = AreaType.None;

    // ── cores ──────────────────────────────────────────────────────────────

    static readonly Color CorLimpa = new Color(0.44f, 0.52f, 0.32f);
    static readonly Color CorPodre = new Color(0.52f, 0.17f, 0.15f);
    static readonly Color CorSelada = new Color(0.78f, 0.63f, 0.32f);
    static readonly Color CorTinta = new Color(0.16f, 0.13f, 0.10f);
    static readonly Color CorTrilha = new Color(0.34f, 0.26f, 0.18f, 0.8f);
    static readonly Color CorRio = new Color(0.42f, 0.48f, 0.53f, 0.75f);
    static readonly Color CorForaDoPapel = new Color(0.07f, 0.06f, 0.055f);

    // ── construção ─────────────────────────────────────────────────────────

    /// <summary>Cria o mapa dentro do painel dado, ou devolve o que já existe ali.</summary>
    public static RegionMapUI Montar(GameObject painelDoPasso1, QuestSelectionUI dono)
    {
        if (painelDoPasso1 == null) return null;

        RegionMapUI existente = painelDoPasso1.GetComponentInChildren<RegionMapUI>(true);
        if (existente != null)
        {
            existente.dono = dono;
            return existente;
        }

        var go = new GameObject("RegionMapRoot", typeof(RectTransform));
        go.transform.SetParent(painelDoPasso1.transform, false);

        var rt = go.GetComponent<RectTransform>();

        // Dois terços da largura do passo, altura inteira: o mapa é o assunto da
        // tela. O terço restante é a coluna que fala do lugar apontado.
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0.66f, 1f);
        rt.offsetMin = new Vector2(16f, 16f);
        rt.offsetMax = new Vector2(-12f, -16f);

        // Último irmão: em UGUI quem nasce depois é desenhado por cima. Como
        // primeiro, o mapa ficava atrás do Scroll View da lista antiga — que tem
        // fundo opaco — e a tela aparecia vazia, sem erro nenhum no console.
        rt.SetAsLastSibling();

        var mapa = go.AddComponent<RegionMapUI>();
        mapa.janela = rt;
        mapa.dono = dono;
        return mapa;
    }

    /// <summary>
    /// Redesenha o mundo.
    ///
    /// <b>O mapa não pergunta ao quadro para onde se pode ir.</b> Toda área é
    /// destino; o que o quadro ainda tem a dizer são a luta de selo e a jornada
    /// final, que se penduram no território a que pertencem.
    /// </summary>
    public void Desenhar(List<QuestData> missoes)
    {
        if (janela == null) janela = GetComponent<RectTransform>();

        // Sem isto, no frame em que o painel nasce o retângulo ainda mede zero e
        // o mapa inteiro colapsa num ponto — uma tela que só fica certa se o
        // jogador reabrir.
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(janela);

        PrepararJanela();

        foreach (var go in desenho)
            if (go != null) Destroy(go);
        desenho.Clear();

        DesenharPapel();
        DesenharRio();
        DesenharSerra();
        DesenharTrilhas();

        foreach (var lugar in AreaCatalog.Todas)
            DesenharTerritorio(lugar, MissaoEspecial(missoes, lugar));

        DesenharGuilda();
        DesenharBussola();

        // Um palmo acima da guilda: abrir centrado nela exatamente jogaria a
        // cidade contra a borda de baixo do papel, e o mundo que interessa
        // procurar fica todo ao norte dela.
        CentralizarEm(AreaCatalog.PosicaoDaGuilda + new Vector2(0f, 0.14f));
    }

    /// <summary>
    /// A janela que corta o papel.
    ///
    /// O <see cref="RectMask2D"/> é o que faz a folha poder ser maior que a
    /// tela: sem ele o papel transborda por cima da coluna de detalhes e do
    /// resto da interface.
    /// </summary>
    void PrepararJanela()
    {
        var fundo = GetComponent<Image>();
        if (fundo == null) fundo = gameObject.AddComponent<Image>();

        fundo.sprite = null;
        fundo.color = CorForaDoPapel;

        // Alvo de raycast de propósito: é este fundo que recebe o arrasto quando
        // o ponteiro não está sobre nenhum território.
        fundo.raycastTarget = true;

        if (GetComponent<RectMask2D>() == null) gameObject.AddComponent<RectMask2D>();

        if (papel == null)
        {
            var go = new GameObject("Papel", typeof(RectTransform));
            go.transform.SetParent(transform, false);

            papel = go.GetComponent<RectTransform>();
            papel.anchorMin = papel.anchorMax = new Vector2(0.5f, 0.5f);
            papel.pivot = new Vector2(0.5f, 0.5f);
            papel.sizeDelta = TamanhoDoPapel;
        }

        escala = EscalaInicial;
        papel.localScale = Vector3.one * escala;
    }

    void DesenharPapel()
    {
        var img = papel.GetComponent<Image>();
        if (img == null) img = papel.gameObject.AddComponent<Image>();

        MapArtCatalog arte = MapArtCatalog.Carregar();
        Sprite folha = arte != null ? arte.papel : null;

        img.sprite = folha;

        // Simple, não Sliced: os sprites do pacote não têm borda 9-slice
        // definida, e esticar um papel de 4K por Sliced desenha o quadro inteiro
        // do mesmo jeito, com o custo de pedir um recorte que não existe.
        img.type = Image.Type.Simple;
        img.color = folha != null ? Color.white : new Color(0.80f, 0.74f, 0.62f);

        // O papel não intercepta o ponteiro: o arrasto é da janela, e os cliques
        // são dos territórios.
        img.raycastTarget = false;
    }

    // ── o terreno ──────────────────────────────────────────────────────────

    /// <summary>
    /// O Rio Cinza, de oeste a leste no meio da folha.
    ///
    /// Não é enfeite: é ele que explica por que a Mata e a Cripta são as áreas
    /// de uma ida curta e todo o resto fica além. Um mapa em que a distância não
    /// tem causa visível volta a ser um diagrama com as distâncias escritas.
    /// </summary>
    void DesenharRio()
    {
        var curso = new List<Vector2>
        {
            new Vector2(-0.02f, 0.40f), new Vector2(0.16f, 0.44f), new Vector2(0.34f, 0.42f),
            new Vector2(0.52f, 0.46f), new Vector2(0.70f, 0.43f), new Vector2(0.88f, 0.47f),
            new Vector2(1.02f, 0.44f)
        };

        for (int i = 0; i < curso.Count - 1; i++)
            Traco($"Rio_{i}", curso[i], curso[i + 1], 22f, CorRio);

        Rotulo("Rio Cinza", new Vector2(0.33f, 0.385f), 22f,
               new Color(0.30f, 0.36f, 0.42f), 300f, true);
    }

    /// <summary>
    /// A Serra Quebrada, fechando o norte. Repete o símbolo de montanha do
    /// pacote ao longo de uma crista — é o que dá fundo ao mapa e diz que o
    /// mundo acaba ali.
    /// </summary>
    void DesenharSerra()
    {
        MapArtCatalog arte = MapArtCatalog.Carregar();
        Sprite pico = arte != null ? arte.IconeDe(BiomeType.Mountain) : null;
        if (pico == null) return;

        var crista = new List<Vector2>
        {
            new Vector2(0.06f, 0.93f), new Vector2(0.17f, 0.96f), new Vector2(0.29f, 0.92f),
            new Vector2(0.41f, 0.95f), new Vector2(0.53f, 0.91f), new Vector2(0.62f, 0.95f),
            new Vector2(0.82f, 0.94f), new Vector2(0.92f, 0.91f)
        };

        for (int i = 0; i < crista.Count; i++)
        {
            var go = Elemento($"Serra_{i}", crista[i], 150f);

            var img = go.AddComponent<Image>();
            img.sprite = pico;
            img.preserveAspect = true;
            img.color = new Color(0.30f, 0.26f, 0.21f, 0.55f);
            img.raycastTarget = false;

            go.transform.SetAsFirstSibling();
        }

        Rotulo("Serra Quebrada", new Vector2(0.44f, 0.875f), 22f,
               new Color(0.34f, 0.29f, 0.23f), 360f, true);
    }

    /// <summary>
    /// Os caminhos entre vizinhas — <b>quebrados, não retos</b>.
    ///
    /// Uma reta entre dois pontos é o desenho de uma aresta de grafo, e era
    /// exatamente assim que o mapa antigo se denunciava. O desvio vem de uma
    /// conta sobre o nome do par, então o mesmo caminho sai igual todas as
    /// vezes: um traçado que mudasse a cada abertura não seria um mapa.
    /// </summary>
    void DesenharTrilhas()
    {
        var feitas = new HashSet<string>();

        var lugares = new List<AreaType> { AreaType.None };
        lugares.AddRange(AreaCatalog.Todas);

        foreach (var origem in lugares)
        {
            foreach (var destino in AreaCatalog.Vizinhas(origem))
            {
                string chave = string.CompareOrdinal(origem.ToString(), destino.ToString()) < 0
                    ? $"{origem}|{destino}" : $"{destino}|{origem}";
                if (!feitas.Add(chave)) continue;

                DesenharCaminho(chave, PosicaoDe(origem), PosicaoDe(destino));
            }
        }
    }

    void DesenharCaminho(string chave, Vector2 de, Vector2 ate)
    {
        const int passos = 6;

        // Semente estável: o mesmo par de lugares dobra sempre para o mesmo lado.
        float semente = Mathf.Abs(chave.GetHashCode() % 1000) / 1000f;
        Vector2 perpendicular = Vector2.Perpendicular((ate - de).normalized);

        Vector2 anterior = de;
        for (int i = 1; i <= passos; i++)
        {
            float t = i / (float)passos;
            Vector2 reto = Vector2.Lerp(de, ate, t);

            // Zero nas pontas, máximo no meio: o caminho sai de um lugar e chega
            // ao outro sem desencontro, e faz barriga no meio do percurso.
            float curva = Mathf.Sin(t * Mathf.PI) * (0.03f + semente * 0.035f);
            float ondulacao = Mathf.Sin(t * Mathf.PI * 3f + semente * 6f) * 0.012f;

            Vector2 ponto = reto + perpendicular * (curva + ondulacao);

            Traco($"Trilha_{chave}_{i}", anterior, ponto, 5f, CorTrilha);
            anterior = ponto;
        }
    }

    // ── os lugares ─────────────────────────────────────────────────────────

    /// <summary>
    /// Uma área como <b>lugar com extensão</b>: uma mancha de terreno com o
    /// símbolo e o nome dentro, clicável inteira.
    ///
    /// Era um ícone de 60px com o nome solto embaixo. O que o jogador enxergava
    /// eram sete marcadores equivalentes — e a área, que é a unidade do jogo,
    /// não tinha tamanho nenhum na tela.
    /// </summary>
    void DesenharTerritorio(AreaType lugar, QuestData especial)
    {
        var ficha = AreaCatalog.De(lugar);
        if (ficha == null) return;

        BiomeType aspecto = ficha.aspecto;
        bool selada = RegionMap.EstaSelada(aspecto);

        float lado = TamanhoDoPapel.x * RaioDoTerritorio * 2f;
        var go = Elemento($"Area_{lugar}", ficha.posicao, lado);

        var mancha = go.AddComponent<Image>();
        mancha.sprite = Mancha(lugar.ToString().GetHashCode());

        // <b>A corrupção continua sendo cor.</b> É a única leitura do mapa que
        // não precisa de texto, e é por ela que o jogador aprende o mundo. Área
        // selada sai dourada: parou de apodrecer, e isso é estado, não grau.
        Color terra = selada ? CorSelada : Color.Lerp(CorLimpa, CorPodre, RegionMap.Fracao(aspecto));
        mancha.color = new Color(terra.r, terra.g, terra.b, 0.55f);
        mancha.raycastTarget = true;

        MapArtCatalog arte = MapArtCatalog.Carregar();
        Sprite icone = arte != null ? arte.IconeDe(aspecto) : null;
        if (icone != null)
        {
            var simbolo = new GameObject("Simbolo", typeof(RectTransform));
            simbolo.transform.SetParent(go.transform, false);

            float correcao = arte.EscalaDe(aspecto);
            var srt = simbolo.GetComponent<RectTransform>();
            srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.62f);
            srt.pivot = new Vector2(0.5f, 0.5f);
            srt.sizeDelta = Vector2.one * (lado * 0.40f * correcao);
            srt.anchoredPosition = Vector2.zero;

            var img = simbolo.AddComponent<Image>();
            img.sprite = icone;
            img.preserveAspect = true;
            img.color = new Color(0.14f, 0.11f, 0.09f, 0.9f);
            img.raycastTarget = false;
        }

        // Só o nome fica sobre a folha. Corrupção, dias e espólio moram na coluna
        // ao lado, que já fala do lugar apontado — vinte e uma linhas de número
        // sobre o papel eram o que mais fazia isto parecer um painel.
        var nome = NovoTexto(go.transform, ficha.nome, 30f, CorTinta);
        nome.fontStyle = FontStyles.SmallCaps;
        nome.alignment = TextAlignmentOptions.Center;

        var nrt = nome.rectTransform;
        nrt.anchorMin = new Vector2(0f, 0.08f);
        nrt.anchorMax = new Vector2(1f, 0.34f);
        nrt.offsetMin = Vector2.zero;
        nrt.offsetMax = Vector2.zero;

        var botao = go.AddComponent<Button>();
        botao.targetGraphic = mancha;

        AreaType escolhida = lugar;
        botao.onClick.AddListener(() =>
        {
            // Arrastar o mapa não é escolher destino.
            if (arrastou) return;

            selecionada = escolhida;
            if (dono != null) dono.EscolherArea(escolhida);
            MarcarSelecao();
        });

        if (especial != null) DesenharMissaoEspecial(go, especial, lado);
    }

    /// <summary>
    /// A luta de selo, ou a passagem final, cravada no território.
    ///
    /// Fica ao lado do símbolo e não no lugar dele: a área continua valendo como
    /// expedição comum — tem espólio, evento e escrito —, e trocar o botão
    /// obrigaria a selar para poder voltar lá.
    /// </summary>
    void DesenharMissaoEspecial(GameObject territorio, QuestData missao, float lado)
    {
        var go = new GameObject("Selo", typeof(RectTransform));
        go.transform.SetParent(territorio.transform, false);

        float tamanho = lado * 0.24f;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.82f, 0.80f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.one * tamanho;

        var img = go.AddComponent<Image>();
        img.sprite = Circulo();
        img.color = missao.isFinalBoss ? new Color(0.62f, 0.16f, 0.16f) : CorSelada;

        var marca = NovoTexto(go.transform, missao.isFinalBoss ? "⚔" : "\U0001F512",
                              tamanho * 0.5f, new Color(0.10f, 0.09f, 0.08f));
        marca.alignment = TextAlignmentOptions.Center;

        var mrt = marca.rectTransform;
        mrt.anchorMin = Vector2.zero;
        mrt.anchorMax = Vector2.one;
        mrt.offsetMin = Vector2.zero;
        mrt.offsetMax = Vector2.zero;

        var botao = go.AddComponent<Button>();
        botao.targetGraphic = img;

        QuestData escolhida = missao;
        botao.onClick.AddListener(() =>
        {
            if (arrastou) return;

            selecionada = AreaCatalog.Da(escolhida.biomeType);
            if (dono != null) dono.EscolherDestino(escolhida);
            MarcarSelecao();
        });
    }

    void DesenharGuilda()
    {
        MapArtCatalog arte = MapArtCatalog.Carregar();
        Sprite marca = arte != null ? arte.guilda : null;

        float lado = TamanhoDoPapel.x * 0.075f;
        var go = Elemento("Marcador_Guilda", AreaCatalog.PosicaoDaGuilda, lado);

        var img = go.AddComponent<Image>();
        img.sprite = marca != null ? marca : Circulo();
        img.preserveAspect = marca != null;
        img.color = marca != null ? new Color(0.42f, 0.22f, 0.11f) : new Color(0.85f, 0.72f, 0.42f);
        img.raycastTarget = false;

        var rotulo = NovoTexto(go.transform, "A GUILDA", 26f, new Color(0.45f, 0.24f, 0.12f));
        rotulo.alignment = TextAlignmentOptions.Top;

        var rt = rotulo.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -6f);
        rt.sizeDelta = new Vector2(320f, 34f);
    }

    /// <summary>
    /// A rosa dos ventos, presa ao canto da <b>janela</b> e não do papel: ela
    /// orienta quem está olhando, então não pode sair de vista quando o mapa se
    /// move.
    /// </summary>
    void DesenharBussola()
    {
        MapArtCatalog arte = MapArtCatalog.Carregar();
        if (arte == null || arte.bussola == null) return;

        var go = new GameObject("Bussola", typeof(RectTransform));
        go.transform.SetParent(transform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-18f, 18f);
        rt.sizeDelta = new Vector2(74f, 74f);

        var img = go.AddComponent<Image>();
        img.sprite = arte.bussola;
        img.preserveAspect = true;
        img.color = new Color(0.72f, 0.66f, 0.55f, 0.6f);
        img.raycastTarget = false;

        desenho.Add(go);
    }

    /// <summary>Contorno no destino escolhido — a única leitura de estado da tela.</summary>
    void MarcarSelecao()
    {
        foreach (var go in desenho)
        {
            if (go == null || !go.name.StartsWith("Area_")) continue;

            var contorno = go.GetComponent<Outline>();
            bool eEste = go.name == $"Area_{selecionada}";

            if (eEste && contorno == null)
            {
                contorno = go.AddComponent<Outline>();
                contorno.effectColor = new Color(0.95f, 0.85f, 0.55f, 0.9f);
                contorno.effectDistance = new Vector2(6f, -6f);
            }
            else if (!eEste && contorno != null)
            {
                Destroy(contorno);
            }
        }
    }

    // ── percorrer ──────────────────────────────────────────────────────────

    public void OnBeginDrag(PointerEventData evento)
    {
        arrastou = false;
    }

    public void OnDrag(PointerEventData evento)
    {
        if (papel == null) return;

        arrastou = true;
        papel.anchoredPosition += evento.delta;
        LimitarPapel();
    }

    /// <summary>
    /// A marca de arrasto dura um quadro a mais do que o arrasto.
    ///
    /// O clique chega <b>depois</b> do fim do arrasto na ordem de eventos do
    /// UGUI; zerar a marca aqui faria o território sob o ponteiro ser escolhido
    /// ao soltar o botão.
    /// </summary>
    public void OnEndDrag(PointerEventData evento)
    {
        StartCoroutine(LimparArrasto());
    }

    System.Collections.IEnumerator LimparArrasto()
    {
        yield return null;
        arrastou = false;
    }

    public void OnScroll(PointerEventData evento)
    {
        if (papel == null) return;

        float antes = escala;
        escala = Mathf.Clamp(escala + evento.scrollDelta.y * 0.1f, EscalaMin, EscalaMax);
        if (Mathf.Approximately(antes, escala)) return;

        papel.localScale = Vector3.one * escala;

        // O ponto que estava no centro da janela continua no centro: sem isto o
        // mapa foge para um canto a cada passo da roda.
        papel.anchoredPosition *= escala / antes;
        LimitarPapel();
    }

    /// <summary>
    /// Impede que o papel saia da janela e deixe a moldura vazia. Quando a folha
    /// couber inteira numa direção, ela fica centrada nela.
    /// </summary>
    void LimitarPapel()
    {
        Vector2 meioPapel = TamanhoDoPapel * escala * 0.5f;
        Vector2 meiaJanela = janela.rect.size * 0.5f;

        Vector2 folga = meioPapel - meiaJanela;
        Vector2 p = papel.anchoredPosition;

        p.x = folga.x <= 0f ? 0f : Mathf.Clamp(p.x, -folga.x, folga.x);
        p.y = folga.y <= 0f ? 0f : Mathf.Clamp(p.y, -folga.y, folga.y);

        papel.anchoredPosition = p;
    }

    /// <summary>Põe aquele ponto do papel no meio da janela — é onde o mapa abre.</summary>
    void CentralizarEm(Vector2 normalizada)
    {
        if (papel == null) return;

        Vector2 doCentro = (normalizada - new Vector2(0.5f, 0.5f)) * TamanhoDoPapel;
        papel.anchoredPosition = -doCentro * escala;
        LimitarPapel();
    }

    // ── peças ──────────────────────────────────────────────────────────────

    static Sprite circuloCache;
    static readonly Dictionary<int, Sprite> manchas = new Dictionary<int, Sprite>();

    /// <summary>
    /// Um círculo branco desenhado na hora. Sem sprite o <c>Image</c> sai
    /// quadrado — e não se usa <c>Resources.GetBuiltinResource("UI/Skin/Knob.psd")</c>:
    /// aquele caminho é recurso de <b>editor</b>, devolve null em runtime e
    /// cospe dois erros por marcador.
    /// </summary>
    static Sprite Circulo()
    {
        if (circuloCache != null) return circuloCache;

        const int lado = 64;
        var textura = new Texture2D(lado, lado, TextureFormat.RGBA32, false);
        textura.filterMode = FilterMode.Bilinear;

        float raio = lado * 0.5f;
        for (int y = 0; y < lado; y++)
            for (int x = 0; x < lado; x++)
            {
                float dx = x + 0.5f - raio, dy = y + 0.5f - raio;
                float alfa = Mathf.Clamp01(raio - Mathf.Sqrt(dx * dx + dy * dy));
                textura.SetPixel(x, y, new Color(1f, 1f, 1f, alfa));
            }

        textura.Apply();
        circuloCache = Sprite.Create(textura, new Rect(0f, 0f, lado, lado), new Vector2(0.5f, 0.5f));
        return circuloCache;
    }

    /// <summary>
    /// Uma mancha de terreno: círculo com o raio amassado por duas ondas.
    ///
    /// Placeholder estrutural, e é de propósito que não seja um círculo — sete
    /// bolhas idênticas leem como botões, e o que precisa aparecer é território.
    /// Quando houver mapa desenhado, cada área troca isto pelo recorte dela.
    /// </summary>
    static Sprite Mancha(int semente)
    {
        if (manchas.ContainsKey(semente)) return manchas[semente];

        const int lado = 256;
        var textura = new Texture2D(lado, lado, TextureFormat.RGBA32, false);
        textura.filterMode = FilterMode.Bilinear;

        float meio = lado * 0.5f;
        float faseA = Mathf.Abs(semente % 97) / 97f * Mathf.PI * 2f;
        float faseB = Mathf.Abs(semente % 53) / 53f * Mathf.PI * 2f;

        for (int y = 0; y < lado; y++)
            for (int x = 0; x < lado; x++)
            {
                float dx = x + 0.5f - meio, dy = y + 0.5f - meio;
                float distancia = Mathf.Sqrt(dx * dx + dy * dy);
                float angulo = Mathf.Atan2(dy, dx);

                float raio = meio * (0.80f
                    + 0.11f * Mathf.Sin(angulo * 3f + faseA)
                    + 0.06f * Mathf.Sin(angulo * 5f + faseB));

                // A borda decai em oito pixels: uma mancha de terreno não tem
                // contorno de botão.
                float alfa = Mathf.Clamp01((raio - distancia) / 8f);
                textura.SetPixel(x, y, new Color(1f, 1f, 1f, alfa));
            }

        textura.Apply();

        var sprite = Sprite.Create(textura, new Rect(0f, 0f, lado, lado), new Vector2(0.5f, 0.5f));
        manchas[semente] = sprite;
        return sprite;
    }

    static Vector2 PosicaoDe(AreaType lugar)
    {
        return lugar == AreaType.None ? AreaCatalog.PosicaoDaGuilda : AreaCatalog.Posicao(lugar);
    }

    /// <summary>
    /// A luta de selo ou a passagem final daquela área, se estiverem no quadro.
    /// A final vem antes: quando as duas existem no mesmo lugar, o que interessa
    /// é a que termina a partida.
    /// </summary>
    static QuestData MissaoEspecial(List<QuestData> missoes, AreaType lugar)
    {
        if (missoes == null) return null;

        BiomeType aspecto = AreaCatalog.Aspecto(lugar);
        QuestData selo = null;

        foreach (var m in missoes)
        {
            if (m == null || m.biomeType != aspecto) continue;
            if (m.isFinalBoss) return m;
            if (m.isRegionBoss && selo == null) selo = m;
        }

        return selo;
    }

    /// <summary>Um objeto do papel, posicionado por fração da folha.</summary>
    GameObject Elemento(string nome, Vector2 normalizada, float lado)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        go.transform.SetParent(papel, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(lado, lado);
        rt.anchoredPosition = EmPixels(normalizada);

        desenho.Add(go);
        return go;
    }

    /// <summary>Traço reto entre dois pontos do papel — a peça de rio e de trilha.</summary>
    void Traco(string nome, Vector2 de, Vector2 ate, float espessura, Color cor)
    {
        Vector2 origem = EmPixels(de);
        Vector2 destino = EmPixels(ate);
        Vector2 delta = destino - origem;

        var go = new GameObject(nome, typeof(RectTransform));
        go.transform.SetParent(papel, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = origem;
        rt.sizeDelta = new Vector2(delta.magnitude, espessura);
        rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

        var img = go.AddComponent<Image>();
        img.color = cor;
        img.raycastTarget = false;

        go.transform.SetAsFirstSibling();
        desenho.Add(go);
    }

    /// <summary>Texto solto sobre a folha — usado só para os acidentes do terreno.</summary>
    void Rotulo(string texto, Vector2 normalizada, float corpo, Color cor, float largura, bool italico)
    {
        var go = Elemento($"Rotulo_{texto}", normalizada, largura);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = texto;
        tmp.fontSize = corpo;
        tmp.color = cor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        if (italico) tmp.fontStyle = FontStyles.Italic;

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(largura, corpo * 2f);
    }

    Vector2 EmPixels(Vector2 normalizada)
    {
        return new Vector2((normalizada.x - 0.5f) * TamanhoDoPapel.x,
                           (normalizada.y - 0.5f) * TamanhoDoPapel.y);
    }

    TMP_Text NovoTexto(Transform pai, string texto, float corpo, Color cor)
    {
        var go = new GameObject("Rotulo", typeof(RectTransform));
        go.transform.SetParent(pai, false);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = texto;
        tmp.fontSize = corpo;
        tmp.color = cor;
        tmp.alignment = TextAlignmentOptions.Top;
        tmp.raycastTarget = false;
        tmp.richText = true;

        return tmp;
    }
}
