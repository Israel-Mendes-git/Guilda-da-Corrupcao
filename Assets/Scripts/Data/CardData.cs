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

/// <summary>
/// O que a carta faz fora do combate.
///
/// Mesma regra do <see cref="CombatEffectType"/>: acrescentar só no fim, porque
/// os assets guardam o número, não o nome.
/// </summary>
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
    ExtraRations,

    /// <summary>
    /// Traz alguém de volta da Beira da Morte e devolve fôlego.
    ///
    /// A carta Ressurgir prometia "revive um herói morto" e tinha efeito
    /// <c>None</c>: custava 4 de energia e não fazia nada. Reviver de verdade
    /// contradiz a morte permanente, que é pilar do jogo — o que ela faz é
    /// impedir que a morte aconteça.
    /// </summary>
    Revive
}

public static class JourneyEffectUtil
{
    /// <summary>
    /// Nome do efeito na língua do jogador. A tela de evento precisa dizer o que
    /// uma opção travada exige — "🔒 Precisa de: abrir caminho" só funciona se o
    /// efeito tiver nome de coisa, não de enum.
    /// </summary>
    public static string GetLabel(JourneyEffectType effect)
    {
        switch (effect)
        {
            case JourneyEffectType.RemoveObstacle: return "abrir caminho";
            case JourneyEffectType.HealInjury: return "tratar um ferimento";
            case JourneyEffectType.GainFood: return "conseguir comida";
            case JourneyEffectType.GainGold: return "arrancar lucro";
            case JourneyEffectType.RevealNextEvent: return "enxergar adiante";
            case JourneyEffectType.SkipDay: return "ganhar tempo";
            case JourneyEffectType.Intimidate: return "intimidar";
            case JourneyEffectType.Purify: return "purificar";
            case JourneyEffectType.Teleport: return "atravessar";
            case JourneyEffectType.ProtectFromWeather: return "abrigar o grupo";
            case JourneyEffectType.RestoreMorale: return "levantar o ânimo";
            case JourneyEffectType.ExtraRations: return "estocar mantimentos";
            case JourneyEffectType.Revive: return "trazer de volta";
            default: return "nada";
        }
    }
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