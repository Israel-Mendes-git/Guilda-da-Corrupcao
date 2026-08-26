using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A peça que aparece na bigorna: qual arma e qual escudo o herói mostra em cada
/// nível de forja.
///
/// <b>Por que peça, e não só um número.</b> A forja antiga dizia "arma 1/3" num
/// rótulo, e era só isso que mudava ao pagar 120 de ouro. Uma peça que troca de
/// desenho é o retorno percebido de que a compra aconteceu — o mesmo motivo pelo
/// qual os inimigos ganharam retrato em vez de continuarem sendo um nome.
///
/// <b>Por que <c>Resources.Load</c>, e não referência na cena.</b> Os sprites do
/// SPUM já moram dentro de uma pasta <c>Resources</c> do próprio pacote, então o
/// caminho basta e não é preciso ferramenta de Editor para ligar nada. Se o
/// pacote sair do projeto, <see cref="Arma"/> devolve <c>null</c> e a forja volta
/// a mostrar só o nível — nada quebra, como no catálogo de mapas.
///
/// <b>Onde falta material, a peça repete.</b> Guerreiro e ladino têm quatro
/// desenhos; caçador, três; mago e curandeiro, um só. Inventar uma família
/// diferente a cada nível (varinha que vira tridente) confundiria mais do que
/// informa, então o que muda nesses casos é a moldura e o número na carta.
/// </summary>
public static class ForgeArt
{
    const string Raiz = "Addons/Ver300/0_Unit/0_Sprite/8_Weapons/";

    /// <summary>Uma família por classe, do nível 0 ao <see cref="ForgeManager.maxUpgradeLevel"/>.</summary>
    static readonly Dictionary<HeroClass, string[]> ArmaPorClasse = new Dictionary<HeroClass, string[]>
    {
        { HeroClass.Warrior, new[] { "0_Sword/New_Weapon_01",  "0_Sword/New_Weapon_06",
                                     "0_Sword/New_Weapon_17",  "0_Sword/New_Weapon_20"  } },
        { HeroClass.Rogue,   new[] { "6_Dagger/New_Weapon_05", "6_Dagger/New_Weapon_11",
                                     "0_Sword/New_Weapon_18",  "0_Sword/New_Weapon_19"  } },
        { HeroClass.Hunter,  new[] { "3_Bow/New_Weapon_10",    "3_Bow/New_Weapon_12",
                                     "3_Bow/New_Weapon_15"                              } },
        { HeroClass.Mage,    new[] { "5_Wand/New_Weapon_03"                             } },
        { HeroClass.Healer,  new[] { "8_Mace/New_Weapon_07"                             } },
        { HeroClass.Bard,    new[] { "6_Dagger/New_Weapon_05"                           } },
    };

    /// <summary>O escudo é o mesmo para todos: quem muda é o nível.</summary>
    static readonly string[] Escudos =
    {
        "7_Shield/New_Shield_01", "7_Shield/New_Shield_02",
        "7_Shield/New_Shield_03", "7_Shield/New_Shield_04",
    };

    /// <summary>
    /// Cor da moldura do slot, do ferro cru ao acabamento máximo. É o que
    /// carrega o nível quando a própria peça não muda de desenho.
    /// </summary>
    static readonly Color[] CoresPorNivel =
    {
        new Color(0.42f, 0.40f, 0.38f),   // 0 — ferro cru
        new Color(0.62f, 0.55f, 0.42f),   // 1 — bronze
        new Color(0.78f, 0.70f, 0.48f),   // 2 — aço polido
        new Color(0.94f, 0.82f, 0.42f),   // 3 — o melhor que esta forja faz
    };

    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    /// <summary>A arma daquela classe no nível pedido, ou null se o pacote saiu do projeto.</summary>
    public static Sprite Arma(HeroClass classe, int nivel)
    {
        if (!ArmaPorClasse.TryGetValue(classe, out string[] familia) || familia.Length == 0)
            return null;

        return Carregar(familia[Mathf.Clamp(nivel, 0, familia.Length - 1)]);
    }

    /// <summary>O escudo do nível pedido, ou null se o pacote saiu do projeto.</summary>
    public static Sprite Armadura(int nivel)
    {
        return Carregar(Escudos[Mathf.Clamp(nivel, 0, Escudos.Length - 1)]);
    }

    /// <summary>Cor da moldura para aquele nível.</summary>
    public static Color Cor(int nivel)
    {
        return CoresPorNivel[Mathf.Clamp(nivel, 0, CoresPorNivel.Length - 1)];
    }

    /// <summary>Blocos cheios e vazios: o nível legível sem ler número.</summary>
    public static string Blocos(int nivel, int maximo)
    {
        nivel = Mathf.Clamp(nivel, 0, maximo);
        return new string('■', nivel) + new string('□', maximo - nivel);
    }

    static Sprite Carregar(string caminho)
    {
        if (cache.TryGetValue(caminho, out Sprite guardado)) return guardado;

        Sprite sprite = Resources.Load<Sprite>(Raiz + caminho);
        cache[caminho] = sprite;   // guarda inclusive o null: não adianta tentar de novo
        return sprite;
    }
}
