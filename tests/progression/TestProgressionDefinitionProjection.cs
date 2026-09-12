using System;
using System.Collections.Generic;
using Godot;

internal static class TestProgressionDefinitionProjection
{
    internal static QuestDefinition Quest(QuestTestDefinitionBuilder source) =>
        source?.ToDefinition(Path("quest", source.quest_id))
        ?? throw new ArgumentNullException(nameof(source));

    internal static TagRequirementDefinition TagRequirement(TagRequirement source) =>
        TagRequirementDefinition.FromDiagnosticFixture(source, "test.tag_requirement");

    internal static Dictionary<StringName, QuestDefinition> Quests(
        IReadOnlyDictionary<StringName, QuestTestDefinitionBuilder> source
    ) => Project(source, Quest);

    private static Dictionary<StringName, TDefinition> Project<TSource, TDefinition>(
        IReadOnlyDictionary<StringName, TSource> source,
        Func<TSource, TDefinition> projector
    )
        where TSource : class
        where TDefinition : class
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(projector);
        var result = new Dictionary<StringName, TDefinition>(source.Count);
        foreach ((StringName key, TSource value) in source)
        {
            if (key == "")
                throw new ArgumentException("Test authored fixture contains an empty key.", nameof(source));
            result.Add(key, projector(value));
        }
        return result;
    }

    private static string Path(string kind, StringName id) =>
        $"test.{kind}.{(id == "" ? "<missing>" : id.ToString())}";
}
