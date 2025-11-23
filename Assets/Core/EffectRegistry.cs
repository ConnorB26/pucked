using System.Collections.Generic;

public sealed class EffectRegistry
{
    private readonly Dictionary<CardType, IEffect> _byType = new();

    public void Register(CardType type, IEffect effect) => _byType[type] = effect;

    public IEffect Find(CardType type) => _byType.GetValueOrDefault(type);
}