using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Carta no editor de baralho, arrastável entre a coleção e o deck.
///
/// O arrasto antigo nunca completava: nada implementava IDropHandler, então a
/// carta sempre voltava ao lugar. Agora quem recebe é a <see cref="DeckDropZone"/>.
/// </summary>
public class CardInDeck : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    /// <summary>Carta sendo arrastada agora, ou null. Lida pelas zonas de drop.</summary>
    public static CardInDeck Dragging { get; private set; }

    public CardData cardData;
    public bool isInDeck;

    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;

    private Transform startParent;
    private int startSiblingIndex;
    private Vector2 startAnchoredPos;
    private bool consumed;

    [Header("UI References")]
    public TMP_Text cardNameText;
    public TMP_Text cardCostText;
    public TMP_Text cardDescriptionText;
    public Image cardImage;
    public Image rarityBorder;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        canvas = GetComponentInParent<Canvas>();
    }

    public void Initialize(CardData card, bool inDeck)
    {
        cardData = card;
        isInDeck = inDeck;

        if (cardNameText != null) cardNameText.text = card.cardName;
        if (cardCostText != null) cardCostText.text = $"⚡ {card.energyCost}";
        if (cardDescriptionText != null) cardDescriptionText.text = card.cardDescription;
        if (cardImage != null && card.cardImage != null) cardImage.sprite = card.cardImage;

        if (rarityBorder != null)
        {
            switch (card.rarity)
            {
                case CardRarity.Common: rarityBorder.color = new Color(0.5f, 0.5f, 0.5f); break;
                case CardRarity.Rare: rarityBorder.color = new Color(0.2f, 0.4f, 0.8f); break;
                case CardRarity.Epic: rarityBorder.color = new Color(0.6f, 0.2f, 0.8f); break;
                case CardRarity.Legendary: rarityBorder.color = new Color(0.9f, 0.7f, 0.1f); break;
            }
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        Dragging = this;
        consumed = false;

        startParent = transform.parent;
        startSiblingIndex = transform.GetSiblingIndex();
        startAnchoredPos = rectTransform.anchoredPosition;

        // Sobe para o Canvas para não ser recortada pelo container de origem.
        if (canvas != null)
            transform.SetParent(canvas.transform, true);
        transform.SetAsLastSibling();

        canvasGroup.alpha = 0.7f;
        canvasGroup.blocksRaycasts = false;   // sem isto o drop nunca chega à zona
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (Dragging != this || canvas == null) return;

        // Converter para o espaço do Canvas: atribuir a posição de tela direto
        // fazia a carta descolar do cursor quando o Canvas tinha escala ≠ 1.
        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform,
            eventData.position,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
            out local);

        rectTransform.localPosition = local;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        Dragging = null;

        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;

        // Consumida: o DeckManager vai reconstruir as listas.
        if (consumed) return;

        transform.SetParent(startParent, false);
        transform.SetSiblingIndex(startSiblingIndex);
        rectTransform.anchoredPosition = startAnchoredPos;
    }

    /// <summary>Chamado pela zona que aceitou a carta.</summary>
    public void MarkConsumed()
    {
        consumed = true;
    }
}
