using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cemitério: onde a guilda encara o próprio custo.
///
/// A lista dos caídos já existia em <see cref="GuildManager.fallenHeroes"/> sem
/// nenhuma tela que a mostrasse — morrer era um nome somindo do roster. Aqui os
/// mortos ficam registrados, podem ser homenageados (o que devolve parte da
/// reputação perdida) e a vigília converte luto em alívio de estresse para quem
/// ficou, que é o único jeito de baixar estresse fora do vinho do Mercado.
/// </summary>
public class CemeteryManager : MonoBehaviour
{
    public static CemeteryManager Instance;

    [Header("UI References")]
    public TMP_Text summaryText;
    public Transform graveContainer;
    public TMP_Text emptyStateText;
    public TMP_Text feedbackText;

    [Header("Buttons")]
    public Button vigilButton;
    public Button closeButton;

    [Header("Preços")]
    public int tributeCost = 80;
    public int vigilCost = 120;

    [Header("Efeitos")]
    public int tributeReputation = 10;
    public float vigilStressRelief = 12f;

    // Quem já recebeu homenagem, por id de herói — homenagear duas vezes seria
    // apenas comprar reputação em loop.
    private readonly HashSet<string> honored = new HashSet<string>();

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
            closeButton.onClick.AddListener(() => UIManager.Instance?.CloseCemetery());

        if (vigilButton != null)
            vigilButton.onClick.AddListener(HoldVigil);
    }

    public void RefreshCemetery()
    {
        List<HeroData> fallen = Fallen();

        if (summaryText != null)
        {
            summaryText.text = fallen.Count == 0
                ? "Nenhum herói tombou até aqui."
                : $"⚰️ {fallen.Count} tumba(s) — {honored.Count} homenageada(s)"
                  + (fallen.Count > MaxVisibleGraves ? $"   <size=15>(as {MaxVisibleGraves} mais recentes)</size>" : "");
        }

        if (emptyStateText != null)
            emptyStateText.gameObject.SetActive(fallen.Count == 0);

        if (vigilButton != null)
        {
            TMP_Text label = vigilButton.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = $"VIGÍLIA - {vigilCost}💰";

            vigilButton.interactable = fallen.Count > 0
                && GuildManager.Instance != null
                && GuildManager.Instance.gold >= vigilCost
                && LivingRoster().Any(h => h.stress > 0f);
        }

        BuildGraves(fallen);
    }

    static List<HeroData> Fallen()
    {
        if (GuildManager.Instance == null) return new List<HeroData>();
        return GuildManager.Instance.fallenHeroes.Where(h => h != null).ToList();
    }

    static IEnumerable<HeroData> LivingRoster()
    {
        if (GuildManager.Instance == null) return Enumerable.Empty<HeroData>();
        return GuildManager.Instance.roster.Where(h => h != null && !h.isDead);
    }

    void SetFeedback(string message)
    {
        if (feedbackText != null)
            feedbackText.text = message;
    }

    #region Tumbas

    /// <summary>Quantas tumbas cabem na tela antes de a lista transbordar.</summary>
    const int MaxVisibleGraves = 8;

    void BuildGraves(List<HeroData> fallen)
    {
        if (graveContainer == null) return;

        UIUtil.ClearChildrenNow(graveContainer);

        // As mais recentes primeiro: são as que ainda doem e as que o jogador
        // reconhece. A lista não rola, então o excesso fica só na contagem.
        IEnumerable<HeroData> visiveis = Enumerable.Reverse(fallen).Take(MaxVisibleGraves);

        foreach (var hero in visiveis)
            BuildGraveRow(hero);
    }

    void BuildGraveRow(HeroData hero)
    {
        bool jaHomenageado = honored.Contains(hero.GetId());
        int gold = GuildManager.Instance != null ? GuildManager.Instance.gold : 0;

        var row = new GameObject(hero.heroName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        row.transform.SetParent(graveContainer, false);
        row.GetComponent<Image>().color = new Color(0.15f, 0.14f, 0.16f);

        var element = row.AddComponent<LayoutElement>();
        element.minHeight = 62;
        element.preferredHeight = 62;

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(row.transform, false);

        var label = labelGo.AddComponent<TextMeshProUGUI>();
        label.fontSize = 19;
        label.alignment = TextAlignmentOptions.TopLeft;
        label.raycastTarget = false;
        label.color = new Color(0.80f, 0.78f, 0.75f);
        label.text = $"🪦 {hero.heroName}  <size=14>{GetClassName(hero.heroClass)} Nv.{hero.level}</size>\n"
                   + $"<size=14><i>{Epitaph(hero)}</i></size>";

        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(12, 4);
        labelRect.offsetMax = new Vector2(-190, -4);

        var buttonGo = new GameObject("Btn_Tribute", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonGo.transform.SetParent(row.transform, false);
        buttonGo.GetComponent<Image>().color = new Color(0.20f, 0.17f, 0.16f);

        var buttonRect = buttonGo.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1, 0.5f);
        buttonRect.anchorMax = new Vector2(1, 0.5f);
        buttonRect.sizeDelta = new Vector2(160, 42);
        buttonRect.anchoredPosition = new Vector2(-95, 0);

        var buttonTextGo = new GameObject("Text", typeof(RectTransform));
        buttonTextGo.transform.SetParent(buttonGo.transform, false);

        var buttonText = buttonTextGo.AddComponent<TextMeshProUGUI>();
        buttonText.text = jaHomenageado ? "HOMENAGEADO" : $"HOMENAGEAR {tributeCost}💰";
        buttonText.fontSize = 14;
        buttonText.alignment = TextAlignmentOptions.Center;
        buttonText.color = new Color(0.94f, 0.88f, 0.72f);
        buttonText.raycastTarget = false;

        var buttonTextRect = buttonTextGo.GetComponent<RectTransform>();
        buttonTextRect.anchorMin = Vector2.zero;
        buttonTextRect.anchorMax = Vector2.one;
        buttonTextRect.offsetMin = Vector2.zero;
        buttonTextRect.offsetMax = Vector2.zero;

        var button = buttonGo.GetComponent<Button>();
        button.interactable = !jaHomenageado && gold >= tributeCost;

        HeroData capturado = hero;
        button.onClick.AddListener(() => PayTribute(capturado));
    }

    /// <summary>Uma linha sobre como o herói era, tirada do que o jogo já sabia dele.</summary>
    string Epitaph(HeroData hero)
    {
        if (hero.mentalState != MentalState.Normal && MentalStateUtil.IsAffliction(hero.mentalState))
            return $"Partiu {MentalStateUtil.GetLabel(hero.mentalState).ToLower()}.";

        if (MentalStateUtil.IsVirtue(hero.mentalState))
            return $"Manteve-se {MentalStateUtil.GetLabel(hero.mentalState).ToLower()} até o fim.";

        switch (hero.personality)
        {
            case Personality.Brave: return "Nunca recuou.";
            case Personality.Coward: return "Temia o escuro, e foi mesmo assim.";
            case Personality.Ambitious: return "Queria mais do que a estrada tinha.";
            case Personality.Loyal: return "Ficou até o último.";
            case Personality.Stubborn: return "Não ouviu ninguém.";
            case Personality.Selfish: return "Morreu com os bolsos cheios.";
            default: return "Serviu à guilda.";
        }
    }

    void PayTribute(HeroData hero)
    {
        if (honored.Contains(hero.GetId()))
        {
            SetFeedback($"{hero.heroName} já tem seu monumento.");
            return;
        }

        if (GuildManager.Instance == null || !GuildManager.Instance.SpendGold(tributeCost))
        {
            SetFeedback("Ouro insuficiente para a homenagem.");
            return;
        }

        honored.Add(hero.GetId());
        GuildManager.Instance.AddReputation(tributeReputation);

        SetFeedback($"{hero.heroName} recebe seu monumento. +{tributeReputation} de reputação.");
        RefreshCemetery();
    }

    #endregion

    /// <summary>A guilda inteira vela os mortos: caro, mas é alívio para todos de uma vez.</summary>
    void HoldVigil()
    {
        if (Fallen().Count == 0)
        {
            SetFeedback("Não há quem velar.");
            return;
        }

        if (GuildManager.Instance == null || !GuildManager.Instance.SpendGold(vigilCost))
        {
            SetFeedback("Ouro insuficiente para a vigília.");
            return;
        }

        int aliviados = 0;
        foreach (var hero in LivingRoster())
        {
            if (hero.stress <= 0f) continue;

            hero.stress = Mathf.Max(0f, hero.stress - vigilStressRelief);
            aliviados++;
        }

        SetFeedback(aliviados > 0
            ? $"A guilda velou os mortos. {aliviados} herói(s) respiram melhor."
            : "A vigília foi silenciosa. Ninguém carregava peso.");

        RefreshCemetery();
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
