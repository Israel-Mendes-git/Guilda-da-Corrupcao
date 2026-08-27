#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Monta o Mercado na cena. Última das sete salas a sair da lista.
///
/// <b>Por que a sala deixou de ser uma lista.</b> Dez linhas com um "COMPRAR"
/// cada, sobre 60% de tela vazia, e o item mais caro da prateleira com o mesmo
/// peso visual da ração de 8 de ouro. Pior: o efeito da compra não aparecia. O
/// vinho aliviava 18 de estresse e a única prova disso era uma frase no rodapé,
/// já com o ouro gasto.
///
/// Agora são três faixas, no molde da Forja: a <b>carroça</b> à esquerda, com o
/// que o mercador trouxe nesta volta; o <b>balcão</b> no meio, com um item de
/// cada vez e o botão que cobra; e à direita <b>em quem a compra pega</b> — o
/// herói que seria atendido, com a barra do antes e do depois, ou o estoque que
/// a próxima jornada vai levar, ou o que já está guardado na prateleira.
///
/// <b>O que este arquivo faz e o manager não.</b> Tudo o que precisa de
/// <c>AssetDatabase</c>: as molduras de pedra do kit em volta do balcão e da
/// faixa do efeito. As fichas e as barras nascem em execução, no
/// <see cref="MarketManager"/>, porque mudam a cada compra.
///
/// <b>Sem cenário de mercado.</b> A Forja ganhou a silhueta de caverna porque o
/// sprite existia no projeto, e o Cemitério, a linha de árvores. Não há arte de
/// feira aqui, e pintar barracas com retângulos foi o erro que a própria Forja
/// cometeu e desfez.
/// </summary>
internal static class MarketRoom
{
    const string KitRoot = "Assets/Alebardium/Bloodlines UI/";
    const string PanelSpritePath = KitRoot + "Textures/Frame/Frame_background.png";

    /// <summary>O objeto que hospeda o manager, fora de qualquer painel.</summary>
    const string HostName = "MarketManager";

    internal static GameObject Montar(Canvas canvas)
    {
        GameObject panel = GuildSceneSetup.FindOrCreatePanel(canvas, "Panel_Market");

        LimparMontagemAntiga(panel);

        // ── Cabeçalho ─────────────────────────────────────────────────────────
        GuildSceneSetup.EnsureText(panel.transform, "Txt_Title", "🛒 Mercado", 32,
            new Vector2(0, 1), new Vector2(0.7f, 1), new Vector2(20, -70), new Vector2(0, -20));

        var gold = GuildSceneSetup.EnsureText(panel.transform, "Txt_Gold", "💰 0", 26,
            new Vector2(0.7f, 1), new Vector2(1, 1), new Vector2(0, -70), new Vector2(-20, -20));

        var hint = GuildSceneSetup.EnsureText(panel.transform, "Txt_Hint", "", 18,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -112), new Vector2(-20, -76));
        hint.color = GuildSceneSetup.SubtleTextColor;

        // ── A carroça ─────────────────────────────────────────────────────────
        GameObject cart = GuildSceneSetup.EnsureScrollColumn(panel.transform, "Cart",
            new Vector2(0, 0), new Vector2(0, 1), new Vector2(20, 110), new Vector2(360, -140), 8);

        AjustarColuna(cart);

        // ── O balcão ──────────────────────────────────────────────────────────
        GameObject counter = GuildSceneSetup.EnsureFreeArea(panel.transform, "Counter",
            new Vector2(0, 0), new Vector2(0, 1), new Vector2(380, 110), new Vector2(1060, -140));
        VestirCaixa(counter, new Color(0.16f, 0.14f, 0.12f, 0.94f));

        // O bloco do item é ancorado ao <b>centro</b> do balcão, e não ao topo:
        // preso em cima, ele deixava 400px de caixa preta entre a descrição e o
        // botão — a mesma tela vazia que a sala existia para consertar.
        //
        // Sprite e emoji dividem o mesmo retângulo porque nunca aparecem juntos:
        // só frascos e relíquias têm arte em Resources/ItemIcons, e o manager
        // desliga um dos dois.
        GameObject iconGo = GuildSceneSetup.EnsureFreeArea(counter.transform, "Img_Icon",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-110, 40), new Vector2(110, 260));
        var icon = GarantirImagem(iconGo);
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.enabled = false;

        var emoji = GuildSceneSetup.EnsureText(counter.transform, "Txt_Emoji", "", 120,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-110, 40), new Vector2(110, 260));
        emoji.alignment = TextAlignmentOptions.Center;

        var name = GuildSceneSetup.EnsureText(counter.transform, "Txt_Name", "", 30,
            new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(16, -20), new Vector2(-16, 26));
        name.alignment = TextAlignmentOptions.Center;

        var desc = GuildSceneSetup.EnsureText(counter.transform, "Txt_Desc", "", 19,
            new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(24, -140), new Vector2(-24, -30));
        desc.alignment = TextAlignmentOptions.Top;
        desc.enableWordWrapping = true;

        // O estoque da próxima jornada vivia espremido no cabeçalho, ao lado do
        // ouro, e ninguém o ligava à ração que acabara de comprar. Aqui ele fica
        // no balcão, logo acima do botão que o alimenta.
        var stock = GuildSceneSetup.EnsureText(counter.transform, "Txt_Stock", "", 19,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(16, 92), new Vector2(-16, 132));
        stock.alignment = TextAlignmentOptions.Center;
        stock.color = GuildSceneSetup.SubtleTextColor;

        var buy = GuildSceneSetup.EnsureButton(panel.transform, "Btn_Buy", "COMPRAR",
            new Vector2(0, 0), new Vector2(0, 0), new Vector2(420, 130), new Vector2(1020, 186));

        // ── Em quem a compra pega ─────────────────────────────────────────────
        var effectTitle = GuildSceneSetup.EnsureText(panel.transform, "Txt_EffectTitle", "", 19,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(1090, -190), new Vector2(-20, -140));
        effectTitle.alignment = TextAlignmentOptions.Center;

        GameObject effect = GuildSceneSetup.EnsureScrollColumn(panel.transform, "Effect",
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(1090, 110), new Vector2(-20, -200), 8);

        AjustarColuna(effect);

        // ── Rodapé ────────────────────────────────────────────────────────────
        var feedback = GuildSceneSetup.EnsureText(panel.transform, "Txt_Feedback", "", 18,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(260, 40), new Vector2(-20, 96));

        var close = GuildSceneSetup.EnsureButton(panel.transform, "Btn_Close", "Voltar",
            new Vector2(0, 0), new Vector2(0, 0), new Vector2(20, 20), new Vector2(220, 60));

        // ── As referências ────────────────────────────────────────────────────
        MarketManager market = GarantirManager();

        Undo.RecordObject(market, "Montar Cena");
        market.goldText = gold;
        market.hintText = hint;
        market.itemContainer = cart.transform;
        market.counterRoot = counter;
        market.counterIcon = icon;
        market.counterEmoji = emoji;
        market.counterName = name;
        market.counterDesc = desc;
        market.counterStock = stock;
        market.buyButton = buy;
        market.effectTitle = effectTitle;
        market.effectContainer = effect.transform;
        market.feedbackText = feedback;
        market.closeButton = close;
        EditorUtility.SetDirty(market);

        panel.SetActive(false);
        return panel;
    }

    /// <summary>
    /// A prateleira antiga, com as dez linhas e o rótulo de estoque do cabeçalho.
    /// Sem apagar, ela continuaria na cena por baixo da montagem nova — o
    /// <c>MarketManager</c> não a preencheria mais, e sobraria uma coluna vazia
    /// segurando cliques no meio da carroça.
    /// </summary>
    static void LimparMontagemAntiga(GameObject panel)
    {
        foreach (string nome in new[] { "List", "Txt_Stock" })
        {
            Transform velho = panel.transform.Find(nome);
            if (velho != null) Undo.DestroyObjectImmediate(velho.gameObject);
        }
    }

    /// <summary>
    /// O manager num objeto próprio, e um só na cena.
    ///
    /// <c>FindObjectOfType</c> não serve aqui: ele ignora o que está desligado, e
    /// é num painel desligado que o componente herdado mora — o Mercado nasceu
    /// com o manager dentro do próprio painel, que só liga quando o jogador abre
    /// a sala. Até lá, <c>Instance</c> era nulo.
    /// </summary>
    static MarketManager GarantirManager()
    {
        var naCena = Resources.FindObjectsOfTypeAll<MarketManager>()
            .Where(m => m != null && !EditorUtility.IsPersistent(m) && m.gameObject.scene.IsValid())
            .ToList();

        MarketManager escolhido = naCena.FirstOrDefault(m => m.transform.parent == null
                                                          && m.gameObject.name == HostName);

        if (escolhido == null)
        {
            GameObject host = GameObject.Find(HostName);
            if (host == null)
            {
                host = new GameObject(HostName);
                Undo.RegisterCreatedObjectUndo(host, "Criar MarketManager");
            }

            escolhido = host.GetComponent<MarketManager>();
            if (escolhido == null) escolhido = Undo.AddComponent<MarketManager>(host);
        }

        foreach (MarketManager herdado in naCena)
        {
            if (herdado == escolhido) continue;
            Undo.DestroyObjectImmediate(herdado);
        }

        return escolhido;
    }

    /// <summary>
    /// As colunas nascem com o layout largo do <c>EnsureColumn</c>, que deixa cada
    /// filho com a largura que ele pediu. Aqui as fichas precisam ocupar a coluna
    /// inteira, senão a carroça vira uma pilha de retângulos de tamanhos
    /// diferentes.
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
    /// para esta sala mexeria num arquivo que as outras seis também usam.
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
