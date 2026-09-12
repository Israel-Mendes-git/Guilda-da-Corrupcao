using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Faz a moldura da sala acesa respirar.
///
/// <b>Por que pulsar.</b> A guilda são sete portas do mesmo tamanho e do mesmo
/// peso, e a moldura dourada parada some no meio delas — na captura de 11/09 a
/// Forja estava acesa e era preciso procurar para notar. O que o olho pega numa
/// tela estática é o que se move; um brilho que vai e volta chama sem escrever
/// nada, que é a regra que o autor pôs: <i>nem tutorial, nem caixa de texto</i>.
///
/// Devagar de propósito — dois segundos por ciclo. Pisca-pisca vira urgência, e
/// a porta acesa não é urgência: é a sugestão do que fazer agora.
///
/// Anda em <see cref="Time.unscaledDeltaTime"/> porque a guilda pode estar com o
/// tempo parado (menu de pausa por cima), e uma moldura que congela no meio do
/// fade parece defeito.
/// </summary>
[RequireComponent(typeof(Image))]
public class RealcePulsante : MonoBehaviour
{
    /// <summary>Quanto o alfa cai no ponto mais baixo da respiração.</summary>
    const float AlfaMinimo = 0.45f;

    /// <summary>Segundos de um ciclo completo, ida e volta.</summary>
    const float Periodo = 2f;

    Image moldura;
    float alfaCheio = 1f;

    void Awake()
    {
        moldura = GetComponent<Image>();
        if (moldura != null) alfaCheio = moldura.color.a;
    }

    void OnEnable()
    {
        // Começa cheio: a porta acende no instante em que a condição casa, e
        // aparecer no ponto baixo do ciclo atrasaria a leitura.
        if (moldura != null) Aplicar(alfaCheio);
    }

    void Update()
    {
        if (moldura == null) return;

        // Seno de 0 a 1, e daí para a faixa de alfa.
        float onda = (Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / Periodo) + 1f) * 0.5f;
        Aplicar(Mathf.Lerp(AlfaMinimo * alfaCheio, alfaCheio, onda));
    }

    void Aplicar(float alfa)
    {
        Color c = moldura.color;
        moldura.color = new Color(c.r, c.g, c.b, alfa);
    }
}
