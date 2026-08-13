#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Teste de integração em Play Mode, sem interação manual.
///
/// Tools → Guild of Legends → Testar em Play Mode
///
/// Entra em Play Mode, audita as referências ligadas no Inspector, tenta rodar
/// uma jornada inteira clicando nos botões de verdade, sai do Play Mode e grava
/// PlayModeReport.txt na raiz do projeto.
/// </summary>
public static class PlayModeTestLauncher
{
    public const string Flag = "GoL.RunPlayModeProbe";

    [MenuItem("Tools/Guild of Legends/Testar em Play Mode")]
    public static void Launch()
    {
        EditorPrefs.SetBool(PlayModeProbe.SemFormacaoFlag, false);
        LaunchInternal();
    }

    /// <summary>
    /// A mesma jornada com as regras de formação desligadas. É o par de controle:
    /// comparar duas runs mudando só isso é o que distingue uma regressão real do
    /// azar de uma run.
    /// </summary>
    [MenuItem("Tools/Guild of Legends/Testar em Play Mode (sem formação)")]
    public static void LaunchWithoutFormation()
    {
        EditorPrefs.SetBool(PlayModeProbe.SemFormacaoFlag, true);
        LaunchInternal();
    }

    static void LaunchInternal()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("Saia do Play Mode antes de rodar o teste.");
            return;
        }

        EditorPrefs.SetBool(Flag, true);
        EditorApplication.isPlaying = true;
    }

    /// <summary>Raiz do projeto (pasta que contém Assets/).</summary>
    public static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoStart()
    {
        if (!EditorPrefs.GetBool(Flag, false)) return;
        EditorPrefs.SetBool(Flag, false);

        var go = new GameObject("~PlayModeProbe");
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.AddComponent<PlayModeProbe>();
    }
}

/// <summary>
/// Permite disparar o teste criando o arquivo RunPlayModeTest.trigger na raiz do
/// projeto — assim ele pode ser acionado de fora do Editor, sem usar o menu.
/// O arquivo é apagado assim que detectado.
/// </summary>
[InitializeOnLoad]
public static class PlayModeTriggerWatcher
{
    const string TriggerFile = "RunPlayModeTest.trigger";
    static double nextCheck;

    static PlayModeTriggerWatcher()
    {
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if (EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + 1.0;

        if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

        string path = Path.Combine(PlayModeTestLauncher.ProjectRoot, TriggerFile);
        if (!File.Exists(path)) return;

        try { File.Delete(path); }
        catch { return; }

        Debug.Log("Trigger detectado — iniciando teste de Play Mode.");
        PlayModeTestLauncher.Launch();
    }
}

public class PlayModeProbe : MonoBehaviour
{
    private readonly StringBuilder report = new StringBuilder();
    private readonly List<string> errors = new List<string>();
    private int eventsResolved;
    private int combatTurns;
    private int routeChoices;
    private int cardsPlayed;
    private int combatOffers;
    private bool popupInvisibleReported;

    // De onde vem o desgaste. Sem separar combate de jornada, um relatório com a
    // party morta não diz se a culpa foi das lutas, da fome ou dos eventos.
    private int combatsFought;
    private int hpLostInCombat;
    private int hpLostOutsideCombat;

    void Awake()
    {
        Application.logMessageReceived += OnLog;
    }

    void OnDestroy()
    {
        Application.logMessageReceived -= OnLog;
    }

    void OnLog(string message, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            string first = stackTrace?.Split('\n').FirstOrDefault() ?? "";
            errors.Add($"[{type}] {message} | {first}");
        }
    }

    IEnumerator Start()
    {
        // Deixa Awake/Start de todos os managers rodarem.
        yield return null;
        yield return new WaitForSeconds(0.5f);

        Section("MANAGERS NA CENA");
        bool hasJourney = ReportSingleton("GuildManager", GuildManager.Instance);
        ReportSingleton("UIManager", UIManager.Instance);
        ReportSingleton("QuestManager", QuestManager.Instance);
        hasJourney = ReportSingleton("JourneyManager", JourneyManager.Instance) && hasJourney;
        ReportSingleton("CombatManager", CombatManager.Instance);
        ReportSingleton("MapRoomManager", MapRoomManager.Instance);
        ReportSingleton("JourneyMapUI", JourneyMapUI.Instance);
        ReportSingleton("TavernManager", TavernManager.Instance);
        ReportSingleton("DeckManager", DeckManager.Instance);

        Section("REFERENCIAS NAO LIGADAS NO INSPECTOR");
        AuditInspector(JourneyManager.Instance);
        AuditInspector(CombatManager.Instance);
        AuditInspector(MapRoomManager.Instance);
        AuditInspector(JourneyMapUI.Instance);
        AuditInspector(UIManager.Instance);

        Section("POPUPS (hierarquia e visibilidade)");
        var uim = UIManager.Instance;
        if (uim != null)
        {
            ReportPopup("messagePopup", uim.messagePopup);
            ReportPopup("confirmPopup", uim.confirmPopup);
            ReportPopup("resultPopup", uim.resultPopup);
        }

        Section("ROSTER E MISSOES");
        if (GuildManager.Instance != null)
        {
            Line($"heróis no roster: {GuildManager.Instance.roster.Count}");
            foreach (var h in GuildManager.Instance.roster)
                Line($"  {h.heroName} ({h.heroClass}) Nv.{h.level} HP {h.currentHp}/{h.maxHp}");
        }

        if (QuestManager.Instance != null)
            Line($"missões no quadro: {QuestManager.Instance.GetQuests().Count}");

        Section("FORMACAO DO GRUPO");
        TestFormation();

        Section("SALAS DA GUILDA");
        yield return TestRooms();

        Section("JORNADA AUTOMATICA");
        if (hasJourney)
            yield return RunJourney();
        else
            Line("pulada: GuildManager ou JourneyManager ausentes na cena");

        Section("FIM DE JORNADA COM VITORIA");
        if (hasJourney)
            yield return TestVictoryExit();
        else
            Line("pulada: JourneyManager ausente na cena");

        Section("ERROS CAPTURADOS");
        if (errors.Count == 0)
        {
            Line("nenhum erro ou exceção durante o teste");
        }
        else
        {
            foreach (var e in errors.Take(40))
                Line(e);
            if (errors.Count > 40)
                Line($"... e mais {errors.Count - 40}");
        }

        WriteReport();

        yield return null;
        EditorApplication.isPlaying = false;
    }

    #region Formação

    /// <summary>
    /// Confere as três consequências da formação sem depender do sorteio de um
    /// combate real: quem os inimigos miram, quanto dano chega em cada fileira e
    /// o que acontece com a carta de um herói fora de posição.
    /// </summary>
    void TestFormation()
    {
        if (GuildManager.Instance == null)
        {
            Line("pulada: GuildManager ausente");
            return;
        }

        var party = GuildManager.Instance.roster.Where(h => h != null && !h.isDead).Take(4).ToList();
        if (party.Count < 2)
        {
            Line("pulada: menos de dois heróis no roster");
            return;
        }

        var bem = SortedByPreference(party);
        Line("ordem recomendada:");
        foreach (var h in bem)
            Line($"  {StripTags(PartyFormation.DescribePlacement(h, bem))}");

        int malColocados = bem.Count(h => !PartyFormation.IsWellPlaced(h, bem));
        Line($"heróis fora de posição na ordem recomendada: {malColocados}");

        // Distribuição de alvos: a linha de frente deve concentrar os golpes.
        const int amostras = 2000;
        int naFrente = 0;
        for (int i = 0; i < amostras; i++)
        {
            HeroData alvo = PartyFormation.PickTarget(bem);
            if (alvo != null && PartyFormation.GetRow(alvo, bem) == FormationRow.Front)
                naFrente++;
        }
        Line($"ataques que caem na linha de frente: {naFrente * 100 / amostras}% de {amostras} sorteios");

        // Dano por fileira, com o mesmo golpe.
        Line($"dano de 10 na frente: {PartyFormation.Scale(10, PartyFormation.DamageTakenMultiplier(bem[0], bem))}"
           + $" | na retaguarda: {PartyFormation.Scale(10, PartyFormation.DamageTakenMultiplier(bem[bem.Count - 1], bem))}");

        // Potência das cartas: a mesma party invertida deve penalizar quem trocou de fileira.
        var invertida = Enumerable.Reverse(bem).ToList();
        var built = JourneyDeckBuilder.Build(bem[0], bem);

        int enfraquecidasBem = built.deck.cards.Count(c => built.ownership.PowerMultiplier(c, bem) < 1f);
        int enfraquecidasInvertida = built.deck.cards.Count(c => built.ownership.PowerMultiplier(c, invertida) < 1f);

        Line($"cartas enfraquecidas — ordem recomendada: {enfraquecidasBem}/{built.deck.cards.Count}"
           + $" | ordem invertida: {enfraquecidasInvertida}/{built.deck.cards.Count}");

        if (enfraquecidasInvertida <= enfraquecidasBem && bem.Count >= 3)
            Line("SUSPEITO: inverter a formação não penalizou mais cartas — a posição pode não estar sendo lida.");

        // Todo dono registrado deve ser alguém da party.
        int semDono = built.deck.cards.Count(c => built.ownership.BestOwner(c, bem) == null);
        Line($"cartas sem dono registrado: {semDono}");
    }

    /// <summary>A ordem que a tela de preparação recomendaria: frente, coringas, retaguarda.</summary>
    static List<HeroData> SortedByPreference(List<HeroData> party)
    {
        return party.OrderBy(h =>
        {
            FormationRow? preferida = PartyFormation.PreferredRow(h.heroClass);
            if (preferida == null) return 1;
            return preferida.Value == FormationRow.Front ? 0 : 2;
        }).ToList();
    }

    static string StripTags(string texto)
    {
        return System.Text.RegularExpressions.Regex.Replace(texto ?? "", "<.*?>", "");
    }

    static int PartyHp(List<HeroData> party)
    {
        return party.Sum(h => Mathf.Max(0, h.currentHp));
    }

    /// <summary>
    /// Liga o teste sem formação, para comparar contra a mesma jornada com ela.
    /// Existe porque um relatório com a party morta não diz, sozinho, se a culpa
    /// é da regra nova ou do sorteio daquela run.
    /// </summary>
    public const string SemFormacaoFlag = "GoL.ProbeSemFormacao";

    static bool SemFormacao => EditorPrefs.GetBool(SemFormacaoFlag, false);

    #endregion

    #region Salas da guilda

    /// <summary>
    /// Abre Mercado, Cemitério e Forja pelo mesmo caminho do jogador e executa
    /// uma compra em cada, conferindo que o ouro sai e o efeito entra.
    /// </summary>
    IEnumerator TestRooms()
    {
        var ui = UIManager.Instance;
        if (ui == null || GuildManager.Instance == null)
        {
            Line("pulada: UIManager ou GuildManager ausentes");
            yield break;
        }

        // Ouro suficiente para exercitar as compras sem depender do saldo inicial.
        GuildManager.Instance.AddGold(2000);

        ReportSingleton("MarketManager", MarketManager.Instance);
        ReportSingleton("CemeteryManager", CemeteryManager.Instance);
        ReportSingleton("ForgeManager", ForgeManager.Instance);

        yield return TestMarket(ui);
        yield return TestForge(ui);
        yield return TestCemetery(ui);
        yield return TestTavern(ui);
        yield return TestLibrary(ui);
        yield return TestMapRoom(ui);
        yield return TestDeckManager(ui);
        yield return TestHeroDetail(ui);
        yield return TestFormationScreen(ui);

        ui.ShowGuildScreen();
        yield return new WaitForSeconds(0.2f);
    }

    /// <summary>
    /// Taverna: contratar já funcionava, renovar a lista não existia na cena.
    /// O teste cobra as duas coisas e confirma que o ouro se move.
    /// </summary>
    IEnumerator TestTavern(UIManager ui)
    {
        ui.ShowTavern();
        yield return new WaitForSeconds(0.4f);

        var tav = TavernManager.Instance;
        if (tav == null)
        {
            Line("FALHA: TavernManager não existe na cena — a taverna não funciona");
            ui.CloseTavern();
            yield break;
        }

        bool visivel = ui.tavernPanel != null && ui.tavernPanel.activeInHierarchy;
        Line($"taverna visível: {visivel} | candidatos: {CountRows(tav.recruitContainer)}");

        if (tav.refreshButton == null)
            Line("FALHA: taverna sem botão de renovar candidatos");
        else
        {
            int ouroAntes = GuildManager.Instance.gold;
            string antes = PrimeiroCandidato(tav);

            ReportarAlcancavel(tav.refreshButton);
            tav.refreshButton.onClick.Invoke();
            yield return new WaitForSeconds(0.3f);

            Line($"renovar candidatos: ouro {ouroAntes} → {GuildManager.Instance.gold}"
               + $" | '{antes}' → '{PrimeiroCandidato(tav)}'");
        }

        int rosterAntes = GuildManager.Instance.roster.Count;
        Button contratar = FirstEnabledButton(tav.recruitContainer);

        if (contratar == null)
            Line("nenhum candidato contratável (roster cheio ou sem ouro)");
        else
        {
            contratar.onClick.Invoke();
            yield return new WaitForSeconds(0.3f);
            Line($"contratação: roster {rosterAntes} → {GuildManager.Instance.roster.Count}");
        }

        yield return Capture("sala_taverna");

        ui.CloseTavern();
        yield return new WaitForSeconds(0.2f);
    }

    static string PrimeiroCandidato(TavernManager tav)
    {
        if (tav.recruitContainer == null || tav.recruitContainer.childCount == 0) return "";

        var label = tav.recruitContainer.GetChild(0).GetComponentInChildren<TMPro.TMP_Text>(true);
        return label == null ? "" : StripTags(label.text).Replace("\n", " ");
    }

    IEnumerator TestLibrary(UIManager ui)
    {
        ui.ShowLibrary();
        yield return new WaitForSeconds(0.4f);

        bool visivel = ui.libraryPanel != null && ui.libraryPanel.activeInHierarchy;
        Line($"biblioteca visível: {visivel}");

        var lib = LibraryManager.Instance;
        if (lib == null)
            Line("FALHA: LibraryManager ausente");
        else
        {
            Line($"cartas à venda: {CountRows(lib.cardsContainer)}");

            if (lib.closeButton != null) ReportarAlcancavel(lib.closeButton);
            else Line("FALHA: biblioteca sem botão de fechar");
        }

        yield return Capture("sala_biblioteca");

        ui.CloseLibrary();
        yield return new WaitForSeconds(0.2f);
        Line($"após fechar: guilda visível={(ui.guildPanel != null && ui.guildPanel.activeInHierarchy)}");
    }

    IEnumerator TestMapRoom(UIManager ui)
    {
        ui.ShowMapRoom();
        yield return new WaitForSeconds(0.4f);

        bool visivel = ui.mapRoomPanel != null && ui.mapRoomPanel.activeInHierarchy;
        Line($"sala de mapas visível: {visivel}");

        var mr = MapRoomManager.Instance;
        if (mr == null)
            Line("FALHA: MapRoomManager ausente");
        else
        {
            int ouroAntes = GuildManager.Instance.gold;

            if (mr.buyScoutingButton != null && mr.buyScoutingButton.interactable)
            {
                mr.buyScoutingButton.onClick.Invoke();
                yield return new WaitForSeconds(0.2f);
                Line($"contratar batedor: ouro {ouroAntes} → {GuildManager.Instance.gold}"
                   + $" | batedores {mr.ScoutingCharges}/{mr.MaxScouting}");
            }
            else Line("botão de batedor indisponível");

            if (mr.closeButton != null) ReportarAlcancavel(mr.closeButton);
        }

        yield return Capture("sala_mapas");

        ui.CloseMapRoom();
        yield return new WaitForSeconds(0.2f);
    }

    IEnumerator TestDeckManager(UIManager ui)
    {
        ui.ShowDeckManager();
        yield return new WaitForSeconds(0.4f);

        bool visivel = ui.deckManagerPanel != null && ui.deckManagerPanel.activeInHierarchy;
        Line($"gerenciador de deck visível: {visivel}");

        var dm = DeckManager.Instance;
        if (dm == null)
            Line("FALHA: DeckManager ausente");
        else
        {
            Line($"heróis para escolher: {CountRows(dm.heroSelectionContainer)}"
               + $" | cartas no deck exibido: {CountRows(dm.currentDeckContainer)}"
               + $" | coleção: {CountRows(dm.collectionContainer)}");

            Button primeiro = FirstEnabledButton(dm.heroSelectionContainer);
            if (primeiro != null)
            {
                primeiro.onClick.Invoke();
                yield return new WaitForSeconds(0.3f);
                Line($"após escolher um herói: cartas no deck={CountRows(dm.currentDeckContainer)}"
                   + $" | título='{TextOf(dm.heroNameText)}'");
            }
            else Line("FALHA: nenhum herói selecionável no gerenciador de deck");

            if (dm.closeButton != null) ReportarAlcancavel(dm.closeButton);
        }

        yield return Capture("tela_deck");

        ui.CloseDeckManager();
        yield return new WaitForSeconds(0.2f);
    }

    /// <summary>
    /// Ficha do herói. Era inalcançável: o componente mora num painel desativado,
    /// então o singleton ficava nulo e o clique no retrato não fazia nada.
    /// </summary>
    IEnumerator TestHeroDetail(UIManager ui)
    {
        if (GuildManager.Instance.roster.Count == 0)
        {
            Line("ficha do herói: pulada (roster vazio)");
            yield break;
        }

        HeroData alvo = GuildManager.Instance.roster[0];

        // Pelo caminho do jogador: o retrato na barra inferior.
        var retrato = UnityEngine.Object.FindObjectOfType<PartyMemberCard>();
        if (retrato == null)
            Line("nenhum retrato de herói na barra inferior — abrindo pelo UIManager");
        else
        {
            var btn = retrato.GetComponent<Button>();
            if (btn != null)
            {
                ReportarAlcancavel(btn);
                btn.onClick.Invoke();
                yield return new WaitForSeconds(0.4f);
            }
        }

        bool abriu = ui.heroDetailPanel != null && ui.heroDetailPanel.activeInHierarchy;

        if (!abriu)
        {
            Line("clique no retrato não abriu a ficha — tentando pelo UIManager");
            ui.ShowHeroDetail(alvo);
            yield return new WaitForSeconds(0.4f);
            abriu = ui.heroDetailPanel != null && ui.heroDetailPanel.activeInHierarchy;
        }

        Line($"ficha do herói visível: {abriu}");

        var hd = HeroDetailPanel.Instance;
        if (hd == null) Line("FALHA: HeroDetailPanel.Instance nulo — ficha inalcançável");
        else Line($"herói exibido: '{(hd.CurrentHero == null ? "nenhum" : hd.CurrentHero.heroName)}'"
                + $" | nome no painel: '{TextOf(hd.heroNameText)}'"
                + $" | estresse: '{TextOf(hd.moraleText)}'");

        yield return Capture("ficha_heroi");

        // Confirma que a ficha continua aberta no frame seguinte: o Start() dela
        // chamava HidePanel() e fechava tudo logo após a primeira abertura.
        yield return new WaitForSeconds(0.6f);
        bool continuaAberta = ui.heroDetailPanel != null && ui.heroDetailPanel.activeInHierarchy;
        Line($"ficha ainda aberta após 1 frame: {continuaAberta}"
           + (continuaAberta ? "" : "  <<< FALHA: fechou sozinha"));

        if (hd != null && hd.closeButton != null)
        {
            ReportarAlcancavel(hd.closeButton);
            hd.closeButton.onClick.Invoke();
            yield return new WaitForSeconds(0.3f);
            Line($"após fechar a ficha: oculta={(ui.heroDetailPanel != null && !ui.heroDetailPanel.activeInHierarchy)}");
        }
        else Line("FALHA: ficha do herói sem botão de fechar");
    }

    /// <summary>
    /// Percorre a preparação até o passo 2 e mexe na formação pelas setas reais.
    /// É o único jeito de saber que a coluna da formação aparece, se preenche e
    /// reordena — nada disso é visível nos números da jornada.
    /// </summary>
    IEnumerator TestFormationScreen(UIManager ui)
    {
        var qs = QuestSelectionUI.Instance;
        if (qs == null)
        {
            Line("tela de formação: QuestSelectionUI ausente");
            yield break;
        }

        ui.ShowQuestSelection();
        qs.RefreshAllData();
        yield return new WaitForSeconds(0.3f);

        // Passo 1: escolher a primeira missão e avançar.
        Button quest = FirstEnabledButton(qs.questListContainer);
        if (quest == null)
        {
            Line("tela de formação: nenhuma missão clicável");
            yield break;
        }

        quest.onClick.Invoke();
        yield return new WaitForSeconds(0.2f);

        yield return Capture("preparacao_passo1_missoes");

        if (qs.nextButton1 != null && qs.nextButton1.interactable)
            qs.nextButton1.onClick.Invoke();
        yield return new WaitForSeconds(0.3f);

        // Passo 2: marcar todos os heróis disponíveis.
        int marcados = 0;
        if (qs.partySelectionContainer != null)
        {
            foreach (Transform child in qs.partySelectionContainer)
            {
                var toggle = child.GetComponentInChildren<Toggle>(true);
                if (toggle == null) continue;
                toggle.isOn = true;
                marcados++;
            }
        }

        yield return new WaitForSeconds(0.3f);

        bool visivel = qs.formationPanel != null && qs.formationPanel.activeInHierarchy;
        int linhas = FormationOrder(qs).Split(',').Count(s => s.Length > 0);

        Line($"tela de formação visível: {visivel} | heróis marcados: {marcados} | posições na coluna: {linhas}");
        Line($"ordem inicial: {FormationOrder(qs)}");

        if (linhas == 0 && marcados > 0)
            Line("FALHA: heróis selecionados mas a coluna de formação ficou vazia");

        yield return Capture("preparacao_formacao");

        // Exercita uma seta: a ordem tem de mudar de fato.
        string antes = FormationOrder(qs);
        Button seta = FormationArrow(qs, "Btn_Down");
        if (seta == null)
        {
            Line("tela de formação: nenhuma seta habilitada");
        }
        else
        {
            seta.onClick.Invoke();
            yield return new WaitForSeconds(0.3f);

            string depois = FormationOrder(qs);
            Line($"reordenar com ▼: '{antes}' → '{depois}'");

            if (antes == depois)
                Line("FALHA: a seta não mudou a ordem da formação");

            yield return Capture("preparacao_formacao_reordenada");
        }

        // Passo 3: é onde ficam as provisões e o resumo da formação, e era a única
        // tela da preparação que nenhuma captura mostrava.
        Button principal = FirstEnabledButton(qs.partySelectionContainer, "MainButton");
        if (principal != null)
        {
            principal.onClick.Invoke();
            yield return new WaitForSeconds(0.2f);
        }

        if (qs.nextButton2 != null && qs.nextButton2.interactable)
        {
            qs.nextButton2.onClick.Invoke();
            yield return new WaitForSeconds(0.3f);

            Line($"passo 3 visível: {(qs.step3Panel != null && qs.step3Panel.activeInHierarchy)}"
               + $" | deck principal: '{TextOf(qs.selectedDeckNameText)}'"
               + $" | resumo da formação: '{TextOf(qs.teamSummaryText)}'");

            if (qs.teamSummaryText != null && string.IsNullOrEmpty(StripTags(qs.teamSummaryText.text)))
                Line("FALHA: o resumo da formação do passo 3 ficou vazio");

            yield return Capture("preparacao_passo3_deck");
        }
        else
        {
            Line("passo 3: botão de avançar indisponível — captura não feita");
        }
    }

    static string TextOf(TMPro.TMP_Text text)
    {
        return text == null ? "<sem referência>" : StripTags(text.text).Replace("\n", " / ");
    }

    /// <summary>
    /// Por que um rótulo não aparece na tela. As salas ganharam rolagem e o texto
    /// das listas sumiu — as caixas ficaram, os rótulos não. Fora do Play Mode a
    /// geometria, a cor e o material do stencil estavam todos certos, então o que
    /// falta medir é o estado de um rótulo criado pelo manager, em execução.
    /// </summary>
    void DumpLabelVisivel(Transform container)
    {
        if (container == null) { Line("diagnóstico: container nulo"); return; }

        TMPro.TMP_Text label = container.GetComponentInChildren<TMPro.TMP_Text>(true);
        if (label == null) { Line("diagnóstico: nenhum rótulo na lista"); return; }

        var rt = label.rectTransform;
        var mat = label.materialForRendering;

        Line($"diagnóstico do rótulo '{StripTags(label.text).Split('\n')[0]}':");
        Line($"  rect={rt.rect.size} ativo={label.gameObject.activeInHierarchy} enabled={label.enabled}");
        Line($"  cor={label.color} alphaCR={label.canvasRenderer.GetAlpha():0.00} chars={label.textInfo.characterCount}");
        Line($"  fonte={(label.font != null ? label.font.name : "NULA")} corpo={label.fontSize:0}");
        Line($"  material={(mat != null ? mat.name : "NULO")}");

        if (mat != null && mat.HasProperty("_Stencil"))
            Line($"  stencil id={mat.GetFloat("_Stencil")} comp={mat.GetFloat("_StencilComp")} readMask={mat.GetFloat("_StencilReadMask")}");
        else
            Line("  material sem _Stencil — não está sob máscara");

        // A máscara que de fato recorta este rótulo, e o que ela grava no stencil.
        var mask = label.GetComponentInParent<UnityEngine.UI.Mask>();
        if (mask != null)
        {
            var maskImg = mask.GetComponent<UnityEngine.UI.Image>();
            Line($"  máscara={mask.name} ativa={mask.isActiveAndEnabled} showGraphic={mask.showMaskGraphic}"
               + $" sprite={(maskImg != null && maskImg.sprite != null ? maskImg.sprite.name : "nenhum")}"
               + $" alphaImg={(maskImg != null ? maskImg.color.a.ToString("0.00") : "-")}");
        }
        else
        {
            Line("  sem Mask ancestral");
        }

        // Compara com uma Image irmã: elas aparecem na captura, o texto não.
        var img = label.transform.parent != null
            ? label.transform.parent.GetComponent<UnityEngine.UI.Image>() : null;
        if (img != null)
            Line($"  Image irmã: material={img.materialForRendering.name}");
    }

    /// <summary>Nomes na coluna da formação, na ordem em que estão desenhados.</summary>
    static string FormationOrder(QuestSelectionUI qs)
    {
        if (qs.formationContainer == null) return "";

        var nomes = new List<string>();
        foreach (Transform child in qs.formationContainer)
        {
            if (!child.name.StartsWith("Slot_")) continue;

            var label = child.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (label == null) continue;

            // O rótulo é "3. 🏹 Sera  Caçador": o nome é o terceiro pedaço.
            string[] partes = StripTags(label.text)
                .Split(new[] { ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            nomes.Add(partes.Length > 2 ? partes[2] : label.name);
        }

        return string.Join(",", nomes);
    }

    static Button FormationArrow(QuestSelectionUI qs, string nome)
    {
        if (qs.formationContainer == null) return null;

        foreach (Transform child in qs.formationContainer)
        {
            Transform arrow = child.Find(nome);
            if (arrow == null) continue;

            var btn = arrow.GetComponent<Button>();
            if (btn != null && btn.interactable) return btn;
        }

        return null;
    }

    IEnumerator TestMarket(UIManager ui)
    {
        var market = MarketManager.Instance;
        if (market == null) yield break;

        ui.ShowMarket();
        yield return new WaitForSeconds(0.3f);

        Line($"mercado visível: {(ui.marketPanel != null && ui.marketPanel.activeInHierarchy)}");
        Line($"itens na prateleira: {CountRows(market.itemContainer)}");
        DumpLabelVisivel(market.itemContainer);
        yield return Capture("sala_mercado");

        int ouroAntes = GuildManager.Instance.gold;
        int racoesAntes = market.StockedRations;

        Button comprar = FirstEnabledButton(market.itemContainer);
        if (comprar == null)
        {
            Line("FALHA: nenhum item comprável no mercado");
        }
        else
        {
            comprar.onClick.Invoke();
            yield return new WaitForSeconds(0.2f);
            Line($"compra: ouro {ouroAntes} → {GuildManager.Instance.gold} | rações estocadas {racoesAntes} → {market.StockedRations}");
        }

        ui.CloseMarket();
        yield return new WaitForSeconds(0.2f);
    }

    IEnumerator TestForge(UIManager ui)
    {
        var forge = ForgeManager.Instance;
        if (forge == null) yield break;

        ui.ShowForge();
        yield return new WaitForSeconds(0.3f);

        Line($"forja visível: {(ui.forgePanel != null && ui.forgePanel.activeInHierarchy)}");
        Line($"heróis na bancada: {CountRows(forge.heroContainer)}");
        yield return Capture("sala_forja");

        HeroData alvo = GuildManager.Instance.roster.FirstOrDefault(h => h != null && !h.isDead);
        if (alvo == null)
        {
            Line("pulada: roster sem heróis vivos");
            yield break;
        }

        int armaAntes = alvo.weaponLevel;
        int hpMaxAntes = alvo.maxHp;

        Button upgrade = FirstEnabledButton(forge.heroContainer);
        if (upgrade == null)
        {
            Line("FALHA: nenhuma melhoria disponível na forja");
        }
        else
        {
            upgrade.onClick.Invoke();
            yield return new WaitForSeconds(0.2f);

            Line($"melhoria em {alvo.heroName}: arma {armaAntes} → {alvo.weaponLevel}"
               + $" | HP máx {hpMaxAntes} → {alvo.maxHp}"
               + $" | bônus de dano das cartas dele: +{ForgeManager.WeaponBonus(alvo)}");
        }

        ui.CloseForge();
        yield return new WaitForSeconds(0.2f);
    }

    IEnumerator TestCemetery(UIManager ui)
    {
        var cemetery = CemeteryManager.Instance;
        if (cemetery == null) yield break;

        ui.ShowCemetery();
        yield return new WaitForSeconds(0.3f);

        int tumbas = CountRows(cemetery.graveContainer);
        Line($"cemitério visível: {(ui.cemeteryPanel != null && ui.cemeteryPanel.activeInHierarchy)}");
        Line($"tumbas listadas: {tumbas} (caídos registrados: {GuildManager.Instance.fallenHeroes.Count})");

        if (tumbas == 0)
            Line("estado vazio exibido — ainda não morreu ninguém nesta sessão");

        yield return Capture("sala_cemiterio");

        ui.CloseCemetery();
        yield return new WaitForSeconds(0.2f);
    }

    /// <summary>
    /// Captura a tela atual em Assets/Screenshots.
    ///
    /// Vale a pena estar aqui dentro: entrar em Play Mode por fora e capturar
    /// depois não funciona — sem foco na janela o Play Mode congela e o pedido de
    /// screenshot expira. Durante o probe o jogo está rodando de verdade.
    /// </summary>
    IEnumerator Capture(string nome)
    {
        string pasta = Path.Combine(Application.dataPath, "Screenshots");
        Directory.CreateDirectory(pasta);

        string caminho = Path.Combine(pasta, nome + ".png");
        ScreenCapture.CaptureScreenshot(caminho);

        // A captura só acontece no fim do frame, e o arquivo demora a aparecer.
        yield return new WaitForSeconds(0.6f);

        Line($"captura: {nome}.png {(File.Exists(caminho) ? "ok" : "(ainda gravando)")}");
    }

    static int CountRows(Transform container)
    {
        if (container == null) return 0;

        int total = 0;
        foreach (Transform child in container)
            if (child.gameObject.activeSelf) total++;

        return total;
    }

    static Button FirstEnabledButton(Transform container)
    {
        return FirstEnabledButton(container, null);
    }

    /// <summary>
    /// Primeiro botão clicável dos filhos do container. Com <paramref name="name"/>
    /// preenchido, só considera botões com aquele nome — o card de herói é ele
    /// próprio um Button, e sem o filtro sempre voltava o card em vez do
    /// "USAR DECK" de dentro dele.
    /// </summary>
    static Button FirstEnabledButton(Transform container, string name)
    {
        if (container == null) return null;

        foreach (Transform child in container)
        {
            foreach (var btn in child.GetComponentsInChildren<Button>(true))
            {
                if (!btn.interactable || !btn.gameObject.activeInHierarchy) continue;
                if (name != null && btn.gameObject.name != name) continue;
                return btn;
            }
        }

        return null;
    }

    #endregion

    #region Fim de jornada

    /// <summary>
    /// Vencer a jornada e conseguir voltar para a guilda.
    ///
    /// A jornada automática quase sempre termina em derrota (a party morre antes
    /// do último nó), então o caminho da vitória passava sem teste — e é nele que
    /// o jogador relatou ficar preso, sem botão para sair da tela.
    ///
    /// O desfecho é forçado por reflexão em vez de jogado até o fim: o que se quer
    /// medir é a saída da tela, não a rota até ela.
    /// </summary>
    IEnumerator TestVictoryExit()
    {
        var jm = JourneyManager.Instance;
        var ui = UIManager.Instance;

        if (jm == null || ui == null)
        {
            Line("FALHA: JourneyManager ou UIManager ausente");
            yield break;
        }

        // A jornada anterior pode ter matado todo mundo, e RegisterDeath tira o
        // herói do roster — depois de uma party dizimada ele fica vazio. Repor é
        // parte do teste: sem grupo, o desfecho de vitória nem chega a acontecer.
        foreach (var hero in GuildManager.Instance.roster)
        {
            hero.isDead = false;
            hero.isOnDeathsDoor = false;
            hero.currentHp = hero.maxHp;
            hero.stress = 0f;
        }

        HeroClass[] reposicao = { HeroClass.Warrior, HeroClass.Mage, HeroClass.Healer, HeroClass.Hunter };
        int faltam = Mathf.Max(0, 4 - GuildManager.Instance.roster.Count);
        for (int i = 0; i < faltam; i++)
        {
            // Direto na lista: contratar pela porta da frente cobraria salário e
            // esbarraria no ouro disponível, que não é o objeto deste teste.
            GuildManager.Instance.roster.Add(
                HeroFactory.CreateHero($"Reserva {i + 1}", reposicao[i % reposicao.Length], 2));
        }

        var party = GuildManager.Instance.roster.Take(4).ToList();

        if (party.Count == 0)
        {
            Line("FALHA: sem heróis para testar a saída da jornada");
            yield break;
        }

        Line($"grupo do teste: {string.Join(", ", party.Select(h => h.heroName))}"
           + (faltam > 0 ? $" ({faltam} repostos — a jornada anterior esvaziou o roster)" : ""));
        QuestData quest = QuestManager.Instance != null && QuestManager.Instance.GetQuests().Count > 0
            ? QuestManager.Instance.GetQuests()[0]
            : QuestGenerator.GenerateQuests(1, 2)[0];

        var built = JourneyDeckBuilder.Build(party[0], party);

        jm.StartJourney(quest, party, built.deck, -1, -1, built.ownership);
        yield return new WaitForSeconds(0.4f);

        Line($"jornada iniciada para o teste de saída: painel ativo={jm.journeyPanel.activeSelf}");

        // Força o desfecho vitorioso.
        var endJourney = typeof(JourneyManager).GetMethod("EndJourney",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (endJourney == null)
        {
            Line("FALHA: EndJourney não encontrado por reflexão — teste de saída não pôde rodar");
            yield break;
        }

        endJourney.Invoke(jm, new object[] { true });
        yield return new WaitForSeconds(0.6f);

        GameObject popup = ui.resultPopup;
        bool popupVisivel = popup != null && popup.activeInHierarchy;
        Line($"popup de resultado visível após vencer: {popupVisivel}");

        if (popup != null)
        {
            Line($"  escala do popup: {popup.transform.localScale.x:0.00} (0 = invisível na prática)");
            var cg = popup.GetComponent<CanvasGroup>();
            if (cg != null) Line($"  alpha={cg.alpha:0.00} blocksRaycasts={cg.blocksRaycasts}");
        }

        Button fechar = ui.resultCloseButton;
        Line($"botão de fechar: {(fechar == null ? "SEM REFERÊNCIA" : fechar.name)}"
           + (fechar != null ? $" interativo={fechar.interactable} ativo={fechar.gameObject.activeInHierarchy}" : ""));

        if (!popupVisivel)
            Line("FALHA: venceu a jornada e o popup de resultado não apareceu — jogador fica sem saída");

        // Estar ativo não é estar clicável: o popup nasce dentro de "Background",
        // que é desenhado antes das telas de jornada e combate, e o fundo delas
        // engolia o clique. Só o raycast revela isso — invocar o onClick por
        // código passa por cima do problema e o teste dava verde.
        if (fechar != null)
            ReportarAlcancavel(fechar);

        if (fechar == null || !fechar.gameObject.activeInHierarchy)
        {
            Line("FALHA: sem botão de fechar acessível — a tela de jornada não tem saída");
            yield return Capture("fim_jornada_travado");
            yield break;
        }

        yield return Capture("fim_jornada_vitoria");

        // O clique que devolve o jogador à guilda.
        fechar.onClick.Invoke();
        yield return new WaitForSeconds(0.8f);

        bool jornadaFechada = jm.journeyPanel == null || !jm.journeyPanel.activeSelf;
        bool guildaAberta = ui.guildPanel != null && ui.guildPanel.activeInHierarchy;
        bool popupFechado = popup == null || !popup.activeInHierarchy;

        Line($"após fechar: jornada oculta={jornadaFechada} | guilda visível={guildaAberta} | popup fechado={popupFechado}");

        if (!jornadaFechada || !guildaAberta)
            Line("FALHA: vencer a jornada não devolve o jogador à guilda — este é o travamento relatado");
        else
            Line("saída da jornada vitoriosa: OK");

        yield return Capture("fim_jornada_voltou");
    }

    /// <summary>
    /// O jogador consegue clicar neste botão? Dispara um raycast de UI no centro
    /// dele e verifica quem está por cima — que é o que o mouse acertaria.
    /// </summary>
    void ReportarAlcancavel(Button alvo)
    {
        var es = UnityEngine.EventSystems.EventSystem.current;
        if (es == null)
        {
            Line("  alcançável pelo clique: não verificável (sem EventSystem)");
            return;
        }

        var rect = alvo.transform as RectTransform;
        Vector3[] cantos = new Vector3[4];
        rect.GetWorldCorners(cantos);
        Vector3 centro = (cantos[0] + cantos[2]) * 0.5f;

        var cam = alvo.GetComponentInParent<Canvas>()?.worldCamera;
        Vector2 tela = cam != null
            ? RectTransformUtility.WorldToScreenPoint(cam, centro)
            : (Vector2)centro;

        var dados = new UnityEngine.EventSystems.PointerEventData(es) { position = tela };
        var hits = new List<UnityEngine.EventSystems.RaycastResult>();
        es.RaycastAll(dados, hits);

        if (hits.Count == 0)
        {
            Line("  FALHA: nada recebe o clique nessa posição — o botão é inalcançável");
            return;
        }

        GameObject topo = hits[0].gameObject;
        bool ehOAlvo = topo == alvo.gameObject || topo.transform.IsChildOf(alvo.transform);

        Line($"  alcançável pelo clique: {ehOAlvo} (quem está por cima: {topo.name})");

        if (!ehOAlvo)
            Line($"  FALHA: '{topo.name}' cobre o botão — o jogador não consegue fechar o popup");
    }

    #endregion

    #region Jornada

    IEnumerator RunJourney()
    {
        var jm = JourneyManager.Instance;

        var party = GuildManager.Instance.roster.Where(h => !h.isDead).Take(4).ToList();
        if (party.Count == 0)
        {
            Line("FALHA: roster vazio, impossível iniciar jornada");
            yield break;
        }

        // A ordem da lista é a formação. O teste parte da ordem recomendada, que
        // é a mesma que a tela de preparação sugere ao jogador.
        party = SortedByPreference(party);

        QuestData quest = QuestManager.Instance != null && QuestManager.Instance.GetQuests().Count > 0
            ? QuestManager.Instance.GetQuests()[0]
            : QuestGenerator.GenerateQuests(1, 2)[0];

        // Mesmo caminho da tela de preparação: principal + cartas dos companheiros.
        var built = JourneyDeckBuilder.Build(party[0], party);
        DeckData deck = built.deck;

        Line($"missão: {quest.questName}");
        Line($"party: {string.Join(", ", party.Select(h => h.heroName))}");
        Line($"baralho da jornada: {deck.cards.Count} cartas");
        foreach (var linha in built.breakdown)
            Line($"  {linha}");

        Line("formação:");
        foreach (var h in party)
            Line($"  {StripTags(PartyFormation.DescribePlacement(h, party))}");

        if (SemFormacao)
        {
            PartyFormation.Enabled = false;
            Line("MODO COMPARACAO: formação desligada (alvo uniforme, sem bônus de fileira nem penalidade de carta)");
        }

        int errosAntes = errors.Count;

        // As mesmas provisões que a tela de preparação entrega, e não o padrão
        // interno do StartJourney: com -1 o teste saía com 10–14 rações e 5–7
        // tochas, folga que o jogador não tem, e media uma jornada mais fácil que
        // a real.
        var prep = UnityEngine.Object.FindObjectOfType<QuestSelectionUI>(true);
        int racoes = prep != null ? prep.baseRations : 8;
        int tochas = prep != null ? prep.baseTorches : 4;
        Line($"provisões da preparação: {racoes} rações, {tochas} tochas (missão de {quest.GetActualDuration()} dias na estimativa)");

        // A propriedade das cartas segue junto: sem ela o combate não teria como
        // saber de quem é cada carta, e a formação não afetaria nada.
        jm.StartJourney(quest, party, deck, racoes, tochas, SemFormacao ? null : built.ownership);
        yield return new WaitForSeconds(0.4f);

        if (jm.journeyPanel == null)
        {
            Line("FALHA: journeyPanel não atribuído — jornada não pode ser exibida");
            yield break;
        }

        Line($"painel de jornada ativo: {jm.journeyPanel.activeSelf}");

        // Dirige a jornada clicando nos botões reais, como um jogador faria.
        int guard = 0;
        string lastDay = jm.dayText != null ? jm.dayText.text : "";
        int clicksSemProgresso = 0;

        // Acompanha o HP do grupo a cada volta para separar o desgaste do combate
        // do desgaste da estrada (fome, eventos, clima).
        bool emCombateAntes = false;
        bool combateCapturado = false;
        int hpAnterior = PartyHp(party);

        // A jornada sai da tela enquanto o combate acontece, então "painel da
        // jornada inativo" não significa mais que a jornada acabou — significa
        // que ela pode estar apenas cedendo a tela ao combate.
        while ((jm.journeyPanel.activeSelf || IsCombatOpen()) && guard < 600)
        {
            guard++;

            bool emCombateAgora = IsCombatOpen();
            if (emCombateAgora && !emCombateAntes) combatsFought++;

            int hpAgora = PartyHp(party);
            int perdido = hpAnterior - hpAgora;
            if (perdido > 0)
            {
                if (emCombateAntes) hpLostInCombat += perdido;
                else hpLostOutsideCombat += perdido;
            }
            hpAnterior = hpAgora;
            emCombateAntes = emCombateAgora;

            // Detecta laço improdutivo: cliques que não fazem o dia avançar.
            string nowDay = jm.dayText != null ? jm.dayText.text : "";
            if (nowDay != lastDay)
            {
                lastDay = nowDay;
                clicksSemProgresso = 0;
            }
            else if (clicksSemProgresso > 40 && !IsCombatOpen())
            {
                Line($"TRAVADO: {clicksSemProgresso} ações sem o dia avançar (parado em '{nowDay}')");
                Line($"         journeyPanel.activeSelf={jm.journeyPanel.activeSelf}");
                DumpStuckState(jm);
                break;
            }

            // Popups bloqueiam o fluxo até serem fechados.
            if (ClickPopupButton())
            {
                yield return new WaitForSeconds(0.2f);
                continue;
            }

            // Se um combate abriu, joga o combate até o fim.
            if (IsCombatOpen())
            {
                // Uma captura do primeiro combate: é a única tela onde as cartas
                // aparecem em tamanho de jogo, e nenhuma outra imagem as mostrava.
                if (!combateCapturado)
                {
                    combateCapturado = true;
                    yield return Capture("combate_cartas");
                }

                PlayCombatStep();
                yield return new WaitForSeconds(0.12f);
                continue;
            }

            Button choice = FindFirstChoiceButton(jm);
            if (choice != null)
            {
                choice.onClick.Invoke();
                eventsResolved++;
                clicksSemProgresso++;
                yield return new WaitForSeconds(0.15f);
                continue;
            }

            // Rota ramificada: sem escolhas de evento na tela, o que a jornada
            // espera é que o jogador escolha um nó no mapa.
            if (ClickMapNode())
            {
                routeChoices++;
                clicksSemProgresso++;
                yield return new WaitForSeconds(0.15f);
                continue;
            }

            // Sem UI de escolhas, o botão de turno é a saída prevista no código.
            if (jm.endTurnButton != null && jm.endTurnButton.interactable)
            {
                jm.endTurnButton.onClick.Invoke();
                eventsResolved++;
                yield return new WaitForSeconds(0.15f);
                continue;
            }

            yield return null;
        }

        Line($"eventos resolvidos por clique: {eventsResolved}");
        Line($"escolhas de rota no mapa: {routeChoices}");
        Line($"ações de combate executadas: {combatTurns}");
        Line($"cartas jogadas no combate: {cardsPlayed}");
        Line($"vezes que a opção de combate apareceu: {combatOffers}");
        Line($"combates travados: {combatsFought}");
        Line($"HP perdido em combate: {hpLostInCombat} | fora de combate: {hpLostOutsideCombat}");

        // De onde vem o desgaste da estrada. As provisões são dimensionadas pelos
        // DIAS da missão, mas cobradas a cada trecho — se estes números divergirem
        // muito, a fome deixa de ser escolha do jogador e vira aritmética.
        Line($"dias planejados: {jm.PlannedDays} | dias percorridos: {jm.DaysElapsed}"
           + $" | cobranças de manutenção: {jm.UpkeepTicks}");
        Line($"trechos com fome: {jm.StarvationTicks} (dano total {jm.StarvationDamage})"
           + $" | trechos no escuro: {jm.DarknessTicks}");
        Line($"painel ainda ativo ao fim: {jm.journeyPanel.activeSelf} (iterações: {guard})");

        if (guard >= 600)
            Line("ATENÇÃO: laço de segurança atingido — a jornada não terminou sozinha");

        Section("ESTADO DOS HEROIS APOS A JORNADA");
        foreach (var h in party)
        {
            Line($"  {h.heroName}: HP {h.currentHp}/{h.maxHp} | estresse {Mathf.RoundToInt(h.stress)} " +
                 $"| {(h.isDead ? "MORTO" : h.isOnDeathsDoor ? "beira da morte" : "vivo")} " +
                 $"| {MentalStateUtil.GetLabel(h.mentalState)}");
        }

        if (GuildManager.Instance != null)
            Line($"ouro final: {GuildManager.Instance.gold} | reputação: {GuildManager.Instance.reputation}");

        Line($"erros surgidos durante a jornada: {errors.Count - errosAntes}");

        // Estático sobrevive ao fim da corrotina; deixar desligado contaminaria
        // qualquer partida seguinte no mesmo Play Mode.
        PartyFormation.Enabled = true;
    }

    /// <summary>
    /// Fecha popups que travariam o fluxo. Usa activeSelf, não activeInHierarchy:
    /// um popup preso sob um pai desativado ainda precisa ser destravado aqui,
    /// e a diferença entre os dois é justamente o que queremos flagrar.
    /// </summary>
    bool ClickPopupButton()
    {
        var ui = UIManager.Instance;
        if (ui == null) return false;

        if (ui.resultPopup != null && ui.resultPopup.activeSelf && ui.resultCloseButton != null)
        {
            if (!ui.resultPopup.activeInHierarchy && !popupInvisibleReported)
            {
                popupInvisibleReported = true;
                Line("BUG: resultPopup foi ativado mas está sob um pai desativado — o jogador nunca o veria.");
            }
            ui.resultCloseButton.onClick.Invoke();
            return true;
        }

        if (ui.confirmPopup != null && ui.confirmPopup.activeSelf && ui.confirmYesButton != null)
        {
            ui.confirmYesButton.onClick.Invoke();
            return true;
        }

        return false;
    }

    void DumpStuckState(JourneyManager jm)
    {
        int botoes = 0;
        if (jm.choiceContainer != null)
            foreach (Transform c in jm.choiceContainer) botoes++;

        Line($"         botões no choiceContainer: {botoes}");

        var ui = UIManager.Instance;
        if (ui != null && ui.resultPopup != null)
            Line($"         resultPopup activeSelf={ui.resultPopup.activeSelf} inHierarchy={ui.resultPopup.activeInHierarchy}");
    }

    /// <summary>
    /// Escolhe uma rota clicando num nó habilitado do mapa. Só os alcançáveis
    /// ficam interactable, então basta pegar o primeiro que estiver ativo.
    /// </summary>
    bool ClickMapNode()
    {
        var map = JourneyMapUI.Instance;
        if (map == null || map.nodeContainer == null) return false;

        foreach (Transform child in map.nodeContainer)
        {
            if (!child.name.StartsWith("Node_")) continue;

            Button btn = child.GetComponent<Button>();
            if (btn == null || !btn.interactable || !child.gameObject.activeInHierarchy) continue;

            btn.onClick.Invoke();
            return true;
        }

        return false;
    }

    bool IsCombatOpen()
    {
        var cm = CombatManager.Instance;
        return cm != null && cm.combatPanel != null && cm.combatPanel.activeInHierarchy;
    }

    /// <summary>
    /// Um passo de combate: prioriza confirmar alvo, depois jogar carta,
    /// e por fim encerrar o turno.
    /// </summary>
    /// <summary>
    /// Joga uma carta por turno.
    ///
    /// Cartas deixaram de ser clicáveis — agora se joga arrastando. Simular o
    /// gesto de ponteiro aqui seria frágil, então o teste percorre o mesmo ponto
    /// de entrada que o drop usa (`TryPlayCardOnAnyTarget`), exercitando as
    /// mesmas regras de energia e de alvo.
    /// </summary>
    void PlayCombatStep()
    {
        var cm = CombatManager.Instance;
        if (cm == null) return;

        combatTurns++;

        if (cm.handContainer != null)
        {
            foreach (Transform child in cm.handContainer)
            {
                var drag = child.GetComponent<CardDragHandler>();
                if (drag == null || drag.Card == null || !child.gameObject.activeInHierarchy) continue;
                if (!cm.CanAffordCard(drag.Card)) continue;

                if (cm.TryPlayCardOnAnyTarget(drag.Card))
                {
                    cardsPlayed++;
                    return;
                }
            }
        }

        if (cm.endTurnButton != null && cm.endTurnButton.interactable)
            cm.endTurnButton.onClick.Invoke();
    }

    /// <summary>
    /// Escolha a clicar no evento atual.
    ///
    /// Prefere explicitamente "Enfrentar em combate" quando a opção existe: o
    /// combate é o caminho que precisa ser exercitado, e depender da ordem dos
    /// filhos deixava isso à mercê de qualquer mudança na montagem da lista.
    /// </summary>
    Button FindFirstChoiceButton(JourneyManager jm)
    {
        if (jm.choiceContainer == null) return null;

        Button primeiro = null;

        foreach (Transform child in jm.choiceContainer)
        {
            Button b = child.GetComponent<Button>();
            if (b == null || !b.interactable || !b.gameObject.activeInHierarchy) continue;

            if (primeiro == null) primeiro = b;

            var txt = b.GetComponentInChildren<TMPro.TMP_Text>();
            if (txt != null && txt.text.Contains("Enfrentar em combate"))
            {
                combatOffers++;
                return b;
            }
        }

        return primeiro;
    }

    #endregion

    #region Auditoria

    /// <summary>
    /// Um popup dentro de um painel desativado nunca fica visível: SetActive(true)
    /// no próprio objeto não vence um pai inativo.
    /// </summary>
    void ReportPopup(string name, GameObject popup)
    {
        if (popup == null)
        {
            Line($"     {name}: NULO");
            return;
        }

        string path = popup.name;
        Transform t = popup.transform.parent;
        while (t != null)
        {
            path = $"{t.name} / {path}";
            t = t.parent;
        }

        // Algum ancestral desativado?
        string blocker = "nenhum";
        Transform p = popup.transform.parent;
        while (p != null)
        {
            if (!p.gameObject.activeSelf) { blocker = p.name; break; }
            p = p.parent;
        }

        Line($"     {name}: {path}");
        Line($"         activeSelf={popup.activeSelf} activeInHierarchy={popup.activeInHierarchy} ancestral_desativado={blocker}");
    }

    bool ReportSingleton(string name, UnityEngine.Object instance)
    {
        bool ok = instance != null;
        Line($"{(ok ? "ok  " : "AUSENTE")} {name}");
        return ok;
    }

    /// <summary>Lista campos públicos de referência que ficaram nulos no Inspector.</summary>
    void AuditInspector(MonoBehaviour target)
    {
        if (target == null) return;

        var missing = new List<string>();
        FieldInfo[] fields = target.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);

        foreach (var f in fields)
        {
            if (!typeof(UnityEngine.Object).IsAssignableFrom(f.FieldType)) continue;

            var value = f.GetValue(target) as UnityEngine.Object;
            if (value == null)
                missing.Add(f.Name);
        }

        if (missing.Count == 0)
            Line($"ok   {target.GetType().Name}: todas as referências ligadas");
        else
            Line($"     {target.GetType().Name}: {missing.Count} nulas → {string.Join(", ", missing)}");
    }

    #endregion

    #region Relatório

    void Section(string title)
    {
        report.AppendLine();
        report.AppendLine($"── {title} ──");
    }

    void Line(string line)
    {
        report.AppendLine(line);
    }

    void WriteReport()
    {
        string header = errors.Count == 0
            ? "PLAY MODE OK — nenhum erro capturado"
            : $"PLAY MODE COM {errors.Count} ERRO(S)";

        report.Insert(0, $"{header}\n(gerado em {DateTime.Now:yyyy-MM-dd HH:mm:ss})\n");

        string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "PlayModeReport.txt");

        try
        {
            File.WriteAllText(path, report.ToString(), Encoding.UTF8);
            Debug.Log($"Relatório de Play Mode salvo em: {path}\n\n{report}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Não foi possível salvar o relatório: {e.Message}\n\n{report}");
        }
    }

    #endregion
}
#endif
