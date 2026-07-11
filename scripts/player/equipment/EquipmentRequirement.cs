using System.Collections.Generic;
using Godot;

public readonly struct EquipmentRequirementCheckResult
{
    public readonly bool Allowed;
    public readonly IReadOnlyList<string> Blockers;

    public EquipmentRequirementCheckResult(
        bool allowed,
        IEnumerable<string> blockers = null
    )
    {
        Allowed = allowed;
        Blockers = (
            blockers != null ? new List<string>(blockers) : new List<string>()
        ).AsReadOnly();
    }

}

[GlobalClass]
public partial class EquipmentRequirement : Resource
{
    [Export]
    public Godot.Collections.Array<string> required_profession_ids = new();

    [Export(PropertyHint.Range, "0,99,1")]
    public int min_body_size;

    [Export(PropertyHint.Range, "0,99,1")]
    public int max_body_size;

    [Export]
    public Godot.Collections.Array<EquipmentAttributeRequirementDef> attribute_requirements = new();

    internal Godot.Collections.Array<string> RequiredProfessionIdsProjectionBorrowed =>
        required_profession_ids;

    internal Godot.Collections.Array<EquipmentAttributeRequirementDef> AttributeRequirementsProjectionBorrowed =>
        attribute_requirements;

    internal EquipmentRequirementDefinition ToDefinition() =>
        EquipmentRequirementDefinition.FromResource(this);
}
