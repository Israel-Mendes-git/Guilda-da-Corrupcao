using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sala de Mapas: converte ouro em informação e em rotas alternativas.
/// O que se compra aqui é consumido pela próxima jornada — revelações antecipadas
/// do percurso e desvios que permitem recusar um evento no meio do caminho.
/// </summary>
public class MapRoomManager : MonoBehaviour
{
    public static MapRoomManager Instance;

    [Header("UI References")]
    public TMP_Text levelText;
    public TMP_Text scoutingText;
    public TMP_Text detourText;
    public Transform revealedEventsContainer;
    public GameObject revealedEventPrefab;
    public TMP_Text emptyStateText;

    [Header("Buttons")]
    public Button upgradeButton;
    public Button buyScoutingButton;
    public Button buyDetourButton;
    public Button closeButton;

    [Header("Preços")]
    public int upgradeBaseCost = 400;
    public int scoutingCost = 60;
    public int detourCost = 90;

    [Header("Progresso")]
    public int mapRoomLevel = 1;

    // Comprado na guilda, gasto na jornada.
    private int scoutingCharges;
    private int detourCharges;

    public int MaxScouting => 1 + mapRoomLevel;   // eventos revelados de uma vez
    public int MaxDetours => mapRoomLevel;        // desvios por jornada

    public int ScoutingCharges => scoutingCharges;
    public int DetourCharges => detourCharges;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(() => UIManager.Instance?.CloseMapRoom());

        if (upgradeButton != null)
            upgradeButton.onClick.AddListener(UpgradeMapRoom);

        if (buyScoutingButton != null)
            buyScoutingButton.onClick.AddListener(BuyScouting);

        if (buyDetourButton != null)
            buyDetourButton.onClick.AddListener(BuyDetour);
    }

    public void RefreshMapRoom()
    {
        UpdateTexts();
        RefreshRevealedEvents();
    }

    void UpdateTexts()
    {
        if (levelText != null)
            levelText.text = $"Nível {mapRoomLevel}";

        if (scoutingText != null)
            scoutingText.text = $"🔭 Batedores: {scoutingCharges}/{MaxScouting}";

        if (detourText != null)
            detourText.text = $"🧭 Desvios: {detourCharges}/{MaxDetours}";

        if (upgradeButton != null)
        {
            int cost = GetUpgradeCost();
            TMP_Text label = upgradeButton.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = $"MELHORAR - {cost}💰";
            upgradeButton.interactable = GuildManager.Instance != null && GuildManager.Instance.gold >= cost;
        }

        if (buyScoutingButton != null)
        {
            TMP_Text label = buyScoutingButton.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = $"CONTRATAR BATEDOR - {scoutingCost}💰";
            buyScoutingButton.interactable = scoutingCharges < MaxScouting
                && GuildManager.Instance != null && GuildManager.Instance.gold >= scoutingCost;
        }

        if (buyDetourButton != null)
        {
            TMP_Text label = buyDetourButton.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = $"TRAÇAR DESVIO - {detourCost}💰";
            buyDetourButton.interactable = detourCharges < MaxDetours
                && GuildManager.Instance != null && GuildManager.Instance.gold >= detourCost;
        }
    }

    public int GetUpgradeCost() => upgradeBaseCost * mapRoomLevel;

    void UpgradeMapRoom()
    {
        int cost = GetUpgradeCost();

        if (GuildManager.Instance == null || !GuildManager.Instance.SpendGold(cost))
        {
            UIManager.Instance?.ShowMessage("Ouro insuficiente!", 2f);
            return;
        }

        mapRoomLevel++;
        UIManager.Instance?.ShowMessage($"Sala de Mapas agora é nível {mapRoomLevel}!", 2f);
        RefreshMapRoom();
    }

    void BuyScouting()
    {
        if (scoutingCharges >= MaxScouting)
        {
            UIManager.Instance?.ShowMessage("Você já contratou todos os batedores disponíveis.", 2f);
            return;
        }

        if (GuildManager.Instance == null || !GuildManager.Instance.SpendGold(scoutingCost))
        {
            UIManager.Instance?.ShowMessage("Ouro insuficiente!", 2f);
            return;
        }

        scoutingCharges++;
        UIManager.Instance?.ShowMessage("Batedor contratado. Ele seguirá à frente na próxima jornada.", 2f);
        RefreshMapRoom();
    }

    void BuyDetour()
    {
        if (detourCharges >= MaxDetours)
        {
            UIManager.Instance?.ShowMessage("Não há mais rotas alternativas para esta região.", 2f);
            return;
        }

        if (GuildManager.Instance == null || !GuildManager.Instance.SpendGold(detourCost))
        {
            UIManager.Instance?.ShowMessage("Ouro insuficiente!", 2f);
            return;
        }

        detourCharges++;
        UIManager.Instance?.ShowMessage("Rota alternativa traçada.", 2f);
        RefreshMapRoom();
    }

    #region Consumo pela jornada

    /// <summary>Quantos eventos do percurso já nascem revelados.</summary>
    public int ConsumeScoutingForJourney()
    {
        int charges = scoutingCharges;
        scoutingCharges = 0;
        return charges;
    }

    /// <summary>Quantos desvios o grupo leva para esta jornada.</summary>
    public int ConsumeDetoursForJourney()
    {
        int charges = detourCharges;
        detourCharges = 0;
        return charges;
    }

    #endregion

    #region Eventos revelados

    /// <summary>Mostra o que os batedores trouxeram da última jornada iniciada.</summary>
    void RefreshRevealedEvents()
    {
        if (revealedEventsContainer == null) return;

        foreach (Transform child in revealedEventsContainer)
            Destroy(child.gameObject);

        List<string> known = JourneyManager.Instance != null
            ? JourneyManager.Instance.GetRevealedEventTitles()
            : new List<string>();

        if (emptyStateText != null)
            emptyStateText.gameObject.SetActive(known.Count == 0);

        if (known.Count == 0) return;

        foreach (string title in known)
            CriarItemRevelado(title);
    }

    /// <summary>
    /// Uma linha da lista de eventos revelados.
    ///
    /// O prefab é opcional de propósito: sem ele, a sala simplesmente não
    /// mostrava nada — o jogador pagava pelos batedores e voltava para uma lista
    /// vazia, sem erro no console. Um rótulo criado na hora é feio, mas honesto.
    /// </summary>
    void CriarItemRevelado(string title)
    {
        if (revealedEventPrefab != null)
        {
            GameObject item = Instantiate(revealedEventPrefab, revealedEventsContainer);
            TMP_Text label = item.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = title;
            return;
        }

        var go = new GameObject("EventoRevelado", typeof(RectTransform));
        go.transform.SetParent(revealedEventsContainer, false);

        var texto = go.AddComponent<TextMeshProUGUI>();
        texto.text = $"🔮 {title}";
        texto.fontSize = 18;
        texto.color = new Color(0.86f, 0.82f, 0.72f);
        texto.alignment = TextAlignmentOptions.Left;

        // Sem altura mínima o item colapsa dentro de um VerticalLayoutGroup.
        var layout = go.AddComponent<LayoutElement>();
        layout.minHeight = 26;
        layout.preferredHeight = 26;
    }

    #endregion
}
