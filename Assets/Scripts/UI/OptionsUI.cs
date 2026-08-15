using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// As opções: volume da música, volume dos efeitos, resolução e tela cheia.
///
/// Até aqui o <c>GameAudio</c> trazia <c>volumeMusica</c> e <c>volumeEfeitos</c>
/// no Inspector e ninguém os controlava — quem quisesse baixar a trilha tinha que
/// baixar o volume do sistema.
///
/// O mesmo componente serve ao título e à pausa. Por isso ele guarda quem o
/// abriu e devolve o controle no fechar, em vez de saber para onde voltar.
///
/// A resolução é um passo-a-passo (◀ ▶) e não uma lista suspensa: montar um
/// <c>TMP_Dropdown</c> por código exige um template com viewport, item e
/// scrollbar próprios, e esta cena inteira nasce de código.
/// </summary>
public class OptionsUI : MonoBehaviour
{
    [Header("Painel")]
    public GameObject panel;

    [Header("Áudio")]
    public Slider musicSlider;
    public TMP_Text musicValueText;
    public Slider sfxSlider;
    public TMP_Text sfxValueText;

    [Header("Vídeo")]
    public TMP_Text resolutionText;
    public Button resolutionPrevButton;
    public Button resolutionNextButton;
    public Button fullscreenButton;
    public TMP_Text fullscreenLabel;

    [Header("Rodapé")]
    public Button restoreButton;
    public Button closeButton;

    System.Action aoFechar;
    System.Collections.Generic.List<Vector2Int> resolucoes;
    int indiceResolucao;

    /// <summary>Enquanto true, mexer nos controles não dispara o valor de volta.</summary>
    bool preenchendo;

    // Sem Awake que desative o painel de propósito: este componente **mora no
    // painel**, então ligar o painel roda o Awake dele, que o desligaria de novo
    // — a tela abriria e fecharia no mesmo frame. Quem nasce fechado é a cena,
    // montada assim pelo MenuSceneSetup.

    public void Abrir(System.Action quandoFechar)
    {
        aoFechar = quandoFechar;

        Ligar();
        Preencher();

        if (panel != null)
        {
            panel.SetActive(true);
            panel.transform.SetAsLastSibling();
        }
    }

    void Ligar()
    {
        if (musicSlider != null)
        {
            musicSlider.onValueChanged.RemoveAllListeners();
            musicSlider.onValueChanged.AddListener(v =>
            {
                if (preenchendo) return;

                GameSettings.VolumeMusica = v;
                if (musicValueText != null) musicValueText.text = Porcento(v);
            });
        }

        if (sfxSlider != null)
        {
            sfxSlider.onValueChanged.RemoveAllListeners();
            sfxSlider.onValueChanged.AddListener(v =>
            {
                if (preenchendo) return;

                GameSettings.VolumeEfeitos = v;
                if (sfxValueText != null) sfxValueText.text = Porcento(v);

                // Um efeito de amostra a cada passo: ajustar volume de efeitos
                // sem ouvir efeito nenhum é ajustar às cegas.
                GameAudio.Efeito(Sfx.Click);
            });
        }

        Ligar(resolutionPrevButton, () => TrocarResolucao(-1));
        Ligar(resolutionNextButton, () => TrocarResolucao(+1));
        Ligar(fullscreenButton, AlternarTelaCheia);
        Ligar(restoreButton, Restaurar);
        Ligar(closeButton, Fechar);
    }

    void Preencher()
    {
        preenchendo = true;

        if (musicSlider != null)
        {
            musicSlider.minValue = 0f;
            musicSlider.maxValue = 1f;
            musicSlider.value = GameSettings.VolumeMusica;
        }
        if (musicValueText != null) musicValueText.text = Porcento(GameSettings.VolumeMusica);

        if (sfxSlider != null)
        {
            sfxSlider.minValue = 0f;
            sfxSlider.maxValue = 1f;
            sfxSlider.value = GameSettings.VolumeEfeitos;
        }
        if (sfxValueText != null) sfxValueText.text = Porcento(GameSettings.VolumeEfeitos);

        resolucoes = GameSettings.ResolucoesDisponiveis();
        indiceResolucao = Mathf.Max(0, resolucoes.IndexOf(GameSettings.ResolucaoAtual));

        AtualizarVideo();

        preenchendo = false;
    }

    void AtualizarVideo()
    {
        if (resolutionText != null && resolucoes != null && resolucoes.Count > 0)
        {
            Vector2Int r = resolucoes[Mathf.Clamp(indiceResolucao, 0, resolucoes.Count - 1)];
            resolutionText.text = $"{r.x} × {r.y}";
        }

        bool cheia = GameSettings.TelaCheia;

        if (fullscreenLabel != null)
            fullscreenLabel.text = cheia ? "Tela cheia: SIM" : "Tela cheia: NÃO";

        // No Editor a resolução é a da Game view; mexer nela atrapalharia quem
        // está testando, então o controle aparece desligado e explicado.
#if UNITY_EDITOR
        if (resolutionPrevButton != null) resolutionPrevButton.interactable = false;
        if (resolutionNextButton != null) resolutionNextButton.interactable = false;
        if (resolutionText != null) resolutionText.text += "  <size=70%>(só na build)</size>";
#endif
    }

    void TrocarResolucao(int passo)
    {
        if (resolucoes == null || resolucoes.Count == 0) return;

        indiceResolucao = (indiceResolucao + passo + resolucoes.Count) % resolucoes.Count;
        GameSettings.DefinirResolucao(resolucoes[indiceResolucao]);

        GameAudio.Efeito(Sfx.Click);
        AtualizarVideo();
    }

    void AlternarTelaCheia()
    {
        GameSettings.TelaCheia = !GameSettings.TelaCheia;
        GameAudio.Efeito(Sfx.Click);
        AtualizarVideo();
    }

    void Restaurar()
    {
        GameSettings.Restaurar();
        GameAudio.Efeito(Sfx.Click);
        Preencher();
    }

    void Fechar()
    {
        GameSettings.Salvar();
        GameAudio.Efeito(Sfx.Click);

        if (panel != null) panel.SetActive(false);

        var acao = aoFechar;
        aoFechar = null;
        acao?.Invoke();
    }

    static string Porcento(float v) => Mathf.RoundToInt(v * 100f) + "%";

    static void Ligar(Button botao, UnityEngine.Events.UnityAction acao)
    {
        if (botao == null) return;

        botao.onClick.RemoveAllListeners();
        botao.onClick.AddListener(acao);
    }
}
