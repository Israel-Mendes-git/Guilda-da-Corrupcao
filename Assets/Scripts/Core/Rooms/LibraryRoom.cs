#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Monta a Biblioteca na cena.
///
/// <b>Por que a sala deixou de ser uma lista.</b> A versão anterior era um título,
/// um botão de melhorar e uma faixa com três cartas soltas, cada uma com um
/// "COMPRAR" embaixo. O jogador via o preço e não via mais nada: nem de quem era
/// o baralho que ia receber a carta, nem se ela era melhor do que o que aquele
/// herói já carrega. Comprar não mudava nada na tela — a carta sumia da faixa e
/// pronto.
///
/// Agora são três faixas, no mesmo molde da Forja: a <b>fila</b> à esquerda, com
/// quem pode receber carta; a <b>mesa de leitura</b> no meio, com um herói de cada
/// vez e o baralho que ele já leva; e a <b>estante</b> à direita, com o que está à
/// venda. Cada carta da estante é julgada contra o baralho de quem está na mesa —
/// é isso que responde "para quem serve esta carta", que era a informação que
/// faltava na hora de pagar.
///
/// <b>Sem papel de parede inventado.</b> A Forja ganhou a silhueta de caverna
/// porque o pacote <i>Pixel Fantasy Caves</i> já estava no projeto. Não há arte de
/// biblioteca aqui, e pintar estantes com retângulos marrons foi o erro que a
/// própria Forja cometeu e desfez. O que faz esta sala parecer um lugar é a
/// composição — retratos, a mesa emoldurada e os nichos da estante, todos com as
/// molduras de pedra do kit — e não uma textura de fundo.
/// </summary>
internal static class LibraryRoom
{
    const string KitRoot = "Assets/Alebardium/Bloodlines UI/";
    const string PanelSpritePath = KitRoot + "Textures/Frame/Frame_background.png";
    const string OutlineSpritePath = KitRoot + "Textures/Frame/Frame_outline.png";

    internal static GameObject Montar(Canvas canvas, GameObject cardPrefab)
    {
        GameObject panel = GuildSceneSetup.FindOrCreatePanel(canvas, "Panel_Library");

        var fundo = panel.GetComponent<Image>();
        if (fundo != null) fundo.color = GuildSceneSetup.PanelColor;

        // ── Cabeçalho ─────────────────────────────────────────────────────────
        GuildSceneSetup.EnsureText(panel.transform, "Txt_Title", "📚 Biblioteca", 32,
            new Vector2(0, 1), new Vector2(0.42f, 1), new Vector2(20, -70), new Vector2(0, -20));

        var level = GuildSceneSetup.EnsureText(panel.transform, "Txt_Level", "", 22,
            new Vector2(0.42f, 1), new Vector2(0.72f, 1), new Vector2(0, -70), new Vector2(0, -20));

        var gold = GuildSceneSetup.EnsureText(panel.transform, "Txt_Gold", "💰 0", 26,
            new Vector2(0.72f, 1), new Vector2(1, 1), new Vector2(0, -70), new Vector2(-20, -20));

        var hint = GuildSceneSetup.EnsureText(panel.transform, "Txt_Hint", "", 18,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -112), new Vector2(-20, -76));

        // ── A fila ────────────────────────────────────────────────────────────
        // A fila para em 330 para abrir espaço à mesa de tradução. Ela é uma
        // coluna com scroll, então encolher não esconde herói nenhum — e o
        // roster de cinco a oito fichas continua cabendo sem rolar.
        GameObject list = GuildSceneSetup.EnsureScrollColumn(panel.transform, "List",
            new Vector2(0, 0), new Vector2(0, 1), new Vector2(20, 330), new Vector2(360, -140), 8);

        AjustarColuna(list);

        // ── A mesa de leitura ─────────────────────────────────────────────────
        GameObject desk = GuildSceneSetup.EnsureFreeArea(panel.transform, "Desk",
            new Vector2(0, 0), new Vector2(0, 1), new Vector2(380, 110), new Vector2(1060, -140));
        VestirCaixa(desk, new Color(0.14f, 0.13f, 0.17f, 0.92f));

        GameObject portraitGo = GuildSceneSetup.EnsureFreeArea(desk.transform, "Img_Portrait",
            new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-110, -250), new Vector2(110, -30));
        var portrait = GarantirImagem(portraitGo);
        portrait.preserveAspect = true;
        portrait.raycastTarget = false;

        var heroName = GuildSceneSetup.EnsureText(desk.transform, "Txt_HeroName", "", 26,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -302), new Vector2(-16, -256));
        heroName.alignment = TextAlignmentOptions.Center;

        var heroStats = GuildSceneSetup.EnsureText(desk.transform, "Txt_HeroStats", "", 19,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -344), new Vector2(-16, -304));
        heroStats.alignment = TextAlignmentOptions.Center;

        var deckTitle = GuildSceneSetup.EnsureText(desk.transform, "Txt_DeckTitle", "", 18,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -392), new Vector2(-16, -352));
        deckTitle.alignment = TextAlignmentOptions.Center;
        deckTitle.color = GuildSceneSetup.SubtleTextColor;

        // O baralho de quem está na mesa é o "antes" contra o qual a compra se
        // mede, então ele fica inteiro à vista — 12 cartas cabem sem rolar, e o
        // scroll existe para o dia em que o teto do deck subir.
        GameObject deckList = GuildSceneSetup.EnsureScrollColumn(desk.transform, "Deck",
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(14, 14), new Vector2(-14, -400), 4);

        AjustarColuna(deckList);

        // ── A estante ─────────────────────────────────────────────────────────
        var shelfTitle = GuildSceneSetup.EnsureText(panel.transform, "Txt_Shelf", "", 19,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(1090, -190), new Vector2(-20, -140));
        shelfTitle.alignment = TextAlignmentOptions.Center;

        GameObject shelf = GuildSceneSetup.EnsureFreeArea(panel.transform, "Shelf",
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(1090, 110), new Vector2(-20, -200));

        // ── A mesa de tradução ────────────────────────────────────────────────
        //
        // Embaixo da fila, e não na coluna da direita: os seis nichos da estante
        // precisam de 698px de altura, e encolhê-la para abrir espaço fez as
        // cartas de baixo vazarem por trás desta caixa — o botão COMPRAR da
        // segunda fileira sumiu sob ela, e só a captura mostrou. Aqui o espaço
        // estava ocioso: cinco fichas de herói usam menos da metade da coluna.
        GameObject writings = GuildSceneSetup.EnsureFreeArea(panel.transform, "Writings",
            new Vector2(0, 0), new Vector2(0, 0), new Vector2(20, 110), new Vector2(360, 320));
        VestirCaixa(writings, new Color(0.16f, 0.14f, 0.11f, 0.92f));

        var writingsTitle = GuildSceneSetup.EnsureText(writings.transform, "Txt_WritingsTitle",
            "📜 Escritos", 19,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(12, -40), new Vector2(-12, -8));
        writingsTitle.alignment = TextAlignmentOptions.Center;

        var writingsBody = GuildSceneSetup.EnsureText(writings.transform, "Txt_WritingsBody", "", 15,
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(12, 58), new Vector2(-12, -44));
        writingsBody.alignment = TextAlignmentOptions.TopLeft;

        var translate = GuildSceneSetup.EnsureButton(writings.transform, "Btn_Translate", "TRADUZIR",
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(12, 12), new Vector2(-12, 50));

        // ── Rodapé ────────────────────────────────────────────────────────────
        var feedback = GuildSceneSetup.EnsureText(panel.transform, "Txt_Feedback", "", 18,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(260, 40), new Vector2(-360, 96));

        // Melhorar a sala troca o que a estante oferece, então o botão mora ao pé
        // dela, e não junto do título como na versão anterior — lá ele disputava a
        // atenção com o nome da sala e não se ligava a nada visível.
        var upgrade = GuildSceneSetup.EnsureButton(panel.transform, "Btn_Upgrade", "MELHORAR",
            new Vector2(1, 0), new Vector2(1, 0), new Vector2(-340, 26), new Vector2(-20, 80));

        var close = GuildSceneSetup.EnsureButton(panel.transform, "Btn_Close", "Voltar",
            new Vector2(0, 0), new Vector2(0, 0), new Vector2(20, 20), new Vector2(220, 60));

        // ── As referências ────────────────────────────────────────────────────
        // Inclui inativos: o painel da biblioteca nasce desligado, e um
        // FindObjectOfType comum devolveria null e criaria um segundo manager —
        // dois Awake disputando o mesmo Instance, e a sala inteira ficaria sem
        // dono.
        LibraryManager library = Object.FindObjectOfType<LibraryManager>(true);
        if (library == null)
        {
            var host = new GameObject("LibraryManager");
            Undo.RegisterCreatedObjectUndo(host, "Criar LibraryManager");
            library = host.AddComponent<LibraryManager>();
        }

        Undo.RecordObject(library, "Montar Cena");
        library.levelText = level;
        library.goldText = gold;
        library.hintText = hint;
        library.feedbackText = feedback;
        library.heroContainer = list.transform;
        library.deskRoot = desk;
        library.portraitImage = portrait;
        library.heroNameText = heroName;
        library.heroStatsText = heroStats;
        library.deckTitleText = deckTitle;
        library.deckContainer = deckList.transform;
        library.cardsContainer = shelf.transform;
        library.shelfTitleText = shelfTitle;
        library.cardPrefab = cardPrefab;
        library.upgradeButton = upgrade;
        library.closeButton = close;
        library.writingsTitleText = writingsTitle;
        library.writingsText = writingsBody;
        library.translateButton = translate;

        // A moldura dos nichos vem do Editor porque os nichos nascem em tempo de
        // execução, onde o AssetDatabase não existe. Nula, o nicho vira uma caixa
        // escura e a estante continua legível.
        library.nicheSprite = AssetDatabase.LoadAssetAtPath<Sprite>(OutlineSpritePath);

        EditorUtility.SetDirty(library);

        panel.SetActive(false);
        return panel;
    }

    /// <summary>
    /// As colunas nascem com o layout largo do <c>EnsureColumn</c>, que deixa cada
    /// filho com a largura que ele pediu. Aqui as fichas precisam ocupar a coluna
    /// inteira, senão a fila vira uma pilha de retângulos de tamanhos diferentes.
    /// </summary>
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

    /// <summary>
    /// Caixa com a moldura de pedra do kit. Cópia local: a do
    /// <see cref="GuildSceneSetup"/> é privada, e abrir a visibilidade dela só
    /// para esta sala mexeria num arquivo que outras pessoas estão editando.
    /// </summary>
    static void VestirCaixa(GameObject alvo, Color cor)
    {
        var image = GarantirImagem(alvo);
        image.color = cor;
        image.raycastTarget = false;

        Sprite painel = AssetDatabase.LoadAssetAtPath<Sprite>(PanelSpritePath);
        if (painel == null) return;

        image.sprite = painel;
        image.type = Image.Type.Sliced;
    }

    /// <summary>Cópia local, pelo mesmo motivo do <see cref="VestirCaixa"/>.</summary>
    static Image GarantirImagem(GameObject alvo)
    {
        var image = alvo.GetComponent<Image>();
        if (image == null) image = Undo.AddComponent<Image>(alvo);
        return image;
    }
}
#endif
