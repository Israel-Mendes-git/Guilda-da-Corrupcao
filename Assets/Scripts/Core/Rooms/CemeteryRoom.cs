#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Monta o Cemitério na cena.
///
/// <b>Por que a sala deixou de ser uma lista.</b> Um herói morto virava uma linha
/// de texto com um botão "HOMENAGEAR" ao lado — o mesmo peso visual de um item de
/// mercado, numa sala que é o registro do custo do jogo. E o que a guilda perdeu
/// com ele (o nível a que chegou, a vida que tinha, o que a Forja cobrou, as
/// relíquias e as poções enterradas junto) não aparecia em canto nenhum, embora
/// tudo estivesse gravado no <see cref="HeroData"/>.
///
/// Agora são três faixas, no molde da Forja: as <b>tumbas</b> à esquerda; a tumba
/// em <b>foco</b> no meio, com o retrato apagado, a ficha do morto e o botão do
/// monumento; e à direita <b>quem ficou</b>, com uma barra de estresse por vivo.
/// A vigília move essas barras no mesmo clique em que o ouro sai — antes, os 12
/// de alívio eram uma frase no rodapé.
///
/// <b>O fundo é um bosque de inverno, não um cemitério desenhado.</b> Não há arte
/// de cemitério no projeto; o que existe é a silhueta de taiga do
/// <i>Distant Forest Assets</i>, que já serve de fundo de bioma. Tingida de
/// escuro, ela dá à sala uma linha de árvores em vez do mármore genérico da
/// guilda. Desenhar lápides com retângulos foi o erro que a Forja cometeu e
/// desfez com a "brasa"; não se repete aqui.
/// </summary>
internal static class CemeteryRoom
{
    const string KitRoot = "Assets/Alebardium/Bloodlines UI/";
    const string PanelSpritePath = KitRoot + "Textures/Frame/Frame_background.png";
    const string OutlineSpritePath = KitRoot + "Textures/Frame/Frame_outline.png";

    /// <summary>Linha de árvores nuas com faixa de céu no alto — a camada que
    /// emoldura sem cobrir o meio da tela.</summary>
    const string BosquePath = "Assets/Distant Forest Assets/Taiga Forest/TaigaBg_3.png";

    /// <summary>
    /// O que a tela mostra quando ninguém morreu — que é a maioria das visitas.
    ///
    /// Não é aviso de erro nem piada ("Aproveite enquanto dura", da versão
    /// anterior): é a regra da casa. Quem chega aqui cedo precisa saber o que a
    /// morte custa e o que esta sala faz quando houver o que fazer.
    ///
    /// A primeira frase não repete "Nenhum herói tombou até aqui." — essa já
    /// está no resumo do cabeçalho (<c>Txt_Summary</c>). Aqui embaixo ela diz o
    /// que a própria caixa vai mostrar no dia em que houver um morto.
    /// </summary>
    const string TextoVazio =
        "Quando um herói cair, a tumba dele aparece aqui, com o retrato apagado e o "
      + "que ele carregava.\n\n"
      + "A morte é permanente. Quem cai sai do grupo para sempre e leva consigo as relíquias, "
      + "as poções e tudo o que a Forja tiver melhorado nele.\n\n"
      + "O registro de cada perda fica nesta sala. O monumento devolve reputação, e a vigília "
      + "alivia estresse em todos os vivos — mas só há vigília quando há quem velar.";

    internal static GameObject Montar(Canvas canvas)
    {
        GameObject panel = GuildSceneSetup.FindOrCreatePanel(canvas, "Panel_Cemetery");

        VestirBosque(panel);

        // ── Cabeçalho ─────────────────────────────────────────────────────────
        GuildSceneSetup.EnsureText(panel.transform, "Txt_Title", "⚰️ Cemitério", 32,
            new Vector2(0, 1), new Vector2(0.60f, 1), new Vector2(20, -70), new Vector2(0, -20));

        var gold = GuildSceneSetup.EnsureText(panel.transform, "Txt_Gold", "💰 0", 26,
            new Vector2(0.60f, 1), new Vector2(0.78f, 1), new Vector2(0, -70), new Vector2(0, -20));

        // A reputação está no alto porque é o efeito da homenagem. Sem ela na
        // tela, pagar 80 de ouro não mudava nada que o jogador pudesse ver — e
        // era essa a queixa sobre a sala.
        var reputation = GuildSceneSetup.EnsureText(panel.transform, "Txt_Reputation", "", 24,
            new Vector2(0.78f, 1), new Vector2(1, 1), new Vector2(0, -70), new Vector2(-20, -20));

        var summary = GuildSceneSetup.EnsureText(panel.transform, "Txt_Summary", "", 22,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -112), new Vector2(-20, -76));

        var hint = GuildSceneSetup.EnsureText(panel.transform, "Txt_Hint", "", 18,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -148), new Vector2(-20, -114));

        // ── As tumbas ─────────────────────────────────────────────────────────
        GameObject list = GuildSceneSetup.EnsureScrollColumn(panel.transform, "List",
            new Vector2(0, 0), new Vector2(0, 1), new Vector2(20, 110), new Vector2(360, -175), 8);

        AjustarColuna(list);

        // ── A tumba em foco ───────────────────────────────────────────────────
        GameObject grave = GuildSceneSetup.EnsureFreeArea(panel.transform, "Grave",
            new Vector2(0, 0), new Vector2(0, 1), new Vector2(380, 110), new Vector2(1060, -175));
        VestirCaixa(grave, new Color(0.13f, 0.13f, 0.15f, 0.92f));

        // A caixa do meio fica na tela mesmo sem morto nenhum: é o chão do
        // cemitério. O que liga e desliga é o conteúdo dela.
        GameObject occupant = GuildSceneSetup.EnsureFreeArea(grave.transform, "Occupant",
            new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);
        occupant.SetActive(true);

        GameObject frameGo = GuildSceneSetup.EnsureFreeArea(occupant.transform, "Frame",
            new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-138, -296), new Vector2(138, -20));
        var frame = GarantirImagem(frameGo);
        frame.raycastTarget = false;

        Sprite moldura = AssetDatabase.LoadAssetAtPath<Sprite>(OutlineSpritePath);
        if (moldura != null)
        {
            frame.sprite = moldura;
            frame.type = Image.Type.Sliced;
        }

        GameObject portraitGo = GuildSceneSetup.EnsureFreeArea(frameGo.transform, "Img_Portrait",
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(12, 12), new Vector2(-12, -12));
        var portrait = GarantirImagem(portraitGo);
        portrait.preserveAspect = true;
        portrait.raycastTarget = false;

        var heroName = GuildSceneSetup.EnsureText(occupant.transform, "Txt_HeroName", "", 26,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -348), new Vector2(-16, -302));
        heroName.alignment = TextAlignmentOptions.Center;

        var record = GuildSceneSetup.EnsureText(occupant.transform, "Txt_Record", "", 18,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -492), new Vector2(-24, -352));
        record.alignment = TextAlignmentOptions.Top;

        var belongingsTitle = GuildSceneSetup.EnsureText(occupant.transform, "Txt_Belongings", "", 17,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -528), new Vector2(-16, -496));
        belongingsTitle.alignment = TextAlignmentOptions.Center;

        GameObject belongings = GuildSceneSetup.EnsureFreeArea(occupant.transform, "Belongings",
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -632), new Vector2(-16, -532));

        var tribute = GuildSceneSetup.EnsureButton(occupant.transform, "Btn_Tribute", "ERGUER MONUMENTO",
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(70, -712), new Vector2(-70, -652));

        var tributeLabel = tribute.GetComponentInChildren<TMP_Text>();
        if (tributeLabel != null) tributeLabel.fontSize = 17;

        // O estado vazio mora dentro da caixa, e não solto no painel: quem chega
        // sem mortos precisa ver a sala, não um parágrafo pairando no escuro.
        // A versão anterior punha este texto colado no cabeçalho — a migração
        // apaga o órfão para os dois não aparecerem juntos.
        Transform orfao = panel.transform.Find("Txt_Empty");
        if (orfao != null) Undo.DestroyObjectImmediate(orfao.gameObject);

        var empty = GuildSceneSetup.EnsureText(grave.transform, "Txt_Empty", TextoVazio, 20,
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(48, 190), new Vector2(-48, -200));
        empty.text = TextoVazio;
        empty.alignment = TextAlignmentOptions.Center;
        empty.color = GuildSceneSetup.SubtleTextColor;

        // O wrap deveria vir ligado por padrão do TMP, mas este objeto é
        // reaproveitado entre execuções do setup (EnsureText só reposiciona
        // quem já existe) — se alguma vez ficou desligado, ficava preso assim.
        // Sem isto, o parágrafo virava uma linha só, larga demais para a caixa,
        // e transbordava para os dois lados.
        empty.enableWordWrapping = true;

        // O mesmo ícone do título, grande e apagado, dá peso à caixa quando ela
        // só tem texto — sem ele a moldura de pedra ficava enorme em volta de um
        // parágrafo pequeno, e o estado vazio (a maioria das visitas) parecia um
        // acidente, não uma composição. Filho de Txt_Empty para ligar e desligar
        // junto com ele, sem precisar de outra referência no manager.
        var emptyIcon = GuildSceneSetup.EnsureText(empty.transform, "Txt_EmptyIcon", "⚰️", 56,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 15), new Vector2(0, 130));
        emptyIcon.alignment = TextAlignmentOptions.Center;
        emptyIcon.color = GuildSceneSetup.SubtleTextColor;

        // ── Quem ficou ────────────────────────────────────────────────────────
        var livingTitle = GuildSceneSetup.EnsureText(panel.transform, "Txt_Living", "", 19,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(1090, -215), new Vector2(-20, -175));
        livingTitle.alignment = TextAlignmentOptions.Center;

        GameObject living = GuildSceneSetup.EnsureScrollColumn(panel.transform, "Living",
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(1090, 190), new Vector2(-20, -225), 8);

        AjustarColuna(living);

        var vigil = GuildSceneSetup.EnsureButton(panel.transform, "Btn_Vigil", "VIGÍLIA",
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(1090, 110), new Vector2(-20, 170));

        var vigilLabel = vigil.GetComponentInChildren<TMP_Text>();
        if (vigilLabel != null) vigilLabel.fontSize = 17;

        // ── Rodapé ────────────────────────────────────────────────────────────
        var feedback = GuildSceneSetup.EnsureText(panel.transform, "Txt_Feedback", "", 18,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(240, 40), new Vector2(-20, 96));

        var close = GuildSceneSetup.EnsureButton(panel.transform, "Btn_Close", "Voltar",
            new Vector2(0, 0), new Vector2(0, 0), new Vector2(20, 20), new Vector2(220, 60));

        // Inativo incluído: numa cena já montada o manager pode estar pendurado
        // num objeto desligado, e criar um segundo faria o Awake matar um deles.
        CemeteryManager cemetery = Object.FindObjectOfType<CemeteryManager>(true);
        if (cemetery == null)
        {
            var host = new GameObject("CemeteryManager");
            Undo.RegisterCreatedObjectUndo(host, "Criar CemeteryManager");
            cemetery = host.AddComponent<CemeteryManager>();
        }

        Undo.RecordObject(cemetery, "Montar Cena");
        cemetery.goldText = gold;
        cemetery.reputationText = reputation;
        cemetery.summaryText = summary;
        cemetery.hintText = hint;
        cemetery.feedbackText = feedback;
        cemetery.graveContainer = list.transform;
        cemetery.emptyStateText = empty;
        cemetery.graveRoot = occupant;
        cemetery.graveFrame = frame;
        cemetery.portraitImage = portrait;
        cemetery.heroNameText = heroName;
        cemetery.recordText = record;
        cemetery.belongingsTitle = belongingsTitle;
        cemetery.belongingsRow = belongings.transform;
        cemetery.tributeButton = tribute;
        cemetery.livingTitle = livingTitle;
        cemetery.livingContainer = living.transform;
        cemetery.vigilButton = vigil;
        cemetery.closeButton = close;
        EditorUtility.SetDirty(cemetery);

        panel.SetActive(false);
        return panel;
    }

    /// <summary>
    /// O fundo que faz a sala parecer um lugar. A silhueta é opaca e cobre o
    /// painel inteiro, como a caverna da Forja; o tom escuro deixa os troncos
    /// visíveis sem competir com as três caixas que ficam por cima.
    /// </summary>
    static void VestirBosque(GameObject panel)
    {
        var fundo = panel.GetComponent<Image>();
        if (fundo != null) fundo.color = GuildSceneSetup.PanelColor;

        GameObject bosque = GuildSceneSetup.EnsureFreeArea(panel.transform, "Bg_Grove",
            new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);
        bosque.transform.SetAsFirstSibling();

        var image = GarantirImagem(bosque);
        image.raycastTarget = false;
        image.color = new Color(0.24f, 0.24f, 0.27f);

        // Diferente da caverna da Forja, esta arte não é pixel art: são formas
        // grandes e chapadas, e o filtro padrão as estica até 1920 sem serrilhar.
        // Não há reimport a fazer aqui.
        Sprite arte = AssetDatabase.LoadAssetAtPath<Sprite>(BosquePath);
        image.sprite = arte;
        image.enabled = arte != null;
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
