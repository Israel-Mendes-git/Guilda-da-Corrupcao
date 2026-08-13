using UnityEngine;

public class GameInitializer : MonoBehaviour
{
    void Awake()
    {
        Debug.Log("=== GAME INITIALIZER ===");
        Invoke("InitializeGame", 0.1f);
    }

    void InitializeGame()
    {
        Debug.Log("Inicializando jogo...");

        if (GuildManager.Instance == null)
        {
            Debug.LogError("GuildManager.Instance é NULL!");
            return;
        }

        // Se não há heróis, adiciona heróis iniciais
        if (GuildManager.Instance.roster.Count == 0)
        {
            Debug.Log("Nenhum herói encontrado! Criando heróis iniciais...");
            CreateInitialHeroes();
        }
        else
        {
            Debug.Log($"Heróis já existem: {GuildManager.Instance.roster.Count}");
        }

        // FORÇA ATUALIZAÇÃO DO QuestSelectionUI SE ELE ESTIVER ATIVO
        if (QuestSelectionUI.Instance != null && QuestSelectionUI.Instance.gameObject.activeSelf)
        {
            Debug.Log("Forçando atualização do QuestSelectionUI...");
            QuestSelectionUI.Instance.RefreshAllData();
        }

        // Gera quests se necessário
        if (QuestManager.Instance != null && !QuestManager.Instance.HasQuests())
        {
            Debug.Log("Gerando quests iniciais...");
            int playerLevel = GetPlayerAverageLevel();
            var quests = QuestGenerator.GenerateQuests(3, playerLevel);
            QuestManager.Instance.SetQuests(quests);
        }
    }

    void CreateInitialHeroes()
    {
        // Cria heróis manualmente
        HeroData gromm = ScriptableObject.CreateInstance<HeroData>();
        gromm.heroName = "Gromm";
        gromm.heroClass = HeroClass.Warrior;
        gromm.level = 3;
        gromm.maxHp = 45;
        gromm.currentHp = 45;
        gromm.salary = 50;
        gromm.loyalty = 75;
        gromm.morale = 80;
        gromm.personality = Personality.Brave;
        gromm.trait = Trait.None;
        GuildManager.Instance.roster.Add(gromm);

        HeroData lyra = ScriptableObject.CreateInstance<HeroData>();
        lyra.heroName = "Lyra";
        lyra.heroClass = HeroClass.Mage;
        lyra.level = 2;
        lyra.maxHp = 28;
        lyra.currentHp = 28;
        lyra.salary = 40;
        lyra.loyalty = 70;
        lyra.morale = 75;
        lyra.personality = Personality.Ambitious;
        lyra.trait = Trait.Lucky;
        GuildManager.Instance.roster.Add(lyra);

        HeroData finn = ScriptableObject.CreateInstance<HeroData>();
        finn.heroName = "Finn";
        finn.heroClass = HeroClass.Healer;
        finn.level = 2;
        finn.maxHp = 30;
        finn.currentHp = 30;
        finn.salary = 40;
        finn.loyalty = 85;
        finn.morale = 90;
        finn.personality = Personality.Loyal;
        finn.trait = Trait.FastHealer;
        GuildManager.Instance.roster.Add(finn);

        HeroData sera = ScriptableObject.CreateInstance<HeroData>();
        sera.heroName = "Sera";
        sera.heroClass = HeroClass.Hunter;
        sera.level = 1;
        sera.maxHp = 24;
        sera.currentHp = 24;
        sera.salary = 30;
        sera.loyalty = 60;
        sera.morale = 65;
        sera.personality = Personality.Coward;
        sera.trait = Trait.None;
        GuildManager.Instance.roster.Add(sera);

        Debug.Log($"Criados {GuildManager.Instance.roster.Count} heróis:");
        foreach (var hero in GuildManager.Instance.roster)
        {
            Debug.Log($"  - {hero.heroName} ({hero.heroClass}) Nv.{hero.level}");
        }

        // Notifica que o roster mudou
        GuildManager.Instance.onRosterChanged?.Invoke();
    }

    int GetPlayerAverageLevel()
    {
        if (GuildManager.Instance == null || GuildManager.Instance.roster.Count == 0)
            return 1;

        int total = 0;
        foreach (var hero in GuildManager.Instance.roster)
            total += hero.level;
        return total / GuildManager.Instance.roster.Count;
    }
}