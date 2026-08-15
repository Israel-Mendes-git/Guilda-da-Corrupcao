using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A ordem do round, no alto da tela de combate.
///
/// Mostra a sequência em que as coisas vão acontecer — primeiro o grupo, depois
/// cada inimigo vivo, na ordem em que agirão — e destaca quem está agindo agora.
///
/// <b>Por que informativa e não iniciativa por personagem:</b> a regra do combate
/// é a do Slay the Spire — o grupo age junto, gastando energia, e só então os
/// inimigos respondem. Trocar isso por vez-por-personagem mudaria energia, posse
/// de carta, simulador e todo o balanceamento, que foi medido em mortes por
/// combate. A barra existe para o jogador **ler o ritmo** que já existe: quantos
/// golpes ainda vêm antes de ele jogar de novo, e de quem.
///
/// A sequência é remontada a cada round porque inimigo morto sai da fila — e ver
/// a fila encurtar quando um cai é metade do que a barra comunica.
/// </summary>
public class TurnOrderBar : MonoBehaviour
{
    [Header("Montagem")]
    public Transform container;

    /// <summary>Molde de uma ficha, desativado. Clonado por participante.</summary>
    public GameObject chipTemplate;

    [Header("Cores")]
    public Color corDoGrupo = new Color(0.36f, 0.52f, 0.72f);
    public Color corDoInimigo = new Color(0.52f, 0.24f, 0.22f);

    /// <summary>Quem já agiu neste round some para o fundo.</summary>
    public Color corGasta = new Color(0.22f, 0.21f, 0.23f);

    readonly List<GameObject> fichas = new List<GameObject>();

    /// <summary>Índice 0 é sempre o grupo; de 1 em diante, os inimigos vivos.</summary>
    public int Atual { get; private set; }

    /// <summary>Quantos participantes a fila tem. O teste confere isto.</summary>
    public int Tamanho => fichas.Count;

    /// <summary>
    /// Remonta a fila. Chamado no começo de cada round e sempre que um inimigo
    /// cai — a fila que não encurta mente sobre o que ainda vem.
    /// </summary>
    public void Montar(IList<EnemyInstance> inimigos)
    {
        if (container == null || chipTemplate == null) return;

        Limpar();

        CriarFicha("GRUPO", corDoGrupo, null, Color.white);

        if (inimigos == null) return;

        // Três "Carniçal" seguidos não dizem qual é qual, e a fila existe para
        // dizer exatamente isso. Só numera quando há repetição — "Carniçal I"
        // sozinho seria ruído.
        var vivos = new List<EnemyInstance>();
        foreach (var inimigo in inimigos)
            if (inimigo != null && inimigo.IsAlive) vivos.Add(inimigo);

        var quantos = new Dictionary<string, int>();
        foreach (var inimigo in vivos)
        {
            string nome = inimigo.data.enemyName;
            quantos[nome] = quantos.TryGetValue(nome, out int n) ? n + 1 : 1;
        }

        var vistos = new Dictionary<string, int>();
        foreach (var inimigo in vivos)
        {
            string nome = inimigo.data.enemyName;
            string rotulo = nome;

            if (quantos[nome] > 1)
            {
                int indice = vistos.TryGetValue(nome, out int v) ? v + 1 : 1;
                vistos[nome] = indice;
                rotulo = $"{nome} {Romano(indice)}";
            }

            CriarFicha(rotulo, corDoInimigo, inimigo.data.portrait, inimigo.data.portraitTint);
        }

        Destacar(0);
    }

    /// <summary>Quem está agindo agora. 0 = o grupo.</summary>
    public void Destacar(int indice)
    {
        Atual = indice;

        for (int i = 0; i < fichas.Count; i++)
        {
            GameObject ficha = fichas[i];
            if (ficha == null) continue;

            bool agora = i == indice;
            bool jaAgiu = i < indice;

            var fundo = ficha.GetComponent<Image>();
            if (fundo != null)
            {
                Color baseCor = i == 0 ? corDoGrupo : corDoInimigo;
                fundo.color = jaAgiu ? corGasta : baseCor;
            }

            // Quem age agora cresce um pouco e fica opaco; quem já agiu apaga.
            // É a diferença que se lê de canto de olho, sem parar para conferir.
            ficha.transform.localScale = agora ? Vector3.one * 1.12f : Vector3.one;

            var grupo = ficha.GetComponent<CanvasGroup>();
            if (grupo != null) grupo.alpha = jaAgiu ? 0.45f : 1f;

            var seta = ficha.transform.Find("Now");
            if (seta != null) seta.gameObject.SetActive(agora);
        }
    }

    /// <summary>É a vez do jogador.</summary>
    public void DestacarGrupo() => Destacar(0);

    /// <summary>
    /// É a vez de um inimigo. Recebe a posição dele **entre os vivos**, que é a
    /// mesma ordem em que a fila foi montada.
    /// </summary>
    public void DestacarInimigo(int posicaoEntreVivos) => Destacar(posicaoEntreVivos + 1);

    /// <summary>Numeral curto para desempatar homônimos. Nunca passa de cinco na prática.</summary>
    static string Romano(int n)
    {
        switch (n)
        {
            case 1: return "I";
            case 2: return "II";
            case 3: return "III";
            case 4: return "IV";
            case 5: return "V";
            default: return n.ToString();
        }
    }

    void CriarFicha(string rotulo, Color cor, Sprite arte, Color tint)
    {
        GameObject ficha = Instantiate(chipTemplate, container);
        ficha.SetActive(true);
        fichas.Add(ficha);

        var fundo = ficha.GetComponent<Image>();
        if (fundo != null) fundo.color = cor;

        var texto = ficha.transform.Find("Label")?.GetComponent<TMP_Text>();
        if (texto != null) texto.text = rotulo;

        var retrato = ficha.transform.Find("Art")?.GetComponent<Image>();
        if (retrato != null)
        {
            retrato.sprite = arte;
            retrato.color = arte != null ? tint : new Color(1f, 1f, 1f, 0f);
            retrato.preserveAspect = true;
        }
    }

    void Limpar()
    {
        foreach (var ficha in fichas)
            if (ficha != null) DestroyImmediate(ficha);

        fichas.Clear();
        Atual = 0;
    }
}
