# Handoff — Guilda da Corrupção: as salas viram lugar (2026-08-26)

## Objetivo

Desenvolver o **Guilda da Corrupção** (Unity 2022.3.62f3): roguelike deckbuilder + gerência de
guilda, mistura declarada de *Darkest Dungeon* com *Slay the Spire*.

Projeto: `C:\Users\Israel\Documents\GitHub\Guilda-da-Corrupcao`.

Plano e histórico verificável em **[`ROADMAP.md`](ROADMAP.md)**, design em **[`GDD.md`](GDD.md)**,
inventário dos assets em **[`ASSETS.md`](ASSETS.md)**. Leia os três — este handoff só cobre o que
eles não contam.

---

## Estado atual

**Fases 0 a 3.8 concluídas.** Nada enviado a remoto até aqui.

**Validado em 26/08, 23:32:**

| Prova | Resultado |
|---|---|
| `SmokeTestReport.txt` | **45 verificações, 0 falhas** · letalidade 0,54 mortes/jornada (alvo 0,33–0,67) |
| `PlayModeReport.txt` | **PLAY MODE OK — nenhum erro capturado**, jornada completa, seis salas abertas e compradas |
| Compilação por fora (Roslyn) | exit 0, só os avisos antigos de campo do Inspector |
| Capturas | as seis salas, o combate, a jornada e os menus, todas olhadas uma a uma |

### O que a Fase 3.8 entregou

As quatro salas restantes seguiram o molde da Forja (**fila → foco → efeito visível**): Taverna,
Biblioteca, Cemitério e Sala de Mapas. Junto vieram o **estoque por ciclo** (`CycleStock`), as
**cartas de 17 para 40**, o **`CardRole`** (baralho montado por função, não só por raridade) e a
**ilustração dos 25 eventos**. Detalhes e números no `ROADMAP.md`.

### 🔄 Pendências reais

| O quê | Situação |
|---|---|
| **O Mercado** | É a única sala que não passou pelo molde: dez linhas com "COMPRAR". Já usa o estoque por ciclo |
| **Duas poções de cura** | "Poção de cura" (70, cura na guilda) e "Poção de Cura" (60, levada na estrada) na mesma prateleira. Nomes quase iguais, efeitos diferentes |
| **Tela de baralhos** (`Panel_DeckManager`) | Nunca foi refeita: botões brancos escritos "Button", rótulos sobrepostos, cartas fora do lugar. Ver `Assets/Screenshots/tela_deck.png` |
| **Nomes das 23 cartas novas** | Provisórios e descritivos, à espera do autor |
| **Cartas de Ladino e Bardo** | Não existem. Por isso a taverna deixou de oferecer as duas classes |
| **UI prefab a prefab** | 2 de 12, parado desde agosto |
| **Ilustração de evento** | Resolvida para os 25 de `Resources/Events`; os eventos montados em código (`EventPool`) seguem sem arte, por desenho |

---

## Próximos passos

1. **O Mercado no molde das outras seis** — é o que sobrou da frente das salas, e o caso que o autor
   apontou como o mais gritante.
2. **A tela de baralhos** — hoje é a tela mais quebrada do jogo, e aparece em toda preparação.
3. **Fase 4** (`ROADMAP.md`): dar efeito a `corruptionExposure`, traços e personalidades — tudo já
   acumula e ninguém lê.
4. **Fase 5**: cartas de Ladino e Bardo, eventos e chefes para Deserto, Tundra e Vulcão, áudio.

---

## Pegadinhas / lições

### Quando um número oscila, procure o *n* efetivo antes de aumentar o *n* aparente

Três execuções do smoke test, sem nada mudar no jogo, deram 0,41 · 0,74 · 0,51 mortes por jornada —
oscilação do tamanho do alvo inteiro. A causa não eram as 200 jornadas: `RodarJornadas` reciclava
**25 grupos**, sorteados de novo a cada execução, e era a composição desse elenco que mandava. Com
100 grupos e 1000 jornadas a dispersão caiu para ±0,03. É a quarta vez que o instrumento, e não o
jogo, produziu a conclusão errada (ver as outras três no `ROADMAP.md`).

### O padrão que mais se repete no projeto: dado certo, exibição ausente

Já são **quinze** ocorrências, todas com o relatório em "0 erros". As quatro desta sessão:

1. Os painéis das salas a alfa 0,98 — o rodapé da guilda atravessava cada uma. O alfa fica
   **serializado na cena**, então mudar a constante não bastou: `FindOrCreatePanel` passou a repor o
   alfa dos painéis que já existem.
2. O texto vazio do Cemitério transbordando a caixa pelos dois lados: `EnsureText` só configura
   quebra de linha ao **criar** o objeto, e num objeto que já existe na cena ninguém liga o
   `enableWordWrapping`.
3. 23 das 40 cartas sem ícone — a tabela do `CardArt` só conhecia as 17 antigas.
4. "Custo m□dio" e "Ca□ador" na tela de baralhos: `DeckManager.cs` ficou fora da reencodagem da
   Fase 0 e tinha 16 caracteres corrompidos.

**Sempre olhe `Assets/Screenshots/*.png` depois do Play Mode.**

### O teste que passa porque testa o caminho certo

O smoke test conferia o baralho das quatro classes jogáveis e passava — enquanto a taverna sorteava
**seis**, incluindo Ladino e Bardo, que não têm carta nenhuma. O recruta entrava com o baralho de
emergência do `DeckGenerator` (oito cópias de um "ataque básico" criado em memória) pelo salário
cheio. **Quando um teste enumera casos à mão, confira se é a mesma lista que o jogo usa.**

### Antes de corrigir o que a captura mostra, confirme que é defeito

Duas coisas pareciam bug e não eram: a descrição do evento cortada no meio da palavra é o efeito de
máquina de escrever (`JourneyManager.TypeText`) pego pela captura, e o 🏹 ao lado de um Curandeiro é
o ícone de **fileira** (`PartyFormation.PreferenceIcon`), não de classe — decisão deliberada, para
não dizer a classe duas vezes.

### O gatilho é consumido antes de o Unity recompilar

O watcher roda no `EditorApplication.update` e **não espera o refresh de assets**: o gatilho executa
a versão velha do código, e com o de Play Mode o Unity recompila já dentro do Play Mode, mata a
corrotina do probe e não grava relatório nenhum.

**Use sempre o `unity-run.ps1` da raiz**, que espera a DLL ficar mais nova que o `.cs` mais recente
antes de disparar.

### Outras, de sessões anteriores

- **O "Montar Cena" pode rodar na cena errada.** Corrigido em dois lugares, mas a armadilha existe:
  o gatilho abre a cena do jogo antes de montar, e o `GuildSceneSetup` recusa rodar em qualquer outra.
- **Singleton `DontDestroyOnLoad` + troca de cena = a segunda partida herda a primeira.** Quem
  resolve é `SceneFlow.DescartarMundo`; **manager novo que vire `DontDestroyOnLoad` precisa entrar
  nessa lista.**
- **O componente que mora no painel que ele controla** se desliga no próprio `Awake`. Quem nasce
  fechado é a cena. E quem precisa de `Update` (o ESC da pausa) não pode morar no painel.
- **Pivô antes do rect** — trocar o pivô depois move o retângulo. **Quem nasce depois é desenhado por
  cima.** **`preserveAspect` encaixa o quadro inteiro**, transparência incluída.
- **Glifos:** a fonte tem `◆ ◇ · →` e emoji; **não tem `✦`**.
- **Campo público é serializado**: mudar o valor no script não muda a cena — altere os dois.
- **Acrescentar valor de enum só no fim**: os assets guardam o número.
- **Uma run de Play Mode é n=1** — balanceamento se mede no simulador.
- O pacote **Dark Knight** foi escrito para Unity 6; se for reimportado, os 3 erros de
  `Rigidbody2D.linearVelocity` travam o `Assembly-CSharp` inteiro.
- **`git add <pasta>` não inclui o `.meta` da própria pasta.** Mensagem de commit com aspas quebra a
  here-string do PowerShell: use `git commit -F arquivo.txt`.

---

## Arquivos e comandos relevantes

### Criados na Fase 3.8

| Arquivo | Papel |
|---|---|
| `Assets/Scripts/Core/Rooms/TavernRoom.cs` … `MapRoom.cs` | Montam as quatro salas na cena (só Editor) |
| `Assets/Scripts/Core/CycleStock.cs` | O estoque de cada sala neste ciclo — sorteio estável, fora do save |
| `Assets/Scripts/Data/CardRole.cs` | Para que a carta serve num baralho: ataque, defesa, suporte, utilidade |
| `Assets/Scripts/Core/EventArt.cs` | Preenche `EventData.eventImage` nos 25 eventos |
| `Assets/Scripts/UI/EventArtBackdrop.cs` | Desenha essa arte atrás do texto, em runtime |

### Gatilhos (arquivo vazio na raiz, consumido pelo Editor ao ganhar foco)

`RunPlayModeTest` · `RunSmokeTest` · `RunSceneSetup` · `RunMenuSetup` · `RunCardCreator` ·
`RunCardArt` · `RunEventArt` · `RunEnemyArt` · `RunMapArt` · `RunItemArt` · `RunEventBalance` ·
`RunBarSkin` · `RunPortraitCatalog` · `RunBiomeArt` · `RunUiSkinPrefabs` · `RunHeroPanelSkin` ·
`RunPartyCardSkin` · `RunCardFrameSkin` · `RunAudioCatalog` — todos `.trigger`, todos no
`.gitignore`.

**Depois de mexer na cena**, nesta ordem:

```powershell
.\unity-run.ps1 -Triggers "RunSceneSetup.trigger","RunMenuSetup.trigger","RunBarSkin.trigger","RunPlayModeTest.trigger"
```

**O `RunEnemyArt` sobrescreve** a arte dos inimigos — rode só ao mudar a tabela do `EnemyArt.cs`. O
`RunCardCreator` e o `RunEventArt` **nunca** sobrescrevem o que já existe.

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

- **Push** — os commits seguem só no repositório local.
- **Nomes das 23 cartas novas** e o nome da poção de cura antiga do Mercado.
- **Lobo, aranha e golem de pedra** — três dos onze inimigos usam arte que não os representa.
- **Deserto e Vulcão sem arte de bioma** — nenhum pacote importado cobre.
- **Animação dos inimigos** — os spritesheets trazem Idle, Attack, Take Hit e Death; hoje só o
  primeiro quadro do Idle é usado. Decisão de escopo, não falta de material.
- **Git LFS** — 763 MB de binários no histórico. Funciona, mas não sai fácil depois.
- **ESave e Bench**: importados, commitados e oficialmente **não usados**. Removê-los economizaria
  repositório.
- **Conflito de estilo**: retratos e inimigos são **pixel art**; ícones de carta, UI e as cenas de
  evento são **pintados**.
</content>
