internal enum ShutdownReason
{
    WindowClose = 0,
    RequestedExit,
    TestComplete,
}

internal sealed record ShutdownCallerResult(string Label, bool Passed);

internal sealed record ShutdownRequest(
    int RequestedExitCode,
    ShutdownReason Reason,
    ShutdownCallerResult CallerResult = null
);
