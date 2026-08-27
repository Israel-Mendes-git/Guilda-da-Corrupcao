#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Monta a Sala de Mapas na cena. Chamada pelo <see cref="GuildSceneSetup"/>,
/// como <c>BuildForge</c> — o painel inteiro nasce daqui, e o
/// <see cref="MapRoomManager"/> só recebe as referências prontas.
///
/// <b>Três faixas, e não uma pilha de botões.</b> À esquerda a <b>fila das sete
/// regiões</b>, com a corrupção de cada uma; no meio a <b>mesa</b>, com a região
/// em foco, o contrato que o quadro oferece lá e as duas compras; à direita a
/// <b>estrada</b> até aquele destino, desenhada sobre papel. É o mesmo desenho
/// que a forja estabeleceu: fila, foco, efeito visível — e aqui o efeito é
/// literalmente o marco que se abre quando o batedor é contratado.
///
/// <b>O que este arquivo faz e o manager não.</b> Tudo o que precisa de
/// <c>AssetDatabase</c>: o papel do mapa como fundo da sala, a moldura do
/// pacote em volta da estrada e a caixa do kit atrás da mesa. O desenho da
/// estrada em si é do manager, que roda em execução — a estrada muda a cada
/// compra, e uma hierarquia serializada não acompanharia isso.
///
/// <b>Por que reimplementar dois auxiliares.</b> <c>EnsureImageComponent</c> e
/// <c>VestirCaixa</c> são privados no <see cref="GuildSceneSetup"/>. Abrir a
/// visibilidade deles mexeria num arquivo que está em mãos de outra pessoa;
/// copiar oito linhas custa menos que um conflito.
/// </summary>
internal static class MapRoom
{
    const string KitRoot = "Assets/Alebardium/Bloodlines UI/";
    const string PanelSpritePath = KitRoot + "Textures/Frame/Frame_background.png";
    const string OutlineSpritePath = KitRoot + "Textures/Frame/Frame_outline.png";

    /// <summary>Onde o <see cref="MapArtBuilder"/> grava o catálogo.</summary>
    const string CatalogoPath = "Assets/Resources/MapArtCatalog.asset";

    internal static GameObject Montar(Canvas canvas)
    {
        GameObject panel = GuildSceneSetup.FindOrCreatePanel(canvas, "Panel_MapRoom");

        LimparMontagemAntiga(panel);

        MapArtCatalog arte = AssetDatabase.LoadAssetAtPath<MapArtCatalog>(CatalogoPath);
        VestirSalaDeCartografia(panel, arte);

        // ── Cabeçalho ─────────────────────────────────────────────────────────
        GuildSceneSetup.EnsureText(panel.transform, "Txt_Title", "🗺️ Sala de Mapas", 32,
            new Vector2(0, 1), new Vector2(0.7f, 1), new Vector2(20, -70), new Vector2(0, -20));

        var gold = GuildSceneSetup.EnsureText(panel.transform, "Txt_Gold", "💰 0", 26,
            new Vector2(0.7f, 1), new Vector2(1, 1), new Vector2(0, -70), new Vector2(-20, -20));
        gold.alignment = TextAlignmentOptions.Right;

        var hint = GuildSceneSetup.EnsureText(panel.transform, "Txt_Hint", "", 18,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -112), new Vector2(-20, -76));

        // ── A fila das regiões ────────────────────────────────────────────────
        GameObject list = GuildSceneSetup.EnsureScrollColumn(panel.transform, "List",
            new Vector2(0, 0), new Vector2(0, 1), new Vector2(20, 110), new Vector2(360, -140), 8);

        var listLayout = list.GetComponent<VerticalLayoutGroup>();
        if (listLayout != null)
        {
            listLayout.childControlWidth = true;
            listLayout.childForceExpandWidth = true;
            listLayout.childControlHeight = true;
            listLayout.childForceExpandHeight = false;
            listLayout.childAlignment = TextAnchor.UpperCenter;
        }

        // ── A mesa ────────────────────────────────────────────────────────────
        GameObject table = GuildSceneSetup.EnsureFreeArea(panel.transform, "Table",
            new Vector2(0, 0), new Vector2(0, 1), new Vector2(380, 110), new Vector2(1060, -140));

        // Tom de madeira, e não o mármore que as sete salas dividem: a mesa
        // precisa se separar do papel que está do lado.
        VestirCaixa(table, new Color(0.19f, 0.15f, 0.12f, 0.94f));

        // 240 de moldura para ~108 de tinta: os símbolos do pacote ocupam menos
        // da metade do próprio quadro, e é a correção do catálogo que iguala uns
        // aos outros. O que passa da moldura é transparência.
        GameObject regionGo = GuildSceneSetup.EnsureFreeArea(table.transform, "Img_Region",
            new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-120, -260), new Vector2(120, -20));
        var regionIcon = GarantirImagem(regionGo);
        regionIcon.preserveAspect = true;
        regionIcon.raycastTarget = false;

        var regionName = GuildSceneSetup.EnsureText(table.transform, "Txt_RegionName", "", 26,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -300), new Vector2(-16, -254));
        regionName.alignment = TextAlignmentOptions.Center;

        Image corruptionFill = BuildCorruptionBar(table.transform);

        var dossier = GuildSceneSetup.EnsureText(table.transform, "Txt_Dossier", "", 19,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -452), new Vector2(-16, -340));
        // Top já é "centrado na horizontal, colado no topo" — o dossiê tem de
        // uma a três linhas, e centralizá-lo na vertical o faria dançar.
        dossier.alignment = TextAlignmentOptions.Top;
        dossier.enableWordWrapping = true;

        Image scoutFrame, detourFrame;
        TMP_Text scoutGlyph, scoutCount, detourGlyph, detourCount;
        Button scoutBtn, detourBtn;

        BuildTableSlot(table.transform, "Slot_Scout", 0.02f, 0.49f, "🔭", "🔭 CONTRATAR BATEDOR",
                       out scoutFrame, out scoutGlyph, out scoutCount, out scoutBtn);
        BuildTableSlot(table.transform, "Slot_Detour", 0.51f, 0.98f, "🧭", "🧭 TRAÇAR DESVIO",
                       out detourFrame, out detourGlyph, out detourCount, out detourBtn);

        var level = GuildSceneSetup.EnsureText(table.transform, "Txt_Level", "", 17,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(16, 74), new Vector2(-16, 102));
        level.alignment = TextAlignmentOptions.Center;
        level.color = GuildSceneSetup.SubtleTextColor;

        var upgrade = GuildSceneSetup.EnsureButton(table.transform, "Btn_Upgrade", "AMPLIAR A SALA",
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(40, 16), new Vector2(-40, 68));

        var upgradeLabel = upgrade.GetComponentInChildren<TMP_Text>();
        if (upgradeLabel != null) upgradeLabel.fontSize = 17;

        // ── A estrada ─────────────────────────────────────────────────────────
        var roadTitle = GuildSceneSetup.EnsureText(panel.transform, "Txt_Road", "", 20,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(1090, -190), new Vector2(-20, -140));
        roadTitle.alignment = TextAlignmentOptions.Center;

        GameObject road = GuildSceneSetup.EnsureFreeArea(panel.transform, "Road",
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(1090, 110), new Vector2(-20, -200));

        var paper = GarantirImagem(road);
        paper.raycastTarget = false;
        paper.type = Image.Type.Simple;
        paper.sprite = arte != null ? arte.papel : null;
        paper.color = paper.sprite != null ? Color.white : new Color(0.11f, 0.10f, 0.09f, 0.92f);

        VestirBorda(road.transform, arte);

        GameObject trilha = GuildSceneSetup.EnsureFreeArea(road.transform, "Trilha",
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(24, 130), new Vector2(-24, -24));

        GameObject desvios = GuildSceneSetup.EnsureFreeArea(road.transform, "Desvios",
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(24, 14), new Vector2(-24, 118));

        // Último irmão: nasce por cima da estrada, que é onde o aviso de "sem
        // contrato" precisa estar para ser lido.
        var empty = GuildSceneSetup.EnsureText(road.transform, "Txt_Empty", "", 20,
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(60, 200), new Vector2(-60, -200));
        empty.alignment = TextAlignmentOptions.Center;
        empty.enableWordWrapping = true;
        empty.transform.SetAsLastSibling();

        // ── Rodapé ────────────────────────────────────────────────────────────
        var feedback = GuildSceneSetup.EnsureText(panel.transform, "Txt_Feedback", "", 18,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(260, 40), new Vector2(-20, 96));

        var close = GuildSceneSetup.EnsureButton(panel.transform, "Btn_Close", "Voltar",
            new Vector2(0, 0), new Vector2(0, 0), new Vector2(20, 20), new Vector2(220, 60));

        // ── As referências ────────────────────────────────────────────────────
        MapRoomManager mr = Object.FindObjectOfType<MapRoomManager>();
        if (mr == null)
        {
            var host = new GameObject("MapRoomManager");
            Undo.RegisterCreatedObjectUndo(host, "Criar MapRoomManager");
            mr = host.AddComponent<MapRoomManager>();
        }

        Undo.RecordObject(mr, "Montar Cena");
        mr.goldText = gold;
        mr.hintText = hint;
        mr.feedbackText = feedback;
        mr.levelText = level;
        mr.closeButton = close;
        mr.upgradeButton = upgrade;
        mr.buyScoutingButton = scoutBtn;
        mr.buyDetourButton = detourBtn;
        mr.regionContainer = list.transform;
        mr.tableRoot = table;
        mr.regionIcon = regionIcon;
        mr.regionNameText = regionName;
        mr.dossierText = dossier;
        mr.corruptionFill = corruptionFill;
        mr.scoutFrame = scoutFrame;
        mr.scoutGlyph = scoutGlyph;
        mr.scoutingText = scoutCount;
        mr.detourFrame = detourFrame;
        mr.detourGlyph = detourGlyph;
        mr.detourText = detourCount;
        mr.roadPaper = paper;
        mr.roadTitleText = roadTitle;
        mr.revealedEventsContainer = trilha.transform;
        mr.detourContainer = desvios.transform;
        mr.emptyStateText = empty;
        EditorUtility.SetDirty(mr);

        panel.SetActive(false);
        return panel;
    }

    /// <summary>
    /// O que a montagem antiga deixou no painel.
    ///
    /// Os auxiliares do setup encontram por nome e reposicionam; o que <b>mudou
    /// de pai</b> — o nível e o botão de ampliar, que agora moram na mesa — ou o
    /// que simplesmente deixou de existir fica para trás, visível e morto. Uma
    /// cena já montada guardaria os dois contadores antigos e os três botões da
    /// versão em lista por cima do papel, sem erro nenhum no console.
    /// </summary>
    static void LimparMontagemAntiga(GameObject panel)
    {
        string[] herdados =
        {
            "Txt_Level", "Txt_Scouting", "Txt_Detours", "Txt_Empty",
            "RevealedEvents", "RevealedEvents_Scroll",
            "Btn_Upgrade", "Btn_Scout", "Btn_Detour",
        };

        foreach (string nome in herdados)
        {
            Transform velho = panel.transform.Find(nome);
            if (velho != null) Undo.DestroyObjectImmediate(velho.gameObject);
        }
    }

    /// <summary>
    /// Um lugar para a compra: moldura, o símbolo dentro dela, a carga em blocos
    /// e o botão que paga. Espelha o slot da bigorna, com uma diferença — aqui o
    /// símbolo é glifo da fonte, não sprite: o pacote de mapa não tem luneta nem
    /// bússola de inventário, e desenhar um marco de estrada dentro da moldura
    /// diria que a compra age naquele ponto específico.
    /// </summary>
    static void BuildTableSlot(Transform table, string nome, float esquerda, float direita,
                               string glifo, string rotulo,
                               out Image frame, out TMP_Text glyphText, out TMP_Text count, out Button button)
    {
        GameObject slot = GuildSceneSetup.EnsureFreeArea(table, nome,
            new Vector2(esquerda, 0), new Vector2(direita, 1), new Vector2(0, 105), new Vector2(0, -462));

        GameObject frameGo = GuildSceneSetup.EnsureFreeArea(slot.transform, "Frame",
            new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-70, -140), new Vector2(70, 0));
        frame = GarantirImagem(frameGo);
        frame.raycastTarget = false;

        Sprite moldura = AssetDatabase.LoadAssetAtPath<Sprite>(OutlineSpritePath);
        if (moldura != null)
        {
            frame.sprite = moldura;
            frame.type = Image.Type.Sliced;
        }

        glyphText = GuildSceneSetup.EnsureText(frameGo.transform, "Txt_Glyph", glifo, 60,
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(8, 8), new Vector2(-8, -8));
        glyphText.alignment = TextAlignmentOptions.Center;

        count = GuildSceneSetup.EnsureText(slot.transform, "Txt_Count", "", 20,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(4, -182), new Vector2(-4, -146));
        count.alignment = TextAlignmentOptions.Center;

        button = GuildSceneSetup.EnsureButton(slot.transform, "Btn_Buy", rotulo,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(10, -250), new Vector2(-10, -192));

        var texto = button.GetComponentInChildren<TMP_Text>();
        if (texto != null) texto.fontSize = 16;
    }

    /// <summary>
    /// A barra da corrupção da região: trilho e um preenchimento
    /// <see cref="Image.Type.Filled"/>, no mesmo molde (trilho + filho "Fill")
    /// que o resto do jogo usa e que o <see cref="BarSkin"/> reconhece.
    ///
    /// <b>Os sprites vêm daqui, e não do BarSkin.</b> Um <c>Image</c> sem sprite
    /// desenha um quadrado e <b>ignora o fillAmount</b> — a barra apareceria
    /// sempre cheia, dizendo que toda região está 100% corrompida. O BarSkin é
    /// um item de menu que ninguém garante ter rodado; a informação não pode
    /// depender disso.
    /// </summary>
    static Image BuildCorruptionBar(Transform table)
    {
        const string BarraRoot = KitRoot + "Textures/Progress_Bar/Rectangle/";
        Sprite trilho = AssetDatabase.LoadAssetAtPath<Sprite>(BarraRoot + "Progress_Bar_Rectangle_empty_v1.png");
        Sprite cheia = AssetDatabase.LoadAssetAtPath<Sprite>(BarraRoot + "Progress_Bar_Rectangle_full_v1.png");

        GameObject track = GuildSceneSetup.EnsureFreeArea(table, "Bar_Corruption",
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(90, -334), new Vector2(-90, -308));

        var fundo = GarantirImagem(track);
        fundo.raycastTarget = false;
        fundo.type = Image.Type.Simple;
        fundo.sprite = trilho;
        fundo.color = trilho != null ? Color.white : GuildSceneSetup.TrackColor;

        GameObject fillGo = GuildSceneSetup.EnsureFreeArea(track.transform, "Fill",
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(2, 2), new Vector2(-2, -2));

        var fill = GarantirImagem(fillGo);
        fill.raycastTarget = false;
        fill.sprite = cheia;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 0f;

        return fill;
    }

    /// <summary>
    /// O fundo que faz a sala parecer um lugar: o mesmo papel do mapa da
    /// preparação, escurecido, cobrindo a parede toda. Era a queixa mais direta
    /// do autor sobre as sete salas — todas usam o mármore escuro da guilda e
    /// nenhuma parece um lugar.
    ///
    /// Sem o catálogo montado, a imagem fica desligada e a sala volta ao fundo
    /// de sempre. Nada aqui depende da arte para funcionar.
    /// </summary>
    static void VestirSalaDeCartografia(GameObject panel, MapArtCatalog arte)
    {
        var fundo = panel.GetComponent<Image>();
        if (fundo != null) fundo.color = GuildSceneSetup.PanelColor;

        GameObject parede = GuildSceneSetup.EnsureFreeArea(panel.transform, "Bg_Paper",
            new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);
        parede.transform.SetAsFirstSibling();

        var image = GarantirImagem(parede);
        image.raycastTarget = false;
        image.type = Image.Type.Simple;
        image.sprite = arte != null ? arte.papel : null;

        // Escuro o bastante para não competir com o papel da estrada, que é onde
        // a informação está. Em cheio, os dois papéis brigavam e a tela virava
        // uma mancha bege só.
        image.color = new Color(0.30f, 0.26f, 0.21f, 0.55f);
        image.enabled = image.sprite != null;
    }

    /// <summary>A moldura decorada do pacote em volta da estrada, quando existe.</summary>
    static void VestirBorda(Transform road, MapArtCatalog arte)
    {
        GameObject borda = GuildSceneSetup.EnsureFreeArea(road, "Borda",
            new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);

        var image = GarantirImagem(borda);
        image.raycastTarget = false;
        image.type = Image.Type.Simple;
        image.sprite = arte != null ? arte.borda : null;
        image.color = new Color(0.24f, 0.19f, 0.14f, 0.9f);
        image.enabled = image.sprite != null;
    }

    /// <summary>Caixa com a moldura do kit — cópia local do auxiliar privado do setup.</summary>
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

    static Image GarantirImagem(GameObject alvo)
    {
        var image = alvo.GetComponent<Image>();
        if (image == null) image = Undo.AddComponent<Image>(alvo);
        return image;
    }
}
#endif
