using System.Collections;
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

        var label = go.AddComponent<TextMeshProUGUI>();
        label.text = texto;
        label.color = cor;
        label.fontSize = 30f * escala;
        label.alignment = TextAlignmentOptions.Center;
        label.fontStyle = FontStyles.Bold;
        label.raycastTarget = false;

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(220, 60);

        // Nasce sobre o alvo, convertendo do mundo para o espaço do Canvas.
        var alvoRect = target.GetComponent<RectTransform>();
        if (alvoRect != null)
        {
            Vector3 canto = alvoRect.TransformPoint(alvoRect.rect.center);
            rt.position = canto;
        }

        go.transform.SetAsLastSibling();
        StartCoroutine(FloatAndFade(rt, label));
    }

    IEnumerator FloatAndFade(RectTransform rt, TextMeshProUGUI label)
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
            label.alpha = p < 0.5f ? 1f : Mathf.Lerp(1f, 0f, (p - 0.5f) * 2f);

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

    /// <summary>Move a barra de vida suavemente até o novo valor.</summary>
    public void LerpBar(Image bar, float alvo)
    {
        if (bar == null) return;

        if (!bar.gameObject.activeInHierarchy)
        {
            bar.fillAmount = alvo;
            return;
        }

        StartCoroutine(LerpBarRoutine(bar, Mathf.Clamp01(alvo)));
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

        if (bar != null) bar.fillAmount = alvo;
    }
}
