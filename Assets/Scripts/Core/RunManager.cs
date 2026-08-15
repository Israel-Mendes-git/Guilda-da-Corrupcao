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

            var go = new GameObject("~RunManager");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<RunManager>();
            return instance;
        }
    }

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

    /// <summary>O Chefe Supremo já pode ser convocado?</summary>
    public bool BossAvailable => !IsOver && Corruption >= BossThreshold;

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
        AddCorruption(CorruptionPerCycle);

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
