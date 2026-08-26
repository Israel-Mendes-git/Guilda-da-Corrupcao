#if UNITY_EDITOR
using UnityEngine;

/// <summary>
/// Onde está o desenho dentro do quadro, medido nos pixels.
///
/// <b>O quadro nunca é o desenho.</b> Spritesheets de criatura são grades de
/// tamanho fixo com o bicho solto no meio; ícones de mapa são quadrados de 512
/// com a montanha ocupando um terço. Quem trata o quadro como se fosse a figura
/// erra duas vezes: assenta no chão o que devia flutuar e desenha do mesmo
/// tamanho o que tem tamanhos diferentes.
///
/// Isso foi descoberto duas vezes no projeto — no esqueleto que ficou de pé no
/// ar sobre o próprio nome, e no vulcão que saiu do tamanho de um traço ao lado
/// da serra. Daqui em diante é uma conta só, feita no Editor: em execução não há
/// como ler pixels de textura que não foi marcada como legível.
/// </summary>
public static class SpriteConteudo
{
    /// <summary>O retângulo opaco do sprite, em frações do próprio quadro.</summary>
    public struct Medida
    {
        /// <summary>Distância do pé do quadro até o começo do desenho (0 a 1).</summary>
        public float baseVisivel;

        /// <summary>Quanto da altura do quadro o desenho ocupa (0 a 1).</summary>
        public float alturaVisivel;

        /// <summary>Quanto da largura do quadro o desenho ocupa (0 a 1).</summary>
        public float larguraVisivel;

        /// <summary>A medição deu certo?</summary>
        public bool Valida => alturaVisivel > 0.001f;
    }

    /// <summary>
    /// Mede o sprite lendo os pixels.
    ///
    /// A textura do pacote não é legível, e marcá-la como legível mexeria no
    /// import de um asset de terceiro. Uma cópia pela GPU lê os mesmos pixels
    /// sem tocar em nada no disco.
    /// </summary>
    public static Medida Medir(Sprite quadro, float limiarDeAlfa = 0.05f)
    {
        var medida = new Medida { alturaVisivel = 1f, larguraVisivel = 1f };
        if (quadro == null || quadro.texture == null) return medida;

        var rt = RenderTexture.GetTemporary(quadro.texture.width, quadro.texture.height,
                                            0, RenderTextureFormat.ARGB32);
        Graphics.Blit(quadro.texture, rt);

        RenderTexture anterior = RenderTexture.active;
        RenderTexture.active = rt;

        var copia = new Texture2D(quadro.texture.width, quadro.texture.height,
                                  TextureFormat.RGBA32, false);
        copia.ReadPixels(new Rect(0, 0, quadro.texture.width, quadro.texture.height), 0, 0);
        copia.Apply();

        RenderTexture.active = anterior;
        RenderTexture.ReleaseTemporary(rt);

        Rect r = quadro.rect;
        int x0 = Mathf.RoundToInt(r.x), y0 = Mathf.RoundToInt(r.y);
        int largura = Mathf.RoundToInt(r.width), altura = Mathf.RoundToInt(r.height);

        int primeiraLinha = -1, ultimaLinha = -1;
        int primeiraColuna = int.MaxValue, ultimaColuna = -1;

        for (int y = 0; y < altura; y++)
        {
            bool temTinta = false;

            for (int x = 0; x < largura; x++)
            {
                if (copia.GetPixel(x0 + x, y0 + y).a <= limiarDeAlfa) continue;

                temTinta = true;
                if (x < primeiraColuna) primeiraColuna = x;
                if (x > ultimaColuna) ultimaColuna = x;
            }

            if (!temTinta) continue;

            if (primeiraLinha < 0) primeiraLinha = y;
            ultimaLinha = y;
        }

        Object.DestroyImmediate(copia);

        if (primeiraLinha < 0 || altura <= 0) return medida;

        medida.baseVisivel = primeiraLinha / (float)altura;
        medida.alturaVisivel = (ultimaLinha - primeiraLinha + 1) / (float)altura;
        medida.larguraVisivel = ultimaColuna >= primeiraColuna
            ? (ultimaColuna - primeiraColuna + 1) / (float)largura
            : 1f;

        return medida;
    }
}
#endif
