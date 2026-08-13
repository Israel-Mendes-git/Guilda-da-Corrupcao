using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MapManager : MonoBehaviour
{
    [Header("Botões do Mapa")]
    public Button tavernButton;
    public Button libraryButton;
    public Button mapRoomButton;
    public Button forgeButton;
    public Button cemeteryButton;
    public Button marketButton;
    public Button journeyButton;
    public Button deckButton;

    [Header("UI de Informação - Location Info Panel")]
    public GameObject locationInfoPanel;
    public TMP_Text locationNameText;
    public TMP_Text locationDescText;
    public Button upgradeButton;
    public Button enterButton;

    [Header("Debug")]
    public bool showDebugLogs = true;

    private string currentLocationName;
    private string currentLocationAction;

    void Start()
    {
        ConnectMapButtons();
        InitializeLocationInfoPanel();
    }

    void ConnectMapButtons()
    {
        // Taverna - AGORA MOSTRA INFO PRIMEIRO
        if (tavernButton != null)
        {
            tavernButton.onClick.RemoveAllListeners();
            tavernButton.onClick.AddListener(() => ShowLocationInfo(
                "🍺 Taverna",
                "Contrate novos aventureiros para sua guilda.\n\n" +
                "Deseja entrar na taverna?",
                "Tavern"));
            if (showDebugLogs) Debug.Log("Botão da Taverna conectado");
        }

        // Biblioteca
        if (libraryButton != null)
            libraryButton.onClick.AddListener(() => ShowLocationInfo(
                "📚 Biblioteca",
                "Desbloqueia conhecimento sobre biomas.\n\n" +
                "• Revela eventos futuros\n" +
                "• Aumenta chance de encontrar tesouros\n" +
                "• Heróis ganham mais XP\n\n" +
                "Deseja entrar na biblioteca?",
                "Library"));

        // Sala de Mapas
        if (mapRoomButton != null)
            mapRoomButton.onClick.AddListener(() => ShowLocationInfo(
                "🗺️ Sala de Mapas",
                "Planeje melhor sua jornada.\n\n" +
                "• Mostra rotas alternativas\n" +
                "• Reduz chance de se perder\n" +
                "• Revela locais de interesse\n\n" +
                "Deseja entrar na sala de mapas?",
                "MapRoom"));

        // Forja
        if (forgeButton != null)
            forgeButton.onClick.AddListener(() => ShowLocationInfo(
                "⚔️ Forja",
                "Melhore o equipamento dos seus heróis.\n\n" +
                "• Heróis começam com itens melhores\n" +
                "• Aumenta dano base\n" +
                "• Reduz custo de manutenção\n\n" +
                "Deseja entrar na forja?",
                "Forge"));

        // Cemitério
        if (cemeteryButton != null)
            cemeteryButton.onClick.AddListener(() => ShowLocationInfo(
                "⚰️ Cemitério",
                "Honre seus heróis caídos.\n\n" +
                "• Heróis mortos deixam itens\n" +
                "• Reduz chance de morte permanente\n" +
                "• Reza por bênçãos\n\n" +
                "Deseja entrar no cemitério?",
                "Cemetery"));

        // Mercado
        if (marketButton != null)
            marketButton.onClick.AddListener(() => ShowLocationInfo(
                "🛒 Mercado",
                "Compre recursos para suas missões.\n\n" +
                "• Poções de cura\n" +
                "• Rações para viagem\n" +
                "• Mapas e ferramentas\n\n" +
                "Deseja entrar no mercado?",
                "Market"));

        // Jornada
        if (journeyButton != null)
            journeyButton.onClick.AddListener(() => ShowLocationInfo(
                "⚔️ Jornada",
                "Comece sua jornada em busca de recursos e glória!\n\n" +
                "• Selecione uma missão\n" +
                "• Escolha seus heróis\n" +
                "• Enfrente eventos e desafios\n\n" +
                "Deseja iniciar uma jornada?",
                "Journey"));

        if (deckButton != null)
            deckButton.onClick.AddListener(() => {
                CloseLocationInfo();
                UIManager.Instance?.ShowDeckManager();
            });


    }

    void InitializeLocationInfoPanel()
    {
        if (locationInfoPanel != null)
            locationInfoPanel.SetActive(false);

        if (enterButton != null)
            enterButton.onClick.AddListener(OnEnterButtonClick);

        if (upgradeButton != null)
            upgradeButton.onClick.AddListener(OnUpgradeButtonClick);
    }

    void ShowLocationInfo(string name, string description, string action)
    {
        if (showDebugLogs) Debug.Log($"MapManager: Mostrando info - {name}");

        if (locationInfoPanel != null)
        {
            currentLocationName = name;
            currentLocationAction = action;

            if (locationNameText != null)
                locationNameText.text = name;
            else
                Debug.LogError("locationNameText não está atribuído!");

            if (locationDescText != null)
                locationDescText.text = description;
            else
                Debug.LogError("locationDescText não está atribuído!");

            locationInfoPanel.SetActive(true);
        }
        else
        {
            Debug.LogError("locationInfoPanel não está atribuído!");
        }
    }

    void OnEnterButtonClick()
    {
        if (showDebugLogs) Debug.Log($"MapManager: Enter clicado para {currentLocationName} - Ação: {currentLocationAction}");

        CloseLocationInfo();

        switch (currentLocationAction)
        {
            case "Tavern":
                if (UIManager.Instance != null)
                    UIManager.Instance.ShowTavern();
                break;

            case "Library":
                if(UIManager.Instance  != null)
                    UIManager.Instance.ShowLibrary();
                break;

            case "MapRoom":
                if (UIManager.Instance != null)
                    UIManager.Instance.ShowMapRoom();
                break;

            case "Forge":
                if (UIManager.Instance != null)
                    UIManager.Instance.ShowForge();
                break;

            case "Cemetery":
                if (UIManager.Instance != null)
                    UIManager.Instance.ShowCemetery();
                break;

            case "Market":
                if (UIManager.Instance != null)
                    UIManager.Instance.ShowMarket();
                break;

            // No método OnEnterButtonClick, caso "Journey":
            case "Journey":
                Debug.Log("Abrindo seleção de missão...");
                if (UIManager.Instance != null)
                {
                    // Fecha o painel de info
                    CloseLocationInfo();

                    // Abre o painel de seleção de missão
                    UIManager.Instance.ShowQuestSelection();

                    // Força o QuestSelectionUI a atualizar
                    if (QuestSelectionUI.Instance != null)
                    {
                        QuestSelectionUI.Instance.gameObject.SetActive(true);
                        QuestSelectionUI.Instance.RefreshAllData();
                    }
                }
                break;

            default:
                Debug.LogWarning($"Ação desconhecida: {currentLocationAction}");
                break;
        }
    }

    public void EnableJourneyButton()
    {
        if (journeyButton != null)
        {
            journeyButton.interactable = true;
            if (showDebugLogs) Debug.Log("Botão da Jornada ativado");
        }
    }

    void OnUpgradeButtonClick()
    {
        UIManager.Instance?.ShowMessage($"Melhorar {currentLocationName} custa 500 ouro.", 2f);
    }

    public void CloseLocationInfo()
    {
        if (locationInfoPanel != null)
            locationInfoPanel.SetActive(false);
    }
}