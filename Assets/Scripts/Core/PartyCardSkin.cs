#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Põe rosto no card do grupo — o que aparece na estrada e no combate.
///
/// Tools → Guild of Legends → Vestir Card do Grupo
///
/// O prefab era só texto e barras (Name, HP, Stress, State, Block, HPBar,
/// StressBar). Num jogo em que a party é o elenco e a morte é permanente, o
/// jogador olha essa fileira o tempo todo e não via quem estava lá — só nomes.
///
/// O retrato entra como PRIMEIRO irmão, atrás de tudo: é a lição que custou duas
/// regressões nesta sessão. Um Image acrescentado no fim da lista é desenhado por
/// cima e apaga justamente os números que o card existe para mostrar.
/// </summary>
public static class PartyCardSkin
{
    const string Caminho = "Assets/Prefabs/UI/PartyStatusPrefab.prefab";
    const string MolduraSprite = "Assets/Alebardium/Bloodlines UI/Textures/Frame/Frame_outline_v2.png";

    [MenuItem("Tools/Guild of Legends/Vestir Card do Grupo")]
    public static void Vestir()
    {
        GameObject raiz = PrefabUtility.LoadPrefabContents(Caminho);
        if (raiz == null)
        {
            Debug.LogError($"PartyCardSkin: prefab não encontrado — {Caminho}");
            return;
        }

        // --- Retrato ---
        Transform existente = raiz.transform.Find("Portrait");
        GameObject retrato = existente != null ? existente.gameObject : null;

        if (retrato == null)
        {
            retrato = new GameObject("Portrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            retrato.transform.SetParent(raiz.transform, false);
        }

        // Ocupa o card inteiro, como plano de fundo.
        //
        // A primeira versão punha o rosto em tamanho cheio na metade de cima, e
        // o resultado foi ilegível: o card do grupo tem ~190x165 e precisa caber
        // nome, HP, estresse, estado e duas barras. Retrato e número disputando a
        // mesma área deixam os dois ruins. Rebaixado a fundo translúcido, ele dá
        // identidade ao herói sem cobrir a informação — que é o que o jogador
        // realmente lê durante a luta.
        var rt = retrato.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(4f, 4f);
        rt.offsetMax = new Vector2(-4f, -4f);

        var img = retrato.GetComponent<Image>();
        img.preserveAspect = true;
        img.raycastTarget = false;
        img.color = new Color(1f, 1f, 1f, 0f);   // invisível até receber um rosto

        // Atrás de tudo: HP, estresse e estado precisam ficar por cima.
        retrato.transform.SetAsFirstSibling();

        // --- Moldura do card ---
        var fundo = raiz.GetComponent<Image>();
        if (fundo != null)
        {
            Sprite moldura = Carregar(MolduraSprite);
            if (moldura != null)
            {
                fundo.sprite = moldura;
                fundo.type = Image.Type.Sliced;
                fundo.color = new Color(0.20f, 0.19f, 0.17f);
                EditorUtility.SetDirty(fundo);
            }
        }

        // --- Texto legível sobre fundo escuro ---
        Clarear(raiz, "Name", new Color(0.92f, 0.89f, 0.81f));
        Clarear(raiz, "HP", new Color(0.92f, 0.89f, 0.81f));
        Clarear(raiz, "Stress", new Color(0.78f, 0.74f, 0.66f));
        Clarear(raiz, "State", new Color(0.78f, 0.74f, 0.66f));

        EditorUtility.SetDirty(retrato);
        PrefabUtility.SaveAsPrefabAsset(raiz, Caminho);
        PrefabUtility.UnloadPrefabContents(raiz);
        AssetDatabase.SaveAssets();

        Debug.Log("PartyCardSkin: card do grupo vestido (retrato atrás, moldura e texto claro).");
    }

    static void Clarear(GameObject raiz, string nome, Color cor)
    {
        Transform t = raiz.GetComponentsInChildren<Transform>(true)
                          .FirstOrDefault(x => x.gameObject.name == nome);
        if (t == null) return;

        var txt = t.GetComponent<TMP_Text>();
        if (txt == null) return;

        txt.color = cor;
        EditorUtility.SetDirty(txt);
    }

    static Sprite Carregar(string caminho)
    {
        Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(caminho);
        if (s != null) return s;
        return AssetDatabase.LoadAllAssetsAtPath(caminho).OfType<Sprite>().FirstOrDefault();
    }
}
#endif
