# Game Design Document — *Guilda da Corrupção* (título provisório)

> **Documento de Design de Jogo (GDD)**
> Projeto Unity `Guilda-da-Corrupcao` · Unity 2022.3.62f3 · URP · Idioma: pt-BR
> Versão do documento: **1.1 — 14/08/2026** (1.0 em 19/06/2026)
> Status do projeto: **protótipo jogável de ponta a ponta**
>
> **O que mudou da 1.0 para a 1.1:** o combate tático, a resolução de eventos por
> escolha, a biblioteca de eventos, as quatro salas da guilda, a formação de grupo,
> o estresse e o mapa ramificado saíram de `[PLANEJADO]` — todos existem e funcionam.
> A progressão de XP entrou. O plano de trabalho vive em [`ROADMAP.md`](ROADMAP.md).
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
 ├──> TavernPanel · LibraryPanel · MarketPanel · ForgePanel · CemeteryPanel · MapRoomPanel
 ├──> DeckManagerPanel ──> HeroDetailPanel
 └──> QuestSelectionPanel (missão → party+formação → deck)
        └──> JourneyPanel ──> CombatPanel ──> (Resultado) ──> GuildPanel
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

### 5.2 Taverna — Recrutamento **[IMPLEMENTADO]**
- Exibe **3 recrutas** aleatórios (níveis 1–3) gerados por `HeroFactory.CreateRandomHero`.
- Recrutar custa o **salário** do herói (`20 + nível×10`). Ao recrutar, um deck inicial é gerado automaticamente para o herói.
- **Refresh** dos recrutas custa **50 de ouro**, cobrados em `TavernManager.PayToRefresh`.
- Recrutas atualizam após cada jornada.
- Teto do roster: **8** (o campo estava em 4 na cena, o que fazia a guilda nascer cheia).

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

### 5.5 As outras quatro salas **[IMPLEMENTADAS]**

| Local | O que faz hoje | Onde |
|---|---|---|
| 🛒 **Mercado** | Rações e tochas viram estoque que a próxima jornada consome; poção (+15 HP), bandagem (tira ferimento) e vinho (−18 estresse) agem na hora, sobre quem mais precisa | `MarketManager` |
| ⚔️ **Forja** | **Arma** (+1 de dano nas cartas *daquele* herói, via `CardOwnership`) e **armadura** (+4 de HP máximo), até nível 3 | `ForgeManager` |
| ⚰️ **Cemitério** | Lista os caídos, monumento devolve reputação (uma vez por herói) e a vigília alivia estresse de quem ficou | `CemeteryManager` |
| 🗺️ **Sala de Mapas** | Batedores revelam trechos do percurso; desvios permitem trocar um evento adiante; a sala sobe de nível | `MapRoomManager` |

> A Forja é o elo mais interessante: como o combate sabe de quem é cada carta, melhorar
> a arma do mago fortalece exatamente as magias dele, não o baralho inteiro.

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
| `isInjured` | Ferido: +25% de dano recebido |
| `isDead` | Morte **permanente** (removido do roster ao fim da jornada) |
| `corruptionExposure` | Exposição à corrupção 0–100. **[PARCIAL]** acumula, mas ninguém lê |
| `xp` / `xpToNextLevel` | Experiência do nível atual. Curva `100 + (nível−1)×75` |
| `stress` / `mentalState` | Estresse 0–100; ao estourar vira **Aflição** ou (22%) **Virtude** |
| `isOnDeathsDoor` | HP zerado: o próximo golpe pode ser fatal |
| `weaponLevel` / `armorLevel` | Equipamento comprado na Forja |

**Progressão [IMPLEMENTADO]:** sobreviventes ganham XP ao fim da jornada
(`base 60 + 8×trechos`, multiplicado pela corrupção da região, 40% se a missão fracassa).
Do **5º herói da formação em diante o XP rende metade** — grupos grandes ajudam na
estrada mas diluem a experiência, e ainda comem uma ração a mais por dia. Subir de
nível recalcula o HP máximo pela fórmula da `HeroFactory` e entrega o ganho como cura.
O nível já governa o tamanho do deck (`8+nível`), a raridade liberada (rara no 3,
lendária no 5), o salário e a dificuldade das missões geradas.

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

### 6.5 Estresse, Moral e Corrupção

**Estresse [IMPLEMENTADO]** — o eixo emocional, no espírito de *Darkest Dungeon*:
- Sobe com dano recebido (0,8 por ponto de HP), ao ver um companheiro cair na Beira da
  Morte (+12) ou morrer (+25), com a escuridão e com a corrupção da região.
- Personalidade e traço modulam: covardes sofrem +35%, valentes −25%, amaldiçoados +25%.
- Ao chegar a 100, o herói **quebra**: 78% de chance de Aflição, 22% de Virtude.
- Só se alivia fora da estrada — vinho no Mercado, vigília no Cemitério, e −15 ao voltar.

**Beira da Morte [IMPLEMENTADO]** — o HP para em 0 na primeira vez; só um golpe seguinte
mata, e ainda assim por rolagem (~45%, pior com estresse alto, melhor se Sortudo).

**Moral [PARCIAL]** — sobe ao concluir jornadas (+20) e cai com estresse e eventos ruins,
mas ainda não muda o comportamento de ninguém.

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

### 7.6 Efeitos de Combate **[IMPLEMENTADO]** (`CombatEffectType`)
`None, Damage, DamageAll, Block, BlockAll, Heal, HealAll, Debuff, Buff, DrawCards, GainEnergy, Poison, ShieldBreak, BuffNextCard, Evade, Cleanse`.

> **Regra de manutenção:** acrescentar valores **só no fim** do enum. Eles são gravados
> como número nos assets — inserir no meio troca o efeito de toda carta já configurada.

> **Lição registrada:** `Buff` e `None` não tinham `case` no `CombatManager`. Quatro das
> 16 cartas cobravam energia, saíam da mão e não faziam nada, enquanto a descrição
> prometia um efeito. Implementá-las levou a vitória contra chefes de 24% para ~39%
> sem tocar em nenhum outro número. O smoke test hoje tem uma trava para isso.
>
> **O mesmo buraco segue aberto do lado da jornada:** `Ressurgir` e `Flecha Precisa`
> têm `journeyEffect = None`, e `Intimidate` — o único jeito de evitar um combate — não
> existe em carta nenhuma.

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

### 8.4 Preparação — assistente de 3 passos **[IMPLEMENTADO]** (`QuestSelectionUI`)
1. **Passo 1 — Missão:** escolher entre as 3 disponíveis (mostra bioma, duração, recompensa, risco, corrupção, requisitos).
2. **Passo 2 — Party e formação:** marcar quem vai e **ordenar** o grupo. A ordem *é* a
   formação: os dois primeiros são a linha de frente. A tela avisa quem rende mais na
   retaguarda e quanto o grupo comerá por dia.
   - **Sem teto de tamanho**, mas com custo: acima de 4 heróis, cada bloco de 4 soma uma
     ração diária (`PartyFormation.DailyRations`) — e o XP dos extras cai pela metade.
   - Os **requisitos de classe** da missão são verificados de verdade
     (`GetUnmetRequirements`), não apenas exibidos.
3. **Passo 3 — Deck principal e provisões:** escolher qual herói fornece o baralho base.
   Os companheiros **emprestam cartas** ao baralho da jornada (`JourneyDeckBuilder`), e o
   `CardOwnership` guarda de quem é cada uma. Rações e tochas compradas no Mercado entram aqui.

---

## 9. Jornada (Micro-loop de exploração)

### 9.1 Estrutura — mapa ramificado **[IMPLEMENTADO]** (`JourneyManager`, `JourneyMap`)
A missão vira uma **rota ramificada no formato do *Slay the Spire***: cada camada oferece
dois ou três caminhos, e todos desembocam no nó do chefe. A fila linear anterior virou um
caso particular disto — um mapa de uma coluna só.

O gerador garante duas propriedades: **todo nó tem saída** (logo, todo caminho chega ao
chefe) e **todo nó tem pai** (logo, nenhum nó fica inalcançável). As arestas não se cruzam,
o leque nunca passa de três destinos, e a última camada estreita para dar sensação de funil.

A escolha da rota é do jogador desde o primeiro passo — inclusive a entrada.

### 9.2 Recursos da jornada **[IMPLEMENTADO]**
| Recurso | Inicial | Regra |
|---|---|---|
| **Rações** | `10 + rand(0,5)` | −1/dia; ao zerar, todos os heróis perdem 5 HP/dia (fome) |
| **Tochas** | `5 + rand(0,3)` | −1/dia (efeito de escuridão **[PLANEJADO]**) |
| **Energia** | `5` (+bônus) | Custo das cartas; +2 ao usar "Encerrar Turno" |
| **Mão** | 5 cartas (máx. 7) | Compra 1 carta ao encerrar turno |

### 9.3 Resolução híbrida de eventos **[IMPLEMENTADO, com uma lacuna de design]**
> **Decisão de design:** modelo **híbrido (cartas + escolhas)**.

Como funciona hoje:
- **Eventos narrativos** apresentam **3 opções**, e o `EventResolver` aplica as
  consequências: ouro, reputação, HP, ferimento, traço negativo, moral, dias extras e
  corrupção — passando por Beira da Morte e estresse.
- **Cartas** podem ser jogadas *antes* de decidir, gastando energia. Acumulam
  **mitigação** (até 75%) que reduz o dano do desfecho escolhido, além do próprio efeito.
- **Combates** (`JourneyEventType.Combat` e o chefe) ganham o botão
  "⚔️ Enfrentar em combate", que abre a tela tática (§10).

### 9.3.1 Carta destrava escolha **[IMPLEMENTADO em 14/08]**

Cada `EventOutcome` pode declarar um **`requiredEffect`**: um efeito de carta que aquela
opção exige. É o que liga o baralho à decisão.

- A opção com requisito aparece **travada e legível** — *"🔒 Precisa de uma carta capaz de
  purificar"* — e **nunca some**: o jogador precisa ver o que perdeu por não ter trazido a
  carta. É isso que faz a preparação pesar na jornada seguinte.
- Jogar a carta destrava a opção na hora (as escolhas são redesenhadas).
- O evento pode declarar `empoweredConsequences`: um desfecho melhor para quem trouxe a
  carta certa. Sem ele, a carta apenas abre o caminho.
- Todo evento mantém ao menos uma saída **sem** requisito — o smoke test verifica isso,
  junto com "todo requisito é satisfazível por alguma carta existente".

Os **25 eventos** têm esse caminho de especialista. Antes, qualquer carta servia para
qualquer ocasião e o único vínculo era uma mitigação genérica de 10%: o baralho mudava o
quanto o grupo apanhava, nunca o que ele podia fazer.

### 9.4 Filtros de evento **[IMPLEMENTADO]** (`EventPool`)
Eventos são filtrados por `biome` (enum `BiomeType`, não mais string com emoji),
`minCorruptionToAppear` e `minDay`. Carregados de `Resources/Events`, com memória dos
3 últimos usados para não repetir e 70% de preferência pelo evento próprio da região.

**Acervo atual: 19 eventos** — 14 comuns e 5 chefes, todos com 3 opções.

| Bioma | Eventos próprios | Chefe |
|---|---|---|
| 🧭 Any (curinga) | 4 — Acampamento Noturno, Mau Presságio, Mercador Errante, O Que Restou da Expedição | O Guardião Sem Nome |
| 🌲 Floresta | 2 — Alcateia Faminta, Ponte Quebrada | A Coisa da Mata |
| ⛰️ Montanha | 2 — Desfiladeiro Estreito, Mina Abandonada | O Gigante de Pedra |
| 🏚️ Pântano | 2 — Altar Afundado, Névoa Pútrida | O Afogado |
| 🏯 Ruínas | 1 — Salão dos Ídolos | O Bibliotecário Cego |
| 🏜️ Deserto | 1 — Tempestade de Areia | — |
| ❄️ Tundra | 1 — Silêncio Branco | — |
| 🌋 Vulcão | 1 — Veios de Enxofre | — |

> **[PARCIAL]** Deserto, Tundra e Vulcão têm **um evento cada** e nenhum chefe próprio —
> uma jornada nesses biomas repete os genéricos. E há **um único** evento de combate
> não-chefe no acervo inteiro (Alcateia Faminta), o que faz o sistema de combate quase
> não aparecer fora da luta final.
>
> Os eventos padrão hardcoded do `EventPool` continuam no código como rede de segurança,
> usados só se `Resources/Events` estiver vazia.

### 9.5 Fim da jornada **[IMPLEMENTADO]**

Sucesso quando a rota chega ao fim; fracasso se a party inteira morre, se o chefe vence ou ao
abandonar. Mortos são removidos do roster e registrados no Cemitério.

**A volta para casa** (`JourneyResultUI`) é uma tela de balanço, não um aviso:

- Uma linha por herói: vida, estado (ferido, abalado, virtuoso), **XP ganho com barra** e promoções.
- Ouro discriminado: contrato + sobreviventes + espólio das lutas + bônus da Biblioteca.
- **Escolha de despojo** ao vencer — ouro extra, noite na taverna (−30 de estresse), cuidados do
  curandeiro (trata feridos) ou contar a história (+15 de reputação). As opções que dependem de
  alguém precisar (estresse, ferimento) só aparecem quando há a quem servir, e a saída fica travada
  até a escolha: a decisão é o momento.

**O que a jornada deixa marcado [IMPLEMENTADO]:**

| Consequência | Regra |
|---|---|
| Vida | O descanso é **piso**, não teto: quem voltou acima de 60% não é rebaixado |
| Ferimento | **Não sara por sorteio.** Só Recuperação Rápida se vira sozinho; o resto precisa de bandagem, do curandeiro ou de uma jornada inteira em casa |
| Luto | Quem viu companheiro cair perde moral e ganha estresse ao voltar |
| Esgotamento | Acima de **85 de estresse** o herói recusa partir; aparece bloqueado e com o motivo na preparação |
| Quem fica | Descansa a cada jornada concluída pelos outros — é a válvula que impede a guilda de travar |

---

## 10. Combate Tático **[IMPLEMENTADO]** (`CombatManager`)

Combate por turnos com cartas, estilo *Slay the Spire*, reaproveitando o bloco de combate
já definido em cada carta.

### 10.1 Como entra
Eventos `JourneyEventType.Combat` e o **chefe** de cada missão oferecem o botão
"⚔️ Enfrentar em combate", que abre a tela dedicada. Ao entrar, as demais telas são
escondidas (`UIManager.EnterCombatScreen`) e restauradas ao sair.

### 10.2 Regras
- **Turnos:** o jogador joga cartas gastando **energia**; ao encerrar, os inimigos agem.
- **Mão de 5 cartas**, com compra e pilha de descarte próprias do combate.
- **Bloqueio dos dois lados** — heróis e inimigos acumulam bloqueio que absorve dano.
- **Intenções telegrafadas:** cada inimigo anuncia o que fará no turno —
  **Atacar**, **Atacar todos**, **Defender** ou **Estressar** — por pesos próprios do asset.
- **Formação importa:** a ordem da party é a formação. Ataques caem na linha de frente em
  ~73% das vezes, e a carta rende menos se o dono estiver fora da posição dela.
- **Dono da carta:** `CardOwnership` liga cada carta ao herói que a emprestou — é isso que
  faz a arma da Forja fortalecer só as cartas daquele herói.
- **Dano nos heróis passa pelo `EventResolver`**, então Beira da Morte, estresse e morte
  permanente valem igual dentro e fora do combate.
- **Recuar** é possível: custa moral e estresse, e não rende recompensa.
- **Derrota para o chefe** encerra a jornada; perder um encontro comum apenas cobra caro.

### 10.3 Balanceamento medido
Contra chefes, **~39% de vitória** (alvo 35–75%), combate em torno de 16 turnos.
Encontros comuns ficam em 90–94% — dentro do alvo, mas é o lado folgado da curva.

### 10.3.1 O chefe e a saída narrativa **[REBALANCEADO em 14/08]**

O combate era **estritamente dominado** pela narrativa: "aceitar o acordo" com A Coisa da
Mata pagava 300 de ouro sem risco, contra 140 por vencer lutando. Fugir da luta era a
jogada racional, e o sistema mais caro do projeto virava opcional.

A escolha foi mantida — decisão do autor — mas passou a custar o que promete:

| | antes | depois |
|---|---|---|
| Ouro das saídas narrativas | 140 a 320 | 40% disso (60% na que exige carta) |
| Dano das saídas narrativas | — | +40% |
| Ouro por vencer o chefe lutando | 130–155 | **260–310** |
| Melhor saída narrativa | livre | **exige a carta temática do bioma** |

Também entraram **seis encontros de combate**, um por bioma que não tinha nenhum: o acervo
inteiro tinha **um** combate fora o chefe, e as lutas por jornada subiram de 1,2 para 2,73.

### 10.4 Integração
- Vitória → ouro dos inimigos, alívio de estresse (−8) e volta para a rota.
- Derrota → estresse (+15), possíveis mortes e, se era o chefe, fim da jornada.

---

## 11. Economia e Balanceamento (referência atual)

| Parâmetro | Valor |
|---|---|
| Ouro inicial | 500 |
| Salário do herói | `20 + nível×10` |
| HP do herói | `20 + nível×4` (+10 Guerreiro / −5 Mago) |
| Refresh da taverna | 50 |
| XP por jornada | `60 + 8×trechos`, ×(1 + corrupção/100 × 0,5); 40% se fracassa |
| XP do 5º herói em diante | 50% |
| Curva de nível | `100 + (nível−1)×75` |
| Preço de carta | 100 / 250 / 500 / 1000 (Comum→Lendária) |
| Upgrade da Biblioteca | `500 × nível` |
| Tamanho do deck | 8–12 (`8 + nível`) |
| Recompensa de missão | `base + duração×20 + sobreviventes×25` |
| Dano por fome | 5 HP/dia a todos quando rações = 0 |

**Letalidade alvo** (decisão do autor): punitiva, no espírito de *Darkest Dungeon* —
**0,33 a 0,67 mortes por jornada** com party de 4. Medição atual: **0,48**.

Os números que governam o desgaste da estrada são `QuestSelectionUI.baseRations` /
`baseTorches`, `JourneyManager.starvationDamage` e `darknessStress`. São **campos
serializados**: mudar o valor no script não altera a instância salva na cena.

> **Como medir:** `Tools ▸ Guild of Legends ▸ Rodar Smoke Test` simula 200 jornadas e 800
> combates com o código real. **Uma run de Play Mode é n=1 e não serve para balancear.**
>
> **⚠️ Ressalva:** o simulador da jornada usa um "jogador neutro, sem mitigação por
> cartas", enquanto o jogador real reduz de 10% a 75% do desfecho jogando cartas. As
> mortes/jornada medidas estão **superestimadas**. Corrigir isso é item da Fase 2.

> **[PLANEJADO]** Passe de balanceamento completo depois que a estrutura de run fechar.

---

## 12. Interface e UX **[IMPLEMENTADO]**

- **Arquitetura de telas:** `UIManager` (singleton) controla painéis principais com animações de fade + scale e popups reutilizáveis: **Mensagem** (toast temporizado), **Confirmação** (sim/não), **Resultado** (título + corpo + fechar), **Loading**.
- **A cena é montada por código:** `Tools ▸ Guild of Legends ▸ Montar Cena`
  (`GuildSceneSetup`) cria e liga os painéis. Mudança de layout se faz ali, não à mão no
  Inspector — assim o estado da cena é reproduzível.
- **Kit visual Bloodlines UI**: molduras de pedra em 9-slice, botões com estados e fonte
  MedievalSharp, aplicados pelo mesmo setup.
- **Estilo atual:** ícones via **emoji** como placeholders (⚔️🔮⚕️🏹💰❤️), texto com *typewriter effect* nos eventos.
- **[PLANEJADO]** Substituir emojis por ícones de arte; tooltips de carta; usar as barras
  de progresso do kit no HP e no estresse (hoje são retângulos chapados).

### 12.1 Armadilhas de UI já pagas (não repetir)
- **Singleton em painel inativo nasce nulo** — `Awake` não roda enquanto o objeto está
  desligado. Combinado com `Instance?.Metodo()`, a tela vira inalcançável em silêncio.
  `DeckManager` e `HeroDetailPanel` resolvem procurando sob demanda.
- **Uma animação por painel** — duas corrotinas concorrentes deixavam a tela em branco: a
  de fechar terminava depois e desligava o painel recém-aberto.
- **Ordem de irmãos, não `sortingOrder`** — é `SetAsLastSibling()` que põe a tela na frente.
- **`onClick.Invoke()` não prova que dá para clicar** — o teste usa `EventSystem.RaycastAll`.

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
| Taverna / recrutamento (com refresh pago) | ✅ Implementado |
| Biblioteca (loja + upgrade) | ✅ Implementado |
| Mercado / Forja / Cemitério / Sala de Mapas | ✅ Implementado |
| Gerenciador de deck | ✅ Implementado |
| Geração de quests + requisitos verificados | ✅ Implementado |
| Preparação (missão → party → formação → deck) | ✅ Implementado |
| Formação de grupo e efeitos de posição | ✅ Implementado |
| Jornada com **mapa ramificado** | ✅ Implementado |
| Resolução por **carta** + por **escolha** | ✅ Implementado |
| Biblioteca de eventos por bioma | ⚠️ 19 eventos; 3 biomas com 1 evento e sem chefe |
| **Combate tático** | ✅ Implementado |
| Estresse, estados mentais, Beira da Morte | ✅ Implementado |
| Morte permanente + Cemitério | ✅ Implementado |
| **Progressão de XP / nível** | ✅ Implementado |
| Carta que muda a **escolha** do evento | ❌ A lacuna principal (§9.3) |
| Combate como caminho preferível ao texto | ❌ Hoje é dominado (§10.3) |
| Reputação como mecânica | ⚠️ Sobe e desce, não consome nem trava nada |
| Exposição à corrupção | ⚠️ Acumula, ninguém lê |
| Personalidade / Traços / Estados mentais | ⚠️ Metade sem efeito mecânico |
| Classes Ladino/Bardo (cartas) | ❌ Sem cartas |
| Estrutura de **run** + fim de jogo | ❌ Planejado (hoje é sandbox infinito) |
| Corrupção global como relógio | ❌ Planejado |
| Meta-progressão entre runs | ❌ Planejado |
| Chefe Supremo plugado ao fluxo | ⚠️ `GenerateBossQuest` existe, não é chamado |
| Persistência entre sessões | ⚠️ Só os decks (PlayerPrefs) |
| Arte e áudio | ❌ Placeholder / inexistente |

---

## 15. Roadmap

O plano de trabalho detalhado, com diagnóstico e verificação, vive em
**[`ROADMAP.md`](ROADMAP.md)**. Em resumo:

| Fase | Do que trata | Estado |
|---|---|---|
| 0 | Limpeza: logs, referências da cena, encoding, duplicações | ✅ concluída em 14/08 |
| 1 | **Progressão de XP** dos heróis | ✅ concluída em 14/08 |
| 2 | **Carta ⨯ escolha** na jornada, chefe rebalanceado, simulador que joga cartas | ▶️ próxima |
| 3 | **A run**: início/fim, Corrupção global, Chefe Supremo, meta-progressão | pendente |
| 4 | Dar efeito ao que só tem rótulo (reputação, traços, estados mentais) | pendente |
| 5 | Conteúdo (cartas de Ladino/Bardo, eventos, inimigos), arte e áudio | pendente |

---

## 16. Apêndice — Enums e dados de referência (do código)

**HeroClass:** `Warrior, Mage, Healer, Rogue, Bard, Hunter`
**Personality:** `Brave, Coward, Ambitious, Loyal, Stubborn, Selfish`
**Trait:** `None, Drunkard, Lucky, Scarred, FastHealer, Cursed`
**CardRarity:** `Common, Rare, Epic, Legendary`
**QuestRisk:** `Low, Medium, High`
**JourneyEventType:** `Normal, Combat, Treasure, Trap, Rest, Shop, Story`
— chamava-se `EventType`, no namespace global, onde vencia o `UnityEngine.EventType` e
quebrava a compilação de todo pacote de terceiros importado.
**JourneyEffectType:** `None, RemoveObstacle, HealInjury, GainFood, GainGold, RevealNextEvent, SkipDay, Intimidate, Purify, Teleport, ProtectFromWeather, RestoreMorale, ExtraRations`
**CombatEffectType:** `None, Damage, DamageAll, Block, BlockAll, Heal, HealAll, Debuff, Buff, DrawCards, GainEnergy, Poison, ShieldBreak, BuffNextCard, Evade, Cleanse`
**BiomeType:** `Any, Forest, Mountain, Swamp, Desert, Tundra, Volcano, Ruins`
**MentalState:** `Normal` · aflições `Paranoid, Fearful, Hopeless, Irrational, Abusive` · virtudes `Courageous, Focused, Vigorous, Stalwart`
**EnemyIntent:** `Attack, AttackAll, Defend, Stress`

**Biomas:** 🌲 Floresta · ⛰️ Montanha · 🏚️ Pântano · 🏜️ Deserto · ❄️ Tundra · 🌋 Vulcão · 🏯 Ruínas

**Scripts principais (mapa do código):**
- *Dados (ScriptableObjects):* `CardData`, `DeckData`, `HeroData`, `QuestData`, `EventData`, `EnemyData`
- *Guilda:* `GuildManager`, `TavernManager`, `LibraryManager`, `MarketManager`, `ForgeManager`, `CemeteryManager`, `MapRoomManager`, `MapManager`, `DeckManager`, `DeckRepository`, `HeroFactory`, `DeckGenerator`
- *Missão e jornada:* `QuestManager`, `QuestGenerator`, `JourneyManager`, `JourneyMap` (+ gerador), `JourneyDeckBuilder`, `EventPool`, `EventResolver`, `PartyFormation`, `CardManager`, `CardOwnership`
- *Combate:* `CombatManager`, `EnemyPool`, `CombatDropTarget`, `CombatFeedback`
- *UI:* `UIManager`, `QuestSelectionUI`, `JourneyMapUI`, `CardUI`, `CardDragHandler`, `HandFanLayout`, `HeroDetailPanel`, `PartyDisplayManager`, `PartyMemberCard`
- *Ferramentas de Editor* (`#if UNITY_EDITOR`, fora do build): `GuildSceneSetup` (monta a cena por código), `GuildSmokeTest` (38 verificações + simulação de balanceamento), `PlayModeProbe` (percorre o jogo e grava `PlayModeReport.txt`), `CardCreator`

---

*Fim do documento. Esta versão 1.1 reflete o código auditado em 14/08/2026 e as decisões de
design: Dark Fantasy + Corrupção, jornada híbrida (cartas + escolhas), combate tático com
cartas, e estrutura roguelike por runs — esta última ainda por construir.*
