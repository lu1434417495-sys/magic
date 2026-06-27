using System;
using System.Collections.Generic;
using Godot;

public sealed class BattleSimContentProvider : IDisposable
{
    private ProgressionContentRegistry _progression_content_registry = new();
    private EnemyContentRegistry _enemy_content_registry = new();

    public void Dispose()
    {
        _progression_content_registry?.Dispose();
        _enemy_content_registry?.Dispose();
        _progression_content_registry = null;
        _enemy_content_registry = null;
    }

    internal IReadOnlyDictionary<StringName, SkillDefinition> GetSkillDefinitionsTyped()
    {
        return _progression_content_registry.GetSkillDefinitionsTyped();
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
