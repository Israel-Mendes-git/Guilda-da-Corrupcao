using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Dispõe a mão em leque, como num jogo de cartas de mesa, e destaca a carta
/// sob o cursor.
///
/// Substitui o HorizontalLayoutGroup: com muitas cartas ele as espremia até
/// ficarem ilegíveis, enquanto o leque as sobrepõe de forma controlada e
/// mantém cada uma reconhecível.
///
/// Nota de implementação: a ordem do leque é mantida numa lista própria, e não
/// lida de `transform.GetChild(i)`. A carta em destaque precisa ir para a
/// frente das vizinhas (`SetAsLastSibling`), o que reordena a hierarquia — se a
/// posição de cada carta dependesse do índice de irmão, o destaque saltaria
/// para a carta errada a cada movimento do mouse.
/// </summary>
[ExecuteAlways]
public class HandFanLayout : MonoBehaviour
{
    [Header("Forma do leque")]
    [Tooltip("Quanto uma carta cobre da vizinha: 0 = lado a lado, 0.5 = metade escondida.")]
    [Range(0f, 0.6f)]
    public float overlap = 0.22f;

    [Tooltip("Largura total disponível; as cartas se apertam para caber nela.")]
    public float maxWidth = 900f;

    [Tooltip("Inclinação da carta mais externa, em graus.")]
    public float maxAngle = 9f;

    [Tooltip("Quanto as cartas das pontas descem, formando o arco.")]
    public float arcDrop = 26f;

    [Header("Destaque")]
    public float hoverLift = 42f;
    public float hoverScale = 1.14f;
    public float lerpSpeed = 14f;

    /// <summary>Ordem lógica do leque — estável, independente da hierarquia.</summary>
    private readonly List<RectTransform> cartas = new List<RectTransform>();

    private RectTransform hovered;

    /// <summary>Evita que a reordenação feita aqui seja lida como "a mão mudou".</summary>
    private bool reordenando;

    void OnEnable() { Rebuild(); }

    void OnTransformChildrenChanged()
    {
        // SetAsLastSibling também dispara este callback. Sem esta guarda, o
        // destaque se reconstruía (e se perdia) a cada frame com o mouse parado
        // sobre uma carta, produzindo um tremor contínuo.
        if (reordenando) return;

        Rebuild();
    }

    void Update()
    {
        Apply(Application.isPlaying ? Time.deltaTime * lerpSpeed : 1f);
    }

    /// <summary>Recaptura as cartas e reinstala os gatilhos de hover.</summary>
    public void Rebuild()
    {
        cartas.Clear();

        for (int i = 0; i < transform.childCount; i++)
        {
            var rt = transform.GetChild(i) as RectTransform;
            if (rt == null) continue;

            cartas.Add(rt);

            var hover = rt.GetComponent<HandCardHover>();
            if (hover == null) hover = rt.gameObject.AddComponent<HandCardHover>();
            hover.Bind(this);
        }

        // A carta destacada pode ter saído da mão nesse meio-tempo.
        if (hovered != null && !cartas.Contains(hovered))
            hovered = null;
    }

    /// <summary>Chamado pelas cartas quando o cursor entra ou sai.</summary>
    public void SetHovered(RectTransform carta, bool on)
    {
        if (on) hovered = carta;
        else if (hovered == carta) hovered = null;
    }

    /// <summary>
    /// Fator para a carta caber na faixa da mão. Reserva espaço para o arco e
    /// para a carta destacada crescer sem invadir o resto da tela.
    /// </summary>
    float CalculateBaseScale()
    {
        var container = transform as RectTransform;
        if (container == null || cartas.Count == 0) return 1f;

        float alturaCarta = AlturaCarta();
        if (alturaCarta <= 1f) return 1f;

        float disponivel = container.rect.height - arcDrop;
        if (disponivel <= 1f) return 1f;

        // hoverScale entra na conta para a carta em destaque também caber.
        return Mathf.Clamp(disponivel / (alturaCarta * hoverScale), 0.3f, 1f);
    }

    float AlturaCarta()
    {
        foreach (var c in cartas)
            if (c != null) return c.rect.height;
        return 0f;
    }

    float LarguraCarta()
    {
        foreach (var c in cartas)
            if (c != null) return c.rect.width;
        return 100f;
    }

    void Apply(float t)
    {
        // Descarta cartas destruídas sem reconstruir tudo.
        for (int i = cartas.Count - 1; i >= 0; i--)
            if (cartas[i] == null) cartas.RemoveAt(i);

        int count = cartas.Count;
        if (count == 0) return;

        // As cartas do prefab são maiores que a faixa reservada à mão, então o
        // conjunto encolhe até caber.
        float baseScale = CalculateBaseScale();

        // O espaçamento acompanha a largura real da carta já escalada, senão as
        // cartas viram uma pilha ilegível quando o leque encolhe.
        float larguraCarta = Mathf.Max(20f, LarguraCarta() * baseScale);
        float espacoIdeal = larguraCarta * (1f - overlap);

        float spacing = count > 1 ? Mathf.Min(espacoIdeal, maxWidth / (count - 1)) : 0f;
        float inicio = -(spacing * (count - 1)) * 0.5f;

        RectTransform paraFrente = null;

        for (int i = 0; i < count; i++)
        {
            RectTransform rt = cartas[i];

            // A carta arrastada saiu da mão: quem manda nela é o drag.
            if (Application.isPlaying)
            {
                var drag = rt.GetComponent<CardDragHandler>();
                if (drag != null && CardDragHandler.Dragging == drag) continue;
            }

            // -1 na ponta esquerda, +1 na direita, 0 no meio.
            float norm = count > 1 ? (i / (float)(count - 1)) * 2f - 1f : 0f;
            bool destacada = rt == hovered;

            Vector2 alvoPos = new Vector2(
                inicio + spacing * i,
                (-Mathf.Abs(norm) * arcDrop + (destacada ? hoverLift : 0f)) * baseScale);

            Quaternion alvoRot = Quaternion.Euler(0, 0, destacada ? 0f : -norm * maxAngle);
            Vector3 alvoEsc = Vector3.one * baseScale * (destacada ? hoverScale : 1f);

            if (t >= 1f)
            {
                rt.anchoredPosition = alvoPos;
                rt.localRotation = alvoRot;
                rt.localScale = alvoEsc;
            }
            else
            {
                rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, alvoPos, t);
                rt.localRotation = Quaternion.Lerp(rt.localRotation, alvoRot, t);
                rt.localScale = Vector3.Lerp(rt.localScale, alvoEsc, t);
            }

            if (destacada) paraFrente = rt;
        }

        BringToFront(paraFrente);
    }

    /// <summary>
    /// Põe a carta destacada à frente das vizinhas, mexendo na hierarquia só
    /// quando ela ainda não está no topo — reordenar todo frame era o que
    /// realimentava o ciclo de reconstrução.
    /// </summary>
    void BringToFront(RectTransform carta)
    {
        if (carta == null) return;
        if (carta.GetSiblingIndex() == transform.childCount - 1) return;

        reordenando = true;
        carta.SetAsLastSibling();
        reordenando = false;
    }
}

/// <summary>Avisa o leque quando o cursor entra e sai de uma carta.</summary>
public class HandCardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private HandFanLayout fan;
    private RectTransform rect;

    public void Bind(HandFanLayout owner)
    {
        fan = owner;
        rect = transform as RectTransform;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (fan != null) fan.SetHovered(rect, true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (fan != null) fan.SetHovered(rect, false);
    }

    void OnDisable()
    {
        // Sem isto, uma carta jogada deixava o leque achando que ainda está sob
        // o cursor, e a próxima carta a ocupar aquele lugar nascia destacada.
        if (fan != null) fan.SetHovered(rect, false);
    }
}
