using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Inimigo ou herói visto como destino de uma carta arrastada.
///
/// Existe um destes em cada view criada pelo CombatManager. Ele não conhece
/// regras: pergunta ao CombatManager se a jogada vale e repassa o pedido.
/// </summary>
public class CombatDropTarget : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    /// <summary>Preenchido para alvos inimigos; nulo em alvos aliados.</summary>
    public EnemyInstance enemy;

    /// <summary>Preenchido para alvos aliados; nulo em alvos inimigos.</summary>
    public HeroData hero;

    [Header("Destaque")]
    public Color validHighlight = new Color(0.45f, 0.85f, 0.5f, 1f);
    public Color hoverHighlight = new Color(0.95f, 0.85f, 0.4f, 1f);

    private Image frame;
    private Color baseColor;
    private bool highlighted;

    void Awake()
    {
        // Usa a moldura própria se existir; senão, a imagem do próprio objeto.
        frame = transform.Find("Highlight")?.GetComponent<Image>() ?? GetComponent<Image>();
        if (frame != null) baseColor = frame.color;
    }

    public void Bind(EnemyInstance enemyTarget, HeroData heroTarget)
    {
        enemy = enemyTarget;
        hero = heroTarget;
    }

    public void OnDrop(PointerEventData eventData)
    {
        var drag = CardDragHandler.Dragging;
        if (drag == null) return;

        var combat = CombatManager.Instance;
        if (combat == null) return;

        if (combat.TryPlayCard(drag.Card, enemy, hero))
            drag.MarkConsumed();

        SetHighlight(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!IsValidForDraggedCard()) return;

        if (frame != null) frame.color = hoverHighlight;
        transform.localScale = Vector3.one * 1.05f;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        transform.localScale = Vector3.one;
        if (frame != null)
            frame.color = highlighted ? validHighlight : baseColor;
    }

    /// <summary>Acende o alvo enquanto uma carta compatível está na mão do jogador.</summary>
    public void SetHighlight(bool on)
    {
        highlighted = on;
        transform.localScale = Vector3.one;

        if (frame != null)
            frame.color = on ? validHighlight : baseColor;
    }

    bool IsValidForDraggedCard()
    {
        var drag = CardDragHandler.Dragging;
        var combat = CombatManager.Instance;

        return drag != null && combat != null && combat.CanPlayCard(drag.Card, enemy, hero);
    }
}
