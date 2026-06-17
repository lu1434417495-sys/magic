using Godot.Collections;

internal static class GameRuntimeBattleWritebackProjection
{
    internal static Dictionary Project(
        GameRuntimeBattleWritebackService.BattleLocalWritebackResult result
    )
    {
        if (result == null)
        {
            return ProjectFailure("battle_local_writeback_missing_result", null);
        }
        return result.Ok
            ? new Dictionary
            {
                ["ok"] = true,
                ["error_code"] = "",
                ["committed_member_count"] = result.CommittedMemberCount,
                ["used_slots"] = result.UsedSlots,
                ["capacity"] = result.Capacity,
            }
            : ProjectFailure(result.ErrorCode, result.Details);
    }

    private static Dictionary ProjectFailure(string errorCode, Dictionary details)
    {
        return new Dictionary
        {
            ["ok"] = false,
            ["error_code"] = errorCode ?? "",
            ["details"] = details != null ? details.Duplicate(true) : new Dictionary(),
        };
    }
}
