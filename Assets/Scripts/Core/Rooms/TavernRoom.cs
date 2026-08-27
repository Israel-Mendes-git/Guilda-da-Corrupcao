#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Monta a Taverna na cena e liga as referências do <see cref="TavernManager"/>.
///
/// É idempotente como o resto do <see cref="GuildSceneSetup"/>: rodar de novo
/// reaproveita o que já existe.
///
/// <b>Três faixas em vez de três cards.</b> A taverna era uma vitrine de fichas
/// iguais no meio da tela, com a metade esquerda vazia. Agora ela segue o desenho
/// da Forja: a <b>fila</b> à esquerda, com quem espera para ser chamado; a
/// <b>mesa</b> no meio, com um candidato de cada vez, o rosto grande e o botão que
/// cobra; e à direita <b>o que ele traz</b> — as cartas que vão para o baralho e a
/// comparação com quem já está no quadro. A contratação é a decisão de mais peso
/// da guilda, e era a que a tela mostrava com menos informação.
///
/// <b>Por que o manager não mora no painel.</b> O <see cref="TavernManager"/>
/// vivia no painel antigo da taverna, que nasce desligado — o <c>Awake</c> não
/// rodava e <c>Instance</c> ficava nulo até alguém abrir a sala. O componente
/// passa a morar num objeto próprio, sempre ativo, como o da Forja, e o que
/// sobrou no painel velho é removido: dois na cena e o <c>Awake</c> que ganha a
/// corrida pode ser o do painel antigo, com todos os campos nulos.
///
/// <b>Sem cenário de taverna.</b> A Forja ganhou a silhueta de caverna porque o
/// sprite existia no projeto; não há arte de taberna aqui, e vestir a sala com
/// uma caverna seria pior do que não vestir. O que separa a sala do resto é a
/// madeira quente da mesa, e o fundo fica esperando arte.
/// </summary>
internal static class TavernRoom
{
    const string CardPrefabPath = "Assets/Prefabs/UI/CardPrefab.prefab";
    const string PanelSpritePath = "Assets/Alebardium/Bloodlines UI/Textures/Frame/Frame_background.png";

    /// <summary>O objeto que hospeda o manager, fora de qualquer painel.</summary>
    const string HostName = "TavernManager";

    /// <summary>
    /// A madeira à luz de vela. Precisa destoar do marrom frio da bancada da
    /// Forja — se as duas salas usassem a mesma caixa, continuariam sendo a mesma
    /// tela com rótulos diferentes, que é a queixa de origem.
    /// </summary>
    static readonly Color MesaColor = new Color(0.20f, 0.15f, 0.10f, 0.94f);

    internal static GameObject Montar(Canvas canvas)
    {
        GameObject panel = GuildSceneSetup.FindOrCreatePanel(canvas, "Panel_Tavern");

        GuildSceneSetup.EnsureText(panel.transform, "Txt_Title", "🍺 Taverna", 32,
            new Vector2(0, 1), new Vector2(0.7f, 1), new Vector2(20, -70), new Vector2(0, -20));

        var gold = GuildSceneSetup.EnsureText(panel.transform, "Txt_Gold", "💰 0", 26,
            new Vector2(0.7f, 1), new Vector2(1, 1), new Vector2(0, -70), new Vector2(-20, -20));

        var hint = GuildSceneSetup.EnsureText(panel.transform, "Txt_Hint", "", 18,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -112), new Vector2(-20, -76));

        // ── A fila ────────────────────────────────────────────────────────────
        GameObject fila = GuildSceneSetup.EnsureScrollColumn(panel.transform, "Candidatos",
            new Vector2(0, 0), new Vector2(0, 1), new Vector2(20, 190), new Vector2(360, -140), 8);

        var filaLayout = fila.GetComponent<VerticalLayoutGroup>();
        if (filaLayout != null)
        {
            filaLayout.childControlWidth = true;
            filaLayout.childForceExpandWidth = true;
            filaLayout.childControlHeight = true;
            filaLayout.childForceExpandHeight = false;
            filaLayout.childAlignment = TextAnchor.UpperCenter;
        }

        // Renovar a leva fica embaixo da fila, que é o que ele troca. No canto
        // direito, como estava, o botão parecia falar dos cards do meio.
        var refreshCost = GuildSceneSetup.EnsureText(panel.transform, "Txt_RefreshCost", "", 20,
            new Vector2(0, 0), new Vector2(0, 0), new Vector2(20, 140), new Vector2(360, 178));
        refreshCost.alignment = TextAlignmentOptions.Center;

        Button refresh = GuildSceneSetup.EnsureButton(panel.transform, "Btn_Refresh", "NOVOS CANDIDATOS",
            new Vector2(0, 0), new Vector2(0, 0), new Vector2(20, 78), new Vector2(360, 132));

        // ── A mesa ────────────────────────────────────────────────────────────
        GameObject mesa = GuildSceneSetup.EnsureFreeArea(panel.transform, "Table",
            new Vector2(0, 0), new Vector2(0, 1), new Vector2(380, 110), new Vector2(1060, -140));
        VestirCaixa(mesa, MesaColor);

        GameObject portraitGo = GuildSceneSetup.EnsureFreeArea(mesa.transform, "Img_Portrait",
            new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-130, -290), new Vector2(130, -30));
        var portrait = GarantirImagem(portraitGo);
        portrait.preserveAspect = true;
        portrait.raycastTarget = false;

        var heroName = GuildSceneSetup.EnsureText(mesa.transform, "Txt_HeroName", "", 26,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -344), new Vector2(-16, -296));
        heroName.alignment = TextAlignmentOptions.Center;

        var heroStats = GuildSceneSetup.EnsureText(mesa.transform, "Txt_HeroStats", "", 19,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -388), new Vector2(-16, -346));
        heroStats.alignment = TextAlignmentOptions.Center;

        // Duas linhas de bagagem, com o efeito escrito ao lado do rótulo: por isso
        // a caixa é alta e quebra linha.
        var heroTraits = GuildSceneSetup.EnsureText(mesa.transform, "Txt_HeroTraits", "", 18,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -500), new Vector2(-24, -396));
        heroTraits.alignment = TextAlignmentOptions.Top;
        heroTraits.enableWordWrapping = true;

        var hireEffect = GuildSceneSetup.EnsureText(mesa.transform, "Txt_HireEffect", "", 17,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(20, 124), new Vector2(-20, 196));
        hireEffect.alignment = TextAlignmentOptions.Bottom;
        hireEffect.enableWordWrapping = true;
        hireEffect.color = GuildSceneSetup.SubtleTextColor;

        Button hire = GuildSceneSetup.EnsureButton(mesa.transform, "Btn_Hire", "🍺 CONTRATAR",
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(30, 44), new Vector2(-30, 116));

        var hireLabel = hire.GetComponentInChildren<TMP_Text>();
        if (hireLabel != null) hireLabel.fontSize = 22;

        // ── O que ele traz ────────────────────────────────────────────────────
        var shelfTitle = GuildSceneSetup.EnsureText(panel.transform, "Txt_Shelf", "", 19,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(1090, -190), new Vector2(-20, -140));
        shelfTitle.alignment = TextAlignmentOptions.Center;

        var compare = GuildSceneSetup.EnsureText(panel.transform, "Txt_Compare", "", 18,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(1090, -290), new Vector2(-20, -196));
        compare.alignment = TextAlignmentOptions.Top;
        compare.enableWordWrapping = true;
        compare.color = GuildSceneSetup.SubtleTextColor;

        GameObject shelf = GuildSceneSetup.EnsureFreeArea(panel.transform, "Shelf",
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(1090, 110), new Vector2(-20, -300));

        // ── Rodapé ────────────────────────────────────────────────────────────
        // O retorno da sala começa depois da fila: no canto esquerdo ele ficaria
        // debaixo do botão de renovar.
        var feedback = GuildSceneSetup.EnsureText(panel.transform, "Txt_Feedback", "", 18,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(380, 40), new Vector2(-20, 96));

        var close = GuildSceneSetup.EnsureButton(panel.transform, "Btn_Close", "Voltar",
            new Vector2(0, 0), new Vector2(0, 0), new Vector2(20, 20), new Vector2(220, 60));

        TavernManager tavern = GarantirManager();

        Undo.RecordObject(tavern, "Montar Cena");
        tavern.goldText = gold;
        tavern.hintText = hint;
        tavern.feedbackText = feedback;
        tavern.recruitContainer = fila.transform;
        tavern.refreshButton = refresh;
        tavern.refreshCostText = refreshCost;
        tavern.closeButton = close;
        tavern.tableRoot = mesa;
        tavern.portraitImage = portrait;
        tavern.heroNameText = heroName;
        tavern.heroStatsText = heroStats;
        tavern.heroTraitsText = heroTraits;
        tavern.hireEffectText = hireEffect;
        tavern.hireButton = hire;
        tavern.cardShelfTitle = shelfTitle;
        tavern.compareText = compare;
        tavern.cardShelf = shelf.transform;
        tavern.cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
        EditorUtility.SetDirty(tavern);

        panel.SetActive(false);
        return panel;
    }

    /// <summary>
    /// O manager num objeto próprio, e um só na cena.
    ///
    /// <c>FindObjectOfType</c> não serve aqui: ele ignora o que está desligado, e
    /// é exatamente num painel desligado que o componente herdado mora.
    /// </summary>
    static TavernManager GarantirManager()
    {
        var naCena = Resources.FindObjectsOfTypeAll<TavernManager>()
            .Where(m => m != null && !EditorUtility.IsPersistent(m) && m.gameObject.scene.IsValid())
            .ToList();

        TavernManager escolhido = naCena.FirstOrDefault(m => m.transform.parent == null
                                                          && m.gameObject.name == HostName);

        if (escolhido == null)
        {
            GameObject host = GameObject.Find(HostName);
            if (host == null)
            {
                host = new GameObject(HostName);
                Undo.RegisterCreatedObjectUndo(host, "Criar TavernManager");
            }

            escolhido = host.GetComponent<TavernManager>();
            if (escolhido == null) escolhido = Undo.AddComponent<TavernManager>(host);
        }

        // O GameManager guarda uma referência ao manager antigo e a usa para
        // renovar a leva no fim da jornada. Repor antes de apagar o herdado evita
        // trocar um bug silencioso por outro.
        GameManager jogo = Object.FindObjectOfType<GameManager>();
        if (jogo != null && jogo.tavernManager != escolhido)
        {
            Undo.RecordObject(jogo, "Montar Cena");
            jogo.tavernManager = escolhido;
            EditorUtility.SetDirty(jogo);
        }

        foreach (TavernManager herdado in naCena)
        {
            if (herdado == escolhido) continue;
            Undo.DestroyObjectImmediate(herdado);
        }

        return escolhido;
    }

    /// <summary>
    /// Caixa com a moldura do kit, para separar a mesa do fundo. Cópia local: a
    /// do <see cref="GuildSceneSetup"/> é privada, e abrir a visibilidade dela
    /// mexeria num arquivo que está em uso por outra sala.
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

    static Image GarantirImagem(GameObject alvo)
    {
        var image = alvo.GetComponent<Image>();
        if (image == null) image = Undo.AddComponent<Image>(alvo);
        return image;
    }
}
#endif
