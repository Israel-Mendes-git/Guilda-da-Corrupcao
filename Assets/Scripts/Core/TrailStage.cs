using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// O palco onde o grupo caminha, filmado e entregue à interface como textura.
///
/// <b>Por que um palco filmado, e não bonecos soltos na tela.</b> Os corpos do
/// SPUM são <c>SpriteRenderer</c> — objetos de mundo. A jornada inteira é
/// Canvas, e a estrada precisa ficar <i>entre</i> duas camadas dele: acima da
/// arte do bioma, abaixo das caixas de texto e da mão de cartas. Um objeto de
/// mundo não se intercala no meio de um Canvas; uma <see cref="RenderTexture"/>
/// se põe exatamente onde se quiser na hierarquia da interface.
///
/// O palco fica a 500 unidades da origem, longe do enquadramento da câmera
/// principal. É o que dispensa criar layer nova e mexer em culling mask: as
/// duas câmeras não se enxergam porque não há nada entre elas.
///
/// <b>Andar sem sair do lugar.</b> Os bonecos ficam parados em quadro e tocam a
/// animação de caminhada; quem se move é o cenário. Uma travessia dura o tempo
/// que o trecho pedir, e nada precisa saber quantos metros o grupo "andou".
/// </summary>
public class TrailStage : MonoBehaviour
{
    /// <summary>Longe o bastante da origem para a câmera do jogo não pegar nada.</summary>
    public static readonly Vector3 Palco = new Vector3(500f, 500f, 0f);

    /// <summary>
    /// Proporção da faixa da estrada, usada só quando quem levanta o palco não
    /// sabe dizer o tamanho da janela. Larga e baixa: é uma fila de gente vista
    /// de lado, não um retrato.
    /// </summary>
    public const int LarguraPadrao = 1280;
    public const int AlturaPadrao = 300;

    /// <summary>
    /// Quanto da altura do quadro o grupo ocupa. O resto é céu e chão.
    ///
    /// <b>O enquadramento é medido, não estimado.</b> A primeira versão fixava a
    /// altura de campo em 1,4 unidade supondo bonecos de 1,6 — os do SPUM têm
    /// cerca de 0,5, e o grupo saiu com um quinto do tamanho previsto. Medir os
    /// <c>Bounds</c> depois de montar a fila também torna a troca de qualquer
    /// prefab da tabela inofensiva.
    /// </summary>
    /// <summary>
    /// Fica em 0,70 e não mais alto porque as barras de vida e estresse moram
    /// acima da cabeça: enquadrar o grupo apertado corta justamente elas.
    /// </summary>
    [Range(0.3f, 0.95f)] public float ocupacao = 0.70f;

    /// <summary>
    /// Distância entre um herói e o seguinte, como fração da altura do boneco.
    /// Gente em pé é bem mais alta que larga; 0,55 dá uma fila encorpada, sem
    /// membros se cruzando e sem buraco no meio.
    /// </summary>
    public float passoDaFila = 0.55f;

    /// <summary>Um herói em cena: o boneco e as duas barras sobre a cabeça.</summary>
    class Corpo
    {
        public HeroData heroi;
        public SPUM_Prefabs spum;
        public Transform raiz;
        public SpriteRenderer vida;
        public SpriteRenderer estresse;
        public float larguraDasBarras;
    }

    Camera cameraDoPalco;
    RenderTexture textura;
    readonly List<Corpo> fila = new List<Corpo>();
    bool andando;

    static Sprite quadradoCache;

    /// <summary>
    /// Um quadrado branco de um pixel, para as barras. Gerado em código pelo
    /// mesmo motivo do círculo do mapa: sprite de editor não existe em runtime.
    /// </summary>
    static Sprite Quadrado()
    {
        if (quadradoCache != null) return quadradoCache;

        var textura = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var pixels = new Color[16];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        textura.SetPixels(pixels);
        textura.filterMode = FilterMode.Point;
        textura.Apply();

        quadradoCache = Sprite.Create(textura, new Rect(0, 0, 4, 4), new Vector2(0f, 0.5f), 4f);
        return quadradoCache;
    }

    /// <summary>A filmagem do palco. Null até <see cref="Criar"/> rodar.</summary>
    public RenderTexture Textura => textura;

    /// <summary>Quantos corpos estão em cena — o teste confere contra a party.</summary>
    public int Corpos => fila.Count;

    /// <summary>
    /// Altura do boneco em pixels da faixa, medida depois de enquadrar.
    ///
    /// É o número que denuncia o enquadramento errado sem precisar de captura:
    /// grupo minúsculo ou estourando a moldura aparecem aqui como um valor fora
    /// da faixa esperada, enquanto a contagem de corpos continua certa.
    /// </summary>
    public float AlturaEmPixels { get; private set; }

    /// <summary>
    /// Levanta o palco. Objeto próprio na raiz da cena, e não filho do painel da
    /// jornada: componente que mora no painel que ele serve morre junto com ele,
    /// e o palco precisa continuar filmando enquanto a interface troca de tela.
    /// </summary>
    /// <param name="largura">Largura da janela em pixels. A textura nasce do
    /// tamanho exato da faixa na tela: qualquer outro valor esticaria a filmagem
    /// e deformaria os bonecos, que são pixel art.</param>
    public static TrailStage Criar(int largura = LarguraPadrao, int altura = AlturaPadrao)
    {
        var go = new GameObject("TrailStage");
        go.transform.position = Palco;

        var palco = go.AddComponent<TrailStage>();
        palco.Levantar(Mathf.Max(64, largura), Mathf.Max(64, altura));
        return palco;
    }

    void Levantar(int largura, int altura)
    {
        textura = new RenderTexture(largura, altura, 16, RenderTextureFormat.ARGB32)
        {
            name = "TrailStageRT",
            filterMode = FilterMode.Point,   // pixel art: interpolar borra o traço
        };
        textura.Create();

        var camGo = new GameObject("TrailCamera");
        camGo.transform.SetParent(transform, false);
        camGo.transform.localPosition = new Vector3(0f, 0f, -10f);

        cameraDoPalco = camGo.AddComponent<Camera>();
        cameraDoPalco.orthographic = true;
        cameraDoPalco.orthographicSize = 1f;   // recalculado ao montar a fila
        cameraDoPalco.targetTexture = textura;

        // Fundo transparente: a arte do bioma continua sendo desenhada pela
        // interface, atrás desta faixa. Um fundo opaco taparia o cenário.
        cameraDoPalco.clearFlags = CameraClearFlags.SolidColor;
        cameraDoPalco.backgroundColor = new Color(0f, 0f, 0f, 0f);

        cameraDoPalco.nearClipPlane = 0.1f;
        cameraDoPalco.farClipPlane = 50f;
        cameraDoPalco.allowHDR = false;
        cameraDoPalco.allowMSAA = false;

        // A câmera do palco não é a de áudio: duas AudioListener na cena tiram
        // o som do jogo inteiro e o Unity só avisa por warning.
        var listener = camGo.GetComponent<AudioListener>();
        if (listener != null) Destroy(listener);
    }

    /// <summary>
    /// Põe o grupo em fila na ordem recebida — que é a da formação, a mesma que
    /// decide quem apanha no combate. O <b>primeiro da lista vai na frente</b>,
    /// à direita, no sentido da marcha: é ele quem encontra o que vier.
    /// </summary>
    public void Elenco(IList<HeroData> ordem)
    {
        Limpar();
        if (ordem == null) return;

        var corpos = new List<Transform>();

        for (int i = 0; i < ordem.Count; i++)
        {
            HeroData heroi = ordem[i];
            if (heroi == null) continue;

            GameObject prefab = TrailCast.Prefab(heroi.heroClass);
            if (prefab == null) continue;

            GameObject corpo = Instantiate(prefab, transform);
            corpo.name = $"Trail_{i}_{heroi.heroName}";
            corpo.transform.localPosition = Vector3.zero;

            // Sem agrupar a ordenação, as peças de dois bonecos vizinhos se
            // intercalam — o braço de quem está atrás aparece na frente do
            // tronco de quem está na frente.
            var grupo = corpo.GetComponent<SortingGroup>();
            if (grupo == null) grupo = corpo.AddComponent<SortingGroup>();
            grupo.sortingOrder = ordem.Count - i;

            corpos.Add(corpo.transform);

            var spum = corpo.GetComponent<SPUM_Prefabs>();
            if (spum == null) spum = corpo.GetComponentInChildren<SPUM_Prefabs>();

            if (spum != null)
            {
                // O prefab já vem com as listas preenchidas; a checagem é para o
                // caso de um boneco montado à mão entrar na tabela depois.
                if (!spum.allListsHaveItemsExist()) spum.PopulateAnimationLists();
                spum.OverrideControllerInit();
            }
            else
            {
                Debug.LogWarning($"TrailStage: {prefab.name} não tem SPUM_Prefabs — sem animação.");
            }

            fila.Add(new Corpo { heroi = heroi, spum = spum, raiz = corpo.transform });
        }

        Formar(corpos);
        Tocar(andando ? PlayerState.MOVE : PlayerState.IDLE);
        AtualizarBarras();
    }

    /// <summary>
    /// Repinta vida e estresse de quem está na estrada.
    ///
    /// Chamado sempre que o estado do grupo muda — o dano de um evento, a fome
    /// do dia, o estresse do escuro. As barras existem para o jogador não
    /// precisar tirar os olhos do mapa para saber se pode seguir em frente.
    /// </summary>
    public void AtualizarBarras()
    {
        foreach (var corpo in fila)
        {
            if (corpo?.heroi == null) continue;

            float vida = corpo.heroi.maxHp > 0
                ? Mathf.Clamp01(corpo.heroi.currentHp / (float)corpo.heroi.maxHp)
                : 0f;

            float estresse = Mathf.Clamp01(corpo.heroi.stress / 100f);

            Encher(corpo.vida, vida, corpo.larguraDasBarras);
            Encher(corpo.estresse, estresse, corpo.larguraDasBarras);

            // Morto some do mapa: a fila é quem ainda anda.
            if (corpo.raiz != null && corpo.raiz.gameObject.activeSelf == corpo.heroi.isDead)
                corpo.raiz.gameObject.SetActive(!corpo.heroi.isDead);
        }
    }

    static void Encher(SpriteRenderer barra, float fracao, float larguraTotal)
    {
        if (barra == null) return;

        // Escala em X, com o pivô na ponta esquerda: a barra esvazia da direita
        // para a esquerda, como toda barra de vida desde sempre.
        Vector3 escala = barra.transform.localScale;
        escala.x = Mathf.Max(0.0001f, larguraTotal * fracao);
        barra.transform.localScale = escala;
    }

    /// <summary>
    /// Alinha a fila pelo chão, espaça pela altura medida e enquadra a câmera.
    ///
    /// <b>Alinhar pelos pés, e não pelo pivô.</b> Cada boneco do SPUM tem o
    /// próprio ponto de origem, e usar <c>localPosition.y = 0</c> para todos
    /// deixaria uns pisando mais fundo que os outros. Aqui o que se iguala é a
    /// base real do desenho.
    /// </summary>
    void Formar(List<Transform> corpos)
    {
        if (corpos.Count == 0) return;

        var caixas = new List<Bounds>(corpos.Count);
        float alturaMedia = 0f;

        foreach (var corpo in corpos)
        {
            Bounds b = CaixaDe(corpo);
            caixas.Add(b);
            alturaMedia += b.size.y;
        }

        alturaMedia /= corpos.Count;
        if (alturaMedia <= 0.0001f) alturaMedia = 1f;

        float passo = alturaMedia * passoDaFila;
        float x0 = (corpos.Count - 1) * passo * 0.5f;

        // O primeiro da lista vai na frente, à direita, no sentido da marcha:
        // é ele quem encontra o que vier pela estrada.
        for (int i = 0; i < corpos.Count; i++)
        {
            Vector3 pos = corpos[i].localPosition;
            float centroLocalX = caixas[i].center.x - transform.position.x;
            float baseLocalY = caixas[i].min.y - transform.position.y;

            pos.x += (x0 - i * passo) - centroLocalX;
            pos.y += -baseLocalY;   // pés na linha zero do palco
            pos.z = 0f;
            corpos[i].localPosition = pos;
        }

        // As barras sobre a cabeça, agora que se sabe a altura de cada um.
        for (int i = 0; i < corpos.Count && i < fila.Count; i++)
            MontarBarras(fila[i], caixas[i].size.y, alturaMedia);

        // Enquadramento: a altura do quadro sai da altura do grupo, e o grupo
        // fica assentado um pouco abaixo do meio, com mais céu que chão.
        float alturaDoQuadro = alturaMedia / Mathf.Clamp(ocupacao, 0.3f, 0.95f);
        cameraDoPalco.orthographicSize = alturaDoQuadro * 0.5f;
        cameraDoPalco.transform.localPosition =
            new Vector3(0f, alturaDoQuadro * 0.5f - alturaMedia * 0.12f, -10f);

        AlturaEmPixels = textura != null ? alturaMedia / alturaDoQuadro * textura.height : 0f;
    }

    /// <summary>
    /// Duas barras acima da cabeça: vida em cima, estresse embaixo.
    ///
    /// Ficam <b>dentro do palco</b>, e não na interface. Desenhá-las por fora
    /// exigiria projetar a posição de cada boneco para dentro da ficha a cada
    /// frame — e a ficha se move o tempo todo, então seria uma conta refeita
    /// para sempre. Aqui elas são filhas do boneco e o acompanham de graça.
    ///
    /// O tamanho sai da altura medida do herói, não de um número fixo: a ficha
    /// muda de tamanho conforme o mapa, e barras em unidades absolutas ficariam
    /// gigantes num caso e invisíveis no outro.
    /// </summary>
    void MontarBarras(Corpo corpo, float alturaDele, float alturaMedia)
    {
        if (corpo?.raiz == null) return;

        float largura = alturaMedia * 0.36f;
        float espessura = alturaMedia * 0.055f;

        // Todas na mesma linha, e não cada uma sobre a própria cabeça: os
        // bonecos têm alturas diferentes, e barras escalonadas leem como sujeira
        // no mapa em vez de uma fila de estados comparáveis.
        float acimaDaCabeca = Mathf.Max(alturaDele, alturaMedia) + alturaMedia * 0.10f;

        corpo.larguraDasBarras = largura;

        // Ordem: fundo, preenchimento — e o preenchimento por cima, sempre.
        corpo.vida = Barra(corpo.raiz, "Vida", new Vector3(-largura * 0.5f, acimaDaCabeca, 0f),
                           largura, espessura, new Color(0.74f, 0.19f, 0.17f), 20);

        corpo.estresse = Barra(corpo.raiz, "Estresse",
                               new Vector3(-largura * 0.5f, acimaDaCabeca - espessura * 1.6f, 0f),
                               largura, espessura, new Color(0.83f, 0.69f, 0.36f), 22);
    }

    /// <summary>Uma barra: a moldura escura fixa e o preenchimento que encolhe.</summary>
    static SpriteRenderer Barra(Transform pai, string nome, Vector3 posicao,
                                float largura, float espessura, Color cor, int ordem)
    {
        var fundo = new GameObject(nome + "Fundo");
        fundo.transform.SetParent(pai, false);
        fundo.transform.localPosition = posicao;
        fundo.transform.localScale = new Vector3(largura, espessura, 1f);

        var fundoSr = fundo.AddComponent<SpriteRenderer>();
        fundoSr.sprite = Quadrado();

        // Cinza, não preto: o mapa é escuro, e uma barra vazia com fundo preto
        // simplesmente desaparece — que é o caso do estresse zerado, o estado
        // normal de quem acabou de sair da guilda.
        fundoSr.color = new Color(0.22f, 0.20f, 0.19f, 0.9f);
        fundoSr.sortingOrder = ordem;

        var cheio = new GameObject(nome);
        cheio.transform.SetParent(pai, false);
        cheio.transform.localPosition = posicao;
        cheio.transform.localScale = new Vector3(largura, espessura, 1f);

        var cheioSr = cheio.AddComponent<SpriteRenderer>();
        cheioSr.sprite = Quadrado();
        cheioSr.color = cor;
        cheioSr.sortingOrder = ordem + 1;

        return cheioSr;
    }

    /// <summary>
    /// A caixa que o desenho ocupa de fato. Só renderizadores ligados e com
    /// sprite entram: o SPUM traz peças de reserva desativadas (armas que o
    /// boneco não usa), e contá-las esticaria a caixa para o lado do nada.
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

    /// <summary>
    /// Caminhando ou parado. Idempotente de propósito: o fluxo da jornada vai
    /// chamar isto a cada troca de tela, e trocar a animação toda vez reiniciaria
    /// o passo do zero, o que aparece como um tranco na perna.
    /// </summary>
    public bool Andando
    {
        get => andando;
        set
        {
            if (andando == value) return;

            andando = value;
            Tocar(andando ? PlayerState.MOVE : PlayerState.IDLE);
        }
    }

    void Tocar(PlayerState estado)
    {
        foreach (var corpo in fila)
            if (corpo?.spum != null) corpo.spum.PlayAnimation(estado, 0);
    }

    void Limpar()
    {
        fila.Clear();

        // Filho por filho, pulando a câmera: destruir tudo levaria junto o
        // enquadramento e a próxima jornada abriria com a tela preta.
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform filho = transform.GetChild(i);
            if (cameraDoPalco != null && filho == cameraDoPalco.transform) continue;

            if (Application.isPlaying) Destroy(filho.gameObject);
            else DestroyImmediate(filho.gameObject);
        }
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
