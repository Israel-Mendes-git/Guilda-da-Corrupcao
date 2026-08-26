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
        sr.color = dados.portraitTint;
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
