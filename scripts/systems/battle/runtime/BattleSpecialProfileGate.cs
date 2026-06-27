using System.Collections.Generic;
using Godot;

internal sealed class BattleSpecialProfileGate
{
    private const string PLAYER_BLOCK_MESSAGE = "该禁咒配置未通过校验，暂时无法施放。";

    private SpecialProfileGateSnapshot _registrySnapshot = SpecialProfileGateSnapshot.Empty();

    internal void Setup(Godot.Collections.Dictionary registrySnapshot)
    {
        _registrySnapshot = SpecialProfileGateSnapshot.FromDictionary(registrySnapshot);
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
        if (!_registrySnapshot.Ok)
        {
            return Block(
                result,
                result.ProfileId,
                result.SkillId,
                "content_invalid",
                PLAYER_BLOCK_MESSAGE,
                new Dictionary<string, object>(System.StringComparer.Ordinal)
                    { ["errors"] = _registrySnapshot.Errors }
            );
        }

        if (_registrySnapshot.ProfileIdBySkillId.Count == 0)
            return Block(
                result,
                result.ProfileId,
                result.SkillId,
                "missing_profile_index",
                PLAYER_BLOCK_MESSAGE,
                null
            );

        var skillKey = result.SkillId;
        if (
            !_registrySnapshot.ProfileIdBySkillId.TryGetValue(skillKey, out StringName profileId)
            || profileId != result.ProfileId
        )
            return Block(
                result,
                result.ProfileId,
                result.SkillId,
                "skill_not_owned",
                PLAYER_BLOCK_MESSAGE,
                null
            );

        if (!_registrySnapshot.Profiles.TryGetValue(result.ProfileId, out GateProfileSnapshot profile))
            return Block(
                result,
                result.ProfileId,
                result.SkillId,
                "profile_missing",
                PLAYER_BLOCK_MESSAGE,
                null
            );

        if (profile.RuntimeResolverId != result.ProfileId)
            return Block(
                result,
                result.ProfileId,
                result.SkillId,
                "resolver_mismatch",
                PLAYER_BLOCK_MESSAGE,
                new Dictionary<string, object>(System.StringComparer.Ordinal)
                {
                    ["runtime_resolver_id"] = profile.RuntimeResolverId.ToString(),
                    ["expected_profile_id"] = result.ProfileId.ToString(),
                }
            );

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

    private sealed class SpecialProfileGateSnapshot
    {
        public bool Ok { get; private set; }
        public List<string> Errors { get; } = new();
        public Dictionary<StringName, StringName> ProfileIdBySkillId { get; } = new();
        public Dictionary<StringName, GateProfileSnapshot> Profiles { get; } = new();

        internal static SpecialProfileGateSnapshot Empty() => new();

        internal static SpecialProfileGateSnapshot FromDictionary(
            Godot.Collections.Dictionary snapshot
        )
        {
            var result = new SpecialProfileGateSnapshot();
            if (snapshot == null || snapshot.Count == 0 || !snapshot.ContainsKey("ok"))
            {
                return result;
            }

            result.Ok = snapshot["ok"].AsBool();
            foreach (string error in ReadStringList(snapshot, "errors"))
            {
                result.Errors.Add(error);
            }

            Godot.Collections.Dictionary profileIdBySkillId =
                ReadDictionary(snapshot, "profile_id_by_skill_id");
            foreach (Variant rawSkillId in profileIdBySkillId.Keys)
            {
                StringName skillId = ProgressionDataUtils.to_string_name(rawSkillId);
                StringName profileId = ProgressionDataUtils.to_string_name(
                    profileIdBySkillId[rawSkillId]
                );
                if (skillId != "" && profileId != "")
                {
                    result.ProfileIdBySkillId[skillId] = profileId;
                }
            }

            Godot.Collections.Dictionary profiles = ReadDictionary(snapshot, "profiles");
            foreach (Variant rawProfileId in profiles.Keys)
            {
                StringName profileId = ProgressionDataUtils.to_string_name(rawProfileId);
                Godot.Collections.Dictionary profilePayload =
                    profiles[rawProfileId].AsGodotDictionary();
                GateProfileSnapshot profile = GateProfileSnapshot.FromDictionary(profilePayload);
                if (profileId != "" && !profile.IsEmpty)
                {
                    result.Profiles[profileId] = profile;
                }
            }

            return result;
        }
    }

    private readonly record struct GateProfileSnapshot(
        StringName ProfileId,
        StringName RuntimeResolverId
    )
    {
        public bool IsEmpty => ProfileId == "" && RuntimeResolverId == "";

        internal static GateProfileSnapshot FromDictionary(Godot.Collections.Dictionary payload)
        {
            if (payload == null || payload.Count == 0)
            {
                return new GateProfileSnapshot("", "");
            }
            return new GateProfileSnapshot(
                ReadStringName(payload, "profile_id"),
                ReadStringName(payload, "runtime_resolver_id")
            );
        }
    }

    private static Godot.Collections.Dictionary ReadDictionary(
        Godot.Collections.Dictionary source,
        string key
    )
    {
        if (source == null || !source.ContainsKey(key))
        {
            return new Godot.Collections.Dictionary();
        }
        return source[key].AsGodotDictionary();
    }

    private static List<string> ReadStringList(Godot.Collections.Dictionary source, string key)
    {
        var result = new List<string>();
        if (source == null || !source.ContainsKey(key))
        {
            return result;
        }
        foreach (object value in source[key].AsGodotArray())
        {
            string text = value?.ToString() ?? "";
            if (!string.IsNullOrEmpty(text))
            {
                result.Add(text);
            }
        }
        return result;
    }

    private static StringName ReadStringName(Godot.Collections.Dictionary source, string key)
    {
        if (source == null || !source.ContainsKey(key))
        {
            return "";
        }
        return ProgressionDataUtils.to_string_name(source[key]);
    }
}
