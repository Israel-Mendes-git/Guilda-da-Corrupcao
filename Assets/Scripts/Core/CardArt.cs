#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Dá rosto às cartas: preenche <see cref="CardData.cardImage"/>, que existia
/// desde sempre e estava vazio nas 17 cartas.
///
/// Tools → Guild of Legends → Aplicar Arte nas Cartas
///
/// A arte vem do pacote Blink, cujos ícones são pintados à mão e organizados por
/// arquétipo de classe. A escolha de cada um é pelo **desenho**, não pela classe:
/// o escudo rachado do Guardian é literalmente o que Postura Defensiva faz, e a
/// mão em chamas do Pyromancer é a Bola de Fogo. Onde o desenho não é literal, a
/// escolha cai no arquétipo que corresponde à classe da carta.
///
/// Três telas já leem esse campo e nenhuma precisou mudar — CardUI (combate),
/// CardInCard (gerenciador de deck) e JourneyManager (mão da jornada).
/// </summary>
public static class CardArt
{
    /// <summary>
    /// Carta → nome do arquivo do ícone. O ícone é procurado pelo nome em
    /// qualquer lugar sob Assets/Blink, e não por caminho fixo: a pasta usa
    /// "Beastmaster" enquanto o arquivo é "BeastMaster4", e caminho cravado
    /// quebraria nesse tipo de detalhe.
    /// </summary>
    static readonly Dictionary<string, string> ArtePorCarta = new Dictionary<string, string>
    {
        // Guerreiro — o arquétipo Warrior do pacote
        { "Postura Defensiva", "Guardian8" },      // escudo de madeira rachado
        { "Brado de Guerra",   "Barbarian14" },
        { "Corte Duplo",       "Berserker10" },
        { "Fúria",             "Berserker13" },
        { "Investida",         "Dragonknight4" },

        // Mago — Elementalist
        { "Bola de Fogo",      "Pyromancer2" },    // mão em chamas
        { "Escudo de Gelo",    "Cryomancer4" },
        { "Explosão Arcana",   "Arcanist1" },
        { "Teleporte",         "Electromancer3" },

        // Curandeiro — HolyDarkness
        { "Bênção",            "Priest8" },        // cajado sagrado
        { "Toque Curativo",    "Priest13" },
        { "Purificação",       "Paladin10" },
        { "Ressurgir",         "Medium15" },

        // Caçador — Assassin e Symbiose
        { "Flecha Precisa",    "Hunter1" },        // arco e flecha
        { "Flecha Lunar",      "Ranger2" },
        { "Armadilha",         "BeastMaster4" },
        { "Olhar de Águia",    "DemonHunter8" }
    };

    [MenuItem("Tools/Guild of Legends/Aplicar Arte nas Cartas")]
    public static void Aplicar()
    {
        CardData[] cartas = Resources.LoadAll<CardData>("Cards");
        if (cartas.Length == 0)
        {
            Debug.LogError("CardArt: nenhuma carta em Resources/Cards.");
            return;
        }

        int aplicadas = 0;
        var semMapa = new List<string>();
        var semArte = new List<string>();

        foreach (CardData carta in cartas)
        {
            string nome = carta.cardName;
            if (string.IsNullOrEmpty(nome)) nome = carta.name;

            if (!ArtePorCarta.TryGetValue(nome, out string icone))
            {
                semMapa.Add(nome);
                continue;
            }

            Sprite sprite = AcharSprite(icone);
            if (sprite == null)
            {
                semArte.Add($"{nome} → {icone}");
                continue;
            }

            if (carta.cardImage == sprite) continue;

            Undo.RecordObject(carta, "Aplicar arte nas cartas");
            carta.cardImage = sprite;
            EditorUtility.SetDirty(carta);
            aplicadas++;
        }

        AssetDatabase.SaveAssets();

        Debug.Log($"CardArt: {aplicadas} cartas receberam arte (de {cartas.Length}).");

        // Silêncio aqui seria pior que ruído: uma carta sem arte volta a ser um
        // retângulo vazio e ninguém percebe até ver a tela.
        if (semMapa.Count > 0)
            Debug.LogWarning($"CardArt: sem entrada na tabela — {string.Join(", ", semMapa)}");
        if (semArte.Count > 0)
            Debug.LogWarning($"CardArt: ícone não encontrado — {string.Join(", ", semArte)}");
    }

    /// <summary>Procura o sprite pelo nome do arquivo dentro do pacote Blink.</summary>
    static Sprite AcharSprite(string nomeArquivo)
    {
        string[] guids = AssetDatabase.FindAssets($"{nomeArquivo} t:Sprite", new[] { "Assets/Blink" });

        foreach (string guid in guids)
        {
            string caminho = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(caminho) != nomeArquivo) continue;

            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(caminho);
            if (s != null) return s;

            // Textura em modo Multiple guarda os sprites como sub-ativos.
            Sprite sub = AssetDatabase.LoadAllAssetsAtPath(caminho).OfType<Sprite>().FirstOrDefault();
            if (sub != null) return sub;
        }

        return null;
    }
}
#endif
