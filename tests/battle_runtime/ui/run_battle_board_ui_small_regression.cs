using System;
using System.Collections.Generic;
using Godot;

public partial class run_battle_board_ui_small_regression : SceneTree
{
    public override void _Initialize()
    {
        int exitCode = 1;
        try
        {
            var failures = new List<string>();
            if (BattleBoardController.GetVariantIndexForTest(int.MinValue, 5) < 0)
            {
                failures.Add("variant index was negative");
            }
            if (BattleMapPanel.BuildTimelineTooltipForTest("Hero", 10, 3).Contains("/n"))
            {
                failures.Add("tooltip contains literal /n");
            }
            if (failures.Count > 0)
            {
                throw new Exception(string.Join("; ", failures));
            }
            GD.Print("Battle board UI small regression: PASS");
            exitCode = 0;
        }
        catch (Exception ex)
        {
            GD.PushError($"Battle board UI small regression failed: {ex}");
        }
        GodotSharpCleanup.CollectPendingFinalizers();
        Quit(exitCode);
    }
}
