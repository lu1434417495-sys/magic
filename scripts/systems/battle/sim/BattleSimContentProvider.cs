using System;
using System.Collections.Generic;
using Godot;

public sealed class BattleSimContentProvider : IDisposable
{
    private ContentSnapshot _snapshot;

    internal BattleSimContentProvider(ContentSnapshot snapshot)
    {
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
    }

    public void Dispose()
    {
        _snapshot = null;
    }

    internal IReadOnlyDictionary<StringName, SkillDefinition> GetSkillDefinitionsTyped()
    {
        return RequireSnapshot().Skills;
    }

    internal IReadOnlyDictionary<StringName, BarrierProfileDefinition> GetBarrierProfileDefinitionsTyped()
    {
        return RequireSnapshot().BarrierProfiles;
    }

    internal IReadOnlyDictionary<StringName, EnemyTemplateDefinition> GetEnemyTemplatesTyped()
    {
        return RequireSnapshot().EnemyTemplates;
    }

    internal IReadOnlyDictionary<StringName, EnemyAiBrainDefinition> GetEnemyAiBrainsTyped()
    {
        return RequireSnapshot().EnemyBrains;
    }

    internal IReadOnlyDictionary<StringName, BattleSimProfileDefinition> GetBattleSimProfilesTyped()
    {
        return RequireSnapshot().BattleSimProfiles;
    }

    private ContentSnapshot RequireSnapshot() =>
        _snapshot ?? throw new ObjectDisposedException(nameof(BattleSimContentProvider));
}
