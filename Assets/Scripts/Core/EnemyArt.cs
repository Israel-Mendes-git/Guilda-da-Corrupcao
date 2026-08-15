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

    class Escolha
    {
        public readonly string caminho;   // arquivo do spritesheet
        public readonly float escala;     // chefe grande é linguagem de Darkest Dungeon
        public readonly string porque;    // por que esta criatura, e não outra

        public Escolha(string caminho, string porque, float escala = 1f)
        {
            this.caminho = caminho;
            this.porque = porque;
            this.escala = escala;
        }
    }

    /// <summary>
    /// Inimigo → criatura. A chave é o <c>enemyName</c> do asset.
    /// </summary>
    static readonly Dictionary<string, Escolha> ArtePorInimigo = new Dictionary<string, Escolha>
    {
        // --- Comuns ---------------------------------------------------------
        { "Aranha da Copa",
          new Escolha($"{Monstros}/Flying eye/Flight.png",
                      "criatura que espreita do alto — o olho voador é o que mais se aproxima",
                      2.0f) },

        { "Carniçal",
          new Escolha($"{Monstros}/Skeleton/Idle.png",
                      "morto-vivo armado; a arte é literal",
                      2.0f) },

        { "Estátua Desperta",
          new Escolha($"{Monstros}/Skeleton/Idle.png",
                      "esqueleto emprestado, tingido de pedra e maior — figura de pedra que empunha arma",
                      2.3f) },

        { "Lobo Esfomeado",
          new Escolha($"{Monstros}/Goblin/Idle.png",
                      "não há canídeo em pacote nenhum; o goblin faminto é o predador pequeno disponível",
                      2.0f) },

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
          new Escolha($"{Slimes}/Blue/Sprite Sheet - Blue Idle.png",
                      "água que engole — o slime azul, em tamanho de chefe",
                      3.4f) },

        { "O Bibliotecário Cego",
          new Escolha($"{Monstros}/Flying eye/Flight.png",
                      "o olho que tudo vê para quem é cego: a ironia é o desenho",
                      2.8f) },

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
    static readonly Dictionary<string, Color> TintPorInimigo = new Dictionary<string, Color>
    {
        { "Estátua Desperta",     new Color(0.62f, 0.63f, 0.66f) },
        { "Lobo Esfomeado",       new Color(0.72f, 0.66f, 0.52f) },
        { "Sanguessuga Gigante",  new Color(0.72f, 0.78f, 0.60f) },
        { "O Afogado",            new Color(0.55f, 0.72f, 0.85f) },
        { "O Gigante de Pedra",   new Color(0.55f, 0.56f, 0.60f) },
        { "O Guardião Sem Nome",  new Color(0.45f, 0.42f, 0.52f) },
        { "O Bibliotecário Cego", new Color(0.80f, 0.75f, 0.55f) },
        { "A Coisa da Mata",      new Color(0.70f, 0.80f, 0.55f) }
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
            if (inimigo.portrait != null && !sobrescrever) continue;

            Color tint = TintPorInimigo.TryGetValue(nome, out Color c) ? c : Color.white;

            Undo.RecordObject(inimigo, "Aplicar arte nos inimigos");
            inimigo.portrait = sprite;
            inimigo.portraitTint = tint;
            inimigo.portraitScale = escolha.escala;
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
        var sprites = AssetDatabase.LoadAllAssetsAtPath(caminho).OfType<Sprite>().ToList();
        if (sprites.Count == 0) return null;

        // A ordem de LoadAllAssetsAtPath não é garantida; o nome termina no
        // índice do quadro, e o quadro 0 é a pose parada de que precisamos.
        return sprites.OrderBy(s => s.name, System.StringComparer.Ordinal).First();
    }
}
#endif
