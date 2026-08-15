using UnityEngine;

/// <summary>
/// Liga o fim da run à tela que o mostra, e o botão de recomeçar ao jogo novo.
///
/// Existe separado do <see cref="RunManager"/> de propósito: o relógio da run não
/// precisa saber que existe UI, e a UI não precisa saber como a guilda recomeça.
/// Aqui é o único ponto que conhece os dois.
///
/// Nasce sozinho ao carregar a cena — o mesmo motivo do GameAudio: a cena é
/// montada por código, e um objeto a mais no setup é mais uma coisa para sair de
/// sincronia.
/// </summary>
public class RunFlow : MonoBehaviour
{
    private static RunFlow instance;

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
        RunManager.Instance.onRunEnded += AoTerminarRun;
        RunManager.Instance.onCycleAdvanced += AoAvancarCiclo;

        // O quadro pode já merecer o Chefe Supremo — por exemplo, ao recarregar
        // uma partida em andamento.
        QuestManager.Instance?.GarantirChefeSupremo();
    }

    void OnDestroy()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.onRunEnded -= AoTerminarRun;
            RunManager.Instance.onCycleAdvanced -= AoAvancarCiclo;
        }
    }

    /// <summary>
    /// O mundo piorou, então o que se oferece à guilda muda junto. Sem isto o
    /// quadro guardava missões do ciclo 1 até o fim da run, e a Corrupção subir
    /// não alterava nada do que o jogador via.
    /// </summary>
    void AoAvancarCiclo()
    {
        QuestManager.Instance?.RenovarQuadro();
    }

    void AoTerminarRun(RunState estado, RunEndReason motivo)
    {
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

        UIManager.Instance?.ShowGuildScreen();
    }
}
