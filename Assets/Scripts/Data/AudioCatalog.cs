using UnityEngine;

/// <summary>Momento do jogo que define a trilha.</summary>
public enum MusicContext
{
    Hub = 0,       // a guilda entre jornadas
    Journey = 1,   // a estrada
    Combat = 2,    // luta comum
    Boss = 3       // o chefe
}

/// <summary>Som pontual, disparado por uma ação.</summary>
public enum Sfx
{
    Click = 0,
    Hover = 1,
    Attack = 2,     // carta de dano
    Magic = 3,      // carta de magia
    Hurt = 4,       // herói apanhou
    HeroDeath = 5,  // o stinger que a permadeath pede
    Step = 6,       // avançar um trecho da estrada
    Coin = 7        // ouro
}

/// <summary>
/// O que tocar e quando.
///
/// O projeto não tinha um único AudioSource: toda ação acontecia em silêncio,
/// inclusive a morte permanente de um herói — o momento que mais precisa doer.
///
/// Guarda referências ao áudio importado, sem copiá-lo para Resources; mesma
/// escolha do <see cref="PortraitCatalog"/> e do <see cref="BiomeArtCatalog"/>.
/// </summary>
[CreateAssetMenu(fileName = "AudioCatalog", menuName = "Game/Catálogo de Áudio")]
public class AudioCatalog : ScriptableObject
{
    [System.Serializable]
    public class MusicEntry
    {
        public MusicContext context;
        public AudioClip clip;
    }

    [System.Serializable]
    public class SfxEntry
    {
        public Sfx sfx;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
    }

    [Tooltip("Preenchido por Tools ▸ Guild of Legends ▸ Montar Catálogo de Áudio.")]
    public MusicEntry[] musicas;
    public SfxEntry[] efeitos;

    private static AudioCatalog instance;

    public static AudioCatalog Instance
    {
        get
        {
            if (instance == null) instance = Resources.Load<AudioCatalog>("AudioCatalog");
            return instance;
        }
    }

    public AudioClip Musica(MusicContext contexto)
    {
        if (musicas == null) return null;

        foreach (MusicEntry e in musicas)
            if (e != null && e.context == contexto)
                return e.clip;

        return null;
    }

    public bool Efeito(Sfx sfx, out AudioClip clip, out float volume)
    {
        clip = null;
        volume = 1f;
        if (efeitos == null) return false;

        foreach (SfxEntry e in efeitos)
            if (e != null && e.sfx == sfx && e.clip != null)
            {
                clip = e.clip;
                volume = e.volume;
                return true;
            }

        return false;
    }
}
