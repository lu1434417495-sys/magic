using Godot;

public readonly struct EquipmentRequirementCheckResult
{
    public readonly bool Allowed;
    public readonly Godot.Collections.Array<string> Blockers;

    public EquipmentRequirementCheckResult(
        bool allowed,
        Godot.Collections.Array<string> blockers = null
    )
    {
        Allowed = allowed;
        Blockers = blockers ?? new Godot.Collections.Array<string>();
    }

    public Godot.Collections.Dictionary ToDictionary()
    {
        return new Godot.Collections.Dictionary
        {
            ["allowed"] = Allowed,
            ["blockers"] = Blockers.Duplicate(),
        };
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

    public Godot.Collections.Dictionary Check(PartyMemberState member_state)
    {
        return CheckResult(member_state).ToDictionary();
    }

    public EquipmentRequirementCheckResult CheckResult(PartyMemberState member_state)
    {
        var blockers = new Godot.Collections.Array<string>();
        if (required_profession_ids.Count > 0)
        {
            bool has_profession = false;
            foreach (string raw_id in required_profession_ids)
            {
                var prof_id = ProgressionDataUtils.to_string_name(raw_id);
                if (member_state?.progression?.get_profession_progress(prof_id) != null)
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
        return new EquipmentRequirementCheckResult(blockers.Count == 0, blockers);
    }
}
