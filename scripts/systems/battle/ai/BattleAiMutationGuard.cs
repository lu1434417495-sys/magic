using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleAiMutationGuard : RefCounted
{
    private const int MaxReportedViolations = 64;

    private static readonly HashSet<string> AllowedActiveUnitFields =
        new(StringComparer.Ordinal) { "ai_brain_id", "ai_state_id" };

    private static readonly HashSet<string> AllowedActiveBlackboardKeys =
        new(StringComparer.Ordinal)
        {
            "last_brain_id",
            "last_state_id",
            "last_action_id",
            "last_reason_text",
            "last_transition_previous_state_id",
            "last_transition_state_id",
            "last_transition_rule_id",
            "last_transition_reason",
            "turn_decision_count",
        };

    private BattleAiMutationSnapshot _before_snapshot = BattleAiMutationSnapshot.Empty();
    private StringName _active_unit_id = "";

    public bool capture(BattleAiContext context)
    {
        if (!TryGetContextState(context, out _, out BattleUnitState unitState))
        {
            return false;
        }

        _active_unit_id = unitState.unit_id;
        _before_snapshot = BattleAiMutationSnapshot.Capture(context);
        return true;
    }

    public GArray validate_and_restore(BattleAiContext context)
    {
        return ToViolationArray(ValidateAndRestoreTyped(context));
    }

    internal List<string> ValidateAndRestoreTyped(BattleAiContext context)
    {
        if (_before_snapshot.IsEmpty || !TryGetContextState(context, out _, out _))
        {
            return new List<string>();
        }

        BattleAiMutationSnapshot afterSnapshot = BattleAiMutationSnapshot.Capture(context);
        StableMap afterStable = afterSnapshot.Stable;
        StableMap expectedStable = _before_snapshot.Stable.Clone();
        ApplyAllowedAiBookkeeping(expectedStable, afterStable);

        List<StableDiff> diffs = new();
        CollectDiffs(expectedStable, afterStable, "ai_decision", diffs);
        if (diffs.Count == 0)
        {
            return new List<string>();
        }
        List<string> violations = FormatStableDiffs(diffs);
        if (diffs.Count >= MaxReportedViolations)
        {
            violations.Add(
                $"(report capped at {MaxReportedViolations} violations; additional differences may exist)"
            );
        }

        _before_snapshot.Restore(context);
        return violations;
    }

    private sealed class BattleAiMutationSnapshot
    {
        private readonly BattleStateFieldsSnapshot _stateFields;
        private readonly BattleTimelineState _timeline;
        private readonly WarehouseState _partyBackpackView;
        private readonly Dictionary<Vector2I, BattleCellState> _cells;
        private readonly Dictionary<Vector2I, List<BattleCellState>> _cellColumns;
        private readonly Dictionary<StringName, BattleUnitSnapshot> _units;
        private readonly Dictionary<StringName, SkillDef> _skillDefs;

        private BattleAiMutationSnapshot(
            BattleStateFieldsSnapshot stateFields,
            BattleTimelineState timeline,
            WarehouseState partyBackpackView,
            Dictionary<Vector2I, BattleCellState> cells,
            Dictionary<Vector2I, List<BattleCellState>> cellColumns,
            Dictionary<StringName, BattleUnitSnapshot> units,
            Dictionary<StringName, SkillDef> skillDefs,
            StableMap stable,
            bool isEmpty
        )
        {
            _stateFields = stateFields ?? BattleStateFieldsSnapshot.Empty();
            _timeline = timeline;
            _partyBackpackView = partyBackpackView;
            _cells = cells ?? new Dictionary<Vector2I, BattleCellState>();
            _cellColumns = cellColumns ?? new Dictionary<Vector2I, List<BattleCellState>>();
            _units = units ?? new Dictionary<StringName, BattleUnitSnapshot>();
            _skillDefs = skillDefs ?? new Dictionary<StringName, SkillDef>();
            Stable = stable ?? new StableMap();
            IsEmpty = isEmpty;
        }

        public bool IsEmpty { get; }

        public StableMap Stable { get; }

        public static BattleAiMutationSnapshot Empty()
        {
            return new BattleAiMutationSnapshot(
                BattleStateFieldsSnapshot.Empty(),
                null,
                null,
                new Dictionary<Vector2I, BattleCellState>(),
                new Dictionary<Vector2I, List<BattleCellState>>(),
                new Dictionary<StringName, BattleUnitSnapshot>(),
                new Dictionary<StringName, SkillDef>(),
                new StableMap(),
                true
            );
        }

        public static BattleAiMutationSnapshot Capture(BattleAiContext context)
        {
            BattleState state = context?.state;
            if (state == null)
            {
                return Empty();
            }

            Dictionary<StringName, BattleUnitSnapshot> units = CaptureUnits(state.units);
            var snapshot = new BattleAiMutationSnapshot(
                BattleStateFieldsSnapshot.Capture(state),
                CloneTimeline(state.timeline),
                state.party_backpack_view?.duplicate_state(),
                CaptureCells(state.cells),
                CaptureCellColumns(state.cell_columns),
                units,
                CaptureSkillDefs(context?.skill_defs),
                null,
                false
            );
            snapshot.Stable.CopyFrom(snapshot.BuildStableMap());
            return snapshot;
        }

        public void Restore(BattleAiContext context)
        {
            BattleState state = context?.state;
            if (state == null)
            {
                return;
            }

            _stateFields.Restore(state);
            state.timeline = CloneTimeline(_timeline);
            state.party_backpack_view = _partyBackpackView?.duplicate_state();
            state.cells = BuildCellDictionary(_cells);
            state.cell_columns = BuildCellColumnDictionary(_cellColumns);
            state.units = RestoreUnits(_units);
            context.skill_defs = BuildSkillDefDictionary(_skillDefs);
        }

        private StableMap BuildStableMap()
        {
            StableMap result = new();
            result.Set("state_fields", StableValue.FromMap(_stateFields.ToStableMap()));
            result.Set("timeline", StableValue.FromMap(StableTimeline(_timeline)));
            result.Set("party_backpack_view", StableValue.FromMap(StableWarehouse(_partyBackpackView)));
            result.Set("cells", StableValue.FromMap(StableCells(_cells)));
            result.Set("cell_columns", StableValue.FromMap(StableCellColumns(_cellColumns)));
            result.Set("units", StableValue.FromMap(StableUnits(_units)));
            result.Set("skill_defs", StableValue.FromMap(StableSkillDefs(_skillDefs)));
            return result;
        }

        private static Dictionary<Vector2I, BattleCellState> CaptureCells(GDictionary cells)
        {
            var results = new Dictionary<Vector2I, BattleCellState>();
            if (cells == null)
            {
                return results;
            }
            foreach (
                GodotDictionaryEntry<Vector2I, BattleCellState> entry in ReadGodotDictionaryEntries<
                    Vector2I,
                    BattleCellState
                >(cells, TryAsVector2I, TryAsBattleCellState)
            )
            {
                results[entry.Key] = entry.Value.duplicate_cell();
            }
            return results;
        }

        private static Dictionary<Vector2I, List<BattleCellState>> CaptureCellColumns(
            GDictionary columns
        )
        {
            var results = new Dictionary<Vector2I, List<BattleCellState>>();
            if (columns == null)
            {
                return results;
            }
            foreach (
                GodotDictionaryEntry<Vector2I, List<BattleCellState>> entry in ReadGodotDictionaryEntries<
                    Vector2I,
                    List<BattleCellState>
                >(columns, TryAsVector2I, TryAsCellColumn)
            )
            {
                List<BattleCellState> clonedColumn = new();
                foreach (BattleCellState cell in entry.Value)
                {
                    if (cell != null)
                    {
                        clonedColumn.Add(cell.duplicate_cell());
                    }
                }
                results[entry.Key] = clonedColumn;
            }
            return results;
        }

        private static Dictionary<StringName, BattleUnitSnapshot> CaptureUnits(GDictionary units)
        {
            var results = new Dictionary<StringName, BattleUnitSnapshot>();
            if (units == null)
            {
                return results;
            }
            foreach (
                GodotDictionaryEntry<StringName, BattleUnitState> entry in ReadGodotDictionaryEntries<
                    StringName,
                    BattleUnitState
                >(units, TryAsStringName, TryAsBattleUnitState)
            )
            {
                BattleUnitState unit = entry.Value;
                MaterializeLazyStatusEffects(unit);
                results[unit.unit_id] = BattleUnitSnapshot.Capture(unit);
            }
            return results;
        }

        private static Dictionary<StringName, SkillDef> CaptureSkillDefs(GDictionary skillDefs)
        {
            var results = new Dictionary<StringName, SkillDef>();
            if (skillDefs == null)
            {
                return results;
            }
            foreach (
                GodotDictionaryEntry<StringName, SkillDef> entry in ReadGodotDictionaryEntries<
                    StringName,
                    SkillDef
                >(skillDefs, TryAsStringName, TryAsSkillDef)
            )
            {
                results[entry.Key] = entry.Value;
            }
            return results;
        }

        private static GDictionary BuildCellDictionary(
            Dictionary<Vector2I, BattleCellState> cells
        )
        {
            GDictionary result = new();
            foreach (KeyValuePair<Vector2I, BattleCellState> entry in cells)
            {
                result[entry.Key] = entry.Value?.duplicate_cell();
            }
            return result;
        }

        private static GDictionary BuildCellColumnDictionary(
            Dictionary<Vector2I, List<BattleCellState>> cellColumns
        )
        {
            GDictionary result = new();
            foreach (KeyValuePair<Vector2I, List<BattleCellState>> entry in cellColumns)
            {
                var column = new Godot.Collections.Array<BattleCellState>();
                foreach (BattleCellState cell in entry.Value)
                {
                    if (cell != null)
                    {
                        column.Add(cell.duplicate_cell());
                    }
                }
                result[entry.Key] = column;
            }
            return result;
        }

        private static GDictionary RestoreUnits(
            Dictionary<StringName, BattleUnitSnapshot> unitSnapshots
        )
        {
            GDictionary restoredUnits = new();
            foreach (KeyValuePair<StringName, BattleUnitSnapshot> entry in unitSnapshots)
            {
                BattleUnitState unit = entry.Value.Restore();
                if (unit != null)
                {
                    restoredUnits[entry.Key] = unit;
                }
            }
            return restoredUnits;
        }

        private static GDictionary BuildSkillDefDictionary(Dictionary<StringName, SkillDef> skillDefs)
        {
            GDictionary result = new();
            foreach (KeyValuePair<StringName, SkillDef> entry in skillDefs)
            {
                result[entry.Key] = entry.Value;
            }
            return result;
        }

        private static StableMap StableCells(Dictionary<Vector2I, BattleCellState> cells)
        {
            StableMap result = new();
            foreach (KeyValuePair<Vector2I, BattleCellState> entry in cells)
            {
                result.Set(StableKey(entry.Key), StableValue.FromMap(StableBattleCell(entry.Value)));
            }
            return result;
        }

        private static StableMap StableCellColumns(
            Dictionary<Vector2I, List<BattleCellState>> cellColumns
        )
        {
            StableMap result = new();
            foreach (KeyValuePair<Vector2I, List<BattleCellState>> entry in cellColumns)
            {
                List<StableValue> values = new();
                foreach (BattleCellState cell in entry.Value)
                {
                    values.Add(StableValue.FromMap(StableBattleCell(cell)));
                }
                result.Set(StableKey(entry.Key), StableValue.FromArray(values));
            }
            return result;
        }

        private static StableMap StableUnits(Dictionary<StringName, BattleUnitSnapshot> units)
        {
            StableMap result = new();
            foreach (KeyValuePair<StringName, BattleUnitSnapshot> entry in units)
            {
                result.Set(entry.Key.ToString(), StableValue.FromMap(entry.Value.ToStableMap()));
            }
            return result;
        }

        private static StableMap StableSkillDefs(Dictionary<StringName, SkillDef> skillDefs)
        {
            StableMap result = new();
            foreach (KeyValuePair<StringName, SkillDef> entry in skillDefs)
            {
                result.Set(
                    entry.Key.ToString(),
                    entry.Value != null
                        ? StableValue.FromObjectId((long)entry.Value.GetInstanceId())
                        : StableValue.Nil()
                );
            }
            return result;
        }
    }

    private static StableMap StableBattleUnitState(BattleUnitState unit)
    {
        return unit != null ? BattleUnitSnapshot.Capture(unit).ToStableMap() : new StableMap();
    }

    private static StableMap StableAttributeSnapshot(AttributeSnapshot attributeSnapshot)
    {
        StableMap result = new();
        if (attributeSnapshot == null)
        {
            return result;
        }
        GDictionary values = attributeSnapshot.get_all_values();
        foreach (
            GodotDictionaryEntry<StringName, int> entry in ReadGodotDictionaryEntries<
                StringName,
                int
            >(values, TryAsStringName, TryAsInt)
        )
        {
            result.Set(entry.Key.ToString(), StableValue.FromInteger(entry.Value));
        }
        return result;
    }

    private static StableMap StableBattleCell(BattleCellState cell)
    {
        StableMap result = new();
        if (cell == null)
        {
            return result;
        }
        result.Set("coord", StableValue.FromVector2I(cell.coord));
        result.Set("stack_layer", StableValue.FromInteger(cell.stack_layer));
        result.Set("base_terrain", StableValue.FromText(cell.base_terrain.ToString()));
        result.Set("base_height", StableValue.FromInteger(cell.base_height));
        result.Set("height_offset", StableValue.FromInteger(cell.height_offset));
        result.Set("current_height", StableValue.FromInteger(cell.current_height));
        result.Set("passable", StableValue.FromBool(cell.passable));
        result.Set("move_cost", StableValue.FromInteger(cell.move_cost));
        result.Set("occupant_unit_id", StableValue.FromText(cell.occupant_unit_id.ToString()));
        result.Set("prop_ids", StableValue.FromArray(StableStringNameArray(cell.prop_ids)));
        result.Set(
            "terrain_effect_ids",
            StableValue.FromArray(StableStringNameArray(cell.terrain_effect_ids))
        );
        result.Set(
            "timed_terrain_effects",
            StableValue.FromArray(StableTerrainEffectArray(cell.timed_terrain_effects))
        );
        result.Set("flow_direction", StableValue.FromVector2I(cell.flow_direction));
        result.Set("edge_feature_east", StableValue.FromMap(StableEdgeFeature(cell.edge_feature_east)));
        result.Set("edge_feature_south", StableValue.FromMap(StableEdgeFeature(cell.edge_feature_south)));
        return result;
    }

    private static StableMap StableTimeline(BattleTimelineState timeline)
    {
        StableMap result = new();
        if (timeline == null)
        {
            return result;
        }
        result.Set("current_tu", StableValue.FromInteger(timeline.current_tu));
        result.Set("tu_per_tick", StableValue.FromInteger(timeline.tu_per_tick));
        result.Set("frozen", StableValue.FromBool(timeline.frozen));
        result.Set("ready_unit_ids", StableValue.FromArray(StableStringNameArray(timeline.ready_unit_ids)));
        return result;
    }

    private static StableMap StableStatusEffect(BattleStatusEffectState statusEffect)
    {
        StableMap result = new();
        if (statusEffect == null)
        {
            return result;
        }
        result.Set("status_id", StableValue.FromText(statusEffect.status_id.ToString()));
        result.Set("source_unit_id", StableValue.FromText(statusEffect.source_unit_id.ToString()));
        result.Set("power", StableValue.FromInteger(statusEffect.power));
        result.Set("params", StableValue.FromMap(StableMap.FromGodotDictionary(statusEffect.@params)));
        result.Set("stacks", StableValue.FromInteger(statusEffect.stacks));
        result.Set("duration", StableValue.FromInteger(statusEffect.duration));
        result.Set("tick_interval_tu", StableValue.FromInteger(statusEffect.tick_interval_tu));
        result.Set("next_tick_at_tu", StableValue.FromInteger(statusEffect.next_tick_at_tu));
        result.Set(
            "skip_next_turn_end_decay",
            StableValue.FromBool(statusEffect.skip_next_turn_end_decay)
        );
        return result;
    }

    private static StableMap StableTerrainEffect(BattleTerrainEffectState terrainEffect)
    {
        StableMap result = new();
        if (terrainEffect == null)
        {
            return result;
        }
        result.Set("field_instance_id", StableValue.FromText(terrainEffect.field_instance_id.ToString()));
        result.Set("effect_id", StableValue.FromText(terrainEffect.effect_id.ToString()));
        result.Set("effect_type", StableValue.FromText(terrainEffect.effect_type.ToString()));
        result.Set("source_unit_id", StableValue.FromText(terrainEffect.source_unit_id.ToString()));
        result.Set("source_skill_id", StableValue.FromText(terrainEffect.source_skill_id.ToString()));
        result.Set("target_team_filter", StableValue.FromText(terrainEffect.target_team_filter.ToString()));
        result.Set("power", StableValue.FromInteger(terrainEffect.power));
        result.Set("damage_tag", StableValue.FromText(terrainEffect.damage_tag.ToString()));
        result.Set("remaining_tu", StableValue.FromInteger(terrainEffect.remaining_tu));
        result.Set("tick_interval_tu", StableValue.FromInteger(terrainEffect.tick_interval_tu));
        result.Set("next_tick_at_tu", StableValue.FromInteger(terrainEffect.next_tick_at_tu));
        result.Set("stack_behavior", StableValue.FromText(terrainEffect.stack_behavior.ToString()));
        result.Set("params", StableValue.FromMap(StableMap.FromGodotDictionary(terrainEffect.@params)));
        return result;
    }

    private static StableMap StableEdgeFeature(BattleEdgeFeatureState feature)
    {
        StableMap result = new();
        if (feature == null)
        {
            return result;
        }
        result.Set("feature_kind", StableValue.FromText(feature.feature_kind.ToString()));
        result.Set("render_kind", StableValue.FromText(feature.render_kind.ToString()));
        result.Set("render_layers", StableValue.FromInteger(feature.render_layers));
        result.Set("blocks_move", StableValue.FromBool(feature.blocks_move));
        result.Set("blocks_occupancy", StableValue.FromBool(feature.blocks_occupancy));
        result.Set("blocks_los", StableValue.FromBool(feature.blocks_los));
        result.Set("interaction_kind", StableValue.FromText(feature.interaction_kind.ToString()));
        result.Set("state_tag", StableValue.FromText(feature.state_tag.ToString()));
        return result;
    }

    private static StableMap StableWarehouse(WarehouseState warehouse)
    {
        StableMap result = new();
        if (warehouse == null)
        {
            return result;
        }
        List<StableValue> stacks = new();
        foreach (WarehouseStackState stack in warehouse.get_non_empty_stacks())
        {
            stacks.Add(StableValue.FromMap(StableWarehouseStack(stack)));
        }
        List<StableValue> instances = new();
        foreach (EquipmentInstanceState instance in warehouse.get_non_empty_instances())
        {
            instances.Add(StableValue.FromMap(StableEquipmentInstance(instance)));
        }
        result.Set("stacks", StableValue.FromArray(stacks));
        result.Set("equipment_instances", StableValue.FromArray(instances));
        return result;
    }

    private static StableMap StableWarehouseStack(WarehouseStackState stack)
    {
        StableMap result = new();
        if (stack == null)
        {
            return result;
        }
        result.Set("item_id", StableValue.FromText(stack.item_id.ToString()));
        result.Set("quantity", StableValue.FromInteger(Mathf.Max(stack.quantity, 0)));
        return result;
    }

    private static StableMap StableEquipment(EquipmentState equipment)
    {
        StableMap result = new();
        if (equipment == null)
        {
            return result;
        }
        StableMap slots = new();
        foreach (StringName slotId in equipment.get_entry_slot_ids())
        {
            EquipmentEntryState entry = equipment.get_entry(slotId);
            if (entry != null)
            {
                slots.Set(slotId.ToString(), StableValue.FromMap(StableEquipmentEntry(entry)));
            }
        }
        result.Set("equipped_slots", StableValue.FromMap(slots));
        return result;
    }

    private static StableMap StableEquipmentEntry(EquipmentEntryState entry)
    {
        StableMap result = new();
        if (entry == null)
        {
            return result;
        }
        result.Set(
            "occupied_slot_ids",
            StableValue.FromArray(StableStringNameArray(entry.occupied_slot_ids))
        );
        result.Set(
            "equipment_instance",
            StableValue.FromMap(StableEquipmentInstance(entry.equipment_instance))
        );
        return result;
    }

    private static StableMap StableEquipmentInstance(EquipmentInstanceState instance)
    {
        StableMap result = new();
        if (instance == null)
        {
            return result;
        }
        result.Set("instance_id", StableValue.FromText(instance.instance_id.ToString()));
        result.Set("item_id", StableValue.FromText(instance.item_id.ToString()));
        result.Set("rarity", StableValue.FromInteger(instance.rarity));
        result.Set("current_durability", StableValue.FromInteger(instance.current_durability));
        return result;
    }

    private static StableMap StableWeaponProjection(WeaponProjection projection)
    {
        StableMap result = new();
        if (projection == null)
        {
            return result;
        }
        result.Set("weapon_profile_kind", StableValue.FromText(projection.weapon_profile_kind.ToString()));
        result.Set("weapon_item_id", StableValue.FromText(projection.weapon_item_id.ToString()));
        result.Set("weapon_profile_type_id", StableValue.FromText(projection.weapon_profile_type_id.ToString()));
        result.Set("weapon_family", StableValue.FromText(projection.weapon_family.ToString()));
        result.Set("weapon_current_grip", StableValue.FromText(projection.weapon_current_grip.ToString()));
        result.Set("weapon_attack_range", StableValue.FromInteger(projection.weapon_attack_range));
        result.Set(
            "weapon_one_handed_dice",
            StableValue.FromMap(StableWeaponDice(projection.weapon_one_handed_dice))
        );
        result.Set(
            "weapon_two_handed_dice",
            StableValue.FromMap(StableWeaponDice(projection.weapon_two_handed_dice))
        );
        result.Set("weapon_is_versatile", StableValue.FromBool(projection.weapon_is_versatile));
        result.Set("weapon_uses_two_hands", StableValue.FromBool(projection.weapon_uses_two_hands));
        result.Set(
            "weapon_physical_damage_tag",
            StableValue.FromText(projection.weapon_physical_damage_tag.ToString())
        );
        return result;
    }

    private static StableMap StableWeaponDice(WeaponDice dice)
    {
        StableMap result = new();
        if (dice == null)
        {
            return result;
        }
        result.Set("dice_count", StableValue.FromInteger(dice.dice_count));
        result.Set("dice_sides", StableValue.FromInteger(dice.dice_sides));
        result.Set("flat_bonus", StableValue.FromInteger(dice.flat_bonus));
        return result;
    }

    private static List<StableValue> StableTerrainEffectArray(
        Godot.Collections.Array<BattleTerrainEffectState> values
    )
    {
        List<StableValue> result = new();
        foreach (BattleTerrainEffectState value in values ?? new Godot.Collections.Array<BattleTerrainEffectState>())
        {
            result.Add(StableValue.FromMap(StableTerrainEffect(value)));
        }
        return result;
    }

    private static List<StableValue> StableStringNameArray(Godot.Collections.Array<StringName> values)
    {
        List<StableValue> result = new();
        foreach (StringName value in values ?? new Godot.Collections.Array<StringName>())
        {
            result.Add(StableValue.FromText(value.ToString()));
        }
        return result;
    }

    private sealed class BattleUnitSnapshot
    {
        private readonly BattleUnitState _unit;
        private readonly BattleUnitFieldsSnapshot _fields;
        private readonly Dictionary<StringName, int> _attributeValues;
        private readonly EquipmentState _equipmentView;
        private readonly Dictionary<StringName, BattleStatusEffectState> _statusEffects;

        private BattleUnitSnapshot(
            BattleUnitState unit,
            BattleUnitFieldsSnapshot fields,
            Dictionary<StringName, int> attributeValues,
            EquipmentState equipmentView,
            Dictionary<StringName, BattleStatusEffectState> statusEffects
        )
        {
            _unit = unit;
            _fields = fields ?? BattleUnitFieldsSnapshot.Empty();
            _attributeValues = attributeValues ?? new Dictionary<StringName, int>();
            _equipmentView = equipmentView;
            _statusEffects = statusEffects ?? new Dictionary<StringName, BattleStatusEffectState>();
        }

        public static BattleUnitSnapshot Capture(BattleUnitState unit)
        {
            return new BattleUnitSnapshot(
                unit,
                BattleUnitFieldsSnapshot.Capture(unit),
                CaptureAttributeValues(unit?.attribute_snapshot),
                unit?.equipment_view?.duplicate_state(),
                CaptureStatusEffects(unit?.status_effects)
            );
        }

        public BattleUnitState Restore()
        {
            if (_unit == null)
            {
                return null;
            }
            _fields.Restore(_unit);
            AttributeSnapshot attributeSnapshot = new();
            foreach (KeyValuePair<StringName, int> entry in _attributeValues)
            {
                attributeSnapshot.set_value(entry.Key, entry.Value);
            }
            _unit.attribute_snapshot = attributeSnapshot;
            _unit.equipment_view = _equipmentView?.duplicate_state();
            _unit.status_effects = BuildStatusEffectDictionary(_statusEffects);
            return _unit;
        }

        public StableMap ToStableMap()
        {
            StableMap result = new();
            result.Set("fields", StableValue.FromMap(_fields.ToStableMap()));
            result.Set("attribute_snapshot_values", StableValue.FromMap(StableAttributeValues()));
            result.Set("equipment_view", StableValue.FromMap(StableEquipment(_equipmentView)));
            result.Set("status_effects", StableValue.FromMap(StableStatusEffects()));
            return result;
        }

        private StableMap StableAttributeValues()
        {
            StableMap result = new();
            foreach (KeyValuePair<StringName, int> entry in _attributeValues)
            {
                result.Set(entry.Key.ToString(), StableValue.FromInteger(entry.Value));
            }
            return result;
        }

        private StableMap StableStatusEffects()
        {
            StableMap result = new();
            foreach (KeyValuePair<StringName, BattleStatusEffectState> entry in _statusEffects)
            {
                result.Set(entry.Key.ToString(), StableValue.FromMap(StableStatusEffect(entry.Value)));
            }
            return result;
        }

        private static Dictionary<StringName, int> CaptureAttributeValues(
            AttributeSnapshot attributeSnapshot
        )
        {
            var results = new Dictionary<StringName, int>();
            if (attributeSnapshot == null)
            {
                return results;
            }
            GDictionary values = attributeSnapshot.get_all_values();
            foreach (
                GodotDictionaryEntry<StringName, int> entry in ReadGodotDictionaryEntries<
                    StringName,
                    int
                >(values, TryAsStringName, TryAsInt)
            )
            {
                results[entry.Key] = entry.Value;
            }
            return results;
        }

        private static Dictionary<StringName, BattleStatusEffectState> CaptureStatusEffects(
            GDictionary statusEffects
        )
        {
            var results = new Dictionary<StringName, BattleStatusEffectState>();
            if (statusEffects == null)
            {
                return results;
            }
            foreach (StatusEffectDictionaryEntry entry in ReadStatusEffectDictionaryEntries(statusEffects))
            {
                if (entry.Effect == null || entry.Effect.is_empty())
                {
                    continue;
                }
                results[entry.StatusId] = entry.Effect.duplicate_state();
            }
            return results;
        }

        private static GDictionary BuildStatusEffectDictionary(
            Dictionary<StringName, BattleStatusEffectState> statusEffects
        )
        {
            GDictionary result = new();
            foreach (KeyValuePair<StringName, BattleStatusEffectState> entry in statusEffects)
            {
                result[entry.Key] = entry.Value?.duplicate_state();
            }
            return result;
        }
    }

    private sealed class BattleStateFieldsSnapshot
    {
        private StringName _battleId = "";
        private int _seed;
        private int _attackRollNonce;
        private StringName _phase = "";
        private Vector2I _mapSize = Vector2I.Zero;
        private Vector2I _worldCoord = Vector2I.Zero;
        private StringName _encounterAnchorId = "";
        private StringName _terrainProfileId = "";
        private List<StringName> _attackDisadvantageTags = new();
        private List<StringName> _allyUnitIds = new();
        private List<StringName> _enemyUnitIds = new();
        private StringName _activeUnitId = "";
        private StringName _winnerFactionId = "";
        private List<string> _logEntries = new();
        private List<RestorableMap> _reportEntries = new();
        private List<RestorableMap> _promotionQueue = new();
        private StringName _modalState = "";
        private RestorableMap _layeredBarrierFields = new();

        public static BattleStateFieldsSnapshot Empty() => new();

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
            snapshot._attackDisadvantageTags = StringNameArrayToList(state.attack_disadvantage_tags);
            snapshot._allyUnitIds = StringNameArrayToList(state.ally_unit_ids);
            snapshot._enemyUnitIds = StringNameArrayToList(state.enemy_unit_ids);
            snapshot._activeUnitId = state.active_unit_id;
            snapshot._winnerFactionId = state.winner_faction_id;
            snapshot._logEntries = StringArrayToList(state.log_entries);
            snapshot._reportEntries = DictionaryArrayToRestorableMaps(state.report_entries);
            snapshot._promotionQueue = DictionaryArrayToRestorableMaps(state.promotion_queue);
            snapshot._modalState = state.modal_state;
            snapshot._layeredBarrierFields =
                RestorableMap.FromGodotDictionary(state.layered_barrier_fields);
            return snapshot;
        }

        public void Restore(BattleState state)
        {
            if (state == null)
            {
                return;
            }

            state.battle_id = _battleId;
            state.seed = _seed;
            state.attack_roll_nonce = _attackRollNonce;
            state.phase = _phase;
            state.map_size = _mapSize;
            state.world_coord = _worldCoord;
            state.encounter_anchor_id = _encounterAnchorId;
            state.terrain_profile_id = _terrainProfileId;
            state.attack_disadvantage_tags = BuildStringNameArray(_attackDisadvantageTags);
            state.ally_unit_ids = BuildStringNameArray(_allyUnitIds);
            state.enemy_unit_ids = BuildStringNameArray(_enemyUnitIds);
            state.active_unit_id = _activeUnitId;
            state.winner_faction_id = _winnerFactionId;
            state.log_entries = BuildStringArray(_logEntries);
            state.report_entries = BuildDictionaryArray(_reportEntries);
            state.promotion_queue = BuildDictionaryArray(_promotionQueue);
            state.modal_state = _modalState;
            state.layered_barrier_fields = _layeredBarrierFields.ToGodotDictionary();
        }

        public StableMap ToStableMap()
        {
            StableMap result = new();
            result.Set("battle_id", StableValue.FromText(_battleId.ToString()));
            result.Set("seed", StableValue.FromInteger(_seed));
            result.Set("attack_roll_nonce", StableValue.FromInteger(_attackRollNonce));
            result.Set("phase", StableValue.FromText(_phase.ToString()));
            result.Set("map_size", StableValue.FromVector2I(_mapSize));
            result.Set("world_coord", StableValue.FromVector2I(_worldCoord));
            result.Set("encounter_anchor_id", StableValue.FromText(_encounterAnchorId.ToString()));
            result.Set("terrain_profile_id", StableValue.FromText(_terrainProfileId.ToString()));
            result.Set("attack_disadvantage_tags", StableValue.FromArray(StableStringNameList(_attackDisadvantageTags)));
            result.Set("ally_unit_ids", StableValue.FromArray(StableStringNameList(_allyUnitIds)));
            result.Set("enemy_unit_ids", StableValue.FromArray(StableStringNameList(_enemyUnitIds)));
            result.Set("active_unit_id", StableValue.FromText(_activeUnitId.ToString()));
            result.Set("winner_faction_id", StableValue.FromText(_winnerFactionId.ToString()));
            result.Set("log_entries", StableValue.FromArray(StableTextList(_logEntries)));
            result.Set("report_entries", StableValue.FromArray(StableRestorableMapList(_reportEntries)));
            result.Set("promotion_queue", StableValue.FromArray(StableRestorableMapList(_promotionQueue)));
            result.Set("modal_state", StableValue.FromText(_modalState.ToString()));
            result.Set("layered_barrier_fields", StableValue.FromMap(_layeredBarrierFields.ToStableMap()));
            return result;
        }
    }

    private sealed class BattleUnitFieldsSnapshot
    {
        private StringName _unitId = "";
        private StringName _sourceMemberId = "";
        private StringName _enemyTemplateId = "";
        private string _displayName = "";
        private StringName _factionId = "";
        private StringName _controlMode = "";
        private StringName _aiBrainId = "";
        private StringName _aiStateId = "";
        private RestorableMap _aiBlackboard = new();
        private Vector2I _coord = Vector2I.Zero;
        private int _bodySize;
        private StringName _bodySizeCategory = "";
        private Vector2I _footprintSize = Vector2I.Zero;
        private List<Vector2I> _occupiedCoords = new();
        private bool _isAlive;
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
        private RestorableMap _shieldParams = new();
        private int _actionProgress;
        private int _actionThreshold;
        private List<StringName> _knownActiveSkillIds = new();
        private StringNameIntMapSnapshot _knownSkillLevelMap = new();
        private StringNameIntMapSnapshot _knownSkillLockHitBonusMap = new();
        private List<StringName> _movementTags = new();
        private List<StringName> _visionTags = new();
        private List<StringName> _proficiencyTags = new();
        private List<StringName> _saveAdvantageTags = new();
        private StringNameStringNameMapSnapshot _damageResistances = new();
        private List<StringName> _raceTraitIds = new();
        private List<StringName> _subraceTraitIds = new();
        private List<StringName> _ascensionTraitIds = new();
        private List<StringName> _bloodlineTraitIds = new();
        private StringName _versatilityPick = "";
        private StringName _weaponProfileKind = "";
        private StringName _weaponItemId = "";
        private StringName _weaponProfileTypeId = "";
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
        private RestorableMap _comboState = new();
        private StringNameIntMapSnapshot _perBattleCharges = new();
        private StringNameIntMapSnapshot _perTurnCharges = new();
        private StringNameIntMapSnapshot _perTurnChargeLimits = new();
        private StringNameIntMapSnapshot _fumbleProtectionUsed = new();

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
            snapshot._displayName = unit.display_name ?? "";
            snapshot._factionId = unit.faction_id;
            snapshot._controlMode = unit.control_mode;
            snapshot._aiBrainId = unit.ai_brain_id;
            snapshot._aiStateId = unit.ai_state_id;
            snapshot._aiBlackboard = RestorableMap.FromGodotDictionary(unit.ai_blackboard);
            snapshot._coord = unit.coord;
            snapshot._bodySize = unit.body_size;
            snapshot._bodySizeCategory = unit.body_size_category;
            snapshot._footprintSize = unit.footprint_size;
            snapshot._occupiedCoords = Vector2IArrayToList(unit.occupied_coords);
            snapshot._isAlive = unit.is_alive;
            snapshot._currentHp = unit.current_hp;
            snapshot._currentMp = unit.current_mp;
            snapshot._currentStamina = unit.current_stamina;
            snapshot._currentAura = unit.current_aura;
            snapshot._currentAp = unit.current_ap;
            snapshot._currentMovePoints = unit.current_move_points;
            snapshot._unlockedCombatResourceIds = StringNameArrayToList(unit.unlocked_combat_resource_ids);
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
            snapshot._shieldParams = RestorableMap.FromGodotDictionary(unit.shield_params);
            snapshot._actionProgress = unit.action_progress;
            snapshot._actionThreshold = unit.action_threshold;
            snapshot._knownActiveSkillIds = StringNameArrayToList(unit.known_active_skill_ids);
            snapshot._knownSkillLevelMap = StringNameIntMapSnapshot.FromGodotDictionary(unit.known_skill_level_map);
            snapshot._knownSkillLockHitBonusMap =
                StringNameIntMapSnapshot.FromGodotDictionary(unit.known_skill_lock_hit_bonus_map);
            snapshot._movementTags = StringNameArrayToList(unit.movement_tags);
            snapshot._visionTags = StringNameArrayToList(unit.vision_tags);
            snapshot._proficiencyTags = StringNameArrayToList(unit.proficiency_tags);
            snapshot._saveAdvantageTags = StringNameArrayToList(unit.save_advantage_tags);
            snapshot._damageResistances =
                StringNameStringNameMapSnapshot.FromGodotDictionary(unit.damage_resistances);
            snapshot._raceTraitIds = StringNameArrayToList(unit.race_trait_ids);
            snapshot._subraceTraitIds = StringNameArrayToList(unit.subrace_trait_ids);
            snapshot._ascensionTraitIds = StringNameArrayToList(unit.ascension_trait_ids);
            snapshot._bloodlineTraitIds = StringNameArrayToList(unit.bloodline_trait_ids);
            snapshot._versatilityPick = unit.versatility_pick;
            snapshot._weaponProfileKind = unit.weapon_profile_kind;
            snapshot._weaponItemId = unit.weapon_item_id;
            snapshot._weaponProfileTypeId = unit.weapon_profile_type_id;
            snapshot._weaponFamily = unit.weapon_family;
            snapshot._weaponCurrentGrip = unit.weapon_current_grip;
            snapshot._weaponAttackRange = unit.weapon_attack_range;
            snapshot._weaponOneHandedDice = WeaponDiceSnapshot.FromGodotDictionary(unit.weapon_one_handed_dice);
            snapshot._weaponTwoHandedDice = WeaponDiceSnapshot.FromGodotDictionary(unit.weapon_two_handed_dice);
            snapshot._weaponIsVersatile = unit.weapon_is_versatile;
            snapshot._weaponUsesTwoHands = unit.weapon_uses_two_hands;
            snapshot._weaponPhysicalDamageTag = unit.weapon_physical_damage_tag;
            snapshot._cooldowns = StringNameIntMapSnapshot.FromGodotDictionary(unit.cooldowns);
            snapshot._lastTurnTu = unit.last_turn_tu;
            snapshot._comboState = RestorableMap.FromGodotDictionary(unit.combo_state);
            snapshot._perBattleCharges = StringNameIntMapSnapshot.FromGodotDictionary(unit.per_battle_charges);
            snapshot._perTurnCharges = StringNameIntMapSnapshot.FromGodotDictionary(unit.per_turn_charges);
            snapshot._perTurnChargeLimits =
                StringNameIntMapSnapshot.FromGodotDictionary(unit.per_turn_charge_limits);
            snapshot._fumbleProtectionUsed =
                StringNameIntMapSnapshot.FromGodotDictionary(unit.fumble_protection_used);
            return snapshot;
        }

        public void Restore(BattleUnitState unit)
        {
            if (unit == null)
            {
                return;
            }

            unit.unit_id = _unitId;
            unit.source_member_id = _sourceMemberId;
            unit.enemy_template_id = _enemyTemplateId;
            unit.display_name = _displayName;
            unit.faction_id = _factionId;
            unit.control_mode = _controlMode;
            unit.ai_brain_id = _aiBrainId;
            unit.ai_state_id = _aiStateId;
            unit.ai_blackboard = _aiBlackboard.ToGodotDictionary();
            unit.coord = _coord;
            unit.body_size = _bodySize;
            unit.body_size_category = _bodySizeCategory;
            unit.footprint_size = _footprintSize;
            unit.occupied_coords = BuildVector2IArray(_occupiedCoords);
            unit.is_alive = _isAlive;
            unit.current_hp = _currentHp;
            unit.current_mp = _currentMp;
            unit.current_stamina = _currentStamina;
            unit.current_aura = _currentAura;
            unit.current_ap = _currentAp;
            unit.current_move_points = _currentMovePoints;
            unit.unlocked_combat_resource_ids = BuildStringNameArray(_unlockedCombatResourceIds);
            unit.stamina_recovery_progress = _staminaRecoveryProgress;
            unit.is_resting = _isResting;
            unit.has_taken_action_this_turn = _hasTakenActionThisTurn;
            unit.has_moved_this_turn = _hasMovedThisTurn;
            unit.can_use_locked_move_points_this_turn = _canUseLockedMovePointsThisTurn;
            unit.current_shield_hp = _currentShieldHp;
            unit.shield_max_hp = _shieldMaxHp;
            unit.shield_duration = _shieldDuration;
            unit.shield_family = _shieldFamily;
            unit.shield_source_unit_id = _shieldSourceUnitId;
            unit.shield_source_skill_id = _shieldSourceSkillId;
            unit.shield_params = _shieldParams.ToGodotDictionary();
            unit.action_progress = _actionProgress;
            unit.action_threshold = _actionThreshold;
            unit.known_active_skill_ids = BuildStringNameArray(_knownActiveSkillIds);
            unit.known_skill_level_map = _knownSkillLevelMap.ToGodotDictionary();
            unit.known_skill_lock_hit_bonus_map = _knownSkillLockHitBonusMap.ToGodotDictionary();
            unit.movement_tags = BuildStringNameArray(_movementTags);
            unit.vision_tags = BuildStringNameArray(_visionTags);
            unit.proficiency_tags = BuildStringNameArray(_proficiencyTags);
            unit.save_advantage_tags = BuildStringNameArray(_saveAdvantageTags);
            unit.damage_resistances = _damageResistances.ToGodotDictionary();
            unit.race_trait_ids = BuildStringNameArray(_raceTraitIds);
            unit.subrace_trait_ids = BuildStringNameArray(_subraceTraitIds);
            unit.ascension_trait_ids = BuildStringNameArray(_ascensionTraitIds);
            unit.bloodline_trait_ids = BuildStringNameArray(_bloodlineTraitIds);
            unit.versatility_pick = _versatilityPick;
            unit.weapon_profile_kind = _weaponProfileKind;
            unit.weapon_item_id = _weaponItemId;
            unit.weapon_profile_type_id = _weaponProfileTypeId;
            unit.weapon_family = _weaponFamily;
            unit.weapon_current_grip = _weaponCurrentGrip;
            unit.weapon_attack_range = _weaponAttackRange;
            unit.weapon_one_handed_dice = _weaponOneHandedDice.ToGodotDictionary();
            unit.weapon_two_handed_dice = _weaponTwoHandedDice.ToGodotDictionary();
            unit.weapon_is_versatile = _weaponIsVersatile;
            unit.weapon_uses_two_hands = _weaponUsesTwoHands;
            unit.weapon_physical_damage_tag = _weaponPhysicalDamageTag;
            unit.cooldowns = _cooldowns.ToGodotDictionary();
            unit.last_turn_tu = _lastTurnTu;
            unit.combo_state = _comboState.ToGodotDictionary();
            unit.per_battle_charges = _perBattleCharges.ToGodotDictionary();
            unit.per_turn_charges = _perTurnCharges.ToGodotDictionary();
            unit.per_turn_charge_limits = _perTurnChargeLimits.ToGodotDictionary();
            unit.fumble_protection_used = _fumbleProtectionUsed.ToGodotDictionary();
        }

        public StableMap ToStableMap()
        {
            StableMap result = new();
            result.Set("unit_id", StableValue.FromText(_unitId.ToString()));
            result.Set("source_member_id", StableValue.FromText(_sourceMemberId.ToString()));
            result.Set("enemy_template_id", StableValue.FromText(_enemyTemplateId.ToString()));
            result.Set("display_name", StableValue.FromText(_displayName));
            result.Set("faction_id", StableValue.FromText(_factionId.ToString()));
            result.Set("control_mode", StableValue.FromText(_controlMode.ToString()));
            result.Set("ai_brain_id", StableValue.FromText(_aiBrainId.ToString()));
            result.Set("ai_state_id", StableValue.FromText(_aiStateId.ToString()));
            result.Set("ai_blackboard", StableValue.FromMap(_aiBlackboard.ToStableMap()));
            result.Set("coord", StableValue.FromVector2I(_coord));
            result.Set("body_size", StableValue.FromInteger(_bodySize));
            result.Set("body_size_category", StableValue.FromText(_bodySizeCategory.ToString()));
            result.Set("footprint_size", StableValue.FromVector2I(_footprintSize));
            result.Set("occupied_coords", StableValue.FromArray(StableVector2IList(_occupiedCoords)));
            result.Set("is_alive", StableValue.FromBool(_isAlive));
            result.Set("current_hp", StableValue.FromInteger(_currentHp));
            result.Set("current_mp", StableValue.FromInteger(_currentMp));
            result.Set("current_stamina", StableValue.FromInteger(_currentStamina));
            result.Set("current_aura", StableValue.FromInteger(_currentAura));
            result.Set("current_ap", StableValue.FromInteger(_currentAp));
            result.Set("current_move_points", StableValue.FromInteger(_currentMovePoints));
            result.Set(
                "unlocked_combat_resource_ids",
                StableValue.FromArray(StableStringNameList(_unlockedCombatResourceIds))
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
            result.Set("shield_family", StableValue.FromText(_shieldFamily.ToString()));
            result.Set("shield_source_unit_id", StableValue.FromText(_shieldSourceUnitId.ToString()));
            result.Set("shield_source_skill_id", StableValue.FromText(_shieldSourceSkillId.ToString()));
            result.Set("shield_params", StableValue.FromMap(_shieldParams.ToStableMap()));
            result.Set("action_progress", StableValue.FromInteger(_actionProgress));
            result.Set("action_threshold", StableValue.FromInteger(_actionThreshold));
            result.Set("known_active_skill_ids", StableValue.FromArray(StableStringNameList(_knownActiveSkillIds)));
            result.Set("known_skill_level_map", StableValue.FromMap(_knownSkillLevelMap.ToStableMap()));
            result.Set(
                "known_skill_lock_hit_bonus_map",
                StableValue.FromMap(_knownSkillLockHitBonusMap.ToStableMap())
            );
            result.Set("movement_tags", StableValue.FromArray(StableStringNameList(_movementTags)));
            result.Set("vision_tags", StableValue.FromArray(StableStringNameList(_visionTags)));
            result.Set("proficiency_tags", StableValue.FromArray(StableStringNameList(_proficiencyTags)));
            result.Set("save_advantage_tags", StableValue.FromArray(StableStringNameList(_saveAdvantageTags)));
            result.Set("damage_resistances", StableValue.FromMap(_damageResistances.ToStableMap()));
            result.Set("race_trait_ids", StableValue.FromArray(StableStringNameList(_raceTraitIds)));
            result.Set("subrace_trait_ids", StableValue.FromArray(StableStringNameList(_subraceTraitIds)));
            result.Set("ascension_trait_ids", StableValue.FromArray(StableStringNameList(_ascensionTraitIds)));
            result.Set("bloodline_trait_ids", StableValue.FromArray(StableStringNameList(_bloodlineTraitIds)));
            result.Set("versatility_pick", StableValue.FromText(_versatilityPick.ToString()));
            result.Set("weapon_profile_kind", StableValue.FromText(_weaponProfileKind.ToString()));
            result.Set("weapon_item_id", StableValue.FromText(_weaponItemId.ToString()));
            result.Set("weapon_profile_type_id", StableValue.FromText(_weaponProfileTypeId.ToString()));
            result.Set("weapon_family", StableValue.FromText(_weaponFamily.ToString()));
            result.Set("weapon_current_grip", StableValue.FromText(_weaponCurrentGrip.ToString()));
            result.Set("weapon_attack_range", StableValue.FromInteger(_weaponAttackRange));
            result.Set("weapon_one_handed_dice", StableValue.FromMap(_weaponOneHandedDice.ToStableMap()));
            result.Set("weapon_two_handed_dice", StableValue.FromMap(_weaponTwoHandedDice.ToStableMap()));
            result.Set("weapon_is_versatile", StableValue.FromBool(_weaponIsVersatile));
            result.Set("weapon_uses_two_hands", StableValue.FromBool(_weaponUsesTwoHands));
            result.Set("weapon_physical_damage_tag", StableValue.FromText(_weaponPhysicalDamageTag.ToString()));
            result.Set("cooldowns", StableValue.FromMap(_cooldowns.ToStableMap()));
            result.Set("last_turn_tu", StableValue.FromInteger(_lastTurnTu));
            result.Set("combo_state", StableValue.FromMap(_comboState.ToStableMap()));
            result.Set("per_battle_charges", StableValue.FromMap(_perBattleCharges.ToStableMap()));
            result.Set("per_turn_charges", StableValue.FromMap(_perTurnCharges.ToStableMap()));
            result.Set("per_turn_charge_limits", StableValue.FromMap(_perTurnChargeLimits.ToStableMap()));
            result.Set("fumble_protection_used", StableValue.FromMap(_fumbleProtectionUsed.ToStableMap()));
            return result;
        }
    }

    private enum RestorableValueKind
    {
        Nil,
        Bool,
        Integer,
        Float,
        Text,
        StringName,
        Vector2I,
        Vector2,
        Vector3I,
        Vector3,
        Map,
        Array,
        BattleUnitState,
        BattleCellState,
        BattleStatusEffectState,
        WarehouseState,
        WarehouseStackState,
        EquipmentState,
        EquipmentEntryState,
        EquipmentInstanceState,
        WeaponProjection,
        WeaponDice,
        Resource,
        UnknownObject,
        Fallback,
    }

    private sealed class RestorableValue
    {
        private readonly RestorableValueKind _kind;
        private readonly bool _boolValue;
        private readonly long _integerValue;
        private readonly double _floatValue;
        private readonly string _textValue;
        private readonly StringName _stringNameValue;
        private readonly Vector2I _vector2IValue;
        private readonly Vector2 _vector2Value;
        private readonly Vector3I _vector3IValue;
        private readonly Vector3 _vector3Value;
        private readonly RestorableMap _mapValue;
        private readonly List<RestorableValue> _arrayValue;
        private readonly BattleUnitState _unitValue;
        private readonly BattleCellState _cellValue;
        private readonly BattleStatusEffectState _statusEffectValue;
        private readonly WarehouseState _warehouseValue;
        private readonly WarehouseStackState _warehouseStackValue;
        private readonly EquipmentState _equipmentValue;
        private readonly EquipmentEntryState _equipmentEntryValue;
        private readonly EquipmentInstanceState _equipmentInstanceValue;
        private readonly WeaponProjection _weaponProjectionValue;
        private readonly WeaponDice _weaponDiceValue;
        private readonly Resource _resourceValue;
        private readonly GodotObject _unknownObjectValue;
        private readonly Variant _fallbackValue;

        private RestorableValue(
            RestorableValueKind kind,
            bool boolValue = false,
            long integerValue = 0,
            double floatValue = 0.0,
            string textValue = "",
            StringName stringNameValue = default,
            Vector2I vector2IValue = default,
            Vector2 vector2Value = default,
            Vector3I vector3IValue = default,
            Vector3 vector3Value = default,
            RestorableMap mapValue = null,
            List<RestorableValue> arrayValue = null,
            BattleUnitState unitValue = null,
            BattleCellState cellValue = null,
            BattleStatusEffectState statusEffectValue = null,
            WarehouseState warehouseValue = null,
            WarehouseStackState warehouseStackValue = null,
            EquipmentState equipmentValue = null,
            EquipmentEntryState equipmentEntryValue = null,
            EquipmentInstanceState equipmentInstanceValue = null,
            WeaponProjection weaponProjectionValue = null,
            WeaponDice weaponDiceValue = null,
            Resource resourceValue = null,
            GodotObject unknownObjectValue = null,
            Variant fallbackValue = default
        )
        {
            _kind = kind;
            _boolValue = boolValue;
            _integerValue = integerValue;
            _floatValue = floatValue;
            _textValue = textValue ?? "";
            _stringNameValue = stringNameValue;
            _vector2IValue = vector2IValue;
            _vector2Value = vector2Value;
            _vector3IValue = vector3IValue;
            _vector3Value = vector3Value;
            _mapValue = mapValue;
            _arrayValue = arrayValue;
            _unitValue = unitValue;
            _cellValue = cellValue;
            _statusEffectValue = statusEffectValue;
            _warehouseValue = warehouseValue;
            _warehouseStackValue = warehouseStackValue;
            _equipmentValue = equipmentValue;
            _equipmentEntryValue = equipmentEntryValue;
            _equipmentInstanceValue = equipmentInstanceValue;
            _weaponProjectionValue = weaponProjectionValue;
            _weaponDiceValue = weaponDiceValue;
            _resourceValue = resourceValue;
            _unknownObjectValue = unknownObjectValue;
            _fallbackValue = fallbackValue;
        }

        public static RestorableValue FromGodotValue(Variant rawValue) => FromVariant(rawValue);

        public static RestorableValue Nil() => new(RestorableValueKind.Nil);

        public Variant ToGodotValue()
        {
            return _kind switch
            {
                RestorableValueKind.Nil => default,
                RestorableValueKind.Bool => Variant.From(_boolValue),
                RestorableValueKind.Integer => Variant.From(_integerValue),
                RestorableValueKind.Float => Variant.From(_floatValue),
                RestorableValueKind.Text => Variant.From(_textValue),
                RestorableValueKind.StringName => Variant.From(_stringNameValue),
                RestorableValueKind.Vector2I => Variant.From(_vector2IValue),
                RestorableValueKind.Vector2 => Variant.From(_vector2Value),
                RestorableValueKind.Vector3I => Variant.From(_vector3IValue),
                RestorableValueKind.Vector3 => Variant.From(_vector3Value),
                RestorableValueKind.Map => Variant.From(_mapValue.ToGodotDictionary()),
                RestorableValueKind.Array => Variant.From(ToGodotArray(_arrayValue)),
                RestorableValueKind.BattleUnitState => ToObjectVariant(_unitValue?.clone()),
                RestorableValueKind.BattleCellState => ToObjectVariant(_cellValue?.duplicate_cell()),
                RestorableValueKind.BattleStatusEffectState
                    => ToObjectVariant(DuplicateStatusEffect(_statusEffectValue)),
                RestorableValueKind.WarehouseState => ToObjectVariant(DuplicateWarehouse(_warehouseValue)),
                RestorableValueKind.WarehouseStackState
                    => ToObjectVariant(DuplicateWarehouseStack(_warehouseStackValue)),
                RestorableValueKind.EquipmentState => ToObjectVariant(DuplicateEquipment(_equipmentValue)),
                RestorableValueKind.EquipmentEntryState
                    => ToObjectVariant(DuplicateEquipmentEntry(_equipmentEntryValue)),
                RestorableValueKind.EquipmentInstanceState
                    => ToObjectVariant(DuplicateEquipmentInstance(_equipmentInstanceValue)),
                RestorableValueKind.WeaponProjection
                    => ToObjectVariant(_weaponProjectionValue?.duplicate_state()),
                RestorableValueKind.WeaponDice => ToObjectVariant(_weaponDiceValue?.duplicate_state()),
                RestorableValueKind.Resource => ToObjectVariant(_resourceValue?.Duplicate(true)),
                RestorableValueKind.UnknownObject => ToObjectVariant(_unknownObjectValue),
                RestorableValueKind.Fallback => _fallbackValue,
                _ => default,
            };
        }

        public StableValue ToStableValue()
        {
            return _kind switch
            {
                RestorableValueKind.Nil => StableValue.Nil(),
                RestorableValueKind.Bool => StableValue.FromBool(_boolValue),
                RestorableValueKind.Integer => StableValue.FromInteger(_integerValue),
                RestorableValueKind.Float => StableValue.FromFloat(_floatValue),
                RestorableValueKind.Text => StableValue.FromText(_textValue),
                RestorableValueKind.StringName => StableValue.FromText(_stringNameValue.ToString()),
                RestorableValueKind.Vector2I => StableValue.FromVector2I(_vector2IValue),
                RestorableValueKind.Vector2 => StableValue.FromVector2(_vector2Value),
                RestorableValueKind.Vector3I => StableValue.FromVector3I(_vector3IValue),
                RestorableValueKind.Vector3 => StableValue.FromVector3(_vector3Value),
                RestorableValueKind.Map => StableValue.FromMap(_mapValue.ToStableMap()),
                RestorableValueKind.Array => StableValue.FromArray(StableArrayFromRestorable(_arrayValue)),
                RestorableValueKind.BattleUnitState
                    => StableValue.FromMap(StableBattleUnitState(_unitValue)),
                RestorableValueKind.BattleCellState => StableValue.FromMap(StableBattleCell(_cellValue)),
                RestorableValueKind.BattleStatusEffectState
                    => StableValue.FromMap(StableStatusEffect(_statusEffectValue)),
                RestorableValueKind.WarehouseState
                    => StableValue.FromMap(StableWarehouse(_warehouseValue)),
                RestorableValueKind.WarehouseStackState
                    => StableValue.FromMap(StableWarehouseStack(_warehouseStackValue)),
                RestorableValueKind.EquipmentState
                    => StableValue.FromMap(StableEquipment(_equipmentValue)),
                RestorableValueKind.EquipmentEntryState
                    => StableValue.FromMap(StableEquipmentEntry(_equipmentEntryValue)),
                RestorableValueKind.EquipmentInstanceState
                    => StableValue.FromMap(StableEquipmentInstance(_equipmentInstanceValue)),
                RestorableValueKind.WeaponProjection
                    => StableValue.FromMap(StableWeaponProjection(_weaponProjectionValue)),
                RestorableValueKind.WeaponDice => StableValue.FromMap(StableWeaponDice(_weaponDiceValue)),
                RestorableValueKind.Resource
                    => _resourceValue != null
                        ? StableValue.FromObjectId((long)_resourceValue.GetInstanceId())
                        : StableValue.Nil(),
                RestorableValueKind.UnknownObject
                    => _unknownObjectValue != null
                        ? StableValue.FromObjectId((long)_unknownObjectValue.GetInstanceId())
                        : StableValue.Nil(),
                RestorableValueKind.Fallback => StableValue.FromVariantValue(_fallbackValue),
                _ => StableValue.Nil(),
            };
        }

        private static RestorableValue FromVariant(Variant value)
        {
            return value.VariantType switch
            {
                Variant.Type.Nil => new RestorableValue(RestorableValueKind.Nil),
                Variant.Type.Bool
                    => new RestorableValue(RestorableValueKind.Bool, boolValue: value.AsBool()),
                Variant.Type.Int => FromInteger(value.AsInt64()),
                Variant.Type.Float => FromFloat(value.AsDouble()),
                Variant.Type.String
                    => new RestorableValue(RestorableValueKind.Text, textValue: value.AsString()),
                Variant.Type.StringName
                    => new RestorableValue(
                        RestorableValueKind.StringName,
                        stringNameValue: value.AsStringName()
                    ),
                Variant.Type.Vector2I
                    => new RestorableValue(
                        RestorableValueKind.Vector2I,
                        vector2IValue: value.AsVector2I()
                    ),
                Variant.Type.Vector2
                    => new RestorableValue(
                        RestorableValueKind.Vector2,
                        vector2Value: value.AsVector2()
                    ),
                Variant.Type.Vector3I
                    => new RestorableValue(
                        RestorableValueKind.Vector3I,
                        vector3IValue: value.AsVector3I()
                    ),
                Variant.Type.Vector3
                    => new RestorableValue(
                        RestorableValueKind.Vector3,
                        vector3Value: value.AsVector3()
                    ),
                Variant.Type.Dictionary
                    => new RestorableValue(
                        RestorableValueKind.Map,
                        mapValue: RestorableMap.FromGodotDictionary(value.AsGodotDictionary())
                    ),
                Variant.Type.Array
                    => new RestorableValue(
                        RestorableValueKind.Array,
                        arrayValue: RestorableArrayFromGodot(value.AsGodotArray())
                    ),
                Variant.Type.Object => FromGodotObject(value.AsGodotObject()),
                _ => new RestorableValue(RestorableValueKind.Fallback, fallbackValue: value),
            };
        }

        private static RestorableValue FromGodotObject(GodotObject value)
        {
            if (value == null)
            {
                return new RestorableValue(RestorableValueKind.Nil);
            }
            return value switch
            {
                BattleUnitState unitState
                    => new RestorableValue(
                        RestorableValueKind.BattleUnitState,
                        unitValue: unitState.clone()
                    ),
                BattleCellState cellState
                    => new RestorableValue(
                        RestorableValueKind.BattleCellState,
                        cellValue: cellState.duplicate_cell()
                    ),
                BattleStatusEffectState statusEffectState
                    => new RestorableValue(
                        RestorableValueKind.BattleStatusEffectState,
                        statusEffectValue: DuplicateStatusEffect(statusEffectState)
                    ),
                WarehouseState warehouseState
                    => new RestorableValue(
                        RestorableValueKind.WarehouseState,
                        warehouseValue: DuplicateWarehouse(warehouseState)
                    ),
                WarehouseStackState warehouseStackState
                    => new RestorableValue(
                        RestorableValueKind.WarehouseStackState,
                        warehouseStackValue: DuplicateWarehouseStack(warehouseStackState)
                    ),
                EquipmentState equipmentState
                    => new RestorableValue(
                        RestorableValueKind.EquipmentState,
                        equipmentValue: DuplicateEquipment(equipmentState)
                    ),
                EquipmentEntryState equipmentEntryState
                    => new RestorableValue(
                        RestorableValueKind.EquipmentEntryState,
                        equipmentEntryValue: DuplicateEquipmentEntry(equipmentEntryState)
                    ),
                EquipmentInstanceState equipmentInstanceState
                    => new RestorableValue(
                        RestorableValueKind.EquipmentInstanceState,
                        equipmentInstanceValue: DuplicateEquipmentInstance(equipmentInstanceState)
                    ),
                WeaponProjection weaponProjection
                    => new RestorableValue(
                        RestorableValueKind.WeaponProjection,
                        weaponProjectionValue: weaponProjection.duplicate_state()
                    ),
                WeaponDice weaponDice
                    => new RestorableValue(
                        RestorableValueKind.WeaponDice,
                        weaponDiceValue: weaponDice.duplicate_state()
                    ),
                Resource resource
                    => new RestorableValue(
                        RestorableValueKind.Resource,
                        resourceValue: resource.Duplicate(true)
                    ),
                _ => new RestorableValue(
                    RestorableValueKind.UnknownObject,
                    unknownObjectValue: value
                ),
            };
        }

        private static RestorableValue FromInteger(long value) =>
            new(RestorableValueKind.Integer, integerValue: value);

        private static RestorableValue FromFloat(double value) =>
            new(RestorableValueKind.Float, floatValue: value);

        private static List<RestorableValue> RestorableArrayFromGodot(GArray source)
        {
            return ReadRestorableArrayValues(source);
        }

        private static GArray ToGodotArray(List<RestorableValue> source)
        {
            GArray result = new();
            foreach (RestorableValue item in source ?? new List<RestorableValue>())
            {
                result.Add(item.ToGodotValue());
            }
            return result;
        }

        private static Variant ToObjectVariant(GodotObject value)
        {
            return value != null ? Variant.From(value) : default;
        }

        private static List<StableValue> StableArrayFromRestorable(
            List<RestorableValue> source
        )
        {
            List<StableValue> result = new();
            foreach (RestorableValue item in source ?? new List<RestorableValue>())
            {
                result.Add(item.ToStableValue());
            }
            return result;
        }
    }

    private readonly struct RestorableKey
    {
        private readonly RestorableKeyKind _kind;
        private readonly string _textValue;
        private readonly StringName _stringNameValue;
        private readonly Vector2I _vector2IValue;
        private readonly long _integerValue;

        private RestorableKey(
            RestorableKeyKind kind,
            string textValue = "",
            StringName stringNameValue = default,
            Vector2I vector2IValue = default,
            long integerValue = 0
        )
        {
            _kind = kind;
            _textValue = textValue ?? "";
            _stringNameValue = stringNameValue;
            _vector2IValue = vector2IValue;
            _integerValue = integerValue;
        }

        public string StableText =>
            _kind switch
            {
                RestorableKeyKind.Text => _textValue,
                RestorableKeyKind.StringName => _stringNameValue.ToString(),
                RestorableKeyKind.Vector2I => StableKey(_vector2IValue),
                RestorableKeyKind.Integer => _integerValue.ToString(CultureInfo.InvariantCulture),
                _ => _textValue,
            };

        public static RestorableKey FromGodotKey(Variant rawKey) => FromVariantKey(rawKey);

        public Variant ToGodotKey()
        {
            return _kind switch
            {
                RestorableKeyKind.Text => Variant.From(_textValue),
                RestorableKeyKind.StringName => Variant.From(_stringNameValue),
                RestorableKeyKind.Vector2I => Variant.From(_vector2IValue),
                RestorableKeyKind.Integer => Variant.From(_integerValue),
                _ => Variant.From(_textValue),
            };
        }

        private static RestorableKey FromVariantKey(Variant key)
        {
            return key.VariantType switch
            {
                Variant.Type.String => new RestorableKey(
                    RestorableKeyKind.Text,
                    textValue: key.AsString()
                ),
                Variant.Type.StringName => new RestorableKey(
                    RestorableKeyKind.StringName,
                    stringNameValue: key.AsStringName()
                ),
                Variant.Type.Vector2I => new RestorableKey(
                    RestorableKeyKind.Vector2I,
                    vector2IValue: key.AsVector2I()
                ),
                Variant.Type.Int => new RestorableKey(
                    RestorableKeyKind.Integer,
                    integerValue: key.AsInt64()
                ),
                _ => new RestorableKey(
                    RestorableKeyKind.Fallback,
                    textValue: $"type{(int)key.VariantType}({key})"
                ),
            };
        }
    }

    private enum RestorableKeyKind
    {
        Text,
        StringName,
        Vector2I,
        Integer,
        Fallback,
    }

    private sealed class RestorableMap
    {
        private readonly List<KeyValuePair<RestorableKey, RestorableValue>> _entries = new();

        public static RestorableMap FromGodotDictionary(GDictionary source)
        {
            RestorableMap result = new();
            if (source == null)
            {
                return result;
            }
            foreach (RestorableDictionaryEntry entry in ReadRestorableDictionaryEntries(source))
            {
                result._entries.Add(new KeyValuePair<RestorableKey, RestorableValue>(entry.Key, entry.Value));
            }
            return result;
        }

        public StableMap ToStableMap()
        {
            StableMap result = new();
            foreach (KeyValuePair<RestorableKey, RestorableValue> entry in _entries)
            {
                result.Set(entry.Key.StableText, entry.Value.ToStableValue());
            }
            return result;
        }

        public GDictionary ToGodotDictionary()
        {
            GDictionary result = new();
            foreach (KeyValuePair<RestorableKey, RestorableValue> entry in _entries)
            {
                SetDictionaryValue(result, entry.Key.ToGodotKey(), entry.Value.ToGodotValue());
            }
            return result;
        }
    }

    private sealed class StringNameIntMapSnapshot
    {
        private readonly Dictionary<StringName, int> _values = new();
        private readonly List<RestorableDictionaryEntry> _untypedEntries = new();

        public static StringNameIntMapSnapshot FromGodotDictionary(GDictionary source)
        {
            StringNameIntMapSnapshot result = new();
            if (source == null)
            {
                return result;
            }

            foreach (var rawKey in source.Keys)
            {
                bool hasValue = TryGetDictionaryValue(source, rawKey, out Variant rawValue);
                if (
                    hasValue
                    && TryAsStringName(rawKey, out StringName key)
                    && TryAsInt(rawValue, out int value)
                )
                {
                    result._values[key] = value;
                    continue;
                }

                result._untypedEntries.Add(
                    new RestorableDictionaryEntry(
                        RestorableKey.FromGodotKey(rawKey),
                        hasValue ? RestorableValue.FromGodotValue(rawValue) : RestorableValue.Nil()
                    )
                );
            }
            return result;
        }

        public GDictionary ToGodotDictionary()
        {
            GDictionary result = new();
            foreach (KeyValuePair<StringName, int> entry in _values)
            {
                result[entry.Key] = entry.Value;
            }
            foreach (RestorableDictionaryEntry entry in _untypedEntries)
            {
                SetDictionaryValue(result, entry.Key.ToGodotKey(), entry.Value.ToGodotValue());
            }
            return result;
        }

        public StableMap ToStableMap()
        {
            StableMap result = new();
            foreach (KeyValuePair<StringName, int> entry in _values)
            {
                result.Set(entry.Key.ToString(), StableValue.FromInteger(entry.Value));
            }
            foreach (RestorableDictionaryEntry entry in _untypedEntries)
            {
                result.Set(entry.Key.StableText, entry.Value.ToStableValue());
            }
            return result;
        }
    }

    private sealed class StringNameStringNameMapSnapshot
    {
        private readonly Dictionary<StringName, StringName> _values = new();
        private readonly List<RestorableDictionaryEntry> _untypedEntries = new();

        public static StringNameStringNameMapSnapshot FromGodotDictionary(GDictionary source)
        {
            StringNameStringNameMapSnapshot result = new();
            if (source == null)
            {
                return result;
            }

            foreach (var rawKey in source.Keys)
            {
                bool hasValue = TryGetDictionaryValue(source, rawKey, out Variant rawValue);
                if (
                    hasValue
                    && TryAsStringName(rawKey, out StringName key)
                    && TryAsStringName(rawValue, out StringName value)
                )
                {
                    result._values[key] = value;
                    continue;
                }

                result._untypedEntries.Add(
                    new RestorableDictionaryEntry(
                        RestorableKey.FromGodotKey(rawKey),
                        hasValue ? RestorableValue.FromGodotValue(rawValue) : RestorableValue.Nil()
                    )
                );
            }
            return result;
        }

        public GDictionary ToGodotDictionary()
        {
            GDictionary result = new();
            foreach (KeyValuePair<StringName, StringName> entry in _values)
            {
                result[entry.Key] = entry.Value;
            }
            foreach (RestorableDictionaryEntry entry in _untypedEntries)
            {
                SetDictionaryValue(result, entry.Key.ToGodotKey(), entry.Value.ToGodotValue());
            }
            return result;
        }

        public StableMap ToStableMap()
        {
            StableMap result = new();
            foreach (KeyValuePair<StringName, StringName> entry in _values)
            {
                result.Set(entry.Key.ToString(), StableValue.FromText(entry.Value.ToString()));
            }
            foreach (RestorableDictionaryEntry entry in _untypedEntries)
            {
                result.Set(entry.Key.StableText, entry.Value.ToStableValue());
            }
            return result;
        }
    }

    private sealed class WeaponDiceSnapshot
    {
        private readonly bool _hasTypedDice;
        private readonly WeaponDice _typedDice;
        private readonly RestorableMap _fallbackMap;

        public WeaponDiceSnapshot(
            bool hasTypedDice = false,
            WeaponDice typedDice = null,
            RestorableMap fallbackMap = null
        )
        {
            _hasTypedDice = hasTypedDice;
            _typedDice = typedDice;
            _fallbackMap = fallbackMap;
        }

        public static WeaponDiceSnapshot FromGodotDictionary(GDictionary source)
        {
            if (source == null || source.Count == 0)
            {
                return new WeaponDiceSnapshot();
            }

            GDictionary normalized = BattleUnitState._strict_weapon_dice_from_dict(source);
            if (normalized == null)
            {
                return new WeaponDiceSnapshot(
                    fallbackMap: RestorableMap.FromGodotDictionary(source)
                );
            }
            if (normalized.Count == 0)
            {
                return new WeaponDiceSnapshot();
            }

            return new WeaponDiceSnapshot(
                true,
                WeaponDice.from_dict(normalized).duplicate_state()
            );
        }

        public GDictionary ToGodotDictionary()
        {
            if (_fallbackMap != null)
            {
                return _fallbackMap.ToGodotDictionary();
            }
            return _hasTypedDice ? _typedDice.to_dict() : new GDictionary();
        }

        public StableMap ToStableMap()
        {
            if (_fallbackMap != null)
            {
                return _fallbackMap.ToStableMap();
            }
            return _hasTypedDice ? StableWeaponDice(_typedDice) : new StableMap();
        }
    }

    private static void MaterializeLazyStatusEffects(BattleUnitState unit)
    {
        if (unit == null || unit.status_effects == null)
        {
            return;
        }

        List<StringName> staleStatusIds = new();
        foreach (StatusEffectDictionaryEntry entry in ReadStatusEffectDictionaryEntries(unit.status_effects))
        {
            if (entry.IsTyped)
            {
                continue;
            }

            if (entry.Effect == null || entry.Effect.is_empty())
            {
                staleStatusIds.Add(entry.StatusId);
            }
            else
            {
                unit.status_effects[entry.StatusId] = entry.Effect;
            }
        }

        foreach (StringName statusId in staleStatusIds)
        {
            RemoveStringNameDictionaryKey(unit.status_effects, statusId);
        }
    }

    private static BattleTimelineState CloneTimeline(BattleTimelineState timeline)
    {
        if (timeline == null)
        {
            return null;
        }
        return new BattleTimelineState
        {
            current_tu = timeline.current_tu,
            tu_per_tick = timeline.tu_per_tick,
            frozen = timeline.frozen,
            ready_unit_ids = DuplicateStringNameArray(timeline.ready_unit_ids),
        };
    }

    private static BattleStatusEffectState DuplicateStatusEffect(BattleStatusEffectState effect)
    {
        if (effect == null)
        {
            return null;
        }
        return new BattleStatusEffectState
        {
            status_id = effect.status_id,
            source_unit_id = effect.source_unit_id,
            power = effect.power,
            @params = effect.@params?.Duplicate(true) ?? new GDictionary(),
            stacks = effect.stacks,
            duration = effect.duration,
            tick_interval_tu = effect.tick_interval_tu,
            next_tick_at_tu = effect.next_tick_at_tu,
            skip_next_turn_end_decay = effect.skip_next_turn_end_decay,
        };
    }

    private static WarehouseState DuplicateWarehouse(WarehouseState warehouse)
    {
        if (warehouse == null)
        {
            return null;
        }
        WarehouseState copy = new();
        foreach (WarehouseStackState stack in warehouse.get_non_empty_stacks())
        {
            WarehouseStackState stackCopy = DuplicateWarehouseStack(stack);
            if (stackCopy != null && !stackCopy.is_empty())
            {
                copy.stacks.Add(stackCopy);
            }
        }
        foreach (EquipmentInstanceState instance in warehouse.get_non_empty_instances())
        {
            EquipmentInstanceState instanceCopy = DuplicateEquipmentInstance(instance);
            if (instanceCopy != null && instanceCopy.instance_id != "" && instanceCopy.item_id != "")
            {
                copy.equipment_instances.Add(instanceCopy);
            }
        }
        return copy;
    }

    private static WarehouseStackState DuplicateWarehouseStack(WarehouseStackState stack)
    {
        if (stack == null)
        {
            return null;
        }
        return new WarehouseStackState
        {
            item_id = stack.item_id,
            quantity = stack.quantity,
        };
    }

    private static EquipmentState DuplicateEquipment(EquipmentState equipment)
    {
        if (equipment == null)
        {
            return null;
        }
        EquipmentState copy = new();
        foreach (StringName slotId in equipment.get_entry_slot_ids())
        {
            EquipmentEntryState entry = equipment.get_entry(slotId);
            if (entry == null || entry.is_empty())
            {
                continue;
            }
            copy.set_equipped_entry(
                slotId,
                entry.item_id,
                DuplicateStringNameArray(entry.occupied_slot_ids),
                DuplicateEquipmentInstance(entry.equipment_instance)
            );
        }
        return copy;
    }

    private static EquipmentEntryState DuplicateEquipmentEntry(EquipmentEntryState entry)
    {
        if (entry == null)
        {
            return null;
        }
        return new EquipmentEntryState
        {
            item_id = entry.item_id,
            occupied_slot_ids = DuplicateStringNameArray(entry.occupied_slot_ids),
            instance_id = entry.instance_id,
            equipment_instance = DuplicateEquipmentInstance(entry.equipment_instance),
        };
    }

    private static EquipmentInstanceState DuplicateEquipmentInstance(EquipmentInstanceState instance)
    {
        if (instance == null)
        {
            return null;
        }
        return new EquipmentInstanceState
        {
            instance_id = instance.instance_id,
            item_id = instance.item_id,
            rarity = instance.rarity,
            current_durability = instance.current_durability,
        };
    }

    private static Godot.Collections.Array<StringName> DuplicateStringNameArray(
        Godot.Collections.Array<StringName> values
    )
    {
        Godot.Collections.Array<StringName> result = new();
        foreach (StringName value in values ?? new Godot.Collections.Array<StringName>())
        {
            result.Add(value);
        }
        return result;
    }

    private static List<StringName> StringNameArrayToList(
        Godot.Collections.Array<StringName> values
    )
    {
        List<StringName> result = new();
        foreach (StringName value in values ?? new Godot.Collections.Array<StringName>())
        {
            result.Add(value);
        }
        return result;
    }

    private static List<Vector2I> Vector2IArrayToList(Godot.Collections.Array<Vector2I> values)
    {
        List<Vector2I> result = new();
        foreach (Vector2I value in values ?? new Godot.Collections.Array<Vector2I>())
        {
            result.Add(value);
        }
        return result;
    }

    private static List<string> StringArrayToList(Godot.Collections.Array<string> values)
    {
        List<string> result = new();
        foreach (string value in values ?? new Godot.Collections.Array<string>())
        {
            result.Add(value ?? "");
        }
        return result;
    }

    private static List<RestorableMap> DictionaryArrayToRestorableMaps(
        Godot.Collections.Array<GDictionary> values
    )
    {
        List<RestorableMap> result = new();
        if (values == null)
        {
            return result;
        }
        foreach (GDictionary value in values)
        {
            result.Add(RestorableMap.FromGodotDictionary(value));
        }
        return result;
    }

    private static Godot.Collections.Array<StringName> BuildStringNameArray(
        IEnumerable<StringName> values
    )
    {
        Godot.Collections.Array<StringName> result = new();
        foreach (StringName value in values ?? System.Array.Empty<StringName>())
        {
            result.Add(value);
        }
        return result;
    }

    private static Godot.Collections.Array<Vector2I> BuildVector2IArray(
        IEnumerable<Vector2I> values
    )
    {
        Godot.Collections.Array<Vector2I> result = new();
        foreach (Vector2I value in values ?? System.Array.Empty<Vector2I>())
        {
            result.Add(value);
        }
        return result;
    }

    private static Godot.Collections.Array<string> BuildStringArray(IEnumerable<string> values)
    {
        Godot.Collections.Array<string> result = new();
        foreach (string value in values ?? System.Array.Empty<string>())
        {
            result.Add(value ?? "");
        }
        return result;
    }

    private static Godot.Collections.Array<GDictionary> BuildDictionaryArray(
        IEnumerable<RestorableMap> values
    )
    {
        Godot.Collections.Array<GDictionary> result = new();
        foreach (RestorableMap value in values ?? System.Array.Empty<RestorableMap>())
        {
            result.Add(value?.ToGodotDictionary() ?? new GDictionary());
        }
        return result;
    }

    private static List<StableValue> StableStringNameList(IEnumerable<StringName> values)
    {
        List<StableValue> result = new();
        foreach (StringName value in values ?? System.Array.Empty<StringName>())
        {
            result.Add(StableValue.FromText(value.ToString()));
        }
        return result;
    }

    private static List<StableValue> StableVector2IList(IEnumerable<Vector2I> values)
    {
        List<StableValue> result = new();
        foreach (Vector2I value in values ?? System.Array.Empty<Vector2I>())
        {
            result.Add(StableValue.FromVector2I(value));
        }
        return result;
    }

    private static List<StableValue> StableTextList(IEnumerable<string> values)
    {
        List<StableValue> result = new();
        foreach (string value in values ?? System.Array.Empty<string>())
        {
            result.Add(StableValue.FromText(value));
        }
        return result;
    }

    private static List<StableValue> StableRestorableMapList(IEnumerable<RestorableMap> values)
    {
        List<StableValue> result = new();
        foreach (RestorableMap value in values ?? System.Array.Empty<RestorableMap>())
        {
            result.Add(StableValue.FromMap(value?.ToStableMap() ?? new StableMap()));
        }
        return result;
    }

    private void ApplyAllowedAiBookkeeping(StableMap expectedStable, StableMap afterStable)
    {
        if (
            !expectedStable.TryGetMap("units", out StableMap expectedUnits)
            || !afterStable.TryGetMap("units", out StableMap afterUnits)
        )
        {
            return;
        }

        string activeKey = StableKey(_active_unit_id);
        if (
            !expectedUnits.TryGetMap(activeKey, out StableMap expectedUnit)
            || !afterUnits.TryGetMap(activeKey, out StableMap afterUnit)
        )
        {
            return;
        }
        if (
            !expectedUnit.TryGetMap("fields", out StableMap expectedFields)
            || !afterUnit.TryGetMap("fields", out StableMap afterFields)
        )
        {
            return;
        }

        foreach (string fieldName in AllowedActiveUnitFields)
        {
            if (afterFields.TryGet(fieldName, out StableValue fieldValue))
            {
                expectedFields.Set(fieldName, fieldValue.Clone());
            }
        }

        StableMap expectedBlackboard = expectedFields.GetMapOrEmpty("ai_blackboard");
        StableMap afterBlackboard = afterFields.GetMapOrEmpty("ai_blackboard");
        foreach (string key in AllowedActiveBlackboardKeys)
        {
            if (afterBlackboard.TryGet(key, out StableValue blackboardValue))
            {
                expectedBlackboard.Set(key, blackboardValue.Clone());
            }
            else
            {
                expectedBlackboard.Remove(key);
            }
        }

        expectedFields.Set("ai_blackboard", StableValue.FromMap(expectedBlackboard));
        expectedUnit.Set("fields", StableValue.FromMap(expectedFields));
        expectedUnits.Set(activeKey, StableValue.FromMap(expectedUnit));
        expectedStable.Set("units", StableValue.FromMap(expectedUnits));
    }

    private static string StableKey(string text)
    {
        return text ?? "";
    }

    private static string StableKey(StringName stringName)
    {
        return stringName.ToString();
    }

    private static string StableKey(Vector2I coord)
    {
        return $"Vector2i({coord.X},{coord.Y})";
    }

    private static string StableKeyFromGodotKey(Variant key)
    {
        if (key.VariantType == Variant.Type.String)
        {
            return StableKey(key.AsString());
        }
        if (key.VariantType == Variant.Type.StringName)
        {
            return StableKey(key.AsStringName());
        }
        if (key.VariantType == Variant.Type.Vector2I)
        {
            return StableKey(key.AsVector2I());
        }
        return $"type{(int)key.VariantType}({key})";
    }

    private enum StableDiffKind
    {
        Removed,
        Added,
        Changed,
        SizeChanged,
    }

    private readonly struct StableDiff
    {
        private StableDiff(
            StableDiffKind kind,
            string path,
            StableValue expected = null,
            StableValue actual = null,
            int expectedCount = 0,
            int actualCount = 0
        )
        {
            Kind = kind;
            Path = path ?? "";
            Expected = expected;
            Actual = actual;
            ExpectedCount = expectedCount;
            ActualCount = actualCount;
        }

        public StableDiffKind Kind { get; }

        public string Path { get; }

        public StableValue Expected { get; }

        public StableValue Actual { get; }

        public int ExpectedCount { get; }

        public int ActualCount { get; }

        public static StableDiff Removed(string path) =>
            new(StableDiffKind.Removed, path);

        public static StableDiff Added(string path, StableValue actual) =>
            new(StableDiffKind.Added, path, actual: actual ?? StableValue.Nil());

        public static StableDiff Changed(string path, StableValue expected, StableValue actual) =>
            new(
                StableDiffKind.Changed,
                path,
                expected ?? StableValue.Nil(),
                actual ?? StableValue.Nil()
            );

        public static StableDiff SizeChanged(string path, int expectedCount, int actualCount) =>
            new(
                StableDiffKind.SizeChanged,
                path,
                expectedCount: expectedCount,
                actualCount: actualCount
            );

        public string Format()
        {
            return Kind switch
            {
                StableDiffKind.Removed => $"{Path} was removed",
                StableDiffKind.Added => $"{Path} was added with {Actual.Format()}",
                StableDiffKind.Changed
                    => $"{Path} changed from {Expected.Format()} to {Actual.Format()}",
                StableDiffKind.SizeChanged
                    => $"{Path} size changed from {ExpectedCount} to {ActualCount}",
                _ => Path,
            };
        }
    }

    private static void CollectDiffs(
        StableMap expected,
        StableMap actual,
        string path,
        List<StableDiff> diffs
    )
    {
        CollectDictionaryDiffs(expected, actual, path, diffs);
    }

    private static void CollectDiffs(
        StableValue expected,
        StableValue actual,
        string path,
        List<StableDiff> diffs
    )
    {
        if (diffs.Count >= MaxReportedViolations)
        {
            return;
        }
        if (expected.TryGetMap(out StableMap expectedMap) && actual.TryGetMap(out StableMap actualMap))
        {
            CollectDictionaryDiffs(expectedMap, actualMap, path, diffs);
            return;
        }
        if (
            expected.TryGetArray(out IReadOnlyList<StableValue> expectedArray)
            && actual.TryGetArray(out IReadOnlyList<StableValue> actualArray)
        )
        {
            CollectArrayDiffs(expectedArray, actualArray, path, diffs);
            return;
        }
        if (!expected.ScalarEquals(actual))
        {
            diffs.Add(StableDiff.Changed(path, expected, actual));
        }
    }

    private static void CollectDictionaryDiffs(
        StableMap expected,
        StableMap actual,
        string path,
        List<StableDiff> diffs
    )
    {
        foreach (KeyValuePair<string, StableValue> entry in expected.Entries)
        {
            if (diffs.Count >= MaxReportedViolations)
            {
                return;
            }
            string childPath = $"{path}.{entry.Key}";
            if (!actual.TryGet(entry.Key, out StableValue actualValue))
            {
                diffs.Add(StableDiff.Removed(childPath));
                continue;
            }
            CollectDiffs(entry.Value, actualValue, childPath, diffs);
        }

        foreach (KeyValuePair<string, StableValue> entry in actual.Entries)
        {
            if (diffs.Count >= MaxReportedViolations)
            {
                return;
            }
            if (expected.ContainsKey(entry.Key))
            {
                continue;
            }
            diffs.Add(StableDiff.Added($"{path}.{entry.Key}", entry.Value));
        }
    }

    private static void CollectArrayDiffs(
        IReadOnlyList<StableValue> expected,
        IReadOnlyList<StableValue> actual,
        string path,
        List<StableDiff> diffs
    )
    {
        if (expected.Count != actual.Count)
        {
            diffs.Add(StableDiff.SizeChanged(path, expected.Count, actual.Count));
            return;
        }
        for (int i = 0; i < expected.Count; i += 1)
        {
            if (diffs.Count >= MaxReportedViolations)
            {
                return;
            }
            CollectDiffs(expected[i], actual[i], $"{path}[{i}]", diffs);
        }
    }

    private static List<string> FormatStableDiffs(IEnumerable<StableDiff> diffs)
    {
        List<string> result = new();
        foreach (StableDiff diff in diffs ?? System.Array.Empty<StableDiff>())
        {
            result.Add(diff.Format());
        }
        return result;
    }

    internal static GArray ToViolationArray(IEnumerable<string> violations)
    {
        GArray result = new();
        if (violations == null)
        {
            return result;
        }
        foreach (string violation in violations)
        {
            result.Add(violation);
        }
        return result;
    }

    private enum StableValueKind
    {
        Nil,
        Bool,
        Integer,
        Float,
        Text,
        Vector2I,
        Vector2,
        Vector3I,
        Vector3,
        ObjectId,
        Fallback,
        Map,
        Array,
    }

    private sealed class StableMap
    {
        private readonly Dictionary<string, StableValue> _entries = new(StringComparer.Ordinal);

        public IEnumerable<KeyValuePair<string, StableValue>> Entries => _entries;

        public static StableMap FromGodotDictionary(GDictionary source)
        {
            StableMap result = new();
            if (source == null)
            {
                return result;
            }

            foreach (StableDictionaryEntry entry in ReadStableDictionaryEntries(source))
            {
                result.Set(entry.Key, entry.Value);
            }
            return result;
        }

        public StableMap Clone()
        {
            StableMap clone = new();
            foreach (KeyValuePair<string, StableValue> entry in _entries)
            {
                clone.Set(entry.Key, entry.Value.Clone());
            }
            return clone;
        }

        public bool ContainsKey(string key) => _entries.ContainsKey(key);

        public void CopyFrom(StableMap source)
        {
            _entries.Clear();
            if (source == null)
            {
                return;
            }
            foreach (KeyValuePair<string, StableValue> entry in source._entries)
            {
                Set(entry.Key, entry.Value.Clone());
            }
        }

        public bool Remove(string key) => _entries.Remove(key);

        public void Set(string key, StableValue value)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }
            _entries[key] = value ?? StableValue.Nil();
        }

        public bool TryGet(string key, out StableValue value) => _entries.TryGetValue(key, out value);

        public bool TryGetMap(string key, out StableMap map)
        {
            if (_entries.TryGetValue(key, out StableValue value) && value.TryGetMap(out map))
            {
                return true;
            }
            map = null;
            return false;
        }

        public StableMap GetMapOrEmpty(string key)
        {
            return TryGetMap(key, out StableMap map) ? map : new StableMap();
        }
    }

    private sealed class StableValue
    {
        private readonly StableValueKind _kind;
        private readonly bool _boolValue;
        private readonly long _integerValue;
        private readonly double _floatValue;
        private readonly string _textValue;
        private readonly Vector2I _vector2IValue;
        private readonly Vector2 _vector2Value;
        private readonly Vector3I _vector3IValue;
        private readonly Vector3 _vector3Value;
        private readonly StableMap _mapValue;
        private readonly List<StableValue> _arrayValue;

        private StableValue(
            StableValueKind kind,
            bool boolValue = false,
            long integerValue = 0,
            double floatValue = 0.0,
            string textValue = "",
            Vector2I vector2IValue = default,
            Vector2 vector2Value = default,
            Vector3I vector3IValue = default,
            Vector3 vector3Value = default,
            StableMap mapValue = null,
            List<StableValue> arrayValue = null
        )
        {
            _kind = kind;
            _boolValue = boolValue;
            _integerValue = integerValue;
            _floatValue = floatValue;
            _textValue = textValue ?? "";
            _vector2IValue = vector2IValue;
            _vector2Value = vector2Value;
            _vector3IValue = vector3IValue;
            _vector3Value = vector3Value;
            _mapValue = mapValue;
            _arrayValue = arrayValue;
        }

        public static StableValue Nil() => new(StableValueKind.Nil);

        public static StableValue FromMap(StableMap value) =>
            new(StableValueKind.Map, mapValue: value ?? new StableMap());

        public static StableValue FromGodotValue(Variant rawValue) => FromVariant(rawValue);

        public StableValue Clone()
        {
            if (_kind == StableValueKind.Map)
            {
                return FromMap(_mapValue.Clone());
            }
            if (_kind == StableValueKind.Array)
            {
                List<StableValue> items = new();
                foreach (StableValue item in _arrayValue)
                {
                    items.Add(item.Clone());
                }
                return FromArray(items);
            }
            return this;
        }

        public bool TryGetMap(out StableMap value)
        {
            if (_kind == StableValueKind.Map)
            {
                value = _mapValue;
                return true;
            }
            value = null;
            return false;
        }

        public bool TryGetArray(out IReadOnlyList<StableValue> value)
        {
            if (_kind == StableValueKind.Array)
            {
                value = _arrayValue;
                return true;
            }
            value = null;
            return false;
        }

        public bool ScalarEquals(StableValue other)
        {
            if (other == null || _kind != other._kind)
            {
                return false;
            }
            return _kind switch
            {
                StableValueKind.Nil => true,
                StableValueKind.Bool => _boolValue == other._boolValue,
                StableValueKind.Integer => _integerValue == other._integerValue,
                StableValueKind.Float
                    => Mathf.IsEqualApprox((float)_floatValue, (float)other._floatValue),
                StableValueKind.Text => _textValue == other._textValue,
                StableValueKind.Vector2I => _vector2IValue == other._vector2IValue,
                StableValueKind.Vector2 => _vector2Value == other._vector2Value,
                StableValueKind.Vector3I => _vector3IValue == other._vector3IValue,
                StableValueKind.Vector3 => _vector3Value == other._vector3Value,
                StableValueKind.ObjectId => _integerValue == other._integerValue,
                StableValueKind.Fallback => _textValue == other._textValue,
                _ => false,
            };
        }

        public string Format()
        {
            return _kind switch
            {
                StableValueKind.Nil => "null",
                StableValueKind.Bool => _boolValue ? "true" : "false",
                StableValueKind.Integer => _integerValue.ToString(CultureInfo.InvariantCulture),
                StableValueKind.Float => _floatValue.ToString("R", CultureInfo.InvariantCulture),
                StableValueKind.Text => _textValue ?? "",
                StableValueKind.Vector2I
                    => $"Vector2i({_vector2IValue.X},{_vector2IValue.Y})",
                StableValueKind.Vector2
                    => $"Vector2({_vector2Value.X.ToString("R", CultureInfo.InvariantCulture)},{_vector2Value.Y.ToString("R", CultureInfo.InvariantCulture)})",
                StableValueKind.Vector3I
                    => $"Vector3i({_vector3IValue.X},{_vector3IValue.Y},{_vector3IValue.Z})",
                StableValueKind.Vector3
                    => $"Vector3({_vector3Value.X.ToString("R", CultureInfo.InvariantCulture)},{_vector3Value.Y.ToString("R", CultureInfo.InvariantCulture)},{_vector3Value.Z.ToString("R", CultureInfo.InvariantCulture)})",
                StableValueKind.ObjectId
                    => $"Object#{_integerValue.ToString(CultureInfo.InvariantCulture)}",
                StableValueKind.Fallback => _textValue ?? "",
                StableValueKind.Map => "{...}",
                StableValueKind.Array => "[...]",
                _ => "",
            };
        }

        private static StableValue FromVariant(Variant value)
        {
            return value.VariantType switch
            {
                Variant.Type.Nil => Nil(),
                Variant.Type.Bool => FromBool(value.AsBool()),
                Variant.Type.Int => FromInteger(value.AsInt64()),
                Variant.Type.Float => FromFloat(value.AsDouble()),
                Variant.Type.String => FromText(value.AsString()),
                Variant.Type.StringName => FromText(value.AsStringName().ToString()),
                Variant.Type.Vector2I => FromVector2I(value.AsVector2I()),
                Variant.Type.Vector2 => FromVector2(value.AsVector2()),
                Variant.Type.Vector3I => FromVector3I(value.AsVector3I()),
                Variant.Type.Vector3 => FromVector3(value.AsVector3()),
                Variant.Type.Dictionary => FromMap(StableMap.FromGodotDictionary(value.AsGodotDictionary())),
                Variant.Type.Array => FromGodotArray(value.AsGodotArray()),
                Variant.Type.Object => FromGodotObject(value.AsGodotObject()),
                _ => FromFallback($"type{(int)value.VariantType}", value.ToString()),
            };
        }

        public static StableValue FromVariantValue(Variant value) => FromVariant(value);

        private static StableValue FromGodotObject(GodotObject value)
        {
            if (value == null)
            {
                return Nil();
            }
            return value switch
            {
                BattleUnitState unitState => FromMap(StableBattleUnitState(unitState)),
                BattleCellState cellState => FromMap(StableBattleCell(cellState)),
                BattleStatusEffectState statusEffectState => FromMap(
                    StableStatusEffect(statusEffectState)
                ),
                BattleTimelineState timelineState => FromMap(StableTimeline(timelineState)),
                BattleTerrainEffectState terrainEffectState => FromMap(
                    StableTerrainEffect(terrainEffectState)
                ),
                BattleEdgeFeatureState edgeFeatureState => FromMap(
                    StableEdgeFeature(edgeFeatureState)
                ),
                AttributeSnapshot attributeSnapshot => FromMap(StableAttributeSnapshot(attributeSnapshot)),
                EquipmentState equipmentState => FromMap(StableEquipment(equipmentState)),
                EquipmentEntryState equipmentEntryState => FromMap(
                    StableEquipmentEntry(equipmentEntryState)
                ),
                EquipmentInstanceState equipmentInstanceState => FromMap(
                    StableEquipmentInstance(equipmentInstanceState)
                ),
                WarehouseState warehouseState => FromMap(StableWarehouse(warehouseState)),
                WarehouseStackState warehouseStackState => FromMap(
                    StableWarehouseStack(warehouseStackState)
                ),
                WeaponProjection weaponProjection => FromMap(StableWeaponProjection(weaponProjection)),
                WeaponDice weaponDice => FromMap(StableWeaponDice(weaponDice)),
                Resource resource => FromObjectId((long)resource.GetInstanceId()),
                _ => FromObjectId((long)value.GetInstanceId()),
            };
        }

        private static StableValue FromGodotArray(GArray source)
        {
            return FromArray(ReadStableArrayValues(source));
        }

        public static StableValue FromArray(List<StableValue> value) =>
            new(StableValueKind.Array, arrayValue: value ?? new List<StableValue>());

        public static StableValue FromBool(bool value) =>
            new(StableValueKind.Bool, boolValue: value);

        public static StableValue FromInteger(long value) =>
            new(StableValueKind.Integer, integerValue: value);

        public static StableValue FromFloat(double value) =>
            new(StableValueKind.Float, floatValue: value);

        public static StableValue FromText(string value) =>
            new(StableValueKind.Text, textValue: value ?? "");

        public static StableValue FromVector2I(Vector2I value) =>
            new(StableValueKind.Vector2I, vector2IValue: value);

        public static StableValue FromVector2(Vector2 value) =>
            new(StableValueKind.Vector2, vector2Value: value);

        public static StableValue FromVector3I(Vector3I value) =>
            new(StableValueKind.Vector3I, vector3IValue: value);

        public static StableValue FromVector3(Vector3 value) =>
            new(StableValueKind.Vector3, vector3Value: value);

        public static StableValue FromObjectId(long value) =>
            new(StableValueKind.ObjectId, integerValue: value);

        private static StableValue FromFallback(string typeName, string value) =>
            new(StableValueKind.Fallback, textValue: $"{typeName}({value ?? ""})");
    }

    private delegate bool GodotValueReader<T>(Variant rawValue, out T value);

    private readonly struct GodotDictionaryEntry<TKey, TValue>
    {
        public GodotDictionaryEntry(TKey key, TValue value)
        {
            Key = key;
            Value = value;
        }

        public TKey Key { get; }

        public TValue Value { get; }
    }

    private readonly struct RestorableDictionaryEntry
    {
        public RestorableDictionaryEntry(RestorableKey key, RestorableValue value)
        {
            Key = key;
            Value = value;
        }

        public RestorableKey Key { get; }

        public RestorableValue Value { get; }
    }

    private readonly struct StableDictionaryEntry
    {
        public StableDictionaryEntry(string key, StableValue value)
        {
            Key = key ?? "";
            Value = value ?? StableValue.Nil();
        }

        public string Key { get; }

        public StableValue Value { get; }
    }

    private readonly struct StatusEffectDictionaryEntry
    {
        public StatusEffectDictionaryEntry(
            StringName statusId,
            BattleStatusEffectState effect,
            bool isTyped
        )
        {
            StatusId = statusId;
            Effect = effect;
            IsTyped = isTyped;
        }

        public StringName StatusId { get; }

        public BattleStatusEffectState Effect { get; }

        public bool IsTyped { get; }
    }

    private static List<GodotDictionaryEntry<TKey, TValue>> ReadGodotDictionaryEntries<
        TKey,
        TValue
    >(
        GDictionary source,
        GodotValueReader<TKey> readKey,
        GodotValueReader<TValue> readValue
    )
    {
        List<GodotDictionaryEntry<TKey, TValue>> result = new();
        if (source == null || readKey == null || readValue == null)
        {
            return result;
        }

        foreach (var rawKey in source.Keys)
        {
            if (
                readKey(rawKey, out TKey key)
                && TryGetDictionaryValue(source, rawKey, out Variant rawValue)
                && readValue(rawValue, out TValue value)
            )
            {
                result.Add(new GodotDictionaryEntry<TKey, TValue>(key, value));
            }
        }
        return result;
    }

    private static List<RestorableDictionaryEntry> ReadRestorableDictionaryEntries(
        GDictionary source
    )
    {
        List<RestorableDictionaryEntry> result = new();
        if (source == null)
        {
            return result;
        }

        foreach (var rawKey in source.Keys)
        {
            if (!TryGetDictionaryValue(source, rawKey, out Variant rawValue))
            {
                continue;
            }
            result.Add(
                new RestorableDictionaryEntry(
                    RestorableKey.FromGodotKey(rawKey),
                    RestorableValue.FromGodotValue(rawValue)
                )
            );
        }
        return result;
    }

    private static List<StableDictionaryEntry> ReadStableDictionaryEntries(GDictionary source)
    {
        List<StableDictionaryEntry> result = new();
        if (source == null)
        {
            return result;
        }

        foreach (var rawKey in source.Keys)
        {
            string stableKey = StableKeyFromGodotKey(rawKey);
            if (stableKey == "unit_ref")
            {
                continue;
            }
            if (!TryGetDictionaryValue(source, rawKey, out Variant rawValue))
            {
                continue;
            }
            result.Add(new StableDictionaryEntry(stableKey, StableValue.FromGodotValue(rawValue)));
        }
        return result;
    }

    private static List<StatusEffectDictionaryEntry> ReadStatusEffectDictionaryEntries(
        GDictionary source
    )
    {
        List<StatusEffectDictionaryEntry> result = new();
        if (source == null)
        {
            return result;
        }

        foreach (var rawStatusId in source.Keys)
        {
            if (
                !TryAsStringName(rawStatusId, out StringName statusId)
                || !TryGetDictionaryValue(source, rawStatusId, out Variant rawEffect)
            )
            {
                continue;
            }

            if (TryAsObject(rawEffect, out BattleStatusEffectState typedEffect))
            {
                result.Add(new StatusEffectDictionaryEntry(statusId, typedEffect, true));
                continue;
            }

            BattleStatusEffectState effect =
                TryAsDictionary(rawEffect, out GDictionary effectData)
                    ? BattleStatusEffectState.from_dict(effectData)
                    : null;
            result.Add(new StatusEffectDictionaryEntry(statusId, effect, false));
        }
        return result;
    }

    private static List<RestorableValue> ReadRestorableArrayValues(GArray source)
    {
        List<RestorableValue> result = new();
        if (source == null)
        {
            return result;
        }
        foreach (var item in source)
        {
            result.Add(RestorableValue.FromGodotValue(item));
        }
        return result;
    }

    private static List<StableValue> ReadStableArrayValues(GArray source)
    {
        List<StableValue> result = new();
        if (source == null)
        {
            return result;
        }
        foreach (var item in source)
        {
            result.Add(StableValue.FromGodotValue(item));
        }
        return result;
    }

    private static bool TryAsStringName(Variant rawValue, out StringName value)
    {
        if (rawValue.VariantType == Variant.Type.StringName)
        {
            value = rawValue.AsStringName();
            return true;
        }
        if (rawValue.VariantType == Variant.Type.String)
        {
            value = new StringName(rawValue.AsString());
            return true;
        }
        value = default;
        return false;
    }

    private static bool TryAsBattleCellState(Variant rawValue, out BattleCellState value)
    {
        return TryAsObject(rawValue, out value);
    }

    private static bool TryAsBattleUnitState(Variant rawValue, out BattleUnitState value)
    {
        return TryAsObject(rawValue, out value);
    }

    private static bool TryAsSkillDef(Variant rawValue, out SkillDef value)
    {
        return TryAsObject(rawValue, out value);
    }

    private static bool TryAsCellColumn(Variant rawValue, out List<BattleCellState> value)
    {
        value = new List<BattleCellState>();
        if (!TryAsArray(rawValue, out GArray column))
        {
            return false;
        }
        foreach (var rawCell in column)
        {
            if (TryAsObject(rawCell, out BattleCellState cell))
            {
                value.Add(cell);
            }
        }
        return true;
    }

    private static bool TryGetDictionaryValue(GDictionary dictionary, Variant key, out Variant value)
    {
        if (dictionary == null)
        {
            value = default;
            return false;
        }
        if (dictionary.ContainsKey(key))
        {
            value = dictionary[key];
            return true;
        }
        if (key.VariantType == Variant.Type.StringName)
        {
            StringName stringNameKey = key.AsStringName();
            if (dictionary.ContainsKey(stringNameKey))
            {
                value = dictionary[stringNameKey];
                return true;
            }
            string stringKey = stringNameKey.ToString();
            if (dictionary.ContainsKey(stringKey))
            {
                value = dictionary[stringKey];
                return true;
            }
            value = default;
            return false;
        }
        if (key.VariantType == Variant.Type.String)
        {
            string textKey = key.AsString();
            if (dictionary.ContainsKey(textKey))
            {
                value = dictionary[textKey];
                return true;
            }
            var textStringNameKey = new StringName(textKey);
            if (dictionary.ContainsKey(textStringNameKey))
            {
                value = dictionary[textStringNameKey];
                return true;
            }
            value = default;
            return false;
        }
        if (key.VariantType == Variant.Type.Vector2I)
        {
            Vector2I coordKey = key.AsVector2I();
            if (dictionary.ContainsKey(coordKey))
            {
                value = dictionary[coordKey];
                return true;
            }
            value = default;
            return false;
        }
        value = default;
        return false;
    }

    private static void SetDictionaryValue(GDictionary dictionary, Variant key, Variant value)
    {
        if (dictionary == null || key.VariantType == Variant.Type.Nil)
        {
            return;
        }
        dictionary[key] = value;
    }

    private static void RemoveStringNameDictionaryKey(GDictionary dictionary, StringName key)
    {
        if (dictionary == null)
        {
            return;
        }
        dictionary.Remove(key);
        dictionary.Remove(key.ToString());
    }

    private static bool TryAsObject<T>(Variant rawValue, out T value)
        where T : GodotObject
    {
        if (rawValue.VariantType != Variant.Type.Object)
        {
            value = null;
            return false;
        }
        value = rawValue.AsGodotObject() as T;
        return value != null;
    }

    private static bool TryAsDictionary(Variant rawValue, out GDictionary value)
    {
        if (rawValue.VariantType == Variant.Type.Dictionary)
        {
            value = rawValue.AsGodotDictionary();
            return true;
        }
        value = new GDictionary();
        return false;
    }

    private static bool TryAsArray(Variant rawValue, out GArray value)
    {
        if (rawValue.VariantType == Variant.Type.Array)
        {
            value = rawValue.AsGodotArray();
            return true;
        }
        value = new GArray();
        return false;
    }

    private static bool TryAsVector2I(Variant rawValue, out Vector2I value)
    {
        if (rawValue.VariantType != Variant.Type.Vector2I)
        {
            value = Vector2I.Zero;
            return false;
        }
        value = rawValue.AsVector2I();
        return true;
    }

    private static bool TryAsInt(Variant rawValue, out int value)
    {
        if (rawValue.VariantType == Variant.Type.Nil)
        {
            value = 0;
            return false;
        }
        value = rawValue.AsInt32();
        return true;
    }

    private static bool TryGetContextState(
        BattleAiContext context,
        out BattleState state,
        out BattleUnitState unitState
    )
    {
        state = context?.state;
        unitState = context?.unit_state;
        return context != null && state != null && unitState != null;
    }
}
