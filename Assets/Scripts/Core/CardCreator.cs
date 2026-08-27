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
///
/// <b>Segunda leva (17 → 40 cartas).</b> Com 4 cartas por classe, o baralho de um
/// Mago era <i>o</i> baralho de Mago: o gerador enchia onze espaços sorteando
/// entre duas comuns. E quatro efeitos de combate que o CombatManager executa
/// — Poison, DrawCards, GainEnergy, Debuff — não tinham carta nenhuma, assim como
/// GainGold e ExtraRations na estrada. As 23 cartas novas cobrem os seis efeitos
/// ociosos e levam cada classe a 4 comuns, 3 raras, 2 épicas e 1 lendária — a
/// raridade Lendária também estava vazia, embora a biblioteca cobrasse nível 4
/// para liberá-la.
///
/// <b>Os nomes são provisórios.</b> São descritivos de propósito, para dizer o que
/// a carta faz: batizar é decisão do autor.
///
/// Tools → Guild of Legends → Semear Cartas Faltantes
/// </summary>
public class CardCreator : EditorWindow
{
    private bool sobrescreverExistentes;

    [MenuItem("Tools/Card Creator")]
    public static void ShowWindow()
    {
        GetWindow<CardCreator>("Card Creator");
    }

    /// <summary>
    /// Ponto de entrada sem janela, para o menu e para o watcher de gatilhos.
    /// Nunca sobrescreve: é a variante segura de rodar às cegas.
    /// </summary>
    [MenuItem("Tools/Guild of Legends/Semear Cartas Faltantes")]
    public static void Semear()
    {
        CriarTodas(false);
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
            CriarTodas(sobrescreverExistentes);

        EditorGUILayout.Space();

        if (GUILayout.Button("Criar Cartas de Guerreiro")) CriarDaClasse(HeroClass.Warrior, sobrescreverExistentes);
        if (GUILayout.Button("Criar Cartas de Mago")) CriarDaClasse(HeroClass.Mage, sobrescreverExistentes);
        if (GUILayout.Button("Criar Cartas de Curandeiro")) CriarDaClasse(HeroClass.Healer, sobrescreverExistentes);
        if (GUILayout.Button("Criar Cartas de Caçador")) CriarDaClasse(HeroClass.Hunter, sobrescreverExistentes);
    }

    static void CriarTodas(bool sobrescrever)
    {
        int criadas = 0;
        foreach (var def in Definicoes)
            criadas += Criar(def, sobrescrever) ? 1 : 0;

        Finalizar(criadas, Definicoes.Length);
    }

    static void CriarDaClasse(HeroClass classe, bool sobrescrever)
    {
        int criadas = 0;
        int total = 0;
        foreach (var def in Definicoes)
        {
            if (def.classe != classe) continue;
            total++;
            criadas += Criar(def, sobrescrever) ? 1 : 0;
        }

        Finalizar(criadas, total);
    }

    static void Finalizar(int criadas, int total)
    {
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Sem isto, uma carta recém-criada só entraria em baralho depois de
        // reiniciar o Editor: o DeckGenerator guarda o acervo em memória.
        DeckGenerator.Recarregar();

        Debug.Log($"Card Creator: {criadas} de {total} cartas gravadas ({total - criadas} preservadas).");
    }

    /// <summary>Grava a carta. Devolve false quando a existente foi preservada.</summary>
    static bool Criar(Definicao def, bool sobrescrever)
    {
        string pasta = $"Assets/Resources/Cards/{def.classe}";
        string caminho = $"{pasta}/{def.nome}.asset";

        bool jaExiste = AssetDatabase.LoadAssetAtPath<CardData>(caminho) != null;
        if (jaExiste && !sobrescrever)
            return false;

        // CreateFolder em vez de Directory.CreateDirectory: uma pasta criada por
        // fora não existe para o AssetDatabase até o próximo refresh, e o
        // CreateAsset logo abaixo falharia em silêncio para uma classe nova.
        if (!AssetDatabase.IsValidFolder(pasta))
            AssetDatabase.CreateFolder("Assets/Resources/Cards", def.classe.ToString());

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
        // ══════════════════════════════════════════════════════════════════════
        // ⚔️ GUERREIRO — linha de frente: bate em área e segura o golpe.
        // ══════════════════════════════════════════════════════════════════════
        Def("Corte Duplo", HeroClass.Warrior, CardRarity.Common, 2,
            JourneyEffectType.RemoveObstacle, 0, "Corta galhos e abre caminho. Ignora 1 evento de floresta.",
            CombatEffectType.DamageAll, 8, 0, 0, 0, "8 de dano em 2 inimigos diferentes."),

        Def("Postura Defensiva", HeroClass.Warrior, CardRarity.Common, 2,
            JourneyEffectType.ProtectFromWeather, 0, "Protege o grupo contra dano por 2 dias.",
            CombatEffectType.Block, 0, 10, 0, 0, "Ganha 10 de bloqueio."),

        // O Guerreiro não tinha uma única carta de dano em alvo único: contra o
        // chefe, sozinho, ele batia em área num inimigo só. Custo 1 para ser a
        // carta que sempre cabe no fim do turno.
        Def("Estocada", HeroClass.Warrior, CardRarity.Common, 1,
            JourneyEffectType.RemoveObstacle, 0, "Força a passagem pelo ponto fraco: o grupo atravessa ileso.",
            CombatEffectType.Damage, 5, 0, 0, 0, "5 de dano em um alvo."),

        // Primeira carta de Debuff do jogo e primeira de GainGold: os dois
        // efeitos existiam implementados e sem nenhuma carta que os produzisse.
        Def("Golpe na Guarda", HeroClass.Warrior, CardRarity.Common, 2,
            JourneyEffectType.GainGold, 25, "Revista os caídos pelo caminho: +25 de ouro.",
            CombatEffectType.Debuff, 4, 0, 0, 2, "4 de dano; o inimigo bate menos por 2 turnos."),

        Def("Fúria", HeroClass.Warrior, CardRarity.Rare, 1,
            JourneyEffectType.RestoreMorale, 10, "Aumenta o moral do grupo em +10.",
            CombatEffectType.Buff, 3, 0, 2, 2, "Aumenta o dano em +3 por 2 turnos."),

        // Única carta com Intimidate: é o que permite recusar um combate na
        // estrada. O efeito existia no JourneyManager sem nenhuma carta que o
        // produzisse — código vivo que nunca rodava.
        Def("Brado de Guerra", HeroClass.Warrior, CardRarity.Rare, 2,
            JourneyEffectType.Intimidate, 0, "O grito ecoa pelo vale: o próximo bando recua sem lutar.",
            CombatEffectType.BlockAll, 0, 6, 0, 0, "6 de bloqueio para todos os aliados."),

        // GainEnergy e ExtraRations, os dois efeitos ociosos que sobravam ao
        // Guerreiro. Custa 1 e devolve 2: o lucro é de uma energia, o suficiente
        // para encaixar mais uma carta barata sem virar turno duplo.
        Def("Segundo Fôlego", HeroClass.Warrior, CardRarity.Rare, 1,
            JourneyEffectType.ExtraRations, 0, "Racionamento de veterano: +5 mantimentos.",
            CombatEffectType.GainEnergy, 0, 0, 0, 2, "+2 de energia neste turno."),

        Def("Investida", HeroClass.Warrior, CardRarity.Epic, 3,
            JourneyEffectType.Teleport, 2, "Atravessa terreno difícil. Pula 2 dias.",
            CombatEffectType.Damage, 15, 0, 0, 0, "15 de dano em um alvo."),

        // A outra épica do Guerreiro, e a decisão entre as duas: Investida mata
        // mais rápido, Duelo faz o grupo apanhar menos. O gerador sorteia uma
        // das duas, e o jogador troca na biblioteca.
        Def("Duelo", HeroClass.Warrior, CardRarity.Epic, 3,
            JourneyEffectType.Intimidate, 0, "Chama o líder para o duelo: o bando recua sem lutar.",
            CombatEffectType.Debuff, 10, 0, 0, 3, "10 de dano; o inimigo bate menos por 3 turnos."),

        Def("Sangue nos Olhos", HeroClass.Warrior, CardRarity.Legendary, 3,
            JourneyEffectType.RestoreMorale, 25, "O grupo se reergue: +25 de moral.",
            CombatEffectType.Buff, 6, 0, 0, 3, "O grupo ataca com +6 por 3 turnos."),

        // ══════════════════════════════════════════════════════════════════════
        // 🔮 MAGO — dano em área caro, e agora veneno e roda de cartas.
        // ══════════════════════════════════════════════════════════════════════
        Def("Bola de Fogo", HeroClass.Mage, CardRarity.Common, 3,
            JourneyEffectType.RemoveObstacle, 0, "Queima obstáculos. Remove 1 evento de armadilha.",
            CombatEffectType.DamageAll, 12, 0, 0, 0, "12 de dano em área."),

        Def("Escudo de Gelo", HeroClass.Mage, CardRarity.Common, 2,
            JourneyEffectType.ProtectFromWeather, 0, "Protege contra clima extremo por 2 dias.",
            CombatEffectType.BlockAll, 0, 8, 0, 0, "8 de bloqueio para todos aliados."),

        // Toda carta de Mago custava 2 ou mais: sobrava energia no fim do turno
        // e nada em que gastá-la.
        Def("Dardo Arcano", HeroClass.Mage, CardRarity.Common, 1,
            JourneyEffectType.RevealNextEvent, 1, "Um lampejo do que vem adiante.",
            CombatEffectType.Damage, 5, 0, 0, 0, "5 de dano em um alvo."),

        // Primeira carta de Poison. O veneno cobra no fim de cada turno e a pilha
        // se desgasta: 3 pilhas dão 6 de dano ao longo de três rodadas, e por isso
        // ele custa pouco — quem precisa matar agora não usa veneno.
        // A Purificação na estrada era só do Curandeiro, e 5 eventos a exigem.
        Def("Fagulha Persistente", HeroClass.Mage, CardRarity.Common, 1,
            JourneyEffectType.Purify, 0, "Queima a maldição pela raiz: remove maldições e doenças.",
            CombatEffectType.Poison, 3, 0, 0, 0, "3 de veneno: cobra a cada turno e se desgasta."),

        Def("Teleporte", HeroClass.Mage, CardRarity.Rare, 3,
            JourneyEffectType.SkipDay, 1, "Teletransporta o grupo. Pula 1 dia.",
            CombatEffectType.Evade, 0, 0, 0, 0, "Evita o próximo ataque."),

        // Primeira carta de DrawCards. Uma energia por duas cartas é a troca que
        // o baralho grande da jornada pedia: com 20 cartas, a mão trava.
        Def("Estudo Rápido", HeroClass.Mage, CardRarity.Rare, 1,
            JourneyEffectType.GainGold, 30, "Transmuta bugigangas em moeda: +30 de ouro.",
            CombatEffectType.DrawCards, 0, 0, 0, 2, "Compra 2 cartas."),

        // Debuff puro, sem dano: existe para o turno em que o inimigo anuncia o
        // golpe grande e não há bloqueio na mão.
        Def("Névoa Ilusória", HeroClass.Mage, CardRarity.Rare, 2,
            JourneyEffectType.ProtectFromWeather, 3, "Uma bolha de ar parado: 3 dias de abrigo.",
            CombatEffectType.Debuff, 0, 0, 0, 3, "O inimigo bate menos por 3 turnos."),

        Def("Explosão Arcana", HeroClass.Mage, CardRarity.Epic, 4,
            JourneyEffectType.RemoveObstacle, 0, "Destrói qualquer obstáculo.",
            CombatEffectType.DamageAll, 20, 0, 0, 0, "20 de dano em todos inimigos."),

        // A decisão épica do Mago: Explosão limpa a estrada, Estilhaços derruba o
        // chefe. O veneno rende tudo numa luta longa e quase nada numa curta.
        Def("Chuva de Estilhaços", HeroClass.Mage, CardRarity.Epic, 3,
            JourneyEffectType.SkipDay, 1, "Abre um vão nas pedras e encurta o trecho. Pula 1 dia.",
            CombatEffectType.Poison, 6, 0, 0, 0, "6 de veneno: cobra a cada turno e se desgasta."),

        Def("Poço de Mana", HeroClass.Mage, CardRarity.Legendary, 1,
            JourneyEffectType.Teleport, 2, "Rasga a distância. Pula 2 dias.",
            CombatEffectType.GainEnergy, 0, 0, 0, 4, "+4 de energia neste turno."),

        // ══════════════════════════════════════════════════════════════════════
        // ⚕️ CURANDEIRO — o baralho dele não tinha uma carta que ferisse nem uma
        // que bloqueasse: um Curandeiro principal ia à guerra desarmado.
        // ══════════════════════════════════════════════════════════════════════
        Def("Toque Curativo", HeroClass.Healer, CardRarity.Common, 1,
            JourneyEffectType.HealInjury, 0, "Cura ferimentos de 1 herói.",
            CombatEffectType.Heal, 0, 0, 8, 0, "Cura 8 HP de um aliado."),

        Def("Bênção", HeroClass.Healer, CardRarity.Common, 2,
            JourneyEffectType.RestoreMorale, 10, "Aumenta o moral do grupo em +10.",
            CombatEffectType.HealAll, 0, 0, 5, 0, "Cura 5 HP para todos aliados."),

        Def("Golpe de Cajado", HeroClass.Healer, CardRarity.Common, 1,
            JourneyEffectType.RemoveObstacle, 0, "O cajado firma a passagem e abre a moita.",
            CombatEffectType.Damage, 5, 0, 0, 0, "5 de dano em um alvo."),

        Def("Escudo de Fé", HeroClass.Healer, CardRarity.Common, 2,
            JourneyEffectType.ExtraRations, 0, "Abençoa o que sobrou na mochila: +5 mantimentos.",
            CombatEffectType.Block, 0, 9, 0, 0, "9 de bloqueio em um aliado."),

        Def("Purificação", HeroClass.Healer, CardRarity.Rare, 2,
            JourneyEffectType.Purify, 0, "Remove maldições e doenças.",
            CombatEffectType.Cleanse, 0, 0, 0, 0, "Remove a aflição e alivia o estresse."),

        Def("Oração de Silêncio", HeroClass.Healer, CardRarity.Rare, 2,
            JourneyEffectType.RestoreMorale, 15, "Aumenta o moral do grupo em +15.",
            CombatEffectType.Debuff, 3, 0, 0, 3, "3 de dano; o inimigo bate menos por 3 turnos."),

        // O Curandeiro que faz o grupo bater mais forte sem bater em ninguém:
        // é a carta que dá a ele um papel no turno de ataque.
        Def("Unção de Guerra", HeroClass.Healer, CardRarity.Rare, 2,
            JourneyEffectType.GainGold, 20, "Vende remédios na estrada: +20 de ouro.",
            CombatEffectType.Buff, 4, 0, 0, 2, "O grupo ataca com +4 por 2 turnos."),

        Def("Ressurgir", HeroClass.Healer, CardRarity.Epic, 4,
            JourneyEffectType.Revive, 0, "Traz um herói de volta da beira da morte e devolve metade da vida.",
            CombatEffectType.Heal, 0, 0, 5, 0, "Revive um aliado com 5 HP."),

        Def("Círculo Sagrado", HeroClass.Healer, CardRarity.Epic, 3,
            JourneyEffectType.HealInjury, 0, "Trata os ferimentos de 1 herói.",
            CombatEffectType.HealAll, 0, 0, 9, 0, "Cura 9 HP para todos aliados."),

        Def("Mão da Providência", HeroClass.Healer, CardRarity.Legendary, 3,
            JourneyEffectType.Revive, 0, "Traz um herói de volta da beira da morte e devolve metade da vida.",
            CombatEffectType.HealAll, 0, 0, 12, 0, "Cura 12 HP para todos aliados."),

        // ══════════════════════════════════════════════════════════════════════
        // 🏹 CAÇADOR — dano em alvo único, veneno e informação.
        // ══════════════════════════════════════════════════════════════════════
        Def("Flecha Precisa", HeroClass.Hunter, CardRarity.Common, 1,
            JourneyEffectType.RemoveObstacle, 0, "Acerta o ponto exato: abre passagem por rios e cavernas.",
            CombatEffectType.Damage, 6, 0, 0, 0, "6 de dano, ignora bloqueio."),

        Def("Armadilha", HeroClass.Hunter, CardRarity.Common, 2,
            JourneyEffectType.GainFood, 2, "Captura comida. Ganha +2 rações.",
            CombatEffectType.Damage, 10, 0, 0, 0, "10 de dano quando inimigo ataca."),

        Def("Lâmina Untada", HeroClass.Hunter, CardRarity.Common, 2,
            JourneyEffectType.ExtraRations, 0, "Salga e guarda a caça do dia: +5 mantimentos.",
            CombatEffectType.Poison, 4, 0, 0, 0, "4 de veneno: cobra a cada turno e se desgasta."),

        // O Caçador não tinha uma única carta de defesa em nenhuma raridade, e o
        // baralho dele saía do gerador sem nada para segurar o golpe anunciado.
        // Bloqueia menos que o Guerreiro e menos que o Curandeiro: é o preço de
        // ser a classe que atira.
        Def("Cobertura", HeroClass.Hunter, CardRarity.Common, 2,
            JourneyEffectType.GainGold, 20, "Vende peles no caminho: +20 de ouro.",
            CombatEffectType.Block, 0, 8, 0, 0, "8 de bloqueio em um aliado."),

        Def("Olhar de Águia", HeroClass.Hunter, CardRarity.Rare, 1,
            JourneyEffectType.RevealNextEvent, 0, "Revela o próximo evento.",
            CombatEffectType.BuffNextCard, 0, 0, 0, 0, "Próxima carta dá +50% dano."),

        // Intimidate era exclusivo do Guerreiro, e 8 dos 25 eventos exigem esse
        // efeito para abrir a opção boa: sem um Guerreiro no grupo, esses oito
        // caminhos ficavam fechados a jornada inteira.
        Def("Tiro de Aviso", HeroClass.Hunter, CardRarity.Rare, 2,
            JourneyEffectType.Intimidate, 0, "Uma flecha crava aos pés do líder: o bando recua sem lutar.",
            CombatEffectType.Debuff, 5, 0, 0, 2, "5 de dano; o inimigo bate menos por 2 turnos."),

        // Com 20 cartas no baralho da jornada, a mão trava: a de compra existe
        // para o baralho grande girar. Fica em Rara, e não em Comum, porque
        // quatro cópias de "compre duas" viram um turno inteiro de graça.
        Def("Recuar e Mirar", HeroClass.Hunter, CardRarity.Rare, 1,
            JourneyEffectType.SkipDay, 1, "Escolhe a hora de andar. Pula 1 dia.",
            CombatEffectType.DrawCards, 0, 0, 0, 2, "Compra 2 cartas."),

        Def("Flecha Lunar", HeroClass.Hunter, CardRarity.Epic, 3,
            JourneyEffectType.RevealNextEvent, 0, "Revela todo o mapa da região.",
            CombatEffectType.ShieldBreak, 20, 0, 0, 0, "20 de dano, ignora bloqueio."),

        // A decisão épica do Caçador: a Lunar arromba a guarda agora, a
        // Envenenada cobra devagar e não é bloqueável de jeito nenhum.
        Def("Flecha Envenenada", HeroClass.Hunter, CardRarity.Epic, 3,
            JourneyEffectType.Teleport, 2, "Conhece o atalho da mata. Pula 2 dias.",
            CombatEffectType.Poison, 7, 0, 0, 0, "7 de veneno: cobra a cada turno e se desgasta."),

        Def("Chuva de Flechas", HeroClass.Hunter, CardRarity.Legendary, 4,
            JourneyEffectType.Intimidate, 0, "A saraivada no céu: o próximo bando recua sem lutar.",
            CombatEffectType.DamageAll, 20, 0, 0, 0, "20 de dano em todos inimigos.")
    };
}
#endif
