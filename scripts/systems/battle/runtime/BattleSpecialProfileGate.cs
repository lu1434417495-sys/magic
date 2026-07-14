using System.Collections.Generic;
using Godot;

internal sealed class BattleSpecialProfileGate
{
    private const string PLAYER_BLOCK_MESSAGE = "该禁咒配置未通过校验，暂时无法施放。";
    private static readonly StringName MeteorSwarmProfileId = "meteor_swarm";

    private IBattleSpecialProfileView _profileView = BattleSpecialProfileRuntimeView.Empty;

    internal void Setup(IBattleSpecialProfileView profileView)
    {
        _profileView = profileView ?? BattleSpecialProfileRuntimeView.Empty;
    }

    internal BattleSpecialProfileGateResult PreflightSkill(
        SkillDefinition skillDefinition,
        BattleState battleState
    ) => EvaluateSkill(skillDefinition, battleState);

    internal BattleSpecialProfileGateResult PreviewSkill(
        SkillDefinition skillDefinition,
        BattleCommand command,
        BattleUnitState activeUnit,
        BattleState battleState
    ) => EvaluateSkill(skillDefinition, battleState, command, activeUnit);

    internal BattleSpecialProfileGateResult PreviewSkill(
        SkillDefinition skillDefinition,
        BattleCommand command,
        BattleUnitReadView activeUnit,
        BattleState battleState
    ) => EvaluateSkill(skillDefinition, battleState, command, null);

    internal BattleSpecialProfileGateResult CanExecuteSkill(
        SkillDefinition skillDefinition,
        BattleCommand command,
        BattleUnitState activeUnit,
        BattleState battleState
    ) => EvaluateSkill(skillDefinition, battleState, command, activeUnit);

    private BattleSpecialProfileGateResult EvaluateSkill(
        SkillDefinition skillDefinition,
        BattleState battleState,
        BattleCommand command = null,
        BattleUnitState activeUnit = null
    )
    {
        var result = new BattleSpecialProfileGateResult();
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (skillDefinition == null || combatProfile == null)
            return Block(
                result,
                "",
                "",
                "missing_skill",
                "Missing skill definition.",
                null
            );
        result.SkillId = skillDefinition.SkillId;
        result.ProfileId = combatProfile.SpecialResolutionProfileId;

        if (result.ProfileId == "")
        {
            result.Allowed = true;
            return result;
        }
        if (
            result.ProfileId != MeteorSwarmProfileId
            || !_profileView.TryGetMeteorSwarmProfile(
                result.ProfileId,
                out MeteorSwarmProfileData _
            )
        )
        {
            return Block(
                result,
                result.ProfileId,
                result.SkillId,
                "content_invalid",
                PLAYER_BLOCK_MESSAGE,
                new Dictionary<string, object>(System.StringComparer.Ordinal)
                {
                    ["errors"] = new List<string>
                    {
                        $"Missing validated battle special profile {result.ProfileId}.",
                    },
                }
            );
        }

        if (battleState == null)
            return Block(
                result,
                result.ProfileId,
                result.SkillId,
                "missing_battle_state",
                PLAYER_BLOCK_MESSAGE,
                null
            );

        if (command == null && activeUnit == null)
        {
            result.Allowed = true;
            return result;
        }
        result.Allowed = true;
        return result;
    }

    private static BattleSpecialProfileGateResult Block(
        BattleSpecialProfileGateResult result,
        StringName profileId,
        StringName skillId,
        StringName blockCode,
        string playerMessage,
        IReadOnlyDictionary<string, object> debugDetails
    )
    {
        result.Allowed = false;
        result.ProfileId = profileId;
        result.SkillId = skillId;
        result.BlockCode = blockCode;
        result.PlayerMessage = playerMessage;
        result.DebugDetails.Clear();
        if (debugDetails != null)
        {
            foreach (KeyValuePair<string, object> entry in debugDetails)
            {
                result.DebugDetails[entry.Key] = entry.Value;
            }
        }
        return result;
    }

}
