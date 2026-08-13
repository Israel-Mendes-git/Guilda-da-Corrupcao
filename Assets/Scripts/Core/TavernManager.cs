using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class TavernManager : MonoBehaviour
{
    public static TavernManager Instance;

    [Header("UI Elements")]
    public Transform recruitContainer;
    public GameObject recruitCardPrefab;
    public Button refreshButton;
    public TMP_Text refreshCostText;
    public Button closeButton;

    [Header("Configuração")]
    public int refreshCost = 50;
    public int minLevel = 1;
    public int maxLevel = 3;

    private List<HeroData> currentRecruits = new List<HeroData>();

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        if (refreshButton != null)
        {
            refreshButton.onClick.RemoveListener(PayToRefresh);
            refreshButton.onClick.AddListener(PayToRefresh);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CloseTavern);
            closeButton.onClick.AddListener(CloseTavern);
        }

        UpdateRefreshButtonUI();
    }

    void OnEnable()
    {
        // A primeira leva da visita é de graça; trocar de ideia é que custa.
        if (currentRecruits.Count == 0)
            RefreshRecruits();
        else
            DisplayRecruits();

        UpdateRefreshButtonUI();
        EnsureQuestsExist();
    }

    /// <summary>
    /// Renova a lista por ouro. O botão existe desde sempre no script, mas a
    /// cobrança estava comentada e o botão não existia na cena — dava para
    /// rolar candidatos infinitamente de graça, ou não rolar de jeito nenhum.
    /// </summary>
    public void PayToRefresh()
    {
        if (GuildManager.Instance == null) return;

        if (GuildManager.Instance.gold < refreshCost)
        {
            UIManager.Instance?.ShowMessage($"Ouro insuficiente! A rodada custa {refreshCost}.", 2f);
            return;
        }

        if (!GuildManager.Instance.SpendGold(refreshCost)) return;

        RefreshRecruits();
        UIManager.Instance?.ShowMessage("A taverna se enche de caras novas.", 1.5f);
    }

    /// <summary>
    /// Garante que haja missões no quadro. Antes a taverna regerava as três missões
    /// a cada abertura, o que apagava a missão que o jogador tinha acabado de escolher.
    /// </summary>
    void EnsureQuestsExist()
    {
        if (QuestManager.Instance == null)
        {
            Debug.LogError("QuestManager.Instance é NULL! Certifique-se de que o QuestManager está na cena.");
            return;
        }

        if (QuestManager.Instance.HasQuests())
            return;

        int playerLevel = GetPlayerAverageLevel();
        List<QuestData> quests = QuestGenerator.GenerateQuests(3, playerLevel);
        QuestManager.Instance.SetQuests(quests);

        UIManager.Instance?.ShowMessage("Novas missões disponíveis na guilda!", 3f);
    }

    int GetPlayerAverageLevel()
    {
        if (GuildManager.Instance == null || GuildManager.Instance.roster.Count == 0)
            return 1;

        int totalLevel = 0;
        foreach (var hero in GuildManager.Instance.roster)
        {
            totalLevel += hero.level;
        }
        return totalLevel / GuildManager.Instance.roster.Count;
    }

    /// <summary>Sorteia candidatos novos sem cobrar. Quem cobra é <see cref="PayToRefresh"/>.</summary>
    public void RefreshRecruits()
    {
        GenerateRecruits();
        DisplayRecruits();
        UpdateRefreshButtonUI();
    }

    void GenerateRecruits()
    {
        currentRecruits.Clear();

        for (int i = 0; i < 3; i++)
        {
            HeroData newHero = HeroFactory.CreateRandomHero(minLevel, maxLevel);
            currentRecruits.Add(newHero);
        }
    }

    void DisplayRecruits()
    {
        foreach (Transform child in recruitContainer)
        {
            Destroy(child.gameObject);
        }

        foreach (var hero in currentRecruits)
        {
            GameObject card = Instantiate(recruitCardPrefab, recruitContainer);
            SetupRecruitCard(card, hero);
        }
    }

    void SetupRecruitCard(GameObject card, HeroData hero)
    {
        // Encontra TODOS os TMP_Text do card
        TMP_Text[] allTexts = card.GetComponentsInChildren<TMP_Text>(true);

        foreach (TMP_Text text in allTexts)
        {
            string lowerName = text.gameObject.name.ToLower();
            string currentText = text.text;

            if (string.IsNullOrEmpty(currentText) || lowerName.Contains("name"))
                text.text = hero.heroName;
            else if (lowerName.Contains("class") || lowerName.Contains("classe"))
                text.text = GetClassName(hero.heroClass);
            else if (lowerName.Contains("level") || lowerName.Contains("lvl"))
                text.text = $"Nv.{hero.level}";
            else if (lowerName.Contains("hp") || lowerName.Contains("health"))
                text.text = $"❤️ {hero.currentHp}/{hero.maxHp}";
            else if (lowerName.Contains("salary") || lowerName.Contains("salario"))
                text.text = $"💰 {hero.salary}";
            else if (lowerName.Contains("personality"))
                text.text = GetPersonalityIcon(hero.personality);
            else if (lowerName.Contains("trait"))
                text.text = GetTraitText(hero.trait);
        }

        Button recruitBtn = card.GetComponent<Button>();
        if (recruitBtn != null)
        {
            recruitBtn.onClick.RemoveAllListeners();
            recruitBtn.onClick.AddListener(() => TryRecruitHero(hero, card));
        }

        if (!GuildManager.Instance.CanRecruit())
        {
            if (recruitBtn != null) recruitBtn.interactable = false;
        }
    }

    void TryRecruitHero(HeroData hero, GameObject card)
    {
        if (!GuildManager.Instance.CanRecruit())
        {
            UIManager.Instance?.ShowMessage("Sua equipe está cheia! (Máximo 8 heróis)", 2f);
            return;
        }

        if (GuildManager.Instance.gold < hero.salary)
        {
            UIManager.Instance?.ShowMessage($"Ouro insuficiente! Preciso de {hero.salary} ouro.", 2f);
            return;
        }

        // Recruta o herói
        GuildManager.Instance.RecruitHero(hero);

        // Cria o deck no momento da contratação, no repositório compartilhado.
        DeckData newDeck = DeckGenerator.GenerateDeckForHero(hero);
        DeckRepository.SetDeck(hero, newDeck);

        Debug.Log($"Deck criado para {hero.heroName} com {newDeck.cards.Count} cartas!");

        if (GuildManager.Instance.roster.Contains(hero))
        {
            currentRecruits.Remove(hero);
            Destroy(card);

            UIManager.Instance?.ShowMessage($"{hero.heroName} se juntou à guilda!", 2f);
        }

        // O ouro mudou: o botão de renovar e os cards restantes precisam saber.
        UpdateRefreshButtonUI();
        RefreshCardInteractivity();
    }

    /// <summary>
    /// Reavalia os cards ainda na bancada. Sem isto, depois de gastar quase todo
    /// o ouro os candidatos continuavam parecendo contratáveis.
    /// </summary>
    void RefreshCardInteractivity()
    {
        if (recruitContainer == null) return;

        foreach (Transform child in recruitContainer)
        {
            Button btn = child.GetComponent<Button>();
            if (btn != null)
                btn.interactable = GuildManager.Instance != null && GuildManager.Instance.CanRecruit();
        }
    }

    void CloseTavern()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.CloseTavern();
        else
            gameObject.SetActive(false);

        // REATIVA O BOTÃO DA JORNADA
        MapManager mapManager = FindObjectOfType<MapManager>();
        if (mapManager != null)
        {
            mapManager.EnableJourneyButton();
        }
    }

    void UpdateRefreshButtonUI()
    {
        bool podePagar = GuildManager.Instance != null && GuildManager.Instance.gold >= refreshCost;

        if (refreshCostText != null)
            refreshCostText.text = podePagar
                ? $"Renovar por {refreshCost}💰"
                : $"<color=#B04040>Renovar por {refreshCost}💰</color>";

        if (refreshButton != null)
            refreshButton.interactable = podePagar;
    }

    string GetClassName(HeroClass heroClass)
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

    string GetPersonalityIcon(Personality personality)
    {
        switch (personality)
        {
            case Personality.Brave: return "🦁 Corajoso";
            case Personality.Coward: return "🐔 Covarde";
            case Personality.Ambitious: return "⭐ Ambicioso";
            case Personality.Loyal: return "🤝 Leal";
            case Personality.Stubborn: return "🪨 Teimoso";
            case Personality.Selfish: return "👑 Egoísta";
            default: return "❓";
        }
    }

    string GetTraitText(Trait trait)
    {
        switch (trait)
        {
            case Trait.Drunkard: return "🍺 Bêbado";
            case Trait.Lucky: return "🍀 Sortudo";
            case Trait.Scarred: return "⚡ Cicatrizado";
            case Trait.FastHealer: return "💚 Cura Rápida";
            case Trait.Cursed: return "💀 Amaldiçoado";
            default: return "";
        }
    }
}