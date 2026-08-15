using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// O menu de pausa da cena de jogo, no ESC.
///
/// <b>Não mexe em <c>Time.timeScale</c>.</b> Este é um jogo de turnos por
/// clique: nada avança sozinho enquanto o painel está aberto, e zerar a escala
/// de tempo congelaria as corrotinas de fade e de transição da jornada — algumas
/// delas responsáveis por avançar o próprio fluxo, que ficariam presas no meio
/// do caminho. O que a pausa faz é cobrir a tela e tirar os cliques do jogo,
/// que é o efeito que se quer.
///
/// <b>O componente não mora no painel</b>, e sim num objeto sempre ativo ao lado
/// dele. Tem que ser assim: o painel nasce desligado, e <c>Update</c> só roda em
/// objeto ativo — com o componente dentro do painel, a tecla ESC só funcionaria
/// depois de o jogador já ter aberto a pausa de outro jeito, que é justamente o
/// que não existe.
/// </summary>
public class PauseMenuUI : MonoBehaviour
{
    private static PauseMenuUI instance;

    public static PauseMenuUI Instance
    {
        get
        {
            if (instance != null) return instance;

            foreach (var candidato in Resources.FindObjectsOfTypeAll<PauseMenuUI>())
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
    public TMP_Text titleText;
    public TMP_Text statusText;

    [Header("Botões")]
    public Button resumeButton;
    public Button saveButton;
    public Button loadButton;
    public Button optionsButton;
    public Button titleButton;
    public Button quitButton;

    [Header("Telas irmãs")]
    public SaveSlotsUI saveSlots;
    public OptionsUI options;

    public bool Aberto => panel != null && panel.activeSelf;

    void Awake()
    {
        if (instance == null || instance == this) instance = this;
    }

    void Start()
    {
        Ligar(resumeButton, Fechar);
        Ligar(saveButton, AbrirSalvar);
        Ligar(loadButton, AbrirCarregar);
        Ligar(optionsButton, AbrirOpcoes);
        Ligar(titleButton, VoltarAoTitulo);
        Ligar(quitButton, Sair);
    }

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        // Com uma sub-tela aberta, o ESC pertence a ela — fechar a pausa por
        // baixo deixaria as opções flutuando sobre o jogo sem como voltar.
        if (options != null && options.panel != null && options.panel.activeSelf) return;
        if (saveSlots != null && saveSlots.panel != null && saveSlots.panel.activeSelf) return;

        if (Aberto) Fechar();
        else Abrir();
    }

    public void Abrir()
    {
        if (panel == null) return;

        GameAudio.Efeito(Sfx.Click);

        panel.SetActive(true);
        panel.transform.SetAsLastSibling();

        Atualizar();
    }

    public void Fechar()
    {
        if (panel == null) return;

        GameAudio.Efeito(Sfx.Click);
        panel.SetActive(false);
    }

    void Atualizar()
    {
        if (titleText != null) titleText.text = "PAUSA";

        bool podeSalvar = SaveSystem.PodeSalvarAgora(out string motivo);

        if (saveButton != null) saveButton.interactable = podeSalvar;

        if (statusText != null)
        {
            if (!podeSalvar)
            {
                statusText.text = $"<color=#B04040>Salvar indisponível: {motivo}.</color>";
            }
            else
            {
                var run = RunManager.Existe ? RunManager.Instance : null;
                var guilda = GuildManager.Instance;

                string ciclo = run != null ? $"Ciclo {run.Cycle}" : "";
                string corrupcao = run != null
                    ? $" · Corrupção {Mathf.RoundToInt(run.Corruption)}%" : "";
                string ouro = guilda != null ? $" · {guilda.gold} de ouro" : "";

                statusText.text = $"<size=90%>{ciclo}{corrupcao}{ouro}</size>";
            }
        }
    }

    #region Ações

    void AbrirSalvar()
    {
        GameAudio.Efeito(Sfx.Click);
        saveSlots?.Abrir(SaveSlotsUI.Modo.Salvar, Atualizar);
    }

    void AbrirCarregar()
    {
        GameAudio.Efeito(Sfx.Click);
        saveSlots?.Abrir(SaveSlotsUI.Modo.Carregar, Atualizar);
    }

    void AbrirOpcoes()
    {
        GameAudio.Efeito(Sfx.Click);
        options?.Abrir(Atualizar);
    }

    /// <summary>
    /// Sair para o título perde o que não foi salvo, e por isso pergunta antes.
    /// O autosave é do fim da jornada: sair no meio da guilda pode custar
    /// recrutamentos, compras e edições de deck feitos desde então.
    /// </summary>
    void VoltarAoTitulo()
    {
        Confirmar("Voltar ao título",
                  "O que você fez desde a última jornada será perdido. Salvar antes?",
                  SceneFlow.VoltarAoTitulo);
    }

    void Sair()
    {
        Confirmar("Sair do jogo",
                  "O que você fez desde a última jornada será perdido. Sair mesmo assim?",
                  SceneFlow.SairDoJogo);
    }

    void Confirmar(string titulo, string mensagem, System.Action acao)
    {
        GameAudio.Efeito(Sfx.Click);

        // Sem o UIManager na cena a pergunta não tem onde aparecer; melhor agir
        // do que travar o jogador dentro da pausa.
        if (UIManager.Instance == null)
        {
            acao();
            return;
        }

        UIManager.Instance.ShowConfirm(titulo, mensagem, acao);
    }

    #endregion

    static void Ligar(Button botao, UnityEngine.Events.UnityAction acao)
    {
        if (botao == null) return;

        botao.onClick.RemoveAllListeners();
        botao.onClick.AddListener(acao);
    }
}
