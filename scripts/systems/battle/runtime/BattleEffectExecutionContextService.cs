using System;
using System.Collections.Generic;

internal sealed class BattleEffectExecutionContextService
{
    private readonly List<Frame> _frames = new();
    private long _nextScopeId = 1;
    private long _generation = 1;

    internal int Depth => _frames.Count;

    internal BattleEffectOrigin CurrentForReporting =>
        _frames.Count > 0
            ? _frames[^1].Origin
            : BattleEffectOrigin.PlayerCommand();

    internal BattleEffectOrigin RequireCurrentForAttack()
    {
        if (_frames.Count == 0)
        {
            throw new InvalidOperationException(
                "logical attack requires an explicit effect origin"
            );
        }
        return _frames[^1].Origin;
    }

    internal IDisposable Push(BattleEffectOrigin origin)
    {
        ArgumentNullException.ThrowIfNull(origin);
        long scopeId = checked(_nextScopeId++);
        long generation = _generation;
        _frames.Add(new Frame(scopeId, origin));
        return new Scope(this, scopeId, generation);
    }

    internal void Clear()
    {
        _frames.Clear();
        _generation = checked(_generation + 1);
        _nextScopeId = 1;
    }

    private void Pop(long scopeId, long generation)
    {
        if (generation != _generation)
            return;
        if (_frames.Count == 0 || _frames[^1].ScopeId != scopeId)
        {
            throw new InvalidOperationException(
                "effect origin scopes must be disposed in LIFO order"
            );
        }
        _frames.RemoveAt(_frames.Count - 1);
    }

    private readonly record struct Frame(
        long ScopeId,
        BattleEffectOrigin Origin
    );

    private sealed class Scope : IDisposable
    {
        private BattleEffectExecutionContextService _owner;
        private readonly long _scopeId;
        private readonly long _generation;

        internal Scope(
            BattleEffectExecutionContextService owner,
            long scopeId,
            long generation
        )
        {
            _owner = owner;
            _scopeId = scopeId;
            _generation = generation;
        }

        public void Dispose()
        {
            BattleEffectExecutionContextService owner = _owner;
            if (owner == null)
                return;
            _owner = null;
            owner.Pop(_scopeId, _generation);
        }
    }
}
