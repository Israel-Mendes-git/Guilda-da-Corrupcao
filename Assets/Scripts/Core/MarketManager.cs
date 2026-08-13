using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mercado: converte ouro em suprimento e em cuidado.
///
/// Divide-se em duas coisas que o resto do jogo já entende. Rações e tochas vão
/// para um estoque que a próxima jornada consome — é o mesmo caminho dos batedores
/// da Sala de Mapas. Poção, bandagem e vinho agem na hora, sobre o herói do roster
/// que mais precisa, porque a jornada só devolve 60% do HP e nada apaga um
/// ferimento ou o estresse acumulado.
/// </summary>
public class MarketManager : MonoBehaviour
{
    public static MarketManager Instance;

    [Header("UI References")]
    public TMP_Text goldText;
    public TMP_Text stockText;
    public Transform itemContainer;
    public TMP_Text feedbackText;

    [Header("Buttons")]
    public Button closeButton;

    [Header("Preços")]
    public int rationCost = 8;
    public int torchCost = 12;
    public int potionCost = 70;
    public int bandageCost = 90;
    public int wineCost = 55;

    [Header("Efeitos")]
    public int potionHeal = 15;
    public float wineStressRelief = 18f;

    // Comprado aqui, gasto na próxima jornada.
    private int stockedRations;
    private int stockedTorches;

    public int StockedRations => stockedRations;
    public int StockedTorches => stockedTorches;

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
            closeButton.onClick.AddListener(() => UIManager.Instance?.CloseMarket());
    }

    public void RefreshMarket()
    {
        UpdateHeader();
        BuildItems();
    }

    void UpdateHeader()
    {
        int gold = GuildManager.Instance != null ? GuildManager.Instance.gold : 0;

        if (goldText != null)
            goldText.text = $"💰 {gold}";

        if (stockText != null)
            stockText.text = $"Estoque para a próxima jornada:  🍖 +{stockedRations}   🔥 +{stockedTorches}";
    }

    void SetFeedback(string message)
    {
        if (feedbackText != null)
            feedbackText.text = message;
    }

    #region Prateleira

    class MarketItem
    {
        public string label;
        public string description;
        public int cost;
        public System.Func<bool> canBuy;
        public System.Action onBought;
    }

    List<MarketItem> BuildCatalog()
    {
        var catalog = new List<MarketItem>();

        catalog.Add(new MarketItem
        {
            label = "🍖 Ração",
            description = "Duas refeições a mais na próxima jornada.",
            cost = rationCost,
            canBuy = () => true,
            onBought = () =>
            {
                stockedRations += 2;
                SetFeedback("Rações guardadas para a próxima partida.");
            }
        });

        catalog.Add(new MarketItem
        {
            label = "🔥 Tocha",
            description = "Uma tocha a mais na próxima jornada.",
            cost = torchCost,
            canBuy = () => true,
            onBought = () =>
            {
                stockedTorches += 1;
                SetFeedback("Tocha guardada para a próxima partida.");
            }
        });

        catalog.Add(new MarketItem
        {
            label = "🧪 Poção de cura",
            description = $"Restaura {potionHeal} de vida a quem estiver pior.",
            cost = potionCost,
            canBuy = () => MostWounded() != null,
            onBought = () =>
            {
                HeroData alvo = MostWounded();
                if (alvo == null) return;

                int curado = Mathf.Min(potionHeal, alvo.maxHp - alvo.currentHp);
                alvo.currentHp += curado;
                SetFeedback($"{alvo.heroName} recupera {curado} de vida.");
            }
        });

        catalog.Add(new MarketItem
        {
            label = "🩹 Bandagem",
            description = "Trata o ferimento de um herói.",
            cost = bandageCost,
            canBuy = () => FirstInjured() != null,
            onBought = () =>
            {
                HeroData alvo = FirstInjured();
                if (alvo == null) return;

                alvo.isInjured = false;
                SetFeedback($"{alvo.heroName} não está mais ferido.");
            }
        });

        catalog.Add(new MarketItem
        {
            label = "🍷 Vinho",
            description = $"Alivia {Mathf.RoundToInt(wineStressRelief)} de estresse de quem mais sofre.",
            cost = wineCost,
            canBuy = () => MostStressed() != null,
            onBought = () =>
            {
                HeroData alvo = MostStressed();
                if (alvo == null) return;

                alvo.stress = Mathf.Max(0f, alvo.stress - wineStressRelief);
                SetFeedback($"{alvo.heroName} bebe e respira melhor.");
            }
        });

        return catalog;
    }

    void BuildItems()
    {
        if (itemContainer == null) return;

        UIUtil.ClearChildrenNow(itemContainer);

        foreach (var item in BuildCatalog())
            BuildItemRow(item);
    }

    void BuildItemRow(MarketItem item)
    {
        int gold = GuildManager.Instance != null ? GuildManager.Instance.gold : 0;
        bool disponivel = item.canBuy() && gold >= item.cost;

        var row = new GameObject(item.label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        row.transform.SetParent(itemContainer, false);
        row.GetComponent<Image>().color = new Color(0.16f, 0.15f, 0.17f);

        var element = row.AddComponent<LayoutElement>();
        element.minHeight = 66;
        element.preferredHeight = 66;

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(row.transform, false);

        var label = labelGo.AddComponent<TextMeshProUGUI>();
        label.fontSize = 19;
        label.alignment = TextAlignmentOptions.TopLeft;
        label.raycastTarget = false;
        label.color = disponivel ? new Color(0.92f, 0.90f, 0.85f) : new Color(0.55f, 0.53f, 0.50f);
        label.text = $"{item.label}   <color=#D4AF37>{item.cost}💰</color>\n<size=14>{item.description}</size>";

        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(12, 4);
        labelRect.offsetMax = new Vector2(-180, -4);

        var buttonGo = new GameObject("Btn_Buy", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonGo.transform.SetParent(row.transform, false);
        buttonGo.GetComponent<Image>().color = new Color(0.20f, 0.17f, 0.16f);

        var buttonRect = buttonGo.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1, 0.5f);
        buttonRect.anchorMax = new Vector2(1, 0.5f);
        buttonRect.sizeDelta = new Vector2(150, 44);
        buttonRect.anchoredPosition = new Vector2(-88, 0);

        var buttonTextGo = new GameObject("Text", typeof(RectTransform));
        buttonTextGo.transform.SetParent(buttonGo.transform, false);

        var buttonText = buttonTextGo.AddComponent<TextMeshProUGUI>();
        buttonText.text = "COMPRAR";
        buttonText.fontSize = 16;
        buttonText.alignment = TextAlignmentOptions.Center;
        buttonText.color = new Color(0.94f, 0.88f, 0.72f);
        buttonText.raycastTarget = false;

        var buttonTextRect = buttonTextGo.GetComponent<RectTransform>();
        buttonTextRect.anchorMin = Vector2.zero;
        buttonTextRect.anchorMax = Vector2.one;
        buttonTextRect.offsetMin = Vector2.zero;
        buttonTextRect.offsetMax = Vector2.zero;

        var button = buttonGo.GetComponent<Button>();
        button.interactable = disponivel;

        MarketItem capturado = item;
        button.onClick.AddListener(() => Buy(capturado));
    }

    void Buy(MarketItem item)
    {
        if (!item.canBuy())
        {
            SetFeedback("Ninguém precisa disso agora.");
            return;
        }

        if (GuildManager.Instance == null || !GuildManager.Instance.SpendGold(item.cost))
        {
            SetFeedback("Ouro insuficiente.");
            return;
        }

        item.onBought();
        RefreshMarket();
    }

    #endregion

    #region Alvos

    HeroData MostWounded()
    {
        return LivingRoster()
            .Where(h => h.currentHp < h.maxHp)
            .OrderBy(h => h.maxHp > 0 ? (float)h.currentHp / h.maxHp : 1f)
            .FirstOrDefault();
    }

    HeroData FirstInjured()
    {
        return LivingRoster().FirstOrDefault(h => h.isInjured);
    }

    HeroData MostStressed()
    {
        return LivingRoster()
            .Where(h => h.stress > 0f)
            .OrderByDescending(h => h.stress)
            .FirstOrDefault();
    }

    static IEnumerable<HeroData> LivingRoster()
    {
        if (GuildManager.Instance == null) return Enumerable.Empty<HeroData>();
        return GuildManager.Instance.roster.Where(h => h != null && !h.isDead);
    }

    #endregion

    #region Consumo pela jornada

    /// <summary>Rações compradas aqui, entregues à próxima jornada e zeradas.</summary>
    public int ConsumeRations()
    {
        int total = stockedRations;
        stockedRations = 0;
        return total;
    }

    /// <summary>Tochas compradas aqui, entregues à próxima jornada e zeradas.</summary>
    public int ConsumeTorches()
    {
        int total = stockedTorches;
        stockedTorches = 0;
        return total;
    }

    #endregion
}
