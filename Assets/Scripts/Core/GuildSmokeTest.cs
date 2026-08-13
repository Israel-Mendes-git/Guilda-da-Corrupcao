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

        Header("BIOMAS");
        TestBiomeMatching();

        Header("INIMIGOS");
        TestEnemyPool();

        Header("DECKS");
        TestDecks();

        Header("REGRAS DE DANO E ESTRESSE");
        TestCombatRules();

        Header("SIMULACAO DE JORNADAS");
        SimulateJourneys(200);

        Header("SIMULACAO DE COMBATE");
        SimulateCombats(200);

        string resumo = failures == 0
            ? $"✅ SMOKE TEST OK — {checks} verificações, 0 falhas"
            : $"❌ SMOKE TEST COM {failures} FALHA(S) de {checks} verificações";

        if (warnings > 0)
            resumo += $" | {warnings} aviso(s) de balanceamento";

        report.Insert(0, resumo + "\n\n");

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

    static void SimulateJourneys(int runs)
    {
        int totalHerois = 0, sobreviventes = 0, mortos = 0;
        int totalAflicoes = 0;
        var duracoes = new List<int>();

        for (int r = 0; r < runs; r++)
        {
            QuestData quest = QuestGenerator.GenerateQuests(1, 3)[0];

            var party = new List<HeroData>
            {
                HeroFactory.CreateHero("A", HeroClass.Warrior, 3),
                HeroFactory.CreateHero("B", HeroClass.Mage, 2),
                HeroFactory.CreateHero("C", HeroClass.Healer, 2),
                HeroFactory.CreateHero("D", HeroClass.Hunter, 1)
            };
            totalHerois += party.Count;

            int dias = quest.GetActualDuration();
            duracoes.Add(dias);

            // As provisões que a tela de preparação entrega. Antes a simulação
            // usava o padrão interno do StartJourney (10–14 rações), folga que o
            // jogador não tem — e media uma jornada mais fácil que a real.
            var prep = Object.FindObjectOfType<QuestSelectionUI>(true);
            int racoes = prep != null ? prep.baseRations : 10;
            int tochas = prep != null ? prep.baseTorches : 8;

            EventPool.ResetHistory();

            for (int dia = 1; dia <= dias + 1 && party.Any(h => h.IsAlive); dia++)
            {
                bool ehChefe = dia == dias + 1;
                EventData ev = ehChefe
                    ? EventPool.GetFinalEvent(quest.biomeType)
                    : EventPool.GetRandomEvent(quest.biomeType, quest.corruptionLevel, dia);

                if (ev?.outcomes == null || ev.outcomes.Length == 0) continue;

                // Jogador neutro: escolhe ao acaso, sem mitigação por cartas.
                EventOutcome escolha = ev.outcomes[Random.Range(0, ev.outcomes.Length)];
                EventResolver.Resolve(escolha, party, 0f);

                // Manutenção diária, igual à do JourneyManager.
                int diasGastos = 1 + escolha.extraDays;
                for (int d = 0; d < diasGastos; d++)
                {
                    racoes--;
                    if (racoes <= 0)
                    {
                        racoes = 0;
                        foreach (var h in party.Where(x => x.IsAlive).ToList())
                            EventResolver.DealDamage(h, 5, party, new EventResolver.Resolution());
                    }

                    tochas--;
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
                Object.DestroyImmediate(h);
            }

            Object.DestroyImmediate(quest);
        }

        float taxa = sobreviventes / (float)totalHerois;
        float mortesPorJornada = mortos / (float)runs;

        Info($"{runs} jornadas, {totalHerois} heróis: {sobreviventes} vivos, {mortos} mortos");
        Info($"duração média: {duracoes.Average():F1} dias");
        Info($"heróis que sucumbiram ao estresse: {totalAflicoes} ({totalAflicoes / (float)totalHerois:P0})");
        Info($"mortes por jornada: {mortesPorJornada:F2} (alvo {MortesPorJornadaMin:F2}–{MortesPorJornadaMax:F2})");

        // O KPI é a letalidade, não a sobrevivência: "punitivo" tem piso e teto.
        // Ficar abaixo do piso é tão fora do alvo quanto passar do teto.
        Expect(mortesPorJornada >= MortesPorJornadaMin && mortesPorJornada <= MortesPorJornadaMax,
            $"letalidade {mortesPorJornada:F2} mortes/jornada dentro do alvo punitivo "
            + $"({MortesPorJornadaMin:F2}–{MortesPorJornadaMax:F2}) com jogador aleatório");

        Info($"taxa de sobrevivência: {taxa:P1}");
        Check(mortos > 0, $"heróis realmente podem morrer ({mortos} mortes)");
    }

    /// <summary>Um inimigo dentro da simulação. Espelha o EnemyInstance do CombatManager.</summary>
    class SimEnemy
    {
        public EnemyData data;
        public int hp;
        public int block;
        public EnemyIntent intent;
        public bool IsAlive => hp > 0;
    }

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
        // Piso E teto: num deckbuilder o combate é o desafio, não formalidade.
        // Ganhar sempre falha o alvo tanto quanto perder sempre.
        //
        // As faixas são largas de propósito. Com 200 amostras por célula, os
        // encontros normais oscilam uns 3 pontos entre execuções; um teto justo
        // demais acusaria variação de sorteio como se fosse regressão.
        Expect(desgastada.normais >= 0.60f && desgastada.normais <= 0.95f,
               $"encontros normais desafiam sem massacrar (alvo 60–95%): {desgastada.normais:P0}");
        Expect(desgastada.chefes >= 0.35f && desgastada.chefes <= 0.75f,
               $"chefes são ameaça real (alvo 35–75%): {desgastada.chefes:P0}");
    }

    /// <summary>Roda encontros normais e chefes para um estado de party e relata.</summary>
    static (float normais, float chefes) SimulateCombatScenario(
        int runs, List<SimParty> grupos, string rotulo, float hpPerdidoFrac, float estresseInicial)
    {
        var taxas = new float[2];

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

            Info($"{(chefe ? "chefes" : "encontros normais")}, party {rotulo}: "
                 + $"{vitorias}/{runs} vitórias ({taxas[modo]:P0})"
                 + (turnos.Count > 0 ? $", {turnos.Average():F1} turnos" : "")
                 + $", {mortesTotais / (float)runs:F2} mortes por combate");
        }

        return (taxas[0], taxas[1]);
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
        const int baseEnergy = 3;
        const int cardsPerTurn = 5;
        const int maxHandSize = cardsPerTurn + 3;
        const int maxTurns = 30;

        var enemies = lineup.Select(e => new SimEnemy { data = e, hp = e.maxHp }).ToList();
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

            foreach (var e in enemies) e.block = 0;
            RollIntents(enemies);
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
              e.intent == EnemyIntent.Attack ? e.data.attackDamage
            : e.intent == EnemyIntent.AttackAll
                ? Mathf.RoundToInt(e.data.attackDamage * 0.6f) * party.Count(h => h.IsAlive)
            : 0);

        if (danoAnunciado >= 12)
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
            damage += ForgeManager.WeaponBonus(ownership?.BestOwner(card, party));

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
            case CombatEffectType.Poison:
            case CombatEffectType.Debuff:
                DamageEnemy(alvo, Mathf.Max(1, damage));
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
                DamageHero(PartyFormation.PickTarget(party), enemy.data.attackDamage,
                           party, heroBlock, buffs, resolution);
                break;

            case EnemyIntent.AttackAll:
            {
                int dmg = Mathf.Max(1, Mathf.RoundToInt(enemy.data.attackDamage * 0.6f));
                foreach (var hero in party.Where(h => h.IsAlive).ToList())
                    DamageHero(hero, dmg, party, heroBlock, buffs, resolution);
                break;
            }

            case EnemyIntent.Defend:
                enemy.block += enemy.data.blockAmount;
                break;

            case EnemyIntent.Stress:
                EventResolver.AddStress(PartyFormation.PickTarget(party),
                                        enemy.data.stressDamage, resolution);
                break;
        }
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
#endif
