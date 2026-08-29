#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// Gera a fonte do corpo do jogo. Tools → Guild of Legends → Gerar fonte do corpo ·
/// gatilho <c>RunBodyFont.trigger</c>.
///
/// <b>O que havia.</b> Seis nomes de objeto ganhavam a MedievalSharp em
/// <see cref="GuildSceneSetup"/>; todo o resto do jogo — botões, cartas, eventos,
/// descrições de sala — ficava na LiberationSans, que é a fonte padrão do
/// TextMeshPro. É a tipografia de protótipo, e é o que mais denuncia a tela.
///
/// <b>Por que não deu para usar a Crimson que já estava no projeto.</b> O
/// <c>Crimson-Bold SDF.asset</c> do pacote DefaceGames é estático e tem
/// <b>98 glifos</b>: ASCII, mais o espaço fixo e as reticências. Nenhum acento.
/// Vesti-lo no corpo transformaria "missão" em "miss o" e "herói" em "her i" —
/// silenciosamente, porque glifo ausente não gera erro no console.
///
/// <b>O que esta ferramenta faz.</b> Gera um asset novo a partir do mesmo
/// <c>Crimson-Bold.otf</c>, com o Latin-1 inteiro (que é onde moram os acentos do
/// português) mais os símbolos que a UI escreve à mão. O original fica onde está.
///
/// <b>O atlas é fechado de propósito.</b> Populado de uma vez e congelado em
/// estático. Fonte dinâmica acrescenta glifo ao atlas conforme o texto aparece, e
/// quando o atlas enche o TMP grava textura nula dentro do asset — foi o que
/// aconteceu com a fonte de emoji, e a UI quebrava só na sessão seguinte.
/// </summary>
public static class BodyFont
{
    const string OtfPath = "Assets/DefaceGames/fonts/Crimson-Bold.otf";
    const string DestPath = "Assets/Fonts/Crimson-Bold PT SDF.asset";

    /// <summary>
    /// 72 e não 90 (o da MedievalSharp): o mesmo atlas de 1024 precisa acomodar
    /// ~90 glifos a mais, e a UI escreve entre corpo 13 e 32 — a resolução sobra.
    /// </summary>
    const int PontoDeAmostragem = 72;
    const int Preenchimento = 8;
    const int LarguraDoAtlas = 1024;
    const int AlturaDoAtlas = 1024;

    /// <summary>
    /// Símbolos que o código escreve direto na string e que não estão no Latin-1.
    ///
    /// Emoji não entra: 💰 ⚔️ 🏹 ⚡ vêm do fallback global do TMP Settings, que é o
    /// <c>SegoeUIEmoji SDF</c> — e é assim que eles aparecem hoje.
    /// </summary>
    const string SimbolosDaUI = "◆◇→←↑↓–—‘’“”…•";

    [MenuItem("Tools/Guild of Legends/Gerar fonte do corpo")]
    public static void Gerar()
    {
        Font otf = AssetDatabase.LoadAssetAtPath<Font>(OtfPath);
        if (otf == null)
        {
            Debug.LogError($"Fonte do corpo: {OtfPath} não encontrado.");
            return;
        }

        TMP_FontAsset fonte = TMP_FontAsset.CreateFontAsset(
            otf, PontoDeAmostragem, Preenchimento, GlyphRenderMode.SDFAA,
            LarguraDoAtlas, AlturaDoAtlas, AtlasPopulationMode.Dynamic, false);

        if (fonte == null)
        {
            Debug.LogError("Fonte do corpo: o TMP não conseguiu criar o asset.");
            return;
        }

        fonte.name = System.IO.Path.GetFileNameWithoutExtension(DestPath);

        string pedidos = Caracteres();
        fonte.TryAddCharacters(pedidos, out string faltando);

        // Congelado antes de salvar: a partir daqui nenhum texto do jogo pode
        // fazer o atlas crescer sozinho.
        fonte.atlasPopulationMode = AtlasPopulationMode.Static;

        PorRedeDeSeguranca(fonte);
        Salvar(fonte);

        int glifos = fonte.characterTable != null ? fonte.characterTable.Count : 0;
        Debug.Log($"Fonte do corpo: {glifos} glifos em {LarguraDoAtlas}×{AlturaDoAtlas} → {DestPath}");

        if (!string.IsNullOrEmpty(faltando))
            Debug.LogWarning($"Fonte do corpo: a Crimson-Bold não desenha {faltando.Length} caractere(s): {faltando}");

        ConferirAtlas(fonte);
    }

    #region Vestir o jogo

    const string LiberationPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

    /// <summary>
    /// Põe a fonte nova no corpo do jogo. Tools → Guild of Legends → Vestir o
    /// corpo com a fonte · gatilho <c>RunBodyFontApply.trigger</c>.
    ///
    /// São dois alcances, e os dois são necessários:
    ///
    /// <b>O padrão do TMP Settings</b> cobre o texto que nasce em execução — os
    /// candidatos da taverna, os nichos da estante, os nós do mapa. Metade da UI
    /// é instanciada, e nenhuma varredura de cena alcança isso.
    ///
    /// <b>A varredura</b> cobre o que já está gravado na cena e nos prefabs, que
    /// guardam a LiberationSans no arquivo e não mudariam sozinhos.
    ///
    /// Só troca quem está na LiberationSans. Os seis títulos que o
    /// <see cref="GuildSceneSetup"/> veste com a MedievalSharp ficam onde estão —
    /// título e corpo são decisões separadas.
    /// </summary>
    [MenuItem("Tools/Guild of Legends/Vestir o corpo com a fonte")]
    public static void Vestir()
    {
        var corpo = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DestPath);
        if (corpo == null)
        {
            Debug.LogError($"Vestir o corpo: {DestPath} não existe — rode 'Gerar fonte do corpo' antes.");
            return;
        }

        var liberation = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LiberationPath);

        int noPadrao = TrocarPadraoDoTMP(corpo) ? 1 : 0;
        int emPrefabs = VestirPrefabs(corpo, liberation);
        int naCena = VestirCenas(corpo, liberation);

        AssetDatabase.SaveAssets();

        Debug.Log($"Vestir o corpo: padrão do TMP {(noPadrao == 1 ? "trocado" : "já estava")}, "
                + $"{emPrefabs} texto(s) em prefabs, {naCena} na(s) cena(s).");
    }

    /// <summary>
    /// O <c>m_defaultFontAsset</c> é privado: só o SerializedObject escreve nele,
    /// e é assim que a janela de Project Settings do TMP faz.
    /// </summary>
    static bool TrocarPadraoDoTMP(TMP_FontAsset corpo)
    {
        var settings = AssetDatabase.LoadAssetAtPath<Object>(TmpSettingsPath);
        if (settings == null)
        {
            Debug.LogWarning($"Vestir o corpo: {TmpSettingsPath} não encontrado.");
            return false;
        }

        var so = new SerializedObject(settings);
        SerializedProperty padrao = so.FindProperty("m_defaultFontAsset");

        if (padrao == null)
        {
            Debug.LogWarning("Vestir o corpo: m_defaultFontAsset não existe neste TMP Settings.");
            return false;
        }

        if (padrao.objectReferenceValue == corpo) return false;

        padrao.objectReferenceValue = corpo;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
        return true;
    }

    static int VestirPrefabs(TMP_FontAsset corpo, TMP_FontAsset liberation)
    {
        int trocados = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject raiz = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (raiz == null) continue;

            int aqui = Trocar(raiz, corpo, liberation);
            if (aqui == 0) continue;

            trocados += aqui;
            EditorUtility.SetDirty(raiz);
            PrefabUtility.SavePrefabAsset(raiz);
        }

        return trocados;
    }

    static int VestirCenas(TMP_FontAsset corpo, TMP_FontAsset liberation)
    {
        int trocados = 0;
        string aberta = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path;

        foreach (string caminho in new[] { MenuSceneSetup.CaminhoDoJogo, MenuSceneSetup.CaminhoDoMenu })
        {
            if (!System.IO.File.Exists(caminho)) continue;

            var cena = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(caminho);
            int aqui = 0;

            foreach (GameObject raiz in cena.GetRootGameObjects())
                aqui += Trocar(raiz, corpo, liberation);

            if (aqui > 0)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(cena);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(cena);
            }

            trocados += aqui;
        }

        // Devolve o Editor à cena em que o autor estava.
        if (!string.IsNullOrEmpty(aberta) && aberta != UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path)
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(aberta);

        return trocados;
    }

    /// <summary>
    /// Troca a fonte de quem está na LiberationSans — e só dela.
    ///
    /// Fonte nula também entra: é o texto que herda o padrão do TMP, e depois da
    /// troca do padrão ele já seria a Crimson de qualquer jeito. Gravar a
    /// referência deixa o arquivo dizendo o que a tela mostra.
    /// </summary>
    static int Trocar(GameObject raiz, TMP_FontAsset corpo, TMP_FontAsset liberation)
    {
        int trocados = 0;

        foreach (TMP_Text texto in raiz.GetComponentsInChildren<TMP_Text>(true))
        {
            if (texto.font == corpo) continue;
            if (texto.font != null && texto.font != liberation) continue;

            Undo.RecordObject(texto, "Vestir o corpo");
            texto.font = corpo;
            EditorUtility.SetDirty(texto);
            trocados++;
        }

        return trocados;
    }

    #endregion

    /// <summary>
    /// A LiberationSans como fallback da fonte nova.
    ///
    /// A Crimson-Bold é uma fonte de texto e não desenha ◆ nem ◇ — que são
    /// justamente os dois glifos com que a Biblioteca marca o veredito de cada
    /// carta. Sem rede, eles sumiriam da tela sem uma linha no console.
    ///
    /// O fallback vale para qualquer glifo que faltar, e não só para estes dois:
    /// a lista de caracteres escrita à mão sempre esquece um, e o preço de errar
    /// é texto que desaparece em silêncio.
    /// </summary>
    static void PorRedeDeSeguranca(TMP_FontAsset fonte)
    {
        var liberation = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");

        if (liberation == null)
        {
            Debug.LogWarning("Fonte do corpo: LiberationSans não encontrada — sem fallback para glifo ausente.");
            return;
        }

        fonte.fallbackFontAssetTable = new List<TMP_FontAsset> { liberation };
    }

    /// <summary>
    /// ASCII imprimível, o Latin-1 inteiro e os símbolos da UI.
    ///
    /// O Latin-1 vai fechado em vez de caractere a caractere porque é barato e
    /// porque a lista escrita à mão é a que esquece o "ü" de um nome gerado ou o
    /// "ª" de uma data — e o que falta some sem avisar.
    /// </summary>
    static string Caracteres()
    {
        var sb = new StringBuilder();

        for (int c = 32; c <= 126; c++) sb.Append((char)c);
        for (int c = 160; c <= 255; c++) sb.Append((char)c);

        sb.Append(SimbolosDaUI);

        return sb.ToString();
    }

    /// <summary>
    /// O asset, a textura do atlas e o material são um arquivo só — é como o
    /// Font Asset Creator grava, e é o que faz a fonte sobreviver a um reimport.
    /// </summary>
    static void Salvar(TMP_FontAsset fonte)
    {
        string pasta = System.IO.Path.GetDirectoryName(DestPath);
        if (!AssetDatabase.IsValidFolder(pasta))
            AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(pasta),
                                       System.IO.Path.GetFileName(pasta));

        AssetDatabase.DeleteAsset(DestPath);
        AssetDatabase.CreateAsset(fonte, DestPath);

        if (fonte.atlasTextures != null)
        {
            foreach (Texture2D atlas in fonte.atlasTextures)
            {
                if (atlas == null) continue;
                atlas.name = fonte.name + " Atlas";
                AssetDatabase.AddObjectToAsset(atlas, fonte);
            }
        }

        if (fonte.material != null)
        {
            fonte.material.name = fonte.name + " Material";
            AssetDatabase.AddObjectToAsset(fonte.material, fonte);
        }

        EditorUtility.SetDirty(fonte);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(DestPath);
    }

    /// <summary>
    /// A prova de que o atlas não estourou: textura viva, e todo caractere pedido
    /// presente na tabela. Contar glifos não basta — a fonte de emoji também
    /// tinha tabela cheia com a textura já nula.
    /// </summary>
    static void ConferirAtlas(TMP_FontAsset fonte)
    {
        var reimportada = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DestPath);
        if (reimportada == null)
        {
            Debug.LogError("Fonte do corpo: o asset não voltou do AssetDatabase.");
            return;
        }

        bool texturaViva = reimportada.atlasTextures != null
                        && reimportada.atlasTextures.Length > 0
                        && reimportada.atlasTextures[0] != null
                        && reimportada.atlasTextures[0].width > 0;

        if (!texturaViva)
        {
            Debug.LogError("Fonte do corpo: o atlas voltou sem textura — não vista nada com esta fonte.");
            return;
        }

        // Distingue os dois casos: o que a Crimson não desenha mas o fallback
        // cobre, e o que não existe em lugar nenhum — só o segundo some da tela.
        var noFallback = new List<char>();
        var perdidosDeVez = new List<char>();

        foreach (char c in Caracteres())
        {
            if (c == ' ' || reimportada.HasCharacter(c)) continue;

            if (reimportada.HasCharacter(c, true, false)) noFallback.Add(c);
            else perdidosDeVez.Add(c);
        }

        if (noFallback.Count > 0)
            Debug.Log($"Fonte do corpo: {noFallback.Count} vem do fallback → {new string(noFallback.ToArray())}");

        if (perdidosDeVez.Count > 0)
            Debug.LogWarning($"Fonte do corpo: {perdidosDeVez.Count} sem glifo em lugar nenhum → "
                           + new string(perdidosDeVez.ToArray()));

        // Os que o português não pode perder, conferidos por nome.
        const string DoPortugues = "áàâãéêíóôõúüçÁÀÂÃÉÊÍÓÔÕÚÜÇ";
        string perdidos = new string(DoPortugues.Where(c => !reimportada.HasCharacter(c)).ToArray());

        if (perdidos.Length > 0)
            Debug.LogError($"Fonte do corpo: FALTAM ACENTOS ({perdidos}) — a fonte não serve ao corpo.");
        else
            Debug.Log("Fonte do corpo: acentos do português conferidos, atlas com textura.");
    }
}
#endif
