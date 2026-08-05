using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

internal enum BattleWeaponProfileKind
{
    Unknown = 0,
    None,
    Unarmed,
    Natural,
    Equipped,
}

internal enum BattleWeaponGripKind
{
    Unknown = 0,
    None,
    OneHanded,
    TwoHanded,
}

public partial class BattleUnitState
{
    private static readonly StringName WeaponProfileKindNone = "none";
    private static readonly StringName WeaponProfileKindUnarmed = "unarmed";
    private static readonly StringName WeaponProfileKindNatural = "natural";
    private static readonly StringName WeaponProfileKindEquipped = "equipped";
    private static readonly StringName WeaponGripNone = "none";
    private static readonly StringName WeaponGripOneHanded = "one_handed";
    private static readonly StringName WeaponGripTwoHanded = "two_handed";
    private static readonly string[] EffectiveTraitFields =
    {
        "trait_id",
        "effective_instance_key",
        "source_type",
        "source_id",
        "effect_type",
        "trigger_type",
        "charge_scope",
        "charge_reset_timing",
        "rank",
        "stacks",
        "roll_values",
    };

    internal const int DefaultMovePointsPerTurn = 2;
    internal const int DefaultActionThreshold = 120;
    internal const int BodySizeTiny = 1;
    internal const int BodySizeSmall = 1;
    internal const int BodySizeMedium = 2;
    internal const int BodySizeLarge = 3;
    internal const int BodySizeHuge = 4;
    internal const int BodySizeGargantuan = 5;
    internal const int BodySizeBoss = 6;

    private static readonly string[] ToDictFields =
    {
        "unit_id",
        "source_member_id",
        "enemy_template_id",
        "encounter_actor_id",
        "display_name",
        "battle_sprite_texture_path",
        "faction_id",
        "control_mode",
        "ai_brain_id",
        "ai_state_id",
        "cognition_kind",
        "coord",
        "body_size",
        "body_size_category",
        "footprint_size",
        "occupied_coords",
        "is_alive",
        "attribute_snapshot",
        "equipment_view",
        "current_hp",
        "current_mp",
        "current_stamina",
        "current_aura",
        "aura_max",
        "current_ap",
        "current_move_points",
        "unlocked_combat_resource_ids",
        "stamina_recovery_progress",
        "is_resting",
        "has_taken_action_this_turn",
        "can_use_locked_move_points_this_turn",
        "current_shield_hp",
        "shield_max_hp",
        "shield_duration",
        "shield_family",
        "shield_source_unit_id",
        "shield_source_skill_id",
        "action_progress",
        "action_threshold",
        "known_active_skill_ids",
        "known_skill_level_map",
        "known_skill_lock_hit_bonus_map",
        "movement_tags",
        "vision_tags",
        "proficiency_tags",
        "save_advantage_tags",
        "save_disadvantage_tags",
        "save_immunity_tags",
        "damage_resistances",
        "save_bonus_by_ability",
        "effective_trait_instances",
        "effective_trait_ids",
        "equipment_ability_sources",
        "creature_type_tags",
        "versatility_pick",
        "weapon_profile_kind",
        "weapon_item_id",
        "weapon_profile_type_id",
        "weapon_range_type",
        "weapon_family",
        "weapon_current_grip",
        "weapon_attack_range",
        "weapon_one_handed_dice",
        "weapon_two_handed_dice",
        "weapon_is_versatile",
        "weapon_uses_two_hands",
        "weapon_physical_damage_tag",
        "cooldowns",
        "last_turn_tu",
        "status_effects",
    };

    internal static IReadOnlyList<StringName> DefaultUnlockedCombatResourceIdsTyped =>
        CombatResourceIds.DefaultUnlocked;

    internal static StringNameList CreateDefaultUnlockedCombatResourceProjection() =>
        new(CombatResourceIds.DefaultUnlocked);

    internal static bool IsValidCombatResourceId(StringName resourceId) =>
        CombatResourceIds.ToResourceKind(resourceId) != CombatResourceIdKind.Unknown;

    internal static StringName ToStringName(BattleWeaponProfileKind kind)
    {
        return kind switch
        {
            BattleWeaponProfileKind.None => WeaponProfileKindNone,
            BattleWeaponProfileKind.Unarmed => WeaponProfileKindUnarmed,
            BattleWeaponProfileKind.Natural => WeaponProfileKindNatural,
            BattleWeaponProfileKind.Equipped => WeaponProfileKindEquipped,
            _ => new StringName(""),
        };
    }

    internal static BattleWeaponProfileKind ToWeaponProfileKind(StringName value)
    {
        if (value == WeaponProfileKindNone)
            return BattleWeaponProfileKind.None;
        if (value == WeaponProfileKindUnarmed)
            return BattleWeaponProfileKind.Unarmed;
        if (value == WeaponProfileKindNatural)
            return BattleWeaponProfileKind.Natural;
        if (value == WeaponProfileKindEquipped)
            return BattleWeaponProfileKind.Equipped;
        return BattleWeaponProfileKind.Unknown;
    }

    internal static StringName ToStringName(BattleWeaponGripKind kind)
    {
        return kind switch
        {
            BattleWeaponGripKind.None => WeaponGripNone,
            BattleWeaponGripKind.OneHanded => WeaponGripOneHanded,
            BattleWeaponGripKind.TwoHanded => WeaponGripTwoHanded,
            _ => new StringName(""),
        };
    }

    internal static BattleWeaponGripKind ToWeaponGripKind(StringName value)
    {
        if (value == WeaponGripNone)
            return BattleWeaponGripKind.None;
        if (value == WeaponGripOneHanded)
            return BattleWeaponGripKind.OneHanded;
        if (value == WeaponGripTwoHanded)
            return BattleWeaponGripKind.TwoHanded;
        return BattleWeaponGripKind.Unknown;
    }

    public StringName unit_id = "";
    public StringName source_member_id = "";
    public StringName enemy_template_id = "";
    public StringName encounter_actor_id = "";
    public string display_name = "";
    public string battle_sprite_texture_path = "";
    public StringName faction_id = "";
    public StringName control_mode = "manual";
    public StringName ai_brain_id = "";
    public StringName ai_state_id = "";
    private BattleCognitionKind _baseCognitionKind =
        BattleCognitionKind.Sapient;
    internal BattleAiBlackboard ai_blackboard = new();
    private BattleUnitGeometryState _geometryState = new();
    public AttributeSnapshot attribute_snapshot = NewAttributeSnapshot();
    public EquipmentState equipment_view = NewEquipmentState();
    public bool equipment_view_initialized;
    private BattleUnitCombatResourceState _combatResourceState = new();
    private BattleUnitRestState _restState = new();
    private BattleUnitCombatResourceUnlockState _combatResourceUnlockState = new();
    private BattleUnitTurnState _turnState = new();
    private BattleUnitShieldState _shieldState = new();
    private BattleConsumedContingencySetupCollection _consumedContingencySetups = new();
    private BattleUnitChargeState _chargeState = new();
    private BattleUnitActionClockState _actionClockState = new();
    private BattleUnitCastingClockState _castingClockState = new();
    private BattleUnitKnownSkillState _knownSkillState = new();
    private BattleUnitMovementTagState _movementTagState = new();
    private BattleUnitVisionProficiencyState _visionProficiencyState = new();
    private BattleUnitSaveModifierState _saveModifierState = new();
    private BattleUnitDamageResistanceState _damageResistanceState = new();
    private BattleUnitEffectiveTraitState _effectiveTraitState = new();
    private BattleUnitEquipmentAbilityProjectionState
        _equipmentAbilityProjectionState = new();
    private BattleUnitCreatureTypeState _creatureTypeState = new();
    internal BattleUnitControlMode ControlModeKind
    {
        get => BattleTypedNames.ToControlMode(control_mode);
        set => control_mode = BattleTypedNames.ToStringName(value);
    }
    public StringName versatility_pick = "";
    private BattleUnitWeaponProjectionState _weaponProjectionState = new();
    private BattleUnitCooldownState _cooldownState = new();
    private BattleStatusEffectCollection _statusEffects = new();
    private BattleStatusEffectCollection StatusEffectCollection
    {
        get => _statusEffects;
        set => _statusEffects = value ?? new();
    }
    private BattleConsumedContingencySetupCollection ConsumedContingencySetups
    {
        get => _consumedContingencySetups;
        set => _consumedContingencySetups = value ?? new();
    }
    private BattleUnitTurnState TurnState
    {
        get => _turnState ??= new();
        set => _turnState = value ?? new();
    }
    private BattleUnitShieldState ShieldState
    {
        get => _shieldState ??= new();
        set => _shieldState = value ?? new();
    }
    private BattleUnitChargeState ChargeState
    {
        get => _chargeState;
        set => _chargeState = value ?? new();
    }
    private BattleUnitCooldownState CooldownState
    {
        get => _cooldownState ??= new();
        set => _cooldownState = value ?? new();
    }
    private BattleUnitActionClockState ActionClockState
    {
        get => _actionClockState ??= new();
        set => _actionClockState = value ?? new();
    }
    private BattleUnitCastingClockState CastingClockState
    {
        get => _castingClockState ??= new();
        set => _castingClockState = value ?? new();
    }
    private BattleUnitKnownSkillState WritableKnownSkillState =>
        _knownSkillState ??= new();
    private BattleUnitCombatResourceUnlockState CombatResourceUnlockState
    {
        get => _combatResourceUnlockState ??= new();
        set => _combatResourceUnlockState = value ?? new();
    }
    private BattleUnitCombatResourceState CombatResourceState
    {
        get => _combatResourceState ??= new();
        set => _combatResourceState = value ?? new();
    }
    private BattleUnitRestState RestState
    {
        get => _restState ??= new();
        set => _restState = value ?? new();
    }
    private BattleUnitWeaponProjectionState WeaponProjectionState
    {
        get => _weaponProjectionState ??= new();
        set => _weaponProjectionState = value ?? new();
    }
    private BattleUnitGeometryState GeometryState
    {
        get => _geometryState ??= new();
        set => _geometryState = value ?? new();
    }
    private BattleUnitMovementTagState MovementTagState
    {
        get => _movementTagState ??= new();
        set => _movementTagState = value ?? new();
    }
    private BattleUnitVisionProficiencyState VisionProficiencyState
    {
        get => _visionProficiencyState ??= new();
        set => _visionProficiencyState = value ?? new();
    }
    private BattleUnitSaveModifierState SaveModifierState
    {
        get => _saveModifierState ??= new();
        set => _saveModifierState = value ?? new();
    }
    private BattleUnitDamageResistanceState DamageResistanceState
    {
        get => _damageResistanceState ??= new();
        set => _damageResistanceState = value ?? new();
    }
    private BattleUnitEffectiveTraitState EffectiveTraitState
    {
        get => _effectiveTraitState ??= new();
        set => _effectiveTraitState = value ?? new();
    }
    private BattleUnitEquipmentAbilityProjectionState
        EquipmentAbilityProjectionState
    {
        get => _equipmentAbilityProjectionState ??= new();
        set => _equipmentAbilityProjectionState = value ?? new();
    }
    private BattleUnitCreatureTypeState CreatureTypeState
    {
        get => _creatureTypeState ??= new();
        set => _creatureTypeState = value ?? new();
    }
    public bool death_ward_consumed_this_battle;
    internal BattlePendingCastState pending_cast;

    internal bool HasPendingCast() => pending_cast != null;

    internal BattleCognitionKind GetBaseCognitionKindTyped() =>
        _baseCognitionKind;

    internal void SetBaseCognitionKindTyped(
        BattleCognitionKind cognitionKind
    )
    {
        if (!BattleCognitionContentRules.IsKnown(cognitionKind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(cognitionKind),
                cognitionKind,
                "Battle unit cognition kind must be known."
            );
        }
        _baseCognitionKind = cognitionKind;
    }

    internal BattleCognitionKind GetEffectiveCognitionKindTyped() =>
        BattleCognitionRules.ResolveEffective(this);

    internal bool IsCasting() => IsAlive() && pending_cast != null;

    internal void SetPendingCast(BattlePendingCastState pendingCast)
    {
        pending_cast = pendingCast?.Clone();
    }

    internal BattlePendingCastState ClearPendingCast()
    {
        BattlePendingCastState pendingCast = pending_cast;
        pending_cast = null;
        return pendingCast;
    }

    internal BattleUnitRestSnapshot GetRestStateTyped() =>
        RestState.CaptureRaw();

    internal BattleUnitRestSnapshot CaptureRestForMutationSnapshotExact() =>
        _restState?.CaptureRaw() ?? BattleUnitRestSnapshot.MissingOwner;

    internal void RestoreRestForMutationSnapshotExact(
        BattleUnitRestSnapshot snapshot
    )
    {
        if (!snapshot.OwnerPresent)
        {
            _restState = null;
            return;
        }
        RestState.RestoreRaw(snapshot);
    }

    internal bool IsRestingTyped() => RestState.IsResting();

    internal void MarkRestingTyped() => RestState.MarkResting();

    internal void ClearRestingTyped() => RestState.ClearResting();

    internal BattleUnitCreatureTypeReadView
        GetCreatureTypeTagsReadViewTyped() =>
            _creatureTypeState?.GetReadView()
            ?? BattleUnitCreatureTypeReadView.MissingOwner;

    internal BattleUnitCreatureTypeSnapshot
        CaptureCreatureTypesForMutationSnapshotExact() =>
            _creatureTypeState?.CaptureRaw()
            ?? BattleUnitCreatureTypeSnapshot.MissingOwner;

    internal void RestoreCreatureTypesForMutationSnapshotExact(
        BattleUnitCreatureTypeSnapshot snapshot
    )
    {
        if (!snapshot.OwnerPresent)
        {
            _creatureTypeState = null;
            return;
        }
        CreatureTypeState.RestoreRaw(snapshot);
    }

    internal bool HasCreatureTypeTag(StringName tag) =>
        _creatureTypeState?.Contains(tag) == true;

    internal void ReplaceCreatureTypeTagsTyped(
        IEnumerable<StringName> tags
    ) =>
        CreatureTypeState.ReplaceNormalized(tags);

    internal bool AddCreatureTypeTagTyped(StringName tag) =>
        CreatureTypeState.AddNormalized(tag);

    internal BattleUnitTurnSnapshot GetTurnStateTyped() =>
        TurnState.CaptureRaw();

    internal BattleUnitTurnSnapshot CaptureTurnForMutationSnapshotExact() =>
        _turnState?.CaptureRaw() ?? BattleUnitTurnSnapshot.MissingOwner;

    internal void RestoreTurnForMutationSnapshotExact(
        BattleUnitTurnSnapshot snapshot
    )
    {
        if (!snapshot.OwnerPresent)
        {
            _turnState = null;
            return;
        }
        TurnState.RestoreRaw(snapshot);
    }

    internal bool HasTakenActionThisTurnTyped() =>
        TurnState.HasTakenActionThisTurn();

    internal bool HasMovedThisTurnTyped() =>
        TurnState.HasMovedThisTurn();

    internal bool CanUseLockedMovePointsThisTurnTyped() =>
        TurnState.CanUseLockedMovePointsThisTurn();

    internal bool IsTurnCastingExhaustedTyped() =>
        TurnState.IsCastingExhausted();

    internal bool IsNormalMovementLockedThisTurnTyped() =>
        TurnState.IsNormalMovementLocked();

    internal void MarkActionTakenThisTurnTyped() =>
        TurnState.MarkActionTaken();

    internal void CommitActionTakenThisTurnTyped()
    {
        TurnState.MarkActionTaken();
        RestState.ClearResting();
    }

    internal void MarkMovedThisTurnTyped() =>
        TurnState.MarkMoved();

    internal void GrantLockedMovePointsThisTurnTyped() =>
        TurnState.GrantLockedMovePoints();

    internal void MarkTurnCastingExhaustedTyped() =>
        TurnState.MarkCastingExhausted();

    internal void ResetTurnStateForTurnStartTyped() =>
        TurnState.ResetForTurnStart();

    internal void ClearCastingTurnFlags() =>
        TurnState.ClearCastingExhaustion();

    internal BattleUnitActionClockSnapshot GetActionClockStateTyped() =>
        ActionClockState.CaptureRaw();

    internal BattleUnitActionClockSnapshot CaptureActionClockForMutationSnapshotExact() =>
        _actionClockState?.CaptureRaw()
        ?? BattleUnitActionClockSnapshot.MissingOwner;

    internal void RestoreActionClockForMutationSnapshotExact(
        BattleUnitActionClockSnapshot snapshot
    )
    {
        if (!snapshot.OwnerPresent)
        {
            _actionClockState = null;
            return;
        }
        ActionClockState.RestoreRaw(snapshot);
    }

    internal int GetActionProgressTyped() =>
        ActionClockState.GetProgress();

    internal void SetActionProgressTyped(int value) =>
        ActionClockState.SetProgressRaw(value);

    internal int GetActionThresholdTyped() =>
        ActionClockState.GetThreshold();

    internal void SetActionThresholdTyped(int value) =>
        ActionClockState.SetThresholdRaw(value);

    internal int GetActionProgressRateRemainderTyped() =>
        ActionClockState.GetProgressRateRemainder();

    internal int ConsumeActionProgressRateGainTyped(
        int baseProgressDelta,
        int ratePercent
    ) => ActionClockState.ConsumeRateScaledGain(baseProgressDelta, ratePercent);

    internal bool AdvanceActionClockTyped(
        int progressGain,
        int positiveThreshold
    ) =>
        ActionClockState.AdvanceAndConsumeThresholds(
            progressGain,
            positiveThreshold
        );

    internal BattleUnitCastingClockSnapshot GetCastingClockStateTyped() =>
        CastingClockState.CaptureRaw();

    internal BattleUnitCastingClockSnapshot
        CaptureCastingClockForMutationSnapshotExact() =>
            _castingClockState?.CaptureRaw()
            ?? BattleUnitCastingClockSnapshot.MissingOwner;

    internal void RestoreCastingClockForMutationSnapshotExact(
        BattleUnitCastingClockSnapshot snapshot
    )
    {
        if (!snapshot.OwnerPresent)
        {
            _castingClockState = null;
            return;
        }
        CastingClockState.RestoreRaw(snapshot);
    }

    internal int GetCastProgressRateRemainderTyped() =>
        CastingClockState.GetProgressRateRemainder();

    internal int ConsumeCastProgressRateGainTyped(
        int baseProgressDelta,
        int ratePercent
    ) => CastingClockState.ConsumeRateScaledGain(baseProgressDelta, ratePercent);

    internal BattleUnitGeometryReadView GetGeometryReadViewTyped() =>
        _geometryState?.GetReadView()
        ?? BattleUnitGeometryReadView.MissingOwner;

    internal BattleUnitGeometrySnapshot
        CaptureGeometryForMutationSnapshotExact() =>
            _geometryState?.CaptureRaw()
            ?? BattleUnitGeometrySnapshot.MissingOwner;

    internal void RestoreGeometryForMutationSnapshotExact(
        BattleUnitGeometrySnapshot snapshot
    )
    {
        if (!snapshot.OwnerPresent)
        {
            _geometryState = null;
            return;
        }
        GeometryState.RestoreRaw(snapshot);
    }

    public Vector2I GetAnchorCoord() =>
        GetGeometryReadViewTyped().AnchorCoord;

    public int GetBodySize() =>
        GetGeometryReadViewTyped().BodySize;

    public StringName GetBodySizeCategory() =>
        GetGeometryReadViewTyped().BodySizeCategory;

    public Vector2I GetFootprintSize() =>
        GetGeometryReadViewTyped().FootprintSize;

    internal BattleOccupiedCoordReadView
        GetOccupiedCoordsReadViewTyped() =>
            GetGeometryReadViewTyped().OccupiedCoords;

    public void SetAnchorCoord(Vector2I anchor_coord) =>
        GeometryState.SetAnchorCoord(anchor_coord);

    public bool OccupiesCoord(Vector2I target_coord) =>
        _geometryState?.OccupiesCoord(target_coord) == true;

    internal BattleUnitMovementTagReadView
        GetMovementTagsReadViewTyped() =>
            _movementTagState?.GetReadView()
            ?? BattleUnitMovementTagReadView.MissingOwner;

    internal BattleUnitMovementTagSnapshot
        CaptureMovementTagsForMutationSnapshotExact() =>
            _movementTagState?.CaptureRaw()
            ?? BattleUnitMovementTagSnapshot.MissingOwner;

    internal void RestoreMovementTagsForMutationSnapshotExact(
        BattleUnitMovementTagSnapshot snapshot
    )
    {
        if (!snapshot.OwnerPresent)
        {
            _movementTagState = null;
            return;
        }
        MovementTagState.RestoreRaw(snapshot);
    }

    public bool HasMovementTag(StringName tag) =>
        _movementTagState?.Contains(tag) == true;

    internal void ReplaceMovementTagsTyped(
        IEnumerable<StringName> tags
    ) =>
        MovementTagState.ReplaceNormalized(tags);

    internal bool AddMovementTagTyped(StringName tag) =>
        MovementTagState.AddNormalized(tag);

    internal BattleUnitVisionProficiencyReadView
        GetVisionProficiencyReadViewTyped() =>
            _visionProficiencyState?.GetReadView()
            ?? BattleUnitVisionProficiencyReadView.MissingOwner;

    internal BattleUnitVisionProficiencySnapshot
        CaptureVisionProficiencyForMutationSnapshotExact() =>
            _visionProficiencyState?.CaptureRaw()
            ?? BattleUnitVisionProficiencySnapshot.MissingOwner;

    internal void RestoreVisionProficiencyForMutationSnapshotExact(
        BattleUnitVisionProficiencySnapshot snapshot
    )
    {
        if (!snapshot.OwnerPresent)
        {
            _visionProficiencyState = null;
            return;
        }
        VisionProficiencyState.RestoreRaw(snapshot);
    }

    internal void ResetVisionProficiencyTagsTyped() =>
        VisionProficiencyState.ResetNormalized();

    internal void ReplaceVisionProficiencyTagsTyped(
        IEnumerable<StringName> visionTags,
        IEnumerable<StringName> proficiencyTags
    ) =>
        VisionProficiencyState.ReplaceNormalized(
            visionTags,
            proficiencyTags
        );

    public bool HasVisionTag(StringName tag) =>
        _visionProficiencyState?.ContainsVision(tag) == true;

    public bool HasProficiencyTag(StringName tag) =>
        _visionProficiencyState?.ContainsProficiency(tag) == true;

    internal bool AddVisionTagTyped(StringName tag) =>
        VisionProficiencyState.AddVisionNormalized(tag);

    internal bool AddProficiencyTagTyped(StringName tag) =>
        VisionProficiencyState.AddProficiencyNormalized(tag);

    internal BattleUnitSaveModifierReadView
        GetSaveModifiersReadViewTyped() =>
            _saveModifierState?.GetReadView()
            ?? BattleUnitSaveModifierReadView.MissingOwner;

    internal BattleUnitSaveModifierSnapshot
        CaptureSaveModifiersForMutationSnapshotExact() =>
            _saveModifierState?.CaptureRaw()
            ?? BattleUnitSaveModifierSnapshot.MissingOwner;

    internal void RestoreSaveModifiersForMutationSnapshotExact(
        BattleUnitSaveModifierSnapshot snapshot
    )
    {
        if (!snapshot.OwnerPresent)
        {
            _saveModifierState = null;
            return;
        }
        SaveModifierState.RestoreRaw(snapshot);
    }

    internal void ResetSaveModifiersTyped() =>
        SaveModifierState.ResetNormalized();

    internal void ReplaceSaveModifiersTyped(
        IEnumerable<StringName> advantageTags,
        IEnumerable<StringName> disadvantageTags,
        IEnumerable<StringName> immunityTags,
        IReadOnlyDictionary<StringName, int> bonusByAbility
    ) =>
        SaveModifierState.ReplaceNormalized(
            advantageTags,
            disadvantageTags,
            immunityTags,
            bonusByAbility
        );

    internal void ReplaceSaveTagsTyped(
        IEnumerable<StringName> advantageTags,
        IEnumerable<StringName> disadvantageTags,
        IEnumerable<StringName> immunityTags
    ) =>
        SaveModifierState.ReplaceTagsNormalized(
            advantageTags,
            disadvantageTags,
            immunityTags
        );

    internal void ReplaceSaveBonusesTyped(
        IReadOnlyDictionary<StringName, int> bonusByAbility
    ) =>
        SaveModifierState.ReplaceBonusesNormalized(bonusByAbility);

    internal void AppendSaveTagsTyped(
        IEnumerable<StringName> advantageTags,
        IEnumerable<StringName> disadvantageTags,
        IEnumerable<StringName> immunityTags
    ) =>
        SaveModifierState.AppendTagsNormalized(
            advantageTags,
            disadvantageTags,
            immunityTags
        );

    public bool HasSaveAdvantageTag(StringName tag) =>
        _saveModifierState?.ContainsAdvantage(tag) == true;

    public bool HasSaveDisadvantageTag(StringName tag) =>
        _saveModifierState?.ContainsDisadvantage(tag) == true;

    public bool HasSaveImmunityTag(StringName tag) =>
        _saveModifierState?.ContainsImmunity(tag) == true;

    internal bool AddSaveAdvantageTagTyped(StringName tag) =>
        SaveModifierState.AddAdvantageNormalized(tag);

    internal bool AddSaveDisadvantageTagTyped(StringName tag) =>
        SaveModifierState.AddDisadvantageNormalized(tag);

    internal BattleUnitEffectiveTraitReadView
        GetEffectiveTraitsReadViewTyped() =>
            _effectiveTraitState?.GetReadView()
            ?? BattleUnitEffectiveTraitReadView.MissingOwner;

    internal BattleEffectiveTraitIdReadView
        GetCanonicalEffectiveTraitIdsReadViewTyped() =>
            _effectiveTraitState?.GetDerivedTraitIdsReadView()
            ?? new BattleEffectiveTraitIdReadView(null);

    internal BattleUnitEffectiveTraitSnapshot
        CaptureEffectiveTraitsForMutationSnapshotExact() =>
            _effectiveTraitState?.CaptureRaw()
            ?? BattleUnitEffectiveTraitSnapshot.MissingOwner;

    internal void RestoreEffectiveTraitsForMutationSnapshotExact(
        BattleUnitEffectiveTraitSnapshot snapshot
    )
    {
        if (!snapshot.OwnerPresent)
        {
            _effectiveTraitState = null;
            return;
        }
        EffectiveTraitState.RestoreRaw(snapshot);
    }

    internal void ReplaceEffectiveTraitsTyped(
        IEnumerable<BattleEffectiveTraitInstanceState> instances
    ) =>
        EffectiveTraitState.ReplaceNormalized(instances);

    internal List<BattleEffectiveTraitInstanceState>
        CopyEffectiveTraitInstancesTyped() =>
            _effectiveTraitState?.CopyInstancesNormalized()
            ?? new List<BattleEffectiveTraitInstanceState>();

    internal int GetEffectiveTraitInstanceCountTyped() =>
        _effectiveTraitState?.GetInstanceCount() ?? 0;

    internal bool HasEffectiveTrait(StringName traitId) =>
        _effectiveTraitState?.ContainsTraitId(traitId) == true;

    internal BattleUnitEquipmentAbilityProjectionReadView
        GetEquipmentAbilityProjectionReadViewTyped() =>
            _equipmentAbilityProjectionState?.GetReadView()
            ?? BattleUnitEquipmentAbilityProjectionReadView
                .MissingOwner;

    internal BattleEquipmentAbilitySourceListReadView
        GetEquipmentAbilitySourcesReadViewTyped() =>
            GetEquipmentAbilityProjectionReadViewTyped().Sources;

    internal BattleTemporalProgressModifierListReadView
        GetTemporalProgressModifiersReadViewTyped() =>
            GetEquipmentAbilityProjectionReadViewTyped()
                .TemporalProgressModifiers;

    internal BattleCognitionCeilingModifierListReadView
        GetCognitionCeilingModifiersReadViewTyped() =>
            GetEquipmentAbilityProjectionReadViewTyped()
                .CognitionCeilingModifiers;

    internal BattleUnitEquipmentAbilityProjectionSnapshot
        CaptureEquipmentAbilityProjectionForMutationSnapshotExact() =>
            _equipmentAbilityProjectionState?.CaptureRaw()
            ?? BattleUnitEquipmentAbilityProjectionSnapshot
                .MissingOwner;

    internal BattleUnitEquipmentAbilityProjectionSeed
        CaptureEquipmentAbilityProjectionSeedTyped() =>
            _equipmentAbilityProjectionState
                ?.CaptureNormalizedSeed()
            ?? BattleUnitEquipmentAbilityProjectionSeed.Empty
                .DeepClone();

    internal void
        RestoreEquipmentAbilityProjectionForMutationSnapshotExact(
            BattleUnitEquipmentAbilityProjectionSnapshot snapshot
        )
    {
        if (!snapshot.OwnerPresent)
        {
            _equipmentAbilityProjectionState = null;
            return;
        }

        EquipmentAbilityProjectionState.RestoreRaw(snapshot);
    }

    internal void ReplaceEquipmentAbilityProjectionTyped(
        IEnumerable<BattleEquipmentAbilitySourceState> sources,
        IEnumerable<BattleTemporalProgressModifierState>
            temporalProgressModifiers,
        IEnumerable<BattleCognitionCeilingModifierState>
            cognitionCeilingModifiers = null
    ) =>
        EquipmentAbilityProjectionState.ReplaceNormalized(
            sources,
            temporalProgressModifiers,
            cognitionCeilingModifiers
        );

    internal void ClearEquipmentAbilityProjectionTyped() =>
        EquipmentAbilityProjectionState.ReplaceNormalized(
            null,
            null
        );

    internal BattleTemporalProgressModifierReadView
        GetSelectedTemporalProgressModifierTyped(
            bool actionProgress
        ) =>
            _equipmentAbilityProjectionState
                ?.GetSelectedTemporalProgressModifier(
                    actionProgress
                );

    internal bool AddSaveImmunityTagTyped(StringName tag) =>
        SaveModifierState.AddImmunityNormalized(tag);

    public int GetSaveBonusByAbilityTyped(
        StringName ability,
        int fallback = 0
    ) =>
        _saveModifierState?.GetAbilityBonus(ability, fallback)
        ?? fallback;

    internal bool AddSaveBonusByAbilityTyped(
        StringName ability,
        int bonus
    ) =>
        SaveModifierState.AddAbilityBonusNormalized(ability, bonus);

    internal BattleUnitCombatResourceReadView
        GetCombatResourcesReadViewTyped() =>
            _combatResourceState?.GetReadView()
            ?? BattleUnitCombatResourceReadView.MissingOwner;

    internal BattleUnitCombatResourceSnapshot
        CaptureCombatResourcesForMutationSnapshotExact() =>
            _combatResourceState?.CaptureRaw()
            ?? BattleUnitCombatResourceSnapshot.MissingOwner;

    internal void RestoreCombatResourcesForMutationSnapshotExact(
        BattleUnitCombatResourceSnapshot snapshot
    )
    {
        if (!snapshot.OwnerPresent)
        {
            _combatResourceState = null;
            return;
        }
        CombatResourceState.RestoreRaw(snapshot);
    }

    public int GetCurrentHp() => CombatResourceState.GetCurrentHp();

    public int GetCurrentMp() => CombatResourceState.GetCurrentMp();

    public int GetCurrentStamina() =>
        CombatResourceState.GetCurrentStamina();

    public int GetCurrentAura() => CombatResourceState.GetCurrentAura();

    public int GetCurrentAp() => CombatResourceState.GetCurrentAp();

    public int GetCurrentMovePoints() =>
        CombatResourceState.GetCurrentMovePoints();

    internal int GetStaminaRecoveryProgressTyped() =>
        CombatResourceState.GetStaminaRecoveryProgress();

    internal bool ApplyStaminaRecoveryTyped(
        int tickCount,
        int staminaMax,
        int progressGainPerTick,
        int progressDenominator
    ) =>
        CombatResourceState.ApplyStaminaRecovery(
            tickCount,
            staminaMax,
            progressGainPerTick,
            progressDenominator
        );

    public int GetMovePointCapacity()
    {
        int delta = 0;
        foreach (BattleStatusEffectState status in GetStatusEffectsTyped())
        {
            if (status == null || status.stacks <= 0)
                continue;
            delta += status.move_point_capacity_delta;
        }
        return Math.Max(DefaultMovePointsPerTurn + delta, 0);
    }

    public void ClampCurrentMovePointsToCapacity()
    {
        SetCurrentMovePoints(
            Math.Min(
                Math.Max(GetCurrentMovePoints(), 0),
                GetMovePointCapacity()
            )
        );
    }

    public bool IsAlive() => CombatResourceState.IsAlive();

    public void SetCurrentHp(int value)
    {
        CombatResourceState.SetCurrentHp(value);
    }

    public void SetCurrentHpClamped(int value, int hpMax)
    {
        CombatResourceState.SetCurrentHpClamped(value, hpMax);
    }

    public int ApplyHpDamage(int damage)
    {
        return CombatResourceState.ApplyHpDamage(damage);
    }

    public int ApplyHealing(int amount, int hpMax)
    {
        return CombatResourceState.ApplyHealing(amount, hpMax);
    }

    public void MarkDead()
    {
        CombatResourceState.MarkDead();
    }

    public void ReviveWithHp(int hp, int hpMax)
    {
        CombatResourceState.ReviveWithHp(hp, hpMax);
    }

    public void SetCurrentMp(int value)
    {
        CombatResourceState.SetCurrentMp(value);
    }

    public void SetCurrentStamina(int value)
    {
        CombatResourceState.SetCurrentStamina(value);
    }

    public void SetCurrentAura(int value)
    {
        CombatResourceState.SetCurrentAura(value);
    }

    public void SetCurrentAp(int value)
    {
        CombatResourceState.SetCurrentAp(value);
    }

    public void SetCurrentMovePoints(int value)
    {
        CombatResourceState.SetCurrentMovePoints(value);
    }

    public void SetCombatResources(
        int hp,
        int mp,
        int stamina,
        int aura,
        int ap,
        int movePoints
    )
    {
        CombatResourceState.SetAllNormalized(
            hp,
            mp,
            stamina,
            aura,
            ap,
            movePoints
        );
    }

    internal void RestoreCombatResourceProjection(
        int hp,
        int mp,
        int stamina,
        int aura,
        int ap,
        int movePoints,
        bool alive
    )
    {
        CombatResourceState.RestoreProjectionNormalized(
            hp,
            mp,
            stamina,
            aura,
            ap,
            movePoints,
            alive
        );
    }

    internal void ClampCombatResources(BattleResourceCaps caps)
    {
        CombatResourceState.ClampToCaps(caps);
    }

    internal void SpendSkillCosts(
        SkillCostTransaction costs,
        bool includeAp = true,
        bool includeCooldown = true
    )
    {
        if (costs == null)
        {
            return;
        }
        CombatResourceState.SpendSkillCosts(costs, includeAp);
        if (includeCooldown && costs.SkillId != "" && costs.CooldownTurns > 0)
            SetCooldownTyped(costs.SkillId, costs.CooldownTurns);
    }

    internal void RefundSkillCosts(SkillCostTransaction costs, BattleResourceCaps caps)
    {
        if (costs == null)
        {
            return;
        }
        CombatResourceState.RefundSkillCosts(costs, caps);
    }

    internal void RefundSkillResources(int mp, int stamina, int aura, BattleResourceCaps caps)
    {
        CombatResourceState.RefundSkillResources(
            mp,
            stamina,
            aura,
            caps
        );
    }

    public IReadOnlyList<Vector2I> GetOccupiedCoordsTyped()
        => _geometryState?.GetOccupiedCoordsDetached()
        ?? System.Array.Empty<Vector2I>();

    public bool SetBodySizeCategory(StringName category)
        => GeometryState.SetBodySizeCategory(category);

    public bool SetBodySizeProjection(int size)
        => GeometryState.SetBodySizeProjection(size);

    internal void RestoreBodyShapeProjection(
        StringName category,
        int size,
        Vector2I footprint,
        IEnumerable<Vector2I> occupiedCoords
    )
        => GeometryState.RestoreBodyShapeProjection(
            category,
            size,
            footprint,
            occupiedCoords
        );

    internal void NormalizeBodySizeProjectionForOwnerWrite()
    {
        // Admission path rebuilds a missing geometry owner from canonical defaults
        // instead of rejecting the unit; strict snapshot paths keep using
        // EnsureBodySizeProjectionInvariant() for missing-owner violations.
        GeometryState.NormalizeForOwnerWrite(unit_id);
    }

    internal void EnsureBodySizeProjectionInvariant()
    {
        if (_geometryState == null)
        {
            throw new InvalidOperationException(
                $"BattleUnitState geometry owner 缺失: "
                + $"unit_id='{unit_id}'。"
            );
        }
        _geometryState.EnsureProjectionInvariant(unit_id);
    }

    public bool HasStatusEffect(StringName status_id)
    {
        return GetStatusEffect(status_id) != null;
    }

    public bool HasShield()
    {
        return ShieldState.HasActiveShield();
    }

    internal BattleUnitShieldSnapshot GetShieldStateTyped() =>
        ShieldState.CaptureRaw();

    internal BattleUnitShieldSnapshot CaptureShieldStateCanonical() =>
        ShieldState.CaptureCanonical();

    internal BattleUnitShieldSnapshot CaptureShieldForMutationSnapshotExact() =>
        _shieldState?.CaptureRaw() ?? BattleUnitShieldSnapshot.MissingOwner;

    internal void ReplaceShieldStateTyped(
        int currentHp,
        int maxHp,
        int duration,
        StringName family,
        StringName sourceUnitId,
        StringName sourceSkillId
    ) =>
        ShieldState.ReplaceAndNormalize(
            new BattleUnitShieldSnapshot(
                currentHp,
                maxHp,
                duration,
                family,
                sourceUnitId,
                sourceSkillId
            )
        );

    internal void RestoreShieldForMutationSnapshotExact(
        BattleUnitShieldSnapshot snapshot
    ) => ShieldState.RestoreRaw(snapshot);

    internal void SetCurrentShieldHpAndNormalizeTyped(int currentHp) =>
        ShieldState.SetCurrentHpAndNormalize(currentHp);

    internal bool AdvanceShieldDurationTyped(int elapsedTu) =>
        ShieldState.AdvanceDuration(elapsedTu);

    internal BattleUnitShieldDrainResult DrainShieldTyped(int requestedDrain) =>
        ShieldState.DrainCurrentHp(requestedDrain);

    public int GetAuraMax()
    {
        return attribute_snapshot?.GetValue("aura_max") ?? 0;
    }

    public void SyncDefaultCombatResourceUnlocks()
    {
        CombatResourceUnlockState.SyncDefaults();
    }

    public bool HasCombatResourceUnlocked(StringName resource_id) =>
        _combatResourceUnlockState?.Contains(resource_id) == true;

    internal BattleUnitCombatResourceUnlockReadView
        GetCombatResourceUnlocksReadViewTyped() =>
            _combatResourceUnlockState?.GetReadView()
            ?? BattleUnitCombatResourceUnlockReadView.MissingOwner;

    internal BattleUnitCombatResourceUnlockSnapshot
        CaptureCombatResourceUnlocksForMutationSnapshotExact() =>
            _combatResourceUnlockState?.CaptureRaw()
            ?? BattleUnitCombatResourceUnlockSnapshot.MissingOwner;

    internal void RestoreCombatResourceUnlocksForMutationSnapshotExact(
        BattleUnitCombatResourceUnlockSnapshot snapshot
    )
    {
        if (!snapshot.OwnerPresent)
        {
            _combatResourceUnlockState = null;
            return;
        }

        CombatResourceUnlockState.RestoreRaw(snapshot);
    }

    internal int GetKnownSkillLevelTyped(StringName skillId, int fallback = 0) =>
        _knownSkillState?.GetSkillLevel(skillId, fallback) ?? fallback;

    internal bool HasKnownSkillLevelTyped(StringName skillId) =>
        _knownSkillState?.HasSkillLevel(skillId) ?? false;

    internal int GetCooldownTyped(StringName skillId, int fallback = 0) =>
        CooldownState.Get(skillId, fallback);

    internal void SetCooldownTyped(StringName skillId, int value) =>
        CooldownState.Set(skillId, value);

    internal void SetCooldownsTyped(IReadOnlyDictionary<StringName, int> values) =>
        CooldownState.ReplaceNormalized(values);

    internal BattleUnitCooldownSnapshot GetCooldownStateTyped() =>
        CooldownState.CaptureRaw();

    internal int GetCooldownAnchorTuTyped() => CooldownState.GetLastTurnTu();

    internal void SetCooldownAnchorTuTyped(int value) =>
        CooldownState.SetLastTurnTu(value);

    internal void EnsureCooldownAnchorTyped(int currentTu) =>
        CooldownState.EnsureAnchor(currentTu);

    internal BattleUnitCooldownAdvanceResult AdvanceCooldownClockToTyped(
        int currentTu,
        int granularity
    ) => CooldownState.AdvanceTo(currentTu, granularity);

    internal void AdvanceCooldownAnchorForStasisTyped(int elapsedTu, int currentTu) =>
        CooldownState.AdvanceFrozenAnchor(elapsedTu, currentTu);

    internal int GetPerBattleChargeTyped(StringName chargeKey, int fallback = 0) =>
        ChargeState.GetPerBattle(chargeKey, fallback);

    internal bool HasPerBattleChargeTyped(StringName chargeKey) =>
        ChargeState.HasPerBattle(chargeKey);

    internal void SetPerBattleChargeTyped(StringName chargeKey, int value) =>
        ChargeState.SetPerBattle(chargeKey, value);

    internal bool RemovePerBattleChargeTyped(StringName chargeKey) =>
        ChargeState.RemovePerBattle(chargeKey);

    internal int GetPerTurnChargeTyped(StringName chargeKey, int fallback = 0) =>
        ChargeState.GetPerTurn(chargeKey, fallback);

    internal bool HasPerTurnChargeTyped(StringName chargeKey) =>
        ChargeState.HasPerTurn(chargeKey);

    internal void SetPerTurnChargeTyped(StringName chargeKey, int value) =>
        ChargeState.SetPerTurn(chargeKey, value);

    internal int GetPerTurnChargeLimitTyped(StringName chargeKey, int fallback = 0) =>
        ChargeState.GetPerTurnLimit(chargeKey, fallback);

    internal bool HasPerTurnChargeLimitTyped(StringName chargeKey) =>
        ChargeState.HasPerTurnLimit(chargeKey);

    internal void SetPerTurnChargeLimitTyped(StringName chargeKey, int value) =>
        ChargeState.SetPerTurnLimit(chargeKey, value);

    internal bool RemovePerTurnChargeAndLimitTyped(StringName chargeKey) =>
        ChargeState.RemovePerTurnAndLimit(chargeKey);

    internal int GetFumbleProtectionUsedTyped(StringName skillId, int fallback = 0) =>
        ChargeState.GetFumbleProtectionUsed(skillId, fallback);

    internal void SetFumbleProtectionUsedTyped(StringName skillId, int value) =>
        ChargeState.SetFumbleProtectionUsed(skillId, value);

    internal Dictionary<StringName, int> GetKnownSkillLevelsTyped() =>
        _knownSkillState?.SnapshotSkillLevels()
        ?? new Dictionary<StringName, int>();

    internal Dictionary<StringName, int> GetKnownSkillLockHitBonusesTyped() =>
        _knownSkillState?.SnapshotLockHitBonuses()
        ?? new Dictionary<StringName, int>();

    internal List<StringName> GetKnownActiveSkillIdsTyped() =>
        _knownSkillState?.SnapshotActiveSkills()
        ?? new List<StringName>();

    internal BattleKnownActiveSkillReadView GetKnownActiveSkillsViewTyped() =>
        _knownSkillState?.GetActiveSkillsView() ?? new(null);

    internal BattleUnitKnownSkillReadView GetKnownSkillsReadViewTyped() =>
        _knownSkillState?.GetReadView()
        ?? BattleUnitKnownSkillReadView.MissingOwner;

    internal bool KnowsActiveSkill(StringName skillId) =>
        _knownSkillState?.KnowsActiveSkill(skillId) ?? false;

    internal bool TryGetFirstKnownActiveSkillIdTyped(out StringName skillId)
    {
        if (_knownSkillState != null)
            return _knownSkillState.TryGetFirstActiveSkill(out skillId);

        skillId = new StringName("");
        return false;
    }

    internal void SetKnownActiveSkillIds(IEnumerable<StringName> skillIds) =>
        WritableKnownSkillState.ReplaceActiveSkillsNormalized(skillIds);

    internal void AddKnownActiveSkill(StringName skillId) =>
        WritableKnownSkillState.AddActiveSkillNormalized(skillId);

    internal void CopyKnownSkillLevelEntriesTo(
        List<KeyValuePair<StringName, int>> destination
    )
    {
        if (_knownSkillState != null)
            _knownSkillState.CopySkillLevelEntriesTo(destination);
    }

    internal BattleUnitKnownSkillSnapshot CaptureKnownSkillsForMutationSnapshotExact() =>
        _knownSkillState?.CaptureRaw()
        ?? BattleUnitKnownSkillSnapshot.MissingOwner;

    internal void RestoreKnownSkillsForMutationSnapshotExact(
        BattleUnitKnownSkillSnapshot snapshot
    )
    {
        if (!snapshot.OwnerPresent)
        {
            _knownSkillState = null;
            return;
        }
        WritableKnownSkillState.RestoreRaw(snapshot);
    }

    internal void SetKnownSkillLevelsTyped(
        IReadOnlyDictionary<StringName, int> values,
        bool preserveZero = false
    ) =>
        WritableKnownSkillState.ReplaceSkillLevelsNormalized(
            values,
            preserveZero
        );

    internal void SetKnownSkillLevelTyped(
        StringName skillId,
        int level,
        bool preserveZero = false
    ) =>
        WritableKnownSkillState.SetSkillLevelNormalized(
            skillId,
            level,
            preserveZero
        );

    internal void RemoveKnownSkillLevelTyped(StringName skillId) =>
        _knownSkillState?.RemoveSkillLevel(skillId);

    internal void SetKnownSkillLockHitBonusesTyped(
        IReadOnlyDictionary<StringName, int> values
    ) =>
        WritableKnownSkillState.ReplaceLockHitBonusesNormalized(values);

    internal void SetKnownSkillLockHitBonusTyped(
        StringName skillId,
        int bonus
    ) =>
        WritableKnownSkillState.SetLockHitBonusNormalized(skillId, bonus);

    internal void SetVersatilityPick(StringName value)
    {
        versatility_pick = value;
    }

    internal int GetKnownSkillLockHitBonusTyped(StringName skillId, int fallback = 0) =>
        _knownSkillState?.GetLockHitBonus(skillId, fallback)
        ?? fallback;

    internal Dictionary<StringName, int> GetCooldownsTyped() =>
        CooldownState.Snapshot();

    internal Dictionary<StringName, int> GetPerBattleChargesTyped() =>
        ChargeState.SnapshotPerBattle();

    internal Dictionary<StringName, int> GetPerTurnChargesTyped() =>
        ChargeState.SnapshotPerTurn();

    internal Dictionary<StringName, int> GetPerTurnChargeLimitsTyped() =>
        ChargeState.SnapshotPerTurnLimits();

    internal Dictionary<StringName, int> GetFumbleProtectionUsedTyped() =>
        ChargeState.SnapshotFumbleProtectionUsed();

    internal BattleUnitDamageResistanceReadView
        GetDamageResistancesReadViewTyped() =>
            _damageResistanceState?.GetReadView()
            ?? BattleUnitDamageResistanceReadView.MissingOwner;

    internal BattleUnitDamageResistanceSnapshot
        CaptureDamageResistancesForMutationSnapshotExact() =>
            _damageResistanceState?.CaptureRaw()
            ?? BattleUnitDamageResistanceSnapshot.MissingOwner;

    internal void RestoreDamageResistancesForMutationSnapshotExact(
        BattleUnitDamageResistanceSnapshot snapshot
    )
    {
        if (!snapshot.OwnerPresent)
        {
            _damageResistanceState = null;
            return;
        }

        DamageResistanceState.RestoreRaw(snapshot);
    }

    internal void ResetDamageResistancesTyped() =>
        DamageResistanceState.ResetNormalized();

    internal void ReplaceDamageResistancesTyped(
        IReadOnlyDictionary<StringName, StringName> resistances
    ) =>
        DamageResistanceState.ReplaceNormalized(resistances);

    internal void MergeDamageResistancesTyped(
        IReadOnlyDictionary<StringName, StringName> resistances
    ) =>
        DamageResistanceState.MergeOverrideNormalized(resistances);

    internal bool SetDamageResistanceTyped(
        StringName damageTag,
        StringName mitigationTier
    ) =>
        DamageResistanceState.SetNormalized(
            damageTag,
            mitigationTier
        );

    internal bool HasDamageResistanceTyped(StringName damageTag) =>
        _damageResistanceState?.Contains(damageTag) == true;

    internal StringName GetDamageResistanceTyped(
        StringName damageTag,
        StringName fallback = default
    ) =>
        _damageResistanceState?.Get(damageTag, fallback)
        ?? fallback;

    internal bool TryGetDamageResistanceTyped(
        StringName damageTag,
        out StringName mitigationTier
    )
    {
        mitigationTier = default;
        return _damageResistanceState?.TryGetValue(
            damageTag,
            out mitigationTier
        ) == true;
    }

    internal Dictionary<StringName, StringName>
        GetDamageResistancesTyped() =>
            _damageResistanceState?.CopyNormalized()
            ?? new Dictionary<StringName, StringName>();

    internal BattleUnitWeaponProjectionReadView
        GetWeaponProjectionReadViewTyped() =>
            _weaponProjectionState?.GetReadView()
            ?? BattleUnitWeaponProjectionReadView.MissingOwner;

    internal BattleUnitWeaponProjectionSnapshot
        CaptureWeaponProjectionForMutationSnapshotExact() =>
            _weaponProjectionState?.CaptureRaw()
            ?? BattleUnitWeaponProjectionSnapshot.MissingOwner;

    internal void RestoreWeaponProjectionForMutationSnapshotExact(
        BattleUnitWeaponProjectionSnapshot snapshot
    )
    {
        if (!snapshot.OwnerPresent)
        {
            _weaponProjectionState = null;
            return;
        }

        WeaponProjectionState.RestoreRaw(snapshot);
    }

    internal WeaponDice GetWeaponOneHandedDiceTyped() =>
        WeaponProjectionState.CopyOneHandedDice();

    internal WeaponDice GetWeaponTwoHandedDiceTyped() =>
        WeaponProjectionState.CopyTwoHandedDice();

    internal WeaponDice GetActiveWeaponDiceTyped() =>
        WeaponProjectionState.CopyActiveDice();

    public bool UnlockCombatResource(StringName resource_id) =>
        CombatResourceUnlockState.Unlock(resource_id);

    public void SetUnlockedCombatResourceIds(IEnumerable<StringName> resource_ids)
    {
        CombatResourceUnlockState.ReplaceNormalized(resource_ids);
    }

    public void ClearShield()
    {
        ShieldState.Clear();
    }

    internal void MarkContingencySetupConsumed(StringName setupId)
    {
        ConsumedContingencySetups.MarkConsumed(setupId);
    }

    internal bool HasConsumedContingencySetup(StringName setupId)
    {
        return ConsumedContingencySetups.Contains(setupId);
    }

    internal IReadOnlyList<StringName> GetConsumedContingencySetupIdsTyped() =>
        ConsumedContingencySetups.GetIds();

    internal void ReplaceConsumedContingencySetupIdsTyped(IEnumerable<StringName> setupIds)
    {
        ConsumedContingencySetups.Replace(setupIds);
    }

    public void NormalizeShieldState()
    {
        ShieldState.Normalize();
    }

    public EquipmentState GetEquipmentView()
    {
        if (equipment_view == null)
        {
            equipment_view = NewEquipmentState();
        }
        return equipment_view;
    }

    public void SetEquipmentView(EquipmentState source_equipment_state)
    {
        equipment_view_initialized = true;
        equipment_view = source_equipment_state?.DuplicateState() ?? NewEquipmentState();
    }

    public void ClearWeaponProjection()
    {
        WeaponProjectionState.Clear();
    }

    public void SetUnarmedWeaponProjection(
        StringName damage_tag = default,
        GDictionary dice = null,
        int attack_range = 1
    )
    {
        SetUnarmedWeaponProjectionTyped(damage_tag, WeaponDice.FromDictionary(dice), attack_range);
    }

    internal void SetUnarmedWeaponProjectionTyped(
        StringName damageTag = default,
        WeaponDice dice = null,
        int attackRange = 1
    )
    {
        if (IsEmpty(damageTag))
        {
            damageTag = "physical_blunt";
        }
        dice = dice == null || dice.IsEmpty()
            ? new WeaponDice
            {
                dice_count = 1,
                dice_sides = 4,
                flat_bonus = 0,
            }
            : dice;
        ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = WeaponProfileKindUnarmed,
                weapon_profile_type_id = "unarmed",
                weapon_range_type = "melee",
                weapon_family = "unarmed",
                weapon_current_grip = WeaponGripOneHanded,
                weapon_attack_range = attackRange,
                weapon_one_handed_dice = dice,
                weapon_uses_two_hands = false,
                weapon_physical_damage_tag = damageTag,
            }
        );
    }

    public void SetNaturalWeaponProjection(
        StringName profile_type_id,
        StringName damage_tag,
        int attack_range,
        GDictionary dice = null,
        StringName family = default
    )
    {
        SetNaturalWeaponProjectionTyped(
            profile_type_id,
            damage_tag,
            attack_range,
            WeaponDice.FromDictionary(dice),
            family
        );
    }

    internal void SetNaturalWeaponProjectionTyped(
        StringName profileTypeId,
        StringName damageTag,
        int attackRange,
        WeaponDice dice = null,
        StringName family = default
    )
    {
        ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = WeaponProfileKindNatural,
                weapon_profile_type_id = !IsEmpty(profileTypeId) ? profileTypeId : "natural_weapon",
                weapon_range_type = "melee",
                weapon_family = family,
                weapon_current_grip = attackRange > 0 ? WeaponGripOneHanded : WeaponGripNone,
                weapon_attack_range = attackRange,
                weapon_one_handed_dice = dice ?? new WeaponDice(),
                weapon_uses_two_hands = false,
                weapon_physical_damage_tag = damageTag,
            }
        );
    }

    internal void ApplyWeaponProjectionTyped(WeaponProjection projection)
    {
        WeaponProjectionState.ApplyNormalized(projection);
    }

    public int GetWeaponAttackRange() =>
        WeaponProjectionState.GetAttackRangeClamped();

    public BattleStatusEffectState GetStatusEffect(StringName status_id)
    {
        return _statusEffects.Get(status_id);
    }

    public List<BattleStatusEffectState> GetStatusEffectsTyped()
    {
        return _statusEffects.GetStatusEffects();
    }

    public List<StringName> GetSortedStatusEffectIdsTyped()
    {
        return _statusEffects.GetSortedStatusEffectIds();
    }

    public void SetStatusEffect(BattleStatusEffectState effect_state)
    {
        if (effect_state == null || effect_state.IsEmpty())
        {
            return;
        }
        _statusEffects.Set(effect_state);
    }

    public void EraseStatusEffect(StringName status_id)
    {
        StringName normalized = ToStringName(status_id);
        if (!IsEmpty(normalized))
        {
            _statusEffects.Remove(normalized);
        }
    }

    public void ClearStatusEffects()
    {
        _statusEffects.Clear();
    }

    internal void ReplaceStatusEffectsForMutationSnapshotExact(
        IEnumerable<KeyValuePair<StringName, BattleStatusEffectState>> effects
    )
    {
        _statusEffects = new BattleStatusEffectCollection();
        if (effects == null)
        {
            return;
        }
        foreach (KeyValuePair<StringName, BattleStatusEffectState> entry in effects)
        {
            _statusEffects.SetForMutationSnapshotExact(entry.Key, entry.Value);
        }
    }

    internal IReadOnlyDictionary<StringName, BattleStatusEffectState> CaptureStatusEffectsTyped()
    {
        var results = new Dictionary<StringName, BattleStatusEffectState>();
        foreach (StringName statusId in GetSortedStatusEffectIdsTyped())
        {
            BattleStatusEffectState effectState = GetStatusEffect(statusId);
            if (effectState != null)
                results[statusId] = effectState.DuplicateState();
        }
        return results;
    }

    internal IReadOnlyList<KeyValuePair<StringName, BattleStatusEffectState>>
        CaptureStatusEffectsForMutationSnapshotExact() =>
        _statusEffects.SnapshotEntriesForMutationSnapshotExact();

    internal BattleUnitCooldownSnapshot CaptureCooldownForMutationSnapshotExact() =>
        _cooldownState?.CaptureRaw() ?? BattleUnitCooldownSnapshot.MissingOwner;

    internal void RestoreCooldownForMutationSnapshotExact(
        BattleUnitCooldownSnapshot snapshot
    ) => CooldownState.RestoreRaw(snapshot);

    internal BattleStringNameIntMap CapturePerBattleChargesForMutationSnapshotExact() =>
        ChargeState.CapturePerBattleForMutationSnapshotExact();

    internal BattleStringNameIntMap CapturePerTurnChargesForMutationSnapshotExact() =>
        ChargeState.CapturePerTurnForMutationSnapshotExact();

    internal BattleStringNameIntMap CapturePerTurnChargeLimitsForMutationSnapshotExact() =>
        ChargeState.CapturePerTurnLimitsForMutationSnapshotExact();

    internal BattleStringNameIntMap CaptureFumbleProtectionForMutationSnapshotExact() =>
        ChargeState.CaptureFumbleProtectionForMutationSnapshotExact();

    internal void RestorePerBattleChargesForMutationSnapshotExact(
        BattleStringNameIntMap values
    ) => ChargeState.RestorePerBattleForMutationSnapshotExact(values);

    internal void RestorePerTurnChargesForMutationSnapshotExact(
        BattleStringNameIntMap values
    ) => ChargeState.RestorePerTurnForMutationSnapshotExact(values);

    internal void RestorePerTurnChargeLimitsForMutationSnapshotExact(
        BattleStringNameIntMap values
    ) => ChargeState.RestorePerTurnLimitsForMutationSnapshotExact(values);

    internal void RestoreFumbleProtectionForMutationSnapshotExact(
        BattleStringNameIntMap values
    ) => ChargeState.RestoreFumbleProtectionForMutationSnapshotExact(values);

    public void ResetPerTurnCharges() => ChargeState.ResetPerTurn();

    public BattleUnitState clone()
    {
        EnsureBodySizeProjectionInvariant();
        NormalizeShieldState();
        NormalizeWeaponProjection();
        SyncDefaultCombatResourceUnlocks();

        return DuplicateStateCore(canonicalizeSource: true);
    }

    internal BattleUnitState DuplicateForPreview()
    {
        return DuplicateStateCore(canonicalizeSource: false);
    }

    private BattleUnitState DuplicateStateCore(bool canonicalizeSource)
    {
        return new BattleUnitState
        {
            unit_id = unit_id,
            source_member_id = source_member_id,
            enemy_template_id = enemy_template_id,
            encounter_actor_id = encounter_actor_id,
            display_name = display_name,
            battle_sprite_texture_path = battle_sprite_texture_path,
            faction_id = faction_id,
            control_mode = control_mode,
            ai_brain_id = ai_brain_id,
            ai_state_id = ai_state_id,
            _baseCognitionKind = _baseCognitionKind,
            ai_blackboard = ai_blackboard?.Clone() ?? new BattleAiBlackboard(),
            GeometryState = canonicalizeSource
                ? GeometryState.DuplicateState()
                : _geometryState?.DuplicateState() ?? new BattleUnitGeometryState(),
            attribute_snapshot = canonicalizeSource
                ? DuplicateAttributeSnapshot(attribute_snapshot)
                : DuplicateAttributeSnapshotForPreview(attribute_snapshot),
            equipment_view = canonicalizeSource
                ? GetEquipmentView()?.DuplicateState() ?? NewEquipmentState()
                : equipment_view?.DuplicateState(),
            equipment_view_initialized =
                canonicalizeSource || equipment_view_initialized,
            CombatResourceState = canonicalizeSource
                ? CombatResourceState.DuplicateState()
                : _combatResourceState?.DuplicateState() ?? new BattleUnitCombatResourceState(),
            CombatResourceUnlockState = canonicalizeSource
                ? CombatResourceUnlockState.DuplicateState()
                : _combatResourceUnlockState?.DuplicateState()
                    ?? new BattleUnitCombatResourceUnlockState(),
            RestState = canonicalizeSource
                ? RestState.DuplicateState()
                : _restState?.DuplicateState() ?? new BattleUnitRestState(),
            TurnState = canonicalizeSource
                ? TurnState.DuplicateState()
                : _turnState?.DuplicateState() ?? new BattleUnitTurnState(),
            ShieldState = canonicalizeSource
                ? ShieldState.DuplicateState()
                : _shieldState?.DuplicateState() ?? new BattleUnitShieldState(),
            ActionClockState = canonicalizeSource
                ? ActionClockState.DuplicateState()
                : _actionClockState?.DuplicateState() ?? new BattleUnitActionClockState(),
            CastingClockState = canonicalizeSource
                ? CastingClockState.DuplicateState()
                : _castingClockState?.DuplicateState() ?? new BattleUnitCastingClockState(),
            _knownSkillState =
                _knownSkillState?.DuplicateState()
                ?? new BattleUnitKnownSkillState(),
            MovementTagState =
                _movementTagState?.DuplicateState()
                ?? new BattleUnitMovementTagState(),
            VisionProficiencyState =
                _visionProficiencyState?.DuplicateState()
                ?? new BattleUnitVisionProficiencyState(),
            SaveModifierState =
                _saveModifierState?.DuplicateState()
                ?? new BattleUnitSaveModifierState(),
            DamageResistanceState =
                _damageResistanceState?.DuplicateState()
                ?? new BattleUnitDamageResistanceState(),
            EffectiveTraitState =
                _effectiveTraitState?.DuplicateState()
                ?? new BattleUnitEffectiveTraitState(),
            EquipmentAbilityProjectionState =
                _equipmentAbilityProjectionState?.DuplicateState()
                ?? new BattleUnitEquipmentAbilityProjectionState(),
            CreatureTypeState =
                _creatureTypeState?.DuplicateState()
                ?? new BattleUnitCreatureTypeState(),
            versatility_pick = versatility_pick,
            WeaponProjectionState = canonicalizeSource
                ? WeaponProjectionState.DuplicateState()
                : _weaponProjectionState?.DuplicateState()
                    ?? new BattleUnitWeaponProjectionState(),
            CooldownState = canonicalizeSource
                ? CooldownState.DuplicateState()
                : _cooldownState?.DuplicateState() ?? new BattleUnitCooldownState(),
            StatusEffectCollection =
                _statusEffects?.DuplicateState() ?? new BattleStatusEffectCollection(),
            ChargeState =
                _chargeState?.DuplicateState() ?? new BattleUnitChargeState(),
            death_ward_consumed_this_battle = death_ward_consumed_this_battle,
            pending_cast = pending_cast?.Clone(),
            ConsumedContingencySetups =
                _consumedContingencySetups?.DuplicateState()
                ?? new BattleConsumedContingencySetupCollection(),
        };
    }

    public static Vector2I GetFootprintSizeForBodySize(int size_value)
    {
        return BattleUnitGeometryState.GetFootprintSizeForBodySize(
            size_value
        );
    }

    internal IReadOnlyDictionary<string, object> BuildSnapshotPlain()
    {
        EnsureBodySizeProjectionInvariant();
        SyncDefaultCombatResourceUnlocks();
        BattleUnitTurnSnapshot turnState = GetTurnStateTyped();
        BattleUnitShieldSnapshot shieldState = CaptureShieldStateCanonical();
        BattleUnitCooldownSnapshot cooldownState = GetCooldownStateTyped();
        BattleUnitActionClockSnapshot actionClockState =
            GetActionClockStateTyped();
        BattleUnitKnownSkillReadView knownSkillState =
            GetKnownSkillsReadViewTyped();
        BattleUnitCombatResourceUnlockReadView combatResourceUnlockState =
            GetCombatResourceUnlocksReadViewTyped();
        BattleUnitCombatResourceValues combatResources =
            GetCombatResourcesReadViewTyped().Values;
        BattleUnitGeometryReadView geometry =
            GetGeometryReadViewTyped();
        BattleUnitSaveModifierReadView saveModifiers =
            GetSaveModifiersReadViewTyped();
        BattleUnitDamageResistanceReadView damageResistances =
            GetDamageResistancesReadViewTyped();
        BattleUnitEffectiveTraitReadView effectiveTraits =
            GetEffectiveTraitsReadViewTyped();
        BattleUnitEquipmentAbilityProjectionReadView
            equipmentAbilityProjection =
                GetEquipmentAbilityProjectionReadViewTyped();
        NormalizeWeaponProjection();
        BattleWeaponProjectionValues weaponProjection =
            GetWeaponProjectionReadViewTyped().Values;

        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["unit_id"] = unit_id.ToString(),
            ["source_member_id"] = source_member_id.ToString(),
            ["enemy_template_id"] = enemy_template_id.ToString(),
            ["encounter_actor_id"] = encounter_actor_id.ToString(),
            ["display_name"] = display_name,
            ["battle_sprite_texture_path"] = battle_sprite_texture_path,
            ["faction_id"] = faction_id.ToString(),
            ["control_mode"] = control_mode.ToString(),
            ["ai_brain_id"] = ai_brain_id.ToString(),
            ["ai_state_id"] = ai_state_id.ToString(),
            ["cognition_kind"] =
                BattleCognitionContentRules
                    .ToStringName(_baseCognitionKind)
                    .ToString(),
            // ai_blackboard is runtime-only and not serialized
            ["coord"] = geometry.AnchorCoord,
            ["body_size"] = geometry.BodySize,
            ["body_size_category"] =
                geometry.BodySizeCategory.ToString(),
            ["footprint_size"] = geometry.FootprintSize,
            ["occupied_coords"] =
                ProjectVector2IListPlain(geometry.OccupiedCoords),
            ["is_alive"] = combatResources.IsAlive,
            ["attribute_snapshot"] = AttributeSnapshotToPlain(attribute_snapshot),
            ["equipment_view"] = EquipmentViewToPlain(GetEquipmentView()),
            ["current_hp"] = combatResources.Hp,
            ["current_mp"] = combatResources.Mp,
            ["current_stamina"] = combatResources.Stamina,
            ["current_aura"] = combatResources.Aura,
            ["aura_max"] = GetAuraMax(),
            ["current_ap"] = combatResources.Ap,
            ["current_move_points"] = combatResources.MovePoints,
            ["unlocked_combat_resource_ids"] = CombatResourceUnlockViewToPlain(
                combatResourceUnlockState.ResourceIds
            ),
            ["stamina_recovery_progress"] =
                combatResources.StaminaRecoveryProgress,
            ["is_resting"] = IsRestingTyped(),
            ["has_taken_action_this_turn"] = turnState.HasTakenActionThisTurn,
            ["can_use_locked_move_points_this_turn"] =
                turnState.CanUseLockedMovePointsThisTurn,
            ["current_shield_hp"] = shieldState.CurrentHp,
            ["shield_max_hp"] = shieldState.MaxHp,
            ["shield_duration"] = shieldState.Duration,
            ["shield_family"] = shieldState.Family.ToString(),
            ["shield_source_unit_id"] = shieldState.SourceUnitId.ToString(),
            ["shield_source_skill_id"] = shieldState.SourceSkillId.ToString(),
            ["action_progress"] = actionClockState.ActionProgress,
            ["action_threshold"] = actionClockState.ActionThreshold,
            ["known_active_skill_ids"] = KnownActiveSkillViewToPlain(
                knownSkillState.ActiveSkills
            ),
            ["known_skill_level_map"] =
                KnownSkillLevelViewToPlain(knownSkillState.SkillLevels),
            ["known_skill_lock_hit_bonus_map"] =
                KnownSkillLevelViewToPlain(knownSkillState.LockHitBonuses),
            ["movement_tags"] = StringNameListToPlain(
                GetMovementTagsReadViewTyped().Tags
            ),
            ["vision_tags"] = StringNameListToPlain(
                GetVisionProficiencyReadViewTyped().VisionTags
            ),
            ["proficiency_tags"] = StringNameListToPlain(
                GetVisionProficiencyReadViewTyped().ProficiencyTags
            ),
            ["save_advantage_tags"] = StringNameListToPlain(
                saveModifiers.AdvantageTags
            ),
            ["save_disadvantage_tags"] = StringNameListToPlain(
                saveModifiers.DisadvantageTags
            ),
            ["save_immunity_tags"] = StringNameListToPlain(
                saveModifiers.ImmunityTags
            ),
            ["damage_resistances"] =
                DamageResistanceViewToPlain(
                    damageResistances.Resistances
                ),
            ["save_bonus_by_ability"] =
                SaveAbilityBonusViewToPlain(
                    saveModifiers.BonusByAbility
                ),
            ["effective_trait_instances"] = EffectiveTraitInstancesToPlain(
                effectiveTraits.Instances
            ),
            ["effective_trait_ids"] = StringNameListToPlain(
                GetCanonicalEffectiveTraitIdsReadViewTyped()
            ),
            ["equipment_ability_sources"] = EquipmentAbilitySourcesToPlain(
                equipmentAbilityProjection.Sources
            ),
            ["creature_type_tags"] = StringNameListToPlain(
                GetCreatureTypeTagsReadViewTyped().Tags
            ),
            ["versatility_pick"] = versatility_pick.ToString(),
            ["weapon_profile_kind"] = weaponProjection.ProfileKind.ToString(),
            ["weapon_item_id"] = weaponProjection.ItemId.ToString(),
            ["weapon_profile_type_id"] = weaponProjection.ProfileTypeId.ToString(),
            ["weapon_range_type"] = weaponProjection.RangeType.ToString(),
            ["weapon_family"] = weaponProjection.Family.ToString(),
            ["weapon_current_grip"] = weaponProjection.CurrentGrip.ToString(),
            ["weapon_attack_range"] = weaponProjection.AttackRange,
            ["weapon_one_handed_dice"] = WeaponDiceToPlain(
                weaponProjection.OneHandedDice
            ),
            ["weapon_two_handed_dice"] = WeaponDiceToPlain(
                weaponProjection.TwoHandedDice
            ),
            ["weapon_is_versatile"] = weaponProjection.IsVersatile,
            ["weapon_uses_two_hands"] = weaponProjection.UsesTwoHands,
            ["weapon_physical_damage_tag"] =
                weaponProjection.PhysicalDamageTag.ToString(),
            ["cooldowns"] = StringNameIntMapToStringNameKeyPlain(
                cooldownState.Cooldowns
            ),
            ["last_turn_tu"] = cooldownState.LastTurnTu,
            ["status_effects"] = _statusEffects.BuildSnapshotPlain(),
        };
    }

    internal GodotProjectionLease<GDictionary> ToDictionaryLease(
        LifetimeDomain domain,
        string reason
    ) =>
        RuntimePlainPayload.ProjectDictionaryLease(
            BuildSnapshotPlain(),
            "battle-unit-state",
            domain,
            reason
        );

    internal static bool TryReadUnitPayload(object rawValue, out BattleUnitState value)
    {
        value = null;
        switch (rawValue)
        {
            case null:
                return false;
            case BattleUnitState unit:
                value = unit;
                return value != null;
            case Variant variantValue when variantValue.VariantType == Variant.Type.Dictionary:
                value = FromDictionary(variantValue.AsGodotDictionary());
                return value != null;
            case GDictionary payload:
                value = FromDictionary(payload);
                return value != null;
            default:
                return false;
        }
    }

    public static BattleUnitState FromDictionary(GDictionary payload)
    {
        if (payload == null)
            return null;
        if (payload.Count == 0)
        {
            return null;
        }
        if (!HasExactFields(payload, ToDictFields))
        {
            return null;
        }

        var coordValue = payload["coord"];
        var bodySizeValue = payload["body_size"];
        var bodySizeCategoryValue = payload["body_size_category"];
        var footprintSizeValue = payload["footprint_size"];
        var occupiedCoordsValue = payload["occupied_coords"];
        if (
            coordValue.VariantType.ToString() != "Vector2I"
            || bodySizeValue.VariantType.ToString() != "Int"
            || footprintSizeValue.VariantType.ToString() != "Vector2I"
        )
        {
            return null;
        }
        int bodySizeInt = bodySizeValue.AsInt32();
        if (bodySizeInt < 1)
        {
            return null;
        }
        if (
            !IsStringNamePayloadType(bodySizeCategoryValue.VariantType.ToString())
            || IsEmpty(ToStringName(bodySizeCategoryValue))
        )
        {
            return null;
        }
        StringName parsedBodySizeCategory = ToStringName(bodySizeCategoryValue);
        if (
            !BattleUnitGeometryState.IsValidBodySizeCategory(
                parsedBodySizeCategory
            )
        )
        {
            return null;
        }
        if (
            BattleUnitGeometryState.GetBodySizeForCategory(
                parsedBodySizeCategory
            ) != bodySizeInt
        )
        {
            return null;
        }
        Vector2I expectedFootprint = GetFootprintSizeForBodySize(bodySizeInt);
        if (footprintSizeValue.AsVector2I() != expectedFootprint)
        {
            return null;
        }
        if (occupiedCoordsValue.VariantType.ToString() != "Array")
        {
            return null;
        }
        Vector2IList parsedOccupiedCoords = new();
        foreach (var occupiedCoordValue in occupiedCoordsValue.AsGodotArray())
        {
            if (occupiedCoordValue.VariantType.ToString() != "Vector2I")
            {
                return null;
            }
            parsedOccupiedCoords.Add(occupiedCoordValue.AsVector2I());
        }
        if (
            !BattleUnitGeometryState.OccupiedCoordsMatch(
                coordValue.AsVector2I(),
                expectedFootprint,
                parsedOccupiedCoords
            )
        )
        {
            return null;
        }

        foreach (
            string fieldName in new[] { "unit_id", "display_name", "faction_id", "control_mode" }
        )
        {
            if (
                !IsStringNamePayloadType(payload[fieldName].VariantType.ToString())
                || IsEmpty(ToStringName(payload[fieldName]))
            )
            {
                return null;
            }
        }
        foreach (
            string fieldName in new[]
            {
                "source_member_id",
                "enemy_template_id",
                "encounter_actor_id",
                "ai_brain_id",
                "ai_state_id",
                "cognition_kind",
                "shield_family",
                "shield_source_unit_id",
                "shield_source_skill_id",
                "weapon_item_id",
                "weapon_profile_type_id",
                "weapon_range_type",
                "weapon_family",
                "weapon_physical_damage_tag",
                "versatility_pick",
            }
        )
        {
            if (!IsStringNamePayloadType(payload[fieldName].VariantType.ToString()))
            {
                return null;
            }
        }
        foreach (
            string fieldName in new[]
            {
                "current_hp",
                "current_mp",
                "current_stamina",
                "current_aura",
                "aura_max",
                "current_ap",
                "current_move_points",
                "stamina_recovery_progress",
                "current_shield_hp",
                "shield_max_hp",
                "shield_duration",
                "action_progress",
                "action_threshold",
                "weapon_attack_range",
                "last_turn_tu",
            }
        )
        {
            if (payload[fieldName].VariantType.ToString() != "Int")
            {
                return null;
            }
        }
        BattleCognitionKind parsedCognitionKind =
            BattleCognitionContentRules.ToKind(
                ToStringName(payload["cognition_kind"])
            );
        if (
            !BattleCognitionContentRules.IsKnown(
                parsedCognitionKind
            )
        )
        {
            return null;
        }
        if (payload["current_move_points"].AsInt32() < 0)
        {
            return null;
        }
        foreach (
            string fieldName in new[]
            {
                "is_alive",
                "is_resting",
                "has_taken_action_this_turn",
                "can_use_locked_move_points_this_turn",
                "weapon_is_versatile",
                "weapon_uses_two_hands",
            }
        )
        {
            if (payload[fieldName].VariantType.ToString() != "Bool")
            {
                return null;
            }
        }
        foreach (
            string fieldName in new[]
            {
                "attribute_snapshot",
                "equipment_view",
                "weapon_one_handed_dice",
                "weapon_two_handed_dice",
                "cooldowns",
                "known_skill_level_map",
                "known_skill_lock_hit_bonus_map",
                "status_effects",
                "damage_resistances",
            }
        )
        {
            if (payload[fieldName].VariantType.ToString() != "Dictionary")
            {
                return null;
            }
        }

        AttributeSnapshot parsedAttributeSnapshot = AttributeSnapshotFromDictionary(
            payload["attribute_snapshot"].AsGodotDictionary()
        );
        if (parsedAttributeSnapshot == null)
        {
            return null;
        }
        BattleStringNameIntMap parsedKnownSkillLevelMap = BattleStringNameIntMap.FromPayloadOrNull(
            payload["known_skill_level_map"].AsGodotDictionary(),
            true
        );
        if (parsedKnownSkillLevelMap == null)
        {
            return null;
        }
        BattleStringNameIntMap parsedKnownSkillLockHitBonusMap =
            BattleStringNameIntMap.FromPayloadOrNull(
                payload["known_skill_lock_hit_bonus_map"].AsGodotDictionary(),
                true
            );
        if (parsedKnownSkillLockHitBonusMap == null)
        {
            return null;
        }
        foreach (StringName skillId in parsedKnownSkillLockHitBonusMap.Keys)
        {
            if (parsedKnownSkillLockHitBonusMap.Get(skillId) < 0)
            {
                return null;
            }
        }
        StringNameList parsedUnlockedResources = _combat_resource_array_from_payload(
            GetArray(payload, "unlocked_combat_resource_ids")
        );
        if (parsedUnlockedResources.Count == 0)
        {
            return null;
        }
        StringNameList parsedKnownActiveSkillIds = _unique_string_name_array_from_payload(
            GetArray(payload, "known_active_skill_ids")
        );
        if (parsedKnownActiveSkillIds == null)
        {
            return null;
        }
        StringNameList parsedMovementTags = _unique_string_name_array_from_payload(
            GetArray(payload, "movement_tags")
        );
        if (parsedMovementTags == null)
        {
            return null;
        }
        StringNameList parsedVisionTags = _unique_string_name_array_from_payload(
            GetArray(payload, "vision_tags")
        );
        if (parsedVisionTags == null)
        {
            return null;
        }
        StringNameList parsedProficiencyTags = _unique_string_name_array_from_payload(
            GetArray(payload, "proficiency_tags")
        );
        if (parsedProficiencyTags == null)
        {
            return null;
        }
        StringNameList parsedSaveAdvantageTags = _unique_string_name_array_from_payload(
            GetArray(payload, "save_advantage_tags")
        );
        if (parsedSaveAdvantageTags == null)
        {
            return null;
        }
        StringNameList parsedSaveDisadvantageTags = _unique_string_name_array_from_payload(
            GetArray(payload, "save_disadvantage_tags")
        );
        if (parsedSaveDisadvantageTags == null)
        {
            return null;
        }
        StringNameList parsedSaveImmunityTags = _unique_string_name_array_from_payload(
            GetArray(payload, "save_immunity_tags")
        );
        if (parsedSaveImmunityTags == null)
        {
            return null;
        }
        List<BattleEffectiveTraitInstanceState> parsedEffectiveTraitInstances = EffectiveTraitInstancesFromPayloadArray(
            GetArray(payload, "effective_trait_instances")
        );
        if (parsedEffectiveTraitInstances == null)
        {
            return null;
        }
        StringNameList parsedEffectiveTraitIds = _unique_string_name_array_from_payload(
            GetArray(payload, "effective_trait_ids")
        );
        if (
            parsedEffectiveTraitIds == null
            || !StringNameSetEquals(
                parsedEffectiveTraitIds,
                BattleUnitEffectiveTraitState.DeriveTraitIds(
                    parsedEffectiveTraitInstances
                )
            )
        )
        {
            return null;
        }
        List<BattleEquipmentAbilitySourceState> parsedEquipmentAbilitySources =
            EquipmentAbilitySourcesFromPayloadArray(GetArray(payload, "equipment_ability_sources"));
        if (parsedEquipmentAbilitySources == null)
        {
            return null;
        }
        StringNameList parsedCreatureTypeTags = _unique_string_name_array_from_payload(
            GetArray(payload, "creature_type_tags")
        );
        if (parsedCreatureTypeTags == null)
        {
            return null;
        }
        BattleStringNameMap parsedDamageResistances = _damage_resistance_map_from_dict(
            payload["damage_resistances"].AsGodotDictionary()
        );
        if (parsedDamageResistances == null)
        {
            return null;
        }
        BattleStringNameIntMap parsedSaveBonusByAbility =
            payload.ContainsKey("save_bonus_by_ability")
            && payload["save_bonus_by_ability"].VariantType.ToString() == "Dictionary"
                ? BattleStringNameIntMap.FromPayloadOrNull(
                    payload["save_bonus_by_ability"].AsGodotDictionary(),
                    true
                ) ?? new BattleStringNameIntMap()
                : new BattleStringNameIntMap();

        StringName parsedWeaponProfileKind = ToStringName(payload["weapon_profile_kind"]);
        if (!IsValidWeaponProfileKind(parsedWeaponProfileKind))
        {
            return null;
        }
        StringName parsedWeaponCurrentGrip = ToStringName(payload["weapon_current_grip"]);
        if (!IsValidWeaponGrip(parsedWeaponCurrentGrip))
        {
            return null;
        }
        WeaponDice parsedWeaponOneHandedDice = StrictWeaponDiceFromDictionary(
            payload["weapon_one_handed_dice"].AsGodotDictionary()
        );
        if (parsedWeaponOneHandedDice == null)
        {
            return null;
        }
        WeaponDice parsedWeaponTwoHandedDice = StrictWeaponDiceFromDictionary(
            payload["weapon_two_handed_dice"].AsGodotDictionary()
        );
        if (parsedWeaponTwoHandedDice == null)
        {
            return null;
        }

        EquipmentState parsedEquipmentState = EquipmentFromDict(
            payload["equipment_view"].AsGodotDictionary()
        );
        if (parsedEquipmentState == null)
        {
            return null;
        }
        BattleStatusEffectCollection parsedStatusEffects;
        try
        {
            parsedStatusEffects = BattleStatusEffectCollection.FromDictionary(
                payload["status_effects"].AsGodotDictionary()
            );
        }
        catch (ArgumentException)
        {
            return null;
        }

        BattleUnitState unitState = new()
        {
            unit_id = ToStringName(payload["unit_id"]),
            source_member_id = ToStringName(payload["source_member_id"]),
            enemy_template_id = ToStringName(payload["enemy_template_id"]),
            encounter_actor_id = ToStringName(payload["encounter_actor_id"]),
            display_name = payload["display_name"].AsString(),
            battle_sprite_texture_path = payload["battle_sprite_texture_path"].AsString(),
            faction_id = ToStringName(payload["faction_id"]),
            control_mode = ToStringName(payload["control_mode"]),
            ai_brain_id = ToStringName(payload["ai_brain_id"]),
            ai_state_id = ToStringName(payload["ai_state_id"]),
            _baseCognitionKind = parsedCognitionKind,
            ai_blackboard = new BattleAiBlackboard(),
            GeometryState = BattleUnitGeometryState.FromRaw(
                BattleUnitGeometrySnapshot.Present(
                    coordValue.AsVector2I(),
                    bodySizeInt,
                    parsedBodySizeCategory,
                    footprintSizeValue.AsVector2I(),
                    parsedOccupiedCoords
                )
            ),
            attribute_snapshot = parsedAttributeSnapshot,
            equipment_view = parsedEquipmentState,
            equipment_view_initialized = true,
            CombatResourceState =
                BattleUnitCombatResourceState.FromRaw(
                    BattleUnitCombatResourceSnapshot.Present(
                        new BattleUnitCombatResourceValues(
                            payload["current_hp"].AsInt32(),
                            payload["current_mp"].AsInt32(),
                            payload["current_stamina"].AsInt32(),
                            payload["current_aura"].AsInt32(),
                            payload["current_ap"].AsInt32(),
                            payload["current_move_points"].AsInt32(),
                            payload[
                                "stamina_recovery_progress"
                            ].AsInt32(),
                            ReadBool(payload, "is_alive")
                        )
                    )
                ),
            CombatResourceUnlockState =
                BattleUnitCombatResourceUnlockState.FromRaw(
                    BattleUnitCombatResourceUnlockSnapshot.Present(
                        parsedUnlockedResources
                    )
                ),
            RestState = BattleUnitRestState.FromRaw(
                BattleUnitRestSnapshot.Present(
                    ReadBool(payload, "is_resting")
                )
            ),
            TurnState = BattleUnitTurnState.FromRaw(
                BattleUnitTurnSnapshot.Present(
                    ReadBool(payload, "has_taken_action_this_turn"),
                    false,
                    ReadBool(payload, "can_use_locked_move_points_this_turn"),
                    false
                )
            ),
            ShieldState = BattleUnitShieldState.FromRaw(
                new BattleUnitShieldSnapshot(
                    payload["current_shield_hp"].AsInt32(),
                    payload["shield_max_hp"].AsInt32(),
                    payload["shield_duration"].AsInt32(),
                    ToStringName(payload["shield_family"]),
                    ToStringName(payload["shield_source_unit_id"]),
                    ToStringName(payload["shield_source_skill_id"])
                )
            ),
            ActionClockState = BattleUnitActionClockState.FromRaw(
                BattleUnitActionClockSnapshot.Present(
                    payload["action_progress"].AsInt32(),
                    payload["action_threshold"].AsInt32(),
                    0
                )
            ),
            _knownSkillState = BattleUnitKnownSkillState.FromRaw(
                BattleUnitKnownSkillSnapshot.Present(
                    parsedKnownActiveSkillIds,
                    parsedKnownSkillLevelMap,
                    parsedKnownSkillLockHitBonusMap
                )
            ),
            MovementTagState = BattleUnitMovementTagState.FromRaw(
                BattleUnitMovementTagSnapshot.Present(
                    parsedMovementTags
                )
            ),
            VisionProficiencyState =
                BattleUnitVisionProficiencyState.FromRaw(
                    BattleUnitVisionProficiencySnapshot.Present(
                        parsedVisionTags,
                        parsedProficiencyTags
                    )
                ),
            SaveModifierState =
                BattleUnitSaveModifierState.FromRaw(
                    BattleUnitSaveModifierSnapshot.Present(
                        parsedSaveAdvantageTags,
                        parsedSaveDisadvantageTags,
                        parsedSaveImmunityTags,
                        parsedSaveBonusByAbility
                    )
                ),
            DamageResistanceState =
                BattleUnitDamageResistanceState.FromRaw(
                    BattleUnitDamageResistanceSnapshot.Present(
                        parsedDamageResistances
                    )
                ),
            EffectiveTraitState = BattleUnitEffectiveTraitState.FromRaw(
                BattleUnitEffectiveTraitSnapshot.Present(
                    parsedEffectiveTraitInstances,
                    parsedEffectiveTraitIds
                )
            ),
            EquipmentAbilityProjectionState =
                BattleUnitEquipmentAbilityProjectionState
                    .FromSourcesNormalized(
                        parsedEquipmentAbilitySources
                    ),
            CreatureTypeState = BattleUnitCreatureTypeState.FromRaw(
                BattleUnitCreatureTypeSnapshot.Present(
                    parsedCreatureTypeTags
                )
            ),
            versatility_pick = ToStringName(payload["versatility_pick"]),
            WeaponProjectionState = BattleUnitWeaponProjectionState.FromRaw(
                BattleUnitWeaponProjectionSnapshot.Present(
                    new BattleWeaponProjectionValues(
                        parsedWeaponProfileKind,
                        ToStringName(payload["weapon_item_id"]),
                        new StringName(""),
                        ToStringName(payload["weapon_profile_type_id"]),
                        ToStringName(payload["weapon_range_type"]),
                        ToStringName(payload["weapon_family"]),
                        parsedWeaponCurrentGrip,
                        payload["weapon_attack_range"].AsInt32(),
                        BattleWeaponDiceValues.FromRaw(
                            parsedWeaponOneHandedDice
                        ),
                        BattleWeaponDiceValues.FromRaw(
                            parsedWeaponTwoHandedDice
                        ),
                        ReadBool(payload, "weapon_is_versatile"),
                        ReadBool(payload, "weapon_uses_two_hands"),
                        false,
                        ToStringName(
                            payload["weapon_physical_damage_tag"]
                        )
                    )
                )
            ),
            CooldownState = BattleUnitCooldownState.FromRaw(
                new BattleUnitCooldownSnapshot(
                    BattleStringNameIntMap.FromPayloadOrNull(
                        payload["cooldowns"].AsGodotDictionary(),
                        false
                    ) ?? new BattleStringNameIntMap(),
                    payload["last_turn_tu"].AsInt32()
                )
            ),
            StatusEffectCollection = parsedStatusEffects,
        };
        unitState.attribute_snapshot.SetValue("aura_max", payload["aura_max"].AsInt32());
        unitState.NormalizeShieldState();
        unitState.EnsureBodySizeProjectionInvariant();
        return unitState;
    }

    private static AttributeSnapshot AttributeSnapshotFromDictionary(GDictionary values)
    {
        if (values == null)
            return null;
        AttributeSnapshot snapshot = NewAttributeSnapshot();
        if (snapshot == null)
        {
            return null;
        }
        foreach (var key in values.Keys)
        {
            if (!IsStringNamePayloadType(key.VariantType.ToString()))
            {
                return null;
            }
            if (values[key].VariantType.ToString() != "Int")
            {
                return null;
            }
            snapshot.SetValue(ToStringName(key), values[key].AsInt32());
        }
        return snapshot;
    }

    private static bool ReadBool(GDictionary payload, string key)
    {
        if (payload == null || !payload.ContainsKey(key))
            return false;
        var value = payload[key];
        return value.VariantType.ToString() == "Bool" && value.AsBool();
    }

    private static bool HasExactFields(GDictionary data, string[] expected_fields)
    {
        if (data.Count != expected_fields.Length)
        {
            return false;
        }
        HashSet<string> expected = new(expected_fields);
        HashSet<string> seen = new();
        foreach (var key in data.Keys)
        {
            if (!IsStringNamePayloadType(key.VariantType.ToString()))
            {
                return false;
            }
            string keyString = key.AsString();
            if (!expected.Contains(keyString) || !seen.Add(keyString))
            {
                return false;
            }
        }
        return seen.Count == expected.Count;
    }

    private static bool IsStringNamePayloadType(string valueType)
    {
        return valueType == "String" || valueType == "StringName";
    }

    private static GDictionary _string_name_int_map_from_dict(
        GDictionary values,
        bool require_non_empty_key
    )
    {
        if (values == null)
        {
            return null;
        }
        GDictionary result = new();
        foreach (var key in values.Keys)
        {
            if (!IsStringNamePayloadType(key.VariantType.ToString()))
            {
                return null;
            }
            StringName keyName = ToStringName(key);
            if (require_non_empty_key && IsEmpty(keyName))
            {
                return null;
            }
            if (values[key].VariantType.ToString() != "Int")
            {
                return null;
            }
            result[keyName] = values[key].AsInt32();
        }
        return result;
    }

    private static BattleStringNameMap _damage_resistance_map_from_dict(GDictionary values)
    {
        if (values == null)
            return null;
        BattleStringNameMap result = new();
        foreach (var key in values.Keys)
        {
            if (
                !IsStringNamePayloadType(key.VariantType.ToString())
                || IsEmpty(ToStringName(key))
            )
            {
                return null;
            }
            StringName damageTag = ToStringName(key);
            if (result.ContainsKey(damageTag))
            {
                return null;
            }
            if (!IsStringNamePayloadType(values[key].VariantType.ToString()))
            {
                return null;
            }
            StringName mitigationTier = ToStringName(values[key]);
            if (
                IsEmpty(mitigationTier)
                || DamageTagContentRules.ToMitigationTierKind(mitigationTier)
                    == DamageMitigationTierKind.Unknown
            )
            {
                return null;
            }
            result.Put(damageTag, mitigationTier);
        }
        return result;
    }

    private static StringNameList _unique_string_name_array_from_payload(GArray values)
    {
        if (values == null)
        {
            return null;
        }
        StringNameList result = new();
        HashSet<StringName> seen = new();
        foreach (var value in values)
        {
            if (
                !IsStringNamePayloadType(value.VariantType.ToString())
                || IsEmpty(ToStringName(value))
            )
            {
                return null;
            }
            StringName normalized = ToStringName(value);
            if (!seen.Add(normalized))
            {
                return null;
            }
            result.Add(normalized);
        }
        return result;
    }

    internal static List<BattleEffectiveTraitInstanceState> EffectiveTraitInstancesFromPayloadArray(GArray values)
    {
        if (values == null)
            return null;

        List<BattleEffectiveTraitInstanceState> result = new();
        List<StringName> seenKeys = new();
        foreach (Variant entry in values)
        {
            if (entry.VariantType != Variant.Type.Dictionary)
                return null;

            BattleEffectiveTraitInstanceState parsed =
                BattleEffectiveTraitInstanceState.FromDictionary(entry.AsGodotDictionary());
            if (parsed == null)
                return null;
            if (ContainsStringName(seenKeys, parsed.effective_instance_key))
                return null;
            seenKeys.Add(parsed.effective_instance_key);
            result.Add(parsed);
        }
        return result;
    }

    private static List<object> EffectiveTraitInstancesToPlain(
        IEnumerable<BattleEffectiveTraitInstanceReadView> source)
    {
        List<object> result = new();
        if (source == null)
            return result;
        foreach (BattleEffectiveTraitInstanceReadView entry in source)
            if (entry.IsPresent)
                result.Add(EffectiveTraitInstanceToPlain(entry));
        return result;
    }

    internal static List<BattleEquipmentAbilitySourceState> EquipmentAbilitySourcesFromPayloadArray(
        GArray values
    )
    {
        if (values == null)
            return null;

        List<BattleEquipmentAbilitySourceState> result = new();
        List<StringName> seenKeys = new();
        foreach (Variant entry in values)
        {
            if (entry.VariantType != Variant.Type.Dictionary)
                return null;

            BattleEquipmentAbilitySourceState parsed =
                BattleEquipmentAbilitySourceState.FromDictionary(entry.AsGodotDictionary());
            if (parsed == null)
                return null;
            if (ContainsStringName(seenKeys, parsed.EffectiveInstanceKey))
                return null;
            seenKeys.Add(parsed.EffectiveInstanceKey);
            result.Add(parsed);
        }
        return result;
    }

    private static List<object> EquipmentAbilitySourcesToPlain(
        IEnumerable<BattleEquipmentAbilitySourceReadView> source
    )
    {
        List<object> result = new();
        if (source == null)
            return result;
        foreach (
            BattleEquipmentAbilitySourceReadView entry in source
        )
            if (entry != null)
                result.Add(EquipmentAbilitySourceToPlain(entry));
        return result;
    }

    private static bool ContainsStringName(List<StringName> values, StringName expected)
    {
        foreach (StringName value in values)
            if (value == expected)
                return true;
        return false;
    }

    private static bool IsStringNameField(GDictionary data, string key)
    {
        return data != null
            && data.ContainsKey(key)
            && IsStringNamePayloadType(data[key].VariantType.ToString());
    }

    private static bool StringNameSetEquals(IEnumerable<StringName> left, IEnumerable<StringName> right)
    {
        if (left == null || right == null)
            return false;
        HashSet<StringName> rightSet = new();
        int rightCount = 0;
        foreach (StringName value in right)
        {
            rightSet.Add(value);
            rightCount += 1;
        }
        int leftCount = 0;
        foreach (StringName value in left)
        {
            leftCount += 1;
            if (!rightSet.Contains(value))
                return false;
        }
        return leftCount == rightCount;
    }

    private static StringNameList _combat_resource_array_from_payload(GArray values)
    {
        StringNameList parsed = _unique_string_name_array_from_payload(values);
        if (parsed == null)
        {
            return new StringNameList();
        }
        if (
            !parsed.Contains(CombatResourceIds.ToStringName(CombatResourceIdKind.Hp))
            || !parsed.Contains(CombatResourceIds.ToStringName(CombatResourceIdKind.Stamina))
        )
        {
            return new StringNameList();
        }
        foreach (StringName resourceId in parsed)
        {
            if (!IsValidCombatResourceId(resourceId))
            {
                return new StringNameList();
            }
        }
        return parsed;
    }

    private static bool IsValidWeaponProfileKind(StringName value)
    {
        return value == WeaponProfileKindNone
            || value == WeaponProfileKindUnarmed
            || value == WeaponProfileKindNatural
            || value == WeaponProfileKindEquipped;
    }

    private static bool IsValidWeaponGrip(StringName value)
    {
        return value == WeaponGripNone
            || value == WeaponGripOneHanded
            || value == WeaponGripTwoHanded;
    }

    private static WeaponDice StrictWeaponDiceFromDictionary(GDictionary diceData)
    {
        if (diceData == null)
            return null;
        if (diceData.Count == 0)
        {
            return new WeaponDice();
        }
        if (!HasExactFields(diceData, new[] { "dice_count", "dice_sides", "flat_bonus" }))
        {
            return null;
        }
        foreach (string fieldName in new[] { "dice_count", "dice_sides", "flat_bonus" })
        {
            if (diceData[fieldName].VariantType.ToString() != "Int")
            {
                return null;
            }
        }
        int diceCount = diceData["dice_count"].AsInt32();
        int diceSides = diceData["dice_sides"].AsInt32();
        if (diceCount <= 0 || diceSides <= 0)
        {
            return null;
        }
        return new WeaponDice
        {
            dice_count = diceCount,
            dice_sides = diceSides,
            flat_bonus = diceData["flat_bonus"].AsInt32(),
        };
    }

    private void NormalizeWeaponProjection() =>
        WeaponProjectionState.NormalizeCanonicalInPlace();

    private static List<object> StringNameListToPlain(IEnumerable<StringName> values)
    {
        List<object> results = new();
        if (values == null)
        {
            return results;
        }
        foreach (StringName value in values)
        {
            results.Add(value.ToString());
        }
        return results;
    }

    private static List<object> KnownActiveSkillViewToPlain(
        BattleKnownActiveSkillReadView values
    )
    {
        List<object> results = new();
        foreach (StringName value in values)
            results.Add(value.ToString());
        return results;
    }

    private static List<object> CombatResourceUnlockViewToPlain(
        BattleCombatResourceUnlockReadView values
    )
    {
        List<object> results = new();
        foreach (StringName value in values)
            results.Add(value.ToString());
        return results;
    }

    private static List<StringName> CopyStringNameListTyped(IEnumerable<StringName> values)
    {
        var results = new List<StringName>();
        foreach (StringName value in values ?? new GStringNameArray())
        {
            if (!IsEmpty(value))
                results.Add(value);
        }
        return results;
    }

    private static GDictionary StringNameMapToStringDict(GDictionary values)
    {
        GDictionary results = new();
        if (values == null)
        {
            return results;
        }
        foreach (var key in values.Keys)
        {
            results[key.AsString()] = values[key].AsString();
        }
        return results;
    }

    private static GStringNameArray StringsToStringNameArray(GArray values)
    {
        GStringNameArray results = new();
        if (values == null)
        {
            return results;
        }
        foreach (var value in values)
        {
            results.Add(ToStringName(value));
        }
        return results;
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
    }

    private static StringName ToStringName(StringName value)
    {
        return value ?? new StringName("");
    }

    private static StringName ToStringName<TValue>(TValue rawValue)
    {
        return ProgressionDataUtils.to_string_name(rawValue);
    }

    private static GArray GetArray(GDictionary values, string key)
    {
        if (values == null || !values.ContainsKey(key))
            return new GArray();
        var value = values[key];
        return value.VariantType == Variant.Type.Array ? value.AsGodotArray() : null;
    }

    private static int GetInt(GDictionary values, string key, int fallback)
    {
        if (values == null || !values.ContainsKey(key))
            return fallback;
        var value = values[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static StringName GetStringNameValue(
        GDictionary values,
        string key,
        StringName fallback = default
    )
    {
        if (values == null || !values.ContainsKey(key))
            return fallback ?? "";
        return ToStringName(values[key]);
    }

    private static GDictionary DuplicateDictionary(GDictionary source, bool deep)
    {
        return source != null ? source.Duplicate(deep) : new GDictionary();
    }

    private static bool TryGetIntMapValue(
        GDictionary source,
        StringName key,
        out int parsedValue
    )
    {
        parsedValue = 0;
        if (source == null || IsEmpty(key))
        {
            return false;
        }

        foreach (Variant rawKey in source.Keys)
        {
            if (ToStringName(rawKey) != key)
            {
                continue;
            }
            return TryReadVariantInt(source[rawKey], out parsedValue);
        }
        return false;
    }

    private static Dictionary<StringName, int> CopyStringNameIntMapTyped(GDictionary source)
    {
        var result = new Dictionary<StringName, int>();
        if (source == null)
        {
            return result;
        }

        foreach (Variant rawKey in source.Keys)
        {
            StringName normalizedKey = ToStringName(rawKey);
            if (IsEmpty(normalizedKey))
            {
                continue;
            }

            Variant value = source[rawKey];
            if (!TryReadVariantInt(value, out int parsedValue))
            {
                continue;
            }

            result[normalizedKey] = parsedValue;
        }

        return result;
    }

    private static Dictionary<StringName, StringName> CopyStringNameMapTyped(GDictionary source)
    {
        var result = new Dictionary<StringName, StringName>();
        if (source == null)
        {
            return result;
        }

        foreach (Variant rawKey in source.Keys)
        {
            StringName normalizedKey = ToStringName(rawKey);
            if (IsEmpty(normalizedKey))
            {
                continue;
            }

            StringName normalizedValue = ToStringName(source[rawKey]);
            if (IsEmpty(normalizedValue))
            {
                continue;
            }

            result[normalizedKey] = normalizedValue;
        }

        return result;
    }

    private static IReadOnlyDictionary<string, object> WeaponDiceToPlain(
        BattleWeaponDiceValues dice
    )
    {
        if (!dice.HasUsableDice)
            return new Dictionary<string, object>(StringComparer.Ordinal);
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["dice_count"] = dice.DiceCount,
            ["dice_sides"] = dice.DiceSides,
            ["flat_bonus"] = dice.FlatBonus,
        };
    }

    private static bool TryReadVariantInt(Variant value, out int parsedValue)
    {
        if (value.VariantType == Variant.Type.Int)
        {
            parsedValue = value.AsInt32();
            return true;
        }

        return int.TryParse(value.ToString(), out parsedValue);
    }

    private static List<object> ProjectVector2IListPlain(IEnumerable<Vector2I> source)
    {
        List<object> result = new();
        foreach (Vector2I value in source ?? System.Array.Empty<Vector2I>())
            result.Add(value);
        return result;
    }

    private static AttributeSnapshot DuplicateAttributeSnapshot(AttributeSnapshot source)
    {
        AttributeSnapshot result = NewAttributeSnapshot();
        if (source == null)
        {
            return result;
        }
        foreach ((StringName key, int value) in source.GetAllValuesTyped())
        {
            result.SetValue(key, value);
        }
        return result;
    }

    private static AttributeSnapshot DuplicateAttributeSnapshotForPreview(
        AttributeSnapshot source
    )
    {
        return source?.DuplicateForPreviewExact() ?? NewAttributeSnapshot();
    }

    private static GDictionary StringNameIntMapToStringDict(GDictionary values)
    {
        GDictionary result = new();
        if (values == null)
        {
            return result;
        }
        foreach (var key in values.Keys)
        {
            result[key.AsString()] = values[key].AsInt32();
        }
        return result;
    }

    private static IReadOnlyDictionary<string, object> AttributeSnapshotToPlain(
        AttributeSnapshot snapshot
    )
    {
        Dictionary<string, object> result = new(StringComparer.Ordinal);
        if (snapshot == null)
            return result;
        foreach ((StringName key, int value) in snapshot.GetAllValuesTyped())
            result[key.ToString()] = value;
        return result;
    }

    private static IReadOnlyDictionary<string, object> EquipmentViewToPlain(EquipmentState view)
    {
        Dictionary<string, object> equippedSlots = new(StringComparer.Ordinal);
        if (view != null)
        {
            foreach (StringName entrySlotId in view.GetEntrySlotIdsTyped())
            {
                EquipmentEntryState entry = view.GetEntry(entrySlotId);
                if (entry != null)
                    equippedSlots[entrySlotId.ToString()] = EquipmentEntryToPlain(entry);
            }
        }
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["equipped_slots"] = equippedSlots,
        };
    }

    private static IReadOnlyDictionary<string, object> EquipmentEntryToPlain(
        EquipmentEntryState entry
    ) =>
        new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["occupied_slot_ids"] = StringNameListToPlain(entry?.occupied_slot_ids),
            ["equipment_instance"] = EquipmentInstanceToPlain(entry?.equipment_instance),
        };

    private static IReadOnlyDictionary<string, object> EquipmentInstanceToPlain(
        EquipmentInstanceState instance
    )
    {
        if (instance == null)
            return new Dictionary<string, object>(StringComparer.Ordinal);
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["instance_id"] = instance.instance_id.ToString(),
            ["item_id"] = instance.item_id.ToString(),
            ["rarity"] = instance.rarity,
            ["current_durability"] = instance.current_durability,
            ["trait_instances"] = TraitInstancesToPlain(instance.trait_instances),
            ["ability_usage_periods"] = AbilityUsagePeriodsToPlain(
                instance.ability_usage_periods
            ),
            ["ability_persistent_counters"] = AbilityPersistentCountersToPlain(
                instance.ability_persistent_counters
            ),
        };
    }

    private static List<object> TraitInstancesToPlain(IEnumerable<TraitInstanceState> source)
    {
        List<object> result = new();
        if (source == null)
            return result;
        foreach (TraitInstanceState instance in source)
        {
            if (instance == null)
                continue;
            result.Add(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["trait_instance_id"] = instance.trait_instance_id.ToString(),
                    ["trait_id"] = instance.trait_id.ToString(),
                    ["source_type"] = instance.source_type.ToString(),
                    ["source_id"] = instance.source_id.ToString(),
                    ["rank"] = instance.rank,
                    ["stacks"] = instance.stacks,
                    ["roll_values"] = TraitRollValuesToPlain(instance.roll_values),
                }
            );
        }
        return result;
    }

    private static IReadOnlyDictionary<StringName, object> TraitRollValuesToPlain(
        IEnumerable<TraitRollValueState> source
    )
    {
        Dictionary<StringName, object> result = new();
        foreach (TraitRollValueState entry in TraitInstanceState.NormalizeRollValues(source))
        {
            result[entry.key] = entry.ValueTypeKind switch
            {
                TraitRollValueType.Int => entry.int_value,
                TraitRollValueType.StringName => entry.string_name_value,
                TraitRollValueType.Bool => entry.bool_value,
                _ => null,
            };
        }
        return result;
    }

    private static List<object> AbilityUsagePeriodsToPlain(
        IEnumerable<EquipmentAbilityUsagePeriodState> source
    )
    {
        List<object> result = new();
        if (source == null)
            return result;
        foreach (EquipmentAbilityUsagePeriodState usage in source)
        {
            if (usage == null)
                continue;
            result.Add(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["ability_id"] = usage.AbilityId ?? "",
                    ["period_kind"] = usage.PeriodKind ?? "",
                    ["period_index"] = usage.PeriodIndex,
                    ["used_count"] = usage.UsedCount,
                }
            );
        }
        return result;
    }

    private static List<object> AbilityPersistentCountersToPlain(
        IEnumerable<EquipmentAbilityPersistentCounterState> source
    )
    {
        List<object> result = new();
        if (source == null)
            return result;
        foreach (EquipmentAbilityPersistentCounterState counter in source)
        {
            if (counter == null)
                continue;
            result.Add(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["counter_id"] = counter.CounterId ?? "",
                    ["value"] = counter.Value,
                }
            );
        }
        return result;
    }

    private static IReadOnlyDictionary<string, object> EffectiveTraitInstanceToPlain(
        BattleEffectiveTraitInstanceReadView entry
    ) =>
        new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["trait_id"] = entry.TraitId.ToString(),
            ["effective_instance_key"] = entry.EffectiveInstanceKey.ToString(),
            ["source_type"] = entry.SourceType.ToString(),
            ["source_id"] = entry.SourceId.ToString(),
            ["effect_type"] = entry.EffectType.ToString(),
            ["trigger_type"] = entry.TriggerType.ToString(),
            ["charge_scope"] = entry.ChargeScope.ToString(),
            ["charge_reset_timing"] = entry.ChargeResetTiming.ToString(),
            ["rank"] = Math.Max(entry.Rank, 1),
            ["stacks"] = Math.Max(entry.Stacks, 1),
            ["roll_values"] = TraitRollValuesToPlain(
                entry.RollValues.CopyNormalized()
            ),
        };

    private static IReadOnlyDictionary<string, object> EquipmentAbilitySourceToPlain(
        BattleEquipmentAbilitySourceReadView entry
    ) =>
        new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["effective_instance_key"] = entry.EffectiveInstanceKey.ToString(),
            ["equipment_def_id"] = entry.EquipmentDefId.ToString(),
            ["source_equipment_instance_id"] = entry.SourceEquipmentInstanceId.ToString(),
            ["source_kind"] = BattleEquipmentAbilitySourceState
                .ToStringName(entry.SourceKind)
                .ToString(),
            ["ability_ids"] = StringNameListToPlain(entry.AbilityIds),
        };

    private static IReadOnlyDictionary<string, object> StringNameIntMapToPlain(
        BattleStringNameIntMap source
    )
    {
        Dictionary<string, object> result = new(StringComparer.Ordinal);
        if (source == null)
            return result;
        foreach (KeyValuePair<StringName, int> entry in source)
            result[entry.Key.ToString()] = entry.Value;
        return result;
    }

    private static IReadOnlyDictionary<string, object> KnownSkillLevelViewToPlain(
        BattleKnownSkillLevelReadView source
    )
    {
        Dictionary<string, object> result = new(StringComparer.Ordinal);
        foreach (KeyValuePair<StringName, int> entry in source)
            result[entry.Key.ToString()] = entry.Value;
        return result;
    }

    private static IReadOnlyDictionary<string, object>
        SaveAbilityBonusViewToPlain(
            BattleSaveAbilityBonusReadView source
        )
    {
        Dictionary<string, object> result = new(StringComparer.Ordinal);
        foreach (KeyValuePair<StringName, int> entry in source)
            result[entry.Key.ToString()] = entry.Value;
        return result;
    }

    private static IReadOnlyDictionary<StringName, int> StringNameIntMapToStringNameKeyPlain(
        BattleStringNameIntMap source
    ) => source?.ToTypedDictionary() ?? new Dictionary<StringName, int>();

    private static IReadOnlyDictionary<string, object>
        DamageResistanceViewToPlain(
        BattleDamageResistanceReadView source
    )
    {
        Dictionary<string, object> result = new(StringComparer.Ordinal);
        foreach (KeyValuePair<StringName, StringName> entry in source)
            result[entry.Key.ToString()] = entry.Value.ToString();
        return result;
    }

    private static EquipmentState EquipmentFromDict(GDictionary payload)
    {
        return EquipmentState.FromDictionary(payload);
    }

    private static AttributeSnapshot NewAttributeSnapshot()
    {
        return new AttributeSnapshot();
    }

    private static EquipmentState NewEquipmentState()
    {
        return new EquipmentState();
    }

    private static List<StringName> SortedStatusEffectIds(GDictionary values)
    {
        List<StringName> result = new();
        if (values == null)
        {
            return result;
        }
        foreach (Variant key in values.Keys)
        {
            if (key.VariantType != Variant.Type.StringName)
            {
                continue;
            }
            StringName statusId = key.AsStringName();
            if (IsEmpty(statusId) || result.Contains(statusId))
            {
                continue;
            }
            result.Add(statusId);
        }
        result.Sort((left, right) => StringComparer.Ordinal.Compare(left.ToString(), right.ToString()));
        return result;
    }
}
