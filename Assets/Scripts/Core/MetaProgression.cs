using UnityEngine;

/// <summary>
/// O que sobrevive à queda de uma guilda.
///
/// Numa run perdida o jogador perde tudo — heróis, ouro, reputação. A moeda daqui
/// é a única coisa que atravessa: é ela que faz a próxima tentativa começar mais
/// forte e a derrota valer alguma coisa.
///
/// Guardado em <see cref="PlayerPrefs"/>, que é o mesmo lugar onde o projeto já
/// guarda os decks (<c>DeckRepository</c>). O pacote ESave está no projeto mas
/// ainda não foi integrado nem confirmado como o save oficial; quando for, esta
/// classe é o único ponto a trocar — o resto do jogo fala só com ela.
/// </summary>
public static class MetaProgression
{
    const string ChaveMoeda = "GoL.Meta.Relics";
    const string ChaveMelhorCiclo = "GoL.Meta.BestCycle";
    const string ChaveRuns = "GoL.Meta.Runs";
    const string ChaveVitorias = "GoL.Meta.Wins";

    /// <summary>Relíquias — a moeda que atravessa as runs.</summary>
    public static int Relics
    {
        get => PlayerPrefs.GetInt(ChaveMoeda, 0);
        private set { PlayerPrefs.SetInt(ChaveMoeda, Mathf.Max(0, value)); PlayerPrefs.Save(); }
    }

    public static int BestCycle => PlayerPrefs.GetInt(ChaveMelhorCiclo, 0);
    public static int TotalRuns => PlayerPrefs.GetInt(ChaveRuns, 0);
    public static int TotalWins => PlayerPrefs.GetInt(ChaveVitorias, 0);

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

        Relics += PreviewReward(run);

        PlayerPrefs.SetInt(ChaveRuns, TotalRuns + 1);
        if (run.State == RunState.Won) PlayerPrefs.SetInt(ChaveVitorias, TotalWins + 1);
        if (run.Cycle > BestCycle) PlayerPrefs.SetInt(ChaveMelhorCiclo, run.Cycle);

        PlayerPrefs.Save();
    }

    /// <summary>Gasta relíquias. Devolve false quando não há saldo.</summary>
    public static bool Spend(int quanto)
    {
        if (quanto <= 0 || Relics < quanto) return false;

        Relics -= quanto;
        return true;
    }

    /// <summary>
    /// Ouro inicial de uma guilda nova, melhorado pelas relíquias acumuladas.
    ///
    /// É o primeiro destravo, e o mais direto de sentir: cada relíquia vira ouro
    /// de partida, até um teto — sem teto, uma run longa tornaria a seguinte
    /// trivial, que é o oposto do que a meta-progressão deve fazer.
    /// </summary>
    public static int StartingGoldBonus()
    {
        return Mathf.Min(Relics * 5, 400);
    }

    /// <summary>Apaga o progresso entre runs. Só para teste.</summary>
    public static void Reset()
    {
        PlayerPrefs.DeleteKey(ChaveMoeda);
        PlayerPrefs.DeleteKey(ChaveMelhorCiclo);
        PlayerPrefs.DeleteKey(ChaveRuns);
        PlayerPrefs.DeleteKey(ChaveVitorias);
        PlayerPrefs.Save();
    }
}
