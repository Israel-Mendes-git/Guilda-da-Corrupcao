using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;

public class GuildManager : MonoBehaviour
{
    public static GuildManager Instance;

    [Header("Recursos")]
    public int gold = 500;
    public int reputation = 100;
    [SerializeField] private TMP_Text goldtxt;
    [SerializeField] private TMP_Text reputationtxt;

    [Header("Heróis")]
    public List<HeroData> roster = new List<HeroData>();
    public int maxRosterSize = 8;

    [Header("Memória da Guilda")]
    public List<HeroData> fallenHeroes = new List<HeroData>();

    [Header("Eventos")]
    public System.Action onGoldChanged;
    public System.Action onRosterChanged;
    public System.Action onReputationChanged;

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
        UpdateGoldUI();
        UpdateReputationUI();

        // Adiciona heróis iniciais se não houver nenhum
        if (roster.Count == 0)
        {
            AddStartingHeroes();
        }
    }

    void AddStartingHeroes()
    {
        roster.Add(HeroFactory.CreateHero("Gromm", HeroClass.Warrior, 3));
        roster.Add(HeroFactory.CreateHero("Lyra", HeroClass.Mage, 2));
        roster.Add(HeroFactory.CreateHero("Finn", HeroClass.Healer, 2));
        roster.Add(HeroFactory.CreateHero("Sera", HeroClass.Hunter, 1));
        onRosterChanged?.Invoke();
    }

    public bool CanRecruit()
    {
        return roster.Count < maxRosterSize;
    }

    public void RecruitHero(HeroData hero)
    {
        if (!CanRecruit())
        {
            Debug.Log("Equipe cheia!");
            return;
        }

        if (gold >= hero.salary)
        {
            gold -= hero.salary;
            roster.Add(hero);

            UpdateGoldUI();
            onGoldChanged?.Invoke();
            onRosterChanged?.Invoke();

            Debug.Log($"{hero.heroName} se juntou à guilda!");
        }
        else
        {
            Debug.Log("Ouro insuficiente!");
        }
    }

    public void RemoveHero(HeroData hero)
    {
        if (roster.Contains(hero))
        {
            roster.Remove(hero);
            onRosterChanged?.Invoke();
        }
    }

    /// <summary>Remove o herói do roster e o registra entre os mortos (base do futuro Cemitério).</summary>
    public void RegisterDeath(HeroData hero)
    {
        if (hero == null) return;

        hero.isDead = true;

        if (!fallenHeroes.Contains(hero))
            fallenHeroes.Add(hero);

        RemoveHero(hero);
        DeckRepository.Remove(hero);

        // Perder heróis mancha o nome da guilda.
        AddReputation(-5);
    }

    public void AddGold(int amount)
    {
        gold += amount;
        UpdateGoldUI();
        onGoldChanged?.Invoke();
    }

    public bool SpendGold(int amount)
    {
        if (gold >= amount)
        {
            gold -= amount;
            UpdateGoldUI();
            onGoldChanged?.Invoke();
            return true;
        }
        return false;
    }

    public void AddReputation(int amount)
    {
        reputation = Mathf.Max(0, reputation + amount);
        UpdateReputationUI();
        onReputationChanged?.Invoke();
    }

    void UpdateGoldUI()
    {
        if (goldtxt != null)
            goldtxt.text = gold.ToString();
    }

    void UpdateReputationUI()
    {
        if (reputationtxt != null)
            reputationtxt.text = reputation.ToString();
    }
}
