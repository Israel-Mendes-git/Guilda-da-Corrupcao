using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// O balanço de uma jornada, herói a herói.
///
/// Antes, voltar para casa era um popup de texto corrido — sobreviventes, perdas
/// e ouro numa string só. O que a jornada custou e rendeu ficava ilegível
/// justamente no momento em que o jogo pede a maior decisão: valeu a pena?
///
/// A tela é montada por código, como o resto da cena (<see cref="GuildSceneSetup"/>),
/// e preenchida a partir de um <see cref="JourneyReport"/> que o
/// <see cref="JourneyManager"/> entrega pronto.
/// </summary>
public class JourneyResultUI : MonoBehaviour
{
    private static JourneyResultUI instance;

    /// <summary>
    /// Resolvido sob demanda: a tela nasce desativada, então o <c>Awake</c> só
    /// rodaria depois de alguém abri-la — e quem abre precisa do singleton antes.
    /// Mesma armadilha que deixou o DeckManager inalcançável.
    /// </summary>
    public static JourneyResultUI Instance
    {
        get
        {
            if (instance != null) return instance;

            foreach (var candidato in Resources.FindObjectsOfTypeAll<JourneyResultUI>())
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

    [Header("Cabeçalho")]
    public TMP_Text titleText;
    public TMP_Text subtitleText;

    [Header("Heróis")]
    public Transform heroContainer;
    public GameObject heroLinePrefab;

    [Header("Recompensa")]
    public TMP_Text rewardText;

    [Header("Escolha de recompensa")]
    public TMP_Text rewardPromptText;
    public Transform rewardContainer;
    public GameObject rewardButtonPrefab;

    [Header("Botões")]
    public Button continueButton;

    private System.Action onContinue;

    void Awake()
    {
        if (instance == null || instance == this) instance = this;
        WireButtons();
    }

    private bool wired;

    void WireButtons()
    {
        if (wired || continueButton == null) return;
        wired = true;

        continueButton.onClick.RemoveAllListeners();
        continueButton.onClick.AddListener(Fechar);
    }

    public void Mostrar(JourneyReport report, System.Action aoContinuar)
    {
        if (report == null) return;

        WireButtons();
        onContinue = aoContinuar;

        if (panel != null)
        {
            panel.SetActive(true);
            // Quem abre vai para a frente: a tela nasce como irmã de painéis que
            // continuam ativos e, sem isto, aparece atrás deles.
            panel.transform.SetAsLastSibling();
        }

        PreencherCabecalho(report);
        PreencherHerois(report);
        PreencherRecompensa(report);
        PreencherEscolhas(report);
    }

    /// <summary>
    /// As opções de despojo. Escolher uma encerra a escolha — é decisão, não
    /// lista de compras: o valor está no que se deixa para trás.
    /// </summary>
    void PreencherEscolhas(JourneyReport report)
    {
        if (rewardContainer != null) UIUtil.ClearChildrenNow(rewardContainer);

        bool temEscolha = report.recompensas.Count > 0;

        if (rewardPromptText != null)
        {
            rewardPromptText.gameObject.SetActive(temEscolha);
            rewardPromptText.text = "O que a guilda leva desta viagem?";
        }

        if (!temEscolha || rewardContainer == null || rewardButtonPrefab == null)
        {
            // Sem escolha a fazer, seguir em frente fica liberado na hora.
            if (continueButton != null) continueButton.interactable = true;
            return;
        }

        // Sem escolher, não se volta para a guilda: a decisão é o momento.
        if (continueButton != null) continueButton.interactable = false;

        foreach (var recompensa in report.recompensas)
        {
            JourneyReport.Reward capturada = recompensa;

            GameObject item = Instantiate(rewardButtonPrefab, rewardContainer);
            item.SetActive(true);

            SetText(item, "Title", capturada.titulo);
            SetText(item, "Desc", capturada.descricao);

            Button botao = item.GetComponent<Button>() ?? item.GetComponentInChildren<Button>();
            if (botao == null) continue;

            botao.onClick.RemoveAllListeners();
            botao.onClick.AddListener(() => EscolherRecompensa(capturada));
        }
    }

    void EscolherRecompensa(JourneyReport.Reward escolhida)
    {
        if (escolhida == null) return;

        escolhida.aplicar?.Invoke();

        if (rewardPromptText != null)
            rewardPromptText.text = $"<color=#D9B85A>{escolhida.confirmacao}</color>";

        // As opções somem: a escolha já foi feita e não se desfaz.
        if (rewardContainer != null) UIUtil.ClearChildrenNow(rewardContainer);
        if (continueButton != null) continueButton.interactable = true;
    }

    void PreencherCabecalho(JourneyReport report)
    {
        if (titleText != null)
            titleText.text = report.success
                ? "<color=#D9B85A>🏆 A GUILDA VOLTA VITORIOSA</color>"
                : "<color=#B04040>💀 O QUE SOBROU VOLTOU</color>";

        if (subtitleText == null) return;

        string missao = report.questName;
        string dias = report.daysTraveled == 1 ? "1 dia" : $"{report.daysTraveled} dias";
        string baixas = report.mortos == 0
            ? "sem baixas"
            : report.mortos == 1 ? "1 herói não voltou" : $"{report.mortos} heróis não voltaram";

        subtitleText.text = $"{missao} · {dias} de estrada · {baixas}";
    }

    void PreencherHerois(JourneyReport report)
    {
        if (heroContainer == null) return;

        UIUtil.ClearChildrenNow(heroContainer);
        if (heroLinePrefab == null) return;

        foreach (var linha in report.herois)
        {
            GameObject item = Instantiate(heroLinePrefab, heroContainer);
            PreencherLinha(item, linha);
        }
    }

    void PreencherLinha(GameObject item, JourneyReport.HeroLine linha)
    {
        SetText(item, "Name", linha.morreu
            ? $"<color=#B04040>⚰️ {linha.nome}</color>"
            : linha.subiuDeNivel
                ? $"<color=#60A060>📈 {linha.nome}</color>"
                : linha.nome);

        SetText(item, "Status", DescreverEstado(linha));
        SetText(item, "Xp", linha.morreu ? "" : DescreverXp(linha));

        Image barra = item.transform.Find("XpBar/Fill")?.GetComponent<Image>();
        if (barra != null)
        {
            barra.fillAmount = linha.morreu ? 0f : linha.xpProgresso;
            barra.color = linha.subiuDeNivel
                ? new Color(0.45f, 0.72f, 0.40f)
                : new Color(0.62f, 0.55f, 0.30f);
        }
    }

    static string DescreverEstado(JourneyReport.HeroLine linha)
    {
        if (linha.morreu)
            return "<color=#8A6A6A>morreu na estrada</color>";

        var partes = new List<string> { $"❤️ {linha.hp}/{linha.maxHp}" };

        if (linha.ferido) partes.Add("<color=#B04040>🩸 ferido</color>");

        if (linha.aflicao)
            partes.Add($"<color=#B0A040>🧠 {linha.estadoMental}</color>");
        else if (linha.virtude)
            partes.Add($"<color=#60A060>🌟 {linha.estadoMental}</color>");
        else if (linha.estresse >= 70)
            partes.Add($"<color=#D9B85A>🧠 {linha.estresse}</color>");

        return string.Join("   ", partes);
    }

    static string DescreverXp(JourneyReport.HeroLine linha)
    {
        if (linha.subiuDeNivel)
            return $"<color=#60A060>Nv.{linha.nivelAntes} → Nv.{linha.nivelDepois}</color>   +{linha.xpGanho} XP";

        return $"Nv.{linha.nivelDepois}   +{linha.xpGanho} XP   ({linha.xpAtual}/{linha.xpMeta})";
    }

    void PreencherRecompensa(JourneyReport report)
    {
        if (rewardText == null) return;

        var linhas = new List<string>();

        if (report.recompensaBase > 0)
            linhas.Add($"Contrato: {report.recompensaBase}");
        if (report.recompensaSobreviventes > 0)
            linhas.Add($"Quem voltou ({report.sobreviventes}): +{report.recompensaSobreviventes}");
        if (report.recompensaCombates > 0)
            linhas.Add($"Espólio das lutas: +{report.recompensaCombates}");
        if (report.recompensaBonus > 0)
            linhas.Add($"Biblioteca: +{report.recompensaBonus}");

        string detalhe = linhas.Count > 0 ? string.Join("\n", linhas) + "\n" : "";

        string reputacao = report.reputacao == 0 ? ""
            : report.reputacao > 0
                ? $"\n<color=#60A060>⭐ +{report.reputacao} de reputação</color>"
                : $"\n<color=#B04040>⭐ {report.reputacao} de reputação</color>";

        rewardText.text = $"{detalhe}<size=130%><color=#D9B85A>💰 {report.recompensaTotal} de ouro</color></size>{reputacao}";
    }

    void Fechar()
    {
        if (panel != null) panel.SetActive(false);

        var acao = onContinue;
        onContinue = null;
        acao?.Invoke();
    }

    /// <summary>Escreve num filho pelo nome, em qualquer profundidade.</summary>
    static void SetText(GameObject root, string childName, string value)
    {
        foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
            if (t.gameObject.name == childName) { t.text = value; return; }
    }
}

/// <summary>
/// O que aconteceu na jornada, pronto para ser exibido. Montado pelo
/// JourneyManager no momento em que ele ainda tem todos os números à mão —
/// depois disso a party é dissolvida e os mortos saem do roster.
/// </summary>
public class JourneyReport
{
    public bool success;
    public string questName;
    public int daysTraveled;

    public int sobreviventes;
    public int mortos;

    public int recompensaBase;
    public int recompensaSobreviventes;
    public int recompensaCombates;
    public int recompensaBonus;
    public int recompensaTotal;
    public int reputacao;

    public readonly List<HeroLine> herois = new List<HeroLine>();

    /// <summary>
    /// Escolhas oferecidas ao voltar. Vazio quando a jornada fracassou: quem
    /// volta derrotado não escolhe prêmio.
    /// </summary>
    public readonly List<Reward> recompensas = new List<Reward>();

    /// <summary>Uma opção de recompensa: o que promete e o que faz.</summary>
    public class Reward
    {
        public string titulo;
        public string descricao;
        public System.Action aplicar;

        /// <summary>Mensagem mostrada depois de escolhida.</summary>
        public string confirmacao;
    }

    public class HeroLine
    {
        public string nome;
        public int hp, maxHp;
        public int estresse;
        public bool ferido;
        public bool morreu;

        public int nivelAntes, nivelDepois;
        public int xpGanho, xpAtual, xpMeta;
        public float xpProgresso;

        public string estadoMental;
        public bool aflicao, virtude;

        public bool subiuDeNivel => nivelDepois > nivelAntes;
    }
}
