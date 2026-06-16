using System;
using System.Collections.Generic;
using Godot;

public sealed class BattleSkillResolutionPolicy
{
    public IReadOnlyList<StringName> TargetUnitIds { get; }
    public CombatCastVariantDef UnitCastVariant { get; }
    public CombatCastVariantDef GroundCastVariant { get; }
    public CombatCastVariantDef CommandCastVariant { get; }
    public CombatCastVariantDef UnitExecutionCastVariant { get; }
    public CombatCastVariantDef ExecutionCastVariant { get; }
    public bool RoutesToUnitTargeting { get; }
    public string OptionErrorMessage { get; }
    public bool OptionAllowed => string.IsNullOrEmpty(OptionErrorMessage);
    public IReadOnlyList<CombatEffectDef> EffectDefs { get; }
    public bool UsesFateAttack { get; }
    public bool ForceHitNoCrit { get; }
    public StringName FatePreviewMode { get; }

    public BattleSkillResolutionPolicy(
        IEnumerable<StringName> targetUnitIds,
        CombatCastVariantDef unitCastVariant,
        CombatCastVariantDef groundCastVariant,
        CombatCastVariantDef commandCastVariant,
        CombatCastVariantDef unitExecutionCastVariant,
        CombatCastVariantDef executionCastVariant,
        bool routesToUnitTargeting,
        string optionErrorMessage,
        IEnumerable<CombatEffectDef> effectDefs,
        bool usesFateAttack,
        bool forceHitNoCrit,
        StringName fatePreviewMode
    )
    {
        TargetUnitIds = new List<StringName>(targetUnitIds ?? Array.Empty<StringName>());
        UnitCastVariant = unitCastVariant;
        GroundCastVariant = groundCastVariant;
        CommandCastVariant = commandCastVariant;
        UnitExecutionCastVariant = unitExecutionCastVariant;
        ExecutionCastVariant = executionCastVariant;
        RoutesToUnitTargeting = routesToUnitTargeting;
        OptionErrorMessage = optionErrorMessage ?? "";
        EffectDefs = new List<CombatEffectDef>(effectDefs ?? Array.Empty<CombatEffectDef>());
        UsesFateAttack = usesFateAttack;
        ForceHitNoCrit = forceHitNoCrit;
        FatePreviewMode = fatePreviewMode;
    }

    internal Godot.Collections.Dictionary ToDictionary()
    {
        return new Godot.Collections.Dictionary
        {
            ["target_unit_ids"] = ToStringNameArray(TargetUnitIds),
            ["unit_cast_variant"] = UnitCastVariant,
            ["ground_cast_variant"] = GroundCastVariant,
            ["command_cast_variant"] = CommandCastVariant,
            ["unit_execution_cast_variant"] = UnitExecutionCastVariant,
            ["execution_cast_variant"] = ExecutionCastVariant,
            ["routes_to_unit_targeting"] = RoutesToUnitTargeting,
            ["option_error_message"] = OptionErrorMessage,
            ["option_allowed"] = OptionAllowed,
            ["effect_defs"] = ToEffectArray(EffectDefs),
            ["uses_fate_attack"] = UsesFateAttack,
            ["force_hit_no_crit"] = ForceHitNoCrit,
            ["fate_preview_mode"] = FatePreviewMode,
        };
    }

    private static Godot.Collections.Array<StringName> ToStringNameArray(
        IEnumerable<StringName> values
    )
    {
        var result = new Godot.Collections.Array<StringName>();
        foreach (StringName value in values ?? Array.Empty<StringName>())
        {
            result.Add(value);
        }
        return result;
    }

    private static Godot.Collections.Array<CombatEffectDef> ToEffectArray(
        IEnumerable<CombatEffectDef> values
    )
    {
        var result = new Godot.Collections.Array<CombatEffectDef>();
        foreach (CombatEffectDef value in values ?? Array.Empty<CombatEffectDef>())
        {
            if (value != null)
            {
                result.Add(value);
            }
        }
        return result;
    }
}

public sealed class BattleSkillResolutionRules
{
    private static readonly StringName EmptyStringName = "";
    private static readonly StringName BlackContractPushSkillId = "black_contract_push";
    private static readonly StringName FatePreviewModeNone = "";
    private static readonly StringName FatePreviewModeStandard = "standard";
    private static readonly StringName FatePreviewModeForceHitNoCritName = "force_hit_no_crit";
    public static StringName FatePreviewModeForceHitNoCrit => FatePreviewModeForceHitNoCritName;

    public BattleSkillResolutionPolicy BuildSkillResolutionPolicy(
        SkillDef skillDef,
        BattleUnitState activeUnit,
        StringName skillVariantId = default,
        IEnumerable<StringName> targetUnitIdsOption = null,
        BattleUnitState targetUnit = null
    )
    {
        List<StringName> targetUnitIds = NormalizeTargetUnitIds(targetUnitIdsOption);
        bool routesToUnitTargeting = ShouldRouteSkillCommandToUnitTargeting(
            skillDef,
            targetUnitIds
        );
        string optionErrorMessage = GetSkillVariantCommandErrorMessage(
            skillDef,
            activeUnit,
            skillVariantId,
            routesToUnitTargeting
        );
        CombatCastVariantDef unitCastVariant = ResolveUnitCastVariant(
            skillDef,
            activeUnit,
            skillVariantId
        );
        CombatCastVariantDef groundCastVariant = ResolveGroundCastVariant(
            skillDef,
            activeUnit,
            skillVariantId
        );
        CombatCastVariantDef commandCastVariant = ResolveCommandRouteCastVariant(
            skillDef,
            activeUnit,
            skillVariantId,
            routesToUnitTargeting
        );
        CombatCastVariantDef unitExecutionCastVariant = routesToUnitTargeting
            ? commandCastVariant
            : unitCastVariant;
        CombatCastVariantDef executionCastVariant = routesToUnitTargeting
            ? unitExecutionCastVariant
            : commandCastVariant;

        List<CombatEffectDef> effectDefs = new();
        if (string.IsNullOrEmpty(optionErrorMessage))
        {
            effectDefs = routesToUnitTargeting
                ? CollectUnitSkillEffectDefs(skillDef, unitExecutionCastVariant, activeUnit)
                : CollectGroundUnitEffectDefs(skillDef, groundCastVariant, activeUnit);
        }

        bool usesFateAttack =
            routesToUnitTargeting
            && ShouldResolveUnitSkillAsFateAttack(activeUnit, targetUnit, skillDef, effectDefs);
        bool forceHitNoCrit = usesFateAttack && IsForceHitNoCritSkill(skillDef);
        StringName fatePreviewMode = FatePreviewModeNone;
        if (usesFateAttack)
        {
            fatePreviewMode = forceHitNoCrit
                ? FatePreviewModeForceHitNoCritName
                : FatePreviewModeStandard;
        }

        return new BattleSkillResolutionPolicy(
            targetUnitIds,
            unitCastVariant,
            groundCastVariant,
            commandCastVariant,
            unitExecutionCastVariant,
            executionCastVariant,
            routesToUnitTargeting,
            optionErrorMessage,
            effectDefs,
            usesFateAttack,
            forceHitNoCrit,
            fatePreviewMode
        );
    }

    public List<StringName> NormalizeTargetUnitIds(IEnumerable<StringName> targetUnitIdsOption)
    {
        var targetUnitIds = new List<StringName>();
        if (targetUnitIdsOption == null)
        {
            return targetUnitIds;
        }
        var seenIds = new HashSet<StringName>();
        foreach (StringName targetUnitId in targetUnitIdsOption)
        {
            if (IsEmpty(targetUnitId) || !seenIds.Add(targetUnitId))
            {
                continue;
            }
            targetUnitIds.Add(targetUnitId);
        }
        return targetUnitIds;
    }

    public bool ShouldRouteSkillCommandToUnitTargeting(
        SkillDef skillDef,
        IReadOnlyList<StringName> targetUnitIds
    )
    {
        CombatSkillDef combatProfile = skillDef?.combat_profile;
        if (skillDef == null || combatProfile == null)
        {
            return false;
        }
        if (targetUnitIds != null && targetUnitIds.Count > 0)
        {
            return true;
        }
        if (
            BattleTypedNames.ToTargetSelectionMode(
                combatProfile.target_selection_mode
            ) == BattleTargetSelectionMode.RandomChain
        )
        {
            return true;
        }
        return combatProfile.TargetModeKind == BattleTargetMode.Unit;
    }

    public string GetSkillVariantCommandErrorMessage(
        SkillDef skillDef,
        BattleUnitState activeUnit,
        StringName skillVariantId = default,
        bool routesToUnitTargeting = false
    )
    {
        CombatSkillDef combatProfile = skillDef?.combat_profile;
        if (skillDef == null || combatProfile == null)
        {
            return "技能或目标无效。";
        }
        if (combatProfile.cast_variants.Count == 0)
        {
            return !IsEmpty(skillVariantId) ? "技能形态无效或尚未解锁。" : "";
        }

        int skillLevel = GetUnitSkillLevel(activeUnit, skillDef.skill_id);
        List<CombatCastVariantDef> unlockedOptions = GetUnlockedCastVariants(
            combatProfile,
            skillLevel
        );
        var matchingModeOptions = new List<CombatCastVariantDef>();
        BattleTargetMode expectedTargetMode = GetCommandRouteCastVariantTargetModeKind(
            skillDef,
            routesToUnitTargeting
        );
        foreach (CombatCastVariantDef castVariant in unlockedOptions)
        {
            if (
                castVariant != null
                && GetCastVariantTargetModeKind(skillDef, castVariant) == expectedTargetMode
            )
            {
                matchingModeOptions.Add(castVariant);
            }
        }
        if (IsEmpty(skillVariantId))
        {
            if (matchingModeOptions.Count > 1)
            {
                return "技能形态不明确。";
            }
            return matchingModeOptions.Count == 0 ? "技能形态无效或尚未解锁。" : "";
        }
        foreach (CombatCastVariantDef castVariant in matchingModeOptions)
        {
            if (castVariant != null && castVariant.variant_id == skillVariantId)
            {
                return "";
            }
        }
        return "技能形态无效或尚未解锁。";
    }

    internal string GetSkillVariantCommandErrorMessage(
        SkillDef skillDef,
        BattleUnitReadView activeUnit,
        StringName skillVariantId = default,
        bool routesToUnitTargeting = false
    )
    {
        CombatSkillDef combatProfile = skillDef?.combat_profile;
        if (skillDef == null || combatProfile == null)
        {
            return "技能或目标无效。";
        }
        if (combatProfile.cast_variants.Count == 0)
        {
            return !IsEmpty(skillVariantId) ? "技能形态无效或尚未解锁。" : "";
        }

        int skillLevel = GetUnitSkillLevel(activeUnit, skillDef.skill_id);
        List<CombatCastVariantDef> unlockedOptions = GetUnlockedCastVariants(
            combatProfile,
            skillLevel
        );
        var matchingModeOptions = new List<CombatCastVariantDef>();
        BattleTargetMode expectedTargetMode = GetCommandRouteCastVariantTargetModeKind(
            skillDef,
            routesToUnitTargeting
        );
        foreach (CombatCastVariantDef castVariant in unlockedOptions)
        {
            if (
                castVariant != null
                && GetCastVariantTargetModeKind(skillDef, castVariant) == expectedTargetMode
            )
            {
                matchingModeOptions.Add(castVariant);
            }
        }
        if (IsEmpty(skillVariantId))
        {
            if (matchingModeOptions.Count > 1)
            {
                return "技能形态不明确。";
            }
            return matchingModeOptions.Count == 0 ? "技能形态无效或尚未解锁。" : "";
        }
        foreach (CombatCastVariantDef castVariant in matchingModeOptions)
        {
            if (castVariant != null && castVariant.variant_id == skillVariantId)
            {
                return "";
            }
        }
        return "技能形态无效或尚未解锁。";
    }

    public bool ShouldResolveUnitSkillAsFateAttack(
        BattleUnitState activeUnit,
        BattleUnitState targetUnit,
        SkillDef skillDef,
        IEnumerable<CombatEffectDef> effectDefs
    )
    {
        CombatSkillDef combatProfile = skillDef?.combat_profile;
        if (
            activeUnit == null
            || targetUnit == null
            || skillDef == null
            || combatProfile == null
        )
        {
            return false;
        }
        if (activeUnit.faction_id == targetUnit.faction_id)
        {
            return false;
        }
        if (effectDefs == null)
        {
            return false;
        }
        foreach (CombatEffectDef effectDef in effectDefs)
        {
            if (
                effectDef == null
                || effectDef.EffectKind != BattleEffectKind.Damage
            )
            {
                continue;
            }
            if (EffectHasSave(effectDef))
            {
                continue;
            }
            if (
                !IsUnitValidForEffect(
                    activeUnit,
                    targetUnit,
                    ResolveEffectTargetFilter(skillDef, effectDef)
                )
            )
            {
                continue;
            }
            return true;
        }
        return false;
    }

    internal bool ShouldResolveUnitSkillAsFateAttack(
        BattleUnitReadView activeUnit,
        BattleUnitReadView targetUnit,
        SkillDef skillDef,
        IEnumerable<CombatEffectDef> effectDefs
    )
    {
        CombatSkillDef combatProfile = skillDef?.combat_profile;
        if (
            !activeUnit.IsValid
            || !targetUnit.IsValid
            || skillDef == null
            || combatProfile == null
        )
        {
            return false;
        }
        if (activeUnit.FactionId == targetUnit.FactionId)
        {
            return false;
        }
        if (effectDefs == null)
        {
            return false;
        }
        foreach (CombatEffectDef effectDef in effectDefs)
        {
            if (
                effectDef == null
                || effectDef.EffectKind != BattleEffectKind.Damage
            )
            {
                continue;
            }
            if (EffectHasSave(effectDef))
            {
                continue;
            }
            if (
                !BattleTargetTeamRules.IsUnitValidForFilter(
                    activeUnit,
                    targetUnit,
                    ResolveEffectTargetFilter(skillDef, effectDef)
                )
            )
            {
                continue;
            }
            return true;
        }
        return false;
    }

    public bool IsForceHitNoCritSkill(SkillDef skillDef)
    {
        return skillDef != null && skillDef.skill_id == BlackContractPushSkillId;
    }

    public CombatCastVariantDef ResolveGroundCastVariant(
        SkillDef skillDef,
        BattleUnitState activeUnit,
        StringName skillVariantId = default
    )
    {
        CombatSkillDef combatProfile = skillDef?.combat_profile;
        if (skillDef == null || combatProfile == null)
        {
            return null;
        }
        if (combatProfile.cast_variants.Count == 0)
        {
            return
                combatProfile.TargetModeKind == BattleTargetMode.Ground
                && IsEmpty(skillVariantId)
                ? BuildImplicitGroundCastVariant(skillDef)
                : null;
        }

        int skillLevel = GetUnitSkillLevel(activeUnit, skillDef.skill_id);
        List<CombatCastVariantDef> unlockedOptions = GetUnlockedCastVariants(
            combatProfile,
            skillLevel
        );
        if (unlockedOptions.Count == 0)
        {
            return null;
        }
        if (IsEmpty(skillVariantId))
        {
            var groundOptions = new List<CombatCastVariantDef>();
            foreach (CombatCastVariantDef castVariant in unlockedOptions)
            {
                if (
                    castVariant != null
                    && GetCastVariantTargetModeKind(skillDef, castVariant)
                        == BattleTargetMode.Ground
                )
                {
                    groundOptions.Add(castVariant);
                }
            }
            return groundOptions.Count == 1 ? groundOptions[0] : null;
        }

        foreach (CombatCastVariantDef castVariant in unlockedOptions)
        {
            if (
                castVariant != null
                && castVariant.variant_id == skillVariantId
                && GetCastVariantTargetModeKind(skillDef, castVariant)
                    == BattleTargetMode.Ground
            )
            {
                return castVariant;
            }
        }
        return null;
    }

    internal CombatCastVariantDef ResolveGroundCastVariant(
        SkillDef skillDef,
        BattleUnitReadView activeUnit,
        StringName skillVariantId = default
    )
    {
        CombatSkillDef combatProfile = skillDef?.combat_profile;
        if (skillDef == null || combatProfile == null)
        {
            return null;
        }
        if (combatProfile.cast_variants.Count == 0)
        {
            return
                combatProfile.TargetModeKind == BattleTargetMode.Ground
                && IsEmpty(skillVariantId)
                ? BuildImplicitGroundCastVariant(skillDef)
                : null;
        }

        int skillLevel = GetUnitSkillLevel(activeUnit, skillDef.skill_id);
        List<CombatCastVariantDef> unlockedOptions = GetUnlockedCastVariants(
            combatProfile,
            skillLevel
        );
        if (unlockedOptions.Count == 0)
        {
            return null;
        }
        if (IsEmpty(skillVariantId))
        {
            var groundOptions = new List<CombatCastVariantDef>();
            foreach (CombatCastVariantDef castVariant in unlockedOptions)
            {
                if (
                    castVariant != null
                    && GetCastVariantTargetModeKind(skillDef, castVariant)
                        == BattleTargetMode.Ground
                )
                {
                    groundOptions.Add(castVariant);
                }
            }
            return groundOptions.Count == 1 ? groundOptions[0] : null;
        }

        foreach (CombatCastVariantDef castVariant in unlockedOptions)
        {
            if (
                castVariant != null
                && castVariant.variant_id == skillVariantId
                && GetCastVariantTargetModeKind(skillDef, castVariant)
                    == BattleTargetMode.Ground
            )
            {
                return castVariant;
            }
        }
        return null;
    }

    public CombatCastVariantDef ResolveUnitCastVariant(
        SkillDef skillDef,
        BattleUnitState activeUnit,
        StringName skillVariantId = default
    )
    {
        CombatSkillDef combatProfile = skillDef?.combat_profile;
        if (skillDef == null || combatProfile == null)
        {
            return null;
        }
        if (combatProfile.cast_variants.Count == 0)
        {
            return null;
        }

        int skillLevel = GetUnitSkillLevel(activeUnit, skillDef.skill_id);
        List<CombatCastVariantDef> unlockedOptions = GetUnlockedCastVariants(
            combatProfile,
            skillLevel
        );
        if (unlockedOptions.Count == 0)
        {
            return null;
        }
        if (IsEmpty(skillVariantId))
        {
            var unitOptions = new List<CombatCastVariantDef>();
            foreach (CombatCastVariantDef castVariant in unlockedOptions)
            {
                if (
                    castVariant != null
                    && GetCastVariantTargetModeKind(skillDef, castVariant)
                        == BattleTargetMode.Unit
                )
                {
                    unitOptions.Add(castVariant);
                }
            }
            return unitOptions.Count == 1 ? unitOptions[0] : null;
        }

        foreach (CombatCastVariantDef castVariant in unlockedOptions)
        {
            if (
                castVariant != null
                && castVariant.variant_id == skillVariantId
                && GetCastVariantTargetModeKind(skillDef, castVariant)
                    == BattleTargetMode.Unit
            )
            {
                return castVariant;
            }
        }
        return null;
    }

    internal CombatCastVariantDef ResolveUnitCastVariant(
        SkillDef skillDef,
        BattleUnitReadView activeUnit,
        StringName skillVariantId = default
    )
    {
        CombatSkillDef combatProfile = skillDef?.combat_profile;
        if (skillDef == null || combatProfile == null)
        {
            return null;
        }
        if (combatProfile.cast_variants.Count == 0)
        {
            return null;
        }

        int skillLevel = GetUnitSkillLevel(activeUnit, skillDef.skill_id);
        List<CombatCastVariantDef> unlockedOptions = GetUnlockedCastVariants(
            combatProfile,
            skillLevel
        );
        if (unlockedOptions.Count == 0)
        {
            return null;
        }
        if (IsEmpty(skillVariantId))
        {
            var unitOptions = new List<CombatCastVariantDef>();
            foreach (CombatCastVariantDef castVariant in unlockedOptions)
            {
                if (
                    castVariant != null
                    && GetCastVariantTargetModeKind(skillDef, castVariant)
                        == BattleTargetMode.Unit
                )
                {
                    unitOptions.Add(castVariant);
                }
            }
            return unitOptions.Count == 1 ? unitOptions[0] : null;
        }

        foreach (CombatCastVariantDef castVariant in unlockedOptions)
        {
            if (
                castVariant != null
                && castVariant.variant_id == skillVariantId
                && GetCastVariantTargetModeKind(skillDef, castVariant)
                    == BattleTargetMode.Unit
            )
            {
                return castVariant;
            }
        }
        return null;
    }

    public CombatCastVariantDef ResolveCommandRouteCastVariant(
        SkillDef skillDef,
        BattleUnitState activeUnit,
        StringName skillVariantId = default,
        bool routesToUnitTargeting = false
    )
    {
        BattleTargetMode targetMode = GetCommandRouteCastVariantTargetModeKind(
            skillDef,
            routesToUnitTargeting
        );
        if (targetMode == BattleTargetMode.Unit)
        {
            return ResolveUnitCastVariant(skillDef, activeUnit, skillVariantId);
        }
        if (targetMode == BattleTargetMode.Ground)
        {
            return ResolveGroundCastVariant(skillDef, activeUnit, skillVariantId);
        }
        return null;
    }

    internal CombatCastVariantDef ResolveCommandRouteCastVariant(
        SkillDef skillDef,
        BattleUnitReadView activeUnit,
        StringName skillVariantId = default,
        bool routesToUnitTargeting = false
    )
    {
        BattleTargetMode targetMode = GetCommandRouteCastVariantTargetModeKind(
            skillDef,
            routesToUnitTargeting
        );
        if (targetMode == BattleTargetMode.Unit)
        {
            return ResolveUnitCastVariant(skillDef, activeUnit, skillVariantId);
        }
        if (targetMode == BattleTargetMode.Ground)
        {
            return ResolveGroundCastVariant(skillDef, activeUnit, skillVariantId);
        }
        return null;
    }

    internal BattleTargetMode GetCommandRouteCastVariantTargetModeKind(
        SkillDef skillDef,
        bool routesToUnitTargeting = false
    )
    {
        CombatSkillDef combatProfile = skillDef?.combat_profile;
        if (skillDef == null || combatProfile == null)
        {
            return BattleTargetMode.Unknown;
        }
        return !routesToUnitTargeting ? BattleTargetMode.Ground : combatProfile.TargetModeKind;
    }

    internal BattleTargetMode GetCastVariantTargetModeKind(
        SkillDef skillDef,
        CombatCastVariantDef castVariant
    )
    {
        if (castVariant == null)
        {
            return BattleTargetMode.Unknown;
        }
        BattleTargetMode targetMode = castVariant.TargetModeKind;
        if (targetMode != BattleTargetMode.Unknown)
        {
            return targetMode;
        }
        return skillDef?.combat_profile?.TargetModeKind ?? BattleTargetMode.Unknown;
    }

    public List<CombatEffectDef> CollectUnitSkillEffectDefs(
        SkillDef skillDef,
        CombatCastVariantDef castVariant,
        BattleUnitState activeUnit = null
    )
    {
        return CollectEffectDefs(skillDef, castVariant, activeUnit);
    }

    internal List<CombatEffectDef> CollectUnitSkillEffectDefs(
        SkillDef skillDef,
        CombatCastVariantDef castVariant,
        BattleUnitReadView activeUnit
    )
    {
        return CollectEffectDefs(skillDef, castVariant, activeUnit);
    }

    public List<CombatEffectDef> CollectGroundUnitEffectDefs(
        SkillDef skillDef,
        CombatCastVariantDef castVariant,
        BattleUnitState activeUnit = null
    )
    {
        var effectDefs = new List<CombatEffectDef>();
        foreach (CombatEffectDef effectDef in CollectEffectDefs(skillDef, castVariant, activeUnit))
        {
            if (IsUnitEffect(effectDef))
            {
                effectDefs.Add(effectDef);
            }
        }
        return effectDefs;
    }

    internal List<CombatEffectDef> CollectGroundUnitEffectDefs(
        SkillDef skillDef,
        CombatCastVariantDef castVariant,
        BattleUnitReadView activeUnit
    )
    {
        var effectDefs = new List<CombatEffectDef>();
        foreach (CombatEffectDef effectDef in CollectEffectDefs(skillDef, castVariant, activeUnit))
        {
            if (IsUnitEffect(effectDef))
            {
                effectDefs.Add(effectDef);
            }
        }
        return effectDefs;
    }

    public List<CombatEffectDef> CollectGroundTerrainEffectDefs(
        SkillDef skillDef,
        CombatCastVariantDef castVariant,
        BattleUnitState activeUnit = null
    )
    {
        var effectDefs = new List<CombatEffectDef>();
        foreach (CombatEffectDef effectDef in CollectEffectDefs(skillDef, castVariant, activeUnit))
        {
            if (IsTerrainEffect(effectDef))
            {
                effectDefs.Add(effectDef);
            }
        }
        return effectDefs;
    }

    internal List<CombatEffectDef> CollectGroundTerrainEffectDefs(
        SkillDef skillDef,
        CombatCastVariantDef castVariant,
        BattleUnitReadView activeUnit
    )
    {
        var effectDefs = new List<CombatEffectDef>();
        foreach (CombatEffectDef effectDef in CollectEffectDefs(skillDef, castVariant, activeUnit))
        {
            if (IsTerrainEffect(effectDef))
            {
                effectDefs.Add(effectDef);
            }
        }
        return effectDefs;
    }

    public List<CombatEffectDef> CollectGroundEffectDefs(
        SkillDef skillDef,
        CombatCastVariantDef castVariant,
        BattleUnitState activeUnit = null
    )
    {
        return CollectEffectDefs(skillDef, castVariant, activeUnit);
    }

    internal List<CombatEffectDef> CollectGroundEffectDefs(
        SkillDef skillDef,
        CombatCastVariantDef castVariant,
        BattleUnitReadView activeUnit
    )
    {
        return CollectEffectDefs(skillDef, castVariant, activeUnit);
    }

    public CombatEffectDef FindRepeatAttackEffect(IEnumerable<CombatEffectDef> effectDefs)
    {
        foreach (CombatEffectDef effectDef in effectDefs ?? Array.Empty<CombatEffectDef>())
        {
            if (
                effectDef != null
                && effectDef.EffectKind == BattleEffectKind.RepeatAttackUntilFail
            )
            {
                return effectDef;
            }
        }
        return null;
    }

    public bool IsUnitEffect(CombatEffectDef effectDef)
    {
        if (effectDef == null)
        {
            return false;
        }
        return BattleTypedNames.IsUnitPayloadEffect(effectDef.EffectKind);
    }

    public bool IsTerrainEffect(CombatEffectDef effectDef)
    {
        if (effectDef == null)
        {
            return false;
        }
        return BattleTypedNames.IsGroundPayloadEffect(effectDef.EffectKind);
    }

    public StringName ResolveEffectTargetFilter(SkillDef skillDef, CombatEffectDef effectDef)
    {
        return BattleTargetTeamRules.ResolveEffectTargetFilter(skillDef, effectDef);
    }

    public bool IsUnitValidForEffect(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        StringName targetTeamFilter
    )
    {
        return BattleTargetTeamRules.IsUnitValidForFilter(
            sourceUnit,
            targetUnit,
            targetTeamFilter
        );
    }

    private List<CombatEffectDef> CollectEffectDefs(
        SkillDef skillDef,
        CombatCastVariantDef castVariant,
        BattleUnitState activeUnit
    )
    {
        var effectDefs = new List<CombatEffectDef>();
        int skillLevel = GetUnitSkillLevel(activeUnit, skillDef?.skill_id ?? EmptyStringName);
        CombatSkillDef combatProfile = skillDef?.combat_profile;
        if (skillDef != null && combatProfile != null)
        {
            AddUnlockedEffectDefs(
                effectDefs,
                combatProfile.effect_defs,
                skillLevel,
                activeUnit != null
            );
        }
        if (castVariant != null)
        {
            AddUnlockedEffectDefs(
                effectDefs,
                castVariant.effect_defs,
                skillLevel,
                activeUnit != null
            );
        }
        return effectDefs;
    }

    private List<CombatEffectDef> CollectEffectDefs(
        SkillDef skillDef,
        CombatCastVariantDef castVariant,
        BattleUnitReadView activeUnit
    )
    {
        var effectDefs = new List<CombatEffectDef>();
        int skillLevel = GetUnitSkillLevel(activeUnit, skillDef?.skill_id ?? EmptyStringName);
        CombatSkillDef combatProfile = skillDef?.combat_profile;
        if (skillDef != null && combatProfile != null)
        {
            AddUnlockedEffectDefs(
                effectDefs,
                combatProfile.effect_defs,
                skillLevel,
                activeUnit.IsValid
            );
        }
        if (castVariant != null)
        {
            AddUnlockedEffectDefs(
                effectDefs,
                castVariant.effect_defs,
                skillLevel,
                activeUnit.IsValid
            );
        }
        return effectDefs;
    }

    private static void AddUnlockedEffectDefs(
        List<CombatEffectDef> target,
        IEnumerable<CombatEffectDef> source,
        int skillLevel,
        bool shouldFilter
    )
    {
        foreach (CombatEffectDef effectDef in source ?? Array.Empty<CombatEffectDef>())
        {
            if (IsEffectUnlockedForSkillLevel(effectDef, skillLevel, shouldFilter))
            {
                target.Add(effectDef);
            }
        }
    }

    private static bool IsEffectUnlockedForSkillLevel(
        CombatEffectDef effectDef,
        int skillLevel,
        bool shouldFilter
    )
    {
        if (effectDef == null)
        {
            return false;
        }
        if (!shouldFilter)
        {
            return true;
        }
        int minLevel = Math.Max(effectDef.min_skill_level, 0);
        int maxLevel = effectDef.max_skill_level;
        return skillLevel >= minLevel && (maxLevel < 0 || skillLevel <= maxLevel);
    }

    private static int GetUnitSkillLevel(BattleUnitState activeUnit, StringName skillId)
    {
        if (activeUnit == null || IsEmpty(skillId))
        {
            return 0;
        }
        return Math.Max(activeUnit.GetKnownSkillLevelTyped(skillId), 0);
    }

    private static int GetUnitSkillLevel(BattleUnitReadView activeUnit, StringName skillId)
    {
        if (!activeUnit.IsValid || IsEmpty(skillId))
        {
            return 0;
        }
        return Math.Max(activeUnit.GetKnownSkillLevel(skillId), 0);
    }

    private static bool EffectHasSave(CombatEffectDef effectDef)
    {
        if (effectDef == null)
        {
            return false;
        }
        return effectDef.SaveDcModeKind == BattleSaveDcMode.CasterSpell || effectDef.save_dc > 0;
    }

    private static List<CombatCastVariantDef> GetUnlockedCastVariants(
        CombatSkillDef combatProfile,
        int skillLevel
    )
    {
        var result = new List<CombatCastVariantDef>();
        foreach (
            CombatCastVariantDef castVariant in combatProfile?.cast_variants
                ?? new Godot.Collections.Array<CombatCastVariantDef>()
        )
        {
            if (castVariant != null && skillLevel >= castVariant.min_skill_level)
            {
                result.Add(castVariant);
            }
        }
        return result;
    }

    private static CombatCastVariantDef BuildImplicitGroundCastVariant(SkillDef skillDef)
    {
        CombatSkillDef combatProfile = skillDef?.combat_profile;
        if (combatProfile == null)
        {
            return null;
        }
        return new CombatCastVariantDef
        {
            variant_id = EmptyStringName,
            display_name = "",
            target_mode = BattleTypedNames.TargetModeGround,
            FootprintPatternKind = CombatCastFootprintPattern.Single,
            required_coord_count = 1,
        };
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
    }
}
