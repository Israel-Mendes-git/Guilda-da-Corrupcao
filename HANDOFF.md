# Handoff — Guild of Legends: combate medido de verdade e telas destravadas (2026-08-02)

## Objetivo

Desenvolver o **Guild of Legends** (Unity 2022.3.62f3): roguelike deckbuilder + gerência de guilda,
**mistura declarada de Darkest Dungeon com Slay the Spire**.

Projeto: `C:\Users\Israel\Documents\GitHub\My project (1)`
**Não é repositório git** — não há histórico; as mudanças só existem no disco.

---

## Estado atual

Última verificação: `PlayModeReport.txt` de **2026-08-02 20:47** — `PLAY MODE OK — nenhum erro
capturado`, console limpo, jornada completa, todas as telas abrem/fecham e a saída pela vitória é
confirmada por raycast. Smoke test: **38 verificações, 0 falhas, 0 avisos**.

### ⚠️ Não verificado ainda

A **última alteração do `DeckManager`** (permitir cópias da mesma carta e mostrar a coleção inteira)
**compilou mas ainda não passou por Play Mode** — ela é posterior ao relatório das 20:47. Rodar o
teste antes de considerá-la pronta.

### Parte 1 — O combate era medido errado (concluído)

A sessão anterior deixou como próximo passo *"o combate não é vetor de dificuldade — 200/200
vitórias; confirmar com o autor antes de mexer"*. **A premissa estava errada e a conclusão
invertida.**

O `GuildSmokeTest.SimulateCombats` escolhia livremente a melhor carta de dano do **baralho inteiro** a
cada turno e ignorava mão de 5 cartas, compra, bloqueio dos dois lados, as quatro intenções do
inimigo (Atacar/AtacarTodos/**Defender**/**Estressar**), o estresse, a formação e o desgaste da
estrada. Toda omissão pesava a favor do jogador. Reescrito para seguir o `CombatManager`, o mesmo
conteúdo media **24% de vitória contra chefes com 3,37 mortes por combate**.

A causa real: **4 das 16 cartas não faziam nada.** O `CombatManager.ResolveCard` não tinha `case`
para `Buff` nem `None` — caíam no `default`, mas a energia era paga e a carta gasta, enquanto a
descrição prometia um efeito.

| Carta | Custo | Agora |
|---|---|---|
| Fúria (Warrior) | 1 | `Buff` — bônus de dano do grupo, com prazo (`combatDuration` 0 → 2) |
| Olhar de Águia (Hunter) | 1 | `BuffNextCard` (13) — próxima carta +50% |
| Teleporte (Mage) | 3 | `Evade` (14) — ignora o próximo golpe |
| Purificação (Healer) | 2 | `Cleanse` (15) — tira a aflição e alivia 25 de estresse |

**Implementar as cartas resolveu o balanceamento sozinho**, sem tocar em HP de chefe, dano ou energia:
chefes foram de 24% para **~39%** de vitória (alvo 35–75%) e o combate encurtou de 20,3 para ~16
turnos.

### Parte 2 — Telas e heróis destravados (concluído, exceto o item acima)

Auditoria via Play Mode revelou que metade das telas não funcionava:

| Problema | Causa | Correção |
|---|---|---|
| Ficha do herói **inalcançável** | `HeroDetailPanel.Instance` é setado no `Awake`, mas o componente mora num painel **inativo** — o `Awake` nunca rodava, o singleton ficava nulo e `PartyMemberCard.OnCardClick` usava `?.`, então o clique não fazia nada | `Instance` resolvido sob demanda com `Resources.FindObjectsOfTypeAll`; clique passa pelo `UIManager.ShowHeroDetail` |
| Ficha fechava sozinha | `Start()` chamava `HidePanel()` no frame seguinte à primeira abertura | wiring movido para `Awake`, sem `HidePanel` |
| **Impossível contratar** na Taverna | `GuildManager.maxRosterSize` estava **4** na cena (o script diz 8) e o `GameInitializer` cria 4 heróis: a guilda nascia cheia | corrigido para 8 **na cena** |
| Taverna sem renovar candidatos | `refreshButton`/`refreshCostText` nulos, botão inexistente na cena; a cobrança estava comentada | `GuildSceneSetup.BuildTavern()` cria `Btn_Refresh`/`Txt_RefreshCost`; novo `TavernManager.PayToRefresh()` cobra 50 |
| **Gerenciador de Deck vazio** (0 heróis, 0 cartas) | `RefreshHeroList()` só roda dentro de `OpenDeckManager()`, e `UIManager.ShowDeckManager()` apenas ativava o painel, sem nunca chamá-la | `ShowDeckManager` chama `OpenDeckManager()`, que também seleciona o primeiro herói |
| **Tela em branco** ao trocar de sala | Duas corrotinas de animação concorrentes: a de fechar terminava depois e desativava o painel recém-aberto — e "abrir" só agia se o objeto estivesse inativo, então a reabertura era ignorada. **O próprio código já resolvia isso para popups** e nunca aplicou aos painéis | uma animação por painel (`panelAnimations`), cancelando a anterior |
| Telas atrás do rodapé | `Panel_DeckManager`, `Journey` e `Library` são irmãos **anteriores** a `Panel_DownBar` dentro de `Background` | `SetPanelActive` faz `SetAsLastSibling()` ao abrir |
| Coleção de cartas sempre vazia | Só há 4 cartas por classe e o deck gerado usa as 4 com cópias; `RefreshCollectionDisplay` escondia o que já estava no deck | mostra o acervo inteiro; cópias permitidas até `MaxCopiasPorCarta = 4` |

**Party sem teto, com custo** (item do handoff anterior): a seleção **já não tinha teto**. Faltava o
custo — implementado em `PartyFormation.DailyRations(partySize)`: acima de 4 heróis, cada bloco de 4
soma uma ração diária (5–8 heróis comem 2/dia). A tela de preparação avisa antes de fechar a mochila.

Resultado medido: contratação `roster 4 → 5`, gerenciador de deck com `5 heróis / 11 cartas`, ficha do
herói abrindo pelo retrato, party de 5 com o extra na retaguarda.

---

## Próximos passos

1. **Rodar o Play Mode** para validar a mudança de cópias no `DeckManager` (ver "Não verificado").
2. **Implementar progressão de XP — decisão do autor tomada nesta sessão, ainda NÃO implementada.**
   Hoje `hero.level` é atribuído em `GameInitializer`/`HeroFactory`/Taverna e **nunca sobe** (não há
   `level++` em lugar nenhum). O autor aprovou: heróis ganham XP ao concluir jornadas e sobem de
   nível. O nível já tem efeito pronto no jogo — `HeroFactory` (HP máximo), `DeckGenerator` (tamanho
   do deck `8+level`, libera raras no 3 e lendárias no 5) e requisitos de missão. Junto disso, fechar
   o item: **o herói além do 4º rende menos XP** (o custo em rações já está feito).
3. **Limpar o ruído de log — decisão do autor tomada, ainda NÃO implementada.** `QuestSelectionUI`
   escreve ~15 linhas por abertura, e `DeckGenerator.GenerateDeckForHero` uma por deck gerado. Isso
   encheu o `Editor.log` em **76 MB** numa única sessão travada. Remover os `Debug.Log` de
   diagnóstico de `QuestSelectionUI`, `DeckGenerator` e `MapManager`, mantendo avisos e erros reais.
4. **Arte**: retratos, ilustrações de carta e de inimigo seguem placeholder.
5. Barras de progresso do kit (`Assets/Alebardium/Bloodlines UI/Textures/Progress_Bar/`) ainda não
   usadas — as barras de HP/estresse do combate continuam retângulos chapados.
6. `MapManager.OnUpgradeButtonClick` só mostra "custa 500 ouro" e não faz nada — melhorar salas não
   está implementado.
7. Encontros normais estão em ~90–94% de vitória. Dentro do alvo, mas é o lado fácil da curva: se um
   dia o combate precisar de mais mordida, é ali que sobra espaço, não nos chefes.

---

## Decisões tomadas (e por quê)

- **Medir antes de mexer.** A decisão sobre dificuldade ia ser tomada sobre um número falso.
  Consertar a régua veio primeiro; depois disso o ajuste nem foi preciso.
- **Acrescentar ao enum `CombatEffectType` só no fim** — os valores são gravados como número nos
  assets; inserir no meio trocaria o efeito de toda carta já configurada.
- **O bônus de dano não empilha** — repetir a Fúria renova o prazo em vez de dobrar o dano.
- **Só carta que fere recebe os bônus** (`CombatManager.DealsDamage`). A Fúria carrega `combatDamage`
  sem atacar; sem isso, jogar Olhar de Águia e depois Fúria queimava o +50% à toa.
- **O simulador chama `CombatManager.DealsDamage`** em vez de repetir a lista — foi esse tipo de
  duplicata que deixou o simulador divergir do jogo.
- **Faixas de `Expect` largas de propósito** — com 200 amostras por célula a variação entre execuções
  é de ~3 pontos; um teto apertado acusa sorteio como regressão.
- **Cópias de carta permitidas no editor de deck** — o `DeckGenerator` já monta baralhos com cópias;
  proibi-las tornava a remoção de uma carta irreversível.
- **Demitir herói passa por confirmação** — é irreversível e leva o deck junto.
- **Grupos reciclados no smoke test** (`SimParty`) — recriar a party a cada run gerava milhares de
  decks e o `DeckGenerator` loga a cada um; só isso travava o Editor por minutos.
- Mantidas de sessões anteriores: ordem de irmãos (não `sortingOrder`) para pôr popup na frente;
  repintar só o que está claro demais; a ordem de `selectedParty` **é** a formação; formação
  enfraquece mas não bloqueia; mapa ramificado livre; deck híbrido; dano de fome em 5;
  **`-batchmode` rejeitado**.

---

## Pegadinhas / lições desta sessão

- **Não chamar `refresh_unity` depois de criar o `RunPlayModeTest.trigger`.** O reimport entra em fila
  e roda **já dentro do Play Mode**; o domain reload zera os singletons do probe e o teste fica preso
  na preparação regerando missões — **sem um único erro no console**. Custou duas runs. Compilar
  antes, criar o gatilho, e só trazer a janela à frente (`SetForegroundWindow` + `AttachThreadInput`).
  Sintoma no `Editor.log`: `Trigger detectado` sem `Jornada iniciada` depois, e um
  `ImportOutOfDateAssets` no meio da execução.
- **`FindObjectOfType` não acha componentes em objetos inativos.** Foi por isso que o relatório dava
  `TavernManager` e `HeroDetailPanel` como "AUSENTE" quando os dois estavam na cena. Usar
  `Resources.FindObjectsOfTypeAll` para inspecionar.
- **Singleton em painel desativado nasce nulo** — `Awake` só roda quando o objeto é ativado. Combinado
  com `Instance?.Metodo()`, a falha é silenciosa e a tela vira inalcançável.
- **Um simulador que não replica as regras mede a si mesmo, não o sistema.** Memória:
  `simulador-que-nao-replica-o-jogo`.
- **Efeito de carta sem `case` vira carta morta e ninguém percebe.** O smoke test agora tem trava:
  *"toda carta faz algo em combate"*. Memória: `cartas-sem-efeito-no-combate`.
- **Com erro de compilação, o Unity entra em Play Mode com o assembly ANTIGO** — sempre conferir o
  console antes de ler um relatório. Memória: `compilacao-quebrada-play-mode-antigo`.
- **`onClick.Invoke()` não prova que o jogador consegue clicar** — usar `EventSystem.RaycastAll`
  (`PlayModeProbe.ReportarAlcancavel()`).
- **A receita de compilar por fora só cobre `Assets/Scripts`**, e precisa rodar **a partir da raiz do
  projeto** (o `.rsp` usa caminhos relativos).
- **NUNCA editar `.cs` com o Play Mode rodando**: o domain reload zera os singletons.
- **Campos públicos são serializados**: `maxRosterSize` estava 4 na cena com o script dizendo 8. Ao
  mudar um valor de jogo, atualizar a cena também.
- Demais armadilhas de automação: memória `unity-mcp-armadilhas-automacao`.

---

## Arquivos e comandos relevantes

**Alterados nesta sessão** (nenhum commit — o projeto não é git):

Combate e balanceamento
- `Assets/Scripts/Core/GuildSmokeTest.cs` — simulador reescrito (`SimulateOneCombat`, `EscolherCarta`,
  `JogarCarta`, `SimParty`, `SimBuffs`), trava de cartas inertes, alvos com faixa
- `Assets/Scripts/Core/CombatManager.cs` — `Buff`/`BuffNextCard`/`Evade`/`Cleanse`, `DealsDamage`,
  expiração do bônus em `EndPlayerTurn`, esquiva em `DamageHero`, `NeedsHeroTarget`
- `Assets/Scripts/Data/CardData.cs` — três valores novos no fim de `CombatEffectType`
- `Assets/Resources/Cards/`: `Hunter/Olhar de Águia`, `Mage/Teleporte`, `Healer/Purificação`
  (efeito), `Warrior/Fúria` (`combatDuration` 0 → 2)

Telas e heróis
- `Assets/Scripts/Core/UIManager.cs` — `SetPanelActive` com uma animação por painel + `SetAsLastSibling`;
  `ShowDeckManager` chama `OpenDeckManager()`
- `Assets/Scripts/Core/HeroDetailPanel.cs` — `Instance` sob demanda, wiring no `Awake`, estresse e
  estado mental na ficha, demissão com confirmação
- `Assets/Scripts/Core/DeckManager.cs` — `Instance` sob demanda, wiring no `Awake`, `OpenDeckManager`
  seleciona o primeiro herói, cópias até `MaxCopiasPorCarta`
- `Assets/Scripts/Core/TavernManager.cs` — `PayToRefresh` cobra, `OnEnable` não re-sorteia à toa,
  `RefreshCardInteractivity`
- `Assets/Scripts/Core/PartyMemberCard.cs` — clique via `UIManager`
- `Assets/Scripts/Core/GuildSceneSetup.cs` — `BuildTavern()`
- `Assets/Scripts/Core/PartyFormation.cs` — `DailyRations(partySize)`
- `Assets/Scripts/Core/JourneyManager.cs` — `DailyRationCost()` no consumo diário
- `Assets/Scripts/UI/QuestSelectionUI.cs` — aviso do custo de rações da party grande
- `Assets/Scripts/Core/PlayModeProbe.cs` — `TestTavern`, `TestLibrary`, `TestMapRoom`,
  `TestDeckManager`, `TestHeroDetail`

**Alterado na CENA** (já salvo): `GuildManager.maxRosterSize` 4 → 8; Taverna ganhou `Btn_Refresh` e
`Txt_RefreshCost`.

**Compilar sem abrir o Editor** (segundos) — rodar **a partir da raiz do projeto**:
```
Set-Location "C:\Users\Israel\Documents\GitHub\My project (1)"
& "C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Data\NetCoreRuntime\dotnet.exe" `
  "C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Data\DotNetSdkRoslyn\csc.dll" `
  "@C:/Users/Israel/AppData/Local/Temp/gol-build/build.rsp"
```
Receita para recriar o `.rsp`: memória `validar-compilacao-sem-abrir-unity`.

**Balanceamento**: `Tools/Guild of Legends/Rodar Smoke Test` — 200 jornadas + 800 combates em quatro
cenários (party descansada/desgastada × normal/chefe). Leva ~1 minuto.

**Aplicar mudanças de layout na cena** — editar `GuildSceneSetup.cs`, `refresh_unity`, conferir o
console, e então via `execute_code`:
```csharp
GuildSceneSetup.Setup(false);
UnityEditor.AssetDatabase.SaveAssets();
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(
    UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
```

**Teste em Play Mode**: criar `RunPlayModeTest.trigger` na raiz → trazer a janela do Unity à frente →
**não chamar refresh depois**. Gera `PlayModeReport.txt` e capturas em `Assets/Screenshots/`.

Relatórios preservados como `PlayModeReport.runNN*.txt` — desta sessão: `run34-antes-cartas`,
`run35-cartas-implementadas`, `run36-auditoria-telas`.

---

## Pendências que dependem do usuário

- **Arte**: retratos, ilustrações de carta e de inimigo.
- **Considerar versionar o projeto com git.** Segue sem histórico, agora com uma biblioteca grande de
  assets de terceiros no disco. Um `.gitignore` de Unity (ignorando `Library/`, `Temp/`, `obj/`)
  tornaria trivial reverter uma importação que quebra a compilação.
- As decisões sobre **XP** e **limpeza de logs** já foram tomadas pelo autor (ver Próximos passos 2 e
  3) — não precisam ser perguntadas de novo, só implementadas.
