using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// As opções do jogador, aplicadas ao jogo.
///
/// O <see cref="PlayerProfile"/> guarda os números; esta classe é quem os põe
/// para funcionar — volume no <see cref="GameAudio"/>, resolução e tela cheia no
/// <c>Screen</c>. Separado de propósito: gravar uma preferência e obedecê-la são
/// duas coisas, e até aqui o jogo não fazia nenhuma das duas — o
/// <c>volumeMusica</c> do GameAudio existia no Inspector sem que nada o
/// controlasse.
///
/// Aplica sozinho no início da sessão, antes de qualquer cena: sem isso, a
/// primeira faixa de música tocaria no volume de fábrica até o jogador abrir as
/// opções.
/// </summary>
public static class GameSettings
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void AplicarTudo()
    {
        AplicarAudio();
        AplicarVideo();
    }

    #region Áudio

    public static float VolumeMusica
    {
        get => PlayerProfile.Dados.settings.musicVolume;
        set
        {
            PlayerProfile.Dados.settings.musicVolume = Mathf.Clamp01(value);
            AplicarAudio();
        }
    }

    public static float VolumeEfeitos
    {
        get => PlayerProfile.Dados.settings.sfxVolume;
        set
        {
            PlayerProfile.Dados.settings.sfxVolume = Mathf.Clamp01(value);
            AplicarAudio();
        }
    }

    /// <summary>
    /// Empurra os volumes para o tocador. Não força o <see cref="GameAudio"/> a
    /// existir: ele nasce sozinho quando alguém pede som, e criá-lo aqui só para
    /// ajustar volume ligaria um AudioSource numa tela que talvez seja muda.
    /// </summary>
    public static void AplicarAudio()
    {
        var settings = PlayerProfile.Dados.settings;

        if (GameAudio.Existe)
            GameAudio.Instance.AplicarVolumes(settings.musicVolume, settings.sfxVolume);
    }

    #endregion

    #region Vídeo

    public static bool TelaCheia
    {
        get => PlayerProfile.Dados.settings.fullscreen;
        set
        {
            PlayerProfile.Dados.settings.fullscreen = value;
            AplicarVideo();
        }
    }

    /// <summary>
    /// As resoluções oferecidas: as do monitor, sem repetir a mesma medida em
    /// taxas de atualização diferentes — a lista do Unity traz 60/120/144 Hz como
    /// entradas separadas, e três linhas "1920 × 1080" seguidas não ajudam
    /// ninguém a escolher.
    /// </summary>
    public static List<Vector2Int> ResolucoesDisponiveis()
    {
        var lista = new List<Vector2Int>();

        foreach (var r in Screen.resolutions)
        {
            var medida = new Vector2Int(r.width, r.height);
            if (!lista.Contains(medida)) lista.Add(medida);
        }

        // Sem monitor (build de servidor, teste em lote) a lista vem vazia — a
        // tela de opções precisa de ao menos uma linha para não ficar em branco.
        if (lista.Count == 0) lista.Add(new Vector2Int(Screen.width, Screen.height));

        return lista;
    }

    public static Vector2Int ResolucaoAtual
    {
        get
        {
            var s = PlayerProfile.Dados.settings;
            if (s.resolutionWidth > 0 && s.resolutionHeight > 0)
                return new Vector2Int(s.resolutionWidth, s.resolutionHeight);

            return new Vector2Int(Screen.width, Screen.height);
        }
    }

    public static void DefinirResolucao(Vector2Int medida)
    {
        var s = PlayerProfile.Dados.settings;
        s.resolutionWidth = medida.x;
        s.resolutionHeight = medida.y;

        AplicarVideo();
    }

    public static void AplicarVideo()
    {
        var s = PlayerProfile.Dados.settings;

        // No Editor, mexer em Screen.SetResolution redimensiona a Game view e
        // atrapalha quem está testando — a opção existe para a build.
#if !UNITY_EDITOR
        Vector2Int medida = ResolucaoAtual;
        if (medida.x > 0 && medida.y > 0)
            Screen.SetResolution(medida.x, medida.y, s.fullscreen);
        else
            Screen.fullScreen = s.fullscreen;
#endif
    }

    #endregion

    /// <summary>Grava o que foi mexido. As telas chamam ao fechar.</summary>
    public static void Salvar() => PlayerProfile.Salvar();

    /// <summary>Devolve as opções ao padrão de fábrica.</summary>
    public static void Restaurar()
    {
        PlayerProfile.Dados.settings = new SettingsSave();
        AplicarTudo();
        Salvar();
    }
}
