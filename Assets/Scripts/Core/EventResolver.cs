using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Aplica as consequências de um EventOutcome sobre a party e a guilda.
/// Até então EventData.outcomes era preenchido pelo EventPool e nunca lido por ninguém:
/// esta classe é quem finalmente dá peso às escolhas do jogador.
/// </summary>
public static class EventResolver
{
    // Regras de risco no espírito de Darkest Dungeon
    private const float DeathsDoorBaseKillChance = 0.45f;
    private const float StressPerDamagePoint = 0.8f;
    private const float StressWitnessDeath = 25f;
    private const float StressWitnessDeathsDoor = 12f;
    private const float StressBreakpoint = 100f;
    private const float VirtueChance = 0.22f;

    /// <summary>Relatório do que aconteceu, para exibir ao jogador.</summary>
    public class Resolution
    {
        public List<string> lines = new List<string>();
        public List<HeroData> died = new List<HeroData>();
        public List<HeroData> newlyAfflicted = new List<HeroData>();
        public int goldChange;
        public int reputationChange;
        public int extraDays;

        public bool AnyoneDied => died.Count > 0;
        public string ToText() => string.Join("\n", lines);
    }

    /// <summary>
    /// Resolve uma escolha de evento.
    /// </summary>
    /// <param name="mitigation">0 a 1 — redução de dano/estresse obtida com cartas jogadas antes da escolha.</param>
    public static Resolution Resolve(EventOutcome outcome, List<HeroData> party, float mitigation = 0f)
    {
        Resolution result = new Resolution();

        if (outcome == null || party == null)
            return result;

        mitigation = Mathf.Clamp01(mitigation);
        result.extraDays = outcome.extraDays;

        if (outcome.extraDays > 0)
            result.lines.Add($"⏳ A jornada se estende por {outcome.extraDays} dia(s) a mais.");

        EventConsequences c = outcome.consequences;
        if (c != null)
        {
            ApplyGold(c, result);
            ApplyReputation(c, result);
            ApplyHeroEffects(c, party, mitigation, result);
            ApplyMoraleChanges(c, party, result);
        }

        if (outcome.triggersCorruption)
            ApplyCorruption(party, result);

        // O estresse só é convertido em Aflição/Virtude depois que tudo foi aplicado,
        // para que um único evento não dispare o mesmo herói duas vezes.
        ResolveStressBreakpoints(party, result);

        return result;
    }

    #region Ouro e reputação

    static void ApplyGold(EventConsequences c, Resolution result)
    {
        if (c.goldChange == 0) return;

        result.goldChange = c.goldChange;

        if (GuildManager.Instance != null)
        {
            if (c.goldChange > 0)
                GuildManager.Instance.AddGold(c.goldChange);
            else
                GuildManager.Instance.SpendGold(-c.goldChange);
        }

        result.lines.Add(c.goldChange > 0
            ? $"💰 +{c.goldChange} ouro."
            : $"💸 {c.goldChange} ouro.");
    }

    static void ApplyReputation(EventConsequences c, Resolution result)
    {
        if (c.reputationChange == 0) return;

        result.reputationChange = c.reputationChange;

        if (GuildManager.Instance != null)
            GuildManager.Instance.AddReputation(c.reputationChange);

        result.lines.Add(c.reputationChange > 0
            ? $"⭐ +{c.reputationChange} de reputação."
            : $"⚠️ {c.reputationChange} de reputação.");
    }

    #endregion

    #region Efeitos sobre heróis

    static void ApplyHeroEffects(EventConsequences c, List<HeroData> party, float mitigation, Resolution result)
    {
        if (c.heroEffects == null) return;

        foreach (var effect in c.heroEffects)
        {
            if (effect == null) continue;

            foreach (var hero in ResolveTargets(effect.heroName, party))
                ApplySingleEffect(effect, hero, party, mitigation, result);
        }
    }

    /// <summary>Traduz "All" / "Random" / nome próprio para os heróis vivos correspondentes.</summary>
    static List<HeroData> ResolveTargets(string heroName, List<HeroData> party)
    {
        List<HeroData> alive = party.Where(h => h != null && h.IsAlive).ToList();
        if (alive.Count == 0) return alive;

        if (string.IsNullOrEmpty(heroName) || heroName == "All")
            return alive;

        if (heroName == "Random")
            return new List<HeroData> { alive[Random.Range(0, alive.Count)] };

        var named = alive.Where(h => h.heroName == heroName).ToList();
        // Um nome que não bate com ninguém na party não deve virar dano silencioso em todos.
        return named;
    }

    static void ApplySingleEffect(HeroEffect effect, HeroData hero, List<HeroData> party, float mitigation, Resolution result)
    {
        if (effect.hpChange < 0)
        {
            int raw = Mathf.RoundToInt(-effect.hpChange * hero.GetDamageTakenMultiplier() * (1f - mitigation));
            int damage = Mathf.Max(0, raw);
            if (damage > 0)
                DealDamage(hero, damage, party, result);
        }
        else if (effect.hpChange > 0)
        {
            int healed = Mathf.Min(effect.hpChange, hero.maxHp - hero.currentHp);
            hero.currentHp += healed;
            if (healed > 0)
            {
                // Curar alguém em Death's Door o tira de lá.
                if (hero.isOnDeathsDoor && hero.currentHp > 0)
                {
                    hero.isOnDeathsDoor = false;
                    result.lines.Add($"✨ {hero.heroName} foi trazido de volta da beira da morte (+{healed} HP).");
                }
                else
                {
                    result.lines.Add($"❤️ {hero.heroName} recuperou {healed} HP.");
                }
            }
        }

        if (effect.addInjury && !hero.isInjured && hero.IsAlive)
        {
            hero.isInjured = true;
            AddStress(hero, 8f, result);
            result.lines.Add($"🩸 {hero.heroName} ficou ferido.");
        }

        if (effect.addTrait && hero.IsAlive)
            AddNegativeTrait(hero, result);
    }

    static void AddNegativeTrait(HeroData hero, Resolution result)
    {
        if (hero.trait != Trait.None && hero.trait != Trait.Lucky && hero.trait != Trait.FastHealer)
            return; // já carrega algo ruim

        Trait[] negatives = { Trait.Drunkard, Trait.Scarred, Trait.Cursed };
        hero.trait = negatives[Random.Range(0, negatives.Length)];
        result.lines.Add($"🖤 {hero.heroName} adquiriu o traço {GetTraitLabel(hero.trait)}.");
    }

    #endregion

    #region Dano, Death's Door e morte

    /// <summary>
    /// Aplica dano seguindo a regra de Death's Door: o HP para em 0 na primeira vez
    /// e só um golpe subsequente pode matar — e ainda assim por rolagem.
    /// </summary>
    public static void DealDamage(HeroData hero, int damage, List<HeroData> party, Resolution result)
    {
        if (hero == null || !hero.IsAlive || damage <= 0) return;

        bool wasOnDeathsDoor = hero.isOnDeathsDoor;

        hero.currentHp -= damage;
        AddStress(hero, damage * StressPerDamagePoint, result);

        if (hero.currentHp > 0)
        {
            result.lines.Add($"💥 {hero.heroName} sofreu {damage} de dano ({hero.currentHp}/{hero.maxHp}).");
            return;
        }

        hero.currentHp = 0;

        if (!wasOnDeathsDoor)
        {
            hero.isOnDeathsDoor = true;
            result.lines.Add($"☠️ {hero.heroName} está à BEIRA DA MORTE!");
            StressParty(party, hero, StressWitnessDeathsDoor, result);
            return;
        }

        // Já estava em Death's Door: rolagem de golpe fatal.
        float killChance = DeathsDoorBaseKillChance + (hero.stress / 100f) * 0.25f;
        if (hero.trait == Trait.Lucky) killChance -= 0.15f;
        if (hero.trait == Trait.Cursed) killChance += 0.15f;

        if (Random.value < Mathf.Clamp01(killChance))
            Kill(hero, party, result);
        else
            result.lines.Add($"🕯️ {hero.heroName} resistiu ao golpe fatal por pouco.");
    }

    static void Kill(HeroData hero, List<HeroData> party, Resolution result)
    {
        hero.isDead = true;
        hero.isOnDeathsDoor = false;
        hero.currentHp = 0;
        result.died.Add(hero);
        result.lines.Add($"⚰️ {hero.heroName} MORREU.");

        StressParty(party, hero, StressWitnessDeath, result);
    }

    #endregion

    #region Estresse e moral

    public static void AddStress(HeroData hero, float amount, Resolution result)
    {
        if (hero == null || !hero.IsAlive || amount == 0) return;

        if (amount > 0)
            amount *= hero.GetStressTakenMultiplier();

        hero.stress = Mathf.Clamp(hero.stress + amount, 0f, StressBreakpoint);

        // Estresse alto corrói a moral.
        if (amount > 0)
            hero.morale = Mathf.Clamp(hero.morale - amount * 0.3f, 0f, 100f);
    }

    /// <summary>Estressa os outros membros por testemunhar o que houve com um companheiro.</summary>
    static void StressParty(List<HeroData> party, HeroData victim, float amount, Resolution result)
    {
        if (party == null) return;

        foreach (var other in party)
        {
            if (other == null || other == victim || !other.IsAlive) continue;

            float amt = amount;
            if (other.personality == Personality.Loyal) amt *= 1.3f;   // sofre mais pelos aliados
            if (other.personality == Personality.Selfish) amt *= 0.6f;
            AddStress(other, amt, result);
        }
    }

    static void ApplyMoraleChanges(EventConsequences c, List<HeroData> party, Resolution result)
    {
        if (c.moraleChanges == null) return;

        foreach (var change in c.moraleChanges)
        {
            if (change == null || change.moraleChange == 0) continue;

            foreach (var hero in ResolveTargets(change.heroName, party))
            {
                hero.morale = Mathf.Clamp(hero.morale + change.moraleChange, 0f, 100f);
                // Moral e estresse andam em direções opostas.
                AddStress(hero, -change.moraleChange * 0.5f, result);
            }
        }

        if (c.moraleChanges.Length > 0)
        {
            int sample = c.moraleChanges[0].moraleChange;
            if (sample != 0)
                result.lines.Add(sample > 0 ? "🎵 O ânimo do grupo melhora." : "😞 O ânimo do grupo piora.");
        }
    }

    static void ApplyCorruption(List<HeroData> party, Resolution result)
    {
        foreach (var hero in party.Where(h => h != null && h.IsAlive))
        {
            hero.corruptionExposure = Mathf.Clamp(hero.corruptionExposure + Random.Range(5, 15), 0, 100);
            AddStress(hero, 10f, result);
        }
        result.lines.Add("🌑 A corrupção da região se infiltra no grupo.");
    }

    /// <summary>Converte estresse máximo em Aflição (comum) ou Virtude (rara).</summary>
    static void ResolveStressBreakpoints(List<HeroData> party, Resolution result)
    {
        foreach (var hero in party.Where(h => h != null && h.IsAlive))
        {
            if (hero.stress < StressBreakpoint) continue;
            if (hero.mentalState != MentalState.Normal) continue; // já colapsou nesta jornada

            if (Random.value < VirtueChance)
            {
                MentalState[] virtues =
                {
                    MentalState.Courageous, MentalState.Focused,
                    MentalState.Vigorous, MentalState.Stalwart
                };
                hero.mentalState = virtues[Random.Range(0, virtues.Length)];
                hero.stress = 60f; // o alívio da virtude
                hero.morale = Mathf.Min(100f, hero.morale + 25f);
                result.lines.Add($"🌟 {hero.heroName} encontrou forças: {MentalStateUtil.GetLabel(hero.mentalState)}!");
            }
            else
            {
                MentalState[] afflictions =
                {
                    MentalState.Paranoid, MentalState.Fearful,
                    MentalState.Hopeless, MentalState.Irrational, MentalState.Abusive
                };
                hero.mentalState = afflictions[Random.Range(0, afflictions.Length)];
                hero.morale = Mathf.Max(0f, hero.morale - 30f);
                result.newlyAfflicted.Add(hero);
                result.lines.Add($"🧠 {hero.heroName} sucumbiu ao estresse: {MentalStateUtil.GetLabel(hero.mentalState)}!");

                // Um herói agressivo arrasta o grupo junto.
                if (hero.mentalState == MentalState.Abusive)
                    StressParty(party, hero, 10f, result);
            }
        }
    }

    #endregion

    #region Helpers

    public static string GetTraitLabel(Trait trait)
    {
        switch (trait)
        {
            case Trait.Drunkard: return "Alcoólatra";
            case Trait.Lucky: return "Sortudo";
            case Trait.Scarred: return "Marcado";
            case Trait.FastHealer: return "Recuperação Rápida";
            case Trait.Cursed: return "Amaldiçoado";
            default: return "Nenhum";
        }
    }

    #endregion
}
