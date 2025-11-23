using System.Collections.Generic;
using System.Linq;

public static class CatalogAdapter
{
    public static List<CardDef> BuildCatalogFromDeck(DeckList deck)
        => deck.Pairs().Select(p => p.card).Distinct().Select(c => new CardDef(c.defId, c.cardName, c.type)).ToList();

    public static List<(string defId, int copies)> BuildDeckTuples(DeckList deck)
        => deck.Pairs().Select(p => (p.card.defId, p.copies)).ToList();

    public static GameConfigDTO ToDto(GameConfig src) => new()
    {
        StartingHandSize = src.startingHandSize,
        StartingSaveCards = src.startingSaveCards,
        UseFixedSeed = src.useFixedSeed,
        Seed = src.seed,
        MainPhaseSeconds = src.mainPhaseSeconds,
        ReactionSeconds = src.reactionSeconds
    };
}