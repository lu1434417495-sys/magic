using System;
using Godot;

public partial class run_battle_board_ui_small_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            if (BattleBoardController.GetVariantIndexForTest(int.MinValue, 5) < 0)
            {
                _test.Fail("variant index was negative");
            }
            if (BattleMapPanel.BuildTimelineTooltipForTest("Hero", 10, 3).Contains("/n"))
            {
                _test.Fail("tooltip contains literal /n");
            }
        }
        catch (Exception ex)
        {
            _test.Fail(ex.ToString());
        }

        Quit(_test.Finish("Battle board UI small regression"));
    }
}
