using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public sealed class BattleBarrierLayerState
{
    private readonly List<StringName> _blockedCategories = new();
    private readonly List<StringName> _breakerSkillIds = new();
    private readonly List<BattleBarrierOutcomeState> _passageOutcomes = new();

    public StringName LayerId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int Order { get; set; }
    public bool Broken { get; set; }
    public IReadOnlyList<StringName> BlockedCategories => _blockedCategories;
    public IReadOnlyList<StringName> BreakerSkillIds => _breakerSkillIds;
    public IReadOnlyList<BattleBarrierOutcomeState> PassageOutcomes => _passageOutcomes;
    public bool HasSaveRollOverride { get; set; }
    public int SaveRollOverride { get; set; }

    internal BattleBarrierLayerState DuplicateForMutationSnapshotExact()
    {
        var duplicate = new BattleBarrierLayerState
        {
            LayerId = LayerId,
            DisplayName = DisplayName,
            Order = Order,
            Broken = Broken,
            HasSaveRollOverride = HasSaveRollOverride,
            SaveRollOverride = SaveRollOverride,
        };
        duplicate.SetBlockedCategoriesForMutationSnapshotExact(_blockedCategories);
        duplicate.SetBreakerSkillIdsForMutationSnapshotExact(_breakerSkillIds);
        var outcomes = new List<BattleBarrierOutcomeState>();
        foreach (BattleBarrierOutcomeState outcome in _passageOutcomes)
        {
            outcomes.Add(outcome?.DuplicateForMutationSnapshotExact());
        }
        duplicate.SetPassageOutcomesForMutationSnapshotExact(outcomes);
        return duplicate;
    }

    internal void SetBlockedCategoriesForMutationSnapshotExact(
        IEnumerable<StringName> values
    )
    {
        ReplaceStringNameListForMutationSnapshotExact(_blockedCategories, values);
    }

    internal void SetBreakerSkillIdsForMutationSnapshotExact(
        IEnumerable<StringName> values
    )
    {
        ReplaceStringNameListForMutationSnapshotExact(_breakerSkillIds, values);
    }

    internal void SetPassageOutcomesForMutationSnapshotExact(
        IEnumerable<BattleBarrierOutcomeState> outcomes
    )
    {
        _passageOutcomes.Clear();
        if (outcomes == null)
        {
            return;
        }
        foreach (BattleBarrierOutcomeState outcome in outcomes)
        {
            _passageOutcomes.Add(outcome);
        }
    }

    internal static BattleBarrierLayerState FromRuntimeDict(GDictionary source)
    {
        var layer = new BattleBarrierLayerState();
        if (source == null || source.Count == 0)
        {
            return layer;
        }

        layer.LayerId = ReadStringName(source, "layer_id");
        layer.DisplayName = ReadString(source, "display_name");
        layer.Order = ReadInt(source, "order");
        layer.Broken = ReadBool(source, "broken");
        layer.SetBlockedCategories(ReadStringNameList(GetArray(source, "blocked_categories")));
        layer.SetBreakerSkillIds(ReadStringNameList(GetArray(source, "breaker_skill_ids")));
        layer.SetPassageOutcomes(ReadOutcomeList(GetArray(source, "passage_outcomes")));
        if (source.ContainsKey("save_roll_override"))
        {
            layer.HasSaveRollOverride = true;
            layer.SaveRollOverride = ReadInt(source, "save_roll_override");
        }
        return layer;
    }

    public void SetBlockedCategories(IEnumerable<StringName> values)
    {
        ReplaceStringNameList(_blockedCategories, values);
    }

    public void SetBreakerSkillIds(IEnumerable<StringName> values)
    {
        ReplaceStringNameList(_breakerSkillIds, values);
    }

    public List<BattleBarrierOutcomeState> GetPassageOutcomesTyped()
    {
        return new List<BattleBarrierOutcomeState>(_passageOutcomes);
    }

    public void SetPassageOutcomes(IEnumerable<BattleBarrierOutcomeState> outcomes)
    {
        _passageOutcomes.Clear();
        if (outcomes == null)
        {
            return;
        }
        foreach (BattleBarrierOutcomeState outcome in outcomes)
        {
            if (outcome != null && !outcome.IsEmpty)
            {
                _passageOutcomes.Add(outcome);
            }
        }
    }

    internal GDictionary ToRuntimeDict()
    {
        var result = new GDictionary
        {
            ["layer_id"] = LayerId.ToString(),
            ["display_name"] = DisplayName,
            ["order"] = Order,
            ["broken"] = Broken,
            ["blocked_categories"] = StringArray(_blockedCategories),
            ["breaker_skill_ids"] = StringArray(_breakerSkillIds),
            ["passage_outcomes"] = OutcomeArray(_passageOutcomes),
        };
        if (HasSaveRollOverride)
        {
            result["save_roll_override"] = SaveRollOverride;
        }
        return result;
    }

    private static void ReplaceStringNameList(List<StringName> target, IEnumerable<StringName> values)
    {
        target.Clear();
        if (values == null)
        {
            return;
        }
        foreach (StringName value in values)
        {
            if (value != "")
            {
                target.Add(value);
            }
        }
    }

    private static void ReplaceStringNameListForMutationSnapshotExact(
        List<StringName> target,
        IEnumerable<StringName> values
    )
    {
        target.Clear();
        if (values == null)
        {
            return;
        }
        foreach (StringName value in values)
        {
            target.Add(value);
        }
    }

    private static GArray StringArray(IEnumerable<StringName> values)
    {
        var result = new GArray();
        if (values == null)
        {
            return result;
        }
        foreach (StringName value in values)
        {
            string text = value.ToString();
            if (!string.IsNullOrEmpty(text))
            {
                result.Add(text);
            }
        }
        return result;
    }

    private static GArray OutcomeArray(IEnumerable<BattleBarrierOutcomeState> values)
    {
        var result = new GArray();
        if (values == null)
        {
            return result;
        }
        foreach (BattleBarrierOutcomeState value in values)
        {
            if (value != null && !value.IsEmpty)
            {
                result.Add(value.ToRuntimeDict());
            }
        }
        return result;
    }

    private static bool HasKey(GDictionary source, string key)
    {
        if (source == null || string.IsNullOrEmpty(key))
        {
            return false;
        }
        return source.ContainsKey(key);
    }

    private static string ReadString(GDictionary source, string key, string fallback = "")
    {
        if (source == null || string.IsNullOrEmpty(key))
        {
            return fallback;
        }
        if (source.ContainsKey(key))
        {
            return source[key].ToString();
        }
        return fallback;
    }

    private static StringName ReadStringName(GDictionary source, string key)
    {
        string text = ReadString(source, key);
        return string.IsNullOrEmpty(text) ? new StringName("") : new StringName(text);
    }

    private static int ReadInt(GDictionary source, string key, int fallback = 0)
    {
        if (source == null || string.IsNullOrEmpty(key))
        {
            return fallback;
        }
        if (source.ContainsKey(key))
        {
            return source[key].AsInt32();
        }
        return fallback;
    }

    private static bool ReadBool(GDictionary source, string key, bool fallback = false)
    {
        if (source == null || string.IsNullOrEmpty(key))
        {
            return fallback;
        }
        if (source.ContainsKey(key))
        {
            return source[key].AsBool();
        }
        return fallback;
    }

    private static GArray GetArray(GDictionary source, string key)
    {
        if (!HasKey(source, key))
        {
            return new GArray();
        }
        if (source.ContainsKey(key))
        {
            return source[key].AsGodotArray();
        }
        return new GArray();
    }

    private static List<StringName> ReadStringNameList(GArray values)
    {
        var result = new List<StringName>();
        if (values == null)
        {
            return result;
        }
        foreach (var rawValue in values)
        {
            StringName value = ProgressionDataUtils.to_string_name(rawValue);
            if (value != "")
            {
                result.Add(value);
            }
        }
        return result;
    }

    private static List<BattleBarrierOutcomeState> ReadOutcomeList(GArray values)
    {
        var result = new List<BattleBarrierOutcomeState>();
        if (values == null)
        {
            return result;
        }
        foreach (var rawValue in values)
        {
            BattleBarrierOutcomeState outcome = BattleBarrierOutcomeState.FromRuntimeDict(
                rawValue.AsGodotDictionary()
            );
            if (outcome != null && !outcome.IsEmpty)
            {
                result.Add(outcome);
            }
        }
        return result;
    }
}
