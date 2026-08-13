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

    static readonly Color PanelColor = new Color(0.08f, 0.07f, 0.09f, 0.98f);
    static readonly Color BoxColor = new Color(0.13f, 0.12f, 0.14f, 1f);
    static readonly Color ButtonColor = new Color(0.20f, 0.17f, 0.16f);
    static readonly Color ButtonLabelColor = new Color(0.94f, 0.88f, 0.72f);
    static readonly Color TextColor = new Color(0.92f, 0.90f, 0.85f);
    static readonly Color TrackColor = new Color(0.10f, 0.09f, 0.11f);
    static readonly Color HandleColor = new Color(0.30f, 0.27f, 0.24f);
    static readonly Color ToggleBoxColor = new Color(0.24f, 0.21f, 0.19f);
    static readonly Color SubtleTextColor = new Color(0.66f, 0.63f, 0.58f);

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
        GameObject combatPanel = BuildCombat(canvas, enemyCard, partyStatus, cardPrefab);
        GameObject mapRoomPanel = BuildMapRoom(canvas);
        GameObject marketPanel = BuildMarket(canvas);
        GameObject cemeteryPanel = BuildCemetery(canvas);
        GameObject forgePanel = BuildForge(canvas);
        BuildTavern(canvas);
        BuildProvisions();
        BuildFormation();
        StylePreparation();
        StyleCardPrefab();
        ApplyKit(canvas);
        LiftPopups(canvas);

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

            // O rodapé da guilda não faz sentido durante o combate e ficava
            // sobreposto às cartas.
            var downBar = canvas.transform.Find("Background/Panel_DownBar");
            if (downBar != null && (ui.hideDuringCombat == null || ui.hideDuringCombat.Length == 0))
                ui.hideDuringCombat = new[] { downBar.gameObject };

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

        // Cabeçalho
        var questName = EnsureText(panel.transform, "Txt_QuestName", "Missão", 30,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -60), new Vector2(-20, -14));
        var day = EnsureText(panel.transform, "Txt_Day", "Dia 0 / 0", 22,
            new Vector2(0, 1), new Vector2(0.5f, 1), new Vector2(20, -96), new Vector2(0, -62));
        var biome = EnsureText(panel.transform, "Txt_Biome", "", 22,
            new Vector2(0.5f, 1), new Vector2(1, 1), new Vector2(0, -96), new Vector2(-20, -62));

        // ── Composição do painel ───────────────────────────────────────────
        // Faixas exclusivas, medidas a partir da base da tela (1080 de altura).
        // A região central é dividida em duas colunas porque empilhar tudo não
        // cabia: três escolhas somam 222px e cada card de herói tem 165px.
        //
        //   topo   mapa                 800–980
        //          título do evento     750–795
        //          ┌ escolhas (esq.)    515–740
        //          └ narrativa (dir.)   515–740
        //          status da party      340–505
        //          mão de cartas         90–330
        //          recursos              50–82
        //   base   botões                 8–44

        // Mapa: área livre, pois o JourneyMapUI posiciona nós e arestas por
        // coordenada — um LayoutGroup sobrescreveria tudo.
        var mapRow = EnsureFreeArea(panel.transform, "MapNodes", new Vector2(0, 1), new Vector2(1, 1),
            new Vector2(20, -280), new Vector2(-20, -100));

        var evTitle = EnsureText(panel.transform, "Txt_EventTitle", "Evento", 26,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -330), new Vector2(-20, -285));

        // Coluna esquerda: as decisões.
        var choices = EnsureColumn(panel.transform, "ChoiceContainer",
            new Vector2(0, 0), new Vector2(0.55f, 0), new Vector2(20, 515), new Vector2(0, 740), 8);

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

        // Coluna direita: narrativa e informação, empilhadas.
        var evDesc = EnsureText(panel.transform, "Txt_EventDescription", "", 19,
            new Vector2(0.57f, 0), new Vector2(1, 0), new Vector2(0, 640), new Vector2(-20, 740));
        var log = EnsureText(panel.transform, "Txt_ResolutionLog", "", 17,
            new Vector2(0.57f, 0), new Vector2(1, 0), new Vector2(0, 555), new Vector2(-20, 635));
        var upcoming = EnsureText(panel.transform, "Txt_Upcoming", "", 16,
            new Vector2(0.57f, 0), new Vector2(1, 0), new Vector2(0, 515), new Vector2(-20, 550));

        // Status da party: faixa alta o bastante para o card de 165px.
        var partyRow = EnsureRow(panel.transform, "PartyStatus", new Vector2(0, 0), new Vector2(1, 0),
            new Vector2(20, 340), new Vector2(-20, 505), 10);

        // Mão de cartas: área livre (o leque posiciona sozinho).
        var hand = EnsureFreeArea(panel.transform, "HandContainer", new Vector2(0, 0), new Vector2(1, 0),
            new Vector2(20, 90), new Vector2(-20, 330));

        // Recursos e contadores
        var rations = EnsureText(panel.transform, "Txt_Rations", "", 20, new Vector2(0, 0), new Vector2(0.2f, 0), new Vector2(20, 50), new Vector2(0, 82));
        var torches = EnsureText(panel.transform, "Txt_Torches", "", 20, new Vector2(0.2f, 0), new Vector2(0.4f, 0), new Vector2(0, 50), new Vector2(0, 82));
        var energy = EnsureText(panel.transform, "Txt_Energy", "", 20, new Vector2(0.4f, 0), new Vector2(0.6f, 0), new Vector2(0, 50), new Vector2(0, 82));
        var detourCount = EnsureText(panel.transform, "Txt_Detours", "", 20, new Vector2(0.6f, 0), new Vector2(0.75f, 0), new Vector2(0, 50), new Vector2(0, 82));

        // Botões
        var abort = EnsureButton(panel.transform, "Btn_Abort", "Abandonar",
            new Vector2(0, 0), new Vector2(0, 0), new Vector2(20, 8), new Vector2(200, 36));
        var endTurn = EnsureButton(panel.transform, "Btn_EndTurn", "Descansar",
            new Vector2(0, 0), new Vector2(0, 0), new Vector2(212, 8), new Vector2(392, 36));
        var detour = EnsureButton(panel.transform, "Btn_Detour", "Desviar",
            new Vector2(0, 0), new Vector2(0, 0), new Vector2(404, 8), new Vector2(584, 36));

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
        jm.abortButton = abort;
        jm.endTurnButton = endTurn;
        jm.detourButton = detour;
        EditorUtility.SetDirty(jm);

        // Mapa da jornada
        JourneyMapUI map = Object.FindObjectOfType<JourneyMapUI>();
        if (map == null)
            map = jm.gameObject.AddComponent<JourneyMapUI>();

        Undo.RecordObject(map, "Montar Cena");
        map.nodeContainer = mapRow.GetComponent<RectTransform>();
        map.nodePrefab = mapNode;
        map.nodeDetailText = upcoming;
        EditorUtility.SetDirty(map);

        panel.SetActive(false);
        return panel;
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
    static GameObject EnsureBox(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
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

    static GameObject BuildCombat(Canvas canvas, GameObject enemyCard, GameObject partyStatus, GameObject cardPrefab)
    {
        GameObject panel = FindOrCreatePanel(canvas, "Panel_Combat");

        var turn = EnsureText(panel.transform, "Txt_Turn", "Turno 0", 26,
            new Vector2(0, 1), new Vector2(0.4f, 1), new Vector2(20, -60), new Vector2(0, -16));
        var energy = EnsureText(panel.transform, "Txt_Energy", "", 26,
            new Vector2(0.4f, 1), new Vector2(0.7f, 1), new Vector2(0, -60), new Vector2(0, -16));
        var instruction = EnsureText(panel.transform, "Txt_Instruction", "", 20,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -96), new Vector2(-20, -62));

        var enemies = EnsureRow(panel.transform, "EnemyContainer", new Vector2(0, 1), new Vector2(1, 1),
            new Vector2(20, -360), new Vector2(-20, -104), 14);

        var log = EnsureText(panel.transform, "Txt_CombatLog", "", 17,
            new Vector2(0.62f, 0), new Vector2(1, 0), new Vector2(0, 540), new Vector2(-20, 710));

        var heroes = EnsureRow(panel.transform, "HeroContainer", new Vector2(0, 0), new Vector2(0.6f, 0),
            new Vector2(20, 540), new Vector2(0, 710), 10);

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
        cm.enemyPrefab = enemyCard;
        cm.heroContainer = heroes.transform;
        cm.heroStatusPrefab = partyStatus;
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

    #region Sala de Mapas

    static GameObject BuildMapRoom(Canvas canvas)
    {
        GameObject panel = FindOrCreatePanel(canvas, "Panel_MapRoom");

        var title = EnsureText(panel.transform, "Txt_Title", "🗺️ Sala de Mapas", 32,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -70), new Vector2(-20, -20));
        var level = EnsureText(panel.transform, "Txt_Level", "Nível 1", 24,
            new Vector2(0, 1), new Vector2(0.5f, 1), new Vector2(20, -110), new Vector2(0, -74));
        var scouting = EnsureText(panel.transform, "Txt_Scouting", "", 22,
            new Vector2(0, 1), new Vector2(0.5f, 1), new Vector2(20, -150), new Vector2(0, -114));
        var detours = EnsureText(panel.transform, "Txt_Detours", "", 22,
            new Vector2(0.5f, 1), new Vector2(1, 1), new Vector2(0, -150), new Vector2(-20, -114));
        var empty = EnsureText(panel.transform, "Txt_Empty", "Nenhum evento revelado.", 20,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -230), new Vector2(-20, -170));

        var revealed = EnsureColumn(panel.transform, "RevealedEvents",
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(20, 120), new Vector2(-20, -240), 6);

        var upgrade = EnsureButton(panel.transform, "Btn_Upgrade", "MELHORAR",
            new Vector2(0, 0), new Vector2(0, 0), new Vector2(20, 60), new Vector2(280, 100));
        var buyScout = EnsureButton(panel.transform, "Btn_Scout", "CONTRATAR BATEDOR",
            new Vector2(0, 0), new Vector2(0, 0), new Vector2(292, 60), new Vector2(600, 100));
        var buyDetour = EnsureButton(panel.transform, "Btn_Detour", "TRAÇAR DESVIO",
            new Vector2(0, 0), new Vector2(0, 0), new Vector2(612, 60), new Vector2(900, 100));
        var close = EnsureButton(panel.transform, "Btn_Close", "Voltar",
            new Vector2(0, 0), new Vector2(0, 0), new Vector2(20, 14), new Vector2(200, 50));

        MapRoomManager mr = Object.FindObjectOfType<MapRoomManager>();
        if (mr == null)
        {
            var host = new GameObject("MapRoomManager");
            Undo.RegisterCreatedObjectUndo(host, "Criar MapRoomManager");
            mr = host.AddComponent<MapRoomManager>();
        }

        Undo.RecordObject(mr, "Montar Cena");
        mr.levelText = level;
        mr.scoutingText = scouting;
        mr.detourText = detours;
        mr.emptyStateText = empty;
        mr.revealedEventsContainer = revealed.transform;
        mr.upgradeButton = upgrade;
        mr.buyScoutingButton = buyScout;
        mr.buyDetourButton = buyDetour;
        mr.closeButton = close;
        EditorUtility.SetDirty(mr);

        panel.SetActive(false);
        return panel;
    }

    #endregion

    #region Taverna

    /// <summary>
    /// Completa a Taverna. Contratar já funcionava, mas o botão de renovar a
    /// lista nunca existiu na cena: os campos do TavernManager estavam nulos e a
    /// única forma de ver outros candidatos era sair da sala e entrar de novo.
    /// </summary>
    static void BuildTavern(Canvas canvas)
    {
        Transform panel = canvas.transform.Find("Background/Taverna")
                       ?? canvas.transform.Find("Taverna");
        if (panel == null) return;

        TavernManager tv = panel.GetComponent<TavernManager>();
        if (tv == null) tv = panel.gameObject.AddComponent<TavernManager>();

        Button refresh = EnsureButton(panel, "Btn_Refresh", "NOVOS CANDIDATOS",
            new Vector2(1, 0), new Vector2(1, 0), new Vector2(-330, 24), new Vector2(-30, 78));

        TMP_Text cost = EnsureText(panel, "Txt_RefreshCost", "", 20,
            new Vector2(1, 0), new Vector2(1, 0), new Vector2(-330, 82), new Vector2(-30, 116));
        cost.alignment = TextAlignmentOptions.Center;

        Undo.RecordObject(tv, "Montar Cena");
        if (tv.refreshButton == null) tv.refreshButton = refresh;
        if (tv.refreshCostText == null) tv.refreshCostText = cost;
        EditorUtility.SetDirty(tv);
    }

    #endregion

    #region Mercado, Cemitério e Forja

    /// <summary>
    /// As três salas seguem o mesmo desenho: cabeçalho, uma lista que o manager
    /// preenche em tempo de execução e um rodapé com o retorno da última ação.
    /// As linhas são criadas por código (não há prefab para elas), então a coluna
    /// precisa controlar a largura dos filhos.
    /// </summary>
    static GameObject BuildRoomShell(Canvas canvas, string panelName, string title,
                                     out GameObject list, out TMP_Text feedback, out Button close)
    {
        GameObject panel = FindOrCreatePanel(canvas, panelName);

        EnsureText(panel.transform, "Txt_Title", title, 32,
            new Vector2(0, 1), new Vector2(0.7f, 1), new Vector2(20, -70), new Vector2(0, -20));

        list = EnsureScrollColumn(panel.transform, "List",
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(20, 120), new Vector2(-20, -190), 8);

        var layout = list.GetComponent<VerticalLayoutGroup>();
        if (layout != null)
        {
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperCenter;
        }

        feedback = EnsureText(panel.transform, "Txt_Feedback", "", 18,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(240, 66), new Vector2(-20, 108));

        close = EnsureButton(panel.transform, "Btn_Close", "Voltar",
            new Vector2(0, 0), new Vector2(0, 0), new Vector2(20, 20), new Vector2(220, 60));

        panel.SetActive(false);
        return panel;
    }

    static GameObject BuildMarket(Canvas canvas)
    {
        GameObject list;
        TMP_Text feedback;
        Button close;
        GameObject panel = BuildRoomShell(canvas, "Panel_Market", "🛒 Mercado", out list, out feedback, out close);

        var gold = EnsureText(panel.transform, "Txt_Gold", "💰 0", 26,
            new Vector2(0.7f, 1), new Vector2(1, 1), new Vector2(0, -70), new Vector2(-20, -20));
        var stock = EnsureText(panel.transform, "Txt_Stock", "", 20,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -112), new Vector2(-20, -76));

        MarketManager market = Object.FindObjectOfType<MarketManager>();
        if (market == null)
        {
            var host = new GameObject("MarketManager");
            Undo.RegisterCreatedObjectUndo(host, "Criar MarketManager");
            market = host.AddComponent<MarketManager>();
        }

        Undo.RecordObject(market, "Montar Cena");
        market.goldText = gold;
        market.stockText = stock;
        market.itemContainer = list.transform;
        market.feedbackText = feedback;
        market.closeButton = close;
        EditorUtility.SetDirty(market);

        return panel;
    }

    static GameObject BuildCemetery(Canvas canvas)
    {
        GameObject list;
        TMP_Text feedback;
        Button close;
        GameObject panel = BuildRoomShell(canvas, "Panel_Cemetery", "⚰️ Cemitério", out list, out feedback, out close);

        var summary = EnsureText(panel.transform, "Txt_Summary", "", 22,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -112), new Vector2(-20, -76));
        var empty = EnsureText(panel.transform, "Txt_Empty",
            "Nenhum herói tombou até aqui. Aproveite enquanto dura.", 20,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -180), new Vector2(-20, -130));

        var vigil = EnsureButton(panel.transform, "Btn_Vigil", "VIGÍLIA",
            new Vector2(0, 0), new Vector2(0, 0), new Vector2(232, 20), new Vector2(492, 60));

        CemeteryManager cemetery = Object.FindObjectOfType<CemeteryManager>();
        if (cemetery == null)
        {
            var host = new GameObject("CemeteryManager");
            Undo.RegisterCreatedObjectUndo(host, "Criar CemeteryManager");
            cemetery = host.AddComponent<CemeteryManager>();
        }

        Undo.RecordObject(cemetery, "Montar Cena");
        cemetery.summaryText = summary;
        cemetery.emptyStateText = empty;
        cemetery.graveContainer = list.transform;
        cemetery.feedbackText = feedback;
        cemetery.vigilButton = vigil;
        cemetery.closeButton = close;
        EditorUtility.SetDirty(cemetery);

        return panel;
    }

    static GameObject BuildForge(Canvas canvas)
    {
        GameObject list;
        TMP_Text feedback;
        Button close;
        GameObject panel = BuildRoomShell(canvas, "Panel_Forge", "⚔️ Forja", out list, out feedback, out close);

        var gold = EnsureText(panel.transform, "Txt_Gold", "💰 0", 26,
            new Vector2(0.7f, 1), new Vector2(1, 1), new Vector2(0, -70), new Vector2(-20, -20));
        var hint = EnsureText(panel.transform, "Txt_Hint", "", 18,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -112), new Vector2(-20, -76));

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
        EditorUtility.SetDirty(forge);

        return panel;
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
        Sprite painel = AssetDatabase.LoadAssetAtPath<Sprite>(PanelSpritePath);
        Sprite moldura = AssetDatabase.LoadAssetAtPath<Sprite>(OutlineSpritePath);
        Sprite botao = AssetDatabase.LoadAssetAtPath<Sprite>(ButtonDefaultPath);
        Sprite marca = AssetDatabase.LoadAssetAtPath<Sprite>(CheckmarkSpritePath);

        if (painel == null || botao == null)
        {
            Debug.LogWarning("Montar Cena: kit Bloodlines UI não encontrado — visual do kit não aplicado.");
            return;
        }

        var estados = new SpriteState
        {
            highlightedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ButtonHoverPath),
            pressedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ButtonPressedPath),
            disabledSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ButtonDisabledPath)
        };

        foreach (var img in canvas.GetComponentsInChildren<Image>(true))
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

            if (img.sprite != null) continue;

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
            }
        }

        if (moldura != null)
            foreach (var img in canvas.GetComponentsInChildren<Image>(true))
                if (img.color == BoxColor && img.GetComponent<Button>() == null)
                    EnsureOutline(img.rectTransform, moldura);

        ApplyTitleFont(canvas);
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

        // ── Passo 1: lista de missões ──────────────────────────────────────
        Restyle(root, "QuestListContainer/Panel_QuestDetails/QuestDetailsTxt", null, 20,
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(18, 18), new Vector2(-18, -18));
        Reposition(root, "QuestListContainer/Button_Next1",
            new Vector2(1, 0), new Vector2(1, 0), new Vector2(-220, -88), new Vector2(0, -24));

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

    static GameObject FindOrCreatePanel(Canvas canvas, string name)
    {
        Transform existing = canvas.transform.Find(name);
        if (existing != null) return existing.gameObject;

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

    static TMP_Text EnsureText(Transform parent, string name, string content, int size,
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

    static Button EnsureButton(Transform parent, string name, string label,
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

    static GameObject EnsureRow(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
                                Vector2 offsetMin, Vector2 offsetMax, int spacing)
    {
        return EnsureLayout<HorizontalLayoutGroup>(parent, name, anchorMin, anchorMax, offsetMin, offsetMax, spacing);
    }

    static GameObject EnsureColumn(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
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
    static GameObject EnsureScrollColumn(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
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
    static GameObject EnsureFreeArea(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
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

    static void ApplyRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }

    #endregion
}
#endif
