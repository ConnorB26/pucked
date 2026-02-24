using System.Collections.Generic;
using Cards;
using UnityEngine;
using Random = System.Random;

namespace Gameplay
{
    /// <summary>
    /// Manages draw and discard piles for a single match.
    /// Builds the deck from a <see cref="DeckDefinition"/> asset.
    /// </summary>
    public class DeckManager
    {
        #region Fields & Properties

        private readonly List<CardInstance> _drawPile = new();
        private readonly List<CardInstance> _discardPile = new();
        private readonly Random _rng = new();
        private int _nextInstanceId = 1;

        public int DrawCount => _drawPile.Count;
        public int DiscardCount => _discardPile.Count;

        #endregion

        #region Initialization

        /// <summary>
        /// Builds the full deck: non-save cards by count, save cards by weighted variant selection.
        /// Reserves up to one save per player for starting hands; extras go into the draw pile.
        /// </summary>
        public void InitializeFromDeckDefinition(
            DeckDefinition definition, int playerCount, bool shuffle,
            out List<CardInstance> startingSaves)
        {
            _drawPile.Clear();
            _discardPile.Clear();
            _nextInstanceId = 1;
            startingSaves = new List<CardInstance>();

            if (definition == null)
            {
                Debug.LogError("DeckManager: definition is null");
                return;
            }

            foreach (var (card, count) in definition.EnumerateBaseCardCounts())
            {
                if (card == null || count <= 0) continue;
                for (var i = 0; i < count; i++)
                    _drawPile.Add(CreateInstance(card));
            }

            var saveInstances = GenerateSaveInstances(definition, playerCount);

            var guaranteedCount = Mathf.Min(playerCount, saveInstances.Count);
            for (var i = 0; i < guaranteedCount; i++)
                startingSaves.Add(saveInstances[i]);
            for (var i = guaranteedCount; i < saveInstances.Count; i++)
                _drawPile.Add(saveInstances[i]);

            if (shuffle) Shuffle();
        }

        /// <summary>Overload without starting saves output.</summary>
        public void InitializeFromDeckDefinition(DeckDefinition definition, int playerCount, bool shuffle)
        {
            InitializeFromDeckDefinition(definition, playerCount, shuffle, out _);
        }

        #endregion

        #region Core Operations

        /// <summary>Fisher-Yates shuffle on the draw pile.</summary>
        public void Shuffle()
        {
            for (var i = _drawPile.Count - 1; i > 0; i--)
            {
                var j = _rng.Next(0, i + 1);
                (_drawPile[i], _drawPile[j]) = (_drawPile[j], _drawPile[i]);
            }
        }

        /// <summary>Removes and returns the top card, or null if empty.</summary>
        public CardInstance? DrawTop()
        {
            if (_drawPile.Count == 0) return null;
            var lastIndex = _drawPile.Count - 1;
            var inst = _drawPile[lastIndex];
            _drawPile.RemoveAt(lastIndex);
            return inst;
        }

        /// <summary>Returns the top N cards without removing them.</summary>
        public List<CardInstance> PeekTop(int count)
        {
            var result = new List<CardInstance>();
            var actual = Mathf.Min(count, _drawPile.Count);
            for (var i = 0; i < actual; i++)
                result.Add(_drawPile[_drawPile.Count - 1 - i]);
            return result;
        }

        /// <summary>Adds a card to the discard pile.</summary>
        public void Discard(CardInstance instance) => _discardPile.Add(instance);

        /// <summary>Adds multiple cards to the discard pile.</summary>
        public void DiscardMany(IEnumerable<CardInstance> instances)
        {
            if (instances == null) return;
            _discardPile.AddRange(instances);
        }

        /// <summary>Returns cards to the draw pile and reshuffles.</summary>
        public void ReturnCardsToDrawAndShuffle(IEnumerable<CardInstance> instances)
        {
            if (instances == null) return;
            _drawPile.AddRange(instances);
            Shuffle();
        }

        #endregion

        #region Helpers

        private CardInstance CreateInstance(CardDefinition def) => new(_nextInstanceId++, def);

        /// <summary>Generates save card instances using weighted variant selection.</summary>
        private List<CardInstance> GenerateSaveInstances(DeckDefinition definition, int playerCount)
        {
            var result = new List<CardInstance>();
            var saveCount = definition.GetExpectedSaveCount(playerCount);
            if (saveCount <= 0) return result;

            var variants = new List<SaveVariant>();
            foreach (var v in definition.saveVariants)
            {
                if (v == null || v.card == null || v.weight <= 0f) continue;
                variants.Add(v);
            }

            if (variants.Count == 0)
            {
                Debug.LogWarning("DeckManager: no valid save variants configured.");
                return result;
            }

            // Normalize weights as relative values.
            var totalWeight = 0f;
            foreach (var v in variants) totalWeight += v.weight;
            if (totalWeight <= 0f)
            {
                totalWeight = variants.Count;
                foreach (var v in variants) v.weight = 1f;
            }

            for (var i = 0; i < saveCount; i++)
            {
                var chosen = PickVariant(variants, totalWeight);
                if (chosen?.card != null)
                    result.Add(CreateInstance(chosen.card));
            }

            return result;
        }

        private SaveVariant PickVariant(List<SaveVariant> variants, float totalWeight)
        {
            var roll = _rng.NextDouble() * totalWeight;
            var accum = 0.0;
            foreach (var v in variants)
            {
                accum += v.weight;
                if (roll <= accum) return v;
            }
            return variants.Count > 0 ? variants[^1] : null;
        }

        #endregion
    }
}
