using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class PartyDisplayManager : MonoBehaviour
{
    [Header("UI")]
    public Transform partyContainer;
    public GameObject partyMemberPrefab;
    public TMP_Text partyCountText;

    private List<PartyMemberCard> currentCards = new List<PartyMemberCard>();

    void Start()
    {
        if (GuildManager.Instance != null)
        {
            GuildManager.Instance.onRosterChanged += RefreshPartyDisplay;
        }

        RefreshPartyDisplay();
    }

    void OnDestroy()
    {
        if (GuildManager.Instance != null)
        {
            GuildManager.Instance.onRosterChanged -= RefreshPartyDisplay;
        }
    }

    void RefreshPartyDisplay()
    {
        foreach (var card in currentCards)
        {
            if (card != null)
                Destroy(card.gameObject);
        }
        currentCards.Clear();

        foreach (var hero in GuildManager.Instance.roster)
        {
            GameObject cardObj = Instantiate(partyMemberPrefab, partyContainer);
            PartyMemberCard card = cardObj.GetComponent<PartyMemberCard>();

            if (card != null)
            {
                card.Initialize(hero);
                currentCards.Add(card);
            }
        }

        if (partyCountText != null)
        {
            partyCountText.text = $"{GuildManager.Instance.roster.Count}/{GuildManager.Instance.maxRosterSize}";
        }
    }

    // Método para atualizar a saúde de um herói específico (chamado após jornada)
    public void UpdateHeroHealth(HeroData hero)
    {
        foreach (var card in currentCards)
        {
            // Se você tiver acesso ao herói no card, pode comparar
            // Por enquanto, recria tudo
        }
        RefreshPartyDisplay();
    }
}