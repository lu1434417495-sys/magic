using System;
using System.Collections.Generic;
using Godot;

internal sealed class RuntimeEnemyAiResourceFactory
{
    private readonly GodotTransientResourceScope _scope;
    private readonly string _ownerName;

    internal RuntimeEnemyAiResourceFactory(GodotTransientResourceScope scope, string ownerName)
    {
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _ownerName = string.IsNullOrEmpty(ownerName) ? "RuntimeEnemyAiResourceFactory" : ownerName;
    }

    internal EnemyAiBrainDef NewBrain(Action<EnemyAiBrainDef> configure, string reason)
    {
        var brain = new EnemyAiBrainDef();
        configure?.Invoke(brain);
        return _scope.Own(brain, Label(reason));
    }

    internal EnemyAiStateDef NewState(Action<EnemyAiStateDef> configure, string reason)
    {
        var state = new EnemyAiStateDef();
        configure?.Invoke(state);
        return _scope.Own(state, Label(reason));
    }

    internal TAction NewAction<TAction>(Action<TAction> configure, string reason)
        where TAction : EnemyAiAction, new()
    {
        var action = new TAction();
        configure?.Invoke(action);
        return _scope.Own(action, Label(reason));
    }

    internal EnemyAiAction OwnAction(EnemyAiAction action, string reason)
    {
        return action != null ? _scope.Own(action, Label(reason)) : null;
    }

    internal Godot.Collections.Array<StringName> NewStringNameArray(
        IEnumerable<StringName> values,
        string reason
    )
    {
        var result = new Godot.Collections.Array<StringName>();
        if (values != null)
        {
            foreach (StringName value in values)
                result.Add(value);
        }
        return _scope.OwnWrapper(result, Label(reason));
    }

    private string Label(string reason) =>
        string.IsNullOrEmpty(reason) ? _ownerName : $"{_ownerName}:{reason}";
}
