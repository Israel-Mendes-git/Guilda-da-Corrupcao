#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Dá corpo aos inimigos: preenche <see cref="EnemyData.portrait"/>, que existia
/// desde sempre e estava vazio nos 11 — no combate eles eram caixas com nome e
/// número, e o jogador lutava contra um retângulo.
///
/// Tools → Guild of Legends → Aplicar Arte nos Inimigos
///
/// **A escolha da arte, e a honestidade dela:** há 11 inimigos e **7 criaturas
/// desenhadas** nos pacotes importados — quatro do *Monsters Creatures Fantasy*
/// (esqueleto, goblin, cogumelo, olho voador) e três slimes. Não há lobo, aranha
/// nem gigante de pedra em pacote nenhum. Onde falta, a criatura mais próxima é
/// emprestada e separada por **cor** e **tamanho**: o mesmo esqueleto serve ao
/// Carniçal (pálido) e à Estátua Desperta (cinza-pedra, maior), e ninguém os
/// confunde na tela.
///
/// Só pixel art entra. O pacote *Dark Knight* tem um chefe bonito, mas é um
/// conjunto de **peças** para animação esqueletal — elmo, braço, perna, capa — e
/// não uma criatura inteira; e o traço vetorial dele brigaria com o pixel art dos
/// retratos de herói. O *100 Fantasy Characters* é pintado e colorido demais para
/// a paleta dessaturada do jogo.
///
/// O campo é serializado, então o autor pode trocar qualquer escolha no Inspector
/// — e rodar isto de novo **não** sobrescreve o que já estiver preenchido com
/// outra coisa, a menos que se marque a opção.
/// </summary>
public static class EnemyArt
{
    /// <summary>Onde mora cada criatura. O quadro usado é o primeiro do Idle.</summary>
    const string Monstros = "Assets/Monsters Creatures Fantasy/Sprites";
    const string Slimes = "Assets/War/Slime Enemy - Pixel Art/Sprites/Idle";

    /// <summary>Onde ficam as criaturas com quadro a quadro em arquivos separados.</summary>
    const string Horror = "Assets/HorrorEnemyPack";

    class Escolha
    {
        public readonly string caminho;   // arquivo do spritesheet
        public readonly float escala;     // chefe grande é linguagem de Darkest Dungeon
        public readonly string porque;    // por que esta criatura, e não outra

        /// <summary>
        /// Pasta de quadros soltos, quando a criatura vem assim.
        ///
        /// O <i>Monsters Creatures Fantasy</i> entrega um spritesheet por estado,
        /// já fatiado. O <i>Horror Enemy Pack</i> entrega um PNG por quadro, com
        /// todos os estados numa sequência só — e o ReadMe dele diz quantos
        /// quadros tem cada animação. As duas formas convivem aqui porque
        /// converter uma na outra custaria fatiar textura por código, e o que
        /// muda de verdade é só onde os quadros são procurados.
        /// </summary>
        public readonly string pastaDeQuadros;
        public readonly string prefixo;
        public readonly (int ini, int fim) idle, attack, hit, death;

        public Escolha(string caminho, string porque, float escala = 1f)
        {
            this.caminho = caminho;
            this.porque = porque;
            this.escala = escala;
        }

        public Escolha(string pasta, string prefixo, string porque, float escala,
                       (int, int) idle, (int, int) attack, (int, int) hit, (int, int) death)
        {
            this.pastaDeQuadros = pasta;
            this.prefixo = prefixo;
            this.porque = porque;
            this.escala = escala;
            this.idle = idle;
            this.attack = attack;
            this.hit = hit;
            this.death = death;

            // O retrato continua sendo o primeiro quadro do parado.
            this.caminho = $"{pasta}/{prefixo}{idle.Item1}.png";
        }

        public bool PorQuadros => !string.IsNullOrEmpty(pastaDeQuadros);
    }

    /// <summary>
    /// Inimigo → criatura. A chave é o <c>enemyName</c> do asset.
    /// </summary>
    static readonly Dictionary<string, Escolha> ArtePorInimigo = new Dictionary<string, Escolha>
    {
        // --- Comuns ---------------------------------------------------------
        { "Aranha da Copa",
          new Escolha($"{Horror}/Abomination", "Abomination Separate frame ",
                      "massa escura cheia de patas e olhos: é o que mais se parece com algo "
                    + "que espreita numa copa de árvore",
                      2.2f,
                      idle: (0, 3), attack: (8, 16), hit: (17, 18), death: (19, 27)) },

        { "Carniçal",
          new Escolha($"{Monstros}/Skeleton/Idle.png",
                      "morto-vivo armado; a arte é literal",
                      2.0f) },

        { "Estátua Desperta",
          new Escolha($"{Monstros}/Skeleton/Idle.png",
                      "esqueleto emprestado, tingido de pedra e maior — figura de pedra que empunha arma",
                      2.3f) },

        // Quadros soltos do Horror Enemy Pack. As faixas saem do ReadMe dele, na
        // ordem em que os quadros foram exportados — Idle, Walk, Attack, Hit,
        // Death — e é por isso que elas estão escritas aqui em vez de deduzidas:
        // a numeração não tem nome, só posição.
        { "Lobo Esfomeado",
          new Escolha($"{Horror}/Catto", "Cat monster separated frame ",
                      "felino monstruoso: o predador de quatro patas que o goblin nunca foi",
                      2.2f,
                      idle: (14, 17), attack: (18, 23), hit: (24, 25), death: (26, 34)) },

        { "Salteador da Serra",
          new Escolha($"{Monstros}/Goblin/Idle.png",
                      "bandido pequeno e ágil — o goblin é a leitura direta",
                      2.0f) },

        { "Sanguessuga Gigante",
          new Escolha($"{Slimes}/Green/Sprite Sheet - Green Idle.png",
                      "massa sem forma que suga; o slime é a criatura certa",
                      3.2f) },

        // --- Chefes ---------------------------------------------------------
        { "A Coisa da Mata",
          new Escolha($"{Monstros}/Mushroom/Idle.png",
                      "fungo da mata; a arte é literal, ampliada para peso de chefe",
                      3.0f) },

        { "O Afogado",
          // O prefixo é copiado do disco, letra por letra: os quatro pacotes
          // nomeiam diferente ("Separate", "separated", "Separated Frame"), e
          // o AssetDatabase diferencia maiúscula — arquivo não encontrado aqui
          // não dá erro, só devolve animação vazia.
          new Escolha($"{Horror}/Mad Ghost", "Mad Ghost Separated frame ",
                      "o que sobra de quem se afogou — vulto, não gosma",
                      3.0f,
                      idle: (0, 3), attack: (13, 15), hit: (20, 21), death: (22, 30)) },

        { "O Bibliotecário Cego",
          new Escolha($"{Horror}/Mage", "Mage Separated Frame ",
                      "quem guarda a biblioteca é um leitor, e leitor tem corpo de gente",
                      2.6f,
                      idle: (0, 3), attack: (8, 17), hit: (18, 19), death: (20, 28)) },

        // Continua no esqueleto tingido de pedra: a abominação do Horror pack foi
        // testada aqui e não lê como pedra — é um horror orgânico, e foi parar
        // na Aranha da Copa, onde as patas fazem sentido.
        { "O Gigante de Pedra",
          new Escolha($"{Monstros}/Skeleton/Idle.png",
                      "esqueleto tingido de pedra, no maior tamanho da tabela",
                      3.0f) },

        { "O Guardião Sem Nome",
          new Escolha($"{Monstros}/Skeleton/Idle.png",
                      "guardião morto que não larga o posto; separado do Carniçal pela cor de sombra",
                      2.8f) }
    };

    /// <summary>Cores aplicadas separadamente, para a tabela acima ficar legível.</summary>
    /// <summary>
    /// Cores aplicadas separadamente, para a tabela acima ficar legível.
    ///
    /// <b>Tingir era remendo, não estilo.</b> Cada cor aqui existia para separar
    /// dois inimigos que dividiam o mesmo desenho — o esqueleto pálido do
    /// Carniçal e o cinza-pedra da Estátua. Quem ganhou criatura própria no
    /// Horror Enemy Pack saiu desta lista: o gato, o vulto, o mago e a
    /// abominação são bichos diferentes por desenho, e pintá-los por cima só
    /// estragaria a arte que veio resolver o problema.
    /// </summary>
    static readonly Dictionary<string, Color> TintPorInimigo = new Dictionary<string, Color>
    {
        { "Estátua Desperta",     new Color(0.62f, 0.63f, 0.66f) },
        { "Sanguessuga Gigante",  new Color(0.72f, 0.78f, 0.60f) },
        { "O Guardião Sem Nome",  new Color(0.45f, 0.42f, 0.52f) },
        { "O Gigante de Pedra",   new Color(0.55f, 0.56f, 0.60f) },
        { "A Coisa da Mata",      new Color(0.70f, 0.80f, 0.55f) },

        // As quatro criaturas do Horror pack são silhuetas quase pretas, e o
        // campo de batalha é escuro. Um cinza-azulado claro por cima devolve a
        // borda sem apagar o desenho — é o mesmo problema dos ícones pretos
        // sobre o mapa noturno, resolvido do lado da cor porque aqui dá.
        { "Aranha da Copa",       new Color(1.55f, 1.35f, 1.45f) },
        { "Lobo Esfomeado",       new Color(1.55f, 1.40f, 1.35f) },
        { "O Afogado",            new Color(1.35f, 1.50f, 1.75f) },
        { "O Bibliotecário Cego", new Color(1.60f, 1.50f, 1.25f) },
    };

    [MenuItem("Tools/Guild of Legends/Aplicar Arte nos Inimigos")]
    public static void Aplicar()
    {
        Aplicar(false);
    }

    /// <param name="sobrescrever">true para trocar arte já escolhida à mão.</param>
    public static void Aplicar(bool sobrescrever)
    {
        EnemyData[] inimigos = Resources.LoadAll<EnemyData>("Enemies");
        if (inimigos.Length == 0)
        {
            Debug.LogError("EnemyArt: nenhum inimigo em Resources/Enemies.");
            return;
        }

        int aplicados = 0;
        var mapa = new List<string>();
        var semMapa = new List<string>();
        var semArte = new List<string>();

        foreach (EnemyData inimigo in inimigos)
        {
            string nome = string.IsNullOrEmpty(inimigo.enemyName) ? inimigo.name : inimigo.enemyName;

            if (!ArtePorInimigo.TryGetValue(nome, out Escolha escolha))
            {
                semMapa.Add(nome);
                continue;
            }

            Sprite sprite = PrimeiroQuadro(escolha.caminho);
            if (sprite == null)
            {
                semArte.Add($"{nome} → {escolha.caminho}");
                continue;
            }

            // Escolha do autor no Inspector tem precedência: rodar a ferramenta
            // de novo não deve desfazer o que ele trocou à mão. Foi a armadilha
            // do Card Creator, que sobrescrevia assets sem avisar.
            //
            // Retrato e animação são conferidos separadamente porque a animação
            // chegou depois: os onze já tinham retrato, e uma checagem só
            // deixaria todos eles parados para sempre.
            bool temRetrato = inimigo.portrait != null;
            bool temAnimacao = inimigo.animation != null && inimigo.animation.TemQuadros;
            if (temRetrato && temAnimacao && !sobrescrever) continue;

            Color tint = TintPorInimigo.TryGetValue(nome, out Color c) ? c : Color.white;

            Undo.RecordObject(inimigo, "Aplicar arte nos inimigos");

            if (!temRetrato || sobrescrever)
            {
                inimigo.portrait = sprite;
                inimigo.portraitTint = tint;
                inimigo.portraitScale = escolha.escala;
            }

            if (!temAnimacao || sobrescrever)
                inimigo.animation = escolha.PorQuadros
                    ? AnimarPorQuadros(escolha)
                    : Animar(escolha.caminho);

            EditorUtility.SetDirty(inimigo);
            aplicados++;

            // A razão de cada escolha vai para o log: quatro dos onze usam arte
            // emprestada, e quem for revisar precisa saber qual e por quê sem ter
            // de abrir este arquivo.
            mapa.Add($"  {nome} → {System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(escolha.caminho))}"
                   + $" ×{escolha.escala:0.##} — {escolha.porque}");
        }

        AssetDatabase.SaveAssets();

        Debug.Log($"EnemyArt: {aplicados} inimigos receberam arte (de {inimigos.Length}).\n"
                + string.Join("\n", mapa));

        // Silêncio aqui seria pior que ruído: um inimigo sem arte volta a ser
        // uma caixa vazia e ninguém percebe até ver a tela.
        if (semMapa.Count > 0)
            Debug.LogWarning($"EnemyArt: sem entrada na tabela — {string.Join(", ", semMapa)}");
        if (semArte.Count > 0)
            Debug.LogWarning($"EnemyArt: arte não encontrada — {string.Join(", ", semArte)}");
    }

    /// <summary>
    /// O primeiro quadro de um spritesheet já fatiado.
    ///
    /// Os pacotes vêm com <c>spriteMode: Multiple</c> e os quadros nomeados
    /// (<c>Idle_0</c>, <c>Idle_1</c>…), então basta pegar o primeiro sub-ativo —
    /// carregar a textura inteira mostraria a tira de animação lado a lado.
    /// </summary>
    static Sprite PrimeiroQuadro(string caminho)
    {
        Sprite[] quadros = Quadros(caminho);
        return quadros.Length > 0 ? quadros[0] : null;
    }

    /// <summary>
    /// Todos os quadros de um spritesheet fatiado, na ordem em que devem tocar.
    ///
    /// A ordem de <c>LoadAllAssetsAtPath</c> não é garantida, e ordenar por nome
    /// como texto poria <c>Idle_10</c> antes de <c>Idle_2</c> — a animação
    /// tocaria embaralhada, o que numa criatura de dez quadros aparece como
    /// tremor, não como erro.
    /// </summary>
    static Sprite[] Quadros(string caminho)
    {
        if (string.IsNullOrEmpty(caminho)) return new Sprite[0];

        return AssetDatabase.LoadAllAssetsAtPath(caminho)
            .OfType<Sprite>()
            .OrderBy(Indice)
            .ThenBy(s => s.name, System.StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>O número no fim do nome do quadro, ou 0 se não houver.</summary>
    static int Indice(Sprite s)
    {
        if (s == null) return 0;

        int i = s.name.Length;
        while (i > 0 && char.IsDigit(s.name[i - 1])) i--;

        return i < s.name.Length && int.TryParse(s.name.Substring(i), out int n) ? n : 0;
    }

    /// <summary>
    /// Monta o conjunto de animações a partir do spritesheet parado escolhido na
    /// tabela: os outros estados moram ao lado dele, com nome previsível.
    ///
    /// Os dois pacotes organizam de formas diferentes. No <i>Monsters Creatures
    /// Fantasy</i> uma pasta por criatura guarda <c>Idle</c>, <c>Attack1</c>,
    /// <c>Take Hit</c> e <c>Death</c>. Nos slimes é o contrário: uma pasta por
    /// <b>estado</b>, com uma subpasta por cor. Daí os dois palpites para cada
    /// estado — o primeiro que existir no disco vence.
    ///
    /// Estado que não existir fica vazio, e o <see cref="EnemyBody"/> cai no
    /// parado. Nenhum slime do pacote ataca: o que ele tem é um pulo, e é o pulo
    /// que serve de bote.
    /// </summary>
    /// <summary>
    /// Monta as animações de quem vem em quadros soltos, pelas faixas do ReadMe.
    /// </summary>
    static EnemyAnimation AnimarPorQuadros(Escolha escolha)
    {
        var anim = new EnemyAnimation
        {
            idle = Faixa(escolha, escolha.idle),
            attack = Faixa(escolha, escolha.attack),
            hit = Faixa(escolha, escolha.hit),
            death = Faixa(escolha, escolha.death),

            // Estes pacotes olham para a esquerda; os do Monsters, para a
            // direita. Sem isto o bicho luta de costas para a party.
            desenhadaOlhandoParaDireita = false,
        };

        Medir(anim);
        return anim;
    }

    /// <summary>
    /// Os quadros de uma faixa, na ordem.
    ///
    /// Quadro que não abre é contado e denunciado: prefixo escrito com a
    /// maiúscula errada devolve <c>null</c> em silêncio, e o inimigo acaba com
    /// animação vazia sem nada no console — foi o que aconteceu com o Mad Ghost
    /// na primeira tentativa.
    /// </summary>
    static Sprite[] Faixa(Escolha escolha, (int ini, int fim) faixa)
    {
        var quadros = new List<Sprite>();
        int perdidos = 0;

        for (int i = faixa.ini; i <= faixa.fim; i++)
        {
            string caminho = $"{escolha.pastaDeQuadros}/{escolha.prefixo}{i}.png";
            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(caminho);

            if (s != null) quadros.Add(s);
            else perdidos++;
        }

        if (perdidos > 0)
            Debug.LogWarning($"EnemyArt: {perdidos} quadro(s) não encontrado(s) em "
                           + $"{escolha.pastaDeQuadros}/{escolha.prefixo}[{faixa.ini}..{faixa.fim}].png "
                           + "— confira o nome exato do arquivo, inclusive maiúsculas.");

        return quadros.ToArray();
    }

    static EnemyAnimation Animar(string caminhoDoIdle)
    {
        var anim = new EnemyAnimation
        {
            idle = Quadros(caminhoDoIdle),
            attack = Quadros(Vizinho(caminhoDoIdle, "Attack1", "Jump", "Jump Down")),
            hit = Quadros(Vizinho(caminhoDoIdle, "Take Hit", "Hurt", "Hurt")),
            death = Quadros(Vizinho(caminhoDoIdle, "Death", "Death", "Death")),
        };

        if (anim.attack.Length == 0)
            anim.attack = Quadros(Vizinho(caminhoDoIdle, "Attack", "Jump", "Jump Land"));

        Medir(anim);
        return anim;
    }

    /// <summary>
    /// Descobre onde, dentro do quadro, está o desenho de verdade.
    ///
    /// Os quadros são grades de tamanho fixo com a criatura solta no meio e
    /// transparência em volta. Sem esta medida, assentar o quadro no chão deixa
    /// o bicho flutuando, e o tamanho na tela passa a depender de quanta folga o
    /// desenhista deixou em volta — duas criaturas do mesmo porte saem uma o
    /// dobro da outra.
    ///
    /// A conta é feita no primeiro quadro do parado e vale para todos: os
    /// estados de uma mesma criatura compartilham a grade.
    /// </summary>
    static void Medir(EnemyAnimation anim)
    {
        if (anim?.idle == null || anim.idle.Length == 0) return;

        SpriteConteudo.Medida m = SpriteConteudo.Medir(anim.idle[0]);
        if (!m.Valida) return;

        anim.baseVisivel = m.baseVisivel;
        anim.alturaVisivel = m.alturaVisivel;
    }

    /// <summary>
    /// O arquivo do estado vizinho, procurado nas duas convenções de pacote.
    /// Devolve string vazia se nenhuma existir — o estado fica sem quadros.
    /// </summary>
    /// <param name="mesmaPasta">Nome do arquivo irmão no pacote de monstros.</param>
    /// <param name="pastaDoEstado">Pasta do estado no pacote de slimes.</param>
    /// <param name="sufixoDoArquivo">Fim do nome do arquivo no pacote de slimes.</param>
    static string Vizinho(string idle, string mesmaPasta, string pastaDoEstado, string sufixoDoArquivo)
    {
        if (string.IsNullOrEmpty(idle)) return "";

        string pasta = System.IO.Path.GetDirectoryName(idle)?.Replace('\\', '/') ?? "";
        string candidato = $"{pasta}/{mesmaPasta}.png";
        if (Existe(candidato)) return candidato;

        // Pacote de slimes: "Sprites/Idle/Green/Sprite Sheet - Green Idle.png"
        // vira "Sprites/Hurt/Green/Sprite Sheet - Green Hurt.png".
        if (idle.Contains("/Idle/"))
        {
            string trocado = idle
                .Replace("/Idle/", $"/{pastaDoEstado}/")
                .Replace(" Idle.png", $" {sufixoDoArquivo}.png");

            if (Existe(trocado)) return trocado;
        }

        return "";
    }

    static bool Existe(string caminho)
    {
        return !string.IsNullOrEmpty(caminho)
            && AssetDatabase.LoadAssetAtPath<Texture2D>(caminho) != null;
    }
}
#endif
