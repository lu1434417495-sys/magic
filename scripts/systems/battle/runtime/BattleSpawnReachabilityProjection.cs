using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

internal static class BattleSpawnReachabilityProjection
{
    private static readonly Vector2I InvalidCoord = new(-1, -1);

    internal static GDictionary Project(BattleStartFailureSnapshot snapshot)
    {
        var result = new GDictionary();
        if (snapshot == null)
            return result;

        if (!string.IsNullOrEmpty(snapshot.Reason))
            result["reason"] = snapshot.Reason;
        if (snapshot.AllyUnitCount >= 0)
            result["ally_unit_count"] = snapshot.AllyUnitCount;
        if (snapshot.EnemyUnitCount >= 0)
            result["enemy_unit_count"] = snapshot.EnemyUnitCount;
        if (snapshot.PlacementAttempt >= 0)
            result["placement_attempt"] = snapshot.PlacementAttempt;
        if (snapshot.TerrainSeed != 0)
            result["terrain_seed"] = snapshot.TerrainSeed;
        if (snapshot.AllySpawnCount >= 0)
            result["ally_spawn_count"] = snapshot.AllySpawnCount;
        if (snapshot.EnemySpawnCount >= 0)
            result["enemy_spawn_count"] = snapshot.EnemySpawnCount;
        if (snapshot.PlacementAttempts >= 0)
            result["placement_attempts"] = snapshot.PlacementAttempts;

        GDictionary reachability = Project(snapshot.ReachabilityResult);
        if (reachability.Count > 0)
            result["reachability"] = reachability;
        return result;
    }

    internal static GDictionary Project(BattleSpawnReachabilityResult result)
    {
        if (result == null)
            return new GDictionary();

        var details = new GArray();
        foreach (BattleSpawnReachabilityUnitResult detail in result.Details)
        {
            details.Add(ProjectUnit(detail));
        }

        return new GDictionary
        {
            ["valid"] = result.Valid,
            ["invalid_enemy_unit_ids"] = ToStringNameArray(result.InvalidEnemyUnitIds),
            ["invalid_player_unit_ids"] = ToStringNameArray(result.InvalidPlayerUnitIds),
            ["details"] = details,
        };
    }

    internal static BattleSpawnReachabilityResult ParseResultPayload(GDictionary payload)
    {
        if (payload == null || payload.Count == 0)
            return null;
        return BattleSpawnReachabilityResult.FromProjectionPayload(
            ReadBool(payload, "valid", true),
            ReadStringNameList(payload, "invalid_enemy_unit_ids"),
            ReadStringNameList(payload, "invalid_player_unit_ids"),
            ReadUnitResultList(payload, "details")
        );
    }

    private static GDictionary ProjectUnit(
        BattleSpawnReachabilityUnitResult result
    )
    {
        var payload = new GDictionary { ["valid"] = result.Valid };
        if (!IsEmpty(result.UnitId))
            payload["unit_id"] = result.UnitId;
        if (!IsEmpty(result.FactionId))
            payload["faction_id"] = result.FactionId;
        if (!string.IsNullOrEmpty(result.Reason))
            payload["reason"] = result.Reason;
        if (result.AttackAnchor != InvalidCoord)
            payload["attack_anchor"] = result.AttackAnchor;
        if (!IsEmpty(result.TargetUnitId))
            payload["target_unit_id"] = result.TargetUnitId;
        if (!IsEmpty(result.SkillId))
            payload["skill_id"] = result.SkillId;
        if (result.ReachableAnchorCount >= 0)
            payload["reachable_anchor_count"] = result.ReachableAnchorCount;
        if (result.AttackSkillIds.Count > 0)
            payload["attack_skill_ids"] = ToStringNameArray(result.AttackSkillIds);
        return payload;
    }

    private static Godot.Collections.Array<StringName> ToStringNameArray(
        IEnumerable<StringName> values
    )
    {
        var result = new Godot.Collections.Array<StringName>();
        if (values == null)
            return result;
        foreach (StringName value in values)
        {
            result.Add(value);
        }
        return result;
    }

    private static IReadOnlyList<BattleSpawnReachabilityUnitResult> ReadUnitResultList(
        GDictionary payload,
        string key
    )
    {
        var result = new List<BattleSpawnReachabilityUnitResult>();
        if (payload == null || !payload.ContainsKey(key))
            return result;
        Variant value = payload[key];
        if (value.VariantType != Variant.Type.Array)
            return result;
        foreach (Variant entry in value.AsGodotArray())
        {
            if (entry.VariantType != Variant.Type.Dictionary)
                continue;
            result.Add(ReadUnitResult(entry.AsGodotDictionary()));
        }
        return result;
    }

    private static BattleSpawnReachabilityUnitResult ReadUnitResult(GDictionary payload)
    {
        if (payload == null || payload.Count == 0)
            return BattleSpawnReachabilityUnitResult.Invalid("");
        return new BattleSpawnReachabilityUnitResult
        {
            Valid = ReadBool(payload, "valid", true),
            UnitId = ReadStringName(payload, "unit_id"),
            FactionId = ReadStringName(payload, "faction_id"),
            Reason = ReadString(payload, "reason"),
            AttackAnchor = ReadVector2I(payload, "attack_anchor", InvalidCoord),
            TargetUnitId = ReadStringName(payload, "target_unit_id"),
            SkillId = ReadStringName(payload, "skill_id"),
            ReachableAnchorCount = ReadInt(payload, "reachable_anchor_count", -1),
            AttackSkillIds = ReadStringNameList(payload, "attack_skill_ids"),
        };
    }

    private static IReadOnlyList<StringName> ReadStringNameList(GDictionary payload, string key)
    {
        var result = new List<StringName>();
        if (payload == null || !payload.ContainsKey(key))
            return result;
        Variant value = payload[key];
        if (value.VariantType != Variant.Type.Array)
            return result;
        foreach (Variant entry in value.AsGodotArray())
        {
            StringName parsed = ReadStringName(entry);
            if (!IsEmpty(parsed))
                result.Add(parsed);
        }
        return result;
    }

    private static bool ReadBool(GDictionary payload, string key, bool fallback)
    {
        if (payload == null || !payload.ContainsKey(key))
            return fallback;
        Variant value = payload[key];
        return value.VariantType == Variant.Type.Bool ? value.AsBool() : fallback;
    }

    private static int ReadInt(GDictionary payload, string key, int fallback)
    {
        if (payload == null || !payload.ContainsKey(key))
            return fallback;
        Variant value = payload[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static Vector2I ReadVector2I(GDictionary payload, string key, Vector2I fallback)
    {
        if (payload == null || !payload.ContainsKey(key))
            return fallback;
        Variant value = payload[key];
        return value.VariantType == Variant.Type.Vector2I ? value.AsVector2I() : fallback;
    }

    private static StringName ReadStringName(GDictionary payload, string key) =>
        payload != null && payload.ContainsKey(key) ? ReadStringName(payload[key]) : "";

    private static StringName ReadStringName(Variant value)
    {
        return value.VariantType switch
        {
            Variant.Type.StringName => value.AsStringName(),
            Variant.Type.String => new StringName(value.AsString()),
            _ => new StringName(""),
        };
    }

    private static string ReadString(GDictionary payload, string key)
    {
        if (payload == null || !payload.ContainsKey(key))
            return "";
        Variant value = payload[key];
        return value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName().ToString(),
            _ => "",
        };
    }

    private static bool IsEmpty(StringName value) =>
        value == default || value == (StringName)"";
}
