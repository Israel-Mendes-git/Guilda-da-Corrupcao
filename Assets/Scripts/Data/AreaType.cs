using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// As sete áreas do mundo — o lugar do jogo desde 10/09/2026.
///
/// O que o código chama de <see cref="BiomeType"/> deixou de ser conceito de
/// design e virou o <b>aspecto</b> do local: a arte, o pool de eventos e o
/// bestiário continuam indexados por ele. A área é a camada por cima, e é ela
/// que tem regra própria, o que dá, o que cobra e como se fecha.
///
/// <b>Por que duas camadas e não um enum só:</b> os 25 eventos e os 11 inimigos
/// guardam o bioma por número. Renomear os valores existentes trocaria o
/// significado de cada asset em silêncio — a armadilha que já custou caro no
/// projeto. Com a área vestindo um aspecto, nenhum asset se mexe.
/// </summary>
public enum AreaType
{
    /// <summary>Nenhuma área — e, no grafo do mapa, a própria guilda.</summary>
    None = 0,
    Mata = 1,
    Cripta = 2,
    Aldeia = 3,
    Oraculo = 4,
    Torre = 5,
    Forja = 6,
    Covil = 7
}

/// <summary>O que cada área dá em troca da viagem. Dentro de um grupo elas são
/// substitutas: escolhe-se por qual caminho conseguir, não se coleciona todas.</summary>
public enum AreaMoeda { Tempo, Forca, Informacao }

/// <summary>
/// O mundo como plano navegável: onde cada área fica, quem toca quem, e o que
/// cada uma é.
///
/// Estático pela mesma razão do <see cref="RegionMap"/>: o smoke test simula
/// centenas de jornadas em edit mode, onde não há MonoBehaviour vivo.
/// </summary>
public static class AreaCatalog
{
    /// <summary>A ficha de uma área — tudo o que o mapa e a estrada precisam saber dela.</summary>
    public class Ficha
    {
        public AreaType area;
        public string nome;

        /// <summary>
        /// O gênero do nome, para quem escreve frase sobre o lugar.
        ///
        /// "O Covil selada" e "O Oráculo inteira no mapa" são o mesmo defeito
        /// que os nomes sorteados do quadro antigo tinham — e aqui não há
        /// sorteio que sirva de desculpa: são sete nomes fixos.
        /// </summary>
        public bool feminino = true;

        public BiomeType aspecto;
        public AreaMoeda moeda;
        public Vector2 posicao;
        public AreaType[] vizinhas;

        /// <summary>A regra que só vale ali. Uma frase — é o que o mapa mostra.</summary>
        public string regra;
        public string oQueDa;
        public string oQueCobra;

        /// <summary>O ato que fecha a área. Só a Mata é combate limpo.</summary>
        public string selo;

        /// <summary>Nome com o símbolo do aspecto na frente, como o resto do jogo escreve.</summary>
        public string NomeComIcone => $"{Simbolo} {nome}";

        /// <summary>
        /// O emoji vem do aspecto, e não é escolhido por área de propósito: cada
        /// glifo novo entra no atlas da fonte de emoji, que já estourou uma vez
        /// e gravou textura nula no asset.
        /// </summary>
        public string Simbolo
        {
            get
            {
                string nomeDoBioma = BiomeUtil.GetDisplayName(aspecto);
                int espaco = nomeDoBioma.IndexOf(' ');
                return espaco > 0 ? nomeDoBioma.Substring(0, espaco) : "🧭";
            }
        }
    }

    /// <summary>As sete, na ordem em que o jogador as alcança.</summary>
    public static readonly AreaType[] Todas =
    {
        AreaType.Mata, AreaType.Cripta, AreaType.Aldeia, AreaType.Oraculo,
        AreaType.Torre, AreaType.Forja, AreaType.Covil
    };

    /// <summary>
    /// A guilda no sul do mapa, na margem de cá do rio — e não no centro.
    ///
    /// No centro todas as áreas ficam à mesma distância, e a distância deixa de
    /// significar alguma coisa. As coordenadas são frações do papel, que é
    /// deitado: <b>x cresce para o leste, y para o norte</b>.
    ///
    /// <b>A geografia, e não um leque de nós.</b> O <b>Rio Cinza</b> corre de
    /// oeste a leste no meio do papel e separa o que fica ao alcance de uma ida
    /// curta — a Mata e a Cripta, na margem de cá — de todo o resto. A <b>Serra
    /// Quebrada</b> fecha o norte, e é nela que o Covil se enfia. Foi por isso
    /// que a Torre e o Covil ficaram longe: não é distância escolhida, é o que
    /// há entre eles e a cidade.
    /// </summary>
    public static readonly Vector2 PosicaoDaGuilda = new Vector2(0.44f, 0.16f);

    /// <summary>
    /// Quanto custa atravessar de um lugar ao vizinho.
    ///
    /// Com isto, a Mata sai por 2 dias de ida e 2 de volta, e o Covil por 6 e 6.
    /// Encadear Aldeia e Forja numa saída custa 12 dias contra 20 em duas
    /// viagens — é o que faz encadear ser mais barato que voltar, sem nenhuma
    /// regra dizendo isso ao jogador.
    /// </summary>
    public const int DiasPorTravessia = 2;

    static readonly Dictionary<AreaType, Ficha> fichas = new Dictionary<AreaType, Ficha>
    {
        {
            AreaType.Mata, new Ficha
            {
                area = AreaType.Mata,
                nome = "A Mata",
                aspecto = BiomeType.Forest,
                moeda = AreaMoeda.Tempo,
                posicao = new Vector2(0.19f, 0.31f),
                vizinhas = new[] { AreaType.None, AreaType.Cripta, AreaType.Oraculo, AreaType.Aldeia },
                regra = "O mato fecha atrás do grupo: quanto mais fundo, mais caro o dia.",
                oQueDa = "Caça — mantimentos que renovam na estrada.",
                oQueCobra = "Dias. O caminho serpenteia, e a viagem é mais lenta que a distância.",
                selo = "Derrubar o que espalha. Combate limpo."
            }
        },
        {
            AreaType.Cripta, new Ficha
            {
                area = AreaType.Cripta,
                nome = "A Cripta",
                aspecto = BiomeType.Ruins,
                moeda = AreaMoeda.Forca,
                posicao = new Vector2(0.70f, 0.29f),
                vizinhas = new[] { AreaType.None, AreaType.Mata, AreaType.Aldeia, AreaType.Forja },
                regra = "Os mortos se erguem uma vez por turno enquanto o necromante estiver de pé. "
                      + "Herói enterrado sem tributo levanta contra o grupo.",
                oQueDa = "Espólio em dobro — foram enterrados com o que tinham.",
                oQueCobra = "Estresse.",
                selo = "Consagrar de novo, com os nomes dos seus próprios mortos."
            }
        },
        {
            AreaType.Aldeia, new Ficha
            {
                area = AreaType.Aldeia,
                nome = "A Aldeia",
                aspecto = BiomeType.Swamp,
                moeda = AreaMoeda.Tempo,
                posicao = new Vector2(0.46f, 0.58f),
                vizinhas = new[] { AreaType.Mata, AreaType.Cripta, AreaType.Oraculo, AreaType.Torre, AreaType.Forja },
                regra = "Parte dos moradores ainda é gente, e os corrompidos têm consciência. "
                      + "Poupar devolve moral; matar dá recurso e cobra moral.",
                oQueDa = "Recurso de quem foi morto, ou moral de quem foi poupado.",
                oQueCobra = "A escolha, um a um.",
                selo = "Fogo, ou levar embora quem ainda dá para levar. Duas saídas."
            }
        },
        {
            AreaType.Oraculo, new Ficha
            {
                area = AreaType.Oraculo,
                nome = "O Oráculo",
                feminino = false,
                aspecto = BiomeType.Desert,
                moeda = AreaMoeda.Informacao,
                posicao = new Vector2(0.12f, 0.55f),
                vizinhas = new[] { AreaType.Mata, AreaType.Aldeia, AreaType.Torre },
                regra = "A cega da gruta responde qualquer coisa. Cada resposta custa uma lembrança: "
                      + "um herói perde uma carta do baralho, para sempre.",
                oQueDa = "Saber antes — a rota inteira, quem guarda o quê, o que falta para selar.",
                oQueCobra = "Uma carta, sem volta.",
                selo = "Perguntar a ela o que ela é. A resposta cobra o preço de sempre."
            }
        },
        {
            AreaType.Torre, new Ficha
            {
                area = AreaType.Torre,
                nome = "A Torre",
                aspecto = BiomeType.Tundra,
                moeda = AreaMoeda.Forca,
                posicao = new Vector2(0.23f, 0.83f),
                vizinhas = new[] { AreaType.Oraculo, AreaType.Aldeia, AreaType.Covil },
                regra = "A cada combate, um dos seus heróis aparece do outro lado, "
                      + "com o baralho e o equipamento dele.",
                oQueDa = "As cartas que o feiticeiro colecionou — as que a Biblioteca não vende.",
                oQueCobra = "Estresse em quem derrubou a própria cópia.",
                selo = "Subir até o alto e quebrar o altar com o livro corrompido."
            }
        },
        {
            AreaType.Forja, new Ficha
            {
                area = AreaType.Forja,
                nome = "A Forja",
                aspecto = BiomeType.Volcano,
                moeda = AreaMoeda.Forca,
                posicao = new Vector2(0.82f, 0.54f),
                vizinhas = new[] { AreaType.Cripta, AreaType.Aldeia, AreaType.Covil },
                regra = "Dá para forjar na estrada, sem voltar à guilda — e cada peça forjada "
                      + "chama o que mora lá.",
                oQueDa = "Equipamento corrompido: melhor que o da guilda, e apodrece quem o carrega.",
                oQueCobra = "O que vem quando você bate.",
                selo = "Apagar o fogo. A área para de forjar para sempre."
            }
        },
        {
            AreaType.Covil, new Ficha
            {
                area = AreaType.Covil,
                nome = "O Covil",
                feminino = false,
                aspecto = BiomeType.Mountain,
                moeda = AreaMoeda.Forca,
                posicao = new Vector2(0.70f, 0.85f),
                vizinhas = new[] { AreaType.Torre, AreaType.Forja },
                regra = "A luz desperta o dragão. Cada tocha acesa e cada peça levada "
                      + "aproximam o despertar.",
                oQueDa = "As relíquias — é o único lugar do mundo onde elas se acumulam.",
                oQueCobra = "Nervo. Atravessar no escuro desgasta mais que qualquer outra área.",
                selo = "Acordar o dragão de propósito e sair antes. Você não o mata: você o usa."
            }
        }
    };

    /// <summary>A ficha da área, ou <c>null</c> para a guilda.</summary>
    public static Ficha De(AreaType area)
    {
        return fichas.ContainsKey(area) ? fichas[area] : null;
    }

    /// <summary>
    /// Qual área veste este aspecto. A correspondência é um para um: cada um dos
    /// sete biomas é o aspecto de exatamente uma área, e é isso que faz o mundo
    /// novo caber sobre os assets antigos.
    /// </summary>
    public static AreaType Da(BiomeType aspecto)
    {
        foreach (var par in fichas)
            if (par.Value.aspecto == aspecto) return par.Key;

        return AreaType.None;
    }

    public static BiomeType Aspecto(AreaType area)
    {
        Ficha f = De(area);
        return f != null ? f.aspecto : BiomeType.Any;
    }

    public static string Nome(AreaType area)
    {
        Ficha f = De(area);
        return f != null ? f.NomeComIcone : "🧭 Terras Ermas";
    }

    /// <summary>A forma que concorda com o nome do lugar.</summary>
    public static string Concordar(AreaType area, string feminino, string masculino)
    {
        Ficha f = De(area);
        return f == null || f.feminino ? feminino : masculino;
    }

    public static Vector2 Posicao(AreaType area)
    {
        Ficha f = De(area);
        return f != null ? f.posicao : PosicaoDaGuilda;
    }

    public static bool SaoVizinhas(AreaType a, AreaType b)
    {
        Ficha f = De(a);
        if (f == null) return De(b) != null && System.Array.IndexOf(De(b).vizinhas, AreaType.None) >= 0;

        return System.Array.IndexOf(f.vizinhas, b) >= 0;
    }

    /// <summary>
    /// O caminho da guilda até a área, sem contar a guilda e contando o destino.
    ///
    /// É a largura primeiro num grafo de oito nós — não há por que ser mais
    /// esperto que isso. O que importa é que a rota exista: <b>as áreas se
    /// tocam</b>, e chegar às distantes obriga a atravessar as próximas. É por
    /// isso que a corrupção de uma área pesa mesmo quando não se vai a ela.
    /// </summary>
    public static List<AreaType> Rota(AreaType destino)
    {
        var rota = new List<AreaType>();
        if (De(destino) == null) return rota;

        var anterior = new Dictionary<AreaType, AreaType>();
        var vistos = new HashSet<AreaType> { AreaType.None };
        var fila = new Queue<AreaType>();
        fila.Enqueue(AreaType.None);

        while (fila.Count > 0)
        {
            AreaType atual = fila.Dequeue();
            if (atual == destino) break;

            foreach (var vizinha in Vizinhas(atual))
            {
                if (vistos.Contains(vizinha)) continue;

                vistos.Add(vizinha);
                anterior[vizinha] = atual;
                fila.Enqueue(vizinha);
            }
        }

        if (!anterior.ContainsKey(destino)) return rota;

        AreaType passo = destino;
        while (passo != AreaType.None)
        {
            rota.Insert(0, passo);
            passo = anterior[passo];
        }

        return rota;
    }

    /// <summary>
    /// Quem faz fronteira com este lugar. A guilda não tem ficha, então as
    /// vizinhas dela são as áreas que a listam.
    /// </summary>
    public static List<AreaType> Vizinhas(AreaType area)
    {
        var lista = new List<AreaType>();

        if (area == AreaType.None)
        {
            foreach (var par in fichas)
                if (System.Array.IndexOf(par.Value.vizinhas, AreaType.None) >= 0) lista.Add(par.Key);

            return lista;
        }

        Ficha f = De(area);
        if (f == null) return lista;

        foreach (var vizinha in f.vizinhas)
            if (vizinha != AreaType.None) lista.Add(vizinha);

        return lista;
    }

    /// <summary>Quantas travessias da guilda até lá.</summary>
    public static int Saltos(AreaType destino)
    {
        return Rota(destino).Count;
    }

    /// <summary>Dias só de ida. A volta custa o mesmo, e quem paga é a jornada.</summary>
    public static int DiasDeIda(AreaType destino)
    {
        return Saltos(destino) * DiasPorTravessia;
    }
}
