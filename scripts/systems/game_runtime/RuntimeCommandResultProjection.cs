using GDictionary = Godot.Collections.Dictionary;

internal static class RuntimeCommandResultProjection
{
    internal static GDictionary Project(GameRuntimeFacade.RuntimeCommandResult result)
    {
        result ??= GameRuntimeFacade.RuntimeCommandResult.Failure("");
        var payload = new GDictionary
        {
            ["ok"] = result.Ok,
            ["message"] = result.Message,
            ["code"] = (int)result.Code,
        };
        if (result.Ok)
            payload["battle_refresh_mode"] =
                BattleRefreshModes.ToPayloadValue(result.BattleRefreshMode);
        return payload;
    }
}
