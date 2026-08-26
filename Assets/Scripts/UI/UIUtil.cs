using UnityEngine;

/// <summary>
/// Utilidades de UI compartilhadas.
/// </summary>
public static class UIUtil
{
    /// <summary>
    /// Esvazia um container agora, e não no fim do frame.
    ///
    /// `Destroy` apenas agenda a remoção: o objeto continua sendo filho e sendo
    /// desenhado até o frame terminar. Todo código que faz "limpa e reconstrói"
    /// duas vezes no mesmo frame — o que acontece o tempo todo quando uma ação
    /// dispara outra em cadeia — acabava com as duas gerações de elementos na
    /// tela ao mesmo tempo: cartas sobre cartas, botões sobre botões.
    ///
    /// Desanexar antes de destruir tira o objeto da hierarquia imediatamente.
    /// </summary>
    public static void ClearChildrenNow(Transform container)
    {
        if (container == null) return;

        for (int i = container.childCount - 1; i >= 0; i--)
        {
            Transform child = container.GetChild(i);
            if (child == null) continue;

            child.SetParent(null, false);
            Object.Destroy(child.gameObject);
        }
    }

    static Sprite circuloCache;

    /// <summary>
    /// Um círculo branco desenhado na hora, para marcador de mapa.
    ///
    /// Sem sprite, o <c>Image</c> sai quadrado — e um punhado de quadrados
    /// grandes lê como caixas de menu, não como lugares.
    ///
    /// Feito em código, e não com <c>Resources.GetBuiltinResource("UI/Skin/Knob.psd")</c>:
    /// aquele caminho é recurso de <b>editor</b> e não existe em runtime. Ele
    /// devolve null e cospe dois erros por marcador — foram 64 numa jornada, com
    /// a tela funcionando normalmente, porque um sprite nulo apenas volta a ser
    /// um quadrado.
    /// </summary>
    public static Sprite Circulo()
    {
        if (circuloCache != null) return circuloCache;

        const int lado = 64;
        var textura = new Texture2D(lado, lado, TextureFormat.RGBA32, false);
        textura.filterMode = FilterMode.Bilinear;

        float raio = lado * 0.5f;
        for (int y = 0; y < lado; y++)
        {
            for (int x = 0; x < lado; x++)
            {
                float dx = x + 0.5f - raio;
                float dy = y + 0.5f - raio;
                float distancia = Mathf.Sqrt(dx * dx + dy * dy);

                // A borda decai em um pixel: sem isso o círculo sai serrilhado.
                float alfa = Mathf.Clamp01(raio - distancia);
                textura.SetPixel(x, y, new Color(1f, 1f, 1f, alfa));
            }
        }
        textura.Apply();

        circuloCache = Sprite.Create(textura, new Rect(0f, 0f, lado, lado), new Vector2(0.5f, 0.5f));
        return circuloCache;
    }
}
