public enum RuntimeCommandCode
{
    None = 0,
    Ok = 1,
    Failed = 2,
    InvalidArgument = 3,
    InvalidState = 4,
    NotFound = 5,
    RuntimeUnavailable = 6,
    PersistenceFailure = 7,
}

internal sealed class RuntimeCommandResult
{
    public bool Ok { get; private set; }
    public string Message { get; private set; } = "";
    public RuntimeCommandCode Code { get; private set; }
    public BattleRefreshMode BattleRefreshMode { get; private set; } =
        BattleRefreshMode.None;

    public static RuntimeCommandResult Success(
        string message = "",
        RuntimeCommandCode code = RuntimeCommandCode.Ok,
        BattleRefreshMode battleRefreshMode = BattleRefreshMode.None
    )
    {
        return new RuntimeCommandResult
        {
            Ok = true,
            Message = message ?? "",
            Code = code == RuntimeCommandCode.None ? RuntimeCommandCode.Ok : code,
            BattleRefreshMode = battleRefreshMode,
        };
    }

    public static RuntimeCommandResult Failure(
        string message,
        RuntimeCommandCode code = RuntimeCommandCode.Failed
    )
    {
        return new RuntimeCommandResult
        {
            Ok = false,
            Message = message ?? "",
            Code = code == RuntimeCommandCode.None ? RuntimeCommandCode.Failed : code,
        };
    }
}
