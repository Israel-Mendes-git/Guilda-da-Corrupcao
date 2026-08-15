#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Veste o HeroPanel — o card de herói usado pela taverna e pelo roster.
///
/// Tools → Guild of Legends → Vestir HeroPanel
///
/// Feito à mão, e não por varredura, porque varredura já falhou aqui duas vezes:
/// vestir toda imagem grande tapou o texto, e pintar só o fundo deixou texto
/// escuro sobre fundo escuro. O que decide é a **ordem de irmãos**, e ela só se
/// conhece olhando este prefab:
///
///   BackgroundHero      ← pai dos textos: vestir é seguro, fica atrás
///     Image             ← irmão ANTES dos textos: seguro
///     HeroName / HeroClass / HeroLevel
///     Info Hero Panel   ← pai dos próprios textos: seguro
///       HeroHP / HeroSalary / HeroPersonality / HeroTrait
///       Image           ← irmão DEPOIS dos textos: vestir aqui APAGA o texto
///     Slider
///
/// Por isso o fundo é vestido pelo nome do objeto, nunca por tamanho, e o texto
/// é clareado no mesmo passo — fundo escuro com texto escuro é tão ilegível
/// quanto o branco que se queria substituir.
/// </summary>
public static class HeroPanelSkin
{
    const string Caminho = "Assets/Prefabs/UI/HeroPanel.prefab";
    const string PainelSprite = "Assets/Alebardium/Bloodlines UI/Textures/Frame/Frame_background.png";
    const string MolduraSprite = "Assets/Alebardium/Bloodlines UI/Textures/Notice/Frame_notice_v1.png";

    static readonly Color FundoCard = new Color(0.16f, 0.15f, 0.14f);
    static readonly Color FundoInfo = new Color(0.22f, 0.21f, 0.19f);
    static readonly Color TextoClaro = new Color(0.90f, 0.87f, 0.79f);
    static readonly Color TextoSuave = new Color(0.72f, 0.69f, 0.62f);

    [MenuItem("Tools/Guild of Legends/Vestir HeroPanel")]
    public static void Vestir()
    {
        Sprite painel = Carregar(PainelSprite);
        Sprite moldura = Carregar(MolduraSprite);

        if (painel == null)
        {
            Debug.LogError("HeroPanelSkin: kit Bloodlines não encontrado.");
            return;
        }

        GameObject raiz = PrefabUtility.LoadPrefabContents(Caminho);
        if (raiz == null)
        {
            Debug.LogError($"HeroPanelSkin: prefab não encontrado — {Caminho}");
            return;
        }

        // --- Fundos, pelo nome: são os únicos que ficam atrás do texto ---
        VestirFundo(raiz, "BackgroundHero", painel, FundoCard);
        VestirFundo(raiz, "Info Hero Panel", moldura != null ? moldura : painel, FundoInfo);

        // --- Texto: clarear junto, ou o fundo escuro o engole ---
        Clarear(raiz, "HeroName", TextoClaro, 1.0f);
        Clarear(raiz, "HeroClass", TextoClaro, 1.0f);
        Clarear(raiz, "HeroLevel", TextoSuave, 1.0f);
        Clarear(raiz, "HeroHP", TextoClaro, 1.0f);
        Clarear(raiz, "HeroSalary", TextoSuave, 1.0f);
        Clarear(raiz, "HeroPersonality", TextoSuave, 1.0f);
        Clarear(raiz, "HeroTrait", TextoSuave, 1.0f);

        PrefabUtility.SaveAsPrefabAsset(raiz, Caminho);
        PrefabUtility.UnloadPrefabContents(raiz);
        AssetDatabase.SaveAssets();

        Debug.Log("HeroPanelSkin: HeroPanel vestido (fundo escuro do kit + texto claro).");
    }

    static void VestirFundo(GameObject raiz, string nome, Sprite sprite, Color cor)
    {
        Transform t = Achar(raiz.transform, nome);
        if (t == null)
        {
            Debug.LogWarning($"HeroPanelSkin: '{nome}' não encontrado.");
            return;
        }

        var img = t.GetComponent<Image>();
        if (img == null)
        {
            Debug.LogWarning($"HeroPanelSkin: '{nome}' não tem Image.");
            return;
        }

        img.sprite = sprite;
        img.type = Image.Type.Sliced;
        img.color = cor;
        EditorUtility.SetDirty(img);
    }

    static void Clarear(GameObject raiz, string nome, Color cor, float alpha)
    {
        Transform t = Achar(raiz.transform, nome);
        if (t == null) return;

        var txt = t.GetComponent<TMP_Text>();
        if (txt == null) return;

        Color c = cor;
        c.a = alpha;
        txt.color = c;
        EditorUtility.SetDirty(txt);
    }

    /// <summary>Busca por nome em qualquer profundidade.</summary>
    static Transform Achar(Transform raiz, string nome)
    {
        return raiz.GetComponentsInChildren<Transform>(true)
                   .FirstOrDefault(t => t.gameObject.name == nome);
    }

    static Sprite Carregar(string caminho)
    {
        Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(caminho);
        if (s != null) return s;
        return AssetDatabase.LoadAllAssetsAtPath(caminho).OfType<Sprite>().FirstOrDefault();
    }
}
#endif
