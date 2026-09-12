#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Auditoria de jogabilidade: mede se cada coisa que o jogador compra, escolhe ou
/// conquista <b>muda alguma coisa</b>, e a que preço.
///
/// Tools → Guild of Legends → Auditoria de Jogabilidade · gatilho
/// <c>RunGameplayAudit.trigger</c> · relatório em <c>GameplayReport.txt</c>.
///
/// <b>Por que separado do smoke test.</b> O smoke test responde "o jogo está de
/// pé?" e tranca regressão; esta ferramenta responde "o jogo é um jogo?" — se a
/// Forja vale o ouro que cobra, se o deck importa fora do combate, se subir de
/// nível se nota. São perguntas de design, e a resposta delas não reprova build
/// nenhuma: sai em tabela, para o autor decidir.
///
/// <b>O método.</b> Cada linha é a mesma simulação rodada duas vezes, mudando uma
/// coisa só — o A/B que o projeto já usa para energia de combate. A diferença em
/// mortes por jornada é o que aquele sistema vale. Sem isso, "a Forja funciona"
/// significa apenas que o número na tela subiu.
/// </summary>
public static class GameplayAudit
{
    /// <summary>
    /// Jornadas por cenário. Com 100 grupos e 800 jornadas, a dispersão medida
    /// entre execuções fica perto de ±0,03 mortes — abaixo da menor diferença que
    /// interessa aqui. Cada cenário custa alguns segundos.
    /// </summary>
    const int Jornadas = 800;

    static StringBuilder relatorio;

    [MenuItem("Tools/Guild of Legends/Auditoria de Jogabilidade")]
    public static void Rodar()
    {
        relatorio = new StringBuilder();

        Titulo("AUDITORIA DE JOGABILIDADE");
        Linha($"(gerado em {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}, {Jornadas} jornadas por cenário)");
        Linha("");

        JourneyStatsBase();
        ImpactoDosSistemas();
        Economia();
        RitmoDaRun();
        Interacao();

        string caminho = System.IO.Path.Combine(PlayModeTestLauncher.ProjectRoot, "GameplayReport.txt");
        System.IO.File.WriteAllText(caminho, relatorio.ToString());

        Debug.Log($"Auditoria de jogabilidade gravada em {caminho}");
    }

    static GuildSmokeTest.JourneyStats baseLine;

    static void JourneyStatsBase()
    {
        baseLine = Medir(null);

        Titulo("A LINHA DE BASE");
        Linha($"grupo padrão (Guerreiro Nv.3, Mago Nv.2, Curandeiro Nv.2, Caçador Nv.1), sem nada comprado");
        Linha($"  mortes por jornada .......... {baseLine.mortesPorJornada:F2}");
        Linha($"  sobrevivência ............... {baseLine.sobrevivencia:P1}");
        Linha($"  combates por jornada ........ {baseLine.combates:F2}");
        Linha($"  paradas de texto por jornada  {baseLine.paradasDeTexto:F2}");
        Linha($"  cartas jogadas na estrada ... {baseLine.cartasJogadas:F2}");
        Linha($"  duração média ............... {baseLine.duracaoMedia:F1} dias");
        Linha("");
    }

    /// <summary>
    /// O que cada sistema vale, em mortes por jornada. Negativo é bom: o grupo
    /// perde menos gente.
    /// </summary>
    static void ImpactoDosSistemas()
    {
        Titulo("IMPACTO DE CADA SISTEMA");
        Linha("Cada linha é a mesma simulação com uma coisa mudada. 'Δ mortes' é a diferença");
        Linha("contra a linha de base — negativo significa que o grupo perde menos gente.");
        Linha("");
        Linha("  o que muda                              Δ mortes   sobrev.   combates   cartas");
        Linha("  ─────────────────────────────────────────────────────────────────────────────");

        Cenario("Forja: arma nível 1 em todos", h => h.weaponLevel = 1);
        Cenario("Forja: arma nível 3 em todos", h => h.weaponLevel = 3);
        Cenario("Forja: armadura nível 3 em todos", h =>
        {
            h.armorLevel = 3;
            h.maxHp += 12;
            h.currentHp = h.maxHp;
        });

        Cenario("Relíquias: 2 por herói", h =>
        {
            var ids = ItemCatalog.Reliquias.Take(ItemCatalog.SlotsDeReliquia).Select(r => r.id).ToList();
            h.relics = new List<string>(ids);
        });

        Cenario("Poções: 1 frasco por herói", h =>
        {
            PotionDef p = ItemCatalog.Pocoes.FirstOrDefault();
            if (p != null) h.potions = new List<string> { p.id };
        });

        Cenario("Todos um nível acima", h => SubirPara(h, h.level + 1));
        Cenario("Todos no nível 5", h => SubirPara(h, 5));
        Cenario("Todos no nível 1 (elenco cru)", h => SubirPara(h, 1));

        // O deck fora do combate: a pergunta do autor em 21/08.
        GuildSmokeTest.SemCartasNaEstrada = true;
        Cenario("O jogador não joga carta nenhuma na estrada", null);
        GuildSmokeTest.SemCartasNaEstrada = false;

        Linha("");
    }

    static void Cenario(string nome, System.Action<HeroData> ajuste)
    {
        GuildSmokeTest.JourneyStats s = Medir(ajuste);

        float delta = s.mortesPorJornada - baseLine.mortesPorJornada;
        string sinal = delta > 0.005f ? "+" : "";

        Linha($"  {nome,-38} {sinal}{delta,7:F2}   {s.sobrevivencia,6:P1}   "
            + $"{s.combates,7:F2}   {s.cartasJogadas,5:F2}");
    }

    static GuildSmokeTest.JourneyStats Medir(System.Action<HeroData> ajuste)
    {
        GuildSmokeTest.PrepararHeroi = ajuste;
        try
        {
            return GuildSmokeTest.MedirJornadas(Jornadas);
        }
        finally
        {
            GuildSmokeTest.PrepararHeroi = null;
        }
    }

    /// <summary>
    /// Leva o herói a um nível, pelas mesmas contas da <see cref="HeroFactory"/> —
    /// somar HP à mão daria um herói que o jogo não cria.
    /// </summary>
    static void SubirPara(HeroData h, int nivel)
    {
        nivel = Mathf.Max(1, nivel);

        h.level = nivel;
        h.maxHp = HeroFactory.MaxHpFor(h.heroClass, nivel);
        h.currentHp = h.maxHp;
        h.salary = HeroFactory.SalaryFor(nivel);
    }

    /// <summary>
    /// Quanto entra por jornada contra o que a guilda cobra. Responde se o jogador
    /// fica rico sem ter o que comprar, ou pobre demais para usar as salas.
    /// </summary>
    /// <summary>O que sobra por jornada depois da folha — a régua de todo preço abaixo.</summary>
    static float SobraPorJornada =>
        baseLine.ouroDosContratos + baseLine.ouroDosEventos + baseLine.ouroDosSobreviventes
        - baseLine.ouroDeSalarios;

    static void Economia()
    {
        Titulo("ECONOMIA: O QUE ENTRA, O QUE SAI E O QUE AS SALAS COBRAM");

        float entra = baseLine.ouroDosContratos + baseLine.ouroDosEventos + baseLine.ouroDosSobreviventes;
        float sobra = SobraPorJornada;

        Linha($"  contrato médio da missão ......... {baseLine.ouroDosContratos:F0}");
        Linha($"  ouro dos eventos da estrada ...... {baseLine.ouroDosEventos:F0}");
        Linha($"  quem voltou (25 por cabeça) ...... {baseLine.ouroDosSobreviventes:F0}");
        Linha($"  salários da volta ................ −{baseLine.ouroDeSalarios:F0}   "
            + $"(salário por dia fora, {baseLine.duracaoMedia:F1} dias, o grupo inteiro)");
        Linha($"  ────────────────────────────────────────");
        Linha($"  entra {entra:F0}, sobra por jornada ...... {sobra:F0}   (fora o espólio dos combates)");
        Linha("");
        Linha("  o que a guilda vende            preço   jornadas para pagar");
        Linha("  ──────────────────────────────────────────────────────────");

        var forja = Object.FindObjectOfType<ForgeManager>(true);
        var mercado = Object.FindObjectOfType<MarketManager>(true);
        var biblioteca = Object.FindObjectOfType<LibraryManager>(true);
        var mapas = Object.FindObjectOfType<MapRoomManager>(true);
        var cemiterio = Object.FindObjectOfType<CemeteryManager>(true);
        var taverna = Object.FindObjectOfType<TavernManager>(true);

        if (forja != null)
        {
            var alvo = HeroFactory.CreateHero("Teste", HeroClass.Warrior, 3);
            Preco("Forja: arma nível 1", forja.WeaponCost(alvo), sobra);
            Preco("Forja: armadura nível 1", forja.ArmorCost(alvo), sobra);
            alvo.weaponLevel = 2;
            alvo.armorLevel = 2;
            Preco("Forja: arma nível 3", forja.WeaponCost(alvo), sobra);
            Preco("Forja: armadura nível 3", forja.ArmorCost(alvo), sobra);
            Object.DestroyImmediate(alvo);
        }

        if (mercado != null)
        {
            Preco("Mercado: ração", mercado.rationCost, sobra);
            Preco("Mercado: tratamento", mercado.potionCost, sobra);
            Preco("Mercado: bandagem", mercado.bandageCost, sobra);
            Preco("Mercado: vinho", mercado.wineCost, sobra);
        }

        foreach (PotionDef p in ItemCatalog.Pocoes)
            Preco($"Mercado: {p.nome}", p.preco, sobra);

        foreach (RelicDef r in ItemCatalog.Reliquias)
            Preco($"Mercado: {r.nome}", r.preco, sobra);

        if (biblioteca != null)
        {
            Preco("Biblioteca: carta épica", biblioteca.epicCardPrice, sobra);
            Preco("Biblioteca: melhorar a sala", biblioteca.upgradeBaseCost, sobra);
        }

        if (mapas != null)
        {
            Preco("Sala de Mapas: batedor", mapas.scoutingCost, sobra);
            Preco("Sala de Mapas: desvio", mapas.detourCost, sobra);
            Preco("Sala de Mapas: ampliar", mapas.upgradeBaseCost, sobra);
        }

        if (cemiterio != null)
        {
            Preco("Cemitério: monumento", cemiterio.tributeCost, sobra);
            Preco("Cemitério: vigília", cemiterio.vigilCost, sobra);
        }

        Preco("Taverna: recruta nível 1", HeroFactory.SalaryFor(1), sobra);
        Preco("Taverna: recruta nível 3", HeroFactory.SalaryFor(3), sobra);
        if (taverna != null)
            Preco("Taverna: renovar a leva", taverna.refreshCost, sobra);

        Linha("");
        Linha("  A folha: cada herói custa o salário dele por semana fora (nível 1: "
            + $"{HeroFactory.SalaryFor(1)}, nível 3: {HeroFactory.SalaryFor(3)}, nível 5: {HeroFactory.SalaryFor(5)}).");
        Linha("  Uma semana parada na Taverna custa a mesma folha e um ciclo de Corrupção.");
        Linha("");
    }

    static void Preco(string nome, int preco, float porJornada)
    {
        float jornadas = porJornada > 0 ? preco / porJornada : 0f;
        Linha($"  {nome,-30} {preco,6}   {jornadas,6:F1}");
    }

    /// <summary>
    /// A run inteira, ciclo a ciclo: quanto ouro a guilda junta, quando o Chefe
    /// Supremo entra no quadro e quanta gente a permadeath custa até lá.
    ///
    /// É projeção, e não simulação: as regras do relógio vêm do
    /// <see cref="RunManager"/>, e o ouro e as mortes, da medição acima. O que ela
    /// responde é de ritmo — se o ouro deixa de ser decisão antes de a run acabar.
    /// </summary>
    static void RitmoDaRun()
    {
        Titulo("O RITMO DA RUN");

        float sobra = SobraPorJornada;

        // O catálogo do que existe para comprar, para a guilda inteira: as
        // relíquias uma vez cada, a Forja para os quatro do grupo, e as salas
        // que sobem de nível até o teto. Até 12/09 a conta somava a Forja de um
        // herói só, e dizia que tudo custava 2.660 — era por isso que a economia
        // parecia saturar no sétimo ciclo.
        const int Herois = PartyFormation.MaxSlots;
        int tudoQueExiste = ItemCatalog.Reliquias.Sum(r => r.preco);

        var forja = Object.FindObjectOfType<ForgeManager>(true);
        if (forja != null)
        {
            var alvo = HeroFactory.CreateHero("Teste", HeroClass.Warrior, 3);
            int porHeroi = 0;
            for (int nivel = 0; nivel < forja.maxUpgradeLevel; nivel++)
            {
                alvo.weaponLevel = nivel;
                alvo.armorLevel = nivel;
                porHeroi += forja.WeaponCost(alvo) + forja.ArmorCost(alvo);
            }
            Object.DestroyImmediate(alvo);
            tudoQueExiste += porHeroi * Herois;
        }

        var biblioteca = Object.FindObjectOfType<LibraryManager>(true);
        if (biblioteca != null) tudoQueExiste += biblioteca.upgradeBaseCost * (1 + 2 + 3);

        var mapas = Object.FindObjectOfType<MapRoomManager>(true);
        if (mapas != null) tudoQueExiste += mapas.upgradeBaseCost * (1 + 2 + 3);

        Linha("  ciclo   corrupção   o que acontece                          ouro acumulado   do catálogo");
        Linha("  ────────────────────────────────────────────────────────────────────────────────────");

        float ouro = MetaProgression.OuroBasePorRun;
        for (int ciclo = 1; ciclo <= 15; ciclo++)
        {
            float corrupcao = Mathf.Min(RunManager.CorruptionMax,
                                        RunManager.CorruptionStart + ciclo * RunManager.CorruptionPerCycle);

            string marco = "";
            if (corrupcao >= RunManager.BossThreshold && corrupcao - RunManager.CorruptionPerCycle < RunManager.BossThreshold)
                marco = "o Chefe Supremo entra no quadro";
            else if (corrupcao >= RunManager.CorruptionMax)
                marco = "o mundo é consumido — fim de run";

            ouro += sobra;
            float fracao = tudoQueExiste > 0 ? ouro / tudoQueExiste : 0f;

            Linha($"  {ciclo,5}   {corrupcao,8:F0}   {marco,-38}   {ouro,10:F0}   {fracao,8:P0}");

            if (corrupcao >= RunManager.CorruptionMax) break;
        }

        Linha("");
        Linha($"  A guilda inteira equipada — as {ItemCatalog.Reliquias.Count} relíquias, a Forja no nível "
            + $"{(forja != null ? forja.maxUpgradeLevel : 3)} para {Herois} heróis, Biblioteca e Sala de");
        Linha($"  Mapas no teto — custa cerca de {tudoQueExiste} de ouro, ou {tudoQueExiste / Mathf.Max(1f, sobra):F1} jornadas.");
        Linha($"  Ao fim da run a guilda juntou {ouro:F0}: {(tudoQueExiste > 0 ? ouro / tudoQueExiste : 0f):P0} do catálogo, sem contar cartas nem consumíveis.");
        Linha($"  O jogador tem {Mathf.CeilToInt((RunManager.CorruptionMax - RunManager.CorruptionStart) / RunManager.CorruptionPerCycle)} ciclos antes de o mundo acabar.");
        Linha("");
    }

    /// <summary>
    /// O que o jogador tem para fazer em cada tela, contado no acervo em vez de
    /// no palpite: quantas escolhas cada sala oferece por visita.
    /// </summary>
    static void Interacao()
    {
        Titulo("O QUE O JOGADOR ENCONTRA");

        CardData[] cartas = Resources.LoadAll<CardData>("Cards");
        EventData[] eventos = Resources.LoadAll<EventData>("Events");
        EnemyData[] inimigos = Resources.LoadAll<EnemyData>("Enemies");

        Linha($"  cartas no acervo ................. {cartas.Length} ({cartas.Length / 4} por classe jogável)");
        Linha($"  eventos de estrada ............... {eventos.Length}");
        Linha($"  inimigos ......................... {inimigos.Length}");
        Linha($"  relíquias · poções ............... {ItemCatalog.Reliquias.Count} · {ItemCatalog.Pocoes.Count}");
        Linha("");

        int comEscolhaTravada = eventos.Count(e => e.outcomes != null && e.outcomes.Any(o => o.RequiresCard));
        int comReforco = eventos.Count(e => e.outcomes != null
                                         && e.outcomes.Any(o => o.RequiresCard && o.empoweredConsequences != null));

        Linha($"  eventos com caminho que exige carta ......... {comEscolhaTravada} de {eventos.Length}");
        Linha($"  desses, com desfecho reforçado escrito ...... {comReforco}");
        Linha("");

        // Uma decisão só é decisão se as opções custarem coisas diferentes.
        int opcoesTotais = eventos.Sum(e => e.outcomes != null ? e.outcomes.Length : 0);
        Linha($"  opções de evento no total ................... {opcoesTotais}"
            + $"  ({opcoesTotais / (float)Mathf.Max(1, eventos.Length):F1} por evento)");
        Linha("");
    }

    static void Titulo(string texto)
    {
        relatorio.AppendLine();
        relatorio.AppendLine($"── {texto} ──");
        relatorio.AppendLine();
    }

    static void Linha(string texto) => relatorio.AppendLine(texto);
}
#endif
