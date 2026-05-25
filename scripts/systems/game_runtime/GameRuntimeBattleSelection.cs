using Godot;

[GlobalClass]
public partial class GameRuntimeBattleSelection : RefCounted
{
    public Vector2I battle_selected_coord = new Vector2I(-1, -1);
    public StringName selected_skill_id = "";
    public void clear() { battle_selected_coord = new Vector2I(-1, -1); selected_skill_id = ""; }
}
