using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class PlayerUI : MonoBehaviour
{
    public Text playerNameText;
    public Transform handParent;
    public GameObject cardUIPrefab;

    public void Initialize(string playerName)
    {
        playerNameText.text = playerName;
    }

    public void RefreshHand(List<Card> hand)
    {
        // Clear old card visuals
        foreach (Transform child in handParent)
            Destroy(child.gameObject);

        // Spawn new ones
        foreach (var card in hand)
        {
            var cardObj = Instantiate(cardUIPrefab, handParent);
            var cardUI = cardObj.GetComponent<CardUI>();
            cardUI.SetCard(card);
        }
    }
}