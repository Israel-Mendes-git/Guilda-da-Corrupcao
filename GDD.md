# Game Design Document — Guilda da Corrupção

Unity 2022.3.62f3 · URP · pt-BR · PC · Versão 2.1 — 08/09/2026 (substitui a 2.0, de 26/08)

Descreve o jogo como ele está em 29/08/2026, conferido contra o `SmokeTestReport.txt`
(45 verificações, 0 falhas), o `PlayModeReport.txt` (nenhum erro) e o
`GameplayReport.txt` (auditoria de jogabilidade). Plano de trabalho em
[`ROADMAP.md`](ROADMAP.md), inventário de arte em [`ASSETS.md`](ASSETS.md).

**[IMPLEMENTADO]** existe e funciona · **[PARCIAL]** existe pela metade, ou
existe e ninguém usa · **[PLANEJADO]** decidido, não construído.

## 1. O que é

Roguelike de gerência de guilda com combate por cartas — *Darkest Dungeon* na
guilda, *Slay the Spire* na estrada.

O jogador é o mestre da guilda. Ele recruta heróis, monta os baralhos deles,
escolhe quem parte, para onde e com quantas provisões — e então a expedição sai
pela estrada e ele a acompanha. Não se controla o herói em campo: controla-se a
preparação e as decisões do caminho.

O que dá urgência a isso é a Corrupção, um medidor que sobe a cada expedição e
encerra o mundo ao encher. Uma partida é a história de uma guilda tentando
alcançar o Chefe Supremo antes disso, gastando gente no caminho. Quem morre não
volta e leva o que carregava; o que atravessa o fim da guilda é a **memória**,
moeda que funda a próxima.

| Pilar | O que significa em jogo |
|---|---|
| Decisões com peso permanente | Herói que morre não volta, e leva as relíquias dele |
| Preparação acima da execução | Quem vai, em que ordem e com qual baralho decide a jornada |
| A Corrupção como relógio | Um medidor que sobe a cada ciclo e encerra a partida no máximo |

**Sobre a ficção:** o tema é dark fantasy com uma Corrupção que se alastra. Os
nomes de regiões, chefes, eventos e heróis que estão nos assets são **de
trabalho**: descrevem a função. O world building **[PLANEJADO]** é decisão em
aberto do autor; até lá, este documento apresenta o mundo pelo que já existe
dentro do jogo.

## 2. Os três loops **[IMPLEMENTADO]**

```
PARTIDA      nova guilda → ciclos → a Corrupção enche ou a guilda cai → memórias → nova guilda
 └ CICLO     guilda (salas) → preparar expedição → jornada → volta e balanço → +1 ciclo
    └ JORNADA rota ramificada → evento ou combate a cada nó → chefe no fim
```

Um ciclo é uma jornada concluída. É o pulso da partida: é ele que faz a Corrupção
avançar.

## 3. A partida **[IMPLEMENTADO]**

| Régua | Valor |
|---|---|
| Corrupção inicial · por ciclo · por evento marcado | 10 · +6 · +3 |
| Máximo | 100 |
| Chefe Supremo entra no quadro | Corrupção ≥ 60 (por volta do 9º ciclo) |
| Partida inteira | ~15 jornadas até o mundo saturar |

Fim da partida: **derrota** quando a Corrupção chega a 100, quando a reputação
zera, ou quando não há herói vivo **e** falta ouro para recrutar (menos de 30);
**vitória** ao derrotar o Chefe Supremo. Vem então uma tela de balanço com ciclos,
mortos, corrupção final e o botão de nova guilda.

### 3.1 Meta-progressão **[IMPLEMENTADO]**

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
novo seria design a fazer, não meta-progressão a ligar. **[PLANEJADO]** Destravar
cartas do acervo da Biblioteca e as classes Ladino e Bardo — as duas dependem de
conteúdo que ainda não existe (§9).

### 3.2 O fim, decidido em 09/09 **[PLANEJADO]**

Hoje a vitória é derrotar o Chefe Supremo, que entra no quadro por corrupção. O
desenho fechado pelo autor é este:

- **Mapear destrava.** Cada expedição traz pedaços do mapa da região percorrida;
  região mapeada mostra o seu chefe, e derrubá-lo **sela** a região — a corrupção
  de lá para de subir. Com três selos, a Sala de Mapas desenha a jornada final.
- **Cada selo é específico.** Não é um contador: é *quais* três. Cada região
  entrega uma verdade sobre a causa, uma regra que entra na luta final e um
  destino no epílogo — 35 combinações, montadas de peças e não escritas uma a uma.
- **Voltar cobra.** A corrupção que a travessia soma à região (+4, §5) ganha
  consequência declarada: a visita seguinte traz inimigos mais fortes e os eventos
  do fundo da escala. Mapear obriga a voltar, e voltar piora o lugar.
- **Escolher fecha portas, e o preço é anunciado.** Ofertas do quadro podem se
  excluir: socorrer um lugar deixa o outro sem socorro, e o que fica sem socorro
  colapsa — some do quadro, e com ele o mapa, o escrito e o chefe que estavam lá.
  A oferta diz o que cai antes de o jogador escolher. Vale também na rota (entrar
  num ramo apaga o outro) e no fim (os selos que você tem não se trocam).
- **Os escritos contêm.** Páginas de civilizações consumidas caem em ruínas,
  espólios e eventos; os batedores as trazem junto com o mapa, e a **Biblioteca**
  traduz uma por ciclo. Cada uma atrasa a corrupção (a do herói, a de uma região,
  ou o que os eventos somam) e nenhuma reverte. Lidas em ordem, apontam a causa —
  o mapa diz onde, a página diz o quê.
- **O baralho vale fora da estrada**, de três maneiras: as salas aceitam carta no
  lugar de ouro; o escrito traduzido **entra no baralho** como carta de contenção,
  a única que age sobre o mundo; e **selar cobra cartas**, queimadas sem volta, do
  tipo que aquela região sempre pede — fixo e sabido desde o começo, para que dê
  para preparar o baralho para o selo.
- **A jornada final não reabastece.** O que saiu da guilda é o que se tem; o resto
  é o que o caminho largar.
- **Alguém fica.** A passagem se fecha por dentro. O herói escolhido define as
  duas metades da luta final: **quanto ela dura** (a contenção segura o que ele
  aguenta) e **contra o que se luta**, porque ele volta como **o Campeão** — o
  chefe final da maioria das partidas, já que algumas combinações de selos levam a
  outro desfecho.
- **O Campeão é montado com o que o herói era.** A classe dá o repertório
  (Guerreiro quebra a formação, Mago drena a energia da mão, Curandeiro devolve
  cura como dano, Caçador ignora a linha de frente, Ladino leva cartas do baralho,
  Bardo vira o estresse do grupo); o nível dá vida e dano; traço e personalidade
  viram comportamento (o covarde vai no mais fraco, o teimoso persegue um alvo, o
  sortudo escapa do que devia acertar); arma, armadura, relíquias e o baralho
  continuam com ele, e as cartas que você escolheu para aquele herói são os golpes
  do chefe. Por cima entram poderes que nenhum herói tem: contaminar as cartas da
  mão, apagar a luz dentro do combate, chamar os mortos do Cemitério pelo nome, e
  recuperar a cada turno o que a contenção segurava.
- **Vencer destrava o Campeão** como herói jogável na guilda seguinte, com
  corrupção que sobe sozinha enquanto ele está em campo.
- **Perder deixa ruína.** A guilda caída vira lugar no mapa da partida seguinte;
  uma expedição recupera parte — relíquias, páginas traduzidas, os nomes dos
  mortos —, não tudo, e o que volta pode voltar com preço.

## 4. Sessão: menus, pausa e save **[IMPLEMENTADO]**

- **Tela de título** em cena própria: continuar, nova guilda, slots, opções e
  Santuário, com memórias, guildas fundadas, vitórias e recorde.
- **Pausa no ESC**, com o estado da partida escrito. O tempo não é congelado: o
  jogo é de turnos por clique e nada avança sozinho.
- **Opções** de áudio e vídeo, guardadas no perfil.
- **Save em JSON**, um arquivo por slot, com escrita atômica e detecção de arquivo
  corrompido. **Autosave + 3 slots manuais** — o autosave é do jogo, e o botão
  "Salvar" da pausa não escreve nele; é o que impede desfazer uma morte.
- **Não se salva no meio da estrada.** O botão aparece desligado com o motivo
  escrito. O ponto seguro é a guilda entre jornadas, onde o autosave já cai.

## 5. O mundo: sete regiões **[IMPLEMENTADO]**

A guilda fica no centro do mapa e sete regiões a cercam. Cada uma tem **corrupção
própria**, ritmo próprio de apodrecimento, eventos que só acontecem nela e um
chefe que a fecha. É o que o jogador aprende a ler: "o Pântano está pior que a
Floresta" é informação estável, não sorteio por missão.

| Região | Como começa | Quem a fecha |
|---|---|---|
| 🌲 Floresta | a mais limpa, e a que apodrece mais devagar | A Coisa da Mata |
| ⛰️ Montanha | pouco abaixo do relógio do mundo | O Gigante de Pedra |
| 🏚️ Pântano | nasce pior que o mundo, e piora rápido | O Afogado |
| 🏯 Ruínas | acima do relógio | O Bibliotecário Cego |
| 🌋 Vulcão | a pior de saída, e a mais rápida a virar | **[PARCIAL]** O Guardião Sem Nome |
| ❄️ Tundra | acompanha o relógio | **[PARCIAL]** idem |
| 🏜️ Deserto | limpo, e o mais lento a apodrecer | **[PARCIAL]** idem |

- A corrupção de uma missão é a da região (±5). Corrupção alta significa
  requisitos de classe mais duros, eventos piores liberados, e mais XP e ouro.
- **Atravessar suja o lugar:** +4 de corrupção na região que a expedição percorreu.
- **[PARCIAL]** Deserto, Tundra e Vulcão caem no chefe curinga e têm um evento
  próprio cada — uma jornada nelas repete os genéricos.
- **[PARCIAL]** O mapa é feito de círculos e linhas montados por código, pintados
  pela corrupção; trocar por um mapa ilustrado é trocar o fundo e sete pares de
  coordenadas.

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
| Baralhos | Monta o baralho de cada herói dentro do limite do nível dele |

**As salas são lugares [IMPLEMENTADO].** A **Forja** estabeleceu o molde — fila à
esquerda, um em foco no meio, e à direita o efeito da compra acontecendo — e as
outras seis seguiram. A taverna mostra o que o candidato traz para o baralho; o
cemitério mostra o que o morto levava e move a barra de estresse dos vivos no
mesmo clique da vigília; o mercado mostra em quem a compra pega, com a barra do
antes e do depois, antes de o ouro sair. A tela de **Baralhos** foi a última a
sair da lista, e mostra o acervo em grade — carta se lê pelo desenho, e numa
coluna comparar era impossível.

**A Biblioteca vende para quem está em casa [IMPLEMENTADO]:** a estante só traz
cartas que servem a alguém do roster, com um nicho por classe presente. Antes ela
saía do acervo inteiro, e quase todo nicho era carta de classe que a guilda não
tinha. O veredito de cada carta é dado contra o baralho de quem está na mesa.

**O guia da guilda [IMPLEMENTADO]:** uma linha diz o que fazer agora e acende a
porta que resolve, pela primeira condição que casa — da mais bloqueante à mais
rotineira. Antes de cair na rotina ele lê três coisas: herói viajando perto do
limite de estresse, arma nunca forjada e ouro parado acima de três jornadas de
renda. E cita nomes e números reais — *"A arma de Gromm nunca foi forjada. Há
3575 de ouro parado, e a Forja é o que se sente no primeiro combate."*

**Motivo para voltar [IMPLEMENTADO]:** o estoque de cada sala é uma **função do
número do ciclo** — a mesma volta mostra sempre a mesma carroça, e a seguinte
mostra outra. Não entra no save, então não há como recarregar até sair o que se
quer. Hoje vale para o Mercado e para a oferta da Forja.

## 7. Relíquias e poções **[IMPLEMENTADO]**

Catálogo em código: **6 relíquias e 4 poções**. São poucos itens e o efeito de
cada um é um caso tratado dentro do combate de todo jeito — um asset por item
seria só mais um lugar para sair de sincronia.

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

## 8. Os heróis

O elenco é gerado, não escrito: cada herói nasce com nome, retrato, classe,
nível, traço e personalidade, e o que o torna *seu* é a folha de serviço que ele
acumula — quanto subiu, o que carrega, quantas vezes voltou da Beira da Morte.
Ele acaba de duas maneiras: envelhecendo no roster ou virando uma linha no
Cemitério, com o nome, a jornada e o que levava no bolso.

| Campo | Regra |
|---|---|
| Classe, nível | Nível define HP, salário, tamanho e raridade do deck |
| HP · salário | `20 + nível×4` (+10 Guerreiro, −5 Mago) · `20 + nível×10` |
| XP | Curva `100 + (nível−1)×75` |
| Estresse 0–100 | Ao estourar, vira Aflição (78%) ou Virtude (22%) |
| Ferido · Beira da Morte | +25% de dano recebido · o HP para em 0 na primeira vez; o golpe seguinte mata em 45%, e pior com estresse alto |
| Relíquias, poções, arma, armadura | Até 2 relíquias; frascos sem limite; equipamento da Forja |
| Exposição à corrupção | **[PARCIAL]** acumula de 5 a 15 por evento e ninguém lê |

**Progressão [IMPLEMENTADO]:** sobreviventes ganham `60 + 8×trechos` de XP,
multiplicado pela corrupção da região (até +50%), 40% se a missão fracassa. Do
**5º herói da formação em diante o XP rende metade** — grupo grande ajuda na
estrada, dilui a experiência e come uma ração a mais por dia.

**Estresse [IMPLEMENTADO]:** sobe com dano recebido (0,8 por ponto de HP), ao ver
um companheiro cair (+12) ou morrer (+25), com escuridão e com corrupção. Só
alivia fora da estrada — vinho, vigília, −15 ao voltar. Acima de **85 o herói
recusa partir**, e aparece bloqueado com o motivo na preparação.

**A quebra é um momento [IMPLEMENTADO].** Quando o estresse estoura, a estrada
para: a tela escurece, o rosto de quem quebrou ocupa o meio dela e a aflição é
dita pelo nome, por menos de dois segundos. Antes, a coisa mais dramática de uma
jornada era uma troca de cor num rótulo de lista. A quebra também é resolvida na
manutenção diária — quem enchia a barra andando no escuro nunca quebrava.

**Morte permanente [IMPLEMENTADO]:** quem morre sai do roster no fim da jornada e
é registrado no Cemitério.

**Classes, traços e personalidades:** Guerreiro, Mago, Curandeiro e Caçador têm
cartas; **[PARCIAL]** Ladino e Bardo existem no enum e não têm nenhuma. **[PARCIAL]**
O estresse é a única coisa que lê traço e personalidade — covarde sofre +35%,
valente −25%, amaldiçoado +25%, sortudo resiste melhor na Beira da Morte. O resto
é rótulo na ficha.

**[PLANEJADO]** Dar efeito ao que só acumula: exposição à corrupção (traço
negativo acima de 50, risco de "virar" acima de 80), os traços e personalidades
sem regra, e os 6 estados mentais que hoje só têm nome.

## 9. Cartas e decks **[IMPLEMENTADO]**

**40 cartas** — 10 por classe jogável (4 comuns, 3 raras, 2 épicas e 1 lendária).
Cada uma traz **dois efeitos**, um de jornada e um de combate; o contexto escolhe
qual vale. **Os nomes das 23 acrescentadas em 26/08 são provisórios**: descritivos,
para dizer o que a carta faz.

- **Deck por herói:** `clamp(8 + nível, 8, 12)`. Raras a partir do nível 3,
  lendária no 5. Preços: Comum 100 · Rara 250 · Épica 500 · Lendária 1000.
- **Montado por função, e só então por raridade** (ataque, defesa, suporte,
  utilidade). Antes o gerador sorteava entre as comuns da classe, e o baralho do
  Curandeiro saía sem uma única carta que ferisse alguém.
- **Toda carta faz alguma coisa nos dois lados, e nenhum efeito sobra sem carta**
  — o smoke test tranca as duas coisas desde que quatro cartas cobravam energia
  sem fazer nada e seis efeitos não tinham dono.
- **[PLANEJADO]** Cartas de Ladino e Bardo. Enquanto não existirem, a **taverna
  não oferece as duas classes** — o recruta entrava com um baralho de emergência
  de oito cópias de um "ataque básico" criado em memória, pelo salário cheio.

> **Regra de manutenção:** valor novo de enum entra **só no fim**. Os assets
> guardam o número, e inserir no meio troca o efeito de toda carta configurada.

## 10. Preparação da expedição **[IMPLEMENTADO]**

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

O nome do contrato diz o lugar e concorda com ele — "Vila Antiga", não "Vila
Antigo" —, e não repete a região, que já aparece ao lado em toda tela que o mostra.

## 11. A jornada **[IMPLEMENTADO]**

**A rota** é um mapa ramificado no formato do *Slay the Spire*: cada camada
oferece dois ou três caminhos e todos desembocam no nó do chefe. O gerador garante
que todo nó tenha saída e todo nó tenha pai — nenhum caminho morre, nenhum nó fica
inalcançável. A escolha é do jogador desde a entrada.

**A estrada [IMPLEMENTADO]:** o grupo aparece **de corpo inteiro, em fila, na
ordem da formação**, andando entre um nó e outro, com vida e estresse em barras
sobre a cabeça. Os bonecos ficam parados em quadro e o cenário é que se move, à
frente da arte do bioma e atrás do texto e da mão. Enquanto o grupo anda o mapa
fica limpo; a caixa do evento só aparece quando ele pára.

| Recurso | Início | Regra |
|---|---|---|
| Rações | 10 + compras | −1 por trecho (mais 1 por bloco de 4 heróis extras); ao zerar, 5 de dano por herói |
| Tochas | 8 + compras | −1 por trecho; sem tocha, estresse |
| Energia · mão | 5 · 5 cartas | Custo das cartas jogadas na estrada; compra ao encerrar o turno |

**A tocha é a luz da tela [IMPLEMENTADO].** Era um contador e uma punição ao
chegar a zero; entre o cinco e o zero nada acontecia. Agora a escuridão avança
sobre a imagem conforme as tochas caem — acima de quatro a tela está limpa, sem
nenhuma a sombra fecha e puxa para o centro. A punição continua onde estava; o
aperto é que se vê antes dela.

### 11.1 Eventos e a carta da estrada **[IMPLEMENTADO]**

**25 eventos** — 20 comuns, dos quais 6 levam a combate, e 5 de chefe —, filtrados
por região, corrupção mínima e dia mínimo, com memória dos 3 últimos para não
repetir. São paradas curtas com nome e ilustração próprios (*Ponte Quebrada*,
*Acampamento Noturno*, *O Que Restou da Expedição*, *Névoa Pútrida*), cada uma com
três opções; o desfecho mexe em ouro, reputação, vida, ferimento, moral, dias e
corrupção do mundo — passando pelas mesmas regras de Beira da Morte e estresse do
combate.

As duas regras que fazem a carta valer o que custa, decididas em 21/08:

1. **A carta dá o melhor desfecho.** Os 25 eventos têm opção que exige um efeito
   de carta, e todos têm o **desfecho reforçado escrito**: é a mesma escolha dando
   certo — o dano não acontece, a cura rende mais, o ouro vem maior. Antes os 25
   reforços estavam vazios e o código trocava o desfecho bom por eles.
2. **A opção com carta é a única saída sem custo.** Toda opção livre cobra alguma
   coisa; as 8 que não cobravam nada passaram a cobrar moral.

A opção travada **aparece e não some** — "precisa de uma carta capaz de purificar"
—, porque o jogador precisa ver o que perdeu por não ter trazido a carta. Todo
evento mantém uma saída sem carta, e todo requisito é satisfazível por alguma
carta existente: as duas coisas são verificadas pelo smoke test.

### 11.2 A volta **[IMPLEMENTADO]**

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

## 12. Combate **[IMPLEMENTADO]**

**O campo de batalha** é frente a frente, como o do *Darkest Dungeon*: **party à
esquerda, com a posição 1 encostada nos inimigos**; **criaturas à direita, com a
intenção acima da cabeça**; **barra de ordem do round no alto**. Numa fila normal
o herói mais exposto ficaria no canto mais distante do perigo — daí a fila do
grupo ser desenhada invertida.

Os **11 inimigos** são bichos e gente do lugar onde aparecem — Lobo Esfomeado e
Aranha da Copa na Floresta, Salteador da Serra na Montanha, Sanguessuga Gigante no
Pântano, Estátua Desperta nas Ruínas —, e os cinco chefes fecham a região que
governam. Todos têm corpo animado, com quadros de Idle, Ataque, Apanhou e Morte;
quem não tiver quadros volta ao retrato parado, porque arte que falta não pode
impedir a luta.

- **Energia 5, mão de 6 cartas.** O grupo age junto e depois os inimigos
  respondem; a barra de ordem do round é informativa, o turno não é por
  personagem.
- **Bloqueio dos dois lados**, e intenções telegrafadas: Atacar, Atacar todos,
  Defender ou Estressar, por pesos próprios de cada inimigo.
- **O alvo sai junto com a intenção**, não no instante do golpe: um 🎯 marca quem
  está na mira. Sem isso não dá para decidir se vale gastar o bloqueio, nem em
  quem — que é a decisão inteira do turno num jogo de formação.
- **A formação importa:** o ataque cai na linha de frente em ~74% das vezes, e a
  carta rende menos se o dono estiver fora da posição dela.
- **O dano nos heróis passa pelas regras da jornada**, então Beira da Morte,
  estresse e morte permanente valem igual dentro e fora do combate.
- **Recuar** custa moral e estresse e não rende nada. Perder para o chefe encerra
  a jornada; perder um encontro comum só cobra caro.
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
de 4. Medido em 29/08, com o simulador rodando o código real em 1000 jornadas:

| Medida | Valor |
|---|---|
| Mortes por jornada | **0,54** — chefe 0,22 · encontro do caminho 0,04 · estrada 0,28 |
| Sobrevivência | 86,4% |
| Duração média · combates por jornada | 7,0 dias · 2,56 |
| Cartas jogadas na estrada | 2,62 por jornada |
| Mortes por combate contra chefe (grupo desgastado) | 0,02 (teto 0,40) |

O KPI do combate é **mortes por combate**, não taxa de vitória: com o grupo
descansado a party vence quase sempre, e o que se mede é o que a luta cobra em
gente. O número oscilava entre execuções por causa do *n* efetivo, não das
jornadas: com 100 grupos sorteados a dispersão fica em ±0,03.

### 13.1 O que a auditoria acusa **[PLANEJADO]**

A auditoria roda a mesma simulação com uma coisa mudada de cada vez, e diz quanto
cada sistema vale em mortes por jornada. Três decisões em aberto:

- **A economia satura.** Entram ~395 de ouro por jornada e tudo o que as salas
  vendem soma ~2.660: por volta do 7º ciclo o jogador tem mais ouro do que o jogo
  tem o que vender, e ainda faltam 8 ciclos. O salário de um recruta nível 3 é 50.
- **A armadura vale quase o dobro da arma** (−0,38 contra −0,22 no nível 3) pelo
  mesmo lugar na Forja, e o primeiro nível já entrega a maior parte do ganho.
- **A poção é a compra mais fraca** (−0,11) e a relíquia, a mais forte (−0,22) —
  os preços não dizem isso.

## 14. Apresentação

- **Interface [IMPLEMENTADO]:** painéis com fade e escala, popups de mensagem,
  confirmação e resultado. A cena é montada por código, e é ali que se muda layout
  — não à mão no Inspector. Kit visual com molduras 9-slice, que chegam a toda
  caixa pelo fundo que ela usa, e não pela cor: as salas refeitas escolhem a cor
  delas, e o critério antigo deixava metade das telas com retângulo chapado.
- **Tipografia [IMPLEMENTADO]:** títulos em MedievalSharp e corpo numa serifada
  gerada a partir da Crimson-Bold com o Latin-1 inteiro — a versão que vinha no
  pacote não tinha um único acento, e teria escrito "miss o" sem erro no console.
- **O véu da tela [IMPLEMENTADO]:** vinheta nas bordas e grão sobre tudo, acima
  até dos popups. É a mesma vinheta que a tocha escurece na estrada (§11).
- **Arte [PARCIAL]:** as 40 cartas, os 11 inimigos e os 25 eventos têm arte, as
  sete portas da guilda mostram interiores de pedra pintados, os retratos vêm de
  um catálogo, e os corpos da estrada e do combate são bonecos animados. A cena do
  evento fica atrás do texto, em opacidade baixa — a tela é de leitura. Falta a
  arte de bioma de Deserto e Vulcão; 3 dos 11 inimigos usam desenho que não os
  representa; boa parte da UI ainda usa emoji como ícone, e o projeto mistura
  pixel art com arte pintada.
- **Áudio [IMPLEMENTADO]:** música por contexto com fade e 8 efeitos, num catálogo
  carregado sozinho, sem depender de objeto na cena.

## 15. Como se verifica

| Ferramenta | O que prova | Custo |
|---|---|---|
| Compilar por fora (Roslyn do Unity) | erro de sintaxe sem abrir o Editor | segundos |
| `RunSmokeTest.trigger` → `SmokeTestReport.txt` | 45 verificações, 1000 jornadas e 800 combates; letalidade | ~1 min |
| `RunPlayModeTest.trigger` → `PlayModeReport.txt` | telas alcançáveis por clique real, jornada inteira, console limpo, custo em cliques | ~2 min |
| `RunGameplayAudit.trigger` → `GameplayReport.txt` | quanto cada sistema vale em mortes por jornada, economia, ritmo da partida | ~1 min |

Uma run de Play Mode é n=1 e não serve para balancear — isso se mede no simulador,
que segue as regras reais do combate. E antes de ler qualquer número, confira se o
simulador executa aquele sistema: o instrumento já errou sobre o jogo seis vezes.

## 16. Apêndice — enums e mapa do código

**O vocabulário que os assets guardam** — 6 classes (4 com cartas), 6
personalidades, 6 traços, 9 estados mentais (5 aflições e 4 virtudes), 4
raridades, 4 papéis de carta, 8 biomas, 13 efeitos de jornada, 16 de combate, 4
intenções de inimigo, 6 relíquias e 4 poções. Todos são enums, e todos seguem a
regra de manutenção do §9: valor novo entra só no fim.

**Onde mora cada coisa:**

- *Partida e save:* `RunManager`, `RunFlow`, `MetaProgression`, `RegionMap`, `SaveSystem`, `GameStateIO`, `PlayerProfile`, `SceneFlow`
- *Guilda:* `GuildManager`, `TavernManager`, `LibraryManager`, `MarketManager`, `ForgeManager`, `CemeteryManager`, `MapRoomManager`, `DeckManager`, `CycleStock`, `HeroFactory`, `DeckGenerator`, e as telas em `Core/Rooms/`
- *Missão e jornada:* `QuestManager`, `QuestGenerator`, `JourneyManager`, `JourneyMap`, `EventPool`, `EventResolver`, `PartyFormation`, `CardOwnership`, `TrailStage`, `TrailCast`
- *Combate:* `CombatManager`, `EnemyPool`, `BattleStage`, `EnemyBody`, `TurnOrderBar`, `CombatFeedback`
- *Dados:* `CardData`, `HeroData`, `QuestData`, `EventData`, `EnemyData`, `ItemData`
- *UI:* `UIManager`, `QuestSelectionUI`, `RegionMapUI`, `JourneyMapUI`, `TrailRoadUI`, `BattleFieldUI`, `HeroGearUI`, `PotionBeltUI`, `GuildGuide`, `AfflictionMoment`, `JourneyLight`, `JourneyResultUI`, `RunEndUI`, e as telas de menu
- *Editor, fora do build:* `GuildSceneSetup`, `MenuSceneSetup`, `ScreenVeil`, `BodyFont`, `GuildSmokeTest`, `PlayModeProbe`, `GameplayAudit`, `EventBalance`, `EnemyArt`, `CardArt`
