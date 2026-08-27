using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cemitério: o registro do que a guilda já pagou.
///
/// A morte é permanente e é o pilar de design do jogo; esta é a única tela onde
/// isso fica escrito. Homenagear devolve parte da reputação que a morte tirou, e
/// a vigília converte o luto em alívio de estresse para quem ficou — fora o
/// vinho do Mercado, é o único jeito de baixar estresse.
///
/// <b>Por que a sala deixou de ser uma lista.</b> Um herói morto virava uma
/// linha de texto com um botão ao lado: o mesmo peso visual de um item de
/// mercado. E o que o jogo já sabia dele — o nível a que chegou, a vida que
/// tinha, o que a Forja cobrou por ele, as relíquias e as poções que foram
/// enterradas junto — não aparecia em lugar nenhum.
///
/// Agora são três faixas, no molde da Forja: as <b>tumbas</b> à esquerda; a
/// tumba em <b>foco</b> no meio, com o retrato apagado e a ficha do que se
/// perdeu; e à direita <b>quem ficou</b>, com a barra de estresse de cada vivo.
/// A vigília faz essas barras descerem no mesmo clique em que o ouro sai — antes
/// os 12 de alívio eram uma linha de feedback e mais nada, e ninguém ligava o
/// gasto ao efeito.
///
/// <b>O epitáfio saiu.</b> A versão anterior escrevia uma frase por
/// personalidade ("Nunca recuou.", "Morreu com os bolsos cheios."). Era ficção
/// inventada aqui dentro, e o mundo do jogo é decisão do autor; no lugar dela
/// estão os números que o jogo já guardava sobre o morto.
///
/// <b>O que esta classe não resolve.</b> O estoque da sala não muda por ciclo:
/// a razão para voltar ao Cemitério continua sendo só a vigília, e isso é
/// tratado fora daqui.
/// </summary>
public class CemeteryManager : MonoBehaviour
{
    public static CemeteryManager Instance;

    [Header("Cabeçalho")]
    public TMP_Text goldText;
    public TMP_Text reputationText;
    public TMP_Text summaryText;
    public TMP_Text hintText;
    public TMP_Text feedbackText;

    [Header("As tumbas")]
    public Transform graveContainer;
    public TMP_Text emptyStateText;

    [Header("A tumba em foco")]
    public GameObject graveRoot;
    public Image graveFrame;
    public Image portraitImage;
    public TMP_Text heroNameText;
    public TMP_Text recordText;
    public TMP_Text belongingsTitle;
    public Transform belongingsRow;
    public Button tributeButton;

    [Header("Quem ficou")]
    public TMP_Text livingTitle;
    public Transform livingContainer;
    public Button vigilButton;

    [Header("Buttons")]
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

    /// <summary>A tumba aberta no meio da tela. Nula enquanto ninguém morreu.</summary>
    HeroData naLapide;

    /// <summary>
    /// As linhas da direita, guardadas entre um refresh e outro.
    ///
    /// A vigília precisa delas vivas para animar as barras: reconstruir a coluna
    /// destruiria os <c>Image</c> no mesmo frame em que a animação começa, e o
    /// alívio voltaria a ser invisível.
    /// </summary>
    readonly List<LinhaViva> vivos = new List<LinhaViva>();

    class LinhaViva
    {
        public HeroData heroi;
        public GameObject raiz;
        public Image preenchimento;
        public TMP_Text rotulo;
    }

    /// <summary>Pedra sem homenagem, e a mesma pedra depois do monumento.</summary>
    static readonly Color CorDaPedra = new Color(0.42f, 0.40f, 0.38f);
    static readonly Color CorDoMonumento = new Color(0.93f, 0.76f, 0.33f);

    // O retrato do morto é o mesmo que o jogador conhece, só que sem cor. Não há
    // shader de dessaturação na UI, e tingir de cinza custa nada e lê igual.
    static readonly Color RetratoApagado = new Color(0.50f, 0.50f, 0.55f);
    static readonly Color RetratoHomenageado = new Color(0.80f, 0.75f, 0.66f);

    static readonly Color CorDoAlivio = new Color(0.55f, 0.75f, 0.90f);
    static readonly Color CorDaReputacao = new Color(0.93f, 0.82f, 0.45f);

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

        if (tributeButton != null)
            tributeButton.onClick.AddListener(() => PayTribute(naLapide));
    }

    public void RefreshCemetery()
    {
        List<HeroData> fallen = Fallen();

        // O foco pode ter ficado para trás: carregar um save ou começar outra
        // partida troca a lista inteira de mortos.
        if (naLapide == null || !fallen.Contains(naLapide))
            naLapide = fallen.Count > 0 ? fallen[fallen.Count - 1] : null;

        AtualizarTopo(fallen);
        BuildGraves(fallen);
        AtualizarTumba();
        BuildLiving();
        AtualizarVigilia(fallen.Count);
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

    static bool CanAfford(int cost)
    {
        return GuildManager.Instance != null && GuildManager.Instance.gold >= cost;
    }

    #region Cabeçalho

    void AtualizarTopo(List<HeroData> fallen)
    {
        int gold = GuildManager.Instance != null ? GuildManager.Instance.gold : 0;
        int rep = GuildManager.Instance != null ? GuildManager.Instance.reputation : 0;

        if (goldText != null)
            goldText.text = $"💰 {gold}";

        // A reputação fica no alto porque é o efeito da homenagem: sem ela na
        // tela, pagar 80 de ouro não mudava nada que o jogador pudesse ver.
        if (reputationText != null)
            reputationText.text = $"Reputação {rep}";

        if (summaryText != null)
        {
            int comMonumento = fallen.Count(h => honored.Contains(h.GetId()));
            summaryText.text = fallen.Count == 0
                ? "Nenhum herói tombou até aqui."
                : $"⚰️ {fallen.Count} tumba(s) — {comMonumento} com monumento";
        }

        if (hintText != null)
        {
            hintText.text = $"Homenagear: +{tributeReputation} de reputação, uma vez por tumba.   "
                          + $"Vigília: −{Mathf.RoundToInt(vigilStressRelief)} de estresse em todos os vivos.";
        }
    }

    #endregion

    #region As tumbas à esquerda

    void BuildGraves(List<HeroData> fallen)
    {
        if (graveContainer == null) return;

        UIUtil.ClearChildrenNow(graveContainer);

        // As mais recentes primeiro: são as que ainda doem e as que o jogador
        // reconhece. A coluna rola, então nenhuma tumba fica de fora — o corte
        // em oito da versão anterior escondia os mortos antigos sem dizer.
        foreach (var hero in Enumerable.Reverse(fallen))
            BuildGraveRow(hero);
    }

    /// <summary>
    /// A lápide da fila. Ela não vende nada: só abre aquele morto no meio da
    /// tela. O botão de homenagear mora na tumba em foco, onde se vê o que a
    /// compra faz.
    /// </summary>
    void BuildGraveRow(HeroData hero)
    {
        bool ativo = hero == naLapide;
        bool jaHomenageado = honored.Contains(hero.GetId());

        var row = new GameObject(hero.heroName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        row.transform.SetParent(graveContainer, false);
        row.GetComponent<Image>().color = ativo
            ? new Color(0.26f, 0.25f, 0.28f)
            : new Color(0.15f, 0.14f, 0.16f);

        var element = row.AddComponent<LayoutElement>();
        element.minHeight = 72;
        element.preferredHeight = 72;

        if (hero.portrait != null)
        {
            var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            portraitGo.transform.SetParent(row.transform, false);

            var portrait = portraitGo.GetComponent<Image>();
            portrait.sprite = hero.portrait;
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            portrait.color = jaHomenageado ? RetratoHomenageado : RetratoApagado;

            var portraitRect = portraitGo.GetComponent<RectTransform>();
            portraitRect.anchorMin = new Vector2(0, 0.5f);
            portraitRect.anchorMax = new Vector2(0, 0.5f);
            portraitRect.sizeDelta = new Vector2(56, 56);
            portraitRect.anchoredPosition = new Vector2(40, 0);
        }

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(row.transform, false);

        var label = labelGo.AddComponent<TextMeshProUGUI>();
        label.fontSize = 18;
        label.alignment = TextAlignmentOptions.Left;
        label.raycastTarget = false;
        label.color = ativo ? new Color(0.94f, 0.92f, 0.88f) : new Color(0.76f, 0.74f, 0.71f);

        // O losango é a marca do monumento. A fonte do jogo não tem ✦, e um
        // emoji a mais aqui é um glifo a mais no atlas do TMP.
        string marca = jaHomenageado ? "<color=#EDC254>◆</color> " : "";
        label.text = $"{marca}{hero.heroName}\n"
                   + $"<size=13>{GetClassName(hero.heroClass)} Nv.{hero.level}</size>";

        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(hero.portrait != null ? 78 : 14, 4);
        labelRect.offsetMax = new Vector2(-10, -4);

        HeroData capturado = hero;
        row.GetComponent<Button>().onClick.AddListener(() => PorNaLapide(capturado));
    }

    /// <summary>Abre este morto no meio da tela. É o único efeito do clique na fila.</summary>
    public void PorNaLapide(HeroData hero)
    {
        if (hero == null) return;

        naLapide = hero;
        BuildGraves(Fallen());
        AtualizarTumba();
    }

    #endregion

    #region A tumba em foco

    void AtualizarTumba()
    {
        bool temAlguem = naLapide != null;

        if (graveRoot != null) graveRoot.SetActive(temAlguem);

        // A caixa do meio continua na tela mesmo vazia: é o chão do cemitério,
        // e some-lo deixaria um buraco no lugar da sala.
        if (emptyStateText != null) emptyStateText.gameObject.SetActive(!temAlguem);

        if (!temAlguem)
        {
            MontarPertences(null);
            return;
        }

        bool jaHomenageado = honored.Contains(naLapide.GetId());

        if (portraitImage != null)
        {
            portraitImage.sprite = naLapide.portrait;
            portraitImage.enabled = naLapide.portrait != null;
            portraitImage.preserveAspect = true;
            portraitImage.color = jaHomenageado ? RetratoHomenageado : RetratoApagado;
        }

        // A moldura acende ao receber o monumento: é o que muda na tela no mesmo
        // clique em que o ouro sai.
        if (graveFrame != null)
            graveFrame.color = jaHomenageado ? CorDoMonumento : CorDaPedra;

        if (heroNameText != null)
        {
            heroNameText.text = $"{naLapide.heroName}"
                              + $"  <size=18><color=#B8B0A0>{GetClassName(naLapide.heroClass)} Nv.{naLapide.level}</color></size>";
        }

        if (recordText != null)
            recordText.text = Ficha(naLapide);

        if (tributeButton != null)
        {
            tributeButton.interactable = !jaHomenageado && CanAfford(tributeCost);

            var texto = tributeButton.GetComponentInChildren<TMP_Text>();
            if (texto != null)
            {
                texto.text = jaHomenageado
                    ? "◆ MONUMENTO ERGUIDO"
                    : CanAfford(tributeCost)
                        ? $"ERGUER MONUMENTO   {tributeCost}💰"
                        : $"<color=#B04040>ERGUER MONUMENTO   {tributeCost}💰</color>";
            }
        }

        MontarPertences(naLapide);
    }

    /// <summary>
    /// O que a guilda perdeu com este herói, tirado só do que já estava gravado
    /// nele. Sem epitáfio: cada linha é um número que o jogador pode conferir em
    /// outra tela.
    /// </summary>
    static string Ficha(HeroData hero)
    {
        int maxForja = ForgeManager.Instance != null ? ForgeManager.Instance.maxUpgradeLevel : 3;
        var linhas = new List<string>();

        linhas.Add($"❤️ {hero.maxHp} de vida máxima");

        // O que a Forja cobrou por ele morre junto: é a parte da conta que o
        // jogador tende a esquecer, porque pagou em outra sala.
        linhas.Add(hero.weaponLevel + hero.armorLevel > 0
            ? $"⚔️ arma {hero.weaponLevel}/{maxForja}   🛡️ armadura {hero.armorLevel}/{maxForja}"
              + "   <size=15><color=#B8B0A0>(a forja foi com ele)</color></size>"
            : "<color=#B8B0A0>Nunca passou pela forja.</color>");

        string estado = MentalStateUtil.GetLabel(hero.mentalState);
        string traco = hero.trait != Trait.None ? $"   ·   {GetTraitName(hero.trait)}" : "";
        linhas.Add($"🧠 {estado}   ·   {GetPersonalityName(hero.personality)}{traco}");

        if (hero.corruptionExposure > 0)
            linhas.Add($"<color=#9E7FA8>Exposição à corrupção: {hero.corruptionExposure}/100</color>");

        return string.Join("\n", linhas);
    }

    /// <summary>
    /// As relíquias e as poções que foram enterradas com ele.
    ///
    /// Itens são do herói e morrem com ele — decisão de design que só aparecia
    /// como um sumiço silencioso da prateleira. Aqui o que se perdeu fica com
    /// nome e ícone, ao lado do preço que o Mercado cobrou por ele.
    /// </summary>
    void MontarPertences(HeroData hero)
    {
        if (belongingsRow == null) return;

        UIUtil.ClearChildrenNow(belongingsRow);

        if (hero == null)
        {
            if (belongingsTitle != null) belongingsTitle.text = "";
            return;
        }

        var itens = new List<(string id, string nome)>();

        if (hero.relics != null)
        {
            foreach (string id in hero.relics)
            {
                RelicDef def = ItemCatalog.Reliquia(id);
                if (def != null) itens.Add((id, def.nome));
            }
        }

        if (hero.potions != null)
        {
            foreach (string id in hero.potions)
            {
                PotionDef def = ItemCatalog.Pocao(id);
                if (def != null) itens.Add((id, def.nome));
            }
        }

        if (belongingsTitle != null)
        {
            belongingsTitle.text = itens.Count == 0
                ? "<color=#B8B0A0>Não carregava nada.</color>"
                : itens.Count > MaxPertencesNaLinha
                    // O corte fica dito: uma prateleira que esconde item em
                    // silêncio é o defeito que esta sala acabou de perder.
                    ? $"Enterrado com ele  <size=14><color=#B8B0A0>({MaxPertencesNaLinha} de {itens.Count})</color></size>"
                    : "Enterrado com ele:";
        }

        // Grade posicionada à mão, como a prateleira da Forja: um LayoutGroup
        // centralizaria mal a linha de um item só.
        const float Largura = 132f;
        int colunas = Mathf.Min(MaxPertencesNaLinha, Mathf.Max(1, itens.Count));

        for (int i = 0; i < itens.Count && i < MaxPertencesNaLinha; i++)
        {
            var go = new GameObject(itens[i].nome, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(belongingsRow, false);
            go.GetComponent<Image>().color = new Color(0.20f, 0.19f, 0.18f);
            go.GetComponent<Image>().raycastTarget = false;

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(Largura - 10f, 84f);
            rect.anchoredPosition = new Vector2((i - (colunas - 1) / 2f) * Largura, 0f);

            Sprite icone = ItemCatalog.Icone(itens[i].id);
            if (icone != null)
            {
                var iconeGo = new GameObject("Icone", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconeGo.transform.SetParent(go.transform, false);

                var img = iconeGo.GetComponent<Image>();
                img.sprite = icone;
                img.preserveAspect = true;
                img.raycastTarget = false;
                img.color = RetratoApagado;

                var iconeRect = iconeGo.GetComponent<RectTransform>();
                iconeRect.anchorMin = new Vector2(0.5f, 1f);
                iconeRect.anchorMax = new Vector2(0.5f, 1f);
                iconeRect.sizeDelta = new Vector2(40, 40);
                iconeRect.anchoredPosition = new Vector2(0, -24);
            }

            var nomeGo = new GameObject("Nome", typeof(RectTransform));
            nomeGo.transform.SetParent(go.transform, false);

            var nome = nomeGo.AddComponent<TextMeshProUGUI>();
            nome.text = itens[i].nome;
            nome.fontSize = 13;
            nome.alignment = TextAlignmentOptions.Center;
            nome.color = new Color(0.78f, 0.76f, 0.72f);
            nome.raycastTarget = false;
            nome.overflowMode = TextOverflowModes.Ellipsis;

            var nomeRect = nomeGo.GetComponent<RectTransform>();
            nomeRect.anchorMin = new Vector2(0, 0);
            nomeRect.anchorMax = new Vector2(1, 0);
            nomeRect.offsetMin = new Vector2(2, 4);
            nomeRect.offsetMax = new Vector2(-2, 30);
        }
    }

    /// <summary>Quantos pertences cabem na largura da tumba sem virar mosaico.</summary>
    const int MaxPertencesNaLinha = 4;

    public void PayTribute(HeroData hero)
    {
        if (hero == null)
        {
            SetFeedback("Nenhuma tumba selecionada.");
            return;
        }

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

        // O ganho sobe sobre o próprio número da reputação: é onde o jogador
        // precisa olhar para ver que os 80 de ouro viraram alguma coisa.
        if (reputationText != null)
            CombatFeedback.Get().ShowText(reputationText.gameObject, $"+{tributeReputation}", CorDaReputacao);

        if (graveFrame != null && graveFrame.gameObject.activeInHierarchy)
            CombatFeedback.Get().Shake(graveFrame.gameObject);
    }

    #endregion

    #region Quem ficou, e a vigília

    void BuildLiving()
    {
        vivos.Clear();

        if (livingContainer == null) return;

        UIUtil.ClearChildrenNow(livingContainer);

        List<HeroData> roster = LivingRoster().ToList();

        if (livingTitle != null)
        {
            if (roster.Count == 0)
                livingTitle.text = "A guilda está sem heróis.";
            else if (roster.All(h => h.stress <= 0f))
                livingTitle.text = "Quem ficou não carrega peso nenhum.";
            else
                livingTitle.text = "O peso que ficou com os vivos:";
        }

        foreach (var hero in roster)
            BuildLivingRow(hero);
    }

    /// <summary>
    /// Um vivo e a barra de estresse dele. É esta coluna que a vigília move — o
    /// equivalente à prateleira de cartas da Forja.
    /// </summary>
    void BuildLivingRow(HeroData hero)
    {
        var row = new GameObject(hero.heroName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        row.transform.SetParent(livingContainer, false);
        row.GetComponent<Image>().color = new Color(0.17f, 0.16f, 0.17f);
        row.GetComponent<Image>().raycastTarget = false;

        var element = row.AddComponent<LayoutElement>();
        element.minHeight = 84;
        element.preferredHeight = 84;

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
            portraitRect.sizeDelta = new Vector2(58, 58);
            portraitRect.anchoredPosition = new Vector2(40, 0);
        }

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(row.transform, false);

        var label = labelGo.AddComponent<TextMeshProUGUI>();
        label.fontSize = 17;
        label.alignment = TextAlignmentOptions.TopLeft;
        label.raycastTarget = false;
        label.color = new Color(0.90f, 0.88f, 0.84f);

        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0, 1);
        labelRect.anchorMax = new Vector2(1, 1);
        labelRect.offsetMin = new Vector2(hero.portrait != null ? 78 : 14, -46);
        labelRect.offsetMax = new Vector2(-12, -8);

        // Trilho e "Fill" — o mesmo molde de barra que o resto do jogo usa.
        var trilho = new GameObject("StressBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        trilho.transform.SetParent(row.transform, false);
        trilho.GetComponent<Image>().color = new Color(0.10f, 0.09f, 0.11f);
        trilho.GetComponent<Image>().raycastTarget = false;

        var trilhoRect = trilho.GetComponent<RectTransform>();
        trilhoRect.anchorMin = new Vector2(0, 0);
        trilhoRect.anchorMax = new Vector2(1, 0);
        trilhoRect.offsetMin = new Vector2(hero.portrait != null ? 78 : 14, 14);
        trilhoRect.offsetMax = new Vector2(-12, 30);

        var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fillGo.transform.SetParent(trilho.transform, false);

        var fillRect = fillGo.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        var fill = fillGo.GetComponent<Image>();
        fill.raycastTarget = false;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;

        var linha = new LinhaViva { heroi = hero, raiz = row, preenchimento = fill, rotulo = label };
        vivos.Add(linha);

        AtualizarLinhaViva(linha, animar: false);
    }

    void AtualizarLinhaViva(LinhaViva linha, bool animar)
    {
        if (linha == null || linha.heroi == null) return;

        float estresse = Mathf.Clamp(linha.heroi.stress, 0f, 100f);
        float fracao = estresse / 100f;

        if (linha.preenchimento != null)
        {
            linha.preenchimento.color = CorDoEstresse(estresse);

            if (animar) CombatFeedback.Get().LerpBar(linha.preenchimento, fracao);
            else linha.preenchimento.fillAmount = fracao;
        }

        if (linha.rotulo == null) return;

        string estado = linha.heroi.mentalState != MentalState.Normal
            ? $"   <size=13><color=#B8B0A0>{MentalStateUtil.GetLabel(linha.heroi.mentalState)}</color></size>"
            : "";

        string aviso = linha.heroi.IsFitForJourney
            ? ""
            : "   <size=13><color=#C0483E>não parte assim</color></size>";

        linha.rotulo.text = $"{linha.heroi.heroName}{estado}\n"
                          + $"<size=14>🧠 {Mathf.RoundToInt(estresse)}/100{aviso}</size>";
    }

    /// <summary>
    /// Verde enquanto dá, âmbar quando pesa, vermelho a partir do ponto em que o
    /// herói se recusa a viajar. O degrau vermelho é o mesmo número que o
    /// <see cref="HeroData.StressLimiteParaViajar"/> usa, e não um valor à parte.
    /// </summary>
    static Color CorDoEstresse(float estresse)
    {
        if (estresse >= HeroData.StressLimiteParaViajar) return new Color(0.78f, 0.28f, 0.26f);
        if (estresse >= 45f) return new Color(0.85f, 0.72f, 0.35f);
        return new Color(0.44f, 0.62f, 0.44f);
    }

    void AtualizarVigilia(int tumbas)
    {
        if (vigilButton == null) return;

        bool temQuemVelar = tumbas > 0;
        bool temPeso = vivos.Any(l => l.heroi != null && l.heroi.stress > 0f);
        bool temOuro = CanAfford(vigilCost);

        vigilButton.interactable = temQuemVelar && temPeso && temOuro;

        var label = vigilButton.GetComponentInChildren<TMP_Text>();
        if (label == null) return;

        if (!temQuemVelar) label.text = "<color=#8A857E>VIGÍLIA — SEM QUEM VELAR</color>";
        else if (!temPeso) label.text = "<color=#8A857E>VIGÍLIA — NINGUÉM CARREGA PESO</color>";
        else if (!temOuro) label.text = $"<color=#B04040>VIGÍLIA   {vigilCost}💰</color>";
        else label.text = $"VIGÍLIA   −{Mathf.RoundToInt(vigilStressRelief)}🧠 EM TODOS   {vigilCost}💰";
    }

    /// <summary>A guilda inteira vela os mortos: caro, mas é alívio para todos de uma vez.</summary>
    void HoldVigil()
    {
        List<HeroData> fallen = Fallen();

        if (fallen.Count == 0)
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

        foreach (var linha in vivos)
        {
            if (linha.heroi == null || linha.heroi.stress <= 0f) continue;

            float antes = linha.heroi.stress;
            linha.heroi.stress = Mathf.Max(0f, antes - vigilStressRelief);
            aliviados++;

            AtualizarLinhaViva(linha, animar: true);

            if (linha.raiz != null)
            {
                CombatFeedback.Get().ShowText(linha.raiz,
                    $"−{Mathf.RoundToInt(antes - linha.heroi.stress)}🧠", CorDoAlivio);
            }
        }

        SetFeedback(aliviados > 0
            ? $"A guilda velou os mortos. {aliviados} herói(s) respiram melhor."
            : "A vigília foi silenciosa. Ninguém carregava peso.");

        // Tudo menos a coluna da direita: reconstruí-la agora destruiria as
        // barras no primeiro frame da animação, e o alívio voltaria a ser
        // invisível — que era exatamente o defeito desta sala.
        AtualizarTopo(fallen);
        BuildGraves(fallen);
        AtualizarTumba();
        AtualizarVigilia(fallen.Count);
    }

    #endregion

    #region Rótulos

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

    static string GetPersonalityName(Personality personality)
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

    static string GetTraitName(Trait trait)
    {
        switch (trait)
        {
            case Trait.Drunkard: return "Bêbado";
            case Trait.Lucky: return "Sortudo";
            case Trait.Scarred: return "Cicatrizado";
            case Trait.FastHealer: return "Cura Rápida";
            case Trait.Cursed: return "Amaldiçoado";
            default: return "Sem traço";
        }
    }

    #endregion
}
