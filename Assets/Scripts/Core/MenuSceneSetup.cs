#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Monta a cena de título e os painéis de menu, e registra as duas cenas na
/// build.
///
/// Tools → Guild of Legends → Montar Menus
///
/// Segue a mesma regra do <see cref="GuildSceneSetup"/>, e reaproveita os
/// utilitários dele: a UI deste projeto nasce de código, não de prefab editado à
/// mão, para que uma cena perdida possa ser refeita com um clique. É idempotente
/// — rodar de novo reaproveita o que já existe.
///
/// Divisão de responsabilidade: a cena <c>MainMenu</c> é inteira daqui; na cena
/// do jogo, este arquivo monta só a pausa e as duas telas que ela compartilha
/// com o título (opções e slots), chamado de dentro do <c>GuildSceneSetup</c>.
/// </summary>
public static class MenuSceneSetup
{
    public const string CaminhoDoMenu = "Assets/Scenes/MainMenu.unity";
    public const string CaminhoDoJogo = "Assets/Scenes/SampleScene.unity";

    /// <summary>
    /// Fundo das telas que cobrem outra tela.
    ///
    /// Opaco de verdade (alfa 1), e não os 0,98 do resto do projeto. Dois por
    /// cento de transparência não se nota sobre uma sala escura, mas sobre um
    /// título em fonte de 64 o texto de baixo atravessa e briga com o de cima —
    /// foi exatamente o que a captura do Santuário mostrou.
    /// </summary>
    static readonly Color FundoSolido = new Color(0.06f, 0.055f, 0.07f, 1f);

    /// <summary>Painel que cobre o que está atrás, sem deixar nada vazar.</summary>
    static GameObject PainelOpaco(Canvas canvas, string nome)
    {
        GameObject panel = GuildSceneSetup.FindOrCreatePanel(canvas, nome);

        var img = panel.GetComponent<Image>();
        if (img == null) img = panel.AddComponent<Image>();

        img.color = FundoSolido;
        img.sprite = null;
        img.raycastTarget = true;   // engole o clique: o que está atrás não é mais alcançável

        return panel;
    }

    [MenuItem("Tools/Guild of Legends/Montar Menus")]
    public static void Montar()
    {
        Montar(true);
    }

    /// <param name="interactive">false para rodar sem nenhum diálogo — os modais
    /// travam a thread do Editor e impedem qualquer chamada automatizada de
    /// retornar.</param>
    public static void Montar(bool interactive)
    {
        string cenaAberta = EditorSceneManager.GetActiveScene().path;

        if (EditorSceneManager.GetActiveScene().isDirty)
            EditorSceneManager.SaveOpenScenes();

        MontarCenaDoMenu();
        RegistrarNaBuild();

        // Sem isto o EditorBuildSettings fica só na memória do Editor: a lista de
        // cenas aparece certa na janela de Build e o arquivo do repositório
        // continua com a lista antiga, o que só se descobre num clone.
        AssetDatabase.SaveAssets();

        // Devolve o Editor à cena em que o autor estava. Sem isto, rodar o
        // gatilho no meio de um trabalho na cena do jogo o largaria no título.
        if (!string.IsNullOrEmpty(cenaAberta) && cenaAberta != CaminhoDoMenu)
            EditorSceneManager.OpenScene(cenaAberta);

        Debug.Log("Menus montados. MainMenu é a cena 0 da build.");

        if (interactive)
            EditorUtility.DisplayDialog("Montar Menus",
                "Cena de título criada e registrada na build.\n\n"
                + "A pausa e as telas de opções/slots da cena do jogo são montadas "
                + "pelo 'Montar Cena'.", "Ok");
    }

    #region A cena de título

    static void MontarCenaDoMenu()
    {
        Scene cena;

        if (System.IO.File.Exists(CaminhoDoMenu))
        {
            cena = EditorSceneManager.OpenScene(CaminhoDoMenu);
        }
        else
        {
            cena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            System.IO.Directory.CreateDirectory("Assets/Scenes");
        }

        Canvas canvas = GarantirCanvas();
        GarantirEventSystem();
        GarantirCamera();

        MainMenuUI menu = BuildMainMenu(canvas);
        OptionsUI opcoes = BuildOptions(canvas);
        SaveSlotsUI slots = BuildSaveSlots(canvas);
        RelicShrineUI santuario = BuildShrine(canvas);

        menu.options = opcoes;
        menu.saveSlots = slots;
        menu.shrine = santuario;
        EditorUtility.SetDirty(menu);

        EditorSceneManager.MarkSceneDirty(cena);
        EditorSceneManager.SaveScene(cena, CaminhoDoMenu);
    }

    static Canvas GarantirCanvas()
    {
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas != null) return canvas;

        var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // Ao contrário da cena do jogo — que está em pixels fixos desde antes
        // deste trabalho — o título escala com a tela. É uma cena nova, sem
        // layout legado para respeitar, e uma tela de título cortada pela metade
        // num monitor menor é a primeira coisa que o jogador veria.
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    static void GarantirEventSystem()
    {
        if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null) return;

        new GameObject("EventSystem",
            typeof(UnityEngine.EventSystems.EventSystem),
            typeof(UnityEngine.EventSystems.StandaloneInputModule));
    }

    static void GarantirCamera()
    {
        if (Object.FindObjectOfType<Camera>() != null) return;

        var go = new GameObject("Main Camera", typeof(Camera));
        go.tag = "MainCamera";

        var cam = go.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.04f, 0.035f, 0.045f);
        cam.orthographic = true;
    }

    #endregion

    #region Painéis da cena do jogo

    /// <summary>
    /// A pausa e as telas que ela compartilha com o título. Chamado pelo
    /// <see cref="GuildSceneSetup"/> para que "Montar Cena" continue sendo o
    /// único comando de quem mexe na cena do jogo.
    /// </summary>
    internal static void MontarNaCenaDoJogo(Canvas canvas)
    {
        OptionsUI opcoes = BuildOptions(canvas);
        SaveSlotsUI slots = BuildSaveSlots(canvas);
        PauseMenuUI pausa = BuildPause(canvas);

        pausa.options = opcoes;
        pausa.saveSlots = slots;
        EditorUtility.SetDirty(pausa);
    }

    #endregion

    #region Título

    static MainMenuUI BuildMainMenu(Canvas canvas)
    {
        GameObject panel = GuildSceneSetup.FindOrCreatePanel(canvas, "Panel_MainMenu");

        var titulo = GuildSceneSetup.EnsureText(panel.transform, "Txt_Title", "GUILDA DA CORRUPÇÃO", 64,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(40, -210), new Vector2(-40, -110));
        titulo.alignment = TextAlignmentOptions.Center;

        var subtitulo = GuildSceneSetup.EnsureText(panel.transform, "Txt_Subtitle", "", 22,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(200, -272), new Vector2(-200, -212));
        subtitulo.alignment = TextAlignmentOptions.Center;
        subtitulo.color = GuildSceneSetup.SubtleTextColor;

        // A dica desce e a pilha de botões desce junto: coladas no subtítulo, as
        // duas linhas se liam como um parágrafo só — a captura mostrou "Nenhuma
        // partida em andamento" parecendo continuação da frase de abertura, e não
        // a explicação do botão "Continuar" logo abaixo.
        var dica = GuildSceneSetup.EnsureText(panel.transform, "Txt_ContinueHint", "", 20,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-420, 170), new Vector2(420, 212));
        dica.alignment = TextAlignmentOptions.Center;
        dica.color = GuildSceneSetup.SubtleTextColor;

        Button continuar = BotaoDaPilha(panel.transform, "Btn_Continue", "Continuar", 0);
        Button novo = BotaoDaPilha(panel.transform, "Btn_NewGame", "Fundar uma nova guilda", 1);
        Button carregar = BotaoDaPilha(panel.transform, "Btn_Load", "Carregar partida", 2);
        Button santuario = BotaoDaPilha(panel.transform, "Btn_Shrine", "Santuário das Relíquias", 3);
        Button opcoes = BotaoDaPilha(panel.transform, "Btn_Options", "Opções", 4);
        Button sair = BotaoDaPilha(panel.transform, "Btn_Quit", "Sair", 5);

        var meta = GuildSceneSetup.EnsureText(panel.transform, "Txt_Meta", "", 20,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(60, 48), new Vector2(-60, 100));
        meta.alignment = TextAlignmentOptions.Center;

        MainMenuUI ui = panel.GetComponent<MainMenuUI>();
        if (ui == null) ui = panel.AddComponent<MainMenuUI>();

        ui.titleText = titulo;
        ui.subtitleText = subtitulo;
        ui.metaText = meta;
        ui.continueHintText = dica;
        ui.continueButton = continuar;
        ui.newGameButton = novo;
        ui.loadButton = carregar;
        ui.shrineButton = santuario;
        ui.optionsButton = opcoes;
        ui.quitButton = sair;

        EditorUtility.SetDirty(ui);

        panel.SetActive(true);
        return ui;
    }

    /// <summary>Um botão da coluna central do título, pelo índice de cima para baixo.</summary>
    static Button BotaoDaPilha(Transform pai, string nome, string rotulo, int indice)
    {
        const float Altura = 64f;
        const float Espaco = 12f;
        const float Topo = 150f;

        float y = Topo - indice * (Altura + Espaco);

        return GuildSceneSetup.EnsureButton(pai, nome, rotulo,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-210, y - Altura), new Vector2(210, y));
    }

    #endregion

    #region Opções

    static OptionsUI BuildOptions(Canvas canvas)
    {
        GameObject panel = PainelOpaco(canvas, "Panel_Options");

        GameObject caixa = GuildSceneSetup.EnsureBox(panel.transform, "Box",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-440, -280), new Vector2(440, 300));

        var titulo = GuildSceneSetup.EnsureText(caixa.transform, "Txt_Title", "OPÇÕES", 38,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(30, -90), new Vector2(-30, -26));
        titulo.alignment = TextAlignmentOptions.Center;

        // --- Áudio ---
        Rotulo(caixa.transform, "Lbl_Music", "Música", -150);
        Slider musica = Barrinha(caixa.transform, "Slider_Music", -150);
        TMP_Text musicaValor = Valor(caixa.transform, "Txt_MusicValue", -150);

        Rotulo(caixa.transform, "Lbl_Sfx", "Efeitos", -230);
        Slider efeitos = Barrinha(caixa.transform, "Slider_Sfx", -230);
        TMP_Text efeitosValor = Valor(caixa.transform, "Txt_SfxValue", -230);

        // --- Vídeo ---
        Rotulo(caixa.transform, "Lbl_Resolution", "Resolução", -330);

        Button anterior = GuildSceneSetup.EnsureButton(caixa.transform, "Btn_ResPrev", "◀",
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(300, -366), new Vector2(352, -314));

        var resolucao = GuildSceneSetup.EnsureText(caixa.transform, "Txt_Resolution", "", 22,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(360, -366), new Vector2(600, -314));
        resolucao.alignment = TextAlignmentOptions.Center;

        Button proximo = GuildSceneSetup.EnsureButton(caixa.transform, "Btn_ResNext", "▶",
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(608, -366), new Vector2(660, -314));

        Button telaCheia = GuildSceneSetup.EnsureButton(caixa.transform, "Btn_Fullscreen", "Tela cheia: SIM",
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(300, -446), new Vector2(660, -394));

        // --- Rodapé ---
        Button restaurar = GuildSceneSetup.EnsureButton(caixa.transform, "Btn_Restore", "Restaurar padrões",
            new Vector2(0, 0), new Vector2(0, 0), new Vector2(30, 28), new Vector2(290, 82));

        Button fechar = GuildSceneSetup.EnsureButton(caixa.transform, "Btn_Close", "Voltar",
            new Vector2(1, 0), new Vector2(1, 0), new Vector2(-290, 28), new Vector2(-30, 82));

        OptionsUI ui = panel.GetComponent<OptionsUI>();
        if (ui == null) ui = panel.AddComponent<OptionsUI>();

        ui.panel = panel;
        ui.musicSlider = musica;
        ui.musicValueText = musicaValor;
        ui.sfxSlider = efeitos;
        ui.sfxValueText = efeitosValor;
        ui.resolutionText = resolucao;
        ui.resolutionPrevButton = anterior;
        ui.resolutionNextButton = proximo;
        ui.fullscreenButton = telaCheia;
        ui.fullscreenLabel = RotuloDoBotao(telaCheia);
        ui.restoreButton = restaurar;
        ui.closeButton = fechar;

        EditorUtility.SetDirty(ui);

        panel.SetActive(false);
        return ui;
    }

    static void Rotulo(Transform pai, string nome, string texto, float y)
    {
        var t = GuildSceneSetup.EnsureText(pai, nome, texto, 24,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, y - 36), new Vector2(280, y + 16));
        t.alignment = TextAlignmentOptions.Left;
    }

    static TMP_Text Valor(Transform pai, string nome, float y)
    {
        var t = GuildSceneSetup.EnsureText(pai, nome, "", 22,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(700, y - 36), new Vector2(830, y + 16));
        t.alignment = TextAlignmentOptions.Right;
        t.color = GuildSceneSetup.SubtleTextColor;
        return t;
    }

    /// <summary>
    /// Uma barra de volume. O <c>Slider</c> do Unity precisa de três peças
    /// posicionadas — trilho, preenchimento e alça — e nenhuma delas existe por
    /// padrão quando o componente é adicionado por código.
    /// </summary>
    static Slider Barrinha(Transform pai, string nome, float y)
    {
        Transform existente = pai.Find(nome);
        GameObject go;

        if (existente != null)
        {
            go = existente.gameObject;
        }
        else
        {
            go = new GameObject(nome, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(pai, false);
        }

        GuildSceneSetup.ApplyRect(go.GetComponent<RectTransform>(),
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(300, y - 26), new Vector2(680, y + 6));

        var trilho = go.GetComponent<Image>();
        if (trilho == null) trilho = go.AddComponent<Image>();
        trilho.color = GuildSceneSetup.TrackColor;

        GameObject areaFill = GuildSceneSetup.EnsureFreeArea(go.transform, "Fill Area",
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(6, 6), new Vector2(-6, -6));

        GameObject fill = GuildSceneSetup.EnsureFreeArea(areaFill.transform, "Fill",
            new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(10, 0));

        var fillImg = fill.GetComponent<Image>();
        if (fillImg == null) fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color(0.62f, 0.55f, 0.30f);

        GameObject areaAlca = GuildSceneSetup.EnsureFreeArea(go.transform, "Handle Slide Area",
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(10, 0), new Vector2(-10, 0));

        GameObject alca = GuildSceneSetup.EnsureFreeArea(areaAlca.transform, "Handle",
            new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(20, 0));

        var alcaImg = alca.GetComponent<Image>();
        if (alcaImg == null) alcaImg = alca.AddComponent<Image>();
        alcaImg.color = GuildSceneSetup.HandleColor;

        var slider = go.GetComponent<Slider>();
        if (slider == null) slider = go.AddComponent<Slider>();

        slider.direction = Slider.Direction.LeftToRight;
        slider.fillRect = fill.GetComponent<RectTransform>();
        slider.handleRect = alca.GetComponent<RectTransform>();
        slider.targetGraphic = alcaImg;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;

        return slider;
    }

    static TMP_Text RotuloDoBotao(Button botao)
    {
        return botao == null ? null : botao.GetComponentInChildren<TMP_Text>(true);
    }

    #endregion

    #region Slots de save

    static SaveSlotsUI BuildSaveSlots(Canvas canvas)
    {
        GameObject panel = PainelOpaco(canvas, "Panel_SaveSlots");

        var titulo = GuildSceneSetup.EnsureText(panel.transform, "Txt_Title", "", 40,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(40, -130), new Vector2(-40, -60));
        titulo.alignment = TextAlignmentOptions.Center;

        var dica = GuildSceneSetup.EnsureText(panel.transform, "Txt_Hint", "", 20,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(120, -176), new Vector2(-120, -134));
        dica.alignment = TextAlignmentOptions.Center;
        dica.color = GuildSceneSetup.SubtleTextColor;

        GameObject lista = GuildSceneSetup.EnsureColumn(panel.transform, "SlotList",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-520, -230), new Vector2(520, 250), 12);

        var layout = lista.GetComponent<VerticalLayoutGroup>();
        if (layout != null)
        {
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childAlignment = TextAnchor.UpperCenter;
        }

        GameObject molde = MoldeDeSlot(panel.transform);

        Button fechar = GuildSceneSetup.EnsureButton(panel.transform, "Btn_Close", "Voltar",
            new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-140, 60), new Vector2(140, 116));

        SaveSlotsUI ui = panel.GetComponent<SaveSlotsUI>();
        if (ui == null) ui = panel.AddComponent<SaveSlotsUI>();

        ui.panel = panel;
        ui.titleText = titulo;
        ui.hintText = dica;
        ui.slotContainer = lista.transform;
        ui.slotTemplate = molde;
        ui.closeButton = fechar;

        EditorUtility.SetDirty(ui);

        panel.SetActive(false);
        return ui;
    }

    /// <summary>Molde de uma linha de slot: nome, resumo e os dois botões.</summary>
    static GameObject MoldeDeSlot(Transform pai)
    {
        GameObject go = MoldeBase(pai, "SlotTemplate", 96);

        GuildSceneSetup.EnsureText(go.transform, "Name", "", 26,
            new Vector2(0, 1), new Vector2(0.5f, 1), new Vector2(20, -46), new Vector2(0, -10));

        var detalhe = GuildSceneSetup.EnsureText(go.transform, "Detail", "", 18,
            new Vector2(0, 0), new Vector2(0.72f, 0), new Vector2(20, 14), new Vector2(0, 52));
        detalhe.color = GuildSceneSetup.SubtleTextColor;

        GuildSceneSetup.EnsureButton(go.transform, "Action", "",
            new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-280, -24), new Vector2(-100, 24));

        GuildSceneSetup.EnsureButton(go.transform, "Delete", "Apagar",
            new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-92, -24), new Vector2(-16, 24));

        go.SetActive(false);
        return go;
    }

    #endregion

    #region Santuário

    static RelicShrineUI BuildShrine(Canvas canvas)
    {
        GameObject panel = PainelOpaco(canvas, "Panel_Shrine");

        var titulo = GuildSceneSetup.EnsureText(panel.transform, "Txt_Title", "", 40,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(40, -120), new Vector2(-40, -50));
        titulo.alignment = TextAlignmentOptions.Center;

        var saldo = GuildSceneSetup.EnsureText(panel.transform, "Txt_Balance", "", 30,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(40, -172), new Vector2(-40, -122));
        saldo.alignment = TextAlignmentOptions.Center;

        var dica = GuildSceneSetup.EnsureText(panel.transform, "Txt_Hint", "", 18,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(200, -212), new Vector2(-200, -174));
        dica.alignment = TextAlignmentOptions.Center;
        dica.color = GuildSceneSetup.SubtleTextColor;

        GameObject lista = GuildSceneSetup.EnsureColumn(panel.transform, "UnlockList",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-540, -240), new Vector2(540, 220), 12);

        var layout = lista.GetComponent<VerticalLayoutGroup>();
        if (layout != null)
        {
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childAlignment = TextAnchor.UpperCenter;
        }

        GameObject molde = MoldeDeDestrave(panel.transform);

        Button fechar = GuildSceneSetup.EnsureButton(panel.transform, "Btn_Close", "Voltar",
            new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-140, 60), new Vector2(140, 116));

        RelicShrineUI ui = panel.GetComponent<RelicShrineUI>();
        if (ui == null) ui = panel.AddComponent<RelicShrineUI>();

        ui.panel = panel;
        ui.titleText = titulo;
        ui.balanceText = saldo;
        ui.hintText = dica;
        ui.unlockContainer = lista.transform;
        ui.unlockTemplate = molde;
        ui.closeButton = fechar;

        EditorUtility.SetDirty(ui);

        panel.SetActive(false);
        return ui;
    }

    static GameObject MoldeDeDestrave(Transform pai)
    {
        GameObject go = MoldeBase(pai, "UnlockTemplate", 112);

        GuildSceneSetup.EnsureText(go.transform, "Name", "", 24,
            new Vector2(0, 1), new Vector2(0.78f, 1), new Vector2(20, -42), new Vector2(0, -10));

        var descricao = GuildSceneSetup.EnsureText(go.transform, "Description", "", 17,
            new Vector2(0, 1), new Vector2(0.78f, 1), new Vector2(20, -74), new Vector2(0, -44));
        descricao.color = GuildSceneSetup.SubtleTextColor;

        GuildSceneSetup.EnsureText(go.transform, "Effect", "", 17,
            new Vector2(0, 0), new Vector2(0.78f, 0), new Vector2(20, 12), new Vector2(0, 40));

        GuildSceneSetup.EnsureButton(go.transform, "Buy", "",
            new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-190, -28), new Vector2(-20, 28));

        go.SetActive(false);
        return go;
    }

    #endregion

    #region Pausa

    static PauseMenuUI BuildPause(Canvas canvas)
    {
        GameObject panel = PainelOpaco(canvas, "Panel_Pause");

        GameObject caixa = GuildSceneSetup.EnsureBox(panel.transform, "Box",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-260, -280), new Vector2(260, 280));

        var titulo = GuildSceneSetup.EnsureText(caixa.transform, "Txt_Title", "PAUSA", 40,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -86), new Vector2(-20, -26));
        titulo.alignment = TextAlignmentOptions.Center;

        var estado = GuildSceneSetup.EnsureText(caixa.transform, "Txt_Status", "", 18,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -122), new Vector2(-20, -88));
        estado.alignment = TextAlignmentOptions.Center;
        estado.color = GuildSceneSetup.SubtleTextColor;

        Button retomar = BotaoDaPausa(caixa.transform, "Btn_Resume", "Retomar", 0);
        Button salvar = BotaoDaPausa(caixa.transform, "Btn_Save", "Salvar partida", 1);
        Button carregar = BotaoDaPausa(caixa.transform, "Btn_Load", "Carregar partida", 2);
        Button opcoes = BotaoDaPausa(caixa.transform, "Btn_Options", "Opções", 3);
        Button titulo2 = BotaoDaPausa(caixa.transform, "Btn_Title", "Voltar ao título", 4);
        Button sair = BotaoDaPausa(caixa.transform, "Btn_Quit", "Sair do jogo", 5);

        // O componente vai num objeto sempre ativo, e não no painel: o painel
        // nasce desligado, e Update só roda em objeto ativo — dentro dele, o ESC
        // nunca chegaria a abrir a pausa.
        GameObject host = GuildSceneSetup.EnsureFreeArea(canvas.transform, "PauseMenu",
            Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);

        PauseMenuUI ui = host.GetComponent<PauseMenuUI>();
        if (ui == null) ui = host.AddComponent<PauseMenuUI>();

        // Componentes de versões anteriores ficavam no painel; sem remover, dois
        // PauseMenuUI disputariam o singleton.
        var antigo = panel.GetComponent<PauseMenuUI>();
        if (antigo != null) Undo.DestroyObjectImmediate(antigo);

        ui.panel = panel;
        ui.titleText = titulo;
        ui.statusText = estado;
        ui.resumeButton = retomar;
        ui.saveButton = salvar;
        ui.loadButton = carregar;
        ui.optionsButton = opcoes;
        ui.titleButton = titulo2;
        ui.quitButton = sair;

        EditorUtility.SetDirty(ui);

        panel.SetActive(false);
        return ui;
    }

    static Button BotaoDaPausa(Transform pai, string nome, string rotulo, int indice)
    {
        const float Altura = 56f;
        const float Espaco = 10f;
        const float Topo = -156f;

        float y = Topo - indice * (Altura + Espaco);

        return GuildSceneSetup.EnsureButton(pai, nome, rotulo,
            new Vector2(0, 1), new Vector2(1, 1),
            new Vector2(30, y - Altura), new Vector2(-30, y));
    }

    #endregion

    /// <summary>Fundo de uma linha de lista, com altura fixa para o layout vertical.</summary>
    static GameObject MoldeBase(Transform pai, string nome, float altura)
    {
        Transform existente = pai.Find(nome);
        GameObject go;

        if (existente != null)
        {
            go = existente.gameObject;
        }
        else
        {
            go = new GameObject(nome, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(pai, false);
        }

        var img = go.GetComponent<Image>();
        if (img == null) img = go.AddComponent<Image>();
        img.color = GuildSceneSetup.BoxColor;

        go.GetComponent<RectTransform>().sizeDelta = new Vector2(0, altura);

        var elemento = go.GetComponent<LayoutElement>();
        if (elemento == null) elemento = go.AddComponent<LayoutElement>();
        elemento.minHeight = altura;
        elemento.preferredHeight = altura;

        return go;
    }

    #region Build settings

    /// <summary>
    /// Põe as duas cenas na build, com o título primeiro.
    ///
    /// A ordem importa: a cena 0 é a que abre quando a build roda. Sem isto, o
    /// jogo compilado continuaria começando direto na guilda, com o menu inteiro
    /// existindo e inalcançável.
    /// </summary>
    static void RegistrarNaBuild()
    {
        var cenas = new List<EditorBuildSettingsScene>();

        cenas.Add(new EditorBuildSettingsScene(CaminhoDoMenu, true));

        if (System.IO.File.Exists(CaminhoDoJogo))
            cenas.Add(new EditorBuildSettingsScene(CaminhoDoJogo, true));

        // Preserva qualquer outra cena que o autor já tenha registrado.
        foreach (var existente in EditorBuildSettings.scenes)
        {
            if (existente.path == CaminhoDoMenu || existente.path == CaminhoDoJogo) continue;
            cenas.Add(existente);
        }

        EditorBuildSettings.scenes = cenas.ToArray();
    }

    #endregion
}
#endif
