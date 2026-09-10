using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sala de Mapas: o quanto da estrada a guilda enxerga antes de pisar nela.
///
/// São duas compras, e cada uma age num sistema que já existe. O <b>batedor</b>
/// abre um dia a mais da rota antes da partida — é assim que o
/// <see cref="JourneyMapUI"/> lê a carga: um nó está revelado enquanto
/// <c>layer &lt;= camada atual + 1 + batedores</c>, ou seja, sem batedor nenhum
/// só o dia seguinte é visível. O <b>desvio</b> recusa um encontro no meio do
/// caminho e troca o que há naquele nó, ao custo de um dia de mantimentos.
///
/// <b>Por que a sala deixou de ser três botões.</b> A versão anterior era um
/// contador: pagar 60 de ouro mudava <c>🔭 Batedores: 0/2</c> para <c>1/2</c> e
/// mais nada. O efeito existia — a jornada seguinte nascia com um dia a mais
/// aberto —, só que ele acontecia numa jornada que ainda não existia, e nenhuma
/// tela ligava a compra ao lugar onde ela agiria.
///
/// Agora a sala tem <b>uma estrada na mesa</b>: à esquerda as sete regiões, com
/// a corrupção de cada uma; no meio a região em foco, com o contrato que o
/// quadro oferece lá e as duas compras; à direita a estrada até lá, desenhada
/// marco a marco sobre papel — os dias abertos com o símbolo do terreno, os
/// selados com <c>?</c>. Contratar um batedor abre o marco seguinte no mesmo
/// clique. É o que o autor pediu por "quanto mais interativa, mais retorno
/// percebido".
///
/// <b>O que foi rejeitado.</b> Redesenhar aqui as sete regiões como mapa, do
/// jeito que a preparação faz: a decisão desta sala não é <i>para onde ir</i> —
/// é o quanto da estrada se quer enxergar, e o mapa-múndi roubaria a tela de
/// quem responde isso. E usar <see cref="QuestData.GetActualDuration"/> para
/// saber o tamanho da estrada: aquele método <b>sorteia a cada chamada</b>, e a
/// estrada mudaria de comprimento a cada atualização da tela. O desenho usa
/// <c>minDuration</c>, que é o trecho certo, e fecha com reticências até o
/// chefe.
///
/// Nada aqui depende de arte: sem o <see cref="MapArtCatalog"/>, o papel volta
/// a ser cor chapada e os marcos, círculos — a mesma regra que o mapa da
/// preparação já seguia.
/// </summary>
public class MapRoomManager : MonoBehaviour
{
    public static MapRoomManager Instance;

    [Header("Cabeçalho")]
    public TMP_Text goldText;
    public TMP_Text hintText;
    public TMP_Text feedbackText;
    public TMP_Text levelText;

    [Header("Buttons")]
    public Button closeButton;
    public Button upgradeButton;
    public Button buyScoutingButton;
    public Button buyDetourButton;

    [Header("A fila das regiões")]
    public Transform regionContainer;

    [Header("A mesa")]
    public GameObject tableRoot;
    public Image regionIcon;
    public TMP_Text regionNameText;
    public TMP_Text dossierText;
    public Image corruptionFill;
    public Image scoutFrame;
    public TMP_Text scoutGlyph;
    public TMP_Text scoutingText;
    public Image detourFrame;
    public TMP_Text detourGlyph;
    public TMP_Text detourText;

    [Header("A estrada")]
    public Image roadPaper;
    public TMP_Text roadTitleText;

    /// <summary>
    /// Onde a estrada é desenhada. O nome é herdado da lista de eventos
    /// revelados que existia aqui, e continua sendo o que o campo guarda —
    /// cada marco é um encontro que o batedor identifica ou não. Renomear
    /// quebraria o montador antigo, que ainda liga este campo.
    /// </summary>
    public Transform revealedEventsContainer;

    /// <summary>A faixa de bússolas embaixo do papel: uma por desvio comprado.</summary>
    public Transform detourContainer;

    public TMP_Text emptyStateText;

    [Header("Preços")]
    public int upgradeBaseCost = 400;
    public int scoutingCost = 60;
    public int detourCost = 90;

    [Header("Progresso")]
    public int mapRoomLevel = 1;

    // Comprado na guilda, gasto na jornada.
    private int scoutingCharges;
    private int detourCharges;

    public int MaxScouting => 1 + mapRoomLevel;   // dias de rota abertos de uma vez
    public int MaxDetours => mapRoomLevel;        // desvios por jornada

    public int ScoutingCharges => scoutingCharges;
    public int DetourCharges => detourCharges;

    /// <summary>Quantos marcos cabem numa linha antes de a estrada dobrar.</summary>
    const int MarcosPorLinha = 4;

    /// <summary>Lado do marco em pixels, antes da correção de tamanho do catálogo.</summary>
    const float LadoDoMarco = 96f;

    /// <summary>
    /// Teto do trecho desenhado. As missões geradas têm de 4 a 7 dias e a do
    /// Chefe Supremo, 10 — mas nada impede um asset feito à mão de pedir 30, e
    /// trinta marcos viram uma grade ilegível. O que passar do teto vira parte
    /// das reticências.
    /// </summary>
    const int MaxDiasDesenhados = 12;

    static readonly Color CorLimpa = new Color(0.42f, 0.55f, 0.30f);
    static readonly Color CorPodre = new Color(0.55f, 0.15f, 0.15f);

    // Sobre pergaminho claro, texto claro some — a mesma correção que o mapa da
    // preparação precisou fazer quando ganhou papel embaixo.
    static readonly Color TintaNoPapel = new Color(0.16f, 0.13f, 0.10f);
    static readonly Color TintaFracaNoPapel = new Color(0.46f, 0.41f, 0.34f);
    static readonly Color TintaEscura = new Color(0.88f, 0.85f, 0.78f);
    static readonly Color TintaFracaEscura = new Color(0.50f, 0.47f, 0.42f);

    /// <summary>Dourado do alcance da sala — o mesmo realce do destino escolhido no mapa.</summary>
    static readonly Color CorDoAlcance = new Color(0.85f, 0.72f, 0.42f);

    /// <summary>Há pergaminho sob a estrada? Decidido ao vestir o papel.</summary>
    bool sobrePapel;

    BiomeType naMesa = BiomeType.Any;

    /// <summary>Enquanto for falso, a sala reabre sozinha numa região com contrato.</summary>
    bool jogadorEscolheu;

    /// <summary>O marco de cada dia, para a compra estourar em cima do que ela abriu.</summary>
    readonly Dictionary<int, GameObject> marcoPorDia = new Dictionary<int, GameObject>();

    /// <summary>As bússolas desenhadas, na ordem — a última é a que acabou de ser comprada.</summary>
    readonly List<GameObject> bussolas = new List<GameObject>();

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
            closeButton.onClick.AddListener(() => UIManager.Instance?.CloseMapRoom());

        if (upgradeButton != null)
            upgradeButton.onClick.AddListener(UpgradeMapRoom);

        if (buyScoutingButton != null)
            buyScoutingButton.onClick.AddListener(BuyScouting);

        if (buyDetourButton != null)
            buyDetourButton.onClick.AddListener(BuyDetour);
    }

    public void RefreshMapRoom()
    {
        GarantirFoco();
        UpdateTexts();
        BuildRegions();
        AtualizarMesa();
        DesenharEstrada();
    }

    public int GetUpgradeCost() => upgradeBaseCost * mapRoomLevel;

    #region Cabeçalho e botões

    void UpdateTexts()
    {
        int gold = GuildManager.Instance != null ? GuildManager.Instance.gold : 0;

        if (goldText != null)
            goldText.text = $"💰 {gold}";

        if (levelText != null)
            levelText.text = $"Sala nível {mapRoomLevel} — alcance de {MaxScouting} dias "
                           + $"e {MaxDetours} desvio{(MaxDetours == 1 ? "" : "s")}";

        // A linha dos selos vem antes da explicação das compras: quando o fim
        // está aberto, é a única coisa desta sala que importa.
        if (hintText != null)
            hintText.text = DescreverSelos() + "\n"
                          + "Batedor: abre mais um dia da rota antes da partida.   "
                          + "Desvio: recusa um encontro no caminho, e custa um dia.   "
                          + "Vale para a próxima jornada, seja qual for o destino.";

        if (scoutingText != null)
            scoutingText.text = $"{Blocos(scoutingCharges, MaxScouting)}  <size=15>{scoutingCharges}/{MaxScouting}</size>";

        if (detourText != null)
            detourText.text = $"{Blocos(detourCharges, MaxDetours)}  <size=15>{detourCharges}/{MaxDetours}</size>";

        VestirBotao(buyScoutingButton, scoutingCharges < MaxScouting, scoutingCost,
                    "🔭 CONTRATAR BATEDOR", "TODOS CONTRATADOS");

        VestirBotao(buyDetourButton, detourCharges < MaxDetours, detourCost,
                    "🧭 TRAÇAR DESVIO", "SEM MAIS ROTAS");

        VestirBotao(upgradeButton, true, GetUpgradeCost(), "AMPLIAR A SALA", "");

        // A moldura do slot carrega a carga sem ler número, como a da bigorna.
        if (scoutFrame != null) scoutFrame.color = CorDaCarga(scoutingCharges, MaxScouting);
        if (detourFrame != null) detourFrame.color = CorDaCarga(detourCharges, MaxDetours);

        if (scoutGlyph != null) scoutGlyph.text = "🔭";
        if (detourGlyph != null) detourGlyph.text = "🧭";
    }

    /// <summary>Blocos cheios e vazios: a carga legível sem ler número.</summary>
    static string Blocos(int quanto, int maximo)
    {
        maximo = Mathf.Max(0, maximo);
        quanto = Mathf.Clamp(quanto, 0, maximo);
        return new string('■', quanto) + new string('□', maximo - quanto);
    }

    /// <summary>Do ferro cru ao cheio — a mesma escada de cor da forja.</summary>
    static Color CorDaCarga(int quanto, int maximo)
    {
        if (maximo <= 0) return new Color(0.42f, 0.39f, 0.36f);
        return Color.Lerp(new Color(0.42f, 0.39f, 0.36f), CorDoAlcance, quanto / (float)maximo);
    }

    void VestirBotao(Button button, bool temVaga, int custo, string rotulo, string rotuloCheio)
    {
        if (button == null) return;

        bool temOuro = GuildManager.Instance != null && GuildManager.Instance.gold >= custo;
        button.interactable = temVaga && temOuro;

        var texto = button.GetComponentInChildren<TMP_Text>();
        if (texto == null) return;

        texto.text = !temVaga
            ? rotuloCheio
            : temOuro ? $"{rotulo}   {custo}💰"
                      : $"<color=#B04040>{rotulo}   {custo}💰</color>";
    }

    void SetFeedback(string message)
    {
        // A sala usava UIManager.ShowMessage, e o popup nascia bem em cima do
        // cabeçalho: na captura da sala antiga, "Batedor contratado" cobria o
        // contador de desvios. A linha do rodapé é a mesma solução da forja.
        if (feedbackText != null)
            feedbackText.text = message;
    }

    #endregion

    #region A fila das regiões

    /// <summary>
    /// Uma região do quadro, quando há contrato lá.
    ///
    /// Duas ofertas no mesmo bioma resolvem-se pela mais corrompida, que é o
    /// mesmo critério do mapa da preparação — a sala não pode mostrar uma
    /// estrada e a preparação, outra.
    /// </summary>
    static QuestData ContratoDe(BiomeType bioma)
    {
        if (QuestManager.Instance == null || bioma == BiomeType.Any) return null;

        List<QuestData> quadro = QuestManager.Instance.QuadroAtual;
        if (quadro == null) return null;

        QuestData escolhida = null;
        foreach (var missao in quadro)
        {
            if (missao == null || missao.biomeType != bioma) continue;
            if (escolhida == null || missao.corruptionLevel > escolhida.corruptionLevel)
                escolhida = missao;
        }

        return escolhida;
    }

    /// <summary>
    /// Abre na primeira região com contrato: sem destino a estrada fica vazia, e
    /// uma sala que abre vazia não mostra o que vende.
    ///
    /// <b>Só enquanto o jogador não escolheu.</b> Reeleger a região a cada
    /// atualização arrancaria a escolha dele da mesa no meio de uma compra —
    /// quem olha uma região sem contrato está olhando de propósito.
    /// </summary>
    void GarantirFoco()
    {
        if (jogadorEscolheu && naMesa != BiomeType.Any) return;

        BiomeType comContrato = BiomeUtil.Playable.FirstOrDefault(b => ContratoDe(b) != null);
        naMesa = comContrato != BiomeType.Any ? comContrato : BiomeUtil.Playable[0];
    }

    void BuildRegions()
    {
        if (regionContainer == null) return;

        UIUtil.ClearChildrenNow(regionContainer);

        foreach (var bioma in BiomeUtil.Playable)
            BuildRegionRow(bioma);
    }

    /// <summary>
    /// A ficha da fila. Não vende nada: só põe aquela estrada na mesa. As duas
    /// compras moram no meio, onde se vê o que elas fazem.
    /// </summary>
    void BuildRegionRow(BiomeType bioma)
    {
        bool ativa = bioma == naMesa;
        QuestData contrato = ContratoDe(bioma);
        float fracao = RegionMap.Fracao(bioma);

        var row = new GameObject(bioma.ToString(), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        row.transform.SetParent(regionContainer, false);
        row.GetComponent<Image>().color = ativa
            ? new Color(0.30f, 0.24f, 0.16f)
            : new Color(0.17f, 0.15f, 0.14f);

        var element = row.AddComponent<LayoutElement>();
        element.minHeight = 84;
        element.preferredHeight = 84;

        // O símbolo do terreno é o mesmo que o mapa da preparação usa, tingido
        // pela corrupção: sem a tinta, os desenhos do pacote são pretos sobre
        // transparente e sumiriam na ficha escura.
        MapArtCatalog arte = MapArtCatalog.Carregar();
        Sprite icone = arte != null ? arte.IconeDe(bioma) : null;

        var iconGo = new GameObject("Icone", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconGo.transform.SetParent(row.transform, false);

        var img = iconGo.GetComponent<Image>();
        img.sprite = icone != null ? icone : UIUtil.Circulo();
        img.preserveAspect = true;
        img.raycastTarget = false;
        img.color = Color.Lerp(CorLimpa, CorPodre, fracao);

        // A correção do catálogo vai de 1 a 2,6: ela só aumenta a moldura, e o
        // desenho dentro dela acaba sempre com os mesmos ~45% de tinta. O que
        // passa da moldura é transparência, e por isso não invade o rótulo.
        float lado = 120f * (icone != null && arte != null ? arte.EscalaDe(bioma) : 1f);
        var iconRect = iconGo.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0, 0.5f);
        iconRect.anchorMax = new Vector2(0, 0.5f);
        iconRect.sizeDelta = new Vector2(lado, lado);
        iconRect.anchoredPosition = new Vector2(40, 0);

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(row.transform, false);

        var label = labelGo.AddComponent<TextMeshProUGUI>();
        label.fontSize = 18;
        label.alignment = TextAlignmentOptions.Left;
        label.raycastTarget = false;
        label.color = ativa ? new Color(0.98f, 0.92f, 0.72f) : new Color(0.88f, 0.86f, 0.82f);
        label.text = $"{BiomeUtil.GetDisplayName(bioma)}\n"
                   + $"<size=13>{Mathf.RoundToInt(RegionMap.Corrupcao(bioma))}% corrompida   ·   "
                   + (contrato != null
                        ? $"{contrato.minDuration}–{contrato.maxDuration} dias"
                        : "sem contrato")
                   + $"\n{EstadoDoMapa(bioma)}</size>";

        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(76, 4);
        labelRect.offsetMax = new Vector2(-10, -4);

        BiomeType capturada = bioma;
        row.GetComponent<Button>().onClick.AddListener(() => PorNaMesa(capturada));
    }

    /// <summary>
    /// Quantos selos, quais, e o que falta — a linha que dá à sala um assunto
    /// além das duas compras.
    ///
    /// Com os três na mesa, diz onde a passagem se abriu: é a região selada por
    /// último, e essa é a informação que o jogador precisa levar daqui para o
    /// quadro de missões.
    /// </summary>
    static string DescreverSelos()
    {
        int selos = RegionMap.Selos;

        if (RegionMap.OFimEstaAberto)
            return $"<color=#D9B85A>🔒 {selos} selos. A passagem se abriu em "
                 + $"{BiomeUtil.GetDisplayName(RegionMap.UltimoSelo)} — a jornada final está no quadro.</color>";

        int faltam = RegionMap.SelosParaOFim - selos;
        string quais = selos == 0
            ? ""
            : "  ·  " + string.Join(", ", RegionMap.RegioesSeladas().ConvertAll(BiomeUtil.GetDisplayName));

        return $"<color=#D9B85A>🔒 {selos} de {RegionMap.SelosParaOFim} selos</color>"
             + $"  ·  faltam {faltam} para abrir a passagem{quais}";
    }

    /// <summary>
    /// Em que pé está o mapa daquela região, em uma linha.
    ///
    /// É a informação que faz esta sala valer a visita: a corrupção diz onde
    /// está pior, e o mapa diz onde a guilda já pode fechar o assunto. Sem ela
    /// o jogador mapeia sem saber que mapeou.
    /// </summary>
    static string EstadoDoMapa(BiomeType bioma)
    {
        if (RegionMap.EstaSelada(bioma))
            return "<color=#D9B85A>🔒 selada</color>";

        if (RegionMap.EstaMapeada(bioma))
            return "<color=#D9B85A>🗺️ mapa completo — o chefe espera no quadro</color>";

        int porcento = Mathf.RoundToInt(RegionMap.FracaoMapeada(bioma) * 100f);
        return porcento == 0
            ? "<color=#8A8278>🗺️ nunca percorrida</color>"
            : $"<color=#8A8278>🗺️ {porcento}% mapeada</color>";
    }

    /// <summary>Traz a estrada daquela região para a mesa. É o único efeito do clique na fila.</summary>
    public void PorNaMesa(BiomeType bioma)
    {
        naMesa = bioma;
        jogadorEscolheu = true;
        SetFeedback($"A estrada até {BiomeUtil.GetDisplayName(bioma)} está na mesa.");

        UpdateTexts();
        BuildRegions();
        AtualizarMesa();
        DesenharEstrada();
    }

    #endregion

    #region A mesa

    void AtualizarMesa()
    {
        if (tableRoot != null) tableRoot.SetActive(true);

        MapArtCatalog arte = MapArtCatalog.Carregar();
        Sprite icone = arte != null ? arte.IconeDe(naMesa) : null;
        float fracao = RegionMap.Fracao(naMesa);

        if (regionIcon != null)
        {
            regionIcon.sprite = icone != null ? icone : UIUtil.Circulo();
            regionIcon.preserveAspect = true;
            regionIcon.color = Color.Lerp(CorLimpa, CorPodre, fracao);

            // Cada símbolo do pacote tem margem transparente própria; sem a
            // correção do catálogo, a serra sai gigante ao lado do cacto.
            float correcao = icone != null && arte != null ? arte.EscalaDe(naMesa) : 1f;
            regionIcon.transform.localScale = Vector3.one * correcao;
        }

        if (regionNameText != null)
            regionNameText.text = BiomeUtil.GetDisplayName(naMesa);

        if (corruptionFill != null)
        {
            corruptionFill.fillAmount = fracao;
            corruptionFill.color = Color.Lerp(CorLimpa, CorPodre, fracao);
        }

        if (dossierText == null) return;

        QuestData contrato = ContratoDe(naMesa);
        string mapa = $"<size=16>{EstadoDoMapa(naMesa)}</size>\n";

        dossierText.text = contrato != null
            ? $"{Mathf.RoundToInt(RegionMap.Corrupcao(naMesa))}% corrompida\n"
              + mapa
              + $"<size=17>{contrato.questName}</size>\n"
              + $"<size=16>{contrato.minDuration}–{contrato.maxDuration} dias  ·  "
              + $"{contrato.baseReward}+ ouro  ·  risco {Risco(contrato.risk)}</size>"
            : $"{Mathf.RoundToInt(RegionMap.Corrupcao(naMesa))}% corrompida\n"
              + mapa
              + "<size=16>Nenhum contrato para esta região neste ciclo.</size>";
    }

    static string Risco(QuestRisk risco)
    {
        switch (risco)
        {
            case QuestRisk.Low: return "baixo";
            case QuestRisk.Medium: return "médio";
            default: return "alto";
        }
    }

    #endregion

    #region A estrada desenhada

    /// <summary>Um ponto da estrada, já resolvido para desenho.</summary>
    struct Marco
    {
        public string rotulo;
        public Sprite sprite;
        public string glifo;
        public bool aberto;
        public bool noAlcance;
        public float escala;
        public int dia;   // 0 para guilda, reticências e chefe
    }

    void DesenharEstrada()
    {
        marcoPorDia.Clear();

        if (revealedEventsContainer == null) return;
        UIUtil.ClearChildrenNow(revealedEventsContainer);

        MapArtCatalog arte = MapArtCatalog.Carregar();
        VestirPapel(arte);
        DesenharDesvios(arte);

        QuestData contrato = ContratoDe(naMesa);
        bool temEstrada = contrato != null;

        if (emptyStateText != null)
        {
            emptyStateText.gameObject.SetActive(!temEstrada);
            emptyStateText.color = sobrePapel ? TintaNoPapel : TintaEscura;
            emptyStateText.text = $"Não há contrato para {BiomeUtil.GetDisplayName(naMesa)} neste ciclo — "
                                + "sem destino, não há estrada para traçar.\n"
                                + "O que você comprar aqui continua valendo para a próxima jornada.";
        }

        if (!temEstrada)
        {
            if (roadTitleText != null) roadTitleText.text = "";
            return;
        }

        List<Marco> marcos = MontarMarcos(contrato, arte);
        int dias = Mathf.Min(contrato.minDuration, MaxDiasDesenhados);
        int abertos = Mathf.Min(1 + scoutingCharges, dias);

        if (roadTitleText != null)
        {
            roadTitleText.text = $"A estrada até {BiomeUtil.GetDisplayName(naMesa)}: "
                               + $"<color=#D9B86B>{abertos} de {dias} dias abertos</color>";
        }

        Vector2 tamanho = TamanhoDaTrilha();

        // As linhas primeiro: quem nasce depois é desenhado por cima, e a
        // estrada tem de passar por baixo dos marcos.
        for (int i = 1; i < marcos.Count; i++)
        {
            // O trecho só é firme quando as duas pontas são conhecidas — o
            // caminho até o chefe passa por dias que ninguém viu ainda.
            Ligar(PosicaoDoMarco(i - 1, marcos.Count, tamanho),
                  PosicaoDoMarco(i, marcos.Count, tamanho),
                  marcos[i - 1].aberto && marcos[i].aberto);
        }

        for (int i = 0; i < marcos.Count; i++)
            DesenharMarco(marcos[i], PosicaoDoMarco(i, marcos.Count, tamanho));
    }

    /// <summary>
    /// A guilda, os dias e o chefe.
    ///
    /// O trecho certo é <c>minDuration</c>; o que vai além é sorteado na
    /// partida, e por isso vira reticências em vez de marcos falsos.
    /// </summary>
    List<Marco> MontarMarcos(QuestData contrato, MapArtCatalog arte)
    {
        var marcos = new List<Marco>();

        marcos.Add(new Marco
        {
            rotulo = "A GUILDA",
            sprite = arte != null ? arte.guilda : null,
            glifo = "",
            aberto = true,
            escala = 1f,
        });

        int dias = Mathf.Min(contrato.minDuration, MaxDiasDesenhados);
        Sprite terreno = arte != null ? arte.IconeDe(naMesa) : null;
        float correcao = terreno != null && arte != null ? arte.EscalaDe(naMesa) : 1f;

        for (int dia = 1; dia <= dias; dia++)
        {
            bool aberto = dia <= 1 + scoutingCharges;

            marcos.Add(new Marco
            {
                rotulo = aberto ? $"Dia {dia}\n<size=11>identificado</size>" : $"Dia {dia}",
                sprite = aberto ? terreno : null,
                glifo = aberto ? "" : "?",
                aberto = aberto,
                noAlcance = !aberto && dia <= 1 + MaxScouting,
                escala = aberto ? correcao : 1f,
                dia = dia,
            });
        }

        // O que não é desenhado: o trecho sorteado na partida (minDuration a
        // maxDuration) mais o que passou do teto de marcos.
        int incerto = Mathf.Max(0, contrato.maxDuration - contrato.minDuration)
                    + Mathf.Max(0, contrato.minDuration - dias);

        if (incerto > 0)
        {
            marcos.Add(new Marco
            {
                rotulo = $"<size=11>mais {incerto} dia{(incerto == 1 ? "" : "s")}\naté o chefe</size>",
                sprite = null,
                glifo = "·  ·  ·",
                aberto = false,
                escala = 1f,
            });
        }

        marcos.Add(new Marco
        {
            rotulo = "O CHEFE",
            sprite = arte != null ? arte.marcoDeChefe : null,
            glifo = arte != null && arte.marcoDeChefe != null ? "" : "■",
            aberto = true,
            escala = arte != null && arte.escalaDoChefe > 0f ? arte.escalaDoChefe : 1f,
        });

        return marcos;
    }

    /// <summary>
    /// O papel sob a estrada. Sem o catálogo é cor chapada — a arte melhora o
    /// mapa, não o faz existir.
    /// </summary>
    void VestirPapel(MapArtCatalog arte)
    {
        Sprite papel = arte != null ? arte.papel : null;
        sobrePapel = papel != null;

        if (roadPaper == null) return;

        roadPaper.sprite = papel;

        // Simple, não Sliced: os sprites do pacote não têm borda 9-slice, e
        // pedir um recorte que não existe só custa.
        roadPaper.type = Image.Type.Simple;
        roadPaper.color = sobrePapel ? Color.white : new Color(0.11f, 0.10f, 0.09f, 0.92f);
        roadPaper.raycastTarget = false;
    }

    Vector2 TamanhoDaTrilha()
    {
        var rt = revealedEventsContainer as RectTransform;

        // Um retângulo de medida zero põe a estrada inteira num ponto só, e isso
        // não dá erro nenhum — só uma tela errada. O painel acabou de ser ligado
        // quando este método roda, então o layout ainda pode não ter medido.
        if (rt != null)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

            if (rt.rect.width > 1f && rt.rect.height > 1f) return rt.rect.size;
        }

        return new Vector2(810f, 640f);
    }

    /// <summary>
    /// A estrada serpenteia: da esquerda para a direita, dobra e volta. Uma fila
    /// reta de doze marcos não caberia na largura, e uma coluna vira lista — que
    /// é justamente o que esta sala deixou de ser.
    /// </summary>
    static Vector2 PosicaoDoMarco(int indice, int total, Vector2 tamanho)
    {
        int colunas = Mathf.Max(1, Mathf.Min(MarcosPorLinha, total));
        int linhas = Mathf.CeilToInt(total / (float)colunas);

        int linha = indice / colunas;
        int coluna = indice % colunas;
        if (linha % 2 == 1) coluna = colunas - 1 - coluna;

        float passoX = tamanho.x / colunas;
        float passoY = tamanho.y / linhas;

        return new Vector2(
            -tamanho.x * 0.5f + passoX * (coluna + 0.5f),
             tamanho.y * 0.5f - passoY * (linha + 0.5f));
    }

    void DesenharMarco(Marco marco, Vector2 posicao)
    {
        // O marco tem tamanho fixo e o símbolo mora num filho. A correção de
        // tamanho do catálogo chega a 2,6× — aplicada ao próprio marco, ela
        // empurraria o rótulo (ancorado no pé dele) mais de cem pixels para
        // baixo, e a legenda se desgrudaria do desenho.
        GameObject go = NovoElemento(revealedEventsContainer, $"Marco_{marco.rotulo}", posicao, LadoDoMarco);

        GameObject simboloGo = NovoElemento(go.transform, "Simbolo", Vector2.zero, LadoDoMarco);
        simboloGo.transform.localScale = Vector3.one * (marco.escala > 0f ? marco.escala : 1f);

        var img = simboloGo.AddComponent<Image>();
        img.sprite = marco.sprite != null ? marco.sprite : UIUtil.Circulo();
        img.preserveAspect = marco.sprite != null;
        img.raycastTarget = false;
        img.color = marco.sprite != null
            ? (sobrePapel ? TintaNoPapel : TintaEscura)
            : (marco.aberto ? CorDoAlcance : new Color(0.55f, 0.50f, 0.42f, 0.35f));

        // O dourado marca até onde esta sala consegue enxergar se o jogador
        // pagar tudo — é o que torna a ampliação da sala visível antes da compra.
        if (marco.noAlcance)
        {
            var contorno = simboloGo.AddComponent<Outline>();
            contorno.effectColor = CorDoAlcance;
            contorno.effectDistance = new Vector2(3f, -3f);
        }

        if (!string.IsNullOrEmpty(marco.glifo))
        {
            var glifo = NovoTexto(go.transform, marco.glifo, 26f,
                                  sobrePapel ? TintaNoPapel : TintaEscura);
            glifo.alignment = TextAlignmentOptions.Center;

            var rt = glifo.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        Color corDoNome = marco.aberto
            ? (sobrePapel ? TintaNoPapel : TintaEscura)
            : (sobrePapel ? TintaFracaNoPapel : TintaFracaEscura);

        var rotulo = NovoTexto(go.transform, marco.rotulo, 14f, corDoNome);
        var rotuloRect = rotulo.rectTransform;
        rotuloRect.anchorMin = new Vector2(0.5f, 0f);
        rotuloRect.anchorMax = new Vector2(0.5f, 0f);
        rotuloRect.pivot = new Vector2(0.5f, 1f);
        rotuloRect.anchoredPosition = new Vector2(0f, -2f);
        rotuloRect.sizeDelta = new Vector2(180f, 48f);

        if (marco.dia > 0) marcoPorDia[marco.dia] = go;
    }

    /// <summary>O trecho de estrada entre dois marcos.</summary>
    void Ligar(Vector2 origem, Vector2 destino, bool firme)
    {
        Vector2 delta = destino - origem;

        var go = new GameObject("Trecho", typeof(RectTransform));
        go.transform.SetParent(revealedEventsContainer, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = origem;
        rt.sizeDelta = new Vector2(delta.magnitude, firme ? 4f : 2f);
        rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

        var img = go.AddComponent<Image>();
        img.raycastTarget = false;

        Color cor = sobrePapel ? new Color(0.28f, 0.22f, 0.16f) : new Color(0.42f, 0.36f, 0.28f);
        img.color = firme ? cor : new Color(cor.r, cor.g, cor.b, 0.35f);
    }

    /// <summary>
    /// As bússolas na margem do papel: uma por desvio comprado, e um lugar vazio
    /// por desvio que a sala ainda comporta.
    ///
    /// Ficam fora da estrada de propósito. O desvio serve em <b>qualquer</b>
    /// ponto do caminho — desenhá-lo numa bifurcação específica diria ao jogador
    /// que ele já escolheu onde vai recuar, e isso seria mentira.
    /// </summary>
    void DesenharDesvios(MapArtCatalog arte)
    {
        bussolas.Clear();

        if (detourContainer == null) return;
        UIUtil.ClearChildrenNow(detourContainer);

        var legenda = NovoTexto(detourContainer, detourCharges > 0
            ? $"🧭 {detourCharges} desvio{(detourCharges == 1 ? "" : "s")} na bagagem — "
              + "cada um recusa um encontro do caminho, e custa um dia."
            : "🧭 Sem desvios: o que aparecer na estrada terá de ser enfrentado.",
            15f, sobrePapel ? TintaNoPapel : TintaFracaEscura);

        legenda.alignment = TextAlignmentOptions.Center;

        var legendaRect = legenda.rectTransform;
        legendaRect.anchorMin = new Vector2(0f, 1f);
        legendaRect.anchorMax = new Vector2(1f, 1f);
        legendaRect.offsetMin = new Vector2(10f, -28f);
        legendaRect.offsetMax = new Vector2(-10f, 0f);

        const float Lado = 40f;
        const float Passo = 54f;

        for (int i = 0; i < MaxDetours; i++)
        {
            bool comprada = i < detourCharges;
            float x = (i - (MaxDetours - 1) / 2f) * Passo;

            var go = new GameObject($"Bussola_{i}", typeof(RectTransform));
            go.transform.SetParent(detourContainer, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(Lado, Lado);
            rt.anchoredPosition = new Vector2(x, 8f);

            var img = go.AddComponent<Image>();
            img.sprite = arte != null && arte.bussola != null ? arte.bussola : UIUtil.Circulo();
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.color = comprada
                ? (sobrePapel ? TintaNoPapel : CorDoAlcance)
                : new Color(0.55f, 0.50f, 0.42f, 0.28f);

            bussolas.Add(go);
        }
    }

    GameObject NovoElemento(Transform pai, string nome, Vector2 posicao, float lado)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        go.transform.SetParent(pai, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(lado, lado);
        rt.anchoredPosition = posicao;

        return go;
    }

    static TMP_Text NovoTexto(Transform pai, string texto, float corpo, Color cor)
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

    #endregion

    #region Comprar

    void UpgradeMapRoom()
    {
        int cost = GetUpgradeCost();

        if (GuildManager.Instance == null || !GuildManager.Instance.SpendGold(cost))
        {
            SetFeedback("Ouro insuficiente.");
            return;
        }

        mapRoomLevel++;
        SetFeedback($"A sala vai ao nível {mapRoomLevel}: os batedores chegam a {MaxScouting} dias "
                  + $"e cabem {MaxDetours} desvios.");

        RefreshMapRoom();

        // O ganho é de capacidade, não de carga: ele aparece nos lugares vazios
        // que os dois slots acabaram de abrir.
        Anunciar(scoutFrame != null ? scoutFrame.gameObject : null, "+1 🔭", CorDoAlcance);
        Anunciar(detourFrame != null ? detourFrame.gameObject : null, "+1 🧭", CorDoAlcance);
    }

    void BuyScouting()
    {
        if (scoutingCharges >= MaxScouting)
        {
            SetFeedback("Todos os batedores que esta sala comporta já estão contratados.");
            return;
        }

        if (GuildManager.Instance == null || !GuildManager.Instance.SpendGold(scoutingCost))
        {
            SetFeedback("Ouro insuficiente.");
            return;
        }

        scoutingCharges++;

        // O dia que este batedor abre é o mesmo que o mapa da jornada vai mostrar
        // revelado: sem carga nenhuma só o dia seguinte é visível.
        int diaAberto = 1 + scoutingCharges;
        SetFeedback($"Batedor contratado: a rota nasce aberta até o dia {diaAberto}.");

        RefreshMapRoom();

        if (marcoPorDia.TryGetValue(diaAberto, out GameObject marco))
        {
            Anunciar(marco, "🔭 aberto", new Color(0.50f, 0.78f, 0.41f));
            Sacudir(marco);
        }
        else
        {
            // A estrada desta região é mais curta que a carga comprada — o
            // batedor não some, só não tem onde aparecer neste destino.
            Anunciar(scoutFrame != null ? scoutFrame.gameObject : null, "+1 🔭",
                     new Color(0.50f, 0.78f, 0.41f));
        }
    }

    void BuyDetour()
    {
        if (detourCharges >= MaxDetours)
        {
            SetFeedback("Não há mais rotas alternativas: amplie a sala para traçar outra.");
            return;
        }

        if (GuildManager.Instance == null || !GuildManager.Instance.SpendGold(detourCost))
        {
            SetFeedback("Ouro insuficiente.");
            return;
        }

        detourCharges++;
        SetFeedback($"Rota alternativa traçada: {detourCharges} encontro"
                  + $"{(detourCharges == 1 ? " pode" : "s podem")} ser recusado"
                  + $"{(detourCharges == 1 ? "" : "s")} no caminho.");

        RefreshMapRoom();

        GameObject nova = bussolas.Count >= detourCharges ? bussolas[detourCharges - 1] : null;
        if (nova != null)
        {
            Anunciar(nova, "🧭", new Color(0.50f, 0.78f, 0.41f));
            Sacudir(nova);
        }
    }

    void Anunciar(GameObject alvo, string texto, Color cor)
    {
        if (alvo != null && alvo.activeInHierarchy)
            CombatFeedback.Get().ShowText(alvo, texto, cor);
    }

    void Sacudir(GameObject alvo)
    {
        if (alvo != null && alvo.activeInHierarchy)
            CombatFeedback.Get().Shake(alvo);
    }

    #endregion

    #region Consumo pela jornada

    /// <summary>Quantos dias de rota já nascem abertos.</summary>
    public int ConsumeScoutingForJourney()
    {
        int charges = scoutingCharges;
        scoutingCharges = 0;
        return charges;
    }

    /// <summary>Quantos desvios o grupo leva para esta jornada.</summary>
    public int ConsumeDetoursForJourney()
    {
        int charges = detourCharges;
        detourCharges = 0;
        return charges;
    }

    #endregion
}
