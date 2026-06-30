using System;
using System.Collections.Generic;
using Godot;

public sealed class BattleSkillResolutionPolicy
{
    public IReadOnlyList<StringName> TargetUnitIds { get; }
    public CombatCastVariantDefinition UnitCastVariantDefinition { get; }
    public CombatCastVariantDefinition GroundCastVariantDefinition { get; }
    public CombatCastVariantDefinition CommandCastVariantDefinition { get; }
    public CombatCastVariantDefinition UnitExecutionCastVariantDefinition { get; }
    public CombatCastVariantDefinition ExecutionCastVariantDefinition { get; }
    public bool RoutesToUnitTargeting { get; }
    public string OptionErrorMessage { get; }
    public bool OptionAllowed => string.IsNullOrEmpty(OptionErrorMessage);
    public IReadOnlyList<CombatEffectDefinition> EffectDefinitions { get; }
    public bool UsesFateAttack { get; }
    public bool ForceHitNoCrit { get; }
    public StringName FatePreviewMode { get; }

    public BattleSkillResolutionPolicy(
        IEnumerable<StringName> targetUnitIds,
        bool routesToUnitTargeting,
        string optionErrorMessage,
        bool usesFateAttack,
        bool forceHitNoCrit,
        StringName fatePreviewMode,
        IEnumerable<CombatEffectDefinition> effectDefinitions = null,
        CombatCastVariantDefinition unitCastVariantDefinition = null,
        CombatCastVariantDefinition groundCastVariantDefinition = null,
        CombatCastVariantDefinition commandCastVariantDefinition = null,
        CombatCastVariantDefinition unitExecutionCastVariantDefinition = null,
        CombatCastVariantDefinition executionCastVariantDefinition = null
    )
    {
        TargetUnitIds = new List<StringName>(targetUnitIds ?? Array.Empty<StringName>());
        UnitCastVariantDefinition = unitCastVariantDefinition;
        GroundCastVariantDefinition = groundCastVariantDefinition;
        CommandCastVariantDefinition = commandCastVariantDefinition;
        UnitExecutionCastVariantDefinition = unitExecutionCastVariantDefinition;
        ExecutionCastVariantDefinition = executionCastVariantDefinition;
        RoutesToUnitTargeting = routesToUnitTargeting;
        OptionErrorMessage = optionErrorMessage ?? "";
        EffectDefinitions = new List<CombatEffectDefinition>(
            effectDefinitions ?? Array.Empty<CombatEffectDefinition>()
        );
        UsesFateAttack = usesFateAttack;
        ForceHitNoCrit = forceHitNoCrit;
        FatePreviewMode = fatePreviewMode;
    }

}

public sealed class BattleSkillResolutionRules : IDisposable
{
    private static readonly StringName EmptyStringName = "";
    private static readonly StringName BlackContractPushSkillId = "black_contract_push";
    private static readonly StringName FatePreviewModeNone = "";
    private static readonly StringName FatePreviewModeStandard = "standard";
    private static readonly StringName FatePreviewModeForceHitNoCritName = "force_hit_no_crit";
    private StringName _scopedSkillUnitId = "";
    private StringName _scopedSkillId = "";
    private int _scopedSkillLevel;
    public static StringName FatePreviewModeForceHitNoCrit => FatePreviewModeForceHitNoCritName;

    public BattleSkillResolutionRules()
    {
    }

    public void Dispose()
    {
        ClearScopedSkillLevel();
    }

    internal IDisposable PushScopedSkillLevel(
        BattleUnitState unitState,
        StringName skillId,
        int skillLevel
    )
    {
        StringName previousUnitId = _scopedSkillUnitId;
        StringName previousSkillId = _scopedSkillId;
        int previousSkillLevel = _scopedSkillLevel;
        _scopedSkillUnitId = ProgressionDataUtils.to_string_name(unitState?.unit_id ?? "");
        _scopedSkillId = ProgressionDataUtils.to_string_name(skillId);
        _scopedSkillLevel = Math.Max(skillLevel, 0);
        return new ScopedSkillLevelScope(
            this,
            previousUnitId,
            previousSkillId,
            previousSkillLevel
        );
    }

    public BattleSkillResolutionPolicy BuildSkillResolutionPolicy(
        SkillDefinition skillDefinition,
        BattleUnitState activeUnit,
        StringName skillVariantId = default,
        IEnumerable<StringName> targetUnitIdsOption = null,
        BattleUnitState targetUnit = null
    )
    {
        List<StringName> targetUnitIds = NormalizeTargetUnitIds(targetUnitIdsOption);
        bool routesToUnitTargeting = ShouldRouteSkillCommandToUnitTargeting(
            skillDefinition,
            targetUnitIds
        );
        string optionErrorMessage = GetSkillVariantCommandErrorMessage(
            skillDefinition,
            activeUnit,
            skillVariantId,
            routesToUnitTargeting
        );
        CombatCastVariantDefinition unitCastVariant = ResolveUnitCastVariantDefinition(
            skillDefinition,
            activeUnit,
            skillVariantId
        );
        CombatCastVariantDefinition groundCastVariant = ResolveGroundCastVariantDefinition(
            skillDefinition,
            activeUnit,
            skillVariantId
        );
        CombatCastVariantDefinition commandCastVariant = ResolveCommandRouteCastVariantDefinition(
            skillDefinition,
            activeUnit,
            skillVariantId,
            routesToUnitTargeting
        );
        CombatCastVariantDefinition unitExecutionCastVariant = routesToUnitTargeting
            ? commandCastVariant
            : unitCastVariant;
        CombatCastVariantDefinition executionCastVariant = routesToUnitTargeting
            ? unitExecutionCastVariant
            : commandCastVariant;

        List<CombatEffectDefinition> effectDefinitions = new();
        if (string.IsNullOrEmpty(optionErrorMessage))
        {
            effectDefinitions = routesToUnitTargeting
                ? CollectUnitSkillEffectDefinitions(
                    skillDefinition,
                    unitExecutionCastVariant,
                    activeUnit
                )
                : CollectGroundUnitEffectDefinitions(
                    skillDefinition,
                    groundCastVariant,
                    activeUnit
                );
        }

        bool usesFateAttack =
            routesToUnitTargeting
            && ShouldResolveUnitSkillAsFateAttack(
                activeUnit,
                targetUnit,
                skillDefinition,
                effectDefinitions
            );
        bool forceHitNoCrit = usesFateAttack && IsForceHitNoCritSkill(skillDefinition);
        StringName fatePreviewMode = FatePreviewModeNone;
        if (usesFateAttack)
        {
            fatePreviewMode = forceHitNoCrit
                ? FatePreviewModeForceHitNoCritName
                : FatePreviewModeStandard;
        }

        return new BattleSkillResolutionPolicy(
            targetUnitIds,
            routesToUnitTargeting,
            optionErrorMessage,
            usesFateAttack,
            forceHitNoCrit,
            fatePreviewMode,
            effectDefinitions,
            unitCastVariant,
            groundCastVariant,
            commandCastVariant,
            unitExecutionCastVariant,
            executionCastVariant
        );
    }

    internal BattleSkillResolutionPolicy BuildSkillResolutionPolicy(
        SkillDefinition skillDefinition,
        BattleUnitReadView activeUnit,
        StringName skillVariantId = default,
        IEnumerable<StringName> targetUnitIdsOption = null,
        BattleUnitReadView targetUnit = default
    )
    {
        List<StringName> targetUnitIds = NormalizeTargetUnitIds(targetUnitIdsOption);
        bool routesToUnitTargeting = ShouldRouteSkillCommandToUnitTargeting(
            skillDefinition,
            targetUnitIds
        );
        string optionErrorMessage = GetSkillVariantCommandErrorMessage(
            skillDefinition,
            activeUnit,
            skillVariantId,
            routesToUnitTargeting
        );
        CombatCastVariantDefinition unitCastVariant = ResolveUnitCastVariantDefinition(
            skillDefinition,
            activeUnit,
            skillVariantId
        );
        CombatCastVariantDefinition groundCastVariant = ResolveGroundCastVariantDefinition(
            skillDefinition,
            activeUnit,
            skillVariantId
        );
        CombatCastVariantDefinition commandCastVariant = ResolveCommandRouteCastVariantDefinition(
            skillDefinition,
            activeUnit,
            skillVariantId,
            routesToUnitTargeting
        );
        CombatCastVariantDefinition unitExecutionCastVariant = routesToUnitTargeting
            ? commandCastVariant
            : unitCastVariant;
        CombatCastVariantDefinition executionCastVariant = routesToUnitTargeting
            ? unitExecutionCastVariant
            : commandCastVariant;

        List<CombatEffectDefinition> effectDefinitions = new();
        if (string.IsNullOrEmpty(optionErrorMessage))
        {
            effectDefinitions = routesToUnitTargeting
                ? CollectUnitSkillEffectDefinitions(
                    skillDefinition,
                    unitExecutionCastVariant,
                    activeUnit
                )
                : CollectGroundUnitEffectDefinitions(
                    skillDefinition,
                    groundCastVariant,
                    activeUnit
                );
        }

        bool usesFateAttack =
            routesToUnitTargeting
            && ShouldResolveUnitSkillAsFateAttack(
                activeUnit,
                targetUnit,
                skillDefinition,
                effectDefinitions
            );
        bool forceHitNoCrit = usesFateAttack && IsForceHitNoCritSkill(skillDefinition);
        StringName fatePreviewMode = FatePreviewModeNone;
        if (usesFateAttack)
        {
            fatePreviewMode = forceHitNoCrit
                ? FatePreviewModeForceHitNoCritName
                : FatePreviewModeStandard;
        }

        return new BattleSkillResolutionPolicy(
            targetUnitIds,
            routesToUnitTargeting,
            optionErrorMessage,
            usesFateAttack,
            forceHitNoCrit,
            fatePreviewMode,
            effectDefinitions,
            unitCastVariant,
            groundCastVariant,
            commandCastVariant,
            unitExecutionCastVariant,
            executionCastVariant
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
        SkillDefinition skillDefinition,
        IReadOnlyList<StringName> targetUnitIds
    )
    {
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (skillDefinition == null || combatProfile == null)
        {
            return false;
        }
        if (targetUnitIds != null && targetUnitIds.Count > 0)
        {
            return true;
        }
        if (combatProfile.TargetSelectionModeKind == BattleTargetSelectionMode.RandomChain)
        {
            return true;
        }
        return combatProfile.TargetModeKind == BattleTargetMode.Unit;
    }

    public string GetSkillVariantCommandErrorMessage(
        SkillDefinition skillDefinition,
        BattleUnitState activeUnit,
        StringName skillVariantId = default,
        bool routesToUnitTargeting = false
    )
    {
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (skillDefinition == null || combatProfile == null)
        {
            return "技能或目标无效。";
        }
        if (combatProfile.CastVariants.Count == 0)
        {
            return !IsEmpty(skillVariantId) ? "技能形态无效或尚未解锁。" : "";
        }

        int skillLevel = GetUnitSkillLevel(activeUnit, skillDefinition.SkillId);
        IReadOnlyList<CombatCastVariantDefinition> unlockedOptions =
            combatProfile.GetUnlockedCastVariants(skillLevel);
        var matchingModeOptions = new List<CombatCastVariantDefinition>();
        BattleTargetMode expectedTargetMode = GetCommandRouteCastVariantTargetModeKind(
            skillDefinition,
            routesToUnitTargeting
        );
        foreach (CombatCastVariantDefinition castVariant in unlockedOptions)
        {
            if (
                castVariant != null
                && GetCastVariantTargetModeKind(skillDefinition, castVariant) == expectedTargetMode
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
        foreach (CombatCastVariantDefinition castVariant in matchingModeOptions)
        {
            if (castVariant != null && castVariant.VariantId == skillVariantId)
            {
                return "";
            }
        }
        return "技能形态无效或尚未解锁。";
    }

    internal string GetSkillVariantCommandErrorMessage(
        SkillDefinition skillDefinition,
        BattleUnitReadView activeUnit,
        StringName skillVariantId = default,
        bool routesToUnitTargeting = false
    )
    {
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (skillDefinition == null || combatProfile == null)
        {
            return "技能或目标无效。";
        }
        if (combatProfile.CastVariants.Count == 0)
        {
            return !IsEmpty(skillVariantId) ? "技能形态无效或尚未解锁。" : "";
        }

        int skillLevel = GetUnitSkillLevel(activeUnit, skillDefinition.SkillId);
        IReadOnlyList<CombatCastVariantDefinition> unlockedOptions =
            combatProfile.GetUnlockedCastVariants(skillLevel);
        var matchingModeOptions = new List<CombatCastVariantDefinition>();
        BattleTargetMode expectedTargetMode = GetCommandRouteCastVariantTargetModeKind(
            skillDefinition,
            routesToUnitTargeting
        );
        foreach (CombatCastVariantDefinition castVariant in unlockedOptions)
        {
            if (
                castVariant != null
                && GetCastVariantTargetModeKind(skillDefinition, castVariant) == expectedTargetMode
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
        foreach (CombatCastVariantDefinition castVariant in matchingModeOptions)
        {
            if (castVariant != null && castVariant.VariantId == skillVariantId)
            {
                return "";
            }
        }
        return "技能形态无效或尚未解锁。";
    }

    public bool ShouldResolveUnitSkillAsFateAttack(
        BattleUnitState activeUnit,
        BattleUnitState targetUnit,
        SkillDefinition skillDefinition,
        IEnumerable<CombatEffectDefinition> effectDefinitions
    )
    {
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (
            activeUnit == null
            || targetUnit == null
            || skillDefinition == null
            || combatProfile == null
        )
        {
            return false;
        }
        if (activeUnit.faction_id == targetUnit.faction_id)
        {
            return false;
        }
        if (effectDefinitions == null)
        {
            return false;
        }
        foreach (CombatEffectDefinition effectDefinition in effectDefinitions)
        {
            if (
                effectDefinition == null
                || effectDefinition.EffectKind != BattleEffectKind.Damage
            )
            {
                continue;
            }
            if (EffectHasSave(effectDefinition))
            {
                continue;
            }
            if (
                !IsUnitValidForEffect(
                    activeUnit,
                    targetUnit,
                    ResolveEffectTargetFilter(skillDefinition, effectDefinition)
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
        SkillDefinition skillDefinition,
        IEnumerable<CombatEffectDefinition> effectDefinitions
    )
    {
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (
            !activeUnit.IsValid
            || !targetUnit.IsValid
            || skillDefinition == null
            || combatProfile == null
        )
        {
            return false;
        }
        if (activeUnit.FactionId == targetUnit.FactionId)
        {
            return false;
        }
        if (effectDefinitions == null)
        {
            return false;
        }
        foreach (CombatEffectDefinition effectDefinition in effectDefinitions)
        {
            if (
                effectDefinition == null
                || effectDefinition.EffectKind != BattleEffectKind.Damage
            )
            {
                continue;
            }
            if (EffectHasSave(effectDefinition))
            {
                continue;
            }
            if (
                !BattleTargetTeamRules.IsUnitValidForFilter(
                    activeUnit,
                    targetUnit,
                    ResolveEffectTargetFilter(skillDefinition, effectDefinition)
                )
            )
            {
                continue;
            }
            return true;
        }
        return false;
    }

    public bool IsForceHitNoCritSkill(SkillDefinition skillDefinition)
    {
        return skillDefinition != null && skillDefinition.SkillId == BlackContractPushSkillId;
    }

    public CombatCastVariantDefinition ResolveGroundCastVariantDefinition(
        SkillDefinition skillDefinition,
        BattleUnitState activeUnit,
        StringName skillVariantId = default
    )
    {
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (skillDefinition == null || combatProfile == null)
        {
            return null;
        }
        if (combatProfile.CastVariants.Count == 0)
        {
            return
                combatProfile.TargetModeKind == BattleTargetMode.Ground
                && IsEmpty(skillVariantId)
                ? BuildImplicitGroundCastVariantDefinition(skillDefinition)
                : null;
        }

        int skillLevel = GetUnitSkillLevel(activeUnit, skillDefinition.SkillId);
        IReadOnlyList<CombatCastVariantDefinition> unlockedOptions =
            combatProfile.GetUnlockedCastVariants(skillLevel);
        if (unlockedOptions.Count == 0)
        {
            return null;
        }
        if (IsEmpty(skillVariantId))
        {
            var groundOptions = new List<CombatCastVariantDefinition>();
            foreach (CombatCastVariantDefinition castVariant in unlockedOptions)
            {
                if (
                    castVariant != null
                    && GetCastVariantTargetModeKind(skillDefinition, castVariant)
                        == BattleTargetMode.Ground
                )
                {
                    groundOptions.Add(castVariant);
                }
            }
            return groundOptions.Count == 1 ? groundOptions[0] : null;
        }

        foreach (CombatCastVariantDefinition castVariant in unlockedOptions)
        {
            if (
                castVariant != null
                && castVariant.VariantId == skillVariantId
                && GetCastVariantTargetModeKind(skillDefinition, castVariant)
                    == BattleTargetMode.Ground
            )
            {
                return castVariant;
            }
        }
        return null;
    }

    internal CombatCastVariantDefinition ResolveGroundCastVariantDefinition(
        SkillDefinition skillDefinition,
        BattleUnitReadView activeUnit,
        StringName skillVariantId = default
    )
    {
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (skillDefinition == null || combatProfile == null)
        {
            return null;
        }
        if (combatProfile.CastVariants.Count == 0)
        {
            return
                combatProfile.TargetModeKind == BattleTargetMode.Ground
                && IsEmpty(skillVariantId)
                ? BuildImplicitGroundCastVariantDefinition(skillDefinition)
                : null;
        }

        int skillLevel = GetUnitSkillLevel(activeUnit, skillDefinition.SkillId);
        IReadOnlyList<CombatCastVariantDefinition> unlockedOptions =
            combatProfile.GetUnlockedCastVariants(skillLevel);
        if (unlockedOptions.Count == 0)
        {
            return null;
        }
        if (IsEmpty(skillVariantId))
        {
            var groundOptions = new List<CombatCastVariantDefinition>();
            foreach (CombatCastVariantDefinition castVariant in unlockedOptions)
            {
                if (
                    castVariant != null
                    && GetCastVariantTargetModeKind(skillDefinition, castVariant)
                        == BattleTargetMode.Ground
                )
                {
                    groundOptions.Add(castVariant);
                }
            }
            return groundOptions.Count == 1 ? groundOptions[0] : null;
        }

        foreach (CombatCastVariantDefinition castVariant in unlockedOptions)
        {
            if (
                castVariant != null
                && castVariant.VariantId == skillVariantId
                && GetCastVariantTargetModeKind(skillDefinition, castVariant)
                    == BattleTargetMode.Ground
            )
            {
                return castVariant;
            }
        }
        return null;
    }

    public CombatCastVariantDefinition ResolveUnitCastVariantDefinition(
        SkillDefinition skillDefinition,
        BattleUnitState activeUnit,
        StringName skillVariantId = default
    )
    {
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (skillDefinition == null || combatProfile == null)
        {
            return null;
        }
        if (combatProfile.CastVariants.Count == 0)
        {
            return null;
        }

        int skillLevel = GetUnitSkillLevel(activeUnit, skillDefinition.SkillId);
        IReadOnlyList<CombatCastVariantDefinition> unlockedOptions =
            combatProfile.GetUnlockedCastVariants(skillLevel);
        if (unlockedOptions.Count == 0)
        {
            return null;
        }
        if (IsEmpty(skillVariantId))
        {
            var unitOptions = new List<CombatCastVariantDefinition>();
            foreach (CombatCastVariantDefinition castVariant in unlockedOptions)
            {
                if (
                    castVariant != null
                    && GetCastVariantTargetModeKind(skillDefinition, castVariant)
                        == BattleTargetMode.Unit
                )
                {
                    unitOptions.Add(castVariant);
                }
            }
            return unitOptions.Count == 1 ? unitOptions[0] : null;
        }

        foreach (CombatCastVariantDefinition castVariant in unlockedOptions)
        {
            if (
                castVariant != null
                && castVariant.VariantId == skillVariantId
                && GetCastVariantTargetModeKind(skillDefinition, castVariant)
                    == BattleTargetMode.Unit
            )
            {
                return castVariant;
            }
        }
        return null;
    }

    internal CombatCastVariantDefinition ResolveUnitCastVariantDefinition(
        SkillDefinition skillDefinition,
        BattleUnitReadView activeUnit,
        StringName skillVariantId = default
    )
    {
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (skillDefinition == null || combatProfile == null)
        {
            return null;
        }
        if (combatProfile.CastVariants.Count == 0)
        {
            return null;
        }

        int skillLevel = GetUnitSkillLevel(activeUnit, skillDefinition.SkillId);
        IReadOnlyList<CombatCastVariantDefinition> unlockedOptions =
            combatProfile.GetUnlockedCastVariants(skillLevel);
        if (unlockedOptions.Count == 0)
        {
            return null;
        }
        if (IsEmpty(skillVariantId))
        {
            var unitOptions = new List<CombatCastVariantDefinition>();
            foreach (CombatCastVariantDefinition castVariant in unlockedOptions)
            {
                if (
                    castVariant != null
                    && GetCastVariantTargetModeKind(skillDefinition, castVariant)
                        == BattleTargetMode.Unit
                )
                {
                    unitOptions.Add(castVariant);
                }
            }
            return unitOptions.Count == 1 ? unitOptions[0] : null;
        }

        foreach (CombatCastVariantDefinition castVariant in unlockedOptions)
        {
            if (
                castVariant != null
                && castVariant.VariantId == skillVariantId
                && GetCastVariantTargetModeKind(skillDefinition, castVariant)
                    == BattleTargetMode.Unit
            )
            {
                return castVariant;
            }
        }
        return null;
    }

    public CombatCastVariantDefinition ResolveCommandRouteCastVariantDefinition(
        SkillDefinition skillDefinition,
        BattleUnitState activeUnit,
        StringName skillVariantId = default,
        bool routesToUnitTargeting = false
    )
    {
        BattleTargetMode targetMode = GetCommandRouteCastVariantTargetModeKind(
            skillDefinition,
            routesToUnitTargeting
        );
        if (targetMode == BattleTargetMode.Unit)
        {
            return ResolveUnitCastVariantDefinition(
                skillDefinition,
                activeUnit,
                skillVariantId
            );
        }
        if (targetMode == BattleTargetMode.Ground)
        {
            return ResolveGroundCastVariantDefinition(
                skillDefinition,
                activeUnit,
                skillVariantId
            );
        }
        return null;
    }

    internal CombatCastVariantDefinition ResolveCommandRouteCastVariantDefinition(
        SkillDefinition skillDefinition,
        BattleUnitReadView activeUnit,
        StringName skillVariantId = default,
        bool routesToUnitTargeting = false
    )
    {
        BattleTargetMode targetMode = GetCommandRouteCastVariantTargetModeKind(
            skillDefinition,
            routesToUnitTargeting
        );
        if (targetMode == BattleTargetMode.Unit)
        {
            return ResolveUnitCastVariantDefinition(
                skillDefinition,
                activeUnit,
                skillVariantId
            );
        }
        if (targetMode == BattleTargetMode.Ground)
        {
            return ResolveGroundCastVariantDefinition(
                skillDefinition,
                activeUnit,
                skillVariantId
            );
        }
        return null;
    }

    internal BattleTargetMode GetCommandRouteCastVariantTargetModeKind(
        SkillDefinition skillDefinition,
        bool routesToUnitTargeting = false
    )
    {
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (skillDefinition == null || combatProfile == null)
        {
            return BattleTargetMode.Unknown;
        }
        return !routesToUnitTargeting ? BattleTargetMode.Ground : combatProfile.TargetModeKind;
    }

    internal BattleTargetMode GetCastVariantTargetModeKind(
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant
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
        return skillDefinition?.CombatProfile?.TargetModeKind ?? BattleTargetMode.Unknown;
    }

    public List<CombatEffectDefinition> CollectUnitSkillEffectDefinitions(
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant,
        BattleUnitState activeUnit = null
    )
    {
        return CollectEffectDefinitions(skillDefinition, castVariant, activeUnit);
    }

    internal List<CombatEffectDefinition> CollectUnitSkillEffectDefinitions(
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant,
        BattleUnitReadView activeUnit
    )
    {
        return CollectEffectDefinitions(skillDefinition, castVariant, activeUnit);
    }

    public List<CombatEffectDefinition> CollectGroundUnitEffectDefinitions(
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant,
        BattleUnitState activeUnit = null
    )
    {
        var effectDefinitions = new List<CombatEffectDefinition>();
        foreach (
            CombatEffectDefinition effectDefinition in CollectEffectDefinitions(
                skillDefinition,
                castVariant,
                activeUnit
            )
        )
        {
            if (IsUnitEffect(effectDefinition))
            {
                effectDefinitions.Add(effectDefinition);
            }
        }
        return effectDefinitions;
    }

    internal List<CombatEffectDefinition> CollectGroundUnitEffectDefinitions(
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant,
        BattleUnitReadView activeUnit
    )
    {
        var effectDefinitions = new List<CombatEffectDefinition>();
        foreach (
            CombatEffectDefinition effectDefinition in CollectEffectDefinitions(
                skillDefinition,
                castVariant,
                activeUnit
            )
        )
        {
            if (IsUnitEffect(effectDefinition))
            {
                effectDefinitions.Add(effectDefinition);
            }
        }
        return effectDefinitions;
    }

    public List<CombatEffectDefinition> CollectGroundTerrainEffectDefinitions(
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant,
        BattleUnitState activeUnit = null
    )
    {
        var effectDefinitions = new List<CombatEffectDefinition>();
        foreach (
            CombatEffectDefinition effectDefinition in CollectEffectDefinitions(
                skillDefinition,
                castVariant,
                activeUnit
            )
        )
        {
            if (IsTerrainEffect(effectDefinition))
            {
                effectDefinitions.Add(effectDefinition);
            }
        }
        return effectDefinitions;
    }

    internal List<CombatEffectDefinition> CollectGroundTerrainEffectDefinitions(
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant,
        BattleUnitReadView activeUnit
    )
    {
        var effectDefinitions = new List<CombatEffectDefinition>();
        foreach (
            CombatEffectDefinition effectDefinition in CollectEffectDefinitions(
                skillDefinition,
                castVariant,
                activeUnit
            )
        )
        {
            if (IsTerrainEffect(effectDefinition))
            {
                effectDefinitions.Add(effectDefinition);
            }
        }
        return effectDefinitions;
    }

    public bool IsUnitEffect(CombatEffectDefinition effectDefinition)
    {
        if (effectDefinition == null)
        {
            return false;
        }
        return BattleTypedNames.IsUnitPayloadEffect(effectDefinition.EffectKind);
    }

    public bool IsTerrainEffect(CombatEffectDefinition effectDefinition)
    {
        if (effectDefinition == null)
        {
            return false;
        }
        return BattleTypedNames.IsGroundPayloadEffect(effectDefinition.EffectKind);
    }

    public StringName ResolveEffectTargetFilter(
        SkillDefinition skillDefinition,
        CombatEffectDefinition effectDefinition
    )
    {
        return BattleTargetTeamRules.ResolveEffectTargetFilter(
            skillDefinition,
            effectDefinition
        );
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

    private List<CombatEffectDefinition> CollectEffectDefinitions(
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant,
        BattleUnitState activeUnit
    )
    {
        var effectDefinitions = new List<CombatEffectDefinition>();
        int skillLevel = GetUnitSkillLevel(activeUnit, skillDefinition?.SkillId ?? EmptyStringName);
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (skillDefinition != null && combatProfile != null)
        {
            AddUnlockedEffectDefinitions(
                effectDefinitions,
                combatProfile.EffectDefinitions,
                skillLevel,
                activeUnit != null
            );
        }
        if (castVariant != null)
        {
            AddUnlockedEffectDefinitions(
                effectDefinitions,
                castVariant.EffectDefinitions,
                skillLevel,
                activeUnit != null
            );
        }
        return effectDefinitions;
    }

    private List<CombatEffectDefinition> CollectEffectDefinitions(
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant,
        BattleUnitReadView activeUnit
    )
    {
        var effectDefinitions = new List<CombatEffectDefinition>();
        int skillLevel = GetUnitSkillLevel(activeUnit, skillDefinition?.SkillId ?? EmptyStringName);
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (skillDefinition != null && combatProfile != null)
        {
            AddUnlockedEffectDefinitions(
                effectDefinitions,
                combatProfile.EffectDefinitions,
                skillLevel,
                activeUnit.IsValid
            );
        }
        if (castVariant != null)
        {
            AddUnlockedEffectDefinitions(
                effectDefinitions,
                castVariant.EffectDefinitions,
                skillLevel,
                activeUnit.IsValid
            );
        }
        return effectDefinitions;
    }

    private static void AddUnlockedEffectDefinitions(
        List<CombatEffectDefinition> target,
        IEnumerable<CombatEffectDefinition> source,
        int skillLevel,
        bool shouldFilter
    )
    {
        foreach (
            CombatEffectDefinition effectDefinition in source
                ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (IsEffectUnlockedForSkillLevel(effectDefinition, skillLevel, shouldFilter))
            {
                target.Add(effectDefinition);
            }
        }
    }

    private static bool IsEffectUnlockedForSkillLevel(
        CombatEffectDefinition effectDefinition,
        int skillLevel,
        bool shouldFilter
    )
    {
        if (effectDefinition == null)
        {
            return false;
        }
        if (!shouldFilter)
        {
            return true;
        }
        int minLevel = Math.Max(effectDefinition.MinSkillLevel, 0);
        int maxLevel = effectDefinition.MaxSkillLevel;
        return skillLevel >= minLevel && (maxLevel < 0 || skillLevel <= maxLevel);
    }

    private int GetUnitSkillLevel(BattleUnitState activeUnit, StringName skillId)
    {
        if (activeUnit == null || IsEmpty(skillId))
        {
            return 0;
        }
        if (
            activeUnit.unit_id == _scopedSkillUnitId
            && skillId == _scopedSkillId
            && _scopedSkillLevel > 0
        )
        {
            return _scopedSkillLevel;
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

    private static bool EffectHasSave(CombatEffectDefinition effectDefinition)
    {
        if (effectDefinition == null)
        {
            return false;
        }
        return effectDefinition.SaveDcModeKind == BattleSaveDcMode.CasterSpell
            || effectDefinition.SaveDc > 0;
    }

    private static CombatCastVariantDefinition BuildImplicitGroundCastVariantDefinition(
        SkillDefinition skillDefinition
    )
    {
        if (skillDefinition?.CombatProfile == null)
        {
            return null;
        }
        return new CombatCastVariantDefinition(
            EmptyStringName,
            "",
            "",
            0,
            BattleTypedNames.TargetModeGround,
            "single",
            1,
            Array.Empty<StringName>(),
            Array.Empty<CombatEffectDefinition>(),
            new Dictionary<string, Variant>()
        );
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
    }

    private void ClearScopedSkillLevel()
    {
        _scopedSkillUnitId = "";
        _scopedSkillId = "";
        _scopedSkillLevel = 0;
    }

    private void RestoreScopedSkillLevel(
        StringName unitId,
        StringName skillId,
        int skillLevel
    )
    {
        _scopedSkillUnitId = unitId;
        _scopedSkillId = skillId;
        _scopedSkillLevel = skillLevel;
    }

    private sealed class ScopedSkillLevelScope : IDisposable
    {
        private BattleSkillResolutionRules _owner;
        private readonly StringName _previousUnitId;
        private readonly StringName _previousSkillId;
        private readonly int _previousSkillLevel;

        internal ScopedSkillLevelScope(
            BattleSkillResolutionRules owner,
            StringName previousUnitId,
            StringName previousSkillId,
            int previousSkillLevel
        )
        {
            _owner = owner;
            _previousUnitId = previousUnitId;
            _previousSkillId = previousSkillId;
            _previousSkillLevel = previousSkillLevel;
        }

        public void Dispose()
        {
            BattleSkillResolutionRules owner = _owner;
            _owner = null;
            owner?.RestoreScopedSkillLevel(
                _previousUnitId,
                _previousSkillId,
                _previousSkillLevel
            );
        }
    }
}
