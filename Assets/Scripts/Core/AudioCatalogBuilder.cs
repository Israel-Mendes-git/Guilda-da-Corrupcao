#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Monta o <see cref="AudioCatalog"/> e arruma os import settings do áudio.
///
/// Tools → Guild of Legends → Montar Catálogo de Áudio
///
/// A escolha das faixas é por duração e por tema: as longas seguram hub e estrada
/// sem repetir cedo demais, e as curtas servem a combate e chefe, onde a cena
/// muda rápido. "horroraction" e "conjuring" são literalmente o que os nomes
/// dizem — luta e ritual.
/// </summary>
public static class AudioCatalogBuilder
{
    const string CaminhoCatalogo = "Assets/Resources/AudioCatalog.asset";
    const string Horror = "Assets/Horror Starter Pack";
    const string DarkKnight = "Assets/Dark Knight/Sounds";
    const string Kit = "Assets/Alebardium/Bloodlines UI/Audio";

    static readonly (MusicContext ctx, string arquivo, string pasta)[] Musicas =
    {
        (MusicContext.Hub,     "sp-theroom",       Horror),  // 3:24, ambiente parado
        (MusicContext.Journey, "sp-frontier",      Horror),  // 3:29, estrada longa
        (MusicContext.Combat,  "sp-horroraction",  Horror),  // 0:57, ação
        (MusicContext.Boss,    "sp-conjuring",     Horror),  // 0:57, ritual
    };

    static readonly (Sfx sfx, string arquivo, string pasta, float vol)[] Efeitos =
    {
        (Sfx.Click,     "Click Button SFX", Kit,        0.7f),
        (Sfx.Hover,     "Hover Button SFX", Kit,        0.4f),
        (Sfx.Attack,    "sword",            DarkKnight, 0.8f),
        (Sfx.Magic,     "beam",             DarkKnight, 0.7f),
        (Sfx.Hurt,      "pain",             DarkKnight, 0.6f),
        (Sfx.HeroDeath, "DeathExplosion",   DarkKnight, 0.9f),
        (Sfx.Step,      "footstep",         DarkKnight, 0.5f),
        (Sfx.Coin,      "power_load",       DarkKnight, 0.5f),
    };

    [MenuItem("Tools/Guild of Legends/Montar Catálogo de Áudio")]
    public static void Montar()
    {
        var musicas = new List<AudioCatalog.MusicEntry>();
        var efeitos = new List<AudioCatalog.SfxEntry>();
        var faltando = new List<string>();

        foreach (var m in Musicas)
        {
            AudioClip c = Achar(m.arquivo, m.pasta);
            if (c == null) { faltando.Add($"{m.ctx} → {m.arquivo}"); continue; }

            AjustarImport(c, musica: true);
            musicas.Add(new AudioCatalog.MusicEntry { context = m.ctx, clip = c });
        }

        foreach (var e in Efeitos)
        {
            AudioClip c = Achar(e.arquivo, e.pasta);
            if (c == null) { faltando.Add($"{e.sfx} → {e.arquivo}"); continue; }

            AjustarImport(c, musica: false);
            efeitos.Add(new AudioCatalog.SfxEntry { sfx = e.sfx, clip = c, volume = e.vol });
        }

        if (musicas.Count == 0 && efeitos.Count == 0)
        {
            Debug.LogError("AudioCatalog: nenhum áudio encontrado.");
            return;
        }

        AudioCatalog cat = AssetDatabase.LoadAssetAtPath<AudioCatalog>(CaminhoCatalogo);
        bool novo = cat == null;

        if (novo)
        {
            cat = ScriptableObject.CreateInstance<AudioCatalog>();
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
        }

        cat.musicas = musicas.ToArray();
        cat.efeitos = efeitos.ToArray();

        if (novo) AssetDatabase.CreateAsset(cat, CaminhoCatalogo);
        else EditorUtility.SetDirty(cat);

        AssetDatabase.SaveAssets();

        Debug.Log($"AudioCatalog: {musicas.Count} trilhas e {efeitos.Count} efeitos catalogados.");

        if (faltando.Count > 0)
            Debug.LogWarning($"AudioCatalog: não encontrado — {string.Join(", ", faltando)}");
    }

    /// <summary>
    /// Música em streaming e comprimida; efeito curto descomprimido na memória.
    ///
    /// Sem isso o Horror Starter Pack vai inteiro para o build: são 271 MB de
    /// AIFF sem compressão, mais que todo o resto do projeto somado.
    /// </summary>
    static void AjustarImport(AudioClip clip, bool musica)
    {
        string caminho = AssetDatabase.GetAssetPath(clip);
        var importer = AssetImporter.GetAtPath(caminho) as AudioImporter;
        if (importer == null) return;

        AudioImporterSampleSettings s = importer.defaultSampleSettings;
        s.compressionFormat = AudioCompressionFormat.Vorbis;
        s.loadType = musica ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
        s.quality = musica ? 0.6f : 0.85f;

        importer.defaultSampleSettings = s;
        importer.forceToMono = musica;   // trilha em mono corta metade do peso
        importer.loadInBackground = musica;

        importer.SaveAndReimport();
    }

    static AudioClip Achar(string nomeArquivo, string pasta)
    {
        if (!AssetDatabase.IsValidFolder(pasta)) return null;

        foreach (string guid in AssetDatabase.FindAssets($"{nomeArquivo} t:AudioClip", new[] { pasta }))
        {
            string caminho = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(caminho) != nomeArquivo) continue;

            AudioClip c = AssetDatabase.LoadAssetAtPath<AudioClip>(caminho);
            if (c != null) return c;
        }

        return null;
    }
}
#endif
