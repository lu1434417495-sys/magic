using Godot;
using System.Collections.Generic;

public class BattleCommand
{
    public StringName command_type = "";
    public StringName unit_id = "";
    public StringName skill_id = "";
    public StringName skill_variant_id = "";
    public StringName target_unit_id = "";
    private readonly List<StringName> _targetUnitIds = new();
    public Vector2I target_coord = new(-1, -1);
    private readonly List<Vector2I> _targetCoords = new();
    public StringName equipment_operation = "";
    public StringName equipment_slot_id = "";
    public StringName equipment_item_id = "";
    public StringName equipment_instance_id = "";
    public EquipmentInstanceState equipment_instance;
    private readonly List<StringName> _equipmentOccupiedSlotIds = new();

    public Godot.Collections.Array<Vector2I> target_coords
    {
        get => new Godot.Collections.Array<Vector2I>(_targetCoords);
        set => SetTargetCoords(value);
    }

    public Godot.Collections.Array<StringName> target_unit_ids
    {
        get => new Godot.Collections.Array<StringName>(_targetUnitIds);
        set => SetTargetUnitIds(value);
    }

    public Godot.Collections.Array<StringName> equipment_occupied_slot_ids
    {
        get => new Godot.Collections.Array<StringName>(_equipmentOccupiedSlotIds);
        set => SetEquipmentOccupiedSlotIds(value);
    }

    internal IReadOnlyList<StringName> EquipmentOccupiedSlotIdsTyped => _equipmentOccupiedSlotIds;
    internal IReadOnlyList<StringName> TargetUnitIdsTyped => _targetUnitIds;
    internal IReadOnlyList<Vector2I> TargetCoordsTyped => _targetCoords;
    internal BattleCommandKind CommandKind
    {
        get => BattleTypedNames.ToCommandKind(command_type);
        set => command_type = BattleTypedNames.ToStringName(value);
    }
    internal BattleEquipmentOperationKind EquipmentOperationKind
    {
        get => BattleTypedNames.ToEquipmentOperationKind(equipment_operation);
        set => equipment_operation = BattleTypedNames.ToStringName(value);
    }

    public bool IsMove() => CommandKind == BattleCommandKind.Move;

    public bool IsSkill() => CommandKind == BattleCommandKind.Skill;

    public bool IsWait() => CommandKind == BattleCommandKind.Wait;

    public bool IsChangeEquipment() => CommandKind == BattleCommandKind.ChangeEquipment;

    public bool IsCancelCast() => CommandKind == BattleCommandKind.CancelCast;

    internal void SetEquipmentOccupiedSlotIds(IEnumerable<StringName> values)
    {
        _equipmentOccupiedSlotIds.Clear();
        if (values == null)
        {
            return;
        }
        foreach (StringName value in values)
        {
            _equipmentOccupiedSlotIds.Add(ProgressionDataUtils.to_string_name(value));
        }
    }

    internal void SetTargetUnitIds(IEnumerable<StringName> values)
    {
        _targetUnitIds.Clear();
        if (values == null)
        {
            return;
        }
        foreach (StringName value in values)
        {
            _targetUnitIds.Add(ProgressionDataUtils.to_string_name(value));
        }
    }

    internal void ClearTargetUnitIds()
    {
        _targetUnitIds.Clear();
    }

    internal void AddTargetUnitId(StringName value)
    {
        _targetUnitIds.Add(ProgressionDataUtils.to_string_name(value));
    }

    internal void SetTargetCoords(IEnumerable<Vector2I> values)
    {
        _targetCoords.Clear();
        if (values == null)
        {
            return;
        }
        foreach (Vector2I value in values)
        {
            _targetCoords.Add(value);
        }
    }

    internal void ClearTargetCoords()
    {
        _targetCoords.Clear();
    }

    internal void AddTargetCoord(Vector2I value)
    {
        _targetCoords.Add(value);
    }
}
