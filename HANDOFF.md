# Handoff — Guilda da Corrupção: a fundação (2026-09-12, noite)

## Objetivo

Desenvolver o **Guilda da Corrupção** (Unity 2022.3.62f3): roguelike deckbuilder + gerência de
guilda, mistura declarada de *Darkest Dungeon* com *Slay the Spire*.

Projeto: `C:\Users\Israel\Documents\GitHub\Guilda-da-Corrupcao`.

Plano e histórico em **[`ROADMAP.md`](ROADMAP.md)** (fase 3.14 é esta sessão), design em
**[`GDD.md`](GDD.md)** (versão 2.3), o mundo em **[`MUNDO.md`](MUNDO.md)**, arte pendente em
**[`ARTE.md`](ARTE.md)**. Leia os quatro — este handoff só cobre o que eles não contam.

---

## Estado atual

**Tudo commitado e verificado.** Um commit nesta sessão: *a guilda nasce vazia, as portas acendem
por motivo e o mapa é a preparação*. Ele fecha a rodada de apresentação pedida pelo autor em 12/09.

**Provas, todas verdes:**

| Prova | Resultado |
|---|---|
| `SmokeTestReport.txt` (12/09 18:51) | **63 verificações, 0 falhas** · letalidade **0,59** (alvo 0,33–0,67) · primeira jornada com dois fundadores **0,06** (teto 0,35) |
| `PlayModeReport.txt` (12/09 18:53) | **PLAY MODE OK — nenhum erro capturado** · seção nova "A FUNDACAO" sem FALHA · partir pelo mapa em 3 cliques |
| `Assets/Screenshots/fundacao_*.png` | a guilda escura com a Taverna pulsando; a Taverna com os quatro fundadores; a guilda fundada com três portas acesas; o mapa com Partir |

**Não commitado:** nada. O `Assets/Fonts/SegoeUIEmoji SDF.asset` volta a mudar a cada Play Mode
(4 MB de diff); reverta antes de qualquer commit: `git checkout -- "Assets/Fonts/SegoeUIEmoji SDF.asset"`.

### O que mudou de estrutural

**A guilda nasce vazia e o jogador escolhe dois fundadores.** `GuildManager` não cria mais elenco;
`EmFundacao` vale enquanto o roster tem menos de 2, ninguém morreu e o ciclo é 0. A Taverna, nesse
estado, mostra um candidato por classe jogável (`HeroFactory.ClassesJogaveis`), todos
`NivelDosFundadores` (2), e `AceitarFundador` entra sem cobrar. O ouro de fábrica caiu de 500 para
100 (`MetaProgression.OuroBasePorRun`), para a Forja não ter motivo antes da primeira saída.

**As portas têm três luzes.** `GuildGuide` continua escolhendo a porta que pulsa, e agora pergunta a
cada porta se há motivo (`TemMotivo`): acesa quando há, escura quando não — mas escura abre. O
brilho é aplicado ao cenário e ao rótulo, **não** ao fundo da porta (ver pegadinhas). O painel de
"deseja entrar?" da primeira visita saiu (`MapManager.ShowLocationInfo` entra direto).

**A Corrupção está no rodapé.** `Assets/Scripts/UI/RelogioDaGuilda.cs` (arquivo novo) nasce em
execução abaixo do `Txt_Gold` do `Panel_DownBar`, criado pelo `GuildGuide.Start`.

**O mapa é a preparação.** `QuestSelectionUI.SelectQuest` chama `MontarGrupoPadrao` (os aptos em
`PartyFormation.OrdemRecomendada`, até 4; o baralho de quem lidera) e escreve "Quem vai" na ficha.
`Btn_Partir` é um clone do botão de avançar, criado em `GarantirBotaoDePartir`; o de avançar virou
"Ajustar" e abre os passos 2 e 3 de sempre. Mexer neles liga `grupoAjustado`, e aí apontar outro
destino não refaz o padrão.

**Formação e encontro acompanham o grupo.** `PartyFormation.FrontSlotsFor(n)` = metade da fila,
até 2 (conta o grupo inteiro, mortos inclusive, para o grupo de 4 não mudar). `EnemyPool.GetLineup`
recebe o tamanho do grupo e encolhe o encontro na proporção (`ParaOGrupo`); `JourneyManager` e o
simulador passam `party.Count`.

---

## Próximos passos

1. **O autor jogar a fundação** e dizer duas coisas: se a primeira jornada está branda demais
   (0,06 mortes por jornada, contra 0,59 da média — os números a mexer são
   `TavernManager.NivelDosFundadores` e `EnemyPool.ParaOGrupo`), e se a luz das portas lê bem na
   tela dele (escura em 30%, acesa em 82%, pulsando em 100%: `GuildGuide.BrilhoEscuro/BrilhoAceso`).

2. **Fase 3.12, passo 4 — a economia para de saturar.** Ganhou urgência: com 100 de ouro inicial a
   curva de entrada mudou, e a auditoria (`RunGameplayAudit.trigger` → `GameplayReport.txt`) ainda
   não foi rodada com o número novo. Os números a mexer seguem sendo o espólio da expedição
   (`QuestGenerator.GerarExpedicao`) e o prêmio das encomendas (`Encomendas.Sortear`).

3. **A regra própria de cada área** (`MUNDO.md`): segue escrita e não construída.

---

## Decisões tomadas (e por quê)

Todas do autor, em duas rodadas de perguntas com opções; a lista completa, com o que foi rejeitado,
está na memória `fundacao-da-guilda-decisoes`.

- **Começar na guilda, não na estrada.** A proposta recomendada era abrir a partida já na Mata, no
  espírito da Estrada Velha do DD; ele preferiu *"pensar o começo do zero"* e *"começar na guilda"*,
  sem os quatro heróis prontos.
- **O jogador escolhe dois fundadores** — não dois fixos, nem nenhum, nem três.
- **Sala sem motivo fica escura, mas clicável.** Trancar foi rejeitado: mentiria sobre o que existe.
- **A primeira jornada é a Mata comum, com inimigos pelo grupo.** Rejeitadas uma "beira da Mata"
  curta e a Mata sem ajuste.
- **O mapa é a preparação**, com Partir e Ajustar.
- **Nenhuma sala se funde** (*"somente aperfeiçoamento em cada uma delas"*), e **a tela da guilda
  continua a guilda** — nem vira o mapa-múndi, nem a porta da Jornada vira mapa em miniatura.
- **A Corrupção no rodapé** estava na base proposta e não foi objetada.

---

## Pegadinhas / lições desta sessão

### O Button repinta o alvo dele, e o recuo de 72% nunca apareceu

A versão anterior escurecia as outras seis portas tingindo o `Image` raiz de cada uma — que é o
`targetGraphic` do `Button`. O `ColorTint` do Button reescreve a cor do alvo a cada entrada e saída
do ponteiro e ao ativar, e o tom voltava a branco. Na captura de 11/09 as sete portas estavam
iguais. **Tinja os filhos** (a cena e o rótulo), nunca o alvo do Button. `GuildGuide.Iluminar` guarda
a cor de fábrica de cada gráfico e multiplica por ela, porque a cena já nasce com um véu escuro.

### O rodapé não é parte do painel da guilda

`ui.guildPanel` é o `GuildMap`; o `Panel_DownBar` é irmão dele. Contar conceitos "na Guilda" sem o
rodapé deixava de fora o atalho de Baralhos e, agora, o relógio — por isso a Guilda saltou de 0 para
2 conceitos no relatório. `MedirPrimeiroContato` passa os dois painéis.

### `unity-run.ps1` do bash: o array vira um nome só

`powershell -File ./unity-run.ps1 -Triggers "A.trigger","B.trigger"` cria **um** arquivo chamado
`A.trigger,B.trigger`, que o Editor nunca consome; o script espera o relatório até o timeout. A
forma certa: `powershell -NoProfile -ExecutionPolicy Bypass -Command "& './unity-run.ps1' -Triggers @('A.trigger','B.trigger')"`.
Com o título da janela certo, smoke test + Play Mode levaram três minutos.

### Compilar por fora: a lista de fontes tem de vir do csproj

Só `Assets/Scripts` dá `CS0246: SPUM_Prefabs` (pacotes fora da pasta fazem parte do
Assembly-CSharp), e sem os `<ProjectReference>` os exemplos do ESave acusam `SaveFile`. O
`build.rsp` desta sessão (regenerado a cada vez; está no `.gitignore`) usa `<Compile Include>` mais
os `.cs` novos, e `-r:Library/ScriptAssemblies/<ref>.dll` para cada ProjectReference. Receita
completa na memória `validar-compilacao-sem-abrir-unity`.

### O probe repõe o elenco de sempre depois da fundação

A guilda nasce vazia, e todas as seções antigas do relatório presumem Gromm, Lyra, Finn e Sera.
`TestarFundacao` roda primeiro, e `RepovoarElencoClassico` põe os quatro direto no roster. **"O
PRIMEIRO CONTATO" mudou de lugar**: vem depois de "SAVE E MENUS", porque a última coisa daquela seção
é fundar uma guilda nova, e é nela que o caminho de quem chega se mede.

### Outras, que seguem valendo

- **Play Mode com compilação quebrada roda o assembly antigo** — confira o console antes de ler o
  relatório.
- **`Image.type = Filled` é ignorado sem sprite** — use `UIUtil.Branco()` em barra criada em execução.
- **`Instantiate` não copia ouvintes de execução** — o clone do botão de avançar nasce sem o
  `AddListener` do original, e é por isso que `Btn_Partir` pode ser clonado com segurança.
- **Quem nasce depois é desenhado por cima.** `Btn_Partir` é último irmão do passo 1; o mapa é o
  primeiro.
- **Campo público é serializado**: mudar o valor no script não muda a cena — altere os dois.
- **Acrescentar valor de enum só no fim**: os assets guardam o número.
- **Uma run de Play Mode é n=1** — balanceamento se mede no simulador.
- **A fonte de emoji é reescrita a cada Play Mode** — reverta antes de commitar.
- **Mensagem de commit com aspas quebra a here-string do PowerShell**: use `git commit -F arquivo.txt`.
- **Sempre olhe `Assets/Screenshots/*.png` depois do Play Mode.**

---

## Arquivos e comandos relevantes

### Onde mora a fundação

| Arquivo | Papel |
|---|---|
| `Assets/Scripts/Core/GuildManager.cs` | `EmFundacao`, `FundadoresDaGuilda`, `AceitarFundador`, `MarcarComoCarregada` |
| `Assets/Scripts/Core/TavernManager.cs` | `GerarFundadores`, `NivelDosFundadores`, o botão "FUNDAR COM …" |
| `Assets/Scripts/UI/GuildGuide.cs` | `TemMotivo`, `Iluminar`, `Luz`, `NivelDe`, `LuzDasPortas` |
| `Assets/Scripts/UI/RelogioDaGuilda.cs` | o relógio de Corrupção do rodapé (novo) |
| `Assets/Scripts/UI/QuestSelectionUI.cs` | `MontarGrupoPadrao`, `QuemVai`, `GarantirBotaoDePartir`, `ResumoDaRota` |
| `Assets/Scripts/Core/PartyFormation.cs` | `FrontSlotsFor`, `IsFront`, `OrdemRecomendada` |
| `Assets/Scripts/Core/EnemyPool.cs` | `GetLineup(…, partySize)`, `ParaOGrupo` |
| `Assets/Scripts/Core/MetaProgression.cs` | `OuroBasePorRun = 100` |

### Gatilhos (arquivo vazio na raiz, consumido pelo Editor ao ganhar foco)

`RunPlayModeTest` · `RunSmokeTest` · `RunGameplayAudit` · `RunSceneSetup` · `RunMenuSetup` ·
`RunGuildArt` · `RunCardCreator` · `RunCardArt` · `RunEventArt` · `RunEnemyArt` · `RunMapArt` ·
`RunItemArt` · `RunEventBalance` · `RunBarSkin` · `RunPortraitCatalog` · `RunBiomeArt` ·
`RunUiSkinPrefabs` · `RunHeroPanelSkin` · `RunPartyCardSkin` · `RunCardFrameSkin` · `RunAudioCatalog`
— todos `.trigger`, todos no `.gitignore`.

**Depois de mexer em código** (não foi preciso `RunSceneSetup` nesta sessão: tudo o que a tela
ganhou nasce em execução):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -Command "& './unity-run.ps1' -Triggers @('RunSmokeTest.trigger','RunPlayModeTest.trigger')"
```

**Se o `unity-run.ps1` falhar** (título da janela ou timeout de foco), use o MCP: `refresh_unity`
com `compile: request` e `wait_for_ready: true`, confira que
`find Assets/Scripts -name "*.cs" -newer Library/ScriptAssemblies/Assembly-CSharp.dll` vem vazio, e
então `touch RunPlayModeTest.trigger`.

### Os quatro relatórios

| Arquivo | Quem gera | O que responde |
|---|---|---|
| `SmokeTestReport.txt` | `RunSmokeTest` | O jogo está de pé? 63 travas + letalidade em 1000 jornadas, e a primeira jornada dos fundadores |
| `PlayModeReport.txt` | `RunPlayModeTest` | As telas funcionam? Console limpo, **a fundação**, diário da partida, o primeiro contato em conceitos |
| `GameplayReport.txt` | `RunGameplayAudit` | O jogo é um jogo? Impacto de cada sistema, economia, ritmo — **desatualizado** desde o ouro inicial 100 |
| `Assets/Screenshots/*.png` | `RunPlayModeTest` | O que o jogador vê — a única prova que pega "dado certo, exibição ausente" |

### Compilar sem abrir o Editor (segundos)

Receita na memória `validar-compilacao-sem-abrir-unity`, atualizada nesta sessão com a lista de
fontes do csproj e os ProjectReferences. Aviso pré-existente esperado: `StatusEffectsPreview.spawnAuraCO`
nunca usado; `goldtxt`, `reputationtxt` e `currentPopupCG` são filtrados.

### Onde ficam os saves

`C:\Users\Israel\AppData\LocalLow\Rapadura Atômica\Guilda da Corrupção\` — `saves\*.json` e
`profile.json`. Um save feito durante a fundação tem roster vazio; `GameStateIO.Aplicar` marca a
guilda como carregada para o `Start` não pôr o ouro de fábrica por cima.

---

## Pendências que dependem do autor

- **A primeira jornada está branda demais?** 0,06 mortes por jornada, 97% de sobrevivência.
- **Push** — os commits seguem só no repositório local.
- **Os nomes das sete áreas** são de trabalho (`MUNDO.md` os marca como descartáveis).
- **O nome do chefe do Covil**: hoje é "O Gigante de Pedra", herdado da Montanha, e o Covil é de um
  dragão.
- **Cinco de sete áreas por partida** — proposto em `MUNDO.md`, não decidido.
- **O que a saída da Abadia deixou em aberto**: o descanso fundo fora da guilda e o único
  esconderijo confiável ficaram sem dono.
- **Nomes das 23 cartas novas** e o nome do "Tratamento" do Mercado.
- **Arte** — ver `ARTE.md`: 285 peças, e nada do que está na tela fica.
- **Ladino e Bardo** seguem sem carta nenhuma.
- **Git LFS** — 763 MB de binários no histórico.
- **ESave e Bench**: importados, commitados e não usados.
