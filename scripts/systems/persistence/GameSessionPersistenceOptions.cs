using System;
using System.Linq;

internal sealed record GameSessionPersistenceOptions(
    string SaveDirectory,
    string SaveIndexPath
)
{
    internal static GameSessionPersistenceOptions Production { get; } =
        new("user://saves", "user://saves/index.dat");

    internal static GameSessionPersistenceOptions ForLifecycleSoak(string runId)
    {
        if (
            string.IsNullOrWhiteSpace(runId)
            || !runId.All(value => char.IsLetterOrDigit(value) || value == '-')
        )
        {
            throw new ArgumentException("Invalid lifecycle soak run ID.", nameof(runId));
        }

        string directory = $"user://lifecycle_soak/{runId}";
        return new GameSessionPersistenceOptions(directory, $"{directory}/index.dat");
    }
}
