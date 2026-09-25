# Pedido: as seis portas que falam

**Entrega:** 6 edições do pátio, uma por sala que tem algo a dizer.
**Destrava:** o guia da guilda sem texto — a porta diz **o quê**, não só "tem coisa aqui".
**Depende de:** o pátio aprovado, anexado em toda edição, e a sala já erguida (terreno não acende).
**Estado:** não geradas. O `GuildGuide` já sabe quando cada estado vale; falta a imagem.

---

## A decisão que está por trás

Decisão fechada no `ARTE.md`: cada porta precisa de um estado que diga **o quê**. Não é brilho, é
conteúdo — gente esperando na porta da Taverna, a lápide nova sem nome no Cemitério, a carroça
chegando no Mercado.

| Porta | Acende quando |
|---|---|
| Taverna | há recruta que serve e ouro para pagá-lo |
| Forja | alguém está desarmado e há ouro |
| Biblioteca | há carta que serve a alguém, ou página por traduzir |
| Cemitério | há morto **sem tributo pago** |
| Sala de Mapas | uma região completou o mapa, ou está pronta para selar |
| Mercado | estoque novo ou desconto |

**A Jornada não tem estado aceso.** É a porta que sempre serve, e acender uma porta que nunca apaga
não diz nada.

O Cemitério é o caso que mostra por que isso importa: com a Cripta, morto sem tributo **volta contra
o grupo**. A porta pesada é o aviso de uma consequência já marcada.

---

## O prefixo

**Anexe o pátio aprovado.** Vale para as seis.

```
Using the attached image, keep the exact same composition, camera angle, framing, palette, brush
style, outline weight and light direction. Change only the one structure described below.
Everything else in the painting must stay pixel-identical.

Beyond the compound wall there is only flat overcast sky — no fields, no horizon line, no trees, no
distant landscape. The sky must be exactly the sky of the attached image.

The change must be readable in a crop of that structure alone, at about a quarter of the width of
the painting: whatever you add has to be large enough and contrasted enough to be seen at that size.
```

## As seis

| Porta | Prompt |
|---|---|
| **Taverna** | `The half-timbered tavern with the sagging roof and the empty sign bracket, lower centre: its windows are now lit with warm light from inside, the door stands open with light spilling onto the mud, and two hooded figures wait outside it in silhouette, one sitting on the step.` |
| **Forja** | `The narrow soot-blackened forge tower on the far left: its furnace is now lit — orange glow pouring out at its foot, sparks around the anvil under the lean-to, and a thick plume of smoke rising from the tall chimney into the grey sky.` |
| **Biblioteca** | `The steep-roofed stone chapel-like building in the centre with the tall pointed window: the window is now lit from within with warm candlelight, its door stands ajar with light in the gap, and a stack of books and loose pages sits on the step outside.` |
| **Cemitério** | `The arched crypt entrance with the iron gate on the left, and the grave slabs in the mud in front of it: add one freshly cut pale grave slab in the foreground, blank and uncarved, the earth around it newly turned, with a shovel left standing in it. Keep the light on this building cold — no fire, no torch, no warm glow anywhere.` |
| **Sala de Mapas** | `The two-storey half-timbered house with the external wooden stair, upper right: its upper windows are now brightly lit from inside, and through the glass a large unrolled map can be seen spread on a table, with a figure leaning over it.` |
| **Mercado** | `The long low market arcade on the right with the hanging lanterns: a loaded merchant's handcart is now parked at its near end, piled with crates and sacks under a canvas cover, a mule tethered beside it, and every lantern along the stalls burning brighter.` |

---

## O que conferir antes de aceitar

| Conferir | Por quê |
|---|---|
| A mudança se vê no recorte sozinho, a ~300 px de largura | é o tamanho da porta na tela. Fumaça fina e vela pequena somem ali |
| A construção não mudou de forma nem de lugar | o recorte é encaixado por âncora fixa: prédio deslocado vira degrau |
| O céu dentro do retângulo continua o do pátio | mesma armadilha dos terrenos |
| O Cemitério continua frio | é a única porta que acende sem fogo, e é isso que a faz ler como aviso |

---

## Como isso entra no jogo

Cada variação é recortada no mesmo retângulo da sala e entra como **terceiro sprite** daquela porta,
ao lado da construção e do terreno. O `GuildArt.Registrar` hoje entrega dois (`sala` e `terreno`) ao
`GuildGuide`; o aceso é uma entrada a mais na mesma tabela.

Enquanto a imagem não existe, a porta acesa é a construção normal com a moldura pulsante por cima —
que é o estado em que a guilda está hoje.
