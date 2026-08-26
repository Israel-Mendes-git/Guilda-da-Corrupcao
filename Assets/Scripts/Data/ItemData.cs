using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// O que uma relíquia faz. Um valor por efeito, sempre.
///
/// Cada um destes existe porque já havia um gancho no combate onde encaixá-lo:
/// o bônus de dano da Forja, o bloqueio inicial do turno, a conta de estresse do
/// <c>EventResolver</c>, o fim do combate. Efeito sem gancho é regra nova
/// disfarçada de item, e regra nova precisa de balanceamento próprio.
/// </summary>
public enum RelicEffect
{
    /// <summary>Soma dano às cartas que o dono empresta ao baralho.</summary>
    DanoDeCarta,

    /// <summary>O dono começa cada combate já com bloqueio.</summary>
    BloqueioInicial,

    /// <summary>Reduz, em porcentagem, o estresse que o dono recebe.</summary>
    ResistenciaAEstresse,

    /// <summary>Cura o dono ao fim de cada combate vencido.</summary>
    CuraPosCombate,

    /// <summary>Quem acerta o dono leva dano de volta.</summary>
    Retaliacao,

    /// <summary>A primeira carta do dono em cada combate sai mais barata.</summary>
    PrimeiraCartaBarata,
}

/// <summary>
/// O que uma poção faz quando o frasco é aberto.
///
/// Todas agem sobre o dono e nenhuma pede alvo: a poção pertence ao herói que a
/// carrega, e escolher quem bebe seria escolher duas vezes a mesma coisa.
/// </summary>
public enum PotionEffect
{
    /// <summary>Restaura vida.</summary>
    Cura,

    /// <summary>Bloqueio imediato.</summary>
    Bloqueio,

    /// <summary>A próxima carta do grupo sai reforçada.</summary>
    ForcaNaProximaCarta,

    /// <summary>Alívio de estresse.</summary>
    Calma,
}

/// <summary>
/// Uma relíquia do catálogo.
///
/// <b>Tabela em código, e não asset por item.</b> Cartas, inimigos e eventos são
/// <c>ScriptableObject</c> porque são dezenas e o autor os edita no Inspector;
/// aqui são seis, e o efeito de cada uma é um <c>case</c> no combate de todo
/// jeito. Um asset por relíquia acrescentaria só um lugar a mais para sair de
/// sincronia com o código que a faz valer.
///
/// O <c>id</c> é o que vai para o save. Trocar o nome de exibição é seguro;
/// trocar o id apaga a relíquia de quem já a tinha.
/// </summary>
public class RelicDef
{
    public readonly string id;
    public readonly string nome;
    public readonly string descricao;
    public readonly RelicEffect efeito;
    public readonly int valor;

    /// <summary>Ouro que o Mercado cobra por ela.</summary>
    public readonly int preco;

    public RelicDef(string id, string nome, RelicEffect efeito, int valor, int preco, string descricao)
    {
        this.id = id;
        this.nome = nome;
        this.efeito = efeito;
        this.valor = valor;
        this.preco = preco;
        this.descricao = descricao;
    }
}

/// <summary>Uma poção do catálogo. Mesmas razões da <see cref="RelicDef"/>.</summary>
public class PotionDef
{
    public readonly string id;
    public readonly string nome;
    public readonly string descricao;
    public readonly PotionEffect efeito;
    public readonly int valor;
    public readonly int preco;

    public PotionDef(string id, string nome, PotionEffect efeito, int valor, int preco, string descricao)
    {
        this.id = id;
        this.nome = nome;
        this.efeito = efeito;
        this.valor = valor;
        this.preco = preco;
        this.descricao = descricao;
    }
}

/// <summary>
/// O catálogo de relíquias e poções, e as contas que dependem do que o herói
/// está carregando.
///
/// <b>Nomes provisórios, descritos pela função.</b> Nenhum destes itens tem
/// história: o world building é decisão do autor, e batizar seis relíquias aqui
/// seria inventar ficção que ele não pediu. Trocar o rótulo é trocar uma string;
/// o <c>id</c> é que não pode mudar depois de estar em algum save.
/// </summary>
public static class ItemCatalog
{
    /// <summary>Quantas relíquias cabem num herói.</summary>
    public const int SlotsDeReliquia = 2;

    static readonly List<RelicDef> reliquias = new List<RelicDef>
    {
        new RelicDef("lamina_afiada", "Lâmina Afiada", RelicEffect.DanoDeCarta, 2, 220,
                     "As cartas deste herói causam +2 de dano."),

        new RelicDef("broquel_gasto", "Broquel Gasto", RelicEffect.BloqueioInicial, 5, 190,
                     "Começa cada combate com 5 de bloqueio."),

        new RelicDef("talisma_de_calma", "Talismã de Calma", RelicEffect.ResistenciaAEstresse, 40, 240,
                     "Sofre 40% menos estresse."),

        new RelicDef("unguento_do_campo", "Unguento do Campo", RelicEffect.CuraPosCombate, 4, 200,
                     "Recupera 4 de vida ao fim de cada combate vencido."),

        new RelicDef("armadura_de_espinhos", "Armadura de Espinhos", RelicEffect.Retaliacao, 3, 230,
                     "Quem o atinge sofre 3 de dano."),

        new RelicDef("anel_do_impeto", "Anel do Ímpeto", RelicEffect.PrimeiraCartaBarata, 1, 260,
                     "A primeira carta deste herói em cada combate custa 1 a menos."),
    };

    static readonly List<PotionDef> pocoes = new List<PotionDef>
    {
        new PotionDef("pocao_de_cura", "Poção de Cura", PotionEffect.Cura, 12, 60,
                      "Restaura 12 de vida."),

        new PotionDef("pocao_de_pedra", "Poção de Pedra", PotionEffect.Bloqueio, 10, 55,
                      "Concede 10 de bloqueio."),

        new PotionDef("pocao_de_furia", "Poção de Fúria", PotionEffect.ForcaNaProximaCarta, 50, 70,
                      "A próxima carta sai com +50% de dano."),

        new PotionDef("pocao_de_sonho", "Poção de Sonho", PotionEffect.Calma, 25, 50,
                      "Alivia 25 de estresse."),
    };

    public static IReadOnlyList<RelicDef> Reliquias => reliquias;
    public static IReadOnlyList<PotionDef> Pocoes => pocoes;

    /// <summary>A relíquia deste id, ou null se o save trouxer um id que sumiu.</summary>
    public static RelicDef Reliquia(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        foreach (var r in reliquias)
            if (r.id == id) return r;

        return null;
    }

    public static PotionDef Pocao(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        foreach (var p in pocoes)
            if (p.id == id) return p;

        return null;
    }

    /// <summary>
    /// Quanto as relíquias do herói somam num efeito.
    ///
    /// Somar, e não pegar a maior: com dois slots, duas relíquias do mesmo tipo
    /// são uma escolha legítima do jogador — abrir mão de variedade por
    /// concentração. Efeito que não empilhasse tornaria o segundo slot inútil
    /// sem dizer por quê.
    /// </summary>
    public static int Total(HeroData heroi, RelicEffect efeito)
    {
        if (heroi?.relics == null) return 0;

        int total = 0;

        foreach (string id in heroi.relics)
        {
            RelicDef def = Reliquia(id);
            if (def != null && def.efeito == efeito) total += def.valor;
        }

        return total;
    }

    /// <summary>O herói carrega esta relíquia?</summary>
    public static bool Tem(HeroData heroi, string id)
    {
        return heroi?.relics != null && heroi.relics.Contains(id);
    }

    static readonly Dictionary<string, Sprite> icones = new Dictionary<string, Sprite>();

    /// <summary>
    /// O ícone do item, ou null se ele não tiver arte.
    ///
    /// Um arquivo por id em <c>Resources/ItemIcons</c>: o nome do arquivo é o
    /// próprio id, então não existe tabela intermediária para sair de sincronia
    /// — se o ícone existe, ele aparece; se não, o item continua sendo texto,
    /// que é como as dez relíquias e poções nasceram.
    ///
    /// O cache guarda inclusive a ausência: <c>Resources.Load</c> de arquivo
    /// que não existe custa o mesmo de um que existe, e a ficha do herói é
    /// redesenhada a cada clique.
    /// </summary>
    public static Sprite Icone(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (icones.TryGetValue(id, out Sprite cache)) return cache;

        Sprite s = Resources.Load<Sprite>("ItemIcons/" + id);
        icones[id] = s;
        return s;
    }
}
