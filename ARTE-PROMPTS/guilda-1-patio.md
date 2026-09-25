# Pedido: o pátio da guilda — a peça mestra

**Entrega:** 1 pintura, 21:9. Saiu em **3168×1344** em 25/09.
**Destrava:** o fundo da guilda e as sete portas, que são recortes dela.
**Estado:** gerado, recortado e no jogo. Este arquivo fica como referência — se o pátio precisar ser
refeito, é este o pedido, e **tudo o que existe hoje sai junto**.

---

## A decisão que organiza o bloco

As sete portas **não são sete ilustrações**. São sete pedaços de **uma pintura só** — o pátio visto
de fora, no espírito do mapa da vila do *Darkest Dungeon*, onde cada construção é parte do mesmo
quadro e clicar nela entra na sala.

Isso revisou o que o `ARTE.md` previa: lá as sete eram *interiores vistos por uma porta*. O interior
de cada sala continua existindo como arte, mas é a tela de dentro — outro bloco.

**A consequência prática é a ordem do trabalho.** Sete peças soltas sairiam com sete linhas de
horizonte e sete alturas de sol, e o conjunto leria como sete quadros pendurados. Então: gera-se o
pátio inteiro; recorta-se cada porta dele; e os estados (terreno, aceso) são **edições** do pátio
aprovado, nunca gerações novas.

---

## O prompt

```
Grim dark-fantasy game illustration, hand-painted digital gouache. Visible brush strokes, heavy
black ink outline around every major shape, deliberately rough and uneven edges. Limited palette:
soot black, cold stone grey, ochre, moss green, dried-blood red. Overcast, colourless daylight
from a heavy sky, with small warm pools of torch and lantern light at ground level. Heavy
chiaroscuro, thick shadows, muted and desaturated, oppressive and worn-down mood, as if painted
on aged parchment. Painterly texture throughout. No clean vector lines, no cel shading, no anime,
no photorealism, no 3D render, no glossy finish. No text, no letters, no numbers, no logos, no
watermark, no border, no UI frame.

A single continuous painted scene of a ruined adventurers' guild compound, seen from a slightly
elevated three-quarter view, like the village map screen of a dark fantasy management game. One
courtyard of packed mud and broken flagstone, enclosed by a crumbling stone wall, with seven
distinct structures arranged around it. All structures share the same ground plane, the same
overcast grey daylight from the upper left, and the same scale. Ultra wide panoramic composition.

Beyond the compound wall there is only flat overcast sky — no fields, no horizon line, no trees, no
distant landscape.

Place the structures exactly as follows:

- FAR LEFT, MIDDLE HEIGHT: a low crypt entrance built into the wall — stone arches, iron gate,
  grave slabs in the mud in front of it, cold blue-grey light, no fire.
- FAR LEFT, BOTTOM CORNER: a tall narrow forge tower, much taller than wide, soot-blackened
  stone, a chimney rising out of frame, an anvil under a lean-to at its foot.
- BOTTOM LEFT OF CENTRE: a squat timber-and-stone tavern with a low sagging roof, shuttered
  windows and a hanging sign bracket with no sign on it.
- CENTRE, SLIGHTLY ABOVE THE MIDDLE: a small stone library annex with a steep slate roof and one
  tall narrow window.
- DEAD CENTRE: an open gatehouse in the courtyard wall — a heavy arch leading out to a muddy
  road that disappears into cold fog beyond the compound. This is the only view of the outside
  world in the painting.
- RIGHT, BELOW THE MIDDLE: a long low market arcade, much wider than tall, a row of stalls under
  a single sloping roof with hanging lanterns.
- FAR RIGHT, TOP CORNER: a two-storey map room with a bay window and an external wooden stair.

The courtyard between them is empty — no people, no crowds. Dead grass in the cracks, puddles,
a broken cart, scattered crates. Everything worn, damp and half-abandoned.
```

Gere 4 variações e escolha **uma**. Tudo o que vem depois é edição dela — trocar de base no meio do
caminho desfaz o encaixe de todas as peças já feitas.

---

## Os recortes, como ficaram

Medidos sobre a pintura de 3168×1344 e conferidos com `python Tools/guild_art.py conferir`, que
desenha cada retângulo por cima. Deduzi-los da diferença entre as imagens **não funciona**: a
regeração muda área demais, e o maior componente conectado caía na construção errada em três dos
cinco casos.

| Sala | Retângulo (x0, y0, x1, y1) |
|---|---|
| Forja | 50, 70, 500, 1300 |
| Cemitério | 330, 340, 930, 800 |
| Biblioteca | 1142, 105, 1677, 664 |
| Jornada | 1681, 144, 2184, 656 |
| Sala de Mapas | 2425, 18, 3135, 727 |
| Mercado | 1974, 556, 2929, 1210 |
| Taverna | 754, 564, 1574, 1297 |

Levam ~30 px de folga em volta da construção **de propósito**: é nessa margem que a borda do recorte
derrete no pátio. Sem folga, o degradê comeria a construção em vez da emenda.

**As posições da arte mandam nas do código, e não o contrário.** As âncoras das portas em
`GuildArt.Construcoes` saíram destes retângulos. O caso que obrigou a essa ordem foi a Jornada: o
portão foi pintado embutido na muralha do fundo, e não no meio do pátio, onde o botão estava.

---

## Como cada construção ficou na pintura de 25/09

Descrever a construção **como ela é na imagem** é o que faz o modelo editar a certa. Estas são as
referências a usar nos pedidos de terreno, aceso e refino:

| Sala | Como reconhecê-la na pintura |
|---|---|
| Forja | a torre estreita de pedra enegrecida na borda esquerda, com a chaminé alta e a bigorna sob o telheiro, ao pé |
| Cemitério | a entrada de cripta de arcos com portão de ferro, à esquerda do centro, com as lápides na lama à frente |
| Biblioteca | a capela de pedra de telhado íngreme e janela alta em ogiva, no centro, acima do meio |
| Jornada | o arco de pedra embutido na muralha do fundo, com a estrada de lama saindo dele para a névoa |
| Sala de Mapas | a casa de dois andares em enxaimel no canto superior direito, com a sacada e a escada externa de madeira |
| Mercado | a arcada longa e baixa de telhado corrido à direita, com as lanternas acesas sobre as bancas |
| Taverna | a construção de enxaimel de telhado caído no centro-baixo, com o suporte de placa vazio |

---

## O que conferir antes de aceitar

| Conferir | Por quê |
|---|---|
| Cada construção se sustenta sozinha no seu retângulo | é isso que o jogador vê ao passar o mouse; recorte frouxo vira parede com pátio em volta |
| Nenhuma construção invade o retângulo da vizinha | a emenda aparece quando o pedaço de uma entra na outra |
| Há céu chapado além da muralha | é a única defesa contra o horizonte inventado nas edições seguintes |
| A metade inferior de cada construção é calma e escura | o nome da sala e o preço são escritos ali pelo motor |
