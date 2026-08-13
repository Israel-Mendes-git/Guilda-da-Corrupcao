using System.Collections.Generic;
using UnityEngine;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance;

    [Header("Configuração")]
    public int questBoardSize = 3;

    private List<QuestData> currentQuests = new List<QuestData>();
    private bool hasQuests = false;

    public System.Action onQuestsChanged;

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

    public void SetQuests(List<QuestData> quests)
    {
        currentQuests = quests;
        hasQuests = true;
        Debug.Log($"QuestManager: {quests.Count} quests armazenadas");
        onQuestsChanged?.Invoke();
    }

    public List<QuestData> GetQuests()
    {
        if (!hasQuests || currentQuests == null || currentQuests.Count == 0)
        {
            Debug.Log("QuestManager: Nenhuma quest armazenada, gerando novas...");
            currentQuests = QuestGenerator.GenerateQuests(questBoardSize, GetPlayerAverageLevel());
            hasQuests = true;
        }

        return currentQuests;
    }

    /// <summary>
    /// Tira a missão do quadro depois da jornada e repõe as vagas,
    /// para que o jogador não repita eternamente a mesma missão.
    /// </summary>
    public void CompleteQuest(QuestData quest)
    {
        if (quest == null) return;

        currentQuests.Remove(quest);

        int missing = questBoardSize - currentQuests.Count;
        if (missing > 0)
            currentQuests.AddRange(QuestGenerator.GenerateQuests(missing, GetPlayerAverageLevel()));

        hasQuests = currentQuests.Count > 0;
        onQuestsChanged?.Invoke();
    }

    public bool HasQuests()
    {
        return hasQuests && currentQuests != null && currentQuests.Count > 0;
    }

    public void ClearQuests()
    {
        currentQuests.Clear();
        hasQuests = false;
        onQuestsChanged?.Invoke();
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
}
