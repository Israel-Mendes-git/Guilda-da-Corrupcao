#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Veste a carta: moldura do kit na frente, cor por raridade na borda.
///
/// Tools → Guild of Legends → Vestir Carta
///
/// A frente usa o Bloodlines, e não os versos do Card_Shirts_Lite, por decisão do
/// autor: aqueles são pintados em madeira e metal alaranjados, e puxariam a carta
/// para longe da paleta dessaturada do resto. O sleeve fica para o verso — o
/// baralho e o descarte —, onde um estilo diferente não compete com o ícone.
///
/// A ordem de irmãos manda aqui como mandou no HeroPanel: o fundo é o Image do
/// próprio root (desenhado antes dos filhos), então vesti-lo é seguro. O "Border"
/// existe separado e recebe a cor da raridade.
/// </summary>
public static class CardFrameSkin
{
    const string Caminho = "Assets/Prefabs/UI/CardPrefab.prefab";
    const string MolduraSprite = "Assets/Alebardium/Bloodlines UI/Textures/Frame/Frame_outline_v2.png";
    const string FundoSprite = "Assets/Alebardium/Bloodlines UI/Textures/Frame/Frame_background.png";

    [MenuItem("Tools/Guild of Legends/Vestir Carta")]
    public static void Vestir()
    {
        Sprite moldura = Carregar(MolduraSprite);
        Sprite fundoSprite = Carregar(FundoSprite);

        if (moldura == null || fundoSprite == null)
        {
            Debug.LogError("CardFrameSkin: kit Bloodlines não encontrado.");
            return;
        }

        GameObject raiz = PrefabUtility.LoadPrefabContents(Caminho);
        if (raiz == null)
        {
            Debug.LogError($"CardFrameSkin: prefab não encontrado — {Caminho}");
            return;
        }

        // Fundo da carta: o Image do root, que é desenhado antes de tudo.
        var fundo = raiz.GetComponent<Image>();
        if (fundo != null)
        {
            fundo.sprite = fundoSprite;
            fundo.type = Image.Type.Sliced;
            // A cor fica com o CardUI/JourneyManager, que já pinta por raridade.
            EditorUtility.SetDirty(fundo);
        }

        // Moldura por cima, sem bloquear clique nem cobrir a arte.
        Transform borda = raiz.transform.Find("Border");
        if (borda != null)
        {
            var img = borda.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = moldura;
                img.type = Image.Type.Sliced;
                img.raycastTarget = false;
                EditorUtility.SetDirty(img);
            }
        }
        else
        {
            Debug.LogWarning("CardFrameSkin: 'Border' não encontrado — a carta fica sem moldura.");
        }

        PrefabUtility.SaveAsPrefabAsset(raiz, Caminho);
        PrefabUtility.UnloadPrefabContents(raiz);
        AssetDatabase.SaveAssets();

        Debug.Log("CardFrameSkin: carta vestida com moldura do kit.");
    }

    static Sprite Carregar(string caminho)
    {
        Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(caminho);
        if (s != null) return s;
        return AssetDatabase.LoadAllAssetsAtPath(caminho).OfType<Sprite>().FirstOrDefault();
    }
}
#endif
