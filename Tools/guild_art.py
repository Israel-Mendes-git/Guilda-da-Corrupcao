"""Recorta a arte do patio da guilda em pecas por sala.

O patio base e as cinco variacoes de terreno foram gerados pelo Gemini em
25/09/2026, todos em 21:9. Cada variacao regenera a pintura inteira com variacao
minima em todo lugar -- nuvem que anda, poca que muda de forma -- entao o que
entra no jogo e apenas o RETANGULO da construcao afetada, sobreposto ao patio
base. Assim o patio nunca troca e a tela nao pisca ao mudar de estado.

Os retangulos abaixo foram medidos a olho sobre a pintura e conferidos pelo
`conferir`, que desenha cada um sobre o patio. Tentar deduzi-los da diferenca
entre as imagens nao funcionou: a regeneracao muda area demais, e o maior
componente conectado caia na construcao errada em tres dos cinco casos.

Uso:
    python Tools/guild_art.py conferir   # grava Temp/guild_rects.png para olhar
    python Tools/guild_art.py recortar   # grava as pecas em Assets/Art/Guild
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter
from scipy import ndimage

ROOT = Path(__file__).resolve().parent.parent
SAIDA = ROOT / "Assets" / "Art" / "Guild"

FONTE = ROOT / "ArtSource" / "Guild"
BASE = FONTE / "patio.jpg"

# Variacao de terreno -> sala cuja construcao foi removida nela.
TERRENOS = {
    "forja":       FONTE / "terreno_forja.jpg",
    "cemiterio":   FONTE / "terreno_cemiterio.jpg",
    "biblioteca":  FONTE / "terreno_biblioteca.jpg",
    "mercado":     FONTE / "terreno_mercado.jpg",
    "saladomapas": FONTE / "terreno_saladomapas.jpg",
}

# Retangulos em pixels sobre a pintura de 3168x1344: (x0, y0, x1, y1).
#
# Levam ~30px de folga em volta da construcao de proposito: e nessa margem que a
# borda do recorte derrete no patio. Sem folga, o degrade comeria a construcao em
# vez da emenda.
RETANGULOS = {
    "forja":       (50,    70,  500, 1300),
    "cemiterio":   (330,  340,  930,  800),
    "biblioteca":  (1142, 105, 1677,  664),
    "jornada":     (1681, 144, 2184,  656),
    "saladomapas": (2425,  18, 3135,  727),
    "mercado":     (1974, 556, 2929, 1210),
    "taverna":     (754,  564, 1574, 1297),
}

CORES = {
    "forja": "#ff4d4d", "cemiterio": "#4dff88", "biblioteca": "#4db8ff",
    "jornada": "#ffd24d", "saladomapas": "#c86bff", "mercado": "#ff9a4d",
    "taverna": "#4dffe0",
}


def carregar(caminho, tamanho):
    im = Image.open(caminho).convert("RGB")
    return im.resize(tamanho, Image.LANCZOS) if im.size != tamanho else im


# Quanto da borda do recorte e usado para casar a cor, e quanto dela derrete no
# patio. A moldura externa do recorte e patio nas DUAS imagens -- e o unico
# pedaco que deveria ser identico, e por isso e ele que mede o desvio.
MOLDURA = 26
DERRETER = 22


def casar_cor(recorte, referencia):
    """Tira do recorte o desvio de cor que a regeneracao introduziu.

    Comparar as imagens inteiras seria errado: o terreno e legitimamente mais
    claro que o predio que saiu, e igualar as medias devolveria um canteiro tao
    escuro quanto a construcao. So a moldura serve de referencia, porque ali as
    duas imagens pintam a mesma coisa.
    """
    a = np.asarray(recorte, dtype=np.float32)
    b = np.asarray(referencia, dtype=np.float32)

    borda = np.zeros(a.shape[:2], dtype=bool)
    borda[:MOLDURA, :] = borda[-MOLDURA:, :] = True
    borda[:, :MOLDURA] = borda[:, -MOLDURA:] = True

    for canal in range(3):
        a[..., canal] += b[..., canal][borda].mean() - a[..., canal][borda].mean()

    return Image.fromarray(np.clip(a, 0, 255).astype(np.uint8))


def mascara_do_que_mudou(recorte, referencia):
    """Alfa opaco so onde a variacao mudou de verdade.

    Derreter a borda do retangulo resolvia a emenda de tom, mas nao a de
    conteudo: a variacao da Forja pintou campos alem da muralha onde o patio tem
    so ceu, e o retangulo inteiro carregava aquele pedaco de paisagem para dentro
    da guilda.

    Aqui o recorte entra no jogo apenas onde ele difere do patio -- a construcao
    que saiu e o canteiro que ficou. Onde as duas pinturas dizem a mesma coisa, o
    alfa e zero e quem aparece e o patio original, que nunca muda. E o que faz a
    emenda deixar de existir em vez de ser disfarcada.
    """
    a = np.asarray(recorte.filter(ImageFilter.GaussianBlur(5)).convert("L"), dtype=np.int16)
    b = np.asarray(referencia.filter(ImageFilter.GaussianBlur(5)).convert("L"), dtype=np.int16)

    mudou = np.abs(a - b) > 26

    # Engorda a mancha antes de suavizar: sem isso, cada pedaco do canteiro que
    # por acaso tenha o tom do chao do patio vira um furo no meio da peca.
    mudou = ndimage.binary_closing(mudou, structure=np.ones((21, 21)))
    mudou = ndimage.binary_dilation(mudou, structure=np.ones((17, 17)))

    alfa = Image.fromarray((mudou * 255).astype(np.uint8))
    return alfa.filter(ImageFilter.GaussianBlur(9))


def compor(recorte, referencia):
    """A peca pronta: cor casada pela moldura e alfa pelo que mudou."""
    peca = casar_cor(recorte, referencia)
    fora = peca.convert("RGBA")
    fora.putalpha(mascara_do_que_mudou(peca, referencia))
    return fora


def refazer_o_ceu(peca, base_inteira, ret, limite):
    """Repinta o ceu da peca com o ceu do proprio patio.

    Duas variacoes inventaram horizonte: onde o patio tem so ceu cinza alem da
    muralha, elas pintaram campos lavrados e uma linha de arvores. Como aquilo
    difere do patio, a mascara o deixava passar, e a guilda abria com um pedaco
    de paisagem colado no canto.

    O conserto nao e mascarar -- a construcao removida ESTAVA ali, e algo precisa
    ocupar o lugar dela. E repintar: para cada linha acima de `limite`, a cor
    media do ceu do patio naquela altura, medida longe da construcao. Ceu de
    pintura e um gradiente liso, entao a media basta e nao se ve a emenda.
    """
    x0, y0, x1, y1 = ret
    a = np.asarray(peca.convert("RGB"), dtype=np.float32)
    todo = np.asarray(base_inteira.convert("RGB"), dtype=np.float32)

    for linha in range(min(limite, a.shape[0])):
        # Amostra o ceu do patio na mesma altura, numa faixa larga e distante
        # do retangulo -- e o mesmo ceu, sem a construcao no caminho.
        faixa = todo[y0 + linha, x1 + 40:x1 + 400]
        if faixa.size == 0:
            faixa = todo[y0 + linha, max(0, x0 - 400):max(1, x0 - 40)]
        if faixa.size == 0:
            continue

        # Transicao suave nas ultimas linhas, para o ceu novo encostar no que
        # a peca ja trazia certo sem degrau.
        peso = 1.0 if linha < limite - 60 else (limite - linha) / 60.0
        a[linha] = a[linha] * (1 - peso) + faixa.mean(axis=0) * peso

    saida = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8)).convert("RGBA")
    saida.putalpha(peca.getchannel("A"))
    return saida


# Ate que altura do recorte a variacao inventou horizonte, por peca.
# Vazio de proposito. A tentativa de repintar o ceu por media de linha alisou
# tambem o que nao era ceu -- a muralha virou um bloco cinza -- e o conserto
# ficou pior que o defeito. Horizonte inventado se resolve na geracao, com uma
# frase a mais no prompt, e nao aqui. Ver ARTE-PROMPTS.md.
CEU_A_REFAZER = {}


def conferir():
    base = Image.open(BASE).convert("RGB")
    tela = base.copy()
    desenho = ImageDraw.Draw(tela)

    for nome, (x0, y0, x1, y1) in RETANGULOS.items():
        desenho.rectangle((x0, y0, x1, y1), outline=CORES[nome], width=9)
        desenho.text((x0 + 16, y0 + 12), nome.upper(), fill=CORES[nome])

    destino = ROOT / "Temp" / "guild_rects.png"
    destino.parent.mkdir(exist_ok=True)
    tela.save(destino)
    print(f"{destino}  {tela.size[0]}x{tela.size[1]}")

    for nome, (x0, y0, x1, y1) in RETANGULOS.items():
        print(f"  {nome:12} {x1-x0:5}x{y1-y0:<5} proporcao {(x1-x0)/(y1-y0):.3f}")


def recortar():
    base = Image.open(BASE).convert("RGB")
    SAIDA.mkdir(parents=True, exist_ok=True)

    base.save(SAIDA / "patio.png")
    print(f"patio.png  {base.size[0]}x{base.size[1]}")

    for nome, ret in RETANGULOS.items():
        base.crop(ret).save(SAIDA / f"sala_{nome}.png")
        print(f"sala_{nome}.png  {ret[2]-ret[0]}x{ret[3]-ret[1]}")

    for nome, caminho in TERRENOS.items():
        var = carregar(caminho, base.size)
        ret = RETANGULOS[nome]

        peca = compor(var.crop(ret), base.crop(ret))

        if nome in CEU_A_REFAZER:
            peca = refazer_o_ceu(peca, base, ret, CEU_A_REFAZER[nome])

        peca.save(SAIDA / f"terreno_{nome}.png")

        cobertura = 100 * (np.asarray(peca.getchannel("A")) > 128).mean()
        print(f"terreno_{nome}.png  {cobertura:.0f}% do retangulo e mudanca de verdade")


def ancoras():
    """Os retangulos em fracao do patio, no formato da tabela do GuildArt."""
    base = Image.open(BASE)
    larg, alt = base.size
    print("    // colar em GuildArt.Construcoes")
    for nome, (x0, y0, x1, y1) in RETANGULOS.items():
        print(f"    // {nome:12} {x0/larg:.4f}f, {1-y1/alt:.4f}f, {x1/larg:.4f}f, {1-y0/alt:.4f}f")


if __name__ == "__main__":
    if len(sys.argv) > 1 and sys.argv[1] == "recortar":
        recortar()
    elif len(sys.argv) > 1 and sys.argv[1] == "ancoras":
        ancoras()
    else:
        conferir()
