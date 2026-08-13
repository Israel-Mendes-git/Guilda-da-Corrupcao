using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Forja: melhora o equipamento de cada herói.
///
/// São dois eixos, e cada um conversa com um sistema que já existe. A armadura
/// aumenta o HP máximo, o que importa mais para quem fica na linha de frente. A
/// arma soma dano às cartas que aquele herói empresta ao baralho — o combate sabe
/// de quem é cada carta por causa do <see cref="CardOwnership"/>, então melhorar
/// a arma do mago fortalece exatamente as magias dele, e não o baralho inteiro.
/// </summary>
public class ForgeManager : MonoBehaviour
{
    public static ForgeManager Instance;

    [Header("UI References")]
    public TMP_Text goldText;
    public TMP_Text hintText;
    public Transform heroContainer;
    public TMP_Text feedbackText;

    [Header("Buttons")]
    public Button closeButton;

    [Header("Preços")]
    public int weaponBaseCost = 120;
    public int armorBaseCost = 100;

    [Header("Limites e efeitos")]
    public int maxUpgradeLevel = 3;
    public int hpPerArmorLevel = 4;
    public int damagePerWeaponLevel = 1;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(() => UIManager.Instance?.CloseForge());
    }

    /// <summary>Dano extra que as cartas emprestadas por este herói carregam.</summary>
    public static int WeaponBonus(HeroData hero)
    {
        if (hero == null) return 0;

        int perLevel = Instance != null ? Instance.damagePerWeaponLevel : 1;
        return hero.weaponLevel * perLevel;
    }

    public int WeaponCost(HeroData hero) => weaponBaseCost * (hero.weaponLevel + 1);
    public int ArmorCost(HeroData hero) => armorBaseCost * (hero.armorLevel + 1);

    public void RefreshForge()
    {
        int gold = GuildManager.Instance != null ? GuildManager.Instance.gold : 0;

        if (goldText != null)
            goldText.text = $"💰 {gold}";

        if (hintText != null)
            hintText.text = $"Arma: +{damagePerWeaponLevel} de dano nas cartas do herói.   "
                          + $"Armadura: +{hpPerArmorLevel} de vida máxima.   Limite: nível {maxUpgradeLevel}.";

        BuildHeroes();
    }

    void SetFeedback(string message)
    {
        if (feedbackText != null)
            feedbackText.text = message;
    }

    void BuildHeroes()
    {
        if (heroContainer == null) return;

        UIUtil.ClearChildrenNow(heroContainer);

        if (GuildManager.Instance == null) return;

        foreach (var hero in GuildManager.Instance.roster.Where(h => h != null && !h.isDead))
            BuildHeroRow(hero);
    }

    void BuildHeroRow(HeroData hero)
    {
        var row = new GameObject(hero.heroName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        row.transform.SetParent(heroContainer, false);
        row.GetComponent<Image>().color = new Color(0.17f, 0.15f, 0.14f);

        var element = row.AddComponent<LayoutElement>();
        element.minHeight = 68;
        element.preferredHeight = 68;

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(row.transform, false);

        var label = labelGo.AddComponent<TextMeshProUGUI>();
        label.fontSize = 19;
        label.alignment = TextAlignmentOptions.TopLeft;
        label.raycastTarget = false;
        label.color = new Color(0.92f, 0.90f, 0.85f);
        label.text = $"{PartyFormation.PreferenceIcon(hero.heroClass)} {hero.heroName}  "
                   + $"<size=14>{GetClassName(hero.heroClass)} Nv.{hero.level}</size>\n"
                   + $"<size=14>⚔️ arma {hero.weaponLevel}/{maxUpgradeLevel}   "
                   + $"🛡️ armadura {hero.armorLevel}/{maxUpgradeLevel}   ❤️ {hero.currentHp}/{hero.maxHp}</size>";

        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(12, 4);
        labelRect.offsetMax = new Vector2(-330, -4);

        HeroData capturado = hero;

        Button weapon = BuildUpgradeButton(row.transform, "Btn_Weapon",
            hero.weaponLevel >= maxUpgradeLevel ? "⚔️ MÁXIMA" : $"⚔️ ARMA {WeaponCost(hero)}💰", -170);
        weapon.interactable = CanAfford(WeaponCost(hero)) && hero.weaponLevel < maxUpgradeLevel;
        weapon.onClick.AddListener(() => UpgradeWeapon(capturado));

        Button armor = BuildUpgradeButton(row.transform, "Btn_Armor",
            hero.armorLevel >= maxUpgradeLevel ? "🛡️ MÁXIMA" : $"🛡️ ARMADURA {ArmorCost(hero)}💰", -12);
        armor.interactable = CanAfford(ArmorCost(hero)) && hero.armorLevel < maxUpgradeLevel;
        armor.onClick.AddListener(() => UpgradeArmor(capturado));
    }

    static bool CanAfford(int cost)
    {
        return GuildManager.Instance != null && GuildManager.Instance.gold >= cost;
    }

    Button BuildUpgradeButton(Transform parent, string name, string label, float right)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = new Color(0.22f, 0.18f, 0.15f);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1, 0.5f);
        rect.anchorMax = new Vector2(1, 0.5f);
        rect.sizeDelta = new Vector2(150, 46);
        rect.anchoredPosition = new Vector2(right - 75, 0);

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);

        var text = textGo.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 14;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0.94f, 0.88f, 0.72f);
        text.raycastTarget = false;

        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        return go.GetComponent<Button>();
    }

    void UpgradeWeapon(HeroData hero)
    {
        if (hero.weaponLevel >= maxUpgradeLevel)
        {
            SetFeedback($"A arma de {hero.heroName} já é a melhor que esta forja faz.");
            return;
        }

        int cost = WeaponCost(hero);
        if (GuildManager.Instance == null || !GuildManager.Instance.SpendGold(cost))
        {
            SetFeedback("Ouro insuficiente.");
            return;
        }

        hero.weaponLevel++;
        SetFeedback($"A arma de {hero.heroName} vai ao nível {hero.weaponLevel}: "
                  + $"+{WeaponBonus(hero)} de dano nas cartas dele.");
        RefreshForge();
    }

    void UpgradeArmor(HeroData hero)
    {
        if (hero.armorLevel >= maxUpgradeLevel)
        {
            SetFeedback($"A armadura de {hero.heroName} já é a melhor que esta forja faz.");
            return;
        }

        int cost = ArmorCost(hero);
        if (GuildManager.Instance == null || !GuildManager.Instance.SpendGold(cost))
        {
            SetFeedback("Ouro insuficiente.");
            return;
        }

        hero.armorLevel++;

        // A vida ganha vem cheia: pagar por armadura e continuar ferido soaria
        // como se a compra não tivesse acontecido.
        hero.maxHp += hpPerArmorLevel;
        hero.currentHp += hpPerArmorLevel;

        SetFeedback($"A armadura de {hero.heroName} vai ao nível {hero.armorLevel}: {hero.maxHp} de vida máxima.");
        RefreshForge();
    }

    static string GetClassName(HeroClass heroClass)
    {
        switch (heroClass)
        {
            case HeroClass.Warrior: return "Guerreiro";
            case HeroClass.Mage: return "Mago";
            case HeroClass.Healer: return "Curandeiro";
            case HeroClass.Rogue: return "Ladino";
            case HeroClass.Bard: return "Bardo";
            case HeroClass.Hunter: return "Caçador";
            default: return heroClass.ToString();
        }
    }
}
