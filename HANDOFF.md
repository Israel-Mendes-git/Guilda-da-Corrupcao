# Handoff — Guilda da Corrupção: a sessão de apresentação (2026-08-28)

## Objetivo

Desenvolver o **Guilda da Corrupção** (Unity 2022.3.62f3): roguelike deckbuilder + gerência de
guilda, mistura declarada de *Darkest Dungeon* com *Slay the Spire*.

Projeto: `C:\Users\Israel\Documents\GitHub\Guilda-da-Corrupcao`.

Plano e histórico verificável em **[`ROADMAP.md`](ROADMAP.md)**, design em **[`GDD.md`](GDD.md)**,
inventário dos assets em **[`ASSETS.md`](ASSETS.md)**. Leia os três — este handoff só cobre o que
eles não contam.

---

## O que a próxima sessão precisa discutir

O autor pediu, ao fim da sessão de 27–28/08: *"eu gostaria de algo mais Darkest Dungeon, quais
pontos ainda podemos organizar"*. Esta seção existe para essa conversa. **Nada aqui está decidido.**

### A. O que separa este jogo do Darkest Dungeon, hoje

Ordenado por quanto muda a impressão pelo custo de fazer.

| # | O ponto | Onde está a evidência | Custo |
|---|---|---|---|
| **A1** | **A fonte do corpo é a padrão do Unity.** `ApplyTitleFont` veste **seis** nomes de objeto com a MedievalSharp; todo o resto — botões, cartas, eventos, salas — está em `LiberationSans`. É a tipografia de protótipo, e é o que mais denuncia. O projeto já tem a **Crimson-Bold** (serifada, do pacote DefaceGames) sem uso | qualquer captura; `GuildSceneSetup.ApplyTitleFont` (~linha 1995) | baixo |
| **A2** | **Não há vinheta nem grão.** O DD escurece as bordas da tela e põe textura de tela em tudo; aqui os painéis são cor chapada até a borda | `Assets/Screenshots/guilda.png` | baixo |
| **A3** | **A paleta é neutra, não sépia.** Os cinzas do jogo são levemente arroxeados (`0.13, 0.115, 0.125`); o DD é ocre e sangue | `GuildSceneSetup`, bloco da paleta | baixo |
| **A4** | **A quebra do herói não é um momento.** O estresse chega a 100, o `MentalState` muda e o rótulo troca de cor. No DD isso é o instante mais dramático da partida | `EventResolver`, `MentalStateUtil` | médio |
| **A5** | **O narrador não narra.** O guia da guilda repete "5 heróis prontos. Escolha uma missão" em todas as voltas, sem reagir a ouro parado, arma nível 0 ou herói prestes a quebrar | `DIARIO DE UMA PARTIDA` no `PlayModeReport.txt` | médio |
| **A6** | **A tocha é só um número.** O recurso existe e cobra estresse quando acaba, mas a tela não escurece nem avisa — no DD a luz é o medidor de tensão | `JourneyManager`, contador de tochas | médio |
| **A7** | **Três estilos convivem.** Heróis e inimigos em pixel art, cenas de sala e eventos em pintura digital, ícones de carta pintados num terceiro traço | `guilda.png` × `combate_cartas.png` | alto (é decisão de arte) |
| **A8** | **As molduras ornamentadas do kit são usadas em parte das telas.** Onde não estão, a caixa é um retângulo | comparar `sala_forja.png` e `tela_deck.png` | baixo |

### B. Dívida de jogabilidade, medida nesta sessão

Os números saíram da auditoria (`GameplayReport.txt`, gerada por `RunGameplayAudit.trigger`).

| # | O ponto | O número | Decisão que falta |
|---|---|---|---|
| **B1** | **A economia satura no ciclo 7.** Entram ~395 de ouro por jornada e tudo o que existe para comprar custa 2.660 — o jogador tem mais ouro do que o jogo tem coisas, e ainda faltam 8 ciclos | seção "Ritmo da run" | subir preços? mais itens? dreno recorrente? |
| **B2** | **Escolher missão não é escolha.** As quatro ofertas do quadro ficam entre 201 e 314 de ouro e 5 e 9 dias; a corrupção varia e não muda nada visível | "Diário de uma partida" | o que distingue um contrato de outro? |
| **B3** | **A curva da Forja é invertida.** Arma nível 1 vale −0,07 mortes/jornada; até o nível 3, −0,22. O primeiro upgrade é o que o jogador compra, e é o que ele não sente | tabela de impacto | rebalancear a curva? |
| **B4** | **Poções custam 60–70 e valem −0,08.** As mais fracas da lista | tabela de impacto | subir o efeito ou baixar o preço |
| **B5** | **O salário de um recruta nível 3 é 50** — 0,1 jornada. Contratar não é decisão econômica | seção "Economia" | salário recorrente por ciclo? |

### C. Interface, do que a auditoria de telas mostrou

| # | O ponto | Evidência |
|---|---|---|
| **C1** | **A Guilda é a única tela sem linha de orientação** — e é a primeira que o jogador vê | "O que cada tela oferece" no `PlayModeReport.txt` |
| **C2** | **A Biblioteca tem 10 botões desligados contra 7 ativos.** A maior parte do que ela mostra é indisponível | idem |
| **C3** | **Os nomes gerados quebram a concordância:** "Vila Antigo", "Cripta Sagrado", "Masmorra Antigo"; e o bioma sai duas vezes ("Santuário Amaldiçoado - 🏯 Ruínas 🏯 Ruínas") | `QuestGenerator`; "Diário de uma partida" |
| **C4** | **As cenas das salas foram trocadas uma vez** (paisagens → interiores de pedra) e podem ser trocadas de novo: a tabela é uma lista de sete linhas | `GuildArt.cs` |

### D. Conteúdo que falta

- **Cartas de Ladino e Bardo** não existem — por isso a taverna deixou de oferecer as duas classes.
- **Deserto, Tundra e Vulcão** têm 1 evento próprio cada, e nenhum dos três tem chefe.
- **Nomes das 23 cartas novas** são provisórios e descritivos, à espera do autor.
- **UI prefab a prefab:** 2 de 12.

---

## Estado atual

**Fases 0 a 3.9 concluídas**, mais a sessão de apresentação de 27–28/08. Nada enviado a remoto.

**Validado em 28/08:**

| Prova | Resultado |
|---|---|
| `SmokeTestReport.txt` | **45 verificações, 0 falhas** · letalidade 0,55 mortes/jornada (alvo 0,33–0,67) |
| `PlayModeReport.txt` | **PLAY MODE OK — nenhum erro capturado** |
| `GameplayReport.txt` | auditoria de jogabilidade: impacto por sistema, economia, ritmo da run |
| Capturas | as sete salas, o combate, a jornada, o balanço e os menus |

### O que a sessão de 27–28/08 entregou

**Jogabilidade medida.** A auditoria (`GameplayAudit.cs`) responde quanto cada sistema vale em mortes
por jornada, rodando a mesma simulação com e sem cada coisa. Foi ela que expôs a saturação da
economia e a curva invertida da Forja.

**O combate ficou justo e legível.** O alvo do inimigo passou a sair junto com a intenção — antes era
sorteado no instante do golpe, e o jogador não tinha como decidir em quem gastar o bloqueio. Junto,
três defeitos de feedback: a barra que saltava no meio do ataque (animações concorrentes), a criatura
que ficava branca ao apanhar (o "take hit" do pacote é a criatura pintada de branco) e os números de
dano nascendo no centro do alvo.

**A guilda virou lugar.** As sete portas mostram cenas pintadas em vez de retângulos pretos, a paleta
subiu ~8 pontos de luminância e os retratos do rodapé apareceram.

**As telas pararam de empurrar tudo para o canto.** O grupo caminha a 52% da largura do mapa (era
70%, colado na borda), o papel desce até os cards, e os inimigos ocupam a metade direita do combate.

---

## Pegadinhas / lições

### O instrumento erra mais que o jogo — cinco vezes até agora

A auditoria de jogabilidade acusou "relíquias valem −0,01 mortes" e "poções, −0,03". Nenhuma das duas
era fraca: `SimulateOneCombat` nunca leu `ItemCatalog`, e não havia política para beber frasco. Com o
simulador corrigido, relíquias valem **−0,30** — trinta vezes mais.

**Antes de ler qualquer número de sistema, confira se o simulador executa aquele sistema.** As outras
quatro ocorrências estão no `ROADMAP.md`.

### Quando um número oscila, procure o *n* efetivo

Três execuções do smoke test, sem nada mudar, deram 0,41 · 0,74 · 0,51 mortes por jornada. A causa
não eram as 200 jornadas: `RodarJornadas` reciclava **25 grupos**, sorteados de novo a cada execução.
Com 100 grupos e 1000 jornadas, a dispersão caiu para ±0,03.

### Duas armadilhas de UGUI que custaram caro

1. **`Image.type = Filled` é ignorado quando o `Image` não tem sprite.** Sem sprite o componente
   desenha o retângulo inteiro e o `fillAmount` não vale nada — a barra fica sempre cheia, sem erro
   no console. A tarja de estresse do Cemitério mentia desde que a sala foi feita. Use
   `UIUtil.Branco()` em barra criada em execução.
2. **`Instantiate` de um molde desligado devolve um clone desligado.** As fichas de herói da tela de
   balanço eram instanciadas, preenchidas e nunca ligadas: existiam na hierarquia — o teste contava
   as quatro — e a tela aparecia vazia. **Contar filhos não prova que a tela mostra algo**: conte os
   `activeInHierarchy`.

### Uma animação por alvo, sempre

O `UIManager` já protegia painéis e popups; o `CombatFeedback` não protegia as barras. Como há um
refresh do combate por carta jogada, por golpe e por morte, três corrotinas corriam juntas sobre a
mesma barra e ela saltava para trás no meio do ataque.

### O padrão que mais se repete: dado certo, exibição ausente

Já são **vinte** ocorrências, todas com o relatório em "0 erros". **Sempre olhe
`Assets/Screenshots/*.png` depois do Play Mode** — e depois de mudar layout, olhe de novo.

### Antes de corrigir o que a captura mostra, confirme que é defeito

Duas coisas pareciam bug e não eram: a descrição do evento cortada no meio da palavra é o efeito de
máquina de escrever (`JourneyManager.TypeText`) pego pela captura, e o 🏹 ao lado de um Curandeiro é
o ícone de **fileira** (`PartyFormation.PreferenceIcon`), não de classe.

### O Editor precisa de foco para recompilar

Duas vezes nesta sessão o `unity-run.ps1` deu `TIMEOUT esperando a compilacao` com o Editor aberto e
saudável. Numa delas havia uma segunda janela do Unity presa em "Open Project", segurando o
foreground; na outra, bastou o autor clicar na janela do Editor. **Se der timeout duas vezes seguidas
sem erro de compilação, é foco, não código.**

### Outras, de sessões anteriores

- **O gatilho é consumido antes de o Unity recompilar** — use sempre o `unity-run.ps1`.
- **O "Montar Cena" pode rodar na cena errada.** O gatilho abre a cena do jogo antes de montar.
- **Singleton `DontDestroyOnLoad` + troca de cena = a segunda partida herda a primeira.** Quem
  resolve é `SceneFlow.DescartarMundo`; manager novo precisa entrar nessa lista.
- **O componente que mora no painel que ele controla** se desliga no próprio `Awake`.
- **Pivô antes do rect.** **Quem nasce depois é desenhado por cima.** **`preserveAspect` encaixa o
  quadro inteiro**, transparência incluída.
- **`GridLayoutGroup` controla o retângulo do filho direto** — prefab de tamanho fixo precisa de um
  envelope e encolher por `localScale`.
- **Glifos:** a fonte tem `◆ ◇ · →` e emoji; **não tem `✦` nem `✚`**.
- **Campo público é serializado**: mudar o valor no script não muda a cena — altere os dois.
- **Acrescentar valor de enum só no fim**: os assets guardam o número.
- **Uma run de Play Mode é n=1** — balanceamento se mede no simulador.
- **`git add <pasta>` não inclui o `.meta` da própria pasta.** Mensagem de commit com aspas quebra a
  here-string do PowerShell: use `git commit -F arquivo.txt`.
- **A fonte de emoji (`SegoeUIEmoji SDF.asset`) é reescrita a cada Play Mode** — 4 MB de diff por
  run. Reverta antes de commitar (`git checkout --`).

---

## Ferramentas

### Gatilhos (arquivo vazio na raiz, consumido pelo Editor ao ganhar foco)

`RunPlayModeTest` · `RunSmokeTest` · `RunGameplayAudit` · `RunSceneSetup` · `RunMenuSetup` ·
`RunGuildArt` · `RunCardCreator` · `RunCardArt` · `RunEventArt` · `RunEnemyArt` · `RunMapArt` ·
`RunItemArt` · `RunEventBalance` · `RunBarSkin` · `RunPortraitCatalog` · `RunBiomeArt` ·
`RunUiSkinPrefabs` · `RunHeroPanelSkin` · `RunPartyCardSkin` · `RunCardFrameSkin` · `RunAudioCatalog`
— todos `.trigger`, todos no `.gitignore`.

**Depois de mexer na cena**, nesta ordem:

```powershell
.\unity-run.ps1 -Triggers "RunSceneSetup.trigger","RunGuildArt.trigger","RunPlayModeTest.trigger"
```

**O `RunEnemyArt` sobrescreve** a arte dos inimigos — rode só ao mudar a tabela do `EnemyArt.cs`. O
`RunCardCreator`, o `RunEventArt` e o `RunGuildArt` **nunca** sobrescrevem escolha feita à mão.

### Os três relatórios

| Arquivo | Quem gera | O que responde |
|---|---|---|
| `SmokeTestReport.txt` | `RunSmokeTest` | O jogo está de pé? 45 travas + letalidade medida em 1000 jornadas |
| `PlayModeReport.txt` | `RunPlayModeTest` | As telas funcionam? Console limpo, raycast, diário da partida, custo em cliques |
| `GameplayReport.txt` | `RunGameplayAudit` | O jogo é um jogo? Impacto de cada sistema, economia, ritmo da run |

### Compilar sem abrir o Editor (segundos)

```powershell
$root = "C:\Users\Israel\Documents\GitHub\Guilda-da-Corrupcao"
$out  = "C:\Users\Israel\AppData\Local\Temp\gol-build"
New-Item -ItemType Directory -Force $out | Out-Null
$csproj = Get-Content "$root\Assembly-CSharp.csproj" -Raw
$refs = [regex]::Matches($csproj, '<HintPath>(.*?)</HintPath>') | ForEach-Object { $_.Groups[1].Value }
$projRefs = [regex]::Matches($csproj, '<ProjectReference Include="([^"]+)\.csproj"') | ForEach-Object { $_.Groups[1].Value }
$defines = [regex]::Match($csproj, '<DefineConstants>(.*?)</DefineConstants>').Groups[1].Value
$asmdefDirs = Get-ChildItem "$root\Assets" -Recurse -Filter *.asmdef | ForEach-Object { $_.Directory.FullName }
$todos = Get-ChildItem "$root\Assets" -Recurse -Filter *.cs | Where-Object {
  $f = $_.FullName; $dentro = $false
  foreach ($d in $asmdefDirs) { if ($f.StartsWith($d)) { $dentro = $true; break } }
  -not $dentro -and $f -notmatch '\\Editor\\' -and $f -notmatch 'DOTween\\Modules'
}
$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('-target:library'); $lines.Add('-nostdlib+'); $lines.Add('-langversion:9.0')
$lines.Add("-out:`"$out/Check.dll`"")
foreach ($d in $defines -split ';') { if ($d.Trim()) { $lines.Add("-define:$($d.Trim())") } }
foreach ($r in $refs) { $lines.Add("-r:`"$r`"") }
foreach ($p in $projRefs) { $dll = "$root\Library\ScriptAssemblies\$p.dll"; if (Test-Path $dll) { $lines.Add("-r:`"$dll`"") } }
foreach ($f in $todos) { $lines.Add("`"$($f.FullName)`"") }
$lines | Out-File "$out\build.rsp" -Encoding utf8
Set-Location $root
& "C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Data\NetCoreRuntime\dotnet.exe" `
  "C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Data\DotNetSdkRoslyn\csc.dll" "@$out/build.rsp"
```

Avisos pré-existentes esperados: `goldtxt`, `reputationtxt` e `currentPopupCG` nunca atribuídos.

### Onde ficam os saves em execução

`C:\Users\Israel\AppData\LocalLow\Rapadura Atômica\Guilda da Corrupção\` — `saves\*.json` e
`profile.json`.

---

## Pendências que dependem do autor

- **Push** — os commits seguem só no repositório local.
- **As decisões das seções A, B e C** acima.
- **Nomes das 23 cartas novas** e o nome do "Tratamento" do Mercado.
- **Lobo, aranha e golem de pedra** — três dos onze inimigos usam arte que não os representa.
- **Deserto e Vulcão sem arte de bioma.**
- **Animação dos inimigos** — os spritesheets trazem Idle, Attack, Take Hit e Death; hoje só o Idle é
  usado, e o Take Hit foi trocado por um clarão porque o quadro do pacote é a criatura em branco.
- **Git LFS** — 763 MB de binários no histórico.
- **ESave e Bench**: importados, commitados e não usados.
