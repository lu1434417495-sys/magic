using System.Collections.Generic;

internal sealed record TestResult(
    string Label,
    bool Passed,
    int ExitCode,
    IReadOnlyList<string> Failures
);
