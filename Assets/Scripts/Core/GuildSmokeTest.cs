#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Bateria de verificação da lógica de jogo, executável sem entrar em Play Mode.
/// Tools → Guild of Legends → Rodar Smoke Test
///
/// Exercita o código real (EventPool, EnemyPool, EventResolver, DeckGenerator).
/// A simulação de combate no fim reimplementa as regras do CombatManager, que não
/// dá para chamar direto por depender de UI e corrotinas. O que ela decide sozinha
/// é a jogada do jogador; o resto — mão, energia, bloqueio, intenções, formação e
/// estresse — segue o combate de verdade.
/// </summary>
public static class GuildSmokeTest
{
    private static StringBuilder report;
    private static int checks;
    private static int failures;
    private static int warnings;

    [MenuItem("Tools/Guild of Legends/Rodar Smoke Test")]
    public static void Run()
    {
        report = new StringBuilder();
        checks = 0;
        failures = 0;
        warnings = 0;

        Header("ASSETS");
        TestAssets();

        Header("O MUNDO");
        TestAreas();

        Header("ASPECTOS");
        TestBiomeMatching();

        Header("INIMIGOS");
        TestEnemyPool();

        Header("DECKS");
        TestDecks();

        Header("REGRAS DE DANO E ESTRESSE");
        TestCombatRules();

        Header("SIMULACAO DE JORNADAS");

        // 1000, e não 200. Três execuções seguidas do mesmo código deram 0,41 ·
        // 0,74 · 0,51 mortes por jornada — a oscilação entre rodadas era quase a
        // largura do alvo (0,33–0,67), e uma delas acusou balanceamento quebrado
        // sem nada ter mudado no jogo. Uma régua que reprova por sorteio faz
        // perder tempo caçando regressão que não existe, e pior: ensina a ignorar
        // o aviso. A amostra maior custa segundos.
        SimulateJourneys(1000);

        Header("SIMULACAO DE COMBATE");
        SimulateCombats(200);

        string resumo = failures == 0
            ? $"✅ SMOKE TEST OK — {checks} verificações, 0 falhas"
            : $"❌ SMOKE TEST COM {failures} FALHA(S) de {checks} verificações";

        if (warnings > 0)
            resumo += $" | {warnings} aviso(s) de balanceamento";

        report.Insert(0, resumo + "\n\n");

        // Gravado em arquivo, e não só no console: o balanceamento é comparado
        // entre execuções, e ler o Editor.log para isso é como procurar agulha —
        // o mesmo motivo pelo qual o Play Mode escreve PlayModeReport.txt.
        try
        {
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(PlayModeTestLauncher.ProjectRoot, "SmokeTestReport.txt"),
                $"(gerado em {System.DateTime.Now:yyyy-MM-dd HH:mm:ss})\n\n" + report.ToString());
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Smoke test: não consegui gravar o relatório — {e.Message}");
        }

        if (failures == 0)
            Debug.Log(report.ToString());
        else
            Debug.LogError(report.ToString());
    }

    #region Infra

    static void Header(string title)
    {
        report.AppendLine();
        report.AppendLine($"── {title} ──");
    }

    static void Check(bool condition, string description)
    {
        checks++;
        if (condition)
        {
            report.AppendLine($"  ok   {description}");
        }
        else
        {
            failures++;
            report.AppendLine($"  FALHA {description}");
        }
    }

    static void Info(string line)
    {
        report.AppendLine($"       {line}");
    }

    /// <summary>
    /// Expectativa de balanceamento. Não conta como falha: número fora do alvo
    /// pede ajuste de valores, não conserto de código.
    /// </summary>
    static void Expect(bool condition, string description)
    {
        report.AppendLine(condition ? $"  ok   {description}" : $"  ⚠️  {description}");
        if (!condition) warnings++;
    }

    #endregion

    #region Testes de conteúdo

    static void TestAssets()
    {
        var events = Resources.LoadAll<EventData>("Events");
        var enemies = Resources.LoadAll<EnemyData>("Enemies");
        var cards = Resources.LoadAll<CardData>("Cards");

        Check(events.Length > 0, $"eventos carregados de Resources/Events ({events.Length})");
        Check(enemies.Length > 0, $"inimigos carregados de Resources/Enemies ({enemies.Length})");
        Check(cards.Length > 0, $"cartas carregadas de Resources/Cards ({cards.Length})");

        int semOpcoes = events.Count(e => e.outcomes == null || e.outcomes.Length == 0);
        Check(semOpcoes == 0, $"todo evento tem ao menos uma opção (sem opções: {semOpcoes})");

        int textoVazio = events.Count(e =>
            e.outcomes != null && e.outcomes.Any(o => string.IsNullOrWhiteSpace(o.optionText)));
        Check(textoVazio == 0, $"toda opção tem texto (eventos com opção vazia: {textoVazio})");

        int semTitulo = events.Count(e => string.IsNullOrWhiteSpace(e.eventTitle));
        Check(semTitulo == 0, $"todo evento tem título (sem título: {semTitulo})");

        int hpZero = enemies.Count(e => e.maxHp <= 0);
        Check(hpZero == 0, $"todo inimigo tem HP > 0 (com HP inválido: {hpZero})");

        int semPeso = enemies.Count(e =>
            e.attackWeight + e.defendWeight + e.stressWeight + e.attackAllWeight <= 0);
        Check(semPeso == 0, $"todo inimigo tem ao menos um comportamento possível (inertes: {semPeso})");

        // Efeito sem case no CombatManager vira carta morta: ela sai da mão,
        // cobra a energia e não faz nada — enquanto a descrição promete algo.
        // Quatro das dezesseis viviam assim sem ninguém notar.
        var implementados = new HashSet<CombatEffectType>
        {
            CombatEffectType.Damage,       CombatEffectType.DamageAll,
            CombatEffectType.Block,        CombatEffectType.BlockAll,
            CombatEffectType.Heal,         CombatEffectType.HealAll,
            CombatEffectType.Debuff,       CombatEffectType.Buff,
            CombatEffectType.DrawCards,    CombatEffectType.GainEnergy,
            CombatEffectType.Poison,       CombatEffectType.ShieldBreak,
            CombatEffectType.BuffNextCard, CombatEffectType.Evade,
            CombatEffectType.Cleanse
        };

        var inertes = cards.Where(c => !implementados.Contains(c.combatEffect)).ToList();
        Check(inertes.Count == 0,
              $"toda carta faz algo em combate (inertes: {inertes.Count}"
              + (inertes.Count > 0 ? $" — {string.Join(", ", inertes.Select(c => c.cardName))}" : "")
              + ")");

        // A mesma trava do outro lado do jogo. Ela não existia, e por isso
        // Ressurgir (⚡4) e Flecha Precisa passaram meses cobrando energia na
        // estrada sem fazer nada — o mesmo bug já corrigido no combate.
        var inertesNaEstrada = cards.Where(c => c.journeyEffect == JourneyEffectType.None).ToList();
        Check(inertesNaEstrada.Count == 0,
              $"toda carta faz algo na jornada (inertes: {inertesNaEstrada.Count}"
              + (inertesNaEstrada.Count > 0
                    ? $" — {string.Join(", ", inertesNaEstrada.Select(c => c.cardName))}"
                    : "")
              + ")");

        // Efeito sem nenhuma carta é espaço de design vazio, não defeito: fica
        // como aviso para orientar a próxima leva de conteúdo.
        var comCarta = new HashSet<JourneyEffectType>(cards.Select(c => c.journeyEffect));
        var semCarta = System.Enum.GetValues(typeof(JourneyEffectType))
                                  .Cast<JourneyEffectType>()
                                  .Where(e => e != JourneyEffectType.None && !comCarta.Contains(e))
                                  .ToList();

        Info(semCarta.Count == 0
            ? "todo efeito de jornada tem ao menos uma carta"
            : $"efeitos de jornada sem carta nenhuma: {string.Join(", ", semCarta)}");

        var comCartaCombate = new HashSet<CombatEffectType>(cards.Select(c => c.combatEffect));
        var semCartaCombate = implementados.Where(e => !comCartaCombate.Contains(e)).ToList();

        Info(semCartaCombate.Count == 0
            ? "todo efeito de combate tem ao menos uma carta"
            : $"efeitos de combate implementados e sem carta: {string.Join(", ", semCartaCombate)}");

        // Uma opção que exige um efeito sem carta é um caminho que o jogador vê
        // e nunca pode tomar — pior que não existir, porque promete.
        var exigidos = events
            .Where(e => e.outcomes != null)
            .SelectMany(e => e.outcomes)
            .Where(o => o != null && o.RequiresCard)
            .Select(o => o.requiredEffect)
            .Distinct()
            .ToList();

        var impossiveis = exigidos.Where(e => !comCarta.Contains(e)).ToList();
        Check(impossiveis.Count == 0,
              $"todo requisito de carta dos eventos é satisfazível (impossíveis: {impossiveis.Count}"
              + (impossiveis.Count > 0 ? $" — {string.Join(", ", impossiveis)}" : "")
              + ")");

        int comRequisito = events.Count(e => e.outcomes != null && e.outcomes.Any(o => o != null && o.RequiresCard));
        Info($"eventos com caminho que exige carta: {comRequisito} de {events.Length}");

        // Um evento em que TODAS as opções exigem carta pode travar a jornada.
        var semSaidaLivre = events
            .Where(e => e.outcomes != null && e.outcomes.Length > 0
                     && e.outcomes.All(o => o != null && o.RequiresCard))
            .ToList();

        Check(semSaidaLivre.Count == 0,
              $"todo evento tem ao menos uma saída sem exigir carta (sem saída: {semSaidaLivre.Count}"
              + (semSaidaLivre.Count > 0 ? $" — {string.Join(", ", semSaidaLivre.Select(e => e.eventTitle))}" : "")
              + ")");
    }

    /// <summary>
    /// O plano navegável: sete áreas, todas alcançáveis, cada uma vestindo um
    /// aspecto diferente.
    ///
    /// Vale a pena travar isto porque o mapa é feito de dados, não de cena: uma
    /// vizinhança escrita só de um lado deixa a área inalcançável e o jogo não
    /// dá erro nenhum — o marcador continua no papel, e a trilha até ele some.
    /// É o mesmo tipo de defeito silencioso que já apareceu vinte vezes aqui:
    /// dado certo, exibição ausente.
    /// </summary>
    static void TestAreas()
    {
        Check(AreaCatalog.Todas.Length == 7, $"o mundo tem sete áreas ({AreaCatalog.Todas.Length})");

        var aspectos = new List<BiomeType>();
        int semFicha = 0, foraDoPapel = 0, assimetricas = 0;

        foreach (var area in AreaCatalog.Todas)
        {
            var ficha = AreaCatalog.De(area);
            if (ficha == null) { semFicha++; continue; }

            aspectos.Add(ficha.aspecto);

            if (ficha.posicao.x < 0f || ficha.posicao.x > 1f ||
                ficha.posicao.y < 0f || ficha.posicao.y > 1f) foraDoPapel++;

            // Vizinhança é mão dupla: se A lista B, B tem de listar A. Escrita
            // de um lado só, a trilha aparece e o caminho não conta.
            foreach (var vizinha in AreaCatalog.Vizinhas(area))
                if (!AreaCatalog.SaoVizinhas(vizinha, area)) assimetricas++;
        }

        Check(semFicha == 0, $"toda área tem ficha (sem ficha: {semFicha})");
        Check(foraDoPapel == 0, $"toda área cabe no plano (fora de 0..1: {foraDoPapel})");
        Check(assimetricas == 0, $"vizinhança é mão dupla (só de um lado: {assimetricas})");
        Check(aspectos.Distinct().Count() == aspectos.Count,
              $"cada área veste um aspecto próprio ({aspectos.Distinct().Count()} de {aspectos.Count})");

        // A volta: todo aspecto jogável tem de achar a área dele, ou os assets
        // daquele bioma ficam sem lugar no mundo.
        int orfaos = BiomeUtil.Playable.Count(b => AreaCatalog.Da(b) == AreaType.None);
        Check(orfaos == 0, $"todo aspecto tem área (órfãos: {orfaos})");

        foreach (var area in AreaCatalog.Todas)
        {
            var rota = AreaCatalog.Rota(area);
            bool alcancavel = rota.Count > 0 && rota[rota.Count - 1] == area;

            Check(alcancavel, $"{AreaCatalog.Nome(area)}: alcançável a pé da guilda"
                            + (alcancavel ? $" ({AreaCatalog.DiasDeIda(area) * 2} dias de estrada)" : ""));
        }

        // A expedição que nasce do clique no mapa: é ela que a partida inteira
        // usa desde que o quadro deixou de oferecer destino.
        int semExpedicao = 0, semDias = 0;
        foreach (var area in AreaCatalog.Todas)
        {
            QuestData e = QuestGenerator.GerarExpedicao(area, 3);
            if (e == null) { semExpedicao++; continue; }
            if (e.minDuration < 2 || e.maxDuration <= e.minDuration) semDias++;
        }

        Check(semExpedicao == 0, $"toda área gera expedição (falhas: {semExpedicao})");
        Check(semDias == 0, $"toda expedição tem duração válida (inválidas: {semDias})");

        // O quadro: pedidos, não destinos.
        Encomendas.Reiniciar();
        Encomendas.Renovar(0, 3);

        var pedidos = Encomendas.Ativas();
        Check(pedidos.Count == Encomendas.Vagas,
              $"o quadro pendura {Encomendas.Vagas} encomendas ({pedidos.Count})");
        Check(pedidos.All(e => e != null && e.premio > 0 && !string.IsNullOrEmpty(e.Pedido)),
              "toda encomenda pede algo e paga algo");

        // Prazo: o pedido sai do quadro quando vence, e outro entra no lugar.
        Encomendas.Renovar(Encomendas.PrazoEmCiclos + 1, 3);
        Check(Encomendas.Ativas().All(e => e.cicloLimite > Encomendas.PrazoEmCiclos),
              "encomenda vencida sai do quadro");

        Encomendas.Reiniciar();
    }

    static void TestBiomeMatching()
    {
        // Este é o teste do bug antigo: bioma da quest nunca casava com o do evento.
        foreach (BiomeType biome in BiomeUtil.Playable)
        {
            bool achouEspecifico = false;
            bool bossOk = false;

            for (int i = 0; i < 60; i++)
            {
                EventData e = EventPool.GetRandomEvent(biome, 100, 5);
                if (e != null && e.biome == biome) achouEspecifico = true;

                if (e != null && e.biome != BiomeType.Any && e.biome != biome)
                {
                    Check(false, $"{biome}: evento de bioma errado ({e.biome})");
                    return;
                }
            }

            EventData boss = EventPool.GetFinalEvent(biome);
            bossOk = boss != null && boss.isBossEvent;

            Check(bossOk, $"{BiomeUtil.GetDisplayName(biome)}: tem chefe alcançável ({boss?.eventTitle})");
            Info($"{BiomeUtil.GetDisplayName(biome)}: eventos próprios do bioma disponíveis = {achouEspecifico}");
        }
    }

    static void TestEnemyPool()
    {
        foreach (BiomeType biome in BiomeUtil.Playable)
        {
            var normal = EnemyPool.GetLineup(biome, false, 5);
            var boss = EnemyPool.GetLineup(biome, true, 5);

            Check(normal.Count > 0, $"{BiomeUtil.GetDisplayName(biome)}: encontro normal tem inimigos ({normal.Count})");
            Check(boss.Count > 0 && boss[0].isBoss, $"{BiomeUtil.GetDisplayName(biome)}: chefe montado ({boss.FirstOrDefault()?.enemyName})");
        }
    }

    static void TestDecks()
    {
        foreach (HeroClass cls in new[] { HeroClass.Warrior, HeroClass.Mage, HeroClass.Healer, HeroClass.Hunter })
        {
            HeroData hero = HeroFactory.CreateHero("Teste", cls, 3);
            DeckData deck = DeckGenerator.GenerateDeckForHero(hero);

            Check(deck.cards.Count >= 8 && deck.cards.Count <= 12,
                $"{cls}: deck com {deck.cards.Count} cartas (alvo 8–12)");

            Object.DestroyImmediate(hero);
            Object.DestroyImmediate(deck);
        }

        // O laço acima testa as quatro classes jogáveis, e era exatamente por
        // isso que o defeito passava: quem sorteia o recruta da taverna é a
        // HeroFactory, que varria o enum inteiro e oferecia Ladino e Bardo — sem
        // uma única carta no acervo. O herói entrava com o baralho de emergência
        // do DeckGenerator (oito cópias de um "ataque básico" criado em memória),
        // pelo salário cheio e sem nada na tela dizendo isso.
        var oferecidas = new HashSet<HeroClass>();
        for (int i = 0; i < 200; i++)
        {
            HeroData recruta = HeroFactory.CreateRandomHero(1, 3);
            oferecidas.Add(recruta.heroClass);
            Object.DestroyImmediate(recruta);
        }

        CardData[] acervo = Resources.LoadAll<CardData>("Cards");

        foreach (HeroClass cls in oferecidas)
        {
            int proprias = 0;
            foreach (CardData c in acervo)
                if (c != null && c.requiredClass == cls) proprias++;

            Check(proprias >= 4, $"a taverna oferece {cls} e o acervo tem {proprias} carta(s) da classe");
        }
    }

    static void TestCombatRules()
    {
        // Beira da Morte: o primeiro golpe letal não mata.
        HeroData hero = HeroFactory.CreateHero("Alvo", HeroClass.Warrior, 1);
        hero.currentHp = 5;
        var party = new List<HeroData> { hero };
        var res = new EventResolver.Resolution();

        EventResolver.DealDamage(hero, 50, party, res);
        Check(!hero.isDead && hero.isOnDeathsDoor, "dano letal leva à Beira da Morte, não à morte");

        // O segundo golpe pode matar — testado estatisticamente.
        int mortes = 0;
        for (int i = 0; i < 400; i++)
        {
            HeroData h = HeroFactory.CreateHero("Alvo", HeroClass.Warrior, 1);
            h.currentHp = 0;
            h.isOnDeathsDoor = true;
            var p = new List<HeroData> { h };
            EventResolver.DealDamage(h, 10, p, new EventResolver.Resolution());
            if (h.isDead) mortes++;
            Object.DestroyImmediate(h);
        }
        float taxaMorte = mortes / 400f;
        Check(taxaMorte > 0.2f && taxaMorte < 0.9f, $"golpe na Beira da Morte mata em {taxaMorte:P0} dos casos");

        // Estresse a 100 precisa virar aflição ou virtude.
        int afligidos = 0, virtuosos = 0;
        for (int i = 0; i < 300; i++)
        {
            HeroData h = HeroFactory.CreateHero("Nervoso", HeroClass.Mage, 1);
            var p = new List<HeroData> { h };
            var r = new EventResolver.Resolution();
            EventResolver.AddStress(h, 500f, r);

            var outcome = new EventOutcome { consequences = new EventConsequences() };
            EventResolver.Resolve(outcome, p, 0f);

            if (MentalStateUtil.IsAffliction(h.mentalState)) afligidos++;
            else if (MentalStateUtil.IsVirtue(h.mentalState)) virtuosos++;

            Object.DestroyImmediate(h);
        }
        Check(afligidos + virtuosos == 300, $"estresse máximo sempre resolve em estado mental ({afligidos + virtuosos}/300)");
        Info($"aflições {afligidos}, virtudes {virtuosos} (virtude esperada ~22%)");

        Object.DestroyImmediate(hero);
    }

    #endregion

    #region Simulações

    /// <summary>
    /// Letalidade alvo, escolhida pelo autor: punitiva, no espírito de Darkest
    /// Dungeon — 1 a 2 mortes a cada 3 jornadas, com party de 4. Fora dessa faixa
    /// o jogo deixa de ser o que ele pediu: abaixo vira passeio, acima vira
    /// aniquilação (era 1,16 antes deste ajuste).
    /// </summary>
    const float MortesPorJornadaMin = 0.33f;
    const float MortesPorJornadaMax = 0.67f;

    /// <summary>Espelha JourneyManager.darknessStress; as duas devem andar juntas.</summary>
    const float DarknessStress = 5f;

    /// <summary>
    /// Energia e mão do combate, lidas da instância da cena — que é onde o valor
    /// de verdade mora, já que são campos serializados.
    ///
    /// <c>OverrideEnergiaDeCombate</c> existe só para a varredura de parâmetros:
    /// permite medir "e se a energia fosse 4?" sem tocar na cena.
    /// </summary>
    public static int? OverrideEnergiaDeCombate;
    public static int? OverrideCartasPorTurno;

    /// <summary>
    /// Ajuste aplicado a cada herói do grupo simulado, logo depois de criado e
    /// antes de o baralho ser montado. Nulo na medição normal.
    ///
    /// É por aqui que a auditoria de jogabilidade compara "com" e "sem": a mesma
    /// simulação roda duas vezes, e a diferença é o que aquele sistema vale.
    /// </summary>
    public static System.Action<HeroData> PrepararHeroi;

    /// <summary>
    /// O jogador que ignora o baralho fora do combate. Serve para medir quanto o
    /// deck vale na estrada — a pergunta que o autor levantou em 21/08: "não
    /// existe motivo real para usar as cartas fora de combate".
    /// </summary>
    public static bool SemCartasNaEstrada;

    /// <summary>
    /// Roda a simulação de jornada e devolve os números, sem escrever relatório.
    /// Usada pela auditoria de jogabilidade.
    /// </summary>
    public static JourneyStats MedirJornadas(int runs) => RodarJornadas(runs);

    static CombatManager CombateDaCena =>
        Object.FindObjectOfType<CombatManager>(true);

    static int EnergiaDeCombate
    {
        get
        {
            if (OverrideEnergiaDeCombate.HasValue) return OverrideEnergiaDeCombate.Value;
            var cm = CombateDaCena;
            return cm != null ? cm.baseEnergy : 3;
        }
    }

    static int CartasPorTurno
    {
        get
        {
            if (OverrideCartasPorTurno.HasValue) return OverrideCartasPorTurno.Value;
            var cm = CombateDaCena;
            return cm != null ? cm.cardsPerTurn : 5;
        }
    }

    /// <summary>
    /// Varre energia × tamanho de mão de uma vez. Com uma variável só a curva
    /// satura perto de 0,8 mortes por jornada e some a impressão de que o
    /// problema acabou — é preciso ver as duas juntas.
    /// </summary>
    public static string VarrerCombate(int[] energias, int[] maos, int runs)
    {
        var sb = new StringBuilder();
        sb.AppendLine("energia | mão | mortes/jornada | chefe | estrada | sobrev.");

        int? energiaOriginal = OverrideEnergiaDeCombate;
        int? maoOriginal = OverrideCartasPorTurno;

        try
        {
            foreach (int energia in energias)
            foreach (int mao in maos)
            {
                OverrideEnergiaDeCombate = energia;
                OverrideCartasPorTurno = mao;

                JourneyStats s = RodarJornadas(runs);
                sb.AppendLine($"   {energia}    |  {mao}  |      {s.mortesPorJornada:F2}      "
                            + $"| {s.mortesNoChefe:F2}  |  {s.mortesNaEstrada:F2}   | {s.sobrevivencia:P0}");
            }
        }
        finally
        {
            OverrideEnergiaDeCombate = energiaOriginal;
            OverrideCartasPorTurno = maoOriginal;
        }

        return sb.ToString();
    }

    /// <summary>
    /// Simula jornadas inteiras: eventos, cartas jogadas pelo jogador, combates
    /// do caminho e desgaste da estrada.
    ///
    /// Duas correções sobre a versão anterior, ambas medidas em 14/08:
    ///
    /// 1. **Ela não travava os combates.** Eventos de combate e o chefe eram
    ///    resolvidos pelo texto, e a luta ficava só em SimulateCombats, à parte.
    ///    Como o combate é onde o grupo mais perde HP (87 contra 40 numa run de
    ///    Play Mode), a jornada saía muito mais leve do que é.
    /// 2. **Ela jogava sem cartas.** O jogador real gasta energia antes de
    ///    decidir e reduz o desfecho em até 75%; o simulador entrava com 0 de
    ///    mitigação, o que puxava a letalidade para cima.
    ///
    /// Os dois erros se cancelavam parcialmente, e o número resultante — 0,54
    /// mortes por jornada — não media nem um jogo nem o outro.
    /// </summary>
    /// <summary>O que uma leva de jornadas simuladas produziu.</summary>
    public struct JourneyStats
    {
        public float mortesPorJornada;
        public float mortesNoChefe;

        /// <summary>
        /// O que o chefe cobra <b>na jornada em que ele aparece</b>.
        ///
        /// Desde 11/09 ele só está na luta de selo — uma em cada cinco jornadas
        /// simuladas. Diluído em todas, o número afunda para perto de zero e
        /// deixa de dizer se o clímax ainda cobra alguma coisa.
        /// </summary>
        public float mortesPorLutaDeSelo;
        public float mortesEmCombateComum;
        public float mortesNaEstrada;
        public float cartasJogadas;
        public float combates;
        public float sobrevivencia;
        public float duracaoMedia;
        public float aflicoes;

        /// <summary>Ouro que a estrada paga por jornada, fora o contrato.</summary>
        public float ouroDosEventos;

        /// <summary>O contrato médio das missões sorteadas.</summary>
        public float ouroDosContratos;
    }

    static void SimulateJourneys(int runs)
    {
        JourneyStats s = RodarJornadas(runs);

        Info($"{runs} jornadas: {s.sobrevivencia:P1} de sobrevivência");
        Info($"duração média: {s.duracaoMedia:F1} dias");
        Info($"combates travados: {s.combates:F2} por jornada"
           + $" | cartas jogadas na estrada: {s.cartasJogadas:F2} por jornada");
        Info($"o chefe cobra {s.mortesPorLutaDeSelo:F2} morte(s) por luta de selo — "
           + "é a única jornada em que ele aparece");
        Info($"origem das mortes por jornada — chefe: {s.mortesNoChefe:F2}"
           + $" | encontro do caminho: {s.mortesEmCombateComum:F2}"
           + $" | estrada (fome, eventos): {s.mortesNaEstrada:F2}");
        Info($"heróis que sucumbiram ao estresse: {s.aflicoes:P0}");
        Info($"mortes por jornada: {s.mortesPorJornada:F2} (alvo {MortesPorJornadaMin:F2}–{MortesPorJornadaMax:F2})");

        // O KPI é a letalidade, não a sobrevivência: "punitivo" tem piso e teto.
        // Ficar abaixo do piso é tão fora do alvo quanto passar do teto.
        Expect(s.mortesPorJornada >= MortesPorJornadaMin && s.mortesPorJornada <= MortesPorJornadaMax,
            $"letalidade {s.mortesPorJornada:F2} mortes/jornada dentro do alvo punitivo "
            + $"({MortesPorJornadaMin:F2}–{MortesPorJornadaMax:F2}), jornada inteira: "
            + "eventos, cartas e combates");

        Check(s.mortesPorJornada > 0f, "heróis realmente podem morrer");

        // O piso do chefe mora aqui, e não em SimulateCombats, por causa do
        // tamanho da amostra: no combate isolado o chefe cobra de 0,02 a 0,06
        // mortes — 4 a 12 mortes em 200 lutas, ruído puro para servir de piso.
        // Medido ao longo da jornada, com o grupo chegando desgastado de verdade,
        // o mesmo chefe cobra ~0,27, e aí o sinal aguenta um alvo.
        //
        // É o que impede o chefe de virar formalidade: se as mortes migrarem
        // todas para a fome e os eventos, a letalidade total continua no alvo e
        // o clímax da jornada some sem ninguém notar.
        // O piso mudou de régua em 11/09, não de exigência: o chefe deixou de
        // fechar toda jornada e passou a morar só na luta de selo, uma em cada
        // cinco aqui. Medido por jornada geral, 0,25 por luta aparece como 0,05 —
        // e a trava reprovaria um chefe saudável.
        Expect(s.mortesPorLutaDeSelo >= 0.10f,
            $"o chefe é o clímax e cobra por isso (piso 0,10 por luta de selo): {s.mortesPorLutaDeSelo:F2}");
    }

    /// <summary>
    /// Varredura de parâmetro: mede a letalidade da jornada para cada valor de
    /// energia de combate, sem tocar na cena. Serve para escolher o número antes
    /// de gravá-lo, em vez de gravar e torcer.
    /// </summary>
    public static string VarrerEnergiaDeCombate(int[] valores, int runs)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"energia | mortes/jornada | chefe | estrada | sobrev. | cartas/jornada");

        int? original = OverrideEnergiaDeCombate;
        try
        {
            foreach (int energia in valores)
            {
                OverrideEnergiaDeCombate = energia;
                JourneyStats s = RodarJornadas(runs);
                sb.AppendLine($"   {energia}    |     {s.mortesPorJornada:F2}      "
                            + $"| {s.mortesNoChefe:F2}  |  {s.mortesNaEstrada:F2}   "
                            + $"| {s.sobrevivencia:P0}    | {s.cartasJogadas:F2}");
            }
        }
        finally
        {
            OverrideEnergiaDeCombate = original;
        }

        return sb.ToString();
    }

    /// <summary>
    /// Varre a força do chefe olhando os DOIS alvos ao mesmo tempo: a letalidade
    /// da jornada e o que o chefe cobra em gente.
    ///
    /// Otimizar um sem ver o outro foi como se chegou aqui — dar energia ao
    /// jogador trouxe a letalidade para dentro da faixa, mas esvaziou o chefe.
    ///
    /// **Teste de sanidade obrigatório:** a curva tem de ser monotônica. Um chefe
    /// mais forte que mate menos denuncia heurística com degrau no simulador —
    /// já aconteceu, e calibrou um balanceamento inteiro contra um adversário
    /// artificialmente incompetente.
    /// </summary>
    public static string VarrerEscalaDeChefe(float[] escalas, int runs)
    {
        var sb = new StringBuilder();
        sb.AppendLine("escala | mortes/jornada | chefe | estrada | mortes/combate do chefe | vitória");

        float original = EscalaDeChefe;
        try
        {
            foreach (float escala in escalas)
            {
                EscalaDeChefe = escala;

                JourneyStats s = RodarJornadas(runs);
                var chefe = MedirChefe(runs, 0.5f, 50f);

                sb.AppendLine($" {escala:F2}  |      {s.mortesPorJornada:F2}      | {s.mortesNoChefe:F2}  "
                            + $"|  {s.mortesNaEstrada:F2}   |          {chefe.mortes:F2}           | {chefe.vitoria:P0}");
            }
        }
        finally
        {
            EscalaDeChefe = original;
        }

        return sb.ToString();
    }

    /// <summary>
    /// O que um combate de chefe cobra, sem escrever no relatório — serve às
    /// varreduras, que rodam fora do Run(). Mortes primeiro: é o KPI; a vitória
    /// vai junto só para enxergar se o chefe virou formalidade.
    /// </summary>
    static (float mortes, float vitoria) MedirChefe(int runs, float hpPerdidoFrac, float estresse)
    {
        var grupos = new List<SimParty>();
        for (int i = 0; i < 10; i++) grupos.Add(SimParty.Create());

        int vitorias = 0, mortos = 0;
        for (int r = 0; r < runs; r++)
        {
            SimParty grupo = grupos[r % grupos.Count];
            grupo.Reset(hpPerdidoFrac, estresse);

            var lineup = EnemyPool.GetLineup(BiomeType.Forest, true, 5);
            CombatOutcome resultado = SimulateOneCombat(grupo.heroes, grupo.ownership, grupo.deck, lineup);

            if (resultado.vitoria) vitorias++;
            mortos += resultado.mortos;
        }

        foreach (var g in grupos) g.Dispose();
        return (mortos / (float)runs, vitorias / (float)runs);
    }

    static JourneyStats RodarJornadas(int runs)
    {
        int totalHerois = 0, sobreviventes = 0, mortos = 0;
        int totalAflicoes = 0;
        int combatesTravados = 0, cartasJogadas = 0;
        var duracoes = new List<int>();

        // O que a estrada paga, sem o contrato da missão: é o que a auditoria de
        // jogabilidade compara com o preço das coisas da guilda.
        int ouroDosEventos = 0;
        int ouroDosContratos = 0;

        // De onde vêm as mortes. Sem separar, "2,40 mortes por jornada" não diz
        // se o culpado é o chefe, o encontro do caminho ou a fome — e as três
        // causas pedem correções opostas.
        int mortesEmCombateComum = 0, mortesNoChefe = 0, mortesNaEstrada = 0;
        int lutasDeSelo = 0;

        // Grupos reciclados, como em SimulateCombats: recriar a party a cada run
        // geraria milhares de decks e travaria o Editor por minutos.
        // 100 grupos, e não 25: com 25, o número de jornadas simuladas quase não
        // importava — quem mandava na letalidade era o sorteio dessas 25 partys,
        // refeito a cada execução. Daí três rodadas idênticas darem 0,41 · 0,74 ·
        // 0,51: era a composição do elenco variando, não o jogo. Subir só as
        // jornadas não resolvia, porque o n de verdade era o número de grupos.
        var grupos = new List<SimParty>();
        for (int i = 0; i < 100; i++) grupos.Add(SimParty.Create());

        for (int r = 0; r < runs; r++)
        {
            // Uma jornada em cada cinco é luta de selo.
            //
            // Não é enfeite de simulação: desde 11/09 o chefe só aparece nessas,
            // e a partida real cabe umas três em quinze ciclos. Medir só
            // expedições comuns tiraria o chefe da conta de letalidade — e ele é
            // justamente a morte que o alvo existe para vigiar.
            bool deSelo = r % 5 == 4;
            if (deSelo) lutasDeSelo++;

            QuestData quest;
            if (deSelo)
            {
                var area = AreaCatalog.Todas[r % AreaCatalog.Todas.Length];
                quest = QuestGenerator.GenerateRegionBossQuest(AreaCatalog.Aspecto(area), 3);
            }
            else
            {
                quest = QuestGenerator.ExpedicaoQualquer(3);
            }

            SimParty grupo = grupos[r % grupos.Count];
            grupo.Reset(0f, 0f);
            List<HeroData> party = grupo.heroes;
            totalHerois += party.Count;

            int dias = quest.GetActualDuration();
            duracoes.Add(dias);
            ouroDosContratos += quest.GetTotalReward(dias);

            // As provisões que a tela de preparação entrega. Antes a simulação
            // usava o padrão interno do StartJourney (10–14 rações), folga que o
            // jogador não tem — e media uma jornada mais fácil que a real.
            //
            // Desde 11/09 elas vêm da mesma régua da tela, que acompanha os dias:
            // com o plano navegável a expedição ao Covil dura o dobro da que vai
            // à Mata, e uma mochila fixa faria a distância matar de fome em vez
            // de cobrar tempo.
            int previstos = QuestSelectionUI.DiasPrevistos(quest);
            int racoes = QuestSelectionUI.RacoesPara(previstos);
            int tochas = QuestSelectionUI.TochasPara(previstos);

            EventPool.ResetHistory();

            // Baralho da jornada, com as mesmas regras de mão do JourneyManager.
            var mao = new SimHand(grupo.deck, 5, 7);
            int energia = EnergiaInicialDaJornada;
            int protecaoClima = 0;
            bool evitarProximoCombate = false;

            for (int dia = 1; dia <= dias + 1 && party.Any(h => h.IsAlive); dia++)
            {
                // O fim da rota, pela mesma regra do JourneyMapGenerator: chefe
                // só na luta de selo e na final; expedição comum fecha num
                // encontro forte. Medir com chefe em toda jornada media um jogo
                // que deixou de existir em 11/09 — e o simulador errar mais que
                // o jogo já custou cinco conclusões invertidas aqui.
                bool ehFimDaRota = dia == dias + 1;
                bool ehChefe = ehFimDaRota && (quest.isRegionBoss || quest.isFinalBoss);

                EventData ev = ehChefe
                    ? EventPool.GetFinalEvent(quest.biomeType)
                    : ehFimDaRota
                        ? EventPool.GetStrongEncounter(quest.biomeType, quest.corruptionLevel, dia)
                        : EventPool.GetRandomEvent(quest.biomeType, quest.corruptionLevel, dia);

                if (ev == null) continue;

                int diasGastos = 1;

                bool ehCombate = ev.eventType == JourneyEventType.Combat || ev.isBossEvent;
                if (ehCombate && !(evitarProximoCombate && !ev.isBossEvent))
                {
                    // O caminho que o jogo quer premiar: resolver na mesa.
                    var lineup = EnemyPool.GetLineup(quest.biomeType, ev.isBossEvent, dia, dias + 1);

                    int vivosAntes = party.Count(h => h.IsAlive);
                    SimulateOneCombat(party, grupo.ownership, grupo.deck, lineup);
                    int caidos = vivosAntes - party.Count(h => h.IsAlive);

                    if (ev.isBossEvent) mortesNoChefe += caidos;
                    else mortesEmCombateComum += caidos;

                    combatesTravados++;
                }
                else if (ehCombate)
                {
                    // Intimidação gastou-se aqui: não há luta neste trecho.
                    evitarProximoCombate = false;
                }
                else
                {
                    if (ev.outcomes == null || ev.outcomes.Length == 0) continue;

                    // O jogador prepara o trecho com cartas antes de decidir.
                    float mitigacao = 0f;
                    var efeitosJogados = new HashSet<JourneyEffectType>();

                    foreach (CardData carta in SemCartasNaEstrada
                                                 ? System.Linq.Enumerable.Empty<CardData>()
                                                 : mao.EscolherPreparo(energia, ev))
                    {
                        energia -= carta.energyCost;
                        mitigacao = Mathf.Min(JourneyManager.MaxMitigationValue,
                                              mitigacao + JourneyManager.GetMitigationFor(carta));
                        AplicarEfeitoDeEstrada(carta, party, ref racoes, ref protecaoClima,
                                               ref evitarProximoCombate);
                        efeitosJogados.Add(carta.journeyEffect);
                        mao.Descartar(carta);
                        cartasJogadas++;
                    }

                    // Opção travada não entra no sorteio: sem a carta exigida,
                    // aquele caminho não existe para o jogador.
                    var disponiveis = ev.outcomes
                        .Where(o => !o.RequiresCard || efeitosJogados.Contains(o.requiredEffect))
                        .ToList();

                    if (disponiveis.Count == 0) disponiveis.Add(ev.outcomes[0]);

                    EventOutcome escolha = disponiveis[Random.Range(0, disponiveis.Count)];

                    // Carta certa também melhora o desfecho, quando o evento define isso.
                    if (escolha.RequiresCard && escolha.empoweredConsequences != null
                        && efeitosJogados.Contains(escolha.requiredEffect))
                    {
                        escolha = new EventOutcome
                        {
                            optionText = escolha.optionText,
                            consequences = escolha.empoweredConsequences,
                            extraDays = escolha.extraDays,
                            triggersCorruption = escolha.triggersCorruption
                        };
                    }

                    EventResolver.Resolution efeito = EventResolver.Resolve(escolha, party, mitigacao);
                    ouroDosEventos += efeito.goldChange;
                    diasGastos += escolha.extraDays;
                }

                // Manutenção diária, igual à do JourneyManager.
                for (int d = 0; d < diasGastos; d++)
                {
                    racoes--;
                    tochas--;

                    if (protecaoClima > 0)
                    {
                        protecaoClima--;
                        racoes = Mathf.Max(0, racoes);
                        tochas = Mathf.Max(0, tochas);
                        continue;
                    }

                    if (racoes <= 0)
                    {
                        racoes = 0;
                        foreach (var h in party.Where(x => x.IsAlive).ToList())
                            EventResolver.DealDamage(h, 5, party, new EventResolver.Resolution());
                    }

                    if (tochas <= 0)
                    {
                        tochas = 0;
                        foreach (var h in party.Where(x => x.IsAlive))
                            EventResolver.AddStress(h, DarknessStress, new EventResolver.Resolution());
                    }
                }
            }

            foreach (var h in party)
            {
                if (h.isDead) mortos++;
                else sobreviventes++;

                if (MentalStateUtil.IsAffliction(h.mentalState)) totalAflicoes++;
            }

            Object.DestroyImmediate(quest);
        }

        foreach (var g in grupos) g.Dispose();

        mortesNaEstrada = mortos - mortesNoChefe - mortesEmCombateComum;

        return new JourneyStats
        {
            mortesPorJornada = mortos / (float)runs,
            mortesNoChefe = mortesNoChefe / (float)runs,
            mortesPorLutaDeSelo = lutasDeSelo > 0 ? mortesNoChefe / (float)lutasDeSelo : 0f,
            mortesEmCombateComum = mortesEmCombateComum / (float)runs,
            mortesNaEstrada = mortesNaEstrada / (float)runs,
            cartasJogadas = cartasJogadas / (float)runs,
            combates = combatesTravados / (float)runs,
            sobrevivencia = sobreviventes / (float)totalHerois,
            duracaoMedia = (float)duracoes.Average(),
            aflicoes = totalAflicoes / (float)totalHerois,
            ouroDosEventos = ouroDosEventos / (float)runs,
            ouroDosContratos = ouroDosContratos / (float)runs
        };
    }

    /// <summary>Espelha JourneyManager.maxEnergy. A energia da estrada não regenera
    /// entre trechos — só o descanso devolve +2, ao custo de um dia de mantimentos.</summary>
    const int EnergiaInicialDaJornada = 5;

    /// <summary>
    /// A mão da jornada e a decisão de quando gastar energia.
    ///
    /// O ponto delicado é que a energia é escassa: são 5 para a jornada inteira,
    /// contra 6 a 9 trechos. Um simulador que jogasse tudo no primeiro evento
    /// mediria um jogador que não existe, e um que nunca jogasse repetiria o erro
    /// anterior. O modelo aqui é o de um jogador econômico: no máximo uma carta
    /// por trecho, preferindo a de maior proteção, e só gastando com as cartas
    /// fracas (10%) quando ainda sobra energia.
    /// </summary>
    class SimHand
    {
        private readonly List<CardData> mao = new List<CardData>();
        private readonly List<CardData> monte = new List<CardData>();

        public SimHand(DeckData deck, int tamanhoMao, int maxMao)
        {
            monte.AddRange(deck.cards.Where(c => c != null));
            Shuffle(monte);

            for (int i = 0; i < Mathf.Min(tamanhoMao, monte.Count); i++)
            {
                mao.Add(monte[0]);
                monte.RemoveAt(0);
            }
        }

        public IEnumerable<CardData> EscolherPreparo(int energia, EventData evento)
        {
            // Prioridade máxima: a carta que destrava uma opção do evento. É a
            // jogada que muda o que o grupo PODE fazer, não só o quanto apanha —
            // um jogador que ignorasse isso mediria o jogo de antes da mudança.
            var exigidos = evento != null && evento.outcomes != null
                ? new HashSet<JourneyEffectType>(
                    evento.outcomes.Where(o => o != null && o.RequiresCard)
                                   .Select(o => o.requiredEffect))
                : new HashSet<JourneyEffectType>();

            CardData chave = mao.FirstOrDefault(c => c.energyCost <= energia
                                                  && exigidos.Contains(c.journeyEffect));
            if (chave != null)
            {
                yield return chave;
                energia -= chave.energyCost;
            }

            CardData melhor = null;
            float melhorProtecao = 0f;

            foreach (var carta in mao)
            {
                if (carta == chave) continue;
                if (carta.energyCost > energia) continue;

                float protecao = JourneyManager.GetMitigationFor(carta);

                // Carta fraca só entra se a energia estiver folgada.
                if (protecao <= 0.10f && energia < 3) continue;

                if (protecao > melhorProtecao)
                {
                    melhorProtecao = protecao;
                    melhor = carta;
                }
            }

            if (melhor != null) yield return melhor;
        }

        public void Descartar(CardData carta)
        {
            mao.Remove(carta);
        }
    }

    /// <summary>
    /// O que a carta muda na estrada, além da mitigação: comida, abrigo, cura e
    /// intimidação. Os efeitos de informação (revelar evento) não alteram
    /// sobrevivência e ficam de fora.
    /// </summary>
    static void AplicarEfeitoDeEstrada(CardData carta, List<HeroData> party,
                                       ref int racoes, ref int protecaoClima,
                                       ref bool evitarProximoCombate)
    {
        switch (carta.journeyEffect)
        {
            case JourneyEffectType.GainFood:
                racoes += carta.journeyEffectValue;
                break;

            case JourneyEffectType.ExtraRations:
                racoes += 5;
                break;

            case JourneyEffectType.ProtectFromWeather:
                protecaoClima += Mathf.Max(2, carta.journeyEffectValue);
                break;

            case JourneyEffectType.HealInjury:
                var ferido = party.FirstOrDefault(h => h.isInjured && h.IsAlive);
                if (ferido != null) ferido.isInjured = false;
                break;

            case JourneyEffectType.Purify:
                foreach (var h in party.Where(x => x.IsAlive))
                    h.isInjured = false;
                break;

            case JourneyEffectType.Intimidate:
                evitarProximoCombate = true;
                break;

            case JourneyEffectType.RestoreMorale:
                foreach (var h in party.Where(x => x.IsAlive))
                    h.morale = Mathf.Min(100f, h.morale + carta.journeyEffectValue);
                break;

            case JourneyEffectType.Revive:
            {
                HeroData alvo = party.FirstOrDefault(h => h.IsAlive && h.isOnDeathsDoor)
                             ?? party.Where(h => h.IsAlive)
                                     .OrderBy(h => h.currentHp / (float)Mathf.Max(1, h.maxHp))
                                     .FirstOrDefault();
                if (alvo == null) break;

                alvo.isOnDeathsDoor = false;
                alvo.currentHp = Mathf.Min(alvo.maxHp,
                    alvo.currentHp + Mathf.Max(1, Mathf.RoundToInt(alvo.maxHp * 0.5f)));
                alvo.stress = Mathf.Max(0f, alvo.stress - 20f);
                break;
            }
        }
    }

    /// <summary>
    /// Um inimigo dentro da simulação. Espelha o EnemyInstance do CombatManager.
    ///
    /// Os atributos são copiados na criação em vez de lidos do asset a cada uso,
    /// para a varredura conseguir perguntar "e se o chefe fosse 30% mais forte?"
    /// sem editar — muito menos estragar — os cinco assets de chefe.
    /// </summary>
    class SimEnemy
    {
        public EnemyData data;
        public int hp;
        public int block;
        public EnemyIntent intent;

        public int dano;
        public int bloqueio;
        public int estresse;

        // Espelham EnemyInstance.poison / damageDebuff / debuffTurns.
        public int veneno;
        public int reducaoDeDano;
        public int turnosEnfraquecido;

        public bool IsAlive => hp > 0;

        /// <summary>Dano já descontado o enfraquecimento, como no CombatManager.</summary>
        public int DanoAtual => Mathf.Max(1, dano - (turnosEnfraquecido > 0 ? reducaoDeDano : 0));

        public static SimEnemy From(EnemyData e)
        {
            float k = e.isBoss ? EscalaDeChefe : 1f;
            return new SimEnemy
            {
                data = e,
                hp = Mathf.RoundToInt(e.maxHp * k),
                dano = Mathf.RoundToInt(e.attackDamage * k),
                bloqueio = e.blockAmount,
                estresse = e.stressDamage
            };
        }
    }

    /// <summary>Multiplicador de HP e dano dos chefes, só para varredura.</summary>
    public static float EscalaDeChefe = 1f;

    /// <summary>O que sobrou de um combate simulado.</summary>
    struct CombatOutcome
    {
        public bool vitoria;
        public int turnos;
        public int mortos;
    }

    /// <summary>Efeitos temporários em vigor, espelhando os do CombatManager.</summary>
    class SimBuffs
    {
        public int groupDamageBonus;
        public int groupDamageBonusTurns;
        public float nextCardMultiplier = 1f;
        public readonly HashSet<HeroData> evading = new HashSet<HeroData>();
    }

    /// <summary>
    /// Um grupo de teste com o baralho de jornada dele, reaproveitado entre
    /// combates. Recriar a party a cada run geraria milhares de decks, e o
    /// DeckGenerator escreve no console a cada um — só isso já trava o Editor.
    /// </summary>
    class SimParty
    {
        public List<HeroData> heroes;
        public DeckData deck;
        public CardOwnership ownership;

        public static SimParty Create()
        {
            var heroes = new List<HeroData>
            {
                HeroFactory.CreateHero("A", HeroClass.Warrior, 3),
                HeroFactory.CreateHero("B", HeroClass.Mage, 2),
                HeroFactory.CreateHero("C", HeroClass.Healer, 2),
                HeroFactory.CreateHero("D", HeroClass.Hunter, 1)
            };

            // O grupo que a auditoria de jogabilidade quiser: com a arma forjada,
            // com relíquia, de nível mais alto. Sem isto, medir o que cada compra
            // da guilda vale exigiria uma segunda cópia desta simulação inteira —
            // e duas cópias divergem no primeiro ajuste de regra.
            if (PrepararHeroi != null)
                foreach (HeroData h in heroes) PrepararHeroi(h);

            var build = JourneyDeckBuilder.Build(heroes[0], heroes);
            return new SimParty { heroes = heroes, deck = build.deck, ownership = build.ownership };
        }

        /// <summary>
        /// Devolve o grupo ao estado de partida do cenário. Personalidade e
        /// traço ficam de pé: é deles que vem a variedade de quem aguenta
        /// estresse e quem desmorona.
        /// </summary>
        public void Reset(float hpPerdidoFrac, float estresse)
        {
            foreach (var h in heroes)
            {
                h.isDead = false;
                h.isOnDeathsDoor = false;
                h.isInjured = false;
                h.mentalState = MentalState.Normal;
                h.currentHp = Mathf.Max(1, Mathf.RoundToInt(h.maxHp * (1f - hpPerdidoFrac)));
                h.stress = estresse;
            }
        }

        public void Dispose()
        {
            Object.DestroyImmediate(deck);

            foreach (var h in heroes)
            {
                DeckData guardado = DeckRepository.GetDeck(h);
                DeckRepository.Remove(h);
                if (guardado != null) Object.DestroyImmediate(guardado);
                Object.DestroyImmediate(h);
            }
        }
    }

    /// <summary>
    /// Mede o combate em dois estados de party, porque no jogo ele quase nunca
    /// começa com o grupo inteiro: a estrada cobra antes, e o relatório de Play
    /// Mode mostra mais HP perdido fora de combate do que dentro dele.
    /// </summary>
    static void SimulateCombats(int runs)
    {
        if (Resources.LoadAll<CardData>("Cards").Length == 0)
        {
            Info("sem cartas para simular");
            return;
        }

        var grupos = new List<SimParty>();
        for (int i = 0; i < 25; i++) grupos.Add(SimParty.Create());

        SimulateCombatScenario(runs, grupos, "descansada", 0f, 0f);
        var desgastada = SimulateCombatScenario(runs, grupos, "desgastada (metade do HP, 50 de estresse)", 0.5f, 50f);

        foreach (var g in grupos) g.Dispose();

        // A party desgastada é o caso representativo: é o estado em que a jornada
        // entrega o grupo ao combate. É sobre ela que a expectativa vale.
        //
        // O KPI é MORTES POR COMBATE, não taxa de vitória (decisão do autor,
        // 15/08). Taxa de vitória veio do Slay the Spire, onde perder a luta
        // encerra a run; aqui a party só perde quando os quatro caem e a Beira
        // da Morte segura cada um por um golpe, então derrota total é rara por
        // construção — o alvo de 35–75% media algo que este jogo não cobra.
        // O que o combate custa de fato é gente, e é isso que passa a ser aferido.
        //
        // A taxa de vitória continua no relatório como informação: serve para ver
        // se o combate virou formalidade, mas não reprova a régua.
        //
        // As faixas são largas de propósito. Com 200 amostras por célula o valor
        // oscila entre execuções, e um alvo justo demais acusaria sorteio como
        // se fosse regressão.
        //
        // Só teto, dos dois lados. Este cenário é sintético e mais brando que a
        // jornada real — aqui o chefe cobra 0,02 a 0,06 mortes, enquanto na
        // jornada, com o grupo chegando desgastado de verdade, cobra ~0,27. Uma
        // amostra de 200 lutas com 4 a 12 mortes não sustenta um piso: ele
        // reprovaria por sorteio. O piso do chefe fica em SimulateJourneys, onde
        // o sinal é forte; o que se afere aqui é que o combate não dizima.
        //
        // Com 1 chefe por jornada, 0,40 já estouraria sozinho o alvo de
        // 0,33–0,67 mortes da jornada inteira.
        Expect(desgastada.mortesChefes <= 0.40f,
               $"chefe cobra em gente sem dizimar (teto 0,40 mortes/combate): {desgastada.mortesChefes:F2}");

        // Encontro comum leva só teto. Com ~1,6 deles por jornada, 0,15 cada já
        // somaria 0,24 mortes/jornada e, com o chefe por cima, estouraria o
        // orçamento de letalidade. Um piso aqui seria pedir que o caminho até o
        // chefe matasse sozinho — o desgaste que ele cobra é HP, não vida.
        Expect(desgastada.mortesNormais <= 0.15f,
               $"encontro comum desgasta sem matar (teto 0,15 mortes/combate): {desgastada.mortesNormais:F2}");
    }

    /// <summary>Roda encontros normais e chefes para um estado de party e relata.</summary>
    static (float normais, float chefes, float mortesNormais, float mortesChefes) SimulateCombatScenario(
        int runs, List<SimParty> grupos, string rotulo, float hpPerdidoFrac, float estresseInicial)
    {
        var taxas = new float[2];
        var mortesPorCombate = new float[2];

        for (int modo = 0; modo < 2; modo++)
        {
            bool chefe = modo == 1;
            int vitorias = 0, mortesTotais = 0;
            var turnos = new List<int>();

            for (int r = 0; r < runs; r++)
            {
                SimParty grupo = grupos[r % grupos.Count];
                grupo.Reset(hpPerdidoFrac, estresseInicial);

                var lineup = EnemyPool.GetLineup(BiomeType.Forest, chefe, 5);

                CombatOutcome resultado = SimulateOneCombat(
                    grupo.heroes, grupo.ownership, grupo.deck, lineup);

                if (resultado.vitoria) { vitorias++; turnos.Add(resultado.turnos); }
                mortesTotais += resultado.mortos;
            }

            taxas[modo] = vitorias / (float)runs;
            mortesPorCombate[modo] = mortesTotais / (float)runs;

            Info($"{(chefe ? "chefes" : "encontros normais")}, party {rotulo}: "
                 + $"{mortesPorCombate[modo]:F2} mortes por combate"
                 + $" | {vitorias}/{runs} vitórias ({taxas[modo]:P0})"
                 + (turnos.Count > 0 ? $", {turnos.Average():F1} turnos" : ""));
        }

        return (taxas[0], taxas[1], mortesPorCombate[0], mortesPorCombate[1]);
    }

    /// <summary>
    /// Reproduz um combate com as regras do CombatManager: mão de cinco cartas
    /// compradas do baralho da jornada, energia por turno, bloqueio dos dois
    /// lados, as quatro intenções do inimigo, formação e estresse.
    ///
    /// A versão anterior desta simulação escolhia livremente a melhor carta de
    /// dano do baralho inteiro, ignorava bloqueio, estresse e intenções, e
    /// mandava a party sempre descansada. Dava 100% de vitória em tudo — media
    /// um combate que o jogo não tem.
    /// </summary>
    static CombatOutcome SimulateOneCombat(List<HeroData> party, CardOwnership ownership,
                                           DeckData deck, List<EnemyData> lineup)
    {
        // Lidos da cena, não copiados: uma constante aqui divergiria do jogo no
        // primeiro ajuste de balanceamento feito no Inspector, e o simulador
        // passaria a medir um combate que ninguém joga.
        int baseEnergy = EnergiaDeCombate;
        int cardsPerTurn = CartasPorTurno;
        int maxHandSize = cardsPerTurn + 3;
        const int maxTurns = 30;

        var enemies = lineup.Select(SimEnemy.From).ToList();
        var heroBlock = party.ToDictionary(h => h, h => 0);
        var buffs = new SimBuffs();

        var drawPile = new List<CardData>(deck.cards.Where(c => c != null));
        var hand = new List<CardData>();
        var discard = new List<CardData>();
        Shuffle(drawPile);

        // Compra fiel ao CardManager: recicla o descarte quando o baralho acaba
        // e respeita o teto da mão.
        System.Action comprar = () =>
        {
            if (hand.Count >= maxHandSize) return;

            if (drawPile.Count == 0)
            {
                if (discard.Count == 0) return;
                drawPile.AddRange(discard);
                discard.Clear();
                Shuffle(drawPile);
            }

            hand.Add(drawPile[0]);
            drawPile.RemoveAt(0);
        };

        for (int i = 0; i < cardsPerTurn; i++) comprar();
        RollIntents(enemies);

        int turno = 0;
        bool vitoria = false;

        while (turno < maxTurns)
        {
            turno++;

            // ── Turno do jogador ──
            foreach (var h in party) heroBlock[h] = 0;   // bloqueio não acumula

            // O broquel entra no primeiro turno, como no CombatManager.
            if (turno == 1)
                foreach (var h in party)
                    heroBlock[h] += ItemCatalog.Total(h, RelicEffect.BloqueioInicial);

            BeberPocoes(party, heroBlock, buffs);

            int aComprar = Mathf.Max(0, cardsPerTurn - hand.Count);
            for (int i = 0; i < aComprar; i++) comprar();

            int energia = baseEnergy;

            while (true)
            {
                CardData escolha = EscolherCarta(hand, energia, party, enemies, ownership, buffs);
                if (escolha == null) break;

                energia -= escolha.energyCost;
                hand.Remove(escolha);
                discard.Add(escolha);

                energia += JogarCarta(escolha, party, enemies, ownership, heroBlock, buffs, comprar);

                if (enemies.All(e => !e.IsAlive)) break;
            }

            if (enemies.All(e => !e.IsAlive)) { vitoria = true; break; }

            // O prazo do bônus corre ao fechar o turno, como no CombatManager.
            if (buffs.groupDamageBonusTurns > 0)
            {
                buffs.groupDamageBonusTurns--;
                if (buffs.groupDamageBonusTurns == 0) buffs.groupDamageBonus = 0;
            }

            // ── Fase dos inimigos ──
            var resolution = new EventResolver.Resolution();

            foreach (var enemy in enemies.Where(e => e.IsAlive).ToList())
            {
                if (party.All(h => !h.IsAlive)) break;
                ExecuteIntent(enemy, party, heroBlock, buffs, resolution);
            }

            if (party.All(h => !h.IsAlive)) break;

            // Veneno cobra no fim da rodada e a pilha se desgasta; o
            // enfraquecimento envelhece — mesma ordem do CombatManager.
            foreach (var e in enemies.Where(x => x.IsAlive && x.veneno > 0).ToList())
            {
                e.hp -= e.veneno;
                e.veneno = Mathf.Max(0, e.veneno - 1);
            }

            if (enemies.All(e => !e.IsAlive)) { vitoria = true; break; }

            foreach (var e in enemies)
            {
                e.block = 0;

                if (e.turnosEnfraquecido > 0)
                {
                    e.turnosEnfraquecido--;
                    if (e.turnosEnfraquecido == 0) e.reducaoDeDano = 0;
                }
            }

            RollIntents(enemies);
        }

        // O unguento fecha o combate vencido, como no CombatManager: cura pouca,
        // e só depois da luta — no meio dela seria uma segunda barra de vida.
        if (vitoria)
        {
            foreach (var hero in party.Where(h => h != null && h.IsAlive))
            {
                int unguento = ItemCatalog.Total(hero, RelicEffect.CuraPosCombate);
                if (unguento <= 0) continue;

                hero.currentHp = Mathf.Min(hero.maxHp, hero.currentHp + unguento);
            }
        }

        return new CombatOutcome
        {
            vitoria = vitoria,
            turnos = turno,
            mortos = party.Count(h => !h.IsAlive)
        };
    }

    /// <summary>
    /// Política de jogo do simulador: um jogador competente, não perfeito.
    /// Socorre quem caiu, se protege quando o golpe anunciado é grande e, no
    /// resto, bate no inimigo mais perto de cair. Cartas que não servem ao turno
    /// ficam entulhando a mão — é o que acontece na partida de verdade.
    /// </summary>
    static CardData EscolherCarta(List<CardData> hand, int energia, List<HeroData> party,
                                  List<SimEnemy> enemies, CardOwnership ownership, SimBuffs buffs)
    {
        var jogaveis = hand.Where(c => c != null && c.energyCost <= energia).ToList();
        if (jogaveis.Count == 0) return null;

        float Poder(CardData c) => ownership != null ? ownership.PowerMultiplier(c, party) : 1f;

        // 1. Alguém na Beira da Morte: curar vale mais que qualquer dano.
        if (party.Any(h => h.IsAlive && h.isOnDeathsDoor))
        {
            var cura = jogaveis.Where(CuraAlguem)
                               .OrderByDescending(c => c.combatHeal * Poder(c))
                               .FirstOrDefault();
            if (cura != null) return cura;
        }

        // 2. Aflição em campo: ela agrava o estresse e o dano recebidos.
        if (party.Any(h => h.IsAlive && MentalStateUtil.IsAffliction(h.mentalState)))
        {
            var limpeza = jogaveis.FirstOrDefault(c => c.combatEffect == CombatEffectType.Cleanse);
            if (limpeza != null) return limpeza;
        }

        // 3. Golpe grande anunciado. O jogador vê a intenção, então o simulador
        //    também vê. O corte é da ordem de um golpe de chefe.
        int danoAnunciado = enemies.Where(e => e.IsAlive).Sum(e =>
              e.intent == EnemyIntent.Attack ? e.DanoAtual
            : e.intent == EnemyIntent.AttackAll
                ? Mathf.RoundToInt(e.DanoAtual * 0.6f) * party.Count(h => h.IsAlive)
            : 0);

        // O corte era fixo em 12, e isso escondia um absurdo: o chefe base bate 11,
        // então o simulador NUNCA se defendia dele — mas contra um chefe 30% mais
        // forte (17 de dano) se defendia sempre. Resultado: quanto mais forte o
        // chefe, mais o jogador simulado vencia. Um degrau na heurística do
        // jogador virava uma conclusão invertida sobre o jogo.
        //
        // O critério agora é relativo a quem está mais perto de cair, que é a
        // pergunta que o jogador de verdade se faz ao ler a intenção.
        int menorHpVivo = party.Where(h => h.IsAlive)
                               .Select(h => h.currentHp)
                               .DefaultIfEmpty(1)
                               .Min();

        if (danoAnunciado >= Mathf.Max(6, menorHpVivo / 2))
        {
            var guarda = jogaveis.Where(Bloqueia)
                                 .OrderByDescending(c => c.combatBlock * Poder(c))
                                 .FirstOrDefault();
            if (guarda != null) return guarda;

            var esquiva = jogaveis.FirstOrDefault(c => c.combatEffect == CombatEffectType.Evade);
            if (esquiva != null) return esquiva;
        }

        int menorCustoDeAtaque = jogaveis.Where(CausaDano)
                                         .Select(c => c.energyCost)
                                         .DefaultIfEmpty(int.MaxValue)
                                         .Min();

        // 4. Ímpeto do grupo, enquanto ainda sobra energia para bater com ele.
        if (buffs.groupDamageBonusTurns == 0 && enemies.Count(e => e.IsAlive) > 0)
        {
            var impeto = jogaveis.FirstOrDefault(c => c.combatEffect == CombatEffectType.Buff
                                                   && energia - c.energyCost >= menorCustoDeAtaque);
            if (impeto != null) return impeto;
        }

        // 5. Mirar antes de disparar, só se o disparo ainda couber no turno.
        if (buffs.nextCardMultiplier <= 1f)
        {
            var mira = jogaveis.FirstOrDefault(c => c.combatEffect == CombatEffectType.BuffNextCard
                                                 && energia - c.energyCost >= menorCustoDeAtaque);
            if (mira != null) return mira;
        }

        // 6. Dano.
        var ataque = jogaveis.Where(CausaDano)
                             .OrderByDescending(c => c.combatDamage * Poder(c))
                             .FirstOrDefault();
        if (ataque != null) return ataque;

        // 7. Utilitário que gira o baralho em vez de passar o turno em branco.
        var util = jogaveis.FirstOrDefault(c => c.combatEffect == CombatEffectType.DrawCards
                                             || c.combatEffect == CombatEffectType.GainEnergy);
        if (util != null) return util;

        // 8. Cura sobrando, só se houver ferimento a tratar.
        return jogaveis.Where(CuraAlguem)
                       .FirstOrDefault(c => party.Any(h => h.IsAlive && h.currentHp < h.maxHp));
    }

    // O efeito manda, não o número: a Fúria carrega combatDamage mas não fere
    // ninguém sozinha, e tratá-la como ataque desperdiçava o turno do simulador.
    // A regra vem do CombatManager para as duas não divergirem com o tempo.
    static bool CausaDano(CardData c) => CombatManager.DealsDamage(c.combatEffect);

    static bool CuraAlguem(CardData c) =>
           c.combatEffect == CombatEffectType.Heal
        || c.combatEffect == CombatEffectType.HealAll;

    static bool Bloqueia(CardData c) =>
           c.combatEffect == CombatEffectType.Block
        || c.combatEffect == CombatEffectType.BlockAll;

    /// <summary>Aplica a carta com as mesmas contas do CombatManager. Devolve a energia ganha.</summary>
    static int JogarCarta(CardData card, List<HeroData> party, List<SimEnemy> enemies,
                          CardOwnership ownership, Dictionary<HeroData, int> heroBlock,
                          SimBuffs buffs, System.Action comprar)
    {
        float power = ownership != null ? ownership.PowerMultiplier(card, party) : 1f;

        int rawDamage = PartyFormation.Scale(card.combatDamage, power);
        int block = PartyFormation.Scale(card.combatBlock, power);
        int heal = PartyFormation.Scale(card.combatHeal, power);

        int damage = rawDamage;

        // A arma forjada soma depois da escala, os buffos vêm por cima e o
        // multiplicador fecha a conta — mesma ordem do CombatManager.
        if (damage > 0 && CausaDano(card))
        {
            HeroData dono = ownership?.BestOwner(card, party);
            damage += ForgeManager.WeaponBonus(dono);

            // A relíquia de dano entra junto da arma, como no CombatManager. Sem
            // esta linha o simulador media um jogo em que relíquia não faz nada:
            // a auditoria de jogabilidade acusou "−0,01 mortes" para duas
            // relíquias por herói, e o zero era do instrumento.
            damage += ItemCatalog.Total(dono, RelicEffect.DanoDeCarta);

            if (buffs.groupDamageBonusTurns > 0)
                damage += buffs.groupDamageBonus;

            if (buffs.nextCardMultiplier > 1f)
            {
                damage = Mathf.RoundToInt(damage * buffs.nextCardMultiplier);
                buffs.nextCardMultiplier = 1f;
            }
        }

        // O jogador foca em fechar o inimigo mais perto de cair.
        SimEnemy alvo = enemies.Where(e => e.IsAlive).OrderBy(e => e.hp).FirstOrDefault();
        HeroData ferido = party.Where(h => h.IsAlive)
                               .OrderBy(h => h.isOnDeathsDoor ? 0 : 1)
                               .ThenBy(h => h.currentHp)
                               .FirstOrDefault();

        switch (card.combatEffect)
        {
            case CombatEffectType.Damage:
                DamageEnemy(alvo, Mathf.Max(1, damage));
                break;

            // Veneno e enfraquecimento deixaram de ser dano com outro nome no
            // CombatManager; se continuassem sendo aqui, o simulador voltaria a
            // medir um combate que o jogo não tem.
            case CombatEffectType.Poison:
                if (alvo != null) alvo.veneno += Mathf.Max(1, damage);
                break;

            case CombatEffectType.Debuff:
                if (alvo != null)
                {
                    alvo.reducaoDeDano = Mathf.Max(alvo.reducaoDeDano,
                        Mathf.Max(1, Mathf.RoundToInt(alvo.data.attackDamage * 0.4f)));
                    alvo.turnosEnfraquecido = Mathf.Max(alvo.turnosEnfraquecido,
                        Mathf.Max(2, card.combatDuration));

                    if (damage > 0) DamageEnemy(alvo, damage);
                }
                break;

            case CombatEffectType.ShieldBreak:
                if (alvo != null) { alvo.block = 0; DamageEnemy(alvo, damage); }
                break;

            case CombatEffectType.DamageAll:
                foreach (var e in enemies.Where(e => e.IsAlive).ToList())
                    DamageEnemy(e, damage);
                break;

            case CombatEffectType.Block:
                if (ferido != null) heroBlock[ferido] += block;
                break;

            case CombatEffectType.BlockAll:
                foreach (var h in party.Where(h => h.IsAlive))
                    heroBlock[h] += block;
                break;

            case CombatEffectType.Heal:
                HealHero(ferido, heal);
                break;

            case CombatEffectType.HealAll:
                foreach (var h in party.Where(h => h.IsAlive).ToList())
                    HealHero(h, heal);
                break;

            case CombatEffectType.DrawCards:
                for (int i = 0; i < Mathf.Max(1, card.combatDuration); i++) comprar();
                break;

            case CombatEffectType.GainEnergy:
                return Mathf.Max(1, card.combatDuration);

            case CombatEffectType.Buff:
                buffs.groupDamageBonus = Mathf.Max(buffs.groupDamageBonus, Mathf.Max(1, rawDamage));
                buffs.groupDamageBonusTurns = Mathf.Max(buffs.groupDamageBonusTurns,
                                                        Mathf.Max(1, card.combatDuration));
                break;

            case CombatEffectType.BuffNextCard:
                buffs.nextCardMultiplier = 1.5f;
                break;

            case CombatEffectType.Evade:
            {
                // Protege quem está mais exposto: a linha de frente atrai os golpes.
                HeroData exposto = PartyFormation.LivingOrder(party).FirstOrDefault() ?? ferido;
                if (exposto != null) buffs.evading.Add(exposto);
                break;
            }

            case CombatEffectType.Cleanse:
            {
                HeroData aflito = party.FirstOrDefault(h => h.IsAlive
                                                         && MentalStateUtil.IsAffliction(h.mentalState))
                                  ?? party.Where(h => h.IsAlive)
                                          .OrderByDescending(h => h.stress)
                                          .FirstOrDefault();
                if (aflito != null)
                {
                    if (MentalStateUtil.IsAffliction(aflito.mentalState))
                        aflito.mentalState = MentalState.Normal;

                    aflito.stress = Mathf.Max(0f, aflito.stress - CombatManager.CleanseStressRelief);
                }
                break;
            }
        }

        return 0;
    }

    static void ExecuteIntent(SimEnemy enemy, List<HeroData> party,
                              Dictionary<HeroData, int> heroBlock, SimBuffs buffs,
                              EventResolver.Resolution resolution)
    {
        switch (enemy.intent)
        {
            case EnemyIntent.Attack:
            {
                HeroData alvo = PartyFormation.PickTarget(party);
                DamageHero(alvo, enemy.DanoAtual, party, heroBlock, buffs, resolution);
                Espinhos(alvo, enemy);
                break;
            }

            case EnemyIntent.AttackAll:
            {
                int dmg = Mathf.Max(1, Mathf.RoundToInt(enemy.DanoAtual * 0.6f));
                foreach (var hero in party.Where(h => h.IsAlive).ToList())
                {
                    DamageHero(hero, dmg, party, heroBlock, buffs, resolution);
                    Espinhos(hero, enemy);
                }
                break;
            }

            case EnemyIntent.Defend:
                enemy.block += enemy.bloqueio;
                break;

            case EnemyIntent.Stress:
                EventResolver.AddStress(PartyFormation.PickTarget(party),
                                        enemy.estresse, resolution);
                break;
        }
    }

    /// <summary>
    /// O frasco é uma ação do jogador, e o simulador precisa de uma política —
    /// sem ela, a auditoria acusava "poção: −0,03 mortes", que era o instrumento
    /// não bebendo nada, e não a poção sendo fraca.
    ///
    /// A política é a mesma do resto do simulador: um jogador competente, não
    /// perfeito. Cura quando o dono está abaixo de 40% da vida — é quando o
    /// próximo golpe leva à Beira da Morte —, calma quando o estresse passa de 70,
    /// e as duas de combate no primeiro turno, que é quando ainda há turnos para
    /// aproveitá-las.
    /// </summary>
    static void BeberPocoes(List<HeroData> party, Dictionary<HeroData, int> heroBlock, SimBuffs buffs)
    {
        foreach (HeroData hero in party.Where(h => h.IsAlive && h.potions != null && h.potions.Count > 0).ToList())
        {
            foreach (string id in hero.potions.ToList())
            {
                PotionDef def = ItemCatalog.Pocao(id);
                if (def == null) continue;

                bool bebe;

                switch (def.efeito)
                {
                    case PotionEffect.Cura:
                        bebe = hero.currentHp < hero.maxHp * 0.4f;
                        break;

                    case PotionEffect.Calma:
                        bebe = hero.stress >= 70f;
                        break;

                    default:
                        bebe = true;   // as de combate valem mais cedo do que tarde
                        break;
                }

                if (!bebe) continue;

                hero.potions.Remove(id);

                switch (def.efeito)
                {
                    case PotionEffect.Cura:
                        hero.currentHp = Mathf.Min(hero.maxHp, hero.currentHp + def.valor);
                        break;

                    case PotionEffect.Bloqueio:
                        heroBlock[hero] = (heroBlock.TryGetValue(hero, out int b) ? b : 0) + def.valor;
                        break;

                    case PotionEffect.ForcaNaProximaCarta:
                        buffs.nextCardMultiplier = 1f + def.valor / 100f;
                        break;

                    case PotionEffect.Calma:
                        hero.stress = Mathf.Max(0f, hero.stress - def.valor);
                        break;
                }
            }
        }
    }

    /// <summary>
    /// A armadura de espinhos devolve o golpe a quem encostou. Só a quem encostou:
    /// veneno e dano de evento não têm agressor, e devolver golpe ao nada mataria
    /// inimigos sozinhos — a mesma guarda do <see cref="CombatManager"/>.
    /// </summary>
    static void Espinhos(HeroData hero, SimEnemy agressor)
    {
        if (hero == null || agressor == null || !agressor.IsAlive) return;

        int espinhos = ItemCatalog.Total(hero, RelicEffect.Retaliacao);
        if (espinhos > 0) agressor.hp -= espinhos;
    }

    static void DamageHero(HeroData hero, int amount, List<HeroData> party,
                           Dictionary<HeroData, int> heroBlock, SimBuffs buffs,
                           EventResolver.Resolution resolution)
    {
        if (hero == null || !hero.IsAlive || amount <= 0) return;

        // Quem se preparou para desviar sai limpo deste golpe — e só deste.
        if (buffs.evading.Remove(hero)) return;

        amount = PartyFormation.Scale(amount, PartyFormation.DamageTakenMultiplier(hero, party));

        int block = heroBlock.TryGetValue(hero, out int b) ? b : 0;
        int blocked = Mathf.Min(block, amount);
        heroBlock[hero] = block - blocked;

        int remaining = amount - blocked;
        if (remaining > 0)
            EventResolver.DealDamage(hero, remaining, party, resolution);
    }

    static void DamageEnemy(SimEnemy enemy, int amount)
    {
        if (enemy == null || !enemy.IsAlive || amount <= 0) return;

        int blocked = Mathf.Min(enemy.block, amount);
        enemy.block -= blocked;
        enemy.hp = Mathf.Max(0, enemy.hp - (amount - blocked));
    }

    static void HealHero(HeroData hero, int amount)
    {
        if (hero == null || !hero.IsAlive || amount <= 0) return;

        int healed = Mathf.Min(amount, hero.maxHp - hero.currentHp);
        hero.currentHp += healed;

        if (hero.isOnDeathsDoor && hero.currentHp > 0)
            hero.isOnDeathsDoor = false;
    }

    static void RollIntents(List<SimEnemy> enemies)
    {
        foreach (var enemy in enemies.Where(e => e.IsAlive))
            enemy.intent = RollIntent(enemy.data);
    }

    /// <summary>Cópia fiel do sorteio de intenção do CombatManager.</summary>
    static EnemyIntent RollIntent(EnemyData data)
    {
        int total = data.attackWeight + data.defendWeight + data.stressWeight + data.attackAllWeight;
        if (total <= 0) return EnemyIntent.Attack;

        int roll = Random.Range(0, total);

        if (roll < data.attackWeight) return EnemyIntent.Attack;
        roll -= data.attackWeight;

        if (roll < data.defendWeight) return EnemyIntent.Defend;
        roll -= data.defendWeight;

        if (roll < data.stressWeight) return EnemyIntent.Stress;

        return EnemyIntent.AttackAll;
    }

    static void Shuffle(List<CardData> pile)
    {
        for (int i = 0; i < pile.Count; i++)
        {
            CardData temp = pile[i];
            int j = Random.Range(i, pile.Count);
            pile[i] = pile[j];
            pile[j] = temp;
        }
    }

    #endregion
}

/// <summary>
/// Dispara o smoke test criando RunSmokeTest.trigger na raiz do projeto, do
/// mesmo jeito que o Play Mode — assim o balanceamento pode ser medido sem
/// abrir o menu do Editor. O arquivo é apagado assim que detectado.
/// </summary>
[InitializeOnLoad]
public static class SmokeTestTriggerWatcher
{
    const string TriggerFile = "RunSmokeTest.trigger";
    static double nextCheck;

    static SmokeTestTriggerWatcher()
    {
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if (EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + 1.0;

        // Rodar a simulação durante o Play Mode ou uma recompilação atropelaria
        // o que estiver em curso — a mesma guarda do gatilho do Play Mode.
        if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

        string path = System.IO.Path.Combine(PlayModeTestLauncher.ProjectRoot, TriggerFile);
        if (!System.IO.File.Exists(path)) return;

        try { System.IO.File.Delete(path); }
        catch { return; }

        Debug.Log("Trigger detectado — rodando o smoke test.");
        GuildSmokeTest.Run();
    }
}
#endif
