using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Arrastar a carta até o alvo é como se joga no combate.
///
/// A carta sai da mão e passa a viver no Canvas enquanto é arrastada, senão o
/// layout da mão a recortaria e ela ficaria por baixo dos inimigos. Quem aplica
/// o efeito é o <see cref="CombatDropTarget"/> ao receber o drop; este script
/// cuida apenas do gesto e de devolver a carta se a jogada não acontecer.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class CardDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    /// <summary>Carta sendo arrastada agora, ou null. Lida pelos alvos de drop.</summary>
    public static CardDragHandler Dragging { get; private set; }

    public CardData Card { get; private set; }

    [Header("Sensação")]
    public float dragScale = 1.08f;
    public float dragAlpha = 0.85f;

    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private RectTransform rect;

    private Transform originalParent;
    private int originalSiblingIndex;
    private Vector2 originalAnchoredPos;
    private Vector3 originalScale;

    private bool consumed;

    public void Initialize(CardData card)
    {
        Card = card;
    }

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        canvas = GetComponentInParent<Canvas>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (Card == null) return;

        var combat = CombatManager.Instance;
        if (combat == null || !combat.CanAffordCard(Card))
        {
            // Deixa claro por que a carta não sai do lugar.
            combat?.ShowCardBlockedReason(Card);
            return;
        }

        Dragging = this;
        consumed = false;

        originalParent = transform.parent;
        originalSiblingIndex = transform.GetSiblingIndex();
        originalAnchoredPos = rect.anchoredPosition;
        originalScale = transform.localScale;

        // Sobe para o Canvas para não ser recortada nem ficar atrás de nada.
        if (canvas != null)
            transform.SetParent(canvas.transform, true);
        transform.SetAsLastSibling();

        canvasGroup.alpha = dragAlpha;
        canvasGroup.blocksRaycasts = false;   // deixa o raycast alcançar os alvos
        transform.localScale = originalScale * dragScale;

        combat.OnCardPickedUp(Card);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (Dragging != this) return;

        // Converte o ponteiro para o espaço do Canvas: sem isso a carta
        // descola do cursor em telas com escala diferente de 1.
        if (canvas != null && rect != null)
        {
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform,
                eventData.position,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                out local);
            rect.localPosition = local;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (Dragging != this) return;

        var combat = CombatManager.Instance;

        // Ninguém pegou o drop: cartas sem alvo ainda podem ser jogadas soltando
        // sobre a área de combate, longe da mão.
        if (!consumed && combat != null && !CombatManager.CardNeedsTarget(Card))
        {
            if (DraggedFarFromHand(eventData))
                consumed = combat.TryPlayCard(Card, null, null);
        }

        Dragging = null;
        combat?.OnCardDropped();

        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        transform.localScale = originalScale;

        if (consumed)
        {
            // A mão será reconstruída; esta instância não serve mais.
            gameObject.SetActive(false);
            return;
        }

        ReturnToHand();
    }

    /// <summary>Marca a jogada como aceita — chamado pelo alvo que recebeu o drop.</summary>
    public void MarkConsumed()
    {
        consumed = true;
    }

    bool DraggedFarFromHand(PointerEventData eventData)
    {
        if (originalParent == null) return true;

        var handRect = originalParent as RectTransform;
        if (handRect == null) return true;

        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        // Soltou fora da mão = tentativa de jogar.
        return !RectTransformUtility.RectangleContainsScreenPoint(handRect, eventData.position, cam);
    }

    void ReturnToHand()
    {
        if (originalParent == null) return;

        transform.SetParent(originalParent, false);
        transform.SetSiblingIndex(originalSiblingIndex);
        rect.anchoredPosition = originalAnchoredPos;
    }
}
