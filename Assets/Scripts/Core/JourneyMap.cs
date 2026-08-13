using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Um ponto da rota. O jogador nunca percorre todos: escolhe um por camada.
/// </summary>
public class MapNode
{
    public int id;
    public int layer;              // 0 = primeira escolha; última camada = chefe
    public int slot;               // posição vertical dentro da camada (0 = topo)
    public EventData eventData;
    public readonly List<int> next = new List<int>();

    public bool visited;
    public bool isBoss;

    public override string ToString() => $"#{id} L{layer}S{slot} {eventData?.eventTitle}";
}

/// <summary>
/// Rota ramificada da jornada, no formato do Slay the Spire: cada dia oferece
/// dois ou três caminhos e todos desembocam no chefe.
///
/// A fila linear que existia antes virou um caso particular disto — um mapa de
/// uma coluna só. Quem consome o mapa lida sempre com "nós alcançáveis a partir
/// de onde estou", nunca com índices de dia.
/// </summary>
public class JourneyMap
{
    public readonly List<List<MapNode>> layers = new List<List<MapNode>>();

    /// <summary>-1 enquanto o grupo não entrou no primeiro nó.</summary>
    public int currentNodeId = -1;

    private readonly Dictionary<int, MapNode> byId = new Dictionary<int, MapNode>();

    public int LayerCount => layers.Count;
    public MapNode Current => currentNodeId >= 0 ? GetNode(currentNodeId) : null;

    /// <summary>Camada onde o grupo está; -1 antes de começar.</summary>
    public int CurrentLayer => Current?.layer ?? -1;

    public MapNode GetNode(int id) => byId.TryGetValue(id, out var n) ? n : null;

    public IEnumerable<MapNode> AllNodes() => byId.Values;

    public MapNode BossNode => layers.Count > 0 ? layers[layers.Count - 1][0] : null;

    internal void Register(MapNode node)
    {
        byId[node.id] = node;
    }

    /// <summary>
    /// Para onde o grupo pode ir agora. Antes do primeiro passo, a camada 0
    /// inteira está disponível — a escolha de entrada também é do jogador.
    /// </summary>
    public List<MapNode> GetChoices()
    {
        if (currentNodeId < 0)
            return layers.Count > 0 ? new List<MapNode>(layers[0]) : new List<MapNode>();

        MapNode cur = Current;
        if (cur == null) return new List<MapNode>();

        return cur.next.Select(GetNode).Where(n => n != null).ToList();
    }

    /// <summary>O nó pode ser escolhido a partir da posição atual?</summary>
    public bool IsReachable(int nodeId) => GetChoices().Any(n => n.id == nodeId);

    /// <summary>Move o grupo. Devolve false se o nó não é alcançável daqui.</summary>
    public bool MoveTo(int nodeId)
    {
        if (!IsReachable(nodeId)) return false;

        MapNode node = GetNode(nodeId);
        node.visited = true;
        currentNodeId = nodeId;
        return true;
    }

    /// <summary>Chegou ao fim da rota — não há para onde seguir.</summary>
    public bool IsAtEnd => Current != null && Current.next.Count == 0;

    /// <summary>
    /// Substitui o evento de um nó, preservando a topologia. Usado pelos desvios
    /// da Sala de Mapas: troca-se o que há adiante, não o desenho da rota.
    /// </summary>
    public void ReplaceEvent(int nodeId, EventData replacement)
    {
        MapNode node = GetNode(nodeId);
        if (node != null && replacement != null)
            node.eventData = replacement;
    }
}

/// <summary>
/// Gera a rota ramificada. Duas garantias que o desenho precisa ter:
/// todo nó tem pelo menos uma saída (logo, todo caminho chega ao chefe) e
/// todo nó tem pelo menos um pai (logo, nenhum nó fica inalcançável).
/// </summary>
public static class JourneyMapGenerator
{
    /// <param name="days">Quantos dias antes do chefe.</param>
    public static JourneyMap Generate(QuestData quest, int days, int minPerLayer = 2, int maxPerLayer = 3)
    {
        var map = new JourneyMap();
        int nextId = 0;
        days = Mathf.Max(1, days);

        EventPool.ResetHistory();

        // Camadas de rota: largura variável, para o mapa não virar uma grade.
        for (int layer = 0; layer < days; layer++)
        {
            int width = Random.Range(minPerLayer, maxPerLayer + 1);

            // Estreitar perto do chefe dá a sensação de funil.
            if (layer == days - 1) width = Mathf.Min(width, 2);

            var row = new List<MapNode>();
            for (int slot = 0; slot < width; slot++)
            {
                var node = new MapNode
                {
                    id = nextId++,
                    layer = layer,
                    slot = slot,
                    eventData = EventPool.GetRandomEvent(quest.biomeType, quest.corruptionLevel, layer + 1)
                };
                row.Add(node);
                map.Register(node);
            }
            map.layers.Add(row);
        }

        // Chefe: nó único, destino obrigatório de toda a rota.
        var boss = new MapNode
        {
            id = nextId,
            layer = days,
            slot = 0,
            isBoss = true,
            eventData = EventPool.GetFinalEvent(quest.biomeType)
        };
        map.layers.Add(new List<MapNode> { boss });
        map.Register(boss);

        for (int layer = 0; layer < map.layers.Count - 1; layer++)
            Connect(map.layers[layer], map.layers[layer + 1]);

        return map;
    }

    /// <summary>Com que frequência um nó oferece mais de um destino.</summary>
    public static float branchChance = 0.85f;

    /// <summary>
    /// Liga duas camadas dando a cada nó um intervalo contíguo de destinos.
    /// Como os intervalos avançam junto com o índice (nunca recuam), as arestas
    /// não se cruzam — e como quase todo intervalo tem largura 2, o jogador
    /// realmente escolhe a cada passo, em vez de seguir um corredor.
    /// </summary>
    static void Connect(List<MapNode> from, List<MapNode> to)
    {
        int n = from.Count;
        int m = to.Count;

        int prevLo = 0;
        int prevHi = 0;

        for (int a = 0; a < n; a++)
        {
            int center = n == 1
                ? Mathf.RoundToInt((m - 1) * 0.5f)
                : Mathf.RoundToInt(a * (m - 1) / (float)(n - 1));

            int lo = center;
            int hi = center;

            // Bifurcação: abre o intervalo para o vizinho de cima ou de baixo.
            if (m >= 2 && Random.value < branchChance)
            {
                bool podeDescer = hi + 1 < m;
                bool podeSubir = lo - 1 >= 0;

                if (podeDescer && (!podeSubir || Random.value < 0.5f)) hi++;
                else if (podeSubir) lo--;
            }

            // Ordem vertical preservada: o intervalo deste nó nunca começa nem
            // termina antes do intervalo do nó acima dele.
            lo = Mathf.Max(lo, prevLo);
            hi = Mathf.Max(hi, prevHi);
            hi = Mathf.Clamp(hi, lo, m - 1);

            // Sem leques largos demais: no máximo três destinos por nó.
            hi = Mathf.Min(hi, lo + 2);

            for (int b = lo; b <= hi; b++)
                AddEdge(from[a], to[b]);

            prevLo = lo;
            prevHi = hi;
        }

        // Toda entrada existe: adota os órfãos pelo pai mais próximo.
        for (int b = 0; b < m; b++)
        {
            if (from.Any(node => node.next.Contains(to[b].id))) continue;

            int best = 0;
            int bestDist = int.MaxValue;
            for (int a = 0; a < n; a++)
            {
                int center = n == 1
                    ? Mathf.RoundToInt((m - 1) * 0.5f)
                    : Mathf.RoundToInt(a * (m - 1) / (float)(n - 1));

                int dist = Mathf.Abs(center - b);
                if (dist < bestDist) { bestDist = dist; best = a; }
            }
            AddEdge(from[best], to[b]);
        }
    }

    static void AddEdge(MapNode from, MapNode to)
    {
        if (!from.next.Contains(to.id))
            from.next.Add(to.id);
    }
}
