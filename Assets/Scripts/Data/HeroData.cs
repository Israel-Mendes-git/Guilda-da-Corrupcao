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

    [Header("Experiência")]
    public int xp;                    // acumulado dentro do nível atual
    public int xpToNextLevel;         // meta do nível atual; 0 = ainda não inicializado

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

    #region Aptidão para a estrada

    /// <summary>
    /// Estresse a partir do qual o herói se recusa a partir de novo.
    ///
    /// Alto de propósito: só chega aqui quem voltou perto de quebrar. É o que
    /// dá função ao vinho do Mercado e à vigília do Cemitério — sem isso, o
    /// estresse subia, virava aflição e nada obrigava o jogador a cuidar dele.
    /// </summary>
    public const float StressLimiteParaViajar = 85f;

    /// <summary>Pode entrar numa nova jornada?</summary>
    public bool IsFitForJourney => IsAlive && stress < StressLimiteParaViajar;

    /// <summary>Por que não pode partir — vazio quando está apto.</summary>
    public string UnfitReason
    {
        get
        {
            if (isDead) return "morto";
            if (stress >= StressLimiteParaViajar)
                return $"esgotado ({Mathf.RoundToInt(stress)}/100 de estresse)";
            return "";
        }
    }

    #endregion

    #region Experiência e nível

    /// <summary>Quanto custa sair do nível informado. Cresce de forma linear, não explosiva:
    /// um herói veterano deve continuar valendo a pena levar, não virar projeto de vida.</summary>
    public static int XpNecessarioPara(int nivel) => 100 + Mathf.Max(0, nivel - 1) * 75;

    /// <summary>Zera o progresso dentro do nível atual e refaz a meta.</summary>
    public void ResetXpParaNivel()
    {
        xp = 0;
        xpToNextLevel = XpNecessarioPara(level);
    }

    /// <summary>Meta do nível atual, tolerando heróis gravados antes de existir XP.</summary>
    public int XpMetaAtual => xpToNextLevel > 0 ? xpToNextLevel : XpNecessarioPara(level);

    /// <summary>Fração 0–1 do caminho até o próximo nível, para barras de UI.</summary>
    public float XpProgress => Mathf.Clamp01(xp / (float)Mathf.Max(1, XpMetaAtual));

    /// <summary>
    /// Credita experiência e sobe de nível quantas vezes forem necessárias.
    /// Devolve quantos níveis subiu — o chamador usa isso para avisar o jogador.
    ///
    /// Subir de nível recalcula o HP máximo pela mesma fórmula da criação e
    /// entrega o ganho como cura: o herói volta da jornada mais forte, não com
    /// uma barra maior e igualmente vazia.
    /// </summary>
    public int AddXp(int amount)
    {
        if (amount <= 0 || isDead) return 0;

        // Heróis de saves antigos e assets criados à mão chegam com a meta zerada.
        if (xpToNextLevel <= 0) xpToNextLevel = XpNecessarioPara(level);

        xp += amount;

        int niveisGanhos = 0;
        while (xp >= xpToNextLevel && niveisGanhos < MaxLevelUpsPorVez)
        {
            xp -= xpToNextLevel;
            level++;
            niveisGanhos++;

            int novoMax = HeroFactory.MaxHpFor(heroClass, level);
            int ganho = Mathf.Max(0, novoMax - maxHp);
            maxHp = novoMax;
            currentHp = Mathf.Min(maxHp, currentHp + ganho);

            salary = HeroFactory.SalaryFor(level);
            xpToNextLevel = XpNecessarioPara(level);
        }

        return niveisGanhos;
    }

    /// <summary>Teto de segurança: uma recompensa mal configurada não deve
    /// transformar um recruta em lenda numa jornada só.</summary>
    private const int MaxLevelUpsPorVez = 3;

    #endregion

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
