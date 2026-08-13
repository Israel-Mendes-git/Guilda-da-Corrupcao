using UnityEngine;

[CreateAssetMenu(fileName = "New Card", menuName = "Game/Card")]
public class CardData : ScriptableObject
{
    [Header("Informações Básicas")]
    public string cardName;
    public string cardDescription;
    public Sprite cardImage;
    public CardRarity rarity;
    public HeroClass requiredClass;
    public int energyCost;

    [Header("Efeito de Jornada")]
    public JourneyEffectType journeyEffect;
    public int journeyEffectValue;
    [TextArea(1, 2)] public string journeyEffectDescription;

    [Header("Efeito de Combate")]
    public CombatEffectType combatEffect;
    public int combatDamage;
    public int combatBlock;
    public int combatHeal;
    public int combatDuration;
    [TextArea(1, 2)] public string combatEffectDescription;

    [Header("Visual")]
    public Color cardColor = Color.white;

    // Método auxiliar para pegar descrição correta baseada no modo
    public string GetDescription(bool isJourneyMode)
    {
        return isJourneyMode ? journeyEffectDescription : combatEffectDescription;
    }
}

public enum CardRarity
{
    Common = 0,
    Rare = 1,
    Epic = 2,
    Legendary = 3
}

public enum JourneyEffectType
{
    None,
    RemoveObstacle,
    HealInjury,
    GainFood,
    GainGold,
    RevealNextEvent,
    SkipDay,
    Intimidate,
    Purify,
    Teleport,
    ProtectFromWeather,
    RestoreMorale,
    ExtraRations
}

/// <summary>
/// O que a carta faz em combate.
///
/// Os valores são gravados como número nos assets: acrescentar só no fim, nunca
/// no meio, senão toda carta já configurada muda de efeito.
/// </summary>
public enum CombatEffectType
{
    None,
    Damage,
    DamageAll,
    Block,
    BlockAll,
    Heal,
    HealAll,
    Debuff,
    Buff,            // dano extra no grupo enquanto durar
    DrawCards,
    GainEnergy,
    Poison,
    ShieldBreak,
    BuffNextCard,    // a próxima carta de dano sai mais forte
    Evade,           // o herói ignora o próximo golpe
    Cleanse          // tira a aflição e alivia o estresse
}