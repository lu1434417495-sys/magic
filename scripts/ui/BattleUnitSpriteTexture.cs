using Godot;

/// <summary>Authored sole contacts in the atlas frame's logical pixel coordinates.</summary>
[GlobalClass]
public partial class BattleUnitSpriteTexture : AtlasTexture
{
    [Export] public Vector2 LeftFootContact { get; set; }
    [Export] public Vector2 RightFootContact { get; set; }

    public Vector2 GroundAnchor => (LeftFootContact + RightFootContact) * 0.5f;
}
