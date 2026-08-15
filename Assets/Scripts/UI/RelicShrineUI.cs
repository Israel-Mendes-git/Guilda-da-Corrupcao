using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// O Santuário das Relíquias — onde o que sobrou das guildas mortas é gasto.
///
/// A Fase 3 criou a moeda e a fez virar ouro sozinha, o que dava à
/// meta-progressão um efeito real mas nenhuma decisão: o jogador nunca escolhia
/// nada. Aqui ele escolhe, e o custo por nível faz a escolha doer — comprar o
/// terceiro nível do cofre é abrir mão dos alojamentos por mais uma run.
///
/// Só aparece entre partidas, no título: os destraves entram na guilda no
/// instante em que ela é fundada (<c>GuildManager.AplicarDestraves</c>), e
/// comprar no meio de uma run não mudaria nada do que está em curso.
/// </summary>
public class RelicShrineUI : MonoBehaviour
{
    [Header("Painel")]
    public GameObject panel;
    public TMP_Text titleText;
    public TMP_Text balanceText;
    public TMP_Text hintText;

    [Header("Lista")]
    public Transform unlockContainer;

    /// <summary>Molde de um destrave, desativado. Clonado por item do catálogo.</summary>
    public GameObject unlockTemplate;

    [Header("Rodapé")]
    public Button closeButton;

    System.Action aoFechar;
    readonly List<GameObject> linhas = new List<GameObject>();

    // Sem Awake que desative o painel: o componente mora nele, e ligar o painel
    // rodaria o Awake que o desligaria no mesmo frame. Ver OptionsUI.

    public void Abrir(System.Action quandoFechar)
    {
        aoFechar = quandoFechar;

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Fechar);
        }

        Preencher();

        if (panel != null)
        {
            panel.SetActive(true);
            panel.transform.SetAsLastSibling();
        }
    }

    void Preencher()
    {
        if (titleText != null) titleText.text = "SANTUÁRIO DAS RELÍQUIAS";

        if (balanceText != null)
            balanceText.text = $"<color=#D9B85A>◆ {MetaProgression.Relics}</color> "
                             + "<size=80%>relíquias</size>";

        if (hintText != null)
            hintText.text = "As relíquias vêm de cada ciclo sobrevivido. "
                          + "O que for comprado aqui vale para a próxima guilda fundada.";

        Limpar();

        if (unlockContainer == null || unlockTemplate == null) return;

        foreach (Unlock destrave in MetaProgression.Catalogo)
            MontarLinha(destrave);
    }

    void MontarLinha(Unlock destrave)
    {
        GameObject linha = Instantiate(unlockTemplate, unlockContainer);
        linha.SetActive(true);
        linhas.Add(linha);

        int nivel = MetaProgression.NivelDe(destrave.id);
        bool noTeto = destrave.NoTeto(nivel);
        int custo = noTeto ? 0 : destrave.CustoDoNivel(nivel + 1);

        SetText(linha, "Name", $"{destrave.nome}   <size=80%>{Pontinhos(destrave, nivel)}</size>");
        SetText(linha, "Description", destrave.descricao);

        string efeitoAtual = nivel > 0
            ? $"<color=#7FA05A>agora: {destrave.efeitoPorNivel} × {nivel}</color>   "
            : "";

        SetText(linha, "Effect", noTeto
            ? efeitoAtual + "<color=#8A867F>no máximo</color>"
            : efeitoAtual + $"<size=90%>próximo nível: {destrave.efeitoPorNivel}</size>");

        Button comprar = Achar<Button>(linha, "Buy");
        if (comprar == null) return;

        SetText(comprar.gameObject, "Text", noTeto ? "Completo" : $"◆ {custo}");
        comprar.interactable = !noTeto && MetaProgression.Relics >= custo;

        string id = destrave.id;
        comprar.onClick.RemoveAllListeners();
        comprar.onClick.AddListener(() => Comprar(id));
    }

    /// <summary>Níveis como marcas cheias e vazias — lê-se mais rápido que "2/3".</summary>
    static string Pontinhos(Unlock destrave, int nivel)
    {
        var sb = new System.Text.StringBuilder();

        for (int i = 0; i < destrave.maxLevel; i++)
            sb.Append(i < nivel ? "◆" : "◇");

        return sb.ToString();
    }

    void Comprar(string id)
    {
        if (MetaProgression.Comprar(id))
            GameAudio.Efeito(Sfx.Coin);

        Preencher();
    }

    void Limpar()
    {
        foreach (var linha in linhas)
            if (linha != null) Destroy(linha);

        linhas.Clear();
    }

    void Fechar()
    {
        GameAudio.Efeito(Sfx.Click);

        if (panel != null) panel.SetActive(false);

        var acao = aoFechar;
        aoFechar = null;
        acao?.Invoke();
    }

    static void SetText(GameObject raiz, string nomeDoFilho, string valor)
    {
        foreach (var t in raiz.GetComponentsInChildren<TMP_Text>(true))
            if (t.gameObject.name == nomeDoFilho) { t.text = valor; return; }
    }

    static T Achar<T>(GameObject raiz, string nomeDoFilho) where T : Component
    {
        foreach (var c in raiz.GetComponentsInChildren<T>(true))
            if (c.gameObject.name == nomeDoFilho) return c;

        return null;
    }
}
