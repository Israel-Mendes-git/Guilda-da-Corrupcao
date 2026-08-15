# Roadmap — Guilda da Corrupção

> Plano de seguimento escrito em **2026-08-14**, a partir de uma auditoria do código, dos assets e
> do último `PlayModeReport.txt` (02/08, 20:47 — console limpo, 38 verificações, 0 falhas).
> Foco escolhido pelo autor: **fechar as dívidas do núcleo** e, em seguida, **construir a estrutura
> de roguelike**.
>
> **Conferido no Editor em 14/08** (via MCP, não só pelo relatório antigo): o projeto **compila** —
> os únicos avisos no console vêm de DLLs duplicadas do pacote `realvirtual-MCP`, alheias ao jogo.
> Seguem valendo: as 6 referências nulas da cena, `maxRosterSize = 8`, as 2 cartas sem efeito de
> jornada e **1 único** evento de combate não-chefe entre os 19.

---

## 1. Onde o projeto está

O protótipo é sólido e maior do que a documentação admite. O `GDD.md` (19/06) está **desatualizado**:
descreve o combate tático como `[PLANEJADO]`, o `EventResolver` como vazio e `Resources/Events` como
pasta sem conteúdo. Hoje os três existem e funcionam — combate por turnos com intenções, resolução de
escolhas com Beira da Morte e estresse, e 19 eventos autorais.

**Funciona de ponta a ponta:** hub → taverna/biblioteca/forja/mercado/cemitério/sala de mapas →
seleção de missão em 3 passos com formação → mapa ramificado → eventos com escolhas → combate por
cartas → resultado → volta ao hub.

**Escala de conteúdo:** 16 cartas (4 por classe, 4 classes), 11 inimigos (6 comuns + 5 chefes),
19 eventos (14 comuns + 5 chefes), 1 cena.

---

## 2. Diagnóstico

### 2.1 O buraco de design: as duas metades do jogo não se sustentam

**O combate é estritamente dominado pela narrativa.** No nó de chefe o jogador recebe o botão
"⚔️ Enfrentar em combate" **e** as opções narrativas do evento (`JourneyManager.cs:390-397`).
Comparando `CHEFE - A Coisa da Mata`:

| Caminho | Ouro | Custo |
|---|---|---|
| Vencer em combate | 140 (`goldReward` do asset) | risco real de morte permanente |
| "🤝 Ouvir o que ela oferece" | **300** | −20 reputação (que não faz nada), 1 traço, −20 moral |

Escolher a narrativa é sempre melhor. O sistema mais caro do projeto — 1153 linhas de
`CombatManager` — é opcional e financeiramente pior. Some-se a isso que **há apenas 1 evento de
combate não-chefe** no pool inteiro (`Alcateia Faminta`, Floresta): o relatório de Play Mode registra
"vezes que a opção de combate apareceu: **1**" numa jornada de 7 dias.

**A carta não conversa com o evento.** Qualquer carta pode ser jogada em qualquer evento, e o efeito
é aplicado sem olhar para o que o evento é (`ApplyCardEffectOnJourney`, `JourneyManager.cs:648`). A
única ligação é a mitigação genérica de `GetMitigationFor` (`:861`) — 10% para quase tudo. Nenhuma
escolha do evento exige, destrava ou muda por causa de uma carta. Isso esvazia o pilar declarado
*"Preparação > execução"*: o deck não altera o que o jogador **pode fazer**, só o quanto ele apanha.

**Duas cartas são inertes na jornada** — exatamente o bug que já foi corrigido do lado do combate:

| Carta | `journeyEffect` | O que acontece hoje |
|---|---|---|
| Ressurgir (Healer, ⚡4) | `None` | cai no `default`, gasta 4 de energia e só mostra "usado com sucesso" |
| Flecha Precisa (Hunter, ⚡1) | `None` | idem |

E há efeitos sem carta nenhuma que os produza: **`Intimidate`** (o único jeito de evitar um combate,
`JourneyManager.cs:697`), `GainGold` e `ExtraRations`. Código vivo que nunca roda.

**O simulador mede uma jornada que ninguém joga.** `GuildSmokeTest.cs:319` diz *"Jogador neutro:
escolhe ao acaso, sem mitigação por cartas"*. O balanceamento de 0,48 mortes/jornada foi medido com o
jogador **sem jogar cartas** — no jogo real ele mitiga até 35% por carta. As mortes reais são
menores que as medidas. É o mesmo erro que já custou uma conclusão invertida sobre o combate
(memória `simulador-que-nao-replica-o-jogo`), agora do lado da estrada.

### 2.2 Não existe run — o jogo não começa nem termina

O jogo é um sandbox infinito. Não há `GameOver`, fim de run, nem tela de início (busca por
`GameOver|EndRun|derrota da guilda` em `Assets/Scripts`: **nenhum resultado**).

- **Heróis nunca sobem de nível.** Não existe `xp` em `HeroData` nem um `level++` em lugar algum — a
  única menção a XP no projeto é um texto de propaganda em `MapManager.cs:57`. Como
  `QuestManager.GetPlayerAverageLevel()` (`:79`) define a dificuldade das missões, **a dificuldade
  nunca escala**: só sobe se o jogador contratar gente de nível maior na taverna (1–3).
- **A Corrupção não é relógio de nada.** `corruptionLevel` é sorteado por missão
  (`QuestGenerator.cs:45`) e nunca avança no mundo. Não há medidor global.
- **`corruptionExposure` é número morto**: acumula em `EventResolver.cs:305` e ninguém lê.
- **Reputação é decorativa**: sobe e desce (`GuildManager.cs:137`), não é gasta, não trava nada, não
  encerra nada. É por isso que a saída narrativa do chefe sai de graça.
- **Chefe Supremo não está plugado**: `QuestGenerator.GenerateBossQuest` (`:95`) existe e nunca é
  chamado pelo fluxo.

### 2.3 Dívidas conhecidas e pendências técnicas

| Item | Evidência | Situação |
|---|---|---|
| Mudança de cópias no `DeckManager` | `HANDOFF.md` "Não verificado" | compilou, **nunca passou por Play Mode** |
| Ruído de log | 51 `Debug.Log` em `QuestSelectionUI`, 10 em `DeckGenerator`, 8 em `GameInitializer`, 5 em `MapManager` | encheu o `Editor.log` em 76 MB numa sessão |
| Referências nulas na cena | relatório: `JourneyManager` (biomeIcon, deckCountText, handCountText, discardCountText), `MapRoomManager.revealedEventPrefab`, `JourneyMapUI.edgePrefab` | contadores de baralho/mão/descarte não atualizam |
| Heróis iniciais duplicados | `GuildManager.AddStartingHeroes` (`:53`, via `HeroFactory`) **e** `GameInitializer.CreateInitialHeroes` (`:49`, hardcoded) | valores divergentes (Gromm HP 42 × 45); a segunda cria heróis sem `heroId` e sem deck |
| Encoding legado | `CardCreator`, `DeckGenerator`, `GameInitializer`, `GameManager`, `HeroFactory` não são UTF-8 | comentários corrompidos ("her�is") |
| `MapManager.OnUpgradeButtonClick` | `:237` | só mostra "custa 500 ouro" e não faz nada |
| Traços e personalidades sem efeito | `Drunkard`, `Scarred`; `Ambitious`, `Stubborn`; 6 dos 9 `MentalState` | só aparecem como rótulo |
| Áudio | nenhum `AudioSource`/`AudioClip` em `Assets/Scripts` | inexistente (o kit Bloodlines traz um `SoundManager` não usado) |
| Barras de progresso do kit | `Assets/Alebardium/Bloodlines UI/Textures/Progress_Bar/` | HP e estresse ainda são retângulos chapados |
| Cobertura de conteúdo | Deserto, Tundra e Vulcão têm **1 evento próprio cada**; nenhum desses 3 biomas tem chefe | jornada repete os 4 eventos genéricos |
| `GDD.md` | versão 1.0, 19/06 | descreve como planejado o que já existe |

**Fora de escopo agora:** persistência entre sessões. O autor vai importar um pacote de save e
avisar. Vale notar que o projeto **já tem** `Assets/Bench Universal Save System/` no disco, com
`PersistentObject` e UI de slots — pode ser exatamente a peça procurada.

---

## 3. O plano

### Fase 0 — Limpar a mesa ✅ *concluída em 14/08*
*Nada aqui muda design; tudo aqui remove ruído que atrapalha medir o resto.*

1. ✅ **Ruído de log podado** — 74 `Debug.Log` de diagnóstico saíram de `QuestSelectionUI`,
   `DeckGenerator`, `GameInitializer` e `MapManager`. Junto foram os métodos `DebugReferences` e
   `DebugAllTexts`, que despejavam a hierarquia inteira a cada card criado. `LogWarning`/`LogError`
   reais ficaram, e o atalho F5 (recarregar a tela) sobreviveu sem o log.
2. ✅ **Contadores de baralho/mão/descarte criados na cena** via `GuildSceneSetup` — existiam no
   script e nunca tinham sido montados, então o jogador não via quantas cartas restavam.
   *Das 6 referências nulas do relatório, só 4 eram bugs:* `edgePrefab` é opcional por design
   ("deixe vazio para gerar em runtime") e `biomeIcon` depende de arte de bioma inexistente.
   O `PlayModeProbe` agora distingue **opcional vazio** de **referência faltando** — uma linha de
   alerta que sempre aparece deixa de ser lida.
3. ✅ **Sala de Mapas volta a listar o que os batedores acharam** — `revealedEventPrefab` era nulo e
   o método saía cedo, então o jogador pagava pelos batedores e via uma lista vazia, sem erro no
   console. Agora o rótulo é criado em runtime quando não há prefab, como o `JourneyMapUI` já fazia.
4. ✅ **`GameInitializer.CreateInitialHeroes` removido** — `GuildManager.Start()` já cria o elenco
   pela `HeroFactory`. A cópia hardcoded divergia (Gromm com 45 de HP contra 42) e gerava heróis sem
   `heroId`, o que impedia o deck deles de ser salvo.
5. ✅ **Cinco arquivos reencodados para UTF-8** (`CardCreator`, `DeckGenerator`, `GameInitializer`,
   `GameManager`, `HeroFactory`).
6. ✅ **Botão "Melhorar" do painel de info escondido** — prometia "custa 500 ouro" e não fazia nada.
   Quem melhora de verdade é cada sala, no próprio botão (`LibraryManager`, `MapRoomManager`);
   duplicar isso no mapa só criava uma segunda promessa, agora falsa em dois lugares.
7. ✅ **`GDD.md` atualizado para a versão 1.1** — combate, `EventResolver`, eventos, salas, formação,
   estresse e mapa ramificado saíram de `[PLANEJADO]`.

**Achado extra, corrigido no caminho:** o `Tools ▸ Card Creator` era uma armadilha. Ele ainda
gravava `CombatEffectType.None` em Teleporte e Purificação — ou seja, rodá-lo **desfazia** a
correção das cartas mortas, sobrescrevendo os assets sem avisar. Agora a tabela dele espelha os
assets reais (`Evade`, `Cleanse`, `BuffNextCard`) e nada é sobrescrito sem o autor marcar a opção.

### Fase 1 — O herói progride ✅ *concluída em 14/08*

1. ✅ **`HeroData` ganhou `xp`/`xpToNextLevel`** e `AddXp`, com curva `100 + (nível−1)×75`
   (100 · 175 · 250 · 325 · 400…). Subir de nível recalcula o HP máximo pela fórmula da
   `HeroFactory` — extraída para `MaxHpFor`/`SalaryFor`, para criação e promoção usarem a mesma
   conta — e **entrega o ganho como cura**: o herói volta mais forte, não com uma barra maior e
   igualmente vazia. Teto de 3 níveis por vez, para uma recompensa mal configurada não transformar
   um recruta em lenda numa jornada só. Heróis gravados antes disso (meta zerada) são tratados.
2. ✅ **XP concedido em `JourneyManager.EndJourney`**: `60 + 8×trechos`, multiplicado pela corrupção
   da região (até +50%), 40% se a missão fracassa. **Do 5º herói da formação em diante, metade** —
   o mesmo limite a partir do qual a party come uma ração a mais por dia.
3. ✅ **Visível ao jogador**: barra e contagem de XP na ficha do herói (com campos opcionais, para
   não exigir edição manual da cena) e a lista "📈 Subiram de nível" no resultado da jornada.
4. ✅ **Correção de tabela encontrada aqui**: o retorno para casa fixava o HP em 60% do máximo,
   o que **rebaixava** quem voltasse melhor que isso. Virou piso, não teto.

**Consequência a vigiar:** a dificuldade das missões sai do nível médio do roster
(`QuestManager.GetPlayerAverageLevel`) e estava congelada, porque ninguém subia de nível. Agora ela
anda. Medir a cada ciclo.

#### Verificação executada (14/08)

| Prova | Resultado |
|---|---|
| Compilação por fora (Roslyn) | exit 0, só warnings antigos de campo do Inspector |
| `Tools ▸ Rodar Smoke Test` | **38 verificações, 0 falhas** — nada regrediu |
| Run de Play Mode | **"PLAY MODE OK — nenhum erro capturado"**, jornada completa, saída por vitória |
| XP no fluxo real | `Gromm: Nv.3 | XP 0 → 143/250` — bate com a fórmula (60 + 7×8, ×1,23 de corrupção); mortos não recebem |
| Pendência do handoff | **resolvida**: coleção do `DeckManager` em 4 itens, não mais zerada |
| Referências | todas ligadas; os 3 opcionais aparecem rotulados como opcionais |
| `TavernManager` | deixou de ser reportado como AUSENTE |

Também foram consertadas duas mentiras do próprio relatório, que corroíam a confiança nele: os
erros de DLL duplicada do pacote `realvirtual-MCP` faziam toda run abrir com "PLAY MODE COM 2
ERRO(S)" mesmo sem nada quebrado, e o `TavernManager` era dado como ausente três linhas antes de a
taverna abrir, contratar e renovar candidatos.

#### ⚠️ Achado novo da validação: o simulador ignora o combate da jornada

Duas runs seguidas de Play Mode terminaram com **3 mortes numa única jornada**, enquanto o smoke
test mede **0,54 mortes por jornada**. A causa não é azar: `SimulateJourneys` resolve eventos e
desgaste, mas **não trava os combates que a jornada contém** — eles são medidos à parte, em
`SimulateCombats`. E é o combate que mata: na run, `HP perdido em combate: 87` contra `40` fora dele.

Ou seja, o simulador da estrada erra por **dois** motivos somados, em direções opostas: mede sem as
cartas do jogador (o que superestima a letalidade) e sem os combates (o que a subestima muito mais).
Nenhum número de letalidade de jornada deve ser levado a sério antes de a Fase 2 corrigir os dois.

---

### Fase 1 — O herói progride
*Decisão já tomada pelo autor; nunca implementada. É o que faz o tempo passar dentro da guilda.*

1. **`HeroData`**: campos `xp` e `xpToNextLevel`, mais `AddXp(int)` devolvendo se subiu de nível.
   Curva sugerida: `100 + (nível−1)×75`.
2. **Ganho de XP em `JourneyManager.EndJourney`** (`:1080`, no laço dos sobreviventes):
   base por jornada concluída, escalada por dias percorridos e corrupção da missão; **metade** para
   quem morreu na estrada não recebe nada. Conforme decidido: **do 5º herói em diante o XP rende
   menos** — o mesmo critério de bloco de 4 já usado em `PartyFormation.DailyRations`.
3. **Subir de nível** aumenta `maxHp` pela mesma fórmula da `HeroFactory` e o salário; o resto já é
   automático — `DeckGenerator` lê `level` para tamanho de deck (`8+nível`) e libera raras no 3 e
   lendárias no 5.
4. **Mostrar na ficha** (`HeroDetailPanel`) a barra de XP e o próximo nível, e no relatório de fim de
   jornada quem subiu.

**Cuidado:** isso liga a escala de dificuldade que hoje está congelada
(`QuestManager.GetPlayerAverageLevel`). Rodar o smoke test antes e depois e comparar mortes/jornada —
missões vão ficar mais duras sozinhas.

---

### Fase 2 — A jornada vira decisão de verdade ✅ *concluída em 14/08*
*Aqui mora a resposta do autor: manter a escolha no chefe, rebalancear, e **mesclar cartas com o
sistema de decisão**.*

**Resultado, medido com a régua corrigida:**

| | antes | depois |
|---|---|---|
| mortes por jornada | 2,36 (alvo 0,33–0,67) | **0,44** |
| — vindas do chefe | 1,98 | 0,23 |
| — vindas da estrada | 0,30 | 0,19 |
| combates por jornada | 1,20 | **2,73** |
| cartas jogadas na estrada | 2,16 | 2,53 |
| eventos no acervo | 19 | **25** |
| eventos com caminho que exige carta | 0 | **25 de 25** |
| cartas | 16 | 17 |
| verificações do smoke test | 38 | **41**, 0 falhas |

O que foi feito, item a item:

1. ✅ **A régua, primeiro.** `SimulateJourneys` passou a jogar cartas e a travar os combates do
   caminho. Ver o achado abaixo — o erro mais grave estava na própria medição.
2. ✅ **Energia de combate 3 → 5 e mão 5 → 6.** Com 3 de energia e custo médio 2,25, o jogador via
   cinco cartas e jogava pouco mais de uma: o baralho era decoração. O valor foi escolhido por
   varredura, não por palpite, e **remedido depois de cada mudança de conteúdo** — com 1,2 lutas por
   jornada o ponto era 4; com 2,73, é 5.
3. ✅ **Nenhuma carta inerte na estrada.** `Ressurgir` (⚡4, prometia reviver e não fazia nada) ganhou
   o efeito `Revive` — tira da Beira da Morte e devolve metade da vida, sem contradizer a permadeath.
   `Flecha Precisa` virou `RemoveObstacle`. E `Intimidate`, o único jeito de evitar um combate, não
   existia em carta nenhuma: entrou **Brado de Guerra** (Guerreiro, Rara, ⚡2).
4. ✅ **Carta destrava escolha.** `EventOutcome` ganhou `requiredEffect` e `empoweredConsequences`.
   A opção com requisito aparece **travada e legível** — "🔒 Precisa de uma carta capaz de purificar"
   — e destrava quando a carta é jogada. Ela nunca some: o jogador precisa ver o que perdeu por não
   a ter trazido. Os 25 eventos ganharam esse caminho.
5. ✅ **Chefe rebalanceado, mantendo a escolha.** O ouro das saídas narrativas caiu para 40% (60% na
   que exige carta), o dano delas subiu 40%, e o ouro por vencer lutando **dobrou** (140→280 etc.).
   A melhor saída narrativa de cada chefe agora exige a carta temática do bioma.
6. ✅ **Seis encontros de combate novos**, um por bioma que não tinha nenhum. O acervo tinha **um**
   encontro fora o chefe; o combate quase não aparecia no jogo.

**Travas novas no smoke test:** toda carta faz algo na jornada (simétrica à de combate); todo
requisito de carta é satisfazível; todo evento tem ao menos uma saída sem exigir carta.

#### ⚠️ O achado: uma heurística com degrau invertia a medição

O simulador decidia se defender por um corte **fixo** (`danoAnunciado >= 12`). O chefe base bate 11:
o jogador simulado **nunca** se defendia dele, mas se defendia sempre de um chefe 30% mais forte.
Resultado: a varredura dizia que **quanto mais forte o chefe, mais o jogador vencia**.

Corrigido para um critério relativo (`>= max(6, menorHpVivo / 2)`), a curva ficou monotônica — e o
valor certo de energia caiu de 6 para 5. Um balanceamento inteiro havia sido calibrado contra um
adversário artificialmente incompetente. É o terceiro caso da mesma família neste projeto; o teste
de sanidade que passou a valer é: **varra a dificuldade e confira se a curva é monotônica.**

#### 🔶 Decisão pendente: o KPI de combate

O smoke test ainda acusa dois avisos: `encontros normais 100%` e `chefes 100%` de vitória, contra um
alvo de 35–75%. Isso **não** é balanceamento por fazer — é um alvo que não cabe neste jogo:

- A party só perde o combate quando os **quatro** caem, e a Beira da Morte segura cada um por um
  golpe. Derrota total é rara por construção.
- O que o jogador realmente perde é gente: 0,02 a 0,18 mortes por combate de chefe.
- O alvo de 35–75% veio do *Slay the Spire*, onde perder a luta encerra a run.

**Recomendação:** trocar o KPI de combate de *taxa de vitória* para *mortes por combate*, que é o que
o jogo cobra de fato. Precisa da sua decisão — está deixado como aviso, não como falha.

#### Verificação executada (14/08)

Smoke test: **41 verificações, 0 falhas**. Run de Play Mode: **"PLAY MODE OK — nenhum erro
capturado"**, jornada completa de 8 dias nas Ruínas.

| Prova | Antes da Fase 2 | Depois |
|---|---|---|
| Vezes que o combate apareceu numa jornada | 1 | **3** |
| Cartas jogadas em combate | 21 | **45** |
| Sobreviventes da party de 4 | 1 | **4** |
| XP no fluxo real | — | Gromm 124/250; **Sera subiu Nv.1 → Nv.2** |
| HP perdido em combate / fora | 87 / 40 | 52 / 0 |

#### 2.1 Cartas destravam e mudam escolhas

O modelo: cada opção de evento pode **exigir** ou **ser melhorada por** um efeito de carta jogado
naquele trecho.

- `EventOutcome` ganha `JourneyEffectType requiredEffect` (`None` = sem requisito) e um bloco
  `EventConsequences empoweredConsequences` opcional (o desfecho quando o requisito foi cumprido).
  Campos **no fim da classe** — os assets guardam ordem, e 19 eventos já existem.
- `JourneyManager` passa a registrar `HashSet<JourneyEffectType> playedThisEvent` em `PlayCard`
  (`:838`), zerado em `ShowEvent` junto de `currentMitigation`.
- `BuildChoices` (`:368`) desenha a opção com requisito **travada e legível** — "🔒 Precisa de: abrir
  caminho" — e a destrava assim que a carta é jogada. A opção nunca some: o jogador precisa ver o que
  perdeu por não ter levado a carta. É isso que transforma o deckbuilding em preparação.
- Onde o requisito for cumprido, `ChooseOutcome` resolve com `empoweredConsequences`.

Isso reaproveita o que já existe: `EventResolver.Resolve` continua sendo quem aplica, e a mitigação
por carta segue funcionando por cima.

#### 2.2 Nenhuma carta inerte na estrada

- Dar `journeyEffect` a **Ressurgir** e **Flecha Precisa**.
- Dar dono aos efeitos órfãos: **`Intimidate`** (evitar combate) precisa existir em alguma carta —
  candidata natural é uma carta de Guerreiro ou do futuro Bardo; `GainGold` e `ExtraRations` idem.
- **Trava no smoke test**, simétrica à de combate (`GuildSmokeTest.cs:152`): *"toda carta faz algo na
  jornada"*, e *"todo `JourneyEffectType` tem ao menos uma carta"*.

#### 2.3 O simulador passa a jogar cartas — e a lutar

Duas correções na mesma simulação, ambas confirmadas pela validação da Fase 1:

- Trocar o "jogador neutro" (`GuildSmokeTest.cs:319`) por um que gasta energia, escolhe carta pelo
  evento e acumula mitigação — as mesmas contas de `PlayCard`/`GetMitigationFor`, **chamando o
  código do `JourneyManager`** em vez de reescrever as regras.
- **Travar os combates dentro da jornada simulada**, reaproveitando `SimulateOneCombat`. Hoje a
  jornada e o combate são medidos em simulações separadas, e a jornada — onde o jogador de verdade
  perde mais HP no combate do que fora dele — sai muito mais leve do que é.

Enquanto isso não for feito, `mortes por jornada` mede um jogo que ninguém joga: sem as cartas do
jogador e sem as lutas do caminho.

#### 2.4 Rebalancear o chefe (mantendo a escolha)

- A saída narrativa passa a **custar** o que promete: ouro bem abaixo do combate, dano/estresse altos,
  `triggersCorruption` ligado, e a variante "aceitar o acordo" perde ouro em vez de ganhar.
- **Vencer lutando paga mais**: ouro do inimigo + bônus de chefe, e é o único caminho para a
  recompensa cheia da missão.
- As melhores saídas narrativas passam a **exigir carta** (§2.1) — negociar com A Coisa da Mata só
  com `Purify`, atravessar o Afogado só com `ProtectFromWeather`, etc. Deixa de ser "clicar na opção
  boa" e vira "ter trazido a carta certa".
- **Encontros comuns de combate precisam existir**: hoje há 1. Criar ao menos um por bioma jogável
  (7), usando os 6 inimigos comuns já prontos.

**Verificação:** smoke test com o novo simulador; comparar mortes/jornada e ouro/jornada antes e
depois; uma run de Play Mode confirmando que a opção travada aparece, destrava ao jogar a carta e
resolve pelo desfecho reforçado.

---

### Fase 2.5 — Bug de navegação, juice do combate e pós-jornada ✅ *14/08*

Pedidos do autor fora da ordem das fases, feitos entre a 2 e a 3.

#### O painel da sala que sumia

*"Ao entrar em alguma sala e voltar, o painel de descrição fica atrás de tudo e não dá pra clicar."*

Confirmado na cena: o painel é o irmão de índice **5** dentro de `Background`, e o `GuildMap` é o
**7**. Como `ShowGuildScreen()` chama `SetAsLastSibling()` no mapa toda vez que o jogador volta de
uma sala, o mapa passava a cobrir o painel — e a engolir os cliques. A **primeira** abertura
funcionava, a segunda não, e é por isso que nenhum teste tinha pego: todos abriam o painel uma vez só.

Corrigido com `SetAsLastSibling()` ao abrir — a mesma regra que o `UIManager` já aplica aos painéis
que passam por ele. O `PlayModeProbe` ganhou `TestLocationInfo`, que abre, visita uma sala, volta e
abre de novo, checando a ordem de irmãos e o raycast.

#### Combate: cada efeito funcionando e reagindo

Dois efeitos não faziam o que o nome dizia:

| | antes | depois |
|---|---|---|
| `Poison` | dano imediato e a palavra "envenenado" no log | pilhas que **cobram no fim de cada rodada** e se desgastam |
| `Debuff` | dano imediato e a palavra "enfraquecido" | reduz **40% do ataque** por 2+ turnos, e a intenção anunciada já mostra o número menor com 🔻 |

E o que acontecia em silêncio passou a aparecer: bloqueio ganho, estresse sofrido (era a única fonte
de dano do jogo sem reação visual — subia até o herói quebrar), ímpeto do grupo, carta reforçada,
limpeza de aflição, energia, compra e quebra de guarda. A view do inimigo mostra veneno e
enfraquecimento junto do bloqueio.

O simulador recebeu as mesmas regras — sem isso ele voltaria a medir um combate que o jogo não tem.

#### Pós-jornada, nas três etapas pedidas

1. **Tela de balanço** (`JourneyResultUI`) no lugar do popup de texto corrido: uma linha por herói
   com vida, estado, XP ganho e **barra de progresso**, promoções em verde, mortos em vermelho, e o
   ouro discriminado — contrato, sobreviventes, espólio das lutas e bônus da Biblioteca.
2. **Consequências que duram**: ferimento **não sara mais por sorteio** (era 40% de chance no
   caminho de volta, o que apagava o machucado sem ninguém cuidar dele); quem viu companheiro cair
   volta com luto; e herói acima de **85 de estresse recusa partir**, aparecendo bloqueado e com o
   motivo na tela de preparação. Isso dá função ao vinho do Mercado e à vigília do Cemitério.
   Para não virar beco sem saída, quem **fica** na guilda descansa a cada jornada concluída.
3. **Escolha de despojo**: ao vencer, o jogador escolhe entre ouro extra, noite na taverna
   (−30 de estresse), cuidados do curandeiro (trata os feridos) e contar a história (+15 de
   reputação). As duas do meio só aparecem quando há a quem servir. O botão de voltar fica travado
   até a escolha — a decisão é o momento, não um detalhe a passar batido.

#### Verificação executada (15/08) — `PlayModeReport.run43-fase25-validada.txt`

A run que faltava. **"PLAY MODE OK — nenhum erro capturado"**, jornada completa de 5 dias na Tundra,
4 sobreviventes de 4.

| Prova | Resultado |
|---|---|
| Painel da sala, 2ª abertura | `ordem entre irmãos: 7 de 7 — na frente`, alcançável pelo clique — **o bug reportado saiu** |
| Tela de balanço como saída da jornada | `jornada encerrada pela tela de balanço: '🏆 A GUILDA VOLTA VITORIOSA'` |
| Escolha de despojo | 2 opções, `botão de voltar travado até escolher: True`, `após escolher: botão liberado=True`, ouro 2855 → 2956 |
| Jornada termina sozinha | `painel ainda ativo ao fim: False (iterações: 134)` |
| Console | 0 erros, 0 exceções |

#### ⚠️ O achado: o instrumento envelheceu junto com a tela que ele mede

As duas primeiras tentativas de validar esta fase pararam no limite de segurança de 600 iterações,
com a jornada aparentemente sem fim — 305 eventos resolvidos numa jornada de 6 dias, party morta,
estresse em 92. Parecia regressão grave de letalidade. **Não era o jogo.**

A Fase 2.5 trocou a saída da jornada: onde havia `UIManager.resultPopup`, agora há a
`JourneyResultUI`. O laço da jornada no `PlayModeProbe` só sabia fechar os dois popups do
`UIManager` (`ClickPopupButton`). A jornada terminava certo, a tela de balanço abria — e o probe
ficava clicando um botão que não fazia nada até estourar o limite. As mortes e o estresse alto eram
efeito de centenas de eventos rodados **depois** de a jornada ter acabado.

Duas coisas esconderam a causa:

- **O detector de travamento era cego ao caminho que travava.** Ele contava cliques em opções de
  evento e nós de mapa, mas não no `endTurnButton` — e `EndTurn` é um no-op quando a jornada não
  espera escolha (`JourneyManager.cs:1048`). O laço girava sem nunca acusar.
- **O teste dedicado de fim de jornada passava**, porque *ele* conhece a tela de balanço. Só o laço
  da jornada automática não conhecia — os dois caminhos chegam à mesma tela por códigos diferentes.

Corrigido no probe: o laço fecha a tela de balanço (escolhendo o despojo, que trava o botão de
propósito), o `endTurn` conta como ação sem progresso, e o `DumpStuckState` passou a despejar a
máquina de estados inteira da jornada — `esperandoEscolha`, `escolhendoRota`, `jornadaEncerrada`,
dia, evento, camada do mapa, rotas e nós clicáveis. Foi essa linha que resolveu o caso:
`jornadaEncerrada=True` com o painel ainda ativo diz na hora que o fluxo terminou e a saída é que
não foi acionada. E se a tela de balanço abrir **sem** botão utilizável, agora o relatório acusa
`BUG: ... o jogador ficaria preso` — o caso real ficou detectável.

**A regra que fica:** ao trocar a tela por onde um fluxo termina, atualize quem o dirige
automaticamente. É a mesma família de erro do simulador que media uma jornada sem combates — o
instrumento e o jogo saem de sincronia em silêncio, e o número errado parece um achado.

### Fase 3 — A run: começo, relógio e fim

1. **`RunManager`** (novo, `DontDestroyOnLoad` como os outros): número do ciclo, corrupção global,
   estado da run, e o evento `onCycleAdvanced`. Um ciclo = uma jornada concluída.
2. **Corrupção global como relógio.** Sobe por ciclo e com eventos `triggersCorruption`. Consequências
   concretas, todas em cima de código existente:
   - `QuestGenerator` sorteia `corruptionLevel` a partir do medidor global, não de `Random.Range(0,100)`;
   - biomas de baixa corrupção "secam" e saem do sorteio de missão;
   - `EventPool` já filtra por `minCorruptionToAppear` — os eventos duros entram sozinhos.
3. **Fim de run**, com as três condições do GDD: sem heróis vivos **e** sem ouro para recrutar;
   reputação em 0; corrupção global no máximo. É aqui que a reputação finalmente pesa.
4. **Vitória**: plugar `QuestGenerator.GenerateBossQuest` — o Chefe Supremo aparece no quadro quando
   a corrupção global passa de um limiar.
5. **Tela de fim de run** com o balanço (ciclos, mortos, ouro, chefe derrotado) e o botão de nova run.
6. **Meta-progressão**: moeda persistente ganha por desempenho, gasta entre runs para destravar
   cartas no pool da Biblioteca, classes iniciais (Ladino/Bardo) e bônus de guilda. **Depende do
   save** — construir a moeda e os destraves quando o pacote de save estiver no projeto.

---

### Fase 4 — Dar peso ao que já está escrito
*Barato, porque os dados já existem e só falta quem os leia.*

- **`corruptionExposure`**: acima de 50, o herói ganha traço negativo ao voltar; acima de 80, risco de
  "virar" e sair do roster. Já acumula, ninguém lê.
- **Traços sem efeito**: `Drunkard` (gasta ouro extra ao voltar, ou começa a jornada com estresse),
  `Scarred` (resiste a estresse, apanha mais).
- **Personalidades sem efeito**: `Ambitious` (exige mais salário/recompensa), `Stubborn` (recusa
  reposicionamento na formação).
- **Estados mentais cosméticos**: `Fearful`, `Paranoid`, `Irrational`, `Courageous`, `Focused`,
  `Vigorous` — 6 dos 9 só têm rótulo. `Focused` (energia extra) e `Vigorous` (cura na estrada) são os
  mais fáceis e os que mais aparecem.

---

### Fase 5 — Backlog de conteúdo e apresentação
*Fora do ciclo atual; listado para não se perder.*

- Cartas de **Ladino** e **Bardo**; ampliar de 4 para 6–8 cartas por classe (com 4 cartas o "deck
  híbrido" é só cópias).
- Eventos para **Deserto, Tundra e Vulcão** (1 cada hoje) e **chefes** para esses 3 biomas.
- Mais inimigos comuns (6 hoje) e variação de comportamento por bioma.
- **Arte**: retratos, ilustrações de carta e inimigo — segue placeholder.
- **Áudio**: não existe nada. O kit Bloodlines já traz `SoundManager`, `ButtonSFX` e `ToggleSFX`.
- Barras de HP/estresse com os sprites de `Progress_Bar/` do kit.
- Menu principal / tela de título (hoje só `SampleScene`).

---

## 4. Como verificar cada passo

| Ferramenta | Para quê | Custo |
|---|---|---|
| Compilar por fora (Roslyn do Unity, receita no `HANDOFF.md`) | erro de sintaxe antes de abrir o Editor | segundos |
| `Tools ▸ Guild of Legends ▸ Rodar Smoke Test` | 38 verificações + 200 jornadas + 800 combates; mortes/jornada, vitória de chefe | ~1 min |
| `RunPlayModeTest.trigger` → `PlayModeReport.txt` | telas alcançáveis por raycast, jornada completa, console limpo | ~2 min |

**Regras que já custaram caro** (do handoff — valem para todo este plano):
- Play Mode com compilação quebrada roda o **assembly antigo**: conferir o console antes de ler o relatório.
- **Não** chamar `refresh_unity` depois de criar o gatilho.
- Campo público é serializado: mudar o valor no script **não** muda a cena. Alterar os dois.
- Uma run de Play Mode é n=1 — balanceamento se mede no simulador.
- Acrescentar valor de enum **só no fim**: os assets guardam o número.

---

## 5. Ordem recomendada

```
Fase 0 (limpeza)  →  Fase 1 (XP)  →  Fase 2 (carta ⨯ escolha, chefe, simulador)
                                          ↓
                     Fase 3 (run, corrupção global, fim de run)
                                          ↓
                     Fase 4 (peso) e Fase 5 (conteúdo/arte/áudio)
```

Fase 0 e 1 são independentes e podem ir juntas. A Fase 2 é a que mais muda a experiência: hoje o
jogador pode terminar uma jornada inteira sem que o baralho dele importe. A Fase 3 depende da Fase 1
(sem progressão, uma run não tem arco) e a meta-progressão dentro dela depende do pacote de save.
