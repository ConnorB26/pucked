using System.Collections.Generic;
using Cards;
using UnityEngine;
using Random = System.Random;

namespace Gameplay
{
    /// <summary>
    /// Manages draw/discard piles for a single match instance,
    /// using CardInstance for all runtime cards and building from DeckDefinition.
    /// </summary>
    public class DeckManager
    {
        #region Fields & Properties

        private readonly List<CardInstance> _drawPile = new();
        private readonly List<CardInstance> _discardPile = new();
        private readonly Random _rng = new();

        // Instance IDs are unique *per match*, managed here.
        private int _nextInstanceId = 1;

        public int DrawCount => _drawPile.Count;
        public int DiscardCount => _discardPile.Count;

        #endregion

        #region Initialization

        /// <summary>
        /// Builds the deck for a match, given a DeckDefinition and player count.
        /// - Adds all non-save cards based on the definition's composition.
        /// - Generates save cards according to the save rules + variant weights.
        /// - Reserves up to one save per player in 'startingSaves' (for guaranteed starting hands),
        ///   and shuffles any extra saves into the draw pile.
        /// </summary>
        /// <param name="definition">Deck definition asset.</param>
        /// <param name="playerCount">Number of players in this match.</param>
        /// <param name="shuffle">Whether to shuffle after building the deck.</param>
        /// <param name="startingSaves">
        /// Output list of CardInstances reserved to be dealt 1-per-player
        /// at the beginning of the game (like Defuses in Exploding Kittens).
        /// </param>
        public void InitializeFromDeckDefinition(
            DeckDefinition definition,
            int playerCount,
            bool shuffle,
            out List<CardInstance> startingSaves)
        {
            _drawPile.Clear();
            _discardPile.Clear();
            _nextInstanceId = 1;
            startingSaves = new List<CardInstance>();

            if (definition == null)
            {
                Debug.LogError("DeckManager.InitializeFromDeckDefinition: definition is null");
                return;
            }

            // -----------------------------------------------------------------
            // 1) Add all non-save cards from the deck definition
            // -----------------------------------------------------------------
            foreach (var (card, count) in definition.EnumerateBaseCardCounts())
            {
                if (card == null || count <= 0)
                    continue;

                for (var i = 0; i < count; i++)
                {
                    _drawPile.Add(CreateInstance(card));
                }
            }

            // -----------------------------------------------------------------
            // 2) Generate save card instances based on rules & variant weights
            // -----------------------------------------------------------------
            var saveInstances = GenerateSaveInstances(definition, playerCount);

            // Reserve up to one save per player for guaranteed starting hands
            var guaranteedCount = Mathf.Min(playerCount, saveInstances.Count);
            for (var i = 0; i < guaranteedCount; i++)
            {
                startingSaves.Add(saveInstances[i]);
            }

            // Any remaining saves go into the draw pile
            for (var i = guaranteedCount; i < saveInstances.Count; i++)
            {
                _drawPile.Add(saveInstances[i]);
            }

            if (shuffle)
                Shuffle();
        }

        /// <summary>
        /// Convenience overload if you don't care about the list of starting saves.
        /// (They will still be "consumed" logically but you won't get them back.)
        /// </summary>
        public void InitializeFromDeckDefinition(
            DeckDefinition definition,
            int playerCount,
            bool shuffle)
        {
            InitializeFromDeckDefinition(definition, playerCount, shuffle, out _);
        }

        #endregion

        #region Core Operations

        public void Shuffle()
        {
            // Fisher–Yates on instances
            for (var i = _drawPile.Count - 1; i > 0; i--)
            {
                var j = _rng.Next(0, i + 1);
                (_drawPile[i], _drawPile[j]) = (_drawPile[j], _drawPile[i]);
            }
        }

        /// <summary>Draws the top CardInstance from the draw pile. Returns null if empty.</summary>
        public CardInstance? DrawTop()
        {
            if (_drawPile.Count == 0)
                return null;

            var lastIndex = _drawPile.Count - 1;
            var inst = _drawPile[lastIndex];
            _drawPile.RemoveAt(lastIndex);
            return inst;
        }

        /// <summary>Peeks at the top N instances without removing them.</summary>
        public List<CardInstance> PeekTop(int count)
        {
            var result = new List<CardInstance>();
            var actual = Mathf.Min(count, _drawPile.Count);

            for (var i = 0; i < actual; i++)
            {
                result.Add(_drawPile[_drawPile.Count - 1 - i]);
            }

            return result;
        }

        public void Discard(CardInstance instance)
        {
            _discardPile.Add(instance);
        }

        public void DiscardMany(IEnumerable<CardInstance> instances)
        {
            if (instances == null) return;
            _discardPile.AddRange(instances);
        }

        #endregion

        #region Helpers (Instance creation & Save generation)

        private CardInstance CreateInstance(CardDefinition def)
        {
            return new CardInstance(_nextInstanceId++, def);
        }

        /// <summary>
        /// Generates all save card instances for this match based on the deck definition.
        /// Uses saveVariants and their weights (0–100) to pick variants.
        /// </summary>
        private List<CardInstance> GenerateSaveInstances(DeckDefinition definition, int playerCount)
        {
            var result = new List<CardInstance>();

            var saveCount = definition.GetExpectedSaveCount(playerCount);
            if (saveCount <= 0)
                return result;

            // Collect valid variants
            var variants = new List<SaveVariant>();
            foreach (var v in definition.saveVariants)
            {
                if (v == null || v.card == null || v.weight <= 0f)
                    continue;
                variants.Add(v);
            }

            if (variants.Count == 0)
            {
                Debug.LogWarning(
                    "DeckManager.GenerateSaveInstances: no valid save variants configured, " +
                    "but saveCount > 0. No save cards will be generated.");
                return result;
            }

            // Normalize weights across the configured variants (we ignore the "≤ 100%" constraint here
            // and just treat the configured weights as relative).
            var totalWeight = 0f;
            foreach (var v in variants)
                totalWeight += v.weight;

            if (totalWeight <= 0f)
            {
                // Fallback: equal weights
                totalWeight = variants.Count;
                foreach (var v in variants)
                    v.weight = 1f;
            }

            for (var i = 0; i < saveCount; i++)
            {
                var chosen = PickVariant(variants, totalWeight);
                if (chosen == null || chosen.card == null)
                    continue;

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
                if (roll <= accum)
                    return v;
            }

            // Fallback (shouldn't normally happen)
            return variants.Count > 0 ? variants[^1] : null;
        }

        #endregion

        #region Return / Reinsert

        /// <summary>
        /// Returns a collection of card instances back into the draw pile
        /// and shuffles them in. Used when a player leaves mid-game.
        /// </summary>
        public void ReturnCardsToDrawAndShuffle(IEnumerable<CardInstance> instances)
        {
            if (instances == null) return;

            _drawPile.AddRange(instances);
            Shuffle();
        }

        #endregion
    }
}