#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dá cenário às portas da guilda. Tools → Guild of Legends → Vestir a Guilda ·
/// gatilho <c>RunGuildArt.trigger</c>.
///
/// <b>O que havia.</b> Sete retângulos pretos com o nome da sala escrito no meio,
/// sobre um mármore tão escuro que não aparecia. A queixa do autor foi direta:
/// <i>"as áreas do mapa da guilda são só botões, gostaria que fosse igual o
/// Darkest Dungeon, parte do cenário"</i>. E é literal — não havia imagem
/// nenhuma na tela inicial do jogo.
///
/// <b>De onde vem a arte.</b> Das mesmas 27 cenas do <i>Dwarves and
/// Underground</i> que os eventos usam. Nenhuma foi pintada para ser uma taverna
/// ou uma forja, então a escolha é por <b>luz e assunto</b>: o forno vermelho de
/// <c>Interior 8</c> é a forja de qualquer jogo; a rua de barracas de
/// <c>City 7</c> é a taverna; os menires na névoa de <c>Lake 1</c> são o
/// cemitério. Cada empréstimo está anotado na tabela, como no
/// <see cref="MapArtBuilder"/> e no <see cref="EventArt"/>.
///
/// <b>O véu existe para o nome ser lido.</b> A cena entra atrás do rótulo, e
/// pintura clara sob texto claro apaga o texto — foi o que aconteceu com a arte
/// dos eventos. Aqui o véu é escuro e o nome ganha sombra própria.
/// </summary>
public static class GuildArt
{
    const string Pasta = "Assets/Dwarves and Underground/";

    /// <summary>
    /// Sala → cena, pelo nome do objeto dentro de <c>Background/GuildMap</c>.
    ///
    /// A chave é comparada sem acento e sem caixa: os objetos da cena vêm de
    /// versões diferentes do projeto e nem todos foram batizados igual.
    /// </summary>
    /// <summary>
    /// Procurado por <b>trecho</b> do nome, e não por igualdade: os objetos da
    /// cena vêm de versões diferentes do projeto e nem todos foram batizados
    /// igual — "Sala de Mapas", "SalaMapas", "MapRoom". Uma tabela de chaves
    /// exatas deixou a Sala de Mapas sem cena na primeira aplicação, e o silêncio
    /// só apareceu na captura.
    /// </summary>
    static readonly (string trecho, string arquivo, string porque)[] CenaPorSala =
    {
        ("taverna",    "City 7",     "rua de barracas com lanternas acesas — o único lugar do acervo com gente e luz quente"),
        ("tavern",     "City 7",     "idem, para o objeto em inglês"),
        ("biblioteca", "Interior 7", "salão de colunas e arcos dourados, o mais próximo de um arquivo"),
        ("librar",     "Interior 7", "idem"),
        ("forja",      "Interior 8", "o forno aceso no fundo da câmara; é a forja de qualquer jogo"),
        ("forge",      "Interior 8", "idem"),
        ("mercado",    "Interior 6", "a caverna de chão dourado — riqueza é o assunto da sala"),
        ("market",     "Interior 6", "idem"),
        ("cemiterio",  "Lake 1",     "menires na névoa fria, e um caminho de pedra entre eles"),
        ("cemetery",   "Lake 1",     "idem"),
        ("mapa",       "Tunnel 13",  "salão com tapete e janelas altas: onde se planeja, não onde se anda"),
        ("map",        "Tunnel 13",  "idem"),
        ("jornada",    "Fortress 1", "a fortaleza distante na névoa: o destino, e não a guilda"),
        ("journey",    "Fortress 1", "idem"),
        ("quest",      "Fortress 1", "idem")
    };

    /// <summary>
    /// Quanto a cena aparece. Escuro de propósito: o rótulo da sala é texto claro
    /// por cima, e a tela inteira tem sete destes lado a lado — em opacidade alta
    /// viram sete pinturas brigando entre si, e nenhuma lê como porta.
    /// </summary>
    static readonly Color Veu = new Color(0.62f, 0.60f, 0.58f, 0.85f);

    [MenuItem("Tools/Guild of Legends/Vestir a Guilda")]
    public static void Aplicar()
    {
        Canvas canvas = Object.FindObjectOfType<Canvas>();
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

        int vestidas = 0;
        var semCena = new List<string>();

        foreach (Transform sala in mapa)
        {
            if (sala.GetComponent<Button>() == null) continue;

            string chave = Chave(sala.name);

            string arquivo = null;
            foreach (var candidata in CenaPorSala)
            {
                if (!chave.Contains(candidata.trecho)) continue;
                arquivo = candidata.arquivo;
                break;
            }

            if (arquivo == null)
            {
                semCena.Add($"{sala.name} (chave '{chave}')");
                continue;
            }

            Sprite cena = AssetDatabase.LoadAssetAtPath<Sprite>(Pasta + arquivo + ".jpg");
            if (cena == null)
            {
                semCena.Add($"{sala.name} → {arquivo} (arquivo não encontrado)");
                continue;
            }

            Vestir(sala, cena);
            vestidas++;
        }

        ClarearOFundo(mapa);
        EditorSceneManagerSalvar();

        Debug.Log($"Vestir a Guilda: {vestidas} porta(s) ganharam cenário.");

        if (semCena.Count > 0)
            Debug.LogWarning($"Vestir a Guilda: sem cena na tabela — {string.Join(", ", semCena)}");
    }

    /// <summary>
    /// O mármore do salão da guilda estava tingido tão escuro que a textura não
    /// aparecia: a tela inicial do jogo era um retângulo preto com sete portas
    /// flutuando. O tint sobe para deixar a pedra visível — o sprite continua o
    /// mesmo, e é ele que dá a superfície.
    ///
    /// Só sobe: um fundo que alguém já tenha clareado à mão fica como está.
    /// </summary>
    static void ClarearOFundo(Transform mapa)
    {
        foreach (Transform alvo in new[] { mapa, mapa.parent })
        {
            if (alvo == null) continue;

            var img = alvo.GetComponent<Image>();
            if (img == null || img.sprite == null) continue;

            Color c = img.color;
            float luz = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
            if (luz >= 0.34f) continue;

            Undo.RecordObject(img, "Clarear o fundo da guilda");
            img.color = new Color(0.42f, 0.40f, 0.38f, c.a);
            EditorUtility.SetDirty(img);
        }
    }

    static void Vestir(Transform sala, Sprite cena)
    {
        // A cena é o PRIMEIRO filho: o rótulo, a moldura e o realce nascem depois
        // e continuam por cima. Ordem de irmãos é ordem de desenho.
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
        img.sprite = cena;
        img.color = Veu;
        img.raycastTarget = false;

        // Sem preserveAspect: a porta precisa ficar preenchida de ponta a ponta.
        // São pinturas de fundo, e esticá-las um pouco não se nota — deixar tarja
        // preta em volta, sim.
        img.preserveAspect = false;
        img.type = Image.Type.Simple;

        EditorUtility.SetDirty(img);

        // O nome da sala sobre pintura precisa de sombra, ou some no primeiro
        // trecho claro da cena.
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
    /// Uma cópia preta atrás do rótulo. É o mesmo recurso que os números do
    /// combate ganharam: contorno de verdade exigiria material próprio de TMP, e
    /// um material a mais por texto é peso e uma coisa a mais para sair de
    /// sincronia.
    /// </summary>
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
