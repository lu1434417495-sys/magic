using Godot;

internal interface IValidatableRegistry
{
    Godot.Collections.Array<string> validate();
}
