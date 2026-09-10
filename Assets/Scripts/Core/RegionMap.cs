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

    /// <summary>Mapa completo, e o que uma expedição traz dele.</summary>
    ///
    /// <b>Duas expedições bem-sucedidas mapeiam uma região.</b> O número saiu do
    /// tamanho da run: são três selos para abrir o fim, e cada selo custa duas
    /// idas mais a luta contra o chefe — nove ciclos dos quinze, deixando folga
    /// para o jogador errar de lugar. Não é medida de balanceamento; é o passo
    /// que faz mapear caber na partida, e mexer nele muda o ritmo do meio.
    public const float MapeamentoCompleto = 100f;
    public const float MapeamentoPorExpedicao = 50f;

    /// <summary>
    /// O que a expedição fracassada ainda traz. O grupo voltou com menos gente e
    /// menos coisa, mas voltou — e o pedaço de mapa é a única recompensa que
    /// sobrevive a uma jornada perdida.
    /// </summary>
    public const float MapeamentoPorFracasso = 20f;

    /// <summary>Quantos selos abrem a jornada final.</summary>
    public const int SelosParaOFim = 3;

    static Dictionary<BiomeType, float> corrupcao;
    static Dictionary<BiomeType, float> mapeamento;

    /// <summary>
    /// As regiões seladas, <b>na ordem em que caíram</b>.
    ///
    /// Lista e não conjunto porque a ordem é regra: o terceiro selo é o que abre
    /// a passagem, e é a região dele que decide onde o fim acontece. Guardar só
    /// "quais" perderia exatamente a informação que o fim usa.
    /// </summary>
    static List<BiomeType> ordemDosSelos;

    static void GarantirEstado()
    {
        if (corrupcao != null && mapeamento != null && ordemDosSelos != null) return;

        if (corrupcao == null) corrupcao = new Dictionary<BiomeType, float>();
        if (mapeamento == null) mapeamento = new Dictionary<BiomeType, float>();
        if (ordemDosSelos == null) ordemDosSelos = new List<BiomeType>();
        Reiniciar();
    }

    /// <summary>Volta o mundo ao começo de uma run.</summary>
    public static void Reiniciar()
    {
        if (corrupcao == null) corrupcao = new Dictionary<BiomeType, float>();
        if (mapeamento == null) mapeamento = new Dictionary<BiomeType, float>();
        if (ordemDosSelos == null) ordemDosSelos = new List<BiomeType>();

        ordemDosSelos.Clear();

        foreach (var bioma in BiomeUtil.Playable)
        {
            float desvio = desvioInicial.ContainsKey(bioma) ? desvioInicial[bioma] : 0f;
            corrupcao[bioma] = Mathf.Clamp(RunManager.CorruptionStart + desvio, 0f, RunManager.CorruptionMax);
            mapeamento[bioma] = 0f;
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
            // Região selada não apodrece mais: é o que o selo compra, e é a
            // única coisa no jogo que faz o relógio andar mais devagar.
            if (EstaSelada(bioma)) continue;

            float fator = ritmo.ContainsKey(bioma) ? ritmo[bioma] : 1f;
            corrupcao[bioma] = Mathf.Min(RunManager.CorruptionMax, corrupcao[bioma] + avancoGlobal * fator);
        }
    }

    /// <summary>A expedição passou por ali e deixou o lugar pior.</summary>
    public static void Corromper(BiomeType bioma, float quantidade)
    {
        GarantirEstado();

        if (bioma == BiomeType.Any || !corrupcao.ContainsKey(bioma) || quantidade <= 0f) return;
        if (EstaSelada(bioma)) return;

        corrupcao[bioma] = Mathf.Min(RunManager.CorruptionMax, corrupcao[bioma] + quantidade);
    }

    // ---------------------------------------------------------------- mapear

    /// <summary>O quanto da região já está no mapa, de 0 a 100.</summary>
    public static float Mapeamento(BiomeType bioma)
    {
        GarantirEstado();

        if (bioma == BiomeType.Any) return 0f;
        return mapeamento.ContainsKey(bioma) ? mapeamento[bioma] : 0f;
    }

    /// <summary>Fração de 0 a 1 — para a barra da Sala de Mapas.</summary>
    public static float FracaoMapeada(BiomeType bioma)
    {
        return Mathf.Clamp01(Mapeamento(bioma) / MapeamentoCompleto);
    }

    /// <summary>
    /// A região está inteira no mapa. É esta a condição que revela o chefe: o
    /// quadro não oferece a luta de uma região que a guilda ainda não conhece.
    /// </summary>
    public static bool EstaMapeada(BiomeType bioma)
    {
        return Mapeamento(bioma) >= MapeamentoCompleto;
    }

    /// <summary>
    /// A expedição voltou e trouxe pedaço de mapa. Devolve <c>true</c> quando
    /// foi esta ida que completou a região — quem chama usa isso para anunciar,
    /// porque completar o mapa é o instante em que o chefe aparece no quadro.
    /// </summary>
    public static bool Mapear(BiomeType bioma, float quantidade)
    {
        GarantirEstado();

        if (bioma == BiomeType.Any || !mapeamento.ContainsKey(bioma) || quantidade <= 0f) return false;

        bool faltava = !EstaMapeada(bioma);
        mapeamento[bioma] = Mathf.Min(MapeamentoCompleto, mapeamento[bioma] + quantidade);
        return faltava && EstaMapeada(bioma);
    }

    // ----------------------------------------------------------------- selar

    /// <summary>O chefe daquela região caiu e a corrupção dela parou.</summary>
    public static bool EstaSelada(BiomeType bioma)
    {
        GarantirEstado();

        if (bioma == BiomeType.Any) return false;
        return ordemDosSelos.Contains(bioma);
    }

    /// <summary>
    /// Sela a região. Devolve <c>true</c> só na primeira vez: derrubar o mesmo
    /// chefe duas vezes não conta dois selos.
    /// </summary>
    public static bool Selar(BiomeType bioma)
    {
        GarantirEstado();

        if (bioma == BiomeType.Any || ordemDosSelos.Contains(bioma)) return false;
        if (System.Array.IndexOf(BiomeUtil.Playable, bioma) < 0) return false;

        ordemDosSelos.Add(bioma);
        return true;
    }

    /// <summary>Quantas regiões já foram seladas.</summary>
    public static int Selos
    {
        get
        {
            GarantirEstado();
            return ordemDosSelos.Count;
        }
    }

    /// <summary>Quais, na ordem em que caíram — o fim depende de <i>quais</i> três.</summary>
    public static List<BiomeType> RegioesSeladas()
    {
        GarantirEstado();
        return new List<BiomeType>(ordemDosSelos);
    }

    /// <summary>
    /// A região selada por último: é nela que a passagem se abre.
    ///
    /// Devolve <c>Any</c> enquanto não houver selo nenhum — quem chama trata
    /// isso como "o fim ainda não tem lugar".
    /// </summary>
    public static BiomeType UltimoSelo
    {
        get
        {
            GarantirEstado();
            return ordemDosSelos.Count == 0 ? BiomeType.Any : ordemDosSelos[ordemDosSelos.Count - 1];
        }
    }

    /// <summary>Três selos, e a Sala de Mapas pode desenhar a jornada final.</summary>
    public static bool OFimEstaAberto => Selos >= SelosParaOFim;

    /// <summary>As sete, na ordem do enum — é o formato que o save guarda.</summary>
    public static List<float> Serializar()
    {
        GarantirEstado();

        var lista = new List<float>();
        foreach (var bioma in BiomeUtil.Playable) lista.Add(corrupcao[bioma]);
        return lista;
    }

    /// <summary>O quanto de cada região está no mapa, na mesma ordem.</summary>
    public static List<float> SerializarMapeamento()
    {
        GarantirEstado();

        var lista = new List<float>();
        foreach (var bioma in BiomeUtil.Playable) lista.Add(mapeamento[bioma]);
        return lista;
    }

    /// <summary>
    /// Os selos como índices de <see cref="BiomeUtil.Playable"/>, na ordem em
    /// que caíram — e não uma lista de sim/não por região, porque é a ordem que
    /// o fim lê.
    /// </summary>
    public static List<int> SerializarSelos()
    {
        GarantirEstado();

        var lista = new List<int>();
        foreach (var bioma in ordemDosSelos) lista.Add(System.Array.IndexOf(BiomeUtil.Playable, bioma));
        return lista;
    }

    /// <summary>
    /// Devolve o mundo ao ponto em que o save o deixou. Lista vazia ou de
    /// tamanho errado cai no início — save antigo não deve derrubar a partida.
    /// </summary>
    public static void Restaurar(List<float> valores)
    {
        Restaurar(valores, null, null);
    }

    /// <summary>
    /// A versão inteira: corrupção, mapa e selos.
    ///
    /// Cada lista é conferida por conta própria porque elas nasceram em versões
    /// diferentes do save — um arquivo gravado antes de mapear existir tem a
    /// corrupção das sete regiões e nenhuma das outras duas, e precisa abrir
    /// como partida com o mundo intacto e o mapa em branco.
    /// </summary>
    public static void Restaurar(List<float> valores, List<float> mapas, List<int> selos)
    {
        Reiniciar();

        int n = BiomeUtil.Playable.Length;

        if (valores != null && valores.Count == n)
            for (int i = 0; i < n; i++)
                corrupcao[BiomeUtil.Playable[i]] = Mathf.Clamp(valores[i], 0f, RunManager.CorruptionMax);

        if (mapas != null && mapas.Count == n)
            for (int i = 0; i < n; i++)
                mapeamento[BiomeUtil.Playable[i]] = Mathf.Clamp(mapas[i], 0f, MapeamentoCompleto);

        // Os selos vêm por índice e na ordem gravada. Índice fora da faixa ou
        // repetido é descartado em silêncio: um save adulterado não deve dar à
        // partida dois selos da mesma região nem um oitavo bioma.
        if (selos != null)
            foreach (int indice in selos)
            {
                if (indice < 0 || indice >= n) continue;
                Selar(BiomeUtil.Playable[indice]);
            }
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
