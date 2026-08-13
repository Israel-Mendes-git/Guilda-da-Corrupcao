using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PartyMemberCard : MonoBehaviour
{
    private HeroData linkedHero;
    private Button button;

    [Header("UI References")]
    public TMP_Text nameText;
    public TMP_Text hpText;
    public Image hpBar;
    public GameObject injuredIcon;

    void Awake()
    {
        button = GetComponent<Button>();
        if (button != null)
            button.onClick.AddListener(OnCardClick);
    }

    public void Initialize(HeroData hero)
    {
        linkedHero = hero;

        if (nameText != null) nameText.text = hero.heroName;
        UpdateHealthDisplay();

        if (injuredIcon != null) injuredIcon.SetActive(hero.isInjured);
    }

    public void UpdateHealthDisplay()
    {
        if (linkedHero == null) return;

        if (hpText != null) hpText.text = $"❤️ {linkedHero.currentHp}";
        if (hpBar != null) hpBar.fillAmount = (float)linkedHero.currentHp / linkedHero.maxHp;
    }

    void OnCardClick()
    {
        if (linkedHero == null) return;

        // Pelo UIManager, não direto pelo painel: é ele quem ativa o objeto e o
        // traz para a frente. Chamando o painel direto, um objeto desativado
        // recebia os dados e continuava invisível.
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowHeroDetail(linkedHero);
            return;
        }

        HeroDetailPanel.Instance?.ShowHeroDetails(linkedHero);
    }
}