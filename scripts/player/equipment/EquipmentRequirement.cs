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
        Blockers = blockers != null ? new List<string>(blockers) : new List<string>();
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

    public EquipmentRequirementCheckResult CheckResult(PartyMemberState member_state)
    {
        var blockers = new List<string>();
        if (required_profession_ids.Count > 0)
        {
            bool has_profession = false;
            foreach (string raw_id in required_profession_ids)
            {
                var prof_id = ProgressionDataUtils.to_string_name(raw_id);
                if (member_state?.progression?.GetProfessionProgress(prof_id) != null)
                {
                    has_profession = true;
                    break;
                }
            }
            if (!has_profession)
                blockers.Add("missing_profession");
        }
        if (min_body_size > 0 && (member_state == null || member_state.body_size < min_body_size))
            blockers.Add("body_size_too_small");
        if (max_body_size > 0 && (member_state == null || member_state.body_size > max_body_size))
            blockers.Add("body_size_too_large");
        foreach (EquipmentAttributeRequirementDef requirement in attribute_requirements ?? new())
        {
            StringName attributeId = ProgressionDataUtils.to_string_name(
                requirement?.attribute_id ?? ""
            );
            if (attributeId == "" || requirement.min_value <= 0)
                continue;
            int value =
                member_state
                    ?.progression
                    ?.unit_base_attributes
                    ?.GetAttributeValue(attributeId) ?? 0;
            if (value < requirement.min_value)
                blockers.Add("attribute_too_low");
        }
        return new EquipmentRequirementCheckResult(blockers.Count == 0, blockers);
    }
}
