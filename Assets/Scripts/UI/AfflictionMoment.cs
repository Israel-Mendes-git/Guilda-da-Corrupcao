using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// O instante em que um herói quebra.
///
/// <b>O que havia.</b> O estresse chegava a 100, o <see cref="MentalState"/>
/// mudava e o rótulo do herói trocava de cor numa lista. A quebra é a coisa mais
/// dramática que acontece numa jornada e passava como uma linha de log —
/// <c>EventResolver</c> chegava a montar a lista <c>newlyAfflicted</c>, que
/// <b>nunca era lida por ninguém</b>: o dado certo, a exibição ausente.
///
/// <b>O que é agora.</b> A estrada para. A tela escurece, o rosto de quem quebrou
/// ocupa o meio dela e a aflição é dita pelo nome. Dura menos de dois segundos —
/// o suficiente para o jogador saber a quem aconteceu, e não tanto que atrapalhe
/// quem já viu vinte vezes.
///
/// <b>Montado e destruído a cada vez.</b> Não há prefab, e não há painel dormindo
/// na cena: o momento é raro e curto, e um painel guardado seria mais uma coisa
/// para nascer desligada e não aparecer.
/// </summary>
public static class AfflictionMoment
{
    /// <summary>Acima do véu da tela, que é 1000 — nada fica na frente disto.</summary>
    const int Ordem = 1100;

    const float DuracaoDaEntrada = 0.18f;
    const float DuracaoNoAr = 1.5f;
    const float DuracaoDaSaida = 0.35f;

    static readonly Color Fundo = new Color(0.03f, 0.02f, 0.02f, 0.94f);
    static readonly Color Creme = new Color(0.97f, 0.94f, 0.86f);
    static readonly Color Sangue = new Color(0.69f, 0.25f, 0.25f);

    /// <summary>
    /// Mostra um momento por herói que acabou de quebrar, em sequência.
    ///
    /// Em sequência, e não empilhados: dois heróis quebrando no mesmo evento é
    /// raro, e quando acontece cada um merece o seu instante — sobrepostos, não
    /// se leria nenhum dos dois.
    /// </summary>
    public static IEnumerator MostrarTodos(List<HeroData> quebrados)
    {
        if (quebrados == null) yield break;

        foreach (HeroData heroi in quebrados)
        {
            if (heroi == null) continue;
            yield return Mostrar(heroi);
        }
    }

    public static IEnumerator Mostrar(HeroData heroi)
    {
        if (heroi == null) yield break;

        Canvas canvas = UIUtil.CanvasPrincipal();
        if (canvas == null) yield break;

        GameObject raiz = Montar(canvas, heroi);
        var grupo = raiz.GetComponent<CanvasGroup>();

        yield return Desvanecer(grupo, 0f, 1f, DuracaoDaEntrada);
        yield return new WaitForSecondsRealtime(DuracaoNoAr);
        yield return Desvanecer(grupo, 1f, 0f, DuracaoDaSaida);

        Object.Destroy(raiz);
    }

    static IEnumerator Desvanecer(CanvasGroup grupo, float de, float para, float duracao)
    {
        if (grupo == null) yield break;

        for (float t = 0f; t < duracao; t += Time.unscaledDeltaTime)
        {
            grupo.alpha = Mathf.Lerp(de, para, t / duracao);
            yield return null;
        }

        grupo.alpha = para;
    }

    static GameObject Montar(Canvas canvas, HeroData heroi)
    {
        var raiz = new GameObject("AfflictionMoment",
            typeof(RectTransform), typeof(CanvasGroup), typeof(Canvas));

        raiz.transform.SetParent(canvas.transform, false);
        raiz.transform.SetAsLastSibling();
        Esticar(raiz.GetComponent<RectTransform>());

        var proprio = raiz.GetComponent<Canvas>();
        proprio.overrideSorting = true;
        proprio.sortingOrder = Ordem;

        var grupo = raiz.GetComponent<CanvasGroup>();
        grupo.alpha = 0f;

        // Não bloqueia clique porque não há o que clicar aqui, e um véu opaco que
        // engolisse o clique deixaria o jogador achando que a tela travou caso a
        // corrotina morresse no meio.
        grupo.blocksRaycasts = false;
        grupo.interactable = false;

        Fundir(raiz.transform);

        if (heroi.portrait != null)
            Retrato(raiz.transform, heroi.portrait);

        Texto(raiz.transform, "Txt_Nome", heroi.heroName, 54, Creme,
              new Vector2(0.5f, 0.5f), new Vector2(0, -170), new Vector2(900, 70));

        Texto(raiz.transform, "Txt_Aflicao", MentalStateUtil.GetLabel(heroi.mentalState).ToUpperInvariant(),
              38, Sangue, new Vector2(0.5f, 0.5f), new Vector2(0, -232), new Vector2(900, 54));

        Texto(raiz.transform, "Txt_Linha", "a mente cedeu na estrada", 22,
              new Color(0.72f, 0.68f, 0.62f),
              new Vector2(0.5f, 0.5f), new Vector2(0, -284), new Vector2(900, 40));

        return raiz;
    }

    static void Fundir(Transform pai)
    {
        var go = new GameObject("Fundo", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(pai, false);
        Esticar(go.GetComponent<RectTransform>());

        var img = go.GetComponent<Image>();
        img.color = Fundo;
        img.raycastTarget = false;
    }

    static void Retrato(Transform pai, Sprite sprite)
    {
        var go = new GameObject("Img_Retrato", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(pai, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0, 60);
        rt.sizeDelta = new Vector2(280, 280);

        var img = go.GetComponent<Image>();
        img.sprite = sprite;

        // preserveAspect encaixa o quadro inteiro, transparência incluída — que
        // é o que se quer aqui: o retrato é pixel art com margem própria.
        img.preserveAspect = true;
        img.raycastTarget = false;
    }

    static void Texto(Transform pai, string nome, string conteudo, int corpo, Color cor,
                      Vector2 ancora, Vector2 posicao, Vector2 tamanho)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        go.transform.SetParent(pai, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = ancora;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = posicao;
        rt.sizeDelta = tamanho;

        var texto = go.AddComponent<TextMeshProUGUI>();
        texto.text = conteudo;
        texto.fontSize = corpo;
        texto.color = cor;
        texto.alignment = TextAlignmentOptions.Center;
        texto.enableAutoSizing = false;
        texto.raycastTarget = false;
    }

    static void Esticar(RectTransform rt)
    {
        if (rt == null) return;

        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }
}
