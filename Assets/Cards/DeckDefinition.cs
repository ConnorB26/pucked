using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Cards
{
    /// <summary>
    /// High-level description of how to build a deck for a match of Puck'd.
    /// - Save cards (Goalie Save / Defuse equivalents) are defined by rules (players + ratio, variants).
    /// - All other cards are specified by category & explicit counts.
    /// Runtime code will read this and actually generate CardInstances for a specific player count.
    /// </summary>
    [CreateAssetMenu(fileName = "DeckDefinition", menuName = "Puckd/Deck / Deck Definition")]
    public class DeckDefinition : ScriptableObject
    {
        public string deckName = "Default Puck'd Deck";
        [TextArea] public string description;

        // --------------------------------------------------------------------
        // Save card settings (Defuse / Goalie Save equivalents)
        // --------------------------------------------------------------------

        [Tooltip("Which category counts as a 'save' card (Defuse / Goalie Save equivalent).")]
        public CardCategory saveCategory = CardCategory.GoalieSave;

        [Min(0)]
        [Tooltip("Extra saves as a ratio of player count.\n" +
                 "Total saves = players + floor(players * extraRatio).")]
        public float extraSavesPerPlayerRatio = 0.5f;

        /// <summary>
        /// Available save card variations. At deck-build time, the system will generate
        /// the required number of saves and for each save choose a variant based
        /// on these weights.
        /// 
        /// Weight is interpreted as a percent [0–100], and all weights should sum
        /// to <= 100. The remaining probability (if any) can be treated as
        /// "fallback" or simply unused, depending on how you implement the builder.
        /// </summary>
        public List<SaveVariant> saveVariants = new();

        // --------------------------------------------------------------------
        // Non-save card composition
        // --------------------------------------------------------------------

        [Tooltip("All cards that are NOT part of the save category.\n" +
                 "Save cards are controlled by the rules above.")]
        public List<CategoryEntry> categories = new();

        /// <summary>Total non-save cards across all categories.</summary>
        public int TotalBaseCardCount => categories?.Sum(c => c.TotalCount) ?? 0;

        /// <summary>
        /// Expected number of save cards for a given player count:
        /// players + floor(players * extraRatio).
        /// </summary>
        public int GetExpectedSaveCount(int playerCount)
        {
            if (playerCount <= 0) return 0;
            var extras = Mathf.FloorToInt(playerCount * Mathf.Max(0f, extraSavesPerPlayerRatio));
            return playerCount + extras;
        }

        /// <summary>
        /// Sum of save variant weights (treated as percentages).
        /// Should be <= 100 for a well-formed config.
        /// </summary>
        public float TotalSaveWeight =>
            saveVariants?.Where(v => v != null && v.card != null && v.weight > 0f)
                .Sum(v => v.weight) ?? 0f;

        /// <summary>
        /// Enumerates non-save cards with their counts.
        /// Runtime deck-building code can use this to generate instances.
        /// </summary>
        public IEnumerable<(CardDefinition card, int count)> EnumerateBaseCardCounts()
        {
            if (categories == null) yield break;

            foreach (var cat in categories)
            {
                if (cat.cards == null) continue;

                foreach (var slot in cat.cards)
                {
                    if (slot.card == null || slot.count <= 0) continue;

                    // Ignore any slots that accidentally reference the save category.
                    if (slot.card.category == saveCategory) continue;

                    yield return (slot.card, slot.count);
                }
            }
        }

        /// <summary>
        /// Expected total deck size for a given player count, including saves.
        /// </summary>
        public int GetExpectedTotalCardCount(int playerCount)
        {
            return TotalBaseCardCount + GetExpectedSaveCount(playerCount);
        }
    }

    // ------------------------------------------------------------------------
    // Supporting data types
    // ------------------------------------------------------------------------

    [Serializable]
    public class SaveVariant
    {
        [Tooltip("A specific save card variant (e.g., 'Goalie Save', 'Miracle Save').")]
        public CardDefinition card;

        [Range(0f, 100f)]
        [Tooltip("Percentage chance for this variant when generating save cards.\n" +
                 "All save variant weights should sum to <= 100%.")]
        public float weight;
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