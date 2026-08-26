using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Diz ao jogador o que fazer agora, e acende a sala que resolve.
///
/// A guilda é sete portas escuras lado a lado, todas com o mesmo peso visual:
/// nada distingue a sala que faz o jogo andar da sala que o jogador só visita
/// quando alguém morre. Quem abre o jogo pela primeira vez não tem como saber
/// que "Jornada" é o loop e o resto é apoio — e quem volta de uma jornada com o
/// grupo esgotado não tem como saber que o remédio está no Mercado.
///
/// A regra é uma só: **a primeira condição que casa manda**, da mais bloqueante
/// para a mais rotineira. Um guia que listasse tudo o que é possível fazer não
/// seria guia nenhum — seria o mesmo menu de sete portas, agora por escrito.
///
/// O texto descreve o estado real (nomes, números), nunca uma instrução genérica:
/// "Gromm está esgotado" ensina a regra do estresse de passagem, "Visite o
/// Mercado" não ensina nada.
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

    public TMP_Text linha;
    public List<Sala> salas = new List<Sala>();

    /// <summary>
    /// Abaixo disto a jornada sai com o banco vazio: sem reserva, uma baixa no
    /// meio do caminho já deixa o grupo abaixo da formação que o combate espera.
    /// </summary>
    const int GrupoConfortavel = PartyFormation.MaxSlots;

    bool inscrito;

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

    /// <summary>Recalcula o conselho e acende a porta correspondente.</summary>
    public void Atualizar()
    {
        string sala, texto;
        Decidir(out sala, out texto);

        if (linha != null) linha.text = texto;

        foreach (var s in salas)
            if (s != null && s.realce != null)
                s.realce.SetActive(s.nome == sala);
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

        List<HeroData> vivos = guilda.roster.Where(h => h != null && h.IsAlive).ToList();
        List<HeroData> aptos = vivos.Where(h => h.IsFitForJourney).ToList();

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

        // 3. Grupo curto. Não bloqueia a jornada, mas o combate espera quatro.
        if (aptos.Count < GrupoConfortavel)
        {
            int faltam = GrupoConfortavel - aptos.Count;
            sala = "Taverna";
            texto = $"Só {aptos.Count} {(aptos.Count == 1 ? "herói apto" : "heróis aptos")} para a estrada — "
                  + $"um grupo completo leva {GrupoConfortavel}. Há candidatos na Taverna ({faltam} a contratar).";
            return;
        }

        // 4. O caminho de sempre: o jogo anda pela Jornada.
        sala = "Jornada";
        texto = $"{aptos.Count} heróis prontos. Escolha uma missão em Jornada e parta.";
    }

    static bool ChefeNoQuadro()
    {
        if (QuestManager.Instance == null) return false;

        // GetQuests() gera um quadro quando não há nenhum — perguntar não pode
        // criar o mundo, e o guia é chamado a cada mudança de ouro.
        List<QuestData> quadro = QuestManager.Instance.QuadroAtual;
        return quadro != null && quadro.Any(q => q != null && q.isFinalBoss);
    }
}
