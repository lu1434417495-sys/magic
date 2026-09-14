using System;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class FixedCriticalOneDamageResolver : FixedHitOneDamageResolver
{
    public FixedCriticalOneDamageResolver()
    {
        SetHitResolver(new FixedCriticalHitResolver());
    }

    internal new BattleFateEventBus GetFateEventBus() => base.GetFateEventBus();

}
