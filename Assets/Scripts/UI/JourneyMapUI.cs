using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Desenha a rota ramificada da jornada e recebe a escolha do jogador.
///
/// O posicionamento é feito à mão (RectTransform absoluto) em vez de layout
/// automático: as arestas precisam saber onde cada nó ficou, e um LayoutGroup
/// só resolve isso no fim do frame.
/// </summary>
public class JourneyMapUI : MonoBehaviour
{
    public static JourneyMapUI Instance;

    [Header("Nós")]
    public RectTransform nodeContainer;
    public GameObject nodePrefab;

    [Header("Arestas")]
    [Tooltip("Deixe vazio para gerar uma imagem simples em runtime.")]
    public GameObject edgePrefab;
    public Color edgeColor = new Color(0.35f, 0.33f, 0.30f, 0.9f);
    public Color edgeAvailableColor = new Color(0.83f, 0.69f, 0.36f, 1f);
    public Color edgeTraveledColor = new Color(0.55f, 0.50f, 0.42f, 1f);
    public float edgeThickness = 3f;

    [Header("Detalhe")]
    public TMP_Text nodeDetailText;

    [Header("Layout")]
    public float layerSpacing = 120f;
    public float slotSpacing = 78f;
    public float leftMargin = 60f;

    [Header("Cores")]
    public Color pastColor = new Color(0.22f, 0.22f, 0.24f);
    public Color currentColor = new Color(0.83f, 0.69f, 0.36f);
    public Color availableColor = new Color(0.30f, 0.55f, 0.38f);
    public Color revealedColor = new Color(0.30f, 0.38f, 0.52f);
    public Color hiddenColor = new Color(0.16f, 0.15f, 0.17f);

    private readonly Dictionary<int, GameObject> nodeViews = new Dictionary<int, GameObject>();
    private readonly List<GameObject> edgeViews = new List<GameObject>();
    private JourneyMap map;
    private int revealedCount;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    /// <summary>Reconstrói o mapa inteiro. Chamar quando a jornada começa.</summary>
    public void BuildMap(JourneyMap journeyMap, int revealed)
    {
        map = journeyMap;
        revealedCount = revealed;

        if (nodeContainer == null || nodePrefab == null || map == null) return;

        UIUtil.ClearChildrenNow(nodeContainer);
        nodeViews.Clear();
        edgeViews.Clear();

        for (int layer = 0; layer < map.layers.Count; layer++)
        {
            var row = map.layers[layer];
            for (int i = 0; i < row.Count; i++)
            {
                MapNode node = row[i];
                GameObject view = Instantiate(nodePrefab, nodeContainer);
                view.name = $"Node_{node.id}";

                var rt = view.GetComponent<RectTransform>();
                if (rt != null)
                {
                    // Todos no mesmo referencial (meio da borda esquerda) para
                    // que as arestas possam ser posicionadas pela diferença.
                    rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = PositionFor(node, row.Count);
                }

                int captured = node.id;
                Button btn = view.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => OnNodeClicked(captured));
                }

                nodeViews[node.id] = view;
            }
        }

        BuildEdges();
        Refresh(map, revealedCount);
    }

    /// <summary>Posição do nó: camadas na horizontal, alternativas na vertical.</summary>
    Vector2 PositionFor(MapNode node, int rowCount)
    {
        float x = leftMargin + node.layer * EffectiveLayerSpacing();

        // Centra a coluna verticalmente, seja ela de 1, 2 ou 3 nós.
        float y = -(node.slot - (rowCount - 1) * 0.5f) * slotSpacing;
        return new Vector2(x, y);
    }

    /// <summary>
    /// Distância entre camadas, encolhida quando a rota é longa demais para a
    /// largura disponível. Sem isto, mapas de 9 camadas saíam pela borda e as
    /// arestas viravam um emaranhado.
    /// </summary>
    float EffectiveLayerSpacing()
    {
        if (map == null || map.LayerCount <= 1 || nodeContainer == null)
            return layerSpacing;

        float util = nodeContainer.rect.width - leftMargin * 2f;
        if (util <= 0f) return layerSpacing;

        return Mathf.Min(layerSpacing, util / (map.LayerCount - 1));
    }

    void BuildEdges()
    {
        foreach (var kv in nodeViews)
        {
            MapNode node = map.GetNode(kv.Key);
            if (node == null) continue;

            foreach (int nextId in node.next)
            {
                if (!nodeViews.TryGetValue(nextId, out GameObject targetView)) continue;
                edgeViews.Add(CreateEdge(kv.Value, targetView, node.id, nextId));
            }
        }

        // As arestas ficam atrás dos nós.
        foreach (var edge in edgeViews)
            if (edge != null) edge.transform.SetAsFirstSibling();
    }

    GameObject CreateEdge(GameObject fromView, GameObject toView, int fromId, int toId)
    {
        GameObject edge = edgePrefab != null
            ? Instantiate(edgePrefab, nodeContainer)
            : new GameObject("Edge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));

        if (edgePrefab == null)
            edge.transform.SetParent(nodeContainer, false);

        edge.name = $"Edge_{fromId}_{toId}";

        var rt = edge.GetComponent<RectTransform>();
        var a = fromView.GetComponent<RectTransform>().anchoredPosition;
        var b = toView.GetComponent<RectTransform>().anchoredPosition;

        Vector2 delta = b - a;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = a;
        rt.sizeDelta = new Vector2(delta.magnitude, edgeThickness);
        rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

        var img = edge.GetComponent<Image>();
        if (img != null)
        {
            img.color = edgeColor;
            img.raycastTarget = false;
        }

        return edge;
    }

    /// <summary>Repinta estados sem recriar nada.</summary>
    public void Refresh(JourneyMap journeyMap, int revealed)
    {
        if (journeyMap != null) map = journeyMap;
        revealedCount = revealed;

        if (map == null) return;

        foreach (var kv in nodeViews)
        {
            MapNode node = map.GetNode(kv.Key);
            if (node != null) PaintNode(kv.Value, node);
        }

        RefreshEdges();
    }

    void RefreshEdges()
    {
        MapNode current = map.Current;

        foreach (var edge in edgeViews)
        {
            if (edge == null) continue;

            var img = edge.GetComponent<Image>();
            if (img == null) continue;

            bool fromCurrent = current != null && edge.name.StartsWith($"Edge_{current.id}_");

            // Trecho já percorrido: liga dois nós visitados. Mostrar por onde o
            // grupo veio dá noção de progresso na rota.
            bool traveled = IsTraveledEdge(edge.name);

            img.color = fromCurrent ? edgeAvailableColor
                      : traveled ? edgeTraveledColor
                                 : edgeColor;

            var rt = edge.GetComponent<RectTransform>();
            if (rt != null)
                rt.sizeDelta = new Vector2(rt.sizeDelta.x,
                    fromCurrent ? edgeThickness * 2f : traveled ? edgeThickness * 1.5f : edgeThickness);
        }
    }

    bool IsTraveledEdge(string edgeName)
    {
        // Formato: Edge_{from}_{to}
        var partes = edgeName.Split('_');
        if (partes.Length < 3) return false;

        int from, to;
        if (!int.TryParse(partes[1], out from) || !int.TryParse(partes[2], out to)) return false;

        MapNode a = map.GetNode(from);
        MapNode b = map.GetNode(to);

        return a != null && b != null && a.visited && b.visited;
    }

    void PaintNode(GameObject view, MapNode node)
    {
        if (view == null) return;

        NodeState state = GetState(node);

        TMP_Text icon = view.transform.Find("Icon")?.GetComponent<TMP_Text>();
        TMP_Text label = view.transform.Find("Label")?.GetComponent<TMP_Text>();
        Image background = view.GetComponent<Image>();
        Button btn = view.GetComponent<Button>();

        // O rótulo diz o que é o nó, não só em que dia ele fica: escolher a rota
        // exige comparar "combate" contra "tesouro", não "3" contra "3".
        if (label != null)
            label.text = state == NodeState.Hidden ? "?" : DescribeType(node);

        if (icon != null)
        {
            switch (state)
            {
                case NodeState.Past: icon.text = "✓"; break;
                case NodeState.Hidden: icon.text = "?"; break;
                default: icon.text = GetIcon(node.eventData); break;
            }
        }

        if (background != null)
        {
            switch (state)
            {
                case NodeState.Past: background.color = pastColor; break;
                case NodeState.Current: background.color = currentColor; break;
                case NodeState.Available: background.color = availableColor; break;
                case NodeState.Revealed: background.color = revealedColor; break;
                default: background.color = hiddenColor; break;
            }
        }

        // Alcançável não basta: o mapa só aceita cliques na janela em que a
        // jornada está de fato esperando a escolha da rota.
        if (btn != null)
        {
            bool aceitandoEscolha = JourneyManager.Instance == null || JourneyManager.Instance.IsChoosingRoute;
            btn.interactable = state == NodeState.Available && aceitandoEscolha;
        }
    }

    NodeState GetState(MapNode node)
    {
        if (node.visited)
            return map.Current != null && map.Current.id == node.id
                ? NodeState.Current
                : NodeState.Past;

        if (map.IsReachable(node.id)) return NodeState.Available;

        // Batedores enxergam além do alcance imediato.
        int currentLayer = map.CurrentLayer;
        if (revealedCount > 0 && node.layer <= currentLayer + 1 + revealedCount)
            return NodeState.Revealed;

        return NodeState.Hidden;
    }

    void OnNodeClicked(int nodeId)
    {
        if (map == null) return;

        MapNode node = map.GetNode(nodeId);
        if (node == null) return;

        ShowNodeDetail(node);

        // Clicar num nó alcançável é a forma de escolher a rota.
        if (map.IsReachable(nodeId))
            JourneyManager.Instance?.OnNodeChosen(nodeId);
    }

    void ShowNodeDetail(MapNode node)
    {
        if (nodeDetailText == null) return;

        NodeState state = GetState(node);
        string dia = $"Dia {node.layer + 1}";

        switch (state)
        {
            case NodeState.Hidden:
                nodeDetailText.text = $"{dia}: território desconhecido.\nContrate batedores na Sala de Mapas.";
                break;

            case NodeState.Past:
                nodeDetailText.text = $"{dia}: {node.eventData?.eventTitle} — já resolvido.";
                break;

            default:
                nodeDetailText.text = $"{dia}: {node.eventData?.eventTitle}\n{node.eventData?.description}";
                break;
        }
    }

    /// <summary>Nome curto do tipo de nó, para caber sob o ícone.</summary>
    static string DescribeType(MapNode node)
    {
        if (node.isBoss) return "Chefe";
        if (node.eventData == null) return "?";

        switch (node.eventData.eventType)
        {
            case JourneyEventType.Combat: return "Combate";
            case JourneyEventType.Treasure: return "Tesouro";
            case JourneyEventType.Trap: return "Perigo";
            case JourneyEventType.Rest: return "Descanso";
            case JourneyEventType.Shop: return "Mercador";
            case JourneyEventType.Story: return "História";
            default: return "Jornada";
        }
    }

    static string GetIcon(EventData data)
    {
        if (data == null) return "🚶";
        if (data.isBossEvent) return "💀";

        switch (data.eventType)
        {
            case JourneyEventType.Combat: return "⚔️";
            case JourneyEventType.Treasure: return "💰";
            case JourneyEventType.Trap: return "⚠️";
            case JourneyEventType.Rest: return "🔥";
            case JourneyEventType.Shop: return "🛒";
            case JourneyEventType.Story: return "📜";
            default: return "🚶";
        }
    }

    private enum NodeState { Past, Current, Available, Revealed, Hidden }
}
