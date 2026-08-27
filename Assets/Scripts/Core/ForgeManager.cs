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
///
/// <b>Por que a sala deixou de ser uma lista.</b> A versão anterior era uma linha
/// por herói com dois botões de compra: pagar 120 de ouro mudava <c>arma 0/3</c>
/// para <c>arma 1/3</c> e mais nada. O efeito existia — as cartas daquele herói
/// passavam a bater mais forte —, só que ele só era visível no combate seguinte,
/// e a essa altura ninguém liga o dano maior à compra.
///
/// Agora a sala tem <b>um herói na bigorna por vez</b>: à esquerda escolhe-se
/// quem sobe à bancada, no meio ficam a arma e o escudo dele, e à direita as
/// cartas que a arma afeta, com o dano que elas realmente vão causar. Forjar
/// troca a peça, acende a moldura e sobe o número na própria carta, no mesmo
/// clique. É o que o autor pediu por "quanto mais interativa, mais retorno
/// percebido".
/// </summary>
public class ForgeManager : MonoBehaviour
{
    public static ForgeManager Instance;

    [Header("Cabeçalho")]
    public TMP_Text goldText;
    public TMP_Text hintText;
    public TMP_Text feedbackText;

    [Header("Buttons")]
    public Button closeButton;

    [Header("Quem está na fila")]
    public Transform heroContainer;

    [Header("A bigorna")]
    public GameObject benchRoot;
    public Image portraitImage;
    public TMP_Text heroNameText;
    public TMP_Text heroStatsText;
    public Image weaponIcon;
    public Image weaponFrame;
    public TMP_Text weaponLevelText;
    public Button weaponButton;
    public Image armorIcon;
    public Image armorFrame;
    public TMP_Text armorLevelText;
    public Button armorButton;

    [Header("O que muda nas cartas")]
    public Transform cardShelf;
    public GameObject cardPrefab;
    public TMP_Text cardShelfTitle;

    [Header("Preços")]
    public int weaponBaseCost = 120;
    public int armorBaseCost = 100;

    [Header("Limites e efeitos")]
    public int maxUpgradeLevel = 3;
    public int hpPerArmorLevel = 4;
    public int damagePerWeaponLevel = 1;

    /// <summary>Quantas cartas do herói cabem na prateleira sem virar mosaico.</summary>
    const int MaxCartasNaPrateleira = 4;

    /// <summary>A carta do prefab é 245×345; quatro delas cabem na prateleira assim.</summary>
    const float EscalaDaCarta = 0.95f;

    HeroData naBigorna;
    readonly List<GameObject> cartasNaPrateleira = new List<GameObject>();

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

        if (weaponButton != null)
            weaponButton.onClick.AddListener(() => UpgradeWeapon(naBigorna));

        if (armorButton != null)
            armorButton.onClick.AddListener(() => UpgradeArmor(naBigorna));
    }

    /// <summary>Dano extra que as cartas emprestadas por este herói carregam.</summary>
    public static int WeaponBonus(HeroData hero)
    {
        if (hero == null) return 0;

        int perLevel = Instance != null ? Instance.damagePerWeaponLevel : 1;
        return hero.weaponLevel * perLevel;
    }

    public int WeaponCost(HeroData hero) => ComDesconto(weaponBaseCost * (hero.weaponLevel + 1), Eixo.Arma);
    public int ArmorCost(HeroData hero) => ComDesconto(armorBaseCost * (hero.armorLevel + 1), Eixo.Armadura);

    /// <summary>Os dois eixos que a forja vende, e o "nenhum" dos ciclos sem oferta.</summary>
    public enum Eixo { Nenhum, Arma, Armadura }

    /// <summary>
    /// O que a forja tem de bom neste ciclo.
    ///
    /// A sala não tem estoque para rodar — o que ela vende são dois níveis com
    /// teto —, então o que muda por volta é o preço: em dois de cada três ciclos
    /// o ferreiro recebeu material para um dos eixos e cobra menos por ele. É a
    /// mesma decisão do Mercado (ver <see cref="CycleStock"/>), aplicada ao que
    /// esta sala tem: sem isso, voltar à Forja nunca mostrava nada novo.
    ///
    /// Um em cada três ciclos não tem oferta nenhuma. Desconto em toda volta
    /// deixaria de ser oferta e viraria o preço de tabela.
    /// </summary>
    public Eixo EixoEmOferta
    {
        get
        {
            int sorteio = CycleStock.Numero("forja-oferta", 0, 3);
            return sorteio == 0 ? Eixo.Nenhum : sorteio == 1 ? Eixo.Arma : Eixo.Armadura;
        }
    }

    /// <summary>Quanto a oferta do ciclo tira do preço.</summary>
    public const float DescontoDaOferta = 0.25f;

    int ComDesconto(int preco, Eixo eixo)
    {
        if (EixoEmOferta != eixo) return preco;
        return Mathf.Max(1, Mathf.RoundToInt(preco * (1f - DescontoDaOferta)));
    }

    /// <summary>Quem está na bancada agora. Nulo enquanto a guilda não tem ninguém vivo.</summary>
    public HeroData NaBigorna => naBigorna;

    public void RefreshForge()
    {
        int gold = GuildManager.Instance != null ? GuildManager.Instance.gold : 0;

        if (goldText != null)
            goldText.text = $"💰 {gold}";

        if (hintText != null)
        {
            string regra = $"Arma: +{damagePerWeaponLevel} de dano nas cartas do herói.   "
                         + $"Armadura: +{hpPerArmorLevel} de vida máxima.   Limite: nível {maxUpgradeLevel}.";

            string oferta = EixoEmOferta == Eixo.Nenhum
                ? "<color=#8A8378>Sem material bom nesta volta.</color>"
                : $"<color=#D4AF37>Material bom nesta volta: "
                  + $"{(EixoEmOferta == Eixo.Arma ? "arma" : "armadura")} "
                  + $"{Mathf.RoundToInt(DescontoDaOferta * 100)}% mais barata.</color>";

            hintText.text = $"{regra}      {oferta}";
        }

        // Quem morreu na última jornada não pode continuar na bigorna, e quem
        // entrou agora precisa aparecer na fila.
        if (naBigorna == null || naBigorna.isDead || !EstaNoRoster(naBigorna))
            naBigorna = PrimeiroVivo();

        BuildHeroes();
        AtualizarBigorna();
    }

    static bool EstaNoRoster(HeroData hero)
    {
        return GuildManager.Instance != null && GuildManager.Instance.roster.Contains(hero);
    }

    static HeroData PrimeiroVivo()
    {
        if (GuildManager.Instance == null) return null;
        return GuildManager.Instance.roster.FirstOrDefault(h => h != null && !h.isDead);
    }

    void SetFeedback(string message)
    {
        if (feedbackText != null)
            feedbackText.text = message;
    }

    #region A fila à esquerda

    void BuildHeroes()
    {
        if (heroContainer == null) return;

        UIUtil.ClearChildrenNow(heroContainer);

        if (GuildManager.Instance == null) return;

        foreach (var hero in GuildManager.Instance.roster.Where(h => h != null && !h.isDead))
            BuildHeroRow(hero);
    }

    /// <summary>
    /// A ficha da fila. Ela não vende nada: só põe o herói na bigorna. Os dois
    /// botões de compra moram na bancada, onde se vê o que a compra faz.
    /// </summary>
    void BuildHeroRow(HeroData hero)
    {
        bool ativo = hero == naBigorna;

        var row = new GameObject(hero.heroName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        row.transform.SetParent(heroContainer, false);
        row.GetComponent<Image>().color = ativo
            ? new Color(0.30f, 0.24f, 0.16f)
            : new Color(0.17f, 0.15f, 0.14f);

        var element = row.AddComponent<LayoutElement>();
        element.minHeight = 76;
        element.preferredHeight = 76;

        // Retrato pequeno: é como o jogador reconhece o herói em todas as outras
        // telas, e sem ele a fila viraria de novo uma lista de nomes.
        if (hero.portrait != null)
        {
            var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            portraitGo.transform.SetParent(row.transform, false);

            var portrait = portraitGo.GetComponent<Image>();
            portrait.sprite = hero.portrait;
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;

            var portraitRect = portraitGo.GetComponent<RectTransform>();
            portraitRect.anchorMin = new Vector2(0, 0.5f);
            portraitRect.anchorMax = new Vector2(0, 0.5f);
            portraitRect.sizeDelta = new Vector2(60, 60);
            portraitRect.anchoredPosition = new Vector2(42, 0);
        }

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(row.transform, false);

        var label = labelGo.AddComponent<TextMeshProUGUI>();
        label.fontSize = 18;
        label.alignment = TextAlignmentOptions.Left;
        label.raycastTarget = false;
        label.color = ativo ? new Color(0.98f, 0.92f, 0.72f) : new Color(0.88f, 0.86f, 0.82f);
        label.text = $"{PartyFormation.PreferenceIcon(hero.heroClass)} {hero.heroName}\n"
                   + $"<size=13>⚔️ {ForgeArt.Blocos(hero.weaponLevel, maxUpgradeLevel)}   "
                   + $"🛡️ {ForgeArt.Blocos(hero.armorLevel, maxUpgradeLevel)}</size>";

        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(hero.portrait != null ? 80 : 14, 4);
        labelRect.offsetMax = new Vector2(-10, -4);

        HeroData capturado = hero;
        row.GetComponent<Button>().onClick.AddListener(() => PorNaBigorna(capturado));
    }

    /// <summary>Traz o herói para a bancada. É o único efeito do clique na fila.</summary>
    public void PorNaBigorna(HeroData hero)
    {
        if (hero == null || hero.isDead) return;

        naBigorna = hero;
        SetFeedback($"{hero.heroName} está na bigorna.");
        BuildHeroes();
        AtualizarBigorna();
    }

    #endregion

    #region A bigorna e a prateleira de cartas

    void AtualizarBigorna()
    {
        bool temAlguem = naBigorna != null;

        if (benchRoot != null) benchRoot.SetActive(temAlguem);
        if (!temAlguem)
        {
            MontarCartas();
            return;
        }

        if (portraitImage != null)
        {
            portraitImage.sprite = naBigorna.portrait;
            portraitImage.enabled = naBigorna.portrait != null;
            portraitImage.preserveAspect = true;
        }

        if (heroNameText != null)
            heroNameText.text = $"{PartyFormation.PreferenceIcon(naBigorna.heroClass)} {naBigorna.heroName}"
                              + $"  <size=18><color=#B8B0A0>{GetClassName(naBigorna.heroClass)} Nv.{naBigorna.level}</color></size>";

        if (heroStatsText != null)
            heroStatsText.text = $"❤️ {naBigorna.currentHp}/{naBigorna.maxHp}"
                               + (WeaponBonus(naBigorna) > 0
                                    ? $"   <color=#7FB069>⚔️ +{WeaponBonus(naBigorna)} nas cartas dele</color>"
                                    : "");

        VestirSlot(weaponIcon, weaponFrame, weaponLevelText, weaponButton,
                   ForgeArt.Arma(naBigorna.heroClass, naBigorna.weaponLevel),
                   naBigorna.weaponLevel, WeaponCost(naBigorna), "⚔️ FORJAR ARMA");

        VestirSlot(armorIcon, armorFrame, armorLevelText, armorButton,
                   ForgeArt.Armadura(naBigorna.armorLevel),
                   naBigorna.armorLevel, ArmorCost(naBigorna), "🛡️ REFORÇAR ARMADURA");

        MontarCartas();
    }

    void VestirSlot(Image icon, Image frame, TMP_Text level, Button button,
                    Sprite peca, int nivel, int custo, string rotulo)
    {
        if (icon != null)
        {
            icon.sprite = peca;
            icon.enabled = peca != null;
            icon.preserveAspect = true;
        }

        // A moldura carrega o nível quando a peça não muda de desenho — mago e
        // curandeiro têm uma família de arma só.
        if (frame != null)
            frame.color = ForgeArt.Cor(nivel);

        if (level != null)
            level.text = $"{ForgeArt.Blocos(nivel, maxUpgradeLevel)}  <size=15>nível {nivel}/{maxUpgradeLevel}</size>";

        if (button == null) return;

        bool noMaximo = nivel >= maxUpgradeLevel;
        bool temOuro = CanAfford(custo);

        button.interactable = !noMaximo && temOuro;

        var texto = button.GetComponentInChildren<TMP_Text>();
        if (texto != null)
        {
            texto.text = noMaximo
                ? "NO MÁXIMO"
                : temOuro ? $"{rotulo}   {custo}💰"
                          : $"<color=#B04040>{rotulo}   {custo}💰</color>";
        }
    }

    /// <summary>
    /// As cartas que a arma deste herói afeta, com o dano que elas vão causar de
    /// verdade. É aqui que a compra se torna visível: o número na carta sobe no
    /// mesmo clique em que o ouro sai.
    /// </summary>
    void MontarCartas()
    {
        cartasNaPrateleira.Clear();

        if (cardShelf == null) return;
        UIUtil.ClearChildrenNow(cardShelf);

        if (naBigorna == null || cardPrefab == null)
        {
            if (cardShelfTitle != null) cardShelfTitle.text = "";
            return;
        }

        DeckData deck = DeckRepository.GetDeck(naBigorna);
        List<CardData> atingidas = deck?.cards == null
            ? new List<CardData>()
            : deck.cards.Where(c => c != null && c.combatDamage > 0)
                        .Distinct()
                        .Take(MaxCartasNaPrateleira)
                        .ToList();

        if (cardShelfTitle != null)
        {
            cardShelfTitle.text = atingidas.Count == 0
                ? $"Nenhuma carta de {naBigorna.heroName} causa dano — a arma não muda nada para ele."
                : $"O que a arma de {naBigorna.heroName} muda nas cartas dele:";
        }

        int bonus = WeaponBonus(naBigorna);

        for (int i = 0; i < atingidas.Count; i++)
        {
            CardData card = atingidas[i];
            GameObject view = Instantiate(cardPrefab, cardShelf);

            // Grade posicionada à mão: um LayoutGroup ignoraria a escala e deixaria
            // buracos do tamanho da carta inteira entre elas. As colunas seguem a
            // quantidade — com uma carta só, a grade de duas deixava a carta
            // encostada num canto da prateleira vazia.
            var rect = view.transform as RectTransform;
            if (rect != null)
            {
                const float Largura = 245f * EscalaDaCarta;
                const float Altura = 345f * EscalaDaCarta;

                int colunas = Mathf.Min(2, atingidas.Count);
                int linhas = Mathf.CeilToInt(atingidas.Count / (float)colunas);
                int coluna = i % colunas;
                int linha = i / colunas;

                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.localScale = Vector3.one * EscalaDaCarta;
                rect.anchoredPosition = new Vector2(
                    (coluna - (colunas - 1) / 2f) * (Largura + 24f),
                    ((linhas - 1) / 2f - linha) * (Altura + 20f));
            }

            // O prefab da carta não tem CardUI: o combate preenche os filhos pelo
            // nome, e a forja precisa do mesmo caminho. Só com o Bind, a carta
            // aparecia com o "New Text" do editor.
            var cardUI = view.GetComponent<CardUI>();
            string descricao = card.GetDescription(false);

            // Mesma nota do combate, e pelo mesmo motivo: o jogador precisa ver o
            // efeito na carta, não num rótulo de sala.
            string nota = bonus > 0
                ? $"<color=#7FB069>⚔️ {card.combatDamage + bonus} de dano (+{bonus} da forja)</color>"
                : "";

            if (cardUI != null)
            {
                cardUI.Bind(card, journeyMode: false);
                if (nota.Length > 0) cardUI.AppendNote(nota);
            }
            else
            {
                SetTextoDoFilho(view, "CardName", card.cardName);
                SetTextoDoFilho(view, "CardDescription",
                    nota.Length > 0 ? descricao + "\n" + nota : descricao);
                SetTextoDoFilho(view, "CostTxt", $"⚡ {card.energyCost}");
                CardUI.AplicarArte(view, card);
            }

            // A carta aqui é mostruário: arrastar ou clicar não faz nada.
            var drag = view.GetComponent<CardDragHandler>();
            if (drag != null) Destroy(drag);

            var botao = view.GetComponent<Button>();
            if (botao != null) botao.interactable = false;

            cartasNaPrateleira.Add(view);
        }
    }

    #endregion

    #region Forjar

    static bool CanAfford(int cost)
    {
        return GuildManager.Instance != null && GuildManager.Instance.gold >= cost;
    }

    public void UpgradeWeapon(HeroData hero)
    {
        if (hero == null) return;

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
        AnunciarNasCartas($"+{damagePerWeaponLevel}", new Color(0.50f, 0.78f, 0.41f));
        Sacudir(weaponIcon);
    }

    public void UpgradeArmor(HeroData hero)
    {
        if (hero == null) return;

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

        if (portraitImage != null)
            CombatFeedback.Get().ShowText(portraitImage.gameObject,
                $"+{hpPerArmorLevel} ❤️", new Color(0.85f, 0.35f, 0.35f));

        Sacudir(armorIcon);
    }

    /// <summary>Preenche um texto do prefab da carta pelo nome do filho.</summary>
    static void SetTextoDoFilho(GameObject root, string nomeDoFilho, string valor)
    {
        TMP_Text alvo = root.transform.Find(nomeDoFilho)?.GetComponent<TMP_Text>();
        if (alvo != null) alvo.text = valor;
    }

    /// <summary>O ganho subindo sobre cada carta afetada, como no combate.</summary>
    void AnunciarNasCartas(string texto, Color cor)
    {
        foreach (GameObject view in cartasNaPrateleira)
        {
            if (view != null)
                CombatFeedback.Get().ShowText(view, texto, cor);
        }
    }

    void Sacudir(Image alvo)
    {
        if (alvo != null && alvo.gameObject.activeInHierarchy)
            CombatFeedback.Get().Shake(alvo.gameObject);
    }

    #endregion

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
