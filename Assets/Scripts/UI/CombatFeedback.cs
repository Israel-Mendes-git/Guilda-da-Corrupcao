using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reação visual do combate: número que sobe, tremor de quem apanha e barra de
/// vida que desliza em vez de saltar.
///
/// Tudo é criado em runtime e não exige prefab nem referência no Inspector —
/// quem chama só precisa dizer "aconteceu isto neste objeto".
/// </summary>
public class CombatFeedback : MonoBehaviour
{
    public static CombatFeedback Instance { get; private set; }

    [Header("Cores")]
    public Color damageColor = new Color(0.90f, 0.30f, 0.28f);
    public Color healColor = new Color(0.42f, 0.82f, 0.45f);
    public Color blockColor = new Color(0.55f, 0.72f, 0.95f);
    public Color stressColor = new Color(0.85f, 0.72f, 0.35f);

    [Header("Tempos")]
    public float floatDuration = 0.85f;
    public float floatDistance = 70f;
    public float shakeDuration = 0.22f;
    public float shakeStrength = 12f;
    public float barLerpDuration = 0.25f;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(this); return; }
    }

    /// <summary>Cria o componente sob demanda, para funcionar sem setup de cena.</summary>
    public static CombatFeedback Get()
    {
        if (Instance != null) return Instance;

        var host = CombatManager.Instance != null
            ? CombatManager.Instance.gameObject
            : new GameObject("CombatFeedback");

        Instance = host.GetComponent<CombatFeedback>() ?? host.AddComponent<CombatFeedback>();
        return Instance;
    }

    public void ShowDamage(GameObject target, int amount) => ShowNumber(target, $"-{amount}", damageColor, 1.15f);
    public void ShowHeal(GameObject target, int amount) => ShowNumber(target, $"+{amount}", healColor, 1f);
    public void ShowBlock(GameObject target, int amount) => ShowNumber(target, $"🛡️ {amount}", blockColor, 1f);
    public void ShowStress(GameObject target, int amount) => ShowNumber(target, $"🧠 +{amount}", stressColor, 1f);
    public void ShowText(GameObject target, string texto, Color cor) => ShowNumber(target, texto, cor, 1f);

    /// <summary>Número que sobe e desaparece sobre o alvo.</summary>
    public void ShowNumber(GameObject target, string texto, Color cor, float escala)
    {
        if (target == null || !target.activeInHierarchy) return;

        var canvas = target.GetComponentInParent<Canvas>();
        if (canvas == null) return;

        var go = new GameObject("FloatingNumber", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(220, 60);

        // A sombra nasce primeiro, para ficar atrás. Sem ela o número se perdia
        // dentro do desenho da criatura: texto claro sobre pixel art clara, no
        // meio de uma figura de 300px, é a mesma coisa que não mostrar número.
        var sombraGo = new GameObject("Sombra", typeof(RectTransform));
        sombraGo.transform.SetParent(go.transform, false);

        var sombra = sombraGo.AddComponent<TextMeshProUGUI>();
        sombra.text = texto;
        sombra.color = new Color(0f, 0f, 0f, 0.85f);
        sombra.fontSize = 32f * escala;
        sombra.alignment = TextAlignmentOptions.Center;
        sombra.fontStyle = FontStyles.Bold;
        sombra.raycastTarget = false;

        var sombraRt = sombraGo.GetComponent<RectTransform>();
        sombraRt.anchorMin = Vector2.zero;
        sombraRt.anchorMax = Vector2.one;
        sombraRt.offsetMin = new Vector2(3, -3);
        sombraRt.offsetMax = new Vector2(3, -3);

        var labelGo = new GameObject("Numero", typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, false);

        var label = labelGo.AddComponent<TextMeshProUGUI>();
        label.text = texto;
        label.color = cor;
        label.fontSize = 32f * escala;
        label.alignment = TextAlignmentOptions.Center;
        label.fontStyle = FontStyles.Bold;
        label.raycastTarget = false;

        var labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;

        // Nasce ACIMA do alvo, e não no centro dele: no centro, o número caía
        // sobre a barriga da criatura ou sobre o retrato do herói, disputando
        // leitura com o desenho. Acima da cabeça é onde o jogador já olha para
        // ler a intenção.
        var alvoRect = target.GetComponent<RectTransform>();
        if (alvoRect != null)
        {
            Vector3 acima = alvoRect.TransformPoint(
                new Vector3(alvoRect.rect.center.x, alvoRect.rect.yMax - 24f, 0f));
            rt.position = acima;
        }

        go.transform.SetAsLastSibling();
        StartCoroutine(FloatAndFade(rt, label, sombra));
    }

    IEnumerator FloatAndFade(RectTransform rt, TextMeshProUGUI label, TextMeshProUGUI sombra)
    {
        Vector3 inicio = rt.position;
        Vector3 fim = inicio + Vector3.up * floatDistance;

        float t = 0f;
        while (t < floatDuration)
        {
            // O combate reconstrói as views a cada ação; o alvo desta animação
            // pode ter sido destruído no meio do caminho.
            if (rt == null || label == null) yield break;

            t += Time.deltaTime;
            float p = t / floatDuration;

            rt.position = Vector3.Lerp(inicio, fim, 1f - (1f - p) * (1f - p));  // desacelera

            // Some só na segunda metade, para o número ser lido antes.
            float alfa = p < 0.5f ? 1f : Mathf.Lerp(1f, 0f, (p - 0.5f) * 2f);
            label.alpha = alfa;
            if (sombra != null) sombra.alpha = alfa * 0.85f;

            yield return null;
        }

        if (rt != null) Destroy(rt.gameObject);
    }

    /// <summary>Sacode o alvo — usado quando ele leva dano.</summary>
    public void Shake(GameObject target)
    {
        if (target == null || !target.activeInHierarchy) return;

        var rt = target.GetComponent<RectTransform>();
        if (rt != null) StartCoroutine(ShakeRoutine(rt));
    }

    IEnumerator ShakeRoutine(RectTransform rt)
    {
        Vector2 origem = rt.anchoredPosition;
        float t = 0f;

        while (t < shakeDuration)
        {
            // A view pode ser destruída enquanto treme (o combate a recria a
            // cada ação); sem esta checagem vira MissingReferenceException.
            if (rt == null) yield break;

            t += Time.deltaTime;

            // Amplitude decrescente: o tranco perde força em vez de parar seco.
            float forca = shakeStrength * (1f - t / shakeDuration);
            rt.anchoredPosition = origem + new Vector2(Random.Range(-forca, forca), Random.Range(-forca, forca) * 0.4f);

            yield return null;
        }

        if (rt != null) rt.anchoredPosition = origem;
    }

    /// <summary>
    /// Uma animação por barra. Sem isto, cada refresh do combate — e há um por
    /// carta jogada, por golpe e por morte — largava uma corrotina nova sobre a
    /// mesma barra, cada uma partindo de onde encontrou o preenchimento. Três
    /// correndo juntas faziam a barra do inimigo saltar para trás e voltar no
    /// meio do ataque, que é o defeito que o autor reportou.
    ///
    /// É a mesma proteção que o <c>UIManager</c> já tem para painéis e popups.
    /// </summary>
    private readonly Dictionary<Image, Coroutine> animacoesDeBarra = new Dictionary<Image, Coroutine>();

    /// <summary>Move a barra de vida suavemente até o novo valor.</summary>
    public void LerpBar(Image bar, float alvo)
    {
        if (bar == null) return;

        alvo = Mathf.Clamp01(alvo);

        if (animacoesDeBarra.TryGetValue(bar, out Coroutine emCurso))
        {
            if (emCurso != null) StopCoroutine(emCurso);
            animacoesDeBarra.Remove(bar);
        }

        if (!bar.gameObject.activeInHierarchy)
        {
            bar.fillAmount = alvo;
            return;
        }

        // Já está onde deveria: reanimar faria a barra piscar sem nada ter
        // mudado, e o refresh do combate chama isto o tempo todo.
        if (Mathf.Approximately(bar.fillAmount, alvo)) return;

        animacoesDeBarra[bar] = StartCoroutine(LerpBarRoutine(bar, alvo));
    }

    IEnumerator LerpBarRoutine(Image bar, float alvo)
    {
        float inicio = bar.fillAmount;
        float t = 0f;

        while (t < barLerpDuration)
        {
            if (bar == null) yield break;

            t += Time.deltaTime;
            bar.fillAmount = Mathf.Lerp(inicio, alvo, t / barLerpDuration);
            yield return null;
        }

        if (bar != null)
        {
            bar.fillAmount = alvo;
            animacoesDeBarra.Remove(bar);
        }
    }
}
