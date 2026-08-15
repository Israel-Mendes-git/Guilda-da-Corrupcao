using System.Collections;
using UnityEngine;

/// <summary>
/// A trilha e os efeitos do jogo.
///
/// Nasce sozinho na primeira vez que alguém pede som — nenhuma cena precisa ser
/// editada para o áudio existir, o que importa aqui porque a cena é montada por
/// código e um objeto a mais no setup é mais uma coisa para sair de sincronia.
///
/// Duas fontes separadas: música em laço, que troca com fade para a mudança de
/// contexto não estalar, e efeitos em one-shot por cima. Sem isso, um SFX cortaria
/// a trilha toda vez.
/// </summary>
public class GameAudio : MonoBehaviour
{
    private static GameAudio instance;

    public static GameAudio Instance
    {
        get
        {
            if (instance != null) return instance;

            // Fora do Play Mode não há tocador — e tentar criar um estoura:
            // DontDestroyOnLoad é proibido em script de Editor. O smoke test
            // simula 200 jornadas em edit mode, e cada morte de herói pede o
            // stinger da permadeath; sem esta guarda, a primeira baixa derruba
            // a bateria inteira de balanceamento com uma exceção de áudio.
            if (!Application.isPlaying) return null;

            var go = new GameObject("~GameAudio");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<GameAudio>();
            return instance;
        }
    }

    /// <summary>
    /// Já existe tocador? Perguntar por <see cref="Instance"/> o criaria, e as
    /// opções não devem ligar um AudioSource só para ajustar um volume que
    /// ninguém está ouvindo ainda.
    /// </summary>
    public static bool Existe => instance != null;

    private AudioSource musica;
    private AudioSource efeitos;
    private MusicContext atual = (MusicContext)(-1);
    private Coroutine troca;

    [Range(0f, 1f)] public float volumeMusica = 0.45f;
    [Range(0f, 1f)] public float volumeEfeitos = 0.85f;
    public float fadeDuration = 1.2f;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        musica = gameObject.AddComponent<AudioSource>();
        musica.loop = true;
        musica.playOnAwake = false;
        musica.volume = 0f;

        efeitos = gameObject.AddComponent<AudioSource>();
        efeitos.loop = false;
        efeitos.playOnAwake = false;
        efeitos.volume = volumeEfeitos;

        // O tocador pode nascer bem depois das opções terem sido aplicadas — ele
        // só existe quando alguém pede som. Buscar os volumes aqui é o que
        // impede a primeira faixa de tocar no valor de fábrica.
        AplicarVolumes(GameSettings.VolumeMusica, GameSettings.VolumeEfeitos);
    }

    /// <summary>
    /// Ajusta os volumes vindos das opções, sem cortar o que está tocando — é o
    /// que permite o jogador ouvir o efeito de arrastar a barrinha.
    /// </summary>
    public void AplicarVolumes(float musicaVol, float efeitosVol)
    {
        volumeMusica = Mathf.Clamp01(musicaVol);
        volumeEfeitos = Mathf.Clamp01(efeitosVol);

        // Enquanto há fade em curso, quem manda no volume é a corrotina: escrever
        // por cima aqui daria um salto no meio da transição.
        if (musica != null && troca == null) musica.volume = volumeMusica;
        if (efeitos != null) efeitos.volume = volumeEfeitos;
    }

    /// <summary>
    /// Troca a trilha. Repetir o mesmo contexto não reinicia a faixa — sem essa
    /// guarda, voltar de uma sala para o hub cortaria a música toda vez.
    /// </summary>
    public static void Tocar(MusicContext contexto)
    {
        if (AudioCatalog.Instance == null) return;

        GameAudio a = Instance;
        if (a == null || a.atual == contexto) return;

        AudioClip clip = AudioCatalog.Instance.Musica(contexto);
        if (clip == null) return;

        a.atual = contexto;

        if (a.troca != null) a.StopCoroutine(a.troca);
        a.troca = a.StartCoroutine(a.TrocarMusica(clip));
    }

    IEnumerator TrocarMusica(AudioClip novo)
    {
        // Fade out do que está tocando.
        if (musica.isPlaying)
        {
            float v0 = musica.volume;
            for (float t = 0; t < fadeDuration * 0.5f; t += Time.unscaledDeltaTime)
            {
                musica.volume = Mathf.Lerp(v0, 0f, t / (fadeDuration * 0.5f));
                yield return null;
            }
        }

        musica.clip = novo;
        musica.volume = 0f;
        musica.Play();

        for (float t = 0; t < fadeDuration * 0.5f; t += Time.unscaledDeltaTime)
        {
            musica.volume = Mathf.Lerp(0f, volumeMusica, t / (fadeDuration * 0.5f));
            yield return null;
        }

        musica.volume = volumeMusica;
        troca = null;
    }

    /// <summary>Um som pontual. Silencioso e inofensivo se não houver clipe.</summary>
    public static void Efeito(Sfx sfx)
    {
        if (AudioCatalog.Instance == null) return;

        if (!AudioCatalog.Instance.Efeito(sfx, out AudioClip clip, out float volume)) return;

        GameAudio a = Instance;
        if (a == null) return;

        a.efeitos.PlayOneShot(clip, volume * a.volumeEfeitos);
    }

    /// <summary>Silêncio — para telas que pedem peso, como o fim de uma run.</summary>
    public static void Parar()
    {
        if (instance == null) return;

        instance.atual = (MusicContext)(-1);
        if (instance.troca != null) instance.StopCoroutine(instance.troca);
        instance.musica.Stop();
    }
}
