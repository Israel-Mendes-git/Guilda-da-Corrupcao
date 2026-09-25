# Pedido: os cinco terrenos vazios

**Entrega:** 5 edições do pátio, uma por sala que o jogador ainda não ergueu.
**Destrava:** a guilda da primeira partida — ela abre com duas construções de pé e cinco canteiros.
**Depende de:** o pátio aprovado (`ArtSource/Guild/patio.jpg`), anexado em toda edição.
**Estado:** os cinco geraram em 25/09 e estão no jogo. **Dois precisam voltar** — ver o fim.

---

## A regra que faz isto funcionar

O modelo **regenera a pintura inteira** a cada edição, com variação mínima em tudo: nuvem que anda,
poça que muda de forma, pincelada que troca de lugar. Se cada estado entrasse no jogo como pintura
inteira, o pátio piscaria ao erguer uma sala.

Por isso o motor recorta **só o retângulo daquela construção** e o sobrepõe ao pátio base. O resto
da variação é descartado — mas o que estiver dentro do retângulo entra, inclusive o que o modelo
inventou sem que ninguém pedisse.

---

## O prefixo

**Anexe o pátio aprovado.** Vale para os cinco.

```
Using the attached image, keep the exact same composition, camera angle, framing, palette, brush
style, outline weight, ground plane and light direction. Everything else in the painting must stay
pixel-identical.

Beyond the compound wall there is only flat overcast sky — no fields, no horizon line, no trees, no
distant landscape, no rooftops, no silhouettes of any kind. The sky must be exactly the sky of the
attached image.

Remove the one structure described below and leave an unbuilt plot in its place: bare packed earth
and broken flagstone, lashed wooden scaffolding poles marking out its footprint, a stack of uncut
stone blocks, a bucket, a coil of rope and a tarpaulin. No roof, no walls, no door, no light of its
own. Where the removed structure used to rise above the wall, there is now only sky.
```

## Os cinco

| Terreno | Prompt |
|---|---|
| **Forja** | `Remove the narrow soot-blackened tower on the far left, chimney and all. In its place, a cold half-built stone furnace stands unfinished, its chimney a stub open to the sky, the anvil lying on its side in the mud.` |
| **Cemitério** | `Remove the arched crypt entrance with the iron gate on the left. In its place the earth is partly dug out, with two empty grave-sized pits open in the ground and the existing grave slabs still lying around them.` |
| **Biblioteca** | `Remove the steep-roofed stone building with the tall pointed window in the centre. In its place, one bare wooden shelf frame stands alone in the open, unvarnished and empty.` |
| **Mercado** | `Remove the long low market arcade on the right, roof, stalls and lanterns. In its place, a collapsed stall frame lies broken across the plot, and the lanterns hang dark from a bare pole.` |
| **Sala de Mapas** | `Remove the two-storey half-timbered house with the external stair, upper right. In its place, a bare trestle table stands on the open plot, covered in dust, with nothing on it.` |

---

## O que conferir antes de aceitar

**Olhe o céu primeiro, e só depois o canteiro.** O canteiro sai bom quase sempre; o céu é onde o
modelo inventa.

| Conferir | Como |
|---|---|
| O céu dentro do retângulo é o céu do pátio | ponha a variação e o pátio lado a lado e olhe só a faixa de cima |
| Não sobrou fantasma da construção removida | contorno claro no céu com a forma do telhado que saiu |
| A muralha não mudou de altura nem de recorte | ela é a emenda: muralha diferente vira degrau no pátio |
| O chão do canteiro continua no mesmo plano | canteiro mais alto ou mais baixo que o pátio descola a construção vizinha |

---

## As duas regerações pendentes *(25/09)*

Os cinco entraram no jogo e a guilda foi capturada com eles. Duas falharam no céu, e as duas são no
mesmo lugar: **acima da muralha**, onde a construção removida abria espaço para o modelo inventar.

- **Forja — grave.** O terço de cima do recorte voltou como **campo lavrado com linha de árvores e
  céu claro**. Na tela isso aparece como um retângulo de paisagem colado no canto superior esquerdo
  da guilda, e é o defeito mais visível da tela hoje. Regere com o prefixo acima.
- **Sala de Mapas — leve.** Sobrou no céu o **contorno claro do telhado** da casa que foi removida,
  como uma marca. Aparece acima da muralha direita. Regere com o prefixo acima.

**Não tente consertar por processamento.** Repintar o céu por média de linha já foi tentado: alisou
também o que não era céu, e a muralha virou um bloco cinza. Está registrado em `Tools/guild_art.py`,
em `CEU_A_REFAZER`, que ficou vazio de propósito. E recortar mais baixo não serve para a Forja: a
mesma caixa é usada pela torre de pé, que é alta.

Depois de regerar, grave em `ArtSource/Guild/terreno_forja.jpg` (e `terreno_saladomapas.jpg`),
sobrescrevendo, e rode `python Tools/guild_art.py recortar`.
