using System;
using System.Collections.Generic;
using Godot;

internal sealed class RuntimeEnemyAiResourceFactory
{
    private readonly GodotTransientResourceScope _legacyScope;
    private readonly NativeLeaseScope _nativeScope;
    private readonly string _ownerName;

    internal RuntimeEnemyAiResourceFactory(GodotTransientResourceScope scope, string ownerName)
    {
        _legacyScope = scope ?? throw new ArgumentNullException(nameof(scope));
        _ownerName = string.IsNullOrEmpty(ownerName) ? "RuntimeEnemyAiResourceFactory" : ownerName;
    }

    internal RuntimeEnemyAiResourceFactory(NativeLeaseScope scope, string ownerName)
    {
        _nativeScope = scope ?? throw new ArgumentNullException(nameof(scope));
        _ownerName = string.IsNullOrEmpty(ownerName) ? "RuntimeEnemyAiResourceFactory" : ownerName;
    }

    internal EnemyAiBrainDef NewBrain(Action<EnemyAiBrainDef> configure, string reason)
    {
        return Create(() => new EnemyAiBrainDef(), configure, reason);
    }

    internal EnemyAiStateDef NewState(Action<EnemyAiStateDef> configure, string reason)
    {
        return Create(() => new EnemyAiStateDef(), configure, reason);
    }

    internal TAction NewAction<TAction>(Action<TAction> configure, string reason)
        where TAction : EnemyAiAction, new()
    {
        return Create(() => new TAction(), configure, reason);
    }

    internal EnemyAiAction OwnAction(EnemyAiAction action, string reason)
    {
        return action != null ? Own(action, reason) : null;
    }

    internal Godot.Collections.Array<StringName> NewStringNameArray(
        IEnumerable<StringName> values,
        string reason
    )
    {
        if (_nativeScope != null)
        {
            throw new InvalidOperationException(
                "Typed Godot arrays are not IDisposable and must be removed before this factory uses a NativeLeaseScope."
            );
        }

        var result = new Godot.Collections.Array<StringName>();
        if (values != null)
        {
            foreach (StringName value in values)
                result.Add(value);
        }
        return _legacyScope.OwnWrapper(result, Label(reason));
    }

    private T Own<T>(T resource, string reason)
        where T : Resource
    {
        return _nativeScope != null
            ? _nativeScope.Own(resource, Label(reason))
            : _legacyScope.Own(resource, Label(reason));
    }

    private T Create<T>(Func<T> create, Action<T> configure, string reason)
        where T : Resource
    {
        if (_nativeScope != null)
        {
            if (_nativeScope.IsClosed)
                throw new ObjectDisposedException(nameof(NativeLeaseScope));

            T resource = null;
            try
            {
                resource = create();
                configure?.Invoke(resource);
                return _nativeScope.Own(resource, Label(reason));
            }
            catch (Exception creationFailure)
            {
                if (resource == null || !GodotObject.IsInstanceValid(resource))
                    throw;

                try
                {
                    resource.Dispose();
                }
                catch (Exception disposeFailure)
                {
                    throw new AggregateException(
                        "Native resource creation failed and cleanup also failed.",
                        creationFailure,
                        disposeFailure
                    );
                }
                throw;
            }
        }

        T legacyResource = create();
        configure?.Invoke(legacyResource);
        return _legacyScope.Own(legacyResource, Label(reason));
    }

    private string Label(string reason) =>
        string.IsNullOrEmpty(reason) ? _ownerName : $"{_ownerName}:{reason}";
}
