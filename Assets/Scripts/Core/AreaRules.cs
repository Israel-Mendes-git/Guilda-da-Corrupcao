using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// A regra própria de cada área — o que cada lugar <i>faz</i>, desde 13/09.
///
/// Escrita em <c>MUNDO.md</c> em 10/09 e construída aqui. As sete áreas se
/// diferenciavam por distância, corrupção, aspecto e nome; a regra de cada uma
/// existia como frase na ficha do mapa e nada no código a lia. O que mora aqui
/// são as <b>constantes e as contas</b>; quem as aplica é a estrada
/// (<see cref="JourneyManager"/>), o combate (<see cref="CombatManager"/>) e o
/// bestiário (<see cref="EnemyPool"/>) — e o simulador do smoke test, pelas
/// mesmas funções, para que a letalidade meça o jogo que existe.
///
/// Estático, sem MonoBehaviour: o smoke test roda em edit mode.
///
/// <b>O que cada área faz, numa linha:</b>
/// <list type="bullet">
/// <item><b>A Mata</b> — o mato fecha: o último trecho da rota cobra ração e
/// tocha em dobro; a caça repõe mantimentos nas paradas.</item>
/// <item><b>A Cripta</b> — os mortos se erguem uma vez por combate; herói
/// enterrado sem tributo luta contra o grupo; espólio em dobro.</item>
/// <item><b>A Aldeia</b> — depois de cada luta vencida, poupar ou matar quem
/// ainda é gente: moral e estresse contra ouro e comida.</item>
/// <item><b>O Oráculo</b> — perguntar revela a rota inteira e custa uma carta
/// do baralho, para sempre.</item>
/// <item><b>A Torre</b> — a cada combate, a cópia de um herói do grupo aparece
/// do outro lado; derrubá-la estressa o original e pode render uma carta que a
/// Biblioteca não vende.</item>
/// <item><b>A Forja</b> — forjar na estrada, sem voltar: a peça é melhor e
/// corrompida, e o martelo chama o que mora lá.</item>
/// <item><b>O Covil</b> — a luz desperta o dragão: cada trecho com tocha e cada
/// peça levada aproximam o despertar; o escuro desgasta mais.</item>
/// </list>
/// </summary>
public static class AreaRules
{
    // ─────────────────────────────────────────────────────────────── A Mata

    /// <summary>A partir desta fração da rota o mato fecha e o dia fica mais caro.</summary>
    public const float MataFechaAPartirDe = 0.6f;

    /// <summary>Quantas rações e tochas o trecho cobra depois que o mato fecha.</summary>
    public const int MataConsumoFechado = 2;

    /// <summary>Chance de a caça render numa parada sem luta, e quanto rende.</summary>
    public const float MataChanceDeCaca = 0.35f;
    public const int MataRacoesDaCaca = 2;

    /// <summary>
    /// Quantas vezes o consumo diário é cobrado neste trecho.
    ///
    /// A Mata é a única área que mexe aqui. É a regra da primeira jornada de
    /// toda partida, e é uma conta na manutenção diária: barata de ler e
    /// impossível de não sentir — a mochila que sobrava no dia 5 acaba no 7.
    /// </summary>
    public static int ConsumoPorTrecho(AreaType area, float progresso)
    {
        return area == AreaType.Mata && progresso >= MataFechaAPartirDe ? MataConsumoFechado : 1;
    }

    // ───────────────────────────────────────────────────────────── A Cripta

    /// <summary>O espólio dos combates da Cripta: foram enterrados com o que tinham.</summary>
    public const int CriptaEspolio = 2;

    /// <summary>Com quanto de vida um inimigo caído se ergue. Uma vez cada.</summary>
    public const float CriptaVidaAoErguer = 0.34f;

    /// <summary>
    /// Os mortos da guilda que ninguém honrou — é quem a Cripta levanta contra o
    /// grupo. Fora do Play Mode não há guilda, e a lista é vazia: o simulador
    /// mede a Cripta sem dívida, e a dívida é o que o jogador acumula.
    /// </summary>
    public static List<HeroData> MortosSemTributo()
    {
        var guilda = GuildManager.Instance;
        return guilda != null ? guilda.MortosSemTributo() : new List<HeroData>();
    }

    // ───────────────────────────────────────────────────────────── A Aldeia

    /// <summary>Poupar: quem ainda é gente devolve moral e alivia estresse.</summary>
    public const int AldeiaPouparMoral = 10;
    public const float AldeiaPouparAlivio = 8f;

    /// <summary>Matar: recurso de quem foi morto, e a moral paga.</summary>
    public const int AldeiaMatarOuro = 30;
    public const int AldeiaMatarRacoes = 2;
    public const int AldeiaMatarMoral = -8;

    /// <summary>
    /// A política do simulador para a escolha da Aldeia: com o grupo à beira
    /// da quebra, poupa; com folga, mata. É o que a regra existe para produzir
    /// — a escolha muda de peso com o estado da partida.
    /// </summary>
    public static bool SimuladorPoupa(IEnumerable<HeroData> party)
    {
        var vivos = party.Where(h => h != null && h.IsAlive).ToList();
        if (vivos.Count == 0) return false;
        return vivos.Average(h => h.stress) >= 50f;
    }

    public static void Poupar(IEnumerable<HeroData> party)
    {
        foreach (var h in party.Where(x => x != null && x.IsAlive).ToList())
        {
            h.morale = Mathf.Min(100f, h.morale + AldeiaPouparMoral);
            EventResolver.AddStress(h, -AldeiaPouparAlivio, new EventResolver.Resolution());
        }
    }

    public static void Matar(IEnumerable<HeroData> party)
    {
        foreach (var h in party.Where(x => x != null && x.IsAlive).ToList())
            h.morale = Mathf.Max(0f, h.morale + AldeiaMatarMoral);
    }

    // ──────────────────────────────────────────────────────────── O Oráculo

    /// <summary>Quantos pontos adiante a resposta revela — a rota inteira.</summary>
    public const int OraculoRevela = 99;

    // ────────────────────────────────────────────────────────────── A Torre

    /// <summary>O que derrubar a própria cópia custa ao original.</summary>
    public const float TorreEstresseDaCopia = 15f;

    /// <summary>Chance de a luta render uma carta que a Biblioteca não vende.</summary>
    public const float TorreChanceDeCarta = 0.5f;

    /// <summary>
    /// A carta que o feiticeiro colecionou: rara ou melhor, da classe de um
    /// herói vivo do grupo, e entra no baralho guardado dele — mesmo cheio.
    /// Devolve null quando não há o que dar.
    /// </summary>
    public static CardData CartaDaTorre(IList<HeroData> party, out HeroData quem)
    {
        quem = null;
        var vivos = party?.Where(h => h != null && h.IsAlive).ToList();
        if (vivos == null || vivos.Count == 0) return null;

        quem = vivos[Random.Range(0, vivos.Count)];
        HeroClass classe = quem.heroClass;

        var acervo = Resources.LoadAll<CardData>("Cards")
            .Where(c => c != null && c.requiredClass == classe && c.rarity >= CardRarity.Rare)
            .ToList();

        return acervo.Count == 0 ? null : acervo[Random.Range(0, acervo.Count)];
    }

    // ────────────────────────────────────────────────────────────── A Forja

    /// <summary>Níveis de arma que a forja da estrada dá de uma vez.</summary>
    public const int ForjaNiveisPorPeca = 1;

    /// <summary>Exposição que cada peça corrompida soma ao portador por combate.</summary>
    public const int ExposicaoPorPecaCorrompida = 6;

    /// <summary>Acima daqui o herói volta marcado: um traço negativo.</summary>
    public const int ExposicaoQueMarca = 50;

    /// <summary>Acima daqui ele pode virar — e sair do roster.</summary>
    public const int ExposicaoQueVira = 80;
    public const float ChanceDeVirar = 0.35f;

    /// <summary>O equipamento corrompido apodrece o portador a cada luta.</summary>
    public static void CobrarEquipamentoCorrompido(IEnumerable<HeroData> party)
    {
        foreach (var h in party.Where(x => x != null && x.IsAlive && x.equipamentoCorrompido > 0).ToList())
            h.corruptionExposure = Mathf.Clamp(
                h.corruptionExposure + h.equipamentoCorrompido * ExposicaoPorPecaCorrompida, 0, 100);
    }

    // ────────────────────────────────────────────────────────────── O Covil

    /// <summary>
    /// Quanto de despertar acorda o dragão.
    ///
    /// Vinte, e não dez: com a mochila proporcional aos dias, a expedição de
    /// dezesseis dias ao Covil vai inteira iluminada, e com dez o dragão descia
    /// no sétimo dia de toda ida — 2,4 mortes por jornada. Com vinte, a rota
    /// inteira à luz mais o espólio das lutas chega ao teto no fim: quem quer
    /// evitá-lo apaga a tocha no último terço, e paga em estresse.
    /// </summary>
    public const int CovilDespertarMaximo = 20;

    /// <summary>Cada trecho atravessado com tocha acesa.</summary>
    public const int CovilDespertarPorLuz = 1;

    /// <summary>Cada peça levada: espólio de luta e relíquia achada.</summary>
    public const int CovilDespertarPorEspolio = 1;
    public const int CovilDespertarPorReliquia = 3;

    /// <summary>O escuro do Covil desgasta mais que o de qualquer outra área.</summary>
    public const float CovilEscuridao = 1.5f;

    /// <summary>Chance de uma luta vencida no Covil largar relíquia — é onde elas se acumulam.</summary>
    public const float CovilChanceDeReliquia = 0.5f;

    /// <summary>Quanto o escuro cobra de estresse neste lugar.</summary>
    public static float EstresseDaEscuridao(AreaType area, float baseStress)
    {
        return area == AreaType.Covil ? baseStress * CovilEscuridao : baseStress;
    }

    // ────────────────────────────────────────────────────────────── O HUD

    /// <summary>
    /// A regra em vigor, numa linha, para o alto da tela da jornada.
    ///
    /// Substitui o nome do bioma, que repetia o nome da missão ("🌲 A Mata"
    /// ao lado de "🌲 Floresta"). Diz o que este lugar está fazendo agora —
    /// e só o que muda com o tempo muda de texto.
    /// </summary>
    public static string Lembrete(AreaType area, float progresso, int despertar)
    {
        switch (area)
        {
            // Só glifos que o jogo já usa: emoji novo entra no atlas da fonte, que
            // já estourou uma vez e gravou textura nula no asset.
            case AreaType.Mata:
                return progresso >= MataFechaAPartirDe
                    ? "🌲 O mato fechou: cada trecho cobra ração e tocha em dobro"
                    : "🌲 O mato fecha adiante: os últimos trechos cobram o dobro";

            case AreaType.Cripta:
                return "💀 Os mortos se erguem uma vez por luta; espólio em dobro";

            case AreaType.Aldeia:
                return "🏚️ Depois de cada luta: poupar ou matar quem ainda é gente";

            case AreaType.Oraculo:
                return "🔮 A cega responde por uma lembrança: uma carta, para sempre";

            case AreaType.Torre:
                return "❄️ A cada luta, um dos seus aparece do outro lado";

            case AreaType.Forja:
                return "🔨 Dá para forjar aqui — e o martelo chama o que mora lá";

            case AreaType.Covil:
                return $"🐉 A luz desperta o dragão: {despertar}/{CovilDespertarMaximo}";

            default:
                return "";
        }
    }
}
