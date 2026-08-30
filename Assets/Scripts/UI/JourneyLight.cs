using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A tocha escurece a tela.
///
/// <b>O que havia.</b> As tochas eram um contador — <c>🔥 3</c> no HUD — e uma
/// cobrança de estresse quando chegavam a zero. Entre o cinco e o zero nada
/// acontecia na tela, então a decisão de gastar um dia a mais só tinha peso
/// depois de a punição já ter começado. No Darkest Dungeon a luz é o medidor de
/// tensão: ela aperta a imagem muito antes de cobrar qualquer coisa.
///
/// <b>Como funciona.</b> Acende a camada <c>Img_Escuridao</c> do véu da tela, que
/// é uma segunda cópia da mesma vinheta. Somadas, as duas escurecem mais e a
/// sombra avança para o centro — é o aperto, e não só um escurecimento.
///
/// <b>Só na estrada.</b> Fora da jornada o alvo é zero: a guilda tem a sua
/// própria luz, e escurecer a tela do menu não diria nada sobre tocha nenhuma.
/// </summary>
public class JourneyLight : MonoBehaviour
{
    const string CaminhoDaEscuridao = "ScreenVeil/Img_Escuridao";

    /// <summary>Com este tanto de tochas a tela ainda está limpa.</summary>
    const int TochasParaLuzCheia = 4;

    /// <summary>
    /// Quanto a escuridão pesa no breu total. Não é 1: a tela precisa continuar
    /// jogável, e o combate acontece no meio dela.
    /// </summary>
    const float EscuridaoMaxima = 0.85f;

    /// <summary>
    /// A transição leva cerca de um segundo. Instantâneo pareceria defeito de
    /// renderização; lento demais e o jogador não liga o efeito à tocha que
    /// acabou de queimar.
    /// </summary>
    const float VelocidadeDaTransicao = 1.4f;

    /// <summary>
    /// Sobe junto com a cena, como o <see cref="GuildRosterPortraits"/>: o véu é
    /// montado por ferramenta de Editor e não teria onde hospedar isto.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Ligar()
    {
        var host = new GameObject(nameof(JourneyLight));
        DontDestroyOnLoad(host);
        host.AddComponent<JourneyLight>();
    }

    Image escuridao;
    float atual;

    void Update()
    {
        if (escuridao == null && !Procurar()) return;

        float alvo = Alvo();

        // Sem Lerp para um alvo fixo por frame: aqui a interpolação é para o
        // alvo do momento, e ele muda quando a tocha queima.
        atual = Mathf.MoveTowards(atual, alvo, VelocidadeDaTransicao * Time.unscaledDeltaTime);

        Color c = escuridao.color;
        if (Mathf.Approximately(c.a, atual)) return;

        c.a = atual;
        escuridao.color = c;
    }

    bool Procurar()
    {
        Canvas canvas = UIUtil.CanvasPrincipal();
        if (canvas == null) return false;

        Transform achado = canvas.transform.Find(CaminhoDaEscuridao);
        if (achado == null) return false;

        escuridao = achado.GetComponent<Image>();
        return escuridao != null;
    }

    /// <summary>
    /// Quanto a tela deve estar escura agora.
    ///
    /// A conta é da tocha, e não do dia: o jogador gasta tochas, e é a tocha que
    /// ele pode comprar no Mercado antes de partir.
    /// </summary>
    static float Alvo()
    {
        var jornada = JourneyManager.Instance;
        if (jornada == null) return 0f;

        // O painel desligado é o jogador de volta à guilda. Perguntar ao
        // JourneyManager se ele existe não basta: ele sobrevive à volta.
        if (jornada.journeyPanel == null || !jornada.journeyPanel.activeInHierarchy) return 0f;

        float fracao = Mathf.InverseLerp(TochasParaLuzCheia, 0f, jornada.torches);
        return fracao * EscuridaoMaxima;
    }
}
