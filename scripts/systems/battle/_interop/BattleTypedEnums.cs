using Godot;

internal enum BattleAiActionKind
{
    Unknown = 0,
    Move,
    Skill,
    UnitSkill,
}

internal enum BattleAreaPattern
{
    Unknown = 0,
    Single,
    Self,
    Diamond,
    Square,
    Radius,
    Cross,
    Line,
    Cone,
    NarrowCone,
    FrontArc,
    Other,
}

internal enum BattleEffectKind
{
    Unknown = 0,
    Damage,
    ChainDamage,
    Charge,
    ForcedMove,
    PathStepAoe,
    RepeatAttackUntilFail,
    EquipmentDurabilityDamage,
    DispelMagic,
    Heal,
    StaminaRestore,
    Shield,
    LayeredBarrier,
    OnKillGainResources,
    Status,
    ApplyStatus,
    BodySizeCategoryOverride,
    Execute,
    Terrain,
    TerrainReplace,
    TerrainReplaceTo,
    Height,
    HeightDelta,
    TerrainEffect,
    EdgeClear,
}

internal enum BattleForcedMoveMode
{
    Unknown = 0,
    Jump,
    Blink,
}

internal enum BattlePositionObjectiveKind
{
    Unknown = 0,
    None,
    CastDistance,
    DistanceBand,
    DistanceBandProgress,
    DistanceFloor,
}

internal enum BattleTargetFilter
{
    Unknown = 0,
    Any,
    Self,
    Ally,
    Enemy,
}

internal enum BattleTargetMode
{
    Unknown = 0,
    Unit,
    Ground,
}

internal enum BattleTargetSelectionMode
{
    Unknown = 0,
    RandomChain,
}

internal static class BattleTypedNames
{
    public static readonly StringName Empty = "";
    public static readonly StringName ActionKindMove = "move";
    public static readonly StringName ActionKindSkill = "skill";
    public static readonly StringName ActionKindUnitSkill = "unit_skill";
    public static readonly StringName AreaPatternSingle = "single";
    public static readonly StringName AreaPatternSelf = "self";
    public static readonly StringName AreaPatternDiamond = "diamond";
    public static readonly StringName AreaPatternSquare = "square";
    public static readonly StringName AreaPatternRadius = "radius";
    public static readonly StringName AreaPatternCross = "cross";
    public static readonly StringName AreaPatternLine = "line";
    public static readonly StringName AreaPatternCone = "cone";
    public static readonly StringName AreaPatternNarrowCone = "narrow_cone";
    public static readonly StringName AreaPatternFrontArc = "front_arc";
    public static readonly StringName EffectApplyStatus = "apply_status";
    public static readonly StringName EffectBodySizeCategoryOverride =
        "body_size_category_override";
    public static readonly StringName EffectChainDamage = "chain_damage";
    public static readonly StringName EffectCharge = "charge";
    public static readonly StringName EffectDamage = "damage";
    public static readonly StringName EffectDispelMagic = "dispel_magic";
    public static readonly StringName EffectEdgeClear = "edge_clear";
    public static readonly StringName EffectEquipmentDurabilityDamage =
        "equipment_durability_damage";
    public static readonly StringName EffectExecute = "execute";
    public static readonly StringName EffectForcedMove = "forced_move";
    public static readonly StringName EffectHeal = "heal";
    public static readonly StringName EffectHeight = "height";
    public static readonly StringName EffectHeightDelta = "height_delta";
    public static readonly StringName EffectLayeredBarrier = "layered_barrier";
    public static readonly StringName EffectOnKillGainResources = "on_kill_gain_resources";
    public static readonly StringName EffectPathStepAoe = "path_step_aoe";
    public static readonly StringName EffectRepeatAttackUntilFail = "repeat_attack_until_fail";
    public static readonly StringName EffectShield = "shield";
    public static readonly StringName EffectStaminaRestore = "stamina_restore";
    public static readonly StringName EffectStatus = "status";
    public static readonly StringName EffectTerrain = "terrain";
    public static readonly StringName EffectTerrainEffect = "terrain_effect";
    public static readonly StringName EffectTerrainReplace = "terrain_replace";
    public static readonly StringName EffectTerrainReplaceTo = "terrain_replace_to";
    public static readonly StringName ForcedMoveBlink = "blink";
    public static readonly StringName ForcedMoveJump = "jump";
    public static readonly StringName PositionCastDistance = "cast_distance";
    public static readonly StringName PositionDistanceBand = "distance_band";
    public static readonly StringName PositionDistanceBandProgress = "distance_band_progress";
    public static readonly StringName PositionDistanceFloor = "distance_floor";
    public static readonly StringName PositionNone = "none";
    public static readonly StringName TargetFilterAlly = "ally";
    public static readonly StringName TargetFilterAny = "any";
    public static readonly StringName TargetFilterEnemy = "enemy";
    public static readonly StringName TargetFilterSelf = "self";
    public static readonly StringName TargetModeGround = "ground";
    public static readonly StringName TargetModeUnit = "unit";
    public static readonly StringName TargetSelectionRandomChain = "random_chain";

    public static BattleAiActionKind ToAiActionKind(StringName value)
    {
        if (value == ActionKindMove)
            return BattleAiActionKind.Move;
        if (value == ActionKindSkill)
            return BattleAiActionKind.Skill;
        if (value == ActionKindUnitSkill)
            return BattleAiActionKind.UnitSkill;
        return BattleAiActionKind.Unknown;
    }

    public static StringName ToStringName(BattleAiActionKind kind)
    {
        return kind switch
        {
            BattleAiActionKind.Move => ActionKindMove,
            BattleAiActionKind.Skill => ActionKindSkill,
            BattleAiActionKind.UnitSkill => ActionKindUnitSkill,
            _ => Empty,
        };
    }

    public static BattleAreaPattern ToAreaPattern(StringName value)
    {
        if (value == AreaPatternSingle)
            return BattleAreaPattern.Single;
        if (value == AreaPatternSelf)
            return BattleAreaPattern.Self;
        if (value == AreaPatternDiamond)
            return BattleAreaPattern.Diamond;
        if (value == AreaPatternSquare)
            return BattleAreaPattern.Square;
        if (value == AreaPatternRadius)
            return BattleAreaPattern.Radius;
        if (value == AreaPatternCross)
            return BattleAreaPattern.Cross;
        if (value == AreaPatternLine)
            return BattleAreaPattern.Line;
        if (value == AreaPatternCone)
            return BattleAreaPattern.Cone;
        if (value == AreaPatternNarrowCone)
            return BattleAreaPattern.NarrowCone;
        if (value == AreaPatternFrontArc)
            return BattleAreaPattern.FrontArc;
        return GdInterop.IsEmpty(value) ? BattleAreaPattern.Unknown : BattleAreaPattern.Other;
    }

    public static int GetAreaPatternDistanceContractBonus(BattleAreaPattern pattern, int areaValue)
    {
        return GetAreaPatternThreatReachBonus(pattern, areaValue);
    }

    public static int GetAreaPatternThreatReachBonus(BattleAreaPattern pattern, int areaValue)
    {
        int radius = System.Math.Max(areaValue, 0);
        if (radius <= 0)
        {
            return 0;
        }
        return pattern switch
        {
            BattleAreaPattern.Diamond => radius,
            BattleAreaPattern.Cross => radius,
            BattleAreaPattern.Line => radius,
            BattleAreaPattern.FrontArc => radius,
            BattleAreaPattern.NarrowCone => System.Math.Max(radius, 2),
            BattleAreaPattern.Cone => radius * 2,
            BattleAreaPattern.Square => radius * 2,
            BattleAreaPattern.Radius => radius * 2,
            _ => 0,
        };
    }

    public static BattleEffectKind ToEffectKind(StringName value)
    {
        if (value == EffectDamage)
            return BattleEffectKind.Damage;
        if (value == EffectChainDamage)
            return BattleEffectKind.ChainDamage;
        if (value == EffectCharge)
            return BattleEffectKind.Charge;
        if (value == EffectForcedMove)
            return BattleEffectKind.ForcedMove;
        if (value == EffectPathStepAoe)
            return BattleEffectKind.PathStepAoe;
        if (value == EffectRepeatAttackUntilFail)
            return BattleEffectKind.RepeatAttackUntilFail;
        if (value == EffectEquipmentDurabilityDamage)
            return BattleEffectKind.EquipmentDurabilityDamage;
        if (value == EffectDispelMagic)
            return BattleEffectKind.DispelMagic;
        if (value == EffectHeal)
            return BattleEffectKind.Heal;
        if (value == EffectStaminaRestore)
            return BattleEffectKind.StaminaRestore;
        if (value == EffectShield)
            return BattleEffectKind.Shield;
        if (value == EffectLayeredBarrier)
            return BattleEffectKind.LayeredBarrier;
        if (value == EffectOnKillGainResources)
            return BattleEffectKind.OnKillGainResources;
        if (value == EffectStatus)
            return BattleEffectKind.Status;
        if (value == EffectApplyStatus)
            return BattleEffectKind.ApplyStatus;
        if (value == EffectBodySizeCategoryOverride)
            return BattleEffectKind.BodySizeCategoryOverride;
        if (value == EffectExecute)
            return BattleEffectKind.Execute;
        if (value == EffectTerrain)
            return BattleEffectKind.Terrain;
        if (value == EffectTerrainReplace)
            return BattleEffectKind.TerrainReplace;
        if (value == EffectTerrainReplaceTo)
            return BattleEffectKind.TerrainReplaceTo;
        if (value == EffectHeight)
            return BattleEffectKind.Height;
        if (value == EffectHeightDelta)
            return BattleEffectKind.HeightDelta;
        if (value == EffectTerrainEffect)
            return BattleEffectKind.TerrainEffect;
        if (value == EffectEdgeClear)
            return BattleEffectKind.EdgeClear;
        return BattleEffectKind.Unknown;
    }

    public static BattleForcedMoveMode ToForcedMoveMode(StringName value)
    {
        if (value == ForcedMoveJump)
            return BattleForcedMoveMode.Jump;
        if (value == ForcedMoveBlink)
            return BattleForcedMoveMode.Blink;
        return BattleForcedMoveMode.Unknown;
    }

    public static BattlePositionObjectiveKind ToPositionObjectiveKind(StringName value)
    {
        if (value == PositionNone)
            return BattlePositionObjectiveKind.None;
        if (value == PositionCastDistance)
            return BattlePositionObjectiveKind.CastDistance;
        if (value == PositionDistanceBand)
            return BattlePositionObjectiveKind.DistanceBand;
        if (value == PositionDistanceBandProgress)
            return BattlePositionObjectiveKind.DistanceBandProgress;
        if (value == PositionDistanceFloor)
            return BattlePositionObjectiveKind.DistanceFloor;
        return BattlePositionObjectiveKind.Unknown;
    }

    public static StringName ToStringName(BattlePositionObjectiveKind kind)
    {
        return kind switch
        {
            BattlePositionObjectiveKind.None => PositionNone,
            BattlePositionObjectiveKind.CastDistance => PositionCastDistance,
            BattlePositionObjectiveKind.DistanceBand => PositionDistanceBand,
            BattlePositionObjectiveKind.DistanceBandProgress => PositionDistanceBandProgress,
            BattlePositionObjectiveKind.DistanceFloor => PositionDistanceFloor,
            _ => Empty,
        };
    }

    public static BattleTargetFilter ToTargetFilter(StringName value)
    {
        if (value == TargetFilterAny)
            return BattleTargetFilter.Any;
        if (value == TargetFilterSelf)
            return BattleTargetFilter.Self;
        if (value == TargetFilterAlly)
            return BattleTargetFilter.Ally;
        if (value == TargetFilterEnemy)
            return BattleTargetFilter.Enemy;
        return BattleTargetFilter.Unknown;
    }

    public static StringName ToStringName(BattleTargetFilter filter)
    {
        return filter switch
        {
            BattleTargetFilter.Any => TargetFilterAny,
            BattleTargetFilter.Self => TargetFilterSelf,
            BattleTargetFilter.Ally => TargetFilterAlly,
            BattleTargetFilter.Enemy => TargetFilterEnemy,
            _ => Empty,
        };
    }

    public static BattleTargetMode ToTargetMode(StringName value)
    {
        if (value == TargetModeUnit)
            return BattleTargetMode.Unit;
        if (value == TargetModeGround)
            return BattleTargetMode.Ground;
        return BattleTargetMode.Unknown;
    }

    public static StringName ToStringName(BattleTargetMode mode)
    {
        return mode switch
        {
            BattleTargetMode.Unit => TargetModeUnit,
            BattleTargetMode.Ground => TargetModeGround,
            _ => Empty,
        };
    }

    public static BattleTargetSelectionMode ToTargetSelectionMode(StringName value)
    {
        if (value == TargetSelectionRandomChain)
            return BattleTargetSelectionMode.RandomChain;
        return BattleTargetSelectionMode.Unknown;
    }

    public static bool IsAiOffensiveEffect(BattleEffectKind kind)
    {
        return kind
            is BattleEffectKind.Damage
                or BattleEffectKind.ChainDamage
                or BattleEffectKind.Charge
                or BattleEffectKind.ForcedMove
                or BattleEffectKind.PathStepAoe
                or BattleEffectKind.Status;
    }

    public static bool IsGroundPayloadEffect(BattleEffectKind kind)
    {
        return kind
            is BattleEffectKind.Terrain
                or BattleEffectKind.TerrainReplace
                or BattleEffectKind.TerrainReplaceTo
                or BattleEffectKind.Height
                or BattleEffectKind.HeightDelta
                or BattleEffectKind.TerrainEffect
                or BattleEffectKind.EdgeClear;
    }

    public static bool IsUnitPayloadEffect(BattleEffectKind kind)
    {
        return kind
            is BattleEffectKind.Damage
                or BattleEffectKind.ChainDamage
                or BattleEffectKind.EquipmentDurabilityDamage
                or BattleEffectKind.DispelMagic
                or BattleEffectKind.Heal
                or BattleEffectKind.StaminaRestore
                or BattleEffectKind.Shield
                or BattleEffectKind.LayeredBarrier
                or BattleEffectKind.OnKillGainResources
                or BattleEffectKind.Status
                or BattleEffectKind.ApplyStatus
                or BattleEffectKind.BodySizeCategoryOverride
                or BattleEffectKind.ForcedMove
                or BattleEffectKind.Execute;
    }
}
