using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A tela de slots — a mesma para carregar e para salvar.
///
/// Um componente só porque a lista é idêntica nos dois casos e só muda o que o
/// clique faz; duas telas quase iguais é como se cria a que ninguém lembra de
/// atualizar.
///
/// Cada linha mostra o bastante para o jogador reconhecer a partida sem abri-la:
/// ciclo, heróis vivos, ouro, Corrupção e quando foi salva. Um slot corrompido
/// aparece dizendo que está corrompido, em vez de sumir da lista.
/// </summary>
public class SaveSlotsUI : MonoBehaviour
{
    public enum Modo { Carregar, Salvar }

    [Header("Painel")]
    public GameObject panel;
    public TMP_Text titleText;
    public TMP_Text hintText;

    [Header("Lista")]
    public Transform slotContainer;

    /// <summary>Molde de uma linha, desativado. Clonado por slot.</summary>
    public GameObject slotTemplate;

    [Header("Rodapé")]
    public Button closeButton;

    Modo modo;
    System.Action aoFechar;

    readonly List<GameObject> linhas = new List<GameObject>();

    // Sem Awake que desative o painel: o componente mora nele, e ligar o painel
    // rodaria o Awake que o desligaria no mesmo frame. Ver OptionsUI.

    public void Abrir(Modo modo, System.Action quandoFechar)
    {
        this.modo = modo;
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
        if (titleText != null)
            titleText.text = modo == Modo.Carregar ? "CARREGAR PARTIDA" : "SALVAR PARTIDA";

        if (hintText != null)
        {
            if (modo == Modo.Salvar && !SaveSystem.PodeSalvarAgora(out string motivo))
                hintText.text = $"<color=#B04040>Não dá para salvar agora: {motivo}.</color>";
            else if (modo == Modo.Salvar)
                hintText.text = "O slot automático é escrito pelo jogo ao fim de cada jornada.";
            else
                hintText.text = "Carregar substitui a partida em andamento.";
        }

        Limpar();

        if (slotContainer == null || slotTemplate == null) return;

        foreach (SaveHeader cabecalho in SaveSystem.ListarSlots())
            MontarLinha(cabecalho);
    }

    void MontarLinha(SaveHeader h)
    {
        GameObject linha = Instantiate(slotTemplate, slotContainer);
        linha.SetActive(true);
        linhas.Add(linha);

        SetText(linha, "Name", SaveSystem.NomeDoSlot(h.slot));
        SetText(linha, "Detail", Descrever(h));

        bool ehAuto = h.slot == SaveSystem.SlotAuto;

        Button principal = Achar<Button>(linha, "Action");
        Button apagar = Achar<Button>(linha, "Delete");

        if (principal != null)
        {
            SetText(principal.gameObject, "Text", modo == Modo.Carregar ? "Carregar" : "Salvar aqui");

            bool podeAgir = modo == Modo.Carregar
                ? h.exists && !h.corrupted
                // O slot automático pertence ao jogo: deixá-lo sobrescrever à mão
                // faria o "Continuar" apontar para um estado que o jogador
                // escolheu, e não para o último ponto seguro conhecido.
                : !ehAuto && SaveSystem.PodeSalvarAgora(out string _);

            principal.interactable = podeAgir;

            string slot = h.slot;
            principal.onClick.RemoveAllListeners();
            principal.onClick.AddListener(() => Agir(slot));
        }

        if (apagar != null)
        {
            apagar.interactable = h.exists;
            apagar.gameObject.SetActive(!ehAuto || h.exists);

            string slot = h.slot;
            apagar.onClick.RemoveAllListeners();
            apagar.onClick.AddListener(() => Apagar(slot));
        }
    }

    static string Descrever(SaveHeader h)
    {
        if (!h.exists) return "<color=#8A867F>vazio</color>";
        if (h.corrupted) return "<color=#B04040>arquivo ilegível</color>";

        string chefe = h.bossAvailable ? "  <color=#B04040>⚔️ Chefe Supremo à espera</color>" : "";
        string quando = h.savedAt == System.DateTime.MinValue
            ? ""
            : "  <size=85%>" + h.savedAt.ToLocalTime().ToString("dd/MM HH:mm") + "</size>";

        return $"Ciclo {h.cycle} · {h.heroesAlive} heróis · {h.gold} de ouro · "
             + $"Corrupção {h.corruption}%{chefe}{quando}";
    }

    void Agir(string slot)
    {
        GameAudio.Efeito(Sfx.Click);

        if (modo == Modo.Carregar)
        {
            if (!SceneFlow.Carregar(slot)) Preencher();
            return;
        }

        SaveSystem.Salvar(slot);
        Preencher();
    }

    void Apagar(string slot)
    {
        GameAudio.Efeito(Sfx.Click);

        SaveSystem.Apagar(slot);
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
