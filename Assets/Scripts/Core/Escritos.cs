using System.Collections.Generic;
using System.Linq;
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

    // ---------------------------------------------------------------- a carta

    /// <summary>Onde moram as cartas de escrito, fora de <c>Resources/Cards</c>.</summary>
    ///
    /// Pasta própria de propósito: tudo o que carrega "Cards" — o gerador de
    /// baralhos, a Biblioteca, o editor de baralhos, o smoke test — passaria a
    /// sortear e vender o escrito como carta comum. Aqui só quem precisa dela
    /// a encontra: o índice do save e a Biblioteca ao traduzir.
    public const string PastaDasCartas = "Escritos";

    /// <summary>
    /// O escrito traduzido <b>entra no baralho</b> como carta de contenção — a
    /// única que age sobre o mundo, e o que se leva à luta final (decisão do
    /// autor em 09/09). Um asset por região, em <c>Resources/Escritos</c>.
    /// Devolve null enquanto o asset não existir.
    /// </summary>
    public static CardData Carta(BiomeType regiao)
    {
        string nome = NomeDoAsset(regiao);
        return string.IsNullOrEmpty(nome) ? null : Resources.Load<CardData>($"{PastaDasCartas}/{nome}");
    }

    /// <summary>Todas as cartas de escrito do projeto — para as travas do smoke test.</summary>
    public static CardData[] TodasAsCartas() => Resources.LoadAll<CardData>(PastaDasCartas);

    /// <summary>
    /// O nome do asset da carta daquela região. Estrutural: diz de onde veio.
    /// Sem acento no nome do arquivo — é caminho de <c>Resources.Load</c>.
    /// </summary>
    public static string NomeDoAsset(BiomeType regiao)
    {
        switch (AreaCatalog.Da(regiao))
        {
            case AreaType.Mata: return "Escrito da Mata";
            case AreaType.Cripta: return "Escrito da Cripta";
            case AreaType.Aldeia: return "Escrito da Aldeia";
            case AreaType.Oraculo: return "Escrito do Oraculo";
            case AreaType.Torre: return "Escrito da Torre";
            case AreaType.Forja: return "Escrito da Forja";
            case AreaType.Covil: return "Escrito do Covil";
            default: return "";
        }
    }

    /// <summary>Esta carta é um escrito? É a única com efeito de conter.</summary>
    public static bool EhEscrito(CardData carta) =>
        carta != null && carta.journeyEffect == JourneyEffectType.Conter;

    /// <summary>
    /// Quem carrega a carta daquele escrito: o herói vivo cujo baralho a tem.
    /// Derivado do baralho, e não guardado à parte — assim o save não precisa
    /// de campo novo, e um portador morto (o baralho dele some com ele) deixa
    /// a carta sem dono para a Biblioteca entregar de novo.
    /// </summary>
    public static HeroData Portador(BiomeType regiao)
    {
        var guilda = GuildManager.Instance;
        CardData carta = Carta(regiao);
        if (guilda == null || carta == null) return null;

        foreach (HeroData heroi in guilda.roster)
        {
            if (heroi == null || !heroi.IsAlive) continue;

            DeckData baralho = DeckRepository.GetDeck(heroi);
            if (baralho?.cards != null && baralho.cards.Contains(carta)) return heroi;
        }

        return null;
    }

    /// <summary>
    /// Põe a carta do escrito no baralho do herói. Devolve false se a página não
    /// foi traduzida, se a carta não existe, ou se alguém vivo já a carrega.
    /// </summary>
    public static bool Entregar(BiomeType regiao, HeroData heroi)
    {
        GarantirEstado();

        if (heroi == null || !heroi.IsAlive) return false;
        if (!lidos.Contains(regiao)) return false;
        if (Portador(regiao) != null) return false;

        CardData carta = Carta(regiao);
        if (carta == null) return false;

        DeckData baralho = DeckRepository.GetDeck(heroi);
        if (baralho == null) return false;
        if (baralho.cards == null) baralho.cards = new List<CardData>();

        // Direto na lista: o escrito entra mesmo com o baralho no limite — é a
        // única carta que não se compra, e não disputa vaga com as que se compram.
        baralho.cards.Add(carta);
        GuildManager.Instance?.onRosterChanged?.Invoke();
        return true;
    }

    /// <summary>Páginas lidas cuja carta não está com ninguém vivo.</summary>
    public static List<BiomeType> LidosSemPortador()
    {
        GarantirEstado();
        return lidos.Where(r => Carta(r) != null && Portador(r) == null).ToList();
    }

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
