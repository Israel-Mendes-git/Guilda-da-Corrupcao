#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// Semeia o pool de cartas em <c>Resources/Cards</c>.
///
/// Esta janela gerou as 16 cartas originais e depois ficou para trás: os assets
/// foram corrigidos no Inspector (Teleporte virou <c>Evade</c>, Purificação
/// virou <c>Cleanse</c>, Olhar de Águia virou <c>BuffNextCard</c>) enquanto o
/// código aqui continuava mandando <c>None</c> — os efeitos que tornavam a carta
/// morta em combate. Rodar "Criar Todas" desfazia a correção sem avisar.
///
/// Duas travas contra isso: a tabela abaixo é um espelho fiel dos assets atuais,
/// e nada é sobrescrito a menos que se peça explicitamente.
/// </summary>
public class CardCreator : EditorWindow
{
    private bool sobrescreverExistentes;

    [MenuItem("Tools/Card Creator")]
    public static void ShowWindow()
    {
        GetWindow<CardCreator>("Card Creator");
    }

    void OnGUI()
    {
        GUILayout.Label("Criar Cartas Base", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Cria em Resources/Cards as cartas que ainda não existem.\n" +
            "Cartas já existentes são preservadas — marque a opção abaixo só se " +
            "quiser mesmo devolvê-las ao estado original.",
            MessageType.Info);

        sobrescreverExistentes = EditorGUILayout.ToggleLeft(
            "Sobrescrever cartas existentes (descarta ajustes feitos no Inspector)",
            sobrescreverExistentes);

        EditorGUILayout.Space();

        if (GUILayout.Button("Criar Todas as Cartas"))
            CriarTodas();

        EditorGUILayout.Space();

        if (GUILayout.Button("Criar Cartas de Guerreiro")) CriarDaClasse(HeroClass.Warrior);
        if (GUILayout.Button("Criar Cartas de Mago")) CriarDaClasse(HeroClass.Mage);
        if (GUILayout.Button("Criar Cartas de Curandeiro")) CriarDaClasse(HeroClass.Healer);
        if (GUILayout.Button("Criar Cartas de Caçador")) CriarDaClasse(HeroClass.Hunter);
    }

    void CriarTodas()
    {
        int criadas = 0;
        foreach (var def in Definicoes)
            criadas += Criar(def) ? 1 : 0;

        Finalizar(criadas, Definicoes.Length);
    }

    void CriarDaClasse(HeroClass classe)
    {
        int criadas = 0;
        int total = 0;
        foreach (var def in Definicoes)
        {
            if (def.classe != classe) continue;
            total++;
            criadas += Criar(def) ? 1 : 0;
        }

        Finalizar(criadas, total);
    }

    void Finalizar(int criadas, int total)
    {
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Card Creator: {criadas} de {total} cartas gravadas ({total - criadas} preservadas).");
    }

    /// <summary>Grava a carta. Devolve false quando a existente foi preservada.</summary>
    bool Criar(Definicao def)
    {
        string pasta = $"Assets/Resources/Cards/{def.classe}";
        string caminho = $"{pasta}/{def.nome}.asset";

        bool jaExiste = AssetDatabase.LoadAssetAtPath<CardData>(caminho) != null;
        if (jaExiste && !sobrescreverExistentes)
            return false;

        if (!System.IO.Directory.Exists(pasta))
            System.IO.Directory.CreateDirectory(pasta);

        CardData card = ScriptableObject.CreateInstance<CardData>();
        card.cardName = def.nome;
        card.cardDescription = def.nome;
        card.requiredClass = def.classe;
        card.rarity = def.raridade;
        card.energyCost = def.custo;

        card.journeyEffect = def.efeitoJornada;
        card.journeyEffectValue = def.valorJornada;
        card.journeyEffectDescription = def.descricaoJornada;

        card.combatEffect = def.efeitoCombate;
        card.combatDamage = def.dano;
        card.combatBlock = def.bloqueio;
        card.combatHeal = def.cura;
        card.combatDuration = def.duracao;
        card.combatEffectDescription = def.descricaoCombate;

        if (jaExiste)
            AssetDatabase.DeleteAsset(caminho);

        AssetDatabase.CreateAsset(card, caminho);
        return true;
    }

    private struct Definicao
    {
        public string nome;
        public HeroClass classe;
        public CardRarity raridade;
        public int custo;
        public JourneyEffectType efeitoJornada;
        public int valorJornada;
        public string descricaoJornada;
        public CombatEffectType efeitoCombate;
        public int dano, bloqueio, cura, duracao;
        public string descricaoCombate;
    }

    private static Definicao Def(string nome, HeroClass classe, CardRarity raridade, int custo,
        JourneyEffectType efeitoJornada, int valorJornada, string descricaoJornada,
        CombatEffectType efeitoCombate, int dano, int bloqueio, int cura, int duracao, string descricaoCombate)
    {
        return new Definicao
        {
            nome = nome, classe = classe, raridade = raridade, custo = custo,
            efeitoJornada = efeitoJornada, valorJornada = valorJornada, descricaoJornada = descricaoJornada,
            efeitoCombate = efeitoCombate, dano = dano, bloqueio = bloqueio, cura = cura, duracao = duracao,
            descricaoCombate = descricaoCombate
        };
    }

    /// <summary>Espelho dos assets em Resources/Cards. Ao ajustar uma carta no
    /// Inspector, ajustar aqui também — senão as duas fontes divergem de novo.</summary>
    private static readonly Definicao[] Definicoes =
    {
        // ⚔️ Guerreiro
        Def("Corte Duplo", HeroClass.Warrior, CardRarity.Common, 2,
            JourneyEffectType.RemoveObstacle, 0, "Corta galhos e abre caminho. Ignora 1 evento de floresta.",
            CombatEffectType.DamageAll, 8, 0, 0, 0, "8 de dano em 2 inimigos diferentes."),

        Def("Postura Defensiva", HeroClass.Warrior, CardRarity.Common, 2,
            JourneyEffectType.ProtectFromWeather, 0, "Protege o grupo contra dano por 2 dias.",
            CombatEffectType.Block, 0, 10, 0, 0, "Ganha 10 de bloqueio."),

        Def("Fúria", HeroClass.Warrior, CardRarity.Rare, 1,
            JourneyEffectType.RestoreMorale, 10, "Aumenta o moral do grupo em +10.",
            CombatEffectType.Buff, 3, 0, 2, 2, "Aumenta o dano em +3 por 2 turnos."),

        Def("Investida", HeroClass.Warrior, CardRarity.Epic, 3,
            JourneyEffectType.Teleport, 2, "Atravessa terreno difícil. Pula 2 dias.",
            CombatEffectType.Damage, 15, 0, 0, 0, "15 de dano em um alvo."),

        // Única carta com Intimidate: é o que permite recusar um combate na
        // estrada. O efeito existia no JourneyManager sem nenhuma carta que o
        // produzisse — código vivo que nunca rodava.
        Def("Brado de Guerra", HeroClass.Warrior, CardRarity.Rare, 2,
            JourneyEffectType.Intimidate, 0, "O grito ecoa pelo vale: o próximo bando recua sem lutar.",
            CombatEffectType.BlockAll, 0, 6, 0, 0, "6 de bloqueio para todos os aliados."),

        // 🔮 Mago
        Def("Bola de Fogo", HeroClass.Mage, CardRarity.Common, 3,
            JourneyEffectType.RemoveObstacle, 0, "Queima obstáculos. Remove 1 evento de armadilha.",
            CombatEffectType.DamageAll, 12, 0, 0, 0, "12 de dano em área."),

        Def("Escudo de Gelo", HeroClass.Mage, CardRarity.Common, 2,
            JourneyEffectType.ProtectFromWeather, 0, "Protege contra clima extremo por 2 dias.",
            CombatEffectType.BlockAll, 0, 8, 0, 0, "8 de bloqueio para todos aliados."),

        Def("Teleporte", HeroClass.Mage, CardRarity.Rare, 3,
            JourneyEffectType.SkipDay, 1, "Teletransporta o grupo. Pula 1 dia.",
            CombatEffectType.Evade, 0, 0, 0, 0, "Evita o próximo ataque."),

        Def("Explosão Arcana", HeroClass.Mage, CardRarity.Epic, 4,
            JourneyEffectType.RemoveObstacle, 0, "Destrói qualquer obstáculo.",
            CombatEffectType.DamageAll, 20, 0, 0, 0, "20 de dano em todos inimigos."),

        // ⚕️ Curandeiro
        Def("Toque Curativo", HeroClass.Healer, CardRarity.Common, 1,
            JourneyEffectType.HealInjury, 0, "Cura ferimentos de 1 herói.",
            CombatEffectType.Heal, 0, 0, 8, 0, "Cura 8 HP de um aliado."),

        Def("Bênção", HeroClass.Healer, CardRarity.Common, 2,
            JourneyEffectType.RestoreMorale, 10, "Aumenta o moral do grupo em +10.",
            CombatEffectType.HealAll, 0, 0, 5, 0, "Cura 5 HP para todos aliados."),

        Def("Purificação", HeroClass.Healer, CardRarity.Rare, 2,
            JourneyEffectType.Purify, 0, "Remove maldições e doenças.",
            CombatEffectType.Cleanse, 0, 0, 0, 0, "Remove a aflição e alivia o estresse."),

        Def("Ressurgir", HeroClass.Healer, CardRarity.Epic, 4,
            JourneyEffectType.Revive, 0, "Traz um herói de volta da beira da morte e devolve metade da vida.",
            CombatEffectType.Heal, 0, 0, 5, 0, "Revive um aliado com 5 HP."),

        // 🏹 Caçador
        Def("Flecha Precisa", HeroClass.Hunter, CardRarity.Common, 1,
            JourneyEffectType.RemoveObstacle, 0, "Acerta o ponto exato: abre passagem por rios e cavernas.",
            CombatEffectType.Damage, 6, 0, 0, 0, "6 de dano, ignora bloqueio."),

        Def("Armadilha", HeroClass.Hunter, CardRarity.Common, 2,
            JourneyEffectType.GainFood, 2, "Captura comida. Ganha +2 rações.",
            CombatEffectType.Damage, 10, 0, 0, 0, "10 de dano quando inimigo ataca."),

        Def("Olhar de Águia", HeroClass.Hunter, CardRarity.Rare, 1,
            JourneyEffectType.RevealNextEvent, 0, "Revela o próximo evento.",
            CombatEffectType.BuffNextCard, 0, 0, 0, 0, "Próxima carta dá +50% dano."),

        Def("Flecha Lunar", HeroClass.Hunter, CardRarity.Epic, 3,
            JourneyEffectType.RevealNextEvent, 0, "Revela todo o mapa da região.",
            CombatEffectType.ShieldBreak, 20, 0, 0, 0, "20 de dano, ignora bloqueio.")
    };
}
#endif
