using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Baralho, mão e descarte de uma sessão de cartas.
/// Não é singleton de propósito: a jornada e o combate mantêm baralhos
/// independentes, cada um com seu próprio CardManager no mesmo objeto.
/// </summary>
public class CardManager : MonoBehaviour
{
    [Header("Deck Atual")]
    public DeckData currentDeck;
    public List<CardData> hand = new List<CardData>();
    public List<CardData> drawPile = new List<CardData>();
    public List<CardData> discardPile = new List<CardData>();

    [Header("Config")]
    public int handSize = 5;
    public int maxHandSize = 7;

    [Header("Eventos")]
    public System.Action<CardData> onCardDrawn;
    public System.Action<CardData> onCardPlayed;
    public System.Action onDeckShuffled;

    public void InitializeDeck(DeckData deck)
    {
        if (deck == null)
        {
            Debug.LogError("CardManager: deck nulo em InitializeDeck.");
            return;
        }

        currentDeck = deck.Clone();
        drawPile = new List<CardData>(currentDeck.cards);
        hand.Clear();
        discardPile.Clear();

        ShuffleDrawPile();

        for (int i = 0; i < handSize && drawPile.Count > 0; i++)
        {
            DrawCard();
        }
    }

    public void ShuffleDrawPile()
    {
        for (int i = 0; i < drawPile.Count; i++)
        {
            CardData temp = drawPile[i];
            int randomIndex = Random.Range(i, drawPile.Count);
            drawPile[i] = drawPile[randomIndex];
            drawPile[randomIndex] = temp;
        }
        onDeckShuffled?.Invoke();
    }

    public bool DrawCard()
    {
        if (hand.Count >= maxHandSize) return false;

        if (drawPile.Count == 0)
        {
            if (discardPile.Count == 0) return false;

            drawPile = new List<CardData>(discardPile);
            discardPile.Clear();
            ShuffleDrawPile();
        }

        CardData drawnCard = drawPile[0];
        drawPile.RemoveAt(0);
        hand.Add(drawnCard);

        onCardDrawn?.Invoke(drawnCard);
        return true;
    }

    public void PlayCard(CardData card)
    {
        if (hand.Contains(card))
        {
            hand.Remove(card);
            discardPile.Add(card);
            onCardPlayed?.Invoke(card);
        }
    }

    public void ReturnCardToHand(CardData card)
    {
        if (discardPile.Contains(card))
        {
            discardPile.Remove(card);
            hand.Add(card);
        }
    }

    public List<CardData> GetCardsByClass(HeroClass heroClass)
    {
        if (currentDeck == null) return new List<CardData>();
        return currentDeck.cards.Where(c => c.requiredClass == heroClass).ToList();
    }

    /// <summary>Recicla o baralho inteiro (usado entre combates).</summary>
    public void ReshuffleDeck()
    {
        drawPile = new List<CardData>(discardPile);
        discardPile.Clear();
        hand.Clear();
        ShuffleDrawPile();

        for (int i = 0; i < handSize && drawPile.Count > 0; i++)
        {
            DrawCard();
        }
    }
}
