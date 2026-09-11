using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// O passo 1 da preparação: escolher o destino olhando o mundo.
///
/// <b>O plano navegável, decidido em 10/09 e construído em 11/09.</b> Sete áreas
/// que se tocam, a guilda numa borda, e uma trilha ligando cada par de vizinhas.
/// Toda área é clicável: o que se paga por ir longe são os dias de estrada, que
/// saem da distância pelo grafo — não de um contrato ter sorteado aquele lugar.
///
/// A corrupção pinta o marcador, como antes. É a única leitura do mapa que não
/// precisa de texto, e é o que faz o jogador aprender o mundo.
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

    /// <summary>Área selada: parou de apodrecer, e isso se lê como estado, não como grau.</summary>
    static readonly Color CorSelada = new Color(0.85f, 0.72f, 0.42f);
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
    AreaType selecionada = AreaType.None;

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

    /// <summary>
    /// Redesenha o plano: as sete áreas, as trilhas entre vizinhas e a guilda.
    ///
    /// <b>Desde 11/09 o mapa não pergunta mais ao quadro para onde se pode ir.</b>
    /// Toda área é destino, e o que o quadro ainda tem a dizer são a luta de selo
    /// e a jornada final — que se penduram na área a que pertencem, em vez de
    /// decidir quais lugares existem.
    /// </summary>
    public void Desenhar(List<QuestData> missoes)
    {
        if (area == null) area = GetComponent<RectTransform>();

        // Sem isto, no frame em que o painel nasce o retângulo ainda mede zero e
        // as sete áreas empilham no centro — um mapa que só fica certo se o
        // jogador reabrir a tela.
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(area);

        foreach (var m in marcadores)
            if (m != null) Destroy(m);
        marcadores.Clear();

        DesenharFundo();
        DesenharArestas();

        foreach (var lugar in AreaCatalog.Todas)
            DesenharArea(lugar, MissaoEspecial(missoes, lugar));

        DesenharGuilda();
    }

    /// <summary>
    /// A luta de selo ou a passagem final daquela área, se estiverem no quadro.
    ///
    /// A final vem antes do selo: quando as duas existem no mesmo lugar, o que
    /// interessa é a que termina a partida.
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

    /// <summary>Onde o lugar fica — a guilda é o nó sem ficha.</summary>
    static Vector2 PosicaoDe(AreaType lugar)
    {
        return lugar == AreaType.None ? AreaCatalog.PosicaoDaGuilda : AreaCatalog.Posicao(lugar);
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

    /// <summary>
    /// As trilhas entre vizinhas — o que faz do mapa um plano, e não um leque.
    ///
    /// Antes havia uma linha reta da guilda até cada região, e por isso a
    /// distância não significava nada: tudo ficava a um passo de casa. Agora o
    /// caminho até o Covil passa por quem está no meio, e é isso que faz a
    /// corrupção de uma área pesar mesmo quando não se vai a ela.
    ///
    /// Cada par sai uma vez só: a vizinhança é simétrica, e desenhar dos dois
    /// lados dobraria a linha sem que ninguém visse por quê.
    /// </summary>
    void DesenharArestas()
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

                DesenharTrilha(PosicaoDe(origem), PosicaoDe(destino));
            }
        }
    }

    void DesenharTrilha(Vector2 deNormalizado, Vector2 ateNormalizado)
    {
        Vector2 origem = ParaPixels(deNormalizado);
        Vector2 destino = ParaPixels(ateNormalizado);
        Vector2 delta = destino - origem;

        var go = new GameObject("Trilha", typeof(RectTransform));
        go.transform.SetParent(transform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = origem - Tamanho() * 0.5f;
        rt.sizeDelta = new Vector2(delta.magnitude, 3f);
        rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

        var img = go.AddComponent<Image>();
        img.color = sobrePapel ? CorTrilhaNoPapel : CorTrilha;
        img.raycastTarget = false;

        go.transform.SetAsFirstSibling();
        marcadores.Add(go);
    }

    void DesenharArea(AreaType lugar, QuestData especial)
    {
        var ficha = AreaCatalog.De(lugar);
        if (ficha == null) return;

        BiomeType aspecto = ficha.aspecto;
        float corrupcao = RegionMap.Corrupcao(aspecto);
        bool selada = RegionMap.EstaSelada(aspecto);

        MapArtCatalog arte = MapArtCatalog.Carregar();
        Sprite icone = arte != null ? arte.IconeDe(aspecto) : null;

        // Com ícone desenhado o marcador cresce um pouco: o símbolo precisa ser
        // reconhecível. A correção de tamanho vem do catálogo, medida nos pixels
        // do desenho — sem ela, o símbolo com mais margem transparente sai um
        // traço ao lado do que tem menos.
        float correcao = arte != null ? arte.EscalaDe(aspecto) : 1f;
        float raio = RaioDoMarcador();
        float tamanho = icone != null ? raio * 2.2f * correcao : raio * 2f;
        var go = NovoElemento($"Area_{lugar}", ficha.posicao, tamanho);

        var img = go.AddComponent<Image>();
        img.sprite = icone != null ? icone : Circulo();
        img.preserveAspect = icone != null;

        // <b>A corrupção continua sendo cor.</b> É ela que diz onde o mundo está
        // apodrecendo, e é a única leitura do mapa que não precisa de texto.
        // Área selada sai dourada: parou de apodrecer, e isso é estado, não grau.
        img.color = selada ? CorSelada : Color.Lerp(CorLimpa, CorPodre, RegionMap.Fracao(aspecto));
        img.raycastTarget = true;

        var rotulo = NovoTexto(go.transform, TextoDoMarcador(lugar, corrupcao, selada),
                               CorpoDoRotulo(), sobrePapel ? CorTextoNoPapel : Color.white);
        var rt = rotulo.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -2f);
        rt.sizeDelta = new Vector2(190f, 56f);

        var botao = go.AddComponent<Button>();
        botao.targetGraphic = img;

        AreaType escolhida = lugar;
        botao.onClick.AddListener(() =>
        {
            selecionada = escolhida;
            if (dono != null) dono.EscolherArea(escolhida);
            MarcarSelecao();
        });

        marcadores.Add(go);

        if (especial != null) DesenharMissaoEspecial(go, especial);
    }

    /// <summary>
    /// Nome, estado do lugar e o que a viagem custa em dias.
    ///
    /// O preço em dias vem antes de qualquer outra coisa que o mapa poderia
    /// dizer: é o único número que separa ir à Mata de ir ao Covil, e sem ele o
    /// plano volta a ser sete botões equivalentes.
    /// </summary>
    string TextoDoMarcador(AreaType lugar, float corrupcao, bool selada)
    {
        var ficha = AreaCatalog.De(lugar);
        int viagem = AreaCatalog.DiasDeIda(lugar) * 2;

        string estado = selada
            ? $"<color=#D9B85A>{AreaCatalog.Concordar(lugar, "selada", "selado")}</color>"
            : $"{Mathf.RoundToInt(corrupcao)}% corrompida";

        return $"{ficha.nome}\n<size=11>{estado}</size>\n<size=11>{viagem} dias de estrada</size>";
    }

    /// <summary>
    /// A luta de selo, ou a passagem final, presa ao marcador da área.
    ///
    /// Fica ao lado e não no lugar: a área mapeada continua valendo como
    /// expedição comum — tem espólio, evento e escrito —, e trocar o botão
    /// obrigaria a selar para poder voltar lá.
    /// </summary>
    void DesenharMissaoEspecial(GameObject marcadorDaArea, QuestData missao)
    {
        var go = new GameObject("Selo", typeof(RectTransform));
        go.transform.SetParent(marcadorDaArea.transform, false);

        float lado = RaioDoMarcador() * 0.9f;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(lado * 0.15f, lado * 0.15f);
        rt.sizeDelta = new Vector2(lado, lado);

        var img = go.AddComponent<Image>();
        img.sprite = Circulo();
        img.color = missao.isFinalBoss ? new Color(0.62f, 0.16f, 0.16f) : CorSelada;

        var marca = NovoTexto(go.transform, missao.isFinalBoss ? "\u2694" : "\U0001F512",
                              CorpoDoRotulo(), new Color(0.10f, 0.09f, 0.08f));
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
            selecionada = AreaCatalog.Da(escolhida.biomeType);
            if (dono != null) dono.EscolherDestino(escolhida);
            MarcarSelecao();
        });
    }

    /// <summary>Contorno no destino escolhido — a única leitura de estado que a tela precisa.</summary>
    void MarcarSelecao()
    {
        foreach (var m in marcadores)
        {
            if (m == null || !m.name.StartsWith("Area_")) continue;

            var contorno = m.GetComponent<Outline>();
            bool eEsta = m.name == $"Area_{selecionada}";

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
