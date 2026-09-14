using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class BattleDamageResolver
{
    private readonly record struct DamagePreviewSaveEstimate(
        bool HasSave,
        int DamageBeforeSave,
        int DamageAfterSave,
        int DamageAfterSaveEstimate,
        int DamageAfterSaveWorst,
        int DamageOnSaveFailure,
        int DamageOnSaveSuccess,
        bool SavePartialOnSuccess,
        int SaveSuccessProbabilityBasisPoints,
        int SaveSuccessRatePercent,
        int SaveFailureProbabilityBasisPoints,
        int Dc,
        string Ability,
        string SaveTag,
        string AdvantageState,
        int AbilityValue,
        int AbilityModifier,
        int Bonus,
        bool Immune,
        IReadOnlyList<BattleSaveSource> Sources,
        IReadOnlyList<BattleWeightedStatusOutcomePreviewData> SaveFailureStatusOutcomes
    )
    {
        public static DamagePreviewSaveEstimate None(int damageBeforeSave)
        {
            return new DamagePreviewSaveEstimate(
                false,
                damageBeforeSave,
                damageBeforeSave,
                damageBeforeSave,
                damageBeforeSave,
                damageBeforeSave,
                damageBeforeSave,
                false,
                0,
                0,
                10000,
                0,
                "",
                "",
                "",
                0,
                0,
                0,
                false,
                Array.Empty<BattleSaveSource>(),
                Array.Empty<BattleWeightedStatusOutcomePreviewData>()
            );
        }

        public BattleDamagePreviewSaveEstimate ToPreviewSaveEstimate()
        {
            return BattleDamagePreviewSaveEstimate.Create(
                HasSave,
                DamageBeforeSave,
                DamageAfterSave,
                DamageAfterSaveEstimate,
                DamageAfterSaveWorst,
                DamageOnSaveFailure,
                DamageOnSaveSuccess,
                SavePartialOnSuccess,
                SaveSuccessProbabilityBasisPoints,
                SaveSuccessRatePercent,
                SaveFailureProbabilityBasisPoints,
                Dc,
                Ability,
                SaveTag,
                AdvantageState,
                AbilityValue,
                AbilityModifier,
                Bonus,
                Immune,
                Sources,
                SaveFailureStatusOutcomes
            );
        }
    }

    private readonly record struct DamagePreviewCoreResult(
        BattleUnitState SourcePreview,
        BattleUnitState TargetPreview,
        BattleDamagePreviewRollMode RollMode,
        BattleDamagePreviewSaveMode SaveMode,
        int PreSaveDamage,
        int ShieldHpBefore,
        int ShieldHpAfter,
        DamageOutcomeResult DamageOutcome,
        DamagePreviewSaveEstimate SaveEstimate,
        AppliedDamageResult DamageResult,
        bool StableLethal,
        int LethalProbabilityBasisPoints,
        int FatalInterceptProbabilityBasisPoints,
        int ExpectedSurvivalHp,
        bool InvalidDamageTag
    );

    private readonly record struct DamagePreviewBranchLethalEstimate(
        bool FailureKills,
        bool SuccessKills,
        int FailureHpDamage,
        int SuccessHpDamage,
        bool StableLethal,
        int LethalProbabilityBasisPoints,
        int FatalInterceptProbabilityBasisPoints,
        int ExpectedSurvivalHp,
        BattleFatalInterceptPreviewResult FatalInterceptPreview,
        IReadOnlyList<BattleEquipmentAbilityActionPreviewResult> EquipmentActionPreviews
    );

    // Performance contract: full presentation previews and compact AI score previews
    // must share this exact resolver core. Duplicating a cheaper AI damage formula here
    // would trade the allocation win for stale save/shield/resistance scoring.
    private DamagePreviewCoreResult ResolveDamagePreviewCore(
        BattleDamagePreviewWorkingSet workingSet,
        CombatEffectDefinition effectDefinition,
        DamageResolutionContext damageContext,
        BattleDamagePreviewRollMode rollMode,
        BattleDamagePreviewSaveMode saveMode
    )
    {
        if (workingSet?.HasContinuationState == true)
        {
            return ResolveDamagePreviewCoreAcrossContinuationBranches(
                workingSet,
                effectDefinition,
                damageContext,
                rollMode,
                saveMode
            );
        }
        return ResolveDamagePreviewCoreSinglePath(
            workingSet,
            effectDefinition,
            damageContext,
            rollMode,
            saveMode
        );
    }

    private DamagePreviewCoreResult ResolveDamagePreviewCoreSinglePath(
        BattleDamagePreviewWorkingSet workingSet,
        CombatEffectDefinition effectDefinition,
        DamageResolutionContext damageContext,
        BattleDamagePreviewRollMode rollMode,
        BattleDamagePreviewSaveMode saveMode
    )
    {
        BattleUnitState sourcePreview = workingSet.SourcePreview;
        BattleUnitState targetPreview = workingSet.TargetPreview;
        BattleDamagePreviewRollMode resolvedRollMode =
            rollMode == BattleDamagePreviewRollMode.Unknown
                ? BattleDamagePreviewRollMode.Average
                : rollMode;
        BattleDamagePreviewSaveMode resolvedSaveMode =
            saveMode == BattleDamagePreviewSaveMode.Unknown
                ? BattleDamagePreviewSaveMode.Expected
                : saveMode;
        int shieldHpBefore = targetPreview.GetShieldStateTyped().CurrentHp;
        DamageResolutionContext previewContextFlags =
            (damageContext ?? DamageResolutionContext.Empty())
                .WithBattleState(workingSet.BattleState)
                .WithDamageRollMode(ToStringName(resolvedRollMode));
        DamageOutcomeResult damageOutcome = ResolveDamageOutcome(
            sourcePreview,
            targetPreview,
            effectDefinition,
            previewContextFlags
        );
        if (damageOutcome.InvalidDamageTag)
        {
            return new DamagePreviewCoreResult(
                sourcePreview,
                targetPreview,
                resolvedRollMode,
                resolvedSaveMode,
                0,
                shieldHpBefore,
                targetPreview.GetShieldStateTyped().CurrentHp,
                damageOutcome,
                DamagePreviewSaveEstimate.None(0),
                default,
                false,
                0,
                0,
                0,
                true
            );
        }

        int preSaveDamage = damageOutcome.ResolvedDamage;
        DamagePreviewSaveEstimate saveEstimate = BuildDamagePreviewSaveEstimate(
            sourcePreview,
            targetPreview,
            effectDefinition,
            previewContextFlags,
            preSaveDamage,
            resolvedSaveMode
        );
        damageOutcome = WithDamagePreviewSaveEstimate(damageOutcome, saveEstimate);
        DamagePreviewBranchLethalEstimate branchLethalEstimate =
            saveEstimate.HasSave
                ? BuildSaveBranchLethalEstimate(
                    targetPreview,
                    damageOutcome,
                    saveEstimate,
                    sourcePreview,
                    previewContextFlags
                )
                : default;
        DamageApplicationInput expectedDamageInput =
            damageOutcome.ToDamageApplicationInput(suppressDamageApplicationHook: true);
        if (
            saveEstimate.HasSave
            && saveEstimate.SaveFailureProbabilityBasisPoints > 0
            && saveEstimate.SaveSuccessProbabilityBasisPoints > 0
            && branchLethalEstimate.FatalInterceptPreview != null
            && !branchLethalEstimate.FatalInterceptPreview.GuaranteedIntercept
        )
        {
            expectedDamageInput = WithConditionalDeathPreventionSuppressed(
                expectedDamageInput
            );
        }
        AppliedDamageResult damageResult = ApplyDamageToTargetResult(
            targetPreview,
            expectedDamageInput,
            sourcePreview,
            previewContextFlags.WithDetachedPreviewMode()
        );
        if (saveEstimate.HasSave)
        {
            damageResult = damageResult.WithFatalInterceptPreview(
                branchLethalEstimate.FatalInterceptPreview
            )
                .WithEquipmentActionPreviews(
                    branchLethalEstimate.EquipmentActionPreviews
                );
        }
        if ((damageResult.FatalInterceptPreview?.ContinuationBranches?.Count ?? 0) > 0)
        {
            workingSet.ReplaceContinuationBranches(
                damageResult.FatalInterceptPreview.ContinuationBranches
            );
        }
        return new DamagePreviewCoreResult(
            sourcePreview,
            targetPreview,
            resolvedRollMode,
            resolvedSaveMode,
            preSaveDamage,
            shieldHpBefore,
            targetPreview.GetShieldStateTyped().CurrentHp,
            damageOutcome,
            saveEstimate,
            damageResult,
            saveEstimate.HasSave
                ? branchLethalEstimate.StableLethal
                : ResolveDamagePreviewLethalProbabilityBasisPoints(
                    targetPreview,
                    damageResult
                ) >= 10000,
            saveEstimate.HasSave
                ? branchLethalEstimate.LethalProbabilityBasisPoints
                : ResolveDamagePreviewLethalProbabilityBasisPoints(
                    targetPreview,
                    damageResult
                ),
            saveEstimate.HasSave
                ? branchLethalEstimate.FatalInterceptProbabilityBasisPoints
                : Math.Clamp(
                    damageResult.FatalInterceptPreview
                        ?.InterceptProbabilityBasisPoints ?? 0,
                    0,
                    10000
                ),
            saveEstimate.HasSave
                ? branchLethalEstimate.ExpectedSurvivalHp
                : Math.Max(
                    damageResult.FatalInterceptPreview?.ExpectedSurvivalHp ?? 0,
                    0
                ),
            false
        );
    }

    private DamagePreviewCoreResult ResolveDamagePreviewCoreAcrossContinuationBranches(
        BattleDamagePreviewWorkingSet workingSet,
        CombatEffectDefinition effectDefinition,
        DamageResolutionContext damageContext,
        BattleDamagePreviewRollMode rollMode,
        BattleDamagePreviewSaveMode saveMode
    )
    {
        var weightedCores = new List<(DamagePreviewCoreResult Core, int Probability)>();
        var nextBranches = new List<BattleFatalInterceptPreviewBranch>();
        foreach (BattleFatalInterceptPreviewBranch inputBranch in workingSet.ContinuationBranches)
        {
            int inputProbability = Math.Clamp(
                inputBranch?.ProbabilityBasisPoints ?? 0,
                0,
                10000
            );
            if (
                inputProbability <= 0
                || inputBranch?.BattleState == null
                || inputBranch.TargetUnit == null
            )
            {
                continue;
            }
            BattleDamagePreviewWorkingSet branchWorkingSet =
                BattleDamagePreviewWorkingSet.FromDetachedState(
                    inputBranch.SourceUnit,
                    inputBranch.TargetUnit,
                    inputBranch.BattleState
                );
            if (branchWorkingSet == null)
                continue;
            DamagePreviewCoreResult branchCore = ResolveDamagePreviewCoreSinglePath(
                branchWorkingSet,
                effectDefinition,
                (damageContext ?? DamageResolutionContext.Empty())
                    .WithBattleState(branchWorkingSet.BattleState),
                rollMode,
                saveMode
            );
            weightedCores.Add((branchCore, inputProbability));
            if (branchWorkingSet.HasContinuationState)
            {
                AppendScaledContinuationBranches(
                    nextBranches,
                    branchWorkingSet.ContinuationBranches,
                    inputProbability
                );
            }
            else
            {
                nextBranches.Add(
                    new BattleFatalInterceptPreviewBranch
                    {
                        ProbabilityBasisPoints = inputProbability,
                        BattleState = branchWorkingSet.BattleState,
                        SourceUnit = branchWorkingSet.SourcePreview,
                        TargetUnit = branchWorkingSet.TargetPreview,
                        Intercepted = false,
                    }
                );
            }
        }

        if (weightedCores.Count == 0)
        {
            return ResolveDamagePreviewCoreSinglePath(
                workingSet,
                effectDefinition,
                damageContext,
                rollMode,
                saveMode
            );
        }

        workingSet.ReplaceContinuationBranches(nextBranches);
        DamagePreviewCoreResult template = weightedCores[0].Core;
        int preSaveDamage = WeightedCoreValue(weightedCores, core => core.PreSaveDamage);
        int shieldHpBefore = WeightedCoreValue(weightedCores, core => core.ShieldHpBefore);
        int shieldHpAfter = WeightedCoreValue(weightedCores, core => core.ShieldHpAfter);
        int hpDamage = WeightedCoreValue(
            weightedCores,
            core => core.DamageResult.HpDamage
        );
        int damage = WeightedCoreValue(
            weightedCores,
            core => core.DamageResult.Damage
        );
        int shieldAbsorbed = WeightedCoreValue(
            weightedCores,
            core => core.DamageResult.ShieldAbsorbed
        );
        int lethalProbability = WeightedCoreValue(
            weightedCores,
            core => core.LethalProbabilityBasisPoints
        );
        int fatalProbability = WeightedCoreValue(
            weightedCores,
            core => core.FatalInterceptProbabilityBasisPoints
        );
        int expectedSurvivalHp = WeightedCoreValue(
            weightedCores,
            core => core.ExpectedSurvivalHp
        );
        BattleFatalInterceptPreviewResult fatalPreview =
            CombineWeightedFatalInterceptPreviews(
                weightedCores,
                nextBranches
            );
        IReadOnlyList<BattleEquipmentAbilityActionPreviewResult> actionPreviews =
            CombineWeightedCoreEquipmentActions(weightedCores);
        DamageEventResult aggregateEvent = template.DamageResult.Event;
        aggregateEvent.Damage = hpDamage;
        aggregateEvent.HpDamage = hpDamage;
        aggregateEvent.ShieldAbsorbed = shieldAbsorbed;
        aggregateEvent.ShieldBroken = weightedCores.Exists(
            entry => entry.Probability > 0 && entry.Core.DamageResult.ShieldBroken
        );
        aggregateEvent.FullyAbsorbedByShield = hpDamage <= 0 && shieldAbsorbed > 0;
        AppliedDamageResult aggregateDamageResult = new(
            aggregateEvent,
            damage,
            hpDamage,
            shieldAbsorbed,
            aggregateEvent.ShieldBroken,
            weightedCores.Exists(
                entry =>
                    entry.Probability > 0
                    && entry.Core.DamageResult.LowLuckBlackStarWedgeTriggered
            ),
            template.DamageResult.DamageDiceEvent,
            fatalPreview,
            actionPreviews
        );
        DamageOutcomeResult aggregateOutcome = template.DamageOutcome.WithResolvedDamage(
            WeightedCoreValue(
                weightedCores,
                core => core.DamageOutcome.ResolvedDamage
            )
        );
        return new DamagePreviewCoreResult(
            workingSet.SourcePreview,
            workingSet.TargetPreview,
            template.RollMode,
            template.SaveMode,
            preSaveDamage,
            shieldHpBefore,
            shieldHpAfter,
            aggregateOutcome,
            template.SaveEstimate,
            aggregateDamageResult,
            lethalProbability >= 10000,
            Math.Clamp(lethalProbability, 0, 10000),
            Math.Clamp(fatalProbability, 0, 10000),
            Math.Max(expectedSurvivalHp, 0),
            weightedCores.TrueForAll(entry => entry.Core.InvalidDamageTag)
        );
    }

    private static int WeightedCoreValue(
        IReadOnlyList<(DamagePreviewCoreResult Core, int Probability)> weightedCores,
        Func<DamagePreviewCoreResult, int> selector
    )
    {
        long weighted = 0;
        foreach ((DamagePreviewCoreResult core, int probability) in weightedCores)
        {
            weighted += (long)Math.Max(selector(core), 0)
                * Math.Clamp(probability, 0, 10000);
        }
        return (int)Math.Clamp(
            Math.Round(weighted / 10000.0),
            0.0,
            int.MaxValue
        );
    }

    private static void AppendScaledContinuationBranches(
        List<BattleFatalInterceptPreviewBranch> target,
        IReadOnlyList<BattleFatalInterceptPreviewBranch> localBranches,
        int outerProbabilityBasisPoints
    )
    {
        if (target == null || localBranches == null || localBranches.Count == 0)
            return;
        int remaining = Math.Clamp(outerProbabilityBasisPoints, 0, 10000);
        for (int index = 0; index < localBranches.Count; index++)
        {
            BattleFatalInterceptPreviewBranch branch = localBranches[index];
            int probability = index == localBranches.Count - 1
                ? remaining
                : Math.Min(
                    remaining,
                    ScaleBasisPoints(
                        branch?.ProbabilityBasisPoints ?? 0,
                        outerProbabilityBasisPoints
                    )
                );
            remaining = Math.Max(remaining - probability, 0);
            if (branch == null || probability <= 0)
                continue;
            target.Add(
                new BattleFatalInterceptPreviewBranch
                {
                    ProbabilityBasisPoints = probability,
                    BattleState = branch.BattleState,
                    SourceUnit = branch.SourceUnit,
                    TargetUnit = branch.TargetUnit,
                    Intercepted = branch.Intercepted,
                    WinningBindingId = branch.WinningBindingId,
                    WinningInterceptId = branch.WinningInterceptId,
                }
            );
        }
    }

    private static BattleFatalInterceptPreviewResult CombineWeightedFatalInterceptPreviews(
        IReadOnlyList<(DamagePreviewCoreResult Core, int Probability)> weightedCores,
        IReadOnlyList<BattleFatalInterceptPreviewBranch> continuationBranches
    )
    {
        var weightedCandidates = new List<WeightedFatalCandidatePreview>();
        var weightedActions = new List<WeightedEquipmentActionPreview>();
        int interceptProbability = 0;
        int expectedRecoveryHp = 0;
        int expectedSurvivalHp = 0;
        foreach ((DamagePreviewCoreResult core, int probability) in weightedCores)
        {
            BattleFatalInterceptPreviewResult preview =
                core.DamageResult.FatalInterceptPreview;
            if (preview == null)
                continue;
            int outerProbability = Math.Clamp(probability, 0, 10000);
            interceptProbability = Math.Clamp(
                interceptProbability
                    + ScaleBasisPoints(
                        preview.InterceptProbabilityBasisPoints,
                        outerProbability
                    ),
                0,
                10000
            );
            expectedRecoveryHp = Math.Max(
                expectedRecoveryHp
                    + ScaleNonNegativeValue(
                        preview.ExpectedRecoveryHp,
                        outerProbability
                    ),
                0
            );
            expectedSurvivalHp = Math.Max(
                expectedSurvivalHp
                    + ScaleNonNegativeValue(
                        preview.ExpectedSurvivalHp,
                        outerProbability
                    ),
                0
            );
            AppendWeightedCandidates(
                weightedCandidates,
                preview.Candidates,
                outerProbability
            );
            AppendWeightedActions(
                weightedActions,
                preview.SuccessActionPreviews,
                outerProbability
            );
        }
        if (weightedCandidates.Count == 0)
            return null;
        return new BattleFatalInterceptPreviewResult
        {
            InterceptProbabilityBasisPoints = interceptProbability,
            GuaranteedIntercept = interceptProbability >= 10000,
            ExpectedRecoveryHp = expectedRecoveryHp,
            ExpectedSurvivalHp = expectedSurvivalHp,
            Candidates = MergeWeightedFatalCandidates(weightedCandidates),
            SuccessActionPreviews = MergeWeightedActions(weightedActions),
            ContinuationBranches = continuationBranches
                ?? Array.Empty<BattleFatalInterceptPreviewBranch>(),
        };
    }

    private static IReadOnlyList<BattleEquipmentAbilityActionPreviewResult>
        CombineWeightedCoreEquipmentActions(
            IReadOnlyList<(DamagePreviewCoreResult Core, int Probability)> weightedCores
        )
    {
        var weighted = new List<WeightedEquipmentActionPreview>();
        foreach ((DamagePreviewCoreResult core, int probability) in weightedCores)
        {
            AppendWeightedActions(
                weighted,
                core.DamageResult.EquipmentActionPreviews,
                probability
            );
        }
        return MergeWeightedActions(weighted);
    }

    private static DamageApplicationInput WithConditionalDeathPreventionSuppressed(
        DamageApplicationInput input
    )
    {
        DamageEventResult @event = input.Event;
        @event.BypassDeathPrevention = true;
        return input with
        {
            Event = @event,
            BypassDeathPrevention = true,
        };
    }

    private AppliedDamageResult BuildExpectedSaveBranchDamageResult(
        BattleUnitState targetPreview,
        DamageOutcomeResult damageOutcome,
        DamagePreviewSaveEstimate saveEstimate,
        BattleUnitState sourcePreview
    )
    {
        int successBasis = Math.Clamp(saveEstimate.SaveSuccessProbabilityBasisPoints, 0, 10000);
        int failureBasis = Math.Clamp(saveEstimate.SaveFailureProbabilityBasisPoints, 0, 10000);
        int failureDamage = Math.Max(saveEstimate.DamageOnSaveFailure, 0);
        int successDamage = Math.Max(saveEstimate.DamageOnSaveSuccess, 0);

        BattleUnitState failureTarget = targetPreview.clone();
        BattleUnitState successTarget = targetPreview.clone();
        DamageOutcomeResult failureOutcome = damageOutcome.WithResolvedDamage(failureDamage);
        DamageOutcomeResult successOutcome = damageOutcome.WithResolvedDamage(successDamage);
        AppliedDamageResult failureResult = ApplyDamageToTargetResult(
            failureTarget,
            failureOutcome.ToDamageApplicationInput(suppressDamageApplicationHook: true),
            sourcePreview,
            DamageResolutionContext.Empty().WithPreviewMode()
        );
        AppliedDamageResult successResult = ApplyDamageToTargetResult(
            successTarget,
            successOutcome.ToDamageApplicationInput(suppressDamageApplicationHook: true),
            sourcePreview,
            DamageResolutionContext.Empty().WithPreviewMode()
        );

        int expectedHpDamage = RoundToInt(
            (
                failureResult.HpDamage * failureBasis
                + successResult.HpDamage * successBasis
            ) / 10000.0
        );
        int expectedShieldAbsorbed = RoundToInt(
            (
                failureResult.ShieldAbsorbed * failureBasis
                + successResult.ShieldAbsorbed * successBasis
            ) / 10000.0
        );

        DamageEventResult resultEvent = WithDamagePreviewSaveEstimate(
            damageOutcome,
            saveEstimate
        ).Event;
        resultEvent.Damage = expectedHpDamage;
        resultEvent.HpDamage = expectedHpDamage;
        resultEvent.ShieldAbsorbed = expectedShieldAbsorbed;
        resultEvent.ShieldBroken = failureResult.ShieldBroken && failureBasis > 0;
        resultEvent.FullyAbsorbedByShield =
            expectedHpDamage <= 0 && expectedShieldAbsorbed > 0;
        return new AppliedDamageResult(
            resultEvent,
            expectedHpDamage,
            expectedHpDamage,
            expectedShieldAbsorbed,
            failureResult.ShieldBroken && failureBasis > 0,
            failureResult.LowLuckBlackStarWedgeTriggered
                || successResult.LowLuckBlackStarWedgeTriggered,
            damageOutcome.DamageDiceEvent
        );
    }

    private DamagePreviewBranchLethalEstimate BuildSaveBranchLethalEstimate(
        BattleUnitState targetPreview,
        DamageOutcomeResult damageOutcome,
        DamagePreviewSaveEstimate saveEstimate,
        BattleUnitState sourcePreview,
        DamageResolutionContext damageContext
    )
    {
        int failureBasis = Math.Clamp(saveEstimate.SaveFailureProbabilityBasisPoints, 0, 10000);
        int successBasis = Math.Clamp(saveEstimate.SaveSuccessProbabilityBasisPoints, 0, 10000);
        int failureDamage = Math.Max(saveEstimate.DamageOnSaveFailure, 0);
        int successDamage = Math.Max(saveEstimate.DamageOnSaveSuccess, 0);

        BattleDamagePreviewWorkingSet failureWorkingSet =
            BattleDamagePreviewWorkingSet.CreateDetached(
                sourcePreview,
                targetPreview,
                damageContext?.BattleState
            );
        BattleDamagePreviewWorkingSet successWorkingSet =
            BattleDamagePreviewWorkingSet.CreateDetached(
                sourcePreview,
                targetPreview,
                damageContext?.BattleState
            );
        BattleUnitState failureTarget = failureWorkingSet?.TargetPreview;
        BattleUnitState successTarget = successWorkingSet?.TargetPreview;
        DamageOutcomeResult failureOutcome = damageOutcome.WithResolvedDamage(failureDamage);
        DamageOutcomeResult successOutcome = damageOutcome.WithResolvedDamage(successDamage);
        AppliedDamageResult failureResult = ApplyDamageToTargetResult(
            failureTarget,
            failureOutcome.ToDamageApplicationInput(suppressDamageApplicationHook: true),
            failureWorkingSet?.SourcePreview,
            (damageContext ?? DamageResolutionContext.Empty())
                .WithBattleState(failureWorkingSet?.BattleState)
                .WithDetachedPreviewMode()
        );
        AppliedDamageResult successResult = ApplyDamageToTargetResult(
            successTarget,
            successOutcome.ToDamageApplicationInput(suppressDamageApplicationHook: true),
            successWorkingSet?.SourcePreview,
            (damageContext ?? DamageResolutionContext.Empty())
                .WithBattleState(successWorkingSet?.BattleState)
                .WithDetachedPreviewMode()
        );

        int failureLethalBasisPoints =
            ResolveDamagePreviewLethalProbabilityBasisPoints(failureTarget, failureResult);
        int successLethalBasisPoints =
            ResolveDamagePreviewLethalProbabilityBasisPoints(successTarget, successResult);
        int lethalBasisPoints = (int)Math.Clamp(
            Math.Round(
                (
                    failureLethalBasisPoints * (double)failureBasis
                    + successLethalBasisPoints * (double)successBasis
                ) / 10000.0
            ),
            0.0,
            10000.0
        );
        int fatalInterceptBasisPoints = (int)Math.Clamp(
            Math.Round(
                (
                    (failureResult.FatalInterceptPreview
                            ?.InterceptProbabilityBasisPoints ?? 0)
                        * (double)failureBasis
                    + (successResult.FatalInterceptPreview
                            ?.InterceptProbabilityBasisPoints ?? 0)
                        * (double)successBasis
                ) / 10000.0
            ),
            0.0,
            10000.0
        );
        int expectedSurvivalHp = (int)Math.Max(
            Math.Round(
                (
                    (failureResult.FatalInterceptPreview?.ExpectedSurvivalHp ?? 0)
                        * (double)failureBasis
                    + (successResult.FatalInterceptPreview?.ExpectedSurvivalHp ?? 0)
                        * (double)successBasis
                ) / 10000.0
            ),
            0.0
        );
        bool failureKills = failureLethalBasisPoints > 0;
        bool successKills = successLethalBasisPoints > 0;
        BattleFatalInterceptPreviewResult fatalInterceptPreview =
            CombineSaveBranchFatalInterceptPreviews(
                failureResult.FatalInterceptPreview,
                failureBasis,
                successResult.FatalInterceptPreview,
                successBasis,
                BuildSinglePathContinuationBranches(
                    failureWorkingSet,
                    failureResult
                ),
                BuildSinglePathContinuationBranches(
                    successWorkingSet,
                    successResult
                )
            );
        return new DamagePreviewBranchLethalEstimate(
            failureKills,
            successKills,
            failureResult.HpDamage,
            successResult.HpDamage,
            (failureBasis <= 0 || failureLethalBasisPoints >= 10000)
                && (successBasis <= 0 || successLethalBasisPoints >= 10000),
            lethalBasisPoints,
            fatalInterceptBasisPoints,
            expectedSurvivalHp,
            fatalInterceptPreview,
            CombineSaveBranchEquipmentActionPreviews(
                failureResult.EquipmentActionPreviews,
                failureBasis,
                successResult.EquipmentActionPreviews,
                successBasis
            )
        );
    }

    private readonly record struct WeightedFatalCandidatePreview(
        BattleFatalInterceptCandidatePreview Preview,
        int BranchProbabilityBasisPoints
    );

    private readonly record struct WeightedEquipmentActionPreview(
        BattleEquipmentAbilityActionPreviewResult Preview,
        int BranchProbabilityBasisPoints
    );

    private static BattleFatalInterceptPreviewResult
        CombineSaveBranchFatalInterceptPreviews(
            BattleFatalInterceptPreviewResult failurePreview,
            int failureProbabilityBasisPoints,
            BattleFatalInterceptPreviewResult successPreview,
            int successProbabilityBasisPoints,
            IReadOnlyList<BattleFatalInterceptPreviewBranch> failureContinuationBranches,
            IReadOnlyList<BattleFatalInterceptPreviewBranch> successContinuationBranches
        )
    {
        IReadOnlyList<BattleFatalInterceptCandidatePreview> failureCandidates =
            failurePreview?.Candidates
            ?? Array.Empty<BattleFatalInterceptCandidatePreview>();
        IReadOnlyList<BattleFatalInterceptCandidatePreview> successCandidates =
            successPreview?.Candidates
            ?? Array.Empty<BattleFatalInterceptCandidatePreview>();
        if (failureCandidates.Count == 0 && successCandidates.Count == 0)
            return null;

        int normalizedFailureProbability = Math.Clamp(
            failureProbabilityBasisPoints,
            0,
            10000
        );
        int normalizedSuccessProbability = Math.Clamp(
            successProbabilityBasisPoints,
            0,
            10000
        );
        int interceptProbability = Math.Clamp(
            ScaleBasisPoints(
                failurePreview?.InterceptProbabilityBasisPoints ?? 0,
                normalizedFailureProbability
            )
                + ScaleBasisPoints(
                    successPreview?.InterceptProbabilityBasisPoints ?? 0,
                    normalizedSuccessProbability
                ),
            0,
            10000
        );
        int expectedRecoveryHp = Math.Max(
            ScaleNonNegativeValue(
                failurePreview?.ExpectedRecoveryHp ?? 0,
                normalizedFailureProbability
            )
                + ScaleNonNegativeValue(
                    successPreview?.ExpectedRecoveryHp ?? 0,
                    normalizedSuccessProbability
                ),
            0
        );
        int expectedSurvivalHp = Math.Max(
            ScaleNonNegativeValue(
                failurePreview?.ExpectedSurvivalHp ?? 0,
                normalizedFailureProbability
            )
                + ScaleNonNegativeValue(
                    successPreview?.ExpectedSurvivalHp ?? 0,
                    normalizedSuccessProbability
                ),
            0
        );

        var weightedCandidates = new List<WeightedFatalCandidatePreview>();
        AppendWeightedCandidates(
            weightedCandidates,
            failureCandidates,
            normalizedFailureProbability
        );
        AppendWeightedCandidates(
            weightedCandidates,
            successCandidates,
            normalizedSuccessProbability
        );
        IReadOnlyList<BattleFatalInterceptCandidatePreview> candidates =
            MergeWeightedFatalCandidates(weightedCandidates);

        var weightedActions = new List<WeightedEquipmentActionPreview>();
        AppendWeightedActions(
            weightedActions,
            failurePreview?.SuccessActionPreviews,
            normalizedFailureProbability
        );
        AppendWeightedActions(
            weightedActions,
            successPreview?.SuccessActionPreviews,
            normalizedSuccessProbability
        );
        return new BattleFatalInterceptPreviewResult
        {
            InterceptProbabilityBasisPoints = interceptProbability,
            GuaranteedIntercept = interceptProbability >= 10000,
            ExpectedRecoveryHp = expectedRecoveryHp,
            ExpectedSurvivalHp = expectedSurvivalHp,
            SuccessActionPreviews = MergeWeightedActions(weightedActions),
            Candidates = candidates,
            ContinuationBranches = CombineSaveBranchContinuationBranches(
                failureContinuationBranches,
                normalizedFailureProbability,
                successContinuationBranches,
                normalizedSuccessProbability
            ),
        };
    }

    private static IReadOnlyList<BattleFatalInterceptPreviewBranch>
        BuildSinglePathContinuationBranches(
            BattleDamagePreviewWorkingSet workingSet,
            AppliedDamageResult damageResult
        )
    {
        IReadOnlyList<BattleFatalInterceptPreviewBranch> fatalBranches =
            damageResult.FatalInterceptPreview?.ContinuationBranches;
        if ((fatalBranches?.Count ?? 0) > 0)
            return fatalBranches;
        if (
            workingSet?.BattleState == null
            || workingSet.SourcePreview == null
            || workingSet.TargetPreview == null
        )
        {
            return Array.Empty<BattleFatalInterceptPreviewBranch>();
        }
        return new[]
        {
            new BattleFatalInterceptPreviewBranch
            {
                ProbabilityBasisPoints = 10000,
                BattleState = workingSet.BattleState,
                SourceUnit = workingSet.SourcePreview,
                TargetUnit = workingSet.TargetPreview,
                Intercepted = false,
            },
        };
    }

    private static IReadOnlyList<BattleEquipmentAbilityActionPreviewResult>
        CombineSaveBranchEquipmentActionPreviews(
            IReadOnlyList<BattleEquipmentAbilityActionPreviewResult> failureActions,
            int failureProbabilityBasisPoints,
            IReadOnlyList<BattleEquipmentAbilityActionPreviewResult> successActions,
            int successProbabilityBasisPoints
        )
    {
        var weighted = new List<WeightedEquipmentActionPreview>();
        AppendWeightedActions(
            weighted,
            failureActions,
            Math.Clamp(failureProbabilityBasisPoints, 0, 10000)
        );
        AppendWeightedActions(
            weighted,
            successActions,
            Math.Clamp(successProbabilityBasisPoints, 0, 10000)
        );
        return MergeWeightedActions(weighted);
    }

    private static IReadOnlyList<BattleFatalInterceptPreviewBranch>
        CombineSaveBranchContinuationBranches(
            IReadOnlyList<BattleFatalInterceptPreviewBranch> failureBranches,
            int failureProbabilityBasisPoints,
            IReadOnlyList<BattleFatalInterceptPreviewBranch> successBranches,
            int successProbabilityBasisPoints
        )
    {
        var result = new List<BattleFatalInterceptPreviewBranch>();
        AppendWeightedContinuationBranches(
            result,
            failureBranches,
            failureProbabilityBasisPoints
        );
        AppendWeightedContinuationBranches(
            result,
            successBranches,
            successProbabilityBasisPoints
        );
        return result.AsReadOnly();
    }

    private static void AppendWeightedContinuationBranches(
        List<BattleFatalInterceptPreviewBranch> target,
        IReadOnlyList<BattleFatalInterceptPreviewBranch> branches,
        int outerProbabilityBasisPoints
    )
    {
        if (target == null || branches == null || outerProbabilityBasisPoints <= 0)
            return;
        foreach (BattleFatalInterceptPreviewBranch branch in branches)
        {
            int probability = ScaleBasisPoints(
                branch?.ProbabilityBasisPoints ?? 0,
                outerProbabilityBasisPoints
            );
            if (probability <= 0 || branch == null)
                continue;
            target.Add(
                new BattleFatalInterceptPreviewBranch
                {
                    ProbabilityBasisPoints = probability,
                    BattleState = branch.BattleState,
                    SourceUnit = branch.SourceUnit,
                    TargetUnit = branch.TargetUnit,
                    Intercepted = branch.Intercepted,
                    WinningBindingId = branch.WinningBindingId,
                    WinningInterceptId = branch.WinningInterceptId,
                }
            );
        }
    }

    private static void AppendWeightedCandidates(
        List<WeightedFatalCandidatePreview> target,
        IReadOnlyList<BattleFatalInterceptCandidatePreview> candidates,
        int branchProbabilityBasisPoints
    )
    {
        if (target == null || candidates == null || branchProbabilityBasisPoints <= 0)
            return;
        foreach (BattleFatalInterceptCandidatePreview candidate in candidates)
        {
            if (candidate != null)
            {
                target.Add(
                    new WeightedFatalCandidatePreview(
                        candidate,
                        branchProbabilityBasisPoints
                    )
                );
            }
        }
    }

    private static IReadOnlyList<BattleFatalInterceptCandidatePreview>
        MergeWeightedFatalCandidates(
            IReadOnlyList<WeightedFatalCandidatePreview> weightedCandidates
        )
    {
        if (weightedCandidates == null || weightedCandidates.Count == 0)
            return Array.Empty<BattleFatalInterceptCandidatePreview>();

        var result = new List<BattleFatalInterceptCandidatePreview>();
        var consumed = new bool[weightedCandidates.Count];
        for (int index = 0; index < weightedCandidates.Count; index++)
        {
            if (consumed[index])
                continue;
            WeightedFatalCandidatePreview seed = weightedCandidates[index];
            BattleFatalInterceptCandidatePreview template = seed.Preview;
            int reachProbability = 0;
            int contributionProbability = 0;
            long weightedRecovery = 0;
            bool available = false;
            bool blocksDeathSource = false;
            var actionContributions = new List<WeightedEquipmentActionPreview>();
            for (int candidateIndex = index; candidateIndex < weightedCandidates.Count; candidateIndex++)
            {
                if (consumed[candidateIndex])
                    continue;
                WeightedFatalCandidatePreview weighted = weightedCandidates[candidateIndex];
                if (!SameFatalCandidate(template, weighted.Preview))
                    continue;

                consumed[candidateIndex] = true;
                int scaledReach = ScaleBasisPoints(
                    weighted.Preview.ReachProbabilityBasisPoints,
                    weighted.BranchProbabilityBasisPoints
                );
                int scaledContribution = ScaleBasisPoints(
                    weighted.Preview.ContributionProbabilityBasisPoints,
                    weighted.BranchProbabilityBasisPoints
                );
                reachProbability = Math.Clamp(
                    reachProbability + scaledReach,
                    0,
                    10000
                );
                contributionProbability = Math.Clamp(
                    contributionProbability + scaledContribution,
                    0,
                    10000
                );
                weightedRecovery +=
                    (long)Math.Max(weighted.Preview.ExpectedRecoveryHp, 0)
                    * scaledContribution;
                available |= weighted.Preview.Available;
                blocksDeathSource |= weighted.Preview.BlocksDeathSource;
                AppendWeightedActions(
                    actionContributions,
                    weighted.Preview.SuccessActionPreviews,
                    weighted.BranchProbabilityBasisPoints
                );
            }

            int successProbability = reachProbability > 0
                ? (int)Math.Clamp(
                    Math.Round(
                        contributionProbability * 10000.0 / reachProbability
                    ),
                    0.0,
                    10000.0
                )
                : 0;
            int expectedRecoveryHp = contributionProbability > 0
                ? (int)Math.Max(
                    Math.Round(
                        weightedRecovery / (double)contributionProbability
                    ),
                    0.0
                )
                : Math.Max(template.ExpectedRecoveryHp, 0);
            result.Add(
                new BattleFatalInterceptCandidatePreview
                {
                    SourceEquipmentInstanceId = template.SourceEquipmentInstanceId,
                    BindingId = template.BindingId,
                    InterceptId = template.InterceptId,
                    ResolutionOrder = template.ResolutionOrder,
                    ProtectionPriority = template.ProtectionPriority,
                    Available = available,
                    BlocksDeathSource = blocksDeathSource,
                    ReachProbabilityBasisPoints = reachProbability,
                    SuccessProbabilityBasisPoints = successProbability,
                    ContributionProbabilityBasisPoints = contributionProbability,
                    ExpectedRecoveryHp = expectedRecoveryHp,
                    SuccessActionPreviews = MergeWeightedActions(actionContributions),
                }
            );
        }
        return result.AsReadOnly();
    }

    private static bool SameFatalCandidate(
        BattleFatalInterceptCandidatePreview left,
        BattleFatalInterceptCandidatePreview right
    ) =>
        left != null
        && right != null
        && left.SourceEquipmentInstanceId == right.SourceEquipmentInstanceId
        && left.BindingId == right.BindingId
        && left.InterceptId == right.InterceptId
        && left.ResolutionOrder == right.ResolutionOrder
        && left.ProtectionPriority == right.ProtectionPriority;

    private static void AppendWeightedActions(
        List<WeightedEquipmentActionPreview> target,
        IReadOnlyList<BattleEquipmentAbilityActionPreviewResult> actions,
        int branchProbabilityBasisPoints
    )
    {
        if (target == null || actions == null || branchProbabilityBasisPoints <= 0)
            return;
        foreach (BattleEquipmentAbilityActionPreviewResult action in actions)
        {
            if (action != null)
            {
                target.Add(
                    new WeightedEquipmentActionPreview(
                        action,
                        branchProbabilityBasisPoints
                    )
                );
            }
        }
    }

    private static IReadOnlyList<BattleEquipmentAbilityActionPreviewResult>
        MergeWeightedActions(
            IReadOnlyList<WeightedEquipmentActionPreview> weightedActions
        )
    {
        if (weightedActions == null || weightedActions.Count == 0)
            return Array.Empty<BattleEquipmentAbilityActionPreviewResult>();

        var result = new List<BattleEquipmentAbilityActionPreviewResult>();
        var consumed = new bool[weightedActions.Count];
        for (int index = 0; index < weightedActions.Count; index++)
        {
            if (consumed[index])
                continue;
            WeightedEquipmentActionPreview seed = weightedActions[index];
            BattleEquipmentAbilityActionPreviewResult template = seed.Preview;
            int triggerProbability = 0;
            bool allApplied = true;
            IReadOnlyList<BattleDamagePreviewResult> damagePreviews =
                Array.Empty<BattleDamagePreviewResult>();
            for (int actionIndex = index; actionIndex < weightedActions.Count; actionIndex++)
            {
                if (consumed[actionIndex])
                    continue;
                WeightedEquipmentActionPreview weighted = weightedActions[actionIndex];
                if (!SameEquipmentAction(template, weighted.Preview))
                    continue;

                consumed[actionIndex] = true;
                triggerProbability = Math.Clamp(
                    triggerProbability
                        + ScaleBasisPoints(
                            weighted.Preview.TriggerProbabilityBasisPoints,
                            weighted.BranchProbabilityBasisPoints
                        ),
                    0,
                    10000
                );
                allApplied &= weighted.Preview.Applied;
                if (
                    damagePreviews.Count == 0
                    && (weighted.Preview.DamagePreviews?.Count ?? 0) > 0
                )
                {
                    damagePreviews = weighted.Preview.DamagePreviews;
                }
            }

            bool guaranteed = triggerProbability >= 10000;
            result.Add(
                new BattleEquipmentAbilityActionPreviewResult
                {
                    BindingId = template.BindingId,
                    ActionId = template.ActionId,
                    ActionKind = template.ActionKind,
                    TriggerSkillId = template.TriggerSkillId,
                    TriggerProbabilityBasisPoints = triggerProbability,
                    Guaranteed = guaranteed,
                    Applied = guaranteed && allApplied,
                    Conditional = triggerProbability > 0 && !guaranteed,
                    Supported = template.Supported,
                    UnsupportedReason = template.UnsupportedReason,
                    DamagePreviews = damagePreviews,
                }
            );
        }
        return result.AsReadOnly();
    }

    private static bool SameEquipmentAction(
        BattleEquipmentAbilityActionPreviewResult left,
        BattleEquipmentAbilityActionPreviewResult right
    ) =>
        left != null
        && right != null
        && left.BindingId == right.BindingId
        && left.ActionId == right.ActionId
        && left.ActionKind == right.ActionKind
        && left.TriggerSkillId == right.TriggerSkillId
        && left.Supported == right.Supported
        && string.Equals(
            left.UnsupportedReason,
            right.UnsupportedReason,
            StringComparison.Ordinal
        )
        && SameDamagePreviewList(left.DamagePreviews, right.DamagePreviews);

    private static bool SameDamagePreviewList(
        IReadOnlyList<BattleDamagePreviewResult> left,
        IReadOnlyList<BattleDamagePreviewResult> right
    )
    {
        int leftCount = left?.Count ?? 0;
        int rightCount = right?.Count ?? 0;
        if (leftCount != rightCount)
            return false;
        for (int index = 0; index < leftCount; index++)
        {
            BattleDamagePreviewResult leftPreview = left[index];
            BattleDamagePreviewResult rightPreview = right[index];
            if (
                leftPreview == null
                || rightPreview == null
                || leftPreview.PreSaveDamage != rightPreview.PreSaveDamage
                || leftPreview.PostSaveDamage != rightPreview.PostSaveDamage
                || leftPreview.HpDamage != rightPreview.HpDamage
                || leftPreview.ShieldAbsorbed != rightPreview.ShieldAbsorbed
                || leftPreview.LethalProbabilityBasisPoints
                    != rightPreview.LethalProbabilityBasisPoints
                || leftPreview.FatalInterceptProbabilityBasisPoints
                    != rightPreview.FatalInterceptProbabilityBasisPoints
                || leftPreview.SourcePreviewAfter?.unit_id
                    != rightPreview.SourcePreviewAfter?.unit_id
                || leftPreview.TargetPreviewAfter?.unit_id
                    != rightPreview.TargetPreviewAfter?.unit_id
                || !SameFatalPreviewIdentity(
                    leftPreview.FatalInterceptPreview,
                    rightPreview.FatalInterceptPreview
                )
            )
            {
                return false;
            }
        }
        return true;
    }

    private static bool SameFatalPreviewIdentity(
        BattleFatalInterceptPreviewResult left,
        BattleFatalInterceptPreviewResult right
    )
    {
        if (ReferenceEquals(left, right))
            return true;
        if (left == null || right == null)
            return false;
        if (
            left.InterceptProbabilityBasisPoints
                != right.InterceptProbabilityBasisPoints
            || (left.Candidates?.Count ?? 0) != (right.Candidates?.Count ?? 0)
        )
        {
            return false;
        }
        for (int index = 0; index < left.Candidates.Count; index++)
        {
            BattleFatalInterceptCandidatePreview leftCandidate = left.Candidates[index];
            BattleFatalInterceptCandidatePreview rightCandidate = right.Candidates[index];
            if (
                !SameFatalCandidate(leftCandidate, rightCandidate)
                || leftCandidate.ReachProbabilityBasisPoints
                    != rightCandidate.ReachProbabilityBasisPoints
                || leftCandidate.ContributionProbabilityBasisPoints
                    != rightCandidate.ContributionProbabilityBasisPoints
            )
            {
                return false;
            }
        }
        return true;
    }

    private static int ScaleBasisPoints(int value, int probabilityBasisPoints) =>
        (int)Math.Clamp(
            Math.Round(
                Math.Max(value, 0)
                    * Math.Clamp(probabilityBasisPoints, 0, 10000)
                    / 10000.0
            ),
            0.0,
            10000.0
        );

    private static int ScaleNonNegativeValue(
        int value,
        int probabilityBasisPoints
    ) =>
        (int)Math.Clamp(
            Math.Round(
                Math.Max(value, 0)
                    * Math.Clamp(probabilityBasisPoints, 0, 10000)
                    / 10000.0
            ),
            0.0,
            int.MaxValue
        );

    private static int ResolveDamagePreviewLethalProbabilityBasisPoints(
        BattleUnitState targetPreview,
        AppliedDamageResult damageResult
    )
    {
        if (targetPreview?.IsAlive() == true)
            return 0;
        int interceptProbability = Math.Clamp(
            damageResult.FatalInterceptPreview?.InterceptProbabilityBasisPoints ?? 0,
            0,
            10000
        );
        return 10000 - interceptProbability;
    }

    private DamagePreviewSaveEstimate BuildDamagePreviewSaveEstimate(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDefinition effectDefinition,
        DamageResolutionContext damageContext,
        int damageBeforeSave,
        BattleDamagePreviewSaveMode saveMode
    )
    {
        BattleSaveProbabilityResult probability =
            BattleSaveResolver.EstimateSaveSuccessProbabilityResult(
                sourceUnit,
                targetUnit,
                effectDefinition,
                (damageContext ?? DamageResolutionContext.Empty()).ToBattleSaveContext()
            );
        if (!probability.HasSave)
        {
            return DamagePreviewSaveEstimate.None(damageBeforeSave);
        }
        int successBasisPoints = Math.Clamp(probability.SuccessProbabilityBasisPoints, 0, 10000);
        int failureBasisPoints = Math.Clamp(probability.FailureProbabilityBasisPoints, 0, 10000);
        int damageOnSaveSuccess =
            effectDefinition != null
            && effectDefinition.SavePartialOnSuccess
            && !probability.Immune
                ? damageBeforeSave / 2
                : 0;
        int expectedDamage = RoundToInt(
            (damageBeforeSave * failureBasisPoints + damageOnSaveSuccess * successBasisPoints)
                / 10000.0
        );
        int worstDamage = failureBasisPoints <= 0 ? damageOnSaveSuccess : damageBeforeSave;
        int damageAfterSave =
            saveMode == BattleDamagePreviewSaveMode.Worst ? worstDamage : expectedDamage;
        return new DamagePreviewSaveEstimate(
            true,
            damageBeforeSave,
            Math.Max(damageAfterSave, 0),
            Math.Max(expectedDamage, 0),
            Math.Max(worstDamage, 0),
            damageBeforeSave,
            damageOnSaveSuccess,
            effectDefinition != null && effectDefinition.SavePartialOnSuccess,
            successBasisPoints,
            RoundToInt(successBasisPoints / 100.0),
            failureBasisPoints,
            probability.Dc,
            probability.Ability.ToString(),
            probability.SaveTag.ToString(),
            probability.AdvantageState.ToString(),
            probability.AbilityValue,
            probability.AbilityModifier,
            probability.Bonus,
            probability.Immune,
            probability.Sources ?? Array.Empty<BattleSaveSource>(),
            BattleWeightedStatusOutcomeRules.BuildPreview(
                effectDefinition?.SaveFailureStatusOutcomes,
                failureBasisPoints
            )
        );
    }

    private static GArray BuildSaveSourceArray(IReadOnlyList<BattleSaveSource> sources)
    {
        var result = new GArray();
        if (sources == null)
        {
            return result;
        }
        foreach (BattleSaveSource source in sources)
        {
            result.Add(BattleSaveResultProjection.Project(source));
        }
        return result;
    }

    private static Dictionary<string, object> ProjectAppliedDamagePayload(
        AppliedDamageResult result
    ) => AttackEffectResolutionPlainPayload.BuildDamageEvent(result.Event);

    private static Dictionary<string, object> ProjectDamageOutcomePayload(
        DamageOutcomeResult result
    ) => AttackEffectResolutionPlainPayload.BuildDamageEvent(result.Event);

    private static DamageOutcomeResult WithDamagePreviewSaveEstimate(
        DamageOutcomeResult damageOutcome,
        DamagePreviewSaveEstimate saveEstimate
    )
    {
        DamageEventResult @event = damageOutcome.Event;
        @event.PreSaveDamage = saveEstimate.DamageBeforeSave;
        @event.SaveAdjustedDamage = Math.Max(saveEstimate.DamageAfterSave, 0);
        @event.SaveResult = SaveResolutionFromPreviewEstimate(saveEstimate);
        @event.SaveSuccessProbabilityBasisPoints =
            saveEstimate.SaveSuccessProbabilityBasisPoints;
        @event.SaveFailureProbabilityBasisPoints =
            saveEstimate.SaveFailureProbabilityBasisPoints;
        @event.SaveImmune = saveEstimate.Immune;
        @event.SavePartialApplied = saveEstimate.HasSave && saveEstimate.SavePartialOnSuccess;
        @event.FullyAbsorbedBySave =
            saveEstimate.HasSave
            && saveEstimate.DamageBeforeSave > 0
            && saveEstimate.DamageAfterSave <= 0;
        if (saveEstimate.HasSave)
        {
            @event.ResolvedDamage = Math.Max(saveEstimate.DamageAfterSave, 0);
        }
        return damageOutcome with
        {
            Event = @event,
            ResolvedDamage = Math.Max(@event.ResolvedDamage, 0),
        };
    }

    private static SaveResolutionResult SaveResolutionFromPreviewEstimate(
        DamagePreviewSaveEstimate saveEstimate
    )
    {
        return new SaveResolutionResult
        {
            HasSave = saveEstimate.HasSave,
            Immune = saveEstimate.Immune,
            Success = false,
            Dc = saveEstimate.Dc,
            Ability = new StringName(saveEstimate.Ability ?? ""),
            SaveTag = new StringName(saveEstimate.SaveTag ?? ""),
            SaveKind = new StringName(saveEstimate.SaveTag ?? ""),
            AdvantageState = new StringName(saveEstimate.AdvantageState ?? ""),
            AbilityValue = saveEstimate.AbilityValue,
            AbilityModifier = saveEstimate.AbilityModifier,
            Bonus = saveEstimate.Bonus,
            Sources = CopySaveSources(saveEstimate.Sources),
            DamageBeforeSave = saveEstimate.DamageBeforeSave,
            DamageAfterSave = saveEstimate.DamageAfterSave,
            DamageAfterSaveEstimate = saveEstimate.DamageAfterSaveEstimate,
            DamageAfterSaveWorst = saveEstimate.DamageAfterSaveWorst,
            DamageOnSaveFailure = saveEstimate.DamageOnSaveFailure,
            DamageOnSaveSuccess = saveEstimate.DamageOnSaveSuccess,
            SavePartialOnSuccess = saveEstimate.SavePartialOnSuccess,
            SaveSuccessProbabilityBasisPoints =
                saveEstimate.SaveSuccessProbabilityBasisPoints,
            SaveSuccessRatePercent = saveEstimate.SaveSuccessRatePercent,
            SaveFailureProbabilityBasisPoints =
                saveEstimate.SaveFailureProbabilityBasisPoints,
        };
    }
}
