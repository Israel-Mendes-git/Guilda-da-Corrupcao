# Assets importados — o que é cada um e onde entra

> Inventário de 15/08/2026, feito arquivo a arquivo sobre os pacotes importados da Unity Asset Store.
> São **447 arquivos, ~489 MB** em 6 pacotes novos, mais a atualização do `Bloodlines UI`.
> Este documento diz **onde cada coisa se pluga** no projeto — e o que não vamos usar.

A infraestrutura de arte **já existe no código e está inteiramente vazia**. Não é preciso escrever
campo novo para nada abaixo:

| Campo | Onde | Estado |
|---|---|---|
| `CardData.cardImage` | as 17 cartas | ✅ preenchidos (`CardArt.cs`) |
| `EnemyData.portrait` | os 11 inimigos | ✅ preenchidos (`EnemyArt.cs`) — ver §Inimigos |
| `EventData.eventImage` | os 25 eventos | **25 de 25 vazios** |
| `HeroData.portrait` | heróis gerados pela `HeroFactory` | vazio |
| `QuestData.biomeIcon` | missões | vazio — é o `opcionais vazios: biomeIcon` que aparece em toda run de Play Mode |

---

## 1. Interface e HUD — `Alebardium/Bloodlines UI`

O kit foi **atualizado** (ganhou `asmdef` próprio, `Alebardium.BloodlinesUI`, e a moldura
`Frame_outline_v2`). É o pacote mais importante dos seis, porque é o único que já define o estilo da
cena — tudo aqui entra por `GuildSceneSetup`, não à mão no Inspector.

### Barras: 11 sliders prontos, e a dívida que eles pagam

O `ROADMAP.md` lista "HP e estresse ainda são retângulos chapados" como dívida. O material para
pagá-la está no disco desde sempre:

| Prefab | Para quê |
|---|---|
| `Slider (Horizontal) 1–5` | **HP** — na ficha do herói, no status da party durante a jornada e nas linhas do `JourneyResultUI` |
| `Slider (Round) 1–2 (With Runes)` | **Estresse** 0–100 — a variante com runas conversa com o tema da Corrupção |
| `Slider (Segments) 1–4` | **Energia de combate** — são 5 pontos de energia, e a barra é segmentada por natureza |
| `Health_diamond_full/empty`, `Health_rod_full/empty` | texturas alternativas de vida (losango e bastão) |
| `Progress_Bar_Rectangle v1–v5`, `Progress_Bar_Round v1–v4` | texturas cruas, se quisermos montar barra própria |

`SliderTextSynchronizer.cs` (vem no kit) escreve o número dentro da barra — é o que dá `30/42` em vez
de só uma barra preenchida.

### Molduras e botões

| Textura / Prefab | Onde |
|---|---|
| `Frame_notice_v1`, `Frame_notice_v2` | os três popups do `UIManager`: Mensagem, Confirmação, Resultado |
| `Frame_outline_red`, `Frame_main_menu_red` | estados de perigo — herói na **Beira da Morte**, herói **esgotado** que recusa partir |
| `Frame_background`, `Frame_outline`, `Frame_outline_v2` | painéis em geral (já em uso) |
| `Frame_main_menu` | tela de título, quando existir (Fase 5) |
| `Button 1/2/3 (Gray)` | ações neutras: fechar, voltar, navegar |
| `Button 1/2/3 (Red)` | ações de risco: **⚔️ Enfrentar em combate**, **abortar jornada** |
| `Toggle 1–3 (Round/Square)`, `Icon-Toggle 1–4` | marcar herói na formação; marcar carta no deck |

Cada botão traz seis estados, **incluindo `Disable`** — é o que a opção travada de evento
(`🔒 Precisa de uma carta capaz de…`) precisa para parecer travada em vez de só mudar de cor, que é
como ela está hoje (`JourneyManager.CreateChoiceButton`).

### Scripts do kit que valem adotar

| Script | Por quê |
|---|---|
| `SoundManager.cs` | **o projeto não tem nenhum sistema de áudio.** É a base pronta |
| `AudioMixer.mixer` | canais separados de música e efeito — necessário para volume por categoria |
| `ButtonSFX.cs`, `ToggleSFX.cs` | som em botão sem precisar ligar nada por código |
| `Click Button SFX.wav`, `Hover Button SFX.wav` | os sons de UI |
| `SliderTextSynchronizer.cs` | número dentro da barra |
| `LoadingSceneManager.cs`, `LocalSceneManager.cs` | troca de cena — só serve quando houver menu principal (Fase 5) |

`InputModuleBootstrap.cs` e `FastDontDestroyOnLoad.cs` são infraestrutura do kit; não mexer.

---

## 2. Cartas — `Blink` (50 ícones, 256×256)

O achado mais valioso depois da UI. São ícones de **habilidade** pintados à mão — um objeto ou
símbolo sobre fundo colorido por tema —, organizados em 5 arquétipos × 5 classes × 2 ícones.
Não são retratos (ver §6).

Os arquétipos batem quase um a um com o `HeroClass` do jogo:

| Pasta do pacote | Classe do jogo |
|---|---|
| `Warrior/` — Barbarian, Berserker, Deathknight, Dragonknight, Guardian | **Warrior** |
| `Elementalist/` — Arcanist, Cryomancer, Electromancer, Geomancer, Pyromancer | **Mage** |
| `HolyDarkness/` — Priest, Paladin, Medium, Cultist, Necromancer | **Healer** (e a corrupção) |
| `Assassin/` — Hunter, Ranger, DemonHunter, Brawler, Rogue | **Hunter** e **Rogue** |
| `Symbiose/` — Druid, Shaman, Beastmaster, Enchanter, Shapeshifter | **Bard** e Healer |

Cobre inclusive **Rogue e Bard**, que existem em `HeroClass` e nunca foram implementadas — a arte
das cartas delas deixa de ser um impedimento para a Fase 5.

### Sugestão de ícone por carta

São 17 cartas e 50 ícones: dá para escolher pelo desenho, não só pela classe. Os marcados com ★ eu
abri e conferi — o desenho corresponde literalmente ao efeito.

| Carta | Classe | Efeito | Ícone sugerido |
|---|---|---|---|
| Postura Defensiva | Warrior | Block | ★ `Warrior/Guardian/Guardian8` — escudo de madeira rachado |
| Brado de Guerra | Warrior | BlockAll / Intimidate | `Warrior/Barbarian/Barbarian14` |
| Corte Duplo | Warrior | DamageAll | `Warrior/Berserker/Berserker10` |
| Fúria | Warrior | Buff | `Warrior/Berserker/Berserker13` |
| Investida | Warrior | Damage / Teleport | `Warrior/Dragonknight/Dragonknight4` |
| Bola de Fogo | Mage | DamageAll | ★ `Elementalist/Pyromancer/Pyromancer2` — mão em chamas |
| Escudo de Gelo | Mage | BlockAll | `Elementalist/Cryomancer/Cryomancer4` |
| Explosão Arcana | Mage | DamageAll | `Elementalist/Arcanist/Arcanist1` |
| Teleporte | Mage | Evade / SkipDay | `Elementalist/Electromancer/Electromancer3` |
| Bênção | Healer | HealAll | ★ `HolyDarkness/Priest/Priest8` — cajado sagrado dourado |
| Toque Curativo | Healer | Heal | `HolyDarkness/Priest/Priest13` |
| Purificação | Healer | Cleanse / Purify | `HolyDarkness/Paladin/Paladin10` |
| Ressurgir | Healer | Heal / Revive | `HolyDarkness/Medium/Medium15` |
| Flecha Precisa | Hunter | Damage | ★ `Assassin/Hunter/Hunter1` — arco e flecha |
| Flecha Lunar | Hunter | ShieldBreak | `Assassin/Ranger/Ranger2` |
| Armadilha | Hunter | Damage / GainFood | `Symbiose/Beastmaster/Beastmaster4` |
| Olhar de Águia | Hunter | BuffNextCard | `Assassin/DemonHunter/DemonHunter8` |

`Necromancer3` (esqueleto em verde-tóxico) fica de fora das cartas de propósito: é o melhor candidato
a **ícone da Corrupção** na Fase 3, quando a corrupção global ganhar medidor.

---

## 3. Combate — efeitos visuais

O combate calcula veneno, enfraquecimento, bloqueio e ímpeto desde a Fase 2.5, e **tudo isso ainda
se anuncia só por texto e número**. Estes três pacotes fecham exatamente esse buraco.

### `Travis Game Assets` — o que casa melhor com o código

| Prefab | `CombatEffectType` correspondente |
|---|---|
| `StatusAilment_01` + `_Aura` | **Poison** — as pilhas que cobram no fim da rodada |
| `Debuff_03` + `_Aura` | **Debuff** — o −40% de ataque |
| `Buff_01a` / `Buff_02a` / `Buff_03a` + `_Aura` | **Buff** (ímpeto do grupo), **BuffNextCard** (carta reforçada), **Evade** |
| `Guard_01` | **Block** / **BlockAll** |
| `Hit_01` … `Hit_04` | **Damage** / **DamageAll** |

As variantes `_Aura` são contínuas: servem para o estado **enquanto dura** (veneno ativo, buff ativo),
enquanto as sem aura são o disparo do momento. É a distinção que o `CombatManager` já faz entre
aplicar o efeito e mantê-lo por turnos.

### `Matthew Guz` — impacto por elemento

| Prefab | Uso |
|---|---|
| `Basic Hit`, `Basic Hit 2 / 7 / 8` | dano físico — Warrior e Hunter |
| `Fire Hit` | Bola de Fogo; bioma Vulcão |
| `Ice Hit` | Escudo de Gelo; bioma Tundra |
| `Lightning Hit Blue` | dano elétrico |
| `Magic Hit 2` | Explosão Arcana |
| `Love Hit` | **Heal / HealAll** (é um coração) |
| `Shadow Hit` | **a Corrupção e os chefes** — o único de tema sombrio do lote |

### `Inguz Media Studio` — o único 2D de verdade

`Impact01`, `Impact02`, `Impact03` são **sprite sheets**, não partículas 3D. Isso importa: o combate
do jogo acontece em Canvas, e sprite sheet anima ali sem câmera nem sistema de partículas. Vêm com os
`.psd` editáveis, o que permite recolorir para o verde-doentio da Corrupção.

---

## 4. Áudio — `Horror Starter Pack` + kit da UI

Doze faixas, e o GDD §13.2 pede música por contexto. Proposta de distribuição (durações estimadas
pelo tamanho do arquivo):

| Contexto | Faixa | Duração |
|---|---|---|
| Hub da guilda | `sp-theroom`, `sp-tenent` | 3:24 / 3:29 |
| Jornada na estrada | `sp-frontier`, `sp-underthestairs` | 3:29 / 2:53 |
| Tensão / evento ruim | `sp-lost`, `sp-dontlookback` | 1:58 / 1:22 |
| **Combate** | `sp-horroraction` | 0:57 |
| **Chefe** | `sp-conjuring`, `sp-re8` | 0:57 / 0:59 |
| Morte de herói (stinger) | `sp-horrormane`, `sp-getout` — cortar os primeiros segundos | 0:50 / 1:13 |
| Menu / título (Fase 5) | `sp-typewriter` | 5:14 |

O stinger de morte é o que mais paga por si: é o momento em que a permadeath precisa doer, e hoje ele
acontece em silêncio absoluto.

---

## 5. Cenário — `Pixel Fantasy Caves`

Apesar do nome, não é pixel art chapada: são camadas de rocha escura e dessaturada, com fundo
transparente, prontas para parallax. Combina com a direção melhor do que o nome sugere.

- `background1` … `background4b` — camadas de profundidade
- `mainlev_build`, `props1`, `props2` — formações e detalhes

Serve como fundo de jornada em **Ruínas** e **Montanha**, e para os nós de caverna do mapa. Junto com
o `FREE Parallax Forest Background HQ` que já estava no projeto (Floresta), cobre 3 dos 7 biomas.

---

## 6. O que continua faltando

Nada do que foi importado resolve:

- **Retratos de herói** (`HeroData.portrait`). Os ícones da Blink são objetos e símbolos, não rostos.
  Dá para usar o ícone da classe como identidade visual do herói — é melhor que a cor sólida de hoje —
  mas não é retrato, e a ficha do herói vai continuar sem cara.
- ~~**Retratos de inimigo** (`EnemyData.portrait`, 11 vazios). Nenhum dos pacotes traz criatura 2D.~~
  **Corrigido em 15/08: isto estava errado.** Há criatura 2D em dois pacotes — `Monsters Creatures
  Fantasy` (esqueleto, goblin, cogumelo, olho voador, todos em pixel art com Idle/Attack/Hurt/Death
  já fatiados) e `War/Slime Enemy` (três cores). Os 11 inimigos foram preenchidos a partir daí pelo
  `EnemyArt.cs`. Ver a tabela abaixo — e a lição: **o inventário disse "não existe" sobre um pacote
  que estava no disco**, e por isso o combate ficou meses com caixas vazias no lugar dos monstros.
- **Ilustração de evento** (`EventData.eventImage`, 25 vazios).
- **Biomas restantes**: Pântano, Deserto, Tundra e Vulcão seguem sem fundo próprio.

---

## 7. Antes de usar: dois ajustes obrigatórios

### Materiais em URP

Os pacotes de efeito trouxeram **93 materiais e nenhum shader próprio** — usam shaders do Built-in,
que não existem em URP, e por isso aparecem rosa. O Travis **já traz a solução dentro do próprio
pacote**, e é só importar:

```
Assets/Travis Game Assets/Hit Impact Effects/Pipeline Upgrade/Hit Impacts Effects FREE - URP.unitypackage
Assets/Travis Game Assets/Status Effects/Pipeline Upgrade/Status Effects FREE - URP.unitypackage
```

Para os do Matthew Guz e do Inguz, que não trazem versão URP, o caminho é
`Window ▸ Rendering ▸ Render Pipeline Converter ▸ Built-in to URP`.

### Import settings do áudio

271 MB em AIFF/WAV **sem compressão**. Do jeito que está, isso vai inteiro para o build. As faixas de
música devem ficar em `Vorbis` + `Streaming`; só os SFX curtos (clique, hover) ficam em
`Decompress on Load`.

---

## 8. Peso morto — ~204 MB que dá para remover

Nenhum destes é usado pelo jogo. **Todos os 7 scripts** que os pacotes trouxeram estão em pastas de
demonstração — nenhum é necessário para usar os efeitos, e todos caem no `Assembly-CSharp` junto com
o código do jogo.

| Caminho | Peso | O que é |
|---|---|---|
| `Esper/ESave/Examples/` | **119,9 MB** | exemplos do ESave, dos quais 119,8 MB são um único `URP_Example.unitypackage` |
| `Inguz Media Studio/…/Plastic006_4K_*` (4 texturas) | **68,4 MB** | textura de plástico 4K da cena de demonstração |
| `Travis Game Assets/Status Effects/Demo Scene/` | 9,8 MB | esqueleto de aldeão e animações de demo |
| `Inguz Media Studio/…/Demo Scene/` | 2,9 MB | cena de demo |
| `Wallpaper.png`, `mask2.png`, `instructions.psd` | 2,8 MB | material de demo do Inguz |
| `Matthew Guz/…/Scene Demo/`, `Travis/Hit Impact Effects/Demo Scene/`, `Blink/…/Demo/` | 0,3 MB | cenas de demo |

**Não remover** os dois `.unitypackage` em `Pipeline Upgrade/` — são a conversão para URP (§7).

---

## 9. Ordem sugerida

1. **Importar os dois `.unitypackage` de URP do Travis** e rodar o conversor para o resto. Sem isso,
   qualquer efeito que a gente ligar aparece rosa.
2. **Ligar as barras de progresso** — HP, estresse e energia. Paga a dívida mais antiga do ROADMAP e
   não depende de mais nada.
3. **Preencher `cardImage` nas 17 cartas** pela tabela do §2. É a mudança de maior efeito visual por
   hora de trabalho, e o `CardCreator` precisa ser atualizado junto: ele sobrescreve assets de carta
   quando roda, e é armadilha conhecida deste projeto.
4. **Áudio**: `SoundManager` + `AudioMixer` do kit, som de botão, e a trilha por contexto do §4 —
   ajustando os import settings antes.
5. **VFX de combate**, ligando cada prefab ao `CombatEffectType` correspondente pela tabela do §3.
6. **Limpar o peso morto** do §8.

---

## Inimigos — quem empresta a cara de quem

Preenchido por `EnemyArt.cs` (Tools ▸ Guild of Legends ▸ Aplicar Arte nos Inimigos, ou o gatilho
`RunEnemyArt.trigger`). São **11 inimigos para 7 criaturas desenhadas**: quatro do *Monsters
Creatures Fantasy* e três slimes. Onde falta, a criatura mais próxima é emprestada e separada por
**cor** e **tamanho** — a mesma arte em cinza-pedra e maior não se confunde com a original.

| Inimigo | Criatura | Escala | Cor | Por quê |
|---|---|---|---|---|
| Aranha da Copa | Flying eye | 2,0 | — | criatura que espreita do alto |
| Carniçal | Skeleton | 2,0 | — | morto-vivo armado; literal |
| Estátua Desperta | Skeleton | 2,3 | pedra | figura de pedra que empunha arma |
| Lobo Esfomeado | Goblin | 2,0 | fulvo | **não há canídeo em pacote nenhum** |
| Salteador da Serra | Goblin | 2,0 | — | bandido pequeno e ágil |
| Sanguessuga Gigante | Slime verde | 3,2 | esverdeado | massa sem forma que suga |
| CHEFE — A Coisa da Mata | Mushroom | 3,0 | musgo | fungo da mata; literal |
| CHEFE — O Afogado | Slime azul | 3,4 | água | o que engole |
| CHEFE — O Bibliotecário Cego | Flying eye | 2,8 | pergaminho | o olho que tudo vê, para quem é cego |
| CHEFE — O Gigante de Pedra | Skeleton | 3,0 | pedra | **não há golem em pacote nenhum** |
| CHEFE — O Guardião Sem Nome | Skeleton | 2,8 | sombra | guardião morto que não larga o posto |

**Ainda a comprar ou encomendar:** lobo, aranha e golem de pedra — três dos onze usam arte que não
os representa. `portrait`, `portraitTint` e `portraitScale` são campos serializados: trocar qualquer
escolha no Inspector sobrevive a rodar a ferramenta de novo.

**Ficaram de fora, e por quê:**

- **Dark Knight** — é um conjunto de *peças* para animação esqueletal (elmo, braço, perna, capa),
  não uma criatura inteira; e o traço vetorial briga com o pixel art dos retratos de herói.
- **100 Fantasy Characters** (Blackthornprod) — pintado e colorido demais para a paleta dessaturada.
- **SPUM** — montador de personagem por partes; exigiria montar cada criatura à mão.
