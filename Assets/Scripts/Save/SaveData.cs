using System;
using System.Collections.Generic;

/// <summary>
/// O formato do arquivo de save.
///
/// Tudo aqui é DTO puro: classes simples, sem referência a Unity, feitas para
/// atravessar o <c>JsonUtility</c>. É de propósito — o estado vivo do jogo mora
/// em <c>ScriptableObject</c>s criados em runtime (<see cref="HeroData"/>,
/// <see cref="QuestData"/>), e ScriptableObject não sobrevive a um fechar de
/// jogo. Salvar é converter para cá; carregar é reconstruir a partir daqui.
///
/// Duas regras que valem para todo este arquivo:
///
/// 1. <b>Campo novo só entra no fim da classe</b> e com um valor padrão que
///    signifique "save antigo". O <c>JsonUtility</c> deixa em zero/null o que não
///    encontra no arquivo, então um campo no meio não quebra nada — mas a ordem
///    documenta a idade de cada coisa, e é o mesmo hábito que os assets do jogo
///    já exigem para enums.
/// 2. <b>Nada de <c>Sprite</c>, <c>AudioClip</c> ou qualquer referência de
///    asset.</b> O retrato do herói sai do nome dele pelo <c>PortraitCatalog</c>,
///    então é redescoberto no carregamento e não ocupa o save.
/// </summary>
[Serializable]
public class SaveGame
{
    /// <summary>
    /// Versão do formato. Sobe quando um campo muda de significado — não quando
    /// um campo novo aparece, porque para isso o padrão do JsonUtility basta.
    /// É o que permite recusar um save que este código não sabe mais ler, em vez
    /// de carregá-lo torto.
    /// </summary>
    public int version = SaveFormat.Current;

    /// <summary>ISO 8601 em UTC — a lista de slots ordena e mostra por isto.</summary>
    public string savedAtUtc;

    public GuildSave guild = new GuildSave();
    public RunSave run = new RunSave();

    public List<HeroSave> roster = new List<HeroSave>();
    public List<HeroSave> fallen = new List<HeroSave>();
    public List<QuestSave> quests = new List<QuestSave>();
    public List<DeckSave> decks = new List<DeckSave>();

    public DateTime SavedAt
    {
        get
        {
            if (DateTime.TryParse(savedAtUtc, null,
                    System.Globalization.DateTimeStyles.AdjustToUniversal, out DateTime data))
                return data;
            return DateTime.MinValue;
        }
    }
}

/// <summary>A versão do formato, num lugar só.</summary>
public static class SaveFormat
{
    public const int Current = 1;

    /// <summary>Saves mais antigos que isto são recusados em vez de lidos torto.</summary>
    public const int MinimumSupported = 1;
}

[Serializable]
public class GuildSave
{
    public int gold;
    public int reputation;
    public int maxRosterSize;

    /// <summary>
    /// A prateleira: relíquias e frascos que a guilda tem e ninguém carrega.
    /// Campos no fim, como manda a regra do projeto.
    /// </summary>
    public List<string> relicStock = new List<string>();
    public List<string> potionStock = new List<string>();
}

[Serializable]
public class RunSave
{
    public int cycle;
    public float corruption;
    public int state;       // RunState
    public int endReason;   // RunEndReason
    public int fallen;

    /// <summary>
    /// Corrupção de cada região, na ordem de <see cref="BiomeUtil.Playable"/>.
    ///
    /// Campo no fim e lista vazia por padrão: save antigo carrega sem ele e o
    /// <see cref="RegionMap.Restaurar"/> cai no estado inicial em vez de
    /// derrubar a partida.
    /// </summary>
    public List<float> regionCorruption = new List<float>();

    /// <summary>
    /// O quanto de cada região está mapeado, na ordem de
    /// <see cref="BiomeUtil.Playable"/>. Campo no fim pelo mesmo motivo do
    /// anterior: o save que não o tem abre com o mapa em branco.
    /// </summary>
    public List<float> regionMapping = new List<float>();

    /// <summary>
    /// As regiões seladas, por índice de <see cref="BiomeUtil.Playable"/> e
    /// <b>na ordem em que caíram</b> — não uma lista de sim/não por região.
    /// A ordem é regra do jogo: o terceiro selo decide onde o fim acontece.
    /// </summary>
    public List<int> regionSealed = new List<int>();

    /// <summary>
    /// Os escritos, por índice de região: os que esperam na estante e os já
    /// traduzidos, cada lista na sua ordem — a dos lidos é a ordem em que a
    /// história foi montada. <c>lastTranslationCycle</c> guarda a regra de uma
    /// tradução por ciclo, que sem ele voltaria a valer no carregamento.
    /// </summary>
    public List<int> writingsOnShelf = new List<int>();

    public List<int> writingsTranslated = new List<int>();

    public int lastTranslationCycle = -1;
}

/// <summary>
/// Um herói inteiro, menos o que se redescobre.
///
/// O <c>heroId</c> é o que amarra o herói ao deck dele e o que faz o mesmo herói
/// continuar sendo o mesmo entre uma sessão e outra — sem ele, carregar um save
/// daria a todo mundo um baralho novo.
/// </summary>
[Serializable]
public class HeroSave
{
    public string heroId;
    public string heroName;
    public int heroClass;
    public int level;
    public int maxHp;
    public int currentHp;
    public int salary;
    public int personality;
    public int trait;
    public float loyalty;
    public float morale;

    public bool isInjured;
    public bool isDead;
    public int corruptionExposure;

    public int xp;
    public int xpToNextLevel;

    public float stress;
    public int mentalState;
    public bool isOnDeathsDoor;

    public int weaponLevel;
    public int armorLevel;

    /// <summary>
    /// Relíquias e frascos deste herói, por id do <c>ItemCatalog</c>.
    ///
    /// Campos no fim da classe, como manda a regra do projeto. Save antigo abre
    /// sem eles e o herói volta desequipado — que é o estado correto para quem
    /// jogou antes de os itens existirem.
    /// </summary>
    public List<string> relics = new List<string>();
    public List<string> potions = new List<string>();
}

/// <summary>
/// O baralho de um herói, por nome de asset — o mesmo formato que o
/// <c>DeckRepository</c> já usava no PlayerPrefs, para que um save antigo de
/// deck continue legível pelo índice de cartas.
/// </summary>
[Serializable]
public class DeckSave
{
    public string heroId;
    public List<string> cardNames = new List<string>();
}

[Serializable]
public class QuestSave
{
    public string questName;
    public string description;
    public int biomeType;
    public int minDuration;
    public int maxDuration;
    public int baseReward;
    public int recommendedLevel;
    public int corruptionLevel;
    public int risk;
    public string objective;
    public bool isFinalBoss;
    public bool isRegionBoss;
    public List<ClassRequirement> requirements = new List<ClassRequirement>();
}

/// <summary>
/// O resumo de um slot, para a tela de carregar.
///
/// Existe separado do <see cref="SaveGame"/> porque a tela de slots precisa
/// mostrar quatro saves ao mesmo tempo e não precisa de nenhum deles inteiro —
/// e porque um arquivo corrompido tem que aparecer na lista como corrompido, e
/// não derrubar a tela.
/// </summary>
public class SaveHeader
{
    public string slot;
    public bool exists;
    public bool corrupted;

    public int cycle;
    public int corruption;
    public int gold;
    public int heroesAlive;
    public bool bossAvailable;
    public DateTime savedAt;

    public static SaveHeader Vazio(string slot) => new SaveHeader { slot = slot, exists = false };

    public static SaveHeader Corrompido(string slot) =>
        new SaveHeader { slot = slot, exists = true, corrupted = true };
}
