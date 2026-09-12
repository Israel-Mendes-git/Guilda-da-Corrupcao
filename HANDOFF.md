# Handoff — Guilda da Corrupção: a estrada com luta, as cartas fora do combate e a regra de cada área (2026-09-13)

## Objetivo

Desenvolver o **Guilda da Corrupção** (Unity 2022.3.62f3): roguelike deckbuilder + gerência de
guilda, mistura declarada de *Darkest Dungeon* com *Slay the Spire*.

Projeto: `C:\Users\Israel\Documents\GitHub\Guilda-da-Corrupcao`.

Plano e histórico em **[`ROADMAP.md`](ROADMAP.md)** (a fase 3.16 é a última), design em
**[`GDD.md`](GDD.md)** (versão 2.4), o mundo em **[`MUNDO.md`](MUNDO.md)**, arte pendente em
**[`ARTE.md`](ARTE.md)**. Leia os quatro — este handoff só cobre o que eles não contam.

---

## Estado atual

**Os três itens que o autor escolheu em 12/09 estão construídos, medidos e commitados** (fase 3.16
do ROADMAP): a estrada com metade dos pontos em luta, os três usos do baralho fora do combate, e a
regra própria das sete áreas. **Nenhum desenho foi perguntado antes** — a regra dele para
implementação é decidir o escopo e ver na tela —, e por isso **todos os números são meus e
vetáveis**. A lista do que ele pode vetar está em "Pendências que dependem do autor".

**Provas, todas verdes** (12/09, 20:38 em diante):

| Prova | Resultado |
|---|---|
| `SmokeTestReport.txt` | **71 verificações, 0 falhas** · letalidade **0,52** (alvo 0,33–0,67) · 5,87 lutas, 5,29 paradas de texto e 1,65 descansos por jornada · primeira jornada com dois fundadores **0,14** (teto 0,35) |
| `GameplayReport.txt` | entra 567, folha −203, **sobra 365** por jornada · 42% do catálogo ao fim da run · a Forja nível 1 passou a valer −0,17 morte por jornada (era −0,04) |
| `PlayModeReport.txt` | **PLAY MODE OK** · a compra paga com carta, a carta do escrito no baralho, a regra da área no alto da estrada, 6 contadores no topo (eram 11) |

**Não commitado:** nada. O `Assets/Fonts/SegoeUIEmoji SDF.asset` volta a mudar a cada Play Mode
(4 MB de diff); reverta antes de qualquer commit: `git checkout -- "Assets/Fonts/SegoeUIEmoji SDF.asset"`.

### O que existe desde 13/09 (resumo; detalhe no ROADMAP 3.16)

- **A estrada:** o sorteio decide primeiro se o ponto é luta (`EventPool.FracaoDeCombate = 0,5`);
  o topo mostra dia, rações, tochas, energia e a regra da área (`AreaRules.Lembrete`), e o resto
  aparece com a mão; o texto sai a 0,008s por letra; **o descanso cura** (+20% de vida, −8 de
  estresse, `JourneyManager.Descansar`) — foi o que trouxe a letalidade de 1,52 para 0,52.
- **Carta no lugar de ouro** (`CardPayment`): só quando o ouro não cobre, vale o preço da
  Biblioteca, sem troco, baralho nunca abaixo de 8. Cinco salas passam por `CardPayment.Cobrar`.
- **A carta do escrito** (`Resources/Escritos`, 7 assets; `Escritos.Carta/Entregar/Portador`):
  `JourneyEffectType.Conter` na estrada (o +4 da visita não acontece) e `Debuff` no combate; entra
  no baralho de quem está na mesa da Biblioteca ao traduzir.
- **Selar queima cartas** (`AreaCatalog.Ficha.seloPede/seloCobra`, `CardPayment.MontarQueimaDoSelo`):
  o balanço da volta pede a escolha antes do despojo; o preço está na ficha do mapa desde o ciclo 1.
- **As sete regras** (`AreaRules`, lidas por `JourneyManager`, `CombatManager`, `EnemyPool` e
  pelo simulador). A exposição à corrupção passou a valer na volta (traço ≥ 50, virar ≥ 80). O
  tributo do Cemitério mora em `GuildManager.honrados` e no save.

---

## Próximos passos

Nenhum foi começado. Em ordem do que mais pesa:

1. **Os vetos do autor** sobre os números de 13/09 (abaixo). Cada um é uma constante em
   `AreaRules`, `CardPayment` ou `EventPool`, e o smoke test mede em segundos.
2. **Os atos dos selos** — a única parte do item 3 que ficou de fora: devolver os mortos pelo nome,
   escolher entre fogo e resgate, acordar o dragão e sair, quebrar o altar, apagar o fogo, pagar a
   última pergunta. Hoje todo selo é a luta de sempre seguida da queima de cartas. **Depende de
   decisão dele:** quantos passam por uma luta antes do ato.
3. **O Covil é a área mais mortal** (1,29 mortes por jornada, contra 0,19 na Cripta). É a área de
   força mais funda e a única onde as relíquias se acumulam; se ele achar demais, os números são
   `AreaRules.CovilDespertarMaximo` (20) e `CovilEscuridao` (1,5).
4. **O texto das cartas de escrito e das páginas** — world building dele. Os assets têm nome
   estrutural ("Escrito da Mata") e descrição funcional.
5. **Requisitos de missão aleatórios** (`QuestGenerator.GenerateRequirements`) continuam sem
   ligação com o lugar; agora que cada área tem regra, podem nascer dela.

### O que ficou em aberto e não é dos três

- **A curva da Forja** (o nível 1 entrega a maior parte do ganho) e **o Cofre do Santuário**
  (+120 por nível) — decisões de design, sem mudança desde 12/09.
- **A folha:** sobra 365 por jornada com quatro heróis; 42% do catálogo ao fim.
- **A primeira jornada:** 0,14 mortes por jornada, teto 0,35.

---

## Decisões tomadas (e por quê)

- **Todas as de 13/09 estão na memória `regras-de-area-e-cartas-decisoes-13-09`**, marcadas como
  minhas. As que mais merecem veto: metade dos pontos em luta; carta sem troco e só sem ouro;
  papel e quantidade que cada selo queima; o descanso que cura 20%; a cópia da Torre no lugar de
  um inimigo comum; o dragão em 20 de despertar; virar em 35% acima de 80 de exposição.
- **Por que o descanso cura:** com o dobro de lutas e nenhuma recuperação entre elas, as expedições
  longas (Torre, Covil) ficavam em 1,9 a 2,7 mortes por jornada mesmo depois de enfraquecer a
  cópia e adiar o dragão. O botão já existia e já custava um dia de mantimentos; é a fogueira do
  *Slay the Spire*, e o simulador descansa quando o grupo está abaixo da metade da vida.
- **Por que o pagamento com carta só sem ouro:** com ouro na mesa, enfraquecer o baralho para
  poupar moeda não é decisão que alguém tome; sem ouro, é a pergunta inteira.
- **Por que as cartas de escrito moram fora de `Resources/Cards`:** tudo o que carrega "Cards"
  (gerador de baralhos, Biblioteca, editor de baralhos, smoke test) passaria a sortear e vender o
  escrito. Só o índice do save (`DeckRepository`) as conhece.
- **O portador do escrito é derivado do baralho**, sem campo novo no save: morreu, a carta some com
  o baralho dele e a Biblioteca oferece ENTREGAR de novo.

---

## Pegadinhas / lições desta sessão

- **A média escondia a causa.** A primeira medição deu 1,52 mortes por jornada e só o detalhamento
  **por área** (novo no smoke test) mostrou Torre 2,7 e Covil 2,5 contra Mata 0,3. Antes de mexer
  numa régua global, olhar a linha "por área".
- **Heredoc do Bash quebra com aspas simples ou barras** nesta máquina (memória
  `heredoc-bash-colapsa-barras-no-windows`): scripts de patch vão por Write em `Temp/` e `python`.
- **`const` inlina:** `AreaRules.CovilDespertarMaximo` é `const` e quem a lê está no mesmo
  assembly — recompila junto. Para varrer sem recompilar, use campos (`EventPool.FracaoDeCombate`,
  `EnemyPool.TerceiroInimigoAPartirDe` são `static`, não `const`, por isso).
- **`execute_code` com quatro cenários de 500 jornadas devolve `success:false` sem mensagem** —
  é timeout. Um cenário de 400 por chamada passa.
- **A regra da área no alto da estrada é escrita em `UpdateQuestInfo` e em `ShowEvent`:** só no
  segundo, o primeiro quadro ainda mostrava "🏚️ Pântano" e o teste pegou.
- **Emoji novo entra no atlas da fonte** (memória `fonte-emoji-atlas-tmp`): 🌿 e 🪞 não existiam
  no jogo e foram trocados pelos glifos de aspecto que já existem.
- Seguem valendo: Play Mode com compilação quebrada roda o assembly antigo; campo público é
  serializado (o `textTypeSpeed` foi escrito pelo `GuildSceneSetup`); uma run de Play Mode é n=1;
  a fonte de emoji muda a cada Play Mode; mensagem de commit com aspas quebra a here-string (use
  `git commit -F`); olhe as capturas — foi a captura, não o relatório, que mostrou a regra da área
  atropelando os contadores do baralho.

---

## Arquivos e comandos relevantes

### Onde entrar

| Arquivo | Papel |
|---|---|
| `Assets/Scripts/Core/AreaRules.cs` | as sete regras: constantes e contas; `Lembrete` é a linha do topo |
| `Assets/Scripts/Core/CardPayment.cs` | carta no lugar de ouro (valor, candidatas, painel) e a queima do selo |
| `Assets/Scripts/Core/Escritos.cs` | a carta do escrito: `Carta`, `Entregar`, `Portador`, `LidosSemPortador` |
| `Assets/Resources/Escritos/` | os 7 assets de carta de escrito (nome sem acento é caminho de `Resources.Load`) |
| `Assets/Scripts/Core/JourneyManager.cs` | a estrada: HUD, Forja/Oráculo na caixa, Aldeia após a luta, Covil, manutenção, a volta (exposição, selo) |
| `Assets/Scripts/Core/EnemyPool.cs` | `DaArea` (cópia da Torre, morto da Cripta), `CopiaDoHeroi`, réguas do encontro |
| `Assets/Scripts/Core/CombatManager.cs` | `ErguerUmMorto` (Cripta) |
| `Assets/Scripts/Core/EventPool.cs` | `FracaoDeCombate` |
| `Assets/Scripts/UI/JourneyResultUI.cs` | `PreencherQueima`: o selo cobra antes do despojo |
| `Assets/Scripts/Core/GuildSmokeTest.cs` | o simulador com as regras das áreas, descanso, mortes por área |

### Gatilhos (arquivo vazio na raiz, consumido pelo Editor ao ganhar foco)

`RunPlayModeTest` · `RunSmokeTest` · `RunGameplayAudit` · `RunSceneSetup` · `RunMenuSetup` ·
`RunGuildArt` · `RunCardCreator` · `RunCardArt` · `RunEventArt` · `RunEnemyArt` · `RunMapArt` ·
`RunItemArt` · `RunEventBalance` · `RunBarSkin` · `RunPortraitCatalog` · `RunBiomeArt` ·
`RunUiSkinPrefabs` · `RunHeroPanelSkin` · `RunPartyCardSkin` · `RunCardFrameSkin` · `RunAudioCatalog`
— todos `.trigger`, todos no `.gitignore`.

**Depois de mexer em código** (o Editor precisa estar aberto no projeto; `RunSceneSetup` só quando o
`GuildSceneSetup` mudou; o gatilho salva a cena sozinho):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -Command "& './unity-run.ps1' -Triggers @('RunSceneSetup.trigger','RunSmokeTest.trigger','RunGameplayAudit.trigger','RunPlayModeTest.trigger')"
```

**Varrer um número sem recompilar** (Editor fora do Play Mode, DLL assentada), pelo MCP
`execute_code` — um cenário por chamada:

```csharp
EventPool.FracaoDeCombate = 0.4f;
var s = GuildSmokeTest.MedirJornadas(400);
return $"{s.mortesPorJornada:F2} | " + string.Join(" | ", s.mortesPorArea.Select(k => $"{k.Key} {k.Value:F2}"));
```

### Os quatro relatórios

| Arquivo | Quem gera | O que responde |
|---|---|---|
| `SmokeTestReport.txt` | `RunSmokeTest` | O jogo está de pé? 71 travas + letalidade em 1000 jornadas, por área, + a primeira jornada |
| `PlayModeReport.txt` | `RunPlayModeTest` | As telas funcionam? Console limpo, a compra com carta, a carta do escrito, a regra da área |
| `GameplayReport.txt` | `RunGameplayAudit` | O jogo é um jogo? Impacto de cada sistema, o que entra e sai por jornada |
| `Assets/Screenshots/*.png` | `RunPlayModeTest` | O que o jogador vê — `pagar_com_carta.png` e `jornada_mao.png` são as novas |

### Compilar sem abrir o Editor (segundos)

Receita na memória `validar-compilacao-sem-abrir-unity`. O `build.rsp` da raiz **não lista** os
arquivos novos (`AreaRules.cs`, `CardPayment.cs`): copie para `Temp/build.rsp` e acrescente os dois
antes de rodar. Aviso esperado: `StatusEffectsPreview.spawnAuraCO`.

### Onde ficam os saves

`C:\Users\Israel\AppData\LocalLow\Rapadura Atômica\Guilda da Corrupção\` — `saves\*.json` e
`profile.json`. O formato ganhou `GuildSave.honored` e `HeroSave.corruptedGear` em 13/09 (save
antigo lê vazio e 0).

---

## Pendências que dependem do autor

- **Vetar ou não, de 13/09:** metade dos pontos em luta · carta sem troco e só sem ouro · o que cada
  selo queima (papel e quantidade) · o descanso que cura 20% · o Covil em 1,29 mortes por jornada ·
  virar em 35% acima de 80 de exposição · a cópia da Torre no lugar de um inimigo comum.
- **Vetar ou não, de 12/09:** a semana que passa na Taverna; armadura 150; poções 40–50.
- **Os atos dos selos:** quantos passam por uma luta antes do ato.
- **Texto** das cartas de escrito e das páginas; **nomes de trabalho** das sete áreas, do chefe do
  Covil, das 23 cartas de 26/08.
- **Cinco de sete áreas por partida** — proposto em `MUNDO.md`, não decidido.
- **Push** — os commits seguem só no repositório local.
- **Arte** — ver `ARTE.md`. **Ladino e Bardo** seguem sem carta. **Git LFS** — 763 MB de binários.
