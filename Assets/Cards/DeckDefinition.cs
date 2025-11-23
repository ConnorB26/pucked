using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Cards
{
    /// <summary>
    /// A specific deck / ruleset configuration for a match of Puck'd.
    /// Uses references to CardDefinition assets and per-card counts.
    /// </summary>
    [CreateAssetMenu(fileName = "DeckDefinition", menuName = "Puckd/Deck / Deck Definition")]
    public class DeckDefinition : ScriptableObject
    {
        [Header("Deck Info")] public string deckName = "Default Puck'd Deck";
        [TextArea] public string description;

        /// <summary>
        /// Grouped composition by high-level category (Puckd, GoalieSave, Attack, etc).
        /// </summary>
        public List<CategoryEntry> categories = new();

        // ---- Convenience helpers for runtime / editor ----

        /// <summary>Total number of cards across all categories.</summary>
        public int TotalCardCount =>
            categories?.Sum(c => c.TotalCount) ?? 0;

        /// <summary>
        /// Returns a flat list with each card repeated 'count' times.
        /// Useful when actually building the deck at game start.
        /// </summary>
        public List<CardDefinition> BuildDeckList()
        {
            var result = new List<CardDefinition>();

            if (categories == null) return result;

            foreach (var cat in categories)
            {
                if (cat.cards == null) continue;

                foreach (var slot in cat.cards)
                {
                    if (slot.card == null || slot.count <= 0) continue;

                    for (var i = 0; i < slot.count; i++)
                        result.Add(slot.card);
                }
            }

            return result;
        }
    }

    [Serializable]
    public class CategoryEntry
    {
        public CardCategory category;

        /// <summary>Each slot is a specific CardDefinition + quantity.</summary>
        public List<CardSlot> cards = new();

        public int TotalCount => cards?.Sum(c => Mathf.Max(0, c.count)) ?? 0;
    }

    [Serializable]
    public class CardSlot
    {
        public CardDefinition card;
        [Min(0)] public int count = 1;
    }
}