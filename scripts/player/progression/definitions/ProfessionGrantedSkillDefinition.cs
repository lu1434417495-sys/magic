using System;
using Godot;

public sealed class ProfessionGrantedSkillDefinition
{
    public ProfessionGrantedSkillDefinition(
        StringName skillId,
        int unlockRank,
        StringName skillType
    )
    {
        SkillId = skillId;
        UnlockRank = unlockRank;
        SkillType = skillType;
    }

    public StringName SkillId { get; }
    public int UnlockRank { get; }
    public StringName SkillType { get; }

}
