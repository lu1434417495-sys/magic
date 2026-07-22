using System;
using System.Threading.Tasks;
using Godot;

internal sealed class E2eWait
{
    private readonly SceneTree _tree;

    internal E2eWait(SceneTree tree)
    {
        _tree = tree ?? throw new ArgumentNullException(nameof(tree));
    }

    internal async Task NextFrameAsync()
    {
        await _tree.ToSignal(_tree, SceneTree.SignalName.ProcessFrame);
    }

    internal async Task FramesAsync(int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), count, "Frame count cannot be negative.");

        for (int index = 0; index < count; index++)
            await NextFrameAsync();
    }

    internal async Task UntilAsync(
        Func<bool> predicate,
        int maxFrames,
        string description
    )
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ValidateTimeout(maxFrames, description);

        for (int elapsedFrames = 0; elapsedFrames <= maxFrames; elapsedFrames++)
        {
            if (predicate())
                return;
            if (elapsedFrames < maxFrames)
                await NextFrameAsync();
        }

        throw new TimeoutException(
            $"Timed out after {maxFrames} process frames waiting for {description}."
        );
    }

    internal async Task<T> UntilValueAsync<T>(
        Func<T> probe,
        Predicate<T> predicate,
        int maxFrames,
        string description
    )
    {
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(predicate);
        ValidateTimeout(maxFrames, description);

        for (int elapsedFrames = 0; elapsedFrames <= maxFrames; elapsedFrames++)
        {
            T value = probe();
            if (predicate(value))
                return value;
            if (elapsedFrames < maxFrames)
                await NextFrameAsync();
        }

        throw new TimeoutException(
            $"Timed out after {maxFrames} process frames waiting for {description}."
        );
    }

    internal async Task UntilAsync(
        Func<bool> predicate,
        int maxFrames,
        ulong timeoutMsec,
        string description
    )
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ValidateTimeout(maxFrames, description);
        ValidateDuration(timeoutMsec);

        ulong startedAtMsec = Time.GetTicksMsec();
        for (int elapsedFrames = 0; elapsedFrames <= maxFrames; elapsedFrames++)
        {
            if (predicate())
                return;
            if (
                elapsedFrames >= maxFrames
                || Time.GetTicksMsec() - startedAtMsec >= timeoutMsec
            )
                break;
            await NextFrameAsync();
        }

        throw new TimeoutException(
            $"Timed out waiting for {description}; limits were {maxFrames} process frames or {timeoutMsec} ms."
        );
    }

    internal async Task<T> UntilValueAsync<T>(
        Func<T> probe,
        Predicate<T> predicate,
        int maxFrames,
        ulong timeoutMsec,
        string description
    )
    {
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(predicate);
        ValidateTimeout(maxFrames, description);
        ValidateDuration(timeoutMsec);

        ulong startedAtMsec = Time.GetTicksMsec();
        for (int elapsedFrames = 0; elapsedFrames <= maxFrames; elapsedFrames++)
        {
            T value = probe();
            if (predicate(value))
                return value;
            if (
                elapsedFrames >= maxFrames
                || Time.GetTicksMsec() - startedAtMsec >= timeoutMsec
            )
                break;
            await NextFrameAsync();
        }

        throw new TimeoutException(
            $"Timed out waiting for {description}; limits were {maxFrames} process frames or {timeoutMsec} ms."
        );
    }

    private static void ValidateTimeout(int maxFrames, string description)
    {
        if (maxFrames <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxFrames),
                maxFrames,
                "Frame timeout must be positive."
            );
        }
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Wait description is required.", nameof(description));
    }

    private static void ValidateDuration(ulong timeoutMsec)
    {
        if (timeoutMsec == 0)
            throw new ArgumentOutOfRangeException(nameof(timeoutMsec), "Timeout must be positive.");
    }
}
