using System;
using System.Collections.Generic;
using Godot;

public sealed class BattleSimContentProvider : IDisposable
{
    private ProgressionContentRegistry _progression_content_registry;
    private BarrierContentRegistry _barrier_content_registry;
    private EnemyContentRegistry _enemy_content_registry;

    internal BattleSimContentProvider(IContentResourceLoader loader)
    {
        ArgumentNullException.ThrowIfNull(loader);
        _progression_content_registry = new ProgressionContentRegistry(loader);
        _barrier_content_registry = new BarrierContentRegistry(loader);
        _enemy_content_registry = new EnemyContentRegistry(loader);
    }

    public void Dispose()
    {
        _progression_content_registry?.Dispose();
        _barrier_content_registry?.Dispose();
        _enemy_content_registry?.Dispose();
        _progression_content_registry = null;
        _barrier_content_registry = null;
        _enemy_content_registry = null;
    }

    internal IReadOnlyDictionary<StringName, SkillDefinition> GetSkillDefinitionsTyped()
    {
        return _progression_content_registry.GetSkillDefinitionsTyped();
    }

    internal IReadOnlyDictionary<StringName, BarrierProfileDefinition> GetBarrierProfileDefinitionsTyped()
    {
        return _barrier_content_registry.GetProfileDefsTyped();
    }

    internal IReadOnlyDictionary<StringName, EnemyTemplateDef> GetEnemyTemplatesTyped()
    {
        return _enemy_content_registry.GetEnemyTemplatesTyped();
    }

    internal IReadOnlyDictionary<StringName, EnemyAiBrainDef> GetEnemyAiBrainsTyped()
    {
        return _enemy_content_registry.GetEnemyAiBrainsTyped();
    }
}
