using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Cada lugar do mapa da guilda que pode ser erguido.</summary>
///
/// <remarks>
/// A Jornada fica de fora de propósito: ela não é sala, é a porta para a estrada,
/// e sem ela não há jogo. A Taverna está aqui, mas nasce erguida — a guilda se
/// funda nela, e um terreno vazio no lugar dela travaria a primeira escolha do
/// jogador.
/// </remarks>
public enum SalaDaGuilda
{
    Taverna = 0,
    Mercado = 1,
    Cemiterio = 2,
    Forja = 3,
    Biblioteca = 4,
    SalaDeMapas = 5
}

/// <summary>
/// A guilda que o jogador levanta com o tempo de jogo.
///
/// <b>O problema que isto resolve.</b> Até aqui as sete portas existiam desde o
/// primeiro minuto, e o autor voltou duas vezes com a mesma queixa: <i>muito
/// conteúdo, sensação de estar perdido no que fazer</i>. As tentativas anteriores
/// atacaram a apresentação — a moldura pulsante, depois os três níveis de luz do
/// <see cref="GuildGuide"/> — e nenhuma mexeu no número de coisas que chegam ao
/// jogador de uma vez. Aqui o número muda: a guilda abre com <b>duas portas</b>,
/// a Jornada e a Taverna, e as outras cinco são terreno vazio até ele pagar por
/// elas. Cada sala entra sozinha, no momento em que ele decidiu que precisava
/// dela — que é o oposto de um tutorial, e continua sendo nenhuma caixa de texto
/// explicando o jogo antes de jogá-lo.
///
/// <b>As regras, decididas pelo autor em 25/09:</b>
/// <list type="bullet">
/// <item>No primeiro dia existem a Jornada e a Taverna. As cinco outras são obra.</item>
/// <item>Paga-se em <b>ouro</b>, e só. Nem tempo de obra, nem material da estrada.</item>
/// <item>A <b>ordem é livre</b>: tudo fica disponível desde o começo, e ele ergue
///       na ordem que quiser.</item>
/// <item>O que depende de uma sala que ainda não existe <b>espera por ela</b> —
///       o morto é enterrado depois que o Cemitério for erguido, o escrito é
///       traduzido depois que a Biblioteca abrir. Nada se perde na espera.</item>
/// </list>
///
/// <b>Por que a espera sai de graça.</b> O estado já acumulava sozinho: os caídos
/// moram em <c>GuildManager.fallenHeroes</c> e os escritos em <see cref="Escritos"/>,
/// nenhum dos dois passa pela sala para existir. A sala é onde se gasta o que
/// acumulou, e não onde o acúmulo acontece. Foi o que tornou a regra do autor
/// barata de cumprir — e é a razão de este arquivo não ter fila nenhuma: a fila
/// é o próprio estado do jogo, e <see cref="Espera"/> só a lê para dizer, no
/// terreno vazio, o que já está parado esperando aquela sala.
///
/// <b>Os preços</b> somam 700 de ouro, contra os ~395 que uma jornada rende e os
/// 100 com que a guilda nasce (<c>MetaProgression.OuroBasePorRun</c>). São
/// baratos de propósito, a pedido do autor: a construção ensina uma sala por vez
/// e sai do caminho por volta da quarta jornada, em vez de virar a decisão
/// central da partida. Com 100 no cofre, o primeiro dia compra exatamente uma
/// sala — ou nenhuma, se ele preferir provisões.
/// </summary>
public static class Obras
{
    /// <summary>
    /// O que já está de pé. A Taverna entra na fundação e nunca sai: ver a nota
    /// em <see cref="SalaDaGuilda"/>.
    /// </summary>
    static HashSet<SalaDaGuilda> erguidas;

    /// <summary>Uma sala acabou de ser erguida — a guilda mudou de tamanho.</summary>
    public static event Action<SalaDaGuilda> onObraConcluida;

    /// <summary>
    /// Toda sala que o jogador pode erguer, na ordem em que a necessidade delas
    /// costuma aparecer: trata-se o ferido antes de enterrar o morto, enterra-se
    /// antes de sobrar ouro para arma. É a ordem do terreno na lista de obras, e
    /// não uma ordem obrigatória — a escolha é dele.
    /// </summary>
    public static readonly SalaDaGuilda[] Construiveis =
    {
        SalaDaGuilda.Mercado,
        SalaDaGuilda.Cemiterio,
        SalaDaGuilda.Forja,
        SalaDaGuilda.Biblioteca,
        SalaDaGuilda.SalaDeMapas
    };

    static HashSet<SalaDaGuilda> Erguidas
    {
        get
        {
            if (erguidas == null) Reiniciar();
            return erguidas;
        }
    }

    /// <summary>A guilda recém-fundada: a Taverna de pé e cinco terrenos vazios.</summary>
    public static void Reiniciar()
    {
        erguidas = new HashSet<SalaDaGuilda> { SalaDaGuilda.Taverna };
    }

    /// <summary>
    /// Toda sala de pé. É o estado de um save anterior a esta mecânica: aquela
    /// partida foi jogada com a guilda inteira aberta, e abri-la com cinco
    /// terrenos vazios seria demolir o que o jogador já tinha.
    /// </summary>
    public static void ErguerTudo()
    {
        erguidas = new HashSet<SalaDaGuilda>((SalaDaGuilda[])Enum.GetValues(typeof(SalaDaGuilda)));
    }

    public static bool Construida(SalaDaGuilda sala) => Erguidas.Contains(sala);

    /// <summary>Quantas obras ainda faltam — o que o mapa da guilda mostra como terreno.</summary>
    public static int Faltam => Construiveis.Count(s => !Construida(s));

    /// <summary>
    /// O que cada sala cobra para ser erguida.
    ///
    /// Crescem na ordem em que a sala deixa de ser socorro e vira conforto: o
    /// Mercado trata o ferido que volta da primeira estrada, e a Sala de Mapas
    /// só vende antecipação de rota, que se joga sem.
    /// </summary>
    public static int Preco(SalaDaGuilda sala)
    {
        switch (sala)
        {
            case SalaDaGuilda.Mercado:     return 100;
            case SalaDaGuilda.Cemiterio:   return 120;
            case SalaDaGuilda.Forja:       return 150;
            case SalaDaGuilda.Biblioteca:  return 150;
            case SalaDaGuilda.SalaDeMapas: return 180;
            default:                       return 0;
        }
    }

    public static string Nome(SalaDaGuilda sala)
    {
        switch (sala)
        {
            case SalaDaGuilda.Taverna:     return "Taverna";
            case SalaDaGuilda.Mercado:     return "Mercado";
            case SalaDaGuilda.Cemiterio:   return "Cemitério";
            case SalaDaGuilda.Forja:       return "Forja";
            case SalaDaGuilda.Biblioteca:  return "Biblioteca";
            case SalaDaGuilda.SalaDeMapas: return "Sala de Mapas";
            default:                       return sala.ToString();
        }
    }

    /// <summary>
    /// O que a sala fará quando existir, em uma linha.
    ///
    /// É a única apresentação que cada sala ganha, e ela chega no instante em que
    /// o jogador está decidindo pagar por ela — não antes, numa caixa que ele
    /// fecharia sem ler.
    /// </summary>
    public static string Promessa(SalaDaGuilda sala)
    {
        switch (sala)
        {
            case SalaDaGuilda.Mercado:
                return "Bandagem para o ferido, vinho contra o estresse, frascos e provisões para a estrada.";
            case SalaDaGuilda.Cemiterio:
                return "Enterra os caídos: o monumento devolve reputação, a vigília alivia quem ficou.";
            case SalaDaGuilda.Forja:
                return "Arma e armadura para cada herói — mais dano nas cartas dele, mais HP na linha de frente.";
            case SalaDaGuilda.Biblioteca:
                return "Vende cartas para os baralhos e traduz os escritos que voltam da estrada.";
            case SalaDaGuilda.SalaDeMapas:
                return "Batedores que abrem a rota antes da partida e desvios para recusar um encontro.";
            default:
                return "";
        }
    }

    /// <summary>
    /// O que já está parado esperando esta sala abrir, ou vazio se nada espera.
    ///
    /// É o que faz o terreno vazio doer: não "a Forja custa 150", e sim "dois
    /// caídos esperam enterro". Lê o estado do jogo direto, porque é lá que a
    /// espera acontece — ver a nota de topo.
    /// </summary>
    public static string Espera(SalaDaGuilda sala)
    {
        GuildManager guilda = GuildManager.Instance;

        switch (sala)
        {
            case SalaDaGuilda.Cemiterio:
            {
                int caidos = guilda != null ? guilda.fallenHeroes.Count : 0;
                if (caidos <= 0) return "";
                return caidos == 1
                    ? "1 caído espera enterro."
                    : $"{caidos} caídos esperam enterro.";
            }

            case SalaDaGuilda.Biblioteca:
            {
                int escritos = Escritos.TotalNaEstante;
                if (escritos <= 0) return "";
                return escritos == 1
                    ? "1 escrito espera tradução."
                    : $"{escritos} escritos esperam tradução.";
            }

            case SalaDaGuilda.Mercado:
            {
                if (guilda == null) return "";
                int feridos = guilda.roster.Count(h => h != null && h.IsAlive && h.isInjured);
                if (feridos <= 0) return "";
                return feridos == 1
                    ? "1 herói ferido espera tratamento."
                    : $"{feridos} heróis feridos esperam tratamento.";
            }

            default:
                return "";
        }
    }

    public static bool PodePagar(SalaDaGuilda sala)
    {
        GuildManager guilda = GuildManager.Instance;
        return guilda != null && guilda.gold >= Preco(sala);
    }

    /// <summary>
    /// Ergue a sala, cobrando o preço. Devolve falso quando já está de pé ou o
    /// cofre não cobre — a tela não deve chegar aqui nesses casos, e chegar não
    /// pode custar ouro nenhum.
    /// </summary>
    public static bool Construir(SalaDaGuilda sala)
    {
        if (Construida(sala)) return false;

        GuildManager guilda = GuildManager.Instance;
        if (guilda == null) return false;

        if (!guilda.SpendGold(Preco(sala))) return false;

        Erguidas.Add(sala);
        onObraConcluida?.Invoke(sala);
        return true;
    }

    /// <summary>
    /// A sala por trás da ação do <see cref="MapManager"/> ("Market", "Forge"…).
    ///
    /// O roteador de cliques fala em strings desde antes desta mecânica, e
    /// traduzi-las aqui evita espalhar um segundo vocabulário de salas pelo
    /// projeto. "Journey" não tem sala: devolve falso, e a porta da estrada abre
    /// sempre.
    /// </summary>
    public static bool Da(string acao, out SalaDaGuilda sala)
    {
        sala = SalaDaGuilda.Taverna;
        if (string.IsNullOrEmpty(acao)) return false;

        switch (acao)
        {
            case "Tavern":   sala = SalaDaGuilda.Taverna;     return true;
            case "Market":   sala = SalaDaGuilda.Mercado;     return true;
            case "Cemetery": sala = SalaDaGuilda.Cemiterio;   return true;
            case "Forge":    sala = SalaDaGuilda.Forja;       return true;
            case "Library":  sala = SalaDaGuilda.Biblioteca;  return true;
            case "MapRoom":  sala = SalaDaGuilda.SalaDeMapas; return true;
            default:         return false;
        }
    }

    /// <summary>
    /// A sala pelo nome do objeto da cena, com a mesma tolerância que o
    /// <see cref="GuildArt"/> e o <see cref="GuildGuide"/> usam: as portas vêm de
    /// versões diferentes do projeto e nem todas foram batizadas igual.
    /// A chave chega aqui já sem acento e em caixa baixa.
    /// </summary>
    public static bool DaChave(string chave, out SalaDaGuilda sala)
    {
        sala = SalaDaGuilda.Taverna;
        if (string.IsNullOrEmpty(chave)) return false;

        if (chave.Contains("tavern"))                                 { sala = SalaDaGuilda.Taverna;     return true; }
        if (chave.Contains("mercado") || chave.Contains("market"))    { sala = SalaDaGuilda.Mercado;     return true; }
        if (chave.Contains("cemit")   || chave.Contains("cemet"))     { sala = SalaDaGuilda.Cemiterio;   return true; }
        if (chave.Contains("forj")    || chave.Contains("forge"))     { sala = SalaDaGuilda.Forja;       return true; }
        if (chave.Contains("bibliotec") || chave.Contains("librar"))  { sala = SalaDaGuilda.Biblioteca;  return true; }

        // Depois da Jornada, de propósito: "mapa"/"map" casaria com a porta da
        // estrada em projetos onde ela se chama "MapaMundi".
        if (chave.Contains("jornada") || chave.Contains("journey") || chave.Contains("quest"))
            return false;

        if (chave.Contains("mapa") || chave.Contains("map"))          { sala = SalaDaGuilda.SalaDeMapas; return true; }

        return false;
    }

    #region Save

    public static List<int> Serializar() => Erguidas.Select(s => (int)s).OrderBy(i => i).ToList();

    /// <summary>
    /// Devolve ao save o que ele guardou. Lista vazia ou nula seria uma guilda
    /// sem nem a Taverna, que não existe — nesse caso a fundação volta ao
    /// padrão. Saves anteriores a esta mecânica não passam por aqui: o
    /// <see cref="GameStateIO"/> os manda para <see cref="ErguerTudo"/>.
    /// </summary>
    public static void Restaurar(List<int> salas)
    {
        Reiniciar();
        if (salas == null) return;

        foreach (int i in salas)
            if (Enum.IsDefined(typeof(SalaDaGuilda), i))
                Erguidas.Add((SalaDaGuilda)i);
    }

    #endregion
}
