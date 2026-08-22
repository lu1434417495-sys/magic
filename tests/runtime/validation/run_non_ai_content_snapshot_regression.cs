using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;

public partial class run_non_ai_content_snapshot_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        ContentSnapshot snapshot = Root
            .GetNode<ApplicationLifetimeCoordinator>("ApplicationLifetimeCoordinator")
            .ContentHost.GetSnapshot();

        _test.True(snapshot.Skills.Count > 0, "snapshot should publish skills");
        _test.True(snapshot.Traits.Count > 0, "snapshot should publish traits");
        _test.True(snapshot.Items.Count > 0, "snapshot should publish authored and generated items");
        _test.True(snapshot.Recipes.Count > 0, "snapshot should publish recipes");
        _test.True(snapshot.WorldGenerations.Count >= 5, "snapshot should publish every world root");
        _test.True(
            snapshot.WorldGenerations.Values.All(definition => definition != null),
            "world snapshot entries should be non-null"
        );
        _test.True(
            Throws<NotSupportedException>(() =>
                ((IDictionary<StringName, SkillDefinition>)snapshot.Skills).Add(
                    "forbidden",
                    snapshot.Skills.Values.First()
                )
            ),
            "snapshot dictionaries should be immutable"
        );

        AssertSnapshotInstanceGraphIsDetached(
            snapshot,
            "snapshot",
            new HashSet<object>(ReferenceEqualityComparer.Instance)
        );

        RequestTestExit(_test.Finish("Non-AI content snapshot regression"));
    }

    private void AssertSnapshotInstanceGraphIsDetached(
        object value,
        string path,
        HashSet<object> visited
    )
    {
        if (value == null)
            return;

        Type type = value.GetType();
        if (
            value is Variant
            || value is GodotObject
            || type.Namespace?.StartsWith("Godot.Collections", StringComparison.Ordinal) == true
        )
        {
            _test.Fail($"{path} retains native/Godot wrapper instance {type.FullName}.");
            return;
        }

        if (
            value is EnemyTemplateDef
            || value is EnemyAiBrainDef
            || value is WildEncounterRosterDef
        )
        {
            _test.Fail($"{path} retains authored enemy Resource {type.FullName}.");
            return;
        }

        if (IsDetachedScalar(type))
            return;
        if (!type.IsValueType && !visited.Add(value))
            return;

        if (value is IEnumerable enumerable)
        {
            int index = 0;
            foreach (object entry in enumerable)
            {
                AssertSnapshotInstanceGraphIsDetached(entry, $"{path}[{index}]", visited);
                index++;
            }
            return;
        }

        bool inspectFields = type.Assembly == typeof(ContentSnapshot).Assembly
            || (
                type.IsGenericType
                && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>)
            );
        if (!inspectFields)
            return;

        for (Type current = type; current != null; current = current.BaseType)
        foreach (
            FieldInfo field in current.GetFields(
                BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly
            )
        )
        {
            AssertSnapshotInstanceGraphIsDetached(
                field.GetValue(value),
                $"{path}.{field.Name}",
                visited
            );
        }
    }

    private static bool IsDetachedScalar(Type type) =>
        type.IsPrimitive
        || type.IsEnum
        || type == typeof(string)
        || type == typeof(decimal)
        || type == typeof(DateTime)
        || type == typeof(Guid)
        || (type.IsValueType && string.Equals(type.Namespace, "Godot", StringComparison.Ordinal));

    private static bool Throws<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
            return false;
        }
        catch (TException)
        {
            return true;
        }
    }
}
