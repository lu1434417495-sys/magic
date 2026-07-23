using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;

internal sealed class SnapshotState
{
    private readonly StableMap _stable;

    private SnapshotState(StableMap stable, bool isEmpty)
    {
        _stable = stable ?? new StableMap();
        IsEmpty = isEmpty;
    }

    internal bool IsEmpty { get; }

    internal static SnapshotState Empty() => new(new StableMap(), true);

    internal static SnapshotState Capture(BattleAiContext context)
    {
        BattleState state = context?.state;
        if (state == null)
        {
            return Empty();
        }

        using (new BattleAiTraceSpan("mutation_guard:capture_before_stable"))
            return new SnapshotState(CaptureStable(context), false);
    }

    private static StableMap CaptureStable(BattleAiContext context)
    {
        BattleState state = context?.state;
        if (state == null)
        {
            return new StableMap();
        }

        StableMap result = new();
        using (new BattleAiTraceSpan("mutation_guard:stable_state_fields"))
        {
            result.Set(
                "state_fields",
                StableValue.FromMap(BattleStateFieldsSnapshot.Capture(state).ToStableMap())
            );
            result.Set(
                "timeline",
                state.timeline == null
                    ? StableValue.Nil()
                    : StableValue.FromMap(
                        BattleAiMutationStableProjection.StableTimeline(state.timeline)
                    )
            );
            result.Set(
                "party_backpack_view",
                state.party_backpack_view == null
                    ? StableValue.Nil()
                    : StableValue.FromMap(
                        BattleAiMutationStableProjection.StableWarehouse(
                            state.party_backpack_view
                        )
                    )
            );
            result.Set(
                "skill_definitions",
                StableValue.FromMap(
                    StableSkillDefinitions(context?.GetSkillDefinitionIndexTyped())
                )
            );
            result.Set(
                "barrier_profile_definitions",
                StableValue.FromMap(
                    StableBarrierProfileDefinitions(
                        context?.GetBarrierProfileDefinitionIndexTyped()
                    )
                )
            );
        }
        using (new BattleAiTraceSpan("mutation_guard:stable_cells"))
            result.Set("cells", StableValue.FromMap(StableLiveCells(state.CellIndex)));
        using (new BattleAiTraceSpan("mutation_guard:stable_cell_columns"))
            result.Set(
                "cell_columns",
                StableValue.FromMap(
                    StableLiveCellColumns(state.ProjectCellColumnsTyped())
                )
            );
        using (new BattleAiTraceSpan("mutation_guard:stable_units"))
            result.Set("units", StableValue.FromMap(StableLiveUnits(state.UnitIndex)));
        return result;
    }

    internal List<string> Validate(
        BattleAiContext context,
        StringName activeUnitId
    )
    {
        StableMap afterStable;
        using (new BattleAiTraceSpan("mutation_guard:capture_after_stable"))
            afterStable = CaptureStable(context);
        StableMap expectedStable = _stable.Clone();
        BattleAiMutationStableProjection.NormalizeAllowedAiBookkeeping(expectedStable, afterStable, activeUnitId);

        List<StableDiff> diffs = new();
        using (new BattleAiTraceSpan("mutation_guard:collect_diffs"))
            BattleAiMutationGuard.CollectDiffs(expectedStable, afterStable, "ai_decision", diffs);
        if (diffs.Count == 0)
            return new List<string>();

        List<string> violations = BattleAiMutationGuard.FormatStableDiffs(diffs);
        if (diffs.Count >= BattleAiMutationGuard.MaxReportedViolations)
        {
            violations.Add(
                $"(report capped at {BattleAiMutationGuard.MaxReportedViolations} violations; additional differences may exist)"
            );
        }
        return violations;
    }

    internal bool MatchesCurrentState(BattleAiContext context)
    {
        return CompareCurrentState(context).Count == 0;
    }

    internal List<string> CompareCurrentState(BattleAiContext context)
    {
        StableMap current = CaptureStable(context);
        List<StableDiff> diffs = new();
        BattleAiMutationGuard.CollectDiffs(_stable, current, "ai_decision", diffs);
        return BattleAiMutationGuard.FormatStableDiffs(diffs);
    }

    private static StableMap StableSkillDefinitions(
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions
    )
    {
        StableMap result = new();
        if (skillDefinitions == null)
        {
            return result;
        }
        foreach (KeyValuePair<StringName, SkillDefinition> entry in skillDefinitions)
        {
            result.Set(
                BattleAiMutationSnapshotModel.StableKey(entry.Key),
                StableSkillDefinition(entry.Value)
            );
        }
        return result;
    }

    private static StableValue StableSkillDefinition(SkillDefinition skillDefinition)
    {
        return StableValue.FromReference(skillDefinition);
    }

    private static StableMap StableBarrierProfileDefinitions(
        IReadOnlyDictionary<StringName, BarrierProfileDefinition> definitions
    )
    {
        StableMap result = new();
        if (definitions == null)
        {
            return result;
        }
        foreach (
            KeyValuePair<StringName, BarrierProfileDefinition> entry in definitions
        )
        {
            result.Set(
                BattleAiMutationSnapshotModel.StableKey(entry.Key),
                StableValue.FromReference(entry.Value)
            );
        }
        return result;
    }

    private static StableMap StableLiveCells(IReadOnlyDictionary<Vector2I, BattleCellState> cells)
    {
        StableMap result = new();
        if (cells == null)
        {
            return result;
        }
        foreach ((Vector2I key, BattleCellState cell) in cells)
        {
            result.Set(
                BattleAiMutationSnapshotModel.StableKey(key),
                cell == null
                    ? StableValue.Nil()
                    : StableValue.FromMap(
                        BattleAiMutationStableProjection.StableBattleCell(cell)
                    )
            );
        }
        return result;
    }

    private static StableMap StableLiveCellColumns(
        IReadOnlyDictionary<Vector2I, List<BattleCellState>> columns
    )
    {
        StableMap result = new();
        if (columns == null)
        {
            return result;
        }
        foreach ((Vector2I key, List<BattleCellState> column) in columns)
        {
            result.Set(
                BattleAiMutationSnapshotModel.StableKey(key),
                StableCellColumn(column)
            );
        }
        return result;
    }

    private static StableValue StableCellColumn(IReadOnlyList<BattleCellState> column)
    {
        if (column == null)
        {
            return StableValue.Nil();
        }

        List<StableValue> values = new();
        foreach (BattleCellState cell in column)
        {
            values.Add(
                cell == null
                    ? StableValue.Nil()
                    : StableValue.FromMap(
                        BattleAiMutationStableProjection.StableBattleCell(cell)
                    )
            );
        }
        return StableValue.FromArray(values);
    }

    private static StableMap StableLiveUnits(IReadOnlyDictionary<StringName, BattleUnitState> units)
    {
        StableMap result = new();
        if (units == null)
        {
            return result;
        }
        foreach ((StringName unitKey, BattleUnitState unit) in units)
        {
            if (unit != null)
            {
                BattleAiMutationStableProjection.MaterializeLazyStatusEffects(unit);
            }
            result.Set(
                BattleAiMutationSnapshotModel.StableKey(unitKey),
                unit == null
                    ? StableValue.Nil()
                    : StableValue.FromMap(BattleUnitSnapshot.Capture(unit).ToStableMap())
            );
        }
        return result;
    }
}

internal sealed class BattleUnitSnapshot
{
    private readonly BattleUnitFieldsSnapshot _fields;
    private readonly Dictionary<StringName, int> _attributeValues;
    private readonly EquipmentState _equipmentView;
    private readonly Dictionary<StringName, BattleStatusEffectState> _statusEffects;

    private BattleUnitSnapshot(
        BattleUnitFieldsSnapshot fields,
        Dictionary<StringName, int> attributeValues,
        EquipmentState equipmentView,
        Dictionary<StringName, BattleStatusEffectState> statusEffects
    )
    {
        _fields = fields ?? BattleUnitFieldsSnapshot.Empty();
        _attributeValues = attributeValues;
        _equipmentView = equipmentView;
        _statusEffects = statusEffects ?? new Dictionary<StringName, BattleStatusEffectState>();
    }

    public static BattleUnitSnapshot Capture(BattleUnitState unit)
    {
        return new BattleUnitSnapshot(
            BattleUnitFieldsSnapshot.Capture(unit),
            CaptureAttributeValues(unit?.attribute_snapshot),
            BattleAiMutationStableProjection.DuplicateEquipmentExact(
                unit?.equipment_view
            ),
            CaptureStatusEffects(unit)
        );
    }

    public StableMap ToStableMap()
    {
        StableMap result = new();
        result.Set("fields", StableValue.FromMap(_fields.ToStableMap()));
        result.Set(
            "attribute_snapshot_values",
            _attributeValues == null
                ? StableValue.Nil()
                : StableValue.FromMap(StableAttributeValues())
        );
        result.Set(
            "equipment_view",
            _equipmentView == null
                ? StableValue.Nil()
                : StableValue.FromMap(
                    BattleAiMutationStableProjection.StableEquipment(_equipmentView)
                )
        );
        result.Set("status_effects", StableValue.FromMap(StableStatusEffects()));
        return result;
    }

    private StableMap StableAttributeValues()
    {
        StableMap result = new();
        if (_attributeValues == null)
        {
            return result;
        }
        foreach (KeyValuePair<StringName, int> entry in _attributeValues)
        {
            result.Set(
                BattleAiMutationSnapshotModel.StableKey(entry.Key),
                StableValue.FromInteger(entry.Value)
            );
        }
        return result;
    }

    private StableMap StableStatusEffects()
    {
        StableMap result = new();
        foreach (KeyValuePair<StringName, BattleStatusEffectState> entry in _statusEffects)
        {
            result.Set(
                BattleAiMutationSnapshotModel.StableKey(entry.Key),
                entry.Value == null
                    ? StableValue.Nil()
                    : StableValue.FromMap(
                        BattleAiMutationStableProjection.StableStatusEffect(entry.Value)
                    )
            );
        }
        return result;
    }

    private static Dictionary<StringName, int> CaptureAttributeValues(
        AttributeSnapshot attributeSnapshot
    )
    {
        if (attributeSnapshot == null)
        {
            return null;
        }

        var results = new Dictionary<StringName, int>();
        foreach (KeyValuePair<StringName, int> entry in attributeSnapshot.GetAllValuesTyped())
        {
            results[entry.Key] = entry.Value;
        }
        return results;
    }

    private static Dictionary<StringName, BattleStatusEffectState> CaptureStatusEffects(
        BattleUnitState unit
    )
    {
        var results = new Dictionary<StringName, BattleStatusEffectState>();
        if (unit == null)
        {
            return results;
        }

        foreach (
            KeyValuePair<StringName, BattleStatusEffectState> entry in
            unit.CaptureStatusEffectsForMutationSnapshotExact()
        )
        {
            results[entry.Key] =
                entry.Value?.DuplicateForMutationSnapshotExact();
        }
        return results;
    }

}

internal sealed class BattleStateFieldsSnapshot
{
    private StringName _battleId = "";
    private long _seed;
    private int _attackRollNonce;
    private StringName _phase = "";
    private Vector2I _mapSize = Vector2I.Zero;
    private Vector2I _worldCoord = Vector2I.Zero;
    private StringName _encounterAnchorId = "";
    private StringName _terrainProfileId = "";
    private List<StringName> _environmentTags = new();
    private int _environmentRevision;
    private int _environmentWorldStep = -1;
    private List<StringName> _attackDisadvantageTags = new();
    private List<StringName> _allyUnitIds = new();
    private List<StringName> _enemyUnitIds = new();
    private StringName _activeUnitId = "";
    private StableValue _objectiveRuntimeStable = StableValue.Nil();
    private BattleFinalDecision _finalDecision;
    private List<string> _logEntries = new();
    private List<PlainPayloadSnapshot> _reportEntries = new();
    private List<PlainPayloadSnapshot> _promotionQueue = new();
    private StringName _modalState = "";
    private LayeredBarrierFieldsSnapshot _layeredBarrierFields = new();
    private List<BattleEquipmentTargetMarkState> _equipmentTargetMarks = new();
    private List<BattleTemporaryEdgeFeatureState> _temporaryEdgeFeatures = new();
    private ulong _nextCastSequence = 1;
    private int _nextTemporaryEdgeFeatureSequence = 1;
    private long _movementGeometryRevision;

    public static BattleStateFieldsSnapshot Capture(BattleState state)
    {
        BattleStateFieldsSnapshot snapshot = new();
        if (state == null)
        {
            return snapshot;
        }

        snapshot._battleId = state.battle_id;
        snapshot._seed = state.seed;
        snapshot._attackRollNonce = state.attack_roll_nonce;
        snapshot._phase = state.phase;
        snapshot._mapSize = state.map_size;
        snapshot._worldCoord = state.world_coord;
        snapshot._encounterAnchorId = state.encounter_anchor_id;
        snapshot._terrainProfileId = state.terrain_profile_id;
        BattleEnvironmentSnapshot environmentSnapshot = state.GetEnvironmentSnapshot();
        snapshot._environmentTags = BattleAiMutationStableProjection.StringNameArrayToList(
            environmentSnapshot.GlobalEnvironmentTags
        );
        snapshot._environmentRevision = environmentSnapshot.Revision;
        snapshot._environmentWorldStep = environmentSnapshot.WorldStep;
        snapshot._attackDisadvantageTags = state.attack_disadvantage_tags == null
            ? null
            : BattleAiMutationStableProjection.StringNameArrayToList(
                state.attack_disadvantage_tags
            );
        snapshot._allyUnitIds = state.ally_unit_ids == null
            ? null
            : BattleAiMutationStableProjection.StringNameArrayToList(state.ally_unit_ids);
        snapshot._enemyUnitIds = state.enemy_unit_ids == null
            ? null
            : BattleAiMutationStableProjection.StringNameArrayToList(state.enemy_unit_ids);
        snapshot._activeUnitId = state.active_unit_id;
        snapshot._objectiveRuntimeStable =
            BattleAiMutationStableProjection.StableObjectiveRuntimeState(
                state.ObjectiveRuntimeState
            );
        snapshot._finalDecision = state.FinalDecision?.DuplicateState();
        snapshot._logEntries = DuplicateNullableTextListExact(state.log_entries);
        snapshot._reportEntries =
            BattleAiMutationStableProjection.PlainPayloadListToSnapshots(
                state.ReportEntriesTyped
            );
        snapshot._promotionQueue =
            BattleAiMutationStableProjection.PlainPayloadListToSnapshots(
                state.PromotionQueueTyped
            );
        snapshot._modalState = state.modal_state;
        snapshot._layeredBarrierFields =
            LayeredBarrierFieldsSnapshot.Capture(
                state.LayeredBarrierStore.SnapshotEntriesForMutationSnapshotExact()
            );
        snapshot._equipmentTargetMarks =
            state.CaptureEquipmentTargetMarksForMutationSnapshotExact();
        snapshot._temporaryEdgeFeatures =
            state.CaptureTemporaryEdgeFeaturesForMutationSnapshotExact();
        snapshot._nextCastSequence = state.CaptureNextCastSequence();
        snapshot._nextTemporaryEdgeFeatureSequence =
            state.CaptureNextTemporaryEdgeFeatureSequence();
        snapshot._movementGeometryRevision =
            state.CaptureMovementGeometryRevisionForMutationSnapshot();
        return snapshot;
    }

    public StableMap ToStableMap()
    {
        StableMap result = new();
        result.Set("battle_id", BattleAiMutationStableProjection.StableNullableStringName(_battleId));
        result.Set("seed", StableValue.FromInteger(_seed));
        result.Set("attack_roll_nonce", StableValue.FromInteger(_attackRollNonce));
        result.Set("phase", BattleAiMutationStableProjection.StableNullableStringName(_phase));
        result.Set("map_size", StableValue.FromVector2I(_mapSize));
        result.Set("world_coord", StableValue.FromVector2I(_worldCoord));
        result.Set("encounter_anchor_id", BattleAiMutationStableProjection.StableNullableStringName(_encounterAnchorId));
        result.Set("terrain_profile_id", BattleAiMutationStableProjection.StableNullableStringName(_terrainProfileId));
        result.Set("environment_revision", StableValue.FromInteger(_environmentRevision));
        result.Set("environment_world_step", StableValue.FromInteger(_environmentWorldStep));
        result.Set("environment_tags", StableValue.FromArray(BattleAiMutationStableProjection.StableStringNameList(_environmentTags)));
        result.Set(
            "attack_disadvantage_tags",
            StableNullableStringNameList(_attackDisadvantageTags)
        );
        result.Set("ally_unit_ids", StableNullableStringNameList(_allyUnitIds));
        result.Set("enemy_unit_ids", StableNullableStringNameList(_enemyUnitIds));
        result.Set("active_unit_id", BattleAiMutationStableProjection.StableNullableStringName(_activeUnitId));
        result.Set("objective_runtime_state", _objectiveRuntimeStable.Clone());
        result.Set(
            "final_decision_objective_mode",
            _finalDecision == null
                ? StableValue.Nil()
                : StableValue.FromInteger((int)_finalDecision.ObjectiveMode)
        );
        result.Set(
            "final_decision_outcome",
            _finalDecision == null
                ? StableValue.Nil()
                : StableValue.FromInteger((int)_finalDecision.Outcome)
        );
        result.Set(
            "final_decision_end_reason",
            _finalDecision == null
                ? StableValue.Nil()
                : StableValue.FromInteger((int)_finalDecision.EndReason)
        );
        result.Set(
            "final_decision_tu",
            StableValue.FromInteger(_finalDecision?.DecisionTu ?? -1)
        );
        result.Set(
            "winner_faction_id",
            BattleAiMutationStableProjection.StableNullableStringName(
                _finalDecision?.WinnerFactionId
            )
        );
        result.Set("log_entries", StableNullableTextListExact(_logEntries));
        result.Set(
            "report_entries",
            StableValue.FromArray(
                BattleAiMutationStableProjection.StablePlainPayloadSnapshotList(
                    _reportEntries
                )
            )
        );
        result.Set(
            "promotion_queue",
            StableValue.FromArray(
                BattleAiMutationStableProjection.StablePlainPayloadSnapshotList(
                    _promotionQueue
                )
            )
        );
        result.Set("modal_state", BattleAiMutationStableProjection.StableNullableStringName(_modalState));
        result.Set("layered_barrier_fields", StableValue.FromMap(_layeredBarrierFields.ToStableMap()));
        result.Set(
            "equipment_target_marks",
            StableValue.FromArray(
                BattleAiMutationStableProjection.StableEquipmentTargetMarks(
                    _equipmentTargetMarks
                )
            )
        );
        result.Set(
            "temporary_edge_features",
            StableValue.FromArray(BattleAiMutationStableProjection.StableTemporaryEdgeFeatureList(_temporaryEdgeFeatures))
        );
        result.Set(
            "next_cast_sequence",
            StableValue.FromText(_nextCastSequence.ToString(CultureInfo.InvariantCulture))
        );
        result.Set(
            "next_temporary_edge_feature_sequence",
            StableValue.FromInteger(_nextTemporaryEdgeFeatureSequence)
        );
        result.Set(
            "movement_geometry_revision",
            StableValue.FromInteger(_movementGeometryRevision)
        );
        return result;
    }

    private static List<string> DuplicateNullableTextListExact(IEnumerable<string> values)
    {
        if (values == null)
        {
            return null;
        }

        List<string> result = new();
        foreach (string value in values)
        {
            result.Add(value);
        }
        return result;
    }

    private static StableValue StableNullableStringNameList(
        IEnumerable<StringName> values
    )
    {
        return values == null
            ? StableValue.Nil()
            : StableValue.FromArray(
                BattleAiMutationStableProjection.StableStringNameList(values)
            );
    }

    private static StableValue StableNullableTextListExact(IEnumerable<string> values)
    {
        if (values == null)
        {
            return StableValue.Nil();
        }

        List<StableValue> result = new();
        foreach (string value in values)
        {
            result.Add(value == null ? StableValue.Nil() : StableValue.FromText(value));
        }
        return StableValue.FromArray(result);
    }
}

internal sealed class BattleUnitFieldsSnapshot
{
    private StringName _unitId = "";
    private StringName _sourceMemberId = "";
    private StringName _enemyTemplateId = "";
    private StringName _encounterActorId = "";
    private string _displayName = "";
    private string _battleSpriteTexturePath = "";
    private StringName _factionId = "";
    private StringName _controlMode = "";
    private StringName _aiBrainId = "";
    private StringName _aiStateId = "";
    private BattleAiBlackboardSnapshot _aiBlackboard = new();
    private Vector2I _coord = Vector2I.Zero;
    private int _bodySize;
    private StringName _bodySizeCategory = "";
    private Vector2I _footprintSize = Vector2I.Zero;
    private List<Vector2I> _occupiedCoords = new();
    private bool _isAlive;
    private bool _equipmentViewInitialized;
    private int _currentHp;
    private int _currentMp;
    private int _currentStamina;
    private int _currentAura;
    private int _currentAp;
    private int _currentMovePoints;
    private List<StringName> _unlockedCombatResourceIds = new();
    private int _staminaRecoveryProgress;
    private bool _isResting;
    private bool _hasTakenActionThisTurn;
    private bool _hasMovedThisTurn;
    private bool _canUseLockedMovePointsThisTurn;
    private int _currentShieldHp;
    private int _shieldMaxHp;
    private int _shieldDuration;
    private StringName _shieldFamily = "";
    private StringName _shieldSourceUnitId = "";
    private StringName _shieldSourceSkillId = "";
    private List<StringName> _consumedContingencySetupIds = new();
    private int _actionProgress;
    private int _actionThreshold;
    private List<StringName> _knownActiveSkillIds = new();
    private StringNameIntMapSnapshot _knownSkillLevelMap = new();
    private StringNameIntMapSnapshot _knownSkillLockHitBonusMap = new();
    private List<StringName> _movementTags = new();
    private List<StringName> _visionTags = new();
    private List<StringName> _proficiencyTags = new();
    private List<StringName> _saveAdvantageTags = new();
    private List<StringName> _saveDisadvantageTags = new();
    private List<StringName> _saveImmunityTags = new();
    private StringNameStringNameMapSnapshot _damageResistances = new();
    private StringNameIntMapSnapshot _saveBonusByAbility = new();
    private List<BattleEffectiveTraitInstanceState> _effectiveTraitInstances = new();
    private List<StringName> _effectiveTraitIds = new();
    private List<BattleEquipmentAbilitySourceState> _equipmentAbilitySources = new();
    private List<BattleTemporalProgressModifierState> _temporalProgressModifiers = new();
    private List<StringName> _creatureTypeTags = new();
    private StringName _versatilityPick = "";
    private StringName _weaponProfileKind = "";
    private StringName _weaponItemId = "";
    private StringName _weaponProfileTypeId = "";
    private StringName _weaponRangeType = "";
    private StringName _weaponFamily = "";
    private StringName _weaponCurrentGrip = "";
    private int _weaponAttackRange;
    private WeaponDiceSnapshot _weaponOneHandedDice = new();
    private WeaponDiceSnapshot _weaponTwoHandedDice = new();
    private bool _weaponIsVersatile;
    private bool _weaponUsesTwoHands;
    private StringName _weaponPhysicalDamageTag = "";
    private StringNameIntMapSnapshot _cooldowns = new();
    private int _lastTurnTu;
    private StringNameIntMapSnapshot _perBattleCharges = new();
    private StringNameIntMapSnapshot _perTurnCharges = new();
    private StringNameIntMapSnapshot _perTurnChargeLimits = new();
    private StringNameIntMapSnapshot _fumbleProtectionUsed = new();
    private bool _deathWardConsumedThisBattle;
    private BattlePendingCastState _pendingCast;
    private bool _turnCastingExhausted;
    private int _actionProgressRateRemainder;
    private int _castProgressRateRemainder;

    public static BattleUnitFieldsSnapshot Empty() => new();

    public static BattleUnitFieldsSnapshot Capture(BattleUnitState unit)
    {
        BattleUnitFieldsSnapshot snapshot = new();
        if (unit == null)
        {
            return snapshot;
        }

        snapshot._unitId = unit.unit_id;
        snapshot._sourceMemberId = unit.source_member_id;
        snapshot._enemyTemplateId = unit.enemy_template_id;
        snapshot._encounterActorId = unit.encounter_actor_id;
        snapshot._displayName = unit.display_name;
        snapshot._battleSpriteTexturePath = unit.battle_sprite_texture_path;
        snapshot._factionId = unit.faction_id;
        snapshot._controlMode = unit.control_mode;
        snapshot._aiBrainId = unit.ai_brain_id;
        snapshot._aiStateId = unit.ai_state_id;
        snapshot._aiBlackboard = unit.ai_blackboard == null
            ? null
            : BattleAiBlackboardSnapshot.Capture(unit.ai_blackboard);
        snapshot._coord = unit.coord;
        snapshot._bodySize = unit.body_size;
        snapshot._bodySizeCategory = unit.body_size_category;
        snapshot._footprintSize = unit.footprint_size;
        snapshot._occupiedCoords = unit.occupied_coords == null
            ? null
            : BattleAiMutationStableProjection.Vector2IArrayToList(unit.occupied_coords);
        snapshot._isAlive = unit.is_alive;
        snapshot._equipmentViewInitialized = unit.equipment_view_initialized;
        snapshot._currentHp = unit.current_hp;
        snapshot._currentMp = unit.current_mp;
        snapshot._currentStamina = unit.current_stamina;
        snapshot._currentAura = unit.current_aura;
        snapshot._currentAp = unit.current_ap;
        snapshot._currentMovePoints = unit.current_move_points;
        snapshot._unlockedCombatResourceIds = unit.unlocked_combat_resource_ids == null
            ? null
            : BattleAiMutationStableProjection.StringNameArrayToList(
                unit.unlocked_combat_resource_ids
            );
        snapshot._staminaRecoveryProgress = unit.stamina_recovery_progress;
        snapshot._isResting = unit.is_resting;
        snapshot._hasTakenActionThisTurn = unit.has_taken_action_this_turn;
        snapshot._hasMovedThisTurn = unit.has_moved_this_turn;
        snapshot._canUseLockedMovePointsThisTurn = unit.can_use_locked_move_points_this_turn;
        snapshot._currentShieldHp = unit.current_shield_hp;
        snapshot._shieldMaxHp = unit.shield_max_hp;
        snapshot._shieldDuration = unit.shield_duration;
        snapshot._shieldFamily = unit.shield_family;
        snapshot._shieldSourceUnitId = unit.shield_source_unit_id;
        snapshot._shieldSourceSkillId = unit.shield_source_skill_id;
        snapshot._consumedContingencySetupIds =
            BattleAiMutationStableProjection.StringNameArrayToList(
                unit.GetConsumedContingencySetupIdsTyped()
            );
        snapshot._actionProgress = unit.action_progress;
        snapshot._actionThreshold = unit.action_threshold;
        snapshot._knownActiveSkillIds = unit.known_active_skill_ids == null
            ? null
            : BattleAiMutationStableProjection.StringNameArrayToList(
                unit.known_active_skill_ids
            );
        snapshot._knownSkillLevelMap = StringNameIntMapSnapshot.FromTypedMap(
            unit.known_skill_level_map
        );
        snapshot._knownSkillLockHitBonusMap =
            StringNameIntMapSnapshot.FromTypedMap(unit.known_skill_lock_hit_bonus_map);
        snapshot._movementTags = DuplicateNullableStringNameList(unit.movement_tags);
        snapshot._visionTags = DuplicateNullableStringNameList(unit.vision_tags);
        snapshot._proficiencyTags = DuplicateNullableStringNameList(unit.proficiency_tags);
        snapshot._saveAdvantageTags = DuplicateNullableStringNameList(unit.save_advantage_tags);
        snapshot._saveDisadvantageTags = DuplicateNullableStringNameList(
            unit.save_disadvantage_tags
        );
        snapshot._saveImmunityTags = DuplicateNullableStringNameList(unit.save_immunity_tags);
        snapshot._damageResistances =
            StringNameStringNameMapSnapshot.FromTypedMap(unit.damage_resistances);
        snapshot._saveBonusByAbility = StringNameIntMapSnapshot.FromTypedMap(
            unit.save_bonus_by_ability
        );
        snapshot._effectiveTraitInstances =
            BattleAiMutationStableProjection.DuplicateEffectiveTraitInstancesExact(
                unit.effective_trait_instances
            );
        snapshot._effectiveTraitIds = DuplicateNullableStringNameList(
            unit.effective_trait_ids
        );
        snapshot._equipmentAbilitySources =
            BattleAiMutationStableProjection.DuplicateEquipmentAbilitySourcesExact(
                unit.equipment_ability_sources
            );
        snapshot._temporalProgressModifiers =
            BattleAiMutationStableProjection.DuplicateTemporalProgressModifiersExact(
                unit.temporal_progress_modifiers
            );
        snapshot._creatureTypeTags = unit.creature_type_tags == null
            ? null
            : BattleAiMutationStableProjection.StringNameArrayToList(
                unit.creature_type_tags
            );
        snapshot._versatilityPick = unit.versatility_pick;
        snapshot._weaponProfileKind = unit.weapon_profile_kind;
        snapshot._weaponItemId = unit.weapon_item_id;
        snapshot._weaponProfileTypeId = unit.weapon_profile_type_id;
        snapshot._weaponRangeType = unit.weapon_range_type;
        snapshot._weaponFamily = unit.weapon_family;
        snapshot._weaponCurrentGrip = unit.weapon_current_grip;
        snapshot._weaponAttackRange = unit.weapon_attack_range;
        snapshot._weaponOneHandedDice = WeaponDiceSnapshot.FromTyped(
            unit.weapon_one_handed_dice
        );
        snapshot._weaponTwoHandedDice = WeaponDiceSnapshot.FromTyped(
            unit.weapon_two_handed_dice
        );
        snapshot._weaponIsVersatile = unit.weapon_is_versatile;
        snapshot._weaponUsesTwoHands = unit.weapon_uses_two_hands;
        snapshot._weaponPhysicalDamageTag = unit.weapon_physical_damage_tag;
        snapshot._cooldowns = StringNameIntMapSnapshot.FromTypedMap(unit.cooldowns);
        snapshot._lastTurnTu = unit.last_turn_tu;
        snapshot._perBattleCharges = StringNameIntMapSnapshot.FromTypedMap(
            unit.per_battle_charges
        );
        snapshot._perTurnCharges = StringNameIntMapSnapshot.FromTypedMap(
            unit.per_turn_charges
        );
        snapshot._perTurnChargeLimits =
            StringNameIntMapSnapshot.FromTypedMap(unit.per_turn_charge_limits);
        snapshot._fumbleProtectionUsed =
            StringNameIntMapSnapshot.FromTypedMap(unit.fumble_protection_used);
        snapshot._deathWardConsumedThisBattle = unit.death_ward_consumed_this_battle;
        snapshot._pendingCast =
            BattleAiMutationStableProjection.DuplicatePendingCastExact(
                unit.pending_cast
            );
        snapshot._turnCastingExhausted = unit.turn_casting_exhausted;
        snapshot._actionProgressRateRemainder = unit.action_progress_rate_remainder;
        snapshot._castProgressRateRemainder = unit.cast_progress_rate_remainder;
        return snapshot;
    }

    public StableMap ToStableMap()
    {
        StableMap result = new();
        result.Set("unit_id", BattleAiMutationStableProjection.StableNullableStringName(_unitId));
        result.Set("source_member_id", BattleAiMutationStableProjection.StableNullableStringName(_sourceMemberId));
        result.Set("enemy_template_id", BattleAiMutationStableProjection.StableNullableStringName(_enemyTemplateId));
        result.Set("encounter_actor_id", BattleAiMutationStableProjection.StableNullableStringName(_encounterActorId));
        result.Set(
            "display_name",
            _displayName == null
                ? StableValue.Nil()
                : StableValue.FromText(_displayName)
        );
        result.Set(
            "battle_sprite_texture_path",
            _battleSpriteTexturePath == null
                ? StableValue.Nil()
                : StableValue.FromText(_battleSpriteTexturePath)
        );
        result.Set("faction_id", BattleAiMutationStableProjection.StableNullableStringName(_factionId));
        result.Set("control_mode", BattleAiMutationStableProjection.StableNullableStringName(_controlMode));
        result.Set("ai_brain_id", BattleAiMutationStableProjection.StableNullableStringName(_aiBrainId));
        result.Set("ai_state_id", BattleAiMutationStableProjection.StableNullableStringName(_aiStateId));
        result.Set(
            "ai_blackboard",
            _aiBlackboard == null
                ? StableValue.Nil()
                : StableValue.FromMap(_aiBlackboard.ToStableMap())
        );
        result.Set("coord", StableValue.FromVector2I(_coord));
        result.Set("body_size", StableValue.FromInteger(_bodySize));
        result.Set("body_size_category", BattleAiMutationStableProjection.StableNullableStringName(_bodySizeCategory));
        result.Set("footprint_size", StableValue.FromVector2I(_footprintSize));
        result.Set(
            "occupied_coords",
            _occupiedCoords == null
                ? StableValue.Nil()
                : StableValue.FromArray(
                    BattleAiMutationStableProjection.StableVector2IList(
                        _occupiedCoords
                    )
                )
        );
        result.Set("is_alive", StableValue.FromBool(_isAlive));
        result.Set(
            "equipment_view_initialized",
            StableValue.FromBool(_equipmentViewInitialized)
        );
        result.Set("current_hp", StableValue.FromInteger(_currentHp));
        result.Set("current_mp", StableValue.FromInteger(_currentMp));
        result.Set("current_stamina", StableValue.FromInteger(_currentStamina));
        result.Set("current_aura", StableValue.FromInteger(_currentAura));
        result.Set("current_ap", StableValue.FromInteger(_currentAp));
        result.Set("current_move_points", StableValue.FromInteger(_currentMovePoints));
        result.Set(
            "unlocked_combat_resource_ids",
            StableNullableStringNameList(_unlockedCombatResourceIds)
        );
        result.Set("stamina_recovery_progress", StableValue.FromInteger(_staminaRecoveryProgress));
        result.Set("is_resting", StableValue.FromBool(_isResting));
        result.Set("has_taken_action_this_turn", StableValue.FromBool(_hasTakenActionThisTurn));
        result.Set("has_moved_this_turn", StableValue.FromBool(_hasMovedThisTurn));
        result.Set(
            "can_use_locked_move_points_this_turn",
            StableValue.FromBool(_canUseLockedMovePointsThisTurn)
        );
        result.Set("current_shield_hp", StableValue.FromInteger(_currentShieldHp));
        result.Set("shield_max_hp", StableValue.FromInteger(_shieldMaxHp));
        result.Set("shield_duration", StableValue.FromInteger(_shieldDuration));
        result.Set("shield_family", BattleAiMutationStableProjection.StableNullableStringName(_shieldFamily));
        result.Set("shield_source_unit_id", BattleAiMutationStableProjection.StableNullableStringName(_shieldSourceUnitId));
        result.Set("shield_source_skill_id", BattleAiMutationStableProjection.StableNullableStringName(_shieldSourceSkillId));
        result.Set(
            "consumed_contingency_setup_ids",
            StableValue.FromArray(
                BattleAiMutationStableProjection.StableStringNameList(
                    _consumedContingencySetupIds
                )
            )
        );
        result.Set("action_progress", StableValue.FromInteger(_actionProgress));
        result.Set("action_threshold", StableValue.FromInteger(_actionThreshold));
        result.Set(
            "known_active_skill_ids",
            StableNullableStringNameList(_knownActiveSkillIds)
        );
        result.Set("known_skill_level_map", _knownSkillLevelMap.ToStableValue());
        result.Set(
            "known_skill_lock_hit_bonus_map",
            _knownSkillLockHitBonusMap.ToStableValue()
        );
        result.Set("movement_tags", StableNullableStringNameList(_movementTags));
        result.Set("vision_tags", StableNullableStringNameList(_visionTags));
        result.Set("proficiency_tags", StableNullableStringNameList(_proficiencyTags));
        result.Set("save_advantage_tags", StableNullableStringNameList(_saveAdvantageTags));
        result.Set(
            "save_disadvantage_tags",
            StableNullableStringNameList(_saveDisadvantageTags)
        );
        result.Set("save_immunity_tags", StableNullableStringNameList(_saveImmunityTags));
        result.Set("damage_resistances", _damageResistances.ToStableValue());
        result.Set("save_bonus_by_ability", _saveBonusByAbility.ToStableValue());
        result.Set(
            "effective_trait_instances",
            _effectiveTraitInstances == null
                ? StableValue.Nil()
                : StableValue.FromArray(
                    BattleAiMutationStableProjection.StableEffectiveTraitPayload(
                        _effectiveTraitInstances
                    )
                )
        );
        result.Set(
            "effective_trait_ids",
            StableNullableStringNameList(_effectiveTraitIds)
        );
        result.Set(
            "equipment_ability_sources",
            _equipmentAbilitySources == null
                ? StableValue.Nil()
                : StableValue.FromArray(
                    BattleAiMutationStableProjection.StableEquipmentAbilitySources(
                        _equipmentAbilitySources
                    )
                )
        );
        result.Set(
            "temporal_progress_modifiers",
            _temporalProgressModifiers == null
                ? StableValue.Nil()
                : StableValue.FromArray(
                    BattleAiMutationStableProjection.StableTemporalProgressModifiers(
                        _temporalProgressModifiers
                    )
                )
        );
        result.Set(
            "creature_type_tags",
            _creatureTypeTags == null
                ? StableValue.Nil()
                : StableValue.FromArray(
                    BattleAiMutationStableProjection.StableStringNameList(
                        _creatureTypeTags
                    )
                )
        );
        result.Set("versatility_pick", BattleAiMutationStableProjection.StableNullableStringName(_versatilityPick));
        result.Set("weapon_profile_kind", BattleAiMutationStableProjection.StableNullableStringName(_weaponProfileKind));
        result.Set("weapon_item_id", BattleAiMutationStableProjection.StableNullableStringName(_weaponItemId));
        result.Set("weapon_profile_type_id", BattleAiMutationStableProjection.StableNullableStringName(_weaponProfileTypeId));
        result.Set("weapon_range_type", BattleAiMutationStableProjection.StableNullableStringName(_weaponRangeType));
        result.Set("weapon_family", BattleAiMutationStableProjection.StableNullableStringName(_weaponFamily));
        result.Set("weapon_current_grip", BattleAiMutationStableProjection.StableNullableStringName(_weaponCurrentGrip));
        result.Set("weapon_attack_range", StableValue.FromInteger(_weaponAttackRange));
        result.Set("weapon_one_handed_dice", _weaponOneHandedDice.ToStableValue());
        result.Set("weapon_two_handed_dice", _weaponTwoHandedDice.ToStableValue());
        result.Set("weapon_is_versatile", StableValue.FromBool(_weaponIsVersatile));
        result.Set("weapon_uses_two_hands", StableValue.FromBool(_weaponUsesTwoHands));
        result.Set("weapon_physical_damage_tag", BattleAiMutationStableProjection.StableNullableStringName(_weaponPhysicalDamageTag));
        result.Set("cooldowns", _cooldowns.ToStableValue());
        result.Set("last_turn_tu", StableValue.FromInteger(_lastTurnTu));
        result.Set("per_battle_charges", _perBattleCharges.ToStableValue());
        result.Set("per_turn_charges", _perTurnCharges.ToStableValue());
        result.Set("per_turn_charge_limits", _perTurnChargeLimits.ToStableValue());
        result.Set("fumble_protection_used", _fumbleProtectionUsed.ToStableValue());
        result.Set(
            "death_ward_consumed_this_battle",
            StableValue.FromBool(_deathWardConsumedThisBattle)
        );
        result.Set(
            "pending_cast",
            _pendingCast == null
                ? StableValue.Nil()
                : StableValue.FromMap(
                    BattleAiMutationStableProjection.StablePendingCast(_pendingCast)
                )
        );
        result.Set("turn_casting_exhausted", StableValue.FromBool(_turnCastingExhausted));
        result.Set(
            "action_progress_rate_remainder",
            StableValue.FromInteger(_actionProgressRateRemainder)
        );
        result.Set(
            "cast_progress_rate_remainder",
            StableValue.FromInteger(_castProgressRateRemainder)
        );
        return result;
    }

    private static List<StringName> DuplicateNullableStringNameList(
        IEnumerable<StringName> values
    )
    {
        return values == null
            ? null
            : BattleAiMutationStableProjection.StringNameArrayToList(values);
    }

    private static StableValue StableNullableStringNameList(
        IEnumerable<StringName> values
    )
    {
        return values == null
            ? StableValue.Nil()
            : StableValue.FromArray(
                BattleAiMutationStableProjection.StableStringNameList(values)
            );
    }
}

internal sealed class PlainPayloadSnapshot
{
    private readonly StableMap _values = new();

    public static PlainPayloadSnapshot Capture(
        IReadOnlyDictionary<string, object> source
    )
    {
        PlainPayloadSnapshot result = new();
        if (source == null)
            return result;
        foreach (KeyValuePair<string, object> entry in source)
        {
            if (entry.Key == null)
                continue;
            result._values.Set(
                entry.Key,
                BattleAiMutationSnapshotModel.ReadStableTypedValue(entry.Value)
            );
        }
        return result;
    }

    public StableMap ToStableMap() => _values.Clone();
}

internal sealed class BattleAiBlackboardSnapshot
{
    private StringName _lastBrainId = "";
    private StringName _lastStateId = "";
    private StringName _lastActionId = "";
    private StringName _lastReasonText = "";
    private StringName _lastTransitionPreviousStateId = "";
    private StringName _lastTransitionStateId = "";
    private StringName _lastTransitionRuleId = "";
    private StringName _lastTransitionReason = "";
    private int _turnStartedTu;
    private int _turnDecisionCount;
    private bool _hasTurnStartedTu;
    private bool _hasTurnDecisionCount;
    private bool _madnessAiControl;
    private bool _madnessTargetAnyTeam;
    private bool _lowLuckReverseFateUsed;
    private bool _lowLuckBlackStarWedgeUsed;
    private bool _meteorProtectedAlly;
    private bool _protectedAlly;
    private bool _summoned;
    private bool _temporaryUnit;
    private StringName _summonSourceUnitId = "";
    private StringName _summonSourceEquipmentInstanceId = "";
    private StringName _summonBindingId = "";
    private StringName _summonStateKey = "";
    private int _summonExpiresAtTu = -1;

    public static BattleAiBlackboardSnapshot Capture(BattleAiBlackboard blackboard)
    {
        BattleAiBlackboardSnapshot snapshot = new();
        if (blackboard == null)
        {
            return snapshot;
        }
        snapshot._lastBrainId = blackboard.last_brain_id;
        snapshot._lastStateId = blackboard.last_state_id;
        snapshot._lastActionId = blackboard.last_action_id;
        snapshot._lastReasonText = blackboard.last_reason_text;
        snapshot._lastTransitionPreviousStateId =
            blackboard.last_transition_previous_state_id;
        snapshot._lastTransitionStateId = blackboard.last_transition_state_id;
        snapshot._lastTransitionRuleId = blackboard.last_transition_rule_id;
        snapshot._lastTransitionReason = blackboard.last_transition_reason;
        snapshot._hasTurnStartedTu = blackboard.ContainsKey("turn_started_tu");
        snapshot._turnStartedTu = blackboard.turn_started_tu;
        snapshot._hasTurnDecisionCount = blackboard.ContainsKey("turn_decision_count");
        snapshot._turnDecisionCount = blackboard.turn_decision_count;
        snapshot._madnessAiControl = blackboard.madness_ai_control;
        snapshot._madnessTargetAnyTeam = blackboard.madness_target_any_team;
        snapshot._lowLuckReverseFateUsed = blackboard.low_luck_reverse_fate_used;
        snapshot._lowLuckBlackStarWedgeUsed = blackboard.low_luck_black_star_wedge_used;
        snapshot._meteorProtectedAlly = blackboard.meteor_protected_ally;
        snapshot._protectedAlly = blackboard.protected_ally;
        snapshot._summoned = blackboard.summoned;
        snapshot._temporaryUnit = blackboard.temporary_unit;
        snapshot._summonSourceUnitId = blackboard.summon_source_unit_id;
        snapshot._summonSourceEquipmentInstanceId =
            blackboard.summon_source_equipment_instance_id;
        snapshot._summonBindingId = blackboard.summon_binding_id;
        snapshot._summonStateKey = blackboard.summon_state_key;
        snapshot._summonExpiresAtTu = blackboard.summon_expires_at_tu;
        return snapshot;
    }

    public StableMap ToStableMap()
    {
        StableMap result = new();
        result.Set("last_brain_id", BattleAiMutationStableProjection.StableNullableStringName(_lastBrainId));
        result.Set("last_state_id", BattleAiMutationStableProjection.StableNullableStringName(_lastStateId));
        result.Set("last_action_id", BattleAiMutationStableProjection.StableNullableStringName(_lastActionId));
        result.Set("last_reason_text", BattleAiMutationStableProjection.StableNullableStringName(_lastReasonText));
        result.Set(
            "last_transition_previous_state_id",
            BattleAiMutationStableProjection.StableNullableStringName(_lastTransitionPreviousStateId)
        );
        result.Set(
            "last_transition_state_id",
            BattleAiMutationStableProjection.StableNullableStringName(_lastTransitionStateId)
        );
        result.Set(
            "last_transition_rule_id",
            BattleAiMutationStableProjection.StableNullableStringName(_lastTransitionRuleId)
        );
        result.Set(
            "last_transition_reason",
            BattleAiMutationStableProjection.StableNullableStringName(_lastTransitionReason)
        );
        result.Set("has_turn_started_tu", StableValue.FromBool(_hasTurnStartedTu));
        result.Set("turn_started_tu", StableValue.FromInteger(_turnStartedTu));
        result.Set("has_turn_decision_count", StableValue.FromBool(_hasTurnDecisionCount));
        result.Set("turn_decision_count", StableValue.FromInteger(_turnDecisionCount));
        result.Set("madness_ai_control", StableValue.FromBool(_madnessAiControl));
        result.Set("madness_target_any_team", StableValue.FromBool(_madnessTargetAnyTeam));
        result.Set(
            "low_luck_reverse_fate_used",
            StableValue.FromBool(_lowLuckReverseFateUsed)
        );
        result.Set(
            "low_luck_black_star_wedge_used",
            StableValue.FromBool(_lowLuckBlackStarWedgeUsed)
        );
        result.Set("meteor_protected_ally", StableValue.FromBool(_meteorProtectedAlly));
        result.Set("protected_ally", StableValue.FromBool(_protectedAlly));
        result.Set("summoned", StableValue.FromBool(_summoned));
        result.Set("temporary_unit", StableValue.FromBool(_temporaryUnit));
        result.Set(
            "summon_source_unit_id",
            BattleAiMutationStableProjection.StableNullableStringName(_summonSourceUnitId)
        );
        result.Set(
            "summon_source_equipment_instance_id",
            BattleAiMutationStableProjection.StableNullableStringName(_summonSourceEquipmentInstanceId)
        );
        result.Set("summon_binding_id", BattleAiMutationStableProjection.StableNullableStringName(_summonBindingId));
        result.Set("summon_state_key", BattleAiMutationStableProjection.StableNullableStringName(_summonStateKey));
        result.Set("summon_expires_at_tu", StableValue.FromInteger(_summonExpiresAtTu));
        return result;
    }
}

internal sealed class StringNameIntMapSnapshot
{
    private bool _isPresent;
    private readonly Dictionary<StringName, int> _values = new();

    public static StringNameIntMapSnapshot FromTypedMap(BattleStringNameIntMap source)
    {
        StringNameIntMapSnapshot result = new();
        if (source == null)
        {
            return result;
        }

        result._isPresent = true;
        foreach (KeyValuePair<StringName, int> entry in source.ToTypedDictionary())
        {
            if (entry.Key == "")
            {
                continue;
            }

            result._values[entry.Key] = entry.Value;
        }

        return result;
    }

    public StableValue ToStableValue()
    {
        return _isPresent
            ? StableValue.FromMap(ToStableMap())
            : StableValue.Nil();
    }

    public StableMap ToStableMap()
    {
        StableMap result = new();
        foreach (KeyValuePair<StringName, int> entry in _values)
        {
            result.Set(
                BattleAiMutationSnapshotModel.StableKey(entry.Key),
                StableValue.FromInteger(entry.Value)
            );
        }
        return result;
    }
}

internal sealed class StringNameStringNameMapSnapshot
{
    private bool _isPresent;
    private readonly Dictionary<StringName, StringName> _values = new();

    public static StringNameStringNameMapSnapshot FromTypedMap(BattleStringNameMap source)
    {
        StringNameStringNameMapSnapshot result = new();
        if (source == null)
        {
            return result;
        }

        result._isPresent = true;
        foreach (KeyValuePair<StringName, StringName> entry in source.ToTypedDictionary())
        {
            if (entry.Key == "" || entry.Value == "")
            {
                continue;
            }

            result._values[entry.Key] = entry.Value;
        }

        return result;
    }

    public StableValue ToStableValue()
    {
        return _isPresent
            ? StableValue.FromMap(ToStableMap())
            : StableValue.Nil();
    }

    public StableMap ToStableMap()
    {
        StableMap result = new();
        foreach (KeyValuePair<StringName, StringName> entry in _values)
        {
            result.Set(
                BattleAiMutationSnapshotModel.StableKey(entry.Key),
                BattleAiMutationStableProjection.StableNullableStringName(entry.Value)
            );
        }
        return result;
    }
}

internal sealed class WeaponDiceSnapshot
{
    private readonly WeaponDice _typedDice;

    public WeaponDiceSnapshot(WeaponDice typedDice = null)
    {
        _typedDice = typedDice;
    }

    public static WeaponDiceSnapshot FromTyped(WeaponDice typedDice)
    {
        if (typedDice == null)
        {
            return new WeaponDiceSnapshot();
        }

        return new WeaponDiceSnapshot(typedDice.DuplicateState());
    }

    public StableMap ToStableMap()
    {
        return _typedDice == null
            ? new StableMap()
            : BattleAiMutationStableProjection.StableWeaponDice(_typedDice);
    }

    public StableValue ToStableValue()
    {
        return _typedDice == null
            ? StableValue.Nil()
            : StableValue.FromMap(ToStableMap());
    }
}
