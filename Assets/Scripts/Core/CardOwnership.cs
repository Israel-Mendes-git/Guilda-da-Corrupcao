using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Quem emprestou cada carta do baralho da jornada.
///
/// Sem isto uma carta é apenas um efeito solto, e a formação não teria como
/// afetar "quais cartas funcionam": é a posição do herói que trouxe a carta que
/// decide se ela sai com força total. Uma mesma carta pode ter mais de um dono —
/// dois heróis da mesma classe emprestam a mesma —, e nesse caso basta que um
/// deles esteja bem posicionado.
/// </summary>
public class CardOwnership
{
    private readonly Dictionary<CardData, List<HeroData>> owners
        = new Dictionary<CardData, List<HeroData>>();

    public void Register(CardData card, HeroData hero)
    {
        if (card == null || hero == null) return;

        List<HeroData> list;
        if (!owners.TryGetValue(card, out list))
        {
            list = new List<HeroData>();
            owners[card] = list;
        }

        if (!list.Contains(hero))
            list.Add(hero);
    }

    public void RegisterAll(IEnumerable<CardData> cards, HeroData hero)
    {
        if (cards == null) return;

        foreach (var card in cards)
            Register(card, hero);
    }

    public IReadOnlyList<HeroData> GetOwners(CardData card)
    {
        List<HeroData> list;
        return card != null && owners.TryGetValue(card, out list)
            ? list
            : (IReadOnlyList<HeroData>)System.Array.Empty<HeroData>();
    }

    /// <summary>
    /// Dono que melhor sustenta a carta neste momento: o primeiro vivo e bem
    /// posicionado; na falta dele, o primeiro vivo. Nulo se nenhum dono sobreviveu.
    /// </summary>
    public HeroData BestOwner(CardData card, IList<HeroData> party)
    {
        var alive = GetOwners(card).Where(h => h != null && h.IsAlive).ToList();
        if (alive.Count == 0) return null;

        return alive.FirstOrDefault(h => PartyFormation.IsWellPlaced(h, party)) ?? alive[0];
    }

    /// <summary>
    /// Potência da carta conforme a posição de quem a emprestou. Cartas sem dono
    /// conhecido saem inteiras — é o caso de um combate iniciado fora da jornada.
    /// </summary>
    public float PowerMultiplier(CardData card, IList<HeroData> party)
    {
        HeroData owner = BestOwner(card, party);
        return owner == null ? 1f : PartyFormation.PowerMultiplier(owner, party);
    }
}
