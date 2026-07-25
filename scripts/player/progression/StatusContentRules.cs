using System;
using System.Collections.Generic;
using Godot;

internal static class StatusContentRules
{
    internal static readonly StringName KnockdownImmunity = "knockdown_immunity";

    private static readonly IReadOnlyList<StringName> SystemDeclaredStatusIds =
        Array.AsReadOnly(new[] { KnockdownImmunity });

    internal static IReadOnlyList<StringName> SystemDeclaredStatusIdsTyped() =>
        SystemDeclaredStatusIds;
}
