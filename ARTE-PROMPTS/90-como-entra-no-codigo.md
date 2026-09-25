# Como cada bloco entra no código

## A guilda — nenhuma linha nova

O `GuildArt` mapeia sala → arquivo (`Construcoes`) e veste a cena pelo menu
`Tools ▸ Guild of Legends ▸ Vestir a Guilda`, ou pelo gatilho `RunGuildArt.trigger`. Trocar os
arquivos em `Assets/Art/Guild/` e rodar de novo troca a arte.

| Peça | Onde entra |
|---|---|
| `patio.png` | fundo do `GuildMap`, num filho próprio atrás de todas as portas |
| `sala_<nome>.png` | o recorte dentro de cada porta, no filho `Cena` |
| `terreno_<nome>.png` | o mesmo lugar, trocado em runtime pelo `GuildGuide` enquanto a sala não foi erguida |
| `sombra_porta.png` | máscara de bordas derretidas com que o guia apaga a porta sem motivo |
| aceso (quando existir) | uma entrada a mais por sala na mesma tabela — o `GuildGuide` já sabe quando vale |

**O que não fazer:** escurecer o recorte para dizer que a porta está apagada. Sobre uma pintura
contínua, brilho diferente desenha um retângulo — a porta "acesa" a 82% virava uma mancha e a
apagada a 30% virava um quadrado preto. Desde 25/09 quem escurece é a sombra, que some nas bordas.

---

## Os heróis — aqui há código a escrever

Ao contrário da guilda, e ao contrário do que o `ARTE.md` prometia para a lista inteira. A promessa
de "trocar o arquivo troca a arte" valia para catálogos que apontam para imagem; os heróis não são
imagem hoje, são boneco animado do SPUM.

| Arquivo | O que muda |
|---|---|
| `TrailCast` / `TrailStage` | montam o prefab do SPUM e o fazem andar. Passam a receber um `Sprite` por herói; o passo vira movimento e balanço de transform |
| `BattleStage` | pede `ATTACK`, `DAMAGED` e a morte ao Animator. Passa a trocar entre as duas poses, como já faz com as criaturas |
| `ForgeArt` | hoje escolhe a peça de arma do pacote por nível. Passa a escolher o **grau da figura** — e o caso "mago e curandeiro têm um desenho só" deixa de existir |
| `PortraitCatalog` | sorteia entre ~500 rostos de pacote. Passa a ter 12 entradas, e o sorteio vira "qual dos dois corpos da classe" |
| `HeroData.portrait` | continua `Sprite`. Esta parte não muda |
| ferramenta nova | o `Tools/guild_art.py` recorta retângulo sobre fundo opaco; figura precisa de recorte com alpha e caixa apertada |

**Uma peça que sai de graça:** a Torre usa **os heróis do jogador** como inimigos, e o Campeão
Corrompido do fim do jogo é montado com o que o herói era. As duas poses de cada figura servem aos
dois sem geração nova — o que falta ali é o tratamento de corrupção por cima, que é o bloco 10.
