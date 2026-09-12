using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Acende as portas da guilda pelo que cada uma tem a oferecer agora.
///
/// <b>Desde 12/09 a luz tem três níveis.</b> Até então a guilda era sete portas
/// do mesmo peso com uma moldura pulsando numa delas — e a moldura, sozinha,
/// não bastou: o autor voltou dizendo que a sensação de estar perdido
/// continuava a mesma. O que mudou é que a porta passou a dizer se há motivo
/// para entrar:
///
/// - <b>pulsando</b>: a porta mais urgente, escolhida por <see cref="Decidir"/>;
/// - <b>acesa</b>: há o que fazer lá dentro — ouro para a Forja, alguém ferido
///   para o Mercado, um morto para o Cemitério;
/// - <b>escura</b>: nada ainda. Continua clicável, e lá dentro a sala mostra
///   que não há o que fazer — tranca seria mentir sobre o que existe.
///
/// Não é tutorial nem caixa de texto, que o autor descartou. É a guilda dizendo
/// o que tem sem falar, e é o que faz a guilda recém-fundada abrir com duas
/// portas acesas em vez de sete.
///
/// A regra da moldura é uma só: <b>a primeira condição que casa manda</b>, da
/// mais bloqueante para a mais rotineira. O texto de cada caso continua escrito
/// no <see cref="Decidir"/>: explica a regra para quem lê o código, e é lido
/// pelo relatório de Play Mode, que audita se o guia aponta para a sala certa.
///
/// A frase não vai para a tela desde 12/09; o <c>Txt_Guia</c> fica na cena,
/// vazio e desligado, porque é a referência que este componente guarda e apagar
/// objeto de cena por ferramenta é irreversível.
/// </summary>
public class GuildGuide : MonoBehaviour
{
    /// <summary>Uma porta do mapa da guilda e a moldura que a acende.</summary>
    [System.Serializable]
    public class Sala
    {
        public string nome;
        public GameObject realce;
    }

    /// <summary>A luz de uma porta: o que a guilda diz sem falar.</summary>
    public enum Luz { Escura, Acesa, Pulsando }

    public TMP_Text linha;
    public List<Sala> salas = new List<Sala>();

    /// <summary>
    /// Abaixo disto a jornada sai com o banco vazio: sem reserva, uma baixa no
    /// meio do caminho já deixa o grupo abaixo da formação que o combate espera.
    /// </summary>
    const int GrupoConfortavel = PartyFormation.MaxSlots;

    /// <summary>
    /// A partir daqui o guia avisa, embora o herói ainda possa partir.
    ///
    /// O limite de viagem é 85; avisar só ao chegar lá seria avisar tarde, porque
    /// aí ele já está fora do grupo. 65 dá uma volta de guilda de antecedência.
    /// </summary>
    const float AvisoDeEstresse = 65f;

    /// <summary>
    /// O custo da primeira arma na Forja (<c>weaponBaseCost × 1</c>). Escrito aqui
    /// porque o guia não deve acordar o ForgeManager só para perguntar um preço —
    /// a Forja é uma sala, e o guia é lido a cada mudança de ouro.
    /// </summary>
    const int CustoDeUmaPrimeiraArma = 120;

    /// <summary>
    /// Ouro que já daria para várias compras e continua no cofre. A auditoria
    /// mediu ~395 de entrada por jornada; acima de três jornadas guardadas, o
    /// jogador não está economizando, está sem saber onde gastar.
    /// </summary>
    const int OuroParado = 1200;

    /// <summary>
    /// Brilho do cenário e do rótulo de cada nível de luz. A porta que pulsa
    /// fica inteira; a acesa recua um pouco, para a moldura ter onde aparecer;
    /// a escura fica em trinta por cento — lê-se o nome, e lê-se que está
    /// apagada.
    /// </summary>
    const float BrilhoAceso = 0.82f;
    const float BrilhoEscuro = 0.30f;

    // Os preços que decidem se uma sala tem motivo. Espelham os managers das
    // salas pelo mesmo motivo do custo da arma: o guia é recalculado a cada
    // mudança de ouro e não deve acordar sala nenhuma para perguntar um preço.
    const int PrecoDaBandagem = 90;
    const int PrecoDoVinho = 55;
    const int PrecoDeUmFrasco = 70;
    const int PrecoDeUmaCartaComum = 100;
    const int CustoMinimoDaForja = 100;
    const int NivelMaximoDaForja = 3;
    const float EstresseQueOVinhoAlivia = 30f;

    bool inscrito;

    /// <summary>
    /// A cor de fábrica de cada gráfico das portas. O brilho multiplica a cor
    /// original em vez de escrever por cima: a cena de cada porta já nasce com
    /// um véu escuro (GuildArt), e escurecer um véu já escurecido apagaria a
    /// porta de vez.
    /// </summary>
    readonly Dictionary<Graphic, Color> corOriginal = new Dictionary<Graphic, Color>();

    void OnEnable()
    {
        Inscrever();
        Atualizar();
    }

    void Start()
    {
        // Os managers nascem com a cena e podem não existir ainda no OnEnable —
        // a guilda é a primeira tela e sobe junto com eles.
        Inscrever();

        // O relógio da partida, no rodapé. É criado daqui porque este componente
        // já mora na guilda e acorda junto com ela.
        RelogioDaGuilda.Garantir(transform.parent);

        Atualizar();
    }

    void OnDisable()
    {
        Desinscrever();
    }

    void Inscrever()
    {
        if (inscrito) return;
        if (GuildManager.Instance == null || QuestManager.Instance == null) return;

        GuildManager.Instance.onRosterChanged += Atualizar;
        GuildManager.Instance.onGoldChanged += Atualizar;
        QuestManager.Instance.onQuestsChanged += Atualizar;
        inscrito = true;
    }

    void Desinscrever()
    {
        if (!inscrito) return;

        if (GuildManager.Instance != null)
        {
            GuildManager.Instance.onRosterChanged -= Atualizar;
            GuildManager.Instance.onGoldChanged -= Atualizar;
        }
        if (QuestManager.Instance != null)
            QuestManager.Instance.onQuestsChanged -= Atualizar;

        inscrito = false;
    }

    /// <summary>Recalcula o conselho e a luz de cada porta.</summary>
    public void Atualizar()
    {
        string sala, texto;
        Decidir(out sala, out texto);

        if (linha != null && linha.gameObject.activeSelf) linha.text = "";

        foreach (var s in salas)
        {
            if (s == null || s.realce == null) continue;

            Luz luz = s.nome == sala ? Luz.Pulsando
                    : TemMotivo(s.nome) ? Luz.Acesa
                    : Luz.Escura;

            s.realce.SetActive(luz == Luz.Pulsando);
            Iluminar(s.realce.transform.parent, luz);
        }
    }

    /// <summary>A luz de uma porta, pelo nome do objeto — para o relatório de Play Mode.</summary>
    public Luz NivelDe(string nome)
    {
        Decidir(out string sala, out _);
        if (nome == sala) return Luz.Pulsando;
        return TemMotivo(nome) ? Luz.Acesa : Luz.Escura;
    }

    /// <summary>Todas as portas e a luz de cada uma, na ordem da cena.</summary>
    public IEnumerable<(string nome, Luz luz)> LuzDasPortas()
    {
        foreach (var s in salas)
            if (s != null && !string.IsNullOrEmpty(s.nome))
                yield return (s.nome, NivelDe(s.nome));
    }

    /// <summary>
    /// Pinta a porta no nível de luz dado.
    ///
    /// Mexe no cenário e no rótulo, e <b>não</b> no fundo da porta: o fundo é o
    /// alvo do Button, e o Button repinta o alvo dele a cada entrada e saída do
    /// ponteiro — era por isso que o recuo de 72% da versão anterior mal
    /// aparecia. A moldura fica de fora porque tem a cor dela.
    /// </summary>
    void Iluminar(Transform sala, Luz luz)
    {
        if (sala == null) return;

        float brilho = luz == Luz.Escura ? BrilhoEscuro
                     : luz == Luz.Acesa ? BrilhoAceso
                     : 1f;

        foreach (Graphic g in sala.GetComponentsInChildren<Graphic>(true))
        {
            if (g == null || g.transform == sala) continue;
            if (g.GetComponentInParent<RealcePulsante>() != null) continue;

            if (!corOriginal.TryGetValue(g, out Color cor))
            {
                cor = g.color;
                corOriginal[g] = cor;
            }

            g.color = new Color(cor.r * brilho, cor.g * brilho, cor.b * brilho, cor.a);
        }
    }

    /// <summary>
    /// A sala tem o que oferecer agora?
    ///
    /// É a pergunta que acende a porta. Cada resposta é o motivo pelo qual um
    /// jogador entraria: não "a sala funciona", e sim "há algo para você lá
    /// dentro". Uma Forja com ouro para a primeira arma, um Mercado com alguém
    /// ferido para tratar, um Cemitério com alguém para enterrar.
    /// </summary>
    public bool TemMotivo(string nome)
    {
        GuildManager guilda = GuildManager.Instance;
        if (guilda == null) return true;

        string chave = Chave(nome);

        List<HeroData> vivos = guilda.roster.Where(h => h != null && h.IsAlive).ToList();
        List<HeroData> aptos = vivos.Where(h => h.IsFitForJourney).ToList();
        int ouro = guilda.gold;
        int ciclo = RunManager.Existe ? RunManager.Instance.Cycle : 0;

        if (chave.Contains("jornada") || chave.Contains("journey") || chave.Contains("quest"))
            return aptos.Count > 0;

        if (chave.Contains("tavern"))
            return guilda.EmFundacao
                || (guilda.CanRecruit() && aptos.Count < GrupoConfortavel
                    && ouro >= HeroFactory.SalaryFor(1));

        if (chave.Contains("forj") || chave.Contains("forge"))
            return ouro >= CustoMinimoDaForja
                && aptos.Any(h => h.weaponLevel < NivelMaximoDaForja || h.armorLevel < NivelMaximoDaForja);

        if (chave.Contains("mercado") || chave.Contains("market"))
            return (vivos.Any(h => h.isInjured) && ouro >= PrecoDaBandagem)
                || (vivos.Any(h => h.stress >= EstresseQueOVinhoAlivia) && ouro >= PrecoDoVinho)
                || (ciclo >= 1 && vivos.Count > 0 && ouro >= PrecoDeUmFrasco);

        if (chave.Contains("bibliotec") || chave.Contains("librar"))
            return Escritos.TotalNaEstante > 0
                || (ouro >= PrecoDeUmaCartaComum && vivos.Any(BaralhoComVaga));

        if (chave.Contains("cemit") || chave.Contains("cemet"))
            return guilda.fallenHeroes.Count > 0;

        if (chave.Contains("mapa") || chave.Contains("map"))
            return RegionMap.Selos > 0
                || SeloNoQuadro(out _)
                || AreaCatalog.Todas.Any(a => RegionMap.Mapeamento(AreaCatalog.Aspecto(a)) > 0f);

        return true;
    }

    /// <summary>O baralho do herói ainda aceita carta — é o que faz a Biblioteca ter motivo.</summary>
    static bool BaralhoComVaga(HeroData heroi)
    {
        DeckData baralho = DeckRepository.GetDeck(heroi);
        int cartas = baralho != null && baralho.cards != null ? baralho.cards.Count : 0;
        return cartas < DeckGenerator.LimiteDoBaralho(heroi.level);
    }

    /// <summary>
    /// O nome do objeto sem acento e sem caixa. As portas da cena vêm de
    /// versões diferentes do projeto — "Sala de Mapas", "SalaMapas", "MapRoom" —
    /// e a mesma tolerância que o GuildArt usa para vesti-las serve aqui para
    /// reconhecê-las.
    /// </summary>
    static string Chave(string nome)
    {
        if (string.IsNullOrEmpty(nome)) return "";

        string decomposto = nome.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposto.Length);
        foreach (char c in decomposto)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);

        return sb.ToString().ToLowerInvariant();
    }

    /// <summary>
    /// O conselho da vez. Separado do resto para poder ser lido de fora — o teste
    /// de Play Mode confere o texto sem precisar da tela montada.
    /// </summary>
    public void Decidir(out string sala, out string texto)
    {
        GuildManager guilda = GuildManager.Instance;

        if (guilda == null)
        {
            sala = "Jornada";
            texto = "";
            return;
        }

        // 0. A guilda ainda não tem gente: é a fundação, e ela acontece na
        //    Taverna. Vem antes de tudo porque nada mais existe antes disto.
        if (guilda.EmFundacao)
        {
            sala = "Taverna";
            texto = $"A guilda ainda não tem os {GuildManager.FundadoresDaGuilda} fundadores. Escolha-os na Taverna.";
            return;
        }

        List<HeroData> vivos = guilda.roster.Where(h => h != null && h.IsAlive).ToList();
        List<HeroData> aptos = vivos.Where(h => h.IsFitForJourney).ToList();
        int ciclo = RunManager.Existe ? RunManager.Instance.Cycle : 0;

        // 1. Sem ninguém para viajar, nada mais importa.
        if (aptos.Count == 0)
        {
            if (vivos.Count == 0)
            {
                sala = "Taverna";
                texto = "A guilda está sem heróis. Contrate na Taverna antes de partir.";
                return;
            }

            HeroData pior = vivos.OrderByDescending(h => h.stress).First();
            sala = guilda.gold >= 1 ? "Mercado" : "Taverna";
            texto = $"{pior.heroName} está {pior.UnfitReason} e ninguém pode viajar. "
                  + "O vinho do Mercado e a vigília do Cemitério aliviam o estresse.";
            return;
        }

        // 2. O alvo da run apareceu: é a única missão que encerra a partida.
        if (ChefeNoQuadro())
        {
            sala = "Jornada";
            texto = "O Chefe Supremo entrou no quadro de missões. É o fim da run — vá quando estiver pronto.";
            return;
        }

        // 2b. Uma região está inteira no mapa e o que a guarda está no quadro.
        //     Vem logo abaixo da jornada final porque é o passo que leva até
        //     ela: sem selo, aquela missão nunca aparece.
        if (SeloNoQuadro(out string regiaoPronta))
        {
            sala = "Jornada";
            texto = $"{regiaoPronta} está inteira no mapa, e o que a guarda entrou no quadro. "
                  + $"Derrubá-lo sela a região — {RegionMap.Selos} de {RegionMap.SelosParaOFim} selos até o fim.";
            return;
        }

        // 3. Grupo curto. Não bloqueia a jornada, mas o combate espera quatro —
        //    e só vale apontar depois que a guilda saiu uma vez: no ciclo 0,
        //    com os dois fundadores e o ouro de fábrica, o conselho é a
        //    estrada, não a compra.
        if (aptos.Count < GrupoConfortavel && ciclo >= 1
            && guilda.CanRecruit() && guilda.gold >= HeroFactory.SalaryFor(1))
        {
            int faltam = GrupoConfortavel - aptos.Count;
            sala = "Taverna";
            texto = $"Só {aptos.Count} {(aptos.Count == 1 ? "herói apto" : "heróis aptos")} para a estrada — "
                  + $"um grupo completo leva {GrupoConfortavel}. Há candidatos na Taverna ({faltam} a contratar).";
            return;
        }

        // 4. Alguém à beira de quebrar. Ainda pode viajar — e é justamente por
        //    isso que precisa ser dito: o jogador só descobre o limite quando o
        //    herói já voltou afligido.
        HeroData naBeira = aptos
            .Where(h => h.stress >= AvisoDeEstresse)
            .OrderByDescending(h => h.stress)
            .FirstOrDefault();

        if (naBeira != null)
        {
            sala = guilda.gold >= 1 ? "Mercado" : "Cemitério";
            texto = $"{naBeira.heroName} viaja com {Mathf.RoundToInt(naBeira.stress)}/100 de estresse — "
                  + $"a {Mathf.RoundToInt(HeroData.StressLimiteParaViajar - naBeira.stress)} de não poder mais partir. "
                  + "O vinho do Mercado e a vigília do Cemitério aliviam.";
            return;
        }

        // 5. Arma nunca forjada. É a compra de maior efeito por ouro, e a que o
        //    jogador esquece que existe — a Forja não avisa nada da guilda.
        HeroData semArma = aptos.FirstOrDefault(h => h.weaponLevel <= 0);

        if (semArma != null && guilda.gold >= CustoDeUmaPrimeiraArma)
        {
            sala = "Forja";
            texto = $"A arma de {semArma.heroName} nunca foi forjada. "
                  + $"Há {guilda.gold} de ouro parado, e a Forja é o que se sente no primeiro combate.";
            return;
        }

        // 6. Ouro parado. Último aviso antes da rotina: se nada acima casou, a
        //    guilda está saudável e o que sobra é ouro sem uso.
        if (guilda.gold >= OuroParado)
        {
            sala = "Biblioteca";
            texto = $"{guilda.gold} de ouro no cofre e nada comprado. "
                  + "A Biblioteca vende cartas para um baralho, e a Forja melhora arma e armadura.";
            return;
        }

        // 7. O caminho de sempre: o jogo anda pela Jornada.
        sala = "Jornada";
        texto = $"{aptos.Count} heróis prontos. Escolha um destino no mapa e parta.";
    }

    static bool ChefeNoQuadro()
    {
        if (QuestManager.Instance == null) return false;

        List<QuestData> quadro = QuestManager.Instance.QuadroAtual;
        return quadro != null && quadro.Any(q => q != null && q.isFinalBoss);
    }

    /// <summary>
    /// Há luta de selo esperando, e de qual região. Devolve a primeira: duas
    /// regiões prontas ao mesmo tempo são caso raro, e o guia dá um passo por
    /// vez — apontar duas seria não apontar nenhuma.
    /// </summary>
    static bool SeloNoQuadro(out string regiao)
    {
        regiao = null;
        if (QuestManager.Instance == null) return false;

        List<QuestData> quadro = QuestManager.Instance.QuadroAtual;
        if (quadro == null) return false;

        QuestData selo = quadro.FirstOrDefault(q => q != null && q.isRegionBoss);
        if (selo == null) return false;

        regiao = AreaCatalog.Nome(AreaCatalog.Da(selo.biomeType));
        return true;
    }
}
