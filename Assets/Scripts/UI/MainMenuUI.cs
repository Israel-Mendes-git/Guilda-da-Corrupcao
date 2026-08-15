using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A tela de título.
///
/// O jogo não tinha nenhuma: abria direto na guilda, com a partida já em
/// andamento e sem meio de escolher continuar, recomeçar ou mexer no volume.
///
/// Vive na cena <c>MainMenu</c>, que não carrega nenhum manager do jogo — é por
/// isso que ela pode falar de saves sem que exista guilda alguma. Quem carrega o
/// mundo é o <see cref="SceneFlow"/>.
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [Header("Cabeçalho")]
    public TMP_Text titleText;
    public TMP_Text subtitleText;

    /// <summary>Recordes e relíquias — o que sobrou das runs anteriores.</summary>
    public TMP_Text metaText;

    [Header("Botões")]
    public Button continueButton;
    public Button newGameButton;
    public Button loadButton;
    public Button shrineButton;
    public Button optionsButton;
    public Button quitButton;

    /// <summary>Explica por que "Continuar" está desligado, quando está.</summary>
    public TMP_Text continueHintText;

    [Header("Telas irmãs")]
    public SaveSlotsUI saveSlots;
    public OptionsUI options;
    public RelicShrineUI shrine;

    void Start()
    {
        Ligar(continueButton, Continuar);
        Ligar(newGameButton, NovaPartida);
        Ligar(loadButton, AbrirCarregar);
        Ligar(shrineButton, AbrirSantuario);
        Ligar(optionsButton, AbrirOpcoes);
        Ligar(quitButton, SceneFlow.SairDoJogo);

        // O título usa a trilha do hub. Não há faixa própria de menu no catálogo,
        // e acrescentar um valor ao enum MusicContext exigiria remontar o
        // catálogo inteiro por um clipe que ainda não existe.
        GameAudio.Tocar(MusicContext.Hub);

        Atualizar();
    }

    /// <summary>Refaz o que a tela mostra. Chamada ao voltar de qualquer sub-tela.</summary>
    public void Atualizar()
    {
        if (titleText != null) titleText.text = "GUILDA DA CORRUPÇÃO";

        if (subtitleText != null)
            subtitleText.text = "Mande gente boa morrer por ouro, até que sobre coragem para o Chefe Supremo.";

        SaveHeader auto = SaveSystem.LerCabecalho(SaveSystem.SlotAuto);
        bool temPartida = auto.exists && !auto.corrupted;

        if (continueButton != null) continueButton.interactable = temPartida;

        if (continueHintText != null)
        {
            if (temPartida)
                continueHintText.text = $"Ciclo {auto.cycle} · {auto.heroesAlive} heróis · "
                                      + $"{auto.gold} de ouro · Corrupção {auto.corruption}%";
            else if (auto.corrupted)
                continueHintText.text = "<color=#B04040>O save automático está corrompido.</color>";
            else
                continueHintText.text = "Nenhuma partida em andamento.";
        }

        if (metaText != null)
        {
            int relicas = MetaProgression.Relics;
            int runs = MetaProgression.TotalRuns;

            if (runs == 0)
            {
                metaText.text = "<size=90%>Primeira guilda.</size>";
            }
            else
            {
                string vitorias = MetaProgression.TotalWins == 1
                    ? "1 vitória" : $"{MetaProgression.TotalWins} vitórias";

                metaText.text = $"<color=#D9B85A>◆ {relicas} relíquias</color>   "
                              + $"<size=85%>{runs} guildas · {vitorias} · melhor: ciclo "
                              + $"{MetaProgression.BestCycle}</size>";
            }
        }

        if (shrineButton != null)
            shrineButton.interactable = MetaProgression.Relics > 0 || MetaProgression.TotalRuns > 0;
    }

    #region Ações

    void Continuar()
    {
        GameAudio.Efeito(Sfx.Click);

        if (!SceneFlow.Continuar())
        {
            // O arquivo sumiu ou quebrou entre abrir o menu e clicar. Melhor
            // dizer e reabilitar a tela do que carregar uma partida vazia.
            Atualizar();
        }
    }

    void NovaPartida()
    {
        GameAudio.Efeito(Sfx.Click);
        SceneFlow.NovaPartida();
    }

    void AbrirCarregar()
    {
        GameAudio.Efeito(Sfx.Click);
        saveSlots?.Abrir(SaveSlotsUI.Modo.Carregar, Atualizar);
    }

    void AbrirSantuario()
    {
        GameAudio.Efeito(Sfx.Click);
        shrine?.Abrir(Atualizar);
    }

    void AbrirOpcoes()
    {
        GameAudio.Efeito(Sfx.Click);
        options?.Abrir(Atualizar);
    }

    #endregion

    static void Ligar(Button botao, UnityEngine.Events.UnityAction acao)
    {
        if (botao == null) return;

        botao.onClick.RemoveAllListeners();
        botao.onClick.AddListener(acao);
    }
}
