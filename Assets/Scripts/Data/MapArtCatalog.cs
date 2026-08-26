using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A arte dos mapas: o papel do mapa da região, os ícones de cada terreno e os
/// marcos da estrada.
///
/// <b>Por que um catálogo, e não referências na cena.</b> Os sprites moram
/// dentro dos pacotes importados (<c>Assets/Amanz</c>, <c>Assets/EVPO…</c>), fora
/// de <c>Resources</c> — em execução não há como carregá-los por caminho. O
/// catálogo é um asset que <b>fica</b> em Resources e guarda as referências; quem
/// o preenche é uma ferramenta de Editor, do mesmo jeito que o
/// <see cref="BiomeArtCatalog"/> resolveu isto para o cenário do combate.
///
/// Tudo é opcional. Sem o pacote importado, o catálogo fica vazio e cada tela cai
/// no desenho geométrico que ela já tinha: círculos, linhas e cor. A arte
/// melhora o mapa, não o faz existir.
/// </summary>
public class MapArtCatalog : ScriptableObject
{
    /// <summary>O caminho fixo em Resources. Quem carrega usa <see cref="Carregar"/>.</summary>
    public const string NomeEmResources = "MapArtCatalog";

    [System.Serializable]
    public class IconeDeRegiao
    {
        public BiomeType biome;
        public Sprite icone;

        /// <summary>
        /// Correção de tamanho, medida nos pixels do desenho.
        ///
        /// Os símbolos do pacote são quadrados de 512 com margens transparentes
        /// bem diferentes: a serra ocupa metade do quadro, o pico isolado ocupa
        /// um quinto. Desenhados no mesmo retângulo, um vira montanha e o outro
        /// vira um traço — foi assim que o Vulcão apareceu na primeira captura
        /// sobre pergaminho.
        /// </summary>
        public float escala = 1f;
    }

    [Header("Mapa da região")]
    /// <summary>A folha de papel sobre a qual o mundo é desenhado.</summary>
    public Sprite papel;

    /// <summary>Moldura ou borda decorada, desenhada por cima do papel.</summary>
    public Sprite borda;

    /// <summary>O marcador da própria guilda, no centro do mapa.</summary>
    public Sprite guilda;

    public List<IconeDeRegiao> regioes = new List<IconeDeRegiao>();

    [System.Serializable]
    public class IconeDeNo
    {
        public JourneyEventType tipo;
        public Sprite icone;

        /// <summary>Correção de tamanho, medida nos pixels — ver <see cref="IconeDeRegiao.escala"/>.</summary>
        public float escala = 1f;
    }

    [Header("Marcos da estrada")]
    /// <summary>
    /// Um ícone por tipo de ponto da rota. Substitui os emoji que a fonte
    /// desenhava — ⚔️, 💰, 🔥 — por símbolos de mapa, que é o que um mapa usa.
    /// </summary>
    public List<IconeDeNo> nos = new List<IconeDeNo>();

    /// <summary>O chefe tem marca própria: não é um tipo de evento, é o fim da rota.</summary>
    public Sprite marcoDeChefe;

    /// <summary>A bússola, no canto do mapa da região.</summary>
    public Sprite bussola;

    static MapArtCatalog cache;
    static bool procurou;

    /// <summary>
    /// O catálogo, ou null se ninguém o montou ainda.
    ///
    /// A procura acontece uma vez por sessão: <c>Resources.Load</c> de um asset
    /// que não existe custa o mesmo de um que existe, e o mapa da região é
    /// redesenhado a cada abertura da preparação.
    /// </summary>
    public static MapArtCatalog Carregar()
    {
        if (procurou) return cache;

        procurou = true;
        cache = Resources.Load<MapArtCatalog>(NomeEmResources);
        return cache;
    }

    /// <summary>O ícone desta região, ou null se o pacote não cobrir o terreno.</summary>
    public Sprite IconeDe(BiomeType bioma)
    {
        foreach (var entrada in regioes)
            if (entrada != null && entrada.biome == bioma) return entrada.icone;

        return null;
    }

    /// <summary>Quanto o símbolo desta região precisa crescer para se equiparar aos outros.</summary>
    public float EscalaDe(BiomeType bioma)
    {
        foreach (var entrada in regioes)
            if (entrada != null && entrada.biome == bioma)
                return entrada.escala > 0f ? entrada.escala : 1f;

        return 1f;
    }

    /// <summary>O ícone deste tipo de ponto da rota, ou null se não houver.</summary>
    public Sprite IconeDe(JourneyEventType tipo)
    {
        foreach (var entrada in nos)
            if (entrada != null && entrada.tipo == tipo) return entrada.icone;

        return null;
    }

    /// <summary>Correção de tamanho deste ponto da rota.</summary>
    public float EscalaDe(JourneyEventType tipo)
    {
        foreach (var entrada in nos)
            if (entrada != null && entrada.tipo == tipo)
                return entrada.escala > 0f ? entrada.escala : 1f;

        return 1f;
    }

    /// <summary>Correção de tamanho do marco do chefe.</summary>
    public float escalaDoChefe = 1f;

    /// <summary>Quantas peças o catálogo tem de fato — o relatório do teste lê isto.</summary>
    public int Pecas
    {
        get
        {
            int n = 0;
            if (papel != null) n++;
            if (borda != null) n++;
            if (guilda != null) n++;
            if (bussola != null) n++;
            if (marcoDeChefe != null) n++;

            foreach (var entrada in regioes)
                if (entrada != null && entrada.icone != null) n++;

            foreach (var entrada in nos)
                if (entrada != null && entrada.icone != null) n++;

            return n;
        }
    }
}
