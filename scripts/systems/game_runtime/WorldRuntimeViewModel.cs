using System;
using System.Collections.Generic;
using Godot;

internal sealed class WorldRuntimeViewModel
{
    internal static readonly WorldRuntimeViewModel Empty = new();

    internal string StatusText { get; init; } = "";
    internal RuntimeModalKind ActiveModalKind { get; init; } = RuntimeModalKind.None;
    internal string ActiveModalId { get; init; } = "";
    internal Vector2I PlayerCoord { get; init; } = Vector2I.Zero;
    internal Vector2I SelectedCoord { get; init; } = Vector2I.Zero;
    internal bool PlayerVisible { get; init; } = true;
    internal string PlayerFactionId { get; init; } = "player";
    internal string ActiveMapId { get; init; } = "";
    internal string ActiveMapDisplayName { get; init; } = "";
    internal string SubmapReturnHintText { get; init; } = "";
    internal IReadOnlyList<WorldEncounterViewModel> NearbyEncounters { get; init; } =
        Array.Empty<WorldEncounterViewModel>();
    internal IReadOnlyList<WorldEventViewModel> NearbyWorldEvents { get; init; } =
        Array.Empty<WorldEventViewModel>();
}

internal readonly record struct WorldEncounterViewModel(
    string EntityId,
    string DisplayName,
    Vector2I Coord,
    int Distance,
    string EncounterKind,
    int GrowthStage
);

internal readonly record struct WorldEventViewModel(
    string EventId,
    string DisplayName,
    Vector2I Coord,
    int Distance,
    string EventType,
    string TargetSubmapId
);
