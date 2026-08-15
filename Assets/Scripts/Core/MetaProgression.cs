using UnityEngine;

/// <summary>
/// O que sobrevive à queda de uma guilda.
///
/// Numa run perdida o jogador perde tudo — heróis, ouro, reputação. As relíquias
/// daqui são a única coisa que atravessa: é o que faz a próxima tentativa começar
/// mais forte e a derrota valer alguma coisa.
///
/// Guardado no <see cref="PlayerProfile"/> — um arquivo fora dos slots, porque
/// isto pertence ao jogador e não à partida. Antes vivia em PlayerPrefs, que era
/// o provisório declarado da Fase 3; a migração é automática.
///
/// <b>Mudança de desenho em relação à Fase 3:</b> as relíquias deixaram de virar
/// ouro sozinhas (<c>Relics × 5</c>, com teto). Agora elas são <i>gastas</i> no
/// Santuário, e o ouro extra é um destrave entre outros. As duas coisas não podem
/// coexistir: um bônus automático que também é moeda faz o jogador ser punido por
/// comprar qualquer coisa.
/// </summary>
public static class MetaProgression
{
    /// <summary>Relíquias — a moeda que atravessa as runs.</summary>
    public static int Relics
    {
        get => PlayerProfile.Dados.relics;
        private set
        {
            PlayerProfile.Dados.relics = Mathf.Max(0, value);
            PlayerProfile.Salvar();
        }
    }

    public static int BestCycle => PlayerProfile.Dados.bestCycle;
    public static int TotalRuns => PlayerProfile.Dados.totalRuns;
    public static int TotalWins => PlayerProfile.Dados.totalWins;

    /// <summary>
    /// O que a run rendeu. Paga por sobreviver e por vencer, não por perder
    /// depressa: um ciclo a mais é uma jornada inteira a mais de risco.
    /// </summary>
    public static int PreviewReward(RunManager run)
    {
        if (run == null) return 0;

        int ganho = run.Cycle * 3;
        if (run.State == RunState.Won) ganho += 25;

        return ganho;
    }

    /// <summary>Chamado uma vez, quando a run termina.</summary>
    public static void GrantForRun(RunManager run)
    {
        if (run == null) return;

        var perfil = PlayerProfile.Dados;

        perfil.relics = Mathf.Max(0, perfil.relics + PreviewReward(run));
        perfil.totalRuns++;
        if (run.State == RunState.Won) perfil.totalWins++;
        if (run.Cycle > perfil.bestCycle) perfil.bestCycle = run.Cycle;

        PlayerProfile.Salvar();
    }

    /// <summary>Gasta relíquias. Devolve false quando não há saldo.</summary>
    public static bool Spend(int quanto)
    {
        if (quanto <= 0 || Relics < quanto) return false;

        Relics -= quanto;
        return true;
    }

    #region Santuário das Relíquias

    /// <summary>
    /// Os destraves à venda.
    ///
    /// Cada um cai num ponto que já existia no jogo — ouro inicial, reputação
    /// inicial, tamanho do roster e tamanho do quadro de missões. É deliberado:
    /// um destrave que precisasse de sistema novo seria design a fazer, não
    /// meta-progressão a ligar. O custo sobe por nível para que a primeira compra
    /// seja alcançável na segunda run e a última exija umas cinco.
    /// </summary>
    public static readonly Unlock[] Catalogo =
    {
        new Unlock("cofre", "Cofre da guilda",
                   "Começa com mais ouro. Ouro é reserva de vida: é com ele que se recruta depois de uma jornada ruim.",
                   "+120 de ouro inicial", 3, new[] { 8, 16, 28 }),

        new Unlock("renome", "Renome antigo",
                   "A guilda parte com mais crédito na praça. Reputação em zero encerra a run.",
                   "+25 de reputação inicial", 2, new[] { 10, 20 }),

        new Unlock("alojamentos", "Alojamentos",
                   "Mais camas na guilda — mais gente viva ao mesmo tempo, e mais margem para perder alguém.",
                   "+1 vaga no roster", 2, new[] { 12, 24 }),

        new Unlock("contatos", "Contatos na estrada",
                   "Chega mais oferta ao quadro. Mais missões é mais chance de haver uma que sirva ao grupo que sobrou.",
                   "+1 missão no quadro", 1, new[] { 20 })
    };

    public static Unlock Achar(string id)
    {
        foreach (var u in Catalogo)
            if (u.id == id) return u;
        return null;
    }

    public static int NivelDe(string id) => PlayerProfile.NivelDe(id);

    /// <summary>Compra o próximo nível. Devolve false quando não dá — sem saldo ou no teto.</summary>
    public static bool Comprar(string id)
    {
        Unlock destrave = Achar(id);
        if (destrave == null) return false;

        int nivel = NivelDe(id);
        if (nivel >= destrave.maxLevel) return false;

        int custo = destrave.CustoDoNivel(nivel + 1);
        if (!Spend(custo)) return false;

        PlayerProfile.SubirNivel(id);
        return true;
    }

    #endregion

    #region O que os destraves valem, na hora de fundar uma guilda

    public const int OuroBasePorRun = 500;
    public const int ReputacaoBasePorRun = 100;

    /// <summary>Ouro extra de partida — o destrave "Cofre da guilda".</summary>
    public static int StartingGoldBonus() => NivelDe("cofre") * 120;

    /// <summary>Reputação extra de partida — "Renome antigo".</summary>
    public static int StartingReputationBonus() => NivelDe("renome") * 25;

    /// <summary>Vagas extras no roster — "Alojamentos".</summary>
    public static int ExtraRosterSlots() => NivelDe("alojamentos");

    /// <summary>Missões extras no quadro — "Contatos na estrada".</summary>
    public static int ExtraQuestSlots() => NivelDe("contatos");

    #endregion

    /// <summary>Apaga o progresso entre runs. Só para teste.</summary>
    public static void Reset()
    {
        PlayerProfile.Zerar();
    }
}

/// <summary>Um destrave do Santuário: o que é, o que faz e quanto custa cada nível.</summary>
public class Unlock
{
    public readonly string id;
    public readonly string nome;
    public readonly string descricao;

    /// <summary>O efeito de um nível, em uma linha — é o que o botão mostra.</summary>
    public readonly string efeitoPorNivel;

    public readonly int maxLevel;
    readonly int[] custos;

    public Unlock(string id, string nome, string descricao, string efeitoPorNivel,
                  int maxLevel, int[] custos)
    {
        this.id = id;
        this.nome = nome;
        this.descricao = descricao;
        this.efeitoPorNivel = efeitoPorNivel;
        this.maxLevel = maxLevel;
        this.custos = custos;
    }

    /// <summary>Custo do nível pedido (1 a maxLevel). Zero fora da faixa.</summary>
    public int CustoDoNivel(int nivel)
    {
        if (custos == null || nivel < 1 || nivel > custos.Length) return 0;
        return custos[nivel - 1];
    }

    public bool NoTeto(int nivelAtual) => nivelAtual >= maxLevel;
}
