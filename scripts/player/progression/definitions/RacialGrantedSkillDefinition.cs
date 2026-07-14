using Godot;

public sealed class RacialGrantedSkillDefinition
{
    public RacialGrantedSkillDefinition(
        StringName skillId,
        int minimumSkillLevel,
        StringName chargeKind,
        int charges
    )
    {
        SkillId = skillId;
        MinimumSkillLevel = minimumSkillLevel;
        ChargeKind = chargeKind;
        Charges = charges;
    }

    public StringName SkillId { get; }
    public int MinimumSkillLevel { get; }
    public StringName ChargeKind { get; }
    public int Charges { get; }
    internal RacialSkillChargeKind ChargeKindKind => RacialGrantedSkill.ToChargeKind(ChargeKind);

    internal static RacialGrantedSkillDefinition FromResource(
        RacialGrantedSkill source,
        string path
    )
    {
        IdentityDefinitionProjection.RequireResource(
            source,
            path,
            nameof(RacialGrantedSkill)
        );
        return new RacialGrantedSkillDefinition(
            source.skill_id,
            source.minimum_skill_level,
            source.charge_kind,
            source.charges
        );
    }
}
