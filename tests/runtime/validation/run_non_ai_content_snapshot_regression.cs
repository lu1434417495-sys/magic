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

        foreach (Type type in EnumerateTypeGraph(typeof(ContentSnapshot)))
        {
            _test.False(
                typeof(Resource).IsAssignableFrom(type),
                $"non-AI snapshot type graph should not contain Resource: {type.FullName}"
            );
            _test.False(
                type.Namespace?.StartsWith("Godot.Collections", StringComparison.Ordinal) == true,
                $"non-AI snapshot type graph should not contain Godot collections: {type.FullName}"
            );
            _test.False(
                type == typeof(Variant) || type == typeof(GodotObject),
                $"non-AI snapshot type graph should not contain object Variant wrappers: {type.FullName}"
            );
            _test.False(
                type == typeof(EnemyTemplateDef)
                    || type == typeof(EnemyAiBrainDef)
                    || type == typeof(WildEncounterRosterDef)
                    || type == typeof(BattleSimProfileDef),
                $"legacy enemy content must remain outside ContentSnapshot: {type.FullName}"
            );
        }

        RequestTestExit(_test.Finish("Non-AI content snapshot regression"));
    }

    private static IEnumerable<Type> EnumerateTypeGraph(Type root)
    {
        var pending = new Stack<Type>();
        var visited = new HashSet<Type>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            Type current = pending.Pop();
            if (current == null || !visited.Add(current))
                continue;
            yield return current;

            if (current.IsArray)
                pending.Push(current.GetElementType());
            if (current.IsGenericType)
            {
                foreach (Type argument in current.GetGenericArguments())
                    pending.Push(argument);
            }
            foreach (
                PropertyInfo property in current.GetProperties(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                )
            )
            {
                if (property.DeclaringType == current)
                    pending.Push(property.PropertyType);
            }
        }
    }

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
