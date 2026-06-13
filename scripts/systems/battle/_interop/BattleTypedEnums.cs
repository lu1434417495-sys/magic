using Godot;

internal enum BattleAiActionKind
{
    Unknown = 0,
    Move,
    Skill,
    UnitSkill,
}

internal enum BattleCommandKind
{
    Unknown = 0,
    Move,
    Skill,
    Wait,
    ChangeEquipment,
    CancelCast,
}

public enum PendingCastBindingModeKind
{
    SoftAnchor = 0,
    HardAnchor,
    GroundBind,
}

internal enum PendingCastRefundPolicy
{
    None = 0,
    HalfResources,
}

internal enum BattleEquipmentOperationKind
{
    Unknown = 0,
    Equip,
    Unequip,
}

internal enum BattlePhaseKind
{
    Unknown = 0,
    TimelineRunning,
    UnitActing,
    BattleEnded,
}

internal enum BattleModalStateKind
{
    None = 0,
    Unknown,
    StartConfirm,
    PromotionChoice,
}

internal enum BattleUnitControlMode
{
    Unknown = 0,
    Manual,
    Ai,
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
    HealFatal,
    StaminaRestore,
    Shield,
    LayeredBarrier,
    OnKillGainResources,
    Status,
    ApplyStatus,
    EraseStatus,
    CleanseHarmful,
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
    WindPush,
    Evasive,
    Retreat,
    Knockback,
    Reposition,
}

internal enum BattleTerrainEffectRuntimeKind
{
    Unknown = 0,
    Damage,
    MovementCost,
    Status,
    None,
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
    SingleUnit,
    MultiUnit,
    RandomChain,
    Self,
    SingleCoord,
    CoordPair,
    Movement,
}

internal enum BattleTargetSelectionOrderMode
{
    Unknown = 0,
    Stable,
    Manual,
}

internal enum CombatSkillMasteryTriggerMode
{
    Unknown = 0,
    SkillDamageDiceMax,
    WeaponAttackQuality,
    DamageDealt,
    StatusApplied,
    EffectApplied,
    IncomingPhysicalHit,
    SecondaryHit,
}

internal enum CombatSkillMasteryAmountMode
{
    Unknown = 0,
    PerTargetRank,
    PerCastHpRatio,
}

internal enum EnemyTargetRankKind
{
    Unknown = 0,
    Normal,
    Elite,
    Boss,
}

internal static class BattleTypedNames
{
    internal static readonly StringName Empty = "";
    internal static readonly StringName ActionKindMove = "move";
    internal static readonly StringName ActionKindSkill = "skill";
    internal static readonly StringName ActionKindUnitSkill = "unit_skill";
    internal static readonly StringName CommandMove = "move";
    internal static readonly StringName CommandSkill = "skill";
    internal static readonly StringName CommandWait = "wait";
    internal static readonly StringName CommandChangeEquipment = "change_equipment";
    internal static readonly StringName CommandCancelCast = "cancel_cast";
    internal static readonly StringName EquipmentOperationEquip = "equip";
    internal static readonly StringName EquipmentOperationUnequip = "unequip";
    internal static readonly StringName PendingCastBindingSoftAnchor = "soft_anchor";
    internal static readonly StringName PendingCastBindingHardAnchor = "hard_anchor";
    internal static readonly StringName PendingCastBindingGroundBind = "ground_bind";
    internal static readonly StringName PhaseTimelineRunning = "timeline_running";
    internal static readonly StringName PhaseUnitActing = "unit_acting";
    internal static readonly StringName PhaseBattleEnded = "battle_ended";
    internal static readonly StringName ModalStartConfirm = "start_confirm";
    internal static readonly StringName ModalPromotionChoice = "promotion_choice";
    internal static readonly StringName ControlModeManual = "manual";
    internal static readonly StringName ControlModeAi = "ai";
    internal static readonly StringName AreaPatternSingle = "single";
    internal static readonly StringName AreaPatternSelf = "self";
    internal static readonly StringName AreaPatternDiamond = "diamond";
    internal static readonly StringName AreaPatternSquare = "square";
    internal static readonly StringName AreaPatternRadius = "radius";
    internal static readonly StringName AreaPatternCross = "cross";
    internal static readonly StringName AreaPatternLine = "line";
    internal static readonly StringName AreaPatternCone = "cone";
    internal static readonly StringName AreaPatternNarrowCone = "narrow_cone";
    internal static readonly StringName AreaPatternFrontArc = "front_arc";
    internal static readonly StringName EffectApplyStatus = "apply_status";
    internal static readonly StringName EffectBodySizeCategoryOverride =
        "body_size_category_override";
    internal static readonly StringName EffectChainDamage = "chain_damage";
    internal static readonly StringName EffectCharge = "charge";
    internal static readonly StringName EffectDamage = "damage";
    internal static readonly StringName EffectDispelMagic = "dispel_magic";
    internal static readonly StringName EffectEdgeClear = "edge_clear";
    internal static readonly StringName EffectEquipmentDurabilityDamage =
        "equipment_durability_damage";
    internal static readonly StringName EffectExecute = "execute";
    internal static readonly StringName EffectForcedMove = "forced_move";
    internal static readonly StringName EffectHeal = "heal";
    internal static readonly StringName EffectHealFatal = "heal_fatal";
    internal static readonly StringName EffectHeight = "height";
    internal static readonly StringName EffectHeightDelta = "height_delta";
    internal static readonly StringName EffectLayeredBarrier = "layered_barrier";
    internal static readonly StringName EffectOnKillGainResources = "on_kill_gain_resources";
    internal static readonly StringName EffectPathStepAoe = "path_step_aoe";
    internal static readonly StringName EffectRepeatAttackUntilFail = "repeat_attack_until_fail";
    internal static readonly StringName EffectShield = "shield";
    internal static readonly StringName EffectStaminaRestore = "stamina_restore";
    internal static readonly StringName EffectStatus = "status";
    internal static readonly StringName EffectEraseStatus = "erase_status";
    internal static readonly StringName EffectCleanseHarmful = "cleanse_harmful";
    internal static readonly StringName EffectTerrain = "terrain";
    internal static readonly StringName EffectTerrainEffect = "terrain_effect";
    internal static readonly StringName EffectTerrainReplace = "terrain_replace";
    internal static readonly StringName EffectTerrainReplaceTo = "terrain_replace_to";
    internal static readonly StringName ForcedMoveBlink = "blink";
    internal static readonly StringName ForcedMoveJump = "jump";
    internal static readonly StringName ForcedMoveWindPush = "wind_push";
    internal static readonly StringName ForcedMoveEvasive = "evasive";
    internal static readonly StringName ForcedMoveRetreat = "retreat";
    internal static readonly StringName ForcedMoveKnockback = "knockback";
    internal static readonly StringName ForcedMoveReposition = "reposition";
    internal static readonly StringName TerrainEffectRuntimeMovementCost = "movement_cost";
    internal static readonly StringName TerrainEffectRuntimeNone = "none";
    internal static readonly StringName PositionCastDistance = "cast_distance";
    internal static readonly StringName PositionDistanceBand = "distance_band";
    internal static readonly StringName PositionDistanceBandProgress = "distance_band_progress";
    internal static readonly StringName PositionDistanceFloor = "distance_floor";
    internal static readonly StringName PositionNone = "none";
    internal static readonly StringName TargetFilterAlly = "ally";
    internal static readonly StringName TargetFilterAny = "any";
    internal static readonly StringName TargetFilterEnemy = "enemy";
    internal static readonly StringName TargetFilterSelf = "self";
    internal static readonly StringName TargetModeGround = "ground";
    internal static readonly StringName TargetModeUnit = "unit";
    internal static readonly StringName TargetSelectionSingleUnit = "single_unit";
    internal static readonly StringName TargetSelectionMultiUnit = "multi_unit";
    internal static readonly StringName TargetSelectionRandomChain = "random_chain";
    internal static readonly StringName TargetSelectionSelf = "self";
    internal static readonly StringName TargetSelectionSingleCoord = "single_coord";
    internal static readonly StringName TargetSelectionCoordPair = "coord_pair";
    internal static readonly StringName TargetSelectionMovement = "movement";
    internal static readonly StringName TargetSelectionOrderStable = "stable";
    internal static readonly StringName TargetSelectionOrderManual = "manual";
    internal static readonly StringName MasteryTriggerSkillDamageDiceMax =
        "skill_damage_dice_max";
    internal static readonly StringName MasteryTriggerWeaponAttackQuality =
        "weapon_attack_quality";
    internal static readonly StringName MasteryTriggerDamageDealt = "damage_dealt";
    internal static readonly StringName MasteryTriggerStatusApplied = "status_applied";
    internal static readonly StringName MasteryTriggerEffectApplied = "effect_applied";
    internal static readonly StringName MasteryTriggerIncomingPhysicalHit =
        "incoming_physical_hit";
    internal static readonly StringName MasteryTriggerSecondaryHit = "secondary_hit";
    internal static readonly StringName MasteryAmountPerTargetRank = "per_target_rank";
    internal static readonly StringName MasteryAmountPerCastHpRatio = "per_cast_hp_ratio";
    internal static readonly StringName EnemyTargetRankNormal = "normal";
    internal static readonly StringName EnemyTargetRankElite = "elite";
    internal static readonly StringName EnemyTargetRankBoss = "boss";

    internal static BattleAiActionKind ToAiActionKind(StringName value)
    {
        if (value == ActionKindMove)
            return BattleAiActionKind.Move;
        if (value == ActionKindSkill)
            return BattleAiActionKind.Skill;
        if (value == ActionKindUnitSkill)
            return BattleAiActionKind.UnitSkill;
        return BattleAiActionKind.Unknown;
    }

    internal static StringName ToStringName(BattleAiActionKind kind)
    {
        return kind switch
        {
            BattleAiActionKind.Move => ActionKindMove,
            BattleAiActionKind.Skill => ActionKindSkill,
            BattleAiActionKind.UnitSkill => ActionKindUnitSkill,
            _ => Empty,
        };
    }

    internal static BattleCommandKind ToCommandKind(StringName value)
    {
        if (value == CommandMove)
            return BattleCommandKind.Move;
        if (value == CommandSkill)
            return BattleCommandKind.Skill;
        if (value == CommandWait)
            return BattleCommandKind.Wait;
        if (value == CommandChangeEquipment)
            return BattleCommandKind.ChangeEquipment;
        if (value == CommandCancelCast)
            return BattleCommandKind.CancelCast;
        return BattleCommandKind.Unknown;
    }

    internal static StringName ToStringName(BattleCommandKind kind)
    {
        return kind switch
        {
            BattleCommandKind.Move => CommandMove,
            BattleCommandKind.Skill => CommandSkill,
            BattleCommandKind.Wait => CommandWait,
            BattleCommandKind.ChangeEquipment => CommandChangeEquipment,
            BattleCommandKind.CancelCast => CommandCancelCast,
            _ => Empty,
        };
    }

    internal static PendingCastBindingModeKind ToPendingCastBindingMode(StringName value)
    {
        if (value == PendingCastBindingHardAnchor)
            return PendingCastBindingModeKind.HardAnchor;
        if (value == PendingCastBindingGroundBind)
            return PendingCastBindingModeKind.GroundBind;
        return PendingCastBindingModeKind.SoftAnchor;
    }

    internal static StringName ToStringName(PendingCastBindingModeKind kind)
    {
        return kind switch
        {
            PendingCastBindingModeKind.HardAnchor => PendingCastBindingHardAnchor,
            PendingCastBindingModeKind.GroundBind => PendingCastBindingGroundBind,
            _ => PendingCastBindingSoftAnchor,
        };
    }

    internal static BattleEquipmentOperationKind ToEquipmentOperationKind(StringName value)
    {
        if (value == EquipmentOperationEquip)
            return BattleEquipmentOperationKind.Equip;
        if (value == EquipmentOperationUnequip)
            return BattleEquipmentOperationKind.Unequip;
        return BattleEquipmentOperationKind.Unknown;
    }

    internal static StringName ToStringName(BattleEquipmentOperationKind kind)
    {
        return kind switch
        {
            BattleEquipmentOperationKind.Equip => EquipmentOperationEquip,
            BattleEquipmentOperationKind.Unequip => EquipmentOperationUnequip,
            _ => Empty,
        };
    }

    internal static BattlePhaseKind ToPhaseKind(StringName value)
    {
        if (value == PhaseTimelineRunning)
            return BattlePhaseKind.TimelineRunning;
        if (value == PhaseUnitActing)
            return BattlePhaseKind.UnitActing;
        if (value == PhaseBattleEnded)
            return BattlePhaseKind.BattleEnded;
        return BattlePhaseKind.Unknown;
    }

    internal static StringName ToStringName(BattlePhaseKind kind)
    {
        return kind switch
        {
            BattlePhaseKind.TimelineRunning => PhaseTimelineRunning,
            BattlePhaseKind.UnitActing => PhaseUnitActing,
            BattlePhaseKind.BattleEnded => PhaseBattleEnded,
            _ => Empty,
        };
    }

    internal static BattleModalStateKind ToModalStateKind(StringName value)
    {
        if (value == null || value == Empty)
            return BattleModalStateKind.None;
        if (value == ModalStartConfirm)
            return BattleModalStateKind.StartConfirm;
        if (value == ModalPromotionChoice)
            return BattleModalStateKind.PromotionChoice;
        return BattleModalStateKind.Unknown;
    }

    internal static StringName ToStringName(BattleModalStateKind kind)
    {
        return kind switch
        {
            BattleModalStateKind.None => Empty,
            BattleModalStateKind.StartConfirm => ModalStartConfirm,
            BattleModalStateKind.PromotionChoice => ModalPromotionChoice,
            _ => Empty,
        };
    }

    internal static BattleUnitControlMode ToControlMode(StringName value)
    {
        if (value == ControlModeManual)
            return BattleUnitControlMode.Manual;
        if (value == ControlModeAi)
            return BattleUnitControlMode.Ai;
        return BattleUnitControlMode.Unknown;
    }

    internal static StringName ToStringName(BattleUnitControlMode mode)
    {
        return mode switch
        {
            BattleUnitControlMode.Manual => ControlModeManual,
            BattleUnitControlMode.Ai => ControlModeAi,
            _ => Empty,
        };
    }

    internal static BattleAreaPattern ToAreaPattern(StringName value)
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
        return value == null || value.ToString().Length == 0
            ? BattleAreaPattern.Unknown
            : BattleAreaPattern.Other;
    }

    internal static StringName ToStringName(BattleAreaPattern pattern)
    {
        return pattern switch
        {
            BattleAreaPattern.Single => AreaPatternSingle,
            BattleAreaPattern.Self => AreaPatternSelf,
            BattleAreaPattern.Diamond => AreaPatternDiamond,
            BattleAreaPattern.Square => AreaPatternSquare,
            BattleAreaPattern.Radius => AreaPatternRadius,
            BattleAreaPattern.Cross => AreaPatternCross,
            BattleAreaPattern.Line => AreaPatternLine,
            BattleAreaPattern.Cone => AreaPatternCone,
            BattleAreaPattern.NarrowCone => AreaPatternNarrowCone,
            BattleAreaPattern.FrontArc => AreaPatternFrontArc,
            _ => Empty,
        };
    }

    internal static int GetAreaPatternDistanceContractBonus(BattleAreaPattern pattern, int areaValue)
    {
        return GetAreaPatternThreatReachBonus(pattern, areaValue);
    }

    internal static int GetAreaPatternThreatReachBonus(BattleAreaPattern pattern, int areaValue)
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

    internal static BattleEffectKind ToEffectKind(StringName value)
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
        if (value == EffectHealFatal)
            return BattleEffectKind.HealFatal;
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
        if (value == EffectEraseStatus)
            return BattleEffectKind.EraseStatus;
        if (value == EffectCleanseHarmful)
            return BattleEffectKind.CleanseHarmful;
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

    internal static StringName ToStringName(BattleEffectKind kind)
    {
        return kind switch
        {
            BattleEffectKind.Damage => EffectDamage,
            BattleEffectKind.ChainDamage => EffectChainDamage,
            BattleEffectKind.Charge => EffectCharge,
            BattleEffectKind.ForcedMove => EffectForcedMove,
            BattleEffectKind.PathStepAoe => EffectPathStepAoe,
            BattleEffectKind.RepeatAttackUntilFail => EffectRepeatAttackUntilFail,
            BattleEffectKind.EquipmentDurabilityDamage => EffectEquipmentDurabilityDamage,
            BattleEffectKind.DispelMagic => EffectDispelMagic,
            BattleEffectKind.Heal => EffectHeal,
            BattleEffectKind.HealFatal => EffectHealFatal,
            BattleEffectKind.StaminaRestore => EffectStaminaRestore,
            BattleEffectKind.Shield => EffectShield,
            BattleEffectKind.LayeredBarrier => EffectLayeredBarrier,
            BattleEffectKind.OnKillGainResources => EffectOnKillGainResources,
            BattleEffectKind.Status => EffectStatus,
            BattleEffectKind.ApplyStatus => EffectApplyStatus,
            BattleEffectKind.EraseStatus => EffectEraseStatus,
            BattleEffectKind.CleanseHarmful => EffectCleanseHarmful,
            BattleEffectKind.BodySizeCategoryOverride => EffectBodySizeCategoryOverride,
            BattleEffectKind.Execute => EffectExecute,
            BattleEffectKind.Terrain => EffectTerrain,
            BattleEffectKind.TerrainReplace => EffectTerrainReplace,
            BattleEffectKind.TerrainReplaceTo => EffectTerrainReplaceTo,
            BattleEffectKind.Height => EffectHeight,
            BattleEffectKind.HeightDelta => EffectHeightDelta,
            BattleEffectKind.TerrainEffect => EffectTerrainEffect,
            BattleEffectKind.EdgeClear => EffectEdgeClear,
            _ => Empty,
        };
    }

    internal static BattleForcedMoveMode ToForcedMoveMode(StringName value)
    {
        if (value == ForcedMoveJump)
            return BattleForcedMoveMode.Jump;
        if (value == ForcedMoveBlink)
            return BattleForcedMoveMode.Blink;
        if (value == ForcedMoveWindPush)
            return BattleForcedMoveMode.WindPush;
        if (value == ForcedMoveEvasive)
            return BattleForcedMoveMode.Evasive;
        if (value == ForcedMoveRetreat)
            return BattleForcedMoveMode.Retreat;
        if (value == ForcedMoveKnockback)
            return BattleForcedMoveMode.Knockback;
        if (value == ForcedMoveReposition)
            return BattleForcedMoveMode.Reposition;
        return BattleForcedMoveMode.Unknown;
    }

    internal static StringName ToStringName(BattleForcedMoveMode mode)
    {
        return mode switch
        {
            BattleForcedMoveMode.Jump => ForcedMoveJump,
            BattleForcedMoveMode.Blink => ForcedMoveBlink,
            BattleForcedMoveMode.WindPush => ForcedMoveWindPush,
            BattleForcedMoveMode.Evasive => ForcedMoveEvasive,
            BattleForcedMoveMode.Retreat => ForcedMoveRetreat,
            BattleForcedMoveMode.Knockback => ForcedMoveKnockback,
            BattleForcedMoveMode.Reposition => ForcedMoveReposition,
            _ => Empty,
        };
    }

    internal static BattleTerrainEffectRuntimeKind ToTerrainEffectRuntimeKind(StringName value)
    {
        if (value == EffectDamage)
            return BattleTerrainEffectRuntimeKind.Damage;
        if (value == TerrainEffectRuntimeMovementCost)
            return BattleTerrainEffectRuntimeKind.MovementCost;
        if (value == EffectStatus)
            return BattleTerrainEffectRuntimeKind.Status;
        if (value == TerrainEffectRuntimeNone)
            return BattleTerrainEffectRuntimeKind.None;
        return BattleTerrainEffectRuntimeKind.Unknown;
    }

    internal static StringName ToStringName(BattleTerrainEffectRuntimeKind kind)
    {
        return kind switch
        {
            BattleTerrainEffectRuntimeKind.Damage => EffectDamage,
            BattleTerrainEffectRuntimeKind.MovementCost => TerrainEffectRuntimeMovementCost,
            BattleTerrainEffectRuntimeKind.Status => EffectStatus,
            BattleTerrainEffectRuntimeKind.None => TerrainEffectRuntimeNone,
            _ => Empty,
        };
    }

    internal static BattlePositionObjectiveKind ToPositionObjectiveKind(StringName value)
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

    internal static StringName ToStringName(BattlePositionObjectiveKind kind)
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

    internal static BattleTargetFilter ToTargetFilter(StringName value)
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

    internal static StringName ToStringName(BattleTargetFilter filter)
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

    internal static BattleTargetMode ToTargetMode(StringName value)
    {
        if (value == TargetModeUnit)
            return BattleTargetMode.Unit;
        if (value == TargetModeGround)
            return BattleTargetMode.Ground;
        return BattleTargetMode.Unknown;
    }

    internal static StringName ToStringName(BattleTargetMode mode)
    {
        return mode switch
        {
            BattleTargetMode.Unit => TargetModeUnit,
            BattleTargetMode.Ground => TargetModeGround,
            _ => Empty,
        };
    }

    internal static BattleTargetSelectionMode ToTargetSelectionMode(StringName value)
    {
        if (value == TargetSelectionSingleUnit)
            return BattleTargetSelectionMode.SingleUnit;
        if (value == TargetSelectionMultiUnit)
            return BattleTargetSelectionMode.MultiUnit;
        if (value == TargetSelectionRandomChain)
            return BattleTargetSelectionMode.RandomChain;
        if (value == TargetSelectionSelf)
            return BattleTargetSelectionMode.Self;
        if (value == TargetSelectionSingleCoord)
            return BattleTargetSelectionMode.SingleCoord;
        if (value == TargetSelectionCoordPair)
            return BattleTargetSelectionMode.CoordPair;
        if (value == TargetSelectionMovement)
            return BattleTargetSelectionMode.Movement;
        return BattleTargetSelectionMode.Unknown;
    }

    internal static StringName ToStringName(BattleTargetSelectionMode mode)
    {
        return mode switch
        {
            BattleTargetSelectionMode.SingleUnit => TargetSelectionSingleUnit,
            BattleTargetSelectionMode.MultiUnit => TargetSelectionMultiUnit,
            BattleTargetSelectionMode.RandomChain => TargetSelectionRandomChain,
            BattleTargetSelectionMode.Self => TargetSelectionSelf,
            BattleTargetSelectionMode.SingleCoord => TargetSelectionSingleCoord,
            BattleTargetSelectionMode.CoordPair => TargetSelectionCoordPair,
            BattleTargetSelectionMode.Movement => TargetSelectionMovement,
            _ => Empty,
        };
    }

    internal static BattleTargetSelectionOrderMode ToTargetSelectionOrderMode(
        StringName value
    )
    {
        if (value == TargetSelectionOrderStable)
            return BattleTargetSelectionOrderMode.Stable;
        if (value == TargetSelectionOrderManual)
            return BattleTargetSelectionOrderMode.Manual;
        return BattleTargetSelectionOrderMode.Unknown;
    }

    internal static StringName ToStringName(BattleTargetSelectionOrderMode mode)
    {
        return mode switch
        {
            BattleTargetSelectionOrderMode.Stable => TargetSelectionOrderStable,
            BattleTargetSelectionOrderMode.Manual => TargetSelectionOrderManual,
            _ => Empty,
        };
    }

    internal static CombatSkillMasteryTriggerMode ToCombatSkillMasteryTriggerMode(
        StringName value
    )
    {
        if (value == MasteryTriggerSkillDamageDiceMax)
            return CombatSkillMasteryTriggerMode.SkillDamageDiceMax;
        if (value == MasteryTriggerWeaponAttackQuality)
            return CombatSkillMasteryTriggerMode.WeaponAttackQuality;
        if (value == MasteryTriggerDamageDealt)
            return CombatSkillMasteryTriggerMode.DamageDealt;
        if (value == MasteryTriggerStatusApplied)
            return CombatSkillMasteryTriggerMode.StatusApplied;
        if (value == MasteryTriggerEffectApplied)
            return CombatSkillMasteryTriggerMode.EffectApplied;
        if (value == MasteryTriggerIncomingPhysicalHit)
            return CombatSkillMasteryTriggerMode.IncomingPhysicalHit;
        if (value == MasteryTriggerSecondaryHit)
            return CombatSkillMasteryTriggerMode.SecondaryHit;
        return CombatSkillMasteryTriggerMode.Unknown;
    }

    internal static StringName ToStringName(CombatSkillMasteryTriggerMode mode)
    {
        return mode switch
        {
            CombatSkillMasteryTriggerMode.SkillDamageDiceMax =>
                MasteryTriggerSkillDamageDiceMax,
            CombatSkillMasteryTriggerMode.WeaponAttackQuality =>
                MasteryTriggerWeaponAttackQuality,
            CombatSkillMasteryTriggerMode.DamageDealt => MasteryTriggerDamageDealt,
            CombatSkillMasteryTriggerMode.StatusApplied => MasteryTriggerStatusApplied,
            CombatSkillMasteryTriggerMode.EffectApplied => MasteryTriggerEffectApplied,
            CombatSkillMasteryTriggerMode.IncomingPhysicalHit =>
                MasteryTriggerIncomingPhysicalHit,
            CombatSkillMasteryTriggerMode.SecondaryHit => MasteryTriggerSecondaryHit,
            _ => Empty,
        };
    }

    internal static CombatSkillMasteryAmountMode ToCombatSkillMasteryAmountMode(
        StringName value
    )
    {
        if (value == MasteryAmountPerTargetRank)
            return CombatSkillMasteryAmountMode.PerTargetRank;
        if (value == MasteryAmountPerCastHpRatio)
            return CombatSkillMasteryAmountMode.PerCastHpRatio;
        return CombatSkillMasteryAmountMode.Unknown;
    }

    internal static StringName ToStringName(CombatSkillMasteryAmountMode mode)
    {
        return mode switch
        {
            CombatSkillMasteryAmountMode.PerTargetRank => MasteryAmountPerTargetRank,
            CombatSkillMasteryAmountMode.PerCastHpRatio => MasteryAmountPerCastHpRatio,
            _ => Empty,
        };
    }

    internal static EnemyTargetRankKind ToEnemyTargetRank(StringName value)
    {
        if (value == EnemyTargetRankNormal)
            return EnemyTargetRankKind.Normal;
        if (value == EnemyTargetRankElite)
            return EnemyTargetRankKind.Elite;
        if (value == EnemyTargetRankBoss)
            return EnemyTargetRankKind.Boss;
        return EnemyTargetRankKind.Unknown;
    }

    internal static StringName ToStringName(EnemyTargetRankKind rank)
    {
        return rank switch
        {
            EnemyTargetRankKind.Normal => EnemyTargetRankNormal,
            EnemyTargetRankKind.Elite => EnemyTargetRankElite,
            EnemyTargetRankKind.Boss => EnemyTargetRankBoss,
            _ => Empty,
        };
    }

    internal static bool IsAiOffensiveEffect(BattleEffectKind kind)
    {
        return kind
            is BattleEffectKind.Damage
                or BattleEffectKind.ChainDamage
                or BattleEffectKind.Charge
                or BattleEffectKind.ForcedMove
                or BattleEffectKind.PathStepAoe
                or BattleEffectKind.Status;
    }

    internal static bool IsGroundPayloadEffect(BattleEffectKind kind)
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

    internal static bool IsUnitPayloadEffect(BattleEffectKind kind)
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
