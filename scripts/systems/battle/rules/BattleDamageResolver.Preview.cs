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
        IReadOnlyList<BattleSaveSource> Sources
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
                Array.Empty<BattleSaveSource>()
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
                Sources
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
        bool InvalidDamageTag
    );

    private readonly record struct DamagePreviewBranchLethalEstimate(
        bool FailureKills,
        bool SuccessKills,
        int FailureHpDamage,
        int SuccessHpDamage,
        bool StableLethal,
        int LethalProbabilityBasisPoints
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
            (damageContext ?? DamageResolutionContext.Empty()).WithDamageRollMode(
                ToStringName(resolvedRollMode)
            );
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
        AppliedDamageResult damageResult = ApplyDamageToTargetResult(
            targetPreview,
            damageOutcome.ToDamageApplicationInput(suppressDamageApplicationHook: true),
            sourcePreview
        );
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
            false
        );
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
            sourcePreview
        );
        AppliedDamageResult successResult = ApplyDamageToTargetResult(
            successTarget,
            successOutcome.ToDamageApplicationInput(suppressDamageApplicationHook: true),
            sourcePreview
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
        BattleUnitState sourcePreview
    )
    {
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
            sourcePreview
        );
        AppliedDamageResult successResult = ApplyDamageToTargetResult(
            successTarget,
            successOutcome.ToDamageApplicationInput(suppressDamageApplicationHook: true),
            sourcePreview
        );

        bool failureKills = failureTarget != null && failureTarget.GetCurrentHp() <= 0;
        bool successKills = successTarget != null && successTarget.GetCurrentHp() <= 0;
        return new DamagePreviewBranchLethalEstimate(
            failureKills,
            successKills,
            failureResult.HpDamage,
            successResult.HpDamage,
            failureKills && successKills,
            failureKills
                ? (successKills ? 10000 : failureBasis)
                : 0
        );
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
            probability.Sources ?? Array.Empty<BattleSaveSource>()
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
