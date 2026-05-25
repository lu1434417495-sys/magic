using Godot;

[GlobalClass]
public partial class ProgressionService : RefCounted
{
    private GodotObject _unit_progress;
    private Godot.Collections.Dictionary _skill_defs = new();
    private Godot.Collections.Dictionary _profession_defs = new();

    public void setup(GodotObject unitProgress, Godot.Collections.Dictionary skillDefs, Godot.Collections.Dictionary professionDefs) { _unit_progress = unitProgress; _skill_defs = skillDefs; _profession_defs = professionDefs; }
    public static int calculate_constitution_modifier(int constitution) => Mathf.FloorToInt((constitution - 10) / 2.0f);
    public void refresh_runtime_state() { }
    public bool grant_racial_skill(RacialGrantedSkill grant, StringName sourceType, StringName sourceId) { return true; }
}
