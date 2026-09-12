using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;

public class GuildManager : MonoBehaviour
{
    public static GuildManager Instance;

    [Header("Recursos")]
    public int gold = 500;
    public int reputation = 100;
    [SerializeField] private TMP_Text goldtxt;
    [SerializeField] private TMP_Text reputationtxt;

    [Header("Heróis")]
    public List<HeroData> roster = new List<HeroData>();
    public int maxRosterSize = 8;

    [Header("Memória da Guilda")]
    public List<HeroData> fallenHeroes = new List<HeroData>();

    /// <summary>
    /// O que a guilda tem guardado e ninguém está usando, por id do
    /// <see cref="ItemCatalog"/>.
    ///
    /// <b>Por que existe um estoque.</b> A relíquia é do herói e morre com ele,
    /// mas ela precisa chegar de algum lugar: o chefe a larga no meio da
    /// estrada, o Mercado a vende antes de partir, um evento a entrega. Sem
    /// prateleira, todo item teria de ser equipado no instante em que aparece —
    /// e um herói com os dois slots cheios recusaria o espólio do chefe.
    ///
    /// Aqui só mora o que está livre: o que foi equipado sai daqui e passa a
    /// viver na lista do herói.
    /// </summary>
    [Header("Prateleira")]
    public List<string> relicStock = new List<string>();
    public List<string> potionStock = new List<string>();

    [Header("Eventos")]
    public System.Action onGoldChanged;
    public System.Action onRosterChanged;
    public System.Action onReputationChanged;

    /// <summary>Avisa quem estiver mostrando a prateleira ou a ficha do herói.</summary>
    public System.Action onItemsChanged;

    /// <summary>
    /// Guarda um item na prateleira. Id desconhecido é ignorado em silêncio: o
    /// catálogo pode encolher entre versões, e um save antigo não deve travar a
    /// guilda por causa de uma relíquia que deixou de existir.
    /// </summary>
    public void GuardarReliquia(string id)
    {
        if (ItemCatalog.Reliquia(id) == null) return;

        relicStock.Add(id);
        onItemsChanged?.Invoke();
    }

    public void GuardarPocao(string id)
    {
        if (ItemCatalog.Pocao(id) == null) return;

        potionStock.Add(id);
        onItemsChanged?.Invoke();
    }

    /// <summary>
    /// Tira da prateleira e põe no herói. Devolve false quando não há o item ou
    /// quando os dois slots dele já estão ocupados.
    /// </summary>
    public bool EquiparReliquia(HeroData heroi, string id)
    {
        if (heroi == null || !relicStock.Contains(id)) return false;
        if (heroi.relics == null) heroi.relics = new List<string>();
        if (heroi.relics.Count >= ItemCatalog.SlotsDeReliquia) return false;

        relicStock.Remove(id);
        heroi.relics.Add(id);
        onItemsChanged?.Invoke();
        return true;
    }

    /// <summary>Devolve a relíquia à prateleira.</summary>
    public bool DesequiparReliquia(HeroData heroi, string id)
    {
        if (heroi?.relics == null || !heroi.relics.Remove(id)) return false;

        relicStock.Add(id);
        onItemsChanged?.Invoke();
        return true;
    }

    /// <summary>Entrega um frasco da prateleira ao herói, para ele levar na estrada.</summary>
    public bool EntregarPocao(HeroData heroi, string id)
    {
        if (heroi == null || !potionStock.Contains(id)) return false;
        if (heroi.potions == null) heroi.potions = new List<string>();

        potionStock.Remove(id);
        heroi.potions.Add(id);
        onItemsChanged?.Invoke();
        return true;
    }

    public bool RecolherPocao(HeroData heroi, string id)
    {
        if (heroi?.potions == null || !heroi.potions.Remove(id)) return false;

        potionStock.Add(id);
        onItemsChanged?.Invoke();
        return true;
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Com quantos heróis a guilda é fundada — escolhidos pelo jogador, na
    /// Taverna, sem custo.
    ///
    /// <b>Até 12/09 a guilda nascia com quatro prontos</b>, e quatro prontos é
    /// um menu que não pede nada de quem chega: o grupo já estava completo, a
    /// Taverna não tinha motivo, e as sete salas abriam de uma vez sobre um
    /// jogador que ainda não tinha visto uma estrada. Decisão do autor: o
    /// jogador escolhe dois, e a guilda cresce pelo que a estrada rende.
    /// </summary>
    public const int FundadoresDaGuilda = 2;

    /// <summary>
    /// A guilda ainda está sendo fundada: ninguém partiu, ninguém morreu e os
    /// fundadores ainda não estão todos na casa. É o estado em que a Taverna
    /// oferece os fundadores sem cobrar, e o único em que a guilda existe sem
    /// gente para mandar.
    /// </summary>
    public bool EmFundacao =>
        roster.Count < FundadoresDaGuilda
        && fallenHeroes.Count == 0
        && !(RunManager.Existe && RunManager.Instance.Cycle > 0);

    /// <summary>Quantos fundadores faltam escolher.</summary>
    public int FundadoresQueFaltam => EmFundacao ? FundadoresDaGuilda - roster.Count : 0;

    /// <summary>
    /// A guilda desta cena já foi preparada — pelos destraves de uma guilda nova
    /// ou pelo save de uma partida em andamento.
    ///
    /// Antes, roster vazio era o sinal de guilda nova. Desde que a guilda nasce
    /// vazia de propósito, um save feito antes de escolher os fundadores também
    /// tem roster vazio, e o Start passaria o ouro carregado por cima com o ouro
    /// de fábrica.
    /// </summary>
    [System.NonSerialized] bool preparada;

    void Start()
    {
        // Guilda nova: a sessão começou sem save, ou o jogador escolheu "Nova
        // guilda". Quando há save, o SceneFlow já aplicou tudo no sceneLoaded —
        // antes deste Start, de propósito — e marcou a guilda como preparada.
        if (!preparada)
            AplicarDestraves();

        UpdateGoldUI();
        UpdateReputationUI();
    }

    /// <summary>O save já encheu esta guilda; o Start não deve fundar outra por cima.</summary>
    public void MarcarComoCarregada() => preparada = true;

    /// <summary>
    /// O que as runs anteriores compraram, aplicado à guilda que está nascendo.
    ///
    /// Ficava só no <see cref="ResetForNewRun"/>, e por isso valia apenas para a
    /// guilda fundada a partir da tela de fim de run — a primeira partida da
    /// sessão, que é o caminho normal saindo do menu, ignorava tudo o que o
    /// jogador tinha destravado.
    /// </summary>
    void AplicarDestraves()
    {
        gold = MetaProgression.OuroBasePorRun + MetaProgression.StartingGoldBonus();
        reputation = MetaProgression.ReputacaoBasePorRun + MetaProgression.StartingReputationBonus();
        maxRosterSize = BaseRosterSize + MetaProgression.ExtraRosterSlots();
        preparada = true;
    }

    /// <summary>
    /// Um fundador entra sem custo. Só vale durante a fundação: fora dela, quem
    /// entra paga o salário pelo <see cref="RecruitHero"/>.
    /// </summary>
    public bool AceitarFundador(HeroData hero)
    {
        if (hero == null || !EmFundacao || !CanRecruit()) return false;

        roster.Add(hero);
        onRosterChanged?.Invoke();
        return true;
    }

    /// <summary>Vagas de fábrica, antes dos alojamentos comprados no Santuário.</summary>
    public const int BaseRosterSize = 8;

    /// <summary>
    /// Uma guilda nova, para a run seguinte.
    ///
    /// Tudo o que a run acumulou se perde — é o preço da derrota. O que atravessa
    /// são as memórias da <see cref="MetaProgression"/>, e elas entram aqui como
    /// ouro de partida: a próxima tentativa começa mais folgada por causa da
    /// anterior, que é o que faz perder valer alguma coisa.
    /// </summary>
    public void ResetForNewRun()
    {
        roster.Clear();
        fallenHeroes.Clear();

        // Sem elenco: a guilda nova nasce em fundação, e os fundadores são
        // escolhidos na Taverna.
        AplicarDestraves();

        NotificarTudoMudou();
    }

    /// <summary>
    /// Avisa a UI inteira de que tudo mudou de uma vez.
    ///
    /// Existe para o carregamento de save: ouro, reputação e roster trocam no
    /// mesmo instante, e disparar os três eventos separados espalhados pelo
    /// código de carga deixaria a tela meio atualizada se um deles faltasse.
    /// </summary>
    public void NotificarTudoMudou()
    {
        UpdateGoldUI();
        UpdateReputationUI();

        onGoldChanged?.Invoke();
        onReputationChanged?.Invoke();
        onRosterChanged?.Invoke();
    }

    public bool CanRecruit()
    {
        return roster.Count < maxRosterSize;
    }

    public void RecruitHero(HeroData hero)
    {
        if (!CanRecruit())
        {
            Debug.Log("Equipe cheia!");
            return;
        }

        if (gold >= hero.salary)
        {
            gold -= hero.salary;
            roster.Add(hero);

            UpdateGoldUI();
            onGoldChanged?.Invoke();
            onRosterChanged?.Invoke();

            Debug.Log($"{hero.heroName} se juntou à guilda!");
        }
        else
        {
            Debug.Log("Ouro insuficiente!");
        }
    }

    public void RemoveHero(HeroData hero)
    {
        if (roster.Contains(hero))
        {
            roster.Remove(hero);
            onRosterChanged?.Invoke();
        }
    }

    /// <summary>Remove o herói do roster e o registra entre os mortos (base do futuro Cemitério).</summary>
    public void RegisterDeath(HeroData hero)
    {
        if (hero == null) return;

        hero.isDead = true;

        if (!fallenHeroes.Contains(hero))
            fallenHeroes.Add(hero);

        RemoveHero(hero);
        DeckRepository.Remove(hero);

        // Perder heróis mancha o nome da guilda.
        AddReputation(-5);
    }

    public void AddGold(int amount)
    {
        gold += amount;
        UpdateGoldUI();
        onGoldChanged?.Invoke();
    }

    public bool SpendGold(int amount)
    {
        if (gold >= amount)
        {
            gold -= amount;
            UpdateGoldUI();
            onGoldChanged?.Invoke();
            return true;
        }
        return false;
    }

    public void AddReputation(int amount)
    {
        reputation = Mathf.Max(0, reputation + amount);
        UpdateReputationUI();
        onReputationChanged?.Invoke();
    }

    void UpdateGoldUI()
    {
        if (goldtxt != null)
            goldtxt.text = gold.ToString();
    }

    void UpdateReputationUI()
    {
        if (reputationtxt != null)
            reputationtxt.text = reputation.ToString();
    }
}
