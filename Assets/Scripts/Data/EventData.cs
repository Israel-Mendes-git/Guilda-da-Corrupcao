using UnityEngine;

[CreateAssetMenu(fileName = "New Event", menuName = "Game/Event")]
public class EventData : ScriptableObject
{
    [Header("Informações")]
    public string eventTitle;
    [TextArea(3, 5)] public string description;
    public Sprite eventImage;

    [Header("Filtros")]
    public BiomeType biome = BiomeType.Any;
    [Range(0, 100)] public int minCorruptionToAppear;
    public int minDay;  // A partir de qual dia pode aparecer

    [Header("Opções")]
    public EventOutcome[] outcomes;

    [Header("Tipo")]
    public JourneyEventType eventType;
    public bool isBossEvent;   // Marca o confronto final da jornada
}

[System.Serializable]
public class EventOutcome
{
    [TextArea(1, 2)] public string optionText;
    public EventConsequences consequences;
    public int extraDays;
    public bool triggersCorruption;
}

[System.Serializable]
public class EventConsequences
{
    public int goldChange;
    public int reputationChange;
    public HeroEffect[] heroEffects;
    public MoraleChange[] moraleChanges;
}

[System.Serializable]
public class HeroEffect
{
    public string heroName; // "All" para todos, "Random" para um aleatório
    public int hpChange;
    public bool addInjury;
    public bool addTrait; // Adiciona traço negativo
}

[System.Serializable]
public class MoraleChange
{
    public string heroName;
    public int moraleChange;
}

/// <summary>
/// Espécie de evento da jornada.
///
/// Chamava-se apenas <c>EventType</c>, no namespace global — onde vencia o
/// <c>UnityEngine.EventType</c> em qualquer script que não qualificasse o nome.
/// Todo pacote de terceiros importado no projeto passava a não compilar (o
/// DialogueEditor e o Missing Scripts Tool caíram assim). O prefixo tira o jogo
/// do caminho de quem chega depois.
/// </summary>
public enum JourneyEventType { Normal, Combat, Treasure, Trap, Rest, Shop, Story }
