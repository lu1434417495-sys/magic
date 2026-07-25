using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

internal enum BattleDamagePreviewRollMode
{
    Unknown = 0,
    Random,
    Average,
    Maximum,
}

internal enum BattleDamagePreviewSaveMode
{
    Unknown = 0,
    Expected,
    Worst,
}

internal sealed class BattleDamagePreviewOptions
{
    public BattleDamagePreviewRollMode RollMode { get; }
    public BattleDamagePreviewSaveMode SaveMode { get; }

    public BattleDamagePreviewOptions(
        BattleDamagePreviewRollMode rollMode = BattleDamagePreviewRollMode.Average,
        BattleDamagePreviewSaveMode saveMode = BattleDamagePreviewSaveMode.Expected
    )
    {
        RollMode = rollMode;
        SaveMode = saveMode;
    }
}

public partial class BattleDamageResolver : IDisposable
{
    private static readonly StringName FortuneMarkTargetStatId = "fortune_mark_target";
    private static readonly StringName StatusAttackUp = "attack_up";
    private static readonly StringName StatusDamageReductionUp = "damage_reduction_up";
    private static readonly StringName StatusGuarding = "guarding";
    private static readonly StringName StatusMarked = "marked";
    private static readonly StringName StatusArcherPreAim = "archer_pre_aim";
    private static readonly StringName MitigationTierNormal = "normal";
    private static readonly StringName MitigationTierHalf = "half";
    private static readonly StringName MitigationTierDouble = "double";
    private static readonly StringName MitigationTierImmune = "immune";
    private static readonly StringName DiceEventReasonCriticalHit = "critical_hit";
    private static readonly StringName DiceEventReasonDiceThreshold = "dice_threshold";
    private static readonly StringName DiceEventReasonSkillDiceMax = "skill_dice_max";
    private static readonly StringName DiceEventReasonWeaponDiceMax = "weapon_dice_max";
    private static readonly StringName StatusBlackStarBrandEliteGuardWindow =
        "black_star_brand_elite_guard_window";
    private static readonly StringName StatusCrownBreakBrokenFang = "crown_break_broken_fang";
    private static readonly StringName StatusCrownBreakBrokenHand = "crown_break_broken_hand";
    private static readonly StringName StatusCrownBreakBlindedEye = "crown_break_blinded_eye";
    private static readonly StringName StatusMeleeComboStack = "melee_combo_stack";
    private static readonly StringName StatusRangedComboStack = "ranged_combo_stack";
    internal static readonly StringName EffectEquipmentDurabilityDamage =
        BattleTypedNames.EffectEquipmentDurabilityDamage;
    private static readonly StringName DamagePreviewRollModeRandom = "random";
    private static readonly StringName DamagePreviewRollModeAverage = "average";
    private static readonly StringName DamagePreviewRollModeMaximum = "maximum";
    private static readonly StringName DamagePreviewSaveModeExpected = "expected";
    private static readonly StringName DamagePreviewSaveModeWorst = "worst";

    private const int MinDamageFloor = 0;
    private const int DamageReductionUpFixedPerPower = 2;
    private const int DamageDiceHighTotalThresholdNumerator = 4;
    private const int DamageDiceHighTotalThresholdDenominator = 5;
    private const int AttackCheckTarget = 21;
    private const int NaturalHitRoll = 20;
    private const int BlackStarBrandGuardIgnoreFlat = 4;

    internal readonly record struct DamageEffectRuntimeParameters(
        bool UseWeaponPhysicalDamageTag,
        bool AddWeaponDice,
        bool RemoveHarmful,
        bool RemoveHarmfulFromAllies,
        bool RemoveBeneficial,
        bool RemoveBeneficialFromEnemies,
        bool RequireDamageApplied
    )
    {
        public static DamageEffectRuntimeParameters FromEffect(
            CombatEffectDefinition effectDefinition
        )
        {
            return new DamageEffectRuntimeParameters(
                effectDefinition?.UseWeaponPhysicalDamageTag ?? false,
                effectDefinition?.AddWeaponDice ?? false,
                effectDefinition?.RemoveHarmful ?? false,
                effectDefinition?.RemoveHarmfulFromAllies ?? true,
                effectDefinition?.RemoveBeneficial ?? false,
                effectDefinition?.RemoveBeneficialFromEnemies ?? true,
                effectDefinition?.RequireDamageApplied ?? false
            );
        }
    }

    public void Dispose()
    {
        _fate_event_bus.Dispose();
    }

    private readonly record struct AppliedDamageResult(
        DamageEventResult Event,
        int Damage,
        int HpDamage,
        int ShieldAbsorbed,
        bool ShieldBroken,
        bool LowLuckBlackStarWedgeTriggered,
        DamageDiceEventSnapshot DamageDiceEvent
    )
    {
        public bool HasAppliedDamage => Damage > 0 || ShieldAbsorbed > 0;

        public AppliedDamageResult WithHpDamage(int hpDamage)
        {
            int normalizedHpDamage = Math.Max(hpDamage, 0);
            DamageEventResult @event = Event;
            @event.Damage = normalizedHpDamage;
            @event.HpDamage = normalizedHpDamage;
            @event.FullyAbsorbedByShield = normalizedHpDamage <= 0 && ShieldAbsorbed > 0;
            return new AppliedDamageResult(
                @event,
                normalizedHpDamage,
                normalizedHpDamage,
                ShieldAbsorbed,
                ShieldBroken,
                LowLuckBlackStarWedgeTriggered,
                DamageDiceEvent
            );
        }
    }

    private readonly record struct SpellControlCheckContext(
        BattleState BattleState,
        StringName SkillId,
        bool DispatchEvents,
        bool HasIsDisadvantage,
        bool IsDisadvantage
    )
    {
        public static SpellControlCheckContext ForSkill(
            BattleState battleState,
            StringName skillId,
            bool dispatchEvents = true
        )
        {
            return new SpellControlCheckContext(
                battleState,
                skillId,
                dispatchEvents,
                false,
                false
            );
        }

        internal static SpellControlCheckContext FromDictionary(GDictionary payload)
        {
            bool hasDisadvantage = false;
            bool isDisadvantage = false;
            if (TryGetStatusParam(payload, "is_disadvantage", out object disadvantageValue))
            {
                hasDisadvantage = true;
                isDisadvantage = ToBool(disadvantageValue, false);
            }
            return new SpellControlCheckContext(
                null,
                DictStringNameLocal(payload, "skill_id"),
                ReadDispatchEvents(payload),
                hasDisadvantage,
                isDisadvantage
            );
        }

        public AttackContext ToAttackContext()
        {
            var result = new AttackContext
            {
                BattleState = BattleState,
                SkillId = SkillId,
            };
            if (HasIsDisadvantage)
            {
                result.HasIsDisadvantage = true;
                result.IsDisadvantage = IsDisadvantage;
            }
            return result;
        }

        private static bool ReadDispatchEvents(GDictionary payload)
        {
            if (!TryGet(payload, "dispatch_events", out Variant value))
            {
                return true;
            }
            return value.VariantType == Variant.Type.Bool ? value.AsBool() : true;
        }
    }

    private readonly record struct ExecuteEffectResult(
        bool Applied,
        int ExecuteStage,
        StringName ExecuteOutcome,
        IReadOnlyList<AppliedDamageResult> DamageResults
    )
    {
        public static ExecuteEffectResult Empty => new(
            false,
            -1,
            "",
            Array.Empty<AppliedDamageResult>()
        );
    }

    private readonly record struct GradedSaveExecuteEffectResult(
        bool Applied,
        IReadOnlyList<AppliedDamageResult> DamageResults,
        IReadOnlyList<ResolutionDiagnostic> Diagnostics
    )
    {
        public static GradedSaveExecuteEffectResult Empty => new(
            false,
            Array.Empty<AppliedDamageResult>(),
            Array.Empty<ResolutionDiagnostic>()
        );
    }

    private readonly record struct TraitTriggerResultSnapshot(
        TraitTriggerEventResult Event,
        bool Triggered,
        int ExtraWeaponDiceCount,
        int ExtraWeaponDiceSides,
        int ClampToHp
    )
    {
        internal static TraitTriggerResultSnapshot FromAttackTraitTriggerResult(
            AttackTraitTriggerResult result
        )
        {
            if (!result.Triggered)
            {
                return new TraitTriggerResultSnapshot(
                    new TraitTriggerEventResult(),
                    false,
                    0,
                    0,
                    0
                );
            }
            TraitTriggerEventResult @event = new()
            {
                Triggered = true,
                Event = result.Event,
                TraitId = result.TraitId,
                EffectType = result.EffectType,
                OriginalRoll = result.OriginalRoll,
                RerollDie = result.RerollDie,
                RerolledRoll = result.RerolledRoll,
                DieSize = result.DieSize,
                ExtraWeaponDiceCount = Math.Max(result.ExtraWeaponDiceCount, 0),
                ExtraWeaponDiceSides = Math.Max(result.ExtraWeaponDiceSides, 0),
                ClampToHp = Math.Max(result.ClampToHp, 0),
                ProjectedHp = result.ProjectedHp,
                HpDamage = Math.Max(result.HpDamage, 0),
                ChargeKey = result.ChargeKey,
                ChargesRemaining = Math.Max(result.ChargesRemaining, 0),
            };
            return new TraitTriggerResultSnapshot(
                @event,
                true,
                Math.Max(result.ExtraWeaponDiceCount, 0),
                Math.Max(result.ExtraWeaponDiceSides, 0),
                Math.Max(result.ClampToHp, 0)
            );
        }

        internal TraitTriggerEventResult ToEventResult() => Event;
    }

    private readonly record struct DicePoolRollResult(
        int Count,
        int Sides,
        int[] Rolls,
        int Total,
        int Bonus,
        int MaxTotal,
        bool IsMax
    )
    {
        public static DicePoolRollResult Empty => new(
            0,
            0,
            Array.Empty<int>(),
            0,
            0,
            0,
            false
        );

        public bool HasDice => Count > 0 && Sides > 0;

        public int TotalWithBonus => Total + Bonus;

        internal DamageDiceRollDetail ToDamageDiceRollDetail() =>
            new()
            {
                Count = Count,
                Sides = Sides,
                Rolls = Rolls ?? Array.Empty<int>(),
                Total = Total,
                Bonus = Bonus,
                MaxTotal = MaxTotal,
                IsMax = IsMax,
            };
    }

    private readonly record struct DamageDiceEventFlags(DamageDiceEventSnapshot Snapshot);

    private readonly Dictionary<StringName, SkillDefinition> _skillDefinitionIndex = new();
    private readonly List<BattleSkillMasteryGrant> _last_stand_mastery_records = new();
    private readonly BattleFateEventBus _fate_event_bus = new();
    private readonly BattleReportFormatter _report_formatter = new();
    private readonly TraitTriggerHooks _trait_trigger_hooks = new();
    private BattleHitResolver _hit_resolver = new();
    private IBattleDamageApplicationHook _damage_application_hook;
    private IBattleEquipmentDamageQuery _equipment_ability_damage_query;
    private IBattleEquipmentCombatReactionSink _equipment_ability_reaction_sink;
    private readonly BattleEquipmentDurabilityResolver _equipmentDurabilityResolver = new();

    internal static BattleDamagePreviewRollMode ToDamagePreviewRollMode(StringName value)
    {
        if (value == DamagePreviewRollModeRandom)
            return BattleDamagePreviewRollMode.Random;
        if (value == DamagePreviewRollModeAverage)
            return BattleDamagePreviewRollMode.Average;
        if (value == DamagePreviewRollModeMaximum)
            return BattleDamagePreviewRollMode.Maximum;
        return BattleDamagePreviewRollMode.Unknown;
    }

    internal static StringName ToStringName(BattleDamagePreviewRollMode mode)
    {
        return mode switch
        {
            BattleDamagePreviewRollMode.Random => DamagePreviewRollModeRandom,
            BattleDamagePreviewRollMode.Average => DamagePreviewRollModeAverage,
            BattleDamagePreviewRollMode.Maximum => DamagePreviewRollModeMaximum,
            _ => "",
        };
    }

    internal static BattleDamagePreviewSaveMode ToDamagePreviewSaveMode(StringName value)
    {
        if (value == DamagePreviewSaveModeExpected)
            return BattleDamagePreviewSaveMode.Expected;
        if (value == DamagePreviewSaveModeWorst)
            return BattleDamagePreviewSaveMode.Worst;
        return BattleDamagePreviewSaveMode.Unknown;
    }

    internal static StringName ToStringName(BattleDamagePreviewSaveMode mode)
    {
        return mode switch
        {
            BattleDamagePreviewSaveMode.Expected => DamagePreviewSaveModeExpected,
            BattleDamagePreviewSaveMode.Worst => DamagePreviewSaveModeWorst,
            _ => "",
        };
    }

    internal void SetSkillDefinitions(IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions)
    {
        _skillDefinitionIndex.Clear();
        if (skillDefinitions == null || skillDefinitions.Count == 0)
        {
            return;
        }

        foreach ((StringName skillId, SkillDefinition skillDefinition) in skillDefinitions)
        {
            if (skillId == "" || skillDefinition == null)
            {
                continue;
            }
            _skillDefinitionIndex[skillId] = skillDefinition;
        }
    }

    internal List<BattleSkillMasteryGrant> GetAndClearLastStandMasteryRecordsTyped()
    {
        List<BattleSkillMasteryGrant> records = new(_last_stand_mastery_records);
        _last_stand_mastery_records.Clear();
        return records;
    }

    public void SetHitResolver(BattleHitResolver hit_resolver)
    {
        _hit_resolver = hit_resolver ?? new BattleHitResolver();
    }

    internal void SetDamageApplicationHook(IBattleDamageApplicationHook hook)
    {
        _damage_application_hook = hook;
    }

    internal void SetEquipmentAbilityPorts(
        IBattleEquipmentDamageQuery damageQuery,
        IBattleEquipmentCombatReactionSink reactionSink
    )
    {
        _equipment_ability_damage_query = damageQuery;
        _equipment_ability_reaction_sink = reactionSink;
    }

    internal static DamageApplicationProjection ProjectDamageApplication(
        BattleUnitState targetUnit,
        DamageApplicationInput damageInput
    ) =>
        DamageApplicationProjection.Project(targetUnit, damageInput);

    internal BattleFateEventBus GetFateEventBus()
    {
        return _fate_event_bus;
    }

    internal AttackEffectResolutionResult ResolveSkillResult(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        SkillDefinition skillDefinition,
        BattleState battleState = null
    )
    {
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (sourceUnit == null || targetUnit == null || combatProfile == null)
        {
            return BuildEmptyResolutionResult(skillDefinition?.SkillId ?? new StringName(""));
        }
        int skillLevel =
            sourceUnit.GetKnownSkillLevelTyped(skillDefinition.SkillId, fallback: 1);
        return ResolveEffects(
            sourceUnit,
            targetUnit,
            FilterEffectDefinitionsForSkillLevel(combatProfile.EffectDefinitions, skillLevel),
            DamageResolutionContext
                .ForSkill(skillDefinition.SkillId)
                .WithBattleState(battleState)
                .WithSourceSkillLevel(
                    Math.Max(skillLevel, 1)
                )
        );
    }

    private static IReadOnlyList<CombatEffectDefinition> FilterEffectDefinitionsForSkillLevel(
        IEnumerable<CombatEffectDefinition> effectDefinitions,
        int skillLevel
    )
    {
        int normalizedSkillLevel = Math.Max(skillLevel, 1);
        var result = new List<CombatEffectDefinition>();
        foreach (CombatEffectDefinition effectDefinition in effectDefinitions ?? Array.Empty<CombatEffectDefinition>())
        {
            if (effectDefinition == null)
            {
                continue;
            }
            int minLevel = Math.Max(effectDefinition.MinSkillLevel, 0);
            int maxLevel = effectDefinition.MaxSkillLevel;
            if (normalizedSkillLevel >= minLevel && (maxLevel < 0 || normalizedSkillLevel <= maxLevel))
            {
                result.Add(effectDefinition);
            }
        }
        return result.Count == 0 ? Array.Empty<CombatEffectDefinition>() : result;
    }

    internal virtual AttackEffectResolutionResult ResolveAttackEffects(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        IEnumerable<CombatEffectDefinition> effect_definitions,
        AttackCheckInput attack_check
    )
    {
        return ResolveAttackEffects(
            source_unit,
            target_unit,
            effect_definitions,
            attack_check,
            new AttackContext()
        );
    }

    internal virtual AttackEffectResolutionResult ResolveAttackEffects(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        IEnumerable<CombatEffectDefinition> effect_definitions,
        AttackCheckInput attack_check,
        AttackContext attack_context = null
    )
    {
        if (source_unit == null || target_unit == null)
        {
            AttackEffectResolutionResult emptyResult = ApplyAttackMetadataResult(
                BuildEmptyResolutionResult(attack_check.SkillId),
                new AttackResolutionMetadata()
            );
            emptyResult.AttackCheck = attack_check;
            return AttackEffectResolutionResultReader.FinalizeTypedResult(emptyResult);
        }

        IReadOnlyList<CombatEffectDefinition> resolvedEffectDefinitions =
            ToEffectDefinitionList(effect_definitions);
        AttackContext normalizedAttackContext = attack_context ?? new AttackContext();
        AttackResolutionMetadata attackMetadata = ResolveAttackMetadata(
            source_unit,
            target_unit,
            attack_check,
            normalizedAttackContext
        );
        if (attackMetadata.SkillId == "" && normalizedAttackContext.SkillId != "")
        {
            attackMetadata.SkillId = normalizedAttackContext.SkillId;
        }
        bool attackIncludesWeaponDamage =
            EffectDefinitionsIncludeWeaponDamage(resolvedEffectDefinitions);
        ResolveEquipmentAbilityAttackCheckResult(
            source_unit,
            target_unit,
            attackMetadata,
            normalizedAttackContext,
            attackIncludesWeaponDamage
        );
        if (!attackMetadata.AttackSuccess)
        {
            AttackEffectResolutionResult failedResult = ApplyAttackMetadataResult(
                BuildEmptyResolutionResult(attackMetadata.SkillId),
                attackMetadata
            );
            failedResult.AttackCheck = attack_check;
            failedResult = AttachAttackReportEntry(
                failedResult,
                source_unit,
                target_unit,
                attackMetadata
            );
            DispatchAttackResolutionEvents(
                source_unit,
                target_unit,
                attackMetadata,
                normalizedAttackContext
            );
            ClearComboStackOnMiss(source_unit);
            ConsumeOneShotAttackCheckStatuses(source_unit);
            return AttackEffectResolutionResultReader.FinalizeTypedResult(failedResult);
        }

        int secondaryHitDcBase = 10;
        foreach (CombatEffectDefinition effectDefinition in resolvedEffectDefinitions)
        {
            if (effectDefinition?.TriggerEventKind == CombatEffectTriggerEvent.SecondaryHit)
            {
                secondaryHitDcBase = effectDefinition.SecondaryHitDcBase > 0
                    ? effectDefinition.SecondaryHitDcBase
                    : 10;
                break;
            }
        }
        attackMetadata.SecondaryHitSuccess = _resolve_secondary_hit(
            source_unit,
            target_unit,
            normalizedAttackContext,
            secondaryHitDcBase
        );
        DamageResolutionContext attackEffectContext =
            DamageResolutionContext
                .FromDictionary(BuildAttackEffectContext(attackMetadata))
                .WithBattleState(normalizedAttackContext.BattleState);

        AttackEffectResolutionResult resolvedResult = ApplyAttackMetadataResult(
            ResolveEffectsDefinitionCore(
                source_unit,
                target_unit,
                resolvedEffectDefinitions,
                attackEffectContext
            ),
            attackMetadata
        );
        if (
            attackIncludesWeaponDamage
            && BattleRangeService.UnitHasEquippedWeapon(source_unit)
        )
        {
            GrantWeaponTypeComboStackOnHit(source_unit);
        }
        resolvedResult.AttackCheck = attack_check;
        resolvedResult = ApplyEquipmentAbilityAfterHitResult(
            source_unit,
            target_unit,
            attackMetadata,
            normalizedAttackContext,
            resolvedResult
        );
        ReconcileEquipmentProjectionAfterDurabilityEvents(
            target_unit,
            resolvedResult,
            normalizedAttackContext.EventBatch
        );
        resolvedResult = AttachAttackReportEntry(
            resolvedResult,
            source_unit,
            target_unit,
            attackMetadata
        );
        DispatchAttackResolutionEvents(
            source_unit,
            target_unit,
            attackMetadata,
            normalizedAttackContext
        );
        ConsumeOneShotAttackCheckStatuses(source_unit);
        return AttackEffectResolutionResultReader.FinalizeTypedResult(resolvedResult);
    }

    private void ReconcileEquipmentProjectionAfterDurabilityEvents(
        BattleUnitState targetUnit,
        AttackEffectResolutionResult result,
        BattleEventBatch batch
    )
    {
        foreach (
            EquipmentDurabilityEventResult durabilityEvent in result.EquipmentDurabilityEvents
                ?? Array.Empty<EquipmentDurabilityEventResult>()
        )
        {
            if (
                !durabilityEvent.Destroyed
                || (
                    durabilityEvent.TargetUnitId != ""
                    && durabilityEvent.TargetUnitId != targetUnit?.unit_id
                )
            )
            {
                continue;
            }
            _equipment_ability_reaction_sink?.RefreshEquipmentProjectionAfterDurabilityDestruction(
                targetUnit,
                batch
            );
            return;
        }
    }

    private static void ConsumeOneShotAttackCheckStatuses(BattleUnitState sourceUnit)
    {
        if (sourceUnit == null)
            return;
        List<StringName> consumedStatusIds = new();
        foreach (StringName statusId in sourceUnit.GetSortedStatusEffectIdsTyped())
        {
            BattleStatusEffectState status = sourceUnit.GetStatusEffect(statusId);
            if (status?.consume_on_next_attack_check == true)
                consumedStatusIds.Add(statusId);
        }
        foreach (StringName statusId in consumedStatusIds)
            sourceUnit.EraseStatusEffect(statusId);
    }

    private void ResolveEquipmentAbilityAttackCheckResult(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        AttackResolutionMetadata attackMetadata,
        AttackContext attackContext,
        bool attackIncludesWeaponDamage
    )
    {
        IBattleEquipmentCombatReactionSink equipmentAbilityReactions =
            _equipment_ability_reaction_sink;
        if (
            equipmentAbilityReactions == null
            || sourceUnit == null
            || targetUnit == null
            || !attackIncludesWeaponDamage
        )
        {
            return;
        }

        equipmentAbilityReactions.ResolveAttackCheck(
            new BattleEquipmentAbilityAttackCheckContext
            {
                SourceUnit = sourceUnit,
                TargetUnit = targetUnit,
                BattleState = attackContext?.BattleState,
                AttackSucceeded = attackMetadata.AttackSuccess,
                CriticalHit = attackMetadata.CriticalHit,
                SkillId = attackMetadata.SkillId,
            }
        );
    }

    private static bool EffectDefinitionsIncludeWeaponDamage(
        IReadOnlyList<CombatEffectDefinition> effectDefinitions
    )
    {
        foreach (CombatEffectDefinition effectDefinition in effectDefinitions ?? Array.Empty<CombatEffectDefinition>())
        {
            if (
                effectDefinition != null
                && (effectDefinition.AddWeaponDice || effectDefinition.RequiresWeapon)
            )
            {
                return true;
            }
        }
        return false;
    }

    private AttackEffectResolutionResult ApplyEquipmentAbilityAfterHitResult(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        AttackResolutionMetadata attackMetadata,
        AttackContext attackContext,
        AttackEffectResolutionResult result
    )
    {
        IBattleEquipmentCombatReactionSink equipmentAbilityReactions =
            _equipment_ability_reaction_sink;
        if (
            equipmentAbilityReactions == null
            || sourceUnit == null
            || targetUnit == null
            || attackMetadata.AttackSuccess != true
            || !ResultIncludesWeaponDamage(result)
        )
        {
            return result;
        }

        int weaponHpDamage = ComputeWeaponHpDamage(result);
        BattleEquipmentAbilityAfterHitResult afterHitResult =
            equipmentAbilityReactions.ResolveAfterHit(
                new BattleEquipmentAbilityAfterHitContext
                {
                    SourceUnit = sourceUnit,
                    TargetUnit = targetUnit,
                    BattleState = attackContext?.BattleState,
                    AttackSucceeded = true,
                    CriticalHit = attackMetadata.CriticalHit,
                    ApplyDamageDiceActions = false,
                    WeaponHpDamage = weaponHpDamage,
                    Batch = attackContext?.EventBatch,
                    SaveContext = BuildAfterHitSaveContext(attackMetadata, attackContext),
                }
            );
        BattleEquipmentAbilityAfterHitResult hitReceivedResult =
            targetUnit.unit_id != sourceUnit.unit_id
                ? equipmentAbilityReactions.ResolveHitReceived(
                    new BattleEquipmentAbilityAfterHitContext
                    {
                        SourceUnit = targetUnit,
                        TargetUnit = sourceUnit,
                        BattleState = attackContext?.BattleState,
                        AttackSucceeded = true,
                        CriticalHit = attackMetadata.CriticalHit,
                        ApplyDamageDiceActions = false,
                        WeaponHpDamage = weaponHpDamage,
                        Batch = attackContext?.EventBatch,
                        SaveContext = BuildAfterHitSaveContext(attackMetadata, attackContext),
                    }
                )
                : null;
        if (afterHitResult?.Resolved != true && hitReceivedResult?.Resolved != true)
            return result;

        AttackEffectResolutionResult mergedResult =
            AttackEffectResolutionResultReader.FinalizeTypedResult(result);
        if (afterHitResult?.Resolved == true)
        {
            mergedResult.EquipmentDurabilityEvents = MergeEquipmentDurabilityEvents(
                mergedResult.EquipmentDurabilityEvents,
                afterHitResult.DurabilityResults
            );
            MergeAfterHitStatusResults(
                mergedResult.StatusEffectIds,
                mergedResult.SourceStatusEffectIds,
                sourceUnit,
                targetUnit,
                afterHitResult.StatusResults
            );
            mergedResult = MergeTriggeredSkillResults(
                mergedResult,
                targetUnit,
                afterHitResult.TriggeredSkillResults
            );
        }
        if (hitReceivedResult?.Resolved == true)
        {
            MergeAfterHitStatusResults(
                mergedResult.StatusEffectIds,
                mergedResult.SourceStatusEffectIds,
                sourceUnit,
                targetUnit,
                hitReceivedResult.StatusResults
            );
        }
        return mergedResult;
    }

    private static AttackEffectResolutionResult MergeTriggeredSkillResults(
        AttackEffectResolutionResult parent,
        BattleUnitState parentTarget,
        IReadOnlyList<BattleEquipmentAbilityTriggeredSkillResult> triggeredResults
    )
    {
        foreach (BattleEquipmentAbilityTriggeredSkillResult triggered in triggeredResults ?? Array.Empty<BattleEquipmentAbilityTriggeredSkillResult>())
        {
            if (
                triggered == null
                || !triggered.MergeIntoParentResult
                || triggered.TargetUnitId != (parentTarget?.unit_id ?? new StringName(""))
            )
            {
                continue;
            }
            AttackEffectResolutionResult child =
                AttackEffectResolutionResultReader.FinalizeTypedResult(triggered.Resolution);
            parent.Applied = parent.Applied || child.Applied;
            parent.Damage += Math.Max(child.Damage, 0);
            parent.HpDamage += Math.Max(child.HpDamage, 0);
            parent.Healing += Math.Max(child.Healing, 0);
            parent.ShieldAbsorbed += Math.Max(child.ShieldAbsorbed, 0);
            parent.ShieldBroken = parent.ShieldBroken || child.ShieldBroken;
            parent.DamageEvents = MergeArrays(parent.DamageEvents, child.DamageEvents);
            parent.EquipmentDurabilityEvents = MergeArrays(
                parent.EquipmentDurabilityEvents,
                child.EquipmentDurabilityEvents
            );
            parent.DispelEvents = MergeArrays(parent.DispelEvents, child.DispelEvents);
            parent.SaveResults = MergeArrays(parent.SaveResults, child.SaveResults);
            parent.Diagnostics = MergeArrays(parent.Diagnostics, child.Diagnostics);
            AddUniqueStringNames(parent.StatusEffectIds, child.StatusEffectIds);
            AddUniqueStringNames(parent.RemovedStatusEffectIds, child.RemovedStatusEffectIds);
            AddUniqueStringNames(parent.SourceStatusEffectIds, child.SourceStatusEffectIds);
            AddUniqueStringNames(parent.TerrainEffectIds, child.TerrainEffectIds);
            if (child.ExecuteOutcome != ExecuteOutcomeKind.None)
            {
                parent.ExecuteOutcome = child.ExecuteOutcome;
                parent.ExecuteStage = child.ExecuteStage;
            }
        }
        return parent;
    }

    private static T[] MergeArrays<T>(T[] first, T[] second)
    {
        int firstCount = first?.Length ?? 0;
        int secondCount = second?.Length ?? 0;
        if (firstCount == 0)
            return secondCount == 0 ? Array.Empty<T>() : (T[])second.Clone();
        if (secondCount == 0)
            return first;
        var merged = new T[firstCount + secondCount];
        Array.Copy(first, 0, merged, 0, firstCount);
        Array.Copy(second, 0, merged, firstCount, secondCount);
        return merged;
    }

    private static void AddUniqueStringNames(StringNameList target, StringNameList values)
    {
        if (target == null || values == null)
            return;
        foreach (StringName value in values)
            AddUniqueStringName(target, value);
    }

    private static BattleSaveContext BuildAfterHitSaveContext(
        AttackResolutionMetadata attackMetadata,
        AttackContext attackContext
    )
    {
        return new BattleSaveContext(
            attackMetadata.SkillId,
            attackContext?.SaveRollOverrides ?? Array.Empty<int>()
        );
    }

    private static bool ResultIncludesWeaponDamage(AttackEffectResolutionResult result)
    {
        foreach (DamageEventResult damageEvent in result.DamageEvents ?? Array.Empty<DamageEventResult>())
        {
            if (
                damageEvent.AddWeaponDice
                && damageEvent.WeaponDamageDice.Count > 0
                && damageEvent.WeaponDamageDice.Sides > 0
            )
            {
                return true;
            }
        }
        return false;
    }

    private static int ComputeWeaponHpDamage(AttackEffectResolutionResult result)
    {
        int total = 0;
        foreach (DamageEventResult damageEvent in result.DamageEvents ?? Array.Empty<DamageEventResult>())
        {
            if (
                damageEvent.AddWeaponDice
                && damageEvent.WeaponDamageDice.Count > 0
                && damageEvent.WeaponDamageDice.Sides > 0
            )
            {
                total += Math.Max(damageEvent.HpDamage, 0);
            }
        }
        return total;
    }

    private static EquipmentDurabilityEventResult[] MergeEquipmentDurabilityEvents(
        EquipmentDurabilityEventResult[] existingEvents,
        IReadOnlyList<BattleEquipmentAbilityDurabilityResult> durabilityResults
    )
    {
        if (durabilityResults == null || durabilityResults.Count == 0)
            return existingEvents ?? Array.Empty<EquipmentDurabilityEventResult>();

        var mergedEvents = new List<EquipmentDurabilityEventResult>(
            existingEvents ?? Array.Empty<EquipmentDurabilityEventResult>()
        );
        foreach (BattleEquipmentAbilityDurabilityResult durabilityResult in durabilityResults)
        {
            EquipmentDurabilityCommitResult commitResult = durabilityResult?.CommitResult;
            if (commitResult?.Resolved == true)
                mergedEvents.Add(BattleEquipmentDurabilityResolver.BuildEquipmentDurabilityEventResult(commitResult));
        }
        return mergedEvents.Count == 0 ? Array.Empty<EquipmentDurabilityEventResult>() : mergedEvents.ToArray();
    }

    private static void MergeAfterHitStatusResults(
        StringNameList targetStatusEffectIds,
        StringNameList sourceStatusEffectIds,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        IReadOnlyList<BattleEquipmentAbilityStatusActionResult> statusResults
    )
    {
        if (statusResults == null || statusResults.Count == 0)
            return;

        targetStatusEffectIds ??= new StringNameList();
        sourceStatusEffectIds ??= new StringNameList();
        foreach (BattleEquipmentAbilityStatusActionResult statusResult in statusResults)
        {
            if (statusResult?.Applied != true || statusResult.StatusId == "")
                continue;
            if (statusResult.TargetUnitId == sourceUnit?.unit_id)
                AddUniqueStringName(sourceStatusEffectIds, statusResult.StatusId);
            else if (statusResult.TargetUnitId == targetUnit?.unit_id || statusResult.TargetUnitId == "")
                AddUniqueStringName(targetStatusEffectIds, statusResult.StatusId);
        }
    }

    private static void AddUniqueStringName(StringNameList target, StringName value)
    {
        if (target == null || value == "" || target.Contains(value))
            return;
        target.Add(value);
    }

    internal BattleSpellControlMetadata ResolveSpellControlCheckTyped(
        BattleUnitState source_unit,
        GDictionary attack_context = null
    )
    {
        return ResolveSpellControlCheck(
            source_unit,
            SpellControlCheckContext.FromDictionary(attack_context)
        );
    }

    internal BattleSpellControlMetadata ResolveSpellControlCheckTyped(
        BattleUnitState source_unit,
        BattleState battle_state,
        StringName skill_id,
        bool dispatchEvents = true
    )
    {
        return ResolveSpellControlCheck(
            source_unit,
            SpellControlCheckContext.ForSkill(battle_state, skill_id, dispatchEvents)
        );
    }

    private BattleSpellControlMetadata ResolveSpellControlCheck(
        BattleUnitState source_unit,
        SpellControlCheckContext context
    )
    {
        if (source_unit == null)
        {
            return BattleSpellControlMetadata.Empty();
        }
        BattleSpellControlMetadata controlMetadata = ResolveSpellControlMetadata(
            source_unit,
            context
        );
        if (context.DispatchEvents)
        {
            DispatchSpellControlResolutionEvents(source_unit, controlMetadata, context);
        }
        return controlMetadata;
    }

    internal virtual BattleDamagePreviewResult PreviewDamageEffectTyped(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        CombatEffectDefinition effect_definition,
        DamageResolutionContext damage_context,
        BattleDamagePreviewRollMode roll_mode = BattleDamagePreviewRollMode.Average,
        BattleDamagePreviewSaveMode save_mode = BattleDamagePreviewSaveMode.Expected
    )
    {
        if (source_unit == null || target_unit == null || effect_definition == null)
        {
            return BattleDamagePreviewResult.Empty();
        }
        BattleDamagePreviewRollMode resolvedRollMode =
            roll_mode == BattleDamagePreviewRollMode.Unknown
                ? BattleDamagePreviewRollMode.Average
                : roll_mode;
        BattleDamagePreviewSaveMode resolvedSaveMode =
            save_mode == BattleDamagePreviewSaveMode.Unknown
                ? BattleDamagePreviewSaveMode.Expected
                : save_mode;
        StringName resolvedRollModeName = ToStringName(resolvedRollMode);
        StringName resolvedSaveModeName = ToStringName(resolvedSaveMode);
        BattleUnitState sourcePreview = source_unit.clone();
        BattleUnitState targetPreview = target_unit.clone();
        if (sourcePreview == null || targetPreview == null)
        {
            return BattleDamagePreviewResult.Empty();
        }

        DamageResolutionContext previewContextFlags =
            (damage_context ?? DamageResolutionContext.Empty()).WithDamageRollMode(
                resolvedRollModeName
            );
        DamageOutcomeResult damageOutcome = ResolveDamageOutcome(
            sourcePreview,
            targetPreview,
            effect_definition,
            previewContextFlags
        );
        if (damageOutcome.InvalidDamageTag)
        {
            return BattleDamagePreviewResult.Create(
                rollMode: resolvedRollModeName,
                saveMode: resolvedSaveModeName,
                shieldHpBefore: target_unit.GetShieldStateTyped().CurrentHp,
                shieldHpAfter: targetPreview.GetShieldStateTyped().CurrentHp,
                errorCode: damageOutcome.ErrorCode,
                damageOutcome: ProjectDamageOutcomePayload(damageOutcome),
                damageResult: new Dictionary<string, object>(StringComparer.Ordinal),
                saveEstimate: BattleDamagePreviewSaveEstimate.None(0),
                diagnostics: new List<object>
                {
                    BuildInvalidDamageTagDiagnostic(
                        source_unit,
                        target_unit,
                        effect_definition,
                        damageOutcome
                    ),
                },
                sourcePreviewAfter: sourcePreview,
                targetPreviewAfter: targetPreview
            );
        }

        int preSaveDamage = damageOutcome.ResolvedDamage;
        DamagePreviewSaveEstimate saveEstimate = BuildDamagePreviewSaveEstimate(
            sourcePreview,
            targetPreview,
            effect_definition,
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
        return BattleDamagePreviewResult.Create(
            applied: damageResult.HasAppliedDamage,
            rollMode: resolvedRollModeName,
            saveMode: resolvedSaveModeName,
            preSaveDamage: preSaveDamage,
            postSaveDamage: saveEstimate.DamageAfterSave,
            hpDamage: damageResult.HpDamage,
            damage: damageResult.Damage,
            incomingBudgetDamage: saveEstimate.DamageAfterSave,
            shieldAbsorbed: damageResult.ShieldAbsorbed,
            shieldBroken: damageResult.ShieldBroken,
            shieldHpBefore: target_unit.GetShieldStateTyped().CurrentHp,
            shieldHpAfter: targetPreview.GetShieldStateTyped().CurrentHp,
            damageOutcome: ProjectDamageOutcomePayload(damageOutcome),
            damageResult: ProjectAppliedDamagePayload(damageResult),
            saveEstimate: saveEstimate.ToPreviewSaveEstimate(),
            sourcePreviewAfter: sourcePreview,
            targetPreviewAfter: targetPreview
        );
    }

    internal virtual AttackEffectResolutionResult ResolveEffects(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        IEnumerable<CombatEffectDefinition> effect_definitions,
        DamageResolutionContext damage_context
    )
    {
        DamageResolutionContext resolvedContext =
            damage_context ?? DamageResolutionContext.Empty();
        AttackEffectResolutionResult result = ResolveEffectsDefinitionCore(
            source_unit,
            target_unit,
            ToEffectDefinitionList(effect_definitions),
            resolvedContext
        );
        ReconcileEquipmentProjectionAfterDurabilityEvents(
            target_unit,
            result,
            resolvedContext.DamageApplicationHookBatch
        );
        return result;
    }

    internal virtual AttackEffectResolutionResult ResolveEffects(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        IEnumerable<CombatEffectDefinition> effect_definitions
    )
    {
        return ResolveEffects(
            source_unit,
            target_unit,
            effect_definitions,
            DamageResolutionContext.Empty()
        );
    }

    private static IReadOnlyList<CombatEffectDefinition> ToEffectDefinitionList(
        IEnumerable<CombatEffectDefinition> effectDefinitions
    )
    {
        if (effectDefinitions == null)
        {
            return Array.Empty<CombatEffectDefinition>();
        }
        if (effectDefinitions is IReadOnlyList<CombatEffectDefinition> typedList)
        {
            return typedList;
        }
        var result = new List<CombatEffectDefinition>();
        foreach (CombatEffectDefinition effectDefinition in effectDefinitions)
        {
            if (effectDefinition != null)
            {
                result.Add(effectDefinition);
            }
        }
        return result.Count == 0 ? Array.Empty<CombatEffectDefinition>() : result;
    }

    private AttackEffectResolutionResult ResolveEffectsDefinitionCore(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        IReadOnlyList<CombatEffectDefinition> effect_definitions,
        DamageResolutionContext damage_context
    )
    {
        if (source_unit == null || target_unit == null)
        {
            return BuildEmptyResolutionResult();
        }

        DamageResolutionContext contextFlags = damage_context ?? DamageResolutionContext.Empty();
        BattleSaveContext saveContext = contextFlags.ToBattleSaveContext();
        int totalDamage = 0;
        int totalHealing = 0;
        int totalShieldAbsorbed = 0;
        var damageEvents = new List<DamageEventResult>();
        var equipmentDurabilityEvents = new List<EquipmentDurabilityEventResult>();
        var dispelEvents = new List<DispelEventResult>();
        var statusEffectIds = new GStringNameArray();
        var removedStatusEffectIds = new GStringNameArray();
        var sourceStatusEffectIds = new GStringNameArray();
        var terrainEffectIds = new GStringNameArray();
        var saveResults = new List<SaveResolutionResult>();
        var diagnostics = new List<ResolutionDiagnostic>();
        int totalHeightDelta = 0;
        bool shieldBroken = false;
        bool applied = false;
        bool blackStarWedgeTriggered = false;
        int executeStage = -1;
        StringName executeOutcome = "";

        foreach (CombatEffectDefinition effectDefinition in effect_definitions ?? Array.Empty<CombatEffectDefinition>())
        {
            if (effectDefinition == null || !DoesEffectTrigger(effectDefinition, contextFlags))
            {
                continue;
            }
            if (!TargetStatusRequirementPasses(source_unit, target_unit, effectDefinition))
            {
                continue;
            }

            BattleEffectKind effectKind = effectDefinition.EffectKind;
            if (effectKind == BattleEffectKind.Damage)
            {
                DamageOutcomeResult damageOutcome = ResolveDamageOutcome(
                    source_unit,
                    target_unit,
                    effectDefinition,
                    contextFlags,
                    out IReadOnlyList<EquipmentAbilityTaggedBonusDamageRoll> extraEquipmentBonusRolls
                );
                if (damageOutcome.InvalidDamageTag)
                {
                    diagnostics.Add(
                        new ResolutionDiagnostic
                        {
                            ErrorCode = damageOutcome.ErrorCode,
                            Message = damageOutcome.Reason,
                        }
                    );
                    continue;
                }
                BattleSaveResult damageSaveResult = BattleSaveResolver.ResolveSaveResult(
                    source_unit,
                    target_unit,
                    effectDefinition,
                    saveContext
                );
                if (damageSaveResult.HasSave)
                {
                    saveResults.Add(SaveResolutionFromBattleSave(damageSaveResult));
                }
                damageOutcome = WithSaveResult(
                    damageOutcome,
                    damageSaveResult,
                    effectDefinition
                );
                AppliedDamageResult damageResult = ApplyDamageToTargetResult(
                    target_unit,
                    damageOutcome,
                    source_unit,
                    contextFlags
                );
                int hpDamage = damageResult.Damage;
                totalDamage += hpDamage;
                totalShieldAbsorbed += damageResult.ShieldAbsorbed;
                damageEvents.Add(damageResult.Event);
                blackStarWedgeTriggered =
                    blackStarWedgeTriggered
                    || damageResult.LowLuckBlackStarWedgeTriggered;
                shieldBroken = shieldBroken || damageResult.ShieldBroken;
                applied = true;
                if (damageResult.HasAppliedDamage)
                {
                    GrantStatusOnHitToSource(source_unit, effectDefinition);
                }
                if (
                    ApplySaveFailureStatusFromDamage(
                        target_unit,
                        source_unit,
                        effectDefinition,
                        damageSaveResult,
                        out StringName saveFailureStatusId
                    )
                )
                {
                    AddUnique(statusEffectIds, saveFailureStatusId);
                }
                foreach (
                    CombatDamageSegmentDefinition extraSegment in
                        effectDefinition.ExtraDamageSegments
                            ?? Array.Empty<CombatDamageSegmentDefinition>()
                )
                {
                    DamageOutcomeResult extraSegmentOutcome =
                        ResolveExtraDamageSegmentOutcome(
                            source_unit,
                            target_unit,
                            effectDefinition,
                            extraSegment,
                            contextFlags
                        );
                    if (extraSegmentOutcome.InvalidDamageTag)
                    {
                        diagnostics.Add(
                            new ResolutionDiagnostic
                            {
                                ErrorCode = extraSegmentOutcome.ErrorCode,
                                Message = extraSegmentOutcome.Reason,
                            }
                        );
                        continue;
                    }
                    extraSegmentOutcome = WithSaveResult(
                        extraSegmentOutcome,
                        damageSaveResult,
                        effectDefinition
                    );
                    AppliedDamageResult extraSegmentDamageResult =
                        ApplyDamageToTargetResult(
                            target_unit,
                            extraSegmentOutcome,
                            source_unit,
                            contextFlags
                        );
                    totalDamage += extraSegmentDamageResult.Damage;
                    totalShieldAbsorbed += extraSegmentDamageResult.ShieldAbsorbed;
                    damageEvents.Add(extraSegmentDamageResult.Event);
                    blackStarWedgeTriggered =
                        blackStarWedgeTriggered
                        || extraSegmentDamageResult.LowLuckBlackStarWedgeTriggered;
                    shieldBroken = shieldBroken || extraSegmentDamageResult.ShieldBroken;
                    applied = true;
                }
                if (
                    TryResolveConditionalBonusWeaponDamageOutcome(
                        source_unit,
                        target_unit,
                        effectDefinition,
                        contextFlags,
                        out DamageOutcomeResult bonusWeaponOutcome
                    )
                )
                {
                    if (bonusWeaponOutcome.InvalidDamageTag)
                    {
                        diagnostics.Add(
                            new ResolutionDiagnostic
                            {
                                ErrorCode = bonusWeaponOutcome.ErrorCode,
                                Message = bonusWeaponOutcome.Reason,
                            }
                        );
                        continue;
                    }
                    bonusWeaponOutcome = WithSaveResult(
                        bonusWeaponOutcome,
                        damageSaveResult,
                        effectDefinition
                    );
                    AppliedDamageResult bonusWeaponDamageResult =
                        ApplyDamageToTargetResult(
                            target_unit,
                            bonusWeaponOutcome,
                            source_unit,
                            contextFlags
                        );
                    totalDamage += bonusWeaponDamageResult.Damage;
                    totalShieldAbsorbed += bonusWeaponDamageResult.ShieldAbsorbed;
                    damageEvents.Add(bonusWeaponDamageResult.Event);
                    blackStarWedgeTriggered =
                        blackStarWedgeTriggered
                        || bonusWeaponDamageResult.LowLuckBlackStarWedgeTriggered;
                    shieldBroken = shieldBroken || bonusWeaponDamageResult.ShieldBroken;
                    applied = true;
                }
                foreach (
                    EquipmentAbilityTaggedBonusDamageRoll extraEquipmentBonusRoll in
                        extraEquipmentBonusRolls ?? Array.Empty<EquipmentAbilityTaggedBonusDamageRoll>()
                )
                {
                    DamageOutcomeResult extraDamageOutcome =
                        ResolveEquipmentAbilityBonusDamageOutcome(
                            source_unit,
                            target_unit,
                            effectDefinition,
                            contextFlags,
                            extraEquipmentBonusRoll
                        );
                    if (extraDamageOutcome.InvalidDamageTag)
                    {
                        diagnostics.Add(
                            new ResolutionDiagnostic
                            {
                                ErrorCode = extraDamageOutcome.ErrorCode,
                                Message = extraDamageOutcome.Reason,
                            }
                        );
                        continue;
                    }
                    AppliedDamageResult extraDamageResult = ApplyDamageToTargetResult(
                        target_unit,
                        extraDamageOutcome,
                        source_unit,
                        contextFlags
                    );
                    totalDamage += extraDamageResult.Damage;
                    totalShieldAbsorbed += extraDamageResult.ShieldAbsorbed;
                    damageEvents.Add(extraDamageResult.Event);
                    blackStarWedgeTriggered =
                        blackStarWedgeTriggered
                        || extraDamageResult.LowLuckBlackStarWedgeTriggered;
                    shieldBroken = shieldBroken || extraDamageResult.ShieldBroken;
                    applied = true;
                }
            }
            else if (effectKind == BattleEffectKind.EquipmentDurabilityDamage)
            {
                EquipmentDurabilityDamageEffectResult durabilityResult =
                    _equipmentDurabilityResolver.ApplyEquipmentDurabilityDamageEffect(
                        source_unit,
                        target_unit,
                        effectDefinition,
                        contextFlags,
                        totalDamage,
                        totalShieldAbsorbed
                    );
                if (durabilityResult.HasEvent)
                {
                    equipmentDurabilityEvents.Add(durabilityResult.Event);
                    if (durabilityResult.SaveResult.HasSave)
                    {
                        saveResults.Add(durabilityResult.SaveResult.Result);
                    }
                    if (durabilityResult.DurabilityLoss > 0 || durabilityResult.Destroyed)
                    {
                        applied = true;
                    }
                }
            }
            else if (effectKind == BattleEffectKind.Heal)
            {
                int healAmount = ResolveHealAmount(source_unit, effectDefinition);
                healAmount = BattleStatusModifierRules.ApplyHealMultiplier(target_unit, healAmount);
                if (healAmount > 0)
                {
                    ApplyHealing(target_unit, healAmount);
                }
                totalHealing += healAmount;
                applied = true;
            }
            else if (effectKind == BattleEffectKind.StaminaRestore)
            {
                ApplyStaminaRestore(source_unit, target_unit, effectDefinition);
                applied = true;
            }
            else if (effectKind == BattleEffectKind.HealFatal)
            {
                int healAmount = ResolveHealFatalAmount(
                    source_unit,
                    target_unit,
                    effectDefinition,
                    contextFlags
                );
                if (healAmount > 0)
                {
                    ApplyHealing(target_unit, healAmount);
                    totalHealing += healAmount;
                    applied = true;
                }
            }
            else if (effectKind == BattleEffectKind.EraseStatus)
            {
                if (BattleTemporalStatusService.IsTemporalReleaseEffect(effectDefinition))
                {
                    List<StringName> releasedStatusIds =
                        BattleTemporalStatusService.ApplyTemporalReleaseEffects(
                            source_unit,
                            target_unit,
                            effectDefinition
                        );
                    foreach (StringName releasedStatusId in releasedStatusIds)
                    {
                        if (!removedStatusEffectIds.Contains(releasedStatusId))
                        {
                            removedStatusEffectIds.Add(releasedStatusId);
                        }
                        applied = true;
                    }
                }
                else
                {
                    StringName erasedStatusId = ProgressionDataUtils.to_string_name(
                        effectDefinition.StatusId
                    );
                    if (erasedStatusId == "")
                    {
                        erasedStatusId = ProgressionDataUtils.to_string_name(
                            effectDefinition.TriggerStatusId
                        );
                    }
                    if (erasedStatusId != "" && target_unit.HasStatusEffect(erasedStatusId))
                    {
                        target_unit.EraseStatusEffect(erasedStatusId);
                        applied = true;
                    }
                }
            }
            else if (effectKind == BattleEffectKind.CleanseHarmful)
            {
                GStringNameArray removedStatusIds = new();
                foreach (StringName statusId in target_unit.GetSortedStatusEffectIdsTyped())
                {
                    if (
                        BattleStatusSemanticTable.IsCleansableHarmfulStatusEntry(
                            target_unit.GetStatusEffect(statusId)
                        )
                    )
                    {
                        removedStatusIds.Add(statusId);
                    }
                }
                foreach (StringName statusId in removedStatusIds)
                {
                    target_unit.EraseStatusEffect(statusId);
                }
                if (removedStatusIds.Count > 0)
                {
                    applied = true;
                }
            }
            else if (effectKind == BattleEffectKind.DispelMagic)
            {
                DispelEventResult dispelResult = ApplyDispelMagicEffect(
                    source_unit,
                    target_unit,
                    effectDefinition
                );
                IReadOnlyList<StringName> removedIds = dispelResult.RemovedStatusIds is { } ids
                    ? ids
                    : System.Array.Empty<StringName>();
                if (removedIds.Count > 0)
                {
                    dispelEvents.Add(dispelResult);
                    foreach (StringName removedId in removedIds)
                    {
                        if (removedId != "" && !removedStatusEffectIds.Contains(removedId))
                        {
                            removedStatusEffectIds.Add(removedId);
                        }
                    }
                    applied = true;
                }
            }
            else if (
                effectKind == BattleEffectKind.Status
                || effectKind == BattleEffectKind.ApplyStatus
            )
            {
                BattleSaveResult statusSaveResult = BattleSaveResolver.ResolveSaveResult(
                    source_unit,
                    target_unit,
                    effectDefinition,
                    saveContext
                );
                if (statusSaveResult.HasSave)
                {
                    saveResults.Add(SaveResolutionFromBattleSave(statusSaveResult));
                }
                if (DoesSaveBlockEffect(statusSaveResult))
                {
                    continue;
                }
                StringName resolvedStatusId = ResolveStatusIdForSave(effectDefinition, statusSaveResult);
                resolvedStatusId = BattleTemporalStatusService.ApplyEliteBossStasisDowngrade(
                    target_unit,
                    resolvedStatusId
                );
                if (
                    resolvedStatusId != ""
                    && ApplyStatusEffect(
                        target_unit,
                        source_unit,
                        effectDefinition,
                        resolvedStatusId,
                        contextFlags
                    )
                )
                {
                    AddUnique(statusEffectIds, resolvedStatusId);
                    applied = true;
                }
            }
            else if (effectKind == BattleEffectKind.Execute)
            {
                ExecuteEffectResult executeResult = ResolveExecuteEffect(
                    source_unit,
                    target_unit,
                    effectDefinition,
                    contextFlags,
                    statusEffectIds,
                    saveResults
                );
                if (executeResult.ExecuteStage >= 0)
                {
                    executeStage = executeResult.ExecuteStage;
                }
                if (!IsEmpty(executeResult.ExecuteOutcome))
                {
                    executeOutcome = executeResult.ExecuteOutcome;
                }
                if (executeResult.Applied)
                {
                    applied = true;
                }
                foreach (AppliedDamageResult damageResult in executeResult.DamageResults)
                {
                    totalDamage += damageResult.Damage;
                    totalShieldAbsorbed += damageResult.ShieldAbsorbed;
                    shieldBroken = shieldBroken || damageResult.ShieldBroken;
                    damageEvents.Add(damageResult.Event);
                }
            }
            else if (effectKind == BattleEffectKind.GradedSaveExecute)
            {
                GradedSaveExecuteEffectResult gradedSaveResult =
                    ResolveGradedSaveExecuteEffect(
                        source_unit,
                        target_unit,
                        effectDefinition,
                        contextFlags,
                        statusEffectIds,
                        saveResults
                    );
                if (gradedSaveResult.Applied)
                {
                    applied = true;
                }
                foreach (ResolutionDiagnostic diagnostic in gradedSaveResult.Diagnostics)
                {
                    diagnostics.Add(diagnostic);
                }
                foreach (AppliedDamageResult damageResult in gradedSaveResult.DamageResults)
                {
                    totalDamage += damageResult.Damage;
                    totalShieldAbsorbed += damageResult.ShieldAbsorbed;
                    shieldBroken = shieldBroken || damageResult.ShieldBroken;
                    blackStarWedgeTriggered =
                        blackStarWedgeTriggered
                        || damageResult.LowLuckBlackStarWedgeTriggered;
                    damageEvents.Add(damageResult.Event);
                }
            }
        }

        target_unit.SetCurrentHp(target_unit.GetCurrentHp());
        if (
            blackStarWedgeTriggered
            && target_unit.IsAlive()
            && ApplyLowLuckBlackStarWedgeExposed(source_unit)
        )
        {
            sourceStatusEffectIds.Add(LowLuckRelicRules.ToStringName(LowLuckRelicStatusKind.BlackStarWedgeExposed));
        }

        string errorCode = diagnostics.Count > 0 ? diagnostics[0].ErrorCode : "";
        return AttackEffectResolutionResultReader.FinalizeTypedResult(
            new AttackEffectResolutionResult
            {
                Applied = applied,
                Damage = totalDamage,
                HpDamage = totalDamage,
                Healing = totalHealing,
                ShieldAbsorbed = totalShieldAbsorbed,
                ShieldBroken = shieldBroken,
                DamageEvents = damageEvents.ToArray(),
                EquipmentDurabilityEvents = equipmentDurabilityEvents.ToArray(),
                DispelEvents = dispelEvents.ToArray(),
                StatusEffectIds = statusEffectIds,
                RemovedStatusEffectIds = removedStatusEffectIds,
                SourceStatusEffectIds = sourceStatusEffectIds,
                TerrainEffectIds = terrainEffectIds,
                SaveResults = saveResults.ToArray(),
                HeightDelta = totalHeightDelta,
                Diagnostics = diagnostics.ToArray(),
                ExecuteStage = executeStage,
                ExecuteOutcome =
                    executeStage >= 0
                        ? AttackEffectResolutionResultReader.ParseExecuteOutcome(executeOutcome)
                        : ExecuteOutcomeKind.None,
                ErrorCode = errorCode,
                SkillId = contextFlags.SkillId,
                AttackCheck = new AttackCheckInput(skillId: contextFlags.SkillId),
                TraitTriggerResults = Array.Empty<TraitTriggerEventResult>(),
            }
        );
    }

    internal AttackEffectResolutionResult ResolveFallDamageResult(
        BattleUnitState targetUnit,
        int fallLayers,
        BattleState battleState = null
    )
    {
        if (targetUnit == null || fallLayers <= 0 || !targetUnit.IsAlive())
        {
            return BuildEmptyResolutionResult();
        }
        int maxHp = GetAttributeValue(targetUnit, AttributeService.ToStringName(AttributeIdKind.HpMax));
        if (maxHp <= 0)
        {
            maxHp = Math.Max(targetUnit.GetCurrentHp(), 1);
        }
        int damagePerLayer = Math.Max((maxHp + 19) / 20, 1);
        AppliedDamageResult damageResult = ApplyDamageToTargetResult(
            targetUnit,
            damagePerLayer * fallLayers,
            battleState: battleState
        );
        targetUnit.SetCurrentHp(targetUnit.GetCurrentHp());
        return BuildEnvironmentalDamageResult(damageResult);
    }

    private AppliedDamageResult ApplyDamageToTargetResult(
        BattleUnitState targetUnit,
        int rawDamage,
        BattleUnitState sourceUnit = null,
        BattleState battleState = null
    )
    {
        int normalizedDamage = Math.Max(rawDamage, 0);
        if (targetUnit == null || normalizedDamage <= 0)
        {
            return BuildAppliedDamageResult(
                DamageApplicationInput.Empty,
                0,
                0,
                false
            );
        }
        DamageEventResult damageOutcome = new()
        {
            DamageTag = "",
            MitigationTier = MitigationTierKind.Normal,
            MitigationSources = Array.Empty<MitigationSourceResult>(),
            BaseDamage = normalizedDamage,
            OffenseMultiplier = 1.0,
            BypassShield = false,
            ShieldAbsorptionPercent = 100.0,
            MinHpAfterDamage = 0,
            ResolvedDamage = normalizedDamage,
        };
        return ApplyDamageToTargetResult(
            targetUnit,
            DamageApplicationInput.Create(
                damageOutcome,
                normalizedDamage,
                shieldAbsorptionPercent: 100.0
            ),
            sourceUnit,
            DamageResolutionContext.Empty().WithBattleState(battleState)
        );
    }

    internal int ApplyDirectDamageToTargetTyped(
        BattleUnitState targetUnit,
        int rawDamage,
        BattleUnitState sourceUnit = null,
        BattleState battleState = null
    )
    {
        return ApplyDamageToTargetResult(
            targetUnit,
            rawDamage,
            sourceUnit,
            battleState
        ).Damage;
    }

    internal int ApplyTaggedDirectDamageToTargetTyped(
        BattleUnitState targetUnit,
        int rawDamage,
        StringName damageTag,
        BattleUnitState sourceUnit = null,
        BattleState battleState = null
    )
    {
        int normalizedDamage = Math.Max(rawDamage, 0);
        StringName normalizedDamageTag = ProgressionDataUtils.to_string_name(damageTag);
        if (
            targetUnit == null
            || normalizedDamage <= 0
            || DamageTagContentRules.ToDamageTagKind(normalizedDamageTag) == DamageTagKind.Unknown
        )
        {
            return 0;
        }

        MitigationTierResolution mitigation = ResolveMitigationTierResult(
            targetUnit,
            normalizedDamageTag
        );
        int resolvedDamage = normalizedDamage;
        if (mitigation.Tier == MitigationTierImmune)
        {
            resolvedDamage = 0;
        }
        else if (mitigation.Tier == MitigationTierHalf)
        {
            resolvedDamage /= 2;
        }
        else if (mitigation.Tier == MitigationTierDouble)
        {
            resolvedDamage *= 2;
        }

        DamageEventResult damageOutcome = new()
        {
            DamageTag = normalizedDamageTag,
            MitigationTier = AttackEffectResolutionResultReader.ParseMitigationTier(
                mitigation.Tier
            ),
            MitigationSources = mitigation.Sources,
            BaseDamage = normalizedDamage,
            OffenseMultiplier = 1.0,
            RolledDamage = normalizedDamage,
            TierAdjustedDamage = resolvedDamage,
            ResolvedDamage = resolvedDamage,
            BypassShield = false,
            ShieldAbsorptionPercent = 100.0,
            MinHpAfterDamage = 0,
            FixedMitigationSourceLabels = Array.Empty<string>(),
            FixedMitigationTotal = 0,
            FullyAbsorbedByMitigation = false,
            SourceBoundWeaponBonusSkillIds = Array.Empty<StringName>(),
            TraitTriggerResults = Array.Empty<TraitTriggerEventResult>(),
        };
        return ApplyDamageToTargetResult(
            targetUnit,
            DamageApplicationInput.Create(
                damageOutcome,
                resolvedDamage,
                shieldAbsorptionPercent: 100.0
            ),
            sourceUnit,
            DamageResolutionContext.Empty().WithBattleState(battleState)
        ).Damage;
    }

    internal int ApplyDirectDamageToTargetTyped(
        BattleUnitState targetUnit,
        GDictionary resolvedDamageInput,
        BattleUnitState sourceUnit = null,
        BattleState battleState = null
    )
    {
        return ApplyDamageToTargetResult(
            targetUnit,
            DamageApplicationInput.FromDictionary(resolvedDamageInput),
            sourceUnit,
            DamageResolutionContext.Empty().WithBattleState(battleState)
        ).Damage;
    }

    private static bool DoesEffectTrigger(
        CombatEffectDefinition effectDefinition,
        DamageResolutionContext context
    )
    {
        if (effectDefinition == null)
        {
            return false;
        }
        return effectDefinition.TriggerEventKind switch
        {
            CombatEffectTriggerEvent.None => true,
            CombatEffectTriggerEvent.CriticalHit => context.CriticalHit,
            CombatEffectTriggerEvent.OrdinaryHit => context.AttackSuccess && !context.CriticalHit,
            CombatEffectTriggerEvent.SecondaryHit => context.SecondaryHitSuccess,
            _ => false,
        };
    }

    private static bool TargetStatusRequirementPasses(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDefinition effectDefinition
    )
    {
        StringName requiredStatusId = ProgressionDataUtils.to_string_name(
            effectDefinition?.RequiredTargetStatusId ?? ""
        );
        if (requiredStatusId == "")
        {
            return true;
        }

        BattleStatusEffectState statusEntry = targetUnit?.GetStatusEffect(requiredStatusId);
        if (statusEntry == null)
        {
            return false;
        }

        int requiredStacks = Math.Max(effectDefinition.RequiredTargetStatusMinStacks, 1);
        if (Math.Max(statusEntry.stacks, 0) < requiredStacks)
        {
            return false;
        }

        StringName sourceSelector = ProgressionDataUtils.to_string_name(
            effectDefinition.RequiredTargetStatusSourceSelector
        );
        if (sourceSelector == "")
        {
            return true;
        }
        if (
            sourceSelector == "source"
            || sourceSelector == "attacker"
            || sourceSelector == "owner"
            || sourceSelector == "caster"
        )
        {
            return sourceUnit != null
                && ProgressionDataUtils.to_string_name(statusEntry.source_unit_id) == sourceUnit.unit_id;
        }
        return false;
    }

    public bool _resolve_secondary_hit(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        AttackContext attack_context,
        int dc_base = 10
    )
    {
        if (source_unit == null || target_unit == null)
        {
            return false;
        }
        int strMod = GetUnitBaseAttributeModifier(source_unit, UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Strength));
        int conMod = GetUnitBaseAttributeModifier(target_unit, UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Constitution));
        int dc = dc_base + strMod;
        _hit_resolver ??= new BattleHitResolver();
        int saveRoll = _hit_resolver.RollAttackDie(
            20,
            false,
            attack_context ?? new AttackContext()
        );
        int saveBonus = GetTargetSecondaryHitSaveBonus(target_unit);
        return saveRoll + conMod + saveBonus < dc;
    }

    public virtual int _roll_damage_die(int dice_sides)
    {
        return TrueRandomSeedService.RandiRange(1, Math.Max(dice_sides, 1));
    }

    private static int GetIntParam(GDictionary @params, StringName key, int fallback = 0)
    {
        if (@params == null || !@params.ContainsKey(key))
            return fallback;
        return (int)@params[key];
    }

    private static int GetIntParam(
        IReadOnlyDictionary<string, object> @params,
        StringName key,
        int fallback = 0
    )
    {
        if (!TryGetStatusParamTyped(@params, key, out object rawValue))
            return fallback;
        return rawValue switch
        {
            int intValue => intValue,
            long longValue => (int)longValue,
            _ => fallback,
        };
    }

    private static double GetFloatParam(GDictionary @params, StringName key, double fallback = 0.0)
    {
        if (@params == null || !@params.ContainsKey(key))
            return fallback;
        return (double)@params[key];
    }

    private static double GetFloatParam(
        IReadOnlyDictionary<string, object> @params,
        StringName key,
        double fallback = 0.0
    )
    {
        if (!TryGetStatusParamTyped(@params, key, out object rawValue))
            return fallback;
        return rawValue switch
        {
            float floatValue => floatValue,
            double doubleValue => doubleValue,
            int intValue => intValue,
            long longValue => longValue,
            _ => fallback,
        };
    }

    private static StringName GetStringNameParam(
        GDictionary @params,
        StringName key,
        StringName fallback = default
    )
    {
        if (@params == null || !@params.ContainsKey(key))
            return fallback ?? new StringName("");
        return ProgressionDataUtils.to_string_name(@params[key]);
    }

    private static StringName GetStringNameParam(
        IReadOnlyDictionary<string, object> @params,
        StringName key,
        StringName fallback = default
    )
    {
        if (!TryGetStatusParamTyped(@params, key, out object rawValue))
            return fallback ?? new StringName("");
        StringName normalized = ProgressionDataUtils.to_string_name(rawValue);
        return normalized != "" ? normalized : fallback ?? new StringName("");
    }

    private static GArray GetArrayParam(
        GDictionary @params,
        StringName key,
        GArray fallback = null
    )
    {
        if (@params == null || !@params.ContainsKey(key))
            return fallback ?? new GArray();
        try
        {
            return (GArray)@params[key];
        }
        catch
        {
            return fallback ?? new GArray();
        }
    }

    private static IReadOnlyList<object> GetArrayParam(
        IReadOnlyDictionary<string, object> @params,
        StringName key
    )
    {
        if (!TryGetStatusParamTyped(@params, key, out object rawValue))
            return Array.Empty<object>();
        return rawValue as IReadOnlyList<object> ?? Array.Empty<object>();
    }

    private AppliedDamageResult ApplyDamageToTargetResult(
        BattleUnitState targetUnit,
        GDictionary damageOutcome,
        BattleUnitState sourceUnit = null
    )
    {
        return ApplyDamageToTargetResult(
            targetUnit,
            DamageApplicationInput.FromDictionary(damageOutcome),
            sourceUnit
        );
    }

    private AppliedDamageResult ApplyDamageToTargetResult(
        BattleUnitState targetUnit,
        DamageOutcomeResult damageOutcome,
        BattleUnitState sourceUnit = null,
        DamageResolutionContext damageContext = null
    )
    {
        return ApplyDamageToTargetResult(
            targetUnit,
            damageOutcome.ToDamageApplicationInput(),
            sourceUnit,
            damageContext
        );
    }

    private AppliedDamageResult ApplyDamageToTargetResult(
        BattleUnitState targetUnit,
        DamageApplicationInput damageInput,
        BattleUnitState sourceUnit = null,
        DamageResolutionContext damageContext = null
    )
    {
        DamageEventResult applicationEvent = damageInput.Event;
        int normalizedDamage = damageInput.ResolvedDamage;
        if (targetUnit == null || normalizedDamage <= 0)
        {
            return BuildAppliedDamageResult(
                damageInput with { Event = applicationEvent },
                0,
                0,
                false
            );
        }

        DamageApplicationProjection projection = ProjectDamageApplication(targetUnit, damageInput);
        if (_damage_application_hook != null && !damageInput.SuppressDamageApplicationHook)
        {
            IReadOnlyList<Vector2I> currentDamageEventAreaCells =
                BuildCurrentDamageEventAreaCells(targetUnit);
            BattleDamageApplicationHookResult hookResult =
                _damage_application_hook.BeforeDamageResolved(
                    new BattleDamageApplicationHookContext
                    {
                        SourceUnit = sourceUnit,
                        TargetUnit = targetUnit,
                        DamageInput = damageInput,
                        Projection = projection,
                        Batch = damageContext?.DamageApplicationHookBatch,
                        Origin = damageContext?.DamageApplicationHookOrigin
                            ?? BattleEffectOrigin.PlayerCommand(),
                        CurrentDamageEventAreaCells = currentDamageEventAreaCells,
                    }
                );
            if (hookResult.CancelDamage)
            {
                return BuildAppliedDamageResult(
                    damageInput with { Event = applicationEvent },
                    0,
                    0,
                    false
                );
            }
            if (hookResult.HasModifiedResolvedDamage)
            {
                damageInput = damageInput.WithResolvedDamage(hookResult.ModifiedResolvedDamage);
                applicationEvent = damageInput.Event;
                normalizedDamage = damageInput.ResolvedDamage;
                projection = ProjectDamageApplication(targetUnit, damageInput);
            }
            if (hookResult.StateMayHaveChanged)
            {
                projection = ProjectDamageApplication(targetUnit, damageInput);
            }
        }

        bool bypassShield = damageInput.BypassShield;
        bool bypassDeathPrevention = damageInput.BypassDeathPrevention;
        int minHpAfterDamage = damageInput.MinHpAfterDamage;
        int hpBeforeDamage = Math.Max(targetUnit.GetCurrentHp(), 0);
        if (!bypassShield)
        {
            targetUnit.NormalizeShieldState();
        }

        int shieldAbsorbed = projection.ShieldAbsorbed;
        bool shieldBroken = false;
        if (!bypassShield && shieldAbsorbed > 0)
        {
            BattleUnitShieldDrainResult drainResult =
                targetUnit.DrainShieldTyped(projection.ShieldDrain);
            shieldBroken = shieldAbsorbed > 0 && drainResult.Depleted;
        }

        int hpDamage = projection.HpDamage;
        if (hpDamage > 0)
        {
            int maxHp = GetAttributeValue(targetUnit, AttributeService.ToStringName(AttributeIdKind.HpMax));
            if (maxHp > 0 && hpDamage * 10 >= maxHp * 6)
            {
                RecordLastStandMastery(targetUnit, sourceUnit, "critical_survival", 20);
            }
            int projectedHp = targetUnit.GetCurrentHp() - hpDamage;
            if (projectedHp <= minHpAfterDamage)
            {
                if (minHpAfterDamage > 0)
                {
                    targetUnit.SetCurrentHp(Math.Min(
                        Math.Max(projectedHp, minHpAfterDamage),
                        targetUnit.GetCurrentHp()
                    ));
                }
                else if (bypassDeathPrevention)
                {
                    targetUnit.MarkDead();
                }
                else
                {
                    TraitTriggerResultSnapshot fatalTraitResult =
                        ResolveFatalDamageTraitResult(
                            targetUnit,
                            sourceUnit,
                            hpDamage,
                            projectedHp
                        );
                    if (
                        fatalTraitResult.Triggered
                        && fatalTraitResult.ClampToHp > 0
                    )
                    {
                        targetUnit.SetCurrentHp(Math.Max(
                            fatalTraitResult.ClampToHp,
                            1
                        ));
                        AppendTraitTriggerResult(ref applicationEvent, fatalTraitResult);
                    }
                    else if (TryGetBlockingDeathWard(targetUnit, applicationEvent, out _))
                    {
                        targetUnit.MarkDead();
                        if (
                            !TriggerLastStand(
                                targetUnit,
                                sourceUnit,
                                damageContext?.BattleState
                            )
                        )
                        {
                            targetUnit.MarkDead();
                        }
                    }
                    else
                    {
                        targetUnit.MarkDead();
                    }
                }
            }
            else
            {
                targetUnit.SetCurrentHp(projectedHp);
            }
        }

        int actualHpDamage = Math.Max(hpBeforeDamage - Math.Max(targetUnit.GetCurrentHp(), 0), 0);
        IBattleEquipmentCombatReactionSink equipmentAbilityReactions =
            _equipment_ability_reaction_sink;
        if (actualHpDamage > 0 && sourceUnit != null && equipmentAbilityReactions != null)
        {
            equipmentAbilityReactions.ResolveDamageApplied(
                new BattleEquipmentAbilityDamageAppliedContext
                {
                    SourceUnit = sourceUnit,
                    TargetUnit = targetUnit,
                    BattleState = damageContext?.BattleState,
                    HpDamage = actualHpDamage,
                }
            );
        }
        return BuildAppliedDamageResult(
            damageInput with { Event = applicationEvent },
            actualHpDamage,
            shieldAbsorbed,
            shieldBroken
        );
    }

    private static IReadOnlyList<Vector2I> BuildCurrentDamageEventAreaCells(
        BattleUnitState targetUnit
    )
    {
        if (targetUnit == null)
            return Array.Empty<Vector2I>();
        BattleUnitGeometryReadView geometry =
            targetUnit.GetGeometryReadViewTyped();
        if (
            !geometry.OccupiedCoords.IsPresent
            || geometry.OccupiedCoords.Count == 0
        )
        {
            return Array.AsReadOnly(new[] { geometry.AnchorCoord });
        }

        var cells = new List<Vector2I>();
        foreach (Vector2I cell in geometry.OccupiedCoords)
            if (!cells.Contains(cell))
                cells.Add(cell);
        return cells.Count == 0 ? Array.Empty<Vector2I>() : Array.AsReadOnly(cells.ToArray());
    }

    private static bool TryGetBlockingDeathWard(
        BattleUnitState targetUnit,
        DamageEventResult damageEvent,
        out BattleStatusEffectState deathWardEntry
    )
    {
        deathWardEntry = targetUnit?.GetStatusEffect("death_ward");
        if (deathWardEntry == null)
        {
            return false;
        }
        DeathResolutionContext deathContext = ResolveDeathContext(damageEvent);
        int protectionPriority = ResolveDeathPreventionPriority(deathWardEntry);
        return BattleDeathResolutionRules.CanDeathPreventionBlock(
            deathContext,
            protectionPriority
        );
    }

    private static DeathResolutionContext ResolveDeathContext(DamageEventResult damageEvent)
    {
        StringName deathSource = ProgressionDataUtils.to_string_name(damageEvent.DeathSource);
        int deathSourcePriority = Math.Max(damageEvent.DeathSourcePriority, 0);
        if (deathSource != "" || deathSourcePriority > 0)
        {
            return new DeathResolutionContext(
                deathSource == "" ? BattleDeathResolutionRules.DamageDeathSource : deathSource,
                deathSourcePriority
            );
        }
        return BattleDeathResolutionRules.NormalFatalContext();
    }

    private static int ResolveDeathPreventionPriority(BattleStatusEffectState statusEntry)
    {
        if (statusEntry == null)
        {
            return 0;
        }
        if (statusEntry.death_prevention_priority > 0)
        {
            return statusEntry.death_prevention_priority;
        }
        if (
            ProgressionDataUtils.to_string_name(statusEntry.status_id) == "death_ward"
            && ProgressionDataUtils.to_string_name(statusEntry.source_skill_id) == "warrior_last_stand"
        )
        {
            return BattleDeathResolutionRules.NormalFatalPriority;
        }
        return 0;
    }

    private TraitTriggerResultSnapshot ResolveCritTraitResult(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDefinition effectDefinition,
        bool criticalHit
    )
    {
        if (!criticalHit)
        {
            return TraitTriggerResultSnapshot.FromAttackTraitTriggerResult(
                new AttackTraitTriggerResult(
                    @event: TraitTriggerContentRules.ToStringName(TraitTriggerKind.OnCrit)
                )
            );
        }
        BattleWeaponProjectionValues weaponProjection = sourceUnit != null
            ? sourceUnit.GetWeaponProjectionReadViewTyped().Values
            : BattleWeaponProjectionValues.Clear;
        return TraitTriggerResultSnapshot.FromAttackTraitTriggerResult(
            _trait_trigger_hooks.OnCrit(
                sourceUnit,
                targetUnit,
                criticalHit,
                ShouldAddWeaponDice(effectDefinition),
                weaponProjection.AttackRange,
                GetCurrentWeaponDamageDiceSides(weaponProjection)
            )
        );
    }

    private TraitTriggerResultSnapshot ResolveFatalDamageTraitResult(
        BattleUnitState targetUnit,
        BattleUnitState sourceUnit,
        int hpDamage,
        int projectedHp
    )
    {
        return TraitTriggerResultSnapshot.FromAttackTraitTriggerResult(
            _trait_trigger_hooks.OnFatalDamage(
                targetUnit,
                sourceUnit,
                hpDamage,
                projectedHp
            )
        );
    }

    private int GetUnitBaseAttributeModifier(BattleUnitState unitState, StringName attributeId)
    {
        if (unitState?.attribute_snapshot == null || attributeId == "")
        {
            return 0;
        }
        StringName modifierId = AttributeSnapshot.GetBaseAttributeModifierId(attributeId);
        return modifierId == "" ? 0 : GetAttributeValue(unitState, modifierId);
    }

    private static int GetAttributeValue(BattleUnitState unitState, StringName attributeId)
    {
        return unitState?.attribute_snapshot != null
            ? unitState.attribute_snapshot.GetValue(attributeId)
            : 0;
    }

}
