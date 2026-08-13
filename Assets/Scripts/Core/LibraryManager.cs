using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class LibraryManager : MonoBehaviour
{
    public static LibraryManager Instance;

    [Header("Configuração")]
    public int libraryLevel = 1;
    public List<CardData> availableCards = new List<CardData>();

    [Header("UI References - Conecte no Inspector")]
    public Transform cardsContainer;
    public GameObject cardPurchasePrefab;
    public TMP_Text levelText;
    public TMP_Text bonusText1;
    public TMP_Text bonusText2;
    public TMP_Text eventText1;
    public TMP_Text eventText2;
    public TMP_Text eventText3;
    public Button upgradeButton;
    public Button closeButton;

    [Header("Preços")]
    public int upgradeBaseCost = 500;
    public int commonCardPrice = 100;
    public int rareCardPrice = 250;
    public int epicCardPrice = 500;
    public int legendaryCardPrice = 1000;

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
            closeButton.onClick.AddListener(() => UIManager.Instance?.CloseLibrary());

        if (upgradeButton != null)
            upgradeButton.onClick.AddListener(UpgradeLibrary);
    }

    public void RefreshLibrary()
    {
        Debug.Log("LibraryManager: RefreshLibrary chamado");
        UpdateLibraryUI();
        RefreshAvailableCards();
        UpdateBonusTexts();
        UpdateEventTexts();
    }

    void UpdateLibraryUI()
    {
        if (levelText != null)
            levelText.text = $"Nível {libraryLevel}";

        if (upgradeButton != null)
        {
            int cost = upgradeBaseCost * libraryLevel;
            TMP_Text buttonText = upgradeButton.GetComponentInChildren<TMP_Text>();
            if (buttonText != null)
                buttonText.text = $"MELHORAR - {cost}💰";
        }
    }

    void UpdateBonusTexts()
    {
        if (bonusText1 != null)
            bonusText1.text = $"+{libraryLevel * 5}% chance cartas lendárias";

        if (bonusText2 != null)
            bonusText2.text = $"Revela {libraryLevel} evento(s) futuro(s)";
    }

    void UpdateEventTexts()
    {
        if (eventText1 != null)
            eventText1.text = libraryLevel >= 1 ? "Próximo: 🔮 ???" : "🔒 Nível 1 necessário";
        if (eventText2 != null)
            eventText2.text = libraryLevel >= 2 ? "Depois: 🔮 ???" : "🔒 Nível 2 necessário";
        if (eventText3 != null)
            eventText3.text = libraryLevel >= 3 ? "Em breve: 🔮 ???" : "🔒 Nível 3 necessário";
    }

    void RefreshAvailableCards()
    {
        if (cardsContainer == null)
        {
            Debug.LogError("cardsContainer não está atribuído no LibraryManager!");
            return;
        }

        // Limpa container
        foreach (Transform child in cardsContainer)
            Destroy(child.gameObject);

        // Gera cartas
        GenerateCardsByLevel();

        // Cria os cards na UI
        foreach (var card in availableCards)
        {
            if (cardPurchasePrefab == null)
            {
                Debug.LogError("cardPurchasePrefab não está atribuído!");
                return;
            }

            GameObject cardObj = Instantiate(cardPurchasePrefab, cardsContainer);
            SetupCardPurchaseUI(cardObj, card);
        }

        Debug.Log($"Library: {availableCards.Count} cartas disponíveis");
    }

    void GenerateCardsByLevel()
    {
        availableCards.Clear();

        // Carrega todas as cartas da pasta Resources/Cards
        CardData[] allCards = Resources.LoadAll<CardData>("Cards");

        if (allCards.Length == 0)
        {
            Debug.LogWarning("Nenhuma carta encontrada em Resources/Cards! Execute Tools -> Card Creator primeiro.");
            return;
        }

        // Filtra cartas baseado no nível da biblioteca
        foreach (var card in allCards)
        {
            bool shouldAdd = false;

            switch (card.rarity)
            {
                case CardRarity.Common:
                    shouldAdd = libraryLevel >= 1;
                    break;
                case CardRarity.Rare:
                    shouldAdd = libraryLevel >= 2;
                    break;
                case CardRarity.Epic:
                    shouldAdd = libraryLevel >= 3;
                    break;
                case CardRarity.Legendary:
                    shouldAdd = libraryLevel >= 4;
                    break;
            }

            // 50% de chance de aparecer
            if (shouldAdd && Random.value < 0.5f)
                availableCards.Add(card);
        }

        // Limita a 6 cartas
        while (availableCards.Count > 6)
            availableCards.RemoveAt(Random.Range(0, availableCards.Count));

        // Garante pelo menos 3 cartas
        if (availableCards.Count < 3 && allCards.Length > 0)
        {
            for (int i = availableCards.Count; i < 3; i++)
                availableCards.Add(allCards[Random.Range(0, allCards.Length)]);
        }
    }

    void SetupCardPurchaseUI(GameObject cardObj, CardData card)
    {
        // Nome da carta
        TMP_Text nameText = cardObj.transform.Find("Text_CardName")?.GetComponent<TMP_Text>();
        if (nameText != null) nameText.text = card.cardName;

        // Tipo/Classe
        TMP_Text typeText = cardObj.transform.Find("Text_CardType")?.GetComponent<TMP_Text>();
        if (typeText != null) typeText.text = GetClassIcon(card.requiredClass);

        // Descrição/Efeito
        TMP_Text effectText = cardObj.transform.Find("Text_CardEffect")?.GetComponent<TMP_Text>();
        if (effectText != null) effectText.text = card.cardDescription;

        // Preço
        TMP_Text costText = cardObj.transform.Find("Text_CardCost")?.GetComponent<TMP_Text>();
        int price = GetCardPrice(card.rarity);
        if (costText != null) costText.text = $"💰 {price}";

        // Custo de energia
        TMP_Text energyText = cardObj.transform.Find("Text_CardEnergy")?.GetComponent<TMP_Text>();
        if (energyText != null) energyText.text = $"⚡ {card.energyCost}";

        // Borda por raridade
        Image border = cardObj.transform.Find("Image_Border")?.GetComponent<Image>();
        if (border != null)
        {
            switch (card.rarity)
            {
                case CardRarity.Common:
                    border.color = new Color(0.5f, 0.5f, 0.5f);
                    break;
                case CardRarity.Rare:
                    border.color = new Color(0.2f, 0.4f, 0.8f);
                    break;
                case CardRarity.Epic:
                    border.color = new Color(0.6f, 0.2f, 0.8f);
                    break;
                case CardRarity.Legendary:
                    border.color = new Color(0.9f, 0.7f, 0.1f);
                    break;
            }
        }

        // Botão de compra
        Button buyButton = cardObj.transform.Find("Button_Buy")?.GetComponent<Button>();
        if (buyButton != null)
        {
            int priceCapture = price;
            buyButton.onClick.AddListener(() => PurchaseCard(card, priceCapture));
        }

        // Botão principal (detalhes)
        Button mainButton = cardObj.GetComponent<Button>();
        if (mainButton != null)
            mainButton.onClick.AddListener(() => ShowCardDetails(card));
    }

    void PurchaseCard(CardData card, int price)
    {
        if (GuildManager.Instance.SpendGold(price))
        {
            UIManager.Instance?.ShowMessage($"Carta {card.cardName} comprada!", 2f);
            availableCards.Remove(card);
            RefreshAvailableCards();
        }
        else
        {
            UIManager.Instance?.ShowMessage($"Ouro insuficiente! Precisa de {price} ouro.", 2f);
        }
    }

    void ShowCardDetails(CardData card)
    {
        string details = $"<b>{card.cardName}</b>\n\n";
        details += $"🎭 Classe: {GetClassName(card.requiredClass)}\n";
        details += $"⭐ Raridade: {card.rarity}\n";
        details += $"⚡ Custo: {card.energyCost} energia\n\n";
        details += $"<b>Descrição:</b>\n{card.cardDescription}";

        UIManager.Instance?.ShowResult($"📜 {card.cardName}", details, null);
    }

    void UpgradeLibrary()
    {
        int cost = upgradeBaseCost * libraryLevel;
        if (GuildManager.Instance.SpendGold(cost))
        {
            libraryLevel++;
            RefreshLibrary();
            UIManager.Instance?.ShowMessage($"Biblioteca nível {libraryLevel} desbloqueada!", 2f);
        }
        else
        {
            UIManager.Instance?.ShowMessage($"Ouro insuficiente! Precisa de {cost} ouro.", 2f);
        }
    }

    int GetCardPrice(CardRarity rarity)
    {
        switch (rarity)
        {
            case CardRarity.Common: return commonCardPrice;
            case CardRarity.Rare: return rareCardPrice;
            case CardRarity.Epic: return epicCardPrice;
            case CardRarity.Legendary: return legendaryCardPrice;
            default: return 100;
        }
    }

    string GetClassIcon(HeroClass heroClass)
    {
        switch (heroClass)
        {
            case HeroClass.Warrior: return "⚔️ Guerreiro";
            case HeroClass.Mage: return "🔮 Mago";
            case HeroClass.Healer: return "⚕️ Curandeiro";
            case HeroClass.Rogue: return "🗡️ Ladino";
            case HeroClass.Bard: return "🎵 Bardo";
            case HeroClass.Hunter: return "🏹 Caçador";
            default: return "❓";
        }
    }

    string GetClassName(HeroClass heroClass)
    {
        switch (heroClass)
        {
            case HeroClass.Warrior: return "Guerreiro";
            case HeroClass.Mage: return "Mago";
            case HeroClass.Healer: return "Curandeiro";
            case HeroClass.Rogue: return "Ladino";
            case HeroClass.Bard: return "Bardo";
            case HeroClass.Hunter: return "Caçador";
            default: return "Desconhecido";
        }
    }
}