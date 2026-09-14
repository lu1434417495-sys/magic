using System;
using System.Collections.Generic;
using Godot;

internal sealed class BattleFatalInterceptContext
{
    internal BattleUnitState SourceUnit { get; init; }
    internal BattleUnitState TargetUnit { get; init; }
    internal BattleState BattleState { get; init; }
    internal DeathResolutionContext DeathContext { get; init; } =
        BattleDeathResolutionRules.NormalFatalContext();
    internal int HpBefore { get; init; }
    internal int HpDamage { get; init; }
    internal int ProjectedHp { get; init; }
    internal int WorldStep { get; init; } = -1;
    internal BattleEventBatch Batch { get; init; }
    internal BattleSaveContext SaveContext { get; init; }
    internal bool IsDetachedPreview { get; init; }
    internal int DetachedPreviewDepth { get; init; }
}

internal enum BattleFatalInterceptAttemptOutcomeKind
{
    Unavailable,
    BlockedByPriority,
    RollFailed,
    Intercepted,
    RecoveryFailed,
}

internal sealed class BattleFatalInterceptAttemptResult
{
    internal StringName SourceEquipmentInstanceId { get; init; } = "";
    internal StringName BindingId { get; init; } = "";
    internal StringName InterceptId { get; init; } = "";
    internal int ResolutionOrder { get; init; }
    internal int ProtectionPriority { get; init; }
    internal BattleFatalInterceptAttemptOutcomeKind Outcome { get; init; }
    internal bool UsageConsumed { get; init; }
    internal int RolledValue { get; init; }
    internal int RecoveredHp { get; init; }
}

internal sealed class BattleFatalInterceptResult
{
    internal static readonly BattleFatalInterceptResult None = new();

    internal bool Intercepted { get; init; }
    internal StringName WinningBindingId { get; init; } = "";
    internal StringName WinningInterceptId { get; init; } = "";
    internal int RecoveredHp { get; init; }
    internal IReadOnlyList<BattleFatalInterceptAttemptResult> Attempts { get; init; } =
        Array.Empty<BattleFatalInterceptAttemptResult>();
}

internal sealed class BattleFatalInterceptCandidatePreview
{
    internal StringName SourceEquipmentInstanceId { get; init; } = "";
    internal StringName BindingId { get; init; } = "";
    internal StringName InterceptId { get; init; } = "";
    internal int ResolutionOrder { get; init; }
    internal int ProtectionPriority { get; init; }
    internal bool Available { get; init; }
    internal bool BlocksDeathSource { get; init; }
    internal int ReachProbabilityBasisPoints { get; init; }
    internal int SuccessProbabilityBasisPoints { get; init; }
    internal int ContributionProbabilityBasisPoints { get; init; }
    internal int ExpectedRecoveryHp { get; init; }
    internal IReadOnlyList<BattleEquipmentAbilityActionPreviewResult> SuccessActionPreviews
    {
        get;
        init;
    } = Array.Empty<BattleEquipmentAbilityActionPreviewResult>();
}

internal sealed class BattleFatalInterceptPreviewBranch
{
    internal int ProbabilityBasisPoints { get; init; }
    internal BattleState BattleState { get; init; }
    internal BattleUnitState SourceUnit { get; init; }
    internal BattleUnitState TargetUnit { get; init; }
    internal bool Intercepted { get; init; }
    internal StringName WinningBindingId { get; init; } = "";
    internal StringName WinningInterceptId { get; init; } = "";
}

public sealed class BattleFatalInterceptPreviewResult
{
    internal static readonly BattleFatalInterceptPreviewResult None = new();

    internal int InterceptProbabilityBasisPoints { get; init; }
    internal bool GuaranteedIntercept { get; init; }
    internal int ExpectedRecoveryHp { get; init; }
    internal int ExpectedSurvivalHp { get; init; }
    internal IReadOnlyList<BattleEquipmentAbilityActionPreviewResult> SuccessActionPreviews
    {
        get;
        init;
    } = Array.Empty<BattleEquipmentAbilityActionPreviewResult>();
    internal IReadOnlyList<BattleFatalInterceptCandidatePreview> Candidates { get; init; } =
        Array.Empty<BattleFatalInterceptCandidatePreview>();
    internal IReadOnlyList<BattleFatalInterceptPreviewBranch> ContinuationBranches
    {
        get;
        init;
    } = Array.Empty<BattleFatalInterceptPreviewBranch>();
}

internal interface IBattleFatalInterceptArbiter
{
    BattleFatalInterceptResult Resolve(BattleFatalInterceptContext context);

    BattleFatalInterceptPreviewResult Preview(BattleFatalInterceptContext context);
}
