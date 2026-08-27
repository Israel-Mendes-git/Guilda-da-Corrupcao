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
/// <b>Deserto e Vulcão eram os dois buracos da lista</b>, e agora são empréstimos
/// declarados. Nenhum pacote de cenário do projeto tem duna nem cratera; o que
/// existe são duas cenas pintadas do <i>Dwarves and Underground</i> — cristas
/// ocres e uma câmara de lava — trazidas junto com a arte dos eventos. Elas não
/// são silhuetas em camadas como as outras cinco, e isso se vê de perto: aqui o
/// fundo do bioma fica a 22% de opacidade atrás do mapa, onde o que chega ao
/// jogador é a cor da região, não o traço.
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

        // Empréstimo: cristas de rocha ocre sob poeira. Não é areia, é o deserto
        // de pedra — a cor bate, a duna não existe em pacote nenhum do projeto.
        (BiomeType.Desert,   "Mountains 4",      EventArt.Pasta),

        // Empréstimo: câmara de lava, portanto interior. O que ela entrega ao
        // painel é o laranja aceso, que é o que separa o Vulcão das outras seis
        // regiões à primeira vista.
        (BiomeType.Volcano,  "Molten 1",         EventArt.Pasta),
    };

    [MenuItem("Tools/Guild of Legends/Montar Catálogo de Biomas")]
    public static void Montar()
    {
        // As duas cenas do Dwarves chegam como JPG, que o Unity importa como
        // textura comum: sem este ajuste elas não são Sprite, e a busca abaixo
        // as daria como ausentes estando no disco. O ajuste mora no EventArt
        // porque a pasta é dele; aqui só se pede que ele já tenha rodado.
        EventArt.AjustarImportacao();

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
