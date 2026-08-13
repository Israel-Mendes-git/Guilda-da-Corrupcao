using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HeroDetailPanel : MonoBehaviour
{
    private static HeroDetailPanel instance;

    /// <summary>
    /// O painel vive num objeto desativado, então o <c>Awake</c> dele só roda
    /// quando alguém o abre pela primeira vez — e até lá o singleton ficava nulo.
    /// Como quem chama é o retrato do herói (<see cref="PartyMemberCard"/>), com
    /// um <c>?.</c> na frente, o clique simplesmente não fazia nada e o painel
    /// era inalcançável no jogo inteiro. Procurar sob demanda resolve sem exigir
    /// que a cena mantenha o objeto ligado.
    /// </summary>
    public static HeroDetailPanel Instance
    {
        get
        {
            if (instance != null) return instance;

            // Inclui objetos inativos — é justamente o caso aqui.
            var found = Resources.FindObjectsOfTypeAll<HeroDetailPanel>();
            foreach (var candidate in found)
            {
                if (candidate == null || candidate.gameObject.scene.rootCount == 0) continue;
                instance = candidate;
                break;
            }

            return instance;
        }
        private set { instance = value; }
    }

    [Header("Panel References")]
    public GameObject panel;           // O GameObject da barra lateral
    public TMP_Text heroNameText;
    public TMP_Text heroClassText;
    public TMP_Text levelText;
    public TMP_Text hpText;
    public Image hpBar;
    public TMP_Text salaryText;
    public TMP_Text personalityText;
    public TMP_Text traitText;
    public TMP_Text loyaltyText;
    public TMP_Text moraleText;
    public Image portraitImage;

    [Header("Status Indicators")]
    public GameObject injuredIcon;
    public GameObject cursedIcon;

    [Header("Opcionais — se nulos, a informação entra no texto de moral")]
    public TMP_Text stressText;
    public TMP_Text mentalStateText;
    public TMP_Text equipmentText;

    [Header("Buttons")]
    public Button closeButton;
    public Button dismissButton;  // Opcional: botão para demitir herói

    private HeroData currentHero;
    private bool buttonsWired;

    /// <summary>Herói exibido agora, ou nulo com o painel fechado.</summary>
    public HeroData CurrentHero => currentHero;

    void Awake()
    {
        if (instance == null || instance == this)
            instance = this;
        else
            Destroy(gameObject);

        WireButtons();
    }

    /// <summary>
    /// Liga os botões uma vez só. Ficava no <c>Start</c>, que só roda no frame
    /// seguinte à ativação — e junto vinha um <c>HidePanel()</c> que fechava o
    /// painel logo depois de ele ter sido aberto pela primeira vez.
    /// </summary>
    void WireButtons()
    {
        if (buttonsWired) return;
        buttonsWired = true;

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(HidePanel);
            closeButton.onClick.AddListener(HidePanel);
        }

        if (dismissButton != null)
        {
            dismissButton.onClick.RemoveListener(DismissHero);
            dismissButton.onClick.AddListener(DismissHero);
        }
    }

    public void ShowHeroDetails(HeroData hero)
    {
        if (hero == null) return;

        WireButtons();

        currentHero = hero;
        if (panel != null) panel.SetActive(true);

        // Informações básicas
        if (heroNameText != null) heroNameText.text = hero.heroName;
        if (heroClassText != null) heroClassText.text = GetClassName(hero.heroClass);
        if (levelText != null) levelText.text = $"Nível: {hero.level}";

        // Vida
        if (hpText != null) hpText.text = $"{hero.currentHp} / {hero.maxHp}";
        if (hpBar != null) hpBar.fillAmount = (float)hero.currentHp / hero.maxHp;

        // Econômico
        if (salaryText != null) salaryText.text = $"💰 Salário: {hero.salary} ouro";

        // Personalidade e traços
        if (personalityText != null) personalityText.text = $"🎭 Personalidade: {GetPersonalityName(hero.personality)}";
        if (traitText != null) traitText.text = $"✨ Traço: {GetTraitName(hero.trait)}";

        // Lealdade e Moral
        if (loyaltyText != null) loyaltyText.text = $"🤝 Lealdade: {hero.loyalty}%";

        // Estresse e estado mental são o eixo do jogo, e a ficha não os mostrava.
        // Vão junto do moral quando a cena não tem campo próprio para eles, para
        // não depender de editar o painel à mão.
        if (stressText != null)
            stressText.text = DescreverEstresse(hero);
        else if (moraleText != null)
            moraleText.text = $"😊 Moral: {hero.morale}%   {DescreverEstresse(hero)}";

        if (moraleText != null && stressText != null)
            moraleText.text = $"😊 Moral: {hero.morale}%";

        if (equipmentText != null)
            equipmentText.text = $"⚔️ Arma {hero.weaponLevel}   🛡️ Armadura {hero.armorLevel}";

        // Status visuais
        if (injuredIcon != null) injuredIcon.SetActive(hero.isInjured);
        if (cursedIcon != null) cursedIcon.SetActive(hero.trait == Trait.Cursed);

        // Retrato (opcional)
        if (portraitImage != null && hero.portrait != null)
            portraitImage.sprite = hero.portrait;
    }

    /// <summary>Uma linha com o estresse e a aflição ou virtude em vigor.</summary>
    static string DescreverEstresse(HeroData hero)
    {
        string estado = MentalStateUtil.GetLabel(hero.mentalState);

        string cor = MentalStateUtil.IsVirtue(hero.mentalState) ? "#4A7A4A"
                   : MentalStateUtil.IsAffliction(hero.mentalState) ? "#B04040"
                   : hero.stress >= 70f ? "#D9B85A"
                   : "#C8C3B4";

        return $"<color={cor}>🧠 Estresse: {Mathf.RoundToInt(hero.stress)}/100 — {estado}</color>";
    }

    public void HidePanel()
    {
        if (panel != null) panel.SetActive(false);
        currentHero = null;
    }

    /// <summary>
    /// Dispensa o herói. Passa por confirmação: é irreversível, o deck dele vai
    /// junto, e o botão fica ao lado do de fechar.
    /// </summary>
    void DismissHero()
    {
        if (currentHero == null) return;

        HeroData alvo = currentHero;

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowConfirm(
                "Dispensar herói",
                $"{alvo.heroName} deixa a guilda para sempre, levando o baralho dele.\n\nConfirma?",
                () => ConfirmDismiss(alvo));
            return;
        }

        ConfirmDismiss(alvo);
    }

    void ConfirmDismiss(HeroData hero)
    {
        if (hero == null || GuildManager.Instance == null) return;

        GuildManager.Instance.RemoveHero(hero);
        DeckRepository.Remove(hero);

        UIManager.Instance?.ShowMessage($"{hero.heroName} deixou a guilda.", 2f);
        HidePanel();
    }

    string GetClassName(HeroClass heroClass)
    {
        switch (heroClass)
        {
            case HeroClass.Warrior: return "Guerreiro";
            case HeroClass.Mage: return "Mago";
            case HeroClass.Healer: return "Curandeiro";
            case HeroClass.Rogue: return "Ladino";
            case HeroClass.Bard: return "Bardo";
            case HeroClass.Hunter: return "Caçador";
            default: return "Desconhecido";
        }
    }

    string GetPersonalityName(Personality personality)
    {
        switch (personality)
        {
            case Personality.Brave: return "Corajoso";
            case Personality.Coward: return "Covarde";
            case Personality.Ambitious: return "Ambicioso";
            case Personality.Loyal: return "Leal";
            case Personality.Stubborn: return "Teimoso";
            case Personality.Selfish: return "Egoísta";
            default: return "Normal";
        }
    }

    string GetTraitName(Trait trait)
    {
        switch (trait)
        {
            case Trait.Drunkard: return "Bêbado";
            case Trait.Lucky: return "Sortudo";
            case Trait.Scarred: return "Cicatrizado";
            case Trait.FastHealer: return "Cura Rápida";
            case Trait.Cursed: return "Amaldiçoado";
            default: return "Nenhum";
        }
    }
}