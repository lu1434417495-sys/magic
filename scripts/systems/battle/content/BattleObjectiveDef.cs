using System.IO;
using Godot;

[GlobalClass]
public partial class BattleObjectiveDef : Resource
{
    internal virtual BattleObjectiveDefinition ToDefinition()
    {
        throw new InvalidDataException(
            $"Unsupported battle objective authoring type {GetType().Name}."
        );
    }
}
