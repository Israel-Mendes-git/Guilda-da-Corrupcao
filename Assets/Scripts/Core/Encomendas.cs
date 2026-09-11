using System.Collections.Generic;
using UnityEngine;

/// <summary>O que a encomenda pede. Tudo aqui se mede com o que a jornada já devolve.</summary>
public enum TipoDeEncomenda
{
    /// <summary>Trazer espólio: ouro numa única saída.</summary>
    Espolio = 0,

    /// <summary>Voltar com todo mundo vivo.</summary>
    SemPerdas = 1,

    /// <summary>Fechar o mapa de uma área — qualquer uma.</summary>
    Cartografia = 2,

    /// <summary>Aguentar dias seguidos na estrada.</summary>
    ViagemLonga = 3
}

/// <summary>Um pedido pendurado no quadro da guilda.</summary>
[System.Serializable]
public class Encomenda
{
    public TipoDeEncomenda tipo;
    public int alvo;
    public int premio;

    /// <summary>Último ciclo em que ainda vale. Passou disso, o pedido sai do quadro.</summary>
    public int cicloLimite;

    public string Titulo
    {
        get
        {
            switch (tipo)
            {
                case TipoDeEncomenda.Espolio: return "📦 Encomenda de espólio";
                case TipoDeEncomenda.SemPerdas: return "🕯️ Escolta intacta";
                case TipoDeEncomenda.Cartografia: return "🗺️ Levantamento";
                case TipoDeEncomenda.ViagemLonga: return "⏱️ Campanha longa";
                default: return "📜 Pedido";
            }
        }
    }

    public string Pedido
    {
        get
        {
            switch (tipo)
            {
                case TipoDeEncomenda.Espolio: return $"Traga {alvo} de espólio numa só saída";
                case TipoDeEncomenda.SemPerdas: return "Volte de uma saída sem perder ninguém";
                case TipoDeEncomenda.Cartografia: return "Feche o mapa de uma área";
                case TipoDeEncomenda.ViagemLonga: return $"Fique {alvo} dias fora, numa só saída";
                default: return "";
            }
        }
    }

    /// <summary>
    /// Se a saída que acabou de voltar cumpre este pedido.
    ///
    /// Tudo é medido numa <b>única</b> expedição, e não acumulado entre elas: o
    /// que se quer é que o pedido pese na decisão de até onde ir naquela viagem,
    /// não que ele se pague sozinho com o tempo.
    /// </summary>
    public bool Cumprida(int espolio, int mortos, int dias, bool mapaFechado)
    {
        switch (tipo)
        {
            case TipoDeEncomenda.Espolio: return espolio >= alvo;
            case TipoDeEncomenda.SemPerdas: return mortos == 0;
            case TipoDeEncomenda.Cartografia: return mapaFechado;
            case TipoDeEncomenda.ViagemLonga: return dias >= alvo;
            default: return false;
        }
    }
}

/// <summary>
/// O quadro da guilda depois de 11/09/2026: <b>pedidos, não destinos</b>.
///
/// <b>O que mudou e por quê.</b> O quadro oferecia quatro contratos, e era por
/// ele que se escolhia para onde ir. Medidos na auditoria, os quatro saíam entre
/// 239 e 254 de ouro e 6 a 7 dias — a mesma missão quatro vezes, com nome
/// sorteado. Com o plano navegável, o destino passou a ser do mapa; o que o
/// quadro ainda tinha de próprio era o ouro, e é só isso que ficou aqui.
///
/// <b>A encomenda não diz onde.</b> É o que a separa do contrato antigo: ela
/// pede um resultado — espólio, ninguém morto, um mapa fechado, dias fora — e o
/// jogador decide em que área isso é mais fácil. Duas encomendas no quadro já
/// mudam a leitura do mapa sem acrescentar nada ao mapa.
///
/// Estático pelo mesmo motivo do <see cref="Escritos"/> e do
/// <see cref="RegionMap"/>: o smoke test simula centenas de jornadas em edit
/// mode, onde não há MonoBehaviour vivo.
/// </summary>
public static class Encomendas
{
    /// <summary>Quantos pedidos ficam pendurados ao mesmo tempo.</summary>
    public const int Vagas = 3;

    /// <summary>
    /// Por quantos ciclos um pedido espera antes de sair do quadro.
    ///
    /// Três: dá para atravessar o mapa e voltar, e não dá para deixar o quadro
    /// parado enquanto se faz outra coisa. Prazo é o que faz a encomenda ser uma
    /// decisão de agora — sem ele, todo pedido acabaria cumprido por acidente.
    /// </summary>
    public const int PrazoEmCiclos = 3;

    static List<Encomenda> noQuadro;

    static void GarantirEstado()
    {
        if (noQuadro == null) noQuadro = new List<Encomenda>();
    }

    public static void Reiniciar()
    {
        GarantirEstado();
        noQuadro.Clear();
    }

    public static List<Encomenda> Ativas()
    {
        GarantirEstado();
        return new List<Encomenda>(noQuadro);
    }

    public static int Total
    {
        get { GarantirEstado(); return noQuadro.Count; }
    }

    /// <summary>
    /// Tira o que venceu e repõe as vagas. Chamado no pulso do ciclo, junto da
    /// renovação do resto da guilda.
    /// </summary>
    public static void Renovar(int cicloAtual, int nivelMedio, int vagas = Vagas)
    {
        GarantirEstado();

        noQuadro.RemoveAll(e => e == null || e.cicloLimite < cicloAtual);

        // Um por vez, e não de uma vez: cada pedido criado já entra no quadro,
        // e é isso que permite ao seguinte saber o que não repetir.
        int faltando = vagas - noQuadro.Count;
        for (int i = 0; i < faltando; i++)
            noQuadro.Add(Sortear(cicloAtual, nivelMedio));
    }

    /// <summary>
    /// Um pedido novo, com o prêmio na régua do que uma expedição paga.
    ///
    /// Os prêmios ficam entre 90 e 180 de ouro de propósito: a expedição comum
    /// traz 200 e poucos, então a encomenda é <b>metade de uma saída</b> — vale a
    /// pena desviar por ela, e não paga a guilda sozinha. A economia já satura no
    /// ciclo 7 (ROADMAP, fase 3.12, passo 4), e este quadro é justamente onde ela
    /// vai ser recalibrada: os números daqui são os primeiros a mexer.
    /// </summary>
    static Encomenda Sortear(int cicloAtual, int nivelMedio)
    {
        var tipo = TipoInedito();

        var encomenda = new Encomenda
        {
            tipo = tipo,
            cicloLimite = cicloAtual + PrazoEmCiclos
        };

        switch (tipo)
        {
            case TipoDeEncomenda.Espolio:
                encomenda.alvo = 200 + nivelMedio * 20 + Random.Range(0, 4) * 25;
                encomenda.premio = 120;
                break;

            case TipoDeEncomenda.SemPerdas:
                encomenda.alvo = 0;
                encomenda.premio = 150;
                break;

            case TipoDeEncomenda.Cartografia:
                encomenda.alvo = 0;
                encomenda.premio = 180;
                break;

            case TipoDeEncomenda.ViagemLonga:
                // Oito dias não se cumprem indo à Mata: a viagem mais curta do
                // mapa custa seis. É o pedido que empurra para longe de casa.
                encomenda.alvo = 8 + Random.Range(0, 3) * 2;
                encomenda.premio = 90 + encomenda.alvo * 8;
                break;
        }

        return encomenda;
    }

    /// <summary>
    /// Um tipo que ainda não está no quadro.
    ///
    /// "Feche o mapa de uma área" saiu duas vezes no mesmo quadro na primeira
    /// volta medida em Play Mode: dois dos quatro tipos não têm número que os
    /// diferencie, então repetir o tipo é repetir o pedido inteiro — e uma só
    /// saída cumpriria os dois.
    ///
    /// Com todos pendurados, sorteia livremente: são quatro tipos para três ou
    /// quatro vagas, e é melhor repetir um pedido do que deixar vaga vazia.
    /// </summary>
    static TipoDeEncomenda TipoInedito()
    {
        var livres = new List<TipoDeEncomenda>();

        for (int t = 0; t < 4; t++)
        {
            var tipo = (TipoDeEncomenda)t;
            if (!noQuadro.Exists(e => e != null && e.tipo == tipo)) livres.Add(tipo);
        }

        if (livres.Count == 0) return (TipoDeEncomenda)Random.Range(0, 4);
        return livres[Random.Range(0, livres.Count)];
    }

    /// <summary>
    /// A expedição voltou: quais pedidos ela cumpriu.
    ///
    /// As cumpridas saem do quadro e são devolvidas a quem chamou — pagar é do
    /// <see cref="GuildManager"/>, que pode nem existir quando o simulador roda
    /// isto em edit mode.
    ///
    /// Uma saída pode fechar mais de um pedido, e isso é deliberado: é o prêmio
    /// de quem leu o quadro antes de escolher o destino.
    /// </summary>
    public static List<Encomenda> Conferir(int espolio, int mortos, int dias, bool mapaFechado)
    {
        GarantirEstado();

        var cumpridas = new List<Encomenda>();

        foreach (var e in noQuadro)
        {
            if (e == null) continue;
            if (e.Cumprida(espolio, mortos, dias, mapaFechado)) cumpridas.Add(e);
        }

        foreach (var e in cumpridas) noQuadro.Remove(e);

        return cumpridas;
    }

    // ------------------------------------------------------------------ save

    /// <summary>
    /// O quadro como quatro números por pedido: tipo, alvo, prêmio e prazo.
    ///
    /// Lista plana e não objeto porque é o formato que o resto do save usa, e
    /// porque um arquivo gravado antes das encomendas existirem precisa abrir
    /// como quadro vazio em vez de derrubar a partida.
    /// </summary>
    public static List<int> Serializar()
    {
        GarantirEstado();

        var lista = new List<int>();
        foreach (var e in noQuadro)
        {
            if (e == null) continue;

            lista.Add((int)e.tipo);
            lista.Add(e.alvo);
            lista.Add(e.premio);
            lista.Add(e.cicloLimite);
        }

        return lista;
    }

    public static void Restaurar(List<int> valores)
    {
        Reiniciar();

        if (valores == null) return;

        for (int i = 0; i + 3 < valores.Count; i += 4)
        {
            int tipo = valores[i];
            if (tipo < 0 || tipo > 3) continue;

            noQuadro.Add(new Encomenda
            {
                tipo = (TipoDeEncomenda)tipo,
                alvo = valores[i + 1],
                premio = valores[i + 2],
                cicloLimite = valores[i + 3]
            });
        }
    }
}
