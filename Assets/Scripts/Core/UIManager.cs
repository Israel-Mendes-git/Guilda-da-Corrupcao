using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("Principais Painéis")]
    public GameObject guildPanel;           // Tela principal da guilda
    public GameObject tavernPanel;          // Popup da taverna
    public GameObject questSelectionPanel;  // Tela de seleção de missão
    public GameObject journeyPanel;         // Tela da jornada (eventos)
    public GameObject heroDetailPanel;      // Barra lateral de detalhes do herói
    public GameObject deckManagerPanel;

    // NOVOS PAINÉIS
    public GameObject libraryPanel;         // Biblioteca
    public GameObject mapRoomPanel;         // Sala de Mapas
    public GameObject marketPanel;          // Mercado
    public GameObject cemeteryPanel;        // Cemitério
    public GameObject forgePanel;           // Forja

    [Header("Esconder durante o combate")]
    [Tooltip("HUD que não pertence ao combate, como o rodapé da guilda.")]
    public GameObject[] hideDuringCombat;

    [Header("Popup de Mensagem")]
    public GameObject messagePopup;
    public TMP_Text messageText;
    public float messageDuration = 2f;

    [Header("Popup de Confirmação")]
    public GameObject confirmPopup;
    public TMP_Text confirmTitleText;
    public TMP_Text confirmMessageText;
    public Button confirmYesButton;
    public Button confirmNoButton;

    [Header("Popup de Resultado")]
    public GameObject resultPopup;
    public TMP_Text resultTitleText;
    public TMP_Text resultMessageText;
    public Button resultCloseButton;

    [Header("Loading Screen")]
    public GameObject loadingScreen;
    public TMP_Text loadingText;
    public Slider loadingBar;

    [Header("Animações")]
    public float fadeDuration = 0.3f;
    public float popupScaleDuration = 0.2f;

    private CanvasGroup currentPopupCG;
    private System.Action onConfirmAction;
    private System.Action onCancelAction;
    private Coroutine currentMessageCoroutine;

    /// <summary>Acima de qualquer painel de tela cheia, que usam a ordem padrão.</summary>
    const int PopupSortingOrder = 500;

    /// <summary>
    /// Garante que o popup seja desenhado — e clicado — acima de tudo.
    ///
    /// Os popups moram dentro de "Background", o **primeiro** filho do Canvas,
    /// enquanto "Panel_Journey" e "Panel_Combat" são irmãos posteriores. Como a
    /// ordem de irmãos é a ordem de desenho, todo popup nascia ATRÁS dessas telas:
    /// elas ocupam o ecrã inteiro e o `raycastTarget` do fundo delas engolia o
    /// clique. Ao vencer a jornada, o popup de resultado aparecia coberto pelo
    /// painel da jornada — invisível e sem como fechar, deixando o jogador preso
    /// na tela sem saída.
    ///
    /// Um Canvas próprio com ordenação alta resolve sem mexer na hierarquia (que
    /// a cena monta à mão). O GraphicRaycaster é obrigatório: sem ele o popup fica
    /// por cima mas continua sem receber cliques.
    /// </summary>
    void EnsurePopupOnTop(GameObject popup)
    {
        if (popup == null) return;

        // O que de fato decide: ser o último irmão do Canvas. A ordenação por
        // sortingOrder abaixo sozinha não bastou com o Canvas em Screen Space
        // Camera, e a cena é montada à mão — um popup pode voltar para dentro de
        // "Background" a qualquer edição.
        popup.transform.SetAsLastSibling();

        var canvas = popup.GetComponent<Canvas>();
        if (canvas == null) canvas = popup.AddComponent<Canvas>();

        canvas.overrideSorting = true;
        canvas.sortingOrder = PopupSortingOrder;

        if (popup.GetComponent<GraphicRaycaster>() == null)
            popup.AddComponent<GraphicRaycaster>();
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        ShowGuildScreen();
        HideAllPopups();
    }

    #region Gerenciamento de Telas Principais

    public void ShowGuildScreen()
    {
        SetPanelActive(guildPanel, true);
        SetPanelActive(tavernPanel, false);
        SetPanelActive(questSelectionPanel, false);
        SetPanelActive(journeyPanel, false);
        SetPanelActive(heroDetailPanel, false);
        SetPanelActive(libraryPanel, false);
        SetPanelActive(mapRoomPanel, false);
        SetPanelActive(marketPanel, false);
        SetPanelActive(cemeteryPanel, false);
        SetPanelActive(forgePanel, false);

        // Voltar à base traz de volta o rodapé escondido pela jornada/combate.
        RestoreGuildHud();
    }

    public void ShowDeckManager()
    {
        SetPanelActive(guildPanel, false);
        SetPanelActive(deckManagerPanel, true);
        SetPanelActive(tavernPanel, false);
        SetPanelActive(questSelectionPanel, false);
        SetPanelActive(journeyPanel, false);
        SetPanelActive(heroDetailPanel, false);
        SetPanelActive(libraryPanel, false);
        SetPanelActive(mapRoomPanel, false);
        SetPanelActive(marketPanel, false);
        SetPanelActive(cemeteryPanel, false);
        SetPanelActive(forgePanel, false);

        // Ativar o painel não preenche nada: a lista de heróis, o deck e a
        // coleção são montados aqui. Sem esta chamada a tela abria vazia, que é
        // como ela vinha se comportando desde sempre.
        if (DeckManager.Instance != null)
            DeckManager.Instance.OpenDeckManager();
    }

    public void CloseDeckManager()
    {
        SetPanelActive(deckManagerPanel, false);
        SetPanelActive(guildPanel, true);
        SetPanelActive(tavernPanel, false);
        SetPanelActive(questSelectionPanel, false);
        SetPanelActive(journeyPanel, false);
        SetPanelActive(heroDetailPanel, false);
        SetPanelActive(libraryPanel, false);
        SetPanelActive(mapRoomPanel, false);
        SetPanelActive(marketPanel, false);
        SetPanelActive(cemeteryPanel, false);
        SetPanelActive(forgePanel, false);
    }

    public void ShowTavern()
    {
        SetPanelActive(guildPanel, false);
        SetPanelActive(tavernPanel, true);
        SetPanelActive(libraryPanel, false);
        SetPanelActive(mapRoomPanel, false);
        SetPanelActive(marketPanel, false);
        SetPanelActive(cemeteryPanel, false);
        SetPanelActive(forgePanel, false);

        if (TavernManager.Instance != null)
            TavernManager.Instance.RefreshRecruits();
    }

    public void CloseTavern()
    {
        SetPanelActive(tavernPanel, false);
        SetPanelActive(guildPanel, true);
    }

    public void ShowQuestSelection()
    {
        SetPanelActive(guildPanel, false);
        SetPanelActive(questSelectionPanel, true);
        SetPanelActive(libraryPanel, false);
        SetPanelActive(mapRoomPanel, false);
        SetPanelActive(marketPanel, false);
        SetPanelActive(cemeteryPanel, false);
        SetPanelActive(forgePanel, false);
    }

    public void CloseQuestSelection()
    {
        SetPanelActive(questSelectionPanel, false);
        SetPanelActive(guildPanel, true);
    }

    public void ShowJourney()
    {
        SetPanelActive(guildPanel, false);
        SetPanelActive(journeyPanel, true);
    }

    #region Exclusividade de tela no combate

    // Painéis escondidos ao entrar em combate, para restaurar depois.
    private readonly List<GameObject> hiddenByCombat = new List<GameObject>();

    /// <summary>
    /// Tira da tela tudo que disputa espaço com o combate.
    ///
    /// Os painéis ocupam a tela inteira e eram desenhados uns sobre os outros:
    /// o texto da jornada aparecia atrás das cartas e os botões de duas telas
    /// conviviam no mesmo rodapé. Os popups ficam de fora — o combate precisa
    /// deles para anunciar o resultado.
    /// </summary>
    /// <summary>Telas que ocupam o ecrã inteiro e portanto não podem coexistir.</summary>
    GameObject[] TelasExclusivas()
    {
        return new[]
        {
            guildPanel, questSelectionPanel, journeyPanel, heroDetailPanel,
            deckManagerPanel, libraryPanel, mapRoomPanel, marketPanel,
            cemeteryPanel, forgePanel, tavernPanel
        };
    }

    public void EnterCombatScreen()
    {
        hiddenByCombat.Clear();

        foreach (var painel in TelasExclusivas())
            Hide(painel);

        // HUD que não pertence ao combate — o rodapé da guilda, por exemplo.
        if (hideDuringCombat != null)
            foreach (var painel in hideDuringCombat)
                Hide(painel);
    }

    /// <summary>
    /// Deixa a jornada sozinha na tela.
    ///
    /// A jornada abria por cima da guilda sem escondê-la, então os prédios
    /// ("Cemitério", "Mercado"…) continuavam visíveis atrás do mapa e das cartas.
    /// </summary>
    public void EnterJourneyScreen()
    {
        foreach (var painel in TelasExclusivas())
        {
            if (painel == null || painel == journeyPanel) continue;
            painel.SetActive(false);
        }

        // O rodapé da guilda também sai: a jornada tem o próprio status da party
        // e os dois juntos disputavam a mesma faixa da tela.
        if (hideDuringCombat != null)
            foreach (var painel in hideDuringCombat)
                if (painel != null) painel.SetActive(false);

        SetPanelActive(journeyPanel, true);
    }

    /// <summary>Devolve o HUD da guilda ao voltar para a base.</summary>
    public void RestoreGuildHud()
    {
        if (hideDuringCombat == null) return;

        foreach (var painel in hideDuringCombat)
            if (painel != null) painel.SetActive(true);
    }

    void Hide(GameObject painel)
    {
        if (painel == null || !painel.activeSelf) return;

        painel.SetActive(false);
        hiddenByCombat.Add(painel);
    }

    /// <summary>Devolve à tela exatamente o que <see cref="EnterCombatScreen"/> escondeu.</summary>
    public void ExitCombatScreen()
    {
        foreach (var painel in hiddenByCombat)
            if (painel != null) painel.SetActive(true);

        hiddenByCombat.Clear();
    }

    #endregion

    public void CloseJourney()
    {
        SetPanelActive(journeyPanel, false);
        SetPanelActive(guildPanel, true);
    }

    public void ShowHeroDetail(HeroData hero)
    {
        if (heroDetailPanel != null)
        {
            SetPanelActive(heroDetailPanel, true);
            if (HeroDetailPanel.Instance != null)
                HeroDetailPanel.Instance.ShowHeroDetails(hero);
        }
    }

    public void CloseHeroDetail()
    {
        SetPanelActive(heroDetailPanel, false);
    }

    // ===== NOVOS MÉTODOS =====

    public void ShowLibrary()
    {
        SetPanelActive(guildPanel, false);
        SetPanelActive(libraryPanel, true);
        SetPanelActive(tavernPanel, false);
        SetPanelActive(mapRoomPanel, false);
        SetPanelActive(marketPanel, false);
        SetPanelActive(cemeteryPanel, false);
        SetPanelActive(forgePanel, false);

        // Atualiza o conteúdo da biblioteca ao abrir
        if (LibraryManager.Instance != null)
            LibraryManager.Instance.RefreshLibrary();
    }

    public void CloseLibrary()
    {
        SetPanelActive(libraryPanel, false);
        SetPanelActive(guildPanel, true);
    }

    public void ShowMapRoom()
    {
        SetPanelActive(guildPanel, false);
        SetPanelActive(mapRoomPanel, true);
        SetPanelActive(libraryPanel, false);
        SetPanelActive(tavernPanel, false);
        SetPanelActive(marketPanel, false);
        SetPanelActive(cemeteryPanel, false);
        SetPanelActive(forgePanel, false);

        if (MapRoomManager.Instance != null)
            MapRoomManager.Instance.RefreshMapRoom();
    }

    public void CloseMapRoom()
    {
        SetPanelActive(mapRoomPanel, false);
        SetPanelActive(guildPanel, true);
    }

    public void ShowMarket()
    {
        SetPanelActive(guildPanel, false);
        SetPanelActive(marketPanel, true);
        SetPanelActive(libraryPanel, false);
        SetPanelActive(mapRoomPanel, false);
        SetPanelActive(tavernPanel, false);
        SetPanelActive(cemeteryPanel, false);
        SetPanelActive(forgePanel, false);

        BringToFront(marketPanel);

        if (MarketManager.Instance != null)
            MarketManager.Instance.RefreshMarket();
    }

    public void CloseMarket()
    {
        SetPanelActive(marketPanel, false);
        SetPanelActive(guildPanel, true);
    }

    public void ShowCemetery()
    {
        SetPanelActive(guildPanel, false);
        SetPanelActive(cemeteryPanel, true);
        SetPanelActive(libraryPanel, false);
        SetPanelActive(mapRoomPanel, false);
        SetPanelActive(marketPanel, false);
        SetPanelActive(tavernPanel, false);
        SetPanelActive(forgePanel, false);

        BringToFront(cemeteryPanel);

        if (CemeteryManager.Instance != null)
            CemeteryManager.Instance.RefreshCemetery();
    }

    public void CloseCemetery()
    {
        SetPanelActive(cemeteryPanel, false);
        SetPanelActive(guildPanel, true);
    }

    public void ShowForge()
    {
        SetPanelActive(guildPanel, false);
        SetPanelActive(forgePanel, true);
        SetPanelActive(libraryPanel, false);
        SetPanelActive(mapRoomPanel, false);
        SetPanelActive(marketPanel, false);
        SetPanelActive(cemeteryPanel, false);
        SetPanelActive(tavernPanel, false);

        BringToFront(forgePanel);

        if (ForgeManager.Instance != null)
            ForgeManager.Instance.RefreshForge();
    }

    public void CloseForge()
    {
        SetPanelActive(forgePanel, false);
        SetPanelActive(guildPanel, true);
    }

    #endregion

    #region Popup de Mensagem

    public void ShowMessage(string message, float duration = -1)
    {
        if (messagePopup == null || messageText == null) return;

        if (currentMessageCoroutine != null)
            StopCoroutine(currentMessageCoroutine);

        messageText.text = message;
        EnsurePopupOnTop(messagePopup);
        StartCoroutine(ShowMessageCoroutine(duration > 0 ? duration : messageDuration));
    }

    IEnumerator ShowMessageCoroutine(float duration)
    {
        messagePopup.SetActive(true);
        CanvasGroup cg = messagePopup.GetComponent<CanvasGroup>();
        if (cg == null) cg = messagePopup.AddComponent<CanvasGroup>();

        cg.alpha = 0;
        float elapsed = 0;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Lerp(0, 1, elapsed / fadeDuration);
            yield return null;
        }
        cg.alpha = 1;

        yield return new WaitForSeconds(duration);

        elapsed = 0;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Lerp(1, 0, elapsed / fadeDuration);
            yield return null;
        }
        messagePopup.SetActive(false);
    }

    #endregion

    #region Popup de Confirmação

    public void ShowConfirm(string title, string message, System.Action onConfirm, System.Action onCancel = null)
    {
        if (confirmPopup == null || confirmTitleText == null || confirmMessageText == null
            || confirmYesButton == null || confirmNoButton == null)
        {
            Debug.LogWarning($"UIManager: popup de confirmação não configurado. Cancelando: {title}");
            onCancel?.Invoke();
            return;
        }

        confirmTitleText.text = title;
        confirmMessageText.text = message;
        onConfirmAction = onConfirm;
        onCancelAction = onCancel;

        confirmYesButton.onClick.RemoveAllListeners();
        confirmYesButton.onClick.AddListener(() => OnConfirmYes());

        confirmNoButton.onClick.RemoveAllListeners();
        confirmNoButton.onClick.AddListener(() => OnConfirmNo());

        EnsurePopupOnTop(confirmPopup);
        PlayPopupAnimation(confirmPopup, true);
    }

    void OnConfirmYes()
    {
        PlayPopupAnimation(confirmPopup, false);
        onConfirmAction?.Invoke();
    }

    void OnConfirmNo()
    {
        PlayPopupAnimation(confirmPopup, false);
        onCancelAction?.Invoke();
    }

    #endregion

    #region Popup de Resultado

    public void ShowResult(string title, string message, System.Action onClose = null)
    {
        // Sem popup configurado, ainda assim é preciso liberar quem chamou:
        // do contrário a jornada terminaria sem nunca fechar a tela.
        if (resultPopup == null || resultTitleText == null || resultMessageText == null || resultCloseButton == null)
        {
            Debug.LogWarning($"UIManager: popup de resultado não configurado. {title} — {message}");
            onClose?.Invoke();
            return;
        }

        resultTitleText.text = title;
        resultMessageText.text = message;

        resultCloseButton.onClick.RemoveAllListeners();
        resultCloseButton.onClick.AddListener(() => {
            PlayPopupAnimation(resultPopup, false);
            onClose?.Invoke();
        });

        EnsurePopupOnTop(resultPopup);
        PlayPopupAnimation(resultPopup, true);
    }

    #endregion

    #region Loading Screen

    public void ShowLoading(string text = "Carregando...")
    {
        if (loadingScreen != null)
        {
            loadingScreen.SetActive(true);
            if (loadingText != null) loadingText.text = text;
            if (loadingBar != null) loadingBar.value = 0;
        }
    }

    public void UpdateLoadingProgress(float progress)
    {
        if (loadingBar != null)
            loadingBar.value = Mathf.Clamp01(progress);
    }

    public void HideLoading()
    {
        if (loadingScreen != null)
            loadingScreen.SetActive(false);
    }

    #endregion

    #region Animações e Helpers

    /// <summary>
    /// Põe o painel na frente de tudo.
    ///
    /// Os painéis de sala ocupam a tela inteira, mas nascem antes do rodapé da
    /// guilda na hierarquia — e o rodapé acabava desenhado por cima deles, com o
    /// botão "Voltar" em cima dos cards da party. O combate já resolvia isso
    /// escondendo o rodapé; para as salas basta trazer o painel à frente.
    /// </summary>
    void BringToFront(GameObject panel)
    {
        if (panel != null && panel.activeSelf)
            panel.transform.SetAsLastSibling();
    }

    // Uma animação por painel, pelo mesmo motivo já resolvido nos popups: fechar
    // uma tela e abrir outra na mesma sequência deixava duas corrotinas em curso.
    // A de fechar terminava depois e desativava o painel recém-aberto — e como
    // "abrir" só agia quando o objeto estava inativo, a reabertura era ignorada e
    // o jogo ficava com a tela em branco, sem guilda e sem sala nenhuma.
    private readonly Dictionary<GameObject, Coroutine> panelAnimations
        = new Dictionary<GameObject, Coroutine>();

    void SetPanelActive(GameObject panel, bool active)
    {
        if (panel == null) return;

        bool animando = panelAnimations.TryGetValue(panel, out Coroutine emCurso);
        if (animando)
        {
            if (emCurso != null) StopCoroutine(emCurso);
            panelAnimations.Remove(panel);
        }

        if (active)
        {
            // Já visível e parado: não há o que fazer, e reanimar piscaria a tela.
            if (panel.activeSelf && !animando) return;

            // Quem abre vai para a frente. Ordem de irmãos é ordem de desenho na
            // UI do Unity, e várias telas moram dentro de "Background" antes do
            // rodapé da guilda: sem isto, o Gerenciador de Deck e a Biblioteca
            // nascem atrás dele, que continua ativo e come os cliques. É a mesma
            // armadilha que prendia o jogador no popup de fim de jornada.
            panel.transform.SetAsLastSibling();

            panel.SetActive(true);
            panelAnimations[panel] = StartCoroutine(AnimatePanelIn(panel));
            return;
        }

        if (!panel.activeSelf)
        {
            // Uma saída interrompida pode ter deixado o painel meio transparente.
            RestaurarVisual(panel);
            return;
        }

        panelAnimations[panel] = StartCoroutine(AnimatePanelOut(panel));
    }

    /// <summary>Devolve alpha e escala ao normal, para o próximo uso do painel.</summary>
    static void RestaurarVisual(GameObject panel)
    {
        var cg = panel.GetComponent<CanvasGroup>();
        if (cg != null) cg.alpha = 1f;
        panel.transform.localScale = Vector3.one;
    }

    IEnumerator AnimatePanelIn(GameObject panel)
    {
        CanvasGroup cg = panel.GetComponent<CanvasGroup>();
        if (cg == null) cg = panel.AddComponent<CanvasGroup>();

        panel.transform.localScale = new Vector3(0.9f, 0.9f, 1);
        cg.alpha = 0;

        float elapsed = 0;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeDuration;
            cg.alpha = Mathf.Lerp(0, 1, t);
            panel.transform.localScale = Vector3.Lerp(new Vector3(0.9f, 0.9f, 1), Vector3.one, t);
            yield return null;
        }

        cg.alpha = 1;
        panel.transform.localScale = Vector3.one;

        panelAnimations.Remove(panel);
    }

    IEnumerator AnimatePanelOut(GameObject panel)
    {
        CanvasGroup cg = panel.GetComponent<CanvasGroup>();
        if (cg == null) cg = panel.AddComponent<CanvasGroup>();

        float elapsed = 0;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeDuration;
            cg.alpha = Mathf.Lerp(1, 0, t);
            panel.transform.localScale = Vector3.Lerp(Vector3.one, new Vector3(0.9f, 0.9f, 1), t);
            yield return null;
        }

        panel.SetActive(false);
        cg.alpha = 1;
        panel.transform.localScale = Vector3.one;

        panelAnimations.Remove(panel);
    }

    // Uma animação por popup. Sem isto, fechar e reabrir o mesmo popup na mesma
    // sequência deixava duas corrotinas concorrentes: a de fechar terminava
    // depois e desativava o popup recém-aberto. Acontecia ao perder um combate
    // — o resultado do combate fecha e o da jornada abre no mesmo instante —,
    // e a tela ficava presa, sem popup e sem forma de encerrar a jornada.
    private readonly Dictionary<GameObject, Coroutine> popupAnimations
        = new Dictionary<GameObject, Coroutine>();

    /// <summary>Anima o popup, cancelando qualquer animação anterior dele.</summary>
    void PlayPopupAnimation(GameObject popup, bool show)
    {
        if (popup == null) return;

        Coroutine anterior;
        if (popupAnimations.TryGetValue(popup, out anterior) && anterior != null)
            StopCoroutine(anterior);

        popupAnimations[popup] = StartCoroutine(ShowPopupAnimation(popup, show));
    }

    IEnumerator ShowPopupAnimation(GameObject popup, bool show)
    {
        if (show)
        {
            popup.SetActive(true);
            popup.transform.localScale = Vector3.zero;

            float elapsed = 0;
            while (elapsed < popupScaleDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Sin(elapsed / popupScaleDuration * Mathf.PI * 0.5f);
                popup.transform.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, t);
                yield return null;
            }
            popup.transform.localScale = Vector3.one;
        }
        else
        {
            float elapsed = 0;
            while (elapsed < popupScaleDuration)
            {
                elapsed += Time.deltaTime;
                float t = 1 - (elapsed / popupScaleDuration);
                popup.transform.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, t);
                yield return null;
            }
            popup.SetActive(false);
            popup.transform.localScale = Vector3.one;
        }
    }

    void HideAllPopups()
    {
        if (messagePopup) messagePopup.SetActive(false);
        if (confirmPopup) confirmPopup.SetActive(false);
        if (resultPopup) resultPopup.SetActive(false);
        if (loadingScreen) loadingScreen.SetActive(false);
    }

    #endregion
}
