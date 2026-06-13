internal enum BattleRefreshMode
{
    None = 0,
    Full,
    Overlay,
    Error,
}

internal static class BattleRefreshModes
{
    internal static string ToPayloadValue(BattleRefreshMode mode) =>
        mode switch
        {
            BattleRefreshMode.Full => "full",
            BattleRefreshMode.Overlay => "overlay",
            BattleRefreshMode.Error => "error",
            _ => "",
        };
}
