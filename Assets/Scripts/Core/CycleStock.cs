using System.Collections.Generic;

/// <summary>
/// O que cada sala tem para oferecer <i>neste</i> ciclo.
///
/// <b>O problema.</b> Das sete salas da guilda, nenhuma dava motivo para voltar:
/// o Mercado mostra os mesmos dez itens desde a primeira visita, e a Forja, os
/// mesmos dois botões. Quem passou uma vez sabe tudo o que há — e pular a sala
/// por três ciclos não custa nada. O autor escolheu a correção em 26/08 entre
/// quatro caminhos: <b>estoque que muda por ciclo</b>. Rejeitados: encomenda com
/// prazo, a sala evoluir por investimento, e evento próprio de sala.
///
/// <b>Sorteado a partir do ciclo, e não guardado.</b> Um estoque sorteado e
/// salvo teria de entrar no save, e o jogador poderia recarregar até sair o que
/// ele quer — o contrário de "perder o ciclo custa". Aqui o sorteio é uma função
/// do número do ciclo: a mesma volta mostra sempre a mesma carroça, em qualquer
/// sessão, e a volta seguinte mostra outra. Nada disso precisa ser serializado.
///
/// <b>Hash próprio, e não <c>string.GetHashCode</c>.</b> O hash de string do
/// .NET é aleatorizado por processo — o mesmo ciclo daria estoques diferentes a
/// cada vez que o jogo abrisse, que é exatamente o que este arquivo existe para
/// impedir. FNV-1a é quatro linhas e é estável.
///
/// <b><see cref="System.Random"/>, e não <c>UnityEngine.Random</c>.</b> O
/// gerador do Unity é global: semeá-lo aqui mudaria o resultado de qualquer
/// sorteio que estivesse em curso — o dano do combate, a rota da jornada, o
/// evento da estrada.
/// </summary>
public static class CycleStock
{
    /// <summary>O ciclo em que a guilda está. Zero enquanto não há run.</summary>
    public static int CicloAtual => RunManager.Instance != null ? RunManager.Instance.Cycle : 0;

    /// <summary>
    /// Escolhe <paramref name="quantos"/> itens da lista — sempre os mesmos para
    /// o mesmo par (sala, ciclo).
    /// </summary>
    /// <param name="sala">Chave livre, uma por prateleira. Salas diferentes com a
    /// mesma chave sorteariam igual.</param>
    public static List<T> Escolher<T>(IReadOnlyList<T> fonte, int quantos, string sala, int ciclo = -1)
    {
        var escolhidos = new List<T>();
        if (fonte == null || fonte.Count == 0 || quantos <= 0) return escolhidos;

        if (quantos >= fonte.Count)
        {
            escolhidos.AddRange(fonte);
            return escolhidos;
        }

        // Embaralha uma cópia dos índices: mexer na lista de origem apagaria a
        // ordem do catálogo, que é a ordem em que o resto do jogo o lê.
        var indices = new List<int>(fonte.Count);
        for (int i = 0; i < fonte.Count; i++) indices.Add(i);

        var rng = new System.Random(Semente(sala, ciclo < 0 ? CicloAtual : ciclo));

        for (int i = indices.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        indices.RemoveRange(quantos, indices.Count - quantos);
        indices.Sort();   // devolve na ordem do catálogo, não na do sorteio

        foreach (int i in indices) escolhidos.Add(fonte[i]);
        return escolhidos;
    }

    /// <summary>Um número estável para aquele par (sala, ciclo).</summary>
    public static int Numero(string sala, int minInclusivo, int maxExclusivo, int ciclo = -1)
    {
        if (maxExclusivo <= minInclusivo) return minInclusivo;

        var rng = new System.Random(Semente(sala, ciclo < 0 ? CicloAtual : ciclo));
        return rng.Next(minInclusivo, maxExclusivo);
    }

    /// <summary>FNV-1a sobre a chave, misturado com o ciclo.</summary>
    static int Semente(string sala, int ciclo)
    {
        unchecked
        {
            uint hash = 2166136261;

            if (sala != null)
            {
                foreach (char c in sala)
                {
                    hash ^= c;
                    hash *= 16777619;
                }
            }

            hash ^= (uint)(ciclo * 2654435761);
            return (int)hash;
        }
    }
}
