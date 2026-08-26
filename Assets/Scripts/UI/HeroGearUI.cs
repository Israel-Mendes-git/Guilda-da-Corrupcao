using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Onde o jogador põe relíquia e frasco num herói: o rodapé da ficha dele.
///
/// <b>Por que na ficha, e não numa sala.</b> A ficha é a única tela que mostra o
/// herói inteiro — nível, estresse, traço, ferimento. Equipar é decidir <i>quem
/// precisa do quê</i>, e essa decisão se toma olhando exatamente esses números.
/// Numa sala à parte o jogador escolheria no escuro e voltaria à ficha para
/// conferir.
///
/// <b>Duas listas, uma regra só:</b> em cima o que o herói carrega, embaixo o que
/// está na prateleira da guilda. Clicar move de uma para a outra. Sem arrastar,
/// sem menu de contexto e sem confirmação — mover não custa nada e é reversível
/// enquanto se está na guilda.
///
/// Monta-se por código, como o <see cref="RegionMapUI"/> e pela mesma razão: a
/// cena é grande e montada por ferramenta de Editor, e um painel novo dependendo
/// de hierarquia exata seria mais uma coisa para sair de sincronia.
/// </summary>
public class HeroGearUI : MonoBehaviour
{
    const float AlturaDaSecao = 26f;
    const float AlturaDoItem = 30f;

    static readonly Color CorTitulo = new Color(0.72f, 0.68f, 0.58f);
    static readonly Color CorEquipado = new Color(0.85f, 0.72f, 0.42f);
    static readonly Color CorPrateleira = new Color(0.78f, 0.76f, 0.72f);
    static readonly Color CorVazio = new Color(0.45f, 0.42f, 0.38f);
    static readonly Color FundoDoItem = new Color(0.16f, 0.15f, 0.17f, 0.85f);

    RectTransform area;
    HeroData heroi;

    /// <summary>Para o teste: quantas linhas clicáveis a ficha oferece agora.</summary>
    public int LinhasClicaveis { get; private set; }

    /// <summary>
    /// Encaixa a seção no rodapé do painel da ficha, ou devolve a que já existe.
    /// </summary>
    public static HeroGearUI Montar(GameObject painelDaFicha)
    {
        if (painelDaFicha == null) return null;

        HeroGearUI existente = painelDaFicha.GetComponentInChildren<HeroGearUI>(true);
        if (existente != null) return existente;

        var go = new GameObject("HeroGear", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(painelDaFicha.transform, false);

        var rt = go.GetComponent<RectTransform>();

        // <b>Ao lado da ficha, e não dentro dela.</b> A ficha é uma barra lateral
        // estreita, preenchida do topo ao botão de retirar: encaixada no rodapé,
        // esta seção nasceu **por cima** de lealdade, moral e estresse, e a
        // captura mostrou dois textos ocupando a mesma linha. É a armadilha de
        // ordem de irmãos do projeto, agora pelo avesso — o certo era não
        // disputar o espaço.
        // Preso ao topo e com altura própria: esticado até o rodapé da ficha, o
        // painel ficava com dois terços de vazio embaixo de três linhas de item.
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(10f, -40f);
        rt.sizeDelta = new Vector2(460f, 200f);

        var fundo = go.GetComponent<Image>();
        fundo.color = new Color(0.09f, 0.085f, 0.10f, 0.94f);
        fundo.raycastTarget = true;   // segura o clique que passaria para a sala atrás

        var gear = go.AddComponent<HeroGearUI>();
        gear.area = rt;
        return gear;
    }

    /// <summary>Redesenha para este herói. Null limpa a seção.</summary>
    public void Desenhar(HeroData hero)
    {
        heroi = hero;
        LinhasClicaveis = 0;

        UIUtil.ClearChildrenNow(transform);
        if (heroi == null || area == null) return;

        var guilda = GuildManager.Instance;
        float y = 0f;

        // --- O que o herói carrega -------------------------------------------
        y = Secao(y, $"RELÍQUIAS ({Contar(heroi.relics)}/{ItemCatalog.SlotsDeReliquia})");

        for (int i = 0; i < ItemCatalog.SlotsDeReliquia; i++)
        {
            string id = heroi.relics != null && i < heroi.relics.Count ? heroi.relics[i] : null;
            RelicDef def = ItemCatalog.Reliquia(id);

            if (def == null)
            {
                y = Linha(y, "— slot vazio —", CorVazio, null);
                continue;
            }

            string capturado = id;
            y = Linha(y, $"{def.nome} — {def.descricao}", CorEquipado, () =>
            {
                guilda?.DesequiparReliquia(heroi, capturado);
                Desenhar(heroi);
            }, id);
        }

        y = Secao(y, $"FRASCOS NA MOCHILA ({Contar(heroi.potions)})");

        if (Contar(heroi.potions) == 0)
        {
            y = Linha(y, "— nenhum —", CorVazio, null);
        }
        else
        {
            // Cópia: devolver um frasco mexe na lista original no meio do laço.
            foreach (string id in new List<string>(heroi.potions))
            {
                PotionDef def = ItemCatalog.Pocao(id);
                if (def == null) continue;

                string capturado = id;
                y = Linha(y, $"{def.nome} — {def.descricao}", CorEquipado, () =>
                {
                    guilda?.RecolherPocao(heroi, capturado);
                    Desenhar(heroi);
                }, id);
            }
        }

        // --- O que está guardado ---------------------------------------------
        int naPrateleira = (guilda != null ? Contar(guilda.relicStock) + Contar(guilda.potionStock) : 0);
        y = Secao(y, $"PRATELEIRA DA GUILDA ({naPrateleira})");

        if (guilda == null || naPrateleira == 0)
        {
            y = Linha(y, "— vazia. Espólio de chefe, despojo e Mercado enchem daqui —", CorVazio, null);
            Ajustar(y);
            return;
        }

        bool slotsCheios = Contar(heroi.relics) >= ItemCatalog.SlotsDeReliquia;

        foreach (string id in new List<string>(guilda.relicStock))
        {
            RelicDef def = ItemCatalog.Reliquia(id);
            if (def == null) continue;

            string capturado = id;

            // Sem slot livre a linha continua visível, apagada: esconder o que a
            // guilda tem faria parecer que o item sumiu.
            y = Linha(y, $"{def.nome} — {def.descricao}",
                      slotsCheios ? CorVazio : CorPrateleira,
                      slotsCheios ? (System.Action)null : () =>
                      {
                          guilda.EquiparReliquia(heroi, capturado);
                          Desenhar(heroi);
                      },
                      id);
        }

        foreach (string id in new List<string>(guilda.potionStock))
        {
            PotionDef def = ItemCatalog.Pocao(id);
            if (def == null) continue;

            string capturado = id;
            y = Linha(y, $"{def.nome} — {def.descricao}", CorPrateleira, () =>
            {
                guilda.EntregarPocao(heroi, capturado);
                Desenhar(heroi);
            }, id);
        }

        Ajustar(y);
    }

    /// <summary>
    /// Encolhe ou estica o painel até a última linha desenhada.
    ///
    /// A lista muda de tamanho a cada clique — equipar tira uma linha da
    /// prateleira e põe uma no herói —, então a altura não pode ser um número
    /// fixo escolhido na montagem.
    /// </summary>
    void Ajustar(float alturaDoConteudo)
    {
        if (area == null) return;

        area.sizeDelta = new Vector2(area.sizeDelta.x, alturaDoConteudo + 28f);
    }

    static int Contar(List<string> lista) => lista != null ? lista.Count : 0;

    /// <summary>Um título de seção. Devolve a altura já ocupada.</summary>
    float Secao(float y, string titulo)
    {
        var go = new GameObject("Secao", typeof(RectTransform));
        go.transform.SetParent(transform, false);

        var rt = go.GetComponent<RectTransform>();
        Ancorar(rt, y, AlturaDaSecao);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = titulo;
        tmp.fontSize = 13f;
        tmp.color = CorTitulo;
        tmp.alignment = TextAlignmentOptions.BottomLeft;
        tmp.raycastTarget = false;

        return y + AlturaDaSecao;
    }

    /// <summary>
    /// Uma linha de item. Sem ação, é só texto — e sem botão, para o cursor não
    /// prometer um clique que não faz nada.
    /// </summary>
    /// <param name="idDoItem">
    /// Quando informado, a linha ganha o ícone do item à esquerda. Nulo nas
    /// linhas de slot vazio e de título, que não são item nenhum.
    /// </param>
    float Linha(float y, string texto, Color cor, System.Action aoClicar, string idDoItem = null)
    {
        var go = new GameObject("Item", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(transform, false);

        var rt = go.GetComponent<RectTransform>();
        Ancorar(rt, y, AlturaDoItem - 3f);

        var fundo = go.GetComponent<Image>();
        fundo.color = FundoDoItem;
        fundo.raycastTarget = aoClicar != null;

        // O ícone entra à esquerda e empurra o texto: o jogador reconhece o
        // frasco antes de ler o nome dele, que é para isso que a arte serve.
        Sprite icone = ItemCatalog.Icone(idDoItem);
        float recuo = 8f;

        if (icone != null)
        {
            var iconeGo = new GameObject("Icone", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconeGo.transform.SetParent(go.transform, false);

            var iconeRt = iconeGo.GetComponent<RectTransform>();
            iconeRt.anchorMin = new Vector2(0f, 0.5f);
            iconeRt.anchorMax = new Vector2(0f, 0.5f);
            iconeRt.pivot = new Vector2(0f, 0.5f);
            iconeRt.anchoredPosition = new Vector2(6f, 0f);
            iconeRt.sizeDelta = new Vector2(22f, 22f);

            var iconeImg = iconeGo.GetComponent<Image>();
            iconeImg.sprite = icone;
            iconeImg.preserveAspect = true;
            iconeImg.raycastTarget = false;

            recuo = 34f;
        }

        var textoGo = new GameObject("Texto", typeof(RectTransform));
        textoGo.transform.SetParent(go.transform, false);

        var textoRt = textoGo.GetComponent<RectTransform>();
        textoRt.anchorMin = Vector2.zero;
        textoRt.anchorMax = Vector2.one;
        textoRt.offsetMin = new Vector2(recuo, 0f);
        textoRt.offsetMax = new Vector2(-8f, 0f);

        var tmp = textoGo.AddComponent<TextMeshProUGUI>();
        tmp.text = texto;
        tmp.fontSize = 13f;
        tmp.color = cor;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;

        if (aoClicar != null)
        {
            var botao = go.AddComponent<Button>();
            botao.targetGraphic = fundo;
            botao.onClick.AddListener(() => aoClicar());
            LinhasClicaveis++;
        }

        return y + AlturaDoItem;
    }

    /// <summary>Empilha de cima para baixo dentro do painel, com margem.</summary>
    void Ancorar(RectTransform rt, float y, float altura)
    {
        const float margem = 14f;

        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(margem, -(y + altura + margem));
        rt.offsetMax = new Vector2(-margem, -(y + margem));
    }
}
