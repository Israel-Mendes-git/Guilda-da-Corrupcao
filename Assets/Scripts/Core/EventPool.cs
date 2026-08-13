using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public static class EventPool
{
    private static List<EventData> allEvents = new List<EventData>();
    private static bool isInitialized = false;

    // Evita que o mesmo evento apareça em dias seguidos.
    private static readonly Queue<EventData> recentlyUsed = new Queue<EventData>();
    private const int RecentMemory = 3;

    public static void Initialize()
    {
        if (isInitialized) return;

        allEvents.Clear();

        // Carrega eventos da pasta Resources
        EventData[] loadedEvents = Resources.LoadAll<EventData>("Events");
        allEvents.AddRange(loadedEvents);

        // Se não tem eventos, cria eventos padrão
        if (allEvents.Count == 0)
        {
            CreateDefaultEvents();
        }

        isInitialized = true;
        Debug.Log($"EventPool inicializado com {allEvents.Count} eventos");
    }

    /// <summary>Limpa a memória de eventos recentes. Chamar ao começar uma jornada.</summary>
    public static void ResetHistory()
    {
        recentlyUsed.Clear();
    }

    public static EventData GetRandomEvent(BiomeType biome, int corruptionLevel, int currentDay)
    {
        Initialize();

        List<EventData> valid = allEvents.FindAll(e =>
            !e.isBossEvent &&
            BiomeUtil.Matches(e.biome, biome) &&
            e.minCorruptionToAppear <= corruptionLevel &&
            e.minDay <= currentDay
        );

        if (valid.Count == 0)
            return GetDefaultEvent();

        // Sem repetir o que acabou de acontecer, desde que sobre alternativa.
        List<EventData> fresh = valid.Where(e => !recentlyUsed.Contains(e)).ToList();
        List<EventData> pool = fresh.Count > 0 ? fresh : valid;

        // Eventos do bioma têm prioridade sobre os genéricos, para dar identidade à região.
        List<EventData> specific = pool.Where(e => e.biome == biome).ToList();
        if (specific.Count > 0 && Random.value < 0.7f)
            pool = specific;

        EventData chosen = pool[Random.Range(0, pool.Count)];
        Remember(chosen);
        return chosen;
    }

    public static EventData GetFinalEvent(BiomeType biome)
    {
        Initialize();

        List<EventData> bosses = allEvents.FindAll(e => e.isBossEvent && BiomeUtil.Matches(e.biome, biome));

        // Prefere o chefe próprio da região.
        List<EventData> specific = bosses.Where(e => e.biome == biome).ToList();
        if (specific.Count > 0)
            return specific[Random.Range(0, specific.Count)];

        if (bosses.Count > 0)
            return bosses[Random.Range(0, bosses.Count)];

        return GetDefaultBossEvent();
    }

    static void Remember(EventData e)
    {
        recentlyUsed.Enqueue(e);
        while (recentlyUsed.Count > RecentMemory)
            recentlyUsed.Dequeue();
    }

    static void CreateDefaultEvents()
    {
        // Rede de segurança: só entra em uso se Resources/Events estiver vazia.
        EventData bridgeEvent = ScriptableObject.CreateInstance<EventData>();
        bridgeEvent.eventTitle = "🌉 Ponte Quebrada";
        bridgeEvent.description = "O grupo encontra uma ponte de madeira que desabou. O rio está agitado e gelado.";
        bridgeEvent.biome = BiomeType.Forest;
        bridgeEvent.outcomes = new EventOutcome[]
        {
            new EventOutcome {
                optionText = "🌊 Nadar pelo rio",
                consequences = new EventConsequences {
                    heroEffects = new[] { new HeroEffect { heroName = "All", hpChange = -5 } }
                }
            },
            new EventOutcome {
                optionText = "🔨 Construir ponte temporária",
                extraDays = 1
            },
            new EventOutcome {
                optionText = "🏔️ Rodear pela montanha",
                extraDays = 2
            }
        };
        allEvents.Add(bridgeEvent);

        EventData bossEvent = ScriptableObject.CreateInstance<EventData>();
        bossEvent.eventTitle = "⚔️ CONFRONTO FINAL ⚔️";
        bossEvent.description = "O guardião da masmorra surge diante de vocês! Uma criatura colossal de pedra e chamas.";
        bossEvent.eventType = JourneyEventType.Combat;
        bossEvent.isBossEvent = true;
        bossEvent.biome = BiomeType.Any;
        bossEvent.outcomes = new EventOutcome[]
        {
            new EventOutcome {
                optionText = "💪 Lutar com tudo!",
                consequences = new EventConsequences {
                    heroEffects = new[] { new HeroEffect { heroName = "Random", hpChange = -15 } },
                    goldChange = 200
                }
            },
            new EventOutcome {
                optionText = "🎯 Estratégia cuidadosa",
                consequences = new EventConsequences {
                    heroEffects = new[] { new HeroEffect { heroName = "All", hpChange = -5 } },
                    goldChange = 150
                }
            }
        };
        allEvents.Add(bossEvent);
    }

    static EventData GetDefaultEvent()
    {
        EventData defaultEvent = ScriptableObject.CreateInstance<EventData>();
        defaultEvent.eventTitle = "🚶 Seguindo o Caminho";
        defaultEvent.description = "O grupo continua a jornada sem incidentes.";
        defaultEvent.outcomes = new EventOutcome[]
        {
            new EventOutcome { optionText = "Continuar", consequences = new EventConsequences() }
        };
        return defaultEvent;
    }

    static EventData GetDefaultBossEvent()
    {
        EventData bossEvent = ScriptableObject.CreateInstance<EventData>();
        bossEvent.eventTitle = "⚔️ Chefão da Masmorra ⚔️";
        bossEvent.description = "O guardião aparece! Um combate épico começa.";
        bossEvent.eventType = JourneyEventType.Combat;
        bossEvent.isBossEvent = true;
        bossEvent.outcomes = new EventOutcome[]
        {
            new EventOutcome {
                optionText = "Lutar",
                consequences = new EventConsequences {
                    heroEffects = new[] { new HeroEffect { heroName = "All", hpChange = -10 } },
                    goldChange = 100
                }
            }
        };
        return bossEvent;
    }
}
