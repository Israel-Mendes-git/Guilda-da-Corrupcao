using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Deck", menuName = "Game/Deck")]
public class DeckData : ScriptableObject
{
    public string deckName;
    public HeroData owner;
    public List<CardData> cards;
    public int maxDeckSize = 12;

    public void AddCard(CardData card)
    {
        if (cards.Count < maxDeckSize && !cards.Contains(card))
            cards.Add(card);
    }

    public void RemoveCard(CardData card)
    {
        cards.Remove(card);
    }

    public DeckData Clone()
    {
        DeckData clone = CreateInstance<DeckData>();
        clone.deckName = deckName;
        clone.owner = owner;
        clone.cards = new List<CardData>(cards);
        clone.maxDeckSize = maxDeckSize;
        return clone;
    }
}