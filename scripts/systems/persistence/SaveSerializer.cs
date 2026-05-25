using Godot;

[GlobalClass]
public partial class SaveSerializer : RefCounted
{
    public Godot.Collections.Dictionary serialize(Godot.Collections.Dictionary worldData, GodotObject partyState) { return new Godot.Collections.Dictionary(); }
    public bool deserialize(Godot.Collections.Dictionary data, out Godot.Collections.Dictionary worldData, out GodotObject partyState) { worldData = new Godot.Collections.Dictionary(); partyState = null; return true; }
}
