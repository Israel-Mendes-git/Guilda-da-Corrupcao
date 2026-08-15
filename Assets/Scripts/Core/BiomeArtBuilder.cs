#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Monta o <see cref="BiomeArtCatalog"/> a partir da arte de cenário importada.
///
/// Tools → Guild of Legends → Montar Catálogo de Biomas
///
/// Floresta, Pântano e Tundra saem do mesmo pacote (Distant Forest), o que os
/// deixa coerentes entre si — são as três regiões que o jogador mais atravessa.
/// Montanha e Ruínas usam a rocha escura do Pixel Fantasy Caves.
///
/// Deserto e Vulcão ficam de fora: nenhum pacote importado tem arte para eles, e
/// inventar um substituto que não combina seria pior do que a ausência.
/// </summary>
public static class BiomeArtBuilder
{
    const string CaminhoCatalogo = "Assets/Resources/BiomeArtCatalog.asset";

    /// <summary>Bioma → nome do arquivo da arte, procurado dentro do pacote.</summary>
    static readonly (BiomeType biome, string arquivo, string pasta)[] Mapa =
    {
        (BiomeType.Forest,   "ForestBgGreen_1",  "Assets/Distant Forest Assets"),
        (BiomeType.Swamp,    "SwampBgOrange_1",  "Assets/Distant Forest Assets"),
        (BiomeType.Tundra,   "TaigaBg_1",        "Assets/Distant Forest Assets"),
        (BiomeType.Mountain, "background2",      "Assets/Pixel Fantasy Caves"),
        (BiomeType.Ruins,    "background4a",     "Assets/Pixel Fantasy Caves"),
    };

    [MenuItem("Tools/Guild of Legends/Montar Catálogo de Biomas")]
    public static void Montar()
    {
        var entradas = new List<BiomeArtCatalog.Entrada>();
        var faltando = new List<string>();

        foreach (var item in Mapa)
        {
            Sprite s = Achar(item.arquivo, item.pasta);
            if (s == null)
            {
                faltando.Add($"{item.biome} → {item.arquivo}");
                continue;
            }

            entradas.Add(new BiomeArtCatalog.Entrada { biome = item.biome, arte = s });
        }

        if (entradas.Count == 0)
        {
            Debug.LogError("BiomeArt: nenhuma arte encontrada — os pacotes de cenário estão no projeto?");
            return;
        }

        BiomeArtCatalog catalogo = AssetDatabase.LoadAssetAtPath<BiomeArtCatalog>(CaminhoCatalogo);
        bool novo = catalogo == null;

        if (novo)
        {
            catalogo = ScriptableObject.CreateInstance<BiomeArtCatalog>();
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
        }

        catalogo.entradas = entradas.ToArray();

        if (novo) AssetDatabase.CreateAsset(catalogo, CaminhoCatalogo);
        else EditorUtility.SetDirty(catalogo);

        AssetDatabase.SaveAssets();

        var semArte = BiomeUtil.Playable.Where(b => !entradas.Any(e => e.biome == b)).ToArray();

        Debug.Log($"BiomeArt: {entradas.Count} biomas com arte "
                + $"({string.Join(", ", entradas.Select(e => e.biome))}).");

        if (semArte.Length > 0)
            Debug.LogWarning($"BiomeArt: sem arte — {string.Join(", ", semArte)}. "
                           + "Nenhum pacote importado cobre estes biomas.");
        if (faltando.Count > 0)
            Debug.LogWarning($"BiomeArt: arquivo não encontrado — {string.Join(", ", faltando)}");
    }

    static Sprite Achar(string nomeArquivo, string pasta)
    {
        if (!AssetDatabase.IsValidFolder(pasta)) return null;

        foreach (string guid in AssetDatabase.FindAssets($"{nomeArquivo} t:Sprite", new[] { pasta }))
        {
            string caminho = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(caminho) != nomeArquivo) continue;

            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(caminho);
            if (s != null) return s;

            Sprite sub = AssetDatabase.LoadAllAssetsAtPath(caminho).OfType<Sprite>().FirstOrDefault();
            if (sub != null) return sub;
        }

        return null;
    }
}
#endif
