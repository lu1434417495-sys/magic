using System.Collections.Generic;
using Godot.Collections;
using GDictionary = Godot.Collections.Dictionary;

internal static class GameRuntimeBattleWritebackProjection
{
    internal static GodotProjectionLease<GDictionary> ProjectLease(
        GameRuntimeBattleWritebackService.BattleLocalWritebackResult result
    )
    {
        IReadOnlyDictionary<string, object> snapshot = result == null
            ? BuildFailureSnapshot("battle_local_writeback_missing_result", null)
            : result.Ok
                ? new System.Collections.Generic.Dictionary<string, object>
                {
                    ["ok"] = true,
                    ["error_code"] = "",
                    ["committed_member_count"] = result.CommittedMemberCount,
                    ["used_slots"] = result.UsedSlots,
                    ["capacity"] = result.Capacity,
                }
                : BuildFailureSnapshot(result.ErrorCode, result.Details);
        return RuntimePlainPayload.ProjectDictionaryLease(
            snapshot,
            "battle-local-writeback-result",
            LifetimeDomain.Request,
            "GameRuntimeBattleWritebackProjection.ProjectLease"
        );
    }

    private static IReadOnlyDictionary<string, object> BuildFailureSnapshot(
        string errorCode,
        IReadOnlyDictionary<string, object> details
    ) =>
        new System.Collections.Generic.Dictionary<string, object>
            {
            ["ok"] = false,
            ["error_code"] = errorCode ?? "",
            ["details"] = details ?? new System.Collections.Generic.Dictionary<string, object>(),
        };
}
