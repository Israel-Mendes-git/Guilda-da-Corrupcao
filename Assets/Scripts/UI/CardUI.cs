using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CardUI : MonoBehaviour
{
    /// <summary>
    /// Põe a arte da carta numa view montada a partir do prefab, sem depender de
    /// um <see cref="CardUI"/> ligado no Inspector.
    ///
    /// O prefab da carta não tem este componente — o combate e a jornada montam a
    /// carta preenchendo filhos por nome. Só que o prefab tem hierarquia plana e
    /// **quatro** objetos chamados "Image", então procurar pelo nome pega um
    /// enfeite de canto. A arte é a de maior área que não é o fundo nem a moldura.
    /// </summary>
    public static void AplicarArte(GameObject view, CardData card)
    {
        if (view == null || card == null || card.cardImage == null) return;

        Image alvo = null;
        float maiorArea = 0f;

        foreach (Image img in view.GetComponentsInChildren<Image>(true))
        {
            string nome = img.gameObject.name;
            if (nome == "Background" || nome == "Border") continue;

            Rect r = img.rectTransform.rect;
            float area = Mathf.Abs(r.width * r.height);
            if (area > maiorArea)
            {
                maiorArea = area;
                alvo = img;
            }
        }

        if (alvo == null) return;

        alvo.sprite = card.cardImage;
        // O slot nasce com uma cor de preenchimento que serviria de placeholder;
        // com sprite por cima, ela tingiria a arte.
        alvo.color = Color.white;
        alvo.preserveAspect = true;
    }

    [Header("UI References")]
    public TMP_Text cardNameText;
    public TMP_Text descriptionText;
    public TMP_Text costText;
    public Image cardImage;
    public Image backgroundImage;
    public Button cardButton;

    private CardData currentCard;
    private bool isJourneyMode;
    private System.Action<CardData> onCardSelected;

    /// <summary>
    /// Preenche a carta sem ligar clique nenhum. É o que o combate usa, já que
    /// lá as cartas são jogadas por arrasto.
    /// </summary>
    public void Bind(CardData card, bool journeyMode)
    {
        Initialize(card, journeyMode, null);
    }

    public void Initialize(CardData card, bool journeyMode, System.Action<CardData> onSelected)
    {
        currentCard = card;
        isJourneyMode = journeyMode;
        onCardSelected = onSelected;

        // Preenche os textos
        if (cardNameText != null)
            cardNameText.text = card.cardName;

        if (descriptionText != null)
            descriptionText.text = card.GetDescription(journeyMode);

        if (costText != null)
            costText.text = $"⚡ {card.energyCost}";

        if (cardImage != null && card.cardImage != null)
            cardImage.sprite = card.cardImage;

        // Cor por raridade
        if (backgroundImage != null)
        {
            switch (card.rarity)
            {
                case CardRarity.Common:
                    backgroundImage.color = new Color(0.4f, 0.4f, 0.45f);
                    break;
                case CardRarity.Rare:
                    backgroundImage.color = new Color(0.2f, 0.3f, 0.6f);
                    break;
                case CardRarity.Epic:
                    backgroundImage.color = new Color(0.5f, 0.2f, 0.7f);
                    break;
                case CardRarity.Legendary:
                    backgroundImage.color = new Color(0.8f, 0.6f, 0.1f);
                    break;
            }
        }

        // Configura botão. Sem callback, o botão fica fora do caminho para não
        // capturar o ponteiro de quem for arrastar a carta.
        if (cardButton != null)
        {
            cardButton.onClick.RemoveAllListeners();

            if (onSelected != null)
            {
                cardButton.onClick.AddListener(() => onCardSelected?.Invoke(currentCard));
                cardButton.interactable = true;
            }
            else
            {
                cardButton.interactable = false;
            }
        }
    }

    public void SetInteractable(bool interactable)
    {
        if (cardButton != null)
            cardButton.interactable = interactable;
    }

    /// <summary>
    /// Acrescenta uma linha à descrição já preenchida. O combate usa isto para
    /// avisar, na própria carta, que ela vai sair enfraquecida pela formação —
    /// descobrir isso só depois de jogar seria uma armadilha.
    /// </summary>
    public void AppendNote(string note)
    {
        if (descriptionText == null || string.IsNullOrEmpty(note)) return;

        descriptionText.text = string.IsNullOrEmpty(descriptionText.text)
            ? note
            : descriptionText.text + "\n" + note;
    }
}