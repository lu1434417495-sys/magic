using Godot;
using Godot.Collections;

[GlobalClass]
public partial class BattleSimContentProvider : RefCounted
{
    private ProgressionContentRegistry _progression_content_registry = new();
    private EnemyContentRegistry _enemy_content_registry = new();

    public new void Dispose()
    {
        _progression_content_registry?.Dispose();
        _enemy_content_registry?.Dispose();
        _progression_content_registry = null;
        _enemy_content_registry = null;
        base.Dispose();
    }

    public void dispose() => Dispose();

    public Dictionary GetSkillDefs()
    {
        return _progression_content_registry.get_skill_defs();
    }

    public Dictionary get_skill_defs()
    {
        return GetSkillDefs();
    }

    public Dictionary GetEnemyTemplates()
    {
        return _enemy_content_registry.get_enemy_templates();
    }

    public Dictionary get_enemy_templates()
    {
        return GetEnemyTemplates();
    }

    public Dictionary GetEnemyAiBrains()
    {
        return _enemy_content_registry.get_enemy_ai_brains();
    }

    public Dictionary get_enemy_ai_brains()
    {
        return GetEnemyAiBrains();
    }
}
