using UnityEngine;
using UnityEngine.UI;

public class CardUI : MonoBehaviour
{
    public Image artwork;
    public Text cardNameText;
    private Card _cardRef;

    public void SetCard(Card card)
    {
        _cardRef = card;
        cardNameText.text = card.Data.cardName;
        artwork.sprite = card.Data.artwork;
    }

    public void OnPlayButtonClicked()
    {
    }
}