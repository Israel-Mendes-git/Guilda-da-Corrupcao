# Arte — o que falta produzir

> Levantamento de 09/09/2026, feito sobre o conteúdo que existe no projeto hoje.
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
| Cartas | 45 | 40 ícones, 4 molduras de raridade, 1 verso |
| Guilda | 8 | 1 fachada viva + 7 interiores |
| Combate | 29 | 11 inimigos × 2 poses + 7 fundos de bioma |
| Heróis | 12–18 | 6 classes × 2–3 concepts; o retrato sai de recorte |
| Ícones | ~49 | 15 efeitos, 6 recursos, 6 classes, 12 traços, 10 itens |
| Eventos | 25 | ilustração por evento |
| Mapa | 15 + trilha | 1 mapa-mãe, 7 regiões, 7 tipos de ponto |
| VFX | 15 | um por efeito de combate |
| UI | kit | molduras, botões, barras |
| Corrupção | 2 | medidor + tratamento de tela |
| Equipamento | a definir | armas e armaduras da Forja |

A ordem dos blocos abaixo é por **tempo que a peça passa na tela**, não por dificuldade.

---

## 1. Cartas

O objeto mais visto do jogo: a mão fica aberta durante todo o combate e toda a jornada.

| Peça | Quantidade | Hoje |
|---|---|---|
| Ilustração de carta | **40** — 10 × Warrior, Mage, Healer, Hunter | ícone de habilidade do pacote Blink, 256×256 |
| Moldura por raridade | **4** — Common, Rare, Epic, Legendary | moldura única do kit de UI, cor trocada na borda |
| Verso | **1** | não existe; baralho e descarte são retângulos |

A carta montada tem cinco elementos: fundo, moldura, ilustração, nome, descrição e custo. Custo é
número sobre a arte, então a ilustração precisa de área calma no canto superior.

**Ladino e Bardo não têm carta nenhuma.** As duas classes existem em `HeroClass`, têm nome
traduzido em seis telas e já têm arma na Forja — mas `HeroFactory.classesJogaveis` sorteia só
Guerreiro, Mago, Curandeiro e Caçador. A taverna nunca oferece um Ladino ou um Bardo, e o motivo
é o baralho: sem cartas da classe, o herói entraria sem ter o que jogar. O comentário no código
diz isso literalmente — *"quando as cartas das duas classes existirem, é aqui que elas voltam"*.

Para a arte isso separa em dois: **o concept das duas classes entra normalmente** (é desenho, não
depende de sistema), mas **torná-las jogáveis custa +20 ilustrações de carta**, 10 de cada.

## 2. Guilda

| Peça | Quantidade | Hoje |
|---|---|---|
| Fachada da guilda viva | **1** | seis portas retangulares sobre mármore escuro |
| Interiores | **7** — Taverna, Biblioteca, Forja, Mercado, Cemitério, Sala de Mapas, Jornada | cenas emprestadas de um pacote de cavernas, com véu escuro por cima para o nome ser legível |

Os sete precisam ler como **o mesmo lugar** visto por sete portas. O erro já cometido com
placeholder foi escolher por assunto: reduzidas ao tamanho de uma porta, paisagens abertas viram
manchas coloridas. Interior com foco de luz e teto visível funcionou; paisagem, não.

O véu escuro existe só porque a arte emprestada é clara demais sob o texto. Arte própria pode
resolver isso na própria pintura e dispensar o véu.

## 3. Combate

| Peça | Quantidade | Hoje |
|---|---|---|
| Inimigos — pose 3/4 | **11** — 6 comuns, 5 chefes | pixel art de 7 criaturas para 11 inimigos: quatro repetem arte separada por cor e tamanho, e três (lobo, aranha, golem) usam criatura que não os representa |
| Inimigos — pose atacando ou usando magia | **11** | não existe: o inimigo golpeia sem mudar de desenho |
| Fundo de bioma | **7** — Floresta, Montanha, Pântano, Deserto, Tundra, Vulcão, Ruínas | um pacote diferente por bioma, sem paleta comum |

**A Sanguessuga Gigante está sem retrato nenhum** — é uma caixa vazia no combate hoje.

São **duas poses por inimigo**, não animação: parada em 3/4 e o momento do golpe. O código troca
sprite sem Animator, então duas imagens bastam para o ataque deixar de ser só um número subindo.
Animação de dano e de morte fica fora desta lista.

## 4. Heróis

| Peça | Quantidade | Hoje |
|---|---|---|
| Concept de classe, 3/4 | **12–18** — 6 classes × 2–3 | não existe |

**O retrato não é peça separada: é recorte do concept.** Isso fecha a única dívida de arte que o
herói tinha e dispensa o lote de retratos.

A consequência é de escopo, não de arte: herói é gerado pela `HeroFactory` e o retrato é sorteado.
Hoje o sorteio corre sobre ~500 retratos de um pacote; com recorte de concept, a variedade passa a
ser o número de concepts — 2 ou 3 por classe. A taverna vai repetir cara, e é isso que decide se
são 2 ou 3 concepts por classe.

## 5. Ícones

Tudo aqui é **emoji ou texto** hoje. É a lacuna mais barata de fechar e a que mais suja a tela.

| Conjunto | Quantidade | Exemplo do que está lá |
|---|---|---|
| Efeitos de combate | **15** | veneno, bloqueio, buff, evasão, quebra de escudo — todos anunciados por número e texto |
| Recursos | **6** | `💰` ouro, comida, memória, dia, XP, corrupção |
| Classes | **6** | `🗡️ Ladino`, `🔮 Mago` |
| Personalidade e traço | **12** | `🦁 Corajoso`, `🤝 Leal`, `🪨 Teimoso` |
| Relíquias e poções | **10** | únicos com ícone de verdade — 6 relíquias, 4 poções, em `Resources/ItemIcons/` |

Os 10 de item já mostram o contrato: **um arquivo por id**, nome do arquivo igual ao id. Arte nova
entra sobrescrevendo, sem tocar em código.

## 6. Eventos

**25 ilustrações**, uma por evento. Todas preenchidas com cena emprestada do mesmo pacote de
cavernas que veste a guilda — o que significa que evento e sala se parecem demais entre si.

## 7. Mapa

| Peça | Quantidade | Hoje |
|---|---|---|
| Mapa da região | **1** — papel, borda, bússola, marca da guilda | pacote de cartografia lite, preto sobre transparente |
| Marca de região | **7** | Vulcão e Tundra são empréstimos declarados: um pico e uma conífera |
| Ponto de rota | **7** — combate, tesouro, perigo, descanso, mercador, história, chefe | ícones do mesmo pacote |
| Trilha ilustrada da jornada | a definir | diagrama de nós |

A trilha é a peça que ainda não tem forma: a direção definida é **mapa ilustrado com trilha que se
revela conforme o grupo avança**, com os heróis atravessando a pé em fila. Isso troca o diagrama de
nós atual e precisa de desenho antes de virar arte.

## 8. VFX de combate

**15 efeitos**, um por `CombatEffectType`. Hoje são partículas 3D de três pacotes diferentes,
convertidas para URP, mais três PNGs soltos. O combate acontece em Canvas, então **sprite sheet é o
formato que funciona ali** sem câmera nem sistema de partículas.

Cada efeito tem duas formas: o **disparo** (o golpe, a cura) e o **estado enquanto dura** (veneno
ativo, buff ativo). São necessidades diferentes do combate, não variação estética.

## 9. UI

Molduras de painel, três popups, botões com os seis estados — incluindo **Disable**, que a opção
travada de evento (`🔒 Precisa de uma carta capaz de…`) precisa para parecer travada —, toggles,
e barras de HP, estresse e energia. A energia é segmentada: são 5 pontos, não uma barra contínua.

## 10. Corrupção

Não existe arte nenhuma. A corrupção é um número percentual em texto na pausa e no menu, e é o
tema que dá nome ao jogo.

Duas peças: o **medidor** e o **tratamento de tela** — como a corrupção aparece no mapa, na região
tomada e no herói afetado.

## 11. Equipamento

Armas e armaduras da Forja, três níveis. Hoje vêm de um pacote de partes de personagem, onde
guerreiro e ladino têm quatro desenhos, caçador tem três, e mago e curandeiro têm **um só** — nesses
a peça repete e só o número muda. A quantidade própria depende de quantos níveis mostram diferença.

---

## Decisões que a lista espera

- **Estilo.** A direção provável é tudo pintado. Se for, os 11 inimigos em pixel art saem, e o
  bloco 3 passa a ser produção nova em vez de substituição.
- **Ladino e Bardo jogáveis.** O concept das duas entra de qualquer forma; torná-las recrutáveis
  custa +20 ilustrações de carta e é decisão de escopo, não de arte.
- **2 ou 3 concepts por classe.** Como o retrato é recorte do concept, esse número é a variedade
  de caras que a taverna terá.
