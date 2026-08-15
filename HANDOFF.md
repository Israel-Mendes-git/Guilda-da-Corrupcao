# Handoff — Guilda da Corrupção: fases 0–2.5 fechadas, KPI decidido, Fase 3 a começar (2026-08-15)

## Objetivo

Desenvolver o **Guilda da Corrupção** (Unity 2022.3.62f3): roguelike deckbuilder + gerência de
guilda, mistura declarada de *Darkest Dungeon* com *Slay the Spire*.

Projeto: `C:\Users\Israel\Documents\GitHub\Guilda-da-Corrupcao`.

O plano de trabalho vive em **[`ROADMAP.md`](ROADMAP.md)** e o design em **[`GDD.md`](GDD.md)**
(v1.1). Leia os dois antes de continuar — este handoff só cobre o que eles não contam.

---

## Estado atual

### ✅ Tudo que estava pendente foi fechado

**Fases 0, 1, 2 e 2.5** — implementadas, validadas e **commitadas**. Nada em aberto do ciclo
anterior. A run que faltava (`PlayModeReport.run43-fase25-validada.txt`, 15/08 01:18) confirmou os
três pontos que o handoff anterior deixou como "não verificado":

| Prova | Resultado |
|---|---|
| Painel da sala, 2ª abertura | `ordem entre irmãos: 7 de 7 — na frente`, alcançável — **o bug reportado saiu** |
| Tela de balanço | `jornada encerrada pela tela de balanço: '🏆 A GUILDA VOLTA VITORIOSA'` |
| Escolha de despojo | 2 opções, botão travado até escolher, liberado depois, ouro 2855 → 2956 |
| Jornada termina sozinha | `painel ainda ativo ao fim: False (iterações: 134)` |
| Console | 0 erros, 0 exceções |

**Smoke test:** `41 verificações, 0 falhas` e — pela primeira vez — **nenhum aviso de balanceamento**.
Letalidade em 0,47 mortes/jornada (alvo 0,33–0,67).

### Commits desta sessão

O projeto tinha 4 commits e nenhum do trabalho novo. Agora tem, separados por origem como combinado:

```
6299a30 KPI de combate passa a ser mortes por combate, nao taxa de vitoria
ed584c1 Identidade do projeto e pacotes trazidos pelo autor
0d8e9ab Importa o ESave, pacote de save de terceiros
d786d6d Fases 0-2.5: XP, carta cruza escolha, chefe rebalanceado e pos-jornada
```

Árvore limpa. **Nada foi enviado para remoto** — nenhum `push` foi dado.

---

## Próximos passos

1. **Fase 3 — a run** (`ROADMAP.md`): `RunManager`, Corrupção global como relógio, três condições de
   fim de run, Chefe Supremo plugado (`QuestGenerator.GenerateBossQuest` existe e nunca é chamado) e
   meta-progressão. **O ESave está no projeto e commitado**, então a meta-progressão deixou de estar
   bloqueada — falta confirmar se é ele mesmo que deve ser usado (ver pendências).
2. **Vigiar a letalidade a cada mudança de conteúdo.** O ponto de equilíbrio se move: com 1,2 lutas
   por jornada a energia certa era 4; com 2,7, é 5. Ao acrescentar ou remover combates, **remeça**.

---

## Decisões tomadas (e por quê)

- **O KPI de combate é `mortes por combate`, não taxa de vitória** (escolha do autor, 15/08). O alvo
  de 35–75% veio do *Slay the Spire*, onde perder a luta encerra a run; aqui a party só perde quando
  os quatro caem e a Beira da Morte segura cada um por um golpe. A régua nova e o porquê de cada
  alvo estão no `ROADMAP.md`. **O detalhe que importa:** o *piso* do chefe mora na simulação de
  **jornada** (0,10, medido 0,23) e não na de combate, porque no combate isolado ele cobra de 0,02 a
  0,06 conforme a execução — ruído que reprovaria por sorteio. Os *tetos* ficam no combate isolado.
- **Commits separados por origem**, não por fase (escolha do autor, 15/08): as fases 0–2.5 mexeram
  nos mesmos arquivos, e separá-las exigiria dividir hunks à mão, com risco de commits que não
  compilam.
- Mantidas de sessões anteriores: alvo de letalidade 0,33–0,67 para a jornada inteira; a alavanca é
  dar ferramentas ao jogador, não enfraquecer o inimigo (energia 5, mão 6); o chefe mantém a saída
  narrativa, mas cara; opção travada nunca some da tela; `Revive` não ressuscita; ferimento não sara
  por sorteio; quem fica na guilda descansa; moldes de linha da tela de balanço são objetos inativos
  na cena, não prefabs em disco; ordem de irmãos (não `sortingOrder`); a ordem de `selectedParty`
  **é** a formação; acrescentar valor de enum **só no fim**; `-batchmode` rejeitado.

---

## Pegadinhas / lições desta sessão

- **O instrumento envelhece junto com a tela que ele mede.** Duas runs seguidas pararam no limite de
  600 iterações com a jornada aparentemente sem fim — 305 eventos numa jornada de 6 dias, party
  morta, estresse em 92. Parecia regressão grave de letalidade. **Não era o jogo:** a Fase 2.5 trocou
  a saída da jornada de `UIManager.resultPopup` para `JourneyResultUI`, e o laço do `PlayModeProbe`
  só sabia fechar os popups do `UIManager`. A jornada terminava certo, a tela de balanço abria, e o
  probe clicava um botão morto até estourar. **Ao trocar a tela por onde um fluxo termina, atualize
  quem o dirige automaticamente.**
- **Um detector de travamento cego ao caminho que trava é pior que nenhum.** O detector contava
  cliques em opções de evento e nós de mapa, mas não no `endTurnButton` — e `EndTurn` é um no-op
  quando a jornada não espera escolha. O laço girava sem nunca acusar. Corrigido, e o
  `DumpStuckState` agora despeja a máquina de estados inteira: foi a linha `jornadaEncerrada=True`
  com o painel ainda ativo que resolveu o caso em segundos.
- **Amostra pequena não sustenta piso.** O primeiro alvo que escrevi para o chefe (piso 0,02
  mortes/combate) passou raspando numa execução e teria reprovado na seguinte: 4 a 12 mortes em 200
  lutas é ruído. Piso exige sinal forte; teto tolera ruído. **Antes de fixar um alvo, rode duas
  vezes e veja a oscilação.**
- **Mudança de fim de linha polui o `git status`.** Nove dos "95 arquivos modificados" eram só LF→
  CRLF, sem mudança de conteúdo — o `git add -A` normalizou e eles sumiram. Confira com
  `git diff HEAD --stat` antes de tratar volume de diff como trabalho.
- **O Unity não recompila sem foco**, e perde o foco de volta assim que outro comando roda. O que
  funciona é trazer a janela à frente **e esperar dentro do mesmo comando**, em laço, até a
  `Library/ScriptAssemblies/Assembly-CSharp.dll` ficar mais nova que o `.cs`. Fazer o foco e a espera
  em comandos separados falha.
- **`Remove-Item` na mesma chamada que um caminho em `C:\Program Files` é bloqueado** pelo sandbox.
  Separe em dois comandos.
- Continuam valendo: `Tools ▸ Card Creator` sobrescreve assets de carta se a opção for marcada;
  gatilho esquecido dispara Play Mode ao ganhar foco; campo público é serializado (mudar no script
  **não** muda a cena); uma run de Play Mode é n=1 — balanceamento se mede no simulador.

---

## Arquivos e comandos relevantes

**Documentação** — `ROADMAP.md` (plano, diagnóstico e resultados medidos), `GDD.md` (design, v1.1).

**Compilar sem abrir o Editor** (segundos). O `.rsp` precisa ser **regerado quando um `.cs` novo é
criado**:

```powershell
$root = "C:\Users\Israel\Documents\GitHub\Guilda-da-Corrupcao"
$out  = "C:\Users\Israel\AppData\Local\Temp\gol-build"
$csproj = Get-Content "$root\Assembly-CSharp.csproj" -Raw
$refs = [regex]::Matches($csproj, '<HintPath>(.*?)</HintPath>') | ForEach-Object { $_.Groups[1].Value }
$defines = [regex]::Match($csproj, '<DefineConstants>(.*?)</DefineConstants>').Groups[1].Value
$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('-target:library'); $lines.Add('-nostdlib+'); $lines.Add('-langversion:9.0')
$lines.Add('-out:"C:/Users/Israel/AppData/Local/Temp/gol-build/AssemblyCheck.dll"')
foreach ($d in $defines -split ';') { if ($d.Trim()) { $lines.Add("-define:$($d.Trim())") } }
foreach ($r in $refs) { $lines.Add("-r:`"$r`"") }
foreach ($f in (Get-ChildItem "$root\Assets\Scripts" -Recurse -Filter *.cs)) { $lines.Add("`"$($f.FullName)`"") }
$lines | Out-File "$out\build.rsp" -Encoding utf8
Set-Location $root
& "C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Data\NetCoreRuntime\dotnet.exe" `
  "C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Data\DotNetSdkRoslyn\csc.dll" `
  "@C:/Users/Israel/AppData/Local/Temp/gol-build/build.rsp"
```

**Os dois testes rodam por gatilho de arquivo**, sem tocar no menu do Editor. Crie o arquivo vazio na
raiz e traga a janela do Unity à frente; ele é consumido em ~1 s e grava o relatório na raiz:

| Gatilho | Relatório | O que mede | Custo |
|---|---|---|---|
| `RunSmokeTest.trigger` | `SmokeTestReport.txt` | 41 verificações + 200 jornadas + 800 combates | ~1 min |
| `RunPlayModeTest.trigger` | `PlayModeReport.txt` | telas por raycast, jornada completa, console limpo | ~2 min |

Ambos estão no `.gitignore`. **Não** chame refresh depois de criar o gatilho. Varreduras de
parâmetro continuam por `execute_code` do MCP (que **não estava conectado** nesta sessão):

```csharp
GuildSmokeTest.VarrerCombate(new int[]{4,5,6}, new int[]{5,6}, 300)   // energia × mão
GuildSmokeTest.VarrerEscalaDeChefe(new float[]{1.0f,1.3f,1.6f}, 200)  // força do chefe
```

**Aplicar mudanças de layout na cena** — editar `GuildSceneSetup.cs`, recompilar, e então:

```csharp
GuildSceneSetup.Setup(false);
UnityEditor.AssetDatabase.SaveAssets();
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(
    UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
```

---

## Pendências que dependem do usuário

- **Confirmar o ESave como o pacote de save da Fase 3.** Está em `Assets/Esper/ESave`, commitado, e
  **ainda não integrado**: a persistência do jogo segue limitada aos decks (`PlayerPrefs`,
  `DeckRepository`).
- **Push**: os 4 commits novos estão só no repositório local.
- **Arte e áudio**: seguem placeholder e inexistente, respectivamente.
