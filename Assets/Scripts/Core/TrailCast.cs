using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quem representa cada classe quando o grupo aparece de corpo inteiro.
///
/// Os retratos dizem quem é o herói na ficha; na estrada é preciso um corpo que
/// ande. O único material do projeto com caminhada de verdade é o <b>SPUM</b>
/// (53 bonecos montados, com Idle, Move, Attack, Damaged e Death já prontos), e
/// os prefabs moram sob <c>Resources</c> — logo, carregam em runtime sem
/// catálogo intermediário.
///
/// <b>A escolha de cada boneco foi feita pelo que ele veste</b>, não pelo nome:
/// o prefab lista as peças em <c>ImageElement.ItemPath</c>, e é de lá que sai
/// "este carrega cajado", "este carrega arco". Os ids são carimbos de data do
/// gerador do SPUM e não dizem nada.
///
/// Tabela num lugar só, como a do <see cref="EnemyArt"/>: trocar a cara de uma
/// classe é trocar uma linha daqui.
/// </summary>
public static class TrailCast
{
    const string Raiz = "Addons/BasicPack/2_Prefab/";

    /// <summary>
    /// Classe → prefab, com a arma que motivou a escolha.
    ///
    /// Provisória e verificável por captura: nenhum destes bonecos foi visto
    /// antes de ser escolhido, porque o pacote não traz miniatura. A prova é a
    /// tela da jornada depois do Play Mode.
    /// </summary>
    static readonly Dictionary<HeroClass, string> elenco = new Dictionary<HeroClass, string>
    {
        // Espada longa e escudo de aço — o único com as duas peças pesadas.
        { HeroClass.Warrior, Raiz + "Human/SPUM_20240911215639580" },

        // Cajado (Ward_1), sem escudo: as mãos ocupadas com magia.
        { HeroClass.Mage,    Raiz + "Human/SPUM_20240911215639405" },

        // Elfo de cajado e escudo leve — cura de linha de trás, distinta do mago.
        { HeroClass.Healer,  Raiz + "Elf/SPUM_20240911215638048" },

        // Arco, sem escudo.
        { HeroClass.Hunter,  Raiz + "Elf/SPUM_20240911215638140" },

        // Lâmina curta e nada mais: quem depende de não ser visto não carrega escudo.
        { HeroClass.Rogue,   Raiz + "Human/SPUM_20240911215638643" },

        // Não há instrumento no pacote. Espada fina de elfo até haver alternativa.
        { HeroClass.Bard,    Raiz + "Elf/SPUM_20240911222150076" },
    };

    /// <summary>
    /// Cache por caminho: uma party de cinco com dois guerreiros carregaria o
    /// mesmo prefab duas vezes, e <c>Resources.Load</c> não é barato.
    /// </summary>
    static readonly Dictionary<string, GameObject> carregados = new Dictionary<string, GameObject>();

    /// <summary>
    /// O boneco da classe, ou null se o pacote sumir do projeto — quem chama
    /// decide o que fazer sem o corpo, porque a jornada não pode parar por arte.
    /// </summary>
    public static GameObject Prefab(HeroClass classe)
    {
        if (!elenco.TryGetValue(classe, out string caminho)) return null;

        if (carregados.TryGetValue(caminho, out GameObject cache) && cache != null)
            return cache;

        GameObject prefab = Resources.Load<GameObject>(caminho);
        if (prefab == null)
        {
            Debug.LogWarning($"TrailCast: prefab não encontrado em Resources — {caminho}");
            return null;
        }

        carregados[caminho] = prefab;
        return prefab;
    }

    /// <summary>Para diagnóstico: o caminho escolhido, sem carregar nada.</summary>
    public static string CaminhoDe(HeroClass classe)
    {
        return elenco.TryGetValue(classe, out string caminho) ? caminho : "(sem boneco)";
    }
}
