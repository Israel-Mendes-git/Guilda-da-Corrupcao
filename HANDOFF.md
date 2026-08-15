# Handoff — Guilda da Corrupção: arte, áudio, UI e a Fase 3 (2026-08-15)

## Objetivo

Desenvolver o **Guilda da Corrupção** (Unity 2022.3.62f3): roguelike deckbuilder + gerência de
guilda, mistura declarada de *Darkest Dungeon* com *Slay the Spire*.

Projeto: `C:\Users\Israel\Documents\GitHub\Guilda-da-Corrupcao`.

Plano completo em **[`ROADMAP.md`](ROADMAP.md)**, design em **[`GDD.md`](GDD.md)** (v1.1), inventário
dos assets importados em **[`ASSETS.md`](ASSETS.md)**. Leia os três — este handoff só cobre o que
eles não contam.

---

## Estado atual

**Árvore limpa. 12 commits nesta sessão** (de `915ebe4` a `c7b2a76`), nenhum enviado a remoto.

**Validado agora:** `PLAY MODE OK — nenhum erro capturado` e `SMOKE TEST OK — 41 verificações,
0 falhas`.

### ✅ Fases 0, 1, 2, 2.5 e **3** concluídas e verificadas

A **Fase 3 (a run)** foi implementada e testada nesta sessão. O jogo deixou de ser sandbox infinito:
tem relógio (Corrupção global), Chefe Supremo alcançável, três condições de derrota, tela de fim de
run e meta-progressão. Detalhes e a régua medida estão no `ROADMAP.md`.

### ✅ Arte, áudio e UI (Fase 5, parcial)

| Área | Estado |
|---|---|
| Ícones das 17 cartas | ✅ todas preenchidas, aparecendo na tela |
| Retratos de herói | ✅ 500 catalogados; aparecem na taverna e como fundo do card do grupo |
| Barras HP/estresse/XP | ✅ vestidas com o kit Bloodlines |
| Molduras de carta | ✅ frente com o kit; sleeve reservado ao verso |
| Biomas | ✅ 5 dos 7 (falta Deserto e Vulcão — sem arte em nenhum pacote) |
| Áudio | ✅ trilha por contexto + SFX; o jogo era **totalmente mudo** |
| UI prefab a prefab | 🔄 **2 de 12** (`HeroPanel`, `PartyStatusPrefab`) |

### 🔄 Não feito — pedido explícito do autor que ficou pendente

O autor pediu cinco blocos; três foram entregues. **Faltam:**

1. **Etapas da jornada** e **posições das coisas na jornada** — ajuste fino de layout.
2. **Clareza: o jogador saber o que fazer** — destacar a ação principal, indicar o que está pronto.

Ele marcou os quatro eixos de "fluxo" (clareza, ritmo, estrutura, feedback). **Estrutura** (Fase 3) e
**feedback** (áudio) foram feitos; **clareza** e **ritmo** não.

---

## Próximos passos

1. **Clareza e ritmo** (o que o autor pediu e não foi feito). O ponto mais visível: na captura
   `Assets/Screenshots/sala_taverna.png` há um botão genérico escrito **"Button"** no topo da tela —
   sobrou de algum layout e não faz nada de óbvio.
2. **Continuar a UI prefab a prefab.** Faltam 10. Por retorno visual: `CardPurchasePrefab`
   (biblioteca), `QuestItemPrefab` (quadro de missões), `PartyMemberSelectPrefab` (preparação) —
   as três aparecem a cada ciclo. **Receita que funciona** na seção "Pegadinhas".
3. **Ajuste fino pendente no `HeroPanel`:** nome, classe e nível ficam sobrepostos ao retrato. É
   reposicionamento dentro do prefab.
4. **Fase 4** (`ROADMAP.md`): dar efeito a `corruptionExposure`, traços e personalidades — tudo já
   acumula e ninguém lê.

---

## Decisões tomadas (e por quê)

- **Sleeves só no verso** (escolha do autor). A frente da carta usa o kit Bloodlines; os dois versos
  do `Card_Shirts_Lite` são madeira e metal alaranjados, e puxariam a carta para longe da paleta
  dessaturada. **Rejeitado:** usar Card_Shirts na frente.
- **Retrato do herói sai do NOME, não de sorteio** — assim o mesmo herói mantém a cara entre
  execuções, e o elenco vira reconhecível. É o efeito Darkest Dungeon.
- **Catálogos guardam referências, não cópias** (`PortraitCatalog`, `BiomeArtCatalog`,
  `AudioCatalog`). **Rejeitado:** copiar 500 retratos para `Resources` — duplicaria 10 MB e criaria
  duas cópias para manter em sincronia.
- **Meta-progressão em `PlayerPrefs`, não ESave.** O ESave está no projeto e **nunca foi confirmado**
  pelo autor como o save oficial. `MetaProgression` é o único ponto a trocar quando for.
- **"Sem heróis" sozinho não encerra a run** — enquanto houver ouro para recrutar, a guilda tem
  saída. É o que torna o ouro uma reserva de vida.
- **Barra de vida é sangue** — o código tingia o preenchimento de verde/amarelo/vermelho por faixa de
  HP, o que pintava de verde um sprite que é vermelho. Agora só a Beira da Morte escurece.
- **763 MB de binários entraram no histórico do git** (decisão do autor de commitar). Funciona, mas
  não sai fácil depois — vale considerar Git LFS antes que cresça.
- Mantidas: alvo de letalidade 0,33–0,67 mortes/jornada; KPI de combate é **mortes por combate**, não
  taxa de vitória; energia 5 / mão 6; acrescentar valor de enum **só no fim**; `-batchmode` rejeitado.

---

## Pegadinhas / lições desta sessão

### O padrão que mais custou tempo: dado certo, exibição ausente

**Quatro vezes** nesta sessão o dado estava correto e não havia quem o mostrasse. Em todos os casos o
`PlayModeReport` dizia "0 erros" — **só a captura de tela denunciou**:

1. As 17 cartas tinham `cardImage` preenchido e nada aparecia (faltava quem exibisse).
2. A barra de estresse mostrava 100 para um herói com 0 — bug que existia **antes** desta sessão.
3. `biomeIcon` tinha campo e nunca foi montado na cena.
4. O `ApplyKit` existia e não pegava quase nada.

**Sempre olhe `Assets/Screenshots/*.png` depois de rodar o Play Mode.** O relatório de texto não
substitui isso.

### UI: a ordem de irmãos manda, e varredura genérica não funciona

Duas tentativas de vestir as caixas brancas por regra genérica foram **revertidas por piorarem**:

- Vestir o fundo com a pedra do kit **apagou o texto** — em vários prefabs o fundo não é o pai dos
  textos, e sim um irmão desenhado **depois** deles.
- Só pintar de `BoxColor` deixou **texto escuro sobre fundo escuro**, porque a cor do texto vem do
  prefab e não acompanha.

**A receita que funciona** (usada em `HeroPanelSkin.cs` e `PartyCardSkin.cs`): extrair a hierarquia do
prefab **com a ordem de irmãos**, vestir o fundo **pelo nome** (nunca por tamanho), e clarear o texto
**no mesmo passo**. Arte grande sempre entra **atrás** e, em card pequeno, **translúcida** — retrato
em tamanho cheio no card do grupo ficou ilegível e virou fundo a 34%.

### Fase 3: medir o efeito, não o mecanismo

O `RunManager` estava certo desde o começo e o teste passou em tudo — menos numa linha:
`corrupção das missões: 1–39 (global 61)`. O relógio andava e o quadro de missões ficava parado, então
o mundo piorar **não mudava nada do que o jogador via**. Corrigido com `RenovarQuadro()` a cada ciclo.

### Erro de compilação que trava tudo

O pacote **Dark Knight** foi escrito para Unity 6 e usava `Rigidbody2D.linearVelocity`, que não existe
no 2022.3. As 3 ocorrências **travavam o `Assembly-CSharp` inteiro** — o código do jogo não compilava
por causa de um asset de terceiros. **Se o pacote for reimportado, o erro volta** (commit `d7ef2c4`).

### Automação sem o MCP do Unity

O **MCP do Unity não estava conectado** nesta sessão. Todo o trabalho foi feito por **gatilhos de
arquivo** + foco de janela. Ver "Arquivos e comandos".

- **O Unity não recompila sem foco**, e perde o foco assim que outro comando roda. O que funciona é
  trazer a janela à frente **e esperar dentro do mesmo comando**, em laço, até a
  `Library/ScriptAssemblies/Assembly-CSharp.dll` ficar mais nova que o `.cs`. Foco e espera em
  comandos separados **falham**.
- **O Unity foi reiniciado por fora** no meio da sessão (pid mudou). Scripts que guardam o pid dão
  "não recompilou" falso — sempre redescubra o processo.
- **Compilar por fora**: o script precisa incluir as `<ProjectReference>` do `.csproj`, não só as
  `<HintPath>`. Sem isso o ESave dá 7 erros falsos.
- `Remove-Item` na mesma chamada PowerShell que um caminho em `C:\Program Files` é **bloqueado** pelo
  sandbox. Separe em dois comandos.
- Mensagem de commit com aspas quebra a here-string do PowerShell. Use `git commit -F arquivo.txt`.
- `git add <pasta>` **não inclui o `.meta` da própria pasta**. Sem ele o Unity regenera GUIDs em
  qualquer clone e quebra todas as referências.

---

## Arquivos e comandos relevantes

**Documentação** — `ROADMAP.md` (plano e resultados medidos), `GDD.md` (design), `ASSETS.md` (o que
cada pacote traz e onde entra).

### Ferramentas criadas nesta sessão (todas em `Assets/Scripts/Core/`)

| Arquivo | O que faz |
|---|---|
| `BarSkin.cs` | veste as barras **e hospeda o watcher de todos os gatilhos** |
| `CardArt.cs` | preenche `cardImage` das 17 cartas |
| `PortraitCatalogBuilder.cs` / `Data/PortraitCatalog.cs` | catálogo dos 500 retratos |
| `BiomeArtBuilder.cs` / `Data/BiomeArtCatalog.cs` | arte por bioma |
| `AudioCatalogBuilder.cs` / `Data/AudioCatalog.cs` | trilhas e SFX + import settings |
| `GameAudio.cs` | tocador (música com fade + efeitos) |
| `RunManager.cs`, `RunFlow.cs`, `MetaProgression.cs`, `UI/RunEndUI.cs` | a Fase 3 |
| `HeroPanelSkin.cs`, `PartyCardSkin.cs`, `CardFrameSkin.cs`, `UiSkinPrefabs.cs` | UI |

### Gatilhos (arquivo vazio na raiz + trazer o Unity à frente)

`RunPlayModeTest.trigger` · `RunSmokeTest.trigger` · `RunSceneSetup.trigger` · `RunBarSkin.trigger` ·
`RunCardArt.trigger` · `RunPortraitCatalog.trigger` · `RunBiomeArt.trigger` · `RunUiSkinPrefabs.trigger` ·
`RunHeroPanelSkin.trigger` · `RunPartyCardSkin.trigger` · `RunCardFrameSkin.trigger` · `RunAudioCatalog.trigger`

Todos no `.gitignore`. Relatórios: `PlayModeReport.txt` e `SmokeTestReport.txt` (também ignorados).

**Depois de mexer na cena**, rode nesta ordem: `RunSceneSetup` → `RunBarSkin` → `RunPlayModeTest`.
O `Montar Cena` recria objetos e o `BarSkin` repõe os sprites das barras.

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

---

## Pendências que dependem do usuário

- **Confirmar o ESave** como save oficial. Está em `Assets/Esper/ESave`, commitado e **não
  integrado**. A meta-progressão usa `PlayerPrefs` até lá.
- **Deserto e Vulcão sem arte de bioma** — nenhum pacote importado cobre. Precisa de compra ou
  encomenda.
- **Retratos de inimigo** (`EnemyData.portrait`, 11 vazios) e **ilustração de evento**
  (`EventData.eventImage`, 25 vazios) continuam sem arte.
- **Git LFS** — 763 MB de binários no histórico.
- **Push** — 12 commits só no repositório local.
- **Conflito de estilo**: os retratos são **pixel art**, os ícones de carta e a UI são **pintados**.
  Funciona (retrato em moldura), mas é uma escolha estética que o autor pode querer revisar.
