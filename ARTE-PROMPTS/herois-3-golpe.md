# Pedido: a pose do golpe

**Entrega:** 12 edições — uma por figura já recortada (6 classes × 2 corpos).
**Destrava:** o combate. O palco troca entre duas imagens por herói, como já faz com as criaturas.
**Depende de:** as folhas A e B recortadas e aprovadas.
**Estado:** não geradas.

---

## Por que duas poses, e não animação

O `BattleStage` hoje pede `ATTACK` ao Animator do boneco do SPUM. Com figura pintada não há rig: o
palco passa a trocar de sprite, que é exatamente o que já faz com as criaturas dos pacotes. Duas
imagens bastam — **parado em três quartos** e **o momento do golpe**. Dano e morte ficam de fora: o
motor resolve com tremor e transparência.

---

## O prefixo

**Anexe a figura já recortada e aprovada** — uma de cada vez, nunca a folha inteira.

```
Using the attached figure, keep the exact same person: same face, same age, same build, same hair,
same clothing, same equipment, same colours, same palette, same brush style, same heavy ink outline,
same even light from the upper left, same flat uniform mid-grey background, same scale and same
framing. The figure must stay the same height in the frame and stand on the same ground line.

Redraw only the pose, as described below. This must read as the same character one second later,
not as a similar character. No cast shadow, no contact shadow, no scenery, no text.
```

**Repita a descrição física da figura no prompt**, junto da pose. É a armadilha deste pedido: o
modelo troca a cara entre as duas poses, e não há recorte que conserte — no bloco da guilda o
problema era a pintura inteira mudar, e a saída era recortar; aqui o que muda é a pessoa.

## As seis poses

| Classe | Pose |
|---|---|
| **Guerreiro** | `Mid-swing: the sword coming down in a heavy overhead cut, both feet planted wide, weight thrown onto the front foot, shield arm braced forward across the body, head down behind the shoulder.` |
| **Mago** | `Mid-cast: the staff raised high in one hand, the other hand thrown forward with fingers open, the robe swinging back with the movement. Keep any glow small and contained at the fingertips — no beam, no fireball, no explosion, no large spell effect of any kind.` |
| **Curandeiro** | `Mid-action: the mace swung low across the body in one hand, the free hand raised open at shoulder height with the palm forward, the body turned into the movement.` |
| **Ladino** | `Mid-lunge: low and forward, both daggers drawn, the leading blade fully extended at waist height, the trailing arm back for balance, the hood thrown off the head by the movement.` |
| **Bardo** | `Mid-strum: leaning back with the lute swung round to the front and held across the chest, the strumming hand striking down hard across the strings, the mouth open mid-shout.` |
| **Caçador** | `Mid-shot: the short bow drawn to full, the arrow nocked at the cheek, both arms level, the body turned side-on to the viewer, the front foot advanced.` |

O Mago leva a frase do brilho contido porque o VFX é peça própria, desenhada por cima em sprite
sheet. Magia pintada na figura brigaria com ela em toda carta lançada.

---

## O que conferir antes de aceitar

| Conferir | Por quê |
|---|---|
| É a mesma pessoa | ponha as duas lado a lado e olhe só o rosto e o cabelo |
| A roupa e a arma são as mesmas | arma que muda de desenho entre as poses parece troca de herói |
| A figura não cresceu nem encolheu | o palco encaixa as duas no mesmo retângulo: tamanho diferente faz o herói pular ao atacar |
| Os pés continuam na mesma linha | herói que sobe ao atacar descola do chão do combate |
| A silhueta do golpe se lê a 200 px | tape o detalhe: se a pose não se lê como golpe em miniatura, o combate não vai mostrá-la |
