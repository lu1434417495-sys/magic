using Godot;
using Godot.Collections;

[GlobalClass]
public partial class BattleChargeResolver : RefCounted
{
    private GodotObject _runtime;
    public void setup(GodotObject runtime) { _runtime = runtime; }
    public void dispose() { _runtime = null; }
    public Godot.Collections.Dictionary resolve_charge(BattleUnitState unit, StringName skillId, CombatCastVariantDef castVariant, Godot.Collections.Array<Vector2I> pathCoords, BattleEventBatch batch) { return new Godot.Collections.Dictionary { {"executed", false} }; }
}
