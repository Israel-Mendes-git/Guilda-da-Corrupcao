# Prompts de arte — índice

> Um arquivo por **pedido**: o prompt vem pronto para colar, com o bloco de estilo já dentro, e ao
> lado dele o que conferir antes de aceitar a saída. Escritos contra a lista do [`ARTE.md`](ARTE.md).
> Os prompts estão em inglês porque os geradores respondem melhor assim; o texto em volta é para
> você.
>
> Estilo escolhido em 25/09/2026: **pintura digital sombria com traço grosso** — em
> [`00-estilo.md`](ARTE-PROMPTS/00-estilo.md), que vale para todos os blocos.

## Os pedidos

| Pedido | Entrega | Estado |
|---|---|---|
| [Pátio da guilda](ARTE-PROMPTS/guilda-1-patio.md) | 1 pintura 21:9 | ✅ gerado, recortado e no jogo |
| [Terrenos vazios](ARTE-PROMPTS/guilda-4-terrenos.md) | 5 edições | ⚠️ os 5 no jogo; **Forja e Sala de Mapas a regerar** (céu inventado) |
| [Portas que falam](ARTE-PROMPTS/guilda-3-acesos.md) | 6 edições | ⬜ escrito, nada gerado |
| [Refinar uma construção](ARTE-PROMPTS/guilda-2-refinos.md) | 1 edição por construção fraca | ⬜ só se precisar |
| [Folha de elenco A](ARTE-PROMPTS/herois-1-folha-a.md) | 1 folha, 6 figuras | ⚠️ 1ª saída em 25/09 — regerar por resolução |
| [Folha de elenco B](ARTE-PROMPTS/herois-2-folha-b.md) | 1 folha, 6 figuras | ⬜ depende da A |
| [Pose do golpe](ARTE-PROMPTS/herois-3-golpe.md) | 12 edições | ⬜ depende das duas folhas |
| [Graus de equipamento](ARTE-PROMPTS/herois-4-equipamento.md) | 48 edições | ⬜ última leva do bloco |

[Como cada bloco entra no código](ARTE-PROMPTS/90-como-entra-no-codigo.md) — o que é troca de
arquivo e o que é código a escrever.

## A ordem

1. **Regerar os dois terrenos** (Forja e Sala de Mapas). São defeito visível na tela de hoje.
2. **Folha A em resolução cheia.** Com ela, quatro telas trocam de arte de uma vez: retrato, ficha,
   taverna e o herói parado no palco. **Pare aqui e veja na tela** antes de seguir.
3. Folha B, e o elenco fecha em 12.
4. Os seis acesos da guilda, ou as 12 poses de golpe — o que estiver mais perto de ser jogado.
5. Os graus de equipamento, que são metade do volume do bloco de heróis.

## As decisões que estes pedidos carregam

**Da guilda (25/09):** as sete portas são recortes de uma pintura só, e não sete ilustrações. Os
estados são edições dessa pintura, e só o retângulo da construção afetada entra no jogo.

**Dos heróis (25/09):**

| Decisão | Escolha | O que ela cobra |
|---|---|---|
| Onde o corpo pintado entra | substitui os bonecos do SPUM em tudo | duas poses por corpo; a caminhada da estrada acaba e vira balanço por código |
| Corpos por classe | 2 | 12 heróis. O terceiro entra depois sobre a mesma base, se a taverna parecer repetitiva |
| Equipamento | versões do mesmo corpo, por edição | as 36 peças de encaixe do `ARTE.md` somem; entram 3 graus |
| Classes | as 6 | Ladino e Bardo desenhados. Desenhar não os torna jogáveis — isso continua custando +20 cartas |

**A conta do bloco de heróis:** 12 corpos × 2 poses × 3 graus = **72 peças**. O `ARTE.md` previa
48–54 porque contava o corpo inteiro como peça única e não previa a pose do golpe.

## O que continua em aberto

- **Se a Sala de Mapas sobrevive** ao mapa navegável, ou se as duas telas viram uma. Se virarem, são
  uma construção, um terreno e uma porta-que-fala a menos — e as três já estariam feitas.
- **Ladino e Bardo jogáveis**, que é decisão de escopo de carta, não de arte.
