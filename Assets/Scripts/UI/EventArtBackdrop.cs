using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Põe a ilustração do evento <b>atrás</b> do texto da caixa de evento.
///
/// <b>Por que este arquivo existe.</b> <see cref="EventData.eventImage"/> é
/// campo serializado desde o começo do projeto e <i>ninguém o lia</i>: uma busca
/// por <c>eventImage</c> no código inteiro devolvia só a própria declaração.
/// Preencher os 25 eventos sem isto seria gravar dado que nunca aparece na tela.
///
/// <b>Onde a arte entra.</b> Como primeiro filho da caixa do evento. A caixa já
/// tem um <c>Image</c> no próprio objeto (o fundo quase preto que
/// <c>GuildSceneSetup</c> monta); um filho desenha por cima desse fundo e
/// <i>abaixo</i> do título, da descrição e dos botões, que são irmãos
/// posteriores. É o mesmo arranjo do <c>BiomeBackground</c> no painel da
/// jornada — inclusive na opacidade baixa, e pela mesma razão: a tela é de
/// leitura, e arte clara atrás de texto claro apaga o texto.
///
/// <b>Nada é gravado na cena.</b> O objeto nasce em runtime e morre com ela.
/// Trocar a montagem da cena, o prefab da caixa ou a própria arte não exige
/// mexer aqui, e não há um objeto a mais no <c>SampleScene</c> para alguém
/// apagar por engano.
/// </summary>
public class EventArtBackdrop : MonoBehaviour
{
    const string NomeDoQuadro = "EventArt";

    /// <summary>
    /// Quanto da ilustração passa. Baixo de propósito.
    ///
    /// A descrição do evento é texto claro e pequeno (19pt) sobre esta arte. Nos
    /// testes, acima de ~0,35 as cenas de lava e as douradas começam a comer as
    /// letras; o multiplicador de cor abaixo tira mais um pouco do brilho sem
    /// lavar a imagem.
    /// </summary>
    static readonly Color Veu = new Color(0.85f, 0.83f, 0.80f, 0.30f);

    static EventData[] catalogo;

    Image quadro;
    string tituloEmCena;

    /// <summary>
    /// Sobe sozinho junto com a cena, como o <c>RunFlow</c> e o
    /// <c>PlayModeProbe</c> já fazem. Sem isto seria preciso mexer no
    /// <c>GuildSceneSetup</c> e na cena para pendurar um componente.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Ligar()
    {
        var host = new GameObject(nameof(EventArtBackdrop));
        DontDestroyOnLoad(host);
        host.AddComponent<EventArtBackdrop>();
    }

    void Update()
    {
        JourneyManager jm = JourneyManager.Instance;

        if (jm == null || jm.eventBox == null || !jm.eventBox.activeInHierarchy)
        {
            // Fora da parada a caixa some inteira, e o quadro com ela. Zerar o
            // título aqui faz a próxima parada recarregar a arte mesmo que seja
            // o mesmo evento — cada chegada é uma cena nova.
            tituloEmCena = null;
            return;
        }

        if (jm.eventTitleText == null) return;

        // O quadro é refeito quando a caixa é outra: trocar de cena destrói a
        // anterior, e um Image órfão continuaria "existindo" para este script.
        if (quadro == null || quadro.transform.parent != jm.eventBox.transform)
            quadro = Montar(jm.eventBox.transform);

        // O título é a única pista pública de qual evento está aberto —
        // JourneyManager.currentEvent é privado. Comparar o texto evita
        // procurar no catálogo a cada quadro: só muda quando o grupo chega
        // a outro lugar.
        string titulo = jm.eventTitleText.text;
        if (titulo == tituloEmCena) return;
        tituloEmCena = titulo;

        Sprite arte = Procurar(titulo);
        quadro.sprite = arte;
        quadro.enabled = arte != null;
    }

    /// <summary>
    /// Cria o quadro que ocupa a caixa inteira, atrás de tudo que já estiver nela.
    /// </summary>
    Image Montar(Transform caixa)
    {
        Transform achado = caixa.Find(NomeDoQuadro);
        if (achado != null) return achado.GetComponent<Image>();

        var go = new GameObject(NomeDoQuadro, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;

        rt.SetParent(caixa, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // Antes de qualquer texto: o Image do próprio objeto da caixa desenha
        // primeiro, depois os filhos na ordem da hierarquia.
        rt.SetAsFirstSibling();

        var img = go.GetComponent<Image>();
        img.color = Veu;
        img.raycastTarget = false;   // o clique é dos botões de escolha

        // preserveAspect encaixa o quadro inteiro do sprite dentro do retângulo.
        // Aqui isso é o certo, e não a armadilha de sempre: estas cenas são
        // pinturas opacas de borda a borda, sem transparência em volta — o que
        // encaixa é a imagem, não um desenho pequeno no meio de um quadro vazio.
        // A caixa é 620×470 e a cena é 16:9, então ela vira uma faixa centrada,
        // que é justo onde a caixa fica vazia entre a descrição e as escolhas.
        img.preserveAspect = true;
        img.enabled = false;

        return img;
    }

    /// <summary>
    /// A cena do evento com este título, ou nulo se não houver.
    ///
    /// Devolver nulo é resultado normal, não erro: o <c>EventPool</c> monta
    /// eventos em código ("Seguindo o Caminho", o confronto final genérico) que
    /// não existem em <c>Resources/Events</c> e nunca tiveram arte. Neles a
    /// caixa volta a ser o retângulo escuro de antes, e nada quebra.
    /// </summary>
    static Sprite Procurar(string titulo)
    {
        if (string.IsNullOrEmpty(titulo)) return null;

        if (catalogo == null) catalogo = Resources.LoadAll<EventData>("Events");

        foreach (EventData e in catalogo)
            if (e != null && e.eventTitle == titulo)
                return e.eventImage;

        return null;
    }
}
