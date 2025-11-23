using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "DeckList", menuName = "Puckd/Deck List")]
public class DeckList : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public CardConfig card;
        [Min(1)] public int count = 1;
    }

    [Tooltip("Cards included in this decklist and their copy counts.")]
    public List<Entry> entries = new();

    public IEnumerable<(CardConfig card, int copies)> Pairs()
        => entries.Where(e => e.card != null && e.count > 0).Select(e => (e.card, e.count));

    public int TotalCards => entries.Where(e => e.card != null && e.count > 0).Sum(e => e.count);

    public int CountByType(CardType t) =>
        entries.Where(e => e.card != null && e.card.type == t).Sum(e => Mathf.Max(0, e.count));
}