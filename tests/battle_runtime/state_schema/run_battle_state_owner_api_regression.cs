using Godot;

public partial class run_battle_state_owner_api_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestCellOwnerApiUpdatesCoordAndView();
        TestUnitOwnerApiNormalizesBulkSetAndView();

        RequestTestExit(_test.Finish("Battle state owner API regression"));
    }

    private void TestCellOwnerApiUpdatesCoordAndView()
    {
        BattleState state = new();
        BattleCellState cell = new();
        long revisionBefore = state.MovementGeometryRevision;

        state.SetCell(new Vector2I(3, 4), cell);

        _test.True(state.MovementGeometryRevision > revisionBefore, "SetCell 应递增 movement geometry revision。");
        _test.Eq(cell.coord, new Vector2I(3, 4), "SetCell(coord, cell) 应通过 BattleCellState owner 写入 coord。");

        BattleCellReadView view = state.GetCellView(new Vector2I(3, 4));
        _test.True(view.IsValid, "GetCellView 应返回有效只读格子视图。");
        _test.Eq(view.Coord, new Vector2I(3, 4), "GetCellView 应读取 state 持有的 cell。");
    }

    private void TestUnitOwnerApiNormalizesBulkSetAndView()
    {
        BattleState state = new();
        BattleUnitState oldUnit = new() { unit_id = "old" };
        state.SetUnit(oldUnit);
        long revisionBefore = state.MovementGeometryRevision;

        BattleUnitState hero = new() { unit_id = "hero" };
        BattleUnitState missingId = new();
        state.SetUnits(new[] { hero, null, missingId });

        _test.True(state.MovementGeometryRevision > revisionBefore, "SetUnits 应递增 movement geometry revision。");
        _test.True(state.ContainsUnit("hero"), "SetUnits 应写入有效 unit_id。");
        _test.False(state.ContainsUnit("old"), "SetUnits 应替换旧 unit index。");
        _test.False(state.ContainsUnit(""), "SetUnits 应跳过空 unit_id。");

        BattleUnitReadView view = state.GetUnitView("hero");
        _test.True(view.IsValid, "GetUnitView 应返回有效只读单位视图。");
        _test.Eq(view.UnitId, new StringName("hero"), "GetUnitView 应读取 state 持有的 unit。");
    }
}
