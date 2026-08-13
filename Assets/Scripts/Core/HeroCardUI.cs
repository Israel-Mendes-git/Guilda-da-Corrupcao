using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class HeroCardUI : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text nameText;
    public TMP_Text classText;
    public TMP_Text levelText;
    public TMP_Text hpText;
    public TMP_Text salaryText;
    public TMP_Text personalityText;
    public TMP_Text traitText;
    public Image portraitImage;
    public Button recruitButton;

    private HeroData currentHero;

    public void Initialize(HeroData hero, System.Action<HeroData> onRecruit)
    {
        currentHero = hero;

        // Preenche dados
        if (nameText != null) nameText.text = hero.heroName;
        if (classText != null) classText.text = GetClassDisplayName(hero.heroClass);
        if (levelText != null) levelText.text = $"Nv.{hero.level}";
        if (hpText != null) hpText.text = $"❤️ {hero.currentHp}/{hero.maxHp}";
        if (salaryText != null) salaryText.text = $"💰 {hero.salary}";
        if (personalityText != null) personalityText.text = GetPersonalityDisplay(hero.personality);
        if (traitText != null) traitText.text = GetTraitDisplay(hero.trait);

        // Configura botão
        Button btn = recruitButton != null ? recruitButton : GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => onRecruit?.Invoke(hero));
        }

        // Verifica se pode recrutar (equipe cheia)
        if (btn != null && GuildManager.Instance != null)
        {
            btn.interactable = GuildManager.Instance.CanRecruit();
        }
    }

    string GetClassDisplayName(HeroClass heroClass)
    {
        switch (heroClass)
        {
            case HeroClass.Warrior: return "⚔️ Guerreiro";
            case HeroClass.Mage: return "🔮 Mago";
            case HeroClass.Healer: return "⚕️ Curandeiro";
            case HeroClass.Rogue: return "🗡️ Ladino";
            case HeroClass.Bard: return "🎵 Bardo";
            case HeroClass.Hunter: return "🏹 Caçador";
            default: return "❓";
        }
    }

    string GetPersonalityDisplay(Personality personality)
    {
        switch (personality)
        {
            case Personality.Brave: return "🦁 Corajoso";
            case Personality.Coward: return "🐔 Covarde";
            case Personality.Ambitious: return "⭐ Ambicioso";
            case Personality.Loyal: return "🤝 Leal";
            case Personality.Stubborn: return "🪨 Teimoso";
            case Personality.Selfish: return "👑 Egoísta";
            default: return "❓";
        }
    }

    string GetTraitDisplay(Trait trait)
    {
        switch (trait)
        {
            case Trait.Drunkard: return "🍺 Bêbado";
            case Trait.Lucky: return "🍀 Sortudo";
            case Trait.Scarred: return "⚡ Cicatrizado";
            case Trait.FastHealer: return "💚 Cura Rápida";
            case Trait.Cursed: return "💀 Amaldiçoado";
            default: return "";
        }
    }
}