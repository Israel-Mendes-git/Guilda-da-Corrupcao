#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Dá uma cena a cada evento: preenche <see cref="EventData.eventImage"/>, que
/// existia desde sempre e estava vazio nos 25.
///
/// Tools → Guild of Legends → Aplicar Arte nos Eventos
///
/// <b>Por que isto importa mais do que parece.</b> A caixa do evento é a tela
/// que o jogador mais vê numa jornada — dez paradas por missão —, e até aqui ela
/// era um retângulo preto com duas linhas de texto. Trocar de bioma não mudava
/// nada do que se via.
///
/// <b>De onde vem a arte.</b> Do <i>Dwarves and Underground</i>, que o autor
/// tinha baixado e não importado: 71 cenas pintadas, todas escuras e de
/// interior. Foram trazidas <b>27</b>, só as escolhidas nesta tabela, reduzidas
/// a 1024px e gravadas em JPG — o pacote inteiro pesa 273 MB e o que entrou no
/// repositório soma <b>1,5 MB</b>. As cenas são opacas de borda a borda, sem
/// transparência: por isso JPG serve, e por isso <c>preserveAspect</c> pode ser
/// usado sem o risco de encolher o desenho (não há moldura vazia em volta).
///
/// <b>O que o pacote não tem, e como isso está declarado.</b> É um acervo
/// subterrâneo: não há floresta, deserto nem neve. Os eventos desses três
/// biomas usam a cena mais próxima em <i>luz e cor</i> — poeira dourada para a
/// tempestade de areia, gruta de gelo para a tundra, gruta de vegetação para a
/// mata — e cada empréstimo está anotado na coluna do porquê, como no
/// <see cref="MapArtBuilder"/>.
///
/// O campo é serializado: trocar qualquer escolha no Inspector sobrevive a
/// rodar isto de novo, a menos que se marque sobrescrever.
/// </summary>
public static class EventArt
{
    /// <summary>Onde as 27 cenas escolhidas foram gravadas.</summary>
    public const string Pasta = "Assets/Dwarves and Underground";

    /// <summary>
    /// Evento → cena, pelo nome do asset (<c>Resources/Events/*.asset</c>).
    ///
    /// A chave é comparada sem acento e sem caixa (ver <see cref="Chave"/>):
    /// dois dos 25 arquivos têm acento no nome, e o resto foi salvo sem — uma
    /// tabela que dependesse disso quebraria em silêncio no primeiro rename.
    /// </summary>
    static readonly (string evento, string arquivo, string porque)[] Mapa =
    {
        // --- Sem bioma fixo (aparecem em qualquer região) --------------------
        ("Acampamento Noturno", "Mountains 1",
            "porta cavada na rocha ao fim de uma trilha: o abrigo onde o grupo passa a noite"),
        ("Mau Presagio", "Tunnel 14",
            "abóbadas vazias sob uma luz fria — o presságio é a ausência, não a criatura"),
        ("Mercador Errante", "City 7",
            "rua coberta com bancas acesas; é a única cena do pacote onde se compra alguma coisa"),
        ("O Que Restou da Expedicao", "Interior 6",
            "entulho e bagagem largada numa gruta — literalmente o que restou"),
        ("CHEFE - O Guardiao Sem Nome", "Fortress 1",
            "fortaleza escura na névoa: o posto que ele não larga"),

        // --- Floresta --------------------------------------------------------
        ("Alcateia Faminta", "Waterfall",
            "gruta com árvores e água corrente — empréstimo: é a cena mais próxima de mata "
          + "num pacote inteiramente subterrâneo"),
        ("CHEFE - A Coisa da Mata", "Crystals 6",
            "cogumelos gigantes acesos: o fungo do chefe, e a arte é literal"),
        ("Ponte Quebrada", "Bridge 1",
            "ponte de pedra estreita sobre o vão escuro; a leitura direta"),

        // --- Montanha --------------------------------------------------------
        ("Desfiladeiro Estreito", "Mountains 7",
            "garganta que se fecha até virar uma porta — o desfiladeiro sem saída lateral"),
        ("Emboscada na Trilha Alta", "Mountains 5",
            "picos e a trilha exposta no meio deles: o lugar de onde se cai em cima de alguém"),
        ("Mina Abandonada", "Cavern 5",
            "galeria escorada, entulho e uma lanterna esquecida; a arte é literal"),
        ("CHEFE - O Gigante de Pedra", "Tunnel 17",
            "figura de pedra sentada, do tamanho da sala — a melhor imagem do chefe em todo o acervo, "
          + "e a única que o retrato do inimigo ainda não tem"),

        // --- Pântano ---------------------------------------------------------
        ("Altar Afundado", "Interior 8",
            "nicho aceso de vermelho no fundo de uma câmara: o altar que a corrupção marcou"),
        ("Nevoa Putrida", "Tunnel 5",
            "arcadas tomadas por um verde parado — a névoa é a cena inteira"),
        ("O Que Vive na Agua Parada", "Lake 1",
            "água sem corrente e margem de musgo; o que vive nela não precisa aparecer"),
        ("CHEFE - O Afogado", "Lake 2",
            "a mesma água, agora funda e sem margem — o chefe é separado do evento comum pela profundidade"),

        // --- Deserto ---------------------------------------------------------
        ("Caravana de Ossos", "Mountains 6",
            "estrada entre marcos de pedra na poeira — empréstimo: o pacote não tem duna, "
          + "e o que sobra é a cor de osso e a névoa seca"),
        ("Tempestade de Areia", "Mountains 2",
            "a poeira dourada apaga o caminho e o horizonte — empréstimo, mas é exatamente "
          + "o que uma tempestade de areia faz com a vista"),

        // --- Tundra ----------------------------------------------------------
        ("Silencio Branco", "Cavern 7",
            "gruta de gelo azul-branco: o silêncio branco tem cor"),
        ("Uivos na Nevasca", "Cavern 3",
            "boca de caverna aberta para uma ventania branca — empréstimo: não há neve no pacote, "
          + "há a brancura e o vão por onde o som entra"),

        // --- Vulcão ----------------------------------------------------------
        ("Ninho de Cinzas", "Molten 2",
            "ilha de rocha no meio de um lago de lava: o ninho fica onde ninguém chega"),
        ("Veios de Enxofre", "Molten 3",
            "veios amarelos acesos sob as arcadas — a cor do enxofre, e a arte é literal"),

        // --- Ruínas ----------------------------------------------------------
        ("Guarda que Nao Foi Dispensada", "Tunnel 4",
            "cripta fria com os nichos ainda acesos: alguém continua mantendo a luz"),
        ("Salao dos Idolos", "Tunnel 13",
            "estela de pé entre velas e medalhões talhados; a arte é literal"),
        ("CHEFE - O Bibliotecario Cego", "Interior 7",
            "salão de janelas altas, bancadas e estantes — a biblioteca que ele guarda sem ver"),
    };

    [MenuItem("Tools/Guild of Legends/Aplicar Arte nos Eventos")]
    public static void Aplicar() => Aplicar(false);

    /// <param name="sobrescrever">true para trocar arte já escolhida à mão.</param>
    public static void Aplicar(bool sobrescrever)
    {
        AjustarImportacao();

        EventData[] eventos = Resources.LoadAll<EventData>("Events");
        if (eventos.Length == 0)
        {
            Debug.LogError("EventArt: nenhum evento em Resources/Events.");
            return;
        }

        Dictionary<string, Sprite> acervo = Acervo();
        if (acervo.Count == 0)
        {
            Debug.LogWarning($"EventArt: nenhuma cena em {Pasta}. "
                           + "As 27 imagens do 'Dwarves and Underground' estão no projeto?");
            return;
        }

        var tabela = Mapa.ToDictionary(m => Chave(m.evento), m => (m.arquivo, m.porque));

        int aplicados = 0;
        var feitos = new List<string>();
        var semMapa = new List<string>();
        var semArte = new List<string>();
        var jaTinham = new List<string>();

        foreach (EventData evento in eventos)
        {
            if (!tabela.TryGetValue(Chave(evento.name), out var escolha))
            {
                semMapa.Add(evento.name);
                continue;
            }

            if (!acervo.TryGetValue(escolha.arquivo.ToLowerInvariant(), out Sprite cena))
            {
                semArte.Add($"{evento.name} → {escolha.arquivo}");
                continue;
            }

            // Escolha do autor no Inspector tem precedência. É a armadilha do
            // Card Creator antigo, que regravava assets e desfazia correções
            // sem avisar — aqui a ferramenta só preenche o que está vazio.
            if (evento.eventImage != null && !sobrescrever)
            {
                jaTinham.Add(evento.name);
                continue;
            }

            Undo.RecordObject(evento, "Aplicar arte nos eventos");
            evento.eventImage = cena;
            EditorUtility.SetDirty(evento);
            aplicados++;

            feitos.Add($"  {evento.name} → {escolha.arquivo} — {escolha.porque}");
        }

        AssetDatabase.SaveAssets();

        Debug.Log($"EventArt: {aplicados} evento(s) receberam cena (de {eventos.Length}), "
                + $"{jaTinham.Count} já tinham a sua.\n" + string.Join("\n", feitos));

        // Silêncio aqui seria pior que ruído: evento sem cena volta a ser texto
        // sobre preto e ninguém percebe até rodar a jornada inteira.
        if (semMapa.Count > 0)
            Debug.LogWarning($"EventArt: sem entrada na tabela — {string.Join(", ", semMapa)}. "
                           + "Esses eventos ficam sem ilustração.");
        if (semArte.Count > 0)
            Debug.LogWarning($"EventArt: arquivo não encontrado em {Pasta} — {string.Join(", ", semArte)}");
    }

    /// <summary>
    /// Faz o Unity ler os JPG como sprite, e no tamanho em que eles são usados.
    ///
    /// JPG chega ao projeto como <c>Texture2D</c> comum: sem isto,
    /// <c>LoadAssetAtPath&lt;Sprite&gt;</c> devolve <c>null</c> nos 27 arquivos e
    /// a ferramenta acusaria "arquivo não encontrado" sobre arquivos que estão
    /// ali. Os limites são os mesmos do <see cref="MapArtBuilder"/>, e pela mesma
    /// razão: textura de interface nunca é vista de longe, então mipmap só custa
    /// memória.
    ///
    /// Roda uma vez — quem já está ajustado é pulado, e chamar de novo não
    /// reimporta nada.
    /// </summary>
    public static void AjustarImportacao()
    {
        if (!AssetDatabase.IsValidFolder(Pasta)) return;

        var ajustados = new List<string>();

        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Pasta }))
        {
            string caminho = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(caminho) as TextureImporter;
            if (importer == null) continue;

            bool mexeu = false;

            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                mexeu = true;
            }

            if (importer.maxTextureSize > 1024) { importer.maxTextureSize = 1024; mexeu = true; }
            if (importer.mipmapEnabled) { importer.mipmapEnabled = false; mexeu = true; }

            if (!mexeu) continue;

            importer.SaveAndReimport();
            ajustados.Add(System.IO.Path.GetFileName(caminho));
        }

        if (ajustados.Count > 0)
            Debug.Log($"EventArt: {ajustados.Count} cena(s) reimportada(s) como sprite de interface.");
    }

    /// <summary>
    /// As cenas da pasta, indexadas pelo nome do arquivo em minúsculas.
    ///
    /// Só esta pasta: uma busca solta pelo projeto acharia "Bridge" nos ícones do
    /// mapa e "Lake" em qualquer cenário, e o evento nasceria com a arte errada
    /// sem que nada acusasse — é a mesma precaução do <see cref="MapArtBuilder"/>.
    /// </summary>
    static Dictionary<string, Sprite> Acervo()
    {
        var acervo = new Dictionary<string, Sprite>();

        if (!AssetDatabase.IsValidFolder(Pasta)) return acervo;

        foreach (string guid in AssetDatabase.FindAssets("t:Sprite", new[] { Pasta }))
        {
            string caminho = AssetDatabase.GUIDToAssetPath(guid);

            foreach (Sprite s in AssetDatabase.LoadAllAssetsAtPath(caminho).OfType<Sprite>())
            {
                string chave = s.name.ToLowerInvariant();
                if (!acervo.ContainsKey(chave)) acervo[chave] = s;
            }
        }

        return acervo;
    }

    /// <summary>
    /// Nome comparável: sem acento, sem caixa, sem espaço sobrando.
    ///
    /// Os 25 arquivos de evento foram salvos quase todos sem acento
    /// (<c>Nevoa Putrida</c>, <c>Salao dos Idolos</c>) e dois com
    /// (<c>Água Parada</c>, <c>Não Foi Dispensada</c>). Comparar o texto cru
    /// deixaria esses dois de fora da tabela, e o aviso diria "sem entrada"
    /// sobre linhas que existem.
    /// </summary>
    static string Chave(string nome)
    {
        if (string.IsNullOrEmpty(nome)) return "";

        string decomposto = nome.Trim().ToLowerInvariant()
            .Normalize(System.Text.NormalizationForm.FormD);

        var limpo = new System.Text.StringBuilder(decomposto.Length);

        foreach (char c in decomposto)
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                != System.Globalization.UnicodeCategory.NonSpacingMark)
                limpo.Append(c);

        return limpo.ToString();
    }
}
#endif
