using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        System.ArgumentNullException.ThrowIfNull(meteorProfiles);
        var copiedProfiles = new Dictionary<StringName, MeteorSwarmProfileData>();
        foreach (
            (StringName profileId, MeteorSwarmProfileData profile) in meteorProfiles
        )
        {
            if (StringNameIsEmpty(profileId))
                throw new System.ArgumentException("Special profile id must not be empty.", nameof(meteorProfiles));
            if (profile == null)
                throw new System.ArgumentException($"Special profile {profileId} must not be null.", nameof(meteorProfiles));
            copiedProfiles[profileId] = MeteorSwarmProfileData.CopyOf(profile);
        }
        _meteorProfiles = new ReadOnlyDictionary<StringName, MeteorSwarmProfileData>(
            copiedProfiles
        );
    }

    internal static BattleSpecialProfileRuntimeView ForMeteorSwarm(
        StringName profileId,
        MeteorSwarmProfile profile
    )
    {
        MeteorSwarmProfileData data = MeteorSwarmProfileData.FromResource(profileId, profile);
        var profiles = new Dictionary<StringName, MeteorSwarmProfileData>
        {
            [profileId] = data,
        };
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
        if (
            !_meteorProfiles.TryGetValue(
                profileId,
                out MeteorSwarmProfileData storedProfile
            )
            || storedProfile == null
        )
        {
            return false;
        }
        profile = MeteorSwarmProfileData.CopyOf(storedProfile);
        return true;
    }

    private static bool StringNameIsEmpty(StringName value) =>
        value == null || string.IsNullOrEmpty(value.ToString());
}
