using UnityEngine;

/// <summary>
/// A cara de cada bioma. Hoje a jornada só diz o nome da região por texto e
/// emoji — o jogador atravessa Pântano e Tundra vendo exatamente a mesma tela.
///
/// Guarda referências à arte importada, sem copiá-la para Resources; mesma
/// escolha do <see cref="PortraitCatalog"/> e pelo mesmo motivo.
///
/// O acervo não cobre os sete biomas: Deserto e Vulcão não têm arte em nenhum
/// pacote importado, e ficam nulos de propósito. Quem lê precisa aguentar isso
/// sem quebrar — é melhor a região aparecer sem fundo do que a jornada travar.
/// </summary>
[CreateAssetMenu(fileName = "BiomeArtCatalog", menuName = "Game/Catálogo de Biomas")]
public class BiomeArtCatalog : ScriptableObject
{
    [System.Serializable]
    public class Entrada
    {
        public BiomeType biome;
        public Sprite arte;
    }

    [Tooltip("Preenchido por Tools ▸ Guild of Legends ▸ Montar Catálogo de Biomas.")]
    public Entrada[] entradas;

    private static BiomeArtCatalog instance;

    public static BiomeArtCatalog Instance
    {
        get
        {
            if (instance == null) instance = Resources.Load<BiomeArtCatalog>("BiomeArtCatalog");
            return instance;
        }
    }

    /// <summary>A arte do bioma, ou nulo quando ainda não existe arte para ele.</summary>
    public Sprite Para(BiomeType biome)
    {
        if (entradas == null) return null;

        foreach (Entrada e in entradas)
            if (e != null && e.biome == biome)
                return e.arte;

        return null;
    }
}
