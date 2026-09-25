# Handoff — Guilda da Corrupção: o que falta na jornada, discutido e não decidido (2026-09-13)

## Objetivo

Desenvolver o **Guilda da Corrupção** (Unity 2022.3.62f3): roguelike deckbuilder + gerência de
guilda, mistura declarada de *Darkest Dungeon* com *Slay the Spire*.

Projeto: `C:\Users\Israel\Documents\GitHub\Guilda-da-Corrupcao`.

Plano e histórico em **[`ROADMAP.md`](ROADMAP.md)** (a fase 3.16 é a última), design em
**[`GDD.md`](GDD.md)** (versão 2.4), o mundo em **[`MUNDO.md`](MUNDO.md)**, arte pendente em
**[`ARTE.md`](ARTE.md)**. Leia os quatro — este handoff só cobre o que eles não contam.

**A próxima sessão não tem uma tarefa de construção pronta.** Ela tem uma conversa a continuar:
o que falta na jornada, discutido abaixo e explicitamente **não decidido**.

---

## Estado atual

**A fase 3.16 (estrada com luta, cartas fora do combate, regra de cada área) está commitada e
verde**, commit `7a7fb34`. Detalhe completo no `ROADMAP.md` e na memória
`regras-de-area-e-cartas-decisoes-13-09`.

**Não commitado agora:**
- `Assets/Fonts/SegoeUIEmoji SDF.asset` — muda a cada Play Mode, reverta sempre:
  `git checkout -- "Assets/Fonts/SegoeUIEmoji SDF.asset"`.
- `Assets/Scenes/SampleScene.unity` — apareceu modificado depois do commit `7a7fb34`, sem que eu
  tenha rodado `RunSceneSetup` de novo depois dele. Provavelmente drift de Play Mode/autosave do
  Editor (ordem de objetos, GUID). **Não investigado.** Antes de mexer: `git diff Assets/Scenes/SampleScene.unity`
  para ver se é ruído ou se carrega mudança de verdade; se for ruído, `git checkout --`.

**Depois de commitar a fase 3.16, o autor avaliou:** *"creio que ainda está muito do mesmo, realmente
teve uma melhora, tem algo faltando na jornada."* É a segunda vez que essa queixa aparece (a
primeira, 10/09, está em `o-jogo-esta-morto-diagnostico-do-autor`). A fase 3.16 mexeu na proporção
de combate e nos contadores do topo, mas não na estrutura por trás — todo ponto da rota, luta ou
texto, continua sendo a mesma caixa com opções.

---

## Próximos passos

**Não há passo de código pronto para seguir.** O próximo passo é retomar a conversa sobre o que
falta na jornada, com o autor, a partir dos quatro pontos abaixo — nenhum foi aprovado para
construção, mesmo os que ele marcou.

### Os quatro pontos levantados em 13/09 (detalhe na memória `jornada-ainda-repetitiva-apos-fase-3-16`)

Eu levantei quatro coisas que continuam iguais depois da fase 3.16 e perguntei quais o autor sente
falta. Ele marcou três das minhas opções e escreveu uma nota livre. **Marcar "o que falta" não é
aprovar "o que construir agora"** — a pergunta era de diagnóstico, não de escopo.

1. **A luta não paga no lugar** (eu recomendei este; o autor confirmou). Vencer só rende ouro no
   balanço final da jornada — o baralho nunca muda na estrada, só na guilda. Ideia não detalhada:
   ao vencer, escolher 1 de 3 cartas das classes do grupo, ou recusar (como o Slay the Spire).
   **Falta:** desenhar o que exatamente é oferecido, se toda luta ou só algumas, se a Biblioteca
   fica sem função por isso.
2. **O ponto de luta ainda abre caixa de texto primeiro** (autor marcou). Hoje "Enfrentar em
   combate" é um botão entre vários numa caixa de evento — a captura `Assets/Screenshots/jornada_mao.png`
   mostra isto ("Alcateia Faminta", cinco opções, uma delas é lutar). Proposta não decidida: o nó
   de combate abrir a luta direto, sem caixa, e as opções narrativas ficarem só nos eventos de
   texto puro.
3. **Cada tipo de ponto é o mesmo objeto por dentro** (autor marcou). Descanso, Tesouro, Mercador
   e História rodam o mesmo `EventData`/`EventOutcome`; só o `JourneyEventType` muda o rótulo no
   mapa. Ideia não detalhada: Descanso vira a fogueira de verdade (curar ou melhorar uma carta),
   Mercador vira loja com ouro, Tesouro vira escolha de relíquia, e entraria a luta de elite
   (opcional, difícil, relíquia garantida).
4. **Energia por parada, não por jornada** — eu propus, o autor **não marcou** esta. Hoje são 5 de
   energia para a jornada inteira (5 a 16 dias), e o jogador joga ~3,5 cartas no total. Ficou fora
   das prioridades dele, mas ele escreveu depois: *"aqueles pontos discutidos ainda são
   recorrentes"* — o que sugere que nenhum dos quatro, incluindo este, deve ser descartado sem
   conversar de novo.

**Como abrir a próxima sessão:** relembrar os quatro pontos (não assumir que o autor os lembra
palavra por palavra), perguntar qual ele quer construir primeiro e com que desenho — a régua dele
para implementação é decidir o escopo e ver na tela, não aprovar um design completo de antemão
(memória `estilo-de-documento-do-autor`).

---

## Decisões tomadas (e por quê)

**Nenhuma decisão nova nesta parte da sessão.** As decisões da fase 3.16 (a estrada, o pagamento
com carta, as sete regras de área) estão na memória `regras-de-area-e-cartas-decisoes-13-09`, e
continuam de pé — não foram revistas pela queixa de 13/09, só consideradas insuficientes sozinhas.

**O que já foi rejeitado e não deve ser reproposto:** nada nesta rodada — os quatro pontos estão
todos em aberto, nenhum foi descartado pelo autor.

---

## Pegadinhas / lições desta sessão

- **Marcar uma opção numa pergunta de diagnóstico não é aprovar a construção dela.** A pergunta era
  "o que falta", com uma opção marcada por mim como "Recomendado" — o autor confirmar o
  recomendado não é sinal verde para implementar sem mais conversa, especialmente quando a resposta
  seguinte dele foi "aqueles pontos discutidos ainda são recorrentes", reforçando o conjunto inteiro
  em vez de fechar nos três marcados.
- Seguem valendo as lições da fase 3.16 (ver `HANDOFF.md` anterior via `git show 6c155fe:HANDOFF.md`
  se precisar reler): heredoc do Bash quebra com aspas simples ou barras nesta máquina (memória
  `heredoc-bash-colapsa-barras-no-windows`); a régua nova de mortes por área no smoke test já existe
  e vale reusar se qualquer um dos quatro pontos mexer em combate; Play Mode com compilação quebrada
  roda o assembly antigo; a fonte de emoji muda a cada Play Mode.

---

## Arquivos e comandos relevantes

### Onde os quatro pontos entrariam, se construídos

| Arquivo | Papel |
|---|---|
| `Assets/Scripts/Core/JourneyManager.cs` | `BuildChoices`, `ShowEvent`, `StartCombatForCurrentEvent` — onde a caixa de evento e a opção de combate se montam hoje |
| `Assets/Scripts/Data/EventData.cs` | `EventOutcome`, `JourneyEventType` — a estrutura única por trás de todo tipo de ponto |
| `Assets/Scripts/Core/CombatManager.cs` | `EndCombat`, `DistribuirEspolio` — onde uma recompensa de carta ao vencer entraria |
| `Assets/Scripts/UI/JourneyMapUI.cs` | `DescribeType`, `GetIcon` — como o mapa rotula cada tipo de ponto hoje |
| `Assets/Resources/Events/` | os 25 eventos; nenhum precisa mudar se a mudança for estrutural (o código lendo diferente), mas alguns podem precisar de campo novo |
| `Assets/Scripts/Core/GuildSmokeTest.cs` | qualquer regra de combate/recompensa nova precisa entrar aqui também, ou a letalidade mede outro jogo |

### Gatilhos e relatórios

Sem mudança desde a fase 3.16 — ver `ROADMAP.md` fase 3.16 ou o `HANDOFF.md` anterior
(`git show 6c155fe:HANDOFF.md`) para a lista completa de gatilhos, a receita de compilar sem abrir
o Editor, e onde ficam os saves.

---

## Pendências que dependem do autor

- **Qual dos quatro pontos construir primeiro, e com que desenho.** Nenhum está pronto para
  implementação — todos precisam de uma rodada de perguntas de escopo antes.
- **O que fazer com o diff não explicado em `Assets/Scenes/SampleScene.unity`** — investigar antes
  de descartar ou commitar.
- **Vetos pendentes da fase 3.16** (não resolvidos nesta conversa): metade dos pontos em luta,
  carta sem troco, o que cada selo queima, o descanso que cura 20%, o Covil como área mais mortal,
  a cópia da Torre — lista completa no handoff anterior e na memória `regras-de-area-e-cartas-decisoes-13-09`.
- **Push** — os commits seguem só no repositório local.
