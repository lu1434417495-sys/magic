using System;
using System.Collections.Generic;

public partial class BattleMapPanel
{
    internal event Action MovementPlaybackFinished;
    internal bool IsMovementPlaying => _battle_board?._controller?.IsMovementPlaying == true;

    internal bool PlayMovements(IReadOnlyList<BattleMovementPresentation> movements)
    {
        if (!Visible || IsLoadingBattle() || _battle_board?._controller == null
            || !_battle_board._controller.PlayMovements(movements))
            return false;
        ClearHoverPreview();
        _battle_board.ClearHover();
        _battle_board.UpdateSelection(new(-1, -1));
        hint_label.Text = "移动中…";
        _request_map_viewport_update();
        return true;
    }

    internal void CancelMovementPlayback() => _battle_board?._controller?.CancelMovement();

    private void ProcessMovementPlayback(double delta)
    {
        if (!IsMovementPlaying)
            return;
        _battle_board._controller.AdvanceMovement(delta);
        _request_map_viewport_update();
        if (!IsMovementPlaying)
            MovementPlaybackFinished?.Invoke();
    }
}
