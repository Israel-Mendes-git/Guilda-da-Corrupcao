# Pedido: os graus de equipamento

**Entrega:** 48 edições — 12 figuras × 2 poses × 2 graus acima do inicial.
**Destrava:** a Forja. Pagar passa a mudar a figura, e não só o número na ficha.
**Depende de:** as duas poses de cada figura aprovadas.
**Estado:** não geradas. É a última leva do bloco — a mais cara e a que pode esperar mais.

---

## Por que três graus, e não dezesseis

O código guarda `weaponLevel` e `armorLevel` de 0 a 3, **independentes**: 16 combinações por herói.
A régua abaixo é **minha, e vetável** — soma os dois níveis e lê o resultado em três faixas:

| Grau na figura | Soma dos dois níveis | O que o jogador vê |
|---|---|---|
| **Recruta** | 0–1 | o que ele vestia quando foi contratado: remendo, couro velho, arma sem valor. É a figura das folhas A e B, já pronta |
| **Equipado** | 2–4 | arma de ferro decente, peitoral ou gibão reforçado, botas inteiras |
| **Veterano** | 5–6 | arma de qualidade, proteção completa, capa, e as marcas de uso que o nível 3 justifica |

Dois graus em vez de três sairia por 24 edições em vez de 48, ao preço de o primeiro upgrade não
mudar nada na tela.

---

## O prefixo

**Anexe a figura do grau anterior** — o equipado sai do recruta, o veterano sai do equipado. Nunca
pule um degrau: duas edições em cima da mesma base voltam com dois equipamentos que não conversam.

```
Using the attached figure, keep the exact same person, the same pose, the same framing, the same
scale, the same ground line, the same palette, brush style, outline weight, light and flat grey
background. The face, the body, the stance and the overall silhouette of the pose must not change.

Change only the gear described below. This is the same person better paid, not a different person.
No cast shadow, no scenery, no text.
```

## O que muda em cada classe

| Classe | Equipado | Veterano |
|---|---|---|
| **Guerreiro** | `Full chainmail with no mending, a plain iron helm, an intact round shield, a straight well-kept sword with a leather-bound grip.` | `Riveted plate over the mail at chest and shoulders, a heavy cloak pinned at one shoulder, a longer sword, the shield painted with a faded mark.` |
| **Mago** | `A thicker wool robe with a leather over-tunic, a staff shod with an iron cap, a satchel of scrolls at the hip, proper boots instead of rags.` | `A long layered coat of heavy cloth, a staff carved along its whole length, several books chained to the belt, a fur-lined collar.` |
| **Curandeiro** | `A padded gambeson under the linen, a steel-headed mace, a larger worked symbol at the chest, a full field satchel.` | `A mail shirt under a surcoat, a hooded cloak, a heavy flanged mace, the chest symbol now large and worked in pale metal.` |
| **Ladino** | `Studded leather, bracers, a bandolier of throwing knives across the chest, two matched daggers instead of mismatched ones.` | `Blackened brigandine, a hooded half-cloak, a short sword at one hip and a dagger at the other, a belt of small tools and pouches.` |
| **Bardo** | `Clean layered clothes in the same limited palette, a leather shoulder guard, a better lute with an intact soundboard, a short sword at the belt.` | `A long coat with worked cuffs, a fur mantle, a fine dark-wood lute inlaid along the neck, a hat with a full feather.` |
| **Caçador** | `A leather chest guard over the cloak, a proper quiver with matched arrows, a stronger recurve bow, a hunting knife at the belt.` | `A layered leather and fur coat, a long bow, a full quiver on the back and a second at the hip, a tooth or claw trophy hanging at the chest.` |

---

## O que conferir antes de aceitar

| Conferir | Por quê |
|---|---|
| O rosto e a silhueta não mudaram | veterano mais alto ou mais largo lê como substituição do herói, não como compra |
| A pose é a mesma | é a mesma imagem trocada em tempo de execução: pose diferente faz o herói se mexer ao comprar |
| A arma continua da mesma família | varinha que vira tridente confunde mais do que informa — o `ForgeArt` já tinha registrado isso quando as peças vinham de pacote |
| O grau se lê a 200 px | se a diferença entre equipado e veterano só aparece de perto, a compra não deu retorno visível |

---

## Ordem, se o orçamento apertar

1. **Equipado das duas poses dos 12** (24 peças) — já dá retorno à primeira compra.
2. **Veterano das duas poses** (24) — fecha a escada.

Os dois graus do **corpo 1** de cada classe antes de qualquer grau do corpo 2: o corpo 2 é variedade
de taverna, e variedade de taverna não passa pela Forja no primeiro contato com o jogo.
