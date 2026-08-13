using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Área do editor de baralho que aceita cartas arrastadas — o deck de um lado,
/// a coleção do outro. Sem isto o arrasto ficava pela metade: a carta seguia o
/// cursor e voltava, porque nada implementava IDropHandler.
/// </summary>
public class DeckDropZone : MonoBehaviour, IDropHandler
{
    public enum Zone { Deck, Collection }

    public Zone zone = Zone.Deck;

    public void OnDrop(PointerEventData eventData)
    {
        var carta = CardInDeck.Dragging;
        if (carta == null || carta.cardData == null) return;

        var manager = DeckManager.Instance;
        if (manager == null) return;

        // Soltar na área de onde a carta já veio não faz nada.
        bool aceita = zone == Zone.Deck
            ? manager.TryAddCardToDeck(carta.cardData)
            : manager.TryRemoveCardFromDeck(carta.cardData);

        if (aceita)
            carta.MarkConsumed();
    }
}
