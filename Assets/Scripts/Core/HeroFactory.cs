using UnityEngine;

public static class HeroFactory
{
    static string[] firstNames = { "Gromm", "Lyra", "Finn", "Elara", "Thorn", "Mira", "Kael", "Sera", "Bjorn", "Luna" };
    static string[] lastNames = { "Ferro", "Stella", "Vento", "Pedra", "Sombra", "Luz", "Gelo", "Fogo" };

    /// <summary>
    /// HP máximo por classe e nível. Vive aqui porque a criação do herói e a
    /// subida de nível precisam da mesma conta — duas cópias divergiriam na
    /// primeira vez que alguém ajustasse o valor de uma classe.
    /// </summary>
    public static int MaxHpFor(HeroClass heroClass, int level)
    {
        int bonusDaClasse = heroClass == HeroClass.Warrior ? 10
                          : heroClass == HeroClass.Mage ? -5
                          : 0;
        return 20 + (level * 4) + bonusDaClasse;
    }

    /// <summary>Custo de recrutamento e referência de valor do herói.</summary>
    public static int SalaryFor(int level) => 20 + (level * 10);

    public static HeroData CreateHero(string name, HeroClass heroClass, int level)
    {
        HeroData hero = ScriptableObject.CreateInstance<HeroData>();

        hero.heroName = name;
        hero.heroClass = heroClass;
        hero.level = level;
        hero.maxHp = MaxHpFor(heroClass, level);
        hero.currentHp = hero.maxHp;
        hero.salary = SalaryFor(level);
        hero.personality = (Personality)Random.Range(0, System.Enum.GetValues(typeof(Personality)).Length);
        hero.trait = (Trait)Random.Range(0, System.Enum.GetValues(typeof(Trait)).Length);
        hero.loyalty = Random.Range(40, 85);
        hero.morale = Random.Range(50, 95);
        hero.isInjured = false;
        hero.isDead = false;
        hero.corruptionExposure = 0;
        hero.ResetXpParaNivel();

        // O rosto sai do nome, não do acaso: assim o mesmo herói mantém a cara
        // dele entre execuções, e é o elenco que o jogador aprende a reconhecer
        // ao longo das jornadas. Sem catálogo, o campo fica nulo e a ficha
        // continua funcionando como antes.
        if (PortraitCatalog.Instance != null)
            hero.portrait = PortraitCatalog.Instance.Para(name);

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
