# Arte — o que falta produzir

> Levantamento de 09/09/2026, **atualizado em 10/09** contra o mundo desenhado em
> [`MUNDO.md`](MUNDO.md) e as decisões de interface do `ROADMAP.md` (Fase 3.12).
> **O autor ratificou este mundo em 10/09:** as **sete áreas** são o rumo (a Abadia
> saiu na mesma conversa), e o que o código chama de bioma vira só o aspecto do
> local no mapa navegável — deixou de ser conceito de design.
> **Nada da arte que está na tela fica.** Tudo é asset de loja, quase todo gratuito, usado como
> placeholder. A lista abaixo é a arte do jogo inteiro, não um remendo do que ficou ruim.

O placeholder ainda serve para uma coisa: dizer **onde cada peça entra e em que tamanho**. A camada
que liga arte e código já existe e não muda — são catálogos (`CardArt`, `EnemyArt`, `EventArt`,
`GuildArt`, `BiomeArtBuilder`, `MapArtBuilder`, `ForgeArt`, `PortraitCatalog`) que apontam para
arquivo. Trocar o arquivo troca a arte; não há código novo a escrever para receber nenhum item
desta lista.

Referência de tela: **1920×1080**, Canvas em `ScaleWithScreenSize`.

---

## Resumo

| Bloco | Peças | Estado |
|---|---|---|
| Cartas | 45 | 40 ilustrações, 4 molduras de raridade, 1 verso |
| Guilda | 14 | 1 fachada + 7 interiores + **6 portas que falam** |
| Combate | 34–50 | 5 chefes + 8–16 comuns, × 2 poses · 7 fundos de área · 1 camada de corrupção |
| Heróis | 48–54 | 12–18 **corpos inteiros** + 36 peças de equipamento |
| Ícones | ~62 | 15 efeitos, 6 recursos, 6 classes, 12 traços, 10 itens, **7 regras de área**, 6 do fim |
| Eventos | 25 + 1 | ilustração por evento, **a redistribuir pelas áreas novas** + a Encruzilhada |
| Mapa | ~13 | **1 plano ilustrado navegável** + 7 áreas nele + lugares seguros + trilha |
| VFX | 19 | 15 de combate + 4 dos selos |
| UI | kit | molduras, botões, barras |
| Corrupção | 4 | medidor, tela, camada de área, tratamento de item e de herói |
| Estrada | 5 | Estalagem, Posto de pé, Posto tomado, Esconderijo, Encruzilhada |

**Cerca de 285 peças contáveis** — eram 190 antes do mundo novo. O que cresceu: o mapa (de papel com
ícones para plano ilustrado), os heróis (corpo inteiro em vez de retrato) e o combate (sete áreas
com regra e chefe próprios, no lugar de sete pacotes de cenário).

A ordem dos blocos é por **tempo que a peça passa na tela**, não por dificuldade.

---

## 1. Cartas

O objeto mais visto do jogo: a mão fica aberta durante todo o combate e toda a jornada.

| Peça | Quantidade | Hoje |
|---|---|---|
| Ilustração de carta | **40** — 10 × Guerreiro, Mago, Curandeiro, Caçador | ícone de habilidade do pacote Blink, 256×256 |
| Moldura por raridade | **4** — comum, rara, épica, lendária | moldura única do kit de UI, cor trocada na borda |
| Verso | **1** | não existe; baralho e descarte são retângulos |

A carta montada tem cinco elementos: fundo, moldura, ilustração, nome, descrição e custo. Custo é
número sobre a arte, então a ilustração precisa de área calma no canto superior.

**Ladino e Bardo não têm carta nenhuma.** As duas classes existem em `HeroClass` e têm nome
traduzido em seis telas, mas `HeroFactory.classesJogaveis` sorteia só quatro. O concept delas entra
normalmente; **torná-las jogáveis custa +20 ilustrações**.

**Duas fontes novas de carta**, do mundo desenhado: a Torre dá cartas que a Biblioteca não vende, e
o Oráculo **apaga** uma carta do baralho por resposta. A primeira pede ilustrações a mais — quantas
depende de quantas cartas exclusivas existirem.

## 2. Guilda

| Peça | Quantidade | Hoje |
|---|---|---|
| Fachada da guilda | **1** | seis portas retangulares sobre mármore escuro |
| Interiores | **7** — Taverna, Biblioteca, Forja, Mercado, Cemitério, Sala de Mapas, Jornada | cenas emprestadas de um pacote de cavernas, com véu escuro por cima para o nome ser legível |
| **Portas que falam** | **6** | não existe |

Os sete interiores precisam ler como **o mesmo lugar** visto por sete portas. O erro já cometido com
placeholder foi escolher por assunto: reduzidas ao tamanho de uma porta, paisagens abertas viram
manchas coloridas. Interior com foco de luz e teto visível funcionou; paisagem, não.

**As portas que falam são decisão fechada** e substituem o guia de texto da guilda. Cada uma precisa
de um estado que diga **o quê**, não só "tem coisa aqui":

| Porta | Acende quando | O que se vê |
|---|---|---|
| Taverna | há recruta que serve e ouro para pagá-lo | gente na porta |
| Forja | alguém está desarmado e há ouro | fogo aceso, fumaça |
| Biblioteca | há carta que serve a alguém, ou página por traduzir | luz na janela |
| Cemitério | há morto **sem tributo pago** | lápide nova, ainda sem nome |
| Sala de Mapas | uma região completou o mapa, ou está pronta para selar | mapa aberto na mesa |
| Mercado | estoque novo ou desconto | carroça na porta |

O Cemitério é o caso que mostra por que isso importa: com a Cripta, morto sem tributo **volta contra
o grupo**. A porta pesada é o aviso de uma consequência já marcada.

**Pendente:** com o mapa navegável, o quadro de missões e a Sala de Mapas podem virar a mesma tela.
Se virarem, é um interior a menos.

## 3. Combate

| Peça | Quantidade | Hoje |
|---|---|---|
| Chefes — 2 poses cada | **5** — um por área que se fecha lutando | 5 chefes, todos pixel art emprestada |
| Inimigos comuns — 2 poses cada | **8–16** | 6 comuns, com quatro repetindo arte separada por cor e tamanho |
| Fundo de área | **7** | há 7 fundos hoje, um pacote diferente para cada, sem paleta comum |
| Camada de corrupção | **1** | não existe |

São **duas poses por inimigo** — parada em 3/4 e o momento do golpe ou da magia —, não animação. O
código troca sprite sem Animator, então duas imagens bastam. Dano e morte ficam fora.

**Os chefes mudaram de lista com o mundo novo.** As áreas pedem: o que espalha na Mata, o
**necromante** da Cripta, o **dragão** do Covil, o que mora na Forja Abandonada, e o que ainda
comanda na Aldeia. Dois casos especiais e baratos: a **Torre** usa como
inimigo **os próprios heróis do jogador** — a arte já existe, é a do bloco 4 —, e o **Oráculo** não
tem chefe, porque se fecha com uma pergunta.

Dos cinco chefes atuais, **A Coisa da Mata** sobrevive de casa; os outros quatro perdem o lugar
junto com as regiões que os sediavam.

**Quantos comuns por área** é decisão de escopo: dois exclusivos por área dão 16; um pool
compartilhado com um exclusivo por área dá 8 mais 4. A camada de corrupção é uma só, reaproveitada
sobre os sete fundos — repintar cada área em dois estados dobraria o bloco.

## 4. Heróis

| Peça | Quantidade | Hoje |
|---|---|---|
| Corpo inteiro, 3/4 | **12–18** — 6 classes × 2–3 | não existe |
| Arma por nível | **18** — 6 classes × 3 níveis | peças de um pacote de partes: guerreiro e ladino têm quatro desenhos, caçador três, mago e curandeiro **um só** |
| Armadura por nível | **18** | idem |

**O retrato não é peça separada: é recorte do corpo inteiro.** E o corpo inteiro deixou de ser só
concept — é a arte da **ficha do herói**, que passa a concentrar o que hoje aparece pela metade em
seis telas. O herói precisa **vestir** o que tem: a arma na mão, a armadura no corpo, as relíquias
penduradas, as poções no cinto.

Por isso o equipamento entrou aqui e saiu do bloco antigo: são peças que se encaixam na figura, não
ícones de lista.

O **equipamento corrompido** da Forja Abandonada é tratamento sobre a mesma peça, não peça nova —
senão os 36 viram 72.

A variedade de caras da taverna passa a ser o número de corpos: 2 ou 3 por classe. Herói é gerado
pela `HeroFactory` e o retrato é sorteado; hoje o sorteio corre sobre ~500 retratos de um pacote.

## 5. Ícones

Tudo aqui é **emoji ou texto** hoje. É a lacuna mais barata de fechar e a que mais suja a tela.

| Conjunto | Quantidade | Exemplo do que está lá |
|---|---|---|
| Efeitos de combate | **15** | veneno, bloqueio, buff, evasão, quebra de escudo — anunciados por número e texto |
| Recursos | **6** | `💰` ouro, comida, memória, dia, XP, corrupção |
| Classes | **6** | `🗡️ Ladino`, `🔮 Mago` |
| Personalidade e traço | **12** | `🦁 Corajoso`, `🤝 Leal`, `🪨 Teimoso` |
| Relíquias e poções | **10** | únicos com ícone de verdade, em `Resources/ItemIcons/` |
| **Regra de área** | **8** | não existe |
| **O fim e a estrada** | **6** | selo, mapa da região, página, posto, esconderijo, exposição à corrupção |

Os 10 de item já mostram o contrato: **um arquivo por id**, nome do arquivo igual ao id.

**Os 8 símbolos de regra de área são requisito de design, não enfeite.** A decisão fechada é que a
regra de cada área se lê **no mapa, antes de viajar**, sem texto: a Mata mostrando o caminho
estreitando, o Covil com a chama, a Cripta com as lápides abertas. Regra que não couber num símbolo
é regra complexa demais e deve ser cortada do jogo.

## 6. Eventos

**25 ilustrações**, uma por evento, todas com cena emprestada do mesmo pacote que veste a guilda —
por isso evento e sala se parecem demais.

**A redistribuir:** os eventos hoje são pareados pelas regiões antigas, e as sete áreas novas
entram no lugar delas. A conta de 25 continua valendo como volume; o que muda é a que área cada um
pertence.

**+1 peça nova:** a **Encruzilhada**, que não é área e sim ramificação da estrada — vende o que
ninguém vende, corrompido, com preço na compra ou no uso.

## 7. Mapa

Este é o bloco que mais mudou. Era papel com ícones; virou **cenário**.

| Peça | Quantidade | Hoje |
|---|---|---|
| O plano do mundo | **1** ilustração navegável, em camadas | pacote de cartografia lite, preto sobre transparente, sete pontos em roda |
| As áreas no plano | **7** | há 7 marcas hoje, com Vulcão e Tundra emprestados |
| Lugares seguros no plano | **3** — Estalagem, Posto, Esconderijo | não existem |
| Ponto de rota | **7** — combate, tesouro, perigo, descanso, mercador, história, chefe | ícones do mesmo pacote |
| Trilha ilustrada da jornada | a definir | diagrama de nós |

**O mapa é um plano único e navegável**, com a guilda numa borda e as áreas se tocando — referência
dada pelo autor: *Ori and the Blind Forest*. O jogador percorre com os olhos e clica no destino.
Isso não é uma tela de menu com ícones: é uma ilustração grande que precisa aguentar navegação, e
por isso pede camadas.

A trilha da jornada continua sem forma definida: **mapa ilustrado com trilha que se revela conforme
o grupo avança**, com os heróis atravessando a pé em fila.

## 8. VFX

**15 de combate**, um por `CombatEffectType`, cada um em duas formas: o **disparo** (o golpe, a
cura) e o **estado enquanto dura** (veneno ativo, buff ativo). Hoje são partículas 3D de três
pacotes convertidas para URP, mais três PNGs soltos. O combate acontece em Canvas, então **sprite
sheet é o formato que funciona ali**.

**+4 dos selos**, porque quatro deles não são combate e precisam de imagem própria: o fogo do
dragão limpando o Covil, a Aldeia queimando, a forja apagando, e a consagração da Cripta.

## 9. UI

Molduras de painel, três popups, botões com os seis estados — incluindo **Disable**, que a opção
travada de evento (`🔒 Precisa de uma carta capaz de…`) precisa para parecer travada —, toggles,
e barras de HP, estresse e energia. A energia é segmentada: são 5 pontos, não uma barra contínua.

## 10. Corrupção

Não existe arte nenhuma. A corrupção é um número percentual em texto na pausa e no menu, e é o tema
que dá nome ao jogo. Com o mundo novo ela ganhou quatro lugares:

- **O medidor.**
- **O tratamento de tela**, quando a run se aproxima do fim.
- **A camada sobre a área no mapa** — é assim que "voltar cobra" fica visível: o lugar muda de cara
  durante a partida, em vez de o número subir.
- **O tratamento de item e de herói** — o equipamento da Forja Abandonada e o herói com exposição
  alta precisam parecer o que são.

## 11. Estrada e lugares seguros

| Peça | O que é |
|---|---|
| **A Estalagem** | o ponto de retorno no meio da estrada. Precisa de dois estados: de pé, e o vazio que sobra quando a corrupção a toma |
| **O Posto — de pé** | construído pelo jogador, com o herói de guarnição à vista |
| **O Posto — tomado** | o mesmo lugar depois de cair, e o herói que ficou agora do outro lado |
| **O Esconderijo** | onde se larga o que pesa |
| **A Encruzilhada** | contada no bloco 6 |

O Posto tomado é a peça mais importante das cinco: é o momento em que o jogador vê o próprio herói
virar inimigo, e é o ensaio do final do jogo.

---

## Decisões que a lista espera

- **Estilo.** A direção provável é tudo pintado. Se for, os inimigos em pixel art saem, e o bloco 3
  é produção nova em vez de substituição.
- **Ladino e Bardo jogáveis.** O concept entra de qualquer forma; recrutáveis custam +20 cartas.
- **2 ou 3 corpos por classe.** Como o retrato é recorte, esse número é a variedade de caras da
  taverna.
- **Quantos inimigos comuns por área** — 16 exclusivos, ou 8 compartilhados mais 4 exclusivos.
- **Se a Sala de Mapas sobrevive** ao mapa navegável, ou se as duas telas viram uma.
- **Cinco áreas por partida das sete** — se ficar de pé, a arte das sete continua necessária, mas o
  jogador vê cinco por run.
