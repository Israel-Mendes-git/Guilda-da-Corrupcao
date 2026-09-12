# Handoff — Guilda da Corrupção: a fundação, a folha e os becos (2026-09-12, noite)

## Objetivo

Desenvolver o **Guilda da Corrupção** (Unity 2022.3.62f3): roguelike deckbuilder + gerência de
guilda, mistura declarada de *Darkest Dungeon* com *Slay the Spire*.

Projeto: `C:\Users\Israel\Documents\GitHub\Guilda-da-Corrupcao`.

Plano e histórico em **[`ROADMAP.md`](ROADMAP.md)** (fases 3.14 e 3.15 são esta sessão), design em
**[`GDD.md`](GDD.md)** (versão 2.3), o mundo em **[`MUNDO.md`](MUNDO.md)**, arte pendente em
**[`ARTE.md`](ARTE.md)**. Leia os quatro — este handoff só cobre o que eles não contam.

---

## Estado atual

**Tudo commitado e verificado.** Dois commits nesta sessão: *a guilda nasce vazia, as portas
acendem por motivo e o mapa é a preparação* (`bfc8f38`, fase 3.14) e *a folha vence na volta, a
semana passa na Taverna e os becos fecham* (fase 3.15). Os dois atendem ao pedido do autor de 12/09:
reestruturar a apresentação, consertar a economia e caçar pontos de quebra.

**Provas, todas verdes:**

| Prova | Resultado |
|---|---|
| `SmokeTestReport.txt` (12/09 19:19) | **63 verificações, 0 falhas** · letalidade **0,47** (alvo 0,33–0,67) · primeira jornada com dois fundadores **0,08** (teto 0,35) |
| `GameplayReport.txt` (12/09 19:19) | entra 592, folha −198, **sobra 394** por jornada · catálogo da guilda 13.220 · ao fim da run **45% do catálogo** |
| `PlayModeReport.txt` (12/09 19:27) | **PLAY MODE OK — nenhum erro capturado** · "A FUNDACAO" e "BOTOES FORA DA TELA" sem FALHA · Ajustar → Voltar → Voltar à guilda funciona · a semana passa na Taverna |
| `Assets/Screenshots/fundacao_*.png` | a guilda escura com a Taverna pulsando; a Taverna com os quatro fundadores; a guilda fundada; o mapa com Partir; o passo de ajustar **com Voltar e Próximo** |

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

**A folha vence na volta** (fase 3.15). `GuildManager.CobrarSalarios(dias)`: cada herói vivo custa
o salário ÷ 7 por dia fora, quem foi e quem ficou; `JourneyManager` cobra logo depois de pagar o
contrato, o balanço discrimina a linha em vermelho e mostra o líquido como número grande. O que não
dá para pagar vira `GuildManager.divida`, que aparece ao lado do ouro no rodapé e é abatida pelo
primeiro ouro que entrar (`AddGold`). Vai para o save como `GuildSave.debt`.

**A semana passa na Taverna.** `GuildManager.PassarASemana(alivio)`: todos descansam (a mesma
regra de quem fica em casa, agora em `GuildManager.Descansar`), a folha de 7 dias vence e
`RunManager.AdvanceCycle` corre. O botão nasce em execução (`TavernManager.GarantirBotaoDaSemana`),
some durante a fundação, e diz o custo no rótulo.

**Preços.** Armadura 150 por nível (`ForgeManager.armorBaseCost`, **campo serializado**: o
`GuildSceneSetup` escreve o valor na cena, e a cena foi remontada e salva). Poções 40–50
(`ItemData`). A auditoria passou a contar o que sai e o catálogo da guilda inteira.

**Os becos fechados.** Voltar e Próximo do passo 2 dentro do container (`GuildSceneSetup`); o
Voltar do mapa no canto de baixo à esquerda, só no passo 1 (`QuestSelectionUI.GarantirVoltarDoMapa`);
a semana que passa; e a leva da Taverna sempre com um nível 1 quando a guilda está vazia.

---

## Próximos passos

1. **O autor jogar a fundação e a primeira volta** e dizer três coisas: se a primeira jornada está
   branda demais (0,08 mortes por jornada, contra 0,47 da média — os números a mexer são
   `TavernManager.NivelDosFundadores` e `EnemyPool.ParaOGrupo`); se a folha está na medida (sobra
   394 por jornada com quatro heróis; 45% do catálogo ao fim da run — se apertar, o salário por dia
   é `salary / 7` em `CobrarSalarios`); e se a luz das portas lê bem na tela dele
   (`GuildGuide.BrilhoEscuro/BrilhoAceso`).

2. **A curva da Forja.** A auditoria continua acusando que o primeiro nível entrega a maior parte
   do ganho e os níveis 2 e 3 custam o dobro e o triplo por um acréscimo menor. É decisão de design,
   não de preço.

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
- **A folha por dia fora e a dívida** não foram perguntadas: são o desenho do `MUNDO.md` ("O
  relógio": salário a cada 7 dias, acumula como dívida), decidido em 10/09. O que é meu, e ele pode
  vetar, é a **semana que passa na Taverna** — a válvula do beco de todos esgotados sem ouro — e os
  dois preços (armadura 150, poções 40–50), que respondem ao que a auditoria acusava.

---

## Pegadinhas / lições desta sessão

### Botão fora da janela passa em todo teste que usa Invoke

O Voltar e o Próximo do passo de ajustar a equipe estavam ancorados de −88 a −24px **abaixo** do
container — "logo abaixo" quando ele era uma coluna, "abaixo da janela" desde que ele passou a
ocupar o painel inteiro. O probe atravessava o passo por `onClick.Invoke()` e nunca acusou; o autor
sentiu falta. O Voltar do mapa era o mesmo caso com outra causa: ancorado no meio da direita, sob a
ficha do lugar. Régua nova no relatório: **"BOTOES FORA DA TELA"** (`PlayModeProbe.ForaDaTela`), que
abre cada tela e os três passos da preparação. No primeiro uso ela acusou as três áreas do fundo do
mapa-múndi — que ficam fora da vista de propósito —, e conteúdo dentro de `RectMask2D`, `ScrollRect`
ou `Mask` saiu da conta. Memória `botao-fora-da-tela-invoke-esconde`.

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
| `Assets/Scripts/Core/GuildManager.cs` (folha) | `CobrarSalarios`, `divida`, `Descansar`, `PassarASemana`, `SalarioSemanal` |
| `Assets/Scripts/UI/JourneyResultUI.cs` | a linha de salários e o líquido no balanço (`JourneyReport.salarios`) |
| `Assets/Scripts/Core/GameplayAudit.cs` | `Economia` e `RitmoDaRun` com o que sai e o catálogo inteiro |

### Gatilhos (arquivo vazio na raiz, consumido pelo Editor ao ganhar foco)

`RunPlayModeTest` · `RunSmokeTest` · `RunGameplayAudit` · `RunSceneSetup` · `RunMenuSetup` ·
`RunGuildArt` · `RunCardCreator` · `RunCardArt` · `RunEventArt` · `RunEnemyArt` · `RunMapArt` ·
`RunItemArt` · `RunEventBalance` · `RunBarSkin` · `RunPortraitCatalog` · `RunBiomeArt` ·
`RunUiSkinPrefabs` · `RunHeroPanelSkin` · `RunPartyCardSkin` · `RunCardFrameSkin` · `RunAudioCatalog`
— todos `.trigger`, todos no `.gitignore`.

**Depois de mexer em código** (`RunSceneSetup` só quando o `GuildSceneSetup` mudou — foi o caso da
fase 3.15, pelos botões do passo 2 e pelo preço da armadura; o gatilho salva a cena sozinho):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -Command "& './unity-run.ps1' -Triggers @('RunSceneSetup.trigger','RunSmokeTest.trigger','RunGameplayAudit.trigger','RunPlayModeTest.trigger')"
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
| `GameplayReport.txt` | `RunGameplayAudit` | O jogo é um jogo? Impacto de cada sistema, o que entra e o que sai por jornada, a fração do catálogo ao fim da run |
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

- **A primeira jornada está branda demais?** 0,08 mortes por jornada, 96% de sobrevivência.
- **A folha está na medida?** Sobra 394 por jornada com quatro heróis; 45% do catálogo ao fim.
- **A semana que passa na Taverna** é válvula minha para o beco de todos esgotados; ele pode vetar.
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
