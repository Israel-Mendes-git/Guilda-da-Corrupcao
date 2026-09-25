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
    /// A porta abre direto, sempre.
    ///
    /// <b>Até 12/09 a primeira visita a cada sala abria um painel</b> explicando
    /// a sala e perguntando "deseja entrar?" — uma caixa de texto antes de
    /// deixar o jogador ver o lugar, que é exatamente o que o autor descartou
    /// como forma de guiar. A sala explica a si mesma, na própria linha de dica;
    /// e o que ela tem a oferecer agora está na luz da porta (ver
    /// <see cref="GuildGuide"/>). O painel continua na cena, desligado, porque
    /// apagar objeto de cena por ferramenta é irreversível; a descrição fica
    /// aqui só para quem lê o código saber o que cada porta faz.
    /// </summary>
    void ShowLocationInfo(string name, string description, string action)
    {
        currentLocationName = name;
        currentLocationAction = action;

        OnEnterButtonClick();
    }

    void OnEnterButtonClick()
    {
        CloseLocationInfo();

        if (UIManager.Instance == null)
        {
            Debug.LogError("MapManager: UIManager.Instance é nulo — nenhuma tela pode ser aberta.");
            return;
        }

        // A guilda é erguida sala por sala (ver <see cref="Obras"/>): onde ainda
        // não há sala há terreno, e o clique abre a obra em vez da tela. A porta
        // da estrada nunca cai aqui — "Journey" não é sala.
        if (Obras.Da(currentLocationAction, out SalaDaGuilda sala) && !Obras.Construida(sala))
        {
            AbrirTerreno(sala);
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

    /// <summary>
    /// O terreno vazio: o que a sala fará, o que já espera por ela e o preço.
    ///
    /// <b>É a única apresentação que cada sala recebe</b>, e ela chega no instante
    /// em que o jogador está decidindo pagar — não numa caixa de boas-vindas que
    /// ele fecharia antes de ler. É por isso que a guilda pode abrir com duas
    /// portas sem nenhum tutorial: as outras cinco se explicam uma de cada vez,
    /// quando ele for até elas.
    ///
    /// Sem ouro, o botão fica na tela apagado em vez de sumir — o jogador precisa
    /// saber quanto falta para voltar aqui.
    /// </summary>
    void AbrirTerreno(SalaDaGuilda sala)
    {
        int preco = Obras.Preco(sala);
        int ouro = GuildManager.Instance != null ? GuildManager.Instance.gold : 0;
        bool podePagar = Obras.PodePagar(sala);

        string corpo = Obras.Promessa(sala);

        string espera = Obras.Espera(sala);
        if (!string.IsNullOrEmpty(espera))
            corpo += $"\n\n<color=#D9B85A>{espera}</color>";

        if (!podePagar)
            corpo += $"\n\n<color=#B4514E>Faltam {preco - ouro} de ouro.</color>";

        UIManager.Instance.ShowConfirm(
            $"Terreno vazio — {Obras.Nome(sala)}",
            corpo,
            $"Erguer · {preco} ouro",
            "Voltar",
            () => Erguer(sala),
            null,
            podePagar);
    }

    void Erguer(SalaDaGuilda sala)
    {
        if (!Obras.Construir(sala)) return;

        // Entra na sala recém-erguida no mesmo clique: quem acabou de pagar por
        // ela quer ver o que comprou, e voltar ao mapa para clicar na mesma porta
        // de novo seria um passo a mais sem nenhuma decisão dentro.
        OnEnterButtonClick();
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
