using UnityEngine;

/// <summary>
/// A criatura no campo de batalha: um sprite que troca de quadro.
///
/// Os pacotes de inimigo do projeto não trazem prefab, Animator nem controlador
/// — são PNG fatiado, uma fileira de quadros por estado. Montar um Animator por
/// criatura significaria um asset novo por inimigo para trocar quatro sprites;
/// aqui a animação é o que o material já é, e o estado inteiro cabe num
/// acumulador de tempo e num índice.
///
/// <b>Por que animar importa.</b> Até agora o combate mostrava o primeiro quadro
/// do Idle, parado, e todo o acontecimento da luta estava no log de texto: o
/// jogador lia "o Carniçal ataca" e via a mesma imagem de antes. O golpe que se
/// vê dispensa a linha que o descreve.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class EnemyBody : MonoBehaviour
{
    public enum Estado { Idle, Ataque, Apanhou, Morte }

    SpriteRenderer sr;
    EnemyAnimation quadros;

    Sprite[] atual;
    Estado estado = Estado.Idle;
    bool emLoop = true;
    float relogio;
    int indice;

    /// <summary>O que está tocando agora — o teste lê isto sem depender de captura.</summary>
    public Estado EstadoAtual => estado;

    /// <summary>Altura do desenho em unidades de mundo, já com a escala aplicada.</summary>
    public float AlturaEmUnidades => sr != null && sr.sprite != null
        ? sr.sprite.bounds.size.y * transform.lossyScale.y
        : 0f;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    /// <summary>
    /// Veste a criatura: quadros, cor e lado para onde ela olha.
    ///
    /// Aceita <see cref="EnemyData"/> sem animação nenhuma — nesse caso fica o
    /// retrato parado, que é exatamente o combate de antes. Arte que falta não
    /// pode impedir a luta de acontecer.
    /// </summary>
    public void Vestir(EnemyData dados, int ordemNaCena)
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (dados == null) return;

        quadros = dados.animation;
        corNatural = dados.portraitTint;
        sr.color = corNatural;
        sr.sortingOrder = ordemNaCena;

        // Os inimigos ficam à direita e encaram a party. Quem foi desenhado
        // olhando para a direita precisa ser espelhado, ou luta de costas.
        sr.flipX = quadros == null || quadros.desenhadaOlhandoParaDireita;

        atual = null;
        estado = Estado.Idle;
        indice = 0;
        relogio = 0f;

        Sprite[] parado = Serie(Estado.Idle);
        if (parado != null && parado.Length > 0)
        {
            atual = parado;
            sr.sprite = parado[0];
            emLoop = true;
        }
        else
        {
            // Sem quadros: o retrato, que é o primeiro do Idle, já resolvia antes.
            sr.sprite = dados.portrait;
            emLoop = false;
        }

        sr.enabled = sr.sprite != null;
    }

    /// <summary>
    /// Troca o que a criatura está fazendo.
    ///
    /// Idle é o único estado em laço; ataque, dano e morte tocam uma vez. Pedir
    /// de novo o mesmo estado em laço não reinicia a animação — chamar isto a
    /// cada refresh do combate faria o Idle tremer no primeiro quadro.
    /// </summary>
    public void Tocar(Estado novo)
    {
        // Quem morreu não volta a se mexer: a morte é o último quadro e fica.
        if (estado == Estado.Morte && novo != Estado.Idle) return;
        if (estado == Estado.Morte && novo == Estado.Idle) return;

        // "Apanhou" vira um clarão, e não uma troca de série.
        //
        // O Take Hit destes pacotes é a criatura inteira pintada de branco, e o
        // boneco ficava assim por meio segundo: parecia defeito de renderização,
        // não golpe recebido — foi o que o autor reportou. O clarão dura 0,12s,
        // some sozinho e não mexe na animação em curso, então a criatura continua
        // respirando enquanto apanha.
        if (novo == Estado.Apanhou)
        {
            if (clarao != null) StopCoroutine(clarao);
            clarao = StartCoroutine(Clarao());
            return;
        }

        if (novo == estado && emLoop) return;

        Sprite[] serie = Serie(novo);

        if (serie == null || serie.Length == 0)
        {
            // Sem os quadros do estado pedido, o que resta é o parado. A morte
            // sem quadros apaga a criatura, que é o que o card fazia antes.
            if (novo == Estado.Morte)
            {
                estado = Estado.Morte;
                emLoop = false;
                if (sr != null) sr.enabled = false;
                return;
            }

            return;
        }

        estado = novo;
        atual = serie;
        indice = 0;
        relogio = 0f;
        emLoop = novo == Estado.Idle;

        if (sr != null)
        {
            sr.enabled = true;
            sr.sprite = serie[0];
        }
    }

    Coroutine clarao;

    /// <summary>
    /// A cor com que esta criatura foi vestida — é ela que separa duas criaturas
    /// que dividem o mesmo desenho (ver <c>EnemyArt</c>).
    ///
    /// Guardada aqui, e não lida do <c>SpriteRenderer</c> na hora do golpe: dois
    /// golpes seguidos tomariam a cor do clarão como se fosse a natural, e a
    /// criatura ficaria branca pelo resto da luta.
    /// </summary>
    Color corNatural = Color.white;

    /// <summary>O golpe recebido: a criatura clareia e volta à cor dela.</summary>
    System.Collections.IEnumerator Clarao()
    {
        if (sr == null) yield break;

        Color natural = corNatural;
        Color claro = Color.Lerp(natural, Color.white, 0.85f);

        const float duracao = 0.12f;

        sr.color = claro;

        float t = 0f;
        while (t < duracao)
        {
            t += Time.deltaTime;
            if (sr == null) yield break;

            sr.color = Color.Lerp(claro, natural, t / duracao);
            yield return null;
        }

        sr.color = natural;
        clarao = null;
    }

    void Update()
    {
        if (atual == null || atual.Length <= 1 || sr == null) return;

        float fps = quadros != null && quadros.frameRate > 0f ? quadros.frameRate : 10f;
        relogio += Time.deltaTime;
        if (relogio < 1f / fps) return;

        relogio = 0f;
        indice++;

        if (indice >= atual.Length)
        {
            if (emLoop)
            {
                indice = 0;
            }
            else if (estado == Estado.Morte)
            {
                // Congela caído: a criatura morta continua no chão, como no
                // Darkest Dungeon, até o combate acabar e a view sumir.
                indice = atual.Length - 1;
                atual = null;
                sr.sprite = Serie(Estado.Morte)[indice];
                return;
            }
            else
            {
                Tocar(Estado.Idle);
                return;
            }
        }

        sr.sprite = atual[indice];
    }

    Sprite[] Serie(Estado qual)
    {
        if (quadros == null) return null;

        switch (qual)
        {
            case Estado.Ataque:  return quadros.attack;
            case Estado.Apanhou: return quadros.hit;
            case Estado.Morte:   return quadros.death;
            default:             return quadros.idle;
        }
    }
}
