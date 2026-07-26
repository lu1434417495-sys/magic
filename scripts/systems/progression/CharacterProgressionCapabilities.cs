using Godot;

public interface ICharacterMemberStateQuery
{
    PartyMemberState GetMemberState(StringName memberId);
}

public interface IFateCharacterGateway : ICharacterMemberStateQuery
{
    PartyState GetPartyState();

    bool UnlockAchievement(
        StringName memberId,
        StringName achievementId,
        string summaryText = ""
    );
}

public interface ICharacterSkillLearningGateway
{
    PracticeSkillLearnStatus GetPracticeSkillLearnStatus(
        StringName memberId,
        StringName skillId
    );

    bool LearnSkill(
        StringName memberId,
        StringName skillId,
        bool confirmPracticeReplacement
    );
}
