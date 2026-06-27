using System.Collections.Generic;
using Godot;

public interface IBattleSpecialProfileView
{
    bool TryGetMeteorSwarmProfile(
        StringName profileId,
        out MeteorSwarmProfileData profile
    );
}

internal sealed class BattleSpecialProfileRuntimeView : IBattleSpecialProfileView
{
    internal static readonly BattleSpecialProfileRuntimeView Empty = new(
        new Dictionary<StringName, MeteorSwarmProfileData>()
    );

    private readonly IReadOnlyDictionary<StringName, MeteorSwarmProfileData> _meteorProfiles;

    internal BattleSpecialProfileRuntimeView(
        IReadOnlyDictionary<StringName, MeteorSwarmProfileData> meteorProfiles
    )
    {
        _meteorProfiles =
            meteorProfiles != null
                ? new Dictionary<StringName, MeteorSwarmProfileData>(meteorProfiles)
                : new Dictionary<StringName, MeteorSwarmProfileData>();
    }

    internal static BattleSpecialProfileRuntimeView ForMeteorSwarm(
        StringName profileId,
        MeteorSwarmProfile profile
    )
    {
        MeteorSwarmProfileData data = MeteorSwarmProfileData.FromResource(profileId, profile);
        var profiles = new Dictionary<StringName, MeteorSwarmProfileData>();
        if (data != null)
        {
            profiles[profileId] = data;
        }
        return new BattleSpecialProfileRuntimeView(profiles);
    }

    public bool TryGetMeteorSwarmProfile(
        StringName profileId,
        out MeteorSwarmProfileData profile
    )
    {
        profile = null;
        if (StringNameIsEmpty(profileId) || _meteorProfiles == null)
        {
            return false;
        }
        return _meteorProfiles.TryGetValue(profileId, out profile) && profile != null;
    }

    private static bool StringNameIsEmpty(StringName value) =>
        value == null || string.IsNullOrEmpty(value.ToString());
}
