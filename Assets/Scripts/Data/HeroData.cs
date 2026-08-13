using UnityEngine;

[CreateAssetMenu(fileName = "New Hero", menuName = "Guild/Hero")]
public class HeroData : ScriptableObject
{
    public string heroName;
    public HeroClass heroClass;
    public Sprite portrait;
    public int level;
    public int maxHp;
    public int currentHp;
    public int salary;
    public Personality personality;
    public Trait trait;
    public float loyalty; // 0 a 100
    public float morale;   // 0 a 100

    // Status que persistem entre missões
    public bool isInjured;      // -20% em eventos
    public bool isDead;
    public int corruptionExposure; // 0 a 100, afeta eventos

    [Header("Estresse")]
    public float stress;              // 0 a 100. Ao estourar, vira Aflição ou Virtude
    public MentalState mentalState = MentalState.Normal;
    public bool isOnDeathsDoor;       // HP zerado: o próximo golpe pode ser fatal

    [Header("Equipamento (Forja)")]
    public int weaponLevel;           // +1 de dano nas cartas deste herói por nível
    public int armorLevel;            // +4 de HP máximo por nível

    // Identidade estável para salvar deck/progresso. heroName não serve: a HeroFactory
    // sorteia de listas curtas e dois heróis podem acabar com o mesmo nome.
    [SerializeField] private string heroId;

    public string GetId()
    {
        if (string.IsNullOrEmpty(heroId))
            heroId = System.Guid.NewGuid().ToString("N");
        return heroId;
    }

    public bool IsAlive => !isDead;

    /// <summary>Multiplicador de dano recebido conforme ferimento e estado mental.</summary>
    public float GetDamageTakenMultiplier()
    {
        float mult = 1f;
        if (isInjured) mult += 0.25f;
        if (mentalState == MentalState.Hopeless) mult += 0.20f;
        if (mentalState == MentalState.Stalwart) mult -= 0.20f;
        return Mathf.Max(0.1f, mult);
    }

    /// <summary>Multiplicador de estresse recebido. Covardes sofrem mais, valentes menos.</summary>
    public float GetStressTakenMultiplier()
    {
        float mult = 1f;
        if (personality == Personality.Coward) mult += 0.35f;
        if (personality == Personality.Brave) mult -= 0.25f;
        if (trait == Trait.Cursed) mult += 0.25f;
        if (trait == Trait.Lucky) mult -= 0.15f;
        if (MentalStateUtil.IsVirtue(mentalState)) mult -= 0.30f;
        else if (mentalState != MentalState.Normal) mult += 0.25f;
        return Mathf.Max(0.1f, mult);
    }
}

public enum HeroClass { Warrior, Mage, Healer, Rogue, Bard, Hunter }
public enum Personality { Brave, Coward, Ambitious, Loyal, Stubborn, Selfish }
public enum Trait { None, Drunkard, Lucky, Scarred, FastHealer, Cursed }

/// <summary>
/// Estado mental do herói. Definido quando o estresse chega a 100:
/// normalmente vira uma Aflição, raramente uma Virtude.
/// </summary>
public enum MentalState
{
    Normal = 0,

    // Aflições
    Paranoid = 1,     // recusa ajuda, perde moral extra
    Fearful = 2,      // foge de confrontos
    Hopeless = 3,     // recebe mais dano
    Irrational = 4,   // ações imprevisíveis
    Abusive = 5,      // estressa os aliados

    // Virtudes
    Courageous = 10,  // reduz estresse do grupo
    Focused = 11,     // energia extra
    Vigorous = 12,    // cura ao longo da jornada
    Stalwart = 13     // recebe menos dano
}

public static class MentalStateUtil
{
    public static bool IsVirtue(MentalState state) => (int)state >= 10;

    public static bool IsAffliction(MentalState state) =>
        state != MentalState.Normal && !IsVirtue(state);

    public static string GetLabel(MentalState state)
    {
        switch (state)
        {
            case MentalState.Paranoid: return "Paranoico";
            case MentalState.Fearful: return "Amedrontado";
            case MentalState.Hopeless: return "Desesperançado";
            case MentalState.Irrational: return "Irracional";
            case MentalState.Abusive: return "Agressivo";
            case MentalState.Courageous: return "Corajoso";
            case MentalState.Focused: return "Focado";
            case MentalState.Vigorous: return "Vigoroso";
            case MentalState.Stalwart: return "Inabalável";
            default: return "Estável";
        }
    }
}
