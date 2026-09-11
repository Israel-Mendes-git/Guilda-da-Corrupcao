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
    /// Desde 11/09 as vagas são de <see cref="Encomendas"/>: o quadro deixou de
    /// oferecer destino, e o destrave comprado no Santuário passou a comprar
    /// mais pedidos pendurados.
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

    /// <summary>
    /// As missões que existem hoje — e, desde 11/09, <b>só as que não são
    /// destino</b>: a luta de selo de cada área mapeada e a jornada final.
    ///
    /// Não fabrica mais contrato quando a lista está vazia, e vazia é o estado
    /// normal do começo da partida. Para onde ir é pergunta do mapa
    /// (<see cref="RegionMapUI"/>), e a expedição nasce do clique na área; o que
    /// a guilda pendura no quadro são <see cref="Encomendas"/>, que pedem um
    /// resultado e não um lugar.
    /// </summary>
    public List<QuestData> GetQuests()
    {
        if (currentQuests == null) currentQuests = new List<QuestData>();
        return currentQuests;
    }

    /// <summary>
    /// A expedição voltou.
    ///
    /// Não repõe vaga nenhuma: a expedição comum nem estava aqui — ela nasceu do
    /// mapa e morre ao voltar. O que este método ainda faz é tirar da mesa a
    /// luta de selo que acabou de ser disputada e conferir se o fim abriu.
    /// </summary>
    public void CompleteQuest(QuestData quest)
    {
        if (quest == null) return;

        currentQuests.Remove(quest);

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
        if (currentQuests == null) currentQuests = new List<QuestData>();

        // A jornada final e as lutas de selo sobrevivem à renovação: nenhuma das
        // duas é oferta da semana. A área continua mapeada no ciclo seguinte, e
        // o chefe que a guarda continua lá. O resto não existe mais — contrato
        // com destino saiu do jogo em 11/09.
        currentQuests.RemoveAll(q => q == null || (!q.isFinalBoss && !q.isRegionBoss));

        GarantirChefesDeRegiao();
        GarantirChefeSupremo();

        // O quadro de verdade, o que o jogador lê antes de sair: os pedidos.
        var run = RunManager.Instance;
        Encomendas.Renovar(run != null ? run.Cycle : 0, GetPlayerAverageLevel(), TamanhoDoQuadro);

        hasQuests = currentQuests.Count > 0;
        onQuestsChanged?.Invoke();
    }

    /// <summary>
    /// Uma luta de selo por região que já está inteira no mapa e ainda não foi
    /// selada. Elas entram <b>além</b> das quatro ofertas comuns: mapear já
    /// custou duas expedições, e fazer o prêmio disputar vaga com contrato de
    /// escolta seria cobrar duas vezes pela mesma coisa.
    ///
    /// A missão é recriada a cada renovação apenas quando não existe: a
    /// corrupção que ela carrega é a da região no momento em que foi oferecida,
    /// e demorar para aceitar não deve baratear a luta.
    /// </summary>
    public void GarantirChefesDeRegiao()
    {
        var run = RunManager.Instance;
        if (run != null && run.IsOver) return;

        foreach (var regiao in BiomeUtil.Playable)
        {
            if (!RegionMap.EstaMapeada(regiao) || RegionMap.EstaSelada(regiao)) continue;
            if (currentQuests.Exists(q => q != null && q.isRegionBoss && q.biomeType == regiao)) continue;

            QuestData selo = QuestGenerator.GenerateRegionBossQuest(regiao, GetPlayerAverageLevel());
            if (selo != null) currentQuests.Add(selo);
        }

        // Região selada não guarda mais nada: a oferta sai do quadro no mesmo
        // ciclo, e não na renovação seguinte.
        currentQuests.RemoveAll(q => q != null && q.isRegionBoss && RegionMap.EstaSelada(q.biomeType));
    }

    /// <summary>
    /// Põe o Chefe Supremo no quadro quando os três selos estão na mesa, e só
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

    /// <summary>
    /// O quadro precisa ter o que ler antes da primeira saída.
    ///
    /// Ponto único desde 11/09: a taverna e o inicializador chamavam cada um o
    /// seu gerador de contratos, e os dois viravam três ofertas que ninguém
    /// pediu. O que se garante agora são as encomendas — as missões daqui são
    /// consequência do que o jogador fez no mapa, e não existem no começo.
    /// </summary>
    public void GarantirQuadro()
    {
        if (Encomendas.Total > 0) return;

        var run = RunManager.Instance;
        Encomendas.Renovar(run != null ? run.Cycle : 0, GetPlayerAverageLevel(), TamanhoDoQuadro);
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

    /// <summary>
    /// O nível médio de quem está na guilda — a régua de dificuldade de tudo o
    /// que se gera. Público desde que a expedição passou a nascer do clique no
    /// mapa, e não mais só aqui dentro.
    /// </summary>
    public int GetPlayerAverageLevel()
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
