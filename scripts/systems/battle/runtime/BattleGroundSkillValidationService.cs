using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

internal class BattleGroundSkillValidationService
{
    private WeakReference<BattleRuntimeModule> _runtimeRef;
    private BattleGroundEffectService _owner;
    private BattleGroundRelocationService _relocationService;
    private BattleGroundEffectCoordService _coordService;

    private BattleRuntimeModule _runtime
    {
        get => ResolveWeakRef(_runtimeRef);
        set => _runtimeRef = value != null ? new WeakReference<BattleRuntimeModule>(value) : null;
    }

    internal void Setup(
        BattleRuntimeModule runtime,
        BattleGroundEffectService owner,
        BattleGroundRelocationService relocationService,
        BattleGroundEffectCoordService coordService
    )
    {
        _runtime = runtime;
        _owner = owner;
        _relocationService = relocationService;
        _coordService = coordService;
    }

    internal int ActiveDependencyCount =>
        (_runtime != null ? 1 : 0)
        + (_owner != null ? 1 : 0)
        + (_relocationService != null ? 1 : 0)
        + (_coordService != null ? 1 : 0);

    internal void DisposeRuntime()
    {
        _coordService = null;
        _relocationService = null;
        _owner = null;
        _runtime = null;
    }

    private static BattleRuntimeModule ResolveWeakRef(WeakReference<BattleRuntimeModule> weakRef)
    {
        if (weakRef == null || !weakRef.TryGetTarget(out BattleRuntimeModule target))
        {
            return null;
        }
        return target;
    }

    private static readonly StringName Empty = "";

    private BattleRuntimeModule Runtime => _runtime;
    private BattleState State => Runtime?._state;
    private BattleGridService GridService => Runtime?._grid_service;


    internal string GetGroundSpecialEffectValidationMessage(
        BattleUnitState activeUnit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        IReadOnlyList<Vector2I> targetCoords
    )
    {
        CombatEffectDefinition relocationEffectDefinition =
            _relocationService._get_ground_relocation_effect_definition(
                skillDefinition,
                castVariantDefinition
            );
        if (relocationEffectDefinition == null)
        {
            return "";
        }
        if (activeUnit == null || State == null)
        {
            return "位移落点无效。";
        }
        if (_owner._is_movement_blocked(activeUnit))
        {
            return "当前状态下无法移动。";
        }
        if (targetCoords == null || targetCoords.Count == 0)
        {
            return "位移落点无效。";
        }
        return _relocationService._can_use_ground_relocation(
            activeUnit,
            targetCoords[0],
            relocationEffectDefinition
        )
            ? ""
            : "目标地格无法作为位移落点。";
    }

    internal string GetGroundSpecialEffectValidationMessage(
        BattleUnitReadView activeUnit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        IReadOnlyList<Vector2I> targetCoords
    )
    {
        CombatEffectDefinition relocationEffectDefinition =
            _relocationService._get_ground_relocation_effect_definition(
                skillDefinition,
                castVariantDefinition
            );
        if (relocationEffectDefinition == null)
        {
            return "";
        }
        if (!activeUnit.IsValid || State == null)
        {
            return "位移落点无效。";
        }
        if (_owner._is_movement_blocked(activeUnit))
        {
            return "当前状态下无法移动。";
        }
        if (targetCoords == null || targetCoords.Count == 0)
        {
            return "位移落点无效。";
        }
        return _relocationService._can_use_ground_relocation(
            activeUnit,
            targetCoords[0],
            relocationEffectDefinition
        )
            ? ""
            : "目标地格无法作为位移落点。";
    }

    internal BattleGroundSkillValidationResult _validate_ground_skill_command_result(
        BattleUnitState activeUnit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        BattleCommand command
    )
    {
        List<Vector2I> normalizedCoords = NormalizeTargetCoordsTyped(command);
        BattleGroundSkillValidationResult deniedResult =
            BattleGroundSkillValidationResult.Denied(
                "地面技能目标无效。",
                new List<Vector2I>(normalizedCoords)
            );
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (
            State == null
            || activeUnit == null
            || skillDefinition == null
            || combatProfile == null
            || castVariantDefinition == null
        )
        {
            return deniedResult;
        }
        if (ResolveGroundCastTargetMode(skillDefinition, castVariantDefinition) != BattleTargetMode.Ground)
        {
            return deniedResult with { Message = "该技能形态不是地面施法。" };
        }
        BattleSkillCastBlockReasonKind blockReason = _owner._get_skill_cast_block_reason(
            activeUnit,
            skillDefinition
        );
        if (BattleSkillCastBlockReasonKinds.IsBlocked(blockReason))
        {
            return deniedResult with
            {
                Message =
                    Runtime?._get_skill_cast_block_message(activeUnit, skillDefinition)
                    ?? "正式技能检查未绑定，无法施放该技能。",
            };
        }
        int requiredCoordCount = ResolveGroundRequiredCoordCount(castVariantDefinition);
        if (normalizedCoords.Count != requiredCoordCount)
        {
            return deniedResult
                with
                {
                    Message = $"该技能形态需要选择 {requiredCoordCount} 个地格。",
                };
        }
        BattleChargeResolver chargeResolver = Runtime?._charge_resolver;
        if (castVariantDefinition != null && chargeResolver != null && chargeResolver.IsChargeOption(castVariantDefinition))
        {
            return chargeResolver.ValidateChargeCommandResult(
                activeUnit,
                skillDefinition,
                castVariantDefinition,
                normalizedCoords,
                deniedResult
            );
        }

        CombatEffectDefinition relocationEffectDefinition =
            _relocationService._get_ground_relocation_effect_definition(
                skillDefinition,
                castVariantDefinition
            );
        int effectiveSkillRange = _owner._get_effective_skill_range(activeUnit, skillDefinition);
        var seenCoords = new HashSet<Vector2I>();
        foreach (var rawCoord in normalizedCoords)
        {
            Vector2I coord = rawCoord;
            if (!seenCoords.Add(coord))
            {
                return deniedResult with { Message = "同一地格不能重复选择。" };
            }
            if (!GridService.IsInside(State, coord))
            {
                return deniedResult with { Message = "存在超出战场范围的目标地格。" };
            }
            int targetDistance =
                relocationEffectDefinition != null
                    ? GridService.GetChebyshevDistance(
                        activeUnit.GetAnchorCoord(),
                        coord
                    )
                    : GridService.GetDistanceFromUnitToCoord(
                        activeUnit,
                        coord
                    );
            if (targetDistance > effectiveSkillRange)
            {
                return deniedResult with { Message = "目标地格超出技能施放距离。" };
            }
            string casterLineMessage = GetCasterTargetVectorLineValidationMessage(
                activeUnit.GetAnchorCoord(),
                coord,
                combatProfile
            );
            if (!string.IsNullOrEmpty(casterLineMessage))
            {
                return deniedResult with { Message = casterLineMessage };
            }
            if (!GridService.HasCell(State, coord))
            {
                return deniedResult with { Message = "目标地格数据不可用。" };
            }
            if (castVariantDefinition.AllowedBaseTerrains.Count > 0)
            {
                bool normalizedAllowed = false;
                StringName normalizedCellTerrain = BattleTerrainRules.NormalizeTerrainId(
                    GridService.GetCellBaseTerrainId(State, coord)
                );
                foreach (StringName rawAllowedTerrain in castVariantDefinition.AllowedBaseTerrains)
                {
                    if (
                        BattleTerrainRules.NormalizeTerrainId(rawAllowedTerrain)
                        == normalizedCellTerrain
                    )
                    {
                        normalizedAllowed = true;
                        break;
                    }
                }
                if (!normalizedAllowed)
                {
                    return deniedResult with { Message = "目标地格地形不符合该技能形态的要求。" };
                }
            }
            if (_owner._is_crown_break_skill(skillDefinition.SkillId))
            {
                BattleUnitState targetUnit = GridService.GetUnitAtCoord(State, coord);
                if (!_owner._is_crown_break_target_eligible(activeUnit, targetUnit))
                {
                    return deniedResult
                        with
                        {
                            Message = "折冠只能对已被黑星烙印的 elite / boss 施放。",
                        };
                }
            }
        }
        if (
            !_validate_target_coords_shape(
                ResolveGroundFootprintPattern(castVariantDefinition),
                normalizedCoords
            )
        )
        {
            return deniedResult with { Message = "目标地格排布不符合该技能形态。" };
        }
        IReadOnlyList<Vector2I> sortedTargetCoords = BattleGroundEffectCoordService.SortCoordsTyped(normalizedCoords);
        string groundExecuteMessage = GetGroundExecuteValidationMessage(
            skillDefinition,
            castVariantDefinition,
            activeUnit
        );
        if (!string.IsNullOrEmpty(groundExecuteMessage))
        {
            return deniedResult with { Message = groundExecuteMessage };
        }
        string specialValidationMessage = GetGroundSpecialEffectValidationMessage(
            activeUnit,
            skillDefinition,
            castVariantDefinition,
            sortedTargetCoords
        );
        if (!string.IsNullOrEmpty(specialValidationMessage))
        {
            return deniedResult with { Message = specialValidationMessage };
        }
        string targetRequirementMessage =
            GetGroundTargetRequirementValidationMessage(
                activeUnit,
                skillDefinition,
                castVariantDefinition,
                sortedTargetCoords
            );
        if (!string.IsNullOrEmpty(targetRequirementMessage))
        {
            return deniedResult with
            {
                Message = targetRequirementMessage,
            };
        }
        return BattleGroundSkillValidationResult.AllowedResult(
            "可施放。",
            new List<Vector2I>(sortedTargetCoords)
        );
    }

    internal BattleGroundSkillValidationResult _validate_ground_skill_command_result(
        BattleUnitReadView activeUnit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        BattleCommand command
    )
    {
        List<Vector2I> normalizedCoords = NormalizeTargetCoordsTyped(command);
        BattleGroundSkillValidationResult deniedResult =
            BattleGroundSkillValidationResult.Denied(
                "地面技能目标无效。",
                new List<Vector2I>(normalizedCoords)
            );
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (
            State == null
            || !activeUnit.IsValid
            || skillDefinition == null
            || combatProfile == null
            || castVariantDefinition == null
        )
        {
            return deniedResult;
        }
        if (ResolveGroundCastTargetMode(skillDefinition, castVariantDefinition) != BattleTargetMode.Ground)
        {
            return deniedResult with { Message = "该技能形态不是地面施法。" };
        }
        string blockReason = Runtime?._get_skill_command_block_reason(
            activeUnit,
            skillDefinition,
            castVariantDefinition
        ) ?? "正式技能检查未绑定，无法施放该技能。";
        if (!string.IsNullOrEmpty(blockReason))
        {
            return deniedResult with { Message = blockReason };
        }
        int requiredCoordCount = ResolveGroundRequiredCoordCount(castVariantDefinition);
        if (normalizedCoords.Count != requiredCoordCount)
        {
            return deniedResult
                with
                {
                    Message = $"该技能形态需要选择 {requiredCoordCount} 个地格。",
                };
        }
        BattleChargeResolver chargeResolver = Runtime?._charge_resolver;
        if (castVariantDefinition != null && chargeResolver != null && chargeResolver.IsChargeOption(castVariantDefinition))
        {
            return chargeResolver.ValidateChargeCommandResult(
                activeUnit,
                skillDefinition,
                castVariantDefinition,
                normalizedCoords,
                deniedResult
            );
        }

        CombatEffectDefinition relocationEffectDefinition =
            _relocationService._get_ground_relocation_effect_definition(
                skillDefinition,
                castVariantDefinition
            );
        int effectiveSkillRange = _owner._get_effective_skill_range(activeUnit, skillDefinition);
        var seenCoords = new HashSet<Vector2I>();
        foreach (var rawCoord in normalizedCoords)
        {
            Vector2I coord = rawCoord;
            if (!seenCoords.Add(coord))
            {
                return deniedResult with { Message = "同一地格不能重复选择。" };
            }
            if (!GridService.IsInside(State, coord))
            {
                return deniedResult with { Message = "存在超出战场范围的目标地格。" };
            }
            int targetDistance =
                relocationEffectDefinition != null
                    ? GridService.GetChebyshevDistance(
                        activeUnit.Coord,
                        coord
                    )
                    : GridService.GetDistanceFromUnitToCoord(
                        activeUnit,
                        coord
                    );
            if (targetDistance > effectiveSkillRange)
            {
                return deniedResult with { Message = "目标地格超出技能施放距离。" };
            }
            string casterLineMessage = GetCasterTargetVectorLineValidationMessage(
                activeUnit.Coord,
                coord,
                combatProfile
            );
            if (!string.IsNullOrEmpty(casterLineMessage))
            {
                return deniedResult with { Message = casterLineMessage };
            }
            if (!GridService.HasCell(State, coord))
            {
                return deniedResult with { Message = "目标地格数据不可用。" };
            }
            if (castVariantDefinition.AllowedBaseTerrains.Count > 0)
            {
                bool normalizedAllowed = false;
                StringName normalizedCellTerrain = BattleTerrainRules.NormalizeTerrainId(
                    GridService.GetCellBaseTerrainId(State, coord)
                );
                foreach (StringName rawAllowedTerrain in castVariantDefinition.AllowedBaseTerrains)
                {
                    if (
                        BattleTerrainRules.NormalizeTerrainId(rawAllowedTerrain)
                        == normalizedCellTerrain
                    )
                    {
                        normalizedAllowed = true;
                        break;
                    }
                }
                if (!normalizedAllowed)
                {
                    return deniedResult with { Message = "目标地格地形不符合该技能形态的要求。" };
                }
            }
            if (_owner._is_crown_break_skill(skillDefinition.SkillId))
            {
                BattleUnitState targetUnit = GridService.GetUnitAtCoord(State, coord);
                if (!_owner._is_crown_break_target_eligible(activeUnit, new BattleUnitReadView(targetUnit)))
                {
                    return deniedResult
                        with
                        {
                            Message = "折冠只能对已被黑星烙印的 elite / boss 施放。",
                        };
                }
            }
        }
        if (
            !_validate_target_coords_shape(
                ResolveGroundFootprintPattern(castVariantDefinition),
                normalizedCoords
            )
        )
        {
            return deniedResult with { Message = "目标地格排布不符合该技能形态。" };
        }
        IReadOnlyList<Vector2I> sortedTargetCoords = BattleGroundEffectCoordService.SortCoordsTyped(normalizedCoords);
        string groundExecuteMessage = GetGroundExecuteValidationMessage(
            skillDefinition,
            castVariantDefinition,
            activeUnit
        );
        if (!string.IsNullOrEmpty(groundExecuteMessage))
        {
            return deniedResult with { Message = groundExecuteMessage };
        }
        string specialValidationMessage = GetGroundSpecialEffectValidationMessage(
            activeUnit,
            skillDefinition,
            castVariantDefinition,
            sortedTargetCoords
        );
        if (!string.IsNullOrEmpty(specialValidationMessage))
        {
            return deniedResult with { Message = specialValidationMessage };
        }
        string targetRequirementMessage =
            GetGroundTargetRequirementValidationMessage(
                activeUnit,
                skillDefinition,
                castVariantDefinition,
                sortedTargetCoords
            );
        if (!string.IsNullOrEmpty(targetRequirementMessage))
        {
            return deniedResult with
            {
                Message = targetRequirementMessage,
            };
        }
        return BattleGroundSkillValidationResult.AllowedResult(
            "可施放。",
            new List<Vector2I>(sortedTargetCoords)
        );
    }

    private string GetGroundExecuteValidationMessage(
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        BattleUnitState activeUnit
    )
    {
        foreach (
            CombatEffectDefinition effectDefinition in _coordService.CollectGroundUnitEffectDefinitions(
                skillDefinition,
                castVariantDefinition,
                activeUnit
            )
        )
        {
            if (effectDefinition?.EffectKind == BattleEffectKind.Execute)
            {
                return "地面技能不能携带律令死亡。";
            }
        }
        return "";
    }

    private string GetGroundTargetRequirementValidationMessage(
        BattleUnitState activeUnit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        IReadOnlyList<Vector2I> targetCoords
    )
    {
        IReadOnlyList<CombatEffectDefinition> effects =
            _coordService.CollectGroundUnitEffectDefinitions(
                skillDefinition,
                castVariantDefinition,
                activeUnit
            );
        if (!AllUnitEffectsRequireQualifiedTargets(effects))
            return "";
        IReadOnlyList<Vector2I> effectCoords =
            Runtime?.BuildGroundEffectCoordsTyped(
                skillDefinition,
                targetCoords,
                activeUnit.GetAnchorCoord(),
                activeUnit,
                castVariantDefinition
            ) ?? Array.Empty<Vector2I>();
        foreach (
            BattleUnitState targetUnit
            in _coordService.CollectUnitsInCoords(effectCoords)
        )
        {
            foreach (CombatEffectDefinition effect in effects)
            {
                if (
                    _owner._is_unit_valid_for_effect(
                        activeUnit,
                        targetUnit,
                        _owner.ResolveEffectTargetFilter(
                            skillDefinition,
                            effect
                        )
                    )
                    && BattleEffectTargetRequirementRules.IsSatisfied(
                        effect,
                        targetUnit
                    )
                )
                {
                    return "";
                }
            }
        }
        return "范围内没有满足效果要求的有效目标。";
    }

    private string GetGroundTargetRequirementValidationMessage(
        BattleUnitReadView activeUnit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        IReadOnlyList<Vector2I> targetCoords
    )
    {
        IReadOnlyList<CombatEffectDefinition> effects =
            _coordService.CollectGroundUnitEffectDefinitions(
                skillDefinition,
                castVariantDefinition,
                activeUnit
            );
        if (!AllUnitEffectsRequireQualifiedTargets(effects))
            return "";
        IReadOnlyList<Vector2I> effectCoords =
            Runtime?.BuildGroundEffectCoordsTyped(
                skillDefinition,
                targetCoords,
                activeUnit.Coord,
                activeUnit,
                castVariantDefinition
            ) ?? Array.Empty<Vector2I>();
        foreach (
            BattleUnitState targetState
            in _coordService.CollectUnitsInCoords(effectCoords)
        )
        {
            BattleUnitReadView targetUnit = new(targetState);
            foreach (CombatEffectDefinition effect in effects)
            {
                if (
                    _owner._is_unit_valid_for_effect(
                        activeUnit,
                        targetUnit,
                        _owner.ResolveEffectTargetFilter(
                            skillDefinition,
                            effect
                        )
                    )
                    && BattleEffectTargetRequirementRules.IsSatisfied(
                        effect,
                        targetUnit
                    )
                )
                {
                    return "";
                }
            }
        }
        return "范围内没有满足效果要求的有效目标。";
    }

    private static bool AllUnitEffectsRequireQualifiedTargets(
        IReadOnlyList<CombatEffectDefinition> effects
    )
    {
        bool sawEffect = false;
        foreach (
            CombatEffectDefinition effect
            in effects ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (effect == null)
                continue;
            sawEffect = true;
            bool hasCreatureTypeRequirement =
                effect.RequiredTargetCreatureTypeTag != "";
            bool hasCognitionRequirement =
                BattleCognitionContentRules.IsKnown(
                    effect.RequiredTargetMinCognition
                );
            if (
                !hasCreatureTypeRequirement
                && !hasCognitionRequirement
            )
            {
                return false;
            }
        }
        return sawEffect;
    }

    private string GetGroundExecuteValidationMessage(
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        BattleUnitReadView activeUnit
    )
    {
        foreach (
            CombatEffectDefinition effectDefinition in _coordService.CollectGroundUnitEffectDefinitions(
                skillDefinition,
                castVariantDefinition,
                activeUnit
            )
        )
        {
            if (effectDefinition?.EffectKind == BattleEffectKind.Execute)
            {
                return "地面技能不能携带律令死亡。";
            }
        }
        return "";
    }

    private static BattleTargetMode ResolveGroundCastTargetMode(
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition
    )
    {
        if (castVariantDefinition == null)
        {
            return BattleTargetMode.Unknown;
        }
        BattleTargetMode targetMode = castVariantDefinition.TargetModeKind;
        return targetMode != BattleTargetMode.Unknown
            ? targetMode
            : skillDefinition?.CombatProfile?.TargetModeKind ?? BattleTargetMode.Unknown;
    }

    private static int ResolveGroundRequiredCoordCount(
        CombatCastVariantDefinition castVariantDefinition
    )
    {
        return Math.Max(castVariantDefinition?.RequiredCoordCount ?? 0, 0);
    }

    private static string GetCasterTargetVectorLineValidationMessage(
        Vector2I sourceCoord,
        Vector2I targetCoord,
        CombatSkillDefinition combatProfile
    )
    {
        if (
            combatProfile == null
            || BattleTypedNames.ToAreaPattern(combatProfile.AreaPattern) != BattleAreaPattern.Line
            || combatProfile.AreaOriginModeKind != CombatAreaOriginMode.Caster
            || combatProfile.AreaDirectionModeKind != CombatAreaDirectionMode.TargetVector
        )
        {
            return "";
        }
        Vector2I delta = targetCoord - sourceCoord;
        if (delta == Vector2I.Zero)
        {
            return "直线技能不能选择使用者所在格。";
        }
        if (delta.X != 0 && delta.Y != 0)
        {
            return "直线技能目标必须与使用者同行或同列。";
        }
        return "";
    }

    private static CombatCastFootprintPattern ResolveGroundFootprintPattern(
        CombatCastVariantDefinition castVariantDefinition
    )
    {
        return castVariantDefinition?.FootprintPatternKind ?? CombatCastFootprintPattern.Unknown;
    }

    private static bool IsChargeOption(CombatCastVariantDefinition castVariantDefinition)
    {
        foreach (
            CombatEffectDefinition effectDefinition in castVariantDefinition?.EffectDefinitions
                ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (effectDefinition?.EffectKind == BattleEffectKind.Charge)
            {
                return true;
            }
        }
        return false;
    }

    internal bool _validate_target_coords_shape(
        CombatCastFootprintPattern footprint_pattern,
        IReadOnlyList<Vector2I> target_coords
    )
    {
        if (footprint_pattern == CombatCastFootprintPattern.Single)
        {
            return target_coords != null && target_coords.Count == 1;
        }
        if (footprint_pattern == CombatCastFootprintPattern.Line2)
        {
            if (target_coords == null || target_coords.Count != 2)
            {
                return false;
            }
            Vector2I first = target_coords[0];
            Vector2I second = target_coords[1];
            return (first.X == second.X && Math.Abs(first.Y - second.Y) == 1)
                || (first.Y == second.Y && Math.Abs(first.X - second.X) == 1);
        }
        if (footprint_pattern == CombatCastFootprintPattern.Square2)
        {
            if (target_coords == null || target_coords.Count != 4)
            {
                return false;
            }
            Vector2I firstCoord = target_coords[0];
            int minX = firstCoord.X;
            int maxX = firstCoord.X;
            int minY = firstCoord.Y;
            int maxY = firstCoord.Y;
            var coordSet = new HashSet<Vector2I>();
            foreach (Vector2I coord in target_coords)
            {
                minX = Math.Min(minX, coord.X);
                maxX = Math.Max(maxX, coord.X);
                minY = Math.Min(minY, coord.Y);
                maxY = Math.Max(maxY, coord.Y);
                coordSet.Add(coord);
            }
            if (maxX - minX != 1 || maxY - minY != 1)
            {
                return false;
            }
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    if (!coordSet.Contains(new Vector2I(x, y)))
                    {
                        return false;
                    }
                }
            }
            return true;
        }
        if (footprint_pattern == CombatCastFootprintPattern.Unordered)
        {
            return target_coords != null && target_coords.Count > 0;
        }
        return false;
    }

    internal Godot.Collections.Array<Vector2I> _normalize_target_coords(BattleCommand command)
    {
        var result = new Godot.Collections.Array<Vector2I>();
        foreach (Vector2I coord in NormalizeTargetCoordsTyped(command))
            result.Add(coord);
        return result;
    }

    internal List<Vector2I> NormalizeTargetCoordsTyped(BattleCommand command)
    {
        var coords = new List<Vector2I>();
        if (command == null)
        {
            return coords;
        }
        foreach (Vector2I targetCoord in command.TargetCoordsTyped)
        {
            coords.Add(targetCoord);
        }
        if (coords.Count == 0 && command.target_coord != new Vector2I(-1, -1))
        {
            coords.Add(command.target_coord);
        }
        return coords;
    }
}
