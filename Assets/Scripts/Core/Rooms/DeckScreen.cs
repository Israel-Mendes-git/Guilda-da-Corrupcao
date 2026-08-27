#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Monta a tela de Baralhos. A última da guilda que ainda era herdada da cena
/// original, montada à mão.
///
/// <b>O que havia.</b> Fichas de herói que eram o botão branco padrão do Unity,
/// com a palavra <c>Button</c> aparecendo por baixo do nome — o código escrevia
/// no primeiro <c>TMP_Text</c> que achava e o segundo ficava com o rótulo de
/// fábrica. Nomes cortados no meio ("Ca ador"), caixas pretas vazias ocupando
/// metade da tela, o baralho e o acervo espremidos em duas colunas estreitas onde
/// cabiam duas cartas. Ver <c>Assets/Screenshots/tela_deck.png</c> de 26/08.
///
/// <b>O que ficou.</b> O mesmo desenho das seis salas — fila, foco, efeito
/// visível: os heróis à esquerda; no meio o <b>baralho</b> de quem está em foco,
/// com o balanço por papel embaixo; à direita o <b>acervo</b> da classe dele. A
/// carta anda de um lado para o outro pelo clique ou pelo arrasto, que já
/// existiam e continuam valendo.
///
/// <b>Por que grade e não coluna.</b> As outras salas listam linhas de texto;
/// aqui o objeto é a carta, e carta se lê pelo desenho. Numa coluna cabiam duas
/// por vez, e escolher o que tirar do baralho exigia rolar — comparar era
/// impossível. A grade põe as doze à vista.
/// </summary>
internal static class DeckScreen
{
    const string KitRoot = "Assets/Alebardium/Bloodlines UI/";
    const string PanelSpritePath = KitRoot + "Textures/Frame/Frame_background.png";
    const string CardPrefabPath = "Assets/Prefabs/UI/CardPrefab.prefab";

    /// <summary>Onde o painel mora na cena herdada.</summary>
    const string PanelPath = "Background/Panel_DeckManager";

    /// <summary>O objeto que hospeda o manager, fora do painel.</summary>
    const string HostName = "DeckManager";

    /// <summary>
    /// A carta cabe em 170×240 e a faixa tem 740 de largura: quatro por linha,
    /// que é o número de cópias máximo da mesma carta — uma linha cheia lê como
    /// "cheguei ao teto".
    /// </summary>
    static readonly Vector2 CartaTamanho = new Vector2(170, 240);

    internal static GameObject Montar(Canvas canvas)
    {
        Transform achado = canvas.transform.Find(PanelPath);
        if (achado == null)
        {
            Debug.LogWarning($"Montar Cena: {PanelPath} não encontrado — tela de baralhos não montada.");
            return null;
        }

        GameObject panel = achado.gameObject;

        // O painel herdado tinha tamanho próprio e um fundo translúcido: o rodapé
        // da guilda atravessava a tela inteira.
        var rect = panel.GetComponent<RectTransform>();
        Undo.RecordObject(rect, "Montar Cena");
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var fundo = GarantirImagem(panel);
        fundo.color = GuildSceneSetup.PanelColor;
        fundo.sprite = null;

        LimparMontagemAntiga(panel);

        // ── Cabeçalho ─────────────────────────────────────────────────────────
        GuildSceneSetup.EnsureText(panel.transform, "Txt_Title", "🃏 Baralhos", 32,
            new Vector2(0, 1), new Vector2(0.4f, 1), new Vector2(20, -70), new Vector2(0, -20));

        var hint = GuildSceneSetup.EnsureText(panel.transform, "Txt_Hint",
            "Clique numa carta para movê-la entre o baralho e o acervo — ou arraste. "
            + "Nada vale até Salvar.", 18,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -112), new Vector2(-20, -76));
        hint.color = GuildSceneSetup.SubtleTextColor;

        var heroName = GuildSceneSetup.EnsureText(panel.transform, "Txt_HeroName", "", 26,
            new Vector2(0.4f, 1), new Vector2(1, 1), new Vector2(0, -70), new Vector2(-20, -20));
        heroName.alignment = TextAlignmentOptions.Right;

        // ── A fila ────────────────────────────────────────────────────────────
        GameObject fila = GuildSceneSetup.EnsureScrollColumn(panel.transform, "Heroes",
            new Vector2(0, 0), new Vector2(0, 1), new Vector2(20, 110), new Vector2(360, -140), 8);

        AjustarColuna(fila);

        // ── O baralho ─────────────────────────────────────────────────────────
        var deckTitle = GuildSceneSetup.EnsureText(panel.transform, "Txt_DeckTitle", "O baralho deste herói", 20,
            new Vector2(0, 1), new Vector2(0.6f, 1), new Vector2(380, -190), new Vector2(0, -150));

        GameObject deck = EnsureScrollGrid(panel.transform, "Deck",
            new Vector2(0, 0), new Vector2(0, 1), new Vector2(380, 170), new Vector2(1120, -200));

        var deckStats = GuildSceneSetup.EnsureText(panel.transform, "Txt_DeckStats", "", 18,
            new Vector2(0, 0), new Vector2(0, 0), new Vector2(380, 118), new Vector2(1120, 160));
        deckStats.alignment = TextAlignmentOptions.Center;

        // ── O acervo ──────────────────────────────────────────────────────────
        var collectionTitle = GuildSceneSetup.EnsureText(panel.transform, "Txt_CollectionTitle",
            "O acervo da guilda", 20,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(1140, -190), new Vector2(-20, -150));

        GameObject collection = EnsureScrollGrid(panel.transform, "Collection",
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(1140, 170), new Vector2(-20, -200));

        var collectionStats = GuildSceneSetup.EnsureText(panel.transform, "Txt_CollectionStats", "", 18,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(1140, 118), new Vector2(-20, 160));
        collectionStats.alignment = TextAlignmentOptions.Center;

        // ── Rodapé ────────────────────────────────────────────────────────────
        var close = GuildSceneSetup.EnsureButton(panel.transform, "Btn_Close", "Voltar",
            new Vector2(0, 0), new Vector2(0, 0), new Vector2(20, 20), new Vector2(220, 60));

        // Salvar fica longe de Voltar: sair sem salvar descarta a edição, e os
        // dois lado a lado convidam ao clique errado.
        var save = GuildSceneSetup.EnsureButton(panel.transform, "Btn_Save", "SALVAR",
            new Vector2(1, 0), new Vector2(1, 0), new Vector2(-240, 20), new Vector2(-20, 60));

        var reset = GuildSceneSetup.EnsureButton(panel.transform, "Btn_Reset", "DESFAZER",
            new Vector2(1, 0), new Vector2(1, 0), new Vector2(-460, 20), new Vector2(-260, 60));

        // ── As referências ────────────────────────────────────────────────────
        DeckManager manager = GarantirManager();
        GameObject cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);

        Undo.RecordObject(manager, "Montar Cena");
        manager.deckManagerPanel = panel;
        manager.heroNameText = heroName;
        manager.heroSelectionContainer = fila.transform;
        manager.currentDeckContainer = deck.transform;
        manager.collectionContainer = collection.transform;
        manager.deckStatsText = deckStats;
        manager.collectionStatsText = collectionStats;
        manager.cardPrefab = cardPrefab;
        manager.closeButton = close;
        manager.saveButton = save;
        manager.resetButton = reset;
        EditorUtility.SetDirty(manager);

        var ui = Object.FindObjectOfType<UIManager>(true);
        if (ui != null && ui.deckManagerPanel != panel)
        {
            Undo.RecordObject(ui, "Montar Cena");
            ui.deckManagerPanel = panel;
            EditorUtility.SetDirty(ui);
        }

        panel.SetActive(false);
        return panel;
    }

    /// <summary>
    /// Apaga a montagem herdada. São objetos com nomes próprios da cena antiga —
    /// deixá-los ali daria uma tela com duas barras de título e dois pares de
    /// botões, um deles sem dono.
    /// </summary>
    static void LimparMontagemAntiga(GameObject panel)
    {
        string[] velhos =
        {
            "Panel_TopBar", "Panel_HeroSelector", "Panel_DeckContent",
            "Button_Save", "Button_Reset", "Button_Close"
        };

        foreach (string nome in velhos)
        {
            Transform velho = panel.transform.Find(nome);
            if (velho != null) Undo.DestroyObjectImmediate(velho.gameObject);
        }
    }

    /// <summary>
    /// O manager num objeto próprio, e um só na cena — mesma razão da Taverna e
    /// do Mercado: dentro do painel, que nasce desligado, o <c>Awake</c> não roda.
    /// </summary>
    static DeckManager GarantirManager()
    {
        var naCena = Resources.FindObjectsOfTypeAll<DeckManager>()
            .Where(m => m != null && !EditorUtility.IsPersistent(m) && m.gameObject.scene.IsValid())
            .ToList();

        DeckManager escolhido = naCena.FirstOrDefault(m => m.transform.parent == null
                                                        && m.gameObject.name == HostName);

        if (escolhido == null)
        {
            GameObject host = GameObject.Find(HostName);
            if (host == null)
            {
                host = new GameObject(HostName);
                Undo.RegisterCreatedObjectUndo(host, "Criar DeckManager");
            }

            escolhido = host.GetComponent<DeckManager>();
            if (escolhido == null) escolhido = Undo.AddComponent<DeckManager>(host);
        }

        foreach (DeckManager herdado in naCena)
        {
            if (herdado == escolhido) continue;
            Undo.DestroyObjectImmediate(herdado);
        }

        return escolhido;
    }

    /// <summary>
    /// Um scroll com grade, no mesmo molde do <c>EnsureScrollColumn</c> — que só
    /// sabe empilhar em coluna. Cópia, e não parâmetro novo lá: aquele método é
    /// usado por sete telas e o tipo do layout muda o significado dos campos de
    /// espaçamento.
    /// </summary>
    static GameObject EnsureScrollGrid(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
                                       Vector2 offsetMin, Vector2 offsetMax)
    {
        const float BarWidth = 14f;

        GameObject area = GuildSceneSetup.EnsureFreeArea(parent, name + "_Scroll",
            anchorMin, anchorMax, offsetMin, offsetMax);

        var scroll = area.GetComponent<ScrollRect>();
        if (scroll == null) scroll = Undo.AddComponent<ScrollRect>(area);

        GameObject viewport = GuildSceneSetup.EnsureFreeArea(area.transform, "Viewport",
            new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, new Vector2(-(BarWidth + 4f), 0));

        // Mask e não RectMask2D: com RectMask2D o texto das cartas some, como já
        // aconteceu nas salas.
        var mask = viewport.GetComponent<Mask>();
        if (mask == null) mask = Undo.AddComponent<Mask>(viewport);
        mask.showMaskGraphic = false;

        var maskImage = viewport.GetComponent<Image>();
        if (maskImage == null) maskImage = Undo.AddComponent<Image>(viewport);
        maskImage.color = new Color(0.11f, 0.10f, 0.12f, 0.85f);

        GameObject grid = GuildSceneSetup.EnsureFreeArea(viewport.transform, name,
            new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero);

        var layout = grid.GetComponent<GridLayoutGroup>();
        if (layout == null) layout = Undo.AddComponent<GridLayoutGroup>(grid);

        Undo.RecordObject(layout, "Montar Cena");
        layout.cellSize = CartaTamanho;
        layout.spacing = new Vector2(12, 12);
        layout.padding = new RectOffset(12, 12, 12, 12);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.constraint = GridLayoutGroup.Constraint.Flexible;
        EditorUtility.SetDirty(layout);

        var gridRect = grid.GetComponent<RectTransform>();
        Undo.RecordObject(gridRect, "Montar Cena");
        gridRect.pivot = new Vector2(0.5f, 1f);
        gridRect.anchorMin = new Vector2(0, 1);
        gridRect.anchorMax = new Vector2(1, 1);
        gridRect.offsetMin = new Vector2(0, -100);
        gridRect.offsetMax = Vector2.zero;

        var fitter = grid.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = Undo.AddComponent<ContentSizeFitter>(grid);
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Undo.RecordObject(scroll, "Montar Cena");
        scroll.content = gridRect;
        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 32f;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        EditorUtility.SetDirty(scroll);

        return grid;
    }

    static void AjustarColuna(GameObject coluna)
    {
        var layout = coluna != null ? coluna.GetComponent<VerticalLayoutGroup>() : null;
        if (layout == null) return;

        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        layout.childAlignment = TextAnchor.UpperCenter;
    }

    static Image GarantirImagem(GameObject alvo)
    {
        var image = alvo.GetComponent<Image>();
        if (image == null) image = Undo.AddComponent<Image>(alvo);
        return image;
    }
}
#endif
