using UnityEngine;

public class Card
{
    public CardData Data;
    public Player Owner { get; set; }

    public Card(CardData data, Player owner)
    {
        Data = data;
        Owner = owner;
    }

    public virtual void Play(GameManager game)
    {
        Debug.Log($"{Owner.Name} played {Data.cardName}");
    }
}