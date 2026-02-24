using System.Collections.Generic;
using Effects.Base;
using UnityEngine;

namespace Cards
{
    /// <summary>ScriptableObject template defining a card's identity and list of effects.</summary>
    [CreateAssetMenu(fileName = "CardDefinition", menuName = "Puckd/Card")]
    public class CardDefinition : ScriptableObject
    {
        public string cardName;
        public CardCategory category;
        public Sprite artwork;
        public List<CardEffect> effects;
        public string description;
        public int variationIndex;
    }
}
