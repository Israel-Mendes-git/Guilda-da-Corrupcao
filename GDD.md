# Game Design Document — *Guilda da Corrupção* (título provisório)

> **Documento de Design de Jogo (GDD)**
> Projeto Unity `My project (1)` · Unity 2022.3.62f3 · URP · Idioma: pt-BR
> Versão do documento: 1.0 — 19/06/2026
> Status do projeto: **protótipo em desenvolvimento**
>
> Legenda de status usada neste documento:
> - **[IMPLEMENTADO]** — já existe e funciona no código atual.
> - **[PARCIAL]** — existe parcialmente / definido mas não totalmente conectado.
> - **[PLANEJADO]** — decisão de design tomada, ainda não construída.

---

## 1. Visão Geral

### 1.1 High Concept
Você é o **mestre de uma guilda de aventureiros** num mundo medieval sombrio sendo devorado por uma **Corrupção** que se alastra pelos biomas. Recrute heróis, monte suas baralhos (decks), envie-os em **jornadas** perigosas e **combates táticos** com cartas, e tente conter a maré da Corrupção — sabendo que cada herói pode morrer **permanentemente** e que, mais cedo ou mais tarde, sua guilda vai cair. Quando cair, você recomeça mais forte.

### 1.2 Pitch de elevador
> *"Um roguelike de gerência de guilda onde você não controla o herói — você controla o destino. Recrute, equipe baralhos, escolha quem arrisca a vida em cada jornada e resolva eventos por escolhas narrativas e batalhas de cartas. A Corrupção sempre vence no fim; a questão é quão longe você chega antes disso."*

### 1.3 Gênero
Híbrido de três pilares:
- **Gestão de guilda** (estilo *Darkest Dungeon* / *Wildermyth* no hub).
- **Roguelike** com permadeath e meta-progressão entre runs.
- **Deckbuilder** (estilo *Slay the Spire*) para jornadas e combate.

### 1.4 Plataforma e tecnologia
- **Engine:** Unity 2022.3.62f3, Universal Render Pipeline (URP).
- **Plataforma-alvo:** PC (primário). UI orientada a tela única / mobile-friendly (uso intenso de emojis como ícones placeholder hoje).
- **Apresentação:** 2D, baseado em UI (Canvas/TextMeshPro). Sem mundo 3D navegável.

### 1.5 Pilares de design
1. **Decisões com peso permanente** — heróis morrem e não voltam; cada escolha custa.
2. **Preparação > execução** — o jogo é vencido na guilda (recrutamento + deckbuilding), não só na luta.
3. **A Corrupção como relógio** — uma pressão crescente que transforma o mundo e os heróis.
4. **Leitura rápida, profundidade lenta** — turnos simples, sinergias e meta-progressão profundas.

### 1.6 Público-alvo
Jogadores de roguelike/deckbuilder e gestão (fãs de *Slay the Spire*, *Darkest Dungeon*, *Loop Hero*, *Fights in Tight Spaces*). Sessões de 20–60 min por run.

---

## 2. Narrativa e Ambientação

> **Decisão de design:** tema **Dark Fantasy + Corrupção**.

### 2.1 Premissa
O reino já teve guildas gloriosas. Hoje resta a sua — a última fiação de luz numa terra onde a **Corrupção** brota do solo, infecta os biomas e devora quem fica exposto tempo demais. Você não empunha a espada: você decide **quem** empunha, **com qual baralho**, e **até onde** vale a pena ir.

### 2.2 O mundo
Sete biomas, cada um com sua identidade e seus perigos, já presentes na geração de missões:
🌲 Floresta · ⛰️ Montanha · 🏚️ Pântano · 🏜️ Deserto · ❄️ Tundra · 🌋 Vulcão · 🏯 Ruínas.

Cada bioma tem um **nível de corrupção** próprio que define a dificuldade, os eventos disponíveis e as recompensas. Quanto mais corrompido, mais perigoso — e mais lucrativo.

### 2.3 A Corrupção (tema central e mecânica)
- A Corrupção é tanto **narrativa** quanto **numérica** (`corruptionLevel` 0–100 por missão; uma região com ≥50 é considerada "corrompida").
- Heróis acumulam **exposição à corrupção** (`corruptionExposure`) ao longo das jornadas, o que destrava traços negativos, abala a moral e, eventualmente, os perde.
- **[PLANEJADO]** A Corrupção é o "relógio do mundo" da run: avança a cada ciclo, fechando regiões seguras e empurrando o jogador para missões cada vez mais perigosas.

### 2.4 Papel do jogador
**Mestre da Guilda** — administrador, não combatente. Suas ferramentas são: ouro, reputação, contratos (missões), pessoas (heróis) e conhecimento (cartas/biblioteca).

### 2.5 Tom
Sombrio, fatalista, mas com humor seco de taverna. Heróis têm nome, personalidade e defeitos; perdê-los deve doer. A morte é regra, não exceção.

---

## 3. Loop de Jogo

O jogo opera em três loops aninhados:

### 3.1 Macro-loop — a Run (roguelike) **[PLANEJADO]**
```
Nova Run → Guilda inicial → [ciclos de guilda] → Corrupção avança →
Guilda falha (heróis mortos / falência) → Meta-progressão → Nova Run mais forte
```

### 3.2 Meso-loop — o Ciclo da Guilda **[IMPLEMENTADO em grande parte]**
```
Hub da Guilda
   ├─ Taverna: recrutar heróis (gasta ouro)
   ├─ Biblioteca: comprar cartas / melhorar
   ├─ Gerenciador de Deck: montar baralhos dos heróis
   └─ Jornada: selecionar missão → escolher party → escolher deck → partir
        ↓
   Resultado da jornada (ouro, mortes, moral) → volta ao Hub
        ↓
   Novas missões e recrutas disponíveis
```

### 3.3 Micro-loop — a Jornada **[IMPLEMENTADO / PARCIAL]**
```
Para cada Dia da missão:
   Mostra Evento
   → Jogador resolve (escolha narrativa OU carta OU combate)
   → Aplica consequências (HP, ouro, moral, recursos)
   → Consome recursos diários (rações, tochas)
   → Verifica mortes
Dia final = Combate de Chefe
   → Vitória/Derrota → recompensa
```

### 3.4 Diagrama de fluxo de telas **[IMPLEMENTADO]**
```
GuildPanel (hub central)
 ├──> TavernPanel
 ├──> LibraryPanel
 ├──> DeckManagerPanel
 ├──> QuestSelectionPanel (3 passos) ──> JourneyPanel ──> (Resultado) ──> GuildPanel
 └──> [stubs] MarketPanel / ForgePanel / CemeteryPanel / MapRoomPanel
```
Gerenciado por `UIManager` (singleton, `DontDestroyOnLoad`) com animações de fade/scale.

---

## 4. Estrutura de Run (Roguelike) **[PLANEJADO]**

> **Decisão de design:** o jogo é estruturado em **runs com permadeath e meta-progressão**.
> *Nota técnica:* hoje o código mantém estado persistente (`GuildManager`/`QuestManager`/`UIManager` usam `DontDestroyOnLoad`) e funciona como um sandbox infinito. A estrutura de run abaixo é a direção de design a construir por cima dessa base.

### 4.1 Início de run
- Guilda começa com **500 de ouro**, **100 de reputação**, e **4 heróis iniciais**: Gromm (Guerreiro Nv.3), Lyra (Maga Nv.2), Finn (Curandeiro Nv.2), Sera (Caçadora Nv.1). **[IMPLEMENTADO]**
- Roster máximo: **8 heróis**. **[IMPLEMENTADO]**

### 4.2 Progressão e pressão
- A cada ciclo, novas missões aparecem com **corrupção e risco crescentes**.
- **[PLANEJADO]** Um medidor global de Corrupção avança com o tempo; regiões de baixa corrupção "secam", forçando o jogador para o perigo.
- Heróis sobem de nível (decks maiores e mais raros — ver §7.3), mas acumulam exposição/feridas.

### 4.3 Condições de derrota (fim da run)
A run termina quando **[PLANEJADO]**:
- A guilda fica sem heróis vivos **e** sem ouro para recrutar, **ou**
- A reputação chega a 0 (guilda dissolvida), **ou**
- O medidor global de Corrupção atinge o máximo.

### 4.4 Meta-progressão entre runs **[PLANEJADO]**
Moeda persistente (ex.: "Relíquias" ou reputação acumulada) ganha conforme o desempenho da run, gasta entre runs para destravar:
- Novas cartas no pool da Biblioteca.
- Heróis iniciais/classes melhores (incl. **Ladino** e **Bardo**, hoje sem cartas).
- Upgrades permanentes de guilda (locais já esboçados: Forja, Mercado, etc.).
- Modificadores de início de run.

### 4.5 Condição de "vitória" de uma run **[PLANEJADO]**
Derrotar o **Chefe Supremo** (`GenerateBossQuest`: duração 10–15 dias, corrupção 90, risco Alto, recompensa 300 + nível×30) encerra a run com vitória e bônus máximo de meta-progressão.

---

## 5. Sistemas da Guilda (Hub)

### 5.1 Recursos e economia **[IMPLEMENTADO]**
| Recurso | Início | Uso | Fonte |
|---|---|---|---|
| **Ouro** | 500 | Recrutar, comprar cartas, melhorar locais | Recompensas de jornada, eventos |
| **Reputação** | 100 | **[PARCIAL]** — exibida, ainda não consumida | (a definir) |
| **Roster** | 4 heróis (máx. 8) | Compor parties | Taverna |

Gerência centralizada em `GuildManager` (singleton persistente) com eventos `onGoldChanged` / `onRosterChanged`.

### 5.2 Taverna — Recrutamento **[IMPLEMENTADO / PARCIAL]**
- Exibe **3 recrutas** aleatórios (níveis 1–3) gerados por `HeroFactory.CreateRandomHero`.
- Recrutar custa o **salário** do herói (`20 + nível×10`). Ao recrutar, um deck inicial é gerado automaticamente para o herói.
- **Refresh** dos recrutas custa **50 de ouro** — **[PARCIAL]**: o custo está *comentado* no código (hoje grátis).
- Recrutas atualizam após cada jornada.

### 5.3 Biblioteca — Loja de cartas e upgrades **[IMPLEMENTADO]**
- Vende cartas do pool `Resources/Cards`, filtradas pelo **nível da Biblioteca**:
  - Nível 1 → cartas **Comuns**; Nível 2 → **Raras**; Nível 3 → **Épicas**; Nível 4 → **Lendárias**.
- **Preços por raridade:** Comum 100 · Rara 250 · Épica 500 · Lendária 1000 de ouro.
- **Upgrade da Biblioteca:** custa `500 × nível atual`. Bônus por nível:
  - `+5% × nível` de chance de cartas lendárias.
  - Revela `nível` evento(s) futuro(s) na jornada.
  - **[PARCIAL]** Bônus de ouro de jornada (`JourneyManager.AddGoldBonus`) existe mas a Biblioteca ainda não o aplica diretamente.

### 5.4 Gerenciador de Deck **[IMPLEMENTADO]**
- Seleciona um herói → mostra **deck atual** + **coleção** de cartas disponíveis (cartas da classe do herói + cartas de **Bardo** tratadas como **curinga**).
- Adicionar/remover cartas (limite `maxDeckSize = 12`), salvar (via `PlayerPrefs`, por nome de carta) e resetar para o deck padrão.
- Mostra estatísticas: nº de cartas e custo médio de energia.

### 5.5 Locais futuros **[PLANEJADO / stubs no código]**
Botões já existem no `MapManager`, mostrando "será implementado em breve":
| Local | Função planejada |
|---|---|
| 🛒 **Mercado** | Comprar consumíveis: poções, rações, tochas, ferramentas |
| ⚔️ **Forja** | Equipamentos: +dano base, +itens iniciais, -manutenção |
| ⚰️ **Cemitério** | Honrar caídos: itens herdados, reduzir morte permanente, bênçãos |
| 🗺️ **Sala de Mapas** | Planejar rota, reduzir chance de se perder, revelar locais |

> Recomendação: priorizar **Mercado** (alimenta a economia de recursos da jornada) e **Cemitério** (reforça o tema de morte/corrupção e a meta-progressão).

---

## 6. Heróis

### 6.1 Atributos **[IMPLEMENTADO]** (`HeroData`)
| Campo | Descrição |
|---|---|
| `heroClass` | Classe (ver 6.2) |
| `level` | Nível (afeta HP, salário, tamanho/raridade do deck) |
| `maxHp` / `currentHp` | `20 + nível×4` (+10 Guerreiro, −5 Mago) |
| `salary` | Custo de recrutamento: `20 + nível×10` |
| `personality` | Personalidade (ver 6.3) |
| `trait` | Traço (ver 6.4) |
| `loyalty` | Lealdade 0–100 |
| `morale` | Moral 0–100 |
| `isInjured` | Ferido: penalidade em eventos |
| `isDead` | Morte **permanente** (removido do roster ao fim da jornada) |
| `corruptionExposure` | Exposição à corrupção 0–100 |

### 6.2 Classes **[IMPLEMENTADO / PARCIAL]**
`Warrior, Mage, Healer, Rogue, Bard, Hunter`.
- **Com cartas hoje (4 cada):** Guerreiro ⚔️, Mago 🔮, Curandeiro ⚕️, Caçador 🏹.
- **Sem cartas ainda:** Ladino 🗡️ e Bardo 🎵 (Bardo é usado como classe **curinga** no Deck Manager). **[PLANEJADO]** criar seus conjuntos de cartas — bons candidatos para destrave de meta-progressão.

### 6.3 Personalidades **[PARCIAL — definidas, pouco usadas mecanicamente]**
`Brave 🦁, Coward 🐔, Ambitious ⭐, Loyal 🤝, Stubborn 🪨, Selfish 👑`.
**[PLANEJADO]** ligar personalidade a comportamento: covardes fogem/perdem moral mais fácil; ambiciosos exigem mais salário/recompensa; leais resistem à corrupção, etc.

### 6.4 Traços **[PARCIAL]**
`None, Drunkard 🍺, Lucky 🍀, Scarred ⚡, FastHealer 💚, Cursed 💀`.
**[PLANEJADO]** efeitos mecânicos (ex.: FastHealer cura feridas mais rápido; Cursed atrai eventos ruins; Lucky melhora rolagens). **[PLANEJADO]** Corrupção adiciona traços negativos com a exposição.

### 6.5 Moral, Lealdade e Corrupção **[PLANEJADO em sua maioria]**
- **Moral** sobe ao concluir jornadas (+20 aos sobreviventes) e cai com eventos ruins/fome.
- **[PLANEJADO]** Moral/Lealdade baixas → herói recusa missões, exige pagamento, ou abandona a guilda.
- **[PLANEJADO]** Exposição à corrupção alta → traços negativos, queda de moral, risco de "virar".

### 6.6 Morte permanente **[IMPLEMENTADO]**
Heróis que morrem na jornada são **removidos do roster** ao final. **[PLANEJADO]** Integração com o Cemitério (itens herdados, lápides, reduzir permadeath via bênçãos).

---

## 7. Cartas e Decks

### 7.1 Anatomia da carta **[IMPLEMENTADO]** (`CardData`)
Cada carta tem **dois conjuntos de efeitos**, escolhidos conforme o contexto:
- **Efeito de Jornada** (`JourneyEffectType` + valor + descrição) — usado para resolver desafios fora de combate.
- **Efeito de Combate** (`CombatEffectType` + dano/bloqueio/cura/duração) — usado na tela de combate tático.

Demais campos: `cardName`, `cardDescription`, `cardImage`, `rarity`, `requiredClass`, `energyCost`, `cardColor`.

### 7.2 Raridades **[IMPLEMENTADO]**
`Common, Rare, Epic, Legendary` — também codificadas por cor de fundo na UI (cinza/azul/roxo/dourado).

### 7.3 Geração de deck por herói **[IMPLEMENTADO]** (`DeckGenerator`)
- Tamanho do deck: `clamp(8 + nível, 8, 12)`.
- Composição: base de **Comuns** + **Raras** (`1 + nível/3`) + 1 **Épica** (se nível ≥ 3) + 1 **Lendária** (se nível ≥ 5).
- Se não houver cartas da classe, gera um **deck padrão** de ataques básicos.

### 7.4 Pool de cartas atual **[IMPLEMENTADO]** (`Resources/Cards`, 16 cartas)
| Classe | Cartas |
|---|---|
| ⚔️ Guerreiro | Corte Duplo, Fúria, **Investida** (Épica, ⚡3, jornada: pula 2 dias / combate: 15 dano), Postura Defensiva |
| 🔮 Mago | Bola de Fogo, Escudo de Gelo, Explosão Arcana, Teleporte |
| ⚕️ Curandeiro | Bênção, Purificação, Ressurgir, Toque Curativo |
| 🏹 Caçador | Armadilha, Flecha Lunar, Flecha Precisa, Olhar de Águia |

### 7.5 Efeitos de Jornada **[IMPLEMENTADO]** (`JourneyEffectType`)
`None, RemoveObstacle, HealInjury, GainFood, GainGold, RevealNextEvent, SkipDay, Intimidate, Purify, Teleport, ProtectFromWeather, RestoreMorale, ExtraRations`.

### 7.6 Efeitos de Combate **[PARCIAL — definidos, combate não implementado]** (`CombatEffectType`)
`None, Damage, DamageAll, Block, BlockAll, Heal, HealAll, Debuff, Buff, DrawCards, GainEnergy, Poison, ShieldBreak`.

---

## 8. Missões (Quests)

### 8.1 Geração procedural **[IMPLEMENTADO]** (`QuestGenerator`)
Gera 3 missões por ciclo. Cada missão tem:
- **Nome:** `{prefixo} {sufixo} - {bioma}` (ex.: "Cripta Amaldiçoada - 🌋 Vulcão").
- **Duração:** 4–8 dias (+2–5 de variação).
- **Recompensa base:** `50 + nível_jogador×20 + rand(0,80)`.
- **Nível recomendado, Corrupção (0–100), Risco (Baixo/Médio/Alto).**
- **Objetivo:** Derrote o chefe / Colete recursos / Resgate prisioneiros / Explore / Sobreviva / Encontre o artefato.
- **Requisitos de classe:** mais exigentes quanto maior a corrupção/risco (ex.: "2× Guerreiro Nv.3+").

### 8.2 Recompensa **[IMPLEMENTADO]**
- Sucesso: `baseReward + duração×20` + `sobreviventes×25` (+ bônus de Biblioteca).
- Fracasso/abandono: `baseReward/2` + `sobreviventes×25`.

### 8.3 Chefe Supremo **[PARCIAL]**
`GenerateBossQuest` existe e define o confronto final da run (ver §4.5), mas ainda não está plugado ao fluxo de seleção de missões.

### 8.4 Seleção de Missão — assistente de 3 passos **[IMPLEMENTADO]** (`QuestSelectionUI`)
1. **Passo 1 — Missão:** escolher entre as 3 disponíveis (mostra bioma, duração, recompensa, risco, corrupção, requisitos).
2. **Passo 2 — Party de apoio:** marcar os heróis que vão (toggles).
3. **Passo 3 — Deck principal:** escolher **qual herói** da party fornece o **deck** usado na jornada (o "herói principal" precisa estar entre os de apoio).

---

## 9. Jornada (Micro-loop de exploração)

### 9.1 Estrutura **[IMPLEMENTADO]** (`JourneyManager`)
A missão vira uma **fila de eventos**: um evento por dia + um **evento final de chefe**. O jogador avança dia a dia resolvendo eventos.

### 9.2 Recursos da jornada **[IMPLEMENTADO]**
| Recurso | Inicial | Regra |
|---|---|---|
| **Rações** | `10 + rand(0,5)` | −1/dia; ao zerar, todos os heróis perdem 5 HP/dia (fome) |
| **Tochas** | `5 + rand(0,3)` | −1/dia (efeito de escuridão **[PLANEJADO]**) |
| **Energia** | `5` (+bônus) | Custo das cartas; +2 ao usar "Encerrar Turno" |
| **Mão** | 5 cartas (máx. 7) | Compra 1 carta ao encerrar turno |

### 9.3 Resolução híbrida de eventos **[PARCIAL → PLANEJADO]**
> **Decisão de design:** modelo **híbrido (cartas + escolhas)**.
> *Estado atual:* hoje **todo** evento é resolvido jogando uma carta (`ApplyCardEffectOnJourney`). O sistema de **escolhas múltiplas com consequências** (`EventOutcome`/`EventConsequences`) está **definido nos dados mas não conectado** à UI da jornada.

Design-alvo:
- **Eventos narrativos** (`EventType.Normal/Story/Treasure/Trap/Rest/Shop`): apresentam **2–3 opções de texto**, cada uma com consequências (`goldChange`, `reputationChange`, `heroEffects` de HP/ferida/traço, `moraleChanges`, `extraDays`, `triggersCorruption`).
- **Desafios/obstáculos:** resolvidos **jogando a carta certa** (ex.: `RemoveObstacle`, `Purify`, `ProtectFromWeather`), gastando energia.
- **Combates** (`EventType.Combat`): abrem a **tela de combate tático** (§10).

### 9.4 Filtros de evento **[IMPLEMENTADO]** (`EventPool`)
Eventos são filtrados por `biomeTag`, `minCorruptionToAppear` e `minDay`. Carregados de `Resources/Events`.
> **[PARCIAL]** Hoje a pasta `Resources/Events` está vazia, então o jogo cai num conjunto de **eventos padrão hardcoded** (Ponte Quebrada, Baú Escondido, Ataque de Lobos, Confronto Final). Criar uma biblioteca real de eventos por bioma é prioridade de conteúdo.

### 9.5 Fim da jornada **[IMPLEMENTADO]**
Sucesso quando todos os eventos são resolvidos; fracasso se a party inteira morre ou ao abandonar. Sobreviventes curam HP ao máximo e ganham +20 de moral; mortos são removidos. Recompensa creditada (§8.2).

---

## 10. Combate Tático **[PLANEJADO]**

> **Decisão de design:** **combate tático por turnos com cartas**, estilo *Slay the Spire*, reutilizando os stats de combate já definidos nas cartas. Hoje `EventResolver` está **vazio** e combates são resolvidos como eventos comuns.

### 10.1 Visão
Encontros marcados como `EventType.Combat` (e o **chefe final** de cada missão) abrem uma **tela dedicada de batalha**.

### 10.2 Regras propostas
- **Turnos:** jogador joga cartas gastando **energia** (mesma economia da jornada: base 5, regenera no início do turno); ao encerrar, os inimigos agem.
- **Cartas usam o bloco de combate:** `combatDamage`, `combatBlock`, `combatHeal`, `combatDuration` + `CombatEffectType` (dano único/em área, bloqueio, cura, buff/debuff, veneno, comprar cartas, ganhar energia, quebrar escudo).
- **Party:** os heróis selecionados entram com seu HP atual; o **deck principal** é o baralho jogável; **[a decidir]** se decks de apoio contribuem com cartas extras ou apenas com presença/HP.
- **Inimigos:** escalam com `corruptionLevel` e nível recomendado da missão. Telegrafam intenções (estilo StS).
- **Chefe:** padrões de ataque em fases; recompensa alta; derrota = mortes permanentes.

### 10.3 Integração
- Vitória no combate → retorna à fila de eventos da jornada (próximo dia).
- Derrota → mortes da party / possível fim antecipado da jornada.

---

## 11. Economia e Balanceamento (referência atual)

| Parâmetro | Valor |
|---|---|
| Ouro inicial | 500 |
| Salário do herói | `20 + nível×10` |
| HP do herói | `20 + nível×4` (+10 Guerreiro / −5 Mago) |
| Refresh da taverna | 50 (atualmente desativado) |
| Preço de carta | 100 / 250 / 500 / 1000 (Comum→Lendária) |
| Upgrade da Biblioteca | `500 × nível` |
| Tamanho do deck | 8–12 (`8 + nível`) |
| Recompensa de missão | `base + duração×20 + sobreviventes×25` |
| Dano por fome | 5 HP/dia a todos quando rações = 0 |

> **[PLANEJADO]** Passe de balanceamento completo após o combate existir e a estrutura de run estar fechada. Hoje os números são de protótipo.

---

## 12. Interface e UX **[IMPLEMENTADO]**

- **Arquitetura de telas:** `UIManager` (singleton) controla painéis principais com animações de fade + scale e popups reutilizáveis: **Mensagem** (toast temporizado), **Confirmação** (sim/não), **Resultado** (título + corpo + fechar), **Loading**.
- **Estilo atual:** ícones via **emoji** como placeholders (⚔️🔮⚕️🏹💰❤️), texto com *typewriter effect* nos eventos.
- **[PLANEJADO]** Substituir emojis por ícones de arte; tooltips de carta; tela de detalhe de herói mais rica (`HeroDetailPanel` existe).

---

## 13. Arte e Áudio

### 13.1 Direção de arte **[PLANEJADO]**
Dark fantasy ilustrado: paleta dessaturada com acentos de "corrupção" (roxo/verde doentio). Retratos de herói, arte de carta, ícones de bioma e de recurso. Hoje: placeholders (cores sólidas + emoji).

### 13.2 Áudio **[PLANEJADO]**
- Música ambiente por contexto (hub calmo / jornada tensa / combate / chefe).
- SFX de carta, dano, morte, ouro. Stinger de morte de herói para reforçar o peso emocional.

---

## 14. Estado Atual de Implementação (resumo honesto)

| Sistema | Status |
|---|---|
| Hub da guilda + navegação de telas | ✅ Implementado |
| Recursos (ouro) + roster | ✅ Implementado |
| Taverna / recrutamento | ✅ Implementado (refresh pago desativado) |
| Biblioteca (loja + upgrade) | ✅ Implementado |
| Gerenciador de deck | ✅ Implementado |
| Geração de quests | ✅ Implementado |
| Seleção de missão (3 passos) | ✅ Implementado |
| Jornada por dias + recursos | ✅ Implementado |
| Cartas (dados + geração de deck) | ✅ Implementado |
| Resolução por **carta** na jornada | ✅ Implementado |
| Resolução por **escolha** (eventos) | ⚠️ Dados existem, não conectado |
| Biblioteca de eventos por bioma | ⚠️ Só eventos padrão hardcoded |
| **Combate tático** | ❌ Planejado (stats prontos) |
| Reputação como mecânica | ⚠️ Exibida, não consumida |
| Personalidade/Traços/Corrupção (efeitos) | ⚠️ Definidos, pouco usados |
| Classes Ladino/Bardo (cartas) | ❌ Sem cartas |
| Estrutura de **run** + permadeath de guilda | ❌ Planejado (hoje é sandbox persistente) |
| Meta-progressão entre runs | ❌ Planejado |
| Mercado / Forja / Cemitério / Sala de Mapas | ❌ Stubs |
| Chefe Supremo plugado ao fluxo | ⚠️ Função existe, não integrada |

---

## 15. Roadmap sugerido

**Fase 1 — Fechar o núcleo jogável**
1. Conectar o sistema de **escolhas de evento** à UI da jornada (resolução híbrida real).
2. Criar uma **biblioteca de eventos** por bioma (substituir os hardcoded).
3. Cobrar o refresh da taverna; usar **reputação** em algo concreto.

**Fase 2 — Combate**
4. Implementar a **tela de combate tático** (turnos, energia, intenções, inimigos).
5. Plugar combates de evento + **Chefe Supremo** ao fluxo.

**Fase 3 — Roguelike**
6. Definir **início/fim de run** e condições de derrota da guilda.
7. **Meta-progressão** entre runs (moeda persistente + destraves).
8. Medidor global de **Corrupção** que pressiona a run.

**Fase 4 — Profundidade e conteúdo**
9. Cartas de **Ladino** e **Bardo**; efeitos reais de personalidade/traços/corrupção.
10. Locais restantes (Mercado, Forja, Cemitério, Sala de Mapas).
11. Passe de **arte, áudio e balanceamento**.

---

## 16. Apêndice — Enums e dados de referência (do código)

**HeroClass:** `Warrior, Mage, Healer, Rogue, Bard, Hunter`
**Personality:** `Brave, Coward, Ambitious, Loyal, Stubborn, Selfish`
**Trait:** `None, Drunkard, Lucky, Scarred, FastHealer, Cursed`
**CardRarity:** `Common, Rare, Epic, Legendary`
**QuestRisk:** `Low, Medium, High`
**EventType:** `Normal, Combat, Treasure, Trap, Rest, Shop, Story`
**JourneyEffectType:** `None, RemoveObstacle, HealInjury, GainFood, GainGold, RevealNextEvent, SkipDay, Intimidate, Purify, Teleport, ProtectFromWeather, RestoreMorale, ExtraRations`
**CombatEffectType:** `None, Damage, DamageAll, Block, BlockAll, Heal, HealAll, Debuff, Buff, DrawCards, GainEnergy, Poison, ShieldBreak`

**Biomas:** 🌲 Floresta · ⛰️ Montanha · 🏚️ Pântano · 🏜️ Deserto · ❄️ Tundra · 🌋 Vulcão · 🏯 Ruínas

**Scripts principais (mapa do código):**
- *Dados (ScriptableObjects):* `CardData`, `DeckData`, `HeroData`, `QuestData`, `EventData`
- *Núcleo:* `GameManager`, `GuildManager`, `TavernManager`, `QuestManager`, `QuestGenerator`, `JourneyManager`, `CardManager`, `DeckGenerator`, `DeckManager`, `LibraryManager`, `EventPool`, `HeroFactory`, `MapManager`, `UIManager`, `GameInitializer`
- *UI:* `QuestSelectionUI`, `CardUI`, `HeroDetailPanel`, `PartyDisplayManager`
- *Vazios/placeholder:* `EventResolver` (reservado para o combate)

---

*Fim do documento. Este GDD reflete o código analisado em 19/06/2026 e as decisões de design: Dark Fantasy + Corrupção, jornada híbrida (cartas + escolhas), combate tático com cartas, e estrutura roguelike por runs.*
