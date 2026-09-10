#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
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

    /// <summary>Há quanto tempo um gatilho está esperando sem poder ser atendido.</summary>
    static double esperandoDesde;
    static bool avisouDaEspera;

    static void Tick()
    {
        if (EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + 1.0;

        string path = Path.Combine(PlayModeTestLauncher.ProjectRoot, TriggerFile);
        bool esperando = File.Exists(path);

        string bloqueio = Bloqueio();
        if (bloqueio != null)
        {
            // Um gatilho parado porque o Editor está ocupado era invisível: quem
            // esperava de fora só via o arquivo não sumir, sem nenhuma pista de
            // por quê. Um Editor preso em refresh de asset, por exemplo, engole
            // todos os gatilhos em silêncio e parece que a ferramenta quebrou.
            if (esperando)
            {
                if (esperandoDesde <= 0) esperandoDesde = EditorApplication.timeSinceStartup;

                if (!avisouDaEspera && EditorApplication.timeSinceStartup - esperandoDesde > 10.0)
                {
                    avisouDaEspera = true;
                    Debug.LogWarning($"Gatilho {TriggerFile} esperando há mais de 10s: {bloqueio}.");
                }
            }

            return;
        }

        esperandoDesde = 0;
        avisouDaEspera = false;

        if (!esperando) return;

        try { File.Delete(path); }
        catch { return; }

        Debug.Log("Trigger detectado — iniciando teste de Play Mode.");
        PlayModeTestLauncher.Launch();
    }

    /// <summary>O que impede o gatilho de ser atendido agora, ou null se nada impede.</summary>
    static string Bloqueio()
    {
        if (EditorApplication.isPlaying) return "o Editor está em Play Mode";
        if (EditorApplication.isPlayingOrWillChangePlaymode) return "o Editor está entrando ou saindo do Play Mode";
        if (EditorApplication.isCompiling) return "os scripts estão compilando";
        if (EditorApplication.isUpdating) return "a AssetDatabase está atualizando";

        return null;
    }
}

public class PlayModeProbe : MonoBehaviour
{
    private readonly StringBuilder report = new StringBuilder();
    private readonly List<string> errors = new List<string>();

    // Ruído de pacote de terceiros: registrado, mas fora da conta que decide se
    // a run passou. Ver RuidoConhecido.
    private readonly List<string> ignoredErrors = new List<string>();
    private int eventsResolved;
    private int combatTurns;
    private int routeChoices;
    private int cardsPlayed;
    private int combatOffers;
    private bool popupInvisibleReported;

    // A tela de balanço é a saída da jornada; registrada uma vez só, para o
    // relatório dizer como a jornada terminou sem repetir a linha a cada clique.
    private bool balancoVistoNaJornada;
    private bool balancoSemSaidaReportado;

    // De onde vem o desgaste. Sem separar combate de jornada, um relatório com a
    // party morta não diz se a culpa foi das lutas, da fome ou dos eventos.
    private int combatsFought;
    private int hpLostInCombat;
    private int hpLostOutsideCombat;

    void Awake()
    {
        Application.logMessageReceived += OnLog;

        // O teste joga uma jornada inteira e termina runs de propósito. Sem esta
        // trava, cada ciclo avançado gravaria por cima da partida real de quem
        // estiver jogando neste computador — rodar o teste não pode custar o
        // save do autor.
        SaveSystem.AutosaveSuspenso = true;

        // <b>Nenhum reimport enquanto o jogo roda.</b> O teste grava as capturas
        // dentro de Assets/Screenshots, e cada arquivo novo ali acorda o Asset
        // Pipeline **em pleno Play Mode**: ele reimporta a textura, descarrega e
        // recarrega assets e faz hot reload com o jogo no ar.
        //
        // Isso sempre foi frágil e passou anos sem cobrar. Cobrou em 21/08: com
        // dois palcos de RenderTexture vivos e 60 texturas de mapa recém
        // reimportadas, o refresh disparado por três capturas derrubou o Editor
        // com SIGSEGV — crash nativo, sem stack gerenciado, no meio de uma
        // jornada. A última coisa no log antes do sinal era o refresh.
        //
        // As capturas continuam sendo gravadas onde estavam; só entram no
        // AssetDatabase quando o teste acaba e o Editor volta a si.
        AssetDatabase.DisallowAutoRefresh();
        refreshSuspenso = true;
    }

    /// <summary>O auto-refresh foi desligado por nós? Só religa quem desligou.</summary>
    bool refreshSuspenso;

    void OnDestroy()
    {
        Application.logMessageReceived -= OnLog;
        SaveSystem.AutosaveSuspenso = false;

        // Religa mesmo se o teste morreu no meio: deixar o projeto sem
        // auto-refresh seria pior que o crash — o Editor pararia de enxergar
        // qualquer arquivo novo, em silêncio.
        if (refreshSuspenso)
        {
            AssetDatabase.AllowAutoRefresh();
            refreshSuspenso = false;
        }
    }

    /// <summary>
    /// Erros que não são do jogo e se repetem toda sessão. Contá-los no cabeçalho
    /// faz o relatório abrir com "PLAY MODE COM 2 ERRO(S)" mesmo quando nada
    /// quebrou — e um alarme que toca sempre para de ser ouvido.
    /// </summary>
    static readonly string[] RuidoConhecido =
    {
        // Pacote de terceiros que traz a mesma DLL em duas pastas.
        "Multiple plugins with the same name",
    };

    void OnLog(string message, string stackTrace, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
            return;

        string first = stackTrace?.Split('\n').FirstOrDefault() ?? "";
        string linha = $"[{type}] {message} | {first}";

        if (RuidoConhecido.Any(padrao => message.Contains(padrao)))
            ignoredErrors.Add(linha);
        else
            errors.Add(linha);
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
        ReportSingleton<TavernManager>("TavernManager", TavernManager.Instance);
        ReportSingleton<DeckManager>("DeckManager", DeckManager.Instance);

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

        // Um frasco em cada herói antes de partir: a cinta do combate só existe
        // se alguém do grupo carregar algum, e sem isto a tela nunca seria
        // exercitada — o teste passaria em silêncio sobre a metade nova do
        // sistema de itens.
        EquiparGrupoParaOTeste();

        if (hasJourney)
            yield return RunJourney();
        else
            Line("pulada: GuildManager ou JourneyManager ausentes na cena");

        Section("FIM DE JORNADA COM VITORIA");
        if (hasJourney)
            yield return TestVictoryExit();
        else
            Line("pulada: JourneyManager ausente na cena");

        Section("DIARIO DE UMA PARTIDA");

        // A guilda é a tela que o jogador mais vê e a única sem captura no
        // relatório — o que a deixava fora de toda revisão visual.
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowGuildScreen();
            yield return new WaitForSeconds(0.4f);
            yield return Capture("guilda");
        }

        yield return DiarioDaPartida();

        Section("O MOMENTO DA QUEBRA");
        yield return TestarMomentoDaQuebra();

        Section("O QUE CADA TELA OFERECE");
        yield return AuditarTelas();

        Section("CUSTO DE CADA ACAO, EM CLIQUES");
        yield return CustoDasAcoes();

        Section("A RUN (Fase 3)");
        yield return TestRun();

        Section("SAVE E MENUS");
        yield return TestSaveAndMenus();

        Section("ERROS CAPTURADOS");
        if (ignoredErrors.Count > 0)
            Line($"({ignoredErrors.Count} erro(s) de pacote de terceiros ignorados — ver RuidoConhecido)");

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

        // Religa antes de sair do Play Mode: as capturas e o relatório entram no
        // AssetDatabase com o jogo já parado, que é quando isso é seguro.
        if (refreshSuspenso)
        {
            AssetDatabase.AllowAutoRefresh();
            refreshSuspenso = false;
        }

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
        yield return TestLocationInfo(ui);
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

        // Clicar na ficha da fila agora só chama o candidato à mesa — quem
        // contrata é o botão da mesa. Com o clique antigo, o teste dizia
        // "roster 4 → 4" e passava, medindo uma contratação que não aconteceu.
        Button contratar = tav.hireButton != null && tav.hireButton.interactable
            ? tav.hireButton
            : null;

        if (contratar == null)
            Line("nenhum candidato contratável (roster cheio ou sem ouro)");
        else
        {
            ReportarAlcancavel(contratar);
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
        {
            Line("FALHA: LibraryManager ausente");
        }
        else
        {
            Line($"cartas à venda: {CountRows(lib.cardsContainer)}");
            Line($"herói na mesa: {(lib.NaMesa != null ? lib.NaMesa.heroName : "ninguém")}");
            Line($"heróis na fila: {CountRows(lib.heroContainer)}");

            if (lib.closeButton != null) ReportarAlcancavel(lib.closeButton);
            else Line("FALHA: biblioteca sem botão de fechar");

            // A compra agora entra no baralho do herói da mesa. É o que separa
            // esta sala da tela de Baralhos, e o que antes não acontecia: a
            // carta sumia da prateleira e não ia a lugar nenhum.
            HeroData naMesa = lib.NaMesa;
            int cartasAntes = naMesa != null ? TamanhoDoDeck(naMesa) : -1;
            int ouroAntes = GuildManager.Instance != null ? GuildManager.Instance.gold : 0;

            Button comprar = FirstEnabledButton(lib.cardsContainer);
            if (comprar == null)
            {
                Line("nenhuma carta comprável (sem ouro, baralho cheio ou acervo vazio)");
            }
            else
            {
                ReportarAlcancavel(comprar);
                comprar.onClick.Invoke();
                yield return new WaitForSeconds(0.3f);

                Line($"compra: ouro {ouroAntes} → {GuildManager.Instance.gold}"
                   + $" | baralho de {(naMesa != null ? naMesa.heroName : "ninguém")}: "
                   + $"{cartasAntes} → {(naMesa != null ? TamanhoDoDeck(naMesa) : -1)} cartas");
            }
        }

        yield return Capture("sala_biblioteca");

        ui.CloseLibrary();
        yield return new WaitForSeconds(0.2f);
        Line($"após fechar: guilda visível={(ui.guildPanel != null && ui.guildPanel.activeInHierarchy)}");
    }

    /// <summary>
    /// O painel que descreve a sala antes de entrar, aberto duas vezes com uma
    /// visita no meio.
    ///
    /// É o caminho em que ele sumia: o painel é irmão do GuildMap dentro de
    /// "Background" e nasce num índice anterior; ao voltar de qualquer sala,
    /// ShowGuildScreen manda o mapa para o fim da lista de irmãos e ele passa a
    /// cobrir o painel, engolindo os cliques. A primeira abertura funcionava, a
    /// segunda não — por isso um teste que abre só uma vez não pegava.
    /// </summary>
    /// <summary>
    /// A run tem começo, relógio e fim?
    ///
    /// Exercita o ciclo de verdade em vez de só ler os campos: avança até o
    /// limiar do chefe, confere que ele entra no quadro, empurra a Corrupção ao
    /// máximo e verifica que a run termina e a tela aparece. É o teste que a
    /// Fase 2.5 não teve e que a deixou "escrita mas não verificada".
    ///
    /// No fim, restaura a run para não deixar a sessão num estado terminado.
    /// </summary>
    IEnumerator TestRun()
    {
        var run = RunManager.Instance;
        if (run == null)
        {
            Line("FALHA: RunManager não existe.");
            yield break;
        }

        Line($"estado inicial: ciclo {run.Cycle} | corrupção {run.Corruption:F0}"
           + $" | chefe disponível={run.BossAvailable}");

        // --- O relógio anda? ---
        float antes = run.Corruption;
        run.AdvanceCycle();
        Line($"após 1 ciclo: corrupção {antes:F0} → {run.Corruption:F0}"
           + $" (esperado +{RunManager.CorruptionPerCycle:F0})");

        if (Mathf.Approximately(run.Corruption, antes))
            Line("FALHA: a Corrupção não avançou com o ciclo.");

        // --- O relógio sozinho não abre mais o fim ---
        //
        // Até 09/09 bastava a corrupção cruzar o limiar e o Chefe Supremo
        // entrava no quadro. Agora o fim é construído com três selos, e é isto
        // que precisa ficar provado: passar do antigo limiar não abre nada.
        int guarda = 0;
        while (run.Corruption < RunManager.BossThreshold && guarda++ < 40) run.AdvanceCycle();

        Line($"corrupção {run.Corruption:F0} no ciclo {run.Cycle}"
           + $" (antigo limiar {RunManager.BossThreshold:F0}) | selos"
           + $" {RegionMap.Selos}/{RegionMap.SelosParaOFim} | fim aberto={run.BossAvailable}");

        if (run.BossAvailable)
            Line("FALHA: a jornada final abriu sem selo nenhum.");

        var qm = QuestManager.Instance;
        if (qm != null)
        {
            // --- Mapear a região põe a luta de selo no quadro ---
            var regiao = BiomeUtil.Playable[0];
            RegionMap.Mapear(regiao, RegionMap.MapeamentoCompleto);
            qm.GarantirChefesDeRegiao();

            var comMapa = qm.GetQuests();
            int lutasDeSelo = comMapa.Count(q => q != null && q.isRegionBoss && q.biomeType == regiao);

            Line($"{BiomeUtil.GetDisplayName(regiao)} mapeada"
               + $" ({RegionMap.FracaoMapeada(regiao) * 100f:F0}%) | luta de selo no quadro: {lutasDeSelo}");

            if (lutasDeSelo == 0) Line("FALHA: região mapeada e nenhuma luta de selo no quadro.");
            if (lutasDeSelo > 1) Line("FALHA: mais de uma luta de selo para a mesma região.");

            // --- Selar trava a corrupção daquela região ---
            RegionMap.Selar(regiao);
            float antesDoAvanco = RegionMap.Corrupcao(regiao);
            run.AdvanceCycle();

            Line($"selada: corrupção da região {antesDoAvanco:F0} → {RegionMap.Corrupcao(regiao):F0}"
               + " (esperado: sem mudança)");

            if (!Mathf.Approximately(antesDoAvanco, RegionMap.Corrupcao(regiao)))
                Line("FALHA: região selada continuou apodrecendo.");

            qm.GarantirChefesDeRegiao();
            if (qm.GetQuests().Any(q => q != null && q.isRegionBoss && q.biomeType == regiao))
                Line("FALHA: região selada e a luta de selo continua no quadro.");

            // --- Três selos abrem a jornada final ---
            foreach (var outra in BiomeUtil.Playable)
            {
                if (RegionMap.Selos >= RegionMap.SelosParaOFim) break;
                RegionMap.Selar(outra);
            }

            qm.GarantirChefeSupremo();
            var quests = qm.GetQuests();
            int chefes = quests.Count(q => q != null && q.isFinalBoss);

            Line($"selos {RegionMap.Selos}/{RegionMap.SelosParaOFim} | fim aberto={run.BossAvailable}"
               + $" | missões no quadro: {quests.Count} | Chefe Supremo: {chefes}");

            if (!run.BossAvailable) Line("FALHA: três selos na mesa e a jornada final não abriu.");
            if (chefes == 0) Line("FALHA: chefe disponível mas fora do quadro.");
            if (chefes > 1) Line("FALHA: mais de um Chefe Supremo no quadro.");

            // --- A passagem abre onde o último selo caiu ---
            var final = quests.FirstOrDefault(q => q != null && q.isFinalBoss);
            var ultimo = RegionMap.UltimoSelo;

            Line($"ordem dos selos: {string.Join(" → ", RegionMap.RegioesSeladas().Select(BiomeUtil.GetDisplayName))}"
               + $" | passagem: {(final != null ? BiomeUtil.GetDisplayName(final.biomeType) : "—")}"
               + $" (esperado {BiomeUtil.GetDisplayName(ultimo)})");

            if (final != null && final.biomeType != ultimo)
                Line("FALHA: a jornada final não abriu na região do último selo.");

            // A corrupção das missões acompanha o medidor global?
            var comuns = quests.Where(q => q != null && !q.isFinalBoss && !q.isRegionBoss).ToList();
            if (comuns.Count > 0)
                Line($"corrupção das missões: {comuns.Min(q => q.corruptionLevel)}"
                   + $"–{comuns.Max(q => q.corruptionLevel)} (global {run.Corruption:F0})");
        }

        // --- A run termina? ---
        run.AddCorruption(RunManager.CorruptionMax);
        run.CheckEndConditions();

        Line($"após saturar a Corrupção: estado={run.State} motivo={run.EndReason}");

        if (run.State == RunState.Running)
            Line("FALHA: corrupção no máximo e a run não terminou.");

        yield return new WaitForSeconds(0.3f);

        var tela = RunEndUI.Instance;
        bool telaVisivel = tela != null && tela.panel != null && tela.panel.activeInHierarchy;
        Line($"tela de fim de run: {(tela == null ? "AUSENTE na cena" : telaVisivel ? "visível" : "montada, mas não apareceu")}");

        if (tela != null && telaVisivel)
        {
            if (tela.titleText != null) Line($"  título: '{StripTags(tela.titleText.text)}'");
            if (tela.statsText != null) Line($"  balanço: '{StripTags(tela.statsText.text).Replace("\n", " · ")}'");
            if (tela.metaText != null) Line($"  meta: '{StripTags(tela.metaText.text)}'");

            Line($"  botão de nova guilda: {(tela.newRunButton != null ? "ok" : "AUSENTE")}");
            if (tela.newRunButton != null) ReportarAlcancavel(tela.newRunButton);
        }

        // --- Recomeçar devolve o jogo ao início? ---
        run.StartNewRun();
        Line($"após recomeçar: ciclo {run.Cycle} | corrupção {run.Corruption:F0} | estado {run.State}");

        if (run.IsOver) Line("FALHA: a run continuou terminada depois de recomeçar.");

        if (tela != null && tela.panel != null) tela.panel.SetActive(false);
    }

    #region Save e menus

    /// <summary>Slot só deste teste. Os slots do jogador não são tocados.</summary>
    const string SlotDeTeste = "__probe";

    /// <summary>
    /// O save guarda o que o jogo tem, e as telas de menu existem e respondem?
    ///
    /// O teste que importa aqui é a **ida e volta**: fotografar o estado, mexer
    /// no jogo, recarregar e conferir que voltou exatamente. Um save que grava
    /// tudo e restaura errado passa em qualquer verificação de campo — é o mesmo
    /// erro das fases anteriores, em que o dado estava certo e ninguém o exibia.
    ///
    /// O perfil do jogador (memórias e destraves) é fotografado antes e reposto
    /// no fim: testar uma compra não pode custar as memórias de quem programa.
    /// </summary>
    IEnumerator TestSaveAndMenus()
    {
        var guilda = GuildManager.Instance;
        if (guilda == null)
        {
            Line("pulada: GuildManager ausente");
            yield break;
        }

        Line($"pasta dos saves: {SaveSystem.Pasta}");
        Line($"autosave suspenso durante o teste: {SaveSystem.AutosaveSuspenso}");

        // --- Ida e volta ---------------------------------------------------
        int ouroAntes = guilda.gold;
        int reputacaoAntes = guilda.reputation;
        int heroisAntes = guilda.roster.Count;
        int cicloAntes = RunManager.Instance.Cycle;
        float corrupcaoAntes = RunManager.Instance.Corruption;

        var assinaturaAntes = AssinaturaDoRoster();
        int missoesAntes = QuestManager.Instance != null ? QuestManager.Instance.GetQuests().Count : 0;

        bool gravou = SaveSystem.Escrever(SlotDeTeste, GameStateIO.Capturar());
        Line($"gravação: {(gravou ? "ok" : "FALHA — " + SaveSystem.UltimoErro)}");

        if (!gravou) yield break;

        // Estraga tudo de propósito: se o carregamento não desfizer isto, o save
        // não está restaurando coisa nenhuma.
        guilda.gold = 999999;
        guilda.reputation = 7;
        guilda.roster.Clear();
        RunManager.Instance.Restaurar(99, 99f, RunState.Running, RunEndReason.None, 99);

        SaveGame lido = SaveSystem.Ler(SlotDeTeste);
        Line($"leitura: {(lido != null ? "ok" : "FALHA — " + SaveSystem.UltimoErro)}");

        if (lido == null) yield break;

        bool aplicou = GameStateIO.Aplicar(lido);
        Line($"aplicação: {(aplicou ? "ok" : "FALHA")}");

        yield return null;

        Conferir("ouro", ouroAntes, guilda.gold);
        Conferir("reputação", reputacaoAntes, guilda.reputation);
        Conferir("heróis no roster", heroisAntes, guilda.roster.Count);
        Conferir("ciclo", cicloAntes, RunManager.Instance.Cycle);
        Conferir("corrupção", Mathf.RoundToInt(corrupcaoAntes),
                 Mathf.RoundToInt(RunManager.Instance.Corruption));

        if (QuestManager.Instance != null)
            Conferir("missões no quadro", missoesAntes, QuestManager.Instance.GetQuests().Count);

        string assinaturaDepois = AssinaturaDoRoster();
        if (assinaturaAntes == assinaturaDepois)
            Line($"roster idêntico após recarregar ({heroisAntes} heróis, id/HP/XP/estresse conferem)");
        else
            Line($"FALHA: roster mudou.\n  antes: {assinaturaAntes}\n  depois: {assinaturaDepois}");

        // O deck do herói é o que mais depende do id sobreviver à volta.
        if (guilda.roster.Count > 0)
        {
            HeroData primeiro = guilda.roster[0];
            DeckData deck = DeckRepository.GetDeck(primeiro);
            Line($"deck de {primeiro.heroName} após recarregar: "
               + $"{(deck != null ? deck.cards.Count + " cartas" : "NULO")}");

            if (deck == null || deck.cards.Count == 0)
                Line("FALHA: o herói voltou do save sem baralho.");
        }

        SaveSystem.Apagar(SlotDeTeste);
        Line($"slot de teste removido: {!SaveSystem.Existe(SlotDeTeste)}");

        // --- Cabeçalho do slot, que é o que a tela de carregar mostra --------
        SaveSystem.Escrever(SlotDeTeste, GameStateIO.Capturar());
        SaveHeader cabecalho = SaveSystem.LerCabecalho(SlotDeTeste);
        Line($"cabeçalho: existe={cabecalho.exists} corrompido={cabecalho.corrupted} "
           + $"ciclo={cabecalho.cycle} vivos={cabecalho.heroesAlive} ouro={cabecalho.gold}");

        if (!cabecalho.exists || cabecalho.corrupted)
            Line("FALHA: o cabeçalho não descreve o save recém-gravado.");

        // Arquivo quebrado tem que aparecer como quebrado, não derrubar a tela.
        System.IO.File.WriteAllText(SaveSystem.CaminhoDe(SlotDeTeste), "{ isto não é um save");
        SaveHeader quebrado = SaveSystem.LerCabecalho(SlotDeTeste);
        Line($"save corrompido detectado: {quebrado.corrupted}");

        if (!quebrado.corrupted)
            Line("FALHA: um arquivo ilegível passou por save válido.");

        SaveSystem.Apagar(SlotDeTeste);

        // --- A guarda que impede salvar no meio da estrada -------------------
        bool podeNaGuilda = SaveSystem.PodeSalvarAgora(out string motivoGuilda);
        Line($"pode salvar na guilda: {podeNaGuilda}"
           + (podeNaGuilda ? "" : $" (motivo: {motivoGuilda})"));

        if (!podeNaGuilda)
            Line("FALHA: a guilda entre jornadas é o ponto de save e ele está bloqueado.");

        var jm = JourneyManager.Instance;
        Line($"jornada em curso agora: {(jm != null ? jm.EmJornada.ToString() : "sem JourneyManager")}");

        // --- Os painéis existem e respondem? ---------------------------------
        yield return TestPauseMenu();
        TestMenuAssets();
        TestShrine();

        // Por último, porque destrói a cena do jogo.
        yield return TestMainMenuScene();
    }

    /// <summary>
    /// A cena de título e a volta dela para o jogo.
    ///
    /// É o caminho que nenhum teste cobria e onde mora o erro mais fácil de
    /// cometer: os managers são <c>DontDestroyOnLoad</c>, então "voltar ao título
    /// e fundar outra guilda" herdaria a partida anterior inteira se o descarte
    /// falhasse — e o sintoma seria a segunda partida da sessão nascer com o ouro
    /// e o ciclo da primeira, que ninguém repara olhando só a primeira.
    /// </summary>
    IEnumerator TestMainMenuScene()
    {
        SceneFlow.VoltarAoTitulo();
        yield return null;
        yield return new WaitForSeconds(0.6f);

        Line($"cena ativa após voltar ao título: {SceneManager.GetActiveScene().name}");

        Line($"managers descartados: guilda={(GuildManager.Instance == null)}"
           + $" quadro={(QuestManager.Instance == null)} ui={(UIManager.Instance == null)}");

        if (GuildManager.Instance != null)
            Line("FALHA: o GuildManager da partida anterior sobreviveu ao título.");

        var menu = FindObjectOfType<MainMenuUI>();
        if (menu == null)
        {
            Line("FALHA: MainMenuUI ausente na cena de título.");
            yield break;
        }

        AuditInspector(menu);

        Line($"título: '{StripTags(menu.titleText != null ? menu.titleText.text : "")}'");
        Line($"meta: '{StripTags(menu.metaText != null ? menu.metaText.text : "")}'");
        Line($"dica do continuar: '{StripTags(menu.continueHintText != null ? menu.continueHintText.text : "")}'");
        Line($"botão continuar habilitado: {(menu.continueButton != null && menu.continueButton.interactable)}"
           + $" (há autosave: {SaveSystem.TemPartidaEmAndamento()})");

        if (menu.newGameButton != null) ReportarAlcancavel(menu.newGameButton);

        yield return Capture("menu_principal");

        // As sub-telas abrem de verdade? É aqui que o painel que se desliga
        // sozinho aparece — o componente mora no próprio painel.
        if (menu.options != null)
        {
            menu.options.Abrir(null);
            yield return new WaitForSeconds(0.3f);

            bool aberto = menu.options.panel != null && menu.options.panel.activeInHierarchy;
            Line($"opções abertas: {aberto}"
               + $" | música {(menu.options.musicSlider != null ? menu.options.musicSlider.value.ToString("F2") : "?")}"
               + $" | resolução '{StripTags(menu.options.resolutionText != null ? menu.options.resolutionText.text : "")}'");

            if (!aberto) Line("FALHA: a tela de opções não abriu.");
            else yield return Capture("menu_opcoes");

            menu.options.panel.SetActive(false);
        }

        if (menu.shrine != null)
        {
            menu.shrine.Abrir(null);
            yield return new WaitForSeconds(0.3f);

            bool aberto = menu.shrine.panel != null && menu.shrine.panel.activeInHierarchy;
            int linhas = menu.shrine.unlockContainer != null
                ? CountRows(menu.shrine.unlockContainer) : 0;

            Line($"santuário aberto: {aberto} | destraves listados: {linhas}"
               + $" (catálogo: {MetaProgression.Catalogo.Length})");

            if (!aberto) Line("FALHA: o Santuário não abriu.");
            else if (linhas != MetaProgression.Catalogo.Length)
                Line("FALHA: a lista do Santuário não bate com o catálogo.");
            else yield return Capture("menu_santuario");

            menu.shrine.panel.SetActive(false);
        }

        if (menu.saveSlots != null)
        {
            menu.saveSlots.Abrir(SaveSlotsUI.Modo.Carregar, null);
            yield return new WaitForSeconds(0.3f);

            bool aberto = menu.saveSlots.panel != null && menu.saveSlots.panel.activeInHierarchy;
            int linhas = menu.saveSlots.slotContainer != null
                ? CountRows(menu.saveSlots.slotContainer) : 0;

            Line($"slots abertos: {aberto} | linhas: {linhas} (esperado 4: automático + 3)");

            if (!aberto) Line("FALHA: a tela de slots não abriu.");
            else if (linhas != 4) Line("FALHA: a lista de slots não tem uma linha por slot.");
            else yield return Capture("menu_slots");

            menu.saveSlots.panel.SetActive(false);
        }

        // --- E a volta: fundar uma guilda nova a partir do título ------------
        SceneFlow.NovaPartida();
        yield return null;
        yield return new WaitForSeconds(1.2f);

        Line($"cena ativa após 'nova guilda': {SceneManager.GetActiveScene().name}");

        var guilda = GuildManager.Instance;
        if (guilda == null)
        {
            Line("FALHA: a guilda nova não nasceu ao voltar do título.");
            yield break;
        }

        int esperado = MetaProgression.OuroBasePorRun + MetaProgression.StartingGoldBonus();

        Line($"guilda nova: {guilda.roster.Count} heróis | {guilda.gold} de ouro"
           + $" (esperado {esperado}) | reputação {guilda.reputation}"
           + $" | ciclo {(RunManager.Existe ? RunManager.Instance.Cycle : -1)}");

        if (guilda.roster.Count != 4)
            Line($"FALHA: guilda nova deveria ter 4 heróis e tem {guilda.roster.Count}.");

        if (guilda.gold != esperado)
            Line("FALHA: a guilda nova herdou o ouro da partida anterior.");

        if (RunManager.Existe && RunManager.Instance.Cycle != 0)
            Line("FALHA: o relógio da run não voltou ao ciclo 0.");

        yield return Capture("menu_nova_guilda");
    }

    /// <summary>Cada herói reduzido a uma linha comparável antes e depois do save.</summary>
    string AssinaturaDoRoster()
    {
        var guilda = GuildManager.Instance;
        if (guilda == null) return "";

        return string.Join(" | ", guilda.roster
            .Where(h => h != null)
            .OrderBy(h => h.GetId())
            .Select(h => $"{h.GetId().Substring(0, 6)}:{h.heroName}:Nv{h.level}"
                       + $":{h.currentHp}/{h.maxHp}:xp{h.xp}:st{Mathf.RoundToInt(h.stress)}"
                       + $":{(h.isDead ? "morto" : h.isInjured ? "ferido" : "ok")}"));
    }

    void Conferir(string oQue, int esperado, int obtido)
    {
        if (esperado == obtido) Line($"  {oQue}: {obtido} ✓");
        else Line($"  FALHA: {oQue} esperava {esperado} e voltou {obtido}");
    }

    IEnumerator TestPauseMenu()
    {
        var pausa = PauseMenuUI.Instance;
        if (pausa == null)
        {
            Line("FALHA: PauseMenuUI ausente na cena — rode 'Montar Cena'.");
            yield break;
        }

        AuditInspector(pausa);

        pausa.Abrir();
        yield return new WaitForSeconds(0.25f);

        Line($"pausa aberta: {pausa.Aberto}");
        if (!pausa.Aberto) Line("FALHA: a pausa não abriu.");

        if (pausa.panel != null)
            Line($"  ordem entre irmãos: {pausa.panel.transform.GetSiblingIndex() + 1} de "
               + $"{pausa.panel.transform.parent.childCount}");

        if (pausa.resumeButton != null) ReportarAlcancavel(pausa.resumeButton);

        Line($"  botão de salvar habilitado: {(pausa.saveButton != null && pausa.saveButton.interactable)}");
        if (pausa.statusText != null) Line($"  estado: '{StripTags(pausa.statusText.text)}'");

        yield return Capture("menu_pausa");

        pausa.Fechar();
        yield return new WaitForSeconds(0.15f);

        Line($"pausa fechada: {!pausa.Aberto}");
    }

    /// <summary>A cena de título existe, está na build e é a primeira?</summary>
    void TestMenuAssets()
    {
        int cenas = SceneManager.sceneCountInBuildSettings;
        Line($"cenas na build: {cenas}");

        if (cenas == 0)
        {
            Line("FALHA: nenhuma cena registrada na build — rode 'Montar Menus'.");
            return;
        }

        string primeira = SceneUtility.GetScenePathByBuildIndex(0);
        Line($"cena 0 (a que abre a build): {primeira}");

        if (!primeira.EndsWith("MainMenu.unity"))
            Line("FALHA: a build não começa pelo menu principal.");

        bool jogoNaBuild = false;
        for (int i = 0; i < cenas; i++)
            if (SceneUtility.GetScenePathByBuildIndex(i).EndsWith("SampleScene.unity"))
                jogoNaBuild = true;

        Line($"cena do jogo na build: {jogoNaBuild}");
        if (!jogoNaBuild) Line("FALHA: SampleScene fora da build — 'Continuar' não teria para onde ir.");
    }

    /// <summary>
    /// O Santuário cobra o que promete? O perfil é reposto no fim — testar uma
    /// compra não pode custar as memórias de quem está programando.
    /// </summary>
    void TestShrine()
    {
        int memoriasAntes = MetaProgression.Memorias;
        var niveisAntes = MetaProgression.Catalogo
            .ToDictionary(u => u.id, u => MetaProgression.NivelDe(u.id));

        Line($"memórias do perfil: {memoriasAntes} | destraves: "
           + string.Join(", ", niveisAntes.Select(p => $"{p.Key} {p.Value}")));

        Unlock alvo = MetaProgression.Catalogo[0];
        int nivelInicial = MetaProgression.NivelDe(alvo.id);

        if (alvo.NoTeto(nivelInicial))
        {
            Line($"  '{alvo.nome}' já está no teto; compra não testada");
        }
        else
        {
            int custo = alvo.CustoDoNivel(nivelInicial + 1);

            // Sem saldo, a compra tem que recusar.
            PlayerProfile.Dados.memories = 0;
            bool semSaldo = MetaProgression.Comprar(alvo.id);
            Line($"  compra sem saldo recusada: {!semSaldo}");
            if (semSaldo) Line("FALHA: comprou destrave sem memórias.");

            // Com saldo exato, compra e desconta.
            PlayerProfile.Dados.memories = custo;
            bool comprou = MetaProgression.Comprar(alvo.id);

            Line($"  compra de '{alvo.nome}' por {custo}: {comprou}"
               + $" | nível {nivelInicial} → {MetaProgression.NivelDe(alvo.id)}"
               + $" | saldo restante {MetaProgression.Memorias}");

            if (!comprou) Line("FALHA: compra com saldo exato recusada.");
            if (MetaProgression.Memorias != 0) Line("FALHA: o custo não foi descontado.");
        }

        // O destrave chega à guilda?
        Line($"  ouro inicial: {MetaProgression.OuroBasePorRun} + {MetaProgression.StartingGoldBonus()}"
           + $" | reputação: {MetaProgression.ReputacaoBasePorRun} + {MetaProgression.StartingReputationBonus()}"
           + $" | vagas: {GuildManager.BaseRosterSize} + {MetaProgression.ExtraRosterSlots()}"
           + $" | quadro: {(QuestManager.Instance != null ? QuestManager.Instance.TamanhoDoQuadro : 0)}");

        // Repõe o perfil como estava.
        PlayerProfile.Dados.memories = memoriasAntes;
        PlayerProfile.Dados.unlocks.Clear();
        foreach (var par in niveisAntes)
            if (par.Value > 0)
                PlayerProfile.Dados.unlocks.Add(new UnlockSave { id = par.Key, level = par.Value });

        PlayerProfile.Salvar();

        bool reposto = MetaProgression.Memorias == memoriasAntes
                    && MetaProgression.Catalogo.All(u => MetaProgression.NivelDe(u.id) == niveisAntes[u.id]);

        Line($"  perfil do autor reposto: {reposto}");
        if (!reposto) Line("FALHA: o teste alterou o perfil do jogador e não o restaurou.");
    }

    #endregion

    IEnumerator TestLocationInfo(UIManager ui)
    {
        var mm = Resources.FindObjectsOfTypeAll<MapManager>()
                          .FirstOrDefault(m => m != null && m.gameObject.scene.rootCount > 0);

        if (mm == null || mm.locationInfoPanel == null || mm.enterButton == null)
        {
            Line("pulada: MapManager sem painel de informação ou sem botão de entrar");
            yield break;
        }

        ui.ShowGuildScreen();
        yield return new WaitForSeconds(0.35f);

        Transform painel = mm.locationInfoPanel.transform;
        int irmaos = painel.parent != null ? painel.parent.childCount - 1 : 0;

        // ── Primeira abertura ──
        if (mm.tavernButton != null) mm.tavernButton.onClick.Invoke();
        yield return null;

        Line($"1ª abertura: visível={mm.locationInfoPanel.activeInHierarchy}"
           + $" | ordem entre irmãos: {painel.GetSiblingIndex()} de {irmaos}");
        ReportarAlcancavel(mm.enterButton);

        // ── Entra na sala e volta para o mapa ──
        mm.enterButton.onClick.Invoke();
        yield return new WaitForSeconds(0.45f);

        ui.ShowGuildScreen();
        yield return new WaitForSeconds(0.45f);

        // ── Segunda abertura: é aqui que o painel sumia ──
        if (mm.libraryButton != null) mm.libraryButton.onClick.Invoke();
        yield return null;

        int ordemDepois = painel.GetSiblingIndex();
        bool porCima = painel.parent == null || ordemDepois == irmaos;

        Line($"2ª abertura (após visitar uma sala): visível={mm.locationInfoPanel.activeInHierarchy}"
           + $" | ordem entre irmãos: {ordemDepois} de {irmaos}"
           + (porCima ? " — na frente" : " — ATRÁS de alguém"));

        ReportarAlcancavel(mm.enterButton);

        if (!porCima)
            Line("FALHA: o painel de informação da sala ficou atrás e não recebe cliques");

        mm.CloseLocationInfo();
        yield return new WaitForSeconds(0.2f);
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

            // A sala virou mapa: as sete regiões à esquerda, a escolhida na mesa
            // e a estrada até ela à direita. Se a região não chega à mesa, o
            // resto da tela fica vazio e nada acusa isso.
            Line($"regiões na parede: {CountRows(mr.regionContainer)}");
            Line($"região na mesa: {(mr.regionNameText != null ? mr.regionNameText.text : "sem rótulo")}");
            Line($"estrada desenhada: {CountRows(mr.revealedEventsContainer)} marcos");

            if (mr.buyScoutingButton != null && mr.buyScoutingButton.interactable)
            {
                ReportarAlcancavel(mr.buyScoutingButton);
                mr.buyScoutingButton.onClick.Invoke();
                yield return new WaitForSeconds(0.3f);
                Line($"contratar batedor: ouro {ouroAntes} → {GuildManager.Instance.gold}"
                   + $" | batedores {mr.ScoutingCharges}/{mr.MaxScouting}"
                   + $" | dias abertos na estrada: {(mr.roadTitleText != null ? mr.roadTitleText.text : "?")}");
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

        // O mapa da rota: quanto da tela ele ocupa durante a travessia.
        var jmui = JourneyMapUI.Instance;
        if (jmui != null && jmui.nodeContainer != null)
            Line($"janela do mapa da jornada: {jmui.nodeContainer.rect.width:0}×{jmui.nodeContainer.rect.height:0}px");

        // Relíquias e frascos: a ficha é a única tela onde se equipa, e a
        // prateleira da guilda é a única porta de entrada deles.
        yield return ExercitarEquipamento(hd);

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

        // Passo 1: escolher o destino e avançar.
        //
        // O destino agora se aponta no mapa de regiões, então o botão vive nos
        // marcadores dele. A lista antiga continua sendo consultada em seguida:
        // ela é a rede de segurança de quando o mapa não pode ser montado, e o
        // teste precisa cobrir os dois caminhos pelo mesmo motivo.
        Button quest = null;

        var mapa = UnityEngine.Object.FindObjectOfType<RegionMapUI>(true);
        if (mapa != null)
        {
            // O tamanho do mapa na tela, e o do painel que o hospeda. Sem estes
            // dois números não dá para saber se ele está pequeno porque as
            // âncoras o encolhem ou porque o hospedeiro já é pequeno — e o mapa
            // é a tela onde o jogador escolhe para onde vai.
            var rtMapa = mapa.GetComponent<RectTransform>();
            var rtPai = mapa.transform.parent as RectTransform;

            Line($"mapa de regiões: {rtMapa.rect.width:0}×{rtMapa.rect.height:0}px"
               + (rtPai != null ? $" dentro de '{rtPai.name}' de {rtPai.rect.width:0}×{rtPai.rect.height:0}px" : ""));

            quest = FirstEnabledButton(mapa.transform);
        }

        if (quest == null)
            quest = FirstEnabledButton(qs.questListContainer);

        if (quest == null)
        {
            Line("tela de formação: nenhum destino clicável (mapa e lista vazios)");
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
        //
        // O "USAR DECK" do passo 2 saiu: o deck principal era escolhido em dois
        // lugares, e quem usava aquele botão chegava ao passo 3 sem seleção
        // visível e sem a composição do baralho. Agora o passo 3 marca o
        // primeiro da formação sozinho, e trocar continua a um clique.
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
        Line($"itens na carroça: {CountRows(market.itemContainer)}");

        // A sala põe alguém no balcão sozinha ao abrir, como a Forja faz com a
        // bigorna: um balcão vazio esperando clique seria a tela morta de antes.
        string noBalcao = market.counterName != null ? market.counterName.text : "";
        Line($"no balcão ao abrir: {(string.IsNullOrEmpty(noBalcao) ? "nada" : noBalcao)}");

        // Trocar o item do balcão é a interação nova. O vinho serve de alvo do
        // clique porque é o que exercita a faixa da direita com barra: ele mede
        // estresse, e a party do teste sempre tem alguém com algum.
        Button ficha = FirstEnabledButton(market.itemContainer, "Vinho");
        if (ficha == null)
        {
            Line("FALHA: o Vinho está na carroça e a ficha dele não responde ao clique");
        }
        else
        {
            ReportarAlcancavel(ficha);
            ficha.onClick.Invoke();
            yield return new WaitForSeconds(0.2f);

            Line($"clique na ficha do Vinho: no balcão agora = "
               + $"{(market.counterName != null ? market.counterName.text : "?")}");
            Line($"em quem a compra pega: {CountRows(market.effectContainer)} linha(s) — "
               + $"{(market.effectTitle != null ? market.effectTitle.text : "")}");
            DumpLabelVisivel(market.effectContainer);
        }

        yield return Capture("sala_mercado");

        int ouroAntes = GuildManager.Instance.gold;
        int racoesAntes = market.StockedRations;

        // A ração é a compra do teste: barata, sempre disponível e com efeito
        // conferível por número (o estoque que a próxima jornada leva).
        Button racao = FirstEnabledButton(market.itemContainer, "Ração");
        if (racao != null)
        {
            racao.onClick.Invoke();
            yield return new WaitForSeconds(0.2f);
        }

        if (market.buyButton == null || !market.buyButton.interactable)
        {
            Line("FALHA: o botão do balcão não está disponível para a ração");
        }
        else
        {
            ReportarAlcancavel(market.buyButton);
            market.buyButton.onClick.Invoke();
            yield return new WaitForSeconds(0.2f);
            Line($"compra: ouro {ouroAntes} → {GuildManager.Instance.gold} | rações estocadas {racoesAntes} → {market.StockedRations}");
            Line($"o balcão continua no mesmo item: {(market.counterName != null ? market.counterName.text : "?")}");
        }

        RelatarEstoquePorCiclo();

        ui.CloseMarket();
        yield return new WaitForSeconds(0.2f);
    }

    /// <summary>
    /// A prova de que a sala muda de uma volta para a outra — e de que não muda
    /// dentro da mesma. Um estoque instável dentro do ciclo deixaria o jogador
    /// reabrir a sala até sair o que ele quer, que é o contrário do que se quis
    /// construir; um estoque igual em todo ciclo não daria motivo para voltar.
    /// </summary>
    void RelatarEstoquePorCiclo()
    {
        Line("");
        Line("── ESTOQUE POR CICLO ──");

        var vistos = new List<string>();

        for (int ciclo = 0; ciclo < 6; ciclo++)
        {
            var frascos = CycleStock.Escolher(ItemCatalog.Pocoes, 2, "mercado-frascos", ciclo);
            string nomes = string.Join(", ", frascos.ConvertAll(p => p.nome));

            int oferta = CycleStock.Numero("forja-oferta", 0, 3, ciclo);
            string forja = oferta == 0 ? "sem oferta" : oferta == 1 ? "arma" : "armadura";

            vistos.Add(nomes);
            Line($"ciclo {ciclo}: mercado leva {nomes} | forja em oferta: {forja}");
        }

        // Estabilidade: o mesmo ciclo, consultado de novo, tem de dar o mesmo.
        var repetido = CycleStock.Escolher(ItemCatalog.Pocoes, 2, "mercado-frascos", 3);
        bool estavel = string.Join(", ", repetido.ConvertAll(p => p.nome)) == vistos[3];

        int distintos = new HashSet<string>(vistos).Count;

        Line($"estável dentro do mesmo ciclo: {estavel}");
        Line(distintos > 1
            ? $"muda entre ciclos: sim ({distintos} prateleiras diferentes em 6 voltas)"
            : "FALHA: a prateleira é a mesma em todos os ciclos");
    }

    IEnumerator TestForge(UIManager ui)
    {
        var forge = ForgeManager.Instance;
        if (forge == null) yield break;

        ui.ShowForge();
        yield return new WaitForSeconds(0.3f);

        Line($"forja visível: {(ui.forgePanel != null && ui.forgePanel.activeInHierarchy)}");
        Line($"heróis na fila: {CountRows(forge.heroContainer)}");

        // A sala escolhe alguém sozinha ao abrir: uma bancada vazia esperando
        // clique seria a mesma tela morta de antes.
        Line($"na bigorna ao abrir: {(forge.NaBigorna != null ? forge.NaBigorna.heroName : "ninguém")}");

        // Trocar quem está na bigorna é a interação nova da sala. Clicar na
        // segunda ficha da fila prova que a bancada acompanha a escolha.
        HeroData outro = GuildManager.Instance.roster
            .Where(h => h != null && !h.isDead && h != forge.NaBigorna)
            .FirstOrDefault();

        if (outro != null)
        {
            Button ficha = FirstEnabledButton(forge.heroContainer, outro.heroName);
            if (ficha == null)
            {
                Line($"FALHA: {outro.heroName} está na fila, mas a ficha dele não responde ao clique");
            }
            else
            {
                ReportarAlcancavel(ficha);
                ficha.onClick.Invoke();
                yield return new WaitForSeconds(0.2f);
                Line($"clique na ficha de {outro.heroName}: na bigorna agora = "
                   + $"{(forge.NaBigorna != null ? forge.NaBigorna.heroName : "ninguém")}");
            }
        }

        HeroData alvo = forge.NaBigorna;
        if (alvo == null)
        {
            Line("pulada: roster sem heróis vivos");
            ui.CloseForge();
            yield break;
        }

        // A peça é o retorno visível da compra: sem sprite, a bigorna volta a ser
        // um rótulo de nível, e isso não aparece em erro nenhum no console.
        Line($"peça na bigorna: arma={Desenho(forge.weaponIcon)} | armadura={Desenho(forge.armorIcon)}");
        Line($"cartas na prateleira: {CountRows(forge.cardShelf)} (as que a arma afeta)");
        yield return Capture("sala_forja");

        int armaAntes = alvo.weaponLevel;
        int bonusAntes = ForgeManager.WeaponBonus(alvo);

        if (forge.weaponButton == null || !forge.weaponButton.interactable)
        {
            Line("FALHA: o botão de forjar arma não está disponível");
        }
        else
        {
            ReportarAlcancavel(forge.weaponButton);
            forge.weaponButton.onClick.Invoke();
            yield return new WaitForSeconds(0.3f);

            Line($"forjar arma de {alvo.heroName}: nível {armaAntes} → {alvo.weaponLevel}"
               + $" | bônus nas cartas dele: +{bonusAntes} → +{ForgeManager.WeaponBonus(alvo)}");
            Line($"a carta mostra o ganho: {CartaMostraForja(forge)}");
        }

        int hpMaxAntes = alvo.maxHp;

        if (forge.armorButton == null || !forge.armorButton.interactable)
        {
            Line("FALHA: o botão de reforçar armadura não está disponível");
        }
        else
        {
            forge.armorButton.onClick.Invoke();
            yield return new WaitForSeconds(0.3f);
            Line($"reforçar armadura de {alvo.heroName}: HP máx {hpMaxAntes} → {alvo.maxHp}");
        }

        yield return Capture("sala_forja_forjada");

        ui.CloseForge();
        yield return new WaitForSeconds(0.2f);
    }

    /// <summary>Estresse somado de quem está vivo — a régua da vigília.</summary>
    static float EstresseTotalDoRoster()
    {
        if (GuildManager.Instance == null) return 0f;

        float total = 0f;
        foreach (var h in GuildManager.Instance.roster)
            if (h != null && !h.isDead) total += h.stress;

        return total;
    }

    /// <summary>Quantas cartas o baralho daquele herói tem agora.</summary>
    static int TamanhoDoDeck(HeroData hero)
    {
        DeckData deck = DeckRepository.GetDeck(hero);
        return deck?.cards != null ? deck.cards.Count : -1;
    }

    /// <summary>
    /// O que aquele slot está realmente mostrando. Sprite ausente e sprite
    /// desenhado a 8px dão o mesmo "0 erros" no console — o tamanho em pixels é
    /// o que denuncia os dois.
    /// </summary>
    static string Desenho(Image img)
    {
        if (img == null) return "sem componente";
        if (img.sprite == null || !img.enabled) return "SEM SPRITE";

        var rect = img.rectTransform.rect;
        return $"{img.sprite.name} ({rect.width:0}×{rect.height:0}px)";
    }

    /// <summary>
    /// A prova de que a compra ficou visível: alguma carta da prateleira precisa
    /// dizer o ganho da forja. Sem isso a sala volta a ser um rótulo de nível.
    /// </summary>
    static string CartaMostraForja(ForgeManager forge)
    {
        if (forge == null || forge.cardShelf == null) return "sem prateleira";

        foreach (var texto in forge.cardShelf.GetComponentsInChildren<TMP_Text>(true))
        {
            if (texto.text == null || !texto.text.Contains("da forja")) continue;

            // A nota vem colada na descrição da carta e com tag de cor: o
            // relatório mostra só a última linha, já limpa.
            string[] linhas = texto.text.Split('\n');
            string nota = linhas[linhas.Length - 1]
                .Replace("</color>", "")
                .Replace("<color=#7FB069>", "");

            return $"sim — \"{nota.Trim()}\"";
        }

        return "NÃO — nenhuma carta anuncia o ganho";
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

        // A sala refeita mostra, ao lado das tumbas, quem ainda está vivo e com
        // quanto estresse: é onde a vigília vira efeito visível. Sem essa
        // coluna, pagar 120 de ouro voltava a ser um número mudando no rodapé.
        Line($"quem ficou, listado: {CountRows(cemetery.livingContainer)} herói(s)");

        if (tumbas == 0)
        {
            Line("estado vazio exibido — ainda não morreu ninguém nesta sessão");
        }
        else
        {
            Line($"tumba em foco: {(cemetery.heroNameText != null ? cemetery.heroNameText.text : "sem rótulo")}");
            Line($"o que foi enterrado com ele: {Desenho(cemetery.portraitImage)}");

            if (cemetery.vigilButton != null && cemetery.vigilButton.interactable)
            {
                float estresseAntes = EstresseTotalDoRoster();
                ReportarAlcancavel(cemetery.vigilButton);
                cemetery.vigilButton.onClick.Invoke();
                yield return new WaitForSeconds(0.3f);
                Line($"vigília: estresse somado do roster {estresseAntes:0} → {EstresseTotalDoRoster():0}");
            }
        }

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
    /// <summary>
    /// A fila do round bate com o que está em campo?
    ///
    /// Conta as figuras vivas na cena em vez de perguntar ao CombatManager: é o
    /// que o jogador vê, e é aí que mora o erro que este projeto já cometeu
    /// várias vezes — o dado certo com a exibição ausente.
    /// </summary>
    void ReportarOrdemDoRound()
    {
        var cm = CombatManager.Instance;
        if (cm == null || cm.turnOrder == null)
        {
            Line("FALHA: combate sem barra de ordem do round — rode 'Montar Cena'.");
            return;
        }

        int inimigosVivos = cm.enemyContainer == null ? 0 : CountRows(cm.enemyContainer);
        int esperado = inimigosVivos + 1;   // o grupo ocupa a primeira ficha

        Line($"ordem do round: {cm.turnOrder.Tamanho} fichas para {inimigosVivos} inimigos"
           + $" (esperado {esperado}) | apontando para o índice {cm.turnOrder.Atual}");

        if (cm.turnOrder.Tamanho != esperado)
            Line("FALHA: a fila do round não bate com os inimigos em campo.");

        if (cm.turnOrder.Atual != 0)
            Line("FALHA: é a vez do jogador e a fila não está no GRUPO.");
    }

    /// <summary>
    /// Põe um item na prateleira, equipa pelo clique e confere que o efeito
    /// existe de verdade.
    ///
    /// O caminho inteiro é exercitado pela interface, e não pelo manager: o
    /// <c>GuildManager</c> equipar direto não prova nada sobre a ficha, e é na
    /// ficha que o jogador faz isso. Foi assim que o projeto descobriu, mais de
    /// uma vez, telas cujo dado estava certo e a exibição não existia.
    /// </summary>
    IEnumerator ExercitarEquipamento(HeroDetailPanel ficha)
    {
        var guilda = GuildManager.Instance;
        if (ficha == null || guilda == null || ficha.CurrentHero == null)
        {
            Line("FALHA: sem ficha ou sem guilda para testar relíquias.");
            yield break;
        }

        HeroData heroi = ficha.CurrentHero;

        // Estado de partida conhecido: o teste não pode depender do que a jornada
        // já largou na prateleira.
        var reliquia = ItemCatalog.Reliquias[0];
        var pocao = ItemCatalog.Pocoes[0];
        guilda.GuardarReliquia(reliquia.id);
        guilda.GuardarPocao(pocao.id);

        int reliquiasAntes = heroi.relics.Count;
        int prateleiraAntes = guilda.relicStock.Count;

        // Redesenha com a prateleira já cheia — a ficha foi montada antes disto.
        ficha.ShowHeroDetails(heroi);
        yield return null;

        var gear = ficha.panel != null ? ficha.panel.GetComponentInChildren<HeroGearUI>(true) : null;
        if (gear == null)
        {
            Line("FALHA: a ficha do herói não tem a seção de relíquias.");
            yield break;
        }

        Line($"ficha: seção de itens com {gear.LinhasClicaveis} linha(s) clicável(is)"
           + $" | prateleira: {guilda.relicStock.Count} relíquia(s), {guilda.potionStock.Count} frasco(s)");

        // Clica na linha da relíquia guardada. Procurar pelo texto é o que prova
        // que ela chegou à tela: equipar pelo manager passaria com a lista vazia.
        Button linha = BotaoComTexto(gear.transform, reliquia.nome);
        if (linha == null)
        {
            Line("FALHA: a relíquia da prateleira não apareceu na ficha.");
            yield break;
        }

        ReportarAlcancavel(linha);
        linha.onClick.Invoke();
        yield return null;

        bool equipou = heroi.relics.Count == reliquiasAntes + 1 && ItemCatalog.Tem(heroi, reliquia.id);
        Line($"equipar pelo clique: relíquias do herói {reliquiasAntes} → {heroi.relics.Count}"
           + $" | prateleira {prateleiraAntes} → {guilda.relicStock.Count}"
           + (equipou ? "" : "  <<< FALHA"));

        int bonus = ItemCatalog.Total(heroi, RelicEffect.DanoDeCarta);
        Line($"efeito em vigor: +{bonus} de dano nas cartas de {heroi.heroName}"
           + (bonus == reliquia.valor ? "" : "  <<< FALHA: a relíquia não conta"));

        // O frasco vai para a mochila do herói, que é o que a cinta do combate lê.
        Button frasco = BotaoComTexto(gear.transform, pocao.nome);
        if (frasco != null)
        {
            frasco.onClick.Invoke();
            yield return null;
        }

        Line($"frascos com {heroi.heroName}: {heroi.potions.Count}"
           + (heroi.potions.Count > 0 ? "" : "  <<< FALHA: o frasco não foi entregue"));
    }

    /// <summary>
    /// Dá um frasco de cura a cada herói do roster antes da jornada de teste.
    ///
    /// Passa pelo <c>GuildManager</c>, e não pela lista do herói: é o mesmo
    /// caminho que a ficha usa, então uma quebra ali aparece aqui também.
    /// </summary>
    void EquiparGrupoParaOTeste()
    {
        var guilda = GuildManager.Instance;
        if (guilda == null) return;

        var cura = ItemCatalog.Pocoes[0];
        int entregues = 0;

        foreach (var heroi in guilda.roster)
        {
            if (heroi == null || !heroi.IsAlive) continue;

            guilda.GuardarPocao(cura.id);
            if (guilda.EntregarPocao(heroi, cura.id)) entregues++;
        }

        Line($"frascos entregues ao roster para o teste: {entregues}");
    }

    /// <summary>O primeiro botão cujo texto contenha este trecho.</summary>
    static Button BotaoComTexto(Transform raiz, string trecho)
    {
        if (raiz == null || string.IsNullOrEmpty(trecho)) return null;

        foreach (var botao in raiz.GetComponentsInChildren<Button>(true))
        {
            var texto = botao.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (texto != null && texto.text.Contains(trecho)) return botao;
        }

        return null;
    }

    /// <summary>
    /// O campo de batalha tem corpo para cada lutador?
    ///
    /// O palco é filmado por uma câmera fora do enquadramento do jogo: se o
    /// boneco de uma classe sumir da tabela do <c>TrailCast</c>, ou a criatura
    /// ficar sem quadros de animação, nada estoura — a luta acontece com um lado
    /// invisível e o relatório continua em 0 falhas. É o padrão de erro mais
    /// caro deste projeto, e por isso a contagem é conferida aqui.
    ///
    /// A altura em pixels é o segundo número: corpo minúsculo ou estourando a
    /// janela aparece aqui sem precisar de captura, do mesmo jeito que na
    /// estrada.
    /// </summary>
    void ReportarCampoDeBatalha()
    {
        var cm = CombatManager.Instance;
        if (cm == null) return;

        if (cm.battleField == null)
        {
            Line("FALHA: combate sem campo de batalha filmado — rode 'Montar Cena'.");
            return;
        }

        int inimigosVivos = cm.enemyContainer == null ? 0 : CountRows(cm.enemyContainer);
        int heroisEmCena = cm.heroContainer == null ? 0 : CountRows(cm.heroContainer);
        int esperado = inimigosVivos + heroisEmCena;

        // Os frascos que o grupo levou: a cinta só aparece se alguém carregar
        // algum, e o teste equipa um herói antes de partir.
        Line($"frascos à mão no combate: {cm.FrascosEmCombate}");

        // Quem está em campo: com a arte trocada, saber qual criatura apareceu é
        // o que liga o número da captura ao inimigo certo.
        if (cm.enemyContainer != null)
        {
            var nomes = new List<string>();
            foreach (Transform filho in cm.enemyContainer)
            {
                if (!filho.gameObject.activeSelf) continue;

                var texto = filho.Find("Name")?.GetComponent<TMPro.TMP_Text>();
                if (texto != null) nomes.Add(texto.text);
            }

            if (nomes.Count > 0) Line($"inimigos em campo: {string.Join(", ", nomes)}");
        }

        Line($"campo de batalha: {cm.battleField.CorposEmCena} corpos"
           + $" para {heroisEmCena} heróis e {inimigosVivos} inimigos"
           + $" | criaturas animadas: {cm.battleField.CriaturasAnimadas}/{inimigosVivos}"
           + $" | herói com {cm.battleField.AlturaDoHeroiEmPixels:0}px");

        if (cm.battleField.CorposEmCena < esperado)
            Line("FALHA: falta corpo no campo de batalha — alguém está lutando invisível.");

        if (cm.battleField.CriaturasAnimadas < inimigosVivos)
            Line("AVISO: criatura sem quadros de animação — rode 'Aplicar Arte nos Inimigos'.");

        if (cm.battleField.AlturaDoHeroiEmPixels < 60f)
            Line("FALHA: o herói saiu miniatura no campo de batalha.");
    }

    /// <summary>
    /// As cartas da mão estão espalhadas em leque, ou empilhadas na origem?
    ///
    /// Uma pilha na origem é indistinguível de "só há uma carta" numa captura de
    /// tela, e o contador do HUD continua dizendo 5. O que denuncia é a distância
    /// entre a primeira e a última: leque tem largura, pilha não tem.
    /// </summary>
    void ReportarMao(JourneyManager jm)
    {
        if (jm == null || jm.handContainer == null) return;

        var cartas = new List<RectTransform>();
        foreach (Transform c in jm.handContainer)
            if (c is RectTransform rt && c.gameObject.activeSelf) cartas.Add(rt);

        if (cartas.Count == 0)
        {
            Line("mão: nenhuma carta no container");
            return;
        }

        float esquerda = cartas.Min(c => c.anchoredPosition.x);
        float direita = cartas.Max(c => c.anchoredPosition.x);
        float escala = cartas[0].localScale.x;

        var leque = jm.handContainer.GetComponent<HandFanLayout>();

        Line($"mão: {cartas.Count} cartas | leque de {direita - esquerda:F0}px"
           + $" | escala {escala:F2} | container {jm.handContainer.GetComponent<RectTransform>().rect.size}"
           + $" | HandFanLayout={(leque != null ? (leque.enabled ? "ligado" : "desligado") : "AUSENTE")}"
           + $" | container ativo={jm.handContainer.gameObject.activeInHierarchy}");

        if (leque != null)
            Line($"     leque por dentro: vê {leque.CartasNoLeque} cartas"
               + $" | escala calculada {leque.UltimaEscala:F2}"
               + $" | espaçamento {leque.UltimoEspacamento:F0}"
               + $" | altura da carta {leque.UltimaAlturaDeCarta:F0}"
               + $" (-1 = o cálculo nunca chegou até aqui)");

        if (cartas.Count > 1 && direita - esquerda < 40f)
            Line("FALHA: as cartas da mão estão empilhadas — o leque não se aplicou.");
    }

    /// <summary>
    /// A estrada tem corpo para cada herói?
    ///
    /// O palco é filmado por uma câmera fora do enquadramento do jogo: se um
    /// prefab do SPUM sumir da tabela, ou o <c>SortingGroup</c> deixar um boneco
    /// atrás do outro, nada estoura — a faixa fica com gente faltando e o
    /// relatório continua em 0 falhas. É o padrão de erro mais caro do projeto,
    /// e por isso a contagem é conferida aqui, não só olhada na captura.
    /// </summary>
    void ReportarEstrada(JourneyManager jm)
    {
        if (jm == null) return;

        if (jm.trailRoad == null)
        {
            Line("FALHA: a jornada não tem estrada ligada — rode 'Montar Cena'.");
            return;
        }

        int esperado = jm.PartyAtual != null ? jm.PartyAtual.Count : 0;
        int corpos = jm.trailRoad.CorposEmCena;

        string elenco = jm.PartyAtual == null ? "" : string.Join(", ",
            jm.PartyAtual.Select(h => $"{h.heroName} ({h.heroClass})"));

        ReportarMao(jm);

        float altura = jm.trailRoad.AlturaDoGrupoEmPixels;
        float alturaDaFicha = jm.trailRoad.GetComponent<RectTransform>().rect.height;
        float ocupacao = alturaDaFicha > 1f ? altura / alturaDaFicha : 0f;

        Line($"estrada: {corpos} corpos para {esperado} heróis"
           + $" | boneco com {altura:F0}px numa ficha de {alturaDaFicha:F0}px"
           + $" ({ocupacao:P0}) | {elenco}");

        if (corpos != esperado)
            Line("FALHA: a fila da estrada não bate com a party.");

        // O que se mede é a proporção, não o tamanho.
        //
        // A primeira versão exigia 80px absolutos, medida tirada de quando a
        // estrada era uma faixa de 300px de altura. Ao virar peça de mapa, a
        // ficha encolheu e a trava passou a acusar falha num enquadramento
        // correto. Proporção é invariante ao tamanho da ficha e continua pegando
        // o defeito de verdade: grupo perdido no canto ou com a cabeça cortada.
        if (corpos > 0 && (ocupacao < 0.5f || ocupacao > 0.95f))
            Line("FALHA: o grupo não está enquadrado na ficha — o palco perdeu a medida.");

        // Piso absoluto: proporção certa numa ficha minúscula continua sendo um
        // formigueiro no mapa.
        if (corpos > 0 && altura < 45f)
            Line("FALHA: o grupo ficou pequeno demais para se reconhecer no mapa.");
    }

    /// <summary>
    /// O momento da quebra aparece mesmo?
    ///
    /// Não dá para esperar que uma jornada de teste produza uma aflição: numa
    /// execução ninguém quebra, na outra quebram dois. O momento é chamado aqui
    /// à mão, com um herói do roster, e o que se confere é o que a lição do
    /// projeto manda conferir — não que o objeto exista na hierarquia, mas que
    /// ele esteja <b>ativo, visível e com o rosto certo</b>.
    /// </summary>
    IEnumerator TestarMomentoDaQuebra()
    {
        var guilda = GuildManager.Instance;
        HeroData cobaia = guilda != null
            ? guilda.roster.FirstOrDefault(h => h != null && h.IsAlive)
            : null;

        if (cobaia == null)
        {
            Line("pulado: ninguém vivo no roster");
            yield break;
        }

        MentalState antes = cobaia.mentalState;
        cobaia.mentalState = MentalState.Hopeless;

        var runner = UIManager.Instance;
        if (runner == null)
        {
            Line("pulado: UIManager ausente");
            cobaia.mentalState = antes;
            yield break;
        }

        runner.StartCoroutine(AfflictionMoment.Mostrar(cobaia));

        // Depois da entrada e antes da saída: o painel fica ~1,5s no ar.
        yield return new WaitForSecondsRealtime(0.6f);

        GameObject painel = GameObject.Find("AfflictionMoment");

        if (painel == null)
        {
            Line("FALHA: o momento da quebra não chegou a existir.");
            cobaia.mentalState = antes;
            yield break;
        }

        var grupo = painel.GetComponent<CanvasGroup>();
        var textos = painel.GetComponentsInChildren<TMP_Text>(true)
                           .Where(t => t.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(t.text))
                           .ToList();

        int retratos = painel.GetComponentsInChildren<Image>(true)
                             .Count(i => i.gameObject.activeInHierarchy && i.sprite != null);

        float alpha = grupo != null ? grupo.alpha : -1f;

        Line($"painel no ar: ativo={painel.activeInHierarchy} alpha={alpha:0.00} "
           + $"textos={textos.Count} retrato={(retratos > 0 ? "sim" : "NÃO")}");

        foreach (var t in textos)
            Line($"  {t.gameObject.name}: '{StripTags(t.text)}'");

        if (alpha < 0.5f)
            Line("FALHA: o momento existe mas está transparente — ninguém o veria.");

        bool dizONome = textos.Any(t => StripTags(t.text).Contains(cobaia.heroName));
        if (!dizONome)
            Line($"FALHA: o momento não diz de quem é a quebra ({cobaia.heroName}).");

        yield return Capture("momento_quebra");

        // O painel se destrói sozinho ao fim da corrotina; o estado do herói,
        // não — e este teste não pode deixar alguém afligido para as seções
        // seguintes medirem.
        yield return new WaitForSecondsRealtime(1.4f);
        cobaia.mentalState = antes;
    }

    /// <summary>
    /// A tocha muda a tela?
    ///
    /// Como o momento da quebra, isto não pode depender de a jornada de teste
    /// por acaso ficar sem tocha. O contador é zerado à mão no meio da estrada e
    /// o que se mede é o alfa da camada de escuridão do véu — antes e depois.
    /// Sem o "antes", um valor alto não provaria nada: poderia já estar assim.
    /// </summary>
    IEnumerator MedirALuzDaTocha(JourneyManager jm)
    {
        Canvas canvas = UIUtil.CanvasPrincipal();
        Transform achado = canvas != null ? canvas.transform.Find("ScreenVeil/Img_Escuridao") : null;
        var escuridao = achado != null ? achado.GetComponent<Image>() : null;

        if (escuridao == null)
        {
            Line("luz da tocha: camada de escuridão ausente no véu — o efeito não tem onde aparecer");
            yield break;
        }

        int tochasAntes = jm.torches;
        float alphaAntes = escuridao.color.a;

        jm.torches = 0;

        // A transição leva cerca de um segundo; um pouco mais para assentar.
        yield return new WaitForSecondsRealtime(1.6f);

        float alphaDepois = escuridao.color.a;

        Line($"luz da tocha: {tochasAntes} tochas → escuridão {alphaAntes:0.00} | "
           + $"0 tochas → {alphaDepois:0.00}");

        if (alphaDepois <= alphaAntes + 0.05f)
            Line("FALHA: apagar as tochas não escureceu a tela.");

        yield return Capture("jornada_sem_tocha");

        // Devolve as tochas: esta é a mesma jornada que o resto do teste mede, e
        // deixá-la no escuro somaria estresse que nada no jogo pediu.
        jm.torches = tochasAntes;
        yield return new WaitForSecondsRealtime(1.2f);
    }

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

    #region Diário da partida

    /// <summary>
    /// O ciclo pelos olhos de quem joga: o que a guilda mostra ao abrir, o que o
    /// quadro oferece, o que a preparação pede, o que a volta entrega e o que
    /// mudou na guilda depois — três voltas seguidas.
    ///
    /// <b>Por que não jogar as três jornadas.</b> A jornada inteira já é dirigida
    /// e medida na seção "JORNADA AUTOMATICA", e repeti-la triplicaria o tempo do
    /// teste sem dizer nada novo. O que falta ao relatório, e é o que esta seção
    /// cobre, é a <i>moldura</i>: as decisões que o jogador toma fora da estrada,
    /// e se elas mudam de uma volta para a outra.
    /// </summary>
    IEnumerator DiarioDaPartida()
    {
        var guilda = GuildManager.Instance;
        var quadro = QuestManager.Instance;
        var run = RunManager.Instance;

        if (guilda == null || quadro == null)
        {
            Line("pulada: GuildManager ou QuestManager ausentes");
            yield break;
        }

        for (int volta = 1; volta <= 3; volta++)
        {
            Line("");
            Line($"── VOLTA {volta} " + new string('─', 56));

            // ── A guilda ao abrir ──
            var vivos = guilda.roster.Where(h => h != null && !h.isDead).ToList();
            var aptos = vivos.Where(h => h.IsFitForJourney).ToList();

            Line($"  a guilda tem {guilda.gold} de ouro, {vivos.Count} herói(s) e {guilda.reputation} de reputação"
               + (run != null ? $" · ciclo {run.Cycle}, corrupção {run.Corruption:F0}" : ""));

            foreach (var h in vivos.Take(6))
            {
                string estado = h.IsFitForJourney ? "pronto" : "RECUSA PARTIR";
                if (h.isInjured) estado += ", ferido";
                if (h.mentalState != MentalState.Normal) estado += $", {MentalStateUtil.GetLabel(h.mentalState)}";

                DeckData deck = DeckRepository.GetDeck(h);
                int cartas = deck != null && deck.cards != null ? deck.cards.Count : 0;

                Line($"    {h.heroName,-14} Nv.{h.level}  ❤️ {h.currentHp}/{h.maxHp}"
                   + $"  🧠 {h.stress:F0}  ⚔️ arma {h.weaponLevel}  🃏 {cartas} cartas  — {estado}");
            }

            if (aptos.Count < 4)
                Line($"  ATENÇÃO: só {aptos.Count} herói(s) em condição de partir — o grupo sai incompleto");

            // ── O que o guia manda fazer ──
            var guia = UnityEngine.Object.FindObjectOfType<GuildGuide>(true);
            if (guia != null && guia.linha != null && !string.IsNullOrWhiteSpace(guia.linha.text))
                Line($"  o guia da guilda diz: \"{StripTags(guia.linha.text)}\"");
            else
                Line("  o guia da guilda não diz nada nesta volta");

            // ── O que o quadro oferece ──
            var missoes = quadro.GetQuests();
            Line($"  o quadro oferece {missoes.Count} contrato(s):");

            foreach (var q in missoes)
            {
                if (q == null) continue;

                int dias = q.GetActualDuration();
                Line($"    {q.questName,-32} {StripTags(BiomeUtil.GetDisplayName(q.biomeType)),-12} "
                   + $"{dias,2} dias · {q.GetTotalReward(dias),4} de ouro · corrupção {q.corruptionLevel,3}"
                   + (q.isFinalBoss ? "  ◆ CHEFE SUPREMO" : ""));
            }

            // ── O que a carroça traz nesta volta ──
            var frascos = CycleStock.Escolher(ItemCatalog.Pocoes, 2, "mercado-frascos");
            int oferta = CycleStock.Numero("forja-oferta", 0, 3);
            Line($"  o mercador traz: {string.Join(", ", frascos.Select(p => p.nome))}"
               + $"  ·  a forja está com desconto em: {(oferta == 0 ? "nada" : oferta == 1 ? "armas" : "armaduras")}");

            // ── O que o ouro compra agora ──
            Line($"  com {guilda.gold} de ouro dá para: {OQueOuroCompra(guilda.gold)}");

            // A volta seguinte: o ciclo anda como andaria ao fim de uma jornada.
            if (volta < 3 && run != null)
            {
                run.AdvanceCycle();
                quadro.RenovarQuadro();
                guilda.AddGold(400);   // o que uma jornada média entrega
                yield return new WaitForSeconds(0.2f);
            }
        }

        Line("");
        Line("  (o ouro de cada volta é o que a auditoria mede como jornada média: ~400)");
        yield return null;
    }

    /// <summary>
    /// O que a guilda consegue comprar com o que tem — a pergunta que o jogador
    /// faz ao voltar da estrada.
    /// </summary>
    static string OQueOuroCompra(int ouro)
    {
        var lista = new List<string>();

        int reliquias = ItemCatalog.Reliquias.Count(r => r.preco <= ouro);
        if (reliquias > 0) lista.Add($"{reliquias} das {ItemCatalog.Reliquias.Count} relíquias");

        var forja = UnityEngine.Object.FindObjectOfType<ForgeManager>(true);
        if (forja != null)
        {
            var alvo = HeroFactory.CreateHero("Teste", HeroClass.Warrior, 3);
            int custo = forja.WeaponCost(alvo);
            if (custo <= ouro) lista.Add($"{ouro / custo} melhoria(s) de arma");
            UnityEngine.Object.DestroyImmediate(alvo);
        }

        var lib = LibraryManager.Instance;
        if (lib != null && lib.upgradeBaseCost <= ouro) lista.Add("melhorar a Biblioteca");

        return lista.Count > 0 ? string.Join(", ", lista) : "quase nada";
    }

    #endregion

    #region Auditoria de interação

    /// <summary>
    /// O que o jogador encontra em cada tela, contado na tela e não no palpite:
    /// quantos botões, quantos deles estão desligados, e se alguma linha de texto
    /// explica o que fazer ali.
    ///
    /// <b>Por que o desligado importa.</b> Um botão apagado sem motivo escrito é
    /// a forma mais comum de o jogador travar: ele vê a ação, não pode usá-la, e
    /// não sabe o que fazer para poder. As salas refeitas escrevem o motivo na
    /// própria ficha; esta varredura mostra quais ainda não.
    /// </summary>
    IEnumerator AuditarTelas()
    {
        var ui = UIManager.Instance;
        if (ui == null)
        {
            Line("pulada: UIManager ausente");
            yield break;
        }

        Line("  tela                      botões   desligados   textos   dica na tela");
        Line("  ────────────────────────────────────────────────────────────────────");

        var telas = new (string nome, GameObject painel, System.Action abrir)[]
        {
            ("Guilda",        ui.guildPanel,       () => ui.ShowGuildScreen()),
            ("Taverna",       ui.tavernPanel,      () => ui.ShowTavern()),
            ("Biblioteca",    ui.libraryPanel,     () => ui.ShowLibrary()),
            ("Forja",         ui.forgePanel,       () => ui.ShowForge()),
            ("Mercado",       ui.marketPanel,      () => ui.ShowMarket()),
            ("Cemitério",     ui.cemeteryPanel,    () => ui.ShowCemetery()),
            ("Sala de Mapas", ui.mapRoomPanel,     () => ui.ShowMapRoom()),
            ("Baralhos",      ui.deckManagerPanel, () => ui.ShowDeckManager())
        };

        foreach (var tela in telas)
        {
            if (tela.painel == null)
            {
                Line($"  {tela.nome,-24} AUSENTE na cena");
                continue;
            }

            tela.abrir();
            yield return new WaitForSeconds(0.25f);

            int ativos = 0, desligados = 0, textos = 0;
            bool temDica = false;

            foreach (Button b in tela.painel.GetComponentsInChildren<Button>(true))
            {
                if (!b.gameObject.activeInHierarchy) continue;
                if (b.interactable) ativos++; else desligados++;
            }

            foreach (TMP_Text t in tela.painel.GetComponentsInChildren<TMP_Text>(true))
            {
                if (!t.gameObject.activeInHierarchy || string.IsNullOrWhiteSpace(t.text)) continue;
                textos++;

                // A dica é a linha que ensina a sala: reconhecida pelo nome do
                // objeto, que é como as salas refeitas a montam.
                //
                // "Guia" entra na conta porque a guilda tem a sua — o GuildGuide
                // escreve no Txt_Guia, e a auditoria vinha acusando a única tela
                // que dá conselho de ser a única sem orientação. O relatório
                // errou sobre o jogo, de novo; o defeito era do critério.
                if (t.name.Contains("Hint") || t.name.Contains("Guia")) temDica = true;
            }

            Line($"  {tela.nome,-24} {ativos,6}   {desligados,10}   {textos,6}   {(temDica ? "sim" : "NÃO"),12}");
        }

        ui.ShowGuildScreen();
        yield return new WaitForSeconds(0.25f);
    }

    /// <summary>
    /// Quantos cliques o jogador gasta para fazer o que ele faz toda volta.
    ///
    /// A conta é dos cliques que <b>este teste</b> executa para chegar lá, e não
    /// de uma tabela escrita à mão: se o caminho mudar, o número muda junto. O
    /// primeiro clique é sempre o da porta da sala, contado a partir da guilda.
    /// </summary>
    IEnumerator CustoDasAcoes()
    {
        var ui = UIManager.Instance;
        if (ui == null || GuildManager.Instance == null)
        {
            Line("pulada: UIManager ou GuildManager ausentes");
            yield break;
        }

        // Ouro de sobra: o objetivo aqui é medir o caminho, não a economia.
        GuildManager.Instance.AddGold(5000);

        int cliques;

        // ── Forjar a arma de um herói ──
        cliques = 0;
        ui.ShowForge(); cliques++;
        yield return new WaitForSeconds(0.25f);

        var forge = ForgeManager.Instance;
        if (forge != null && forge.weaponButton != null && forge.weaponButton.interactable)
        {
            forge.weaponButton.onClick.Invoke(); cliques++;
            yield return new WaitForSeconds(0.2f);
            Relatar("forjar a arma de quem já está na bigorna", cliques);
        }
        else Line("  forjar a arma: indisponível no momento do teste");

        // Trocar de herói custa um clique a mais, e é o caso comum: o jogador
        // quer equipar alguém específico, não quem a sala escolheu.
        Relatar("forjar a arma de outro herói da fila", cliques + 1);

        ui.CloseForge();
        yield return new WaitForSeconds(0.2f);

        // ── Comprar uma carta ──
        cliques = 0;
        ui.ShowLibrary(); cliques++;
        yield return new WaitForSeconds(0.25f);

        var library = LibraryManager.Instance;
        Button compra = library != null ? FirstEnabledButton(library.cardsContainer, "Btn_Buy") : null;
        if (compra != null)
        {
            compra.onClick.Invoke(); cliques++;
            yield return new WaitForSeconds(0.2f);
            Relatar("comprar uma carta para quem está na mesa", cliques);
        }
        else Line("  comprar carta: nenhuma disponível no momento do teste");

        ui.CloseLibrary();
        yield return new WaitForSeconds(0.2f);

        // ── Comprar no Mercado ──
        cliques = 0;
        ui.ShowMarket(); cliques++;
        yield return new WaitForSeconds(0.25f);

        var market = MarketManager.Instance;
        if (market != null && market.buyButton != null)
        {
            Button ficha = FirstEnabledButton(market.itemContainer, "Ração");
            if (ficha != null) { ficha.onClick.Invoke(); cliques++; yield return new WaitForSeconds(0.15f); }

            if (market.buyButton.interactable)
            {
                market.buyButton.onClick.Invoke(); cliques++;
                yield return new WaitForSeconds(0.2f);
                Relatar("comprar um item escolhido na carroça", cliques);
            }
        }

        ui.CloseMarket();
        yield return new WaitForSeconds(0.2f);

        // ── Contratar na Taverna ──
        cliques = 0;
        ui.ShowTavern(); cliques++;
        yield return new WaitForSeconds(0.25f);

        var tavern = TavernManager.Instance;
        Button contratar = tavern != null ? FirstEnabledButton(tavern.transform, "Btn_Hire") : null;
        if (contratar == null && ui.tavernPanel != null)
            contratar = FirstEnabledButton(ui.tavernPanel.transform, "Btn_Hire");

        if (contratar != null)
        {
            contratar.onClick.Invoke(); cliques++;
            yield return new WaitForSeconds(0.2f);
            Relatar("contratar o candidato que está na mesa", cliques);
        }
        else Line("  contratar: botão não encontrado no momento do teste");

        ui.CloseTavern();
        yield return new WaitForSeconds(0.2f);

        // ── Trocar uma carta de baralho ──
        cliques = 0;
        ui.ShowDeckManager(); cliques++;
        yield return new WaitForSeconds(0.3f);

        var dm = DeckManager.Instance;
        if (dm != null)
        {
            Button carta = FirstEnabledButton(dm.currentDeckContainer);
            if (carta != null) { carta.onClick.Invoke(); cliques++; yield return new WaitForSeconds(0.15f); }

            Button doAcervo = FirstEnabledButton(dm.collectionContainer);
            if (doAcervo != null) { doAcervo.onClick.Invoke(); cliques++; yield return new WaitForSeconds(0.15f); }

            if (dm.saveButton != null) { dm.saveButton.onClick.Invoke(); cliques++; }

            Relatar("trocar uma carta do baralho e salvar", cliques);
        }

        ui.CloseDeckManager();
        yield return new WaitForSeconds(0.2f);

        Line("");
        Line("  Referência: no jogo, a porta da sala é sempre o primeiro clique. Uma volta");
        Line("  completa pela guilda — visitar as seis salas sem comprar nada — custa 12.");
    }

    void Relatar(string acao, int cliques)
    {
        Line($"  {acao,-52} {cliques,2} clique(s)");
    }

    #endregion

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
    /// <summary>
    /// O que a tela de balanço mostrou, e se a escolha de despojo funciona.
    ///
    /// A escolha trava o botão de voltar de propósito: sem ela, o jogador
    /// passaria batido pela decisão. Se o clique não a destravar, ele fica preso
    /// na tela de vitória — daí o teste clicar de verdade em uma das opções.
    /// </summary>
    IEnumerator RelatarBalanco(JourneyResultUI balanco)
    {
        // Ativas, não filhas. Contar childCount dizia "4 linhas" enquanto as
        // quatro estavam desligadas na hierarquia e a tela aparecia vazia — o
        // clone do molde nasce desligado com ele. Foi a captura que pegou.
        int linhas = 0;
        int desligadas = 0;

        if (balanco.heroContainer != null)
        {
            foreach (Transform filho in balanco.heroContainer)
            {
                if (filho.gameObject.activeInHierarchy) linhas++;
                else desligadas++;
            }
        }

        Line($"  linhas de herói no balanço: {linhas}"
           + (desligadas > 0 ? $"  BUG: {desligadas} ficha(s) na hierarquia sem aparecer na tela" : ""));

        if (balanco.titleText != null)
            Line($"  título: '{StripTags(balanco.titleText.text)}'");
        if (balanco.subtitleText != null)
            Line($"  subtítulo: '{StripTags(balanco.subtitleText.text)}'");
        if (balanco.rewardText != null)
            Line($"  recompensa: '{StripTags(balanco.rewardText.text).Replace("\n", " · ")}'");

        int escolhas = balanco.rewardContainer != null ? balanco.rewardContainer.childCount : 0;
        Line($"  opções de despojo oferecidas: {escolhas}");

        bool travadoAntes = balanco.continueButton != null && !balanco.continueButton.interactable;
        Line($"  botão de voltar travado até escolher: {travadoAntes}");

        if (escolhas > 0)
        {
            Button opcao = balanco.rewardContainer.GetChild(0).GetComponent<Button>();
            if (opcao != null)
            {
                ReportarAlcancavel(opcao);

                int ouroAntes = GuildManager.Instance != null ? GuildManager.Instance.gold : 0;
                opcao.onClick.Invoke();
                yield return new WaitForSeconds(0.25f);

                bool liberou = balanco.continueButton != null && balanco.continueButton.interactable;
                Line($"  após escolher: opções restantes={balanco.rewardContainer.childCount}"
                   + $" | botão liberado={liberou}"
                   + (GuildManager.Instance != null ? $" | ouro {ouroAntes} → {GuildManager.Instance.gold}" : ""));

                if (!liberou)
                    Line("FALHA: escolher o despojo não liberou a saída — jogador preso na tela de vitória");
            }
        }

        yield return Capture("fim_jornada_balanco");
    }

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

        // A saída da jornada passou a ser a tela de balanço; o popup de texto só
        // continua respondendo em cenas montadas antes dela existir. O teste
        // aceita as duas, mas exige que UMA apareça — sem saída, o jogador fica
        // preso na jornada vencida.
        var balanco = JourneyResultUI.Instance;
        bool telaVisivel = balanco != null && balanco.panel != null && balanco.panel.activeInHierarchy;

        GameObject popup = ui.resultPopup;
        bool popupVisivel = popup != null && popup.activeInHierarchy;

        Button fechar;

        if (telaVisivel)
        {
            Line("saída após vencer: TELA DE BALANÇO");
            yield return RelatarBalanco(balanco);
            fechar = balanco.continueButton;
        }
        else
        {
            Line($"saída após vencer: popup de resultado (visível={popupVisivel})");

            if (popup != null)
            {
                Line($"  escala do popup: {popup.transform.localScale.x:0.00} (0 = invisível na prática)");
                var cg = popup.GetComponent<CanvasGroup>();
                if (cg != null) Line($"  alpha={cg.alpha:0.00} blocksRaycasts={cg.blocksRaycasts}");
            }

            fechar = ui.resultCloseButton;
        }

        Line($"botão de saída: {(fechar == null ? "SEM REFERÊNCIA" : fechar.name)}"
           + (fechar != null ? $" interativo={fechar.interactable} ativo={fechar.gameObject.activeInHierarchy}" : ""));

        if (!telaVisivel && !popupVisivel)
            Line("FALHA: venceu a jornada e nenhuma tela de resultado apareceu — jogador fica sem saída");

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

        // O caminho inteiro, não só o nome: a cena tem nove objetos chamados
        // "Viewport", e um relatório que diz apenas "Viewport cobre o botão" não
        // aponta a tela culpada — foi preciso caçá-la à mão no arquivo da cena.
        Line($"  alcançável pelo clique: {ehOAlvo} (quem está por cima: {CaminhoNaCena(topo)})");

        if (!ehOAlvo)
            Line($"  FALHA: '{CaminhoNaCena(topo)}' cobre o botão — o jogador não consegue clicar nele");
    }

    /// <summary>Caminho completo do objeto na hierarquia, para relatório.</summary>
    static string CaminhoNaCena(GameObject go)
    {
        if (go == null) return "<nulo>";

        string caminho = go.name;
        for (Transform t = go.transform.parent; t != null; t = t.parent)
            caminho = $"{t.name} / {caminho}";

        return caminho;
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

        // Foto do nível e da experiência antes de partir: sem ela, "Nv.3" no fim
        // não diz se o herói progrediu ou se já saiu de casa assim.
        var xpAntes = party.ToDictionary(h => h, h => new Vector2Int(h.level, h.xp));

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
        bool golpeCapturado = false;
        bool meioCapturado = false;
        bool jornadaCapturada = false;
        int hpAnterior = PartyHp(party);

        // A jornada sai da tela enquanto o combate acontece, então "painel da
        // jornada inativo" não significa mais que a jornada acabou — significa
        // que ela pode estar apenas cedendo a tela ao combate.
        // Frames gastos esperando animação, para o relatório poder distinguir
        // "a jornada é longa" de "a jornada travou".
        int framesDeTravessia = 0;

        // Por travessia, e não no total: é uma travessia que trava, não a soma
        // delas. 900 frames são ~15 segundos parado no mesmo trecho.
        int framesNestaTravessia = 0;
        int maiorTravessia = 0;

        while ((jm.journeyPanel.activeSelf || IsCombatOpen()) && guard < 600)
        {
            // Enquanto o grupo atravessa um trecho não há nada a clicar, e cada
            // frame de caminhada consumia uma iteração do orçamento — uma
            // jornada de nove dias acabava o limite antes de chegar ao chefe, e
            // o relatório acusava travamento onde só havia animação.
            if (jm.EmTravessia && framesNestaTravessia < 900)
            {
                framesDeTravessia++;
                framesNestaTravessia++;
                if (framesNestaTravessia > maiorTravessia) maiorTravessia = framesNestaTravessia;
                yield return null;
                continue;
            }

            // Fora da travessia: o contador da vez zera. Somar todas e comparar o
            // total com um teto acusava "o grupo ficou preso" numa jornada de
            // nove dias que terminou certinho — cada trecho anda alguns segundos,
            // e nove trechos passam de qualquer teto pensado para um.
            framesNestaTravessia = 0;

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

                    // A ordem do round: a fila precisa ter o grupo mais um por
                    // inimigo vivo, e estar apontando para o grupo enquanto é a
                    // vez do jogador. Sem esta trava, ela pode parar de ser
                    // montada e o combate continuaria "funcionando" — que é o
                    // padrão de erro mais caro deste projeto.
                    ReportarOrdemDoRound();
                    ReportarCampoDeBatalha();

                    yield return Capture("combate_cartas");
                }

                CardData jogada = PlayCombatStep();

                // Uma captura no instante do golpe. A tela parada prova que os
                // corpos estão em cena; só esta prova que eles se mexem — a
                // animação dura meio segundo e some, e nada no relatório
                // distingue um boneco que ataca de um que ficou preso no parado.
                //
                // Só serve carta que fere: é a única que faz o herói avançar e a
                // criatura recuar ao mesmo tempo.
                if (!golpeCapturado && jogada != null && CombatManager.DealsDamage(jogada.combatEffect))
                {
                    golpeCapturado = true;
                    yield return new WaitForSeconds(0.15f);
                    yield return Capture("combate_golpe");
                }

                yield return new WaitForSeconds(0.12f);
                continue;
            }

            Button choice = FindFirstChoiceButton(jm);
            if (choice != null)
            {
                // Uma captura da estrada com a mão na tela. Faltava: só o combate
                // era fotografado, e por isso a mão da jornada saía pela metade do
                // tamanho sem ninguém ver — o leque escala pela altura do
                // container, e as duas telas tinham medidas diferentes.
                if (!jornadaCapturada)
                {
                    jornadaCapturada = true;

                    // Deixa a tela assentar antes de medir e fotografar.
                    //
                    // O botão de escolha aparece no mesmo frame em que o evento
                    // abre, e nesse instante nada do que é animado já aconteceu:
                    // o leque da mão só se aplica no Update seguinte (Update roda
                    // antes das corrotinas), e a descrição do evento ainda está
                    // sendo digitada letra a letra. Medir aqui acusava as cinco
                    // cartas como "empilhadas" e fotografava textos com uma letra.
                    yield return new WaitForSeconds(0.6f);

                    ReportarEstrada(jm);
                    yield return Capture("jornada_mao");
                }

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

                // Uma captura no meio da estrada. A do dia 1 mostra o mapa
                // vazio à esquerda, e é justamente o **caminho já andado** que
                // se quer ver: sem esta imagem, ninguém sabe se o percorrido
                // aparece ou some atrás do grupo.
                if (!meioCapturado && routeChoices >= 3)
                {
                    meioCapturado = true;
                    yield return Capture("jornada_meio");
                    yield return MedirALuzDaTocha(jm);
                }

                continue;
            }

            // Sem UI de escolhas, o botão de turno é a saída prevista no código.
            // Conta como ação sem progresso: EndTurn é um no-op quando a jornada
            // não está esperando escolha (ou quando o grupo já descansou neste
            // trecho), e sem contar aqui o laço girava até o limite de segurança
            // sem nunca acusar o travamento.
            if (jm.endTurnButton != null && jm.endTurnButton.interactable)
            {
                jm.endTurnButton.onClick.Invoke();
                eventsResolved++;
                clicksSemProgresso++;
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
        Line($"painel ainda ativo ao fim: {jm.journeyPanel.activeSelf}"
           + $" (iterações: {guard} | frames de travessia: {framesDeTravessia}"
           + $" | a mais longa: {maiorTravessia})");

        if (maiorTravessia >= 900)
            Line("FALHA: uma travessia não terminou — o grupo ficou preso no meio do caminho.");

        // Jornada que não termina sozinha é quebra, não observação.
        //
        // Isto era um "ATENÇÃO", e por isso uma jornada que rodou 600 iterações
        // sem percorrer um único dia — porque o teste tinha perdido o caminho
        // para os pontos do mapa — saiu no relatório como PLAY MODE OK, com
        // zero falhas. O aviso estava lá, no meio de 200 linhas, e passou.
        if (guard >= 600)
        {
            Line("FALHA: laço de segurança atingido — a jornada não terminou sozinha");
            DumpStuckState(jm);
        }

        if (jm.DaysElapsed == 0)
            Line("FALHA: a jornada não andou um dia sequer — a rota não pôde ser escolhida.");

        Section("ESTADO DOS HEROIS APOS A JORNADA");
        foreach (var h in party)
        {
            Vector2Int antes = xpAntes.TryGetValue(h, out var v) ? v : new Vector2Int(h.level, h.xp);

            string progresso = h.isDead
                ? "sem XP (morreu)"
                : antes.x != h.level
                    ? $"SUBIU Nv.{antes.x} → Nv.{h.level} ({h.xp}/{h.XpMetaAtual} XP)"
                    : $"Nv.{h.level} | XP {antes.y} → {h.xp}/{h.XpMetaAtual}";

            Line($"  {h.heroName}: HP {h.currentHp}/{h.maxHp} | estresse {Mathf.RoundToInt(h.stress)} " +
                 $"| {(h.isDead ? "MORTO" : h.isOnDeathsDoor ? "beira da morte" : "vivo")} " +
                 $"| {MentalStateUtil.GetLabel(h.mentalState)} | {progresso}");
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
        // A saída da jornada deixou de ser o popup de texto e passou a ser a tela
        // de balanço. Sem tratá-la aqui, o laço da jornada girava até o limite de
        // segurança: a jornada já tinha terminado, mas nada na tela respondia ao
        // que o laço sabia clicar.
        var balanco = JourneyResultUI.Instance;
        if (balanco != null && balanco.panel != null && balanco.panel.activeInHierarchy)
        {
            bool travado = balanco.continueButton != null && !balanco.continueButton.interactable;

            // O botão de voltar só destrava depois do despojo escolhido.
            if (travado && balanco.rewardContainer != null && balanco.rewardContainer.childCount > 0)
            {
                Button opcao = balanco.rewardContainer.GetChild(0).GetComponent<Button>();
                if (opcao != null)
                {
                    opcao.onClick.Invoke();
                    return true;
                }
            }

            if (balanco.continueButton != null && balanco.continueButton.interactable)
            {
                if (!balancoVistoNaJornada)
                {
                    balancoVistoNaJornada = true;
                    string titulo = balanco.titleText != null ? StripTags(balanco.titleText.text) : "(sem título)";
                    Line($"jornada encerrada pela tela de balanço: '{titulo}'");
                }

                balanco.continueButton.onClick.Invoke();
                return true;
            }

            // Tela aberta e sem saída utilizável: isso, sim, prenderia o jogador.
            if (!balancoSemSaidaReportado)
            {
                balancoSemSaidaReportado = true;
                Line("BUG: tela de balanço aberta sem botão de continuar utilizável — o jogador ficaria preso.");
            }
        }

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
        int botoes = 0, interativos = 0;
        if (jm.choiceContainer != null)
            foreach (Transform c in jm.choiceContainer)
            {
                botoes++;
                var b = c.GetComponent<Button>();
                if (b != null && b.interactable && c.gameObject.activeInHierarchy) interativos++;
            }

        Line($"         botões no choiceContainer: {botoes} (interativos: {interativos})");

        // A máquina de estados da jornada é privada, mas é ela que diz de quem o
        // fluxo está esperando — sem isto o travamento fica sem causa.
        Line("         estado da jornada: "
           + $"esperandoEscolha={PrivField<bool>(jm, "isWaitingForChoice")}"
           + $" | escolhendoRota={PrivField<bool>(jm, "isChoosingRoute")}"
           + $" | caminhando={PrivField<bool>(jm, "caminhando")}"
           + $" | jornadaEncerrada={PrivField<bool>(jm, "journeyEnded")}"
           + $" | dia={PrivField<int>(jm, "currentDay")}/{PrivField<int>(jm, "totalDays")}");

        // Corrotina só roda em componente habilitado, em objeto ativo. Uma
        // travessia interrompida no meio deixa a jornada num limbo: não espera
        // escolha, não espera rota, e não acabou.
        Line($"         JourneyManager: objetoAtivo={jm.gameObject.activeInHierarchy}"
           + $" | componenteHabilitado={jm.enabled}");

        var ev = PrivField<EventData>(jm, "currentEvent");
        Line($"         evento atual: {(ev != null ? ev.eventTitle : "(nenhum)")}"
           + $" | tipo={(ev != null ? ev.eventType.ToString() : "-")}"
           + $" | chefe={(ev != null && ev.isBossEvent)}");

        var mapa = PrivField<JourneyMap>(jm, "journeyMap");
        if (mapa != null)
        {
            var escolhas = mapa.GetChoices();
            Line($"         mapa: nóAtual={(mapa.Current != null ? mapa.Current.id.ToString() : "-")}"
               + $" camada={mapa.CurrentLayer}/{mapa.LayerCount}"
               + $" | noFim={mapa.IsAtEnd} | rotas disponíveis={escolhas.Count}");
        }

        var mapUI = JourneyMapUI.Instance;
        if (mapUI != null)
        {
            int nosClicaveis = 0;
            foreach (var _ in mapUI.PontosClicaveis()) nosClicaveis++;
            Line($"         pontos clicáveis no mapa: {nosClicaveis}");
        }

        if (jm.endTurnButton != null)
            Line($"         endTurnButton: interativo={jm.endTurnButton.interactable}");

        Line($"         combate aberto: {IsCombatOpen()}");

        var ui = UIManager.Instance;
        if (ui != null && ui.resultPopup != null)
            Line($"         resultPopup activeSelf={ui.resultPopup.activeSelf} inHierarchy={ui.resultPopup.activeInHierarchy}");
    }

    /// <summary>Lê um campo privado — a jornada guarda seu estado fora do alcance público.</summary>
    static T PrivField<T>(object alvo, string nome)
    {
        FieldInfo f = alvo.GetType().GetField(nome, BindingFlags.NonPublic | BindingFlags.Instance);
        if (f == null) return default(T);
        object v = f.GetValue(alvo);
        return v is T ? (T)v : default(T);
    }

    /// <summary>
    /// Escolhe uma rota clicando num nó habilitado do mapa. Só os alcançáveis
    /// ficam interactable, então basta pegar o primeiro que estiver ativo.
    /// </summary>
    bool ClickMapNode()
    {
        var map = JourneyMapUI.Instance;
        if (map == null) return false;

        foreach (Button btn in map.PontosClicaveis())
        {
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
    /// <summary>
    /// Uma jogada do combate. Devolve a carta que foi jogada, ou null se o turno
    /// terminou — quem chama precisa saber o que aconteceu para fotografar o
    /// golpe, e não uma carta de bloqueio.
    /// </summary>
    CardData PlayCombatStep()
    {
        var cm = CombatManager.Instance;
        if (cm == null) return null;

        combatTurns++;

        if (cm.handContainer != null)
        {
            foreach (Transform child in cm.handContainer)
            {
                var drag = child.GetComponent<CardDragHandler>();
                if (drag == null || drag.Card == null || !child.gameObject.activeInHierarchy) continue;
                if (!cm.CanAffordCard(drag.Card)) continue;

                CardData carta = drag.Card;
                if (cm.TryPlayCardOnAnyTarget(carta))
                {
                    cardsPlayed++;
                    return carta;
                }
            }
        }

        if (cm.endTurnButton != null && cm.endTurnButton.interactable)
            cm.endTurnButton.onClick.Invoke();

        return null;
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

        string path = CaminhoNaCena(popup);

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

    /// <summary>
    /// Igual à anterior, mas olha também objetos inativos.
    ///
    /// Alguns managers moram em painéis que nascem desligados: o <c>Awake</c>
    /// nunca rodou e o singleton está nulo mesmo com o componente na cena. O
    /// relatório vinha acusando "AUSENTE TavernManager" enquanto a taverna
    /// abria, contratava e renovava candidatos duas seções abaixo — e um alarme
    /// falso ensina a ignorar o alarme.
    ///
    /// O retorno continua sendo "o singleton está utilizável agora", que é o que
    /// decide se o resto do teste pode rodar; a distinção fica no texto.
    /// </summary>
    bool ReportSingleton<T>(string name, UnityEngine.Object instance) where T : MonoBehaviour
    {
        if (instance != null)
        {
            Line($"ok   {name}");
            return true;
        }

        T naCena = Resources.FindObjectsOfTypeAll<T>()
            .FirstOrDefault(c => c != null && c.gameObject.scene.rootCount > 0);

        Line(naCena != null
            ? $"ok   {name} (na cena, em painel inativo — singleton nulo até abrir)"
            : $"AUSENTE {name}");

        return false;
    }

    /// <summary>
    /// Campos que podem ficar vazios de propósito. Sem esta lista o relatório
    /// acusava como falha o que é decisão de design, e uma linha que sempre
    /// aparece deixa de ser lida — inclusive quando passa a apontar algo real.
    /// </summary>
    static readonly HashSet<string> ReferenciasOpcionais = new HashSet<string>
    {
        // Sem prefab, a aresta é desenhada em runtime.
        "JourneyMapUI.edgePrefab",

        // Depende de arte de bioma que ainda não existe; QuestData.biomeIcon
        // também é nulo, então ligar o Image não mudaria nada na tela.
        "JourneyManager.biomeIcon",
    };

    /// <summary>Lista campos públicos de referência que ficaram nulos no Inspector.</summary>
    void AuditInspector(MonoBehaviour target)
    {
        if (target == null) return;

        string tipo = target.GetType().Name;
        var missing = new List<string>();
        var opcionais = new List<string>();
        FieldInfo[] fields = target.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);

        foreach (var f in fields)
        {
            if (!typeof(UnityEngine.Object).IsAssignableFrom(f.FieldType)) continue;

            var value = f.GetValue(target) as UnityEngine.Object;
            if (value != null) continue;

            if (ReferenciasOpcionais.Contains($"{tipo}.{f.Name}"))
                opcionais.Add(f.Name);
            else
                missing.Add(f.Name);
        }

        string cauda = opcionais.Count > 0
            ? $" (opcionais vazios: {string.Join(", ", opcionais)})"
            : "";

        if (missing.Count == 0)
            Line($"ok   {tipo}: todas as referências ligadas{cauda}");
        else
            Line($"     {tipo}: {missing.Count} nulas → {string.Join(", ", missing)}{cauda}");
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
