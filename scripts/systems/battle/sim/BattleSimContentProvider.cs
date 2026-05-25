using Godot;
using Godot.Collections;

[GlobalClass]
public partial class BattleSimContentProvider : RefCounted
{
    private GodotObject _progression_content_registry = GD.Load<GDScript>("res://scripts/player/progression/progression_content_registry.gd").New().AsGodotObject();
    private GodotObject _enemy_content_registry = GD.Load<GDScript>("res://scripts/enemies/enemy_content_registry.gd").New().AsGodotObject();

    public Dictionary GetSkillDefs()
    {
        return (Dictionary)_progression_content_registry.Call("get_skill_defs");
    }

    public Dictionary GetEnemyTemplates()
    {
        return (Dictionary)_enemy_content_registry.Call("get_enemy_templates");
    }

    public Dictionary GetEnemyAiBrains()
    {
        return (Dictionary)_enemy_content_registry.Call("get_enemy_ai_brains");
    }
}
