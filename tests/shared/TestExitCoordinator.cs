using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

internal static class TestExitCoordinator
{
    internal static void Complete(SceneTree tree, TestResult result)
    {
        _ = CompleteAsync(
            tree,
            result,
            WaitForProcessFrameAsync,
            SubmitAsync,
            message => ConsoleProcessOutput.WriteFailure(message)
        );
    }

    internal static async Task CompleteAsync(
        SceneTree tree,
        TestResult result,
        Func<SceneTree, Task> waitForProcessFrameAsync,
        Func<SceneTree, TestResult, ValueTask<ShutdownReport>> submitAsync,
        Action<string> writeFailure
    )
    {
        TestResult terminalResult = result;
        try
        {
            ArgumentNullException.ThrowIfNull(tree);
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(waitForProcessFrameAsync);
            await waitForProcessFrameAsync(tree);
        }
        catch (Exception exception)
        {
            terminalResult = BuildUnexpectedFailureResult(
                result,
                "wait-for-process-frame",
                exception
            );
        }
        finally
        {
            await SubmitWithRecoveryAsync(
                tree,
                terminalResult,
                submitAsync,
                writeFailure
            );
        }
    }

    internal static ValueTask<ShutdownReport> SubmitAsync(SceneTree tree, TestResult result)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ShutdownRequest request = BuildShutdownRequest(result);

        foreach (string failure in result.Failures)
            ConsoleProcessOutput.WriteFailure(FormatFailureDiagnostic(result.Label, failure));

        var coordinator = tree.Root.GetNode<ApplicationLifetimeCoordinator>(
            "ApplicationLifetimeCoordinator"
        );
        return coordinator.RequestShutdownAsync(request);
    }

    private static async Task SubmitWithRecoveryAsync(
        SceneTree tree,
        TestResult result,
        Func<SceneTree, TestResult, ValueTask<ShutdownReport>> submitAsync,
        Action<string> writeFailure
    )
    {
        try
        {
            ArgumentNullException.ThrowIfNull(submitAsync);
            ArgumentNullException.ThrowIfNull(writeFailure);
            await submitAsync(tree, result);
        }
        catch (Exception exception)
        {
            TestResult failureResult = BuildUnexpectedFailureResult(
                result,
                "submit-shutdown",
                exception
            );
            TryWriteFailure(
                writeFailure,
                FormatFailureDiagnostic(
                    failureResult.Label,
                    failureResult.Failures[^1]
                )
            );

            try
            {
                ArgumentNullException.ThrowIfNull(submitAsync);
                await submitAsync(tree, failureResult);
            }
            catch (Exception recoveryException)
            {
                TryWriteFailure(
                    writeFailure,
                    FormatFailureDiagnostic(
                        failureResult.Label,
                        FormatUnexpectedFailure(
                            "submit-shutdown-recovery",
                            recoveryException
                        )
                    )
                );
            }
        }
    }

    internal static ShutdownRequest BuildShutdownRequest(TestResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new ShutdownRequest(
            result.ExitCode,
            ShutdownReason.TestComplete,
            new ShutdownCallerResult(result.Label, result.Passed)
        );
    }

    private static async Task WaitForProcessFrameAsync(SceneTree tree)
    {
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    private static TestResult BuildUnexpectedFailureResult(
        TestResult result,
        string stage,
        Exception exception
    )
    {
        string label = result?.Label ?? "Test exit";
        var failures = new List<string>(
            result?.Failures ?? Array.Empty<string>()
        )
        {
            FormatUnexpectedFailure(stage, exception),
        };
        return new TestResult(
            label,
            false,
            1,
            failures.AsReadOnly()
        );
    }

    private static string FormatUnexpectedFailure(string stage, Exception exception)
    {
        string exceptionType = exception?.GetType().FullName ?? "<unknown>";
        string message = exception?.Message ?? "<no message>";
        return $"Unexpected test exit failure. stage={stage} type={exceptionType} message={message}";
    }

    private static void TryWriteFailure(Action<string> writeFailure, string message)
    {
        try
        {
            writeFailure?.Invoke(message);
        }
        catch (Exception)
        {
            // The canonical shutdown request remains the source of the process exit code.
        }
    }

    internal static string FormatFailureDiagnostic(string label, string failure) =>
        $"[test] {label}: {failure}";
}
