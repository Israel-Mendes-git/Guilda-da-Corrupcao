using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// O campo de batalha filmado: heróis de corpo inteiro de um lado, criaturas do
/// outro, e a luta acontecendo em vez de sendo narrada no log.
///
/// <b>Por que um palco filmado, e não figuras na interface.</b> É a mesma razão
/// do <see cref="TrailStage"/>, que já faz isto na estrada: os bonecos do SPUM
/// são <c>SpriteRenderer</c> com Animator, objetos de mundo — não existem como
/// componente de Canvas. E as criaturas dos pacotes são spritesheets fatiados,
/// que animam trocando de quadro. Uma <see cref="RenderTexture"/> põe os dois no
/// meio da pilha da interface, abaixo da mão de cartas e acima do fundo.
///
/// <b>Quem manda no lugar de cada um é a interface.</b> O palco não decide
/// composição: ele recebe, por corpo, o retângulo em que aquela figura deve
/// caber — o mesmo retângulo da view que já recebe o arrasto da carta, mostra a
/// barra de vida e leva o número de dano. Assim não há duas verdades sobre onde
/// está o Carniçal: a figura aparece exatamente onde a carta pode ser solta, e
/// o layout continua sendo problema de quem já o resolvia.
///
/// O palco fica longe da origem e da estrada, para as três câmeras da cena não
/// se enxergarem — o mesmo truque que dispensa criar layer e mexer em culling.
/// </summary>
public class BattleStage : MonoBehaviour
{
    /// <summary>Longe da origem e da estrada (que fica em +500, +500).</summary>
    public static readonly Vector3 Palco = new Vector3(-500f, 500f, 0f);

    /// <summary>
    /// Quantos pixels da textura valem uma unidade de mundo.
    ///
    /// Fixo de propósito: com a relação constante, converter o retângulo de uma
    /// view em posição de palco é uma multiplicação, e o mesmo corpo tem o mesmo
    /// tamanho em qualquer resolução de textura.
    /// </summary>
    public const float PixelsPorUnidade = 100f;

    public const int LarguraPadrao = 1600;
    public const int AlturaPadrao = 460;

    /// <summary>
    /// Quanto da altura do retângulo o herói ocupa.
    ///
    /// Sobra proposital embaixo e em cima: os pés ficam acima da barra de vida,
    /// que é desenhada pela interface no pé da view, e a cabeça não encosta na
    /// borda de cima, onde passam os números de dano.
    /// </summary>
    public float ocupacaoDoHeroi = 0.84f;

    /// <summary>
    /// O mesmo para a criatura, antes de aplicar o tamanho relativo dela.
    ///
    /// Menor que o do herói porque os quadros dos pacotes trazem muita
    /// transparência em volta do desenho — a caixa medida é bem maior que o
    /// bicho. É a mesma razão de <see cref="EnemyData.portraitScale"/> existir.
    /// </summary>
    public float ocupacaoDoInimigo = 0.92f;

    /// <summary>
    /// A escala de criatura que conta como "tamanho comum".
    ///
    /// A tabela do <c>EnemyArt</c> calibrou os comuns em 2.0 e os chefes entre
    /// 2.8 e 3.4. Dividir por este número preserva a intenção — chefe grande é
    /// linguagem de Darkest Dungeon — sem que o valor precise ser recalibrado.
    /// </summary>
    public const float EscalaComum = 2.0f;

    class Corpo
    {
        public Transform raiz;
        public SPUM_Prefabs spum;      // herói
        public EnemyBody bicho;        // criatura
        public float alturaCrua;       // altura do desenho com escala 1
        public float baseCrua;         // distância do pivô até os pés, com escala 1
        public float larguraDaArea;    // para não refazer a conta a cada frame
        public float alturaDaArea;
        public Coroutine voltando;     // a espera que devolve o boneco ao parado
        public bool olhaParaADireita;  // precisa ser espelhado para encarar o outro lado
    }

    readonly Dictionary<HeroData, Corpo> herois = new Dictionary<HeroData, Corpo>();
    readonly Dictionary<EnemyInstance, Corpo> criaturas = new Dictionary<EnemyInstance, Corpo>();

    Camera cameraDoPalco;
    RenderTexture textura;

    public RenderTexture Textura => textura;

    /// <summary>Quantos corpos estão em cena — o teste confere contra a luta.</summary>
    public int Corpos => herois.Count + criaturas.Count;

    /// <summary>Quantos deles são criaturas com quadros de animação de verdade.</summary>
    public int CriaturasAnimadas
    {
        get
        {
            int n = 0;
            foreach (var par in criaturas)
                if (par.Key?.data?.animation != null && par.Key.data.animation.TemQuadros) n++;
            return n;
        }
    }

    /// <summary>Altura do herói em pixels da textura — denuncia enquadramento errado.</summary>
    public float AlturaDoHeroiEmPixels { get; private set; }

    /// <summary>
    /// Levanta o palco. Objeto próprio na raiz da cena: componente que mora no
    /// painel que ele serve morre junto com ele, e o palco precisa sobreviver à
    /// troca de tela entre um combate e o seguinte.
    /// </summary>
    public static BattleStage Criar(int largura = LarguraPadrao, int altura = AlturaPadrao)
    {
        var go = new GameObject("BattleStage");
        go.transform.position = Palco;

        var palco = go.AddComponent<BattleStage>();
        palco.Levantar(Mathf.Max(64, largura), Mathf.Max(64, altura));
        return palco;
    }

    void Levantar(int largura, int altura)
    {
        textura = new RenderTexture(largura, altura, 16, RenderTextureFormat.ARGB32)
        {
            name = "BattleStageRT",
            filterMode = FilterMode.Point,   // pixel art: interpolar borra o traço
        };
        textura.Create();

        var camGo = new GameObject("BattleCamera");
        camGo.transform.SetParent(transform, false);
        camGo.transform.localPosition = new Vector3(0f, 0f, -10f);

        cameraDoPalco = camGo.AddComponent<Camera>();
        cameraDoPalco.orthographic = true;
        cameraDoPalco.orthographicSize = altura * 0.5f / PixelsPorUnidade;
        cameraDoPalco.targetTexture = textura;

        // Fundo transparente: o cenário do combate continua sendo desenhado pela
        // interface, atrás desta janela.
        cameraDoPalco.clearFlags = CameraClearFlags.SolidColor;
        cameraDoPalco.backgroundColor = new Color(0f, 0f, 0f, 0f);

        cameraDoPalco.nearClipPlane = 0.1f;
        cameraDoPalco.farClipPlane = 50f;
        cameraDoPalco.allowHDR = false;
        cameraDoPalco.allowMSAA = false;

        // Duas AudioListener na cena tiram o som do jogo inteiro, e o Unity só
        // reclama por warning.
        var listener = camGo.GetComponent<AudioListener>();
        if (listener != null) Destroy(listener);
    }

    /// <summary>
    /// Refaz a textura quando a janela muda de tamanho. Esticar a filmagem
    /// deformaria pixel art, que é o material dos dois lados do campo.
    /// </summary>
    public void Redimensionar(int largura, int altura)
    {
        largura = Mathf.Max(64, largura);
        altura = Mathf.Max(64, altura);

        if (textura != null && textura.width == largura && textura.height == altura) return;

        var nova = new RenderTexture(largura, altura, 16, RenderTextureFormat.ARGB32)
        {
            name = "BattleStageRT",
            filterMode = FilterMode.Point,
        };
        nova.Create();

        if (cameraDoPalco != null)
        {
            cameraDoPalco.targetTexture = nova;
            cameraDoPalco.orthographicSize = altura * 0.5f / PixelsPorUnidade;
        }

        if (textura != null)
        {
            textura.Release();
            Destroy(textura);
        }

        textura = nova;

        // As áreas guardadas viraram outra coisa: força recalcular a escala.
        foreach (var c in herois.Values) if (c != null) c.alturaDaArea = 0f;
        foreach (var c in criaturas.Values) if (c != null) c.alturaDaArea = 0f;
    }

    /// <summary>
    /// Põe em cena os dois lados da luta que começa, e descarta o que sobrou da
    /// anterior. Chamado uma vez por combate.
    /// </summary>
    public void Elenco(IList<HeroData> party, IList<EnemyInstance> inimigos)
    {
        Limpar();

        if (party != null)
        {
            for (int i = 0; i < party.Count; i++)
            {
                HeroData heroi = party[i];
                if (heroi == null || herois.ContainsKey(heroi)) continue;

                Corpo corpo = CorpoDeHeroi(heroi, party.Count - i);
                if (corpo != null) herois[heroi] = corpo;
            }
        }

        if (inimigos != null)
        {
            for (int i = 0; i < inimigos.Count; i++)
            {
                EnemyInstance inimigo = inimigos[i];
                if (inimigo?.data == null || criaturas.ContainsKey(inimigo)) continue;

                Corpo corpo = CorpoDeCriatura(inimigo, inimigos.Count - i);
                if (corpo != null) criaturas[inimigo] = corpo;
            }
        }
    }

    Corpo CorpoDeHeroi(HeroData heroi, int ordem)
    {
        GameObject prefab = TrailCast.Prefab(heroi.heroClass);
        if (prefab == null) return null;

        GameObject go = Instantiate(prefab, transform);
        go.name = $"Battle_{heroi.heroName}";
        go.transform.localPosition = Vector3.zero;

        // Sem agrupar a ordenação, as peças de dois bonecos vizinhos se
        // intercalam — braço de um na frente do tronco do outro.
        var grupo = go.GetComponent<SortingGroup>();
        if (grupo == null) grupo = go.AddComponent<SortingGroup>();
        grupo.sortingOrder = ordem;

        var spum = go.GetComponent<SPUM_Prefabs>();
        if (spum == null) spum = go.GetComponentInChildren<SPUM_Prefabs>();

        if (spum != null)
        {
            if (!spum.allListsHaveItemsExist()) spum.PopulateAnimationLists();
            spum.OverrideControllerInit();
            spum.PlayAnimation(PlayerState.IDLE, 0);
        }

        Bounds caixa = CaixaDe(go.transform);

        return new Corpo
        {
            raiz = go.transform,
            spum = spum,
            alturaCrua = Mathf.Max(0.0001f, caixa.size.y),
            baseCrua = caixa.min.y - go.transform.position.y,

            // O boneco do SPUM vem desenhado olhando para a esquerda, e o
            // inimigo está à direita: sem espelhar, o grupo luta de costas.
            olhaParaADireita = true,
        };
    }

    Corpo CorpoDeCriatura(EnemyInstance inimigo, int ordem)
    {
        var go = new GameObject($"Battle_{inimigo.data.enemyName}");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;

        var bicho = go.AddComponent<EnemyBody>();
        bicho.Vestir(inimigo.data, ordem);

        var sr = go.GetComponent<SpriteRenderer>();
        float alturaDoQuadro = sr != null && sr.sprite != null ? sr.sprite.bounds.size.y : 1f;

        // O quadro não é a criatura: os spritesheets são grades de tamanho fixo
        // com o bicho solto no meio. Sem a medida do desenho, o esqueleto fica
        // de pé no ar — foi como ele apareceu na primeira captura deste palco.
        EnemyAnimation anim = inimigo.data.animation;
        float fracaoDaBase = anim != null && anim.TemMedida ? anim.baseVisivel : 0f;
        float fracaoDaAltura = anim != null && anim.TemMedida ? anim.alturaVisivel : 1f;

        return new Corpo
        {
            raiz = go.transform,
            bicho = bicho,
            alturaCrua = Mathf.Max(0.0001f, alturaDoQuadro * fracaoDaAltura),

            // O sprite tem pivô no centro do quadro; os pés do desenho ficam
            // acima da borda de baixo dele, na fração medida.
            baseCrua = -alturaDoQuadro * 0.5f + alturaDoQuadro * fracaoDaBase,
        };
    }

    /// <summary>
    /// Encaixa o herói no retângulo que a interface reservou para ele.
    ///
    /// O retângulo vem em pixels da textura, com origem no canto inferior
    /// esquerdo — que é como a janela lê a própria área. Os pés assentam na base
    /// do retângulo; a figura cresce para cima, de modo que um chefe grande
    /// ocupa mais céu e nunca afunda no chão.
    /// </summary>
    public void Colocar(HeroData heroi, Rect areaEmPixels)
    {
        if (heroi != null && herois.TryGetValue(heroi, out Corpo corpo))
            Encaixar(corpo, areaEmPixels, ocupacaoDoHeroi, true);
    }

    /// <summary>O mesmo para a criatura, com o tamanho relativo dela aplicado.</summary>
    public void Colocar(EnemyInstance inimigo, Rect areaEmPixels)
    {
        if (inimigo == null || !criaturas.TryGetValue(inimigo, out Corpo corpo)) return;

        float relativo = inimigo.data != null && inimigo.data.portraitScale > 0f
            ? inimigo.data.portraitScale / EscalaComum
            : 1f;

        Encaixar(corpo, areaEmPixels, ocupacaoDoInimigo * relativo, false);
    }

    void Encaixar(Corpo corpo, Rect area, float ocupacao, bool medirParaRelatorio)
    {
        if (corpo?.raiz == null || textura == null) return;
        if (area.width <= 1f || area.height <= 1f) return;

        // A escala só muda quando o retângulo muda: recalcular a cada frame
        // custaria uma medição de bounds por corpo, e a tela do combate é
        // estática enquanto ninguém joga carta.
        bool areaNova = !Mathf.Approximately(corpo.alturaDaArea, area.height)
                     || !Mathf.Approximately(corpo.larguraDaArea, area.width);

        if (areaNova)
        {
            corpo.alturaDaArea = area.height;
            corpo.larguraDaArea = area.width;

            float alturaAlvoEmUnidades = area.height * ocupacao / PixelsPorUnidade;
            float escala = alturaAlvoEmUnidades / corpo.alturaCrua;

            // Quem precisa ser espelhado para encarar o outro lado é espelhado
            // aqui, junto com a escala: dois lugares mexendo no mesmo transform
            // acabam se sobrescrevendo, e um deles ganha por ordem de execução.
            corpo.raiz.localScale = new Vector3(corpo.olhaParaADireita ? -escala : escala,
                                                escala, escala);

            if (medirParaRelatorio)
                AlturaDoHeroiEmPixels = corpo.alturaCrua * escala * PixelsPorUnidade;
        }

        // Centro horizontal do retângulo, pés na base dele.
        float x = (area.center.x - textura.width * 0.5f) / PixelsPorUnidade;
        float y = (area.yMin - textura.height * 0.5f) / PixelsPorUnidade;

        float pesEmUnidades = corpo.baseCrua * corpo.raiz.localScale.y;
        corpo.raiz.localPosition = new Vector3(x, y - pesEmUnidades, 0f);
    }

    // --- O que acontece na luta ---------------------------------------------

    /// <summary>O herói golpeia. Volta sozinho ao parado quando a animação acaba.</summary>
    public void HeroiAtaca(HeroData heroi) => TocarNoHeroi(heroi, PlayerState.ATTACK);

    /// <summary>O herói apanhou.</summary>
    public void HeroiApanha(HeroData heroi) => TocarNoHeroi(heroi, PlayerState.DAMAGED);

    /// <summary>
    /// O herói caiu. Não é morte definitiva no jogo — Beira da Morte é um estado
    /// de que se volta —, mas em cena ele fica caído até a luta acabar.
    /// </summary>
    public void HeroiCai(HeroData heroi) => TocarNoHeroi(heroi, PlayerState.DEATH);

    /// <summary>
    /// O herói socorrido volta a ficar de pé.
    ///
    /// O SPUM guarda a morte num bool do Animator: sem alguém pedir o parado de
    /// volta, quem saiu da Beira da Morte continuaria caído até o fim da luta,
    /// contando uma coisa que os números da tela negam.
    /// </summary>
    public void HeroiLevanta(HeroData heroi) => TocarNoHeroi(heroi, PlayerState.IDLE);

    public void CriaturaAtaca(EnemyInstance inimigo) => TocarNaCriatura(inimigo, EnemyBody.Estado.Ataque);
    public void CriaturaApanha(EnemyInstance inimigo) => TocarNaCriatura(inimigo, EnemyBody.Estado.Apanhou);
    public void CriaturaMorre(EnemyInstance inimigo) => TocarNaCriatura(inimigo, EnemyBody.Estado.Morte);

    void TocarNoHeroi(HeroData heroi, PlayerState estado)
    {
        if (heroi == null || !herois.TryGetValue(heroi, out Corpo corpo)) return;
        if (corpo?.spum == null) return;

        corpo.spum.PlayAnimation(estado, 0);

        // O SPUM não avisa quando a animação acaba: ATTACK e DAMAGED são
        // disparos, e sem alguém para devolver o boneco ao IDLE ele fica preso
        // no último quadro do golpe pelo resto da luta.
        if (estado == PlayerState.ATTACK || estado == PlayerState.DAMAGED)
        {
            // Uma espera por corpo: dois golpes seguidos deixariam duas
            // corrotinas correndo, e a primeira devolveria o boneco ao parado no
            // meio do segundo golpe.
            if (corpo.voltando != null) StopCoroutine(corpo.voltando);
            corpo.voltando = StartCoroutine(VoltarAoParado(heroi, corpo, 0.6f));
        }
    }

    System.Collections.IEnumerator VoltarAoParado(HeroData heroi, Corpo corpo, float espera)
    {
        yield return new WaitForSeconds(espera);

        corpo.voltando = null;
        if (corpo.spum == null) yield break;

        // Quem caiu no meio da espera fica caído.
        if (heroi != null && (heroi.isDead || heroi.isOnDeathsDoor)) yield break;

        corpo.spum.PlayAnimation(PlayerState.IDLE, 0);
    }

    void TocarNaCriatura(EnemyInstance inimigo, EnemyBody.Estado estado)
    {
        if (inimigo != null && criaturas.TryGetValue(inimigo, out Corpo corpo) && corpo?.bicho != null)
            corpo.bicho.Tocar(estado);
    }

    /// <summary>
    /// Esconde quem não está mais em cena: herói morto e criatura abatida cuja
    /// animação de morte já rodou. Chamado junto com o refresh da interface.
    /// </summary>
    public void AtualizarPresenca()
    {
        foreach (var par in herois)
        {
            if (par.Key == null || par.Value?.raiz == null) continue;

            bool devePresente = !par.Key.isDead;
            if (par.Value.raiz.gameObject.activeSelf != devePresente)
                par.Value.raiz.gameObject.SetActive(devePresente);
        }
    }

    /// <summary>
    /// A janela existe e está desenhando alguém? O teste pergunta isto antes de
    /// olhar a captura.
    /// </summary>
    public bool EmCena => textura != null && Corpos > 0;

    void Limpar()
    {
        herois.Clear();
        criaturas.Clear();
        StopAllCoroutines();

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform filho = transform.GetChild(i);
            if (cameraDoPalco != null && filho == cameraDoPalco.transform) continue;

            if (Application.isPlaying) Destroy(filho.gameObject);
            else DestroyImmediate(filho.gameObject);
        }
    }

    /// <summary>
    /// A caixa que o desenho ocupa de fato. Só renderizadores ligados e com
    /// sprite entram: o SPUM traz peças de reserva desativadas — armas que o
    /// boneco não usa —, e contá-las esticaria a caixa para o lado do nada.
    /// </summary>
    static Bounds CaixaDe(Transform corpo)
    {
        var renderers = corpo.GetComponentsInChildren<SpriteRenderer>(false);
        Bounds b = new Bounds(corpo.position, Vector3.zero);
        bool primeiro = true;

        foreach (var r in renderers)
        {
            if (r == null || r.sprite == null) continue;

            if (primeiro) { b = r.bounds; primeiro = false; }
            else b.Encapsulate(r.bounds);
        }

        return b;
    }

    void OnDestroy()
    {
        if (cameraDoPalco != null) cameraDoPalco.targetTexture = null;

        if (textura != null)
        {
            textura.Release();
            Destroy(textura);
        }
    }
}
