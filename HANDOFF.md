# Handoff — Guilda da Corrupção: a estrada, as cartas e as áreas (2026-09-12, noite)

## Objetivo

Desenvolver o **Guilda da Corrupção** (Unity 2022.3.62f3): roguelike deckbuilder + gerência de
guilda, mistura declarada de *Darkest Dungeon* com *Slay the Spire*.

Projeto: `C:\Users\Israel\Documents\GitHub\Guilda-da-Corrupcao`.

Plano e histórico em **[`ROADMAP.md`](ROADMAP.md)** (fases 3.14 e 3.15 são as duas últimas), design
em **[`GDD.md`](GDD.md)** (versão 2.3), o mundo em **[`MUNDO.md`](MUNDO.md)**, arte pendente em
**[`ARTE.md`](ARTE.md)**. Leia os quatro — este handoff só cobre o que eles não contam.

**O autor decidiu o que a próxima sessão faz:** *"no próximo chat vamos resolver o 1, 2 e 3"* — os
três primeiros itens da lista de melhorias que fechou a sessão de 12/09 (seção "Próximos passos").

---

## Estado atual

**Tudo commitado e verificado; árvore limpa.** Dois commits em 12/09:

| Commit | O que entrou |
|---|---|
| `bfc8f38` | A guilda nasce vazia, as portas acendem por motivo e o mapa é a preparação (fase 3.14) |
| `29bb079` | A folha vence na volta, a semana passa na Taverna e os becos fecham (fase 3.15) |

**Provas, todas verdes:**

| Prova | Resultado |
|---|---|
| `SmokeTestReport.txt` (12/09 19:19) | **63 verificações, 0 falhas** · letalidade **0,47** (alvo 0,33–0,67) · primeira jornada com dois fundadores **0,08** (teto 0,35) |
| `GameplayReport.txt` (12/09 19:19) | entra 592, folha −198, **sobra 394** por jornada · catálogo da guilda 13.220 · ao fim da run **45% do catálogo** |
| `PlayModeReport.txt` (12/09 19:27) | **PLAY MODE OK — nenhum erro capturado** · "A FUNDACAO" e "BOTOES FORA DA TELA" sem FALHA · partir pelo mapa em 3 cliques |

**Não commitado:** nada. O `Assets/Fonts/SegoeUIEmoji SDF.asset` volta a mudar a cada Play Mode
(4 MB de diff); reverta antes de qualquer commit: `git checkout -- "Assets/Fonts/SegoeUIEmoji SDF.asset"`.

### O que existe desde 12/09 (resumo; detalhe no ROADMAP 3.14 e 3.15)

- **Fundação:** a guilda nasce sem heróis e com 100 de ouro; a Taverna oferece quatro candidatos
  (um por classe, Nv.2) e o jogador escolhe dois sem custo (`GuildManager.EmFundacao`).
- **Portas com três luzes** — pulsando, acesa, escura — pelo que cada sala tem a oferecer
  (`GuildGuide.TemMotivo`). Escura abre. Sem popup de "deseja entrar?". Corrupção no rodapé
  (`RelogioDaGuilda`).
- **O mapa é a preparação:** apontar o destino preenche grupo, formação, baralho e mochila;
  Partir ao lado da ficha; Ajustar abre os passos 2 e 3; Voltar no canto de baixo à esquerda
  (`QuestSelectionUI.MontarGrupoPadrao`, `GarantirBotaoDePartir`, `GarantirVoltarDoMapa`).
- **Formação e encontro no tamanho do grupo** (`PartyFormation.FrontSlotsFor`, `EnemyPool.ParaOGrupo`).
- **A folha:** salário ÷ 7 por dia fora, de todo herói vivo, cobrada na volta; dívida ao lado do
  ouro; **a semana passa na Taverna** (`GuildManager.CobrarSalarios`, `PassarASemana`).
- **Preços:** armadura 150 por nível, poções 40–50.
- **Réguas novas no relatório:** "A FUNDACAO", "BOTOES FORA DA TELA", "O PRIMEIRO CONTATO" no fim
  (mede a guilda recém-fundada), e a auditoria conta o que sai e o catálogo inteiro.

---

## Próximos passos

Os três, na ordem que o autor escolheu. Cada um tem o que está decidido, o que está medido, e por
onde entrar no código. **Nenhum foi começado.**

### 1. A estrada — a jornada monótona

**A queixa** (autor, 10/09): *"a jornada está monótona, muito texto, combate de vez em quando"*,
longe de *Slay the Spire* e *Inscryption*. Nada desde então mexeu na estrada.

**O que está medido** (`SmokeTestReport.txt`, `PlayModeReport.txt`):

| Medida | Valor |
|---|---|
| Duração média · combates por jornada | 10,7 dias · 3,43 |
| Paradas de texto por jornada | cerca de 7 (26 eventos resolvidos por clique numa jornada de 7 dias no Play Mode) |
| Cartas jogadas na estrada | 2,62 por jornada, com 5 de energia para a jornada inteira |
| Eventos | 25 no total: 20 comuns (6 levam a combate) e 5 de chefe. Numa partida de ~100 trechos, cada comum se repete umas 5 vezes |
| Contadores no topo da tela | 11: dia, nome, aspecto, baralho, mão, descarte, rações, tochas, energia, desvios, próximos eventos |

**Três cortes baratos, propostos e não decididos:**
- A barra do topo cair para o que decide algo (dia, ração, tocha, energia); baralho, mão e descarte
  só quando a mão importa. Os campos estão em `Assets/Scripts/Core/JourneyManager.cs` (linhas 14–58:
  `dayText`, `deckCountText`, `handCountText`, `discardCountText`, `rationsText`, `torchesText`,
  `energyText`, `detourCountText`, `upcomingEventsText`) e o layout em
  `GuildSceneSetup.BuildJourney` (a partir da linha ~196).
- O sorteio de eventos pesar mais combate por parada: `EventPool.GetRandomEvent` (linha 40) escolhe
  uniformemente no pool filtrado por região, corrupção e dia, com memória dos 3 últimos.
- Menos paradas por jornada, ou paradas mais curtas: o texto de cada evento mora nos assets
  `Resources/Events` (25), com três opções cada.

**O que respeitar** (memórias `rumo-do-projeto-mapa-combate-salas`, `jornada-carta-vezes-escolha`,
`guild-of-legends-regras-de-design`): a mão na estrada existe por decisão do autor (a carta destrava
a melhor opção do evento e é a única saída sem custo); os heróis atravessam a pé, em fila, na ordem
da formação; mapa ramificado 2–3 caminhos por dia, sem convergência forçada; os eventos ocorrem na
travessia, com a caixa só quando o grupo para. **Rejeitados:** baralho separado para a estrada,
carroça, reduzir a party a um herói, "encurtar a travessia para 1 dia", "a estrada vira travessia
sem nó". Medir antes e depois: letalidade (0,33–0,67) e cartas jogadas na estrada.

### 2. As cartas fora do combate

**Decidido pelo autor em 09/09** (memória `final-do-jogo-campeao-corrompido`), e é o passo 2 da
fase 3.12 do ROADMAP. Três usos, nenhum construído:

| Uso | Onde entra |
|---|---|
| **As salas aceitam carta no lugar de ouro** | as compras de `ForgeManager`, `MarketManager`, `LibraryManager`, `CemeteryManager`, `MapRoomManager` (todas cobram por `GuildManager.SpendGold`); a carta sai do `DeckRepository.GetDeck(heroi)` |
| **O escrito traduzido entra no baralho** como carta de contenção — a única que age sobre o mundo, e o que se leva à luta final | `Escritos.Traduzir` (linha 148) marca como lido; a carta precisaria nascer como `CardData` (assets em `Resources/Cards`, `CardCreator` cria por ferramenta) e entrar no baralho do herói que a leva |
| **Selar cobra cartas**, queimadas sem volta, do tipo que aquela área sempre pede — fixo por área e sabido desde o começo, para dar para preparar o baralho | `RegionMap.Selar` (linha 251) e a luta de selo em `QuestManager`; o tipo pedido cabe na `AreaCatalog.Ficha` (`Assets/Scripts/Data/AreaType.cs`, linhas 44–72: `nome`, `regra`, `oQueDa`, `oQueCobra`, `selo`, `aspecto`, `posicao`, `vizinhas`); o mapa-múndi já mostra a ficha e é onde o preço deve aparecer "desde o ciclo 1" (prova prevista no ROADMAP) |

**Rejeitado:** a carta que se corrompe com o uso e fica mais forte cobrando estresse. A régua: o
smoke test tranca "toda carta faz alguma coisa nos dois lados" — carta nova precisa de efeito de
jornada e de combate, ou de ser tratada como exceção explícita.

### 3. A regra própria de cada área, e o selo como ato

**Decidido em 10/09** (`MUNDO.md`, "As sete áreas"): cada área tem regra, o que dá, o que cobra e
como se fecha. Hoje as sete se diferenciam por distância, corrupção, aspecto e nome; o selo é sempre
combate. As fichas de texto já estão em `AreaCatalog` e aparecem no mapa; o que falta é o código ler
cada regra:

| Área | Regra (MUNDO.md) | Por onde entra |
|---|---|---|
| A Mata | o mato fecha: os últimos dias cobram mais mantimento e tocha | manutenção diária em `JourneyManager` (rações/tochas por trecho) |
| A Cripta | os mortos se erguem uma vez por turno; herói enterrado sem tributo levanta contra o grupo | `CombatManager` (turno do inimigo), `CemeteryManager.tributeCost` já existe |
| A Aldeia | poupar ou matar, um a um: poupar devolve moral, matar dá recurso | eventos da área em `EventPool`/`Resources/Events` |
| O Covil | a luz desperta o dragão; cada relíquia levada aproxima o despertar | `JourneyLight` e o contador de tochas; espólio em `JourneyManager` |
| A Torre | a cada combate um herói seu aparece do outro lado, com baralho e equipamento | `EnemyPool.GetLineup` + `CombatManager` (um inimigo montado de `HeroData`) |
| A Forja | forjar na estrada chama o que mora lá; equipamento corrompido soma exposição | `ForgeManager` reaproveitado num evento; `HeroData.corruptionExposure` acumula e ninguém lê |
| O Oráculo | cada resposta custa uma carta do baralho, para sempre | evento + `DeckRepository` |

**Os sete selos são sete atos**, e só o da Mata é combate limpo: vencer, devolver os mortos pelo
nome, escolher entre fogo e resgate, acordar o dragão e sair, quebrar o altar, apagar o fogo, pagar a
última pergunta. Quantos passam por uma luta antes do ato não foi decidido.

**As regras do autor para isto** (memórias `dark-fantasy-cliche-e-o-tema-central`,
`mapa-navegavel-e-world-building-meio-a-meio`): o arquétipo vem primeiro e tem de ser clichê; a
invenção fica na mecânica; nomes são de trabalho; propor estrutura e função e pedir crítica.
**Pendente dele:** cinco de sete áreas por partida; o que ficou em aberto com a saída da Abadia
(descanso fundo fora da guilda, esconderijo confiável); o nome do chefe do Covil ("O Gigante de
Pedra" é herdado da Montanha, e o Covil é de um dragão).

**Sugestão de ordem dentro do 3:** a Mata primeiro (a regra é uma conta na manutenção diária e é a
área da primeira jornada), depois a Cripta (usa o Cemitério, que hoje quase não tem motivo).

### O que ficou em aberto e não é dos três

- **Requisitos de missão aleatórios** (`QuestGenerator.GenerateRequirements`): sorteiam "1 Guerreiro
  Nv.2+" sem ligação com o lugar, e com dois fundadores quase toda saída avisa "requisitos não
  atendidos". Ou somem, ou nascem da regra da área — cabe junto do item 3.
- **A curva da Forja:** o primeiro nível entrega a maior parte do ganho. Decisão de design.
- **O Cofre do Santuário** (+120 por nível) mais que dobra o começo de 100; com Cofre 2 a Forja já
  pulsa antes da primeira estrada.
- **A primeira jornada está branda?** 0,08 mortes por jornada; teto 0,35 no smoke test.
- **A folha está na medida?** Sobra 394 por jornada com quatro heróis; 45% do catálogo ao fim.

---

## Decisões tomadas (e por quê)

- **Todas as de 12/09 estão na memória `fundacao-da-guilda-decisoes`**: começar na guilda e não na
  estrada; dois fundadores escolhidos; sala escura mas clicável; Mata comum com inimigos pelo grupo;
  o mapa é a preparação; nenhuma sala se funde; a tela da guilda continua a guilda. Com o que foi
  rejeitado — não repropor.
- **A folha por dia fora e a dívida** não foram perguntadas: são o desenho do `MUNDO.md` ("O
  relógio"), decidido em 10/09. **A semana que passa na Taverna** e os dois preços (armadura 150,
  poções 40–50) são meus; o autor pode vetar.
- **A lista de melhorias** que fechou a sessão foi minha, ordenada por peso na queixa dele; ele
  escolheu os três primeiros sem ressalvas.

---

## Pegadinhas / lições desta sessão

- **Botão fora da janela passa em todo teste que usa Invoke** — memória
  `botao-fora-da-tela-invoke-esconde`. A seção "BOTOES FORA DA TELA" do relatório cobre as telas e
  os três passos da preparação; conteúdo em `RectMask2D`/`ScrollRect`/`Mask` fica de fora.
- **O Button repinta o alvo dele:** tingir o `targetGraphic` para escurecer uma porta não pega;
  tinja os filhos (`GuildGuide.Iluminar`).
- **O rodapé não é parte do painel da guilda** (`Panel_DownBar` é irmão do `GuildMap`); quem
  contar texto "na Guilda" precisa dos dois.
- **`unity-run.ps1` do bash: passe o array por `-Command`**, não por `-File` — memória
  `unity-run-falha-por-titulo-da-janela`.
- **Compilar por fora exige as fontes do csproj e os ProjectReferences** — memória
  `validar-compilacao-sem-abrir-unity`. `build.rsp` está no `.gitignore`.
- **Campo público é serializado:** `armorBaseCost` precisou ser escrito pelo `GuildSceneSetup` e a
  cena remontada. Vale para qualquer preço ou régua que vire campo público.
- **O probe repõe o elenco de sempre depois da fundação** (`RepovoarElencoClassico`); "O PRIMEIRO
  CONTATO" roda por último, na guilda nova que "SAVE E MENUS" funda.
- Seguem valendo: Play Mode com compilação quebrada roda o assembly antigo; `Image.type = Filled`
  sem sprite é ignorado; `Instantiate` não copia ouvintes de execução; quem nasce depois é desenhado
  por cima; enum só cresce no fim; uma run de Play Mode é n=1; a fonte de emoji muda a cada Play
  Mode; mensagem de commit com aspas quebra a here-string (use `git commit -F`); olhe as capturas.

---

## Arquivos e comandos relevantes

### Onde entrar para os três itens

| Arquivo | Papel |
|---|---|
| `Assets/Scripts/Core/JourneyManager.cs` | a estrada: contadores do topo, mão, manutenção diária, fim de jornada |
| `Assets/Scripts/Core/EventPool.cs` | sorteio de eventos (`GetRandomEvent` linha 40, `GetStrongEncounter` linha 80) |
| `Assets/Scripts/Core/GuildSceneSetup.cs` | layout do painel da jornada (`BuildJourney`) — mudou? rode `RunSceneSetup.trigger` |
| `Assets/Scripts/Data/AreaType.cs` | `AreaCatalog.Ficha`: as sete áreas, regra, o que dá, o que cobra, selo |
| `Assets/Scripts/Core/RegionMap.cs` · `Escritos.cs` | selar (linha 251) e traduzir (linha 148) |
| `Assets/Scripts/Core/DeckRepository.cs` · `Data/CardData.cs` | o baralho de cada herói; efeito de jornada e de combate por carta |
| `Assets/Scripts/Core/GuildSmokeTest.cs` | o simulador da jornada: qualquer regra de área nova precisa entrar aqui, ou a letalidade mede outro jogo |

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

**Se o `unity-run.ps1` falhar** (título da janela ou timeout de foco), use o MCP: `refresh_unity`
com `compile: request` e `wait_for_ready: true`, confira que
`find Assets/Scripts -name "*.cs" -newer Library/ScriptAssemblies/Assembly-CSharp.dll` vem vazio, e
então `touch RunPlayModeTest.trigger`.

### Os quatro relatórios

| Arquivo | Quem gera | O que responde |
|---|---|---|
| `SmokeTestReport.txt` | `RunSmokeTest` | O jogo está de pé? 63 travas + letalidade em 1000 jornadas + a primeira jornada dos fundadores |
| `PlayModeReport.txt` | `RunPlayModeTest` | As telas funcionam? Console limpo, a fundação, botões fora da tela, diário, primeiro contato em conceitos |
| `GameplayReport.txt` | `RunGameplayAudit` | O jogo é um jogo? Impacto de cada sistema, o que entra e sai por jornada, fração do catálogo ao fim |
| `Assets/Screenshots/*.png` | `RunPlayModeTest` | O que o jogador vê — a única prova que pega "dado certo, exibição ausente" |

### Compilar sem abrir o Editor (segundos)

Receita na memória `validar-compilacao-sem-abrir-unity` (fontes do csproj + `.cs` novos +
ProjectReferences). Aviso esperado: `StatusEffectsPreview.spawnAuraCO`; `goldtxt`, `reputationtxt`
e `currentPopupCG` são filtrados.

### Onde ficam os saves

`C:\Users\Israel\AppData\LocalLow\Rapadura Atômica\Guilda da Corrupção\` — `saves\*.json` e
`profile.json`. O formato ganhou `GuildSave.debt` em 12/09 (save antigo lê 0).

---

## Pendências que dependem do autor

- **Vetar ou não:** a semana que passa na Taverna; armadura 150; poções 40–50.
- **Primeira jornada branda?** (0,08) · **folha na medida?** (sobra 394) · **Cofre +120?**
- **Push** — os commits seguem só no repositório local.
- **Cinco de sete áreas por partida** — proposto em `MUNDO.md`, não decidido.
- **O que a saída da Abadia deixou em aberto**: o descanso fundo fora da guilda e o esconderijo.
- **Nomes de trabalho:** as sete áreas, o chefe do Covil, as 23 cartas novas, o "Tratamento".
- **O que cada nível de dificuldade mexe** (fácil, médio, difícil).
- **Arte** — ver `ARTE.md`: 285 peças, e nada do que está na tela fica.
- **Ladino e Bardo** seguem sem carta nenhuma.
- **Git LFS** — 763 MB de binários no histórico. **ESave e Bench**: importados e não usados.
