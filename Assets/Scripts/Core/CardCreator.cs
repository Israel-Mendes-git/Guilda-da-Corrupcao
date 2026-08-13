#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public class CardCreator : EditorWindow
{
    [MenuItem("Tools/Card Creator")]
    public static void ShowWindow()
    {
        GetWindow<CardCreator>("Card Creator");
    }

    void OnGUI()
    {
        GUILayout.Label("Criar Cartas Base", EditorStyles.boldLabel);

        if (GUILayout.Button("Criar Todas as Cartas"))
        {
            CreateAllCards();
        }

        if (GUILayout.Button("Criar Cartas de Guerreiro"))
        {
            CreateWarriorCards();
        }

        if (GUILayout.Button("Criar Cartas de Mago"))
        {
            CreateMageCards();
        }

        if (GUILayout.Button("Criar Cartas de Curandeiro"))
        {
            CreateHealerCards();
        }

        if (GUILayout.Button("Criar Cartas de Caçador"))
        {
            CreateHunterCards();
        }
    }

    void CreateAllCards()
    {
        CreateWarriorCards();
        CreateMageCards();
        CreateHealerCards();
        CreateHunterCards();
        AssetDatabase.Refresh();
        Debug.Log("Todas as cartas foram criadas!");
    }

    void CreateWarriorCards()
    {
        CreateCard("Corte Duplo", HeroClass.Warrior, CardRarity.Common, 2,
            "Corte Duplo", "Corta galhos e abre caminho. Ignora 1 evento de floresta.",
            JourneyEffectType.RemoveObstacle, 0,
            "8 de dano em 2 inimigos diferentes.", CombatEffectType.DamageAll, 8, 0, 0);

        CreateCard("Postura Defensiva", HeroClass.Warrior, CardRarity.Common, 2,
            "Postura Defensiva", "Protege o grupo contra dano por 2 dias.",
            JourneyEffectType.ProtectFromWeather, 0,
            "Ganha 10 de bloqueio.", CombatEffectType.Block, 0, 10, 0);

        CreateCard("Fúria", HeroClass.Warrior, CardRarity.Rare, 1,
            "Fúria", "Aumenta o moral do grupo em +10.",
            JourneyEffectType.RestoreMorale, 10,
            "Aumenta o dano em +3 por 2 turnos.", CombatEffectType.Buff, 3, 0, 2);

        CreateCard("Investida", HeroClass.Warrior, CardRarity.Epic, 3,
            "Investida", "Atravessa terreno difícil. Pula 2 dias.",
            JourneyEffectType.Teleport, 2,
            "15 de dano em um alvo.", CombatEffectType.Damage, 15, 0, 0);
    }

    void CreateMageCards()
    {
        CreateCard("Bola de Fogo", HeroClass.Mage, CardRarity.Common, 3,
            "Bola de Fogo", "Queima obstáculos. Remove 1 evento de armadilha.",
            JourneyEffectType.RemoveObstacle, 0,
            "12 de dano em área.", CombatEffectType.DamageAll, 12, 0, 0);

        CreateCard("Escudo de Gelo", HeroClass.Mage, CardRarity.Common, 2,
            "Escudo de Gelo", "Protege contra clima extremo por 2 dias.",
            JourneyEffectType.ProtectFromWeather, 0,
            "8 de bloqueio para todos aliados.", CombatEffectType.BlockAll, 0, 8, 0);

        CreateCard("Teleporte", HeroClass.Mage, CardRarity.Rare, 3,
            "Teleporte", "Teletransporta o grupo. Pula 1 dia.",
            JourneyEffectType.SkipDay, 1,
            "Evita o próximo ataque.", CombatEffectType.None, 0, 0, 0);

        CreateCard("Explosão Arcana", HeroClass.Mage, CardRarity.Epic, 4,
            "Explosão Arcana", "Destrói qualquer obstáculo.",
            JourneyEffectType.RemoveObstacle, 0,
            "20 de dano em todos inimigos.", CombatEffectType.DamageAll, 20, 0, 0);
    }

    void CreateHealerCards()
    {
        CreateCard("Toque Curativo", HeroClass.Healer, CardRarity.Common, 1,
            "Toque Curativo", "Cura ferimentos de 1 herói.",
            JourneyEffectType.HealInjury, 0,
            "Cura 8 HP de um aliado.", CombatEffectType.Heal, 0, 0, 8);

        CreateCard("Bênção", HeroClass.Healer, CardRarity.Common, 2,
            "Bênção", "Aumenta o moral do grupo em +10.",
            JourneyEffectType.RestoreMorale, 10,
            "Cura 5 HP para todos aliados.", CombatEffectType.HealAll, 0, 0, 5);

        CreateCard("Purificação", HeroClass.Healer, CardRarity.Rare, 2,
            "Purificação", "Remove maldições e doenças.",
            JourneyEffectType.Purify, 0,
            "Remove todos debuffs de um aliado.", CombatEffectType.None, 0, 0, 0);

        CreateCard("Ressurgir", HeroClass.Healer, CardRarity.Epic, 4,
            "Ressurgir", "Revive um herói morto (1 por jornada).",
            JourneyEffectType.None, 0,
            "Revive um aliado com 5 HP.", CombatEffectType.Heal, 0, 0, 5);
    }

    void CreateHunterCards()
    {
        CreateCard("Flecha Precisa", HeroClass.Hunter, CardRarity.Common, 1,
            "Flecha Precisa", "Atravessa rios e cavernas facilmente.",
            JourneyEffectType.None, 0,
            "6 de dano, ignora bloqueio.", CombatEffectType.Damage, 6, 0, 0);

        CreateCard("Armadilha", HeroClass.Hunter, CardRarity.Common, 2,
            "Armadilha", "Captura comida. Ganha +2 rações.",
            JourneyEffectType.GainFood, 2,
            "10 de dano quando inimigo ataca.", CombatEffectType.Damage, 10, 0, 0);

        CreateCard("Olhar de Águia", HeroClass.Hunter, CardRarity.Rare, 1,
            "Olhar de Águia", "Revela o próximo evento.",
            JourneyEffectType.RevealNextEvent, 0,
            "Próxima carta dá +50% dano.", CombatEffectType.Buff, 0, 0, 0);

        CreateCard("Flecha Lunar", HeroClass.Hunter, CardRarity.Epic, 3,
            "Flecha Lunar", "Revela todo o mapa da região.",
            JourneyEffectType.RevealNextEvent, 0,
            "20 de dano, ignora bloqueio.", CombatEffectType.ShieldBreak, 20, 0, 0);
    }

    void CreateCard(string name, HeroClass heroClass, CardRarity rarity, int cost,
        string cardName, string journeyDesc, JourneyEffectType journeyEffect, int journeyValue,
        string combatDesc, CombatEffectType combatEffect, int damage, int block, int heal)
    {
        CardData card = ScriptableObject.CreateInstance<CardData>();
        card.cardName = name;
        card.cardDescription = cardName;
        card.requiredClass = heroClass;
        card.rarity = rarity;
        card.energyCost = cost;

        card.journeyEffectDescription = journeyDesc;
        card.journeyEffect = journeyEffect;
        card.journeyEffectValue = journeyValue;

        card.combatEffectDescription = combatDesc;
        card.combatEffect = combatEffect;
        card.combatDamage = damage;
        card.combatBlock = block;
        card.combatHeal = heal;

        string path = $"Assets/Resources/Cards/{heroClass}/{name}.asset";

        if (!System.IO.Directory.Exists($"Assets/Resources/Cards/{heroClass}"))
            System.IO.Directory.CreateDirectory($"Assets/Resources/Cards/{heroClass}");

        AssetDatabase.CreateAsset(card, path);
        AssetDatabase.SaveAssets();
    }
}
#endif