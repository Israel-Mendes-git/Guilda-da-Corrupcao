using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// O passo 1 da preparação: escolher o destino olhando o mundo, em vez de ler
/// uma lista de ofertas.
///
/// Sete regiões em posições fixas, pintadas pela própria corrupção, ligadas à
/// guilda no centro. Onde há contrato, o marcador acende e pode ser clicado;
/// onde não há, o lugar continua no mapa, apagado — o jogador precisa ver o
/// mundo inteiro para entender o que está piorando fora do alcance dele.
///
/// Monta-se por código, em runtime. É deliberado: a cena é grande e montada por
/// ferramenta de Editor, e um painel novo dependendo de hierarquia exata seria
/// mais uma coisa para sair de sincronia. Aqui tudo o que a tela precisa nasce
/// de <see cref="RegionMap"/>, que é a única fonte de verdade.
///
/// Visual provisório: círculos e linhas. Os pontos vêm de RegionMap.Posicao, em
/// coordenadas de 0 a 1, então trocar isto por um mapa ilustrado é trocar o
/// fundo e os números daquele dicionário — nada aqui presume geometria.
/// </summary>
public class RegionMapUI : MonoBehaviour
{
    /// <summary>
    /// O raio cresce com o mapa.
    ///
    /// Os 30px fixos vinham de quando o mapa cabia em meia tela; num pergaminho
    /// de 1200px de largura, o mesmo círculo vira um carimbo com o nome maior
    /// que ele. Proporcional, o símbolo mantém o peso que tem na composição —
    /// e o teto impede que numa tela larga a Floresta encoste na Montanha.
    /// </summary>
    float RaioDoMarcador() => Mathf.Clamp(Tamanho().x * 0.042f, 26f, 62f);

    float RaioDaGuilda() => RaioDoMarcador() * 0.66f;

    /// <summary>Corpo do nome da região, também proporcional ao mapa.</summary>
    float CorpoDoRotulo() => Tamanho().x > 900f ? 17f : 13f;

    static Sprite circuloCache;

    /// <summary>
    /// Um círculo branco desenhado na hora. Sem sprite, o Image sai quadrado — e
    /// sete quadrados grandes leem como caixas de menu, não como lugares.
    ///
    /// Feito em código, e não com <c>Resources.GetBuiltinResource("UI/Skin/Knob.psd")</c>:
    /// aquele caminho é recurso de <b>editor</b> e não existe em runtime. Ele
    /// devolve null e cospe dois erros por marcador — foram 64 numa jornada, com
    /// a tela funcionando normalmente, porque um sprite nulo apenas volta a ser
    /// um quadrado.
    /// </summary>
    static Sprite Circulo()
    {
        if (circuloCache != null) return circuloCache;

        const int lado = 64;
        var textura = new Texture2D(lado, lado, TextureFormat.RGBA32, false);
        textura.filterMode = FilterMode.Bilinear;

        float raio = lado * 0.5f;
        for (int y = 0; y < lado; y++)
        {
            for (int x = 0; x < lado; x++)
            {
                float dx = x + 0.5f - raio;
                float dy = y + 0.5f - raio;
                float distancia = Mathf.Sqrt(dx * dx + dy * dy);

                // A borda decai em um pixel: sem isso o círculo sai serrilhado.
                float alfa = Mathf.Clamp01(raio - distancia);
                textura.SetPixel(x, y, new Color(1f, 1f, 1f, alfa));
            }
        }
        textura.Apply();

        circuloCache = Sprite.Create(textura, new Rect(0f, 0f, lado, lado), new Vector2(0.5f, 0.5f));
        return circuloCache;
    }

    static readonly Color CorLimpa = new Color(0.42f, 0.55f, 0.30f);
    static readonly Color CorPodre = new Color(0.55f, 0.15f, 0.15f);
    static readonly Color CorApagada = new Color(0.16f, 0.14f, 0.12f);
    static readonly Color CorTrilha = new Color(0.35f, 0.30f, 0.24f, 0.6f);
    static readonly Color CorGuilda = new Color(0.85f, 0.72f, 0.42f);

    /// <summary>
    /// As mesmas informações, escritas para serem lidas sobre papel claro.
    ///
    /// O mapa nasceu sobre fundo quase preto, e por isso o nome da região era
    /// branco e a guilda, dourada. Sobre o pergaminho do pacote de arte, os dois
    /// simplesmente somem — a cor do texto tem de acompanhar o fundo, ou a
    /// arte nova apaga a informação que ela veio ilustrar.
    /// </summary>
    static readonly Color CorTextoNoPapel = new Color(0.16f, 0.13f, 0.10f);
    static readonly Color CorApagadaNoPapel = new Color(0.42f, 0.38f, 0.32f);
    static readonly Color CorTrilhaNoPapel = new Color(0.28f, 0.22f, 0.16f, 0.75f);
    static readonly Color CorGuildaNoPapel = new Color(0.45f, 0.24f, 0.12f);

    /// <summary>Há pergaminho sob o mapa? Definido ao desenhar o fundo.</summary>
    bool sobrePapel;

    RectTransform area;
    readonly List<GameObject> marcadores = new List<GameObject>();
    QuestSelectionUI dono;
    BiomeType selecionada = BiomeType.Any;

    /// <summary>
    /// Cria o mapa dentro do painel dado, ou devolve o que já existe ali.
    /// </summary>
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
        // tela, não uma ilustração ao lado da lista. O terço restante é a coluna
        // de detalhes do contrato.
        //
        // A faixa de 92px que ficava livre embaixo era do botão "Próximo", que
        // agora mora no rodapé da coluna da direita.
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0.66f, 1f);
        rt.offsetMin = new Vector2(16f, 16f);
        rt.offsetMax = new Vector2(-12f, -16f);

        // Último irmão, não primeiro: em UGUI quem é desenhado por último fica na
        // frente. Como primeiro, o mapa nascia atrás do Scroll View da lista —
        // que tem fundo opaco — e a tela ficava simplesmente vazia, sem erro
        // nenhum no console. É a mesma armadilha de ordem de irmãos que já custou
        // uma investigação nos popups.
        rt.SetAsLastSibling();

        var mapa = go.AddComponent<RegionMapUI>();
        mapa.area = rt;
        mapa.dono = dono;
        return mapa;
    }

    /// <summary>Redesenha o mapa com as missões do quadro.</summary>
    public void Desenhar(List<QuestData> missoes)
    {
        if (area == null) area = GetComponent<RectTransform>();

        // Sem isto, no frame em que o painel nasce o retângulo ainda mede zero e
        // as sete regiões empilham no centro — um mapa que só fica certo se o
        // jogador reabrir a tela.
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(area);

        foreach (var m in marcadores)
            if (m != null) Destroy(m);
        marcadores.Clear();

        DesenharFundo();

        // Uma missão por região. Se o quadro trouxer duas no mesmo bioma, a mais
        // corrompida é a que aparece — é a decisão mais interessante das duas, e
        // o mapa não tem onde empilhar contratos.
        var porRegiao = new Dictionary<BiomeType, QuestData>();
        if (missoes != null)
        {
            foreach (var missao in missoes)
            {
                if (missao == null) continue;
                if (missao.biomeType == BiomeType.Any) continue;

                if (!porRegiao.ContainsKey(missao.biomeType) ||
                    missao.corruptionLevel > porRegiao[missao.biomeType].corruptionLevel)
                    porRegiao[missao.biomeType] = missao;
            }
        }

        foreach (var bioma in BiomeUtil.Playable)
        {
            QuestData missao = porRegiao.ContainsKey(bioma) ? porRegiao[bioma] : null;
            DesenharTrilha(bioma, missao != null);
            DesenharRegiao(bioma, missao);
        }

        DesenharGuilda();
    }

    void DesenharFundo()
    {
        var fundo = GetComponent<Image>();
        if (fundo == null) fundo = gameObject.AddComponent<Image>();

        // O papel do mapa, quando o pacote de arte estiver no projeto. Sem ele,
        // a cor chapada de antes — o mapa não pode depender de arte para
        // existir, só para ficar bonito.
        MapArtCatalog arte = MapArtCatalog.Carregar();
        Sprite papel = arte != null ? arte.papel : null;

        sobrePapel = papel != null;

        fundo.sprite = papel;

        // Simple, não Sliced: os sprites do pacote não têm borda 9-slice
        // definida, e esticar um papel de 4K por Sliced desenha o quadro inteiro
        // de qualquer jeito — com o custo de pedir ao Unity um recorte que não
        // existe.
        fundo.type = Image.Type.Simple;
        fundo.color = sobrePapel ? Color.white : new Color(0.09f, 0.08f, 0.07f, 0.85f);
        fundo.raycastTarget = false;

        if (arte != null && arte.borda != null) DesenharBorda(arte.borda);
        if (arte != null && arte.bussola != null) DesenharBussola(arte.bussola);
    }

    /// <summary>
    /// A rosa dos ventos no canto de baixo. Não informa nada — e é justamente
    /// por isso que entra: é o que faz a tela ler como mapa antigo em vez de
    /// diagrama, que era a queixa contra os círculos.
    /// </summary>
    void DesenharBussola(Sprite sprite)
    {
        var go = new GameObject("Bussola", typeof(RectTransform));
        go.transform.SetParent(transform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-18f, 18f);
        rt.sizeDelta = new Vector2(70f, 70f);

        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.color = new Color(1f, 1f, 1f, 0.55f);
        img.raycastTarget = false;

        marcadores.Add(go);
    }

    /// <summary>
    /// A moldura decorada, por cima do papel e por baixo de tudo o mais.
    ///
    /// Nasce logo depois do fundo justamente para ficar atrás dos marcadores:
    /// desenhada por último, a borda passaria por cima dos nomes das regiões.
    /// </summary>
    void DesenharBorda(Sprite sprite)
    {
        var go = new GameObject("Borda", typeof(RectTransform));
        go.transform.SetParent(transform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Simple;
        img.raycastTarget = false;

        marcadores.Add(go);
    }

    void DesenharGuilda()
    {
        MapArtCatalog arte = MapArtCatalog.Carregar();
        Sprite marca = arte != null ? arte.guilda : null;

        var go = NovoElemento("Marcador_Guilda", RegionMap.PosicaoDaGuilda,
                              marca != null ? RaioDaGuilda() * 3.4f : RaioDaGuilda() * 2f);

        var img = go.AddComponent<Image>();
        img.sprite = marca != null ? marca : Circulo();
        img.preserveAspect = marca != null;
        img.color = marca != null ? (sobrePapel ? CorGuildaNoPapel : Color.white) : CorGuilda;
        img.raycastTarget = false;

        var rotulo = NovoTexto(go.transform, "A GUILDA", CorpoDoRotulo(), sobrePapel ? CorGuildaNoPapel : CorGuilda);
        var rt = rotulo.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -4f);
        rt.sizeDelta = new Vector2(160f, 18f);

        marcadores.Add(go);
    }

    /// <summary>Linha da guilda até a região — o caminho que a expedição faria.</summary>
    void DesenharTrilha(BiomeType bioma, bool ativa)
    {
        Vector2 origem = ParaPixels(RegionMap.PosicaoDaGuilda);
        Vector2 destino = ParaPixels(RegionMap.Posicao(bioma));
        Vector2 delta = destino - origem;

        var go = new GameObject($"Trilha_{bioma}", typeof(RectTransform));
        go.transform.SetParent(transform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = origem - Tamanho() * 0.5f;
        rt.sizeDelta = new Vector2(delta.magnitude, ativa ? 3f : 1.5f);
        rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

        var img = go.AddComponent<Image>();
        Color corDaLinha = sobrePapel ? CorTrilhaNoPapel : CorTrilha;
        img.color = ativa ? corDaLinha : new Color(corDaLinha.r, corDaLinha.g, corDaLinha.b, 0.22f);
        img.raycastTarget = false;

        go.transform.SetAsFirstSibling();
        marcadores.Add(go);
    }

    void DesenharRegiao(BiomeType bioma, QuestData missao)
    {
        bool disponivel = missao != null;
        float corrupcao = RegionMap.Corrupcao(bioma);

        MapArtCatalog arte = MapArtCatalog.Carregar();
        Sprite icone = arte != null ? arte.IconeDe(bioma) : null;

        // Com ícone desenhado o marcador cresce um pouco: o símbolo precisa ser
        // reconhecível. Mas não muito — a 3,2× o nome da região ficava a quase
        // 100px do desenho, e as sete legendas soltas pelo papel não se ligavam
        // mais a nenhum símbolo.
        // A correção de tamanho vem do catálogo, medida nos pixels do desenho:
        // sem ela, o símbolo com mais margem transparente sai um traço ao lado
        // do que tem menos.
        float correcao = arte != null ? arte.EscalaDe(bioma) : 1f;
        float raio = RaioDoMarcador();
        float tamanho = icone != null ? raio * 2.2f * correcao : raio * 2f;
        var go = NovoElemento($"Regiao_{bioma}", RegionMap.Posicao(bioma), tamanho);

        var img = go.AddComponent<Image>();
        img.sprite = icone != null ? icone : Circulo();
        img.preserveAspect = icone != null;

        // <b>A corrupção continua sendo cor, mesmo com arte.</b> No círculo ela
        // era o próprio preenchimento; sobre o desenho, ela tinge. Perder essa
        // leitura seria trocar informação por enfeite — é ela que diz onde o
        // mundo está apodrecendo.
        img.color = disponivel ? Color.Lerp(CorLimpa, CorPodre, RegionMap.Fracao(bioma))
                               : (sobrePapel ? CorApagadaNoPapel : CorApagada);
        img.raycastTarget = disponivel;

        // Nome, corrupção e — quando há contrato — o que ele paga. É o bastante
        // para decidir sem abrir o painel de detalhes.
        string texto = $"{BiomeUtil.GetDisplayName(bioma)}\n<size=11>{Mathf.RoundToInt(corrupcao)}% corrompida</size>";
        if (disponivel)
            texto += $"\n<size=11>{missao.baseReward}+ ouro</size>";
        else
            texto += "\n<size=11>sem contrato</size>";

        Color corDoNome = disponivel
            ? (sobrePapel ? CorTextoNoPapel : Color.white)
            : (sobrePapel ? CorApagadaNoPapel : new Color(0.45f, 0.42f, 0.38f));

        var rotulo = NovoTexto(go.transform, texto, CorpoDoRotulo(), corDoNome);
        var rt = rotulo.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -2f);
        rt.sizeDelta = new Vector2(190f, 56f);

        if (disponivel)
        {
            var botao = go.AddComponent<Button>();
            botao.targetGraphic = img;

            QuestData escolhida = missao;
            BiomeType regiao = bioma;
            botao.onClick.AddListener(() =>
            {
                selecionada = regiao;
                if (dono != null) dono.EscolherDestino(escolhida);
                MarcarSelecao();
            });
        }

        marcadores.Add(go);
    }

    /// <summary>Contorno no destino escolhido — a única leitura de estado que a tela precisa.</summary>
    void MarcarSelecao()
    {
        foreach (var m in marcadores)
        {
            if (m == null || !m.name.StartsWith("Regiao_")) continue;

            var contorno = m.GetComponent<Outline>();
            bool eEsta = m.name == $"Regiao_{selecionada}";

            if (eEsta && contorno == null)
            {
                contorno = m.AddComponent<Outline>();
                contorno.effectColor = CorGuilda;
                contorno.effectDistance = new Vector2(3f, -3f);
            }
            else if (!eEsta && contorno != null)
            {
                Destroy(contorno);
            }
        }
    }

    // ── construção de elementos ────────────────────────────────────────────

    /// <summary>
    /// Área útil em pixels. Cai no tamanho do pai e depois num padrão porque um
    /// retângulo de medida zero põe o mapa inteiro num ponto só — e isso não dá
    /// erro nenhum, só uma tela errada.
    /// </summary>
    Vector2 Tamanho()
    {
        if (area != null && area.rect.width > 1f && area.rect.height > 1f)
            return area.rect.size;

        var pai = area != null ? area.parent as RectTransform : null;
        if (pai != null && pai.rect.width > 1f && pai.rect.height > 1f)
            return pai.rect.size;

        return new Vector2(900f, 620f);
    }

    /// <summary>De coordenada 0..1 do mapa para pixels dentro da área.</summary>
    /// <summary>
    /// Margem interna do papel, em fração da área.
    ///
    /// As sete posições do <see cref="RegionMap"/> vão de 0,14 a 0,86 e foram
    /// escolhidas quando o marcador era um círculo de 60px. Com o símbolo
    /// desenhado e o nome embaixo dele, quem mora perto da borda transborda: na
    /// primeira captura sobre pergaminho, Pântano e Deserto tinham os rótulos
    /// caídos fora do papel. Encolher o campo resolve sem mexer nas posições,
    /// que também são usadas por quem desenha as trilhas.
    /// </summary>
    const float MargemDoPapel = 0.13f;

    Vector2 ParaPixels(Vector2 normalizada)
    {
        Vector2 tamanho = Tamanho();

        float util = 1f - MargemDoPapel * 2f;
        float x = (MargemDoPapel + normalizada.x * util) * tamanho.x;
        float y = (MargemDoPapel + normalizada.y * util) * tamanho.y;

        return new Vector2(x, y);
    }

    GameObject NovoElemento(string nome, Vector2 posicaoNormalizada, float diametro)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        go.transform.SetParent(transform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(diametro, diametro);
        rt.anchoredPosition = ParaPixels(posicaoNormalizada) - Tamanho() * 0.5f;

        return go;
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
