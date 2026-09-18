using Godot;

public sealed record WorldStartingAreaDefinition(Vector2I Size, double MaxChallengeRating)
{
    public static WorldStartingAreaDefinition Disabled { get; } = new(Vector2I.Zero, 0);
    public bool Enabled => Size.X > 0 && Size.Y > 0;
}
