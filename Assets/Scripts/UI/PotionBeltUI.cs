using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Os frascos que o grupo levou, à mão durante a luta.
///
/// <b>Todos numa faixa só, com o nome do dono.</b> A poção pertence ao herói que
/// a carrega, mas espalhar um frasco por figura obrigaria o jogador a procurar
/// quatro cantos da tela no meio de um turno. Aqui é uma lista: o que existe,
/// de quem é, e o que faz.
///
/// <b>Beber não custa energia nem gasta o turno</b> — decisão do autor, a mesma
/// do Slay the Spire. Por isso a faixa fica perto da mão de cartas e não junto
/// dos botões de turno: é uma jogada a mais, não o fim de uma.
///
/// Montada por código dentro do painel de combate, como o resto do que nasceu
/// depois da cena.
/// </summary>
public class PotionBeltUI : MonoBehaviour
{
    const float AlturaDoFrasco = 30f;
    const float Largura = 330f;

    static readonly Color FundoDoFrasco = new Color(0.16f, 0.15f, 0.17f, 0.9f);
    static readonly Color CorDoTexto = new Color(0.85f, 0.82f, 0.74f);
    static readonly Color CorDoTitulo = new Color(0.62f, 0.59f, 0.52f);

    /// <summary>Para o teste: quantos frascos estão clicáveis agora.</summary>
    public int Frascos { get; private set; }

    public static PotionBeltUI Montar(GameObject painelDeCombate)
    {
        if (painelDeCombate == null) return null;

        PotionBeltUI existente = painelDeCombate.GetComponentInChildren<PotionBeltUI>(true);
        if (existente != null) return existente;

        var go = new GameObject("PotionBelt", typeof(RectTransform));
        go.transform.SetParent(painelDeCombate.transform, false);

        var rt = go.GetComponent<RectTransform>();

        // Canto inferior esquerdo, acima dos contadores de baralho e descarte.
        // A mão ocupa o meio; a esquerda estava vazia.
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(20f, 60f);
        rt.sizeDelta = new Vector2(Largura, 200f);

        rt.SetAsLastSibling();

        return go.AddComponent<PotionBeltUI>();
    }

    /// <summary>
    /// Redesenha a faixa com o que o grupo carrega agora.
    ///
    /// Chamada a cada refresh do combate: beber um frasco muda a lista, e quem
    /// morre leva os dele embora no mesmo instante.
    /// </summary>
    public void Desenhar(IList<HeroData> party)
    {
        UIUtil.ClearChildrenNow(transform);
        Frascos = 0;

        if (party == null) return;

        // Conta antes de desenhar: sem frasco nenhum a faixa some inteira, em vez
        // de deixar um título solto ocupando o canto da tela a luta toda.
        var linhas = new List<(HeroData dono, string id, PotionDef def)>();

        foreach (var heroi in party)
        {
            if (heroi == null || !heroi.IsAlive || heroi.potions == null) continue;

            foreach (string id in heroi.potions)
            {
                PotionDef def = ItemCatalog.Pocao(id);
                if (def != null) linhas.Add((heroi, id, def));
            }
        }

        if (linhas.Count == 0) return;

        float y = 0f;
        y = Titulo(y, "FRASCOS  (não custam energia)");

        foreach (var linha in linhas)
        {
            HeroData dono = linha.dono;
            string id = linha.id;

            Frasco(y, $"{linha.def.nome} · {dono.heroName}", id, () =>
            {
                if (CombatManager.Instance != null)
                    CombatManager.Instance.UsarPocao(dono, id);
            });

            y += AlturaDoFrasco;
            Frascos++;
        }
    }

    float Titulo(float y, string texto)
    {
        var go = new GameObject("Titulo", typeof(RectTransform));
        go.transform.SetParent(transform, false);

        Ancorar(go.GetComponent<RectTransform>(), y, 22f);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = texto;
        tmp.fontSize = 12f;
        tmp.color = CorDoTitulo;
        tmp.alignment = TextAlignmentOptions.BottomLeft;
        tmp.raycastTarget = false;

        return y + 22f;
    }

    void Frasco(float y, string texto, string idDoItem, System.Action aoClicar)
    {
        var go = new GameObject("Frasco", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(transform, false);

        Ancorar(go.GetComponent<RectTransform>(), y, AlturaDoFrasco - 3f);

        var fundo = go.GetComponent<Image>();
        fundo.color = FundoDoFrasco;

        // No meio de um turno, o desenho do frasco é o que se acha primeiro —
        // a linha de texto é para confirmar, não para procurar.
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
            iconeRt.sizeDelta = new Vector2(24f, 24f);

            var iconeImg = iconeGo.GetComponent<Image>();
            iconeImg.sprite = icone;
            iconeImg.preserveAspect = true;
            iconeImg.raycastTarget = false;

            recuo = 36f;
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
        tmp.color = CorDoTexto;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;

        var botao = go.AddComponent<Button>();
        botao.targetGraphic = fundo;
        botao.onClick.AddListener(() => aoClicar());
    }

    /// <summary>Empilha de cima para baixo, dentro da faixa.</summary>
    void Ancorar(RectTransform rt, float y, float altura)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(0f, -(y + altura));
        rt.offsetMax = new Vector2(0f, -y);
    }
}
