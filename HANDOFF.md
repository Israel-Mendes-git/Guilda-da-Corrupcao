# Handoff — Guilda da Corrupção: menus, save e campo de batalha (2026-08-15)

## Objetivo

Desenvolver o **Guilda da Corrupção** (Unity 2022.3.62f3): roguelike deckbuilder + gerência de
guilda, mistura declarada de *Darkest Dungeon* com *Slay the Spire*.

Projeto: `C:\Users\Israel\Documents\GitHub\Guilda-da-Corrupcao`.

Plano e histórico verificável em **[`ROADMAP.md`](ROADMAP.md)**, design em **[`GDD.md`](GDD.md)**,
inventário dos assets em **[`ASSETS.md`](ASSETS.md)**. Leia os três — este handoff só cobre o que
eles não contam.

---

## Estado atual

**Árvore limpa. 17 commits locais, nenhum enviado a remoto.** Os 5 desta sessão:

| Hash | O quê |
|---|---|
| `d62326c` | Save em JSON, menus, pausa, opções, Santuário |
| `8bd844d` | Cena `MainMenu.unity` e as duas cenas na build |
| `a668fec` | ROADMAP, GDD e handoff da Fase 3.5 |
| `df27c7d` | Campo de batalha frente a frente + arte dos inimigos |
| `adef518` | Handoff da Fase 3.6 |

**Validado:** `PlayModeReport.txt` de 15/08 20:02 — `PLAY MODE OK — nenhum erro capturado`,
**0 falhas**, jornada vitoriosa com 3 combates travados.

### ✅ Fases 0, 1, 2, 2.5, 3, 3.5 e 3.6 concluídas

Duas frentes nesta sessão (detalhes e régua medida no `ROADMAP.md`):

- **Fase 3.5 — menus e save.** O jogo abria direto na guilda, com a partida já em andamento, e nada
  sobrevivia a fechar a janela além dos decks. Agora tem tela de título (`MainMenu.unity`, cena 0 da
  build), pausa no ESC, opções de áudio e vídeo, save em arquivo JSON (autosave + 3 slots manuais) e
  o Santuário das Relíquias, onde a meta-progressão é gasta.
- **Fase 3.6 — o campo de batalha.** O combate ficou frente a frente como o de *Darkest Dungeon*:
  party à esquerda com a **posição 1 encostada nos inimigos**, inimigos à direita com corpo e
  intenção acima da cabeça, e a barra de ordem do round no alto. Os 11 inimigos ganharam retrato.

### 🔄 Pendências reais

| O quê | Situação |
|---|---|
| **Smoke test não foi re-executado nesta sessão** | O `SmokeTestReport.txt` é de 15/08 **01:28**, anterior a tudo. O autor interrompeu a chamada. Ele estava quebrando por um bug de áudio em edit mode que **já foi corrigido**; falta só rodar e conferir as 41 verificações |
| UI prefab a prefab | **2 de 12** — inalterado desde a sessão anterior |
| Clareza e ritmo | Pedido explícito do autor, nunca feito (ver abaixo) |

### 🔄 Pedido do autor que segue pendente desde a sessão anterior

Ele pediu cinco blocos; três foram entregues em sessões passadas. Faltam dois:

1. **Etapas da jornada** e **posições das coisas na jornada** — ajuste fino de layout.
2. **Clareza: o jogador saber o que fazer** — destacar a ação principal, indicar o que está pronto.
   O ponto mais visível segue lá: em `Assets/Screenshots/sala_taverna.png` há um botão genérico
   escrito **"Button"** no topo da tela, que sobrou de algum layout e não faz nada de óbvio.

---

## Próximos passos

1. **Rodar o smoke test.** É a única prova que faltou:
   ```powershell
   .\unity-run.ps1 -Triggers "RunSmokeTest.trigger"
   ```
   Esperado: `41 verificações, 0 falhas` em `SmokeTestReport.txt`. Se falhar, compare com o relatório
   antigo (01:28) — o balanceamento não foi tocado nesta sessão, então qualquer diferença é regressão.
2. **Clareza e ritmo** — o que o autor pediu e nunca foi feito.
3. **Continuar a UI prefab a prefab.** Faltam 10. Por retorno visual: `CardPurchasePrefab`
   (biblioteca), `QuestItemPrefab` (quadro de missões), `PartyMemberSelectPrefab` (preparação) — as
   três aparecem a cada ciclo. Receita em "Pegadinhas" abaixo.
4. **Ajuste fino no `HeroPanel`:** nome, classe e nível ficam sobrepostos ao retrato.
5. **Fase 4** (`ROADMAP.md`): dar efeito a `corruptionExposure`, traços e personalidades — tudo já
   acumula e ninguém lê.

---

## Decisões tomadas nesta sessão (e por quê)

### Save e menus

- **Save próprio em JSON** — escolha do autor entre três opções apresentadas. Um arquivo por slot em
  `Application.persistentDataPath/saves/`. **Rejeitados:** o **ESave** (`Assets/Esper/ESave`) e o
  **Bench Universal Save System**, ambos no disco, importados e agora oficialmente **não usados**. A
  pendência histórica de "confirmar o ESave" está encerrada — não re-proponha.
- **Autosave + 3 slots manuais.** O autosave pertence ao jogo: o botão "Salvar" da pausa **não**
  escreve nele. **Rejeitados:** só autosave (impede experimentar) e só slots manuais (permite desfazer
  mortes, e permadeath é pilar do design).
- **`MainMenu.unity` como cena separada**, cena 0 da build. **Rejeitado:** painel na própria
  `SampleScene` — sem cena separada a build continuaria abrindo na guilda.
- **O perfil do jogador vive fora dos slots** (`PlayerProfile` → `profile.json`): relíquias,
  destraves, recordes e opções. Apagar um save não apaga o que as runs custaram a conquistar.
- **Não se salva no meio da estrada.** O estado interno da jornada (mapa, dia, mão, descarte,
  mitigação) vive em campos privados do `JourneyManager` e não é serializado por ninguém. O botão da
  pausa aparece desligado **com o motivo escrito**. O ponto seguro é a guilda entre jornadas, que é
  onde o autosave já cai.
- **As relíquias deixaram de virar ouro sozinhas.** Eram `relíquias × 5` com teto, automático; agora
  são gastas no Santuário e o ouro extra virou o destrave "Cofre da guilda". As duas coisas não podem
  coexistir: um bônus automático que também é moeda pune o jogador por comprar qualquer coisa.
- **Os quatro destraves caem em pontos que já existiam** (ouro inicial, reputação inicial,
  `maxRosterSize`, `questBoardSize`). Um destrave que exigisse sistema novo seria design a fazer, não
  meta-progressão a ligar.
- **`Time.timeScale` não é tocado na pausa.** O jogo é de turnos por clique: nada avança sozinho, e
  zerar a escala congelaria corrotinas de transição — algumas responsáveis por avançar o próprio fluxo.
- **A cena de título escala com a tela** (`ScaleWithScreenSize`, 1920×1080); a cena do jogo continua
  em pixels fixos, que é como ela sempre esteve.
- **O menu usa a trilha do hub** (`MusicContext.Hub`). Acrescentar `MusicContext.Menu` exigiria
  remontar o catálogo de áudio inteiro por um clipe que ainda não existe.

### Combate

- **A ordem de combate é uma barra informativa; a regra não mudou.** Escolha do autor entre três
  opções. O grupo continua agindo junto com 5 de energia, como no Slay the Spire, e depois os
  inimigos respondem. **Rejeitado:** vez-por-personagem ao estilo Darkest Dungeon — mudaria energia,
  posse de carta, o simulador e todo o balanceamento, que foi medido em mortes por combate.
- **Layout frente a frente**, com a fila do grupo **invertida** (`reverseArrangement`): a posição 1
  fica à direita, encostada nos inimigos, porque é ela que está na linha de frente. Numa fila normal
  o herói mais exposto apareceria no canto mais distante do perigo.
- **Arte estática, primeiro quadro do Idle.** **Rejeitado:** animar Idle/Attack/Take Hit/Death — os
  quadros existem nos pacotes, foi decisão de escopo, não falta de material.
- **Moldes próprios do combate**, montados por código. O `PartyStatusPrefab` e o `EnemyCardPrefab`
  continuam servindo à jornada, onde o card pequeno é o certo.
- Mantidas de sessões anteriores: alvo de letalidade 0,33–0,67 mortes/jornada; KPI de combate é
  **mortes por combate**, não taxa de vitória; energia 5 / mão 6; acrescentar valor de enum **só no
  fim** (os assets guardam o número); `-batchmode` rejeitado.

---

## Pegadinhas / lições desta sessão

### A que mais custou tempo: o gatilho é consumido antes de o Unity recompilar

O watcher roda no `EditorApplication.update` e **não espera o refresh de assets**. Duas consequências,
as duas vividas hoje:

1. O gatilho executa a **versão velha** do código, e o resultado parece um bug do que você acabou de
   escrever. O `Montar Cena` rodou duas vezes assim, e o painel de pausa "não aparecia".
2. Pior: com o gatilho de Play Mode, o Unity recompila **já dentro do Play Mode**, faz domain reload
   e **mata a corrotina do probe no meio**. Nenhum relatório é gravado, e o Editor fica preso num
   reload que não termina — sem recompilar, sem consumir gatilho, sem erro no console. Levou ~40
   minutos até o Editor voltar a si.

**Use sempre o `unity-run.ps1` da raiz do projeto**, que espera a DLL ficar mais nova que o `.cs` mais
recente antes de disparar. Detalhes na memória `unity-automacao-sem-mcp`.

O `PlayModeTriggerWatcher` agora **avisa quando um gatilho espera há mais de 10 segundos, e por quê**
(`isCompiling`, `isUpdating`, `isPlaying`…). Era esse silêncio que fazia o Editor travado parecer uma
ferramenta quebrada.

### O "Montar Cena" pode rodar na cena errada — e rodou

O teste de Play Mode agora passa pelo título, e ao sair do Play Mode o Editor fica com a `MainMenu`
aberta. O gatilho monta **na cena ativa**, e como há Canvas nas duas, nada falhou: o comando
construiu o jogo inteiro (combate, jornada, sala de mapas) dentro da cena de título, que engordou de
250 KB para 800 KB **em silêncio**. Foi preciso `git checkout Assets/Scenes/MainMenu.unity`.

Corrigido em dois lugares: o gatilho **abre a cena do jogo** antes de montar, e o `GuildSceneSetup`
**recusa** rodar em qualquer outra, dizendo qual esperava e qual encontrou.

### O padrão que mais se repete no projeto: dado certo, exibição ausente

Já são **onze** ocorrências, todas com o relatório em "0 erros". As sete desta sessão:

1. Painéis de overlay a alfa 0,98 — 2% não se nota sobre sala escura, mas sobre um título em fonte 64
   o texto de baixo atravessa e briga com o de cima.
2. O glifo **✦** não existe na fonte do jogo e virava caixinha, em cinco lugares — um deles anterior
   a esta sessão. **Glifos que a fonte tem:** `◆ ◇ · →` e emoji (há fallback de emoji). **Não tem:**
   `✦`. Na dúvida, capture e olhe.
3. A dica do "Continuar" colada no subtítulo, lida como continuação da frase de abertura.
4. O retrato do combate deslocado meia altura pelo pivô.
5. Inimigos desenhados a 50px dentro de um card de 370.
6. A intenção do inimigo coberta pela própria criatura ampliada.
7. O popup "Prepare-se com cartas…" da jornada plantado no meio do campo de batalha — anterior a esta
   sessão, e visível em **toda** captura de combate desde que o popup existe.

**Sempre olhe `Assets/Screenshots/*.png` depois do Play Mode.** Detalhes na memória
`validar-ui-por-captura-de-tela`.

### Três armadilhas de UI do Unity que causaram três dos casos acima

1. **Pivô antes do rect.** Trocar `pivot` depois de aplicar anchors/offsets mantém a
   `anchoredPosition` e **move** o retângulo meia altura.
2. **Quem nasce depois é desenhado por cima.** Ordem de irmãos decide sobreposição — terceira vez que
   custa tempo neste projeto.
3. **`preserveAspect` encaixa o quadro inteiro**, incluindo a transparência em volta do desenho. Por
   isso existe `EnemyData.portraitScale`: sem ela o esqueleto virava um boneco de 50px.

### O componente que mora no painel que ele controla

Quatro telas nasceram com `Awake` chamando `panel.SetActive(false)`. Como o componente **é filho do
painel**, ligar o painel roda o `Awake` — que o desliga no mesmo frame. Sintoma: `pausa aberta: False`
sem erro nenhum. **Quem nasce fechado é a cena**, montada assim pelo setup.

E se o componente precisa de `Update` (o ESC da pausa), ele **não pode morar no painel**: `Update` só
roda em objeto ativo. Daí o objeto `PauseMenu`, sempre ativo, ao lado do painel.

### Singleton `DontDestroyOnLoad` + troca de cena = a segunda partida herda a primeira

`GuildManager`, `QuestManager` e `UIManager` sobrevivem à troca de cena. O `Awake` da instância nova
vê que já existe uma e **se autodestrói**, deixando a antiga viva com o estado da partida anterior e
escrevendo em UI já destruída. É a mesma família do painel das salas da Fase 2.5: **a primeira vez
funciona, a segunda não**, e nenhum teste percebe porque todos testam a primeira.

Quem resolve é `SceneFlow.DescartarMundo`. **Se algum manager novo virar `DontDestroyOnLoad`, ele
precisa entrar nessa lista.**

### O teste não pode custar o save do autor

O probe joga jornadas e termina runs de propósito — cada ciclo dispararia o autosave por cima da
partida real de quem estiver jogando na máquina. Daí `SaveSystem.AutosaveSuspenso`, ligado no `Awake`
do probe e respeitado por `Autosave()` e `DescartarAutosave()`. O teste do Santuário também fotografa
e repõe o perfil do autor, e confere que repôs.

### O inventário pode estar errado sobre o que existe no disco

O `ASSETS.md` afirmava, sobre `EnemyData.portrait`: *"Nenhum dos pacotes traz criatura 2D."* Era
**falso** — havia sete criaturas em pixel art já fatiadas. O combate ficou meses com caixas vazias no
lugar dos monstros por causa de uma linha de inventário que ninguém reconferiu. **Confira o disco
antes de aceitar "não existe" da documentação.**

### UI: a ordem de irmãos manda, e varredura genérica não funciona

Da sessão anterior, segue valendo. **A receita que funciona** (`HeroPanelSkin.cs`, `PartyCardSkin.cs`):
extrair a hierarquia do prefab **com a ordem de irmãos**, vestir o fundo **pelo nome** (nunca por
tamanho), e clarear o texto **no mesmo passo**. Arte grande entra **atrás** e, em card pequeno,
**translúcida**.

### Outras, de sessões anteriores

- O pacote **Dark Knight** foi escrito para Unity 6 e usava `Rigidbody2D.linearVelocity`, inexistente
  no 2022.3. As 3 ocorrências travavam o `Assembly-CSharp` inteiro. **Se o pacote for reimportado, o
  erro volta** (commit `d7ef2c4`).
- **O Unity não recompila sem foco**, e perde o foco assim que outro comando roda.
- **Compilar por fora**: o script precisa incluir as `<ProjectReference>` do `.csproj`, não só as
  `<HintPath>`. Sem isso o ESave dá 7 erros falsos.
- `Remove-Item` na mesma chamada PowerShell que um caminho em `C:\Program Files` é **bloqueado**.
- Mensagem de commit com aspas quebra a here-string do PowerShell. Use `git commit -F arquivo.txt`.
- `git add <pasta>` **não inclui o `.meta` da própria pasta**.

---

## Arquivos e comandos relevantes

### O que foi criado nesta sessão

| Arquivo | Papel |
|---|---|
| `Assets/Scripts/Save/SaveData.cs` | DTOs do arquivo de save (**campo novo só no fim** da classe) |
| `Assets/Scripts/Save/SaveSystem.cs` | Slots, escrita atômica (`.tmp` + troca), cabeçalhos, `AutosaveSuspenso` |
| `Assets/Scripts/Save/GameStateIO.cs` | A ponte entre os managers vivos e o arquivo |
| `Assets/Scripts/Save/PlayerProfile.cs` | Perfil fora dos slots: relíquias, destraves, recordes, opções |
| `Assets/Scripts/Save/GameSettings.cs` | Aplica áudio e vídeo |
| `Assets/Scripts/Save/SceneFlow.cs` | Título ⇄ jogo, e o descarte do mundo anterior |
| `Assets/Scripts/UI/MainMenuUI.cs` … `RelicShrineUI.cs` | As cinco telas de menu |
| `Assets/Scripts/UI/TurnOrderBar.cs` | A fila do round no combate |
| `Assets/Scripts/Core/MenuSceneSetup.cs` | Monta `MainMenu.unity` e os painéis compartilhados; registra a build |
| `Assets/Scripts/Core/EnemyArt.cs` | Preenche o retrato dos 11 inimigos; a tabela de escolhas mora aqui |
| `unity-run.ps1` | **Dirige o Editor com a compilação assentada** — use sempre este |

### Gatilhos (arquivo vazio na raiz, consumido pelo Editor ao ganhar foco)

`RunPlayModeTest` · `RunSmokeTest` · `RunSceneSetup` · `RunMenuSetup` · `RunEnemyArt` · `RunBarSkin` ·
`RunCardArt` · `RunPortraitCatalog` · `RunBiomeArt` · `RunUiSkinPrefabs` · `RunHeroPanelSkin` ·
`RunPartyCardSkin` · `RunCardFrameSkin` · `RunAudioCatalog` — todos `.trigger`, todos no `.gitignore`
(por curinga `*.trigger`).

**Depois de mexer na cena**, nesta ordem:

```powershell
.\unity-run.ps1 -Triggers "RunSceneSetup.trigger","RunMenuSetup.trigger","RunBarSkin.trigger","RunPlayModeTest.trigger"
```

O `Montar Cena` recria objetos, o `Montar Menus` refaz a cena de título e a lista da build, e o
`BarSkin` repõe os sprites das barras. **O `RunEnemyArt` sobrescreve** a arte dos inimigos (o menu do
Editor, não: ele respeita escolha feita à mão) — rode só ao mudar a tabela do `EnemyArt.cs`.

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

Avisos pré-existentes esperados (não são regressão): `goldtxt`, `reputationtxt` e `currentPopupCG`
nunca atribuídos, mais dezenas de avisos de pacotes de terceiros.

### Onde ficam os saves em execução

`C:\Users\Israel\AppData\LocalLow\Rapadura Atômica\Guilda da Corrupção\` — `saves\*.json` e
`profile.json`. Dá para apagar à mão para testar o jogo do zero.

---

## Pendências que dependem do usuário

- **Rodar o smoke test** (interrompido nesta sessão; a causa da quebra já foi corrigida).
- **Lobo, aranha e golem de pedra** — três dos onze inimigos usam arte que não os representa (goblin,
  olho voador e esqueleto tingido). Tabela e razões em `ASSETS.md`. Precisa de compra ou encomenda.
- **Deserto e Vulcão sem arte de bioma** — nenhum pacote importado cobre.
- **Ilustração de evento** (`EventData.eventImage`, 25 vazios) continua sem arte.
- **Animação dos inimigos** — os spritesheets já trazem Idle, Attack, Take Hit e Death; hoje só o
  primeiro quadro do Idle é usado. Decisão de escopo do autor, não limitação de material.
- **Git LFS** — 763 MB de binários no histórico. Funciona, mas não sai fácil depois.
- **Push** — 17 commits só no repositório local.
- **ESave e Bench**: importados, commitados e agora oficialmente **não usados**. Removê-los
  economizaria repositório — decisão do autor.
- **Conflito de estilo**: os retratos e os inimigos são **pixel art**, os ícones de carta e a UI são
  **pintados**. Funciona, mas é uma escolha estética que o autor pode querer revisar.
- **O `GDD.md` está atrás do código** em pontos que estas sessões não tocaram: as tabelas de status
  ainda dão as Fases 2, 2.5 e 3 como pendentes. Foram corrigidas só as linhas que o trabalho de
  menus, save e meta-progressão tornou falsas. Vale uma passada de auditoria.
