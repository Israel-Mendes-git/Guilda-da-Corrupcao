using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A janela por onde se vê o campo de batalha, e a costura entre a interface e
/// o palco.
///
/// O <see cref="BattleStage"/> não sabe o que é uma view de combate; o
/// <see cref="CombatManager"/> não sabe o que é uma RenderTexture. Este
/// componente é quem traduz: recebe as views que o combate acabou de montar e,
/// a cada quadro, diz ao palco em que retângulo cada corpo tem de caber.
///
/// <b>A área é a do retrato, não a da view inteira.</b> Cada figura do combate
/// já tinha um filho "Portrait" ocupando a metade de cima, com o nome, o HP e as
/// barras embaixo. Encaixar o corpo ali põe a figura exatamente onde o desenho
/// já estava, sem tocar no layout que o resto da tela espera — e o retrato
/// parado apaga, virando reserva para quando não houver arte animada.
///
/// <b>Por que a cada quadro.</b> A fila do grupo é um LayoutGroup: as posições
/// só existem depois que o Unity resolve o layout, mudam quando alguém morre e
/// mudam de novo quando a tela troca de resolução. Perguntar todo quadro custa
/// quatro cantos por figura e dispensa saber quando algo se moveu.
/// </summary>
public class BattleFieldUI : MonoBehaviour
{
    /// <summary>A janela. Recebe a filmagem do palco.</summary>
    public RawImage janela;

    BattleStage palco;
    RectTransform janelaRT;

    readonly Dictionary<HeroData, RectTransform> areasDeHeroi = new Dictionary<HeroData, RectTransform>();
    readonly Dictionary<EnemyInstance, RectTransform> areasDeCriatura = new Dictionary<EnemyInstance, RectTransform>();

    static readonly Vector3[] cantosDaArea = new Vector3[4];
    static readonly Vector3[] cantosDaJanela = new Vector3[4];

    /// <summary>Para o teste: quantos corpos o palco tem em cena.</summary>
    public int CorposEmCena => palco != null ? palco.Corpos : 0;

    /// <summary>Para o teste: quantas criaturas têm quadros de animação de verdade.</summary>
    public int CriaturasAnimadas => palco != null ? palco.CriaturasAnimadas : 0;

    /// <summary>Para o teste: o herói ocupa quantos pixels de altura na janela.</summary>
    public float AlturaDoHeroiEmPixels => palco != null ? palco.AlturaDoHeroiEmPixels : 0f;

    /// <summary>O palco existe e tem gente? Quem pergunta é o combate, para saber
    /// se ainda precisa desenhar os retratos parados.</summary>
    public bool EmCena => palco != null && palco.EmCena;

    void Awake()
    {
        janelaRT = janela != null ? janela.rectTransform : GetComponent<RectTransform>();
    }

    void OnDestroy()
    {
        // O palco é objeto solto na raiz da cena, e não filho desta janela: sem
        // isto ele ficaria filmando uma textura que ninguém mostra.
        if (palco != null) Destroy(palco.gameObject);
    }

    /// <summary>
    /// Levanta o palco para o combate que começa e põe os dois lados em cena.
    /// Os registros da luta anterior morrem aqui.
    /// </summary>
    public void Preparar(IList<HeroData> party, IList<EnemyInstance> inimigos)
    {
        areasDeHeroi.Clear();
        areasDeCriatura.Clear();

        if (janelaRT == null)
            janelaRT = janela != null ? janela.rectTransform : GetComponent<RectTransform>();

        Vector2 tamanho = janelaRT != null ? janelaRT.rect.size : Vector2.zero;
        int largura = Mathf.RoundToInt(tamanho.x);
        int altura = Mathf.RoundToInt(tamanho.y);

        if (palco == null)
            palco = BattleStage.Criar(largura > 0 ? largura : BattleStage.LarguraPadrao,
                                      altura > 0 ? altura : BattleStage.AlturaPadrao);
        else if (largura > 0 && altura > 0)
            palco.Redimensionar(largura, altura);

        palco.Elenco(party, inimigos);

        if (janela != null)
        {
            janela.texture = palco.Textura;
            janela.color = Color.white;
            janela.raycastTarget = false;   // a carta é solta na view, não na janela
        }
    }

    /// <summary>
    /// A view deste herói está montada e ocupa este retângulo. Passar null tira
    /// o herói da conta — é o que acontece com quem morre e some da fila.
    /// </summary>
    public void RegistrarHeroi(HeroData heroi, RectTransform area)
    {
        if (heroi == null) return;

        if (area == null) areasDeHeroi.Remove(heroi);
        else areasDeHeroi[heroi] = area;
    }

    public void RegistrarCriatura(EnemyInstance inimigo, RectTransform area)
    {
        if (inimigo == null) return;

        if (area == null) areasDeCriatura.Remove(inimigo);
        else areasDeCriatura[inimigo] = area;
    }

    /// <summary>Fim do combate: apaga a janela e descarta os corpos.</summary>
    public void Encerrar()
    {
        areasDeHeroi.Clear();
        areasDeCriatura.Clear();

        if (palco != null) palco.Elenco(null, null);
        if (janela != null) janela.color = new Color(1f, 1f, 1f, 0f);
    }

    // --- O que acontece na luta, repassado ao palco --------------------------

    public void HeroiAtaca(HeroData heroi) { if (palco != null) palco.HeroiAtaca(heroi); }
    public void HeroiApanha(HeroData heroi) { if (palco != null) palco.HeroiApanha(heroi); }
    public void HeroiCai(HeroData heroi) { if (palco != null) palco.HeroiCai(heroi); }
    public void HeroiLevanta(HeroData heroi) { if (palco != null) palco.HeroiLevanta(heroi); }

    public void CriaturaAtaca(EnemyInstance e) { if (palco != null) palco.CriaturaAtaca(e); }
    public void CriaturaApanha(EnemyInstance e) { if (palco != null) palco.CriaturaApanha(e); }
    public void CriaturaMorre(EnemyInstance e) { if (palco != null) palco.CriaturaMorre(e); }

    public void AtualizarPresenca() { if (palco != null) palco.AtualizarPresenca(); }

    /// <summary>
    /// Depois do layout, e antes de desenhar: é em LateUpdate que as posições do
    /// LayoutGroup já estão resolvidas para este quadro. Em Update os corpos
    /// andariam um quadro atrás das views, o que aparece como figura deslizando
    /// sozinha quando alguém morre e a fila se refaz.
    /// </summary>
    void LateUpdate()
    {
        if (palco == null || palco.Textura == null || janelaRT == null) return;
        if (!janelaRT.gameObject.activeInHierarchy) return;

        janelaRT.GetWorldCorners(cantosDaJanela);

        float jx = cantosDaJanela[0].x;
        float jy = cantosDaJanela[0].y;
        float jw = cantosDaJanela[2].x - jx;
        float jh = cantosDaJanela[2].y - jy;

        if (jw <= 0.0001f || jh <= 0.0001f) return;

        foreach (var par in areasDeHeroi)
        {
            if (par.Value == null) continue;
            palco.Colocar(par.Key, EmPixelsDaTextura(par.Value, jx, jy, jw, jh));
        }

        foreach (var par in areasDeCriatura)
        {
            if (par.Value == null) continue;
            palco.Colocar(par.Key, EmPixelsDaTextura(par.Value, jx, jy, jw, jh));
        }
    }

    /// <summary>
    /// O retângulo da área, em pixels da textura, com origem no canto inferior
    /// esquerdo da janela.
    ///
    /// A conta é em coordenadas de mundo do Canvas, e não em pixels de tela: em
    /// Screen Space - Camera as duas escalas diferem, e a proporção dentro da
    /// janela é a única leitura que vale nos dois modos.
    /// </summary>
    Rect EmPixelsDaTextura(RectTransform area, float jx, float jy, float jw, float jh)
    {
        area.GetWorldCorners(cantosDaArea);

        float x0 = (cantosDaArea[0].x - jx) / jw * palco.Textura.width;
        float y0 = (cantosDaArea[0].y - jy) / jh * palco.Textura.height;
        float x1 = (cantosDaArea[2].x - jx) / jw * palco.Textura.width;
        float y1 = (cantosDaArea[2].y - jy) / jh * palco.Textura.height;

        return Rect.MinMaxRect(x0, y0, x1, y1);
    }
}
