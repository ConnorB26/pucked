using System.Collections.Generic;
using Effects.Base;
using UnityEngine;

namespace Cards
{
    [CreateAssetMenu(fileName = "CardDefinition", menuName = "Puckd/Card")]
    public class CardDefinition : ScriptableObject
    {
        public string cardName;
        public CardCategory category;
        public Sprite artwork;

        public List<CardEffect> effects;
        // Each effect is modular. Cards can have 1 or multiple.

        public string description;
        public int variationIndex;
    }
}