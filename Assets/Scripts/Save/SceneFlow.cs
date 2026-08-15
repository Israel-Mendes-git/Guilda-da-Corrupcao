using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Quem leva o jogador do título ao jogo e de volta.
///
/// Parece trivial e não é: os managers do jogo são <c>DontDestroyOnLoad</c>, e
/// isso significa que voltar ao título e começar de novo **não** os recria. Sem
/// o descarte explícito daqui, o segundo "Nova guilda" da sessão herdaria o ouro,
/// o roster e o ciclo da partida anterior, e o <c>GuildManager</c> antigo ficaria
/// escrevendo num texto de UI que já foi destruído junto com a cena.
///
/// É a mesma família de erro que a Fase 2.5 pegou no painel das salas: a primeira
/// vez funciona, a segunda não, e nenhum teste percebe porque todos testam a
/// primeira.
/// </summary>
public static class SceneFlow
{
    public const string CenaDoJogo = "SampleScene";
    public const string CenaDoMenu = "MainMenu";

    /// <summary>Estamos na cena de jogo? A pausa e o autosave perguntam isto.</summary>
    public static bool NoJogo => SceneManager.GetActiveScene().name == CenaDoJogo;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded -= AoCarregarCena;
        SceneManager.sceneLoaded += AoCarregarCena;

        // A primeira cena já carregou quando este método roda, então ela não
        // passa pelo callback — se houver save pendente (o caso do Editor
        // entrando direto na cena de jogo), aplica agora.
        AplicarPendente();
    }

    static void AoCarregarCena(Scene cena, LoadSceneMode modo)
    {
        AplicarPendente();
    }

    /// <summary>
    /// Reconstrói o jogo a partir do save escolhido no menu.
    ///
    /// Roda no <c>sceneLoaded</c>, ou seja, depois de todos os <c>Awake</c> e
    /// antes dos <c>Start</c>. O momento não é detalhe: é ele que faz o
    /// <c>GuildManager.Start</c> encontrar o roster já cheio e não criar o elenco
    /// inicial por cima do que foi carregado.
    /// </summary>
    static void AplicarPendente()
    {
        SaveGame pendente = SaveSystem.PendingLoad;
        if (pendente == null) return;

        SaveSystem.PendingLoad = null;

        if (GuildManager.Instance == null) return;   // não é a cena do jogo

        if (!GameStateIO.Aplicar(pendente))
            Debug.LogError("SceneFlow: o save não pôde ser aplicado; a partida começa nova.");
    }

    #region O que o menu chama

    /// <summary>Funda uma guilda nova e entra no jogo.</summary>
    public static void NovaPartida()
    {
        SaveSystem.PendingLoad = null;

        // O autosave da partida anterior sai de cena: deixá-lo faria o
        // "Continuar" do título apontar para a guilda que o jogador acabou de
        // abandonar, até a primeira jornada nova gravar por cima.
        SaveSystem.DescartarAutosave();

        DescartarMundo();
        SceneManager.LoadScene(CenaDoJogo);
    }

    /// <summary>
    /// Retoma o autosave. Devolve false quando não há o que retomar — a tela
    /// precisa saber para dizer, em vez de carregar uma partida vazia.
    /// </summary>
    public static bool Continuar() => Carregar(SaveSystem.SlotAuto);

    public static bool Carregar(string slot)
    {
        SaveGame dados = SaveSystem.Ler(slot);
        if (dados == null) return false;

        SaveSystem.PendingLoad = dados;

        DescartarMundo();
        SceneManager.LoadScene(CenaDoJogo);
        return true;
    }

    /// <summary>Volta ao título. O que estiver por salvar já foi salvo, ou se perde.</summary>
    public static void VoltarAoTitulo()
    {
        SaveSystem.PendingLoad = null;

        DescartarMundo();
        SceneManager.LoadScene(CenaDoMenu);
    }

    public static void SairDoJogo()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    #endregion

    /// <summary>
    /// Apaga o mundo da partida anterior.
    ///
    /// Os três managers destruídos aqui sobrevivem à troca de cena por
    /// <c>DontDestroyOnLoad</c> e guardam referências de UI da cena que está indo
    /// embora. Deixá-los vivos faria a cena nova nascer com o
    /// <c>Instance</c> ocupado — o <c>Awake</c> do novo se autodestrói ao ver que
    /// já existe um — e o jogo continuaria falando com o manager morto.
    ///
    /// O relógio da run (<see cref="RunManager"/>) e o áudio não são destruídos:
    /// o primeiro é zerado, porque recriá-lo faria toda a UI que o escuta perder
    /// a inscrição; o segundo não guarda nada da partida.
    /// </summary>
    public static void DescartarMundo()
    {
        DestruirSingleton(GuildManager.Instance);
        DestruirSingleton(QuestManager.Instance);
        DestruirSingleton(UIManager.Instance);

        if (RunManager.Existe) RunManager.Instance.StartNewRun();

        DeckRepository.Limpar();
        GameAudio.Parar();
    }

    static void DestruirSingleton(MonoBehaviour alvo)
    {
        if (alvo == null) return;
        Object.Destroy(alvo.gameObject);
    }
}
