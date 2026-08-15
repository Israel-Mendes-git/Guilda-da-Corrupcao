# Handoff — Guilda da Corrupção: menus, save e campo de batalha (2026-08-15, sessão 2)

## Objetivo

Desenvolver o **Guilda da Corrupção** (Unity 2022.3.62f3): roguelike deckbuilder + gerência de
guilda, mistura declarada de *Darkest Dungeon* com *Slay the Spire*.

Projeto: `C:\Users\Israel\Documents\GitHub\Guilda-da-Corrupcao`.

Plano completo em **[`ROADMAP.md`](ROADMAP.md)**, design em **[`GDD.md`](GDD.md)**, inventário dos
assets em **[`ASSETS.md`](ASSETS.md)**. Leia os três — este handoff só cobre o que eles não contam.

---

## Estado atual

**Árvore limpa. 16 commits locais**, nenhum enviado a remoto (4 desta sessão, de `d62326c` a
`df27c7d`).

**Validado agora:** `PLAY MODE OK — nenhum erro capturado`, **0 falhas**, incluindo as seções novas
`SAVE E MENUS` e a trava da ordem do round. As sete capturas novas foram conferidas a olho.

### ✅ Fases 0, 1, 2, 2.5, 3, **3.5** e **3.6** concluídas

Duas frentes nesta sessão, ambas com detalhes, decisões e régua medida no `ROADMAP.md`:

- **Fase 3.5 — menus e save.** O jogo deixou de abrir direto na guilda: tem tela de título, pausa no
  ESC, opções de áudio e vídeo, save em arquivo (autosave + 3 slots) e o Santuário das Relíquias.
- **Fase 3.6 — o campo de batalha.** O combate ficou frente a frente como o de *Darkest Dungeon*:
  party à esquerda com a posição 1 encostada nos inimigos, inimigos à direita com corpo e intenção
  acima da cabeça, e a barra de ordem do round no alto. **A regra do combate não mudou** — escolha do
  autor: o grupo continua agindo junto com 5 de energia, e a barra é informativa.

### 🔄 Pendências desta sessão

| O quê | Situação |
|---|---|
| **Smoke test não foi re-executado** | O autor interrompeu a chamada. Ele estava quebrando por um bug de áudio em edit mode que **já foi corrigido** (`GameAudio` mudo fora do Play Mode); falta só rodar `RunSmokeTest.trigger` e conferir as 41 verificações |
| UI prefab a prefab | 🔄 **2 de 12** — inalterado desde a sessão anterior |
| Clareza e ritmo | Continua pendente do pedido do autor (ver abaixo) |

### 🔄 Não feito — pedido explícito do autor que segue pendente

Da sessão anterior, dois dos cinco blocos continuam de pé:

1. **Etapas da jornada** e **posições das coisas na jornada** — ajuste fino de layout.
2. **Clareza: o jogador saber o que fazer** — destacar a ação principal, indicar o que está pronto.
   O ponto mais visível segue lá: em `Assets/Screenshots/sala_taverna.png` há um botão genérico
   escrito **"Button"** no topo da tela.

---

## Próximos passos

1. **Rodar o smoke test** e confirmar `41 verificações, 0 falhas`. É a única prova que faltou nesta
   sessão.
2. **Clareza e ritmo** (o que o autor pediu e nunca foi feito).
3. **Continuar a UI prefab a prefab.** Faltam 10. Por retorno visual: `CardPurchasePrefab`,
   `QuestItemPrefab`, `PartyMemberSelectPrefab`. Receita na seção "Pegadinhas" abaixo.
4. **Ajuste fino no `HeroPanel`:** nome, classe e nível sobrepostos ao retrato.
5. **Fase 4** (`ROADMAP.md`): dar efeito a `corruptionExposure`, traços e personalidades.

---

## Decisões tomadas nesta sessão (e por quê)

- **Save próprio em JSON, não ESave nem Bench.** Escolha do autor entre as três opções. Um arquivo
  por slot em `Application.persistentDataPath/saves/`. Os dois pacotes continuam no disco,
  importados e **não integrados** — a pendência de "confirmar o ESave" está encerrada.
- **Autosave + 3 slots manuais.** O autosave é do jogo (o botão "Salvar" não escreve nele à mão);
  os três são do jogador.
- **`MainMenu.unity` como cena separada e cena 0 da build.** Sem isso a build continuaria abrindo na
  guilda com o menu inalcançável.
- **O perfil do jogador vive fora dos slots** (`PlayerProfile` → `profile.json`): relíquias,
  destraves, recordes e opções. Apagar um save não apaga o que as runs custaram a conquistar.
- **Não se salva no meio da estrada.** O estado interno da jornada não é serializado por ninguém e
  reconstruí-lo seria um projeto à parte. O botão da pausa aparece desligado **com o motivo escrito**.
- **As relíquias deixaram de virar ouro sozinhas** (era `relíquias × 5`, com teto) e agora são gastas
  no Santuário. Um bônus automático que também é moeda pune quem compra qualquer coisa. O ouro extra
  virou o destrave "Cofre da guilda".
- **Os quatro destraves caem em pontos que já existiam** (ouro inicial, reputação inicial,
  `maxRosterSize`, `questBoardSize`). Um destrave que exigisse sistema novo seria design a fazer.
- **`Time.timeScale` não é tocado na pausa.** O jogo é de turnos por clique: nada avança sozinho, e
  zerar a escala congelaria as corrotinas de transição — algumas responsáveis por avançar o fluxo.
- **A cena de título escala com a tela** (`ScaleWithScreenSize`, 1920×1080); a cena do jogo continua
  em pixels fixos, que é como ela sempre esteve.
- **O menu usa a trilha do hub.** Acrescentar `MusicContext.Menu` exigiria remontar o catálogo de
  áudio inteiro por um clipe que ainda não existe.
- Mantidas: alvo de letalidade 0,33–0,67 mortes/jornada; KPI de combate é **mortes por combate**;
  energia 5 / mão 6; acrescentar valor de enum **só no fim**; `-batchmode` rejeitado.

---

## Pegadinhas / lições

### O "Montar Cena" pode rodar na cena errada — e rodou

O teste de Play Mode agora passa pelo título, e ao sair do Play Mode o Editor fica com a `MainMenu`
aberta. O gatilho `RunSceneSetup` monta **na cena ativa**, e como há Canvas nas duas, nada falhou: o
comando construiu o jogo inteiro dentro da cena de título, que engordou de 250 KB para 800 KB em
silêncio. Foi preciso `git checkout` na cena para desfazer.

**Corrigido em dois lugares** e vale saber que existem: o gatilho **abre a cena do jogo** antes de
montar, e o `GuildSceneSetup` **recusa** rodar em qualquer outra, dizendo qual esperava e qual achou.

### Três armadilhas de UI que voltam sempre

1. **Pivô depois do rect move o retângulo.** Trocar o pivô mantém a `anchoredPosition`, então o
   elemento anda meia altura — foi o vazio de 80px entre o retrato e o nome, sem erro nenhum.
   **Pivô antes do rect.**
2. **Quem nasce depois é desenhado por cima.** A criatura ampliada cobria a intenção do inimigo.
   Terceira vez que ordem de irmãos custa tempo neste projeto.
3. **`preserveAspect` encaixa o quadro inteiro**, com toda a transparência em volta da criatura — por
   isso existe `EnemyData.portraitScale`. Sem ela o esqueleto virava um boneco de 50px num card de 370.

### A que mais custou tempo nesta sessão: gatilho consumido antes de o Unity recompilar

O watcher de gatilhos roda no `EditorApplication.update` e **pode consumir o arquivo antes de o Unity
perceber que os scripts mudaram**. Duas consequências, as duas vividas hoje:

1. O gatilho executa a **versão velha** do código, e o resultado parece um bug do que você acabou de
   escrever. (O `Montar Cena` rodou duas vezes com código antigo e o painel de pausa não aparecia.)
2. Pior: se o gatilho for o de Play Mode, o Unity recompila **já dentro do Play Mode**, faz domain
   reload e **mata a corrotina do probe no meio**. Nenhum relatório é gravado, e o Editor pode ficar
   preso num reload que não termina — sem recompilar, sem consumir gatilho, sem erro no console.

**A receita:** antes de criar qualquer gatilho, espere a `Library/ScriptAssemblies/Assembly-CSharp.dll`
ficar **mais nova que o `.cs` mais recente**, e só então dispare — mantendo o foco na janela o tempo
todo, dentro do mesmo comando. O script que faz isso está descrito em "Arquivos e comandos".

O `PlayModeTriggerWatcher` agora **avisa quando um gatilho está esperando há mais de 10 segundos, e
por quê** (`isCompiling`, `isUpdating`, `isPlaying`…). Era exatamente esse silêncio que fez o Editor
travado parecer uma ferramenta quebrada.

### O padrão que se repete: dado certo, exibição ausente

Já são **onze** ocorrências ao longo do projeto. As desta sessão, todas com o relatório em 0 falhas:

1. Painéis de overlay translúcidos (alfa 0,98) — o texto de baixo atravessava o de cima.
2. O glifo **✦** não existe na fonte e virava caixinha, em cinco lugares.
3. A dica do "Continuar" colada no subtítulo, lida como parte da frase de abertura.
4. O retrato do combate deslocado meia altura pelo pivô.
5. Inimigos desenhados a 50px dentro de um card de 370.
6. A intenção do inimigo coberta pela própria criatura.
7. O popup "Prepare-se com cartas…" da jornada plantado no meio do campo de batalha — **anterior a
   esta sessão**, e visível em toda captura de combate desde que o popup existe.

**Sempre olhe `Assets/Screenshots/*.png` depois de rodar o Play Mode.** Repetindo pela terceira
sessão seguida porque continua sendo verdade.

**Glifos que a fonte tem:** `◆ ◇ · → ⚔️ 🏆 💰 ⭐ ❄️ 🌲` e os demais emoji (há fallback de emoji).
**Não tem:** `✦`. Na dúvida, capture e olhe.

### O componente que mora no painel que ele controla

Quatro telas nasceram com `Awake` chamando `panel.SetActive(false)`. Como o componente **é filho do
painel**, ligar o painel roda o `Awake` — que o desliga no mesmo frame. Sintoma: `pausa aberta: False`
sem erro nenhum. **Quem nasce fechado é a cena**, montada assim pelo setup.

E se o componente precisa de `Update` (o ESC da pausa), ele **não pode morar no painel**: `Update` só
roda em objeto ativo, então a tecla só funcionaria depois de a tela já estar aberta. Daí o objeto
`PauseMenu`, sempre ativo, ao lado do painel.

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
do probe, respeitado pelo `Autosave()` e pelo `DescartarAutosave()`. O teste do Santuário também
fotografa e repõe o perfil do autor, e confere que repôs.

### UI: a ordem de irmãos manda, e varredura genérica não funciona

Da sessão anterior, segue valendo. **A receita que funciona** (`HeroPanelSkin.cs`, `PartyCardSkin.cs`):
extrair a hierarquia do prefab **com a ordem de irmãos**, vestir o fundo **pelo nome** (nunca por
tamanho), e clarear o texto **no mesmo passo**. Arte grande entra **atrás** e, em card pequeno,
**translúcida**.

### Erro de compilação que trava tudo

O pacote **Dark Knight** foi escrito para Unity 6 e usava `Rigidbody2D.linearVelocity`, inexistente
no 2022.3. As 3 ocorrências travavam o `Assembly-CSharp` inteiro. **Se o pacote for reimportado, o
erro volta** (commit `d7ef2c4`).

### Outras, da sessão anterior

- **O Unity não recompila sem foco**, e perde o foco assim que outro comando roda.
- **O Unity foi reiniciado por fora** numa sessão passada (pid mudou) — sempre redescubra o processo.
- **Compilar por fora**: o script precisa incluir as `<ProjectReference>` do `.csproj`, não só as
  `<HintPath>`. Sem isso o ESave dá 7 erros falsos.
- `Remove-Item` na mesma chamada PowerShell que um caminho em `C:\Program Files` é **bloqueado**.
- Mensagem de commit com aspas quebra a here-string do PowerShell. Use `git commit -F arquivo.txt`.
- `git add <pasta>` **não inclui o `.meta` da própria pasta**.

---

## Arquivos e comandos relevantes

**Documentação** — `ROADMAP.md` (plano e resultados medidos), `GDD.md` (design), `ASSETS.md`.

### O save, em cinco arquivos (`Assets/Scripts/Save/`)

| Arquivo | O que faz |
|---|---|
| `SaveData.cs` | os DTOs do arquivo (campo novo **só no fim** da classe) |
| `SaveSystem.cs` | slots, escrita atômica, cabeçalhos, `AutosaveSuspenso` |
| `GameStateIO.cs` | a ponte entre os managers vivos e o arquivo |
| `PlayerProfile.cs` | perfil fora dos slots: relíquias, destraves, recordes, opções |
| `GameSettings.cs` | aplica áudio e vídeo |
| `SceneFlow.cs` | título ⇄ jogo, e o descarte do mundo anterior |

**Telas** (`Assets/Scripts/UI/`): `MainMenuUI`, `PauseMenuUI`, `OptionsUI`, `SaveSlotsUI`,
`RelicShrineUI`. **Montagem** (`Assets/Scripts/Core/`): `MenuSceneSetup.cs`.

### Gatilhos (arquivo vazio na raiz + trazer o Unity à frente)

`RunPlayModeTest` · `RunSmokeTest` · `RunSceneSetup` · **`RunMenuSetup`** · **`RunEnemyArt`** ·
`RunBarSkin` · `RunCardArt` · `RunPortraitCatalog` · `RunBiomeArt` · `RunUiSkinPrefabs` ·
`RunHeroPanelSkin` · `RunPartyCardSkin` · `RunCardFrameSkin` · `RunAudioCatalog` — todos `.trigger`,
todos no `.gitignore` (agora por curinga `*.trigger`).

**Depois de mexer na cena**, rode nesta ordem: `RunSceneSetup` → `RunMenuSetup` → `RunBarSkin` →
`RunPlayModeTest`. O `Montar Cena` recria objetos, o `Montar Menus` refaz a cena de título e a lista
da build, e o `BarSkin` repõe os sprites das barras.

**O `RunEnemyArt` sobrescreve** a arte dos inimigos (o menu do Editor, não: ele respeita escolha
feita à mão). Rode só quando mudar a tabela do `EnemyArt.cs`.

### Disparar gatilho com a compilação assentada (obrigatório — ver Pegadinhas)

O script usado nesta sessão ficou no scratchpad e vale recriar. O laço essencial:

```powershell
# 1. esperar a DLL ficar mais nova que o .cs mais recente, mantendo o foco
$dll = "$root\Library\ScriptAssemblies\Assembly-CSharp.dll"
$cs  = Get-ChildItem "$root\Assets\Scripts" -Recurse -Filter *.cs |
       Sort-Object LastWriteTime -Descending | Select-Object -First 1
while ((Get-Item $dll).LastWriteTime -le $cs.LastWriteTime) {
  [void][WinRun]::SetForegroundWindow($unity.MainWindowHandle); Start-Sleep -Milliseconds 900
}
# 2. folga de ~6s para o domain reload terminar e os watchers se reinscreverem
# 3. só então criar o .trigger, e esperar o RELATÓRIO mudar (não só o gatilho sumir)
```

Foco e espera **em comandos separados falham**. `SetForegroundWindow` vem de `user32.dll` via
`Add-Type`.

### Compilar sem abrir o Editor (segundos)

```powershell
$root = "C:\Users\Israel\Documents\GitHub\Guilda-da-Corrupcao"
$out  = "C:\Users\Israel\AppData\Local\Temp\gol-build"
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

### Onde ficam os saves em execução

`C:\Users\Israel\AppData\LocalLow\Rapadura Atômica\Guilda da Corrupção\` — `saves\*.json` e
`profile.json`. Dá para apagar à mão para testar o jogo do zero.

---

## Pendências que dependem do usuário

- **Rodar o smoke test** (interrompido nesta sessão; a causa da quebra já foi corrigida).
- **Deserto e Vulcão sem arte de bioma** — nenhum pacote importado cobre.
- **Lobo, aranha e golem de pedra** — três dos onze inimigos usam arte que não os representa
  (goblin, olho voador e esqueleto tingido). Tabela e razões em `ASSETS.md`.
- **Ilustração de evento** (`EventData.eventImage`, 25 vazios) continua sem arte.
- **Animação dos inimigos** — os spritesheets já trazem Idle, Attack, Take Hit e Death; hoje só o
  primeiro quadro do Idle é usado. Foi decisão de escopo do autor, não limitação de material.
- **Git LFS** — 763 MB de binários no histórico.
- **Push** — 16 commits só no repositório local.
- **Conflito de estilo**: retratos em pixel art, ícones e UI pintados.
- **O `GDD.md` está atrás do código** em pontos que esta sessão não tocou: as tabelas de status ainda
  dão as Fases 2, 2.5 e 3 como pendentes. Nesta sessão só foram corrigidas as linhas que o trabalho
  de menus e save tornou falsas (§4.4, meta-progressão e persistência). Vale uma passada de auditoria.
- **ESave e Bench**: importados, commitados e agora oficialmente **não usados**. Removê-los
  economizaria repositório — decisão do autor.
