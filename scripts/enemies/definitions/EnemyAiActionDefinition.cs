using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

internal abstract class EnemyAiActionDefinition
{
    protected EnemyAiActionDefinition(
        StringName actionId,
        StringName scoreBucketId,
        StringName actionIntent,
        EnemyAiActionKind kind,
        IReadOnlyList<StringName> declaredSkillIds
    )
    {
        ActionId = actionId;
        ScoreBucketId = scoreBucketId;
        ActionIntent = actionIntent;
        Kind = kind;
        DeclaredSkillIds = EnemyDefinitionCollections.FreezeList(declaredSkillIds);
    }

    internal StringName ActionId { get; }
    internal StringName ScoreBucketId { get; }
    internal StringName ActionIntent { get; }
    internal EnemyAiActionKind Kind { get; }
    internal IReadOnlyList<StringName> DeclaredSkillIds { get; }

    internal virtual string BuildSignature() =>
        $"{Kind}|{ActionId}|{ScoreBucketId}|{ActionIntent}|skills={string.Join(",", DeclaredSkillIds)}";

}

internal static class EnemyDefinitionCollections
{
    internal static IReadOnlyList<T> FreezeList<T>(IEnumerable<T> source)
    {
        return new ReadOnlyCollection<T>(
            source == null ? new List<T>() : new List<T>(source)
        );
    }

    internal static IReadOnlyDictionary<TKey, TValue> FreezeDictionary<TKey, TValue>(
        IEnumerable<KeyValuePair<TKey, TValue>> source,
        IEqualityComparer<TKey> comparer = null
    )
    {
        var copy = comparer == null
            ? new Dictionary<TKey, TValue>()
            : new Dictionary<TKey, TValue>(comparer);
        if (source != null)
        {
            foreach ((TKey key, TValue value) in source)
                copy.Add(key, value);
        }
        return new ReadOnlyDictionary<TKey, TValue>(copy);
    }

}
