using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A Corrupção no rodapé da guilda, logo abaixo do ouro.
///
/// É o relógio da partida — um dos três pilares do GDD — e até 12/09 a tela
/// principal não o mostrava: aparecia na pausa e, por área, dentro da Sala de
/// Mapas. O jogador tinha urgência escrita no documento e nenhuma na tela em
/// que passa a maior parte do tempo. Uma linha e uma barra, discretas: o número
/// que faz o mundo acabar fica à vista sem virar painel.
///
/// Nasce em execução, e não na montagem da cena: a cena é montada por
/// ferramenta, e um objeto a mais no setup é mais uma coisa para sair de
/// sincronia. Quem o cria é o <see cref="GuildGuide"/>, que já mora na guilda
/// e acorda junto com ela.
/// </summary>
public class RelogioDaGuilda : MonoBehaviour
{
    const string Nome = "Relogio_Corrupcao";

    static readonly Color CorLimpa = new Color(0.44f, 0.52f, 0.32f);
    static readonly Color CorPodre = new Color(0.66f, 0.18f, 0.15f);
    static readonly Color CorTexto = new Color(0.74f, 0.68f, 0.56f);
    static readonly Color CorTrilho = new Color(0.12f, 0.11f, 0.10f, 0.9f);

    TMP_Text texto;
    Image preenchimento;
    RunManager ligado;

    /// <summary>Cria o relógio no rodapé da guilda, ou devolve o que já existe ali.</summary>
    public static RelogioDaGuilda Garantir(Transform raiz)
    {
        if (raiz == null) return null;

        Transform rodape = raiz.Find("Panel_DownBar");
        if (rodape == null) return null;

        Transform existente = rodape.GetComponentsInChildren<Transform>(true)
                                    .FirstOrDefault(t => t.name == Nome);
        if (existente != null)
        {
            var pronto = existente.GetComponent<RelogioDaGuilda>();
            return pronto != null ? pronto : existente.gameObject.AddComponent<RelogioDaGuilda>();
        }

        // O texto do ouro é a referência: o relógio mora logo abaixo dele, com
        // as mesmas âncoras, para acompanhar o ouro se o rodapé mudar.
        TMP_Text ouro = rodape.GetComponentsInChildren<TMP_Text>(true)
                              .FirstOrDefault(t => t.gameObject.name == "Txt_Gold");

        var go = new GameObject(Nome, typeof(RectTransform));
        go.transform.SetParent(ouro != null ? ouro.transform.parent : rodape, false);

        var rt = go.GetComponent<RectTransform>();
        if (ouro != null)
        {
            RectTransform ort = ouro.rectTransform;
            rt.anchorMin = ort.anchorMin;
            rt.anchorMax = ort.anchorMax;
            rt.pivot = ort.pivot;
            rt.sizeDelta = new Vector2(Mathf.Max(240f, ort.sizeDelta.x), 34f);
            rt.anchoredPosition = ort.anchoredPosition + new Vector2(0f, -38f);
        }
        else
        {
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.sizeDelta = new Vector2(240f, 34f);
            rt.anchoredPosition = new Vector2(24f, 12f);
        }

        var relogio = go.AddComponent<RelogioDaGuilda>();
        relogio.Montar();
        return relogio;
    }

    void Montar()
    {
        var txtGo = new GameObject("Txt", typeof(RectTransform));
        txtGo.transform.SetParent(transform, false);

        texto = txtGo.AddComponent<TextMeshProUGUI>();
        texto.fontSize = 17f;
        texto.color = CorTexto;
        texto.alignment = TextAlignmentOptions.Left;
        texto.enableWordWrapping = false;
        texto.raycastTarget = false;

        var trt = texto.rectTransform;
        trt.anchorMin = new Vector2(0f, 1f);
        trt.anchorMax = new Vector2(1f, 1f);
        trt.pivot = new Vector2(0f, 1f);
        trt.anchoredPosition = Vector2.zero;
        trt.sizeDelta = new Vector2(0f, 22f);

        // A barra usa UIUtil.Branco porque Image.type = Filled é ignorado sem
        // sprite: o preenchimento desenharia o retângulo inteiro, sempre cheio.
        var trilho = new GameObject("Barra", typeof(RectTransform));
        trilho.transform.SetParent(transform, false);

        var trilhoRt = trilho.GetComponent<RectTransform>();
        trilhoRt.anchorMin = trilhoRt.anchorMax = new Vector2(0f, 0f);
        trilhoRt.pivot = new Vector2(0f, 0f);
        trilhoRt.anchoredPosition = new Vector2(0f, 2f);
        trilhoRt.sizeDelta = new Vector2(200f, 6f);

        var fundo = trilho.AddComponent<Image>();
        fundo.sprite = UIUtil.Branco();
        fundo.color = CorTrilho;
        fundo.raycastTarget = false;

        var cheio = new GameObject("Preenchimento", typeof(RectTransform));
        cheio.transform.SetParent(trilho.transform, false);

        var crt = cheio.GetComponent<RectTransform>();
        crt.anchorMin = Vector2.zero;
        crt.anchorMax = Vector2.one;
        crt.offsetMin = new Vector2(1f, 1f);
        crt.offsetMax = new Vector2(-1f, -1f);

        preenchimento = cheio.AddComponent<Image>();
        preenchimento.sprite = UIUtil.Branco();
        preenchimento.type = Image.Type.Filled;
        preenchimento.fillMethod = Image.FillMethod.Horizontal;
        preenchimento.raycastTarget = false;

        Atualizar();
    }

    void OnEnable()
    {
        Religar();
        Atualizar();
    }

    void OnDisable()
    {
        Desligar();
    }

    /// <summary>
    /// (Re)assina o relógio da run. Ele sobrevive à troca de cena e este objeto
    /// não, então a assinatura é conferida a cada vez que o rodapé aparece.
    /// </summary>
    void Religar()
    {
        if (!Application.isPlaying) return;

        RunManager run = RunManager.Instance;
        if (ligado == run) return;

        Desligar();
        ligado = run;
        if (ligado != null) ligado.onCycleAdvanced += Atualizar;
    }

    void Desligar()
    {
        if (ligado == null) return;
        ligado.onCycleAdvanced -= Atualizar;
        ligado = null;
    }

    /// <summary>Lê o relógio da run e redesenha. Público para quem mexer na Corrupção fora do ciclo.</summary>
    public void Atualizar()
    {
        if (texto == null || preenchimento == null) return;

        RunManager run = Application.isPlaying ? RunManager.Instance : null;
        float fracao = run != null ? run.CorruptionRatio : 0f;

        texto.text = $"Corrupção {Mathf.RoundToInt(fracao * 100f)}%";
        preenchimento.fillAmount = fracao;
        preenchimento.color = Color.Lerp(CorLimpa, CorPodre, fracao);
    }
}
