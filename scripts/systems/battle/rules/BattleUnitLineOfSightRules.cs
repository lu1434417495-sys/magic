using System;
using Godot;

internal static class BattleUnitLineOfSightRules
{
    internal static bool HasLineOfSight(
        BattleState state,
        BattleGridService gridService,
        Vector2I sourceCoord,
        Vector2I targetCoord
    )
    {
        if (state == null || gridService == null || sourceCoord == targetCoord)
            return true;

        int deltaX = Math.Abs(targetCoord.X - sourceCoord.X);
        int deltaY = Math.Abs(targetCoord.Y - sourceCoord.Y);
        int stepX = Math.Sign(targetCoord.X - sourceCoord.X);
        int stepY = Math.Sign(targetCoord.Y - sourceCoord.Y);
        int doubledX = deltaX * 2;
        int doubledY = deltaY * 2;
        int error = deltaX - deltaY;
        Vector2I current = sourceCoord;
        while (current != targetCoord)
        {
            if (error > 0)
            {
                Vector2I next = current + new Vector2I(stepX, 0);
                if (EdgeBlocksLineOfSight(state, gridService, current, next))
                    return false;
                current = next;
                error -= doubledY;
                continue;
            }
            if (error < 0)
            {
                Vector2I next = current + new Vector2I(0, stepY);
                if (EdgeBlocksLineOfSight(state, gridService, current, next))
                    return false;
                current = next;
                error += doubledX;
                continue;
            }

            Vector2I horizontal = current + new Vector2I(stepX, 0);
            Vector2I vertical = current + new Vector2I(0, stepY);
            if (
                EdgeBlocksLineOfSight(state, gridService, current, horizontal)
                || EdgeBlocksLineOfSight(state, gridService, current, vertical)
            )
            {
                return false;
            }
            current += new Vector2I(stepX, stepY);
            error += doubledX - doubledY;
        }
        return true;
    }

    private static bool EdgeBlocksLineOfSight(
        BattleState state,
        BattleGridService gridService,
        Vector2I fromCoord,
        Vector2I toCoord
    ) => gridService.GetEdgeFace(state, fromCoord, toCoord)?.feature_blocks_los == true;
}
