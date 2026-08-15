#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Veste com o kit Bloodlines os prefabs de UI que são instanciados em runtime.
///
/// Tools → Guild of Legends → Aplicar Kit Visual nos Prefabs
///
/// O "Montar Cena" veste a cena, e por isso metade das telas continuava branca:
/// os candidatos da taverna, as cartas à venda, os nós do mapa e o status do
/// grupo não existem na cena — nascem de prefab quando a tela abre. Vestir a
/// cena e esquecer os prefabs deixa exatamente as telas mais usadas de fora.
/// </summary>
public static class UiSkinPrefabs
{
    const string Pasta = "Assets/Prefabs";

    [MenuItem("Tools/Guild of Legends/Aplicar Kit Visual nos Prefabs")]
    public static void Aplicar()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { Pasta });
        int vestidos = 0;

        foreach (string guid in guids)
        {
            string caminho = AssetDatabase.GUIDToAssetPath(guid);

            GameObject conteudo = PrefabUtility.LoadPrefabContents(caminho);
            if (conteudo == null) continue;

            bool ok = GuildSceneSetup.AplicarKitEm(conteudo);
            if (ok)
            {
                PrefabUtility.SaveAsPrefabAsset(conteudo, caminho);
                vestidos++;
            }

            PrefabUtility.UnloadPrefabContents(conteudo);

            if (!ok) break; // kit ausente: não adianta seguir
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"UiSkinPrefabs: {vestidos} prefabs vestidos com o kit.");
    }
}
#endif
