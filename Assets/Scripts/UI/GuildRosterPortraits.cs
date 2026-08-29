using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Põe o rosto dos heróis nos cards do rodapé da guilda.
///
/// <b>Por que isto existe.</b> Os cards do rodapé são objetos da cena herdada —
/// não há código que os monte —, e o <c>img_Portrait</c> de cada um nasce sem
/// sprite, com um cinza chapado por cor. O resultado é a tela inicial do jogo
/// mostrando o elenco como três quadros vazios com um nome embaixo, enquanto a
/// Taverna, a Forja e a Biblioteca mostram o mesmo <see cref="HeroData.portrait"/>
/// sem problema. É a mesma falha que a tela de preparação já teve e que o
/// <c>QuestSelectionUI.VestirRetrato</c> corrigiu do lado dela.
///
/// <b>Casa pelo nome, e não pela ordem.</b> Os cards do rodapé não são recriados
/// quando alguém morre ou é contratado — a ordem deles não acompanha a do roster,
/// e casar por índice poria o rosto do morto no card de quem entrou no lugar.
/// </summary>
public class GuildRosterPortraits : MonoBehaviour
{
    const string CaminhoDoRodape = "Background/Panel_DownBar/DownInfo/Heroes Panel";

    /// <summary>
    /// Sobe junto com a cena, como o <c>EventArtBackdrop</c>: sem isto seria
    /// preciso pendurar o componente à mão na cena, e a cena é montada por
    /// ferramenta.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Ligar()
    {
        var host = new GameObject(nameof(GuildRosterPortraits));
        DontDestroyOnLoad(host);
        host.AddComponent<GuildRosterPortraits>();
    }

    Transform rodape;
    float proximaConferida;

    void Update()
    {
        // Uma vez a cada meio segundo: o rodapé muda quando alguém é contratado
        // ou morre, e não há evento para isso. Varrer três cards é barato; varrer
        // a cada quadro seria desperdício sem ganho nenhum.
        if (Time.unscaledTime < proximaConferida) return;
        proximaConferida = Time.unscaledTime + 0.5f;

        if (GuildManager.Instance == null) return;

        if (rodape == null)
        {
            var canvas = UIUtil.CanvasPrincipal();
            if (canvas == null) return;

            rodape = canvas.transform.Find(CaminhoDoRodape);
            if (rodape == null) return;
        }

        if (!rodape.gameObject.activeInHierarchy) return;

        foreach (Transform card in rodape)
            Vestir(card);
    }

    void Vestir(Transform card)
    {
        Image quadro = null;

        foreach (Image img in card.GetComponentsInChildren<Image>(true))
        {
            if (img.gameObject.name != "img_Portrait") continue;
            quadro = img;
            break;
        }

        if (quadro == null || quadro.sprite != null) return;

        // O nome está escrito no próprio card — é o que amarra o quadro ao herói.
        string nome = null;
        foreach (TMP_Text texto in card.GetComponentsInChildren<TMP_Text>(true))
        {
            if (string.IsNullOrWhiteSpace(texto.text)) continue;

            // O primeiro texto não vazio que casa com alguém do roster é o nome;
            // os outros são vida e estado.
            string candidato = texto.text.Trim();
            if (GuildManager.Instance.roster.Any(h => h != null && h.heroName == candidato))
            {
                nome = candidato;
                break;
            }
        }

        if (nome == null) return;

        HeroData heroi = GuildManager.Instance.roster.FirstOrDefault(h => h != null && h.heroName == nome);
        if (heroi == null || heroi.portrait == null) return;

        quadro.sprite = heroi.portrait;
        quadro.color = Color.white;
        quadro.preserveAspect = true;
    }
}
