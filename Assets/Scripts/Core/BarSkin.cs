#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Veste as barras de HP, estresse e XP com a arte do kit Bloodlines.
///
/// Tools → Guild of Legends → Aplicar Kit Visual nas Barras
///
/// Até aqui as barras eram retângulos de cor sólida — dívida registrada no
/// ROADMAP desde a auditoria ("HP e estresse ainda são retângulos chapados").
/// A arte para resolver isso já estava no disco desde que o kit foi importado,
/// em Textures/Progress_Bar, e nunca tinha sido usada.
///
/// O molde que o projeto usa para toda barra é sempre o mesmo: um objeto de
/// trilho com um filho chamado "Fill" por cima, e é esse par que esta ferramenta
/// procura — na cena aberta e nos prefabs que contêm barras.
/// </summary>
public static class BarSkin
{
    const string Pasta = "Assets/Alebardium/Bloodlines UI/Textures/Progress_Bar";

    const string TrilhoPath = Pasta + "/Rectangle/Progress_Bar_Rectangle_empty_v1.png";
    const string FillPath   = Pasta + "/Rectangle/Progress_Bar_Rectangle_full_v1.png";

    /// <summary>
    /// Prefabs que contêm barras. A cena entra por fora, porque nela a barra
    /// mora no molde de linha do balanço da jornada.
    /// </summary>
    static readonly string[] PrefabsComBarra =
    {
        "Assets/Prefabs/UI/EnemyCardPrefab.prefab",
        "Assets/Prefabs/UI/PartyStatusPrefab.prefab"
    };

    /// <summary>
    /// O sprite de preenchimento é vermelho. Onde o código tinge a barra por
    /// estado (estresse subindo, herói na Beira da Morte), tingir vermelho com
    /// vermelho apaga a informação — então a barra de estresse recebe branco e
    /// deixa o tom por conta de quem a atualiza em runtime.
    /// </summary>
    static readonly Dictionary<string, Color> CorPorBarra = new Dictionary<string, Color>
    {
        { "HPBar",     Color.white },
        { "StressBar", Color.white }
    };

    [MenuItem("Tools/Guild of Legends/Aplicar Kit Visual nas Barras")]
    public static void Aplicar()
    {
        Sprite trilho = CarregarSprite(TrilhoPath);
        Sprite fill = CarregarSprite(FillPath);

        if (trilho == null || fill == null)
        {
            Debug.LogError($"BarSkin: sprites não encontrados em {Pasta}. O kit Bloodlines está no projeto?");
            return;
        }

        int naCena = 0, emPrefabs = 0;

        // --- Cena aberta ---
        var raizes = EditorSceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var raiz in raizes)
            naCena += VestirRecursivo(raiz.transform, trilho, fill, true);

        if (naCena > 0)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }

        // --- Prefabs ---
        foreach (string caminho in PrefabsComBarra)
        {
            GameObject conteudo = PrefabUtility.LoadPrefabContents(caminho);
            if (conteudo == null)
            {
                Debug.LogWarning($"BarSkin: prefab não encontrado — {caminho}");
                continue;
            }

            int n = VestirRecursivo(conteudo.transform, trilho, fill, false);
            if (n > 0) PrefabUtility.SaveAsPrefabAsset(conteudo, caminho);
            PrefabUtility.UnloadPrefabContents(conteudo);

            emPrefabs += n;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"BarSkin: {naCena + emPrefabs} barras vestidas ({naCena} na cena, {emPrefabs} em prefabs).");
    }

    /// <summary>
    /// Uma barra é reconhecida pelo par trilho + filho "Fill" — o mesmo molde que
    /// GuildSceneSetup cria e que JourneyManager e CombatManager procuram por
    /// caminho ("HPBar/Fill"). Procurar pelo par, e não pelo nome, evita vestir
    /// um objeto que só por acaso se chame algo terminado em "Bar".
    /// </summary>
    static int VestirRecursivo(Transform t, Sprite trilho, Sprite fill, bool registrarUndo)
    {
        int vestidas = 0;

        Transform preenchimento = t.Find("Fill");
        Image imagemTrilho = t.GetComponent<Image>();
        Image imagemFill = preenchimento != null ? preenchimento.GetComponent<Image>() : null;

        // A barra de XP fica de fora: os cinco preenchimentos do kit são vermelhos
        // (R≈130, G≈20), e o Image multiplica sprite por cor — o dourado do XP e o
        // verde da promoção saíam como vermelho escuro, iguais à barra de vida
        // logo ao lado. Ela recebe o branco liso do UIUtil em execução, onde a cor
        // que o JourneyResultUI escolhe é a que aparece.
        bool ehXp = t.name == "XpBar";

        if (imagemTrilho != null && imagemFill != null && imagemFill.type == Image.Type.Filled && !ehXp)
        {
            if (registrarUndo)
            {
                Undo.RecordObject(imagemTrilho, "Aplicar kit nas barras");
                Undo.RecordObject(imagemFill, "Aplicar kit nas barras");
            }

            imagemTrilho.sprite = trilho;
            imagemTrilho.type = Image.Type.Simple;
            imagemTrilho.color = Color.white;

            imagemFill.sprite = fill;
            imagemFill.type = Image.Type.Filled;
            imagemFill.fillMethod = Image.FillMethod.Horizontal;
            imagemFill.fillOrigin = (int)Image.OriginHorizontal.Left;

            // Preserva a cor de quem já tinha uma escolhida de propósito; as
            // conhecidas voltam para branco, para o sprite aparecer como é.
            if (CorPorBarra.TryGetValue(t.name, out Color cor))
                imagemFill.color = cor;

            EditorUtility.SetDirty(imagemTrilho);
            EditorUtility.SetDirty(imagemFill);
            vestidas++;
        }

        foreach (Transform filho in t)
            vestidas += VestirRecursivo(filho, trilho, fill, registrarUndo);

        return vestidas;
    }

    /// <summary>
    /// As texturas do kit estão em modo Multiple, então LoadAssetAtPath&lt;Sprite&gt;
    /// não devolve nada: é preciso pegar o primeiro sub-ativo do tipo Sprite.
    /// </summary>
    static Sprite CarregarSprite(string caminho)
    {
        return AssetDatabase.LoadAllAssetsAtPath(caminho).OfType<Sprite>().FirstOrDefault();
    }
}

/// <summary>
/// Dispara a montagem da cena e a aplicação do kit por arquivo, como o smoke test
/// e o Play Mode já fazem — é o que permite rodar sem abrir o menu do Editor.
/// </summary>
[InitializeOnLoad]
public static class SceneToolsTriggerWatcher
{
    const string SetupTrigger = "RunSceneSetup.trigger";
    const string BarSkinTrigger = "RunBarSkin.trigger";
    const string CardArtTrigger = "RunCardArt.trigger";
    const string PortraitTrigger = "RunPortraitCatalog.trigger";
    const string BiomeArtTrigger = "RunBiomeArt.trigger";
    const string UiPrefabsTrigger = "RunUiSkinPrefabs.trigger";
    const string HeroPanelTrigger = "RunHeroPanelSkin.trigger";
    const string AudioTrigger = "RunAudioCatalog.trigger";
    const string PartyCardTrigger = "RunPartyCardSkin.trigger";
    const string CardFrameTrigger = "RunCardFrameSkin.trigger";
    const string MenuSetupTrigger = "RunMenuSetup.trigger";
    const string EnemyArtTrigger = "RunEnemyArt.trigger";
    const string MapArtTrigger = "RunMapArt.trigger";
    const string EventBalanceTrigger = "RunEventBalance.trigger";
    const string ItemArtTrigger = "RunItemArt.trigger";
    const string CardCreatorTrigger = "RunCardCreator.trigger";
    const string EventArtTrigger = "RunEventArt.trigger";
    const string GameplayAuditTrigger = "RunGameplayAudit.trigger";
    const string GuildArtTrigger = "RunGuildArt.trigger";
    static double nextCheck;

    static SceneToolsTriggerWatcher()
    {
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if (EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + 1.0;

        // Montar cena durante o Play Mode estoura com "This cannot be used during
        // play mode" — a mesma guarda dos outros gatilhos.
        if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

        Consumir(SetupTrigger, () =>
        {
            // Abre a cena do jogo antes de montar. Automação não deve depender
            // de qual cena o autor deixou aberta — e o teste de Play Mode agora
            // termina no título, então o gatilho pegava a cena errada.
            if (EditorSceneManager.GetActiveScene().path != MenuSceneSetup.CaminhoDoJogo)
                EditorSceneManager.OpenScene(MenuSceneSetup.CaminhoDoJogo);

            GuildSceneSetup.Setup(false);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        });

        Consumir(MenuSetupTrigger, () => MenuSceneSetup.Montar(false));

        Consumir(BarSkinTrigger, BarSkin.Aplicar);
        Consumir(CardArtTrigger, CardArt.Aplicar);
        Consumir(EnemyArtTrigger, () => EnemyArt.Aplicar(true));

        // Sem sobrescrever: a escolha por palavra-chave é um chute educado, e o
        // que for corrigido à mão no catálogo tem de sobreviver a rodar de novo.
        Consumir(MapArtTrigger, () => MapArtBuilder.Montar(false));
        Consumir(EventBalanceTrigger, () => EventBalance.Ajustar(false));
        Consumir(ItemArtTrigger, ItemArt.Conferir);

        // Semeia as cartas que faltam. Nunca sobrescreve carta existente — é a
        // mesma precaução do catálogo de mapas, e a razão é a armadilha do Card
        // Creator antigo, que regravava valores corrigidos à mão sem avisar.
        Consumir(CardCreatorTrigger, CardCreator.Semear);

        // Sem sobrescrever, pela mesma razão do catálogo de mapas: a escolha da
        // cena é um chute por bioma e tema, e o que for trocado no Inspector tem
        // de sobreviver.
        Consumir(EventArtTrigger, () => EventArt.Aplicar(false));
        Consumir(GameplayAuditTrigger, GameplayAudit.Rodar);
        Consumir(GuildArtTrigger, GuildArt.Aplicar);
        Consumir(PortraitTrigger, PortraitCatalogBuilder.Montar);
        Consumir(BiomeArtTrigger, BiomeArtBuilder.Montar);
        Consumir(UiPrefabsTrigger, UiSkinPrefabs.Aplicar);
        Consumir(HeroPanelTrigger, HeroPanelSkin.Vestir);
        Consumir(AudioTrigger, AudioCatalogBuilder.Montar);
        Consumir(PartyCardTrigger, PartyCardSkin.Vestir);
        Consumir(CardFrameTrigger, CardFrameSkin.Vestir);
    }

    static void Consumir(string arquivo, System.Action acao)
    {
        string path = System.IO.Path.Combine(PlayModeTestLauncher.ProjectRoot, arquivo);
        if (!System.IO.File.Exists(path)) return;

        try { System.IO.File.Delete(path); }
        catch { return; }

        Debug.Log($"Trigger detectado — {arquivo}");
        acao();
    }
}
#endif
