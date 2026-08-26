using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// O mundo como lugar: sete regiões, cada uma com corrupção própria e posição
/// fixa no mapa.
///
/// Antes só existia o medidor global do <see cref="RunManager"/>, e a corrupção
/// de cada missão era ele mais um sorteio — o que significa que duas missões no
/// mesmo bioma podiam sair uma limpa e outra podre, e que o jogador não tinha
/// como aprender nada sobre o mundo. Com estado por região, "a Mata está pior
/// que o Passo" vira informação estável, e é isso que o mapa mostra.
///
/// Estático de propósito: o smoke test simula centenas de jornadas em edit mode,
/// onde não há Play Mode nem MonoBehaviour vivo — a mesma razão que fez o
/// RunManager.Instance devolver null fora do jogo.
/// </summary>
public static class RegionMap
{
    /// <summary>Onde cada região fica no mapa, em coordenadas de 0 a 1.</summary>
    ///
    /// Placeholder geométrico: as sete em volta do centro, que é onde fica a
    /// guilda. Quando houver mapa ilustrado, é só trocar estes números pelos
    /// pontos reais do desenho — nada mais depende deles.
    static readonly Dictionary<BiomeType, Vector2> posicoes = new Dictionary<BiomeType, Vector2>
    {
        { BiomeType.Forest,   new Vector2(0.20f, 0.72f) },
        { BiomeType.Mountain, new Vector2(0.50f, 0.86f) },
        { BiomeType.Tundra,   new Vector2(0.80f, 0.74f) },
        { BiomeType.Ruins,    new Vector2(0.14f, 0.40f) },
        { BiomeType.Volcano,  new Vector2(0.86f, 0.38f) },
        { BiomeType.Swamp,    new Vector2(0.30f, 0.14f) },
        { BiomeType.Desert,   new Vector2(0.70f, 0.14f) },
    };

    /// <summary>A guilda no mapa — origem de toda expedição.</summary>
    public static readonly Vector2 PosicaoDaGuilda = new Vector2(0.50f, 0.48f);

    /// <summary>
    /// O quanto cada região destoa do relógio global no começo da run.
    ///
    /// Fixo, não sorteado: é o que faz o mapa ter uma leitura para aprender.
    ///
    /// A média destes valores é <b>+2</b>, e não zero, de propósito. A corrupção
    /// das missões vinha de <c>global + Random.Range(-15, 21)</c>, cujo valor
    /// médio é global+2,5 — e foi com essa régua que a letalidade alvo foi
    /// medida. Centrar as regiões no global puro derrubou a letalidade de 0,49
    /// para 0,33 mortes por jornada na primeira medição, encostando no piso da
    /// faixa punitiva. O viés é herdado da fórmula antiga, não uma escolha de
    /// design; mexer nele é rebalancear, e rebalancear se faz medindo.
    /// </summary>
    static readonly Dictionary<BiomeType, float> desvioInicial = new Dictionary<BiomeType, float>
    {
        { BiomeType.Forest,   -6f },
        { BiomeType.Mountain, -3f },
        { BiomeType.Tundra,    1f },
        { BiomeType.Ruins,     5f },
        { BiomeType.Volcano,  13f },
        { BiomeType.Swamp,     9f },
        { BiomeType.Desert,   -5f },
    };

    /// <summary>
    /// Quão rápido cada região apodrece por ciclo, como fração do avanço global.
    /// A média fica em 1: o mundo inteiro anda no passo do relógio, mas não
    /// uniformemente.
    /// </summary>
    static readonly Dictionary<BiomeType, float> ritmo = new Dictionary<BiomeType, float>
    {
        { BiomeType.Forest,   0.8f },
        { BiomeType.Mountain, 0.9f },
        { BiomeType.Tundra,   1.0f },
        { BiomeType.Ruins,    1.1f },
        { BiomeType.Volcano,  1.3f },
        { BiomeType.Swamp,    1.2f },
        { BiomeType.Desert,   0.7f },
    };

    /// <summary>Corrupção extra na região que a expedição acabou de atravessar.</summary>
    public const float CorrupcaoPorVisita = 4f;

    static Dictionary<BiomeType, float> corrupcao;

    static void GarantirEstado()
    {
        if (corrupcao != null) return;

        corrupcao = new Dictionary<BiomeType, float>();
        Reiniciar();
    }

    /// <summary>Volta o mundo ao começo de uma run.</summary>
    public static void Reiniciar()
    {
        if (corrupcao == null) corrupcao = new Dictionary<BiomeType, float>();

        foreach (var bioma in BiomeUtil.Playable)
        {
            float desvio = desvioInicial.ContainsKey(bioma) ? desvioInicial[bioma] : 0f;
            corrupcao[bioma] = Mathf.Clamp(RunManager.CorruptionStart + desvio, 0f, RunManager.CorruptionMax);
        }
    }

    /// <summary>Quanto a região está tomada, de 0 a 100.</summary>
    public static float Corrupcao(BiomeType bioma)
    {
        GarantirEstado();

        if (bioma == BiomeType.Any) return Media();
        return corrupcao.ContainsKey(bioma) ? corrupcao[bioma] : RunManager.CorruptionStart;
    }

    /// <summary>Fração de 0 a 1 — para pintar o marcador no mapa.</summary>
    public static float Fracao(BiomeType bioma)
    {
        return Mathf.Clamp01(Corrupcao(bioma) / RunManager.CorruptionMax);
    }

    /// <summary>Média das sete, que é o que o medidor global representa.</summary>
    public static float Media()
    {
        GarantirEstado();

        float soma = 0f;
        foreach (var bioma in BiomeUtil.Playable) soma += corrupcao[bioma];
        return soma / BiomeUtil.Playable.Length;
    }

    /// <summary>Onde a região fica no mapa, em coordenadas de 0 a 1.</summary>
    public static Vector2 Posicao(BiomeType bioma)
    {
        return posicoes.ContainsKey(bioma) ? posicoes[bioma] : PosicaoDaGuilda;
    }

    /// <summary>
    /// Um ciclo passou: o mundo apodrece, cada região no seu ritmo.
    /// Chamado pelo <see cref="RunManager.AdvanceCycle"/>, que é o pulso da run.
    /// </summary>
    public static void Avancar(float avancoGlobal)
    {
        GarantirEstado();

        foreach (var bioma in BiomeUtil.Playable)
        {
            float fator = ritmo.ContainsKey(bioma) ? ritmo[bioma] : 1f;
            corrupcao[bioma] = Mathf.Min(RunManager.CorruptionMax, corrupcao[bioma] + avancoGlobal * fator);
        }
    }

    /// <summary>A expedição passou por ali e deixou o lugar pior.</summary>
    public static void Corromper(BiomeType bioma, float quantidade)
    {
        GarantirEstado();

        if (bioma == BiomeType.Any || !corrupcao.ContainsKey(bioma) || quantidade <= 0f) return;
        corrupcao[bioma] = Mathf.Min(RunManager.CorruptionMax, corrupcao[bioma] + quantidade);
    }

    /// <summary>As sete, na ordem do enum — é o formato que o save guarda.</summary>
    public static List<float> Serializar()
    {
        GarantirEstado();

        var lista = new List<float>();
        foreach (var bioma in BiomeUtil.Playable) lista.Add(corrupcao[bioma]);
        return lista;
    }

    /// <summary>
    /// Devolve o mundo ao ponto em que o save o deixou. Lista vazia ou de
    /// tamanho errado cai no início — save antigo não deve derrubar a partida.
    /// </summary>
    public static void Restaurar(List<float> valores)
    {
        Reiniciar();

        if (valores == null || valores.Count != BiomeUtil.Playable.Length) return;

        for (int i = 0; i < BiomeUtil.Playable.Length; i++)
            corrupcao[BiomeUtil.Playable[i]] = Mathf.Clamp(valores[i], 0f, RunManager.CorruptionMax);
    }

    /// <summary>A região mais podre — o mapa marca, e o jogador decide se encara.</summary>
    public static BiomeType MaisCorrompida()
    {
        GarantirEstado();

        BiomeType pior = BiomeUtil.Playable[0];
        foreach (var bioma in BiomeUtil.Playable)
            if (corrupcao[bioma] > corrupcao[pior]) pior = bioma;

        return pior;
    }
}
