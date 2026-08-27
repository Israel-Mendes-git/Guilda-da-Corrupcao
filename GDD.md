# Game Design Document — Guilda da Corrupção

Unity 2022.3.62f3 · URP · pt-BR · PC · Versão 2.0 — 26/08/2026 (substitui a 1.1, de 14/08)

Descreve o código como ele está em 21/08/2026, conferido contra o
`SmokeTestReport.txt` (41 verificações, 0 falhas) e o `PlayModeReport.txt`
(nenhum erro) daquela data. Plano de trabalho em [`ROADMAP.md`](ROADMAP.md),
inventário de arte em [`ASSETS.md`](ASSETS.md).

**[IMPLEMENTADO]** existe e funciona · **[PARCIAL]** existe pela metade, ou
existe e ninguém usa · **[PLANEJADO]** decidido, não construído.

## 1. O que é

Roguelike de gerência de guilda com combate por cartas — *Darkest Dungeon* na
guilda, *Slay the Spire* na estrada. O jogador é o mestre: recruta heróis, monta
os baralhos deles, escolhe quem parte, para onde e com que provisões. Ele não
controla o herói em campo; controla a preparação e as decisões do caminho.

| Pilar | O que significa em jogo |
|---|---|
| Decisões com peso permanente | Herói que morre não volta, e leva as relíquias dele |
| Preparação acima da execução | Quem vai, em que ordem e com qual baralho decide a jornada |
| A Corrupção como relógio | Um medidor que sobe a cada ciclo e encerra a partida no máximo |

**Sobre a ficção:** o tema é dark fantasy com uma Corrupção que se alastra. Os
nomes de regiões, chefes e eventos que estão nos assets são de trabalho — o world
building segue como decisão em aberto do autor, e este documento descreve o jogo
por estrutura e função. **[PLANEJADO]**

## 2. Os três loops **[IMPLEMENTADO]**

```
PARTIDA      nova guilda → ciclos → a Corrupção enche ou a guilda cai → memórias → nova guilda
 └ CICLO     guilda (salas) → preparar expedição → jornada → volta e balanço → +1 ciclo
    └ JORNADA rota ramificada → evento ou combate a cada nó → chefe no fim
```

Um ciclo é uma jornada concluída. É o pulso da partida: é ele que faz a Corrupção
avançar.

## 3. A partida **[IMPLEMENTADO]** (`RunManager`, `RunFlow`)

| Régua | Valor |
|---|---|
| Corrupção inicial · por ciclo · por evento marcado | 10 · +6 · +3 |
| Máximo | 100 |
| Chefe Supremo entra no quadro | Corrupção ≥ 60 (por volta do 9º ciclo) |
| Partida inteira | ~15 jornadas até o mundo saturar |

Fim da partida: **derrota** quando a Corrupção chega a 100, quando a reputação
zera, ou quando não há herói vivo **e** falta ouro para recrutar (menos de 30);
**vitória** ao derrotar o Chefe Supremo. Vem então uma tela de balanço com ciclos,
mortos, corrupção final e o botão de nova guilda. A reputação, que por muito tempo
só era exibida, hoje encerra a partida quando zera.

### 3.1 Meta-progressão **[IMPLEMENTADO]** (`MetaProgression`)

A moeda entre partidas é a **memória**: `ciclo × 3`, mais 25 por vencer. Fica no
perfil do jogador, fora dos slots de save — apagar uma partida não apaga o que as
anteriores custaram. Gasta-se no **Santuário**, na tela de título, e o que se
compra vale para a próxima guilda fundada, não para a que está em andamento.

| Destrave | Efeito por nível | Níveis | Custo |
|---|---|---|---|
| Cofre da guilda | +120 de ouro inicial | 3 | 8 · 16 · 28 |
| Renome antigo | +25 de reputação inicial | 2 | 10 · 20 |
| Alojamentos | +1 vaga no roster | 2 | 12 · 24 |
| Contatos na estrada | +1 missão no quadro | 1 | 20 |

Os quatro caem em pontos que já existiam no jogo: destrave que exigisse sistema
novo seria design a fazer, não meta-progressão a ligar.

**[PLANEJADO]** Destravar cartas do acervo da Biblioteca e as classes Ladino e
Bardo — as duas dependem de conteúdo que ainda não existe (§7).

## 4. Sessão: menus, pausa e save **[IMPLEMENTADO]**

- **Tela de título** em cena própria (cena 0 da build): continuar, nova guilda,
  slots, opções e Santuário, com memórias, guildas fundadas, vitórias e recorde.
- **Pausa no ESC**, com o estado da partida escrito. `Time.timeScale` não é
  tocado: o jogo é de turnos por clique e nada avança sozinho.
- **Opções** de áudio e vídeo, guardadas no perfil.
- **Save em JSON**, um arquivo por slot, com escrita atômica e detecção de arquivo
  corrompido. **Autosave + 3 slots manuais** — o autosave é do jogo, e o botão
  "Salvar" da pausa não escreve nele; é o que impede desfazer uma morte.
- **Perfil separado** do save: memórias, destraves, recordes e opções.
- **Não se salva no meio da estrada.** O botão aparece desligado com o motivo
  escrito. O ponto seguro é a guilda entre jornadas, onde o autosave já cai.

## 5. O mundo: sete regiões **[IMPLEMENTADO]** (`RegionMap`)

Floresta, Montanha, Pântano, Deserto, Tundra, Vulcão e Ruínas — cada uma com
**corrupção própria**, posição fixa no mapa e ritmo próprio de apodrecimento. A
guilda fica no centro.

- A corrupção de uma missão é a da região (±5), não um sorteio: "esta região está
  pior que aquela" vira informação estável, e é isso que o mapa serve para mostrar.
- Corrupção alta significa requisitos de classe mais duros, eventos piores
  liberados, e mais XP e ouro.
- **[PARCIAL]** Cada região tem seu chefe, mas Deserto, Tundra e Vulcão ainda caem
  no chefe curinga e têm um evento próprio cada — uma jornada nelas repete os
  genéricos.
- **[PARCIAL]** O mapa é feito de círculos e linhas montados por código, pintados
  pela corrupção. A geometria sai de um dicionário de coordenadas de 0 a 1: trocar
  por um mapa ilustrado é trocar o fundo e aqueles números.

## 6. A guilda **[IMPLEMENTADO]**

| Recurso | Início | Papel |
|---|---|---|
| Ouro | 500 (+ destrave) | Recrutar, comprar carta, item e melhoria |
| Reputação | 100 (+ destrave) | Zerou, a partida acaba |
| Roster | 4 heróis, teto 8 (+ destrave) | Quem existe para mandar |
| Quadro de missões | 3 ofertas (+ destrave) | O que há para fazer |

| Sala | O que faz |
|---|---|
| Taverna | 3 recrutas por vez; contratar custa o salário; renovar a lista custa 50 |
| Biblioteca | Vende cartas por raridade e sobe de nível, liberando raridades melhores |
| Mercado | Rações e tochas; tratamento, bandagem e vinho de efeito imediato; frascos e a relíquia do ciclo |
| Forja | Um herói na bigorna por vez: arma (+1 de dano nas cartas dele) e armadura (+4 de HP), até nível 3 |
| Cemitério | Lista os caídos; monumento devolve reputação; vigília alivia estresse |
| Sala de Mapas | Batedores revelam trechos da rota; desvios trocam um evento adiante |
| Gerenciador de Deck | Monta o baralho de cada herói dentro do limite do nível dele |

**Guia da guilda [IMPLEMENTADO]:** uma linha diz o que fazer agora e acende a
porta que resolve, sempre pela primeira condição que casa — da mais bloqueante à
mais rotineira. O texto cita nomes e números reais, não instrução genérica.

**Salas como lugar [IMPLEMENTADO]:** a **Forja** estabeleceu o molde — fila à
esquerda, um em foco no meio, e à direita o efeito da compra acontecendo —, e as
outras cinco seguiram. A taverna mostra o que o candidato traz para o baralho; a
biblioteca julga cada carta à venda contra o baralho de quem está na mesa; o
cemitério mostra o que o morto levava e move a barra de estresse dos vivos no
mesmo clique da vigília; a sala de mapas desenha a estrada até o destino e abre o
marco quando o batedor é contratado; o mercado mostra em quem a compra pega,
com a barra do antes e do depois, antes de o ouro sair. **[PLANEJADO]** o
**Gerenciador de Deck**, a única tela da guilda que nunca foi refeita.

**Motivo para voltar [IMPLEMENTADO]** (`CycleStock`): o estoque de cada sala é
uma **função do número do ciclo**, não um estado guardado — a mesma volta mostra
sempre a mesma carroça, em qualquer sessão, e a seguinte mostra outra. Não entra
no save, e o jogador não pode recarregar até sair o que quer. Hoje vale para os
frascos e a relíquia do Mercado e para a oferta da Forja.

## 7. Relíquias e poções **[IMPLEMENTADO]** (`ItemData`, `HeroGearUI`, `PotionBeltUI`)

Catálogo em código: **6 relíquias e 4 poções**. São poucos itens e o efeito de
cada um é um `case` no combate de todo jeito — um asset por item seria só mais um
lugar para sair de sincronia.

- **O item é do herói, não da guilda.** Cada um equipa até **duas relíquias**,
  escolhidas no rodapé da ficha dele: é a única tela que mostra nível, estresse,
  ferimento e traço ao mesmo tempo, que é o que a decisão exige.
- **Morrem com o dono.** Não voltam para a prateleira.
- A **prateleira da guilda** guarda o que está livre. Chega do Mercado, do espólio
  do chefe (relíquia garantida) ou de uma luta comum (poção em uma a cada três).
- **Beber não custa energia nem gasta o turno** — é jogada a mais, não fim de uma.
  Os frascos ficam numa faixa perto da mão, com o nome do dono em cada um.

| Relíquias | Poções |
|---|---|
| +2 de dano nas cartas do dono · 5 de bloqueio no início | +12 de vida · +10 de bloqueio |
| −40% de estresse · +4 de vida por combate vencido | +50% de dano na próxima carta |
| 3 de dano de volta · primeira carta custa 1 a menos | −25 de estresse |

Dois efeitos iguais empilham: abrir mão de variedade por concentração é escolha
legítima. Os nomes são provisórios e descrevem a função.

## 8. Heróis

| Campo | Regra |
|---|---|
| Classe, nível | Nível define HP, salário, tamanho e raridade do deck |
| HP · salário | `20 + nível×4` (+10 Guerreiro, −5 Mago) · `20 + nível×10` |
| XP | Curva `100 + (nível−1)×75` |
| Estresse 0–100 | Ao estourar, vira Aflição (78%) ou Virtude (22%) |
| Ferido · Beira da Morte | +25% de dano recebido · o HP para em 0 na primeira vez |
| Relíquias, poções, arma, armadura | Até 2 relíquias; frascos sem limite; equipamento da Forja |
| Exposição à corrupção | **[PARCIAL]** acumula de 5 a 15 por evento e ninguém lê |

**Progressão [IMPLEMENTADO]:** sobreviventes ganham `60 + 8×trechos` de XP,
multiplicado pela corrupção da região (até +50%), 40% se a missão fracassa. Do
**5º herói da formação em diante o XP rende metade** — grupo grande ajuda na
estrada, dilui a experiência e come uma ração a mais por dia.

**Beira da Morte [IMPLEMENTADO]:** o primeiro golpe letal só zera o HP; o seguinte
mata por rolagem, 45% de base e pior com estresse alto.

**Estresse [IMPLEMENTADO]:** sobe com dano recebido (0,8 por ponto de HP), ao ver
um companheiro cair (+12) ou morrer (+25), com escuridão e com corrupção. Só
alivia fora da estrada — vinho, vigília, −15 ao voltar. Acima de **85 o herói
recusa partir**, e aparece bloqueado com o motivo na preparação.

**Morte permanente [IMPLEMENTADO]:** quem morre sai do roster no fim da jornada e
é registrado no Cemitério.

**Classes, traços e personalidades:** Guerreiro, Mago, Curandeiro e Caçador têm
cartas; **[PARCIAL]** Ladino e Bardo existem no enum e não têm nenhuma (o Bardo é
usado como curinga no Gerenciador de Deck). **[PARCIAL]** O estresse é a única
coisa que lê traço e personalidade — covarde sofre +35%, valente −25%, amaldiçoado
+25%, sortudo resiste melhor na Beira da Morte. O resto é rótulo na ficha.

**[PLANEJADO]** Dar efeito ao que só acumula: exposição à corrupção (traço
negativo acima de 50, risco de "virar" acima de 80), os traços e personalidades
sem regra, e os 6 estados mentais que hoje só têm nome.

## 9. Cartas e decks **[IMPLEMENTADO]**

**40 cartas** em `Resources/Cards` — 10 por classe jogável (4 comuns, 3 raras, 2
épicas e 1 lendária). Cada uma traz **dois efeitos**, um de jornada e um de
combate; o contexto escolhe qual vale. **Os nomes das 23 acrescentadas em 26/08
são provisórios**: descritivos, para dizer o que a carta faz.

- **Deck por herói:** `clamp(8 + nível, 8, 12)`. Raras a partir do nível 3,
  lendária no 5. Preços: Comum 100 · Rara 250 · Épica 500 · Lendária 1000.
- **Montado por função, e só então por raridade** (`CardRole`: ataque, defesa,
  suporte, utilidade). Antes o gerador sorteava entre as comuns da classe, e o
  baralho do Curandeiro saía sem uma única carta que ferisse alguém.
- **Toda carta faz alguma coisa nos dois lados** — o smoke test tranca isso desde
  que quatro delas cobravam energia e não faziam nada.
- **Nenhum efeito sobra sem carta**: `GainGold`, `ExtraRations`, `Debuff`,
  `DrawCards`, `GainEnergy` e `Poison` ganharam dono na segunda leva.
- **[PLANEJADO]** Cartas de Ladino e Bardo. Enquanto não existirem, a **taverna
  não oferece as duas classes** — o recruta entrava com um baralho de emergência
  de oito cópias de um "ataque básico" criado em memória, pelo salário cheio.

> **Regra de manutenção:** valor novo de enum entra **só no fim**. Os assets
> guardam o número, e inserir no meio troca o efeito de toda carta configurada.

## 10. Preparação da expedição **[IMPLEMENTADO]** (`QuestSelectionUI`)

1. **Destino — o mapa de regiões.** Substituiu a lista de contratos. As sete
   regiões aparecem no lugar delas, pintadas pela própria corrupção; onde há
   contrato o marcador acende e pode ser clicado, onde não há o lugar continua
   ali, apagado — o jogador precisa ver o mundo inteiro para entender o que está
   piorando fora do alcance dele.
2. **Grupo e formação.** Marcar quem vai e **ordenar**: a ordem é a formação, e os
   dois primeiros são a linha de frente. Sem teto de tamanho, mas acima de 4 cada
   bloco de 4 soma uma ração por dia e o XP dos extras cai pela metade. Os
   requisitos de classe da missão são conferidos de verdade.
3. **Baralho e provisões.** Um herói fornece o deck base e os companheiros
   emprestam cartas; o jogo guarda de quem é cada uma, e é isso que faz a arma da
   Forja fortalecer só as cartas daquele herói. Base de 10 rações e 8 tochas, mais
   o que veio do Mercado.

## 11. A jornada **[IMPLEMENTADO]** (`JourneyManager`)

**A rota** é um mapa ramificado no formato do *Slay the Spire*: cada camada
oferece dois ou três caminhos e todos desembocam no nó do chefe. O gerador garante
que todo nó tenha saída e todo nó tenha pai — nenhum caminho morre, nenhum nó fica
inalcançável. A escolha é do jogador desde a entrada.

**A estrada [IMPLEMENTADO]** (`TrailStage`, `TrailRoadUI`): o grupo aparece **de
corpo inteiro, em fila, na ordem da formação**, andando entre um nó e outro, com
vida e estresse em barras sobre a cabeça. Os bonecos ficam parados em quadro e o
cenário é que se move. Tecnicamente é um palco filmado: os corpos são objetos de
mundo, e uma `RenderTexture` os põe entre duas camadas da interface — acima da
arte do bioma, abaixo do texto e da mão. Enquanto o grupo anda o mapa fica limpo;
a caixa do evento só aparece quando ele pára.

| Recurso | Início | Regra |
|---|---|---|
| Rações | 10 + compras | −1 por trecho (mais 1 por bloco de 4 heróis extras); ao zerar, 5 de dano por herói |
| Tochas | 8 + compras | −1 por trecho; sem tocha, estresse |
| Energia · mão | 5 · 5 cartas | Custo das cartas jogadas na estrada; compra ao encerrar o turno |

### 11.1 Eventos e a carta da estrada **[IMPLEMENTADO]**

**25 eventos** (20 comuns e 5 de chefe), filtrados por região, corrupção mínima e
dia mínimo, com memória dos 3 últimos para não repetir. Cada um tem três opções, e
o desfecho mexe em ouro, reputação, vida, ferimento, moral, dias e corrupção do
mundo — passando pelas mesmas regras de Beira da Morte e estresse do combate.

As duas regras que fazem a carta valer o que custa, decididas em 21/08:

1. **A carta dá o melhor desfecho.** Os 25 eventos têm opção que exige um efeito
   de carta, e todos têm o **desfecho reforçado escrito**: é a mesma escolha dando
   certo — o dano não acontece, a cura rende mais, o ouro vem maior. Antes os 25
   reforços estavam vazios e o código trocava o desfecho bom por eles.
2. **A opção com carta é a única saída sem custo.** Toda opção livre cobra alguma
   coisa; as 8 que não cobravam nada passaram a cobrar moral.

A opção travada **aparece e não some** — "precisa de uma carta capaz de purificar"
—, porque o jogador precisa ver o que perdeu por não ter trazido a carta. Todo
evento mantém ao menos uma saída sem exigir carta, e todo requisito é satisfazível
por alguma carta existente: as duas coisas são verificadas pelo smoke test.

### 11.2 A volta **[IMPLEMENTADO]** (`JourneyResultUI`)

Uma tela de balanço: uma linha por herói com vida, estado, XP com barra e
promoções; o ouro discriminado; e uma **escolha de despojo** ao vencer (ouro
extra, noite na taverna, cuidados do curandeiro ou contar a história), com a saída
travada até escolher.

| Consequência | Regra |
|---|---|
| Vida | O descanso é piso, não teto: quem voltou acima de 60% não é rebaixado |
| Ferimento | Não sara por sorteio — precisa de bandagem, curandeiro ou uma jornada em casa |
| Luto | Quem viu companheiro cair perde moral e ganha estresse |
| Quem fica | Descansa a cada jornada dos outros; é a válvula que impede a guilda de travar |

## 12. Combate **[IMPLEMENTADO]** (`CombatManager`)

**O campo de batalha** (`BattleStage`, `BattleFieldUI`) é frente a frente, como o
do *Darkest Dungeon*: **party à esquerda, com a posição 1 encostada nos
inimigos**; **criaturas à direita, com a intenção acima da cabeça**; **barra de
ordem do round no alto**. Numa fila normal o herói mais exposto ficaria no canto
mais distante do perigo — daí a fila do grupo ser desenhada invertida.

Os corpos são animados: as criaturas trocam de quadro para Idle, Ataque, Apanhou e
Morte, e os **11 inimigos** têm os quatro estados preenchidos. Quem não tiver
quadros volta ao retrato parado — arte que falta não pode impedir a luta. Quem
manda no lugar de cada figura é a interface: o palco recebe, por corpo, o
retângulo em que ela deve caber, o mesmo que recebe o arrasto da carta e mostra a
barra de vida. Assim não há duas verdades sobre onde o inimigo está.

- **Energia 5, mão de 6 cartas.** O grupo age junto e depois os inimigos
  respondem; a barra de ordem do round é informativa, o turno não é por
  personagem.
- **Bloqueio dos dois lados**, e intenções telegrafadas: Atacar, Atacar todos,
  Defender ou Estressar, por pesos próprios de cada inimigo.
- **A formação importa:** o ataque cai na linha de frente em ~74% das vezes, e a
  carta rende menos se o dono estiver fora da posição dela.
- **O dano nos heróis passa pelas regras da jornada**, então Beira da Morte,
  estresse e morte permanente valem igual dentro e fora do combate.
- **Recuar** custa moral e estresse e não rende recompensa. Perder para o chefe
  encerra a jornada; perder um encontro comum só cobra caro.
- **Espólio:** chefe larga relíquia; luta comum larga poção em uma a cada três.

## 13. Números de referência

| Parâmetro | Valor |
|---|---|
| Ouro / reputação iniciais | 500 / 100 (+ destraves) |
| HP · salário do herói | `20 + nível×4` (+10 Guerreiro, −5 Mago) · `20 + nível×10` |
| Renovar recrutas · melhoria da Biblioteca | 50 · `500 × nível` |
| Tamanho do deck | 8 a 12 |
| XP da jornada | `60 + 8×trechos`, ×(1 + corrupção/100 × 0,5); 40% se fracassa |
| Recompensa da missão | `base + duração×20 + sobreviventes×25` |
| Dano de fome | 5 por herói a cada trecho sem ração |
| Energia / mão de combate | 5 / 6 |

**Letalidade alvo** (decisão do autor): 0,33 a 0,67 mortes por jornada com grupo
de 4. Medido em 21/08, com o simulador rodando o código real:

| Medida | Valor |
|---|---|
| Mortes por jornada | **0,45** — chefe 0,15 · encontro do caminho 0,02 · estrada 0,28 |
| Sobrevivência em 200 jornadas | 88,9% |
| Duração média · combates por jornada | 7,1 dias · 2,65 |
| Cartas jogadas na estrada | 2,51 por jornada |
| Mortes por combate contra chefe (grupo desgastado) | 0,03 (teto 0,40) |

O KPI do combate é **mortes por combate**, não taxa de vitória: com o grupo
descansado a party vence quase sempre, e o que se mede é o que a luta cobra em
gente. Os números do desgaste da estrada são campos serializados — mudar o valor
no script não altera a instância salva na cena.

## 14. Apresentação

- **Interface [IMPLEMENTADO]:** painéis com fade e escala, popups de mensagem,
  confirmação e resultado. A cena é montada por código (`Tools ▸ Guild of Legends
  ▸ Montar Cena`), e é ali que se muda layout — não à mão no Inspector. Kit visual
  com molduras 9-slice e fonte medieval.
- **Arte [PARCIAL]:** as 40 cartas, os 11 inimigos e os 25 eventos têm arte, os
  retratos de herói vêm de um catálogo, e os corpos da estrada e do combate são
  bonecos com animação pronta. A cena do evento é desenhada atrás do texto da
  caixa, em opacidade baixa — a tela é de leitura. Falta a arte de bioma de
  Deserto e Vulcão; 3 dos 11 inimigos usam desenho que não os representa; boa
  parte da UI ainda usa emoji como ícone, e o projeto mistura pixel art com arte
  pintada.
- **Áudio [IMPLEMENTADO]:** música por contexto com fade e 8 efeitos, num catálogo
  em `Resources`. Nasce sozinho, sem depender de objeto na cena.

## 15. Como se verifica

| Ferramenta | O que prova | Custo |
|---|---|---|
| Compilar por fora (Roslyn do Unity) | erro de sintaxe sem abrir o Editor | segundos |
| `RunSmokeTest.trigger` → `SmokeTestReport.txt` | 41 verificações, 200 jornadas e 800 combates; letalidade | ~1 min |
| `RunPlayModeTest.trigger` → `PlayModeReport.txt` | telas alcançáveis por clique real, jornada inteira, console limpo | ~2 min |

Uma run de Play Mode é n=1 e não serve para balancear. Balanceamento se mede no
simulador, que segue as regras reais do combate.

## 16. Apêndice — enums e mapa do código

**HeroClass:** `Warrior, Mage, Healer, Rogue, Bard, Hunter`
**Personality:** `Brave, Coward, Ambitious, Loyal, Stubborn, Selfish`
**Trait:** `None, Drunkard, Lucky, Scarred, FastHealer, Cursed`
**MentalState:** `Normal` · aflições `Paranoid, Fearful, Hopeless, Irrational, Abusive` · virtudes `Courageous, Focused, Vigorous, Stalwart`
**CardRarity:** `Common, Rare, Epic, Legendary`
**BiomeType:** `Any, Forest, Mountain, Swamp, Desert, Tundra, Volcano, Ruins`
**QuestRisk:** `Low, Medium, High`
**JourneyEventType:** `Normal, Combat, Treasure, Trap, Rest, Shop, Story`
**JourneyEffectType:** `None, RemoveObstacle, HealInjury, GainFood, GainGold, RevealNextEvent, SkipDay, Intimidate, Purify, Teleport, ProtectFromWeather, RestoreMorale, ExtraRations`
**CombatEffectType:** `None, Damage, DamageAll, Block, BlockAll, Heal, HealAll, Debuff, Buff, DrawCards, GainEnergy, Poison, ShieldBreak, BuffNextCard, Evade, Cleanse`
**EnemyIntent:** `Attack, AttackAll, Defend, Stress`
**RelicEffect:** `DanoDeCarta, BloqueioInicial, ResistenciaAEstresse, CuraPosCombate, Retaliacao, PrimeiraCartaBarata`
**PotionEffect:** `Cura, Bloqueio, ForcaNaProximaCarta, Calma`
**RunState / RunEndReason:** `Running, Won, Lost` / `GuildWiped, NoReputation, WorldConsumed, BossDefeated`

**Onde mora cada coisa:**

- *Partida e save:* `RunManager`, `RunFlow`, `MetaProgression`, `RegionMap`, `SaveSystem`, `GameStateIO`, `PlayerProfile`, `SceneFlow`
- *Guilda:* `GuildManager`, `TavernManager`, `LibraryManager`, `MarketManager`, `ForgeManager`, `CemeteryManager`, `MapRoomManager`, `DeckManager`, `HeroFactory`, `DeckGenerator`
- *Missão e jornada:* `QuestManager`, `QuestGenerator`, `JourneyManager`, `JourneyMap`, `EventPool`, `EventResolver`, `PartyFormation`, `CardOwnership`, `TrailStage`, `TrailCast`
- *Combate:* `CombatManager`, `EnemyPool`, `BattleStage`, `EnemyBody`, `TurnOrderBar`
- *Dados:* `CardData`, `HeroData`, `QuestData`, `EventData`, `EnemyData`, `ItemData`
- *UI:* `UIManager`, `QuestSelectionUI`, `RegionMapUI`, `JourneyMapUI`, `TrailRoadUI`, `BattleFieldUI`, `HeroGearUI`, `PotionBeltUI`, `GuildGuide`, `JourneyResultUI`, `RunEndUI`, `MainMenuUI`, `PauseMenuUI`, `OptionsUI`, `SaveSlotsUI`, `RelicShrineUI`
- *Editor, fora do build:* `GuildSceneSetup`, `MenuSceneSetup`, `GuildSmokeTest`, `PlayModeProbe`, `EventBalance`, `EnemyArt`, `CardArt`
