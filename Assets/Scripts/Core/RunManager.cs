using System;
using UnityEngine;

public enum RunState { Running, Won, Lost }

/// <summary>Por que a run acabou — é o que a tela de fim precisa dizer.</summary>
public enum RunEndReason
{
    None,
    GuildWiped,      // sem heróis e sem ouro para recrutar
    NoReputation,    // ninguém mais contrata a guilda
    WorldConsumed,   // a Corrupção tomou tudo
    BossDefeated     // o Chefe Supremo caiu
}

/// <summary>
/// O relógio da run.
///
/// Até aqui o jogo era um sandbox infinito: não havia começo, fim, nem pressão —
/// `corruptionLevel` era sorteado por missão e nunca avançava no mundo, e
/// `GenerateBossQuest` existia sem nunca ser chamado. Um ciclo é uma jornada
/// concluída; a cada ciclo a Corrupção avança, e é ela que fecha as regiões
/// seguras e acaba convocando o Chefe Supremo.
///
/// Vive fora de cena (DontDestroyOnLoad) e nasce sozinho, como o GameAudio: a
/// cena é montada por código e um objeto a mais no setup é mais uma coisa para
/// sair de sincronia.
/// </summary>
public class RunManager : MonoBehaviour
{
    private static RunManager instance;

    public static RunManager Instance
    {
        get
        {
            if (instance != null) return instance;

            // Fora do Play Mode não há run — e tentar fundar uma estoura:
            // DontDestroyOnLoad é proibido em script de Editor, exatamente como
            // no GameAudio. O smoke test simula 200 jornadas em edit mode e a
            // primeira missão gerada pedia o medidor global de Corrupção; sem
            // esta guarda, a bateria de balanceamento inteira morre antes de
            // escrever relatório, e o Editor devolve um silêncio sem falha.
            //
            // Todo mundo que chama Instance em edit mode já trata o null e cai
            // num padrão: CorruptionStart no QuestGenerator, corrupção do evento
            // ignorada no EventResolver, sem Chefe Supremo no QuestManager.
            if (!Application.isPlaying) return null;

            var go = new GameObject("~RunManager");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<RunManager>();
            return instance;
        }
    }

    /// <summary>
    /// Já existe um relógio? Perguntar por <see cref="Instance"/> o criaria — e
    /// quem só quer saber se há run em andamento (o menu, o save) não deveria
    /// fundar uma ao perguntar.
    /// </summary>
    public static bool Existe => instance != null;

    // --- Régua da run -------------------------------------------------------
    //
    // Com +6 por ciclo a partir de 10, a Corrupção leva ~15 jornadas para tomar
    // o mundo, e o Chefe Supremo aparece por volta da nona. É uma run de tamanho
    // parecido com a de um roguelike de mesa: longa o bastante para o elenco
    // criar história, curta o bastante para a permadeath pesar.

    public const float CorruptionStart = 10f;
    public const float CorruptionPerCycle = 6f;
    public const float CorruptionPerEvent = 3f;
    public const float CorruptionMax = 100f;

    /// <summary>A partir daqui o Chefe Supremo aparece no quadro de missões.</summary>
    public const float BossThreshold = 60f;

    /// <summary>Abaixo disto a guilda não consegue recrutar nem o mais barato.</summary>
    public const int MinGoldToRecruit = 30;

    public int Cycle { get; private set; }
    public float Corruption { get; private set; } = CorruptionStart;
    public RunState State { get; private set; } = RunState.Running;
    public RunEndReason EndReason { get; private set; } = RunEndReason.None;

    /// <summary>Quantos heróis a guilda perdeu nesta run — o balanço final vive disso.</summary>
    public int Fallen { get; private set; }

    public event Action onCycleAdvanced;
    public event Action<RunState, RunEndReason> onRunEnded;

    public bool IsOver => State != RunState.Running;

    /// <summary>Fração de 0 a 1 — para a barra de Corrupção na tela.</summary>
    public float CorruptionRatio => Mathf.Clamp01(Corruption / CorruptionMax);

    /// <summary>
    /// A jornada final já pode ser convocada?
    ///
    /// <b>Mudou em 09/09.</b> Era <c>Corruption >= BossThreshold</c>: bastava o
    /// mundo apodrecer e o Chefe Supremo entrava no quadro sozinho, o que fazia
    /// o fim da run acontecer <i>com</i> o jogador e não <i>por causa</i> dele.
    /// Agora são três regiões seladas — o fim é construído, e quais três muda o
    /// que espera lá.
    ///
    /// O <see cref="BossThreshold"/> continua existindo porque a auditoria mede
    /// em que ciclo o mundo cruza aquele valor; ele deixou de abrir o quadro.
    /// </summary>
    public bool BossAvailable => !IsOver && RegionMap.OFimEstaAberto;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    /// <summary>
    /// Uma jornada terminou. É o pulso da run: o mundo apodrece um pouco e as
    /// condições de fim são conferidas.
    /// </summary>
    public void AdvanceCycle(int fallenThisJourney = 0)
    {
        if (IsOver) return;

        Cycle++;
        Fallen += Mathf.Max(0, fallenThisJourney);

        // O que os escritos traduzidos seguram. É o único efeito que eles têm, e
        // é sempre atraso: o passo do relógio encolhe, nunca inverte, e o teto
        // do <see cref="Escritos.AtrasoMaximo"/> garante que a estante inteira
        // não pare o mundo.
        float passo = CorruptionPerCycle * Escritos.FatorDeAvanco;

        AddCorruption(passo);

        // O mundo apodrece junto com o relógio, cada região no seu ritmo. Sem
        // isto o mapa mostraria sete regiões paradas enquanto o medidor global
        // sobe, que é o oposto do que o mapa existe para contar.
        RegionMap.Avancar(passo);

        onCycleAdvanced?.Invoke();
        CheckEndConditions();
    }

    /// <summary>
    /// Corrupção ganha fora do ciclo — os desfechos marcados com
    /// <c>triggersCorruption</c> nos eventos. É o que faz a escolha do jogador
    /// na estrada custar ao mundo, e não só ao grupo.
    /// </summary>
    public void AddCorruption(float amount)
    {
        if (IsOver || amount <= 0f) return;

        Corruption = Mathf.Min(CorruptionMax, Corruption + amount);
    }

    /// <summary>
    /// As três derrotas do GDD e a vitória.
    ///
    /// "Sem heróis" sozinho não encerra: enquanto houver ouro para recrutar, a
    /// guilda ainda tem uma saída — e é isso que torna o ouro uma reserva de vida,
    /// não só um número.
    /// </summary>
    public void CheckEndConditions()
    {
        if (IsOver) return;

        var guilda = GuildManager.Instance;

        if (Corruption >= CorruptionMax)
        {
            End(RunState.Lost, RunEndReason.WorldConsumed);
            return;
        }

        if (guilda == null) return;

        if (guilda.reputation <= 0)
        {
            End(RunState.Lost, RunEndReason.NoReputation);
            return;
        }

        bool semHerois = guilda.roster == null || guilda.roster.TrueForAll(h => h == null || h.isDead);
        if (semHerois && guilda.gold < MinGoldToRecruit)
            End(RunState.Lost, RunEndReason.GuildWiped);
    }

    /// <summary>O Chefe Supremo caiu: é a única vitória da run.</summary>
    public void ReportBossDefeated()
    {
        if (IsOver) return;
        End(RunState.Won, RunEndReason.BossDefeated);
    }

    void End(RunState estado, RunEndReason motivo)
    {
        State = estado;
        EndReason = motivo;

        MetaProgression.GrantForRun(this);

        onRunEnded?.Invoke(estado, motivo);
    }

    /// <summary>Recomeça — o mundo volta ao início, a meta-progressão fica.</summary>
    public void StartNewRun()
    {
        Cycle = 0;
        Corruption = CorruptionStart;
        Fallen = 0;
        State = RunState.Running;
        EndReason = RunEndReason.None;

        RegionMap.Reiniciar();
        Encomendas.Reiniciar();
        Escritos.Reiniciar();
    }

    /// <summary>
    /// Devolve o relógio ao ponto em que o save o deixou.
    ///
    /// Separado do <see cref="StartNewRun"/> de propósito: um restaura, o outro
    /// zera, e confundir os dois é como um save carrega a partida certa com o
    /// mundo no ciclo 1. Não dispara <c>onCycleAdvanced</c> — carregar não é
    /// avançar, e o quadro de missões vem do próprio save.
    /// </summary>
    public void Restaurar(int ciclo, float corrupcao, RunState estado, RunEndReason motivo, int caidos)
    {
        Cycle = Mathf.Max(0, ciclo);
        Corruption = Mathf.Clamp(corrupcao, 0f, CorruptionMax);
        State = estado;
        EndReason = motivo;
        Fallen = Mathf.Max(0, caidos);
    }

    /// <summary>Texto do motivo, na língua do jogador.</summary>
    public static string DescreverMotivo(RunEndReason motivo)
    {
        switch (motivo)
        {
            case RunEndReason.GuildWiped:
                return "Não sobrou ninguém para enviar, nem ouro para contratar quem fosse.";
            case RunEndReason.NoReputation:
                return "Ninguém mais confia trabalho à guilda. As portas se fecharam.";
            case RunEndReason.WorldConsumed:
                return "A Corrupção tomou as sete regiões. Não há mais estrada para percorrer.";
            case RunEndReason.BossDefeated:
                return "A fonte da Corrupção foi destruída. O mundo respira de novo.";
            default:
                return "";
        }
    }
}
