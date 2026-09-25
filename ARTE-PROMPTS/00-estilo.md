# O estilo — o que entra em toda geração

> Escolhido em 25/09/2026: **pintura digital sombria com traço grosso**. Todo prompt deste diretório
> já vem com o bloco certo embutido; este arquivo existe para quando você precisar montar um pedido
> novo, ou conferir por que uma peça saiu fora do conjunto.

## Bloco de cenário

Vale para pátio, salas, áreas, eventos, mapa — tudo o que é lugar.

```
Grim dark-fantasy game illustration, hand-painted digital gouache. Visible brush strokes, heavy
black ink outline around every major shape, deliberately rough and uneven edges. Limited palette:
soot black, cold stone grey, ochre, moss green, dried-blood red. Overcast, colourless daylight
from a heavy sky, with small warm pools of torch and lantern light at ground level. Heavy
chiaroscuro, thick shadows, muted and desaturated, oppressive and worn-down mood, as if painted
on aged parchment. Painterly texture throughout. No clean vector lines, no cel shading, no anime,
no photorealism, no 3D render, no glossy finish. No text, no letters, no numbers, no logos, no
watermark, no border, no UI frame.
```

## Bloco de figura

Vale para herói, inimigo, retrato — tudo o que é gente ou bicho recortado do fundo.

```
Grim dark-fantasy game illustration, hand-painted digital gouache. Visible brush strokes, heavy
black ink outline around every major shape, deliberately rough and uneven edges. Limited palette:
soot black, cold stone grey, ochre, moss green, dried-blood red. Muted and desaturated, heavy
chiaroscuro, worn-down and unheroic mood, as if painted on aged parchment. Painterly texture
throughout. No clean vector lines, no cel shading, no anime, no photorealism, no 3D render, no
glossy finish. No text, no letters, no numbers, no logos, no watermark, no border, no UI frame.

Full-body figures in three-quarter view: each figure is turned roughly thirty degrees away from
the viewer, one shoulder closer to the camera, head and feet fully inside the frame. This is not a
flat frontal pose. Even, neutral light on every figure, from the upper left. Flat uniform mid-grey
background, completely empty: no scenery, no floor, no props lying around, no cast shadow and no
contact shadow under the feet. The grey of the background must not tint the figures.
```

## As três armadilhas já pagas

1. **O modelo inventa horizonte.** Onde o pátio tinha só céu cinza além da muralha, duas variações
   pintaram campos lavrados e uma linha de árvores. Como aquilo difere da base, entra no recorte. Em
   todo pedido de cenário: `Beyond the compound wall there is only flat overcast sky — no fields, no
   horizon line, no trees, no distant landscape.`
2. **Toda edição regenera a imagem inteira**, com variação mínima em tudo — nuvem que anda, poça que
   muda de forma. Por isso o motor usa só o **retângulo** da coisa que mudou, e o resto da variação é
   descartado. Nunca entregue a imagem de variação inteira ao jogo.
3. **Consertar depois não funciona.** Repintar o céu por média de linha alisou o que não era céu e a
   muralha virou um bloco cinza. Está registrado em `Tools/guild_art.py`, em `CEU_A_REFAZER`, que
   ficou vazio de propósito.

## Resolução — a régua

O pátio saiu em **3168×1344**. É o alvo: peça a maior resolução que o gerador aceitar.

| Peça | O que ela precisa ter |
|---|---|
| Cenário de tela cheia | 3168×1344 (21:9), para sobreviver ao esticamento até 1920×1080 |
| Figura de herói | ~500 px de largura por ~1200 de altura, **por figura** |
| Recorte de porta | o retângulo da construção ocupando quase todo o quadro |

**Se o gerador teimar em 1024 de largura**, não amplie: gere em pedaços menores. Três figuras por
folha em vez de seis dobra a resolução de cada uma, e duas folhas de três casam entre si se a
segunda for pedida com a primeira anexada.
