using System.Collections.Generic;
using UnityEngine;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance;

    [Header("Configuração")]
    public int questBoardSize = 3;

    /// <summary>
    /// Vagas no quadro, já contando o destrave "Contatos na estrada".
    ///
    /// Todo lugar que repunha missões usava o campo cru, então o destrave
    /// comprado no Santuário não teria efeito nenhum depois do primeiro ciclo.
    /// </summary>
    public int TamanhoDoQuadro => questBoardSize + MetaProgression.ExtraQuestSlots();

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

    /// <summary>
    /// O quadro como está, sem fabricar nada.
    ///
    /// <see cref="GetQuests"/> gera missões quando o quadro está vazio, o que é
    /// certo para quem vai escolher uma — e errado para quem só quer olhar. O
    /// guia da guilda consulta o quadro a cada moeda gasta; se perguntar criasse
    /// missões, o quadro nasceria antes da primeira visita ao mural e o Chefe
    /// Supremo poderia entrar nele fora de hora. Mesmo motivo do
    /// <see cref="RunManager.Existe"/>.
    /// </summary>
    public List<QuestData> QuadroAtual => currentQuests;

    public List<QuestData> GetQuests()
    {
        if (!hasQuests || currentQuests == null || currentQuests.Count == 0)
        {
            Debug.Log("QuestManager: Nenhuma quest armazenada, gerando novas...");
            currentQuests = QuestGenerator.GenerateQuests(TamanhoDoQuadro, GetPlayerAverageLevel());
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

        int missing = TamanhoDoQuadro - currentQuests.Count;
        if (missing > 0)
            currentQuests.AddRange(QuestGenerator.GenerateQuests(missing, GetPlayerAverageLevel()));

        // Quando a Corrupção passa do limiar, o Chefe Supremo entra no quadro e
        // fica. GenerateBossQuest existia desde sempre e nunca era chamada — era
        // a vitória do jogo escrita e inalcançável.
        GarantirChefeSupremo();

        hasQuests = currentQuests.Count > 0;
        onQuestsChanged?.Invoke();
    }

    /// <summary>
    /// Troca as missões do quadro por outras, feitas para a Corrupção de agora.
    ///
    /// Sem isto o relógio da run andava e o quadro ficava parado: as missões
    /// geradas no ciclo 1 continuavam com corrupção de ciclo 1 no ciclo 8, e o
    /// mundo piorar não mudava nada do que o jogador tinha para escolher. O GDD
    /// pede o contrário — "a cada ciclo, novas missões com corrupção e risco
    /// crescentes".
    ///
    /// O Chefe Supremo sobrevive à renovação: ele é o alvo da run, não uma oferta
    /// da semana.
    /// </summary>
    public void RenovarQuadro()
    {
        if (currentQuests == null) return;

        currentQuests.RemoveAll(q => q == null || !q.isFinalBoss);

        int faltando = TamanhoDoQuadro - currentQuests.Count;
        if (faltando > 0)
            currentQuests.AddRange(QuestGenerator.GenerateQuests(faltando, GetPlayerAverageLevel()));

        GarantirChefeSupremo();

        hasQuests = currentQuests.Count > 0;
        onQuestsChanged?.Invoke();
    }

    /// <summary>
    /// Põe o Chefe Supremo no quadro quando o mundo está podre o bastante, e só
    /// uma vez: duas missões finais ao mesmo tempo tirariam o peso da decisão.
    /// </summary>
    public void GarantirChefeSupremo()
    {
        var run = RunManager.Instance;
        if (run == null || !run.BossAvailable) return;

        if (currentQuests.Exists(q => q != null && q.isFinalBoss)) return;

        QuestData chefe = QuestGenerator.GenerateBossQuest(GetPlayerAverageLevel());
        if (chefe == null) return;

        chefe.isFinalBoss = true;
        currentQuests.Add(chefe);
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
