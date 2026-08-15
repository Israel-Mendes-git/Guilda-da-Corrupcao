using UnityEngine;

/// <summary>
/// Prepara o quadro de missões no começo da sessão.
///
/// Já criou também os heróis iniciais, com valores digitados à mão que divergiam
/// dos da <see cref="HeroFactory"/> (Gromm nascia com 45 de HP aqui e 42 lá) e
/// sem <c>heroId</c>, o que impedia o deck do herói de ser salvo. Quem cria o
/// elenco inicial é o <see cref="GuildManager"/>, um só lugar.
/// </summary>
public class GameInitializer : MonoBehaviour
{
    void Awake()
    {
        // Um quadro atrás do GuildManager: o roster inicial precisa existir antes
        // de as missões serem geradas, porque a dificuldade sai do nível médio.
        Invoke(nameof(InitializeGame), 0.1f);
    }

    void InitializeGame()
    {
        if (GuildManager.Instance == null)
        {
            Debug.LogError("GameInitializer: GuildManager.Instance é nulo — a cena não tem o manager da guilda.");
            return;
        }

        if (QuestSelectionUI.Instance != null && QuestSelectionUI.Instance.gameObject.activeSelf)
            QuestSelectionUI.Instance.RefreshAllData();

        if (QuestManager.Instance != null && !QuestManager.Instance.HasQuests())
        {
            var quests = QuestGenerator.GenerateQuests(3, GetPlayerAverageLevel());
            QuestManager.Instance.SetQuests(quests);
        }
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
