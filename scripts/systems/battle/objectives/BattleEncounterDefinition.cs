using System;
using Godot;

internal enum BattleObjectiveMode
{
    Unknown = 0,
    Elimination,
    Boss,
    Rescue,
    Escape,
    Escort,
    Defense,
    Intercept,
    NodeOperation,
    Control,
}

public enum BattleWorldResolutionMode
{
    Preserve = 0,
    Clear,
    Suppress,
}

internal abstract class BattleObjectiveDefinition
{
    protected BattleObjectiveDefinition(BattleObjectiveMode mode)
    {
        if (mode == BattleObjectiveMode.Unknown)
            throw new ArgumentOutOfRangeException(nameof(mode));
        Mode = mode;
    }

    internal BattleObjectiveMode Mode { get; }
}

internal sealed class BattleEliminationObjectiveDefinition : BattleObjectiveDefinition
{
    private BattleEliminationObjectiveDefinition()
        : base(BattleObjectiveMode.Elimination) { }

    internal static BattleEliminationObjectiveDefinition Instance { get; } = new();
}

internal sealed class BattleEncounterWorldResolutionDefinition
{
    internal BattleEncounterWorldResolutionDefinition(
        BattleWorldResolutionMode playerSuccessMode,
        BattleWorldResolutionMode playerFailureMode,
        BattleWorldResolutionMode drawMode,
        int suppressionSteps
    )
    {
        if (!Enum.IsDefined(playerSuccessMode))
            throw new ArgumentOutOfRangeException(nameof(playerSuccessMode));
        if (!Enum.IsDefined(playerFailureMode))
            throw new ArgumentOutOfRangeException(nameof(playerFailureMode));
        if (!Enum.IsDefined(drawMode))
            throw new ArgumentOutOfRangeException(nameof(drawMode));
        if (suppressionSteps < 0)
            throw new ArgumentOutOfRangeException(nameof(suppressionSteps));
        bool usesSuppression =
            playerSuccessMode == BattleWorldResolutionMode.Suppress
            || playerFailureMode == BattleWorldResolutionMode.Suppress
            || drawMode == BattleWorldResolutionMode.Suppress;
        if (usesSuppression != (suppressionSteps > 0))
        {
            throw new ArgumentException(
                "Battle encounter suppression steps must be positive exactly when a resolution mode suppresses the encounter."
            );
        }
        PlayerSuccessMode = playerSuccessMode;
        PlayerFailureMode = playerFailureMode;
        DrawMode = drawMode;
        SuppressionSteps = suppressionSteps;
    }

    internal BattleWorldResolutionMode PlayerSuccessMode { get; }
    internal BattleWorldResolutionMode PlayerFailureMode { get; }
    internal BattleWorldResolutionMode DrawMode { get; }
    internal int SuppressionSteps { get; }
}

internal sealed class BattleEncounterDefinition
{
    internal BattleEncounterDefinition(
        StringName encounterId,
        string displayName,
        StringName rosterProfileId,
        BattleObjectiveDefinition objective,
        BattleEncounterWorldResolutionDefinition worldResolution
    )
    {
        if (encounterId == "")
            throw new ArgumentException("Battle encounter id must not be empty.", nameof(encounterId));
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Battle encounter display name must not be empty.", nameof(displayName));
        if (rosterProfileId == "")
            throw new ArgumentException("Battle encounter roster profile id must not be empty.", nameof(rosterProfileId));
        EncounterId = encounterId;
        DisplayName = displayName ?? "";
        RosterProfileId = rosterProfileId;
        Objective = objective ?? throw new ArgumentNullException(nameof(objective));
        WorldResolution =
            worldResolution ?? throw new ArgumentNullException(nameof(worldResolution));
    }

    internal StringName EncounterId { get; }
    internal string DisplayName { get; }
    internal StringName RosterProfileId { get; }
    internal BattleObjectiveDefinition Objective { get; }
    internal BattleEncounterWorldResolutionDefinition WorldResolution { get; }
}
