#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Escurece as bordas da tela e põe grão sobre tudo. Tools → Guild of Legends →
/// Vestir o véu da tela · gatilho <c>RunScreenVeil.trigger</c>.
///
/// <b>O que havia.</b> Painel de cor chapada até a borda, em todas as telas. O
/// Darkest Dungeon faz o contrário: a moldura da tela é sempre mais escura que o
/// meio, e há textura de tela por cima de tudo. É o que faz a imagem parecer
/// iluminada por tocha em vez de por um <c>Image</c>.
///
/// <b>Duas camadas, e não uma.</b> A vinheta é um gradiente radial que só existe
/// nas bordas; o grão é um ladrilho de ruído repetido sobre a tela inteira. Fazer
/// as duas numa textura só amarraria a granulação à resolução — o grão precisa ser
/// pequeno e constante, e a vinheta precisa esticar.
///
/// <b>Nada aqui recebe clique.</b> Um véu de tela cheia com <c>raycastTarget</c>
/// ligado engole todo o jogo por baixo, e o sintoma é uma tela que não responde
/// sem um único erro no console.
/// </summary>
public static class ScreenVeil
{
    const string Pasta = "Assets/Art/Veil";
    const string VinhetaPath = Pasta + "/vinheta.png";
    const string GraoPath = Pasta + "/grao.png";

    const string RaizName = "ScreenVeil";

    /// <summary>
    /// Acima do <c>PopupSortingOrder</c> do <see cref="UIManager"/>, que é 500: o
    /// véu é ambiente da tela, e não um painel. Popup que passasse por cima dele
    /// pareceria recortado de outro jogo.
    /// </summary>
    const int OrdemDoVeu = 1000;

    // ── A vinheta ──────────────────────────────────────────────────────────────

    const int LadoDaVinheta = 512;

    /// <summary>Onde a sombra começa, em raio normalizado. Antes disto, nada.</summary>
    const float InicioDaSombra = 0.34f;

    /// <summary>Quanto a borda escurece no canto mais extremo.</summary>
    const float SombraMaxima = 0.72f;

    /// <summary>Acima de 1 a queda é lenta no meio e rápida na ponta.</summary>
    const float CurvaDaSombra = 1.7f;

    // ── O grão ─────────────────────────────────────────────────────────────────

    const int LadoDoGrao = 256;

    /// <summary>
    /// Baixo de propósito. Grão que se enxerga como grão vira chuvisco; o que se
    /// quer é só tirar a lisura da cor chapada.
    /// </summary>
    const float ForcaDoGrao = 0.045f;

    /// <summary>Quantas vezes o ladrilho cabe na largura da tela.</summary>
    const float RepeticoesNaLargura = 7.5f;

    [MenuItem("Tools/Guild of Legends/Vestir o véu da tela")]
    public static void Aplicar()
    {
        Sprite vinheta = GarantirTextura(VinhetaPath, LadoDaVinheta, PintarVinheta, false);
        Sprite grao = GarantirTextura(GraoPath, LadoDoGrao, PintarGrao, true);

        if (vinheta == null || grao == null)
        {
            Debug.LogError("Véu da tela: não consegui gerar as texturas.");
            return;
        }

        int vestidas = 0;
        string aberta = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path;

        foreach (string caminho in new[] { MenuSceneSetup.CaminhoDoJogo, MenuSceneSetup.CaminhoDoMenu })
        {
            if (!File.Exists(caminho)) continue;

            var cena = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(caminho);

            Canvas canvas = UIUtil.CanvasPrincipal();
            if (canvas == null)
            {
                Debug.LogWarning($"Véu da tela: {caminho} não tem Canvas.");
                continue;
            }

            Montar(canvas, vinheta, grao);
            vestidas++;

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(cena);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(cena);
        }

        if (!string.IsNullOrEmpty(aberta)
            && aberta != UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path)
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(aberta);

        Debug.Log($"Véu da tela: vinheta e grão em {vestidas} cena(s).");
    }

    /// <summary>
    /// O véu é o último filho do Canvas e tem ordenação própria.
    ///
    /// Só a ordem de irmãos não bastaria: os popups do <see cref="UIManager"/>
    /// ganham um Canvas próprio em ordem 500 justamente para escapar da ordem de
    /// irmãos, e passariam por cima do véu.
    /// </summary>
    static void Montar(Canvas canvas, Sprite vinheta, Sprite grao)
    {
        Transform achado = canvas.transform.Find(RaizName);
        GameObject raiz;

        if (achado != null)
        {
            raiz = achado.gameObject;
        }
        else
        {
            raiz = new GameObject(RaizName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(raiz, "Véu da tela");
            raiz.transform.SetParent(canvas.transform, false);
        }

        raiz.transform.SetAsLastSibling();
        Esticar(raiz.GetComponent<RectTransform>());

        var proprio = raiz.GetComponent<Canvas>();
        if (proprio == null) proprio = Undo.AddComponent<Canvas>(raiz);

        Undo.RecordObject(proprio, "Véu da tela");
        proprio.overrideSorting = true;
        proprio.sortingOrder = OrdemDoVeu;
        EditorUtility.SetDirty(proprio);

        // Sem GraphicRaycaster de propósito: o véu não é alvo de clique, e um
        // raycaster aqui só criaria a chance de virar um.
        var raycaster = raiz.GetComponent<GraphicRaycaster>();
        if (raycaster != null) Undo.DestroyObjectImmediate(raycaster);

        // O grão primeiro e a vinheta depois: a sombra da borda entra por cima do
        // ruído, e não o contrário — grão sobre a parte escura seria a única parte
        // visível dele.
        Camada(raiz.transform, "Img_Grao", grao, Image.Type.Tiled, Color.white);
        Camada(raiz.transform, "Img_Vinheta", vinheta, Image.Type.Simple, Color.white);

        AjustarLadrilhoDoGrao(raiz.transform, canvas);
    }

    static Image Camada(Transform pai, string nome, Sprite sprite, Image.Type tipo, Color cor)
    {
        Transform achado = pai.Find(nome);
        GameObject go;

        if (achado != null)
        {
            go = achado.gameObject;
        }
        else
        {
            go = new GameObject(nome, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(go, "Véu da tela");
            go.transform.SetParent(pai, false);
        }

        Esticar(go.GetComponent<RectTransform>());

        var img = go.GetComponent<Image>();
        if (img == null) img = Undo.AddComponent<Image>(go);

        Undo.RecordObject(img, "Véu da tela");
        img.sprite = sprite;
        img.type = tipo;
        img.color = cor;
        img.preserveAspect = false;

        // A regra que custou a tarja de estresse do Cemitério: sem sprite, o
        // Image ignora o tipo e pinta o retângulo inteiro. Aqui há sprite, mas o
        // véu é a última coisa que se quer opaca.
        img.raycastTarget = false;
        EditorUtility.SetDirty(img);

        return img;
    }

    /// <summary>
    /// O ladrilho do grão em pixels, para a granulação não mudar de tamanho com a
    /// resolução de referência do Canvas.
    /// </summary>
    static void AjustarLadrilhoDoGrao(Transform raiz, Canvas canvas)
    {
        Transform achado = raiz.Find("Img_Grao");
        if (achado == null) return;

        var img = achado.GetComponent<Image>();
        if (img == null || img.sprite == null) return;

        var escalador = canvas.GetComponent<CanvasScaler>();
        float largura = escalador != null && escalador.referenceResolution.x > 0
            ? escalador.referenceResolution.x
            : 1920f;

        Undo.RecordObject(img, "Véu da tela");
        img.pixelsPerUnitMultiplier = Mathf.Max(0.01f, LadoDoGrao * RepeticoesNaLargura / largura);
        EditorUtility.SetDirty(img);
    }

    static void Esticar(RectTransform rt)
    {
        if (rt == null) return;

        Undo.RecordObject(rt, "Véu da tela");
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
        EditorUtility.SetDirty(rt);
    }

    #region As texturas

    /// <summary>
    /// Gera o PNG se ainda não existir e devolve o Sprite.
    ///
    /// Nunca sobrescreve: é a mesma precaução do catálogo de mapas e da arte dos
    /// eventos — o que for repintado à mão tem de sobreviver a rodar de novo.
    /// </summary>
    static Sprite GarantirTextura(string path, int lado, System.Func<int, int, int, Color> pintor, bool repetir)
    {
        if (!AssetDatabase.IsValidFolder(Pasta))
        {
            string pai = Path.GetDirectoryName(Pasta).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(pai))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(pai).Replace('\\', '/'), Path.GetFileName(pai));

            AssetDatabase.CreateFolder(pai, Path.GetFileName(Pasta));
        }

        if (!File.Exists(path))
        {
            var tex = new Texture2D(lado, lado, TextureFormat.RGBA32, false);

            for (int y = 0; y < lado; y++)
                for (int x = 0; x < lado; x++)
                    tex.SetPixel(x, y, pintor(x, y, lado));

            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path);
        }

        ConfigurarImportador(path, repetir);
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static void ConfigurarImportador(string path, bool repetir)
    {
        var importador = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importador == null) return;

        bool mudou = false;

        if (importador.textureType != TextureImporterType.Sprite)
        {
            importador.textureType = TextureImporterType.Sprite;
            mudou = true;
        }

        // O ladrilho do grão só emenda sem costura com Repeat; a vinheta precisa
        // de Clamp, ou a borda escura reaparece do outro lado.
        TextureWrapMode modo = repetir ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        if (importador.wrapMode != modo)
        {
            importador.wrapMode = modo;
            mudou = true;
        }

        // Tiled exige malha de retângulo cheio: com Tight o Unity recorta o
        // sprite pelo alfa e o ladrilho sai com buraco.
        if (importador.spriteImportMode == SpriteImportMode.Single)
        {
            var settings = new TextureImporterSettings();
            importador.ReadTextureSettings(settings);

            if (settings.spriteMeshType != SpriteMeshType.FullRect)
            {
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importador.SetTextureSettings(settings);
                mudou = true;
            }
        }

        if (importador.alphaIsTransparency != true)
        {
            importador.alphaIsTransparency = true;
            mudou = true;
        }

        if (mudou)
        {
            importador.SaveAndReimport();
        }
    }

    /// <summary>
    /// Preto transparente no meio, preto opaco no canto. A distância é medida em
    /// espaço quadrado e o Image estica — numa tela 16:9 a sombra fica elíptica,
    /// que é o que se quer.
    /// </summary>
    static Color PintarVinheta(int x, int y, int lado)
    {
        float u = (x + 0.5f) / lado * 2f - 1f;
        float v = (y + 0.5f) / lado * 2f - 1f;

        float d = Mathf.Sqrt(u * u + v * v) / Mathf.Sqrt(2f);
        float t = Mathf.InverseLerp(InicioDaSombra, 1f, d);

        return new Color(0f, 0f, 0f, Mathf.Pow(t, CurvaDaSombra) * SombraMaxima);
    }

    /// <summary>
    /// Ruído monocromático em torno do cinza médio, com alfa constante. O que
    /// varia é a cor, e não a opacidade: alfa variável faz o ladrilho aparecer
    /// como xadrez nas áreas escuras.
    /// </summary>
    static Color PintarGrao(int x, int y, int lado)
    {
        // Semente fixa: rodar de novo tem de dar o mesmo arquivo, ou o PNG muda
        // no diff a cada execução sem nada ter mudado de verdade.
        float ruido = Aleatorio(x * 73856093 ^ y * 19349663 ^ lado * 83492791);
        float tom = Mathf.Lerp(0.35f, 0.65f, ruido);

        return new Color(tom, tom, tom, ForcaDoGrao);
    }

    /// <summary>Hash inteiro → [0,1). Determinístico e sem estado global.</summary>
    static float Aleatorio(int semente)
    {
        unchecked
        {
            uint h = (uint)semente;
            h ^= h >> 16;
            h *= 0x7feb352d;
            h ^= h >> 15;
            h *= 0x846ca68b;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / (float)0x1000000;
        }
    }

    #endregion
}
#endif
