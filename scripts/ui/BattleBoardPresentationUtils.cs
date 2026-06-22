using System.Collections.Generic;

internal static class BattleBoardPresentationUtils
{
    internal static List<T> SnapshotList<T>(IEnumerable<T> values)
    {
        return values != null ? new List<T>(values) : new List<T>();
    }

    internal static void ReplaceList<T>(ref List<T> target, IEnumerable<T> next)
    {
        target?.Clear();
        target = SnapshotList(next);
    }
}
