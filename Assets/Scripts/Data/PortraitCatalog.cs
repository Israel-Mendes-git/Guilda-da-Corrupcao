using UnityEngine;

/// <summary>
/// O acervo de rostos que a guilda pode contratar.
///
/// Os retratos vivem no pacote em que foram importados; este catálogo só guarda
/// referências a eles. Fazer o contrário — copiar 500 imagens para Resources —
/// duplicaria dez megabytes no repositório e obrigaria a lembrar de sincronizar
/// as duas cópias.
///
/// É um asset em Resources porque a <see cref="HeroFactory"/> cria heróis em
/// runtime, fora de qualquer cena, e não tem Inspector onde receber a lista.
/// </summary>
[CreateAssetMenu(fileName = "PortraitCatalog", menuName = "Game/Catálogo de Retratos")]
public class PortraitCatalog : ScriptableObject
{
    [Tooltip("Preenchido por Tools ▸ Guild of Legends ▸ Montar Catálogo de Retratos.")]
    public Sprite[] portraits;

    private static PortraitCatalog instance;

    /// <summary>Carregado sob demanda; ausente, o jogo segue sem retrato.</summary>
    public static PortraitCatalog Instance
    {
        get
        {
            if (instance == null) instance = Resources.Load<PortraitCatalog>("PortraitCatalog");
            return instance;
        }
    }

    public bool IsEmpty => portraits == null || portraits.Length == 0;

    /// <summary>
    /// Um rosto para um herói. A escolha é estável para o mesmo nome: dois heróis
    /// diferentes recebem rostos diferentes, e o mesmo herói mantém o dele entre
    /// execuções — sem isso, cada recarga trocaria a cara de todo mundo, e o
    /// elenco é justamente o que o jogador aprende a reconhecer.
    /// </summary>
    public Sprite Para(string chave)
    {
        if (IsEmpty) return null;

        int hash = 17;
        foreach (char c in chave ?? string.Empty)
            hash = unchecked(hash * 31 + c);

        return portraits[Mathf.Abs(hash) % portraits.Length];
    }
}
