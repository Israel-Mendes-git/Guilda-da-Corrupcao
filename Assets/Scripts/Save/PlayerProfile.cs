using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// O que pertence ao jogador, não à partida.
///
/// Memórias, destraves comprados, recordes e as opções de áudio e vídeo vivem
/// aqui — num arquivo só, fora dos slots. É a divisão que dá sentido à
/// meta-progressão: apagar um save não apaga o que as runs anteriores custaram
/// a conquistar, e trocar de slot não faz o volume da música mudar.
///
/// Substitui o PlayerPrefs que a Fase 3 usou como provisório. A migração é feita
/// uma vez, no primeiro carregamento, para que quem já jogou não perca as
/// memórias que tinha.
/// </summary>
public static class PlayerProfile
{
    const string Arquivo = "profile.json";

    static ProfileData dados;

    public static ProfileData Dados
    {
        get
        {
            if (dados == null) Carregar();
            return dados;
        }
    }

    public static string Caminho => Path.Combine(Application.persistentDataPath, Arquivo);

    static void Carregar()
    {
        dados = null;

        try
        {
            if (File.Exists(Caminho))
                dados = JsonUtility.FromJson<ProfileData>(File.ReadAllText(Caminho));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"PlayerProfile: perfil corrompido, começando limpo — {e.Message}");
        }

        if (dados == null)
        {
            dados = new ProfileData();
            MigrarDoPlayerPrefs(dados);
            Salvar();
            return;
        }

        MigrarMoeda(dados);
    }

    /// <summary>
    /// Passa o saldo do campo antigo (<c>relics</c>) para o novo (<c>memories</c>).
    ///
    /// Sem isto, todo perfil gravado antes da renomeação abriria com zero — o
    /// jogador perderia o que várias runs custaram, e sem erro nenhum, porque
    /// campo que sumiu do JSON simplesmente não é lido.
    /// </summary>
    static void MigrarMoeda(ProfileData perfil)
    {
        if (perfil == null || perfil.relics <= 0) return;

        perfil.memories += perfil.relics;
        perfil.relics = 0;

        Debug.Log($"PlayerProfile: {perfil.memories} memórias herdadas do campo antigo de relíquias.");
        Salvar();
    }

    public static void Salvar()
    {
        if (dados == null) return;

        try
        {
            string temporario = Caminho + ".tmp";
            File.WriteAllText(temporario, JsonUtility.ToJson(dados, true));

            if (File.Exists(Caminho)) File.Delete(Caminho);
            File.Move(temporario, Caminho);
        }
        catch (Exception e)
        {
            Debug.LogError($"PlayerProfile: falha ao gravar o perfil — {e.Message}");
        }
    }

    /// <summary>
    /// Traz o que a Fase 3 deixou no PlayerPrefs.
    ///
    /// Roda uma vez só, quando ainda não há arquivo de perfil. As chaves antigas
    /// não são apagadas de propósito: se algo der errado aqui, o progresso do
    /// jogador ainda está lá para ser recuperado à mão.
    /// </summary>
    static void MigrarDoPlayerPrefs(ProfileData destino)
    {
        const string ChaveMoeda = "GoL.Meta.Relics";
        if (!PlayerPrefs.HasKey(ChaveMoeda)) return;

        destino.memories = PlayerPrefs.GetInt(ChaveMoeda, 0);
        destino.bestCycle = PlayerPrefs.GetInt("GoL.Meta.BestCycle", 0);
        destino.totalRuns = PlayerPrefs.GetInt("GoL.Meta.Runs", 0);
        destino.totalWins = PlayerPrefs.GetInt("GoL.Meta.Wins", 0);

        Debug.Log($"PlayerProfile: {destino.memories} memórias migradas do PlayerPrefs.");
    }

    #region Destraves

    public static int NivelDe(string id)
    {
        foreach (var u in Dados.unlocks)
            if (u != null && u.id == id) return u.level;
        return 0;
    }

    public static void SubirNivel(string id)
    {
        foreach (var u in Dados.unlocks)
        {
            if (u == null || u.id != id) continue;

            u.level++;
            Salvar();
            return;
        }

        Dados.unlocks.Add(new UnlockSave { id = id, level = 1 });
        Salvar();
    }

    #endregion

    /// <summary>Apaga o perfil inteiro — memórias, destraves e opções. Só para teste.</summary>
    public static void Zerar()
    {
        dados = new ProfileData();
        Salvar();
    }

    /// <summary>Força a releitura do disco. Usado pelos testes, que escrevem por fora.</summary>
    public static void Recarregar() => dados = null;
}

[Serializable]
public class ProfileData
{
    public int version = 1;

    /// <summary>
    /// A moeda que atravessa as runs. Chamava-se "relíquia"; o nome passou a
    /// designar o item que altera regras dentro de uma run, no espírito de Slay
    /// the Spire, e a moeda virou <b>memória</b>.
    /// </summary>
    public int memories;

    /// <summary>
    /// O campo antigo, mantido só para não apagar o que o jogador já tinha.
    ///
    /// JsonUtility ignora campos que sumiram: renomear direto zeraria o saldo de
    /// todo perfil existente sem erro nenhum. Fica aqui até uma versão em que
    /// migrar de novo não custe nada — ver <see cref="PlayerProfile.MigrarMoeda"/>.
    /// </summary>
    public int relics;

    public int bestCycle;
    public int totalRuns;
    public int totalWins;

    public List<UnlockSave> unlocks = new List<UnlockSave>();
    public SettingsSave settings = new SettingsSave();
}

[Serializable]
public class UnlockSave
{
    public string id;
    public int level;
}

/// <summary>
/// As opções. Os padrões são os mesmos que o <see cref="GameAudio"/> já trazia
/// no Inspector — sem isso, a primeira abertura do jogo mudaria o volume de quem
/// nunca tocou nas opções.
/// </summary>
[Serializable]
public class SettingsSave
{
    public float musicVolume = 0.45f;
    public float sfxVolume = 0.85f;

    public bool fullscreen = true;

    /// <summary>0 = ainda não escolhida; vale o que o sistema já estava usando.</summary>
    public int resolutionWidth;
    public int resolutionHeight;
}
