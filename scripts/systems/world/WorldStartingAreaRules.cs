using System;
using Godot;

// Uses the saved, actual spawn coordinate, never the player's current position.
internal readonly struct WorldStartingAreaRules
{
    private readonly WorldStartingAreaDefinition _definition;
    private readonly EncounterChallengeCatalog _challenges;
    internal Rect2I Bounds { get; }
    internal bool Enabled => _definition?.Enabled ?? false;

    internal WorldStartingAreaRules(
        WorldStartingAreaDefinition definition,
        Vector2I spawnCoord,
        EncounterChallengeCatalog challenges
    )
    {
        _definition = definition ?? WorldStartingAreaDefinition.Disabled;
        _challenges = challenges;
        if (_definition.Enabled && challenges == null)
            throw new ArgumentNullException(nameof(challenges));
        // Half-open cell bounds give exactly Size.X * Size.Y cells, including corners.
        Bounds = new Rect2I(spawnCoord - _definition.Size / 2, _definition.Size);
    }

    internal bool Contains(Vector2I coord) => Enabled && Bounds.HasPoint(coord);

    internal bool Allows(Vector2I coord, StringName encounterProfileId, int stage) =>
        !Contains(coord)
        || _challenges.GetMaxChallengeRating(encounterProfileId, stage) <= _definition.MaxChallengeRating;

    internal int LimitGrowthStage(Vector2I coord, StringName encounterProfileId, int requestedStage)
    {
        if (!Contains(coord))
            return requestedStage;
        return _challenges.GetHighestAllowedStage(
            encounterProfileId, requestedStage, _definition.MaxChallengeRating
        );
    }
}
