using System;
using System.Collections.Generic;
using Godot;

internal static class BarrierDefinitionTestContent
{
    internal static IReadOnlyDictionary<StringName, BarrierProfileDefinition> LoadValidated()
    {
        using var registry = new BarrierContentRegistry(new TestContentResourceLoader());
        IReadOnlyList<string> errors = registry.ValidateTyped();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Barrier test content failed validation: {string.Join(" | ", errors)}"
            );
        }
        return registry.GetProfileDefsTyped();
    }
}
