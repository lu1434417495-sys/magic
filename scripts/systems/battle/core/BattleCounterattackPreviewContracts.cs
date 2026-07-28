using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Godot;

internal enum BattleCounterattackRiskCoverage
{
    NotEvaluated = 0,
    Complete,
    RandomTargetSelectionUnknown,
    ProducerSequenceUnsupported,
    OutcomeChanceUnsupported,
}

internal static class BattleCounterattackRiskCoverageNames
{
    internal static StringName ToStringName(
        BattleCounterattackRiskCoverage value
    ) => value switch
    {
        BattleCounterattackRiskCoverage.NotEvaluated =>
            new StringName("not_evaluated"),
        BattleCounterattackRiskCoverage.Complete =>
            new StringName("complete"),
        BattleCounterattackRiskCoverage
            .RandomTargetSelectionUnknown =>
            new StringName(
                "random_target_selection_unknown"
            ),
        BattleCounterattackRiskCoverage
            .ProducerSequenceUnsupported =>
            new StringName("producer_sequence_unsupported"),
        BattleCounterattackRiskCoverage
            .OutcomeChanceUnsupported =>
            new StringName("outcome_chance_unsupported"),
        _ => throw new ArgumentOutOfRangeException(
            nameof(value),
            value,
            null
        ),
    };
}

internal readonly record struct
    BattleCounterattackDefinitionDamageRange(
        bool HasDamage,
        int MinDamage,
        int MaxDamage
    )
{
    internal static BattleCounterattackDefinitionDamageRange Empty =>
        new(false, 0, 0);

    internal static BattleCounterattackDefinitionDamageRange From(
        in BattleDamagePreviewRangeService.SkillDamagePreview preview
    ) => preview.HasDamage
        ? new(true, preview.MinDamage, preview.MaxDamage)
        : Empty;
}

internal readonly record struct BattleCounterattackRiskBranch(
    BattleCounterattackTriggerKind TriggerKind,
    StringName CapabilityInstanceId,
    int CapabilityChancePercent,
    BattleCounterattackEligibility Eligibility,
    int StaminaCost,
    BattleCounterattackDefinitionDamageRange DefinitionDamageRange
);

internal readonly record struct BattleCounterattackRiskEntry(
    StringName DefenderUnitId,
    BattleCounterattackRiskBranch? OnHit,
    BattleCounterattackRiskBranch? OnMiss,
    int PotentialCounterattackChanceBasisPoints
)
{
    internal BattleCounterattackDefinitionDamageRange
        ExecutableDefinitionDamageEnvelope =>
            MergeExecutableDamage(OnHit, OnMiss);

    private static BattleCounterattackDefinitionDamageRange
        MergeExecutableDamage(
            BattleCounterattackRiskBranch? left,
            BattleCounterattackRiskBranch? right
        )
    {
        bool leftIncluded =
            left.HasValue
            && left.Value.Eligibility.IsAllowed
            && left.Value.DefinitionDamageRange.HasDamage;
        bool rightIncluded =
            right.HasValue
            && right.Value.Eligibility.IsAllowed
            && right.Value.DefinitionDamageRange.HasDamage;
        if (!leftIncluded && !rightIncluded)
            return BattleCounterattackDefinitionDamageRange.Empty;
        if (!leftIncluded)
            return right.Value.DefinitionDamageRange;
        if (!rightIncluded)
            return left.Value.DefinitionDamageRange;
        return new BattleCounterattackDefinitionDamageRange(
            true,
            Math.Min(
                left.Value.DefinitionDamageRange.MinDamage,
                right.Value.DefinitionDamageRange.MinDamage
            ),
            Math.Max(
                left.Value.DefinitionDamageRange.MaxDamage,
                right.Value.DefinitionDamageRange.MaxDamage
            )
        );
    }
}

internal sealed class BattleCounterattackPreviewTarget
{
    private readonly ReadOnlyCollection<int>
        _stageHitChanceBasisPoints;

    internal BattleCounterattackPreviewTarget(
        StringName defenderUnitId,
        bool producesAttackResolutionFact,
        bool includesWeaponDamage,
        BattleAttackDeliveryKind deliveryKind,
        IReadOnlyList<int> stageHitChanceBasisPoints
    )
    {
        if (defenderUnitId == new StringName(""))
        {
            throw new ArgumentException(
                "counterattack preview defender is required",
                nameof(defenderUnitId)
            );
        }
        int[] copied =
            (stageHitChanceBasisPoints ?? Array.Empty<int>())
                .ToArray();
        if (
            producesAttackResolutionFact
            && includesWeaponDamage
            && deliveryKind
                == BattleAttackDeliveryKind.MeleeWeapon
            && copied.Length == 0
        )
        {
            throw new ArgumentException(
                "melee attack preview requires an outcome chance profile",
                nameof(stageHitChanceBasisPoints)
            );
        }
        if (copied.Any(value => value < 0 || value > 10_000))
        {
            throw new ArgumentOutOfRangeException(
                nameof(stageHitChanceBasisPoints)
            );
        }
        DefenderUnitId = defenderUnitId;
        ProducesAttackResolutionFact =
            producesAttackResolutionFact;
        IncludesWeaponDamage = includesWeaponDamage;
        DeliveryKind = deliveryKind;
        _stageHitChanceBasisPoints =
            Array.AsReadOnly(copied);
    }

    internal StringName DefenderUnitId { get; }
    internal bool ProducesAttackResolutionFact { get; }
    internal bool IncludesWeaponDamage { get; }
    internal BattleAttackDeliveryKind DeliveryKind { get; }
    internal IReadOnlyList<int> StageHitChanceBasisPoints =>
        _stageHitChanceBasisPoints;
}

internal sealed class BattleCounterattackRiskProjection
{
    private readonly ReadOnlyCollection<
        BattleCounterattackRiskEntry
    > _entries;

    internal BattleCounterattackRiskProjection(
        BattleCounterattackRiskCoverage coverage,
        IReadOnlyList<BattleCounterattackRiskEntry> entries,
        long potentialExpectedCountBasisPoints
    )
    {
        if (!Enum.IsDefined(coverage))
            throw new ArgumentOutOfRangeException(nameof(coverage));
        if (potentialExpectedCountBasisPoints < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(potentialExpectedCountBasisPoints)
            );
        }
        BattleCounterattackRiskEntry[] copied =
            (entries ?? Array.Empty<BattleCounterattackRiskEntry>())
                .ToArray();
        if (
            coverage != BattleCounterattackRiskCoverage.Complete
            && (
                copied.Length != 0
                || potentialExpectedCountBasisPoints != 0
            )
        )
        {
            throw new ArgumentException(
                "incomplete risk coverage cannot carry numeric entries"
            );
        }
        long computedExpectedCountBasisPoints = 0;
        foreach (BattleCounterattackRiskEntry entry in copied)
        {
            if (
                entry.DefenderUnitId == new StringName("")
                || (!entry.OnHit.HasValue && !entry.OnMiss.HasValue)
                || (
                    entry.OnHit.HasValue
                    && (
                        entry.OnHit.Value.TriggerKind
                            != BattleCounterattackTriggerKind
                                .MeleeHitReceived
                        || entry.OnHit.Value.CapabilityInstanceId
                            == new StringName("")
                        || entry.OnHit.Value.CapabilityChancePercent < 0
                        || entry.OnHit.Value.CapabilityChancePercent > 100
                    )
                )
                || (
                    entry.OnMiss.HasValue
                    && (
                        entry.OnMiss.Value.TriggerKind
                            != BattleCounterattackTriggerKind
                                .MeleeAttackEvaded
                        || entry.OnMiss.Value.CapabilityInstanceId
                            == new StringName("")
                        || entry.OnMiss.Value.CapabilityChancePercent < 0
                        || entry.OnMiss.Value.CapabilityChancePercent > 100
                    )
                )
                || entry.PotentialCounterattackChanceBasisPoints < 0
                || entry.PotentialCounterattackChanceBasisPoints
                    > 10_000
            )
            {
                throw new ArgumentException(
                    "counterattack risk entry is invalid",
                    nameof(entries)
                );
            }
            computedExpectedCountBasisPoints = checked(
                computedExpectedCountBasisPoints
                + entry.PotentialCounterattackChanceBasisPoints
            );
        }
        if (
            computedExpectedCountBasisPoints
            != potentialExpectedCountBasisPoints
        )
        {
            throw new ArgumentException(
                "counterattack risk aggregate does not match entries",
                nameof(potentialExpectedCountBasisPoints)
            );
        }
        Coverage = coverage;
        _entries = Array.AsReadOnly(copied);
        PotentialExpectedCountBasisPoints =
            potentialExpectedCountBasisPoints;
    }

    internal BattleCounterattackRiskCoverage Coverage { get; }
    internal IReadOnlyList<BattleCounterattackRiskEntry> Entries =>
        _entries;
    internal long PotentialExpectedCountBasisPoints { get; }

    internal static BattleCounterattackRiskProjection Empty(
        BattleCounterattackRiskCoverage coverage
    ) => new(
        coverage,
        Array.Empty<BattleCounterattackRiskEntry>(),
        0
    );
}
