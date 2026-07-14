using Godot;

public partial class run_battle_cell_state_owner_api_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestCoordAndOccupantApiOwnCellIdentity();
        TestPassableAndMoveCostApiNormalizeRuntimeValues();

        RequestTestExit(_test.Finish("Battle cell state owner API regression"));
    }

    private void TestCoordAndOccupantApiOwnCellIdentity()
    {
        BattleCellState cell = new();

        cell.SetCoord(new Vector2I(3, 4));
        _test.Eq(cell.coord, new Vector2I(3, 4), "SetCoord 应写格子坐标。");

        cell.SetOccupant("unit_a");
        _test.Eq(cell.occupant_unit_id, new StringName("unit_a"), "SetOccupant 应写 occupant_unit_id。");

        cell.SetOccupant("");
        _test.Eq(cell.occupant_unit_id, new StringName(""), "空 occupant 应清空 occupant_unit_id。");

        cell.SetOccupant("unit_b");
        cell.ClearOccupant();
        _test.Eq(cell.occupant_unit_id, new StringName(""), "ClearOccupant 应清空 occupant_unit_id。");
    }

    private void TestPassableAndMoveCostApiNormalizeRuntimeValues()
    {
        BattleCellState cell = new();

        cell.SetPassable(false);
        _test.False(cell.passable, "SetPassable 应写 passable。");

        cell.SetMoveCost(-10);
        _test.Eq(cell.move_cost, 1, "SetMoveCost 应 clamp 到最小 1。");

        cell.SetMoveCost(4);
        _test.Eq(cell.move_cost, 4, "SetMoveCost 应保留合法移动成本。");
    }
}
