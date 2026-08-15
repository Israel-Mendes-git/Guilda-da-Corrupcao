using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Onde o jogo escreve e lê os saves.
///
/// Um arquivo JSON por slot em <c>Application.persistentDataPath/saves/</c>.
/// Escolhido em lugar do PlayerPrefs — que é onde o projeto guardava decks e
/// relíquias até aqui — por três motivos concretos: o PlayerPrefs no Windows é
/// o registro, que não aguenta um save deste tamanho com folga; não há como ter
/// vários slots sem inventar prefixos de chave; e um save em arquivo o jogador
/// consegue copiar, mandar num relatório de bug e apagar.
///
/// Esta classe **não sabe o que é o jogo** — só lê e escreve <see cref="SaveGame"/>.
/// Quem traduz o estado vivo para o DTO é o <see cref="GameStateIO"/>. É essa
/// separação que permitiria trocar o ESave por baixo sem tocar em mais nada.
/// </summary>
public static class SaveSystem
{
    /// <summary>O slot que o jogo escreve sozinho. É o que o botão "Continuar" lê.</summary>
    public const string SlotAuto = "autosave";

    /// <summary>Slots que o jogador controla.</summary>
    public static readonly string[] SlotsManuais = { "slot1", "slot2", "slot3" };

    /// <summary>Todos os slots, na ordem em que a tela os mostra.</summary>
    public static IEnumerable<string> TodosOsSlots
    {
        get
        {
            yield return SlotAuto;
            foreach (string s in SlotsManuais) yield return s;
        }
    }

    /// <summary>
    /// O save que a próxima carga da cena de jogo deve aplicar.
    ///
    /// Existe porque carregar uma partida é, na prática, duas coisas em momentos
    /// diferentes: escolher o arquivo (no menu, com a cena de jogo ainda não
    /// carregada) e reconstruir o estado (depois que os managers existem). Quem
    /// consome é o <see cref="SceneFlow"/>, e ele zera assim que aplica.
    /// </summary>
    public static SaveGame PendingLoad { get; set; }

    /// <summary>Última falha de escrita/leitura, para a UI ter o que dizer.</summary>
    public static string UltimoErro { get; private set; }

    /// <summary>
    /// Suspende o autosave.
    ///
    /// Existe pelo teste automatizado: o <c>PlayModeProbe</c> joga uma jornada
    /// inteira, e cada ciclo que ele avança dispararia o autosave por cima da
    /// partida real de quem estiver jogando neste computador. Rodar o teste não
    /// pode custar o save do autor.
    /// </summary>
    public static bool AutosaveSuspenso { get; set; }

    public static string Pasta => Path.Combine(Application.persistentDataPath, "saves");

    public static string CaminhoDe(string slot) => Path.Combine(Pasta, slot + ".json");

    public static bool Existe(string slot) => File.Exists(CaminhoDe(slot));

    /// <summary>Nome que o jogador vê para cada slot.</summary>
    public static string NomeDoSlot(string slot)
    {
        if (slot == SlotAuto) return "Automático";

        int indice = Array.IndexOf(SlotsManuais, slot);
        return indice >= 0 ? $"Slot {indice + 1}" : slot;
    }

    #region Escrita

    /// <summary>
    /// Grava o save. Devolve false e registra <see cref="UltimoErro"/> quando não
    /// consegue — disco cheio e pasta sem permissão existem, e um save que falha
    /// em silêncio é pior que não ter save.
    ///
    /// A escrita é feita num arquivo temporário e só então trocada pelo definitivo:
    /// se o jogo morrer no meio, o save anterior continua inteiro. Sem isso, uma
    /// queda no instante errado apagaria a run em vez de preservá-la.
    /// </summary>
    public static bool Escrever(string slot, SaveGame dados)
    {
        if (string.IsNullOrEmpty(slot) || dados == null)
        {
            UltimoErro = "slot ou dados vazios";
            return false;
        }

        dados.version = SaveFormat.Current;
        dados.savedAtUtc = DateTime.UtcNow.ToString("o");

        string destino = CaminhoDe(slot);
        string temporario = destino + ".tmp";

        try
        {
            Directory.CreateDirectory(Pasta);

            File.WriteAllText(temporario, JsonUtility.ToJson(dados, true));

            if (File.Exists(destino)) File.Delete(destino);
            File.Move(temporario, destino);

            UltimoErro = null;
            return true;
        }
        catch (Exception e)
        {
            UltimoErro = e.Message;
            Debug.LogError($"SaveSystem: falha ao gravar '{slot}' — {e.Message}");

            try { if (File.Exists(temporario)) File.Delete(temporario); }
            catch { /* o temporário sobrando é o menor dos problemas */ }

            return false;
        }
    }

    #endregion

    #region Leitura

    /// <summary>
    /// Lê o save do slot. Devolve null quando não existe, está corrompido ou veio
    /// de uma versão que este código não sabe mais ler.
    /// </summary>
    public static SaveGame Ler(string slot)
    {
        string caminho = CaminhoDe(slot);
        if (!File.Exists(caminho)) return null;

        try
        {
            SaveGame dados = JsonUtility.FromJson<SaveGame>(File.ReadAllText(caminho));

            if (dados == null)
            {
                UltimoErro = "arquivo vazio ou ilegível";
                return null;
            }

            if (dados.version < SaveFormat.MinimumSupported || dados.version > SaveFormat.Current)
            {
                UltimoErro = $"save na versão {dados.version}; este jogo lê "
                           + $"{SaveFormat.MinimumSupported}–{SaveFormat.Current}";
                Debug.LogWarning($"SaveSystem: {UltimoErro}");
                return null;
            }

            UltimoErro = null;
            return dados;
        }
        catch (Exception e)
        {
            UltimoErro = e.Message;
            Debug.LogWarning($"SaveSystem: save '{slot}' corrompido — {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// O resumo de um slot, para a tela de carregar. Nunca lança: um arquivo
    /// corrompido vira uma linha "corrompido" na lista, não uma tela quebrada.
    /// </summary>
    public static SaveHeader LerCabecalho(string slot)
    {
        if (!Existe(slot)) return SaveHeader.Vazio(slot);

        SaveGame dados = Ler(slot);
        if (dados == null) return SaveHeader.Corrompido(slot);

        int vivos = 0;
        if (dados.roster != null)
            foreach (var h in dados.roster)
                if (h != null && !h.isDead) vivos++;

        return new SaveHeader
        {
            slot = slot,
            exists = true,
            corrupted = false,
            cycle = dados.run != null ? dados.run.cycle : 0,
            corruption = dados.run != null ? Mathf.RoundToInt(dados.run.corruption) : 0,
            gold = dados.guild != null ? dados.guild.gold : 0,
            heroesAlive = vivos,
            bossAvailable = dados.run != null && dados.run.corruption >= RunManager.BossThreshold,
            savedAt = dados.SavedAt
        };
    }

    public static List<SaveHeader> ListarSlots()
    {
        var lista = new List<SaveHeader>();
        foreach (string slot in TodosOsSlots)
            lista.Add(LerCabecalho(slot));
        return lista;
    }

    #endregion

    /// <summary>Apaga o slot. Silencioso quando não há nada para apagar.</summary>
    public static bool Apagar(string slot)
    {
        string caminho = CaminhoDe(slot);
        if (!File.Exists(caminho)) return true;

        try
        {
            File.Delete(caminho);
            return true;
        }
        catch (Exception e)
        {
            UltimoErro = e.Message;
            Debug.LogWarning($"SaveSystem: não consegui apagar '{slot}' — {e.Message}");
            return false;
        }
    }

    #region Atalhos que o jogo usa

    /// <summary>
    /// Salva o estado de agora no slot pedido. É o caminho que a pausa e o
    /// autosave usam.
    /// </summary>
    public static bool Salvar(string slot) => Escrever(slot, GameStateIO.Capturar());

    /// <summary>
    /// O autosave do fim de ciclo.
    ///
    /// Só o fim de uma jornada é ponto seguro: o estado de dentro da jornada —
    /// mapa, dia, mão, descarte — não é salvo por ninguém, então gravar no meio
    /// devolveria o jogador à guilda com a missão perdida. É por isso que o
    /// botão "Salvar" da pausa também se recusa a agir durante a estrada.
    /// </summary>
    public static bool Autosave()
    {
        if (AutosaveSuspenso) return false;
        if (!PodeSalvarAgora(out string _)) return false;

        return Salvar(SlotAuto);
    }

    /// <summary>
    /// Dá para salvar neste instante? Devolve também o motivo, porque um botão
    /// desligado sem explicação é um bug do ponto de vista de quem joga.
    /// </summary>
    public static bool PodeSalvarAgora(out string motivo)
    {
        motivo = "";

        if (GuildManager.Instance == null)
        {
            motivo = "a guilda ainda não existe";
            return false;
        }

        var run = RunManager.Instance;
        if (run != null && run.IsOver)
        {
            motivo = "esta run já terminou";
            return false;
        }

        var jornada = JourneyManager.Instance;
        if (jornada != null && jornada.EmJornada)
        {
            motivo = "não dá para salvar no meio da estrada";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Joga fora o autosave — a run acabou, ou o jogador fundou outra guilda, e
    /// não há mais a que voltar.
    ///
    /// Respeita a suspensão pelo mesmo motivo que o <see cref="Autosave"/>: o
    /// teste automatizado termina runs e funda guildas de propósito, e apagar
    /// aqui destruiria a partida real de quem estiver jogando.
    /// </summary>
    public static void DescartarAutosave()
    {
        if (AutosaveSuspenso) return;
        Apagar(SlotAuto);
    }

    /// <summary>Há partida para o botão "Continuar" retomar?</summary>
    public static bool TemPartidaEmAndamento() => Existe(SlotAuto);

    #endregion
}
