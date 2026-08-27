using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// O mapa da guilda: cada local se apresenta na primeira visita e, depois disso,
/// abre direto no clique.
/// </summary>
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

    /// <summary>
    /// Prometia "Melhorar {local} custa 500 ouro" e não fazia nada. Quem melhora
    /// de verdade é cada sala, no próprio botão: <see cref="LibraryManager"/> e
    /// <see cref="MapRoomManager"/> já cobram e sobem de nível lá dentro. O campo
    /// segue aqui só para o painel poder esconder o botão herdado da cena.
    /// </summary>
    public Button upgradeButton;
    public Button enterButton;

    private string currentLocationName;
    private string currentLocationAction;

    void Start()
    {
        ConnectMapButtons();
        InitializeLocationInfoPanel();
    }

    void ConnectMapButtons()
    {
        if (tavernButton != null)
        {
            tavernButton.onClick.RemoveAllListeners();
            tavernButton.onClick.AddListener(() => ShowLocationInfo(
                "🍺 Taverna",
                "Contrate novos aventureiros para sua guilda.\n\n" +
                "Deseja entrar na taverna?",
                "Tavern"));
        }

        if (libraryButton != null)
            libraryButton.onClick.AddListener(() => ShowLocationInfo(
                "📚 Biblioteca",
                "Compre cartas e aprofunde o conhecimento da guilda.\n\n" +
                "• Vende cartas conforme o nível da Biblioteca\n" +
                "• Revela eventos futuros na jornada\n" +
                "• Melhora o retorno em ouro das missões\n\n" +
                "Deseja entrar na biblioteca?",
                "Library"));

        if (mapRoomButton != null)
            mapRoomButton.onClick.AddListener(() => ShowLocationInfo(
                "🗺️ Sala de Mapas",
                "Planeje melhor sua jornada.\n\n" +
                "• Contrata batedores que revelam o percurso\n" +
                "• Compra desvios para recusar um evento\n" +
                "• Sobe de nível para revelar mais\n\n" +
                "Deseja entrar na sala de mapas?",
                "MapRoom"));

        if (forgeButton != null)
            forgeButton.onClick.AddListener(() => ShowLocationInfo(
                "⚔️ Forja",
                "Melhore o equipamento dos seus heróis.\n\n" +
                "• Arma: mais dano nas cartas daquele herói\n" +
                "• Armadura: mais HP máximo\n\n" +
                "Deseja entrar na forja?",
                "Forge"));

        if (cemeteryButton != null)
            cemeteryButton.onClick.AddListener(() => ShowLocationInfo(
                "⚰️ Cemitério",
                "Honre seus heróis caídos.\n\n" +
                "• Monumentos devolvem parte da reputação perdida\n" +
                "• A vigília alivia o estresse de quem ficou\n\n" +
                "Deseja entrar no cemitério?",
                "Cemetery"));

        if (marketButton != null)
            marketButton.onClick.AddListener(() => ShowLocationInfo(
                "🛒 Mercado",
                "Compre recursos para suas missões.\n\n" +
                "• Rações e tochas para a estrada\n" +
                "• Poções, bandagens e vinho para o roster\n\n" +
                "Deseja entrar no mercado?",
                "Market"));

        if (journeyButton != null)
            journeyButton.onClick.AddListener(() => ShowLocationInfo(
                "⚔️ Jornada",
                "Comece sua jornada em busca de recursos e glória!\n\n" +
                "• Selecione uma missão\n" +
                "• Escolha seus heróis e a formação\n" +
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

        // Melhorar é assunto de dentro de cada sala; aqui o botão só mentia.
        if (upgradeButton != null)
            upgradeButton.gameObject.SetActive(false);
    }

    /// <summary>
    /// As salas que o jogador já conhece nesta sessão.
    ///
    /// Toda porta da guilda abria um painel explicando a sala e pedindo
    /// confirmação — sete telas de "deseja entrar?" que o jogador lê uma vez e
    /// depois atravessa no piloto automático, um clique a mais por visita e por
    /// sala. A explicação serve à primeira vez; da segunda em diante ela é
    /// atrito.
    ///
    /// A lista é da sessão e não vai para o save: reabrir o jogo mostrar de novo
    /// o que cada sala faz é aceitável; guardar isso obrigaria a versionar o
    /// arquivo de save por causa de um texto de ajuda.
    /// </summary>
    static readonly HashSet<string> salasJaConhecidas = new HashSet<string>();

    void ShowLocationInfo(string name, string description, string action)
    {
        if (locationInfoPanel == null)
        {
            Debug.LogError("MapManager: locationInfoPanel não está atribuído!");
            return;
        }

        currentLocationName = name;
        currentLocationAction = action;

        // Segunda visita em diante: entra direto.
        if (!salasJaConhecidas.Add(action))
        {
            OnEnterButtonClick();
            return;
        }

        if (locationNameText != null)
            locationNameText.text = name;
        else
            Debug.LogError("MapManager: locationNameText não está atribuído!");

        if (locationDescText != null)
            locationDescText.text = description;
        else
            Debug.LogError("MapManager: locationDescText não está atribuído!");

        locationInfoPanel.SetActive(true);

        // Sem isto, o painel some atrás do mapa depois da primeira visita a uma
        // sala. Ordem de irmãos é ordem de desenho na UI do Unity, e o painel de
        // info é irmão do GuildMap dentro de "Background": toda vez que o jogador
        // volta de uma sala, ShowGuildScreen manda o mapa para o fim da lista e
        // ele passa a cobrir — e a engolir os cliques — este painel, que nasceu
        // num índice anterior. Quem abre vai para a frente, a mesma regra que o
        // UIManager.SetPanelActive já aplica aos painéis que passam por ele.
        locationInfoPanel.transform.SetAsLastSibling();
    }

    void OnEnterButtonClick()
    {
        CloseLocationInfo();

        if (UIManager.Instance == null)
        {
            Debug.LogError("MapManager: UIManager.Instance é nulo — nenhuma tela pode ser aberta.");
            return;
        }

        switch (currentLocationAction)
        {
            case "Tavern":
                UIManager.Instance.ShowTavern();
                break;

            case "Library":
                UIManager.Instance.ShowLibrary();
                break;

            case "MapRoom":
                UIManager.Instance.ShowMapRoom();
                break;

            case "Forge":
                UIManager.Instance.ShowForge();
                break;

            case "Cemetery":
                UIManager.Instance.ShowCemetery();
                break;

            case "Market":
                UIManager.Instance.ShowMarket();
                break;

            case "Journey":
                UIManager.Instance.ShowQuestSelection();

                // A tela de preparação guarda a seleção anterior; sem este refresh
                // ela reabre com missões e roster desatualizados.
                if (QuestSelectionUI.Instance != null)
                {
                    QuestSelectionUI.Instance.gameObject.SetActive(true);
                    QuestSelectionUI.Instance.RefreshAllData();
                }
                break;

            default:
                Debug.LogWarning($"MapManager: ação desconhecida '{currentLocationAction}' para {currentLocationName}.");
                break;
        }
    }

    public void EnableJourneyButton()
    {
        if (journeyButton != null)
            journeyButton.interactable = true;
    }

    public void CloseLocationInfo()
    {
        if (locationInfoPanel != null)
            locationInfoPanel.SetActive(false);
    }
}
