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
        // Roster vazio aqui significa guilda nova: ou a sessão começou sem save,
        // ou o jogador escolheu "Nova guilda". Quando há save, o SceneFlow já
        // preencheu o roster no sceneLoaded — antes deste Start, de propósito —
        // e nada aqui deve passar por cima do que foi carregado.
        if (roster.Count == 0)
        {
            AplicarDestraves();
            AddStartingHeroes();
        }

        UpdateGoldUI();
        UpdateReputationUI();
    }

    void AddStartingHeroes()
    {
        roster.Add(HeroFactory.CreateHero("Gromm", HeroClass.Warrior, 3));
        roster.Add(HeroFactory.CreateHero("Lyra", HeroClass.Mage, 2));
        roster.Add(HeroFactory.CreateHero("Finn", HeroClass.Healer, 2));
        roster.Add(HeroFactory.CreateHero("Sera", HeroClass.Hunter, 1));
        onRosterChanged?.Invoke();
    }

    /// <summary>
    /// O que as runs anteriores compraram, aplicado à guilda que está nascendo.
    ///
    /// Ficava só no <see cref="ResetForNewRun"/>, e por isso valia apenas para a
    /// guilda fundada a partir da tela de fim de run — a primeira partida da
    /// sessão, que é o caminho normal saindo do menu, ignorava tudo o que o
    /// jogador tinha destravado.
    /// </summary>
    void AplicarDestraves()
    {
        gold = MetaProgression.OuroBasePorRun + MetaProgression.StartingGoldBonus();
        reputation = MetaProgression.ReputacaoBasePorRun + MetaProgression.StartingReputationBonus();
        maxRosterSize = BaseRosterSize + MetaProgression.ExtraRosterSlots();
    }

    /// <summary>Vagas de fábrica, antes dos alojamentos comprados no Santuário.</summary>
    public const int BaseRosterSize = 8;

    /// <summary>
    /// Uma guilda nova, para a run seguinte.
    ///
    /// Tudo o que a run acumulou se perde — é o preço da derrota. O que atravessa
    /// são as relíquias da <see cref="MetaProgression"/>, e elas entram aqui como
    /// ouro de partida: a próxima tentativa começa mais folgada por causa da
    /// anterior, que é o que faz perder valer alguma coisa.
    /// </summary>
    public void ResetForNewRun()
    {
        roster.Clear();
        fallenHeroes.Clear();

        AplicarDestraves();
        AddStartingHeroes();

        NotificarTudoMudou();
    }

    /// <summary>
    /// Avisa a UI inteira de que tudo mudou de uma vez.
    ///
    /// Existe para o carregamento de save: ouro, reputação e roster trocam no
    /// mesmo instante, e disparar os três eventos separados espalhados pelo
    /// código de carga deixaria a tela meio atualizada se um deles faltasse.
    /// </summary>
    public void NotificarTudoMudou()
    {
        UpdateGoldUI();
        UpdateReputationUI();

        onGoldChanged?.Invoke();
        onReputationChanged?.Invoke();
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
