using UnityEngine;

public abstract class EffectDef : ScriptableObject
{
    public abstract IEffect CreateEffectInstance();
}