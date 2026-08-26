using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// O fim de uma run — vitória ou queda da guilda.
///
/// Antes não existia: o jogo não tinha como terminar, então também não tinha o
/// momento em que se olha para trás. É aqui que a run vira história — quantos
/// ciclos, quantos morreram, o que sobrou para a próxima.
///
/// Montada por código junto com o resto da cena, como a <see cref="JourneyResultUI"/>,
/// e resolvida sob demanda pelo mesmo motivo: nasce desativada, e quem a abre
/// precisa do singleton antes do Awake rodar.
/// </summary>
public class RunEndUI : MonoBehaviour
{
    private static RunEndUI instance;

    public static RunEndUI Instance
    {
        get
        {
            if (instance != null) return instance;

            foreach (var candidato in Resources.FindObjectsOfTypeAll<RunEndUI>())
            {
                if (candidato == null || candidato.gameObject.scene.rootCount == 0) continue;
                instance = candidato;
                break;
            }

            return instance;
        }
    }

    [Header("Painel")]
    public GameObject panel;

    [Header("Texto")]
    public TMP_Text titleText;
    public TMP_Text reasonText;
    public TMP_Text statsText;
    public TMP_Text metaText;

    [Header("Botões")]
    public Button newRunButton;

    private System.Action onNewRun;

    void Awake()
    {
        if (instance == null || instance == this) instance = this;
    }

    public void Mostrar(RunManager run, System.Action aoRecomecar)
    {
        if (run == null) return;

        onNewRun = aoRecomecar;

        if (newRunButton != null)
        {
            newRunButton.onClick.RemoveAllListeners();
            newRunButton.onClick.AddListener(Recomecar);
            newRunButton.interactable = true;
        }

        if (panel != null)
        {
            panel.SetActive(true);
            panel.transform.SetAsLastSibling();
        }

        bool venceu = run.State == RunState.Won;

        if (titleText != null)
            titleText.text = venceu
                ? "<color=#D9B85A>A CORRUPÇÃO FOI CONTIDA</color>"
                : "<color=#B04040>A GUILDA CAIU</color>";

        if (reasonText != null)
            reasonText.text = RunManager.DescreverMotivo(run.EndReason);

        if (statsText != null)
        {
            string ciclos = run.Cycle == 1 ? "1 jornada" : $"{run.Cycle} jornadas";
            string mortos = run.Fallen == 0
                ? "nenhum herói perdido"
                : run.Fallen == 1 ? "1 herói perdido" : $"{run.Fallen} heróis perdidos";

            statsText.text = $"{ciclos} · {mortos}\n"
                           + $"Corrupção final: {Mathf.RoundToInt(run.Corruption)}%";
        }

        // O que atravessa: é o que faz uma derrota valer alguma coisa.
        if (metaText != null)
        {
            int ganho = MetaProgression.PreviewReward(run);
            metaText.text = $"<color=#D9B85A>◆ +{ganho} memórias</color>   "
                          + $"<size=85%>(total: {MetaProgression.Memorias})</size>";
        }

        GameAudio.Parar();
    }

    void Recomecar()
    {
        if (panel != null) panel.SetActive(false);

        var acao = onNewRun;
        onNewRun = null;
        acao?.Invoke();
    }
}
