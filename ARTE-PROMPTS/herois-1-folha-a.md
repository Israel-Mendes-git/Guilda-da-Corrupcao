# Pedido: a folha de elenco A — o primeiro corpo de cada classe

**Entrega:** 1 imagem, 21:9, com as seis classes em linha.
**Destrava:** o retrato, a ficha do herói, a taverna e o herói parado no palco — a arte de quatro
telas com uma geração.
**Estado:** primeira versão gerada em 25/09. Ver *O que a primeira saída ensinou*, no fim.

---

## Por que uma folha, e não seis gerações

Seis gerações separadas voltam com seis luzes, seis escalas e seis espessuras de traço, e o grupo lê
como seis jogos. Numa folha só a escala é comparável por construção: o Guerreiro é maior que o
Ladino porque foi pintado maior, não porque saiu maior.

**A ordem da esquerda para a direita é a do `enum HeroClass`** — Guerreiro, Mago, Curandeiro, Ladino,
Bardo, Caçador. Não é estética: é a ordem que o código usa e que o recorte vai indexar.

---

## O prompt

Cole inteiro. O bloco de estilo já está dentro.

```
Grim dark-fantasy game illustration, hand-painted digital gouache. Visible brush strokes, heavy
black ink outline around every major shape, deliberately rough and uneven edges. Limited palette:
soot black, cold stone grey, ochre, moss green, dried-blood red. Muted and desaturated, heavy
chiaroscuro, worn-down and unheroic mood, as if painted on aged parchment. Painterly texture
throughout. No clean vector lines, no cel shading, no anime, no photorealism, no 3D render, no
glossy finish. No text, no letters, no numbers, no logos, no watermark, no border, no UI frame.

A character line-up sheet of six adventurers for hire at a poor, half-ruined guild.

Six full-body figures standing in a single row on the same invisible ground line, evenly spaced,
with a clear gap of empty background between neighbours — no overlapping, no touching, no figure
cropped by the edge of the frame. All six drawn at the same scale, so that any difference in height
between them reads as a real difference in body size.

Every figure is in three-quarter view, turned roughly thirty degrees away from the viewer, one
shoulder closer to the camera. None of them faces the viewer straight on. All six stand at rest,
not fighting: weight on one leg, weapon lowered or sheathed, arms relaxed, alert but calm.

Every one of them is poorly equipped — worn, mended, second-hand gear, nothing polished, nothing
heroic, nothing magical-looking. Cloth is undyed or faded; metal is dull and scratched.

From left to right, in this exact order:

1. WARRIOR — a heavy-set middle-aged man, broad through the shoulders and thickening at the waist.
   Short mended chainmail over a quilted leather jerkin, both too old to shine. A broad one-handed
   sword held point-down in his right hand. A splintered round wooden shield slung on his back.
   Grey beard, cropped grey hair, a broken nose.
2. MAGE — a tall gaunt figure in a threadbare grey wool robe with a deep hood pushed back off the
   head. A crooked unshod wooden staff in one hand. A book tied to the belt with cord. Ink-stained
   fingers. Young hollow face, dark circles, shaved or thinning hair.
3. HEALER — a middle-aged woman in an undyed linen dress under a short travelling cloak. A plain
   iron symbol hanging at her chest on a cord. A simple wooden-handled mace held low in one hand. A
   satchel of bandages at her hip. Hair tied back, tired eyes, lined face.
4. ROGUE — a short compact hooded figure in dark worn leather, layered and patched. Two mismatched
   daggers at the belt. A face cloth pulled down to the neck so the chin and mouth show. Soft quiet
   boots, no metal anywhere on the body.
5. BARD — a lean young man in clothes that were good once and are filthy now: a stained doublet,
   loose sleeves, one boot split open at the toe. A battered wooden lute slung on his back. A
   wide-brimmed hat with a broken feather.
6. HUNTER — a wiry woman in a green-stained wool cloak and hood, the hood up. A short bow held in
   one hand, string slack. A quiver of unevenly fletched arrows at her back. Fingerless leather
   gloves, high laced boots, a scar on the chin.

Flat uniform mid-grey background, completely empty: no scenery, no floor, no props lying around, no
cast shadow and no contact shadow under the feet. Even, neutral light on every figure, from the
upper left. The grey of the background must not tint the figures.

Ultra wide panoramic composition, 21:9, highest resolution available.
```

---

## O que conferir antes de aceitar

| Conferir | Por quê |
|---|---|
| **Cada figura tem ~500 px de largura e ~1200 de altura** | no card de combate o herói aparece com ~200 px de largura, e a ficha é maior. Abaixo disso o retrato sobe borrado |
| Ninguém encosta em ninguém | o recorte precisa de fundo limpo entre as figuras |
| Ninguém está frontal | três quartos é o que dá presença quando a fileira encara o inimigo |
| Não há sombra sob os pés | sombra cinza sobre fundo cinza é a primeira coisa que o recorte come pela metade |
| Os seis se reconhecem a 200 px | tape a cara com o dedo: se dois ficam iguais, a diferença está no rosto e não na silhueta |

---

## O que a primeira saída ensinou *(25/09)*

A folha voltou com a ordem certa, os seis arquétipos reconhecíveis, fundo chapado e todo mundo
pobre. A paleta casa com o pátio; o traço das figuras é mais limpo que o da arquitetura, e isso não
é defeito — figura mais limpa que cenário ajuda a leitura no combate.

Três reparos, que já estão no prompt acima:

- **Resolução.** A saída tinha ~1024 de largura: ~170 px por figura, contra os ~500 necessários. É o
  único reparo que obriga a regerar. Se o gerador não passar de 1024, **gere em duas folhas de três
  figuras** (1–3 e 4–6) em vez de uma de seis.
- **Pose.** Quase todos saíram frontais. Daí as duas frases sobre os trinta graus.
- **Sombra sob os pés.** Veio uma sombra suave que o prompt não pedia. Daí o `no contact shadow`.

**Ao regerar, anexe a folha de 25/09** e peça as mesmas seis pessoas: elas foram aprovadas, o que
falta é tamanho e ângulo. Sem anexar, voltam seis estranhos.

---

## O recorte

Cada figura sai da folha com **fundo removido** e **caixa apertada** — margem vazia em volta encolhe
a figura na tela, que foi o que aconteceu com as criaturas dos pacotes e obrigou a existir o
`EnemyData.portraitScale`.

O `Tools/guild_art.py` não serve aqui: ele recorta retângulo sobre fundo opaco. Figura precisa de
alpha, e isso é ferramenta nova.

**O retrato é recorte da mesma peça: cabeça e ombros, não só a cabeça.** Ele aparece a 260 px na
bancada da Forja e a ~180 px no card de combate; uma cabeça sozinha, tirada de uma figura de 1200 px
de altura, chega com ~140 px. A régua é olhar o recorte a 260 px antes de aceitá-lo.
