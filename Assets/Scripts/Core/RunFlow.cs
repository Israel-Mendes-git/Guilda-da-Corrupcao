using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Liga o fim da run à tela que o mostra, o botão de recomeçar ao jogo novo, e o
/// fim de cada ciclo ao autosave.
///
/// Existe separado do <see cref="RunManager"/> de propósito: o relógio da run não
/// precisa saber que existe UI nem disco, e a UI não precisa saber como a guilda
/// recomeça. Aqui é o único ponto que conhece os três.
///
/// Nasce sozinho ao carregar a cena — o mesmo motivo do GameAudio: a cena é
/// montada por código, e um objeto a mais no setup é mais uma coisa para sair de
/// sincronia.
/// </summary>
public class RunFlow : MonoBehaviour
{
    private static RunFlow instance;

    /// <summary>A qual relógio este fluxo está ligado. Ver <see cref="Religar"/>.</summary>
    private RunManager ligado;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null) return;

        var go = new GameObject("~RunFlow");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<RunFlow>();
    }

    void Start()
    {
        Religar();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += AoCarregarCena;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= AoCarregarCena;
        Desligar();
    }

    void AoCarregarCena(Scene cena, LoadSceneMode modo)
    {
        Religar();
    }

    /// <summary>
    /// (Re)assina os eventos da run e garante o Chefe Supremo no quadro.
    ///
    /// Este objeto sobrevive à troca de cena e o resto do mundo não. Sem religar
    /// a cada carga, uma partida iniciada depois da primeira ficaria com o fim de
    /// run **sem tela** e sem autosave — os eventos ainda apontariam para o
    /// quadro de missões da partida anterior, já destruído.
    /// </summary>
    void Religar()
    {
        RunManager run = RunManager.Instance;
        if (ligado == run) return;

        Desligar();

        ligado = run;
        run.onRunEnded += AoTerminarRun;
        run.onCycleAdvanced += AoAvancarCiclo;

        // O quadro pode já merecer o Chefe Supremo — por exemplo, ao carregar
        // uma partida em andamento.
        QuestManager.Instance?.GarantirChefeSupremo();
    }

    void Desligar()
    {
        if (ligado == null) return;

        ligado.onRunEnded -= AoTerminarRun;
        ligado.onCycleAdvanced -= AoAvancarCiclo;
        ligado = null;
    }

    /// <summary>
    /// O mundo piorou, então o que se oferece à guilda muda junto — e o jogo
    /// grava. Sem isto o quadro guardava missões do ciclo 1 até o fim da run, e a
    /// Corrupção subir não alterava nada do que o jogador via.
    ///
    /// O autosave mora aqui e não no fim da jornada porque este é o único ponto
    /// em que o grupo já voltou para casa: é o estado que o save sabe reconstruir.
    /// </summary>
    void AoAvancarCiclo()
    {
        QuestManager.Instance?.RenovarQuadro();
        SaveSystem.Autosave();
    }

    void AoTerminarRun(RunState estado, RunEndReason motivo)
    {
        // A run acabou: retomar por "Continuar" só devolveria o jogador à tela de
        // fim. O que atravessa é a meta-progressão, e ela vive no perfil.
        SaveSystem.DescartarAutosave();

        RunEndUI tela = RunEndUI.Instance;

        if (tela == null)
        {
            // Sem a tela montada, a run ainda precisa terminar de forma visível —
            // silêncio aqui seria o jogo simplesmente parar de responder.
            Debug.LogWarning($"RunFlow: fim de run ({motivo}) sem RunEndUI na cena.");
            UIManager.Instance?.ShowResult(
                estado == RunState.Won ? "🏆 Corrupção contida" : "💀 A guilda caiu",
                RunManager.DescreverMotivo(motivo),
                Recomecar);
            return;
        }

        tela.Mostrar(RunManager.Instance, Recomecar);
    }

    void Recomecar()
    {
        RunManager.Instance.StartNewRun();
        GuildManager.Instance?.ResetForNewRun();
        QuestManager.Instance?.ClearQuests();
        DeckRepository.Limpar();

        UIManager.Instance?.ShowGuildScreen();

        // A guilda nova nasce salva: fechar o jogo agora não deve custar a
        // fundação inteira.
        SaveSystem.Autosave();
    }
}
