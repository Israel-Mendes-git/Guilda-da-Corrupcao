using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Os escritos: páginas de quem tentou conter a Corrupção antes desta guilda.
///
/// <b>De onde vêm.</b> Um por região, entregue quando a região fica <b>inteira no
/// mapa</b>. O GDD diz que os batedores as trazem junto com o mapa, e é
/// literalmente isso: mapear é o que desenterra a página. Sete regiões, sete
/// escritos, e a ordem em que aparecem é a ordem em que o jogador escolheu
/// percorrer o mundo.
///
/// <b>O que fazem.</b> Cada escrito traduzido <b>atrasa</b> a corrupção — e só.
/// Nenhum reverte, nem para de somar: contenção é sempre adiamento, e é por isso
/// que aquelas civilizações caíram mesmo tendo escrito tudo. O atraso tem teto
/// (<see cref="AtrasoMaximo"/>) para que nem a estante inteira congele o relógio;
/// um mundo que para de apodrecer tira da partida o que a faz terminar.
///
/// <b>Quem traduz é a Biblioteca, uma por ciclo.</b> A decisão é do autor e foi
/// desfeita uma vez no mesmo dia — mover a tradução para a Sala de Mapas foi
/// proposto e recusado. Uma por ciclo é o que faz a estante virar fila: com
/// quatro páginas na mesa, qual delas se lê primeiro é escolha.
///
/// <b>O texto de cada escrito não está aqui.</b> World building é decisão do
/// autor, e inventar o que essas civilizações disseram seria escrever o jogo no
/// lugar dele. O que existe é o lugar onde o texto entra — a estrutura sabe que
/// há um título e um corpo por região, e ambos aparecem na tela assim que forem
/// escritos.
///
/// Estático pelo mesmo motivo do <see cref="RegionMap"/>: o smoke test simula
/// centenas de jornadas em edit mode, onde não há MonoBehaviour vivo.
/// </summary>
public static class Escritos
{
    /// <summary>
    /// Quanto cada escrito traduzido tira do avanço por ciclo.
    ///
    /// 8% por página, somando até o teto: com quatro traduzidas o mundo apodrece
    /// a 68% da velocidade, o que numa run de quinze ciclos devolve cerca de
    /// dois ciclos de folga. É atraso que dá para sentir sem tirar o relógio da
    /// partida — e é o número a mexer se a run passar a acabar cedo ou nunca.
    /// </summary>
    public const float AtrasoPorEscrito = 0.08f;

    /// <summary>Teto do atraso somado. Contenção não vira parada.</summary>
    public const float AtrasoMaximo = 0.40f;

    /// <summary>Páginas encontradas e ainda por ler, na ordem em que chegaram.</summary>
    static List<BiomeType> naEstante;

    /// <summary>Páginas traduzidas, na ordem em que foram lidas.</summary>
    static List<BiomeType> lidos;

    /// <summary>
    /// O ciclo da última tradução. Começa em -1 para que a primeira página possa
    /// ser lida no ciclo 0 — sem isso a guilda esperaria um ciclo inteiro para
    /// ler o que acabou de chegar.
    /// </summary>
    static int cicloDaUltimaTraducao = -1;

    static void GarantirEstado()
    {
        if (naEstante != null && lidos != null) return;

        if (naEstante == null) naEstante = new List<BiomeType>();
        if (lidos == null) lidos = new List<BiomeType>();
    }

    /// <summary>Volta a estante ao começo de uma run.</summary>
    public static void Reiniciar()
    {
        GarantirEstado();

        naEstante.Clear();
        lidos.Clear();
        cicloDaUltimaTraducao = -1;
    }

    // -------------------------------------------------------------- a estante

    /// <summary>
    /// A expedição desenterrou a página daquela região. Devolve <c>true</c> só na
    /// primeira vez — a região só tem uma, e mapeá-la de novo não a duplica.
    /// </summary>
    public static bool Encontrar(BiomeType regiao)
    {
        GarantirEstado();

        if (regiao == BiomeType.Any) return false;
        if (naEstante.Contains(regiao) || lidos.Contains(regiao)) return false;

        naEstante.Add(regiao);
        return true;
    }

    /// <summary>Páginas esperando tradução, na ordem em que chegaram.</summary>
    public static List<BiomeType> NaEstante()
    {
        GarantirEstado();
        return new List<BiomeType>(naEstante);
    }

    /// <summary>Páginas já lidas, na ordem — é nessa ordem que elas contam a história.</summary>
    public static List<BiomeType> Lidos()
    {
        GarantirEstado();
        return new List<BiomeType>(lidos);
    }

    public static int TotalNaEstante
    {
        get { GarantirEstado(); return naEstante.Count; }
    }

    public static int TotalLidos
    {
        get { GarantirEstado(); return lidos.Count; }
    }

    // ------------------------------------------------------------- a tradução

    /// <summary>
    /// A Biblioteca já traduziu neste ciclo?
    ///
    /// Lido do <see cref="RunManager"/> quando ele existe. Fora do Play Mode —
    /// no simulador — não há relógio, e a regra do ciclo não se aplica: quem
    /// simula não abre a Biblioteca.
    /// </summary>
    public static bool PodeTraduzirNesteCiclo
    {
        get
        {
            GarantirEstado();

            if (naEstante.Count == 0) return false;

            var run = RunManager.Instance;
            if (run == null) return true;

            return run.Cycle > cicloDaUltimaTraducao;
        }
    }

    /// <summary>
    /// Lê uma página. Devolve <c>false</c> quando ela não está na estante ou
    /// quando a Biblioteca já leu a deste ciclo.
    /// </summary>
    public static bool Traduzir(BiomeType regiao)
    {
        GarantirEstado();

        if (!naEstante.Contains(regiao)) return false;
        if (!PodeTraduzirNesteCiclo) return false;

        naEstante.Remove(regiao);
        lidos.Add(regiao);

        var run = RunManager.Instance;
        cicloDaUltimaTraducao = run != null ? run.Cycle : cicloDaUltimaTraducao + 1;

        return true;
    }

    // --------------------------------------------------------------- o atraso

    /// <summary>Quanto do avanço por ciclo os escritos já seguram, de 0 a 1.</summary>
    public static float Atraso => Mathf.Min(AtrasoMaximo, TotalLidos * AtrasoPorEscrito);

    /// <summary>
    /// O que sobra do avanço depois da contenção — é por isto que o
    /// <see cref="RunManager.AdvanceCycle"/> multiplica o passo do relógio.
    /// </summary>
    public static float FatorDeAvanco => 1f - Atraso;

    // ---------------------------------------------------------------- o texto

    /// <summary>
    /// O título da página. <b>Provisório e estrutural:</b> diz de onde ela veio,
    /// porque o que ela conta é decisão do autor e ainda não foi escrito.
    /// </summary>
    public static string Titulo(BiomeType regiao)
    {
        return $"Escrito: {AreaCatalog.Nome(AreaCatalog.Da(regiao))}";
    }

    /// <summary>
    /// O corpo da página, quando existir. Devolve vazio enquanto o texto não for
    /// escrito — quem mostra decide o que fazer com isso, em vez de receber um
    /// texto inventado aqui.
    /// </summary>
    public static string Corpo(BiomeType regiao)
    {
        return "";
    }

    // --------------------------------------------------------------- o save

    /// <summary>As duas listas viram índices de <see cref="BiomeUtil.Playable"/>.</summary>
    public static List<int> SerializarEstante() => ParaIndices(NaEstante());

    public static List<int> SerializarLidos() => ParaIndices(Lidos());

    /// <summary>O ciclo da última tradução, para a regra de uma por ciclo sobreviver ao save.</summary>
    public static int CicloDaUltimaTraducao
    {
        get { GarantirEstado(); return cicloDaUltimaTraducao; }
    }

    static List<int> ParaIndices(List<BiomeType> regioes)
    {
        var lista = new List<int>();
        foreach (var regiao in regioes) lista.Add(System.Array.IndexOf(BiomeUtil.Playable, regiao));
        return lista;
    }

    /// <summary>
    /// Devolve a estante ao ponto do save. Índice fora da faixa ou repetido é
    /// descartado em silêncio, como no <see cref="RegionMap.Restaurar"/>: save
    /// adulterado não deve dar à guilda a mesma página duas vezes.
    /// </summary>
    public static void Restaurar(List<int> estante, List<int> traduzidos, int ultimoCiclo)
    {
        Reiniciar();

        if (traduzidos != null)
            foreach (int indice in traduzidos)
            {
                if (indice < 0 || indice >= BiomeUtil.Playable.Length) continue;
                var regiao = BiomeUtil.Playable[indice];
                if (!lidos.Contains(regiao)) lidos.Add(regiao);
            }

        if (estante != null)
            foreach (int indice in estante)
            {
                if (indice < 0 || indice >= BiomeUtil.Playable.Length) continue;
                var regiao = BiomeUtil.Playable[indice];
                if (!lidos.Contains(regiao) && !naEstante.Contains(regiao)) naEstante.Add(regiao);
            }

        cicloDaUltimaTraducao = ultimoCiclo;
    }
}
