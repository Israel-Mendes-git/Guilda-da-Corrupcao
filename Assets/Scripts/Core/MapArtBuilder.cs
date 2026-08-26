#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Monta o <see cref="MapArtCatalog"/> a partir do <i>Fantasy Map Assets Pack
/// Lite</i>, importado em <c>Assets/DefaceGames</c>.
///
/// Tools → Guild of Legends → Montar Catálogo de Mapas
///
/// <b>Cada linha é uma escolha, e a razão está escrita nela.</b> O pacote traz
/// 10 montanhas e 10 árvores; qual delas representa a Floresta e qual o Vulcão
/// é decisão de leitura, não de algoritmo. A primeira versão desta ferramenta
/// procurava por palavra-chave (<c>"mountain"</c>) e não achava nada, porque os
/// arquivos se chamam <c>mount1</c>: chute educado erra em silêncio, e o
/// silêncio aqui vira um mapa com regiões invisíveis.
///
/// O que o autor trocar à mão no Inspector do catálogo sobrevive a rodar de
/// novo — a menos que se peça sobrescrever.
/// </summary>
public static class MapArtBuilder
{
    const string CaminhoCatalogo = "Assets/Resources/MapArtCatalog.asset";

    /// <summary>Onde o pacote foi importado. Vazio de sprites = pacote ausente.</summary>
    const string PastaDoPacote = "Assets/DefaceGames";

    /// <summary>Peça do mapa → arquivo do pacote, com a razão da escolha.</summary>
    static readonly (string papel, string arquivo, string porque)[] Fundo =
    {
        ("papel",   "base-paper-2", "papel envelhecido sem manchas fortes — o mapa é desenhado por cima dele"),
        ("borda",   "border 4",     "moldura fina; a grossa come o espaço das sete regiões"),
        ("bussola", "compass",      "rosa dos ventos, canto do mapa"),
        ("guilda",  "open-city solid", "cidade aberta: a guilda é o único lugar habitado que o jogador controla"),
    };

    /// <summary>
    /// Região → símbolo de terreno.
    ///
    /// <b>Vulcão e Tundra são empréstimos declarados.</b> O pacote não tem
    /// vulcão nem gelo: a montanha mais irregular faz o vulcão e o pinheiro faz
    /// a taiga. É a mesma honestidade da tabela do <c>EnemyArt</c> — arte
    /// emprestada anotada como empréstimo, não como escolha.
    /// </summary>
    static readonly (BiomeType biome, string arquivo, string porque)[] PorRegiao =
    {
        (BiomeType.Forest,   "tree5 solid",   "copa densa e larga — a mata fechada"),
        (BiomeType.Mountain, "mount8",        "serra de dois picos: lê-se como cordilheira mesmo pequena"),
        (BiomeType.Swamp,    "waves 1",       "água parada; o pacote não tem brejo"),
        (BiomeType.Desert,   "cactus1 solid", "cacto, a leitura direta"),
        (BiomeType.Tundra,   "pine1 solid",   "conífera — empréstimo: não há gelo no pacote"),
        (BiomeType.Volcano,  "mount5",        "pico único e simétrico, o mais parecido com um cone — "
                                            + "empréstimo: não há vulcão no pacote"),
        (BiomeType.Ruins,    "dungeons",      "entrada de ruína, o símbolo do pacote para lugar abandonado"),
    };

    /// <summary>
    /// Ponto da rota → símbolo. Substitui os emoji que a fonte desenhava.
    ///
    /// O pacote tem <c>battle</c>, <c>boss</c> e <c>chest</c> prontos; descanso
    /// vira <c>home</c> e mercador vira <c>village</c>, que é onde se compra.
    /// A pegada (<c>paw</c>) faz o perigo: no mapa, rastro de bicho é aviso.
    /// </summary>
    /// <summary>
    /// Ponto da rota → o <b>lugar</b> desenhado, e não o ícone do que acontece
    /// nele.
    ///
    /// Escolha do autor: cada parada é uma cena pequena no mapa — a cabana do
    /// descanso, a aldeia do mercador, a boca da caverna. Por isso o repertório
    /// vem de <c>buildings</c>, não de <c>icons</c>: os ícones dizem "aqui há
    /// combate", os prédios dizem "aqui há um lugar".
    ///
    /// Combate e perigo continuam vindo dos ícones — não são lugares, são
    /// encontros, e uma casa desenhada onde há emboscada mentiria.
    /// </summary>
    static readonly (JourneyEventType tipo, string arquivo, string porque)[] PorNo =
    {
        // "Normal" é a maior parte dos pontos da estrada — o dia que não é
        // combate, loja nem descanso. Sem entrada aqui, ele caía no emoji do
        // andarilho e desenhava um círculo azul no meio das ilustrações.
        (JourneyEventType.Normal,   "bridge",           "a ponte: passagem, o que a estrada faz de mais comum"),

        (JourneyEventType.Combat,   "battle",           "espadas cruzadas: encontro, não lugar"),
        (JourneyEventType.Treasure, "chest",            "baú"),
        (JourneyEventType.Trap,     "paw",              "rastro de bicho — no mapa, pegada é aviso"),
        (JourneyEventType.Rest,     "home solid",       "a cabana onde se passa a noite"),
        (JourneyEventType.Shop,     "village solid",    "a aldeia onde se compra"),
        (JourneyEventType.Story,    "cave solid",       "a boca da caverna: um lugar que guarda alguma coisa"),
    };

    const string ArquivoDoChefe = "watchtower solid";

    [MenuItem("Tools/Guild of Legends/Montar Catálogo de Mapas")]
    public static void Montar() => Montar(false);

    /// <param name="sobrescrever">true para trocar escolha feita à mão.</param>
    public static void Montar(bool sobrescrever)
    {
        AjustarImportacao();

        Dictionary<string, Sprite> acervo = Acervo();

        if (acervo.Count == 0)
        {
            Debug.LogWarning($"MapArt: nenhum sprite em {PastaDoPacote}. "
                           + "O 'Fantasy Map Assets Pack Lite' está importado?");
            return;
        }

        MapArtCatalog catalogo = AssetDatabase.LoadAssetAtPath<MapArtCatalog>(CaminhoCatalogo);
        bool novo = catalogo == null;

        if (novo)
        {
            catalogo = ScriptableObject.CreateInstance<MapArtCatalog>();
            System.IO.Directory.CreateDirectory("Assets/Resources");
            AssetDatabase.CreateAsset(catalogo, CaminhoCatalogo);
        }

        Undo.RecordObject(catalogo, "Montar catálogo de mapas");

        var achados = new List<string>();
        var faltando = new List<string>();

        foreach (var (papel, arquivo, porque) in Fundo)
        {
            Sprite s = Pegar(acervo, arquivo, papel, porque, achados, faltando);
            if (s == null) continue;

            switch (papel)
            {
                case "papel":   if (catalogo.papel   == null || sobrescrever) catalogo.papel = s;   break;
                case "borda":   if (catalogo.borda   == null || sobrescrever) catalogo.borda = s;   break;
                case "bussola": if (catalogo.bussola == null || sobrescrever) catalogo.bussola = s; break;
                case "guilda":  if (catalogo.guilda  == null || sobrescrever) catalogo.guilda = s;  break;
            }
        }

        foreach (var (biome, arquivo, porque) in PorRegiao)
        {
            var entrada = catalogo.regioes.FirstOrDefault(e => e != null && e.biome == biome);
            if (entrada == null)
            {
                entrada = new MapArtCatalog.IconeDeRegiao { biome = biome };
                catalogo.regioes.Add(entrada);
            }

            Sprite s = Pegar(acervo, arquivo, BiomeUtil.GetDisplayName(biome), porque, achados, faltando);
            if (s != null && (entrada.icone == null || sobrescrever))
            {
                entrada.icone = s;
                entrada.escala = EscalaPara(s);
            }
        }

        foreach (var (tipo, arquivo, porque) in PorNo)
        {
            var entrada = catalogo.nos.FirstOrDefault(e => e != null && e.tipo == tipo);
            if (entrada == null)
            {
                entrada = new MapArtCatalog.IconeDeNo { tipo = tipo };
                catalogo.nos.Add(entrada);
            }

            Sprite s = Pegar(acervo, arquivo, tipo.ToString(), porque, achados, faltando);
            if (s != null && (entrada.icone == null || sobrescrever))
            {
                entrada.icone = s;
                entrada.escala = EscalaPara(s);
            }
        }

        Sprite chefe = Pegar(acervo, ArquivoDoChefe, "chefe", "a torre no fim da estrada", achados, faltando);
        if (chefe != null && (catalogo.marcoDeChefe == null || sobrescrever))
        {
            catalogo.marcoDeChefe = chefe;
            catalogo.escalaDoChefe = EscalaPara(chefe);
        }

        EditorUtility.SetDirty(catalogo);
        AssetDatabase.SaveAssets();

        Debug.Log($"MapArt: catálogo {(novo ? "criado" : "atualizado")} com {catalogo.Pecas} peça(s), "
                + $"de {acervo.Count} sprite(s) no pacote.\n" + string.Join("\n", achados));

        // Peça faltando vira desenho geométrico na tela, e ninguém percebe qual
        // das sete regiões ficou sem.
        if (faltando.Count > 0)
            Debug.LogWarning("MapArt: arquivo não encontrado para — " + string.Join(", ", faltando));
    }

    /// <summary>
    /// Corta o pacote no tamanho em que ele é usado.
    ///
    /// <b>O pacote vem em 4K.</b> São 59 sprites, a maioria 3840×2160 — o papel,
    /// as bordas, a topografia, a água. Em disco são 57 MB, mas em memória, sem
    /// compressão, passam de <b>500 MB</b>: o Editor engasga e o jogo carrega
    /// meio giga de textura para desenhar um painel de 700 pixels de largura.
    ///
    /// 1024 é folgado para o uso real (o mapa da região ocupa menos que isso na
    /// tela), e desligar mipmap é o certo para interface, que nunca é vista de
    /// longe. Sem isto, a arte que veio melhorar o mapa é a mesma que trava o
    /// Editor de quem a importou.
    ///
    /// Roda uma vez: quem já está em 1024 é pulado, então chamar de novo não
    /// custa nem reimporta nada.
    /// </summary>
    static void AjustarImportacao()
    {
        if (!AssetDatabase.IsValidFolder(PastaDoPacote)) return;

        var ajustados = new List<string>();

        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { PastaDoPacote }))
        {
            string caminho = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(caminho) as TextureImporter;
            if (importer == null) continue;

            bool mexeu = false;

            if (importer.maxTextureSize > 1024) { importer.maxTextureSize = 1024; mexeu = true; }
            if (importer.mipmapEnabled) { importer.mipmapEnabled = false; mexeu = true; }
            if (importer.textureCompression == TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Compressed;
                mexeu = true;
            }

            if (!mexeu) continue;

            importer.SaveAndReimport();
            ajustados.Add(System.IO.Path.GetFileName(caminho));
        }

        if (ajustados.Count > 0)
            Debug.Log($"MapArt: {ajustados.Count} textura(s) do pacote reduzidas a 1024 e comprimidas — "
                    + "em 4K elas custavam mais de 500 MB de memória.");
    }

    /// <summary>
    /// Todos os sprites do pacote, indexados pelo nome do arquivo em minúsculas.
    ///
    /// Só a pasta do pacote: uma busca solta pelo projeto acharia "home" nos
    /// ícones de carta e "battle" nos efeitos, e o mapa nasceria com a arte
    /// errada sem que nada acusasse.
    /// </summary>
    static Dictionary<string, Sprite> Acervo()
    {
        var acervo = new Dictionary<string, Sprite>();

        if (!AssetDatabase.IsValidFolder(PastaDoPacote)) return acervo;

        foreach (string guid in AssetDatabase.FindAssets("t:Sprite", new[] { PastaDoPacote }))
        {
            string caminho = AssetDatabase.GUIDToAssetPath(guid);

            foreach (var sprite in AssetDatabase.LoadAllAssetsAtPath(caminho).OfType<Sprite>())
            {
                string chave = sprite.name.ToLowerInvariant();
                if (!acervo.ContainsKey(chave)) acervo[chave] = sprite;
            }
        }

        return acervo;
    }

    /// <summary>
    /// Quanto este símbolo precisa crescer para ter a mesma presença dos outros.
    ///
    /// O pacote desenha tudo em quadrados de 512, mas com margens muito
    /// diferentes: a serra ocupa metade da altura do quadro, o pico isolado
    /// ocupa um quinto. Desenhados no mesmo retângulo da tela, um lê como
    /// montanha e o outro como um risco.
    ///
    /// O alvo é ocupar 45% do retângulo. O teto de 2,6 existe para o símbolo
    /// mais magro do pacote não invadir o nome da região vizinha.
    /// </summary>
    static float EscalaPara(Sprite sprite)
    {
        SpriteConteudo.Medida m = SpriteConteudo.Medir(sprite);
        if (!m.Valida) return 1f;

        // A maior das duas dimensões manda: preserveAspect encaixa o quadro
        // inteiro, então é a dimensão dominante que decide o tamanho na tela.
        float ocupacao = Mathf.Max(m.alturaVisivel, m.larguraVisivel);
        return Mathf.Clamp(0.45f / Mathf.Max(0.05f, ocupacao), 1f, 2.6f);
    }

    static Sprite Pegar(Dictionary<string, Sprite> acervo, string arquivo, string para, string porque,
                        List<string> achados, List<string> faltando)
    {
        if (acervo.TryGetValue(arquivo.ToLowerInvariant(), out Sprite s))
        {
            achados.Add($"  {para} → {arquivo}  ({porque})");
            return s;
        }

        faltando.Add($"{para} → {arquivo}");
        return null;
    }
}
#endif
