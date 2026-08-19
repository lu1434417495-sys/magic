using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

public sealed class BattleSimContentProvider : IDisposable
{
    private ContentSnapshot _snapshot;
    private IReadOnlyDictionary<StringName, SkillDefinition> _skillDefinitions;

    internal BattleSimContentProvider(ContentSnapshot snapshot)
        : this(snapshot, snapshot?.Skills)
    {
    }

    internal BattleSimContentProvider(
        ContentSnapshot snapshot,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions
    )
    {
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        ArgumentNullException.ThrowIfNull(skillDefinitions);
        _skillDefinitions = new ReadOnlyDictionary<StringName, SkillDefinition>(
            new Dictionary<StringName, SkillDefinition>(skillDefinitions)
        );
    }

    public void Dispose()
    {
        _snapshot = null;
        _skillDefinitions = null;
    }

    internal IReadOnlyDictionary<StringName, SkillDefinition> GetSkillDefinitionsTyped()
    {
        _ = RequireSnapshot();
        return _skillDefinitions
            ?? throw new ObjectDisposedException(nameof(BattleSimContentProvider));
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
