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
        hero.SetAnchorCoord(new Vector2I(5, 6));
        hero.RestoreGeometryForMutationSnapshotExact(
            BattleUnitGeometrySnapshot.Present(
                hero.GetAnchorCoord(),
                BattleUnitState.BodySizeMedium,
                new StringName("medium"),
                new Vector2I(2, 2),
                new Vector2IList { new Vector2I(99, 99) }
            )
        );
        BattleUnitState missingGeometry = new() { unit_id = "missing_geometry" };
        missingGeometry.RestoreGeometryForMutationSnapshotExact(
            BattleUnitGeometrySnapshot.MissingOwner
        );
        BattleUnitState missingId = new();
        state.SetUnits(new[] { hero, missingGeometry, null, missingId });

        _test.True(state.MovementGeometryRevision > revisionBefore, "SetUnits 应递增 movement geometry revision。");
        _test.True(state.ContainsUnit("hero"), "SetUnits 应写入有效 unit_id。");
        _test.False(state.ContainsUnit("old"), "SetUnits 应替换旧 unit index。");
        _test.False(state.ContainsUnit(""), "SetUnits 应跳过空 unit_id。");
        _test.True(
            state.ContainsUnit("missing_geometry"),
            "SetUnits 应接纳其余身份有效的 missing-owner unit。"
        );

        BattleUnitReadView view = state.GetUnitView("hero");
        _test.True(view.IsValid, "GetUnitView 应返回有效只读单位视图。");
        _test.Eq(view.UnitId, new StringName("hero"), "GetUnitView 应读取 state 持有的 unit。");
        _test.Eq(hero.GetFootprintSize(), Vector2I.One, "SetUnit admission 应重建派生 footprint_size。");
        _test.Eq(hero.GetOccupiedCoordsReadViewTyped().Count, 1, "SetUnit admission 应重建 occupied_coords。");
        _test.Eq(
            hero.GetOccupiedCoordsReadViewTyped()[0],
            new Vector2I(5, 6),
            "SetUnit admission 应按 authoritative anchor 重建 occupied_coords。"
        );
        BattleUnitGeometryReadView normalizedMissingGeometry =
            missingGeometry.GetGeometryReadViewTyped();
        _test.True(
            normalizedMissingGeometry.OwnerPresent,
            "SetUnit admission 应重建 missing geometry owner。"
        );
        _test.Eq(
            normalizedMissingGeometry.BodySizeCategory,
            new StringName("medium"),
            "重建的 geometry owner 应采用 canonical 默认体型。"
        );
    }
}
