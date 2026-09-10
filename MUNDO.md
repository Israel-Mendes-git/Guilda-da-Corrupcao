# O mundo — áreas, estrada e relógio

> Desenhado com o autor em 10/09/2026, e **decidido no mesmo dia: este é o rumo.** O lugar do jogo
> é a **área**, e são sete, cada uma com a sua regra. O que o código hoje chama de bioma é só o
> aspecto do local no mapa — deixou de ser conceito de design, e não se fala mais dele aqui.
>
> **Nada disto está construído**, e boa parte contraria o que o código faz hoje: hoje as sete
> regiões do código *são* a unidade de jogo, e o mapa é uma tela de escolher missão. Nomes marcados
> como *(trabalho)* são descartáveis — o que vale é a função.

## A regra do mapa

- **Um plano único e navegável.** O jogador percorre o mapa com os olhos e **clica no destino**. É
  tela de decisão, não fase. Referência dada pelo autor: o mapa de *Ori and the Blind Forest*.
- **A guilda fica numa borda**, não no centro. No centro, todas as áreas ficam à mesma distância e a
  distância não significa nada.
- **As áreas se tocam.** Chegar às distantes obriga a atravessar as próximas, e por isso a corrupção
  de uma área pesa mesmo quando não se vai a ela.
- **O mundo é maior que a partida:** o mapa tem sete áreas e cada run monta o plano com cinco. O
  jogador aprende cinco regras por partida, e as que sobram alimentam o pós-game. *(proposto,
  pendente de decisão)*

**As duas moedas.** Cada área dá **tempo** ou **força**, e dentro de cada grupo elas são substitutas
— escolhe-se por qual caminho conseguir, não se coleciona todas. O Oráculo é a exceção: dá
informação, que nenhuma outra dá.

---

## As sete áreas

### A Mata *(trabalho)* · perto · tempo
- **Regra:** o mato fecha atrás do grupo. **Quanto mais fundo, mais caro o dia** — os últimos dias
  da rota consomem mais mantimentos e mais tochas que os primeiros.
- **Dá:** caça. Mantimentos que renovam na estrada.
- **Cobra:** dias. O caminho serpenteia e a viagem é mais lenta que a distância sugere.
- **Fecha-se:** derrubando o que espalha. Combate limpo — é o selo-escola, e por isso a área fica
  colada na guilda.

### A Cripta *(trabalho)* · perto · força
- **O lugar:** a cripta de um necromante.
- **Regra:** os mortos se erguem **uma vez por turno** enquanto o necromante estiver de pé.
- **A dívida do jogador entra aqui:** herói enterrado **sem tributo pago** no Cemitério levanta
  contra o grupo. Quanto mais gente você perdeu e não honrou, mais dura a área fica.
- **Dá:** espólio em dobro — foram enterrados com o que tinham.
- **Cobra:** estresse.
- **Fecha-se:** consagrando de novo, e o ritual pede os **nomes dos seus próprios mortos**. Não é
  combate: é devolver.

### A Aldeia *(trabalho)* · tempo
- **O lugar:** parte dos moradores ainda é gente; parte são cidadãos corrompidos — **e têm
  consciência**.
- **Regra:** poupar ou matar, um a um, e a escolha é real. **Poupar** devolve moral e alivia
  estresse; **matar** dá recurso e cobra moral.
- **A escolha muda de peso com a partida:** com o grupo inteiro, matar é obviamente melhor; com dois
  heróis à beira da quebra, o estresse é o que mata e poupar vira a saída.
- **Fecha-se:** com fogo, ou levando embora quem ainda dá para levar. **Duas saídas** — a única área
  com selo alternativo.

### O Covil *(trabalho)* · força
- **O lugar:** o covil de um dragão — **o único ser não corrompível**. É a prova de que existe algo
  que a Corrupção não alcança.
- **Regra:** a luz o desperta. Cada tocha acesa aproxima o despertar; no escuro se anda seguro dele
  e cego para o resto.
- **Dá:** **as relíquias** — é o único lugar do mundo onde elas se acumulam. E **cada peça levada
  aproxima o despertar**: a ganância é o segundo relógio, junto com a luz.
- **Cobra:** nervo. Atravessar no escuro desgasta mais que qualquer outra área.
- **Fecha-se:** acordando o dragão de propósito e **saindo antes**. Você não o mata: você o usa, e o
  fogo dele é a única coisa que limpa de verdade.

### A Torre *(trabalho)* · força
- **O lugar:** um mago antigo tentou copiar os efeitos da Corrupção, e o que ele fez copia gente.
- **Regra:** a cada combate, **um dos seus heróis aparece do outro lado**, com o baralho e o
  equipamento dele.
- **Dá:** as cartas que o feiticeiro colecionou — as que a Biblioteca não vende.
- **Cobra:** estresse em quem derrubou a própria cópia.
- **Fecha-se:** subindo até o alto e quebrando o **altar com o livro corrompido**.
- **Por que importa:** é a ideia do Campeão em pequeno e no meio do jogo. Quando o final cobrar o
  herói que ficou, o jogador já viu isso acontecer — e a regra do fim não precisa ser explicada.

### A Forja Abandonada *(trabalho)* · força
- **O lugar:** o ferreiro morreu, o fogo não.
- **Regra:** dá para forjar ali, na estrada, sem voltar à guilda. **Cada peça forjada chama o que
  mora lá** — o martelo é o barulho que atrai.
- **Dá:** equipamento **corrompido**: melhor que o da guilda, e some exposição à corrupção do
  portador a cada combate. Quem carrega a arma boa apodrece primeiro, e o jogador escolhe em quem
  pôr. *(É o que finalmente dá função ao `corruptionExposure`, que hoje acumula e ninguém lê.)*
- **Cobra:** o que vem quando você bate.
- **Fecha-se:** apagando o fogo. A área para de forjar para sempre.

### O Oráculo *(trabalho)* · informação
- **O lugar:** a cega da gruta.
- **Regra:** ela responde qualquer coisa — a rota inteira, qual chefe guarda o quê, o que falta para
  selar.
- **Cobra:** cada resposta custa uma lembrança. Um herói **perde uma carta do baralho, para
  sempre**.
- **Dá:** o único recurso que o mapa não tem — saber antes.
- **Fecha-se:** perguntando a ela o que ela é. A resposta cobra o preço de sempre.

**Os sete selos são sete atos**, e só o da Mata é combate limpo: vencer, devolver os mortos,
escolher entre fogo e resgate, usar o dragão, quebrar o altar, apagar o fogo, e pagar a última
pergunta. *(Quantos desses ainda passam por uma luta antes do ato — a Torre pede subir, a Aldeia
pede limpar — é decisão que não foi tomada. O número "cinco de oito" que este documento trazia antes
não fechava com a lista, e saiu.)*

---

## Na estrada

### A Encruzilhada *(trabalho)*
Não é área — é **ramificação da jornada**, que aparece de vez em quando. Vende o que ninguém mais
vende: itens mais valiosos **e corrompidos**. O preço vem **na compra ou no uso**.

### O Posto da guilda
O único lugar seguro que o jogador constrói.

- **Nasce:** você paga, e **um herói fica** de guarnição, com o nível e o equipamento que tem.
- **Dá:** a região em volta fica mais barata de atravessar, e o posto guarda coisas sem risco.
- **Exige presença.** Não voltar por alguns ciclos entrega o posto à corrupção — o relógio corre
  enquanto você faz outra coisa. *(chute a corrigir: três semanas)*
- **Ou você desarma:** ir lá, recolher e trazer o herói de volta. É a única forma de sair no lucro.
- **Se cair:** o herói volta contra você, com o que tinha, **carregando o que você guardou ali**.
  Recuperar suas coisas é derrotá-lo.

### O Esconderijo
Deixar o que pesa e retomar depois. **Não é seguro por natureza — é seguro conforme o que você faz
naquela área:**

| Área | O que acontece com o que foi escondido |
|---|---|
| **O Covil** | acordar o dragão e fugir **queima tudo** — é o preço de usar o selo |
| **A Aldeia** | se você escolheu o fogo, o seu esconderijo arde junto |
| **A Cripta** | enquanto os mortos andarem, eles levam o que acharem |
| **A Mata** | o mato fecha e você não acha de volta — procurar custa dias |
| **A Torre** | uma cópia sua aparece guardando o seu próprio esconderijo |
| **A Forja** | o que atraiu o martelo continua lá, em cima do que você deixou |

**Nenhuma área guarda com segurança.** A exceção era a Abadia, que saiu em 10/09: esconder passou a
ser sempre uma aposta, e o Esconderijo ficou sem o lugar que o fazia valer a pena. Ver "Em aberto".

---

## O relógio

**O relógio da corrupção conta dias na estrada.** A guilda não gasta relógio: o mundo não apodrece
enquanto o grupo está em casa, e a pressão fica onde ela é interessante — a decisão de sair e de até
onde ir.

**Encadear é mais barato que voltar**, e o jogador chega nisso sozinho: visitar duas áreas médias em
viagens separadas custa uns 24 dias de estrada; encadeando, uns 15. O que segura a permanência em
campo é o outro relógio — estresse, ferimento e fome —, e é isso que a Estalagem e o Posto aliviam.

**A semana substitui o ciclo**, e passa com os dias viajados:

| Antes, por ciclo | Agora | Comportamento |
|---|---|---|
| Corrupção | **0,85 por dia** | é a régua atual dividida: 6 por jornada de 7 dias, e o balanceamento medido continua valendo |
| Tradução da Biblioteca | 1 a cada 7 dias | **acumula como direito** — três semanas fora, três páginas para ler na volta, e a escolha de quais continua sendo do jogador |
| Estoque do Mercado | renova a cada 7 dias | **não acumula.** Vê-se o último, e as ofertas que passaram, passaram |
| Salário | 1 a cada 7 dias | **acumula como dívida.** Quem some por um mês volta devendo quatro |

São três moedas diferentes cobrando a mesma viagem longa: oportunidade perdida, dívida e leitura
represada.

---

## Em aberto

- **A posição das áreas no plano.** Só Mata e Cripta (perto) estão fixadas.
- **Cinco de sete por partida** — proposto para segurar a carga de regras, não decidido.
- **O que a saída da Abadia deixou em aberto**, decidida em 10/09: ela era o descanso fundo fora da
  guilda, o único esconderijo confiável e uma das três áreas de *tempo* (sobraram duas, contra
  quatro de força). Se nada ocupar o lugar, a estrada fica sem alívio de estresse e sem depósito
  seguro — a Estalagem, desenhada e não fechada, é a candidata natural.
- **O que faz o Ermo do mapa**, se houver: nenhuma área hoje é só passagem.
- **Um lugar seguro a mais:** a Estalagem foi desenhada e não foi fechada.
- **Os finais.** A Muralha, descartada como área, deixou a forma que interessa: **selar por recusa**
  — o primeiro selo que não é ato, e sim decisão de não agir. É por aí que sai um final sem combate.
