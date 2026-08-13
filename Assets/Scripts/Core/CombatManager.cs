using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Estado de um inimigo durante um combate.</summary>
public class EnemyInstance
{
    public EnemyData data;
    public int currentHp;
    public int block;
    public EnemyIntent intent;
    public GameObject view;

    public bool IsAlive => currentHp > 0;
}

/// <summary>
/// Combate por turnos com as cartas do herói principal.
/// O dano nos heróis passa por EventResolver, de modo que Beira da Morte,
/// estresse e aflições sigam exatamente as mesmas regras da jornada.
/// </summary>
public class CombatManager : MonoBehaviour
{
    public static CombatManager Instance;

    [Header("Painel")]
    public GameObject combatPanel;

    [Header("Inimigos")]
    public Transform enemyContainer;
    public GameObject enemyPrefab;

    [Header("Heróis")]
    public Transform heroContainer;
    public GameObject heroStatusPrefab;

    [Header("Mão")]
    public Transform handContainer;
    public GameObject cardPrefab;

    [Header("Textos")]
    public TMP_Text turnText;
    public TMP_Text energyText;
    public TMP_Text deckCountText;
    public TMP_Text discardCountText;
    public TMP_Text combatLogText;
    public TMP_Text instructionText;

    [Header("Botões")]
    public Button endTurnButton;
    public Button fleeButton;

    [Header("Config")]
    public int baseEnergy = 3;
    public int cardsPerTurn = 5;
    public float enemyActionDelay = 0.45f;

    /// <summary>Estresse que a carta de limpeza tira junto com a aflição.</summary>
    public const float CleanseStressRelief = 25f;

    private List<HeroData> party = new List<HeroData>();
    private List<EnemyInstance> enemies = new List<EnemyInstance>();
    private readonly Dictionary<HeroData, int> heroBlock = new Dictionary<HeroData, int>();

    // Quem emprestou cada carta. A ordem de 'party' é a formação, e é a posição
    // do dono que decide se a carta sai inteira.
    private CardOwnership ownership;

    private CardManager cards;
    private int energy;
    private int maxEnergy;
    private int turn;
    private bool isPlayerTurn;
    private bool combatOver;

    private CardData pendingCard;          // carta em trânsito no drag

    // Efeitos temporários do grupo. Vivem só enquanto o combate durar.
    private int groupDamageBonus;          // dano extra plano, da carta de Buff
    private int groupDamageBonusTurns;     // turnos que ainda restam ao bônus
    private float nextCardMultiplier = 1f; // gasto pela próxima carta que causar dano
    private readonly HashSet<HeroData> evading = new HashSet<HeroData>();

    // Alvos vivos na tela, para acender e apagar durante o arrasto.
    private readonly List<CombatDropTarget> activeTargets = new List<CombatDropTarget>();

    // Views por herói: o feedback precisa saber onde desenhar o número.
    private readonly Dictionary<HeroData, GameObject> heroViews = new Dictionary<HeroData, GameObject>();

    private readonly List<string> log = new List<string>();

    private Action<bool> onCombatComplete;

    public bool IsInCombat => combatPanel != null && combatPanel.activeSelf && !combatOver;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        if (combatPanel != null)
            combatPanel.SetActive(false);

        if (endTurnButton != null)
            endTurnButton.onClick.AddListener(EndPlayerTurn);

        if (fleeButton != null)
            fleeButton.onClick.AddListener(ConfirmFlee);
    }

    #region Início e fim

    /// <param name="cardOwnership">
    /// Quem emprestou cada carta, para a formação valer no combate. Nulo faz toda
    /// carta sair inteira — é o que acontece num combate iniciado fora da jornada.
    /// </param>
    public void StartCombat(List<HeroData> heroes, DeckData deck, List<EnemyData> lineup, Action<bool> onComplete,
                            CardOwnership cardOwnership = null)
    {
        if (heroes == null || heroes.Count == 0 || lineup == null || lineup.Count == 0)
        {
            Debug.LogError("CombatManager: combate iniciado sem heróis ou sem inimigos.");
            onComplete?.Invoke(true);
            return;
        }

        // A ordem recebida é a formação escolhida na preparação: preservá-la é o
        // que faz as duas primeiras posições serem a linha de frente.
        party = heroes.Where(h => h != null && h.IsAlive).ToList();
        ownership = cardOwnership;
        onCombatComplete = onComplete;
        combatOver = false;
        turn = 0;
        maxEnergy = baseEnergy;
        log.Clear();

        heroBlock.Clear();
        foreach (var hero in party)
            heroBlock[hero] = 0;

        groupDamageBonus = 0;
        groupDamageBonusTurns = 0;
        nextCardMultiplier = 1f;
        evading.Clear();

        enemies.Clear();
        foreach (var data in lineup)
        {
            enemies.Add(new EnemyInstance
            {
                data = data,
                currentHp = data.maxHp,
                block = 0
            });
        }

        // Baralho próprio do combate, em objeto separado para nunca ser confundido
        // com o CardManager da jornada por um GetComponent.
        if (cards == null)
        {
            var host = new GameObject("CombatDeck");
            host.transform.SetParent(transform);
            cards = host.AddComponent<CardManager>();
        }

        cards.handSize = cardsPerTurn;
        cards.maxHandSize = cardsPerTurn + 3;
        cards.InitializeDeck(deck);

        // Todas as outras telas saem: elas ocupam a tela inteira e, desenhadas
        // junto com o combate, viravam texto sobre texto e cartas sobre botões.
        UIManager.Instance?.EnterCombatScreen();

        if (combatPanel != null)
        {
            combatPanel.SetActive(true);
            combatPanel.transform.SetAsLastSibling();   // por cima de tudo
        }

        BuildEnemyViews();
        RollAllIntents();
        AddLog("O combate começa!");

        BeginPlayerTurn();
    }

    void EndCombat(bool victory)
    {
        if (combatOver) return;
        combatOver = true;

        int reward = victory ? enemies.Sum(e => e.data.goldReward) : 0;

        if (victory && reward > 0 && GuildManager.Instance != null)
            GuildManager.Instance.AddGold(reward);

        // Alívio ou trauma coletivo pelo desfecho.
        var aftermath = new EventResolver.Resolution();
        foreach (var hero in party.Where(h => h.IsAlive))
            EventResolver.AddStress(hero, victory ? -8f : 15f, aftermath);

        string title = victory ? "⚔️ Vitória no combate" : "💀 Derrota";
        string message = victory
            ? $"Os inimigos caíram.\n+{reward} ouro"
            : "O grupo foi forçado a recuar.";

        Action finish = () =>
        {
            if (combatPanel != null)
                combatPanel.SetActive(false);

            // Devolve à tela exatamente o que foi escondido ao entrar.
            UIManager.Instance?.ExitCombatScreen();

            onCombatComplete?.Invoke(victory);
            onCombatComplete = null;
        };

        if (UIManager.Instance != null)
            UIManager.Instance.ShowResult(title, message, finish);
        else
            finish();
    }

    void ConfirmFlee()
    {
        if (combatOver || !isPlayerTurn) return;

        UIManager.Instance?.ShowConfirm(
            "Recuar",
            "Fugir custa moral e não rende recompensa. Continuar?",
            () =>
            {
                var flight = new EventResolver.Resolution();
                foreach (var hero in party.Where(h => h.IsAlive))
                    EventResolver.AddStress(hero, 12f, flight);

                AddLog("O grupo recua.");
                EndCombat(false);
            },
            null
        );
    }

    #endregion

    #region Turnos

    void BeginPlayerTurn()
    {
        if (combatOver) return;

        turn++;
        isPlayerTurn = true;
        energy = maxEnergy;
        pendingCard = null;

        // Bloqueio não acumula entre turnos.
        foreach (var hero in party.ToList())
            heroBlock[hero] = 0;

        int toDraw = Mathf.Max(0, cardsPerTurn - cards.hand.Count);
        for (int i = 0; i < toDraw; i++)
            cards.DrawCard();

        AddLog($"— Turno {turn} —");
        RefreshAll();
        SetInstruction("Escolha uma carta.");
    }

    void EndPlayerTurn()
    {
        if (combatOver || !isPlayerTurn) return;

        isPlayerTurn = false;
        pendingCard = null;

        // O prazo do bônus corre ao fechar o turno: jogado no turno 1 com duas
        // rodadas, ele vale o turno 1 e o 2.
        if (groupDamageBonusTurns > 0)
        {
            groupDamageBonusTurns--;
            if (groupDamageBonusTurns == 0)
            {
                groupDamageBonus = 0;
                AddLog("O ímpeto do grupo passa.");
            }
        }

        SetInstruction("Os inimigos agem...");
        StartCoroutine(EnemyPhase());
    }

    IEnumerator EnemyPhase()
    {
        foreach (var enemy in enemies.Where(e => e.IsAlive).ToList())
        {
            if (combatOver) yield break;

            yield return new WaitForSeconds(enemyActionDelay);

            ExecuteIntent(enemy);
            RefreshHeroes();

            if (party.All(h => !h.IsAlive))
            {
                EndCombat(false);
                yield break;
            }
        }

        // Inimigos perdem o bloqueio e escolhem a próxima ação.
        foreach (var enemy in enemies)
            enemy.block = 0;

        RollAllIntents();
        yield return new WaitForSeconds(enemyActionDelay * 0.5f);

        BeginPlayerTurn();
    }

    void ExecuteIntent(EnemyInstance enemy)
    {
        var resolution = new EventResolver.Resolution();

        switch (enemy.intent)
        {
            case EnemyIntent.Attack:
            {
                HeroData target = PickTargetHero();
                if (target == null) return;
                DamageHero(target, enemy.data.attackDamage, resolution);
                AddLog($"{enemy.data.enemyName} ataca {target.heroName}.");
                break;
            }

            case EnemyIntent.AttackAll:
            {
                int dmg = Mathf.Max(1, Mathf.RoundToInt(enemy.data.attackDamage * 0.6f));
                foreach (var hero in party.Where(h => h.IsAlive).ToList())
                    DamageHero(hero, dmg, resolution);
                AddLog($"{enemy.data.enemyName} atinge o grupo inteiro!");
                break;
            }

            case EnemyIntent.Defend:
                enemy.block += enemy.data.blockAmount;
                AddLog($"{enemy.data.enemyName} se protege.");
                break;

            case EnemyIntent.Stress:
            {
                HeroData target = PickTargetHero();
                if (target == null) return;
                EventResolver.AddStress(target, enemy.data.stressDamage, resolution);
                AddLog($"{enemy.data.enemyName} abala {target.heroName}.");
                break;
            }
        }

        foreach (string line in resolution.lines)
            AddLog(line);
    }

    /// <summary>
    /// Quem leva o golpe. A escolha é da formação: a linha de frente concentra os
    /// ataques, e quem está na Beira da Morte atrai menos atenção.
    /// </summary>
    HeroData PickTargetHero()
    {
        return PartyFormation.PickTarget(party);
    }

    void RollAllIntents()
    {
        foreach (var enemy in enemies.Where(e => e.IsAlive))
            enemy.intent = RollIntent(enemy.data);

        RefreshEnemies();
    }

    EnemyIntent RollIntent(EnemyData data)
    {
        int total = data.attackWeight + data.defendWeight + data.stressWeight + data.attackAllWeight;
        if (total <= 0) return EnemyIntent.Attack;

        int roll = UnityEngine.Random.Range(0, total);

        if (roll < data.attackWeight) return EnemyIntent.Attack;
        roll -= data.attackWeight;

        if (roll < data.defendWeight) return EnemyIntent.Defend;
        roll -= data.defendWeight;

        if (roll < data.stressWeight) return EnemyIntent.Stress;

        return EnemyIntent.AttackAll;
    }

    #endregion

    #region Sinais do drag and drop

    /// <summary>Jogador pegou uma carta: acende os alvos onde ela pode cair.</summary>
    public void OnCardPickedUp(CardData card)
    {
        pendingCard = card;

        SetInstruction(CardNeedsTarget(card)
            ? $"{card.cardName}: solte sobre o alvo."
            : $"{card.cardName}: solte para jogar.");

        HighlightTargetsFor(card);
    }

    /// <summary>Fim do gesto, com ou sem jogada.</summary>
    public void OnCardDropped()
    {
        pendingCard = null;
        HighlightTargetsFor(null);
        SetInstruction(isPlayerTurn && !combatOver ? "Arraste uma carta até o alvo." : "");
    }

    public void ShowCardBlockedReason(CardData card)
    {
        if (card == null) return;

        if (!isPlayerTurn) SetInstruction("Aguarde o turno dos inimigos.");
        else if (energy < card.energyCost) SetInstruction($"Energia insuficiente ({card.energyCost} necessária).");
    }

    /// <summary>Acende quem pode receber a carta; passar null apaga tudo.</summary>
    void HighlightTargetsFor(CardData card)
    {
        foreach (var target in activeTargets)
        {
            if (target == null) continue;
            target.SetHighlight(card != null && CanPlayCard(card, target.enemy, target.hero));
        }
    }

    #endregion

    #region Jogar cartas

    #region API de jogada (usada pelo drag and drop e pelos testes)

    /// <summary>A carta exige que o jogador aponte um alvo?</summary>
    public static bool CardNeedsTarget(CardData card)
    {
        return card != null && (NeedsEnemyTarget(card.combatEffect) || NeedsHeroTarget(card.combatEffect));
    }

    public static bool CardTargetsEnemy(CardData card) => card != null && NeedsEnemyTarget(card.combatEffect);
    public static bool CardTargetsHero(CardData card) => card != null && NeedsHeroTarget(card.combatEffect);

    /// <summary>Turno do jogador, combate em andamento e energia suficiente.</summary>
    public bool CanAffordCard(CardData card)
    {
        return !combatOver && isPlayerTurn && card != null && energy >= card.energyCost;
    }

    /// <summary>
    /// A carta pode ser jogada neste alvo? Alvo nulo significa "sem alvo",
    /// válido apenas para efeitos que não miram ninguém.
    /// </summary>
    public bool CanPlayCard(CardData card, EnemyInstance enemy, HeroData hero)
    {
        if (!CanAffordCard(card)) return false;

        if (NeedsEnemyTarget(card.combatEffect))
            return enemy != null && enemy.IsAlive;

        if (NeedsHeroTarget(card.combatEffect))
            return hero != null && hero.IsAlive;

        // Efeito sem alvo: soltar sobre alguém não invalida a jogada.
        return true;
    }

    /// <summary>
    /// Ponto único de entrada para jogar uma carta. O drag and drop e o teste
    /// automatizado passam por aqui, de modo que os dois exercitam o mesmo caminho.
    /// </summary>
    public bool TryPlayCard(CardData card, EnemyInstance enemy, HeroData hero)
    {
        if (!CanPlayCard(card, enemy, hero))
        {
            if (card != null && !CanAffordCard(card))
                SetInstruction($"Energia insuficiente ({card.energyCost} necessária).");
            return false;
        }

        if (!NeedsEnemyTarget(card.combatEffect)) enemy = null;
        if (!NeedsHeroTarget(card.combatEffect)) hero = null;

        ResolveCard(card, enemy, hero);
        return true;
    }

    /// <summary>Primeiro alvo aceitável para a carta, se houver algum.</summary>
    public bool TryPlayCardOnAnyTarget(CardData card)
    {
        if (CardTargetsEnemy(card))
        {
            var alvo = enemies.FirstOrDefault(e => e.IsAlive);
            return alvo != null && TryPlayCard(card, alvo, null);
        }

        if (CardTargetsHero(card))
        {
            var alvo = party.FirstOrDefault(h => h.IsAlive);
            return alvo != null && TryPlayCard(card, null, alvo);
        }

        return TryPlayCard(card, null, null);
    }

    #endregion

    /// <summary>A carta fere alguém ao ser jogada? Só essas recebem os bônus de dano.</summary>
    public static bool DealsDamage(CombatEffectType effect)
    {
        return effect == CombatEffectType.Damage
            || effect == CombatEffectType.DamageAll
            || effect == CombatEffectType.Poison
            || effect == CombatEffectType.Debuff
            || effect == CombatEffectType.ShieldBreak;
    }

    static bool NeedsEnemyTarget(CombatEffectType effect)
    {
        return effect == CombatEffectType.Damage
            || effect == CombatEffectType.Debuff
            || effect == CombatEffectType.Poison
            || effect == CombatEffectType.ShieldBreak;
    }

    static bool NeedsHeroTarget(CombatEffectType effect)
    {
        return effect == CombatEffectType.Heal
            || effect == CombatEffectType.Block
            || effect == CombatEffectType.Evade
            || effect == CombatEffectType.Cleanse;
    }

    void OnEnemyClicked(EnemyInstance enemy)
    {
        if (pendingCard == null || !enemy.IsAlive) return;
        ResolveCard(pendingCard, enemy, null);
    }

    void OnHeroClicked(HeroData hero)
    {
        if (pendingCard == null || !hero.IsAlive) return;
        ResolveCard(pendingCard, null, hero);
    }

    void ResolveCard(CardData card, EnemyInstance enemyTarget, HeroData heroTarget)
    {
        if (energy < card.energyCost) return;

        energy -= card.energyCost;
        pendingCard = null;

        // Uma carta vale o quanto vale a posição de quem a trouxe: o guerreiro
        // empurrado para trás continua atacando, só que sem alcançar de verdade.
        float power = CardPower(card);
        int rawDamage = PartyFormation.Scale(card.combatDamage, power);
        int block = PartyFormation.Scale(card.combatBlock, power);
        int heal = PartyFormation.Scale(card.combatHeal, power);

        int damage = rawDamage;

        // A arma forjada soma depois da escala: é o aço do dono, não a carta.
        // Os buffos do grupo vêm por cima, e o multiplicador fecha a conta —
        // é o último a entrar para valer sobre tudo que já foi somado.
        //
        // Só vale para carta que fere de verdade: a Fúria carrega combatDamage
        // sem atacar ninguém, e gastar a mira dela desperdiçaria o turno.
        if (damage > 0 && DealsDamage(card.combatEffect))
        {
            damage += ForgeManager.WeaponBonus(CardOwner(card));

            if (groupDamageBonusTurns > 0)
                damage += groupDamageBonus;

            if (nextCardMultiplier > 1f)
            {
                damage = Mathf.RoundToInt(damage * nextCardMultiplier);
                AddLog($"🎯 {card.cardName} sai reforçada.");
                nextCardMultiplier = 1f;
            }
        }

        if (power < 1f)
            AddLog($"⤵ {card.cardName} sai enfraquecida — {DescribeOwnerPlacement(card)}.");

        switch (card.combatEffect)
        {
            case CombatEffectType.Damage:
                DamageEnemy(enemyTarget, damage);
                break;

            case CombatEffectType.DamageAll:
                foreach (var e in enemies.Where(e => e.IsAlive).ToList())
                    DamageEnemy(e, damage);
                AddLog($"{card.cardName} atinge todos os inimigos.");
                break;

            case CombatEffectType.Block:
                if (heroTarget != null)
                {
                    heroBlock[heroTarget] += block;
                    AddLog($"{heroTarget.heroName} ganha {block} de bloqueio.");
                }
                break;

            case CombatEffectType.BlockAll:
                foreach (var hero in party.Where(h => h.IsAlive))
                    heroBlock[hero] += block;
                AddLog($"O grupo ganha {block} de bloqueio.");
                break;

            case CombatEffectType.Heal:
                HealHero(heroTarget, heal);
                break;

            case CombatEffectType.HealAll:
                foreach (var hero in party.Where(h => h.IsAlive).ToList())
                    HealHero(hero, heal);
                break;

            case CombatEffectType.DrawCards:
                for (int i = 0; i < Mathf.Max(1, card.combatDuration); i++)
                    cards.DrawCard();
                AddLog($"{card.cardName}: cartas compradas.");
                break;

            case CombatEffectType.GainEnergy:
                energy += Mathf.Max(1, card.combatDuration);
                AddLog($"{card.cardName}: +energia.");
                break;

            case CombatEffectType.Poison:
                if (enemyTarget != null)
                {
                    DamageEnemy(enemyTarget, Mathf.Max(1, damage));
                    AddLog($"{enemyTarget.data.enemyName} foi envenenado.");
                }
                break;

            case CombatEffectType.ShieldBreak:
                if (enemyTarget != null)
                {
                    enemyTarget.block = 0;
                    DamageEnemy(enemyTarget, damage);
                    AddLog($"A guarda de {enemyTarget.data.enemyName} foi quebrada!");
                }
                break;

            case CombatEffectType.Debuff:
                if (enemyTarget != null)
                {
                    DamageEnemy(enemyTarget, Mathf.Max(1, damage));
                    AddLog($"{enemyTarget.data.enemyName} foi enfraquecido.");
                }
                break;

            case CombatEffectType.Buff:
            {
                // O bônus não empilha: repetir a carta renova o prazo em vez de
                // dobrar o dano, que é como uma mão cheia dela venceria sozinha.
                groupDamageBonus = Mathf.Max(groupDamageBonus, Mathf.Max(1, rawDamage));
                groupDamageBonusTurns = Mathf.Max(groupDamageBonusTurns, Mathf.Max(1, card.combatDuration));
                AddLog($"🔥 O grupo ataca com +{groupDamageBonus} por {groupDamageBonusTurns} turno(s).");
                break;
            }

            case CombatEffectType.BuffNextCard:
                nextCardMultiplier = 1.5f;
                AddLog($"{card.cardName}: a próxima carta de dano sai 50% mais forte.");
                break;

            case CombatEffectType.Evade:
                if (heroTarget != null)
                {
                    evading.Add(heroTarget);
                    AddLog($"💨 {heroTarget.heroName} vai desviar do próximo golpe.");
                }
                break;

            case CombatEffectType.Cleanse:
                if (heroTarget != null)
                {
                    if (MentalStateUtil.IsAffliction(heroTarget.mentalState))
                    {
                        heroTarget.mentalState = MentalState.Normal;
                        AddLog($"✨ {heroTarget.heroName} recobra a compostura.");
                    }

                    heroTarget.stress = Mathf.Max(0f, heroTarget.stress - CleanseStressRelief);
                    AddLog($"{heroTarget.heroName} respira aliviado.");
                }
                break;

            default:
                // Carta sem efeito de combate ainda consome o turno dela.
                AddLog($"{card.cardName} não teve efeito aqui.");
                break;
        }

        cards.PlayCard(card);
        RefreshAll();

        if (enemies.All(e => !e.IsAlive))
        {
            EndCombat(true);
            return;
        }

        SetInstruction("Escolha uma carta.");
    }

    #endregion

    #region Formação

    /// <summary>A formação em vigor neste combate, da frente para a retaguarda.</summary>
    public IReadOnlyList<HeroData> Party => party;

    /// <summary>
    /// Quanto a carta rende agora. Depende de onde está o herói que a emprestou:
    /// bem posicionado, ela sai inteira; fora do lugar, sai reduzida.
    /// </summary>
    public float CardPower(CardData card)
    {
        return ownership == null ? 1f : ownership.PowerMultiplier(card, party);
    }

    /// <summary>Dono da carta com a melhor posição, ou nulo se ninguém a reivindica.</summary>
    public HeroData CardOwner(CardData card)
    {
        return ownership?.BestOwner(card, party);
    }

    /// <summary>Frase curta explicando por que a carta saiu fraca.</summary>
    string DescribeOwnerPlacement(CardData card)
    {
        HeroData owner = CardOwner(card);
        if (owner == null) return "sem ninguém para sustentá-la";

        FormationRow? preferred = PartyFormation.PreferredRow(owner.heroClass);
        string quer = preferred != null
            ? PartyFormation.RowLabel(preferred.Value).ToLower()
            : "qualquer fileira";

        return $"{owner.heroName} está na {PartyFormation.RowLabel(PartyFormation.GetRow(owner, party)).ToLower()}, rende na {quer}";
    }

    #endregion

    #region Dano e cura

    void DamageEnemy(EnemyInstance enemy, int amount)
    {
        if (enemy == null || !enemy.IsAlive || amount <= 0) return;

        int blocked = Mathf.Min(enemy.block, amount);
        enemy.block -= blocked;
        int remaining = amount - blocked;

        enemy.currentHp = Mathf.Max(0, enemy.currentHp - remaining);

        AddLog(blocked > 0
            ? $"{enemy.data.enemyName} bloqueia {blocked} e sofre {remaining}."
            : $"{enemy.data.enemyName} sofre {remaining} de dano.");

        var fx = CombatFeedback.Get();
        if (blocked > 0) fx.ShowBlock(enemy.view, blocked);
        if (remaining > 0)
        {
            fx.ShowDamage(enemy.view, remaining);
            fx.Shake(enemy.view);
        }

        if (!enemy.IsAlive)
        {
            AddLog($"☠️ {enemy.data.enemyName} foi derrotado!");
            fx.ShowText(enemy.view, "☠️", Color.white);
        }
    }

    void DamageHero(HeroData hero, int amount, EventResolver.Resolution resolution)
    {
        if (hero == null || !hero.IsAlive || amount <= 0) return;

        // Quem se preparou para desviar sai limpo deste golpe — e só deste.
        if (evading.Remove(hero))
        {
            AddLog($"💨 {hero.heroName} desvia!");
            CombatFeedback.Get().ShowText(GetHeroView(hero), "💨", Color.white);
            return;
        }

        // A retaguarda é atingida de raspão. Vem antes do bloqueio porque descreve
        // como o golpe chega, não o que o herói faz para aparar.
        amount = PartyFormation.Scale(amount, PartyFormation.DamageTakenMultiplier(hero, party));

        int block = heroBlock.TryGetValue(hero, out int b) ? b : 0;
        int blocked = Mathf.Min(block, amount);
        heroBlock[hero] = block - blocked;

        int remaining = amount - blocked;

        var fx = CombatFeedback.Get();
        GameObject view = GetHeroView(hero);

        if (blocked > 0)
        {
            AddLog($"{hero.heroName} bloqueia {blocked}.");
            fx.ShowBlock(view, blocked);
        }

        if (remaining > 0)
        {
            EventResolver.DealDamage(hero, remaining, party, resolution);
            fx.ShowDamage(view, remaining);
            fx.Shake(view);
        }
    }

    void HealHero(HeroData hero, int amount)
    {
        if (hero == null || !hero.IsAlive || amount <= 0) return;

        int healed = Mathf.Min(amount, hero.maxHp - hero.currentHp);
        hero.currentHp += healed;

        if (hero.isOnDeathsDoor && hero.currentHp > 0)
        {
            hero.isOnDeathsDoor = false;
            AddLog($"✨ {hero.heroName} saiu da Beira da Morte.");
        }
        else if (healed > 0)
        {
            AddLog($"❤️ {hero.heroName} recupera {healed} HP.");
        }

        if (healed > 0)
            CombatFeedback.Get().ShowHeal(GetHeroView(hero), healed);
    }

    /// <summary>View do herói na tela, se ela existir neste momento.</summary>
    GameObject GetHeroView(HeroData hero)
    {
        if (hero == null) return null;
        GameObject view;
        return heroViews.TryGetValue(hero, out view) ? view : null;
    }

    #endregion

    #region UI

    void RefreshAll()
    {
        RefreshEnemies();
        RefreshHeroes();
        RefreshHand();
        RefreshCounters();
    }

    void BuildEnemyViews()
    {
        if (enemyContainer == null || enemyPrefab == null) return;

        UIUtil.ClearChildrenNow(enemyContainer);

        foreach (var enemy in enemies)
        {
            enemy.view = Instantiate(enemyPrefab, enemyContainer);

            Button btn = enemy.view.GetComponent<Button>();
            if (btn != null)
            {
                EnemyInstance captured = enemy;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnEnemyClicked(captured));
            }
        }
    }

    void RefreshEnemies()
    {
        foreach (var enemy in enemies)
        {
            if (enemy.view == null) continue;

            enemy.view.SetActive(enemy.IsAlive);
            if (!enemy.IsAlive) continue;

            SetText(enemy.view, "Name", enemy.data.enemyName);
            SetText(enemy.view, "HP", $"{enemy.currentHp}/{enemy.data.maxHp}");
            SetText(enemy.view, "Intent", DescribeIntent(enemy));
            SetText(enemy.view, "Block", enemy.block > 0 ? $"🛡️ {enemy.block}" : "");

            Image hpBar = enemy.view.transform.Find("HPBar/Fill")?.GetComponent<Image>();
            if (hpBar != null)
            {
                float alvo = enemy.data.maxHp > 0 ? (float)enemy.currentHp / enemy.data.maxHp : 0f;
                CombatFeedback.Get().LerpBar(hpBar, alvo);

                // Vermelho quando a vida está baixa: legível de relance.
                hpBar.color = alvo <= 0.25f ? new Color(0.80f, 0.25f, 0.22f)
                            : alvo <= 0.5f ? new Color(0.85f, 0.65f, 0.25f)
                                            : new Color(0.45f, 0.68f, 0.38f);
            }

            Image portrait = enemy.view.transform.Find("Portrait")?.GetComponent<Image>();
            if (portrait != null && enemy.data.portrait != null)
                portrait.sprite = enemy.data.portrait;

            // Alvo de drop: é assim que a carta chega ao inimigo.
            var target = enemy.view.GetComponent<CombatDropTarget>();
            if (target == null) target = enemy.view.AddComponent<CombatDropTarget>();
            target.Bind(enemy, null);
            if (!activeTargets.Contains(target)) activeTargets.Add(target);

            // O clique deixou de jogar cartas; sem isto o botão engoliria o drop.
            Button btn = enemy.view.GetComponent<Button>();
            if (btn != null) btn.interactable = false;
        }

        activeTargets.RemoveAll(t => t == null);
    }

    /// <summary>
    /// O que o inimigo fará no próximo turno. Dizer o verbo junto do número
    /// evita que "🛡️ 6" seja lido como dano — a intenção precisa ser inequívoca
    /// para o jogador decidir entre atacar e se defender.
    /// </summary>
    string DescribeIntent(EnemyInstance enemy)
    {
        switch (enemy.intent)
        {
            case EnemyIntent.Attack:
                return $"<color=#E04B44>⚔️ Ataca {enemy.data.attackDamage}</color>";

            case EnemyIntent.AttackAll:
                return $"<color=#E04B44>💥 Ataca todos ({Mathf.RoundToInt(enemy.data.attackDamage * 0.6f)})</color>";

            case EnemyIntent.Defend:
                return $"<color=#8CB8F0>🛡️ Defende {enemy.data.blockAmount}</color>";

            case EnemyIntent.Stress:
                return $"<color=#D9B85A>🧠 Aterroriza {enemy.data.stressDamage}</color>";

            default:
                return "<color=#9A9A9A>❔ Indeciso</color>";
        }
    }

    void RefreshHeroes()
    {
        if (heroContainer == null || heroStatusPrefab == null) return;

        UIUtil.ClearChildrenNow(heroContainer);

        // As views antigas morreram junto com os filhos.
        activeTargets.RemoveAll(t => t == null || t.hero != null);
        heroViews.Clear();

        foreach (var hero in party)
        {
            GameObject view = Instantiate(heroStatusPrefab, heroContainer);
            heroViews[hero] = view;

            // A posição precisa estar visível durante a luta: é ela que explica
            // por que este herói está apanhando e por que aquela carta saiu fraca.
            // Quem morreu sai da fila, então não tem posição para mostrar.
            int position = PartyFormation.GetPosition(hero, party);
            if (position < 0)
            {
                SetText(view, "Name", hero.heroName);
            }
            else
            {
                bool front = position < PartyFormation.FrontSlots;
                string aviso = PartyFormation.IsWellPlaced(hero, party) ? "" : " <color=#B04040>⚠️</color>";

                SetText(view, "Name",
                    $"<color=#8CB8F0>{position + 1}{(front ? "⚔️" : "🏹")}</color> {hero.heroName}{aviso}");
            }
            SetText(view, "HP", hero.isDead ? "💀"
                              : hero.isOnDeathsDoor ? "<color=#E04B44>☠️ BEIRA DA MORTE</color>"
                              : $"{hero.currentHp}/{hero.maxHp}");

            // Estresse alto é o que antecede uma Aflição: precisa saltar aos olhos.
            int stress = Mathf.RoundToInt(hero.stress);
            SetText(view, "Stress", stress >= 75 ? $"<color=#D9B85A>🧠 {stress}</color>" : $"🧠 {stress}");

            int block = heroBlock.TryGetValue(hero, out int b) ? b : 0;
            SetText(view, "Block", block > 0 ? $"<color=#8CB8F0>🛡️ {block}</color>" : "");

            Image hpBar = view.transform.Find("HPBar/Fill")?.GetComponent<Image>();
            if (hpBar != null)
            {
                float alvo = hero.maxHp > 0 ? (float)hero.currentHp / hero.maxHp : 0f;
                CombatFeedback.Get().LerpBar(hpBar, alvo);

                hpBar.color = hero.isOnDeathsDoor ? new Color(0.55f, 0.15f, 0.15f)
                            : alvo <= 0.25f ? new Color(0.80f, 0.25f, 0.22f)
                            : alvo <= 0.5f ? new Color(0.85f, 0.65f, 0.25f)
                                            : new Color(0.45f, 0.68f, 0.38f);
            }

            var target = view.GetComponent<CombatDropTarget>();
            if (target == null) target = view.AddComponent<CombatDropTarget>();
            target.Bind(null, hero);
            activeTargets.Add(target);

            // Cartas chegam por arrasto; o botão não deve capturar o ponteiro.
            Button btn = view.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.interactable = false;
            }
        }
    }

    void RefreshHand()
    {
        if (handContainer == null || cardPrefab == null || cards == null) return;

        // Imediato: com Destroy adiado, a mão nova era desenhada por cima da
        // antiga e as cartas apareciam sobrepostas.
        UIUtil.ClearChildrenNow(handContainer);

        // Leque no lugar do layout horizontal, que espremia as cartas.
        var fan = handContainer.GetComponent<HandFanLayout>();
        if (fan == null) fan = handContainer.gameObject.AddComponent<HandFanLayout>();

        var legacy = handContainer.GetComponent<LayoutGroup>();
        if (legacy != null) Destroy(legacy);

        foreach (var card in cards.hand)
        {
            GameObject view = Instantiate(cardPrefab, handContainer);

            // O prefab traz um CardUI com as referências já ligadas. Preencher
            // por nome de filho não funcionava — o prefab usa "CardName" e
            // "CardDescription", e as cartas ficavam com o "New Text" do editor.
            var cardUI = view.GetComponent<CardUI>();
            if (cardUI != null)
            {
                cardUI.Bind(card, journeyMode: false);
            }
            else
            {
                SetText(view, "CardName", card.cardName);
                SetText(view, "CardDescription", card.GetDescription(false));
                SetText(view, "CostTxt", $"⚡ {card.energyCost}");
            }

            // Aviso na própria carta: o jogador precisa ver o preço da má formação
            // antes de jogar, não no log depois.
            float power = CardPower(card);
            HeroData owner = CardOwner(card);
            int weapon = card.combatDamage > 0 ? ForgeManager.WeaponBonus(owner) : 0;

            string nota = "";
            if (power < 1f)
                nota = $"<color=#B04040>⤵ {Mathf.RoundToInt(power * 100)}% — "
                     + $"{(owner != null ? owner.heroName : "dono")} fora de posição</color>";

            if (weapon > 0)
                nota += (nota.Length > 0 ? "\n" : "") + $"<color=#7FB069>⚔️ +{weapon} da forja</color>";

            if (nota.Length > 0)
            {
                if (cardUI != null) cardUI.AppendNote(nota);
                else SetText(view, "CardDescription", card.GetDescription(false) + "\n" + nota);
            }

            // Jogar é arrastar: a carta ganha o handler em vez de um listener de clique.
            var drag = view.GetComponent<CardDragHandler>();
            if (drag == null) drag = view.AddComponent<CardDragHandler>();
            drag.Initialize(card);

            // Carta sem energia fica visivelmente indisponível, mas ainda
            // responde ao gesto para poder explicar o motivo.
            var group = view.GetComponent<CanvasGroup>();
            if (group == null) group = view.AddComponent<CanvasGroup>();
            group.alpha = (isPlayerTurn && energy >= card.energyCost) ? 1f : 0.55f;

            Button btn = view.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.interactable = false;
            }
        }

        // Recria os gatilhos de hover para os filhos novos.
        fan.Rebuild();
    }

    void RefreshCounters()
    {
        if (turnText != null) turnText.text = $"Turno {turn}";
        if (energyText != null) energyText.text = $"⚡ {energy}/{maxEnergy}";
        if (deckCountText != null && cards != null) deckCountText.text = $"📚 {cards.drawPile.Count}";
        if (discardCountText != null && cards != null) discardCountText.text = $"🗑️ {cards.discardPile.Count}";
        if (endTurnButton != null) endTurnButton.interactable = isPlayerTurn && !combatOver;
    }

    void SetText(GameObject root, string childName, string value)
    {
        TMP_Text target = root.transform.Find(childName)?.GetComponent<TMP_Text>();
        if (target != null) target.text = value;
    }

    void SetInstruction(string message)
    {
        if (instructionText != null)
            instructionText.text = message;
    }

    void AddLog(string line)
    {
        if (string.IsNullOrEmpty(line)) return;

        log.Add(line);
        while (log.Count > 8)
            log.RemoveAt(0);

        if (combatLogText != null)
            combatLogText.text = string.Join("\n", log);
    }

    #endregion
}
