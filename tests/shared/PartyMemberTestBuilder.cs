using Godot;

public sealed class PartyMemberTestBuilder
{
    private readonly PartyMemberState _member;

    private PartyMemberTestBuilder(StringName memberId)
    {
        _member = new PartyMemberState
        {
            member_id = memberId,
            display_name = memberId.ToString(),
        };
    }

    public static PartyMemberTestBuilder Create(StringName memberId) => new(memberId);

    public PartyMemberTestBuilder Named(string displayName)
    {
        _member.display_name = displayName;
        return this;
    }

    public PartyMemberTestBuilder WithVitals(int hp, int mp = 0, int aura = 0, bool dead = false)
    {
        _member.SetVitals(hp, mp, aura, dead);
        return this;
    }

    public PartyMemberTestBuilder Dead()
    {
        _member.MarkDead();
        return this;
    }

    public PartyMemberTestBuilder WithIdentity(StringName raceId, StringName subraceId)
    {
        _member.SetIdentity(raceId, subraceId);
        return this;
    }

    public PartyMemberTestBuilder WithBodySize(StringName category)
    {
        _member.SetBodySizeCategory(category);
        return this;
    }

    public PartyMemberTestBuilder WithBloodline(StringName bloodlineId, StringName stageId)
    {
        _member.SetBloodline(bloodlineId, stageId);
        return this;
    }

    public PartyMemberTestBuilder WithAscension(
        StringName ascensionId,
        StringName stageId,
        int startedAtWorldStep = 0,
        StringName originalRaceIdBeforeAscension = default
    )
    {
        _member.SetAscension(
            ascensionId,
            stageId,
            startedAtWorldStep,
            originalRaceIdBeforeAscension
        );
        return this;
    }

    public PartyMemberState Build() => _member;
}
