#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Monta o <see cref="PortraitCatalog"/> a partir dos retratos importados.
///
/// Tools → Guild of Legends → Montar Catálogo de Retratos
///
/// O catálogo guarda referências, não cópias: os 500 retratos continuam onde
/// foram importados, e só um asset pequeno entra em Resources.
/// </summary>
public static class PortraitCatalogBuilder
{
    const string PastaRetratos = "Assets/BIG PORTRAITS PACK (by Batareya)/PORTRAITS";
    const string CaminhoCatalogo = "Assets/Resources/PortraitCatalog.asset";

    [MenuItem("Tools/Guild of Legends/Montar Catálogo de Retratos")]
    public static void Montar()
    {
        if (!AssetDatabase.IsValidFolder(PastaRetratos))
        {
            Debug.LogError($"PortraitCatalog: pasta não encontrada — {PastaRetratos}");
            return;
        }

        // Ordem por número, não alfabética: "10" antes de "2" embaralharia o
        // acervo a cada reconstrução, e o rosto de cada herói mudaria junto.
        var sprites = AssetDatabase.FindAssets("t:Sprite", new[] { PastaRetratos })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Distinct()
            .Select(p => new
            {
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(p),
                ordem = int.TryParse(System.IO.Path.GetFileNameWithoutExtension(p), out int n) ? n : int.MaxValue
            })
            .Where(x => x.sprite != null)
            .OrderBy(x => x.ordem)
            .Select(x => x.sprite)
            .ToArray();

        if (sprites.Length == 0)
        {
            Debug.LogError("PortraitCatalog: nenhum sprite encontrado. Os PNGs estão importados como Sprite?");
            return;
        }

        PortraitCatalog catalogo = AssetDatabase.LoadAssetAtPath<PortraitCatalog>(CaminhoCatalogo);
        bool novo = catalogo == null;

        if (novo)
        {
            catalogo = ScriptableObject.CreateInstance<PortraitCatalog>();
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
        }

        catalogo.portraits = sprites;

        if (novo) AssetDatabase.CreateAsset(catalogo, CaminhoCatalogo);
        else EditorUtility.SetDirty(catalogo);

        AssetDatabase.SaveAssets();
        Debug.Log($"PortraitCatalog: {sprites.Length} retratos catalogados ({(novo ? "criado" : "atualizado")}).");
    }
}
#endif
