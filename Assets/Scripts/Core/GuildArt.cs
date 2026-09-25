#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Veste a guilda com o pátio pintado. Tools → Guild of Legends → Vestir a
/// Guilda · gatilho <c>RunGuildArt.trigger</c>.
///
/// <b>O que havia até 25/09.</b> Sete retângulos, cada um com uma cena
/// emprestada de um pacote de cavernas por trás e um véu escuro por cima. Cada
/// porta era uma pintura diferente, de um lugar diferente, e a tela lia como
/// sete quadros pendurados — nunca como uma guilda.
///
/// <b>O que há agora.</b> Uma pintura só: o <b>pátio da guilda</b>, no espírito
/// do mapa da vila do <i>Darkest Dungeon</i>, com as sete construções nele. Cada
/// porta deixou de ter arte própria e virou o <b>recorte da sua construção</b>
/// dentro dessa pintura, ancorado exatamente onde ela está. É por isso que as
/// portas agora se completam: elas nunca foram sete imagens, são sete pedaços da
/// mesma.
///
/// <b>Por que recorte e não uma imagem inteira por estado.</b> As variações
/// (terreno, e mais tarde a porta acesa) vêm de uma edição da pintura inteira, e
/// a edição mexe de leve em tudo — nuvem, poça, textura de pincel. Trocar a tela
/// inteira ao erguer uma sala faria o pátio piscar. Trocando só o pedaço daquela
/// construção, o resto do pátio nunca muda.
///
/// <b>As posições vêm da arte, não o contrário.</b> Os âncoras abaixo foram
/// medidos sobre a pintura por <c>Tools/guild_art.py</c>; as portas se movem para
/// cima das construções. O caso que obrigou a essa ordem foi a Jornada: o portão
/// foi pintado embutido na muralha do fundo, e não no meio do pátio onde o botão
/// estava.
/// </summary>
public static class GuildArt
{
    const string Pasta = "Assets/Art/Guild/";

    /// <summary>
    /// Onde cada construção está dentro da pintura, em fração do pátio.
    ///
    /// São âncoras, não pixels: a porta passa a acompanhar o tamanho do
    /// <c>GuildMap</c> sozinha, e a guilda inteira continua encaixada em
    /// qualquer resolução. Y já vem virado para a convenção do Unity (0 embaixo).
    /// </summary>
    static readonly (string trecho, string arquivo,
                     float xMin, float yMin, float xMax, float yMax)[] Construcoes =
    {
        ("taverna",    "taverna",     0.2380f, 0.0350f, 0.4968f, 0.5804f),
        ("tavern",     "taverna",     0.2380f, 0.0350f, 0.4968f, 0.5804f),
        ("forja",      "forja",       0.0158f, 0.0327f, 0.1578f, 0.9479f),
        ("forge",      "forja",       0.0158f, 0.0327f, 0.1578f, 0.9479f),
        ("cemiterio",  "cemiterio",   0.1042f, 0.4048f, 0.2936f, 0.7470f),
        ("cemetery",   "cemiterio",   0.1042f, 0.4048f, 0.2936f, 0.7470f),
        ("bibliotec",  "biblioteca",  0.3605f, 0.5060f, 0.5294f, 0.9219f),
        ("librar",     "biblioteca",  0.3605f, 0.5060f, 0.5294f, 0.9219f),
        ("mercado",    "mercado",     0.6231f, 0.0997f, 0.9246f, 0.5863f),
        ("market",     "mercado",     0.6231f, 0.0997f, 0.9246f, 0.5863f),

        // A Jornada vem antes de "mapa"/"map": numa cena onde a porta da estrada
        // se chame "MapaMundi", a regra do mapa casaria com ela primeiro.
        ("jornada",    "jornada",     0.5306f, 0.5119f, 0.6894f, 0.8929f),
        ("journey",    "jornada",     0.5306f, 0.5119f, 0.6894f, 0.8929f),
        ("quest",      "jornada",     0.5306f, 0.5119f, 0.6894f, 0.8929f),

        ("mapa",       "saladomapas", 0.7655f, 0.4591f, 0.9896f, 0.9866f),
        ("map",        "saladomapas", 0.7655f, 0.4591f, 0.9896f, 0.9866f)
    };

    [MenuItem("Tools/Guild of Legends/Vestir a Guilda")]
    public static void Aplicar()
    {
        Canvas canvas = UIUtil.CanvasPrincipal();
        if (canvas == null)
        {
            Debug.LogError("Vestir a Guilda: nenhum Canvas na cena aberta.");
            return;
        }

        Transform mapa = canvas.transform.Find("Background/GuildMap");
        if (mapa == null)
        {
            Debug.LogError("Vestir a Guilda: Background/GuildMap não encontrado.");
            return;
        }

        AjustarImportacao();

        if (!PintarOPatio(mapa)) return;

        int vestidas = 0;
        var semConstrucao = new List<string>();

        foreach (Transform sala in mapa)
        {
            if (sala.GetComponent<Button>() == null) continue;

            string chave = Chave(sala.name);
            bool achou = false;

            foreach (var c in Construcoes)
            {
                if (!chave.Contains(c.trecho)) continue;

                Sprite recorte = AssetDatabase.LoadAssetAtPath<Sprite>(Pasta + "sala_" + c.arquivo + ".png");
                if (recorte == null)
                {
                    semConstrucao.Add($"{sala.name} → sala_{c.arquivo}.png (arquivo não encontrado)");
                    achou = true;
                    break;
                }

                Ancorar(sala, c.xMin, c.yMin, c.xMax, c.yMax);
                Vestir(sala, recorte);
                Registrar(mapa, sala.name, recorte,
                          AssetDatabase.LoadAssetAtPath<Sprite>(Pasta + "terreno_" + c.arquivo + ".png"));
                vestidas++;
                achou = true;
                break;
            }

            if (!achou) semConstrucao.Add($"{sala.name} (chave '{chave}')");
        }

        EditorSceneManagerSalvar();

        Debug.Log($"Vestir a Guilda: pátio pintado e {vestidas} porta(s) encaixadas nele.");

        if (semConstrucao.Count > 0)
            Debug.LogWarning($"Vestir a Guilda: sem construção na tabela — {string.Join(", ", semConstrucao)}");
    }

    /// <summary>
    /// A pintura inteira no fundo do <c>GuildMap</c>.
    ///
    /// Vai num filho próprio, o primeiro de todos, e não no Image do próprio
    /// mapa: as portas são irmãs dentro dele, e ordem de irmãos é ordem de
    /// desenho — o pátio precisa nascer atrás de todas.
    ///
    /// <c>preserveAspect</c> fica falso de propósito. O pátio é 21:9 e o mapa da
    /// guilda é 2,1:1; preservar a proporção deixaria faixa vazia em cima e
    /// embaixo. Esticado, a pintura fica ~12% mais alta — e as portas esticam
    /// junto, na mesma conta, então tudo continua encaixado. Prédio um pouco
    /// mais alto numa pintura que não se compara a nada ninguém vê; faixa preta,
    /// sim.
    /// </summary>
    static bool PintarOPatio(Transform mapa)
    {
        Sprite patio = AssetDatabase.LoadAssetAtPath<Sprite>(Pasta + "patio.png");
        if (patio == null)
        {
            Debug.LogError($"Vestir a Guilda: {Pasta}patio.png não encontrado. "
                         + "Rode `python Tools/guild_art.py recortar` antes.");
            return false;
        }

        Transform achado = mapa.Find("Patio");
        GameObject go;

        if (achado != null)
        {
            go = achado.gameObject;
        }
        else
        {
            go = new GameObject("Patio", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(go, "Vestir a Guilda");
            go.transform.SetParent(mapa, false);
        }

        go.transform.SetAsFirstSibling();

        var rt = go.GetComponent<RectTransform>();
        Undo.RecordObject(rt, "Vestir a Guilda");
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = go.GetComponent<Image>();
        if (img == null) img = go.AddComponent<Image>();

        Undo.RecordObject(img, "Vestir a Guilda");
        img.sprite = patio;
        img.color = Color.white;
        img.raycastTarget = false;
        img.preserveAspect = false;
        img.type = Image.Type.Simple;
        EditorUtility.SetDirty(img);

        // O fundo atrás do pátio deixa de importar — a pintura cobre tudo —, mas
        // um mármore escuro tingido por baixo escapa pelas bordas em resoluções
        // que não fecham exato. Branco puro não empresta cor nenhuma.
        var fundo = mapa.GetComponent<Image>();
        if (fundo != null && fundo.sprite != null)
        {
            Undo.RecordObject(fundo, "Vestir a Guilda");
            fundo.color = Color.white;
            EditorUtility.SetDirty(fundo);
        }

        return true;
    }

    /// <summary>
    /// Entrega ao <see cref="GuildGuide"/> as duas caras daquela porta.
    ///
    /// O guia troca entre elas em tempo de execução — a construção quando a sala
    /// está de pé, o terreno enquanto o jogador não a ergueu. Terreno nulo é o
    /// caso normal de Taverna e Jornada, que nascem construídas e nunca viram
    /// canteiro.
    /// </summary>
    static void Registrar(Transform mapa, string nome, Sprite sala, Sprite terreno)
    {
        var guia = mapa.GetComponent<GuildGuide>();
        if (guia == null) return;

        Undo.RecordObject(guia, "Vestir a Guilda");

        GuildGuide.Arte arte = guia.artes.Find(a => a != null && a.nome == nome);
        if (arte == null)
        {
            arte = new GuildGuide.Arte { nome = nome };
            guia.artes.Add(arte);
        }

        arte.sala = sala;
        arte.terreno = terreno;
        EditorUtility.SetDirty(guia);
    }

    /// <summary>
    /// Move a porta para cima da construção dela, em âncoras.
    ///
    /// Offsets zerados: a porta passa a ser uma fração do mapa, e não um
    /// retângulo de pixels que precisaria ser recalculado a cada mudança de
    /// resolução ou de tamanho do <c>GuildMap</c>.
    /// </summary>
    static void Ancorar(Transform sala, float xMin, float yMin, float xMax, float yMax)
    {
        var rt = sala.GetComponent<RectTransform>();
        if (rt == null) return;

        Undo.RecordObject(rt, "Vestir a Guilda");
        rt.anchorMin = new Vector2(xMin, yMin);
        rt.anchorMax = new Vector2(xMax, yMax);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        EditorUtility.SetDirty(rt);
    }

    /// <summary>
    /// O recorte da construção dentro da porta.
    ///
    /// <b>Por que a porta recebe cópia do que já está no pátio.</b> Ela precisa
    /// de imagem própria para poder trocar: o <see cref="GuildGuide"/> troca este
    /// sprite pelo terreno quando a sala não foi erguida, e escurece este sprite
    /// quando a sala não tem o que oferecer. Sem uma imagem por porta, o guia
    /// teria de repintar a pintura inteira.
    ///
    /// Em cima do pátio o recorte cai exatamente sobre o original, então enquanto
    /// nada muda a tela parece uma pintura só.
    /// </summary>
    static void Vestir(Transform sala, Sprite recorte)
    {
        Transform achado = sala.Find("Cena");
        GameObject go;

        if (achado != null)
        {
            go = achado.gameObject;
        }
        else
        {
            go = new GameObject("Cena", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(go, "Vestir a Guilda");
            go.transform.SetParent(sala, false);
        }

        go.transform.SetAsFirstSibling();

        var rt = go.GetComponent<RectTransform>();
        Undo.RecordObject(rt, "Vestir a Guilda");
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = go.GetComponent<Image>();
        if (img == null) img = go.AddComponent<Image>();

        Undo.RecordObject(img, "Vestir a Guilda");
        img.sprite = recorte;

        // Sem véu: o véu existia para separar sete pinturas brigando entre si.
        // Agora é uma pintura só, e o recorte precisa casar com o pátio atrás
        // dele — qualquer tinta por cima denuncia a emenda.
        img.color = Color.white;
        img.raycastTarget = false;
        img.preserveAspect = false;
        img.type = Image.Type.Simple;
        EditorUtility.SetDirty(img);

        Sombrear(sala);

        // O fundo da porta é o alvo do Button e fica ENTRE o pátio e o recorte.
        // Opaco, ele tapava a pintura; transparente, o Button continua clicável
        // porque raycast não depende de alfa do alvo.
        var alvo = sala.GetComponent<Image>();
        if (alvo != null)
        {
            Undo.RecordObject(alvo, "Vestir a Guilda");
            alvo.color = new Color(1f, 1f, 1f, 0f);
            EditorUtility.SetDirty(alvo);
        }

        foreach (TMP_Text texto in sala.GetComponentsInChildren<TMP_Text>(true))
        {
            Undo.RecordObject(texto, "Vestir a Guilda");

            texto.color = new Color(0.97f, 0.94f, 0.86f);
            texto.fontStyle = FontStyles.Bold;
            texto.transform.SetAsLastSibling();

            GarantirSombra(texto);
            EditorUtility.SetDirty(texto);
        }
    }

    /// <summary>
    /// A sombra com que o <see cref="GuildGuide"/> apaga uma porta sem motivo.
    ///
    /// <b>Por que uma peça, e não o brilho do recorte.</b> Escurecer o Image do
    /// recorte escurece um retângulo, e sobre uma pintura contínua retângulo se
    /// vê — a Jornada apagada virou um quadrado preto na captura de 25/09. Esta
    /// máscara é preta no meio e some nas bordas, então o que aparece é sombra.
    ///
    /// Nasce invisível: quem decide o quanto dela aparece é o guia, em tempo de
    /// execução. É a mesma máscara para as sete portas — esticada no retângulo
    /// de cada uma, e a Forja estreita e o Mercado largo recebem a mesma queda
    /// suave na proporção deles.
    /// </summary>
    static void Sombrear(Transform sala)
    {
        Sprite mascara = AssetDatabase.LoadAssetAtPath<Sprite>(Pasta + "sombra_porta.png");
        if (mascara == null)
        {
            Debug.LogWarning($"Vestir a Guilda: {Pasta}sombra_porta.png não encontrado — "
                           + "a porta sem motivo vai continuar apagando o recorte inteiro.");
            return;
        }

        Transform achado = sala.Find("Sombra");
        GameObject go;

        if (achado != null)
        {
            go = achado.gameObject;
        }
        else
        {
            go = new GameObject("Sombra", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(go, "Vestir a Guilda");
            go.transform.SetParent(sala, false);
        }

        // Logo acima do recorte e abaixo de tudo o mais: a sombra escurece a
        // pintura, não o nome da sala nem a moldura que a acende.
        go.transform.SetSiblingIndex(1);

        var rt = go.GetComponent<RectTransform>();
        Undo.RecordObject(rt, "Vestir a Guilda");
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = go.GetComponent<Image>();
        if (img == null) img = go.AddComponent<Image>();

        Undo.RecordObject(img, "Vestir a Guilda");
        img.sprite = mascara;
        img.color = new Color(0f, 0f, 0f, 0f);
        img.raycastTarget = false;
        img.preserveAspect = false;
        img.type = Image.Type.Simple;
        EditorUtility.SetDirty(img);
    }

    /// <summary>
    /// Os recortes chegam como textura comum e precisam virar sprite de
    /// interface, ou o <c>LoadAssetAtPath&lt;Sprite&gt;</c> devolve nulo e a
    /// guilda fica sem arte sem dizer por quê. Mesma armadilha que o
    /// <see cref="EventArt"/> já tratava para as cenas dos eventos.
    /// </summary>
    static void AjustarImportacao()
    {
        var ajustados = new List<string>();

        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art/Guild" }))
        {
            string caminho = AssetDatabase.GUIDToAssetPath(guid);
            var imp = AssetImporter.GetAtPath(caminho) as TextureImporter;
            if (imp == null || imp.textureType == TextureImporterType.Sprite) continue;

            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.mipmapEnabled = false;
            imp.maxTextureSize = 4096;
            imp.SaveAndReimport();
            ajustados.Add(System.IO.Path.GetFileName(caminho));
        }

        if (ajustados.Count > 0)
            Debug.Log($"Vestir a Guilda: {ajustados.Count} imagem(ns) reimportada(s) como sprite.");
    }

    static void GarantirSombra(TMP_Text texto)
    {
        var sombra = texto.GetComponent<Shadow>();
        if (sombra == null) sombra = Undo.AddComponent<Shadow>(texto.gameObject);

        Undo.RecordObject(sombra, "Vestir a Guilda");
        sombra.effectColor = new Color(0f, 0f, 0f, 0.9f);
        sombra.effectDistance = new Vector2(2f, -2f);
        EditorUtility.SetDirty(sombra);
    }

    /// <summary>Sem acento e sem caixa, para casar com o nome do objeto na cena.</summary>
    static string Chave(string nome)
    {
        if (string.IsNullOrEmpty(nome)) return "";

        string limpo = nome.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();

        foreach (char c in limpo)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                == System.Globalization.UnicodeCategory.NonSpacingMark) continue;

            if (char.IsLetter(c)) sb.Append(char.ToLowerInvariant(c));
        }

        return sb.ToString();
    }

    static void EditorSceneManagerSalvar()
    {
        var cena = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(cena);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(cena);
    }
}
#endif
