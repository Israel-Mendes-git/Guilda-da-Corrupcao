#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Monta na cena o que falta para a jornada e o combate rodarem, e liga as
/// referências do Inspector automaticamente.
///
/// Tools → Guild of Legends → Montar Cena
///
/// É idempotente: rodar de novo reaproveita o que já existe.
/// </summary>
public static class GuildSceneSetup
{
    const string ChoiceButtonPath = "Assets/Prefabs/UI/ChoiceButtonPrefab.prefab";
    const string MapNodePath = "Assets/Prefabs/UI/MapNodePrefab.prefab";
    const string EnemyCardPath = "Assets/Prefabs/UI/EnemyCardPrefab.prefab";
    const string PartyStatusPath = "Assets/Prefabs/UI/PartyStatusPrefab.prefab";
    const string CardPath = "Assets/Prefabs/UI/CardPrefab.prefab";
    const string CaveBackgroundPath = "Assets/Pixel Fantasy Caves/background3.png";

    // Opaco. Os 2% que faltavam deixavam o rodapé da guilda — nomes dos heróis,
    // ouro, "Baralhos" — atravessar cada sala e brigar com o texto de cima. É o
    // mesmo defeito que os painéis de menu já tiveram, e só a captura mostra.
    internal static readonly Color PanelColor = new Color(0.08f, 0.07f, 0.09f, 1f);
    internal static readonly Color BoxColor = new Color(0.13f, 0.12f, 0.14f, 1f);
    internal static readonly Color ButtonColor = new Color(0.20f, 0.17f, 0.16f);
    internal static readonly Color ButtonLabelColor = new Color(0.94f, 0.88f, 0.72f);
    internal static readonly Color TextColor = new Color(0.92f, 0.90f, 0.85f);
    internal static readonly Color TrackColor = new Color(0.10f, 0.09f, 0.11f);
    internal static readonly Color HandleColor = new Color(0.30f, 0.27f, 0.24f);
    static readonly Color ToggleBoxColor = new Color(0.24f, 0.21f, 0.19f);
    internal static readonly Color SubtleTextColor = new Color(0.66f, 0.63f, 0.58f);

    // Kit Bloodlines UI: molduras de pedra, botões e marcas de seleção prontos em
    // 9-slice. Substituem os retângulos chapados que o setup vinha desenhando.
    const string KitRoot = "Assets/Alebardium/Bloodlines UI/";
    const string PanelSpritePath = KitRoot + "Textures/Frame/Frame_background.png";
    const string OutlineSpritePath = KitRoot + "Textures/Frame/Frame_outline.png";
    const string ButtonDefaultPath = KitRoot + "Textures/Button/Button1/Status_Grey_Default.png";
    const string ButtonHoverPath = KitRoot + "Textures/Button/Button1/Status_Grey_Hover.png";
    const string ButtonPressedPath = KitRoot + "Textures/Button/Button1/Status_Pressed.png";
    const string ButtonDisabledPath = KitRoot + "Textures/Button/Button1/Status_Disable.png";
    const string CheckmarkSpritePath = KitRoot + "Textures/Toggle/Icon Checkmark 1 (Rect).png";
    const string TitleFontPath = KitRoot + "Fonts/MedievalSharp SDF.asset";

    /// <summary>Tingimento das molduras: a textura já é escura, o branco a mostra como é.</summary>
    static readonly Color PanelTint = new Color(1f, 1f, 1f, 0.98f);
    static readonly Color OutlineTint = new Color(0.46f, 0.39f, 0.29f, 0.85f);

    /// <summary>Dourado do realce: a mesma moldura das caixas, acesa. Precisa
    /// destoar do OutlineTint — se ficasse parecida, "acender" não se notaria.</summary>
    static readonly Color RealceTint = new Color(0.93f, 0.76f, 0.33f, 1f);
    static readonly Color CheckmarkTint = new Color(0.85f, 0.24f, 0.20f);

    [MenuItem("Tools/Guild of Legends/Montar Cena")]
    public static void Setup()
    {
        Setup(true);
    }

    /// <summary>
    /// Monta a cena. Diálogos modais travam a thread do Editor, o que impede
    /// qualquer chamada automatizada de retornar — daí o modo silencioso.
    /// </summary>
    /// <param name="interactive">false para rodar sem nenhum diálogo.</param>
    public static void Setup(bool interactive)
    {
        // Esta montagem é da **cena do jogo**, e o Editor pode estar em qualquer
        // outra — o teste de Play Mode agora passa pelo título, e ao sair deixa a
        // MainMenu aberta. Sem esta guarda o comando construiu o jogo inteiro
        // dentro da cena de menu, em silêncio: há Canvas nas duas, então nada
        // falhava; a cena de título só engordou de 250 KB para 800 KB com um
        // painel de combate escondido dentro dela.
        string cenaAtual = EditorSceneManager.GetActiveScene().path;
        if (cenaAtual != MenuSceneSetup.CaminhoDoJogo)
        {
            string erro = $"Montar Cena precisa da cena do jogo aberta.\n\n"
                        + $"Aberta agora: {(string.IsNullOrEmpty(cenaAtual) ? "(cena sem arquivo)" : cenaAtual)}\n"
                        + $"Esperada: {MenuSceneSetup.CaminhoDoJogo}";

            if (interactive) EditorUtility.DisplayDialog("Montar Cena", erro, "Ok");
            else Debug.LogError("Montar Cena: " + erro.Replace("\n\n", " — ").Replace("\n", " | "));
            return;
        }

        Canvas canvas = Object.FindObjectsOfType<Canvas>()
            .FirstOrDefault(c => c.transform.parent == null || c.GetComponent<CanvasScaler>() != null);

        if (canvas == null)
        {
            const string semCanvas = "Nenhum Canvas encontrado na cena.";
            if (interactive) EditorUtility.DisplayDialog("Montar Cena", semCanvas, "Ok");
            else Debug.LogError("Montar Cena: " + semCanvas);
            return;
        }

        var choiceBtn = AssetDatabase.LoadAssetAtPath<GameObject>(ChoiceButtonPath);
        var mapNode = AssetDatabase.LoadAssetAtPath<GameObject>(MapNodePath);
        var enemyCard = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyCardPath);
        var partyStatus = AssetDatabase.LoadAssetAtPath<GameObject>(PartyStatusPath);
        var cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPath);

        GameObject journeyPanel = BuildJourney(canvas, choiceBtn, mapNode, partyStatus, cardPrefab);
        GameObject combatPanel = BuildCombat(canvas, cardPrefab);
        BuildJourneyResult(canvas);
        BuildRunEnd(canvas);
        GameObject mapRoomPanel = MapRoom.Montar(canvas);
        GameObject marketPanel = MarketRoom.Montar(canvas);
        GameObject cemeteryPanel = CemeteryRoom.Montar(canvas);
        GameObject forgePanel = BuildForge(canvas, cardPrefab);

        // As salas refeitas no molde da Forja moram em arquivo próprio: este
        // setup já tem 3.000 linhas, e uma sala por arquivo é o que permitiu
        // trabalhá-las em paralelo sem que uma pisasse na outra.
        GameObject libraryPanel = LibraryRoom.Montar(canvas, cardPrefab);
        GameObject tavernPanel = TavernRoom.Montar(canvas);
        DeckScreen.Montar(canvas);
        BuildProvisions();
        BuildFormation();

        // A pausa e as duas telas que ela divide com o título. Ficam num arquivo
        // próprio porque a mesma montagem serve à cena de menu.
        MenuSceneSetup.MontarNaCenaDoJogo(canvas);

        StylePreparation();
        StyleCardPrefab();
        ApplyKit(canvas);

        // Depois do kit: o realce da guilda e os rótulos herdados não são
        // decoração genérica, e uma varredura de skin passando por cima deles
        // devolveria o dourado ao marrom das molduras comuns.
        BuildGuildGuide(canvas);
        RotularHerdados(canvas);

        LiftPopups(canvas);
        AssentarAviso(canvas);

        // Registra os painéis novos no UIManager.
        UIManager ui = Object.FindObjectOfType<UIManager>();
        if (ui != null)
        {
            Undo.RecordObject(ui, "Montar Cena");
            if (ui.journeyPanel == null) ui.journeyPanel = journeyPanel;
            if (ui.mapRoomPanel == null) ui.mapRoomPanel = mapRoomPanel;
            if (ui.marketPanel == null) ui.marketPanel = marketPanel;
            if (ui.cemeteryPanel == null) ui.cemeteryPanel = cemeteryPanel;
            if (ui.forgePanel == null) ui.forgePanel = forgePanel;

            // A biblioteca é o único caso em que o painel novo substitui um que
            // já existia na cena: o `if (== null)` dos outros deixaria o
            // UIManager apontando para a tela velha para sempre. O painel antigo
            // sai de cena em vez de ser destruído — apagar objeto de cena por
            // ferramenta é irreversível, e ele ainda guarda a hierarquia que o
            // desenho novo substituiu.
            ReapontarSala(ref ui.libraryPanel, libraryPanel, "biblioteca");
            ReapontarSala(ref ui.tavernPanel, tavernPanel, "taverna");

            // O rodapé da guilda não faz sentido durante o combate e ficava
            // sobreposto às cartas.
            var downBar = canvas.transform.Find("Background/Panel_DownBar");
            if (downBar != null && (ui.hideDuringCombat == null || ui.hideDuringCombat.Length == 0))
                ui.hideDuringCombat = new[] { downBar.gameObject };

            LigarAtalhoDeBaralhos(canvas, ui);

            EditorUtility.SetDirty(ui);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Cena montada. Salve a cena (Ctrl+S) para preservar.");

        if (interactive)
            EditorUtility.DisplayDialog("Montar Cena",
                "Jornada, Combate e Sala de Mapas montados e ligados.\n\nSalve a cena com Ctrl+S.", "Ok");
    }

    #region Jornada

    static GameObject BuildJourney(Canvas canvas, GameObject choiceBtn, GameObject mapNode,
                                   GameObject partyStatus, GameObject cardPrefab)
    {
        GameObject panel = FindOrCreatePanel(canvas, "Panel_Journey");

        // ── Composição do painel ───────────────────────────────────────────
        // <b>O mapa é a tela.</b> Antes eram sete faixas horizontais disputando
        // 1080px — mapa, título, escolhas, narrativa, cards, mão, recursos e
        // botões —, e a rota, que é a decisão da jornada, ficava com 180px no
        // alto. Agora o mapa ocupa tudo e o resto flutua sobre ele:
        //
        //   HUD, medido do teto        missão · bioma · dia | 3 botões
        //                              provisões e contadores do baralho
        //   MAPA                       340–984, borda a borda
        //     └ caixa do evento        flutua à esquerda, só quando há evento
        //     └ ficha do grupo         anda de ponto em ponto
        //   RODAPÉ                     vida do grupo (esq.) | mão (dir.)

        // ── HUD ────────────────────────────────────────────────────────────
        var questName = EnsureText(panel.transform, "Txt_QuestName", "Missão", 26,
            new Vector2(0, 1), new Vector2(0.36f, 1), new Vector2(20, -48), new Vector2(0, -12));
        var biome = EnsureText(panel.transform, "Txt_Biome", "", 22,
            new Vector2(0.36f, 1), new Vector2(0.52f, 1), new Vector2(0, -48), new Vector2(0, -12));
        var day = EnsureText(panel.transform, "Txt_Day", "Dia 0 / 0", 22,
            new Vector2(0.52f, 1), new Vector2(0.66f, 1), new Vector2(0, -48), new Vector2(0, -12));

        // Mapa: área livre, pois o JourneyMapUI posiciona nós e arestas por
        // coordenada — um LayoutGroup sobrescreveria tudo.
        var mapRow = EnsureFreeArea(panel.transform, "MapNodes", new Vector2(0, 0), new Vector2(1, 1),
            new Vector2(20, 340), new Vector2(-20, -96));

        // O terreno desliza sob esta janela, e o que sai dela precisa sumir:
        // sem recorte, um ponto rolado para fora aparece por cima do HUD e dos
        // cards do grupo, que são objetos de outra faixa da tela.
        if (mapRow.GetComponent<RectMask2D>() == null) Undo.AddComponent<RectMask2D>(mapRow);

        // Fundo do bioma: atrás de tudo e discreto, porque a tela é de leitura —
        // a arte serve para o jogador sentir onde está, não para competir com o
        // texto do evento. Fica como primeiro irmão para não cobrir nada.
        var biomeBg = EnsureFreeArea(panel.transform, "BiomeBackground",
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var biomeImg = biomeBg.GetComponent<Image>();
        if (biomeImg == null) biomeImg = Undo.AddComponent<Image>(biomeBg);
        biomeImg.color = new Color(1f, 1f, 1f, 0.22f);
        biomeImg.preserveAspect = false;
        biomeImg.raycastTarget = false;
        biomeImg.enabled = false; // ligado em runtime, só quando há arte do bioma
        biomeBg.transform.SetAsFirstSibling();

        // A ficha do grupo: os heróis filmados pelo palco, andando de ponto a
        // ponto. Filha do mapa, e não do painel, para partilhar o mesmo sistema
        // de coordenadas dos nós — mover a ficha é copiar a posição de um nó.
        TrailRoadUI estrada = BuildTrailRoad(mapRow);

        // ── A caixa do evento, sobre o mapa ────────────────────────────────
        // Uma caixa só, à esquerda, no lugar das quatro faixas soltas de antes.
        // Aparece quando o grupo chega a algum lugar e some enquanto ele anda —
        // é o que dá à viagem um ritmo de "andar, parar, decidir".
        // Alta o bastante para as quatro escolhas e a narrativa, e não mais:
        // com 580px de altura a caixa tomava metade da tela para exibir três
        // linhas de texto, e o vazio no meio dela lia como tela quebrada. O topo
        // fica abaixo da faixa de aviso, que mora no alto e cobria o título.
        GameObject eventBox = EnsureFreeArea(panel.transform, "EventBox",
            new Vector2(0, 0), new Vector2(0, 0), new Vector2(40, 360), new Vector2(660, 830));

        var eventBg = eventBox.GetComponent<Image>();
        if (eventBg == null) eventBg = Undo.AddComponent<Image>(eventBox);
        eventBg.color = new Color(0.05f, 0.04f, 0.03f, 0.88f);
        eventBg.raycastTarget = true;   // a caixa segura o clique, o mapa não recebe

        // Os textos existiam soltos no painel. Mudá-los de pai, em vez de criar
        // outros, preserva o que o Inspector já aponta — e não deixa um par de
        // objetos órfãos invisíveis na cena a cada montagem.
        Reparentar(panel.transform, "Txt_EventTitle", eventBox.transform);
        Reparentar(panel.transform, "Txt_EventDescription", eventBox.transform);
        Reparentar(panel.transform, "Txt_ResolutionLog", eventBox.transform);
        Reparentar(panel.transform, "Txt_Upcoming", eventBox.transform);
        Reparentar(panel.transform, "ChoiceContainer", eventBox.transform);

        var evTitle = EnsureText(eventBox.transform, "Txt_EventTitle", "Evento", 26,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -58), new Vector2(-16, -14));

        var choices = EnsureColumn(eventBox.transform, "ChoiceContainer",
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(16, 16), new Vector2(-16, 240), 8);

        // As opções dividem a altura disponível em vez de manter o tamanho do
        // prefab: eventos de combate têm quatro botões e, com altura fixa, a
        // lista transbordava por cima do status do grupo.
        var choicesLayout = choices.GetComponent<VerticalLayoutGroup>();
        if (choicesLayout != null)
        {
            choicesLayout.childControlHeight = true;
            choicesLayout.childForceExpandHeight = true;
            choicesLayout.childControlWidth = true;
            choicesLayout.childForceExpandWidth = true;
        }

        var evDesc = EnsureText(eventBox.transform, "Txt_EventDescription", "", 19,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -160), new Vector2(-16, -64));
        var log = EnsureText(eventBox.transform, "Txt_ResolutionLog", "", 17,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -212), new Vector2(-16, -164));
        // "Adiante:" fica colado no alto da fila de escolhas, e não solto no meio
        // da caixa — na primeira montagem ele caiu por cima do primeiro botão.
        var upcoming = EnsureText(eventBox.transform, "Txt_Upcoming", "", 16,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(16, 244), new Vector2(-16, 278));

        // ── Rodapé ─────────────────────────────────────────────────────────
        // Vida e estresse à esquerda, mão à direita, lado a lado. Empilhados
        // como antes, os dois comiam 475px de altura da tela.
        var partyRow = EnsureRow(panel.transform, "PartyStatus", new Vector2(0, 0), new Vector2(0.34f, 0),
            new Vector2(20, 20), new Vector2(0, 190), 10);

        // Mão de cartas: área livre (o leque posiciona sozinho).
        //
        // A altura daqui é o que decide o tamanho da carta: HandFanLayout escala
        // pela altura disponível, então uma faixa curta encolhe a mão inteira.
        // Com os 240px de antes a carta saía a ~0,49 — metade do tamanho que tem
        // no combate, onde a faixa é de 444px. O combate já tinha recebido esse
        // ajuste e a jornada ficou para trás.
        var hand = EnsureFreeArea(panel.transform, "HandContainer", new Vector2(0.34f, 0), new Vector2(1, 0),
            new Vector2(10, 34), new Vector2(-20, 340));

        // O leque da jornada nunca foi montado por aqui — só o do combate era.
        // Enquanto a mão viveu numa faixa larga e sempre ativa isso não apareceu;
        // com a faixa mais estreita e o container ligando e desligando a cada
        // parada, as cinco cartas passaram a nascer empilhadas no centro, e a
        // tela mostrava uma carta só com o contador dizendo 5.
        var fanJornada = hand.GetComponent<HandFanLayout>();
        if (fanJornada == null) fanJornada = Undo.AddComponent<HandFanLayout>(hand);
        Undo.RecordObject(fanJornada, "Montar Cena");
        fanJornada.overlap = 0.16f;
        fanJornada.maxWidth = 1000f;
        EditorUtility.SetDirty(fanJornada);

        // Provisões e contadores: segunda linha do HUD, no alto. Estavam na base
        // da tela, onde a mão de cartas passava por cima deles.
        var rations = EnsureText(panel.transform, "Txt_Rations", "", 20, new Vector2(0, 1), new Vector2(0.10f, 1), new Vector2(20, -84), new Vector2(0, -54));
        var torches = EnsureText(panel.transform, "Txt_Torches", "", 20, new Vector2(0.10f, 1), new Vector2(0.20f, 1), new Vector2(0, -84), new Vector2(0, -54));
        var energy = EnsureText(panel.transform, "Txt_Energy", "", 20, new Vector2(0.20f, 1), new Vector2(0.30f, 1), new Vector2(0, -84), new Vector2(0, -54));
        var detourCount = EnsureText(panel.transform, "Txt_Detours", "", 20, new Vector2(0.30f, 1), new Vector2(0.42f, 1), new Vector2(0, -84), new Vector2(0, -54));

        // Baralho, mão e descarte: num deckbuilder, saber quantas cartas restam é
        // informação de jogo, não enfeite. Os três campos existiam no script e
        // nunca tinham sido criados na cena, então os contadores não apareciam.
        var deckCount = EnsureText(panel.transform, "Txt_DeckCount", "", 20, new Vector2(0.42f, 1), new Vector2(0.50f, 1), new Vector2(0, -84), new Vector2(0, -54));
        var handCount = EnsureText(panel.transform, "Txt_HandCount", "", 20, new Vector2(0.50f, 1), new Vector2(0.58f, 1), new Vector2(0, -84), new Vector2(0, -54));
        var discardCount = EnsureText(panel.transform, "Txt_DiscardCount", "", 20, new Vector2(0.58f, 1), new Vector2(0.66f, 1), new Vector2(0, -84), new Vector2(0, -54));

        // Botões: canto superior direito, ao lado do que eles afetam. Na base da
        // tela ficavam sob a mão de cartas, que é justamente o que o jogador
        // arrasta — e "Abandonar" é o botão mais caro do jogo para se clicar sem
        // querer.
        var abort = EnsureButton(panel.transform, "Btn_Abort", "Abandonar",
            new Vector2(1, 1), new Vector2(1, 1), new Vector2(-580, -50), new Vector2(-400, -14));
        var endTurn = EnsureButton(panel.transform, "Btn_EndTurn", "Descansar",
            new Vector2(1, 1), new Vector2(1, 1), new Vector2(-390, -50), new Vector2(-210, -14));
        var detour = EnsureButton(panel.transform, "Btn_Detour", "Desviar",
            new Vector2(1, 1), new Vector2(1, 1), new Vector2(-200, -50), new Vector2(-20, -14));

        // JourneyManager
        JourneyManager jm = Object.FindObjectOfType<JourneyManager>();
        if (jm == null)
        {
            var host = new GameObject("JourneyManager");
            Undo.RegisterCreatedObjectUndo(host, "Criar JourneyManager");
            jm = host.AddComponent<JourneyManager>();
            host.AddComponent<CardManager>();
        }

        Undo.RecordObject(jm, "Montar Cena");
        jm.journeyPanel = panel;
        jm.questNameText = questName;
        jm.dayText = day;
        jm.biomeText = biome;
        jm.biomeIcon = biomeImg;
        jm.eventTitleText = evTitle;
        jm.eventDescriptionText = evDesc;
        jm.resolutionLogText = log;
        jm.upcomingEventsText = upcoming;
        jm.choiceContainer = choices.transform;
        jm.choiceButtonPrefab = choiceBtn;
        jm.partyStatusContainer = partyRow.transform;
        jm.partyStatusPrefab = partyStatus;
        jm.handContainer = hand.transform;
        jm.cardPrefab = cardPrefab;
        jm.rationsText = rations;
        jm.torchesText = torches;
        jm.energyText = energy;
        jm.detourCountText = detourCount;
        jm.deckCountText = deckCount;
        jm.handCountText = handCount;
        jm.discardCountText = discardCount;
        jm.abortButton = abort;
        jm.endTurnButton = endTurn;
        jm.detourButton = detour;
        jm.trailRoad = estrada;
        jm.eventBox = eventBox;
        EditorUtility.SetDirty(jm);

        // Mapa da jornada
        JourneyMapUI map = Object.FindObjectOfType<JourneyMapUI>();
        if (map == null)
            map = jm.gameObject.AddComponent<JourneyMapUI>();

        Undo.RecordObject(map, "Montar Cena");
        map.nodeContainer = mapRow.GetComponent<RectTransform>();
        map.nodePrefab = mapNode;
        map.nodeDetailText = upcoming;
        map.partyToken = estrada;

        // Espaçamento da rota. São campos serializados: mudar o valor no script
        // não muda a cena — os dois precisam concordar, e é aqui que a cena é
        // escrita.
        //
        // Foram de 300/185 para 380/215 quando o mapa ganhou o papel por baixo:
        // com o pergaminho à mostra, a rota apertada lia como um diagrama solto
        // em cima dele, e não como caminho desenhado no mapa.
        map.layerSpacing = 380f;

        // 175, e não 215: com três fileiras, 215 espalhava a rota por 430px e a
        // ficha do grupo — que cresce para cima a partir do ponto — saía pelo
        // topo do papel. O comprimento da rota pode crescer à vontade, porque
        // ele rola; a altura tem de caber de uma vez.
        map.slotSpacing = 175f;
        EditorUtility.SetDirty(map);

        panel.SetActive(false);
        return panel;
    }

    /// <summary>
    /// A ficha do grupo: os heróis filmados pelo palco, do tamanho de uma peça
    /// de tabuleiro, andando de ponto a ponto do mapa.
    ///
    /// Vive <b>dentro do container do mapa</b>, e não do painel: assim ela e os
    /// nós compartilham o mesmo referencial, e mover a ficha para um ponto é
    /// copiar a <c>anchoredPosition</c> dele.
    ///
    /// O tamanho é fixo e a textura nasce dele — <c>RawImage</c> não tem
    /// <c>preserveAspect</c>, então a filmagem preenche o retângulo inteiro e
    /// qualquer descasamento de proporção deforma os bonecos.
    /// </summary>
    static TrailRoadUI BuildTrailRoad(GameObject mapa)
    {
        GameObject ficha = EnsureFreeArea(mapa.transform, "PartyToken",
            new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(FichaLargura, FichaAltura));

        var rt = ficha.GetComponent<RectTransform>();
        rt.pivot = new Vector2(0.5f, 0.18f);   // o pé da fila é o que encosta no ponto

        var raw = ficha.GetComponent<RawImage>();
        if (raw == null) raw = Undo.AddComponent<RawImage>(ficha);
        raw.raycastTarget = false;
        raw.color = Color.white;
        raw.enabled = false;   // ligado em runtime, quando a jornada monta o elenco

        var road = ficha.GetComponent<TrailRoadUI>();
        if (road == null) road = Undo.AddComponent<TrailRoadUI>(ficha);

        Undo.RecordObject(road, "Montar Cena");
        road.janela = raw;
        EditorUtility.SetDirty(road);

        // Último irmão: a ficha anda por cima das trilhas e dos pontos.
        ficha.transform.SetAsLastSibling();
        return road;
    }

    /// <summary>
    /// Tamanho da ficha do grupo, em pixels de canvas.
    ///
    /// Peça de tabuleiro, não cena: com 260px de largura a fila cobria o ponto
    /// vizinho inteiro e o jogador não via para onde podia ir.
    /// </summary>
    const float FichaLargura = 210f;
    const float FichaAltura = 125f;

    /// <summary>
    /// Move um objeto já existente para outro pai, preservando as referências
    /// que o Inspector guarda para ele.
    ///
    /// A alternativa — deixar o antigo onde está e criar um novo no lugar certo —
    /// enche a cena de sósias invisíveis a cada remontagem, e o painel passa a
    /// ter dois "Txt_EventTitle", um deles morto e desenhado em cima do outro.
    /// </summary>
    static void Reparentar(Transform de, string nome, Transform para)
    {
        Transform alvo = de.Find(nome);
        if (alvo == null || para == null || alvo.parent == para) return;

        Undo.SetTransformParent(alvo, para, "Montar Cena");
        alvo.SetParent(para, false);
    }

    #endregion

    #region Provisões

    /// <summary>
    /// Monta a compra de rações e tochas no último passo da preparação.
    ///
    /// Essa tela foi feita à mão fora do Montar Cena, então aqui só acrescentamos
    /// um bloco dentro do passo 3 e ligamos as referências — sem tocar no resto.
    /// </summary>
    static void BuildProvisions()
    {
        QuestSelectionUI qs = Object.FindObjectOfType<QuestSelectionUI>(true);
        if (qs == null)
        {
            Debug.LogWarning("Montar Cena: QuestSelectionUI não encontrado — provisões não montadas.");
            return;
        }

        Transform host = qs.step3Panel != null ? qs.step3Panel.transform
                       : (qs.selectionRoot != null ? qs.selectionRoot.transform : null);

        if (host == null)
        {
            Debug.LogWarning("Montar Cena: sem step3Panel nem selectionRoot — provisões não montadas.");
            return;
        }

        // Abaixo do container, à esquerda: ancorado dentro dele, o bloco cobria o
        // último card de deck. A faixa do rodapé está livre — os botões de
        // navegação ficam do outro lado.
        GameObject box = EnsureBox(host, "Panel_Provisions",
            new Vector2(0, 0), new Vector2(0, 0), new Vector2(12, -190), new Vector2(342, -16));

        EnsureText(box.transform, "Txt_ProvisionsTitle", "Provisões", 20,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(10, -34), new Vector2(-10, -6));

        var rationsText = EnsureText(box.transform, "Txt_Rations", "🍖 8  (+0)", 18,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(52, -72), new Vector2(-52, -40));
        var rationsMinus = EnsureButton(box.transform, "Btn_RationsMinus", "−",
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -72), new Vector2(46, -40));
        var rationsPlus = EnsureButton(box.transform, "Btn_RationsPlus", "+",
            new Vector2(1, 1), new Vector2(1, 1), new Vector2(-46, -72), new Vector2(-10, -40));

        var torchesText = EnsureText(box.transform, "Txt_Torches", "🔥 4  (+0)", 18,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(52, -112), new Vector2(-52, -80));
        var torchesMinus = EnsureButton(box.transform, "Btn_TorchesMinus", "−",
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -112), new Vector2(46, -80));
        var torchesPlus = EnsureButton(box.transform, "Btn_TorchesPlus", "+",
            new Vector2(1, 1), new Vector2(1, 1), new Vector2(-46, -112), new Vector2(-10, -80));

        var cost = EnsureText(box.transform, "Txt_ProvisionsCost", "Nenhuma provisão extra", 16,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(10, -150), new Vector2(-10, -118));

        Undo.RecordObject(qs, "Montar Cena");
        qs.rationsBuyText = rationsText;
        qs.torchesBuyText = torchesText;
        qs.provisionsCostText = cost;
        qs.rationsPlusButton = rationsPlus;
        qs.rationsMinusButton = rationsMinus;
        qs.torchesPlusButton = torchesPlus;
        qs.torchesMinusButton = torchesMinus;
        EditorUtility.SetDirty(qs);
    }

    #endregion

    #region Formação

    /// <summary>
    /// Monta a fila do grupo ao lado da escolha de heróis.
    ///
    /// Fica na raiz da seleção, e não dentro do passo 2: o passo 2 já ocupa a
    /// faixa central com a lista e os requisitos, e a única área livre é a coluna
    /// da esquerda, fora do rect dele. Quem liga e desliga este painel é o
    /// QuestSelectionUI, junto com o passo 2.
    /// </summary>
    static void BuildFormation()
    {
        QuestSelectionUI qs = Object.FindObjectOfType<QuestSelectionUI>(true);
        if (qs == null)
        {
            Debug.LogWarning("Montar Cena: QuestSelectionUI não encontrado — formação não montada.");
            return;
        }

        Transform host = qs.selectionRoot != null ? qs.selectionRoot.transform
                       : (qs.step2Panel != null && qs.step2Panel.transform.parent != null
                            ? qs.step2Panel.transform.parent
                            : null);

        if (host == null)
        {
            Debug.LogWarning("Montar Cena: sem selectionRoot — formação não montada.");
            return;
        }

        // Coluna esquerda da tela de preparação: 320 de largura, colada à borda.
        GameObject box = EnsureBox(host, "Panel_Formation",
            new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(20, -338), new Vector2(340, 338));

        EnsureText(box.transform, "Txt_FormationTitle", "Formação do grupo", 22,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(12, -44), new Vector2(-12, -10));

        var hint = EnsureText(box.transform, "Txt_FormationHint", "", 15,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(12, -126), new Vector2(-12, -48));

        var list = EnsureColumn(box.transform, "FormationList",
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(8, 10), new Vector2(-8, -132), 6);

        // As linhas são criadas em tempo de execução e precisam ocupar a largura
        // toda; sem isto elas nasceriam com o tamanho zero do RectTransform novo.
        var layout = list.GetComponent<VerticalLayoutGroup>();
        if (layout != null)
        {
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperCenter;
        }

        Undo.RecordObject(qs, "Montar Cena");
        qs.formationPanel = box;
        qs.formationContainer = list.transform;
        qs.formationHintText = hint;
        EditorUtility.SetDirty(qs);

        box.SetActive(false);
    }

    /// <summary>Caixa com fundo, para agrupar controles.</summary>
    internal static GameObject EnsureBox(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
                                Vector2 offsetMin, Vector2 offsetMax)
    {
        Transform found = parent.Find(name);
        GameObject go;

        if (found != null)
        {
            go = found.gameObject;
        }
        else
        {
            go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(go, "Criar caixa");
            go.transform.SetParent(parent, false);
        }

        var img = go.GetComponent<Image>();
        if (img == null) img = go.AddComponent<Image>();
        img.color = BoxColor;

        ApplyRect(go.GetComponent<RectTransform>(), anchorMin, anchorMax, offsetMin, offsetMax);
        return go;
    }

    #endregion

    #region Combate

    /// <summary>
    /// A tela de combate, disposta como a de Darkest Dungeon: o grupo à esquerda
    /// em fila, os inimigos à direita, encarando-se.
    ///
    /// Antes os inimigos ficavam numa faixa no alto e a party numa fileira de
    /// cards pequenos embaixo deles — leitura de Slay the Spire, em que só existe
    /// um lado. Aqui há dois lados e **posição importa**: a formação decide quem
    /// apanha e quanta força cada carta tem. Pôr os dois grupos frente a frente é
    /// o que torna essa regra visível sem uma linha de explicação.
    ///
    /// A ordem da fila do grupo é invertida de propósito (<c>reverseArrangement</c>):
    /// a posição 1 fica **à direita**, encostada nos inimigos, porque é ela que
    /// está na linha de frente. Numa fila da esquerda para a direita o herói mais
    /// exposto apareceria no canto mais distante do perigo.
    /// </summary>
    static GameObject BuildCombat(Canvas canvas, GameObject cardPrefab)
    {
        GameObject panel = FindOrCreatePanel(canvas, "Panel_Combat");

        var turn = EnsureText(panel.transform, "Txt_Turn", "Turno 0", 26,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -58), new Vector2(240, -16));
        var energy = EnsureText(panel.transform, "Txt_Energy", "", 26,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(252, -58), new Vector2(440, -16));

        // A ordem do round, logo abaixo do cabeçalho: é a primeira coisa a se
        // olhar antes de decidir a jogada.
        TurnOrderBar ordem = BuildTurnOrder(panel);

        var instruction = EnsureText(panel.transform, "Txt_Instruction", "", 20,
            new Vector2(0, 1), new Vector2(0.64f, 1), new Vector2(24, -176), new Vector2(0, -140));

        // O log sobe para o canto: no rodapé ele disputava espaço com a mão, e a
        // mão é onde o jogador olha.
        var log = EnsureText(panel.transform, "Txt_CombatLog", "", 17,
            new Vector2(0.66f, 1), new Vector2(1, 1), new Vector2(0, -176), new Vector2(-24, -16));
        log.alignment = TextAlignmentOptions.TopRight;
        log.color = SubtleTextColor;

        // --- O campo de batalha: dois lados, frente a frente ------------------

        // A janela por onde se vê o palco filmado. Vem antes das duas fileiras
        // na ordem de irmãos, e é isso que põe os corpos **atrás** dos nomes,
        // das barras e da intenção — quem nasce depois é desenhado por cima.
        BattleFieldUI campo = BuildBattleField(panel);

        var heroes = EnsureRow(panel.transform, "HeroContainer", new Vector2(0, 1), new Vector2(0.5f, 1),
            new Vector2(30, -566), new Vector2(-10, -196), 12);

        var filaDoGrupo = heroes.GetComponent<HorizontalLayoutGroup>();
        if (filaDoGrupo != null)
        {
            Undo.RecordObject(filaDoGrupo, "Montar Cena");
            filaDoGrupo.childAlignment = TextAnchor.LowerRight;
            filaDoGrupo.reverseArrangement = true;
            EditorUtility.SetDirty(filaDoGrupo);
        }

        var enemies = EnsureRow(panel.transform, "EnemyContainer", new Vector2(0.5f, 1), new Vector2(1, 1),
            new Vector2(10, -566), new Vector2(-30, -196), 16);

        var filaInimiga = enemies.GetComponent<HorizontalLayoutGroup>();
        if (filaInimiga != null)
        {
            Undo.RecordObject(filaInimiga, "Montar Cena");
            filaInimiga.childAlignment = TextAnchor.LowerLeft;
            filaInimiga.reverseArrangement = false;
            EditorUtility.SetDirty(filaInimiga);
        }

        // Os dois lados usam moldes próprios, montados aqui: o PartyStatusPrefab
        // e o EnemyCardPrefab continuam servindo à jornada, onde o card pequeno é
        // o certo. No combate a figura é o assunto.
        GameObject moldeHeroi = EnsureCombatHeroTemplate(panel.transform);
        GameObject moldeInimigo = EnsureCombatEnemyTemplate(panel.transform);

        // O leque encolhe a carta até ela caber na altura desta faixa, então é a
        // faixa que decide o corpo do texto: com 284px a carta saía a ~60% e a
        // descrição ficava ilegível. Havia 200px de tela vazia entre a party e os
        // inimigos; a mão ficou com eles.
        var hand = EnsureFreeArea(panel.transform, "HandContainer", new Vector2(0, 0), new Vector2(1, 0),
            new Vector2(20, 56), new Vector2(-20, 500));

        // Espalha o leque: cada carta cobria 54px da anterior, engolindo o fim de
        // cada linha da descrição.
        var fan = hand.GetComponent<HandFanLayout>();
        if (fan == null) fan = Undo.AddComponent<HandFanLayout>(hand);
        Undo.RecordObject(fan, "Montar Cena");
        fan.overlap = 0.16f;
        fan.maxWidth = 1200f;
        EditorUtility.SetDirty(fan);

        var deckCount = EnsureText(panel.transform, "Txt_Deck", "", 18, new Vector2(0, 0), new Vector2(0.2f, 0), new Vector2(20, 12), new Vector2(0, 48));
        var discardCount = EnsureText(panel.transform, "Txt_Discard", "", 18, new Vector2(0.2f, 0), new Vector2(0.4f, 0), new Vector2(0, 12), new Vector2(0, 48));

        var endTurn = EnsureButton(panel.transform, "Btn_EndTurn", "Terminar turno",
            new Vector2(1, 0), new Vector2(1, 0), new Vector2(-410, 12), new Vector2(-210, 48));
        var flee = EnsureButton(panel.transform, "Btn_Flee", "Recuar",
            new Vector2(1, 0), new Vector2(1, 0), new Vector2(-200, 12), new Vector2(-20, 48));

        CombatManager cm = Object.FindObjectOfType<CombatManager>();
        if (cm == null)
        {
            var host = new GameObject("CombatManager");
            Undo.RegisterCreatedObjectUndo(host, "Criar CombatManager");
            cm = host.AddComponent<CombatManager>();
        }

        Undo.RecordObject(cm, "Montar Cena");
        cm.combatPanel = panel;
        cm.enemyContainer = enemies.transform;
        cm.enemyPrefab = moldeInimigo;
        cm.heroContainer = heroes.transform;
        cm.heroStatusPrefab = moldeHeroi;
        cm.turnOrder = ordem;
        cm.battleField = campo;
        cm.handContainer = hand.transform;
        cm.cardPrefab = cardPrefab;
        cm.turnText = turn;
        cm.energyText = energy;
        cm.deckCountText = deckCount;
        cm.discardCountText = discardCount;
        cm.combatLogText = log;
        cm.instructionText = instruction;
        cm.endTurnButton = endTurn;
        cm.fleeButton = flee;
        EditorUtility.SetDirty(cm);

        panel.SetActive(false);
        return panel;
    }

    #endregion

    /// <summary>
    /// A janela do campo de batalha filmado.
    ///
    /// Cobre a faixa das duas fileiras com folga em cima: um chefe é desenhado
    /// muito maior que a área da própria view, e sem essa margem a cabeça dele
    /// seria cortada pela borda da textura — o corte não daria erro nenhum, só
    /// um monstro sem topo.
    ///
    /// Nasce apagada. Quem acende é o <c>CombatManager</c> ao começar a luta;
    /// fora do combate não há palco para filmar.
    /// </summary>
    static BattleFieldUI BuildBattleField(GameObject panel)
    {
        Transform existente = panel.transform.Find("BattleField");
        GameObject go;

        if (existente != null)
        {
            go = existente.gameObject;
        }
        else
        {
            go = new GameObject("BattleField", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            Undo.RegisterCreatedObjectUndo(go, "Criar campo de batalha");
            go.transform.SetParent(panel.transform, false);
        }

        var raw = go.GetComponent<RawImage>();
        if (raw == null) raw = go.AddComponent<RawImage>();

        // A carta é solta na view da figura, que fica por cima: a janela captando
        // o ponteiro engoliria todo drop do combate.
        raw.raycastTarget = false;
        raw.color = new Color(1f, 1f, 1f, 0f);

        ApplyRect(go.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1),
                  new Vector2(0, -610), new Vector2(0, -40));

        var ui = go.GetComponent<BattleFieldUI>();
        if (ui == null) ui = Undo.AddComponent<BattleFieldUI>(go);

        Undo.RecordObject(ui, "Montar Cena");
        ui.janela = raw;
        EditorUtility.SetDirty(ui);

        // Atrás de tudo o que o painel desenha, e à frente só do fundo dele.
        go.transform.SetAsFirstSibling();

        return ui;
    }

    #region Figuras do combate

    /// <summary>Altura das duas figuras. Igual dos dois lados: é um duelo.</summary>
    const float AlturaDaFigura = 370f;

    /// <summary>
    /// O herói no combate: retrato grande em cima, nome e barras embaixo.
    ///
    /// Os nomes dos filhos são contrato com o <c>CombatManager</c>, que os busca
    /// por <c>transform.Find</c> — "Portrait", "Name", "HP", "HPBar/Fill",
    /// "Stress", "StressBar/Fill" e "Block". Renomear qualquer um aqui apaga a
    /// informação na tela sem erro nenhum no console.
    /// </summary>
    static GameObject EnsureCombatHeroTemplate(Transform parent)
    {
        GameObject go = EnsureFigureTemplate(parent, "CombatHeroTemplate", 200f);

        // O bloqueio vai **acima da cabeça**, no lugar em que o inimigo mostra a
        // intenção. São a mesma pergunta lida em sequência — "quanto vem" e
        // "quanto eu aparo" —, e ficavam em pontas opostas da figura.
        var bloqueio = EnsureText(go.transform, "Block", "", 18,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(6, -42), new Vector2(-6, -8));
        bloqueio.alignment = TextAlignmentOptions.Center;

        // A área da figura tem a mesma altura da do inimigo: é um duelo, e um
        // lado desenhado maior que o outro já diria quem vence.
        EnsurePortrait(go.transform, new Vector2(0, 1), new Vector2(1, 1),
                       new Vector2(10, -250), new Vector2(-10, -46));

        var nome = EnsureText(go.transform, "Name", "", 19,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(6, -282), new Vector2(-6, -252));
        nome.alignment = TextAlignmentOptions.Center;

        var hp = EnsureText(go.transform, "HP", "", 17,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(6, -306), new Vector2(-6, -284));
        hp.alignment = TextAlignmentOptions.Center;

        EnsureBar(go.transform, "HPBar", new Vector2(10, -324), new Vector2(-10, -310),
                  new Color(0.62f, 0.16f, 0.16f));

        var estresse = EnsureText(go.transform, "Stress", "", 15,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(6, -348), new Vector2(-6, -326));
        estresse.alignment = TextAlignmentOptions.Center;
        estresse.color = SubtleTextColor;

        EnsureBar(go.transform, "StressBar", new Vector2(10, -364), new Vector2(-10, -352),
                  new Color(0.55f, 0.52f, 0.45f));

        go.SetActive(false);
        return go;
    }

    /// <summary>
    /// O inimigo no combate: a intenção **acima da cabeça**, como no Slay the
    /// Spire — é o dado com que o jogador decide entre atacar e se defender, e
    /// ele precisa estar junto de quem vai executá-la, não numa lista à parte.
    /// </summary>
    static GameObject EnsureCombatEnemyTemplate(Transform parent)
    {
        GameObject go = EnsureFigureTemplate(parent, "CombatEnemyTemplate", 240f);

        // A área do retrato ocupa quase todo o card: os quadros dos pacotes têm
        // muita transparência em volta da criatura, e com preserveAspect é o
        // quadro inteiro que é encaixado — dar pouco espaço encolhe o bicho a um
        // boneco de 50px, que foi como o esqueleto apareceu na primeira captura.
        //
        // O retrato é criado ANTES da intenção porque quem nasce depois é
        // desenhado por cima: com a criatura ampliada crescendo para o topo do
        // card, a intenção ficava atrás dela e sumia. É a mesma regra de ordem de
        // irmãos que já custou caro na sessão do kit visual.
        EnsurePortrait(go.transform, new Vector2(0, 1), new Vector2(1, 1),
                       new Vector2(10, -250), new Vector2(-10, -46));

        // Fundo próprio para a intenção continuar legível sobre a criatura.
        GameObject faixaIntencao = EnsureBox(go.transform, "IntentBox",
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(4, -46), new Vector2(-4, -6));

        var fundoIntencao = faixaIntencao.GetComponent<Image>();
        if (fundoIntencao != null)
        {
            fundoIntencao.color = new Color(0.06f, 0.055f, 0.07f, 0.88f);
            fundoIntencao.raycastTarget = false;
        }

        // "Intent" fica como filho direto da view: é assim que o CombatManager o
        // encontra (transform.Find, um nível só). Dentro da caixa, a intenção
        // ficaria em branco a luta inteira sem erro nenhum no console.
        var intencao = EnsureText(go.transform, "Intent", "", 18,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(8, -44), new Vector2(-8, -8));
        intencao.alignment = TextAlignmentOptions.Center;

        // Fundo e texto por último, nesta ordem: quem nasce depois é desenhado
        // por cima, e a criatura ampliada passa por trás dos dois.
        faixaIntencao.transform.SetAsLastSibling();
        intencao.transform.SetAsLastSibling();

        var nome = EnsureText(go.transform, "Name", "", 19,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(6, -282), new Vector2(-6, -252));
        nome.alignment = TextAlignmentOptions.Center;

        var hp = EnsureText(go.transform, "HP", "", 17,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(6, -310), new Vector2(-6, -284));
        hp.alignment = TextAlignmentOptions.Center;

        EnsureBar(go.transform, "HPBar", new Vector2(10, -330), new Vector2(-10, -314),
                  new Color(0.62f, 0.16f, 0.16f));

        var estado = EnsureText(go.transform, "Block", "", 16,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(6, -362), new Vector2(-6, -334));
        estado.alignment = TextAlignmentOptions.Center;

        // Alvo de carta arrastada: precisa de Image com raycast e de um Button,
        // que o CombatManager desliga para não engolir o drop.
        if (go.GetComponent<Button>() == null) Undo.AddComponent<Button>(go);

        go.SetActive(false);
        return go;
    }

    /// <summary>Base comum das duas figuras: fundo, tamanho fixo e lugar na fila.</summary>
    static GameObject EnsureFigureTemplate(Transform parent, string nome, float largura)
    {
        Transform existente = parent.Find(nome);
        GameObject go;

        if (existente != null)
        {
            go = existente.gameObject;
        }
        else
        {
            go = new GameObject(nome, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(go, "Criar figura de combate");
            go.transform.SetParent(parent, false);
        }

        var img = go.GetComponent<Image>();
        if (img == null) img = go.AddComponent<Image>();

        // Quase transparente de propósito: o fundo existe para receber o arrasto
        // da carta e para marcar o lugar da figura, não para desenhar uma caixa.
        //
        // Baixou de 55% para 16% quando o campo passou a ser filmado: a figura
        // agora é desenhada **atrás** desta imagem, e a 55% o corpo aparecia
        // como se estivesse dentro de um aquário sujo.
        img.color = new Color(0.13f, 0.12f, 0.14f, 0.16f);
        img.raycastTarget = true;

        go.GetComponent<RectTransform>().sizeDelta = new Vector2(largura, AlturaDaFigura);

        var elemento = go.GetComponent<LayoutElement>();
        if (elemento == null) elemento = go.AddComponent<LayoutElement>();
        elemento.minWidth = largura;
        elemento.preferredWidth = largura;
        elemento.minHeight = AlturaDaFigura;
        elemento.preferredHeight = AlturaDaFigura;

        return go;
    }

    /// <summary>O retrato, ancorado pelo pé: criatura grande cresce para cima.</summary>
    static void EnsurePortrait(Transform parent, Vector2 anchorMin, Vector2 anchorMax,
                               Vector2 offsetMin, Vector2 offsetMax)
    {
        Transform existente = parent.Find("Portrait");
        GameObject go;

        if (existente != null)
        {
            go = existente.gameObject;
        }
        else
        {
            go = new GameObject("Portrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(go, "Criar retrato");
            go.transform.SetParent(parent, false);
        }

        var img = go.GetComponent<Image>();
        if (img == null) img = go.AddComponent<Image>();

        img.preserveAspect = true;
        img.raycastTarget = false;

        var rt = go.GetComponent<RectTransform>();

        // O pivô vem ANTES do rect, e a ordem não é detalhe: trocar o pivô depois
        // mantém a anchoredPosition e **move** o retângulo meia altura para cima.
        // Foi o que jogou o retrato para fora do card e abriu um vazio de 80px
        // entre a figura e o nome — sem erro nenhum, só uma tela torta.
        //
        // Pé como pivô é o que faz o chefe ampliado crescer para cima em vez de
        // afundar no chão; a escala vem do EnemyData.portraitScale.
        rt.pivot = new Vector2(0.5f, 0f);

        ApplyRect(rt, anchorMin, anchorMax, offsetMin, offsetMax);
    }

    /// <summary>Trilho com preenchimento, no formato que o CombatManager espera.</summary>
    static void EnsureBar(Transform parent, string nome, Vector2 offsetMin, Vector2 offsetMax, Color corDoFill)
    {
        Transform existente = parent.Find(nome);
        GameObject trilho;

        if (existente != null)
        {
            trilho = existente.gameObject;
        }
        else
        {
            trilho = new GameObject(nome, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(trilho, "Criar barra");
            trilho.transform.SetParent(parent, false);
        }

        var fundo = trilho.GetComponent<Image>();
        if (fundo == null) fundo = trilho.AddComponent<Image>();
        fundo.color = TrackColor;
        fundo.raycastTarget = false;

        ApplyRect(trilho.GetComponent<RectTransform>(),
                  new Vector2(0, 1), new Vector2(1, 1), offsetMin, offsetMax);

        Transform achado = trilho.transform.Find("Fill");
        GameObject fill;

        if (achado != null)
        {
            fill = achado.gameObject;
        }
        else
        {
            fill = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(fill, "Criar preenchimento");
            fill.transform.SetParent(trilho.transform, false);
        }

        ApplyRect(fill.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var img = fill.GetComponent<Image>();
        if (img == null) img = fill.AddComponent<Image>();
        img.color = corDoFill;
        img.raycastTarget = false;
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillAmount = 1f;
    }

    #endregion

    #region Ordem do round

    /// <summary>
    /// A fila do round: uma ficha para o grupo e uma para cada inimigo vivo.
    ///
    /// O <c>BarSkin</c> veste as barras depois; esta faixa nasce chapada de
    /// propósito, porque a cor dela **é** informação (azul = grupo, vermelho =
    /// inimigo, apagado = já agiu) e um sprite por cima a apagaria.
    /// </summary>
    static TurnOrderBar BuildTurnOrder(GameObject panel)
    {
        GameObject faixa = EnsureFreeArea(panel.transform, "TurnOrder",
            new Vector2(0, 1), new Vector2(0.64f, 1), new Vector2(24, -134), new Vector2(0, -74));

        GameObject fila = EnsureRow(faixa.transform, "Chips",
            new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero, 8);

        var layout = fila.GetComponent<HorizontalLayoutGroup>();
        if (layout != null)
        {
            Undo.RecordObject(layout, "Montar Cena");
            layout.childAlignment = TextAnchor.MiddleLeft;
            EditorUtility.SetDirty(layout);
        }

        GameObject molde = EnsureTurnChipTemplate(faixa.transform);

        var barra = faixa.GetComponent<TurnOrderBar>();
        if (barra == null) barra = Undo.AddComponent<TurnOrderBar>(faixa);

        Undo.RecordObject(barra, "Montar Cena");
        barra.container = fila.transform;
        barra.chipTemplate = molde;
        EditorUtility.SetDirty(barra);

        return barra;
    }

    /// <summary>Molde de uma ficha da fila: arte pequena, nome e a marca do "agora".</summary>
    static GameObject EnsureTurnChipTemplate(Transform parent)
    {
        Transform existente = parent.Find("TurnChipTemplate");
        GameObject go;

        if (existente != null)
        {
            go = existente.gameObject;
        }
        else
        {
            go = new GameObject("TurnChipTemplate", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(go, "Criar ficha de ordem");
            go.transform.SetParent(parent, false);
        }

        // 148px cortavam "Salteador da Serra" em "Salteador d…". Nome de inimigo
        // é o que a ficha existe para dizer.
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(190, 48);

        var elemento = go.GetComponent<LayoutElement>();
        if (elemento == null) elemento = go.AddComponent<LayoutElement>();
        elemento.minWidth = 190;
        elemento.preferredWidth = 190;
        elemento.minHeight = 48;
        elemento.preferredHeight = 48;

        var img = go.GetComponent<Image>();
        if (img == null) img = go.AddComponent<Image>();
        img.raycastTarget = false;

        if (go.GetComponent<CanvasGroup>() == null) go.AddComponent<CanvasGroup>();

        // Arte à esquerda, nome à direita — a mesma leitura de uma ficha de
        // iniciativa de mesa.
        Transform arte = go.transform.Find("Art");
        GameObject artGo;

        if (arte != null)
        {
            artGo = arte.gameObject;
        }
        else
        {
            artGo = new GameObject("Art", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(artGo, "Criar arte da ficha");
            artGo.transform.SetParent(go.transform, false);
        }

        ApplyRect(artGo.GetComponent<RectTransform>(),
                  new Vector2(0, 0), new Vector2(0, 1), new Vector2(6, 6), new Vector2(44, -6));

        var artImg = artGo.GetComponent<Image>();
        if (artImg == null) artImg = artGo.AddComponent<Image>();
        artImg.preserveAspect = true;
        artImg.raycastTarget = false;

        var rotulo = EnsureText(go.transform, "Label", "", 14,
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(48, 4), new Vector2(-6, -4));
        rotulo.alignment = TextAlignmentOptions.Left;
        rotulo.enableWordWrapping = false;
        rotulo.overflowMode = TextOverflowModes.Ellipsis;

        var agora = EnsureText(go.transform, "Now", "▼", 16,
            new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-14, 0), new Vector2(14, 22));
        agora.alignment = TextAlignmentOptions.Center;
        agora.color = ButtonLabelColor;

        go.SetActive(false);
        return go;
    }

    #endregion


    // A Taverna, a Biblioteca, o Cemitério e a Sala de Mapas passaram a ser
    // montados por `Assets/Scripts/Core/Rooms/`, um arquivo por sala. Este setup
    // já tinha 3.000 linhas, e uma sala por arquivo é o que permitiu refazê-las em
    // paralelo sem que uma pisasse na outra.

    #region Mercado e Forja

    // O BuildRoomShell saiu daqui: era o molde "cabeçalho, lista, rodapé" que as
    // salas usavam antes de virarem lugar, e o Mercado foi a última a deixá-lo.
    // Cada sala agora mora no próprio arquivo, em Core/Rooms.

    /// <summary>
    /// Liga o atalho de Baralhos do rodapé, que nasceu sem ouvinte nenhum.
    ///
    /// O botão está na cena desde sempre com o <c>onClick</c> vazio: o jogador
    /// clica e não acontece nada, que é pior do que não haver botão. Ele passa a
    /// abrir a tela de baralhos — e o rodapé inteiro some na preparação e no
    /// combate, porque entrar nos baralhos no meio da preparação descarta a
    /// missão e a formação já escolhidas.
    ///
    /// O ouvinte é <b>persistente</b> (gravado na cena, como se tivesse sido
    /// arrastado no Inspector), e não um <c>AddListener</c> de runtime: este
    /// código roda no Editor, e um ouvinte de runtime não sobreviveria ao salvar.
    /// </summary>
    static void LigarAtalhoDeBaralhos(Canvas canvas, UIManager ui)
    {
        Transform atalho = canvas.transform.Find("Background/Panel_DownBar/DownInfo/DeckConfig");
        if (atalho == null) return;

        var botao = atalho.GetComponent<Button>();
        if (botao == null) return;

        // Já ligado numa montagem anterior: religar duplicaria o ouvinte e a
        // tela abriria duas vezes.
        for (int i = 0; i < botao.onClick.GetPersistentEventCount(); i++)
            if (botao.onClick.GetPersistentMethodName(i) == nameof(UIManager.ShowDeckManager))
                return;

        Undo.RecordObject(botao, "Ligar atalho de baralhos");
        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            botao.onClick, new UnityEngine.Events.UnityAction(ui.ShowDeckManager));
        EditorUtility.SetDirty(botao);
    }

    /// <summary>
    /// Aponta o <see cref="UIManager"/> para a sala refeita e tira a antiga de
    /// cena.
    ///
    /// As salas novas substituem painéis que já existiam na cena, então o
    /// <c>if (== null)</c> usado para os painéis inéditos deixaria o UIManager
    /// preso à tela velha para sempre. A antiga é <b>desligada</b>, não
    /// destruída: apagar objeto de cena por ferramenta é irreversível, e ela
    /// ainda guarda a hierarquia que o desenho novo substituiu.
    /// </summary>
    static void ReapontarSala(ref GameObject campo, GameObject novo, string nome)
    {
        if (novo == null || campo == novo) return;

        if (campo != null)
        {
            Undo.RecordObject(campo, $"Aposentar {nome} antiga");
            campo.SetActive(false);
        }

        campo = novo;
    }

    /// <summary>
    /// A Forja é a primeira sala que deixou de ser lista, e serve de molde para
    /// as outras seis.
    ///
    /// Três faixas em vez de uma coluna de linhas: a <b>fila</b> à esquerda, com
    /// quem espera a vez; a <b>bigorna</b> no meio, com um herói de cada vez, a
    /// arma e o escudo dele; e a <b>prateleira</b> à direita, com as cartas que a
    /// arma afeta. O que se compra fica visível no mesmo clique — antes, o efeito
    /// só aparecia no combate seguinte.
    ///
    /// O fundo é a silhueta de caverna do <i>Pixel Fantasy Caves</i>, com o miolo
    /// vazado, sobre a brasa desenhada atrás da bancada. Era a queixa mais direta
    /// do autor sobre as salas: todas usam o mesmo mármore escuro da guilda e
    /// nenhuma parece um lugar.
    /// </summary>
    static GameObject BuildForge(Canvas canvas, GameObject cardPrefab)
    {
        GameObject panel = FindOrCreatePanel(canvas, "Panel_Forge");

        VestirCavernaDaForja(panel);

        EnsureText(panel.transform, "Txt_Title", "⚒️ Forja", 32,
            new Vector2(0, 1), new Vector2(0.7f, 1), new Vector2(20, -70), new Vector2(0, -20));

        var gold = EnsureText(panel.transform, "Txt_Gold", "💰 0", 26,
            new Vector2(0.7f, 1), new Vector2(1, 1), new Vector2(0, -70), new Vector2(-20, -20));
        var hint = EnsureText(panel.transform, "Txt_Hint", "", 18,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -112), new Vector2(-20, -76));

        // ── A fila ────────────────────────────────────────────────────────────
        GameObject list = EnsureScrollColumn(panel.transform, "List",
            new Vector2(0, 0), new Vector2(0, 1), new Vector2(20, 110), new Vector2(360, -140), 8);

        var listLayout = list.GetComponent<VerticalLayoutGroup>();
        if (listLayout != null)
        {
            listLayout.childControlWidth = true;
            listLayout.childForceExpandWidth = true;
            listLayout.childControlHeight = true;
            listLayout.childForceExpandHeight = false;
            listLayout.childAlignment = TextAnchor.UpperCenter;
        }

        // ── A bigorna ─────────────────────────────────────────────────────────
        GameObject bench = EnsureFreeArea(panel.transform, "Bench",
            new Vector2(0, 0), new Vector2(0, 1), new Vector2(380, 110), new Vector2(1060, -140));
        VestirCaixa(bench, new Color(0.16f, 0.13f, 0.12f, 0.92f));

        // A primeira versão tinha uma "brasa" atrás dos slots: um retângulo laranja
        // a 13% de alfa que, sobre a caixa escura, virou um bloco marrom chapado
        // ocupando meia bancada. Sem sprite de fogo, calor não se desenha com
        // retângulo — some.
        Transform brasaVelha = bench.transform.Find("Brasa");
        if (brasaVelha != null) Undo.DestroyObjectImmediate(brasaVelha.gameObject);

        GameObject portraitGo = EnsureFreeArea(bench.transform, "Img_Portrait",
            new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-130, -290), new Vector2(130, -30));
        var portrait = EnsureImageComponent(portraitGo);
        portrait.preserveAspect = true;
        portrait.raycastTarget = false;

        var heroName = EnsureText(bench.transform, "Txt_HeroName", "", 26,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -344), new Vector2(-16, -296));
        heroName.alignment = TextAlignmentOptions.Center;

        var heroStats = EnsureText(bench.transform, "Txt_HeroStats", "", 19,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -388), new Vector2(-16, -346));
        heroStats.alignment = TextAlignmentOptions.Center;

        Image weaponFrame, weaponIcon, armorFrame, armorIcon;
        TMP_Text weaponLevel, armorLevel;
        Button weaponBtn, armorBtn;

        BuildAnvilSlot(bench.transform, "Slot_Weapon", 0.02f, 0.49f, "⚔️ FORJAR ARMA",
                       out weaponFrame, out weaponIcon, out weaponLevel, out weaponBtn);
        BuildAnvilSlot(bench.transform, "Slot_Armor", 0.51f, 0.98f, "🛡️ REFORÇAR ARMADURA",
                       out armorFrame, out armorIcon, out armorLevel, out armorBtn);

        // ── A prateleira de cartas ────────────────────────────────────────────
        var shelfTitle = EnsureText(panel.transform, "Txt_Shelf", "", 19,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(1090, -190), new Vector2(-20, -140));
        shelfTitle.alignment = TextAlignmentOptions.Center;

        GameObject shelf = EnsureFreeArea(panel.transform, "Shelf",
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(1090, 110), new Vector2(-20, -200));

        var feedback = EnsureText(panel.transform, "Txt_Feedback", "", 18,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(260, 40), new Vector2(-20, 96));

        var close = EnsureButton(panel.transform, "Btn_Close", "Voltar",
            new Vector2(0, 0), new Vector2(0, 0), new Vector2(20, 20), new Vector2(220, 60));

        ForgeManager forge = Object.FindObjectOfType<ForgeManager>();
        if (forge == null)
        {
            var host = new GameObject("ForgeManager");
            Undo.RegisterCreatedObjectUndo(host, "Criar ForgeManager");
            forge = host.AddComponent<ForgeManager>();
        }

        Undo.RecordObject(forge, "Montar Cena");
        forge.goldText = gold;
        forge.hintText = hint;
        forge.heroContainer = list.transform;
        forge.feedbackText = feedback;
        forge.closeButton = close;
        forge.benchRoot = bench;
        forge.portraitImage = portrait;
        forge.heroNameText = heroName;
        forge.heroStatsText = heroStats;
        forge.weaponFrame = weaponFrame;
        forge.weaponIcon = weaponIcon;
        forge.weaponLevelText = weaponLevel;
        forge.weaponButton = weaponBtn;
        forge.armorFrame = armorFrame;
        forge.armorIcon = armorIcon;
        forge.armorLevelText = armorLevel;
        forge.armorButton = armorBtn;
        forge.cardShelf = shelf.transform;
        forge.cardShelfTitle = shelfTitle;
        forge.cardPrefab = cardPrefab;
        EditorUtility.SetDirty(forge);

        panel.SetActive(false);
        return panel;
    }

    /// <summary>
    /// Um lugar para a peça: moldura, o desenho dentro dela, o nível em blocos e
    /// o botão que paga. A moldura é um <see cref="Image"/> próprio porque é ela
    /// que muda de cor com o nível — mago e curandeiro têm uma arma só, e sem a
    /// moldura não haveria o que mudar na tela ao forjar.
    /// </summary>
    static void BuildAnvilSlot(Transform bench, string nome, float esquerda, float direita, string rotulo,
                               out Image frame, out Image icon, out TMP_Text level, out Button button)
    {
        GameObject slot = EnsureFreeArea(bench, nome,
            new Vector2(esquerda, 0), new Vector2(direita, 1), new Vector2(0, 40), new Vector2(0, -404));

        GameObject frameGo = EnsureFreeArea(slot.transform, "Frame",
            new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-95, -190), new Vector2(95, 0));
        frame = EnsureImageComponent(frameGo);
        frame.raycastTarget = false;

        Sprite moldura = AssetDatabase.LoadAssetAtPath<Sprite>(OutlineSpritePath);
        if (moldura != null)
        {
            frame.sprite = moldura;
            frame.type = Image.Type.Sliced;
        }

        GameObject iconGo = EnsureFreeArea(frameGo.transform, "Icon",
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(6, 6), new Vector2(-6, -6));
        icon = EnsureImageComponent(iconGo);
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        // O quadro não é o desenho: os sprites do SPUM são 32×32 com a peça
        // ocupando um terço do quadro, e `preserveAspect` encaixa o quadro inteiro
        // — inclusive a transparência. Sem esta escala a varinha saía com 30px
        // dentro de uma moldura de 140. Mesmo motivo do `portraitScale` dos
        // inimigos; o que passar da moldura é transparência.
        iconGo.transform.localScale = Vector3.one * 1.5f;

        level = EnsureText(slot.transform, "Txt_Level", "", 20,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(4, -240), new Vector2(-4, -196));
        level.alignment = TextAlignmentOptions.Center;

        button = EnsureButton(slot.transform, "Btn_Buy", rotulo,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(10, -312), new Vector2(-10, -252));

        var texto = button.GetComponentInChildren<TMP_Text>();
        if (texto != null) texto.fontSize = 16;
    }

    /// <summary>
    /// O fundo que faz a sala parecer um lugar. A silhueta do pacote tem o miolo
    /// vazado, então ela emoldura a tela sem cobrir o que importa; o que aparece
    /// no vão é o mesmo escuro do resto do jogo.
    /// </summary>
    static void VestirCavernaDaForja(GameObject panel)
    {
        var fundo = panel.GetComponent<Image>();
        if (fundo != null) fundo.color = PanelColor;

        GameObject cave = EnsureFreeArea(panel.transform, "Bg_Cave",
            new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);
        cave.transform.SetAsFirstSibling();

        var image = EnsureImageComponent(cave);
        image.raycastTarget = false;
        image.color = new Color(0.42f, 0.37f, 0.34f);

        // Pixel art esticada até 1920 precisa de Point: no filtro padrão a rocha
        // vira borrão cinza, que foi como a primeira montagem saiu.
        var importer = AssetImporter.GetAtPath(CaveBackgroundPath) as TextureImporter;
        if (importer != null && (importer.textureType != TextureImporterType.Sprite
                                 || importer.filterMode != FilterMode.Point))
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.filterMode = FilterMode.Point;
            importer.SaveAndReimport();
        }

        Sprite caverna = AssetDatabase.LoadAssetAtPath<Sprite>(CaveBackgroundPath);
        image.sprite = caverna;
        image.enabled = caverna != null;
    }

    /// <summary>Caixa com a moldura do kit, para separar a bancada do fundo.</summary>
    static void VestirCaixa(GameObject alvo, Color cor)
    {
        var image = EnsureImageComponent(alvo);
        image.color = cor;
        image.raycastTarget = false;

        Sprite painel = AssetDatabase.LoadAssetAtPath<Sprite>(PanelSpritePath);
        if (painel == null) return;

        image.sprite = painel;
        image.type = Image.Type.Sliced;
    }

    static Image EnsureImageComponent(GameObject alvo)
    {
        var image = alvo.GetComponent<Image>();
        if (image == null) image = Undo.AddComponent<Image>(alvo);
        return image;
    }

    #endregion

    #region Clareza: o guia da guilda e o que a cena herdou sem nome

    /// <summary>
    /// A linha de conselho no alto da guilda e a moldura que acende a porta certa.
    ///
    /// O mapa da guilda são sete retângulos escuros de mesmo peso: nada separa a
    /// porta que faz o jogo andar das que só se visita de vez em quando. O realce
    /// é um filho de cada sala, criado desligado — quem decide qual acende é o
    /// <see cref="GuildGuide"/>, em tempo de execução.
    /// </summary>
    static void BuildGuildGuide(Canvas canvas)
    {
        Transform mapa = canvas.transform.Find("Background/GuildMap");
        if (mapa == null)
        {
            Debug.LogWarning("Montar Cena: GuildMap não encontrado — guia da guilda não montado.");
            return;
        }

        // O conselho vive no alto, onde a tela estava vazia, e ocupa a largura
        // toda: as frases citam nome de herói e chegam a duas linhas.
        TMP_Text linha = EnsureText(mapa, "Txt_Guia", "", 24,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(60, -108), new Vector2(-60, -34));
        linha.alignment = TextAlignmentOptions.Center;
        linha.color = ButtonLabelColor;
        linha.enableWordWrapping = true;

        Sprite moldura = AssetDatabase.LoadAssetAtPath<Sprite>(OutlineSpritePath);

        GuildGuide guia = mapa.GetComponent<GuildGuide>();
        if (guia == null) guia = mapa.gameObject.AddComponent<GuildGuide>();

        Undo.RecordObject(guia, "Montar Cena");
        guia.linha = linha;
        guia.salas.Clear();

        foreach (Transform sala in mapa)
        {
            if (sala.GetComponent<Button>() == null) continue;

            guia.salas.Add(new GuildGuide.Sala
            {
                nome = sala.name,
                realce = EnsureRealce(sala, moldura)
            });
        }

        EditorUtility.SetDirty(guia);
    }

    /// <summary>
    /// A moldura que acende uma sala. Nasce desligada e por cima de tudo o que a
    /// sala tem dentro — uma moldura desenhada antes do rótulo fica escondida
    /// atrás dele, que é a armadilha de ordem de irmãos de sempre neste projeto.
    /// </summary>
    static GameObject EnsureRealce(Transform sala, Sprite moldura)
    {
        Transform found = sala.Find("Realce");
        GameObject go;

        if (found != null)
        {
            go = found.gameObject;
        }
        else
        {
            go = new GameObject("Realce", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(go, "Criar realce");
            go.transform.SetParent(sala, false);
        }

        go.transform.SetAsLastSibling();

        var img = go.GetComponent<Image>();
        if (img == null) img = go.AddComponent<Image>();

        img.sprite = moldura;
        img.type = moldura != null ? Image.Type.Sliced : Image.Type.Simple;
        img.color = RealceTint;
        img.raycastTarget = false;

        // Transborda a sala de propósito: uma moldura rente à borda se confunde
        // com a moldura que o kit já pôs em toda caixa.
        ApplyRect(go.GetComponent<RectTransform>(), Vector2.zero, Vector2.one,
                  new Vector2(-10, -10), new Vector2(10, 10));

        go.SetActive(false);
        return go;
    }

    /// <summary>
    /// Dá nome ao que a cena herdou do primeiro protótipo.
    ///
    /// São botões e títulos que nunca saíram do texto de fábrica do editor: um
    /// "Button" e um "New Text" para cada um. O pior deles é o de sair da Taverna
    /// — o jogador não tem outro caminho de volta, e o botão que o leva embora
    /// se anuncia como "Button". O título da tela de baralhos, por sua vez, diz
    /// "Biblioteca", herdado de quando as duas telas eram a mesma.
    ///
    /// Os textos são escritos aqui, e não no Inspector, porque a cena é montada
    /// por código: qualquer edição à mão se perde na próxima montagem.
    /// </summary>
    static void RotularHerdados(Canvas canvas)
    {
        Rotular(canvas, "Background/Taverna/Image/ReturnBtn", "VOLTAR");

        // Os rótulos da tela de baralhos saíram daqui: a montagem herdada foi
        // substituída pelo DeckScreen, que cria os textos com o conteúdo certo.

        // A biblioteca herdada (`Background/Library`) foi substituída pelo
        // `Panel_Library` do `LibraryRoom`. Os rótulos e reposicionamentos que
        // existiam aqui — inclusive o do painel de bônus que prometia revelar
        // eventos, o que é a Sala de Mapas — saíram junto com ela.
    }

    /// <summary>
    /// Escreve no TMP do caminho — no próprio objeto ou no primeiro filho que
    /// tiver um, que é o caso dos botões.
    /// </summary>
    static void Rotular(Canvas canvas, string caminho, string texto)
    {
        Transform alvo = canvas.transform.Find(caminho);
        if (alvo == null)
        {
            Debug.LogWarning($"Rotular: não achei {caminho} — a cena mudou de forma?");
            return;
        }

        TMP_Text txt = alvo.GetComponent<TMP_Text>();
        if (txt == null) txt = alvo.GetComponentInChildren<TMP_Text>(true);

        if (txt == null)
        {
            Debug.LogWarning($"Rotular: {caminho} não tem texto nenhum dentro.");
            return;
        }

        Undo.RecordObject(txt, "Rotular herdados");
        txt.text = texto;
        EditorUtility.SetDirty(txt);
    }

    /// <summary>Recoloca um retângulo herdado, sem recriar o objeto — as
    /// referências do Inspector que apontam para ele continuam valendo.</summary>
    static void Reposicionar(Canvas canvas, string caminho, Vector2 anchorMin, Vector2 anchorMax,
                             Vector2 offsetMin, Vector2 offsetMax)
    {
        Transform alvo = canvas.transform.Find(caminho);
        if (alvo == null)
        {
            Debug.LogWarning($"Reposicionar: não achei {caminho} — a cena mudou de forma?");
            return;
        }

        var rt = alvo as RectTransform;
        if (rt == null) return;

        Undo.RecordObject(rt, "Rotular herdados");
        ApplyRect(rt, anchorMin, anchorMax, offsetMin, offsetMax);
        EditorUtility.SetDirty(rt);
    }

    #endregion

    #region Popups

    /// <summary>
    /// Põe os popups por último no Canvas, para que nada os cubra.
    ///
    /// Eles moravam dentro de "Background", o **primeiro** filho do Canvas,
    /// enquanto "Panel_Journey" e "Panel_Combat" são irmãos posteriores. Como a
    /// ordem de irmãos é a ordem de desenho, todo popup nascia atrás dessas telas
    /// — que ocupam o ecrã inteiro e têm `raycastTarget` ligado no fundo. Ao
    /// vencer a jornada, o popup de resultado aparecia coberto pelo painel da
    /// jornada: invisível e sem como fechar, prendendo o jogador numa tela sem
    /// saída.
    ///
    /// A ordem de irmãos resolve desenho e clique de uma vez, sem depender de
    /// `sortingOrder` — que, com o Canvas em Screen Space Camera, não foi
    /// suficiente sozinho. "Background" tem o mesmo rect do Canvas, então mudar de
    /// pai não desloca nada.
    /// </summary>
    /// <summary>
    /// Tira o aviso passageiro do meio da tela e o põe como faixa no alto.
    ///
    /// O <c>PopupMessage</c> é o recado de dois segundos — "Fulano se juntou à
    /// guilda", "Ouro insuficiente" — e nasceu centralizado e enorme, do tamanho
    /// de um modal. Ele cobre justamente o que o jogador acabou de fazer: na
    /// taverna tapa o candidato contratado, na biblioteca as cartas à venda, no
    /// combate o campo inteiro. Aparece assim em **toda** captura de tela desde
    /// que existe.
    ///
    /// Modal é quem exige decisão (<c>PopupConfirm</c>, <c>PopupResult</c>), e
    /// esses continuam no centro. Um aviso que se fecha sozinho não pode tomar a
    /// tela de quem não pediu nada.
    /// </summary>
    static void AssentarAviso(Canvas canvas)
    {
        var rt = canvas.transform.Find("PopupMessage") as RectTransform;
        if (rt == null) return;

        Undo.RecordObject(rt, "Assentar aviso");

        // Pivô antes da posição: trocá-lo depois manteria a anchoredPosition e
        // deslocaria a faixa meia altura — a armadilha de sempre.
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(940, 92);

        // Abaixo do HUD, não sobre ele. A jornada passou a ter duas linhas de
        // informação no alto — missão, dia, provisões e os três botões —, e a
        // faixa colada no teto cobria justamente o número do dia e o botão de
        // abandonar a expedição.
        rt.anchoredPosition = new Vector2(0, -100);
        EditorUtility.SetDirty(rt);

        // O fundo e o texto acompanham a faixa; o texto encolhe para caber nela.
        foreach (Transform filho in rt)
        {
            var filhoRt = filho as RectTransform;
            if (filhoRt == null) continue;

            Undo.RecordObject(filhoRt, "Assentar aviso");
            ApplyRect(filhoRt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            EditorUtility.SetDirty(filhoRt);
        }

        var texto = rt.GetComponentInChildren<TMP_Text>(true);
        if (texto != null)
        {
            Undo.RecordObject(texto, "Assentar aviso");
            texto.fontSize = 28;
            texto.enableAutoSizing = false;
            texto.alignment = TextAlignmentOptions.Center;
            texto.enableWordWrapping = true;
            EditorUtility.SetDirty(texto);
        }
    }

    static void LiftPopups(Canvas canvas)
    {
        // Do fundo para a frente: mensagem, confirmação, resultado. O de resultado
        // é o último porque pode aparecer sobre uma confirmação ainda na tela.
        string[] nomes = { "Loading", "PopupMessage", "PopupConfirm", "PopupResult" };

        foreach (var nome in nomes)
        {
            Transform popup = FindDeep(canvas.transform, nome);
            if (popup == null) continue;

            if (popup.parent != canvas.transform)
            {
                Undo.SetTransformParent(popup, canvas.transform, "Elevar popup");

                // O rect é o mesmo do pai anterior; reancorar evita que o popup
                // herde uma âncora que não faz sentido no novo pai.
                var rt = popup as RectTransform;
                if (rt != null)
                {
                    Undo.RecordObject(rt, "Elevar popup");
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = Vector2.zero;
                    EditorUtility.SetDirty(rt);
                }
            }

            popup.SetAsLastSibling();
        }
    }

    static Transform FindDeep(Transform root, string nome)
    {
        if (root.name == nome) return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform achado = FindDeep(root.GetChild(i), nome);
            if (achado != null) return achado;
        }

        return null;
    }

    #endregion

    #region Kit visual

    /// <summary>
    /// Veste a UI com o kit Bloodlines: pedra nos painéis, moldura nas caixas,
    /// botões com estados e a marca de seleção no lugar do quadrado vazio.
    ///
    /// Trabalha sobre o que já existe em vez de remontar: só preenche o sprite de
    /// quem ainda não tem. Assim uma escolha feita à mão no Editor sobrevive a
    /// rodar o Montar Cena de novo.
    /// </summary>
    static void ApplyKit(Canvas canvas)
    {
        if (!AplicarKitEm(canvas.gameObject)) return;

        Sprite moldura = AssetDatabase.LoadAssetAtPath<Sprite>(OutlineSpritePath);
        if (moldura != null)
            foreach (var img in canvas.GetComponentsInChildren<Image>(true))
                if (img.color == BoxColor && img.GetComponent<Button>() == null)
                    EnsureOutline(img.rectTransform, moldura);

        ApplyTitleFont(canvas);
    }

    /// <summary>
    /// Veste uma hierarquia qualquer com o kit — a cena ou um prefab.
    ///
    /// Precisa valer para prefabs porque metade da UI do jogo é instanciada em
    /// runtime: os candidatos da taverna, as cartas, os nós do mapa. Vestir só a
    /// cena deixava justamente essas telas brancas, e era o que acontecia.
    /// </summary>
    /// <returns>false quando o kit não está no projeto.</returns>
    public static bool AplicarKitEm(GameObject raiz)
    {
        Sprite painel = AssetDatabase.LoadAssetAtPath<Sprite>(PanelSpritePath);
        Sprite botao = AssetDatabase.LoadAssetAtPath<Sprite>(ButtonDefaultPath);
        Sprite marca = AssetDatabase.LoadAssetAtPath<Sprite>(CheckmarkSpritePath);

        if (painel == null || botao == null)
        {
            Debug.LogWarning("Kit visual: Bloodlines UI não encontrado — nada aplicado.");
            return false;
        }

        var estados = new SpriteState
        {
            highlightedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ButtonHoverPath),
            pressedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ButtonPressedPath),
            disabledSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ButtonDisabledPath)
        };

        foreach (var img in raiz.GetComponentsInChildren<Image>(true))
        {
            // A marca de seleção primeiro: ela é um caso à parte de um Toggle.
            if (img.gameObject.name == "Checkmark" && marca != null)
            {
                Undo.RecordObject(img, "Kit visual");
                img.sprite = marca;
                img.type = Image.Type.Simple;
                img.preserveAspect = true;
                img.color = CheckmarkTint;
                EditorUtility.SetDirty(img);
                continue;
            }

            if (!PrecisaDeSkin(img)) continue;

            var button = img.GetComponent<Button>();
            if (button != null)
            {
                Undo.RecordObject(img, "Kit visual");
                img.sprite = botao;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
                EditorUtility.SetDirty(img);

                Undo.RecordObject(button, "Kit visual");
                button.transition = Selectable.Transition.SpriteSwap;
                button.spriteState = estados;
                EditorUtility.SetDirty(button);
                continue;
            }

            // Painel de tela cheia ganha a pedra; caixa menor ganha moldura por
            // cima, para não perder o próprio fundo.
            if (IsFullScreenPanel(img.rectTransform))
            {
                Undo.RecordObject(img, "Kit visual");
                img.sprite = painel;
                img.type = Image.Type.Sliced;
                img.color = PanelTint;
                EditorUtility.SetDirty(img);
                continue;
            }

            // Caixa comum (nem botão, nem tela cheia) fica como está — de
            // propósito, e a tentativa de melhorar isso custou duas regressões
            // que valem ficar registradas:
            //
            // 1. Vestir com a pedra do kit apagou o texto. Em vários prefabs o
            //    fundo não é o pai dos textos, e sim um irmão desenhado DEPOIS
            //    deles: o sprite passa por cima do nome e dos atributos.
            // 2. Só pintar de BoxColor deixou texto escuro sobre fundo escuro,
            //    porque a cor do texto vem do prefab e não acompanha.
            //
            // As duas são piores que o branco embutido do Unity, que ao menos é
            // legível. Fazer isso direito é ajustar fundo E texto em cada prefab,
            // não uma regra genérica de tamanho.
        }

        return true;
    }

    /// <summary>
    /// Vale a pena vestir esta imagem?
    ///
    /// A checagem era só <c>sprite == null</c>, e por isso o kit não pegava quase
    /// nada: um Image criado por código nasce com o sprite embutido do Unity
    /// ("UISprite" / "Background"), que não é nulo. O resultado era uma tela de
    /// caixas brancas chapadas com o kit aplicado só a meia dúzia de objetos.
    ///
    /// Sprite escolhido de propósito — a arte de uma carta, uma barra de vida
    /// vestida pelo BarSkin — continua intocado, que é o que preserva o ajuste
    /// feito à mão entre duas montagens da cena.
    /// </summary>
    static bool PrecisaDeSkin(Image img)
    {
        // O fundo do bioma nasce sem sprite e do tamanho da tela: seria lido como
        // painel e receberia a pedra por cima, apagando a arte da região que só
        // é escolhida em runtime.
        if (img.gameObject.name == "BiomeBackground") return false;

        if (img.sprite == null) return true;

        string nome = img.sprite.name;
        return nome == "UISprite" || nome == "Background" || nome == "UIMask" || nome == "Knob";
    }

    /// <summary>Ocupa a tela quase inteira — é fundo, não widget.</summary>
    static bool IsFullScreenPanel(RectTransform rt)
    {
        return rt.rect.width > 1200f && rt.rect.height > 600f;
    }

    /// <summary>Moldura desenhada por cima da caixa, sem tocar no fundo dela.</summary>
    static void EnsureOutline(RectTransform host, Sprite moldura)
    {
        Transform found = host.Find("Moldura");
        GameObject go;

        if (found != null)
        {
            go = found.gameObject;
        }
        else
        {
            go = new GameObject("Moldura", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(go, "Kit visual");
            go.transform.SetParent(host, false);
            go.transform.SetAsFirstSibling();
        }

        var img = go.GetComponent<Image>();
        if (img == null) img = go.AddComponent<Image>();

        img.sprite = moldura;
        img.type = Image.Type.Sliced;
        img.color = OutlineTint;
        // A moldura é decoração: não pode roubar o clique de quem está embaixo.
        img.raycastTarget = false;

        ApplyRect(go.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }

    /// <summary>
    /// Títulos na fonte medieval do kit. Só os títulos: o corpo do texto continua
    /// numa fonte de leitura, e o fallback de emoji do projeto é global, então os
    /// ícones dos rótulos continuam aparecendo.
    /// </summary>
    static void ApplyTitleFont(Canvas canvas)
    {
        var fonte = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleFontPath);
        if (fonte == null) return;

        foreach (var text in canvas.GetComponentsInChildren<TMP_Text>(true))
        {
            string nome = text.gameObject.name;
            bool titulo = nome == "Txt_Title" || nome == "Txt_FormationTitle"
                       || nome == "Txt_Requirements" || nome == "Txt_QuestName"
                       || nome == "Txt_ProvisionsTitle" || nome == "Txt_Turn";

            if (!titulo || text.font == fonte) continue;

            Undo.RecordObject(text, "Kit visual");
            text.font = fonte;
            EditorUtility.SetDirty(text);
        }
    }

    #endregion

    #region Tema da tela de preparação

    /// <summary>
    /// A tela de preparação foi montada à mão antes de existir uma paleta: ficou
    /// num verde de placeholder com painéis brancos translúcidos por cima, num
    /// jogo que é escuro em todo o resto. Aqui ela recebe o tema das salas da
    /// guilda.
    ///
    /// Não redesenha o layout que já funciona — mexe em cor, corpo de fonte e nos
    /// rótulos que nunca chegam a ser preenchidos em tempo de execução. Quem
    /// escreve o resto continua sendo o QuestSelectionUI.
    /// </summary>
    static void StylePreparation()
    {
        QuestSelectionUI qs = Object.FindObjectOfType<QuestSelectionUI>(true);
        if (qs == null || qs.selectionRoot == null)
        {
            Debug.LogWarning("Montar Cena: sem QuestSelectionUI/selectionRoot — tema da preparação não aplicado.");
            return;
        }

        Transform root = qs.selectionRoot.transform;

        var rootImage = root.GetComponent<Image>();
        if (rootImage != null)
        {
            Undo.RecordObject(rootImage, "Tema da preparação");
            rootImage.color = PanelColor;
            EditorUtility.SetDirty(rootImage);
        }

        // O prefab primeiro: o Repaint alinha as instâncias ao que ele definir.
        StylePartyCardPrefab();
        StyleDeckCardPrefab();
        Repaint(root);
        FixScrollbars(root);
        FixHeroList(qs);

        // O rodapé da guilda fica visível por baixo desta tela e por baixo das
        // salas, e continuava claro: uma faixa cinza atravessando o jogo inteiro.
        Transform downBar = root.parent != null ? root.parent.Find("Panel_DownBar") : null;
        if (downBar != null) Repaint(downBar);

        // ── Passo 1: o mapa do mundo ───────────────────────────────────────
        //
        // O passo cabia em 948×599 no canto da tela, herdado de quando era uma
        // lista de três contratos: uma caixa de texto não precisa de mais que
        // isso. O mapa precisa — é nele que se lê o estado das sete regiões e se
        // escolhe para onde a guilda vai, e a 526px de largura cada símbolo de
        // terreno virava um carimbo com o nome maior que ele.
        //
        // Agora o passo ocupa a tela entre o cabeçalho e o rodapé da guilda.
        Reposition(root, "QuestListContainer",
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(24, 150), new Vector2(-24, -60));

        // O painel de detalhes encosta na direita e vira uma coluna: era ele que
        // dividia a largura com o mapa meio a meio.
        Reposition(root, "QuestListContainer/Panel_QuestDetails",
            new Vector2(0.68f, 0), new Vector2(1, 1), new Vector2(0, 76), new Vector2(0, 0));

        Restyle(root, "QuestListContainer/Panel_QuestDetails/QuestDetailsTxt", null, 20,
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(18, 18), new Vector2(-18, -18));
        Reposition(root, "QuestListContainer/Button_Next1",
            new Vector2(1, 0), new Vector2(1, 0), new Vector2(-220, 16), new Vector2(-20, 60));

        // ── Passo 2: escolha dos heróis ────────────────────────────────────
        // Txt_Requirements não é lido por nenhum campo do QuestSelectionUI: ficava
        // eternamente com o "New Text" do editor, em corpo 36, no meio da tela.
        // Vira o título do passo — o aviso de requisitos já sai no Txt_PartyCount.
        Restyle(root, "PartySelectionContainer/Panel_Requirements/Txt_Requirements",
            "⚔️ Quem vai à missão", 26,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(18, -56), new Vector2(-18, -14));
        Restyle(root, "PartySelectionContainer/Panel_Requirements/Txt_PartyCount", null, 20,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(18, -300), new Vector2(-18, -64));
        Reposition(root, "PartySelectionContainer/Button_Back2",
            new Vector2(1, 0), new Vector2(1, 0), new Vector2(-460, -88), new Vector2(-240, -24));
        Reposition(root, "PartySelectionContainer/Button_Next2",
            new Vector2(1, 0), new Vector2(1, 0), new Vector2(-220, -88), new Vector2(0, -24));

        // ── Passo 3: deck principal ────────────────────────────────────────
        // Os dois textos do topo cabiam em 200px de largura com corpo 36; o nome
        // do herói principal não entrava. Ficam acima da lista, lado a lado.
        Restyle(root, "DeckSelectionContainer/SelectedTxt", null, 22,
            new Vector2(0, 1), new Vector2(0.5f, 1), new Vector2(12, 10), new Vector2(-8, 52));
        Restyle(root, "DeckSelectionContainer/Txt_TeamSummary", null, 18,
            new Vector2(0.5f, 1), new Vector2(1, 1), new Vector2(8, 10), new Vector2(-12, 52));
        Reposition(root, "DeckSelectionContainer/Button_Back3",
            new Vector2(1, 0), new Vector2(1, 0), new Vector2(-460, -88), new Vector2(-240, -24));
        Reposition(root, "DeckSelectionContainer/JourneyBtn",
            new Vector2(1, 0), new Vector2(1, 0), new Vector2(-220, -88), new Vector2(0, -24));

        // Liga o resumo da formação, que até aqui era um rótulo morto na cena.
        var summary = Find<TMP_Text>(root, "DeckSelectionContainer/Txt_TeamSummary");
        if (summary != null && qs.teamSummaryText == null)
        {
            Undo.RecordObject(qs, "Montar Cena");
            qs.teamSummaryText = summary;
            EditorUtility.SetDirty(qs);
        }
    }

    /// <summary>
    /// Repinta o que veio claro do placeholder e deixa intacto o que já está na
    /// paleta escura — assim rodar de novo não desfaz nada e o que foi montado
    /// pelos outros passos (Panel_Formation, Panel_Provisions) sobrevive.
    /// </summary>
    static void Repaint(Transform root)
    {
        foreach (var img in root.GetComponentsInChildren<Image>(true))
        {
            if (img.transform == root) continue;

            string name = img.gameObject.name;

            // O Viewport precisa do Image para recortar, mas não para aparecer.
            if (name == "Viewport")
            {
                Undo.RecordObject(img, "Tema da preparação");
                var mask = img.GetComponent<Mask>();
                if (mask != null)
                {
                    Undo.RecordObject(mask, "Tema da preparação");
                    mask.showMaskGraphic = false;
                    img.color = BoxColor;
                    EditorUtility.SetDirty(mask);
                }
                else
                {
                    img.color = new Color(0f, 0f, 0f, 0f);
                }
                EditorUtility.SetDirty(img);
                continue;
            }

            // Quem vem de prefab tem a aparência definida lá. Alinhar em vez de
            // repintar evita que a cena guarde um override divergente do card que
            // o jogo instancia de verdade — foi assim que o ✓ do toggle acabou
            // escuro sobre fundo escuro.
            var source = PrefabUtility.GetCorrespondingObjectFromSource(img);
            if (source != null)
            {
                if (img.color != source.color)
                {
                    Undo.RecordObject(img, "Tema da preparação");
                    img.color = source.color;
                    EditorUtility.SetDirty(img);
                }
                continue;
            }

            if (!IsPale(img.color)) continue;

            Undo.RecordObject(img, "Tema da preparação");
            img.color = TargetColor(img);
            EditorUtility.SetDirty(img);
        }

        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            // Mesma regra das Images: o prefab manda na aparência de quem veio dele.
            var source = PrefabUtility.GetCorrespondingObjectFromSource(text);
            if (source != null)
            {
                if (text.color != source.color)
                {
                    Undo.RecordObject(text, "Tema da preparação");
                    text.color = source.color;
                    EditorUtility.SetDirty(text);
                }
                continue;
            }

            // Uma cor escolhida a dedo (avisos em vermelho, fileiras coloridas) é
            // informação, não estilo: só o branco e o preto crus são substituídos.
            if (!IsMonochrome(text.color)) continue;

            // Rótulo de botão puxa o dourado; o resto, o osso claro dos painéis.
            Undo.RecordObject(text, "Tema da preparação");
            text.color = IsButtonLabel(text) ? ButtonLabelColor : TextColor;
            EditorUtility.SetDirty(text);
        }
    }

    /// <summary>
    /// Rótulo de botão é o texto que fica dentro dele, não qualquer texto sob um
    /// ancestral clicável — o card de herói inteiro é um Button, e a regra larga
    /// pintava nome, classe e nível de dourado.
    /// </summary>
    static bool IsButtonLabel(TMP_Text text)
    {
        return text.transform.parent != null && text.transform.parent.GetComponent<Button>() != null;
    }

    /// <summary>
    /// As barras de rolagem da preparação foram montadas à mão e ficaram com
    /// geometria arbitrária — a do passo 3 tinha 33x1792 e cruzava a tela como uma
    /// faixa clara sobre os cards. Aqui elas viram uma coluna fina na borda direita.
    /// </summary>
    static void FixScrollbars(Transform root)
    {
        const float BarWidth = 14f;

        foreach (var bar in root.GetComponentsInChildren<Scrollbar>(true))
        {
            var rt = bar.transform as RectTransform;
            if (rt == null) continue;

            Undo.RecordObject(rt, "Tema da preparação");
            ApplyRect(rt, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-BarWidth, 0), Vector2.zero);
            EditorUtility.SetDirty(rt);

            Undo.RecordObject(bar, "Tema da preparação");
            bar.direction = Scrollbar.Direction.BottomToTop;
            EditorUtility.SetDirty(bar);

            // O punho precisa preencher a área deslizante, senão fica um traço solto.
            if (bar.handleRect != null)
            {
                Undo.RecordObject(bar.handleRect, "Tema da preparação");
                ApplyRect(bar.handleRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                EditorUtility.SetDirty(bar.handleRect);
            }
        }
    }

    /// <summary>
    /// A lista de heróis do passo 2 usa GridLayoutGroup centralizado dentro de um
    /// Content de altura fixa: com quatro heróis as células já não cabiam e
    /// transbordavam para os dois lados, cortando o primeiro card pelo topo. Com o
    /// alinhamento no topo e um fitter, a lista cresce para baixo e rola.
    /// </summary>
    static void FixHeroList(QuestSelectionUI qs)
    {
        var content = qs.partySelectionContainer as RectTransform;
        if (content == null) return;

        var grid = content.GetComponent<GridLayoutGroup>();
        if (grid != null)
        {
            Undo.RecordObject(grid, "Tema da preparação");
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            EditorUtility.SetDirty(grid);
        }

        var fitter = content.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = Undo.AddComponent<ContentSizeFitter>(content.gameObject);

        Undo.RecordObject(fitter, "Tema da preparação");
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        EditorUtility.SetDirty(fitter);

        Undo.RecordObject(content, "Tema da preparação");
        content.pivot = new Vector2(0.5f, 1f);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.offsetMin = new Vector2(0, content.offsetMin.y);
        content.offsetMax = new Vector2(0, 0);
        EditorUtility.SetDirty(content);
    }

    /// <summary>
    /// O card de deck do passo 3 tem dois filhos chamados "Name": um dentro do
    /// retrato e outro no corpo do card. O QuestSelectionUI acha o primeiro pela
    /// busca por nome, preenche aquele, e o de baixo — o grande — ficava anunciando
    /// "New Text" no meio da tela. Renomear o de dentro do retrato desfaz o empate.
    /// </summary>
    static void StyleDeckCardPrefab()
    {
        const string path = "Assets/Prefabs/UI/Deck Card Prefab.prefab";

        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
        {
            Debug.LogWarning("Montar Cena: Deck Card Prefab não encontrado — card de deck não restilizado.");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(path);

        try
        {
            var portrait = root.transform.Find("Portrait");
            if (portrait != null)
            {
                var duplicado = portrait.Find("Name");
                if (duplicado != null) duplicado.gameObject.name = "PortraitName";
            }

            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (IsMonochrome(text.color))
                    text.color = IsButtonLabel(text) ? ButtonLabelColor : TextColor;

                switch (text.gameObject.name)
                {
                    case "Name": text.text = "Nome do herói"; break;
                    case "Class": text.text = "Classe"; break;
                    case "Level": text.text = "Nv.1"; break;
                    case "CardCount": text.text = "0 cartas"; break;
                    case "PortraitName": text.text = ""; break;
                    case "HP": text.text = ""; text.color = SubtleTextColor; break;
                }
            }

            // O vermelho puro da borda de seleção pintava o card inteiro; vira o
            // dourado que o resto do jogo usa para "escolhido".
            var border = root.transform.Find("SelectedBorder");
            if (border != null)
            {
                var img = border.GetComponent<Image>();
                if (img != null) img.color = new Color(0.83f, 0.69f, 0.22f);
            }

            // O corpo do card era um quase-preto avermelhado que destoava do
            // marrom de todo o resto.
            var corpo = root.transform.Find("Image");
            if (corpo != null)
            {
                var img = corpo.GetComponent<Image>();
                if (img != null) img.color = BoxColor;
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// O card de herói do passo 2 nasce do prefab, não da cena: repintar só o
    /// exemplar que está na hierarquia deixava os cards de verdade brancos.
    /// </summary>
    static void StylePartyCardPrefab()
    {
        const string path = "Assets/Prefabs/UI/PartyMemberSelectPrefab.prefab";

        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
        {
            Debug.LogWarning("Montar Cena: PartyMemberSelectPrefab não encontrado — card de herói não restilizado.");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(path);

        try
        {
            foreach (var img in root.GetComponentsInChildren<Image>(true))
                if (IsPale(img.color)) img.color = TargetColor(img);

            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                // Textos de exemplo, para quem abrir o prefab no Editor entender o
                // que cada campo é. Em execução todos são sobrescritos.
                // A hierarquia é dada pela cor: o nome sobressai, classe e nível
                // recuam.
                switch (text.gameObject.name)
                {
                    case "Name": text.text = "Nome do herói"; text.color = TextColor; break;
                    case "Class": text.text = "Classe"; text.color = SubtleTextColor; break;
                    case "Level": text.text = "Nv.1"; text.color = SubtleTextColor; break;
                    case "MainIndicator": text.text = ""; break;
                    default:
                        if (IsMonochrome(text.color))
                            text.color = IsButtonLabel(text) ? ButtonLabelColor : TextColor;
                        break;
                }
            }

            var mainButton = root.transform.Find("MainButton");
            if (mainButton != null)
            {
                var label = mainButton.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    label.text = "USAR DECK";
                    // Em corpo 24 o rótulo quebrava em duas linhas e estourava o botão.
                    label.fontSize = 16;
                    label.enableAutoSizing = false;
                    label.enableWordWrapping = false;
                    label.alignment = TextAlignmentOptions.Center;
                }
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// Cor que um elemento claro do placeholder assume no tema. Vale tanto para a
    /// cena quanto para os prefabs instanciados em tempo de execução, e por isso
    /// mora num lugar só: os dois precisam terminar idênticos, senão o card na
    /// cena e o card instanciado ficam de cores diferentes.
    /// </summary>
    static Color TargetColor(Image img)
    {
        string name = img.gameObject.name;

        // O ✓ é o único elemento que precisa saltar do fundo em vez de recuar.
        if (name == "Checkmark") return ButtonLabelColor;
        if (img.GetComponent<Button>() != null) return ButtonColor;
        if (img.GetComponentInParent<Toggle>() != null) return ToggleBoxColor;
        if (name.StartsWith("Handle")) return HandleColor;
        if (name.StartsWith("Scrollbar")) return TrackColor;

        return BoxColor;
    }

    /// <summary>Cor clara o bastante para ser resto do placeholder.</summary>
    static bool IsPale(Color c)
    {
        return c.a > 0.02f && (c.r + c.g + c.b) / 3f > 0.45f;
    }

    /// <summary>Branco ou preto crus, sem matiz — cor de objeto recém-criado.</summary>
    static bool IsMonochrome(Color c)
    {
        float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
        float min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
        return max - min < 0.02f;
    }

    static T Find<T>(Transform root, string path) where T : Component
    {
        Transform t = root.Find(path);
        return t != null ? t.GetComponent<T>() : null;
    }

    /// <summary>Reposiciona um objeto que já existe na cena, mexendo no texto só se pedido.</summary>
    static void Restyle(Transform root, string path, string content, int size,
                        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var text = Find<TMP_Text>(root, path);
        if (text == null)
        {
            Debug.LogWarning($"Montar Cena: '{path}' não encontrado — tema não aplicado nele.");
            return;
        }

        Undo.RecordObject(text, "Tema da preparação");
        Undo.RecordObject(text.rectTransform, "Tema da preparação");

        if (content != null) text.text = content;
        text.fontSize = size;
        text.enableAutoSizing = false;
        ApplyRect(text.rectTransform, anchorMin, anchorMax, offsetMin, offsetMax);

        EditorUtility.SetDirty(text);
    }

    static void Reposition(Transform root, string path,
                           Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        Transform t = root.Find(path);
        if (t == null)
        {
            Debug.LogWarning($"Montar Cena: '{path}' não encontrado — não reposicionado.");
            return;
        }

        var rt = t as RectTransform;
        if (rt == null) return;

        Undo.RecordObject(rt, "Tema da preparação");
        ApplyRect(rt, anchorMin, anchorMax, offsetMin, offsetMax);
        EditorUtility.SetDirty(rt);
    }

    #endregion

    #region Carta

    /// <summary>
    /// A carta tinha 90px de altura para a descrição e corpo 15 — ilegível no
    /// leque, e ainda pior desde que a formação passou a acrescentar uma linha de
    /// aviso. A ilustração, que hoje é um retângulo branco vazio, cede espaço:
    /// enquanto não houver arte, o texto vale mais que a moldura.
    /// </summary>
    static void StyleCardPrefab()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPath);
        if (prefab == null)
        {
            Debug.LogWarning("Montar Cena: CardPrefab não encontrado — carta não restilizada.");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(CardPath);

        try
        {
            // Medidas a partir da base da carta (245x345):
            //   nome          300–333
            //   ilustração    152–294
            //   descrição      12–146
            var name = root.transform.Find("CardName") as RectTransform;
            var desc = root.transform.Find("CardDescription") as RectTransform;
            var cost = root.transform.Find("CostTxt") as RectTransform;

            // As molduras são Images anônimas ("Image"), identificadas pela ordem
            // em que estão na hierarquia — é como o prefab foi montado.
            RectTransform nameBox = ChildRect(root, 2);
            RectTransform descBox = ChildRect(root, 4);
            RectTransform artBox = ChildRect(root, 6);
            RectTransform costBox = ChildRect(root, 7);

            // No leque, cada carta cobre a faixa direita da anterior. Nome e
            // descrição param antes dessa faixa: texto que só aparece ao passar o
            // mouse não serve para escolher a carta.
            const float Coberto = 40f;

            SetRect(nameBox, new Vector2(0, 0), new Vector2(1, 1), new Vector2(12, 300), new Vector2(-12, -12));
            // O nome fica na faixa do topo, que a carta vizinha cobre bem menos
            // que o corpo — com a margem cheia, "Postura Defensiva" perdia o fim.
            SetRect(name, new Vector2(0, 0), new Vector2(1, 1), new Vector2(18, 302), new Vector2(-(18 + Coberto * 0.4f), -14));
            SetRect(artBox, new Vector2(0, 0), new Vector2(1, 1), new Vector2(12, 152), new Vector2(-12, -51));
            SetRect(descBox, new Vector2(0, 0), new Vector2(1, 1), new Vector2(12, 12), new Vector2(-12, -199));
            SetRect(desc, new Vector2(0, 0), new Vector2(1, 1), new Vector2(18, 16), new Vector2(-(18 + Coberto), -203));
            SetRect(costBox, new Vector2(0, 0), new Vector2(0, 0), new Vector2(14, 156), new Vector2(80, 200));
            SetRect(cost, new Vector2(0, 0), new Vector2(0, 0), new Vector2(14, 156), new Vector2(80, 200));

            StyleCardText(name, 22, TextAlignmentOptions.MidlineLeft, TextColor);
            StyleCardText(desc, 18, TextAlignmentOptions.TopLeft, TextColor);
            StyleCardText(cost, 22, TextAlignmentOptions.Center, ButtonLabelColor);

            // O branco puro do placeholder destoava de tudo; a carta vira
            // pergaminho velho, e as molduras, madeira queimada.
            SetImageColor(root.transform.Find("Border"), new Color(0.05f, 0.04f, 0.05f));
            SetImageColor(root.transform.Find("Background"), new Color(0.16f, 0.14f, 0.13f));
            SetImageColor(nameBox, new Color(0.24f, 0.19f, 0.15f));
            SetImageColor(descBox, new Color(0.10f, 0.09f, 0.10f));
            SetImageColor(artBox, new Color(0.21f, 0.19f, 0.18f));
            SetImageColor(costBox, new Color(0.24f, 0.19f, 0.15f));

            PrefabUtility.SaveAsPrefabAsset(root, CardPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static RectTransform ChildRect(GameObject root, int index)
    {
        return index < root.transform.childCount ? root.transform.GetChild(index) as RectTransform : null;
    }

    static void SetRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        if (rt == null) return;
        ApplyRect(rt, anchorMin, anchorMax, offsetMin, offsetMax);
    }

    static void StyleCardText(RectTransform rt, int size, TextAlignmentOptions align, Color color)
    {
        if (rt == null) return;
        var text = rt.GetComponent<TMP_Text>();
        if (text == null) return;

        text.fontSize = size;
        text.enableAutoSizing = false;
        text.alignment = align;
        text.color = color;
        text.overflowMode = TextOverflowModes.Truncate;
    }

    static void SetImageColor(Transform t, Color color)
    {
        if (t == null) return;
        var img = t.GetComponent<Image>();
        if (img != null) img.color = color;
    }

    #endregion

    #region Helpers de UI

    #region Balanço da jornada

    /// <summary>
    /// A tela de volta para casa. Substitui o popup de texto corrido em que
    /// sobreviventes, perdas, ouro e promoções vinham numa string só.
    ///
    /// A linha de herói é um molde inativo dentro do próprio painel, em vez de um
    /// prefab em disco: o setup não precisa gravar asset novo, e o molde
    /// acompanha o painel se alguém mover a hierarquia.
    /// </summary>
    /// <summary>
    /// A tela de fim de run — vitória ou queda da guilda.
    ///
    /// Não existia porque o jogo não tinha fim: era um sandbox infinito. É a tela
    /// em que a run vira história, e a única que mostra o que atravessa para a
    /// próxima tentativa.
    /// </summary>
    static GameObject BuildRunEnd(Canvas canvas)
    {
        GameObject panel = FindOrCreatePanel(canvas, "Panel_RunEnd");

        var titulo = EnsureText(panel.transform, "Txt_Title", "", 46,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(40, -220), new Vector2(-40, -140));
        titulo.alignment = TextAlignmentOptions.Center;

        var motivo = EnsureText(panel.transform, "Txt_Reason", "", 24,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(120, -320), new Vector2(-120, -235));
        motivo.alignment = TextAlignmentOptions.Center;
        motivo.color = SubtleTextColor;

        var stats = EnsureText(panel.transform, "Txt_Stats", "", 26,
            new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(80, -40), new Vector2(-80, 90));
        stats.alignment = TextAlignmentOptions.Center;

        var meta = EnsureText(panel.transform, "Txt_Meta", "", 24,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(80, 260), new Vector2(-80, 340));
        meta.alignment = TextAlignmentOptions.Center;

        var novaRun = EnsureButton(panel.transform, "Btn_NewRun", "Fundar uma nova guilda",
            new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-200, 140), new Vector2(200, 210));

        var ui = panel.GetComponent<RunEndUI>();
        if (ui == null) ui = Undo.AddComponent<RunEndUI>(panel);

        Undo.RecordObject(ui, "Montar Cena");
        ui.panel = panel;
        ui.titleText = titulo;
        ui.reasonText = motivo;
        ui.statsText = stats;
        ui.metaText = meta;
        ui.newRunButton = novaRun;
        EditorUtility.SetDirty(ui);

        panel.SetActive(false);
        return panel;
    }

    static GameObject BuildJourneyResult(Canvas canvas)
    {
        GameObject panel = FindOrCreatePanel(canvas, "Panel_JourneyResult");

        var titulo = EnsureText(panel.transform, "Txt_Title", "", 40,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(40, -110), new Vector2(-40, -40));
        titulo.alignment = TextAlignmentOptions.Center;

        var subtitulo = EnsureText(panel.transform, "Txt_Subtitle", "", 22,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(40, -152), new Vector2(-40, -112));
        subtitulo.alignment = TextAlignmentOptions.Center;
        subtitulo.color = SubtleTextColor;

        // Coluna dos heróis: é o corpo da tela, então fica com a maior faixa.
        var herois = EnsureColumn(panel.transform, "HeroList",
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(60, 400), new Vector2(-60, -170), 10);

        var layout = herois.GetComponent<VerticalLayoutGroup>();
        if (layout != null)
        {
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
        }

        var recompensa = EnsureText(panel.transform, "Txt_Reward", "", 20,
            new Vector2(0, 0), new Vector2(0.42f, 0), new Vector2(60, 110), new Vector2(0, 390));
        recompensa.alignment = TextAlignmentOptions.TopLeft;

        // Escolha do despojo: coluna à direita, ao lado do que a missão pagou —
        // é a comparação que dá sentido à decisão.
        var convite = EnsureText(panel.transform, "Txt_RewardPrompt", "", 20,
            new Vector2(0.45f, 0), new Vector2(1, 0), new Vector2(0, 350), new Vector2(-60, 390));
        convite.color = SubtleTextColor;

        var escolhas = EnsureColumn(panel.transform, "RewardChoices",
            new Vector2(0.45f, 0), new Vector2(1, 0), new Vector2(0, 110), new Vector2(-60, 344), 8);

        var escolhasLayout = escolhas.GetComponent<VerticalLayoutGroup>();
        if (escolhasLayout != null)
        {
            escolhasLayout.childControlHeight = false;
            escolhasLayout.childForceExpandHeight = false;
            escolhasLayout.childControlWidth = true;
            escolhasLayout.childForceExpandWidth = true;
        }

        var continuar = EnsureButton(panel.transform, "Btn_Continue", "Voltar à guilda",
            new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-140, 40), new Vector2(140, 92));

        GameObject molde = EnsureHeroLineTemplate(panel.transform);
        GameObject moldeRecompensa = EnsureRewardTemplate(panel.transform);

        JourneyResultUI ui = panel.GetComponent<JourneyResultUI>();
        if (ui == null) ui = panel.AddComponent<JourneyResultUI>();

        Undo.RecordObject(ui, "Montar Cena");
        ui.panel = panel;
        ui.titleText = titulo;
        ui.subtitleText = subtitulo;
        ui.heroContainer = herois.transform;
        ui.heroLinePrefab = molde;
        ui.rewardText = recompensa;
        ui.rewardPromptText = convite;
        ui.rewardContainer = escolhas.transform;
        ui.rewardButtonPrefab = moldeRecompensa;
        ui.continueButton = continuar;
        EditorUtility.SetDirty(ui);

        panel.SetActive(false);
        return panel;
    }

    /// <summary>Molde de uma linha de herói: nome, estado, XP e barra.</summary>
    static GameObject EnsureHeroLineTemplate(Transform parent)
    {
        Transform existente = parent.Find("HeroLineTemplate");
        if (existente != null) return existente.gameObject;

        var go = new GameObject("HeroLineTemplate", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Undo.RegisterCreatedObjectUndo(go, "Criar molde de linha");
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 92);
        go.GetComponent<Image>().color = BoxColor;

        var elemento = go.AddComponent<LayoutElement>();
        elemento.minHeight = 92;
        elemento.preferredHeight = 92;

        EnsureText(go.transform, "Name", "", 24,
            new Vector2(0, 1), new Vector2(0.55f, 1), new Vector2(16, -40), new Vector2(0, -8));

        var estado = EnsureText(go.transform, "Status", "", 18,
            new Vector2(0, 0), new Vector2(0.55f, 0), new Vector2(16, 10), new Vector2(0, 44));
        estado.color = SubtleTextColor;

        var xp = EnsureText(go.transform, "Xp", "", 18,
            new Vector2(0.55f, 1), new Vector2(1, 1), new Vector2(0, -40), new Vector2(-16, -8));
        xp.alignment = TextAlignmentOptions.Right;

        // Barra de XP: trilho escuro com preenchimento por cima.
        var trilho = new GameObject("XpBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        trilho.transform.SetParent(go.transform, false);
        ApplyRect(trilho.GetComponent<RectTransform>(),
            new Vector2(0.55f, 0), new Vector2(1, 0), new Vector2(0, 18), new Vector2(-16, 34));
        trilho.GetComponent<Image>().color = TrackColor;

        var preenchimento = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        preenchimento.transform.SetParent(trilho.transform, false);
        ApplyRect(preenchimento.GetComponent<RectTransform>(),
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var img = preenchimento.GetComponent<Image>();
        img.color = new Color(0.62f, 0.55f, 0.30f);
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillAmount = 0f;

        // O molde não aparece: ele só é clonado.
        go.SetActive(false);
        return go;
    }

    /// <summary>Molde de uma opção de despojo: título em cima, descrição embaixo.</summary>
    static GameObject EnsureRewardTemplate(Transform parent)
    {
        Transform existente = parent.Find("RewardTemplate");
        if (existente != null) return existente.gameObject;

        var go = new GameObject("RewardTemplate", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Undo.RegisterCreatedObjectUndo(go, "Criar molde de recompensa");
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 72);
        go.GetComponent<Image>().color = ButtonColor;

        var elemento = go.AddComponent<LayoutElement>();
        elemento.minHeight = 72;
        elemento.preferredHeight = 72;

        var botao = go.AddComponent<Button>();
        botao.targetGraphic = go.GetComponent<Image>();

        var titulo = EnsureText(go.transform, "Title", "", 20,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -38), new Vector2(-16, -8));
        titulo.color = ButtonLabelColor;

        var descricao = EnsureText(go.transform, "Desc", "", 15,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(16, 10), new Vector2(-16, 38));
        descricao.color = SubtleTextColor;

        go.SetActive(false);
        return go;
    }

    #endregion

    internal static GameObject FindOrCreatePanel(Canvas canvas, string name)
    {
        Transform existing = canvas.transform.Find(name);
        if (existing != null)
        {
            // O painel da cena guarda o alfa com que nasceu, e mudar a constante
            // acima não o alcança. Só o alfa é reposto: a cor e o sprite podem
            // ter sido escolhidos pela sala (a caverna da Forja, o papel da Sala
            // de Mapas) e não são deste método.
            var fundoExistente = existing.GetComponent<Image>();
            if (fundoExistente != null && fundoExistente.color.a < 1f)
            {
                Undo.RecordObject(fundoExistente, "Opacificar painel");
                Color c = fundoExistente.color;
                fundoExistente.color = new Color(c.r, c.g, c.b, 1f);
                EditorUtility.SetDirty(fundoExistente);
            }

            return existing.gameObject;
        }

        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Undo.RegisterCreatedObjectUndo(go, "Criar painel");
        go.transform.SetParent(canvas.transform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        go.GetComponent<Image>().color = PanelColor;
        return go;
    }

    internal static TMP_Text EnsureText(Transform parent, string name, string content, int size,
                               Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        Transform found = parent.Find(name);
        TextMeshProUGUI text;

        if (found != null)
        {
            text = found.GetComponent<TextMeshProUGUI>();
            if (text != null)
            {
                // Reposiciona também quando já existe. Antes o método devolvia o
                // texto sem tocar no rect, então qualquer ajuste de layout era
                // silenciosamente ignorado numa cena já montada — foi assim que o
                // título do evento ficou preso dentro da faixa do mapa.
                Undo.RecordObject(text.rectTransform, "Reposicionar texto");
                ApplyRect(text.rectTransform, anchorMin, anchorMax, offsetMin, offsetMax);
                EditorUtility.SetDirty(text);
                return text;
            }
            Object.DestroyImmediate(found.gameObject);
        }

        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Criar texto");
        go.transform.SetParent(parent, false);

        text = go.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = size;
        text.enableAutoSizing = false;
        text.color = TextColor;
        text.raycastTarget = false;

        ApplyRect(go.GetComponent<RectTransform>(), anchorMin, anchorMax, offsetMin, offsetMax);
        return text;
    }

    internal static Button EnsureButton(Transform parent, string name, string label,
                               Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        Transform found = parent.Find(name);
        if (found != null)
        {
            Button existing = found.GetComponent<Button>();
            if (existing != null)
            {
                // Mesma razão do EnsureText: sem reposicionar, mudanças de
                // layout não chegam a uma cena que já foi montada antes.
                Undo.RecordObject(existing.transform as RectTransform, "Reposicionar botão");
                ApplyRect(existing.transform as RectTransform, anchorMin, anchorMax, offsetMin, offsetMax);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            Object.DestroyImmediate(found.gameObject);
        }

        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        Undo.RegisterCreatedObjectUndo(go, "Criar botão");
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = ButtonColor;

        ApplyRect(go.GetComponent<RectTransform>(), anchorMin, anchorMax, offsetMin, offsetMax);

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        var text = textGo.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 20;
        text.alignment = TextAlignmentOptions.Center;
        text.color = ButtonLabelColor;
        text.raycastTarget = false;
        ApplyRect(textGo.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        return go.GetComponent<Button>();
    }

    internal static GameObject EnsureRow(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
                                Vector2 offsetMin, Vector2 offsetMax, int spacing)
    {
        return EnsureLayout<HorizontalLayoutGroup>(parent, name, anchorMin, anchorMax, offsetMin, offsetMax, spacing);
    }

    internal static GameObject EnsureColumn(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
                                   Vector2 offsetMin, Vector2 offsetMax, int spacing)
    {
        return EnsureLayout<VerticalLayoutGroup>(parent, name, anchorMin, anchorMax, offsetMin, offsetMax, spacing);
    }

    /// <summary>
    /// Coluna que rola quando o conteúdo passa da área visível. As salas cabiam
    /// na tela com o roster de hoje (5 itens, 8 heróis, 8 tumbas), mas qualquer
    /// guilda maior transbordava sem aviso nenhum — a lista simplesmente saía
    /// pela borda.
    ///
    /// A lista mantém o nome de sempre e apenas muda de pai, então as referências
    /// já ligadas nos managers continuam válidas.
    /// </summary>
    internal static GameObject EnsureScrollColumn(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
                                         Vector2 offsetMin, Vector2 offsetMax, int spacing)
    {
        const float BarWidth = 14f;

        GameObject area = EnsureFreeArea(parent, name + "_Scroll", anchorMin, anchorMax, offsetMin, offsetMax);

        var scroll = area.GetComponent<ScrollRect>();
        if (scroll == null) scroll = Undo.AddComponent<ScrollRect>(area);

        GameObject viewport = EnsureFreeArea(area.transform, "Viewport",
            new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, new Vector2(-(BarWidth + 4f), 0));

        // Mask (stencil) e não RectMask2D (_ClipRect): com RectMask2D todo o texto
        // das salas desaparecia — as caixas ficavam, os rótulos sumiam. É o mesmo
        // recorte que os Scroll Views antigos da preparação já usam sem problema.
        var mask = viewport.GetComponent<Mask>();
        if (mask == null) mask = Undo.AddComponent<Mask>(viewport);
        mask.showMaskGraphic = false;

        // O Mask exige um gráfico para recortar, mesmo sem desenhá-lo.
        var maskImage = viewport.GetComponent<Image>();
        if (maskImage == null) maskImage = Undo.AddComponent<Image>(viewport);
        maskImage.color = BoxColor;

        var legacyMask = viewport.GetComponent<RectMask2D>();
        if (legacyMask != null) Undo.DestroyObjectImmediate(legacyMask);

        // Migração das cenas montadas antes de existir rolagem: a lista era filha
        // direta do painel.
        Transform legacy = parent.Find(name);
        if (legacy != null && legacy.parent != viewport.transform)
            Undo.SetTransformParent(legacy, viewport.transform, "Mover lista para o scroll");

        GameObject list = EnsureColumn(viewport.transform, name,
            new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero, spacing);

        // Ancorada no topo e crescendo para baixo: é o que o ScrollRect espera de
        // um conteúdo de altura variável.
        var listRect = list.GetComponent<RectTransform>();
        Undo.RecordObject(listRect, "Montar Cena");
        listRect.pivot = new Vector2(0.5f, 1f);
        listRect.anchorMin = new Vector2(0, 1);
        listRect.anchorMax = new Vector2(1, 1);
        listRect.offsetMin = new Vector2(0, -100);
        listRect.offsetMax = Vector2.zero;

        var fitter = list.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = Undo.AddComponent<ContentSizeFitter>(list);
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Scrollbar bar = EnsureScrollbar(area.transform, BarWidth);

        Undo.RecordObject(scroll, "Montar Cena");
        scroll.content = listRect;
        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 32f;
        scroll.verticalScrollbar = bar;
        // Some sozinha quando tudo cabe, para não sugerir conteúdo que não existe.
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        EditorUtility.SetDirty(scroll);

        return list;
    }

    static Scrollbar EnsureScrollbar(Transform parent, float width)
    {
        GameObject bar = EnsureFreeArea(parent, "Scrollbar",
            new Vector2(1, 0), new Vector2(1, 1), new Vector2(-width, 0), Vector2.zero);

        var track = bar.GetComponent<Image>();
        if (track == null) track = Undo.AddComponent<Image>(bar);
        track.color = TrackColor;

        GameObject slidingArea = EnsureFreeArea(bar.transform, "Sliding Area",
            new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);

        GameObject handle = EnsureFreeArea(slidingArea.transform, "Handle",
            new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);

        var handleImage = handle.GetComponent<Image>();
        if (handleImage == null) handleImage = Undo.AddComponent<Image>(handle);
        handleImage.color = HandleColor;

        var scrollbar = bar.GetComponent<Scrollbar>();
        if (scrollbar == null) scrollbar = Undo.AddComponent<Scrollbar>(bar);

        Undo.RecordObject(scrollbar, "Montar Cena");
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handle.GetComponent<RectTransform>();
        scrollbar.targetGraphic = handleImage;
        EditorUtility.SetDirty(scrollbar);

        return scrollbar;
    }

    static GameObject EnsureLayout<T>(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
                                      Vector2 offsetMin, Vector2 offsetMax, int spacing)
        where T : HorizontalOrVerticalLayoutGroup
    {
        Transform found = parent.Find(name);
        GameObject go;

        if (found != null)
        {
            go = found.gameObject;
        }
        else
        {
            go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Criar container");
            go.transform.SetParent(parent, false);
        }

        var layout = go.GetComponent<T>();
        if (layout == null) layout = go.AddComponent<T>();

        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = false;
        layout.childControlHeight = false;

        ApplyRect(go.GetComponent<RectTransform>(), anchorMin, anchorMax, offsetMin, offsetMax);
        return go;
    }

    /// <summary>
    /// Container sem layout automático, para quem posiciona os filhos por conta
    /// própria. Remove um LayoutGroup preexistente — cenas montadas por versões
    /// anteriores tinham um HorizontalLayoutGroup aqui.
    /// </summary>
    internal static GameObject EnsureFreeArea(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
                                     Vector2 offsetMin, Vector2 offsetMax)
    {
        Transform found = parent.Find(name);
        GameObject go;

        if (found != null)
        {
            go = found.gameObject;
        }
        else
        {
            go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Criar container");
            go.transform.SetParent(parent, false);
        }

        var legacy = go.GetComponent<LayoutGroup>();
        if (legacy != null)
            Undo.DestroyObjectImmediate(legacy);

        ApplyRect(go.GetComponent<RectTransform>(), anchorMin, anchorMax, offsetMin, offsetMax);
        return go;
    }

    internal static void ApplyRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }

    #endregion
}
#endif
