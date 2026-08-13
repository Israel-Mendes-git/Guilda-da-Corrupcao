using UnityEngine;

public static class HeroFactory
{
    static string[] firstNames = { "Gromm", "Lyra", "Finn", "Elara", "Thorn", "Mira", "Kael", "Sera", "Bjorn", "Luna" };
    static string[] lastNames = { "Ferro", "Stella", "Vento", "Pedra", "Sombra", "Luz", "Gelo", "Fogo" };
    static string[] traits = { "Sortudo", "Valente", "Teimoso", "Rápido", "Fortão", "Ágil" };

    public static HeroData CreateHero(string name, HeroClass heroClass, int level)
    {
        HeroData hero = ScriptableObject.CreateInstance<HeroData>();

        hero.heroName = name;
        hero.heroClass = heroClass;
        hero.level = level;
        hero.maxHp = 20 + (level * 4) + (heroClass == HeroClass.Warrior ? 10 : heroClass == HeroClass.Mage ? -5 : 0);
        hero.currentHp = hero.maxHp;
        hero.salary = 20 + (level * 10);
        hero.personality = (Personality)Random.Range(0, System.Enum.GetValues(typeof(Personality)).Length);
        hero.trait = (Trait)Random.Range(0, System.Enum.GetValues(typeof(Trait)).Length);
        hero.loyalty = Random.Range(40, 85);
        hero.morale = Random.Range(50, 95);
        hero.isInjured = false;
        hero.isDead = false;
        hero.corruptionExposure = 0;

        return hero;
    }

    public static HeroData CreateRandomHero(int minLevel, int maxLevel)
    {
        int level = Random.Range(minLevel, maxLevel + 1);
        HeroClass randomClass = (HeroClass)Random.Range(0, System.Enum.GetValues(typeof(HeroClass)).Length);
        string fullName = firstNames[Random.Range(0, firstNames.Length)] + " " + lastNames[Random.Range(0, lastNames.Length)];

        return CreateHero(fullName, randomClass, level);
    }
}