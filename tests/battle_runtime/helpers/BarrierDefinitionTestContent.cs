using System.Collections.Generic;
using Godot;

internal static class BarrierDefinitionTestContent
{
    internal static IReadOnlyDictionary<StringName, BarrierProfileDefinition> LoadValidated()
        => GameSessionTestFactory.GetProcessSnapshot().BarrierProfiles;
}
