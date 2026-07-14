using System.Collections;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public class CharacterProgressionDelta
{
    private readonly List<StringName> _leveledSkillIds = new();
    private readonly List<StringName> _grantedSkillIds = new();
    private readonly List<StringName> _changedProfessionIds = new();
    private readonly List<PendingProfessionChoice> _pendingProfessionChoices = new();
    private readonly List<CharacterMasteryChangeFact> _masteryChanges = new();
    private readonly List<StringName> _unlockedAchievementIds = new();
    private readonly List<CharacterKnowledgeChangeFact> _knowledgeChanges = new();
    private readonly List<CharacterAttributeChangeFact> _attributeChanges = new();

    public StringName member_id { get; set; } = "";

    public GStringNameArray leveled_skill_ids
    {
        get => BuildStringNameArray(_leveledSkillIds);
        set => SetLeveledSkillIds(value);
    }

    public GStringNameArray granted_skill_ids
    {
        get => BuildStringNameArray(_grantedSkillIds);
        set => SetGrantedSkillIds(value);
    }

    public GStringNameArray changed_profession_ids
    {
        get => BuildStringNameArray(_changedProfessionIds);
        set => SetChangedProfessionIds(value);
    }

    public int character_level_before { get; set; }

    public int character_level_after { get; set; }

    public GArray pending_profession_choices
    {
        get => BuildPendingProfessionChoicesArray();
        set => SetPendingProfessionChoices(value);
    }

    public bool needs_promotion_modal { get; set; }

    public GStringNameArray unlocked_achievement_ids
    {
        get => BuildStringNameArray(_unlockedAchievementIds);
        set => SetUnlockedAchievementIds(value);
    }

    internal IReadOnlyList<StringName> LeveledSkillIdsTyped => _leveledSkillIds;
    internal IReadOnlyList<StringName> GrantedSkillIdsTyped => _grantedSkillIds;
    internal IReadOnlyList<StringName> ChangedProfessionIdsTyped => _changedProfessionIds;
    internal IReadOnlyList<PendingProfessionChoice> PendingProfessionChoicesTyped =>
        _pendingProfessionChoices;
    internal IReadOnlyList<CharacterMasteryChangeFact> MasteryChangesTyped => _masteryChanges;
    internal IReadOnlyList<StringName> UnlockedAchievementIdsTyped => _unlockedAchievementIds;
    internal IReadOnlyList<CharacterKnowledgeChangeFact> KnowledgeChangesTyped => _knowledgeChanges;
    internal IReadOnlyList<CharacterAttributeChangeFact> AttributeChangesTyped => _attributeChanges;

    public void SetLeveledSkillIds(IEnumerable values)
    {
        SetUniqueStringNames(_leveledSkillIds, values);
    }

    public void AddLeveledSkillId(StringName skillId)
    {
        AddUniqueStringName(_leveledSkillIds, skillId);
    }

    public void SetGrantedSkillIds(IEnumerable values)
    {
        SetUniqueStringNames(_grantedSkillIds, values);
    }

    public void AddGrantedSkillId(StringName skillId)
    {
        AddUniqueStringName(_grantedSkillIds, skillId);
    }

    public void SetChangedProfessionIds(IEnumerable values)
    {
        SetUniqueStringNames(_changedProfessionIds, values);
    }

    public void AddChangedProfessionId(StringName professionId)
    {
        AddUniqueStringName(_changedProfessionIds, professionId);
    }

    public bool HasChangedProfessionId(StringName professionId)
    {
        return professionId != (StringName)"" && _changedProfessionIds.Contains(professionId);
    }

    public void SetPendingProfessionChoices(IEnumerable values)
    {
        _pendingProfessionChoices.Clear();
        if (values == null)
        {
            return;
        }
        foreach (object value in values)
        {
            if (value is PendingProfessionChoice choice)
            {
                AddPendingProfessionChoice(choice);
                continue;
            }
            if (TryParsePendingProfessionChoicePayload(value, out PendingProfessionChoice payloadChoice))
            {
                AddPendingProfessionChoice(payloadChoice);
            }
        }
    }

    public void AddPendingProfessionChoice(PendingProfessionChoice choice)
    {
        if (choice == null)
        {
            return;
        }
        _pendingProfessionChoices.Add(choice.DuplicateState());
    }

    public void AddMasteryChange(CharacterMasteryChangeFact change)
    {
        AddMasteryChangeEntry(_masteryChanges, change);
    }

    public void AppendMasteryChanges(IEnumerable<CharacterMasteryChangeFact> values)
    {
        AppendMasteryChangeEntries(_masteryChanges, values);
    }

    public void SetUnlockedAchievementIds(IEnumerable values)
    {
        SetUniqueStringNames(_unlockedAchievementIds, values);
    }

    public void AddUnlockedAchievementId(StringName achievementId)
    {
        AddUniqueStringName(_unlockedAchievementIds, achievementId);
    }

    public void AppendUnlockedAchievementIds(IEnumerable<StringName> values)
    {
        AppendUniqueStringNames(_unlockedAchievementIds, values);
    }

    public void AddKnowledgeChange(CharacterKnowledgeChangeFact change)
    {
        AddKnowledgeChangeEntry(_knowledgeChanges, change);
    }

    public void AppendKnowledgeChanges(IEnumerable<CharacterKnowledgeChangeFact> values)
    {
        AppendKnowledgeChangeEntries(_knowledgeChanges, values);
    }

    public void AddAttributeChange(CharacterAttributeChangeFact change)
    {
        AddAttributeChangeEntry(_attributeChanges, change);
    }

    public void AppendAttributeChanges(IEnumerable<CharacterAttributeChangeFact> values)
    {
        AppendAttributeChangeEntries(_attributeChanges, values);
    }

    public CharacterProgressionDelta DuplicateState()
    {
        var copy = new CharacterProgressionDelta
        {
            member_id = member_id,
            character_level_before = character_level_before,
            character_level_after = character_level_after,
            needs_promotion_modal = needs_promotion_modal,
        };
        copy.SetLeveledSkillIds(_leveledSkillIds);
        copy.SetGrantedSkillIds(_grantedSkillIds);
        copy.SetChangedProfessionIds(_changedProfessionIds);
        copy.SetPendingProfessionChoices(_pendingProfessionChoices);
        copy.AppendMasteryChanges(_masteryChanges);
        copy.SetUnlockedAchievementIds(_unlockedAchievementIds);
        copy.AppendKnowledgeChanges(_knowledgeChanges);
        copy.AppendAttributeChanges(_attributeChanges);
        return copy;
    }

    public GDictionary ToDictionary() =>
        new()
        {
            ["member_id"] = member_id,
            ["leveled_skill_ids"] = BuildStringNamePayloadArray(_leveledSkillIds),
            ["granted_skill_ids"] = BuildStringNamePayloadArray(_grantedSkillIds),
            ["changed_profession_ids"] = BuildStringNamePayloadArray(_changedProfessionIds),
            ["character_level_before"] = character_level_before,
            ["character_level_after"] = character_level_after,
            ["pending_profession_choices"] = BuildPendingProfessionChoicesArray(),
            ["needs_promotion_modal"] = needs_promotion_modal,
            ["unlocked_achievement_ids"] = BuildStringNamePayloadArray(_unlockedAchievementIds),
            ["mastery_changes"] = BuildMasteryChangesArray(),
            ["knowledge_changes"] = BuildKnowledgeChangesArray(),
            ["attribute_changes"] = BuildAttributeChangesArray(),
        };

    public static CharacterProgressionDelta FromDictionary(GDictionary data)
    {
        if (data == null)
            return null;
        if (!TryGetStringName(data, "member_id", out StringName memberId))
            return null;
        if (!TryGetArray(data, "leveled_skill_ids", out GArray leveledSkillIds))
            return null;
        if (!TryGetArray(data, "granted_skill_ids", out GArray grantedSkillIds))
            return null;
        if (!TryGetArray(data, "changed_profession_ids", out GArray changedProfessionIds))
            return null;
        if (!TryGetInt(data, "character_level_before", out int levelBefore))
            return null;
        if (!TryGetInt(data, "character_level_after", out int levelAfter))
            return null;
        if (!TryGetArray(data, "pending_profession_choices", out GArray pendingChoices))
            return null;
        if (!TryGetBool(data, "needs_promotion_modal", out bool needsPromotionModal))
            return null;
        if (!TryGetArray(data, "unlocked_achievement_ids", out GArray unlockedAchievementIds))
            return null;
        if (!TryGetArray(data, "mastery_changes", out GArray masteryChanges))
            return null;
        if (!TryGetArray(data, "knowledge_changes", out GArray knowledgeChanges))
            return null;
        if (!TryGetArray(data, "attribute_changes", out GArray attributeChanges))
            return null;

        var result = new CharacterProgressionDelta
        {
            member_id = memberId,
            character_level_before = levelBefore,
            character_level_after = levelAfter,
            needs_promotion_modal = needsPromotionModal,
        };
        result.SetLeveledSkillIds(leveledSkillIds);
        result.SetGrantedSkillIds(grantedSkillIds);
        result.SetChangedProfessionIds(changedProfessionIds);
        result.SetPendingProfessionChoices(pendingChoices);
        result.SetUnlockedAchievementIds(unlockedAchievementIds);
        if (!AppendMasteryChangesFromPayload(result, masteryChanges))
            return null;
        if (!AppendKnowledgeChangesFromPayload(result, knowledgeChanges))
            return null;
        if (!AppendAttributeChangesFromPayload(result, attributeChanges))
            return null;
        return result;
    }

    private static void SetUniqueStringNames(List<StringName> target, IEnumerable values)
    {
        target.Clear();
        if (values == null)
        {
            return;
        }
        foreach (object value in values)
        {
            AddUniqueStringName(target, ProgressionDataUtils.to_string_name(value));
        }
    }

    private static void AddUniqueStringName(List<StringName> target, StringName value)
    {
        if (value == (StringName)"" || target.Contains(value))
        {
            return;
        }
        target.Add(value);
    }

    private static void AppendUniqueStringNames(
        List<StringName> target,
        IEnumerable<StringName> values
    )
    {
        if (values == null)
        {
            return;
        }
        foreach (StringName value in values)
        {
            AddUniqueStringName(target, value);
        }
    }

    private static void AddMasteryChangeEntry(
        List<CharacterMasteryChangeFact> target,
        CharacterMasteryChangeFact value
    )
    {
        if (value == null || value.SkillId == (StringName)"")
        {
            return;
        }
        target.Add(
            new CharacterMasteryChangeFact(
                value.SkillId,
                value.SkillName,
                value.MasteryAmount,
                value.SourceType,
                value.SourceLabel,
                value.ReasonText
            )
        );
    }

    private static void AppendMasteryChangeEntries(
        List<CharacterMasteryChangeFact> target,
        IEnumerable<CharacterMasteryChangeFact> values
    )
    {
        if (values == null)
        {
            return;
        }
        foreach (CharacterMasteryChangeFact value in values)
        {
            AddMasteryChangeEntry(target, value);
        }
    }

    private static void AddKnowledgeChangeEntry(
        List<CharacterKnowledgeChangeFact> target,
        CharacterKnowledgeChangeFact value
    )
    {
        if (value == null || value.KnowledgeId == (StringName)"")
        {
            return;
        }
        target.Add(
            new CharacterKnowledgeChangeFact(
                value.KnowledgeId,
                value.KnowledgeLabel,
                value.ReasonText
            )
        );
    }

    private static void AppendKnowledgeChangeEntries(
        List<CharacterKnowledgeChangeFact> target,
        IEnumerable<CharacterKnowledgeChangeFact> values
    )
    {
        if (values == null)
        {
            return;
        }
        foreach (CharacterKnowledgeChangeFact value in values)
        {
            AddKnowledgeChangeEntry(target, value);
        }
    }

    private static void AddAttributeChangeEntry(
        List<CharacterAttributeChangeFact> target,
        CharacterAttributeChangeFact value
    )
    {
        if (value == null || value.AttributeId == (StringName)"")
        {
            return;
        }
        target.Add(
            new CharacterAttributeChangeFact(
                value.AttributeId,
                value.AttributeLabel,
                value.Delta,
                value.ReasonText,
                value.ProgressDelta,
                value.ProgressBefore,
                value.ProgressAfter,
                value.AttributeBefore,
                value.AttributeAfter
            )
        );
    }

    private static void AppendAttributeChangeEntries(
        List<CharacterAttributeChangeFact> target,
        IEnumerable<CharacterAttributeChangeFact> values
    )
    {
        if (values == null)
        {
            return;
        }
        foreach (CharacterAttributeChangeFact value in values)
        {
            AddAttributeChangeEntry(target, value);
        }
    }

    private static GStringNameArray BuildStringNameArray(IEnumerable<StringName> values)
    {
        var result = new GStringNameArray();
        foreach (StringName value in values)
        {
            result.Add(value);
        }
        return result;
    }

    private static GArray BuildStringNamePayloadArray(IEnumerable<StringName> values)
    {
        var result = new GArray();
        foreach (StringName value in values)
            result.Add(value);
        return result;
    }

    private GArray BuildPendingProfessionChoicesArray()
    {
        var result = new GArray();
        foreach (PendingProfessionChoice choice in _pendingProfessionChoices)
        {
            result.Add(choice?.ToDictionary() ?? new GDictionary());
        }
        return result;
    }

    private static bool TryParsePendingProfessionChoicePayload(
        object value,
        out PendingProfessionChoice choice
    )
    {
        choice = null;
        if (value is GDictionary dictionary)
        {
            choice = PendingProfessionChoice.FromDictionary(dictionary);
            return choice != null;
        }
        if (value is Variant variant && variant.VariantType == Variant.Type.Dictionary)
        {
            choice = PendingProfessionChoice.FromDictionary(variant.AsGodotDictionary());
            return choice != null;
        }
        return false;
    }

    private GArray BuildMasteryChangesArray()
    {
        var result = new GArray();
        foreach (CharacterMasteryChangeFact change in _masteryChanges)
        {
            if (change == null)
                continue;
            result.Add(
                new GDictionary
                {
                    ["skill_id"] = change.SkillId,
                    ["skill_name"] = change.SkillName,
                    ["mastery_amount"] = change.MasteryAmount,
                    ["source_type"] = change.SourceType,
                    ["source_label"] = change.SourceLabel,
                    ["reason_text"] = change.ReasonText,
                }
            );
        }
        return result;
    }

    private GArray BuildKnowledgeChangesArray()
    {
        var result = new GArray();
        foreach (CharacterKnowledgeChangeFact change in _knowledgeChanges)
        {
            if (change == null)
                continue;
            result.Add(
                new GDictionary
                {
                    ["knowledge_id"] = change.KnowledgeId,
                    ["knowledge_label"] = change.KnowledgeLabel,
                    ["reason_text"] = change.ReasonText,
                }
            );
        }
        return result;
    }

    private GArray BuildAttributeChangesArray()
    {
        var result = new GArray();
        foreach (CharacterAttributeChangeFact change in _attributeChanges)
        {
            if (change == null)
                continue;
            var payload = new GDictionary
            {
                ["attribute_id"] = change.AttributeId,
                ["attribute_label"] = change.AttributeLabel,
                ["delta"] = change.Delta,
                ["reason_text"] = change.ReasonText,
            };
            AddOptionalInt(payload, "progress_delta", change.ProgressDelta);
            AddOptionalInt(payload, "progress_before", change.ProgressBefore);
            AddOptionalInt(payload, "progress_after", change.ProgressAfter);
            AddOptionalInt(payload, "attribute_before", change.AttributeBefore);
            AddOptionalInt(payload, "attribute_after", change.AttributeAfter);
            result.Add(payload);
        }
        return result;
    }

    private static void AddOptionalInt(GDictionary payload, string key, int? value)
    {
        if (value.HasValue)
            payload[key] = value.Value;
    }

    private static bool AppendMasteryChangesFromPayload(
        CharacterProgressionDelta target,
        GArray values
    )
    {
        foreach (object value in values)
        {
            if (!TryAsDictionary(value, out GDictionary payload))
                return false;
            if (!TryGetStringName(payload, "skill_id", out StringName skillId))
                return false;
            if (!TryGetString(payload, "skill_name", out string skillName))
                return false;
            if (!TryGetInt(payload, "mastery_amount", out int masteryAmount))
                return false;
            if (!TryGetStringName(payload, "source_type", out StringName sourceType))
                return false;
            if (!TryGetString(payload, "source_label", out string sourceLabel))
                return false;
            if (!TryGetString(payload, "reason_text", out string reasonText))
                return false;
            target.AddMasteryChange(
                new CharacterMasteryChangeFact(
                    skillId,
                    skillName,
                    masteryAmount,
                    sourceType,
                    sourceLabel,
                    reasonText
                )
            );
        }
        return true;
    }

    private static bool AppendKnowledgeChangesFromPayload(
        CharacterProgressionDelta target,
        GArray values
    )
    {
        foreach (object value in values)
        {
            if (!TryAsDictionary(value, out GDictionary payload))
                return false;
            if (!TryGetStringName(payload, "knowledge_id", out StringName knowledgeId))
                return false;
            if (!TryGetString(payload, "knowledge_label", out string knowledgeLabel))
                return false;
            if (!TryGetString(payload, "reason_text", out string reasonText))
                return false;
            target.AddKnowledgeChange(
                new CharacterKnowledgeChangeFact(knowledgeId, knowledgeLabel, reasonText)
            );
        }
        return true;
    }

    private static bool AppendAttributeChangesFromPayload(
        CharacterProgressionDelta target,
        GArray values
    )
    {
        foreach (object value in values)
        {
            if (!TryAsDictionary(value, out GDictionary payload))
                return false;
            if (!TryGetStringName(payload, "attribute_id", out StringName attributeId))
                return false;
            if (!TryGetString(payload, "attribute_label", out string attributeLabel))
                return false;
            if (!TryGetInt(payload, "delta", out int delta))
                return false;
            if (!TryGetString(payload, "reason_text", out string reasonText))
                return false;
            if (!TryGetOptionalInt(payload, "progress_delta", out int? progressDelta))
                return false;
            if (!TryGetOptionalInt(payload, "progress_before", out int? progressBefore))
                return false;
            if (!TryGetOptionalInt(payload, "progress_after", out int? progressAfter))
                return false;
            if (!TryGetOptionalInt(payload, "attribute_before", out int? attributeBefore))
                return false;
            if (!TryGetOptionalInt(payload, "attribute_after", out int? attributeAfter))
                return false;
            target.AddAttributeChange(
                new CharacterAttributeChangeFact(
                    attributeId,
                    attributeLabel,
                    delta,
                    reasonText,
                    progressDelta,
                    progressBefore,
                    progressAfter,
                    attributeBefore,
                    attributeAfter
                )
            );
        }
        return true;
    }

    private static bool TryGetStringName(GDictionary data, string key, out StringName value)
    {
        value = default;
        if (!TryGetRawValue(data, key, out object rawValue))
            return false;
        return TryAsStringName(rawValue, out value);
    }

    private static bool TryGetString(GDictionary data, string key, out string value)
    {
        value = "";
        if (!TryGetRawValue(data, key, out object rawValue))
            return false;
        if (rawValue is Variant variant)
        {
            if (variant.VariantType != Variant.Type.String)
                return false;
            value = variant.AsString();
            return true;
        }
        if (rawValue is string stringValue)
        {
            value = stringValue;
            return true;
        }
        return false;
    }

    private static bool TryGetInt(GDictionary data, string key, out int value)
    {
        value = 0;
        if (!TryGetRawValue(data, key, out object rawValue))
            return false;
        return TryAsInt(rawValue, out value);
    }

    private static bool TryGetOptionalInt(GDictionary data, string key, out int? value)
    {
        value = null;
        if (!TryGetRawValue(data, key, out object rawValue))
            return true;
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Nil)
            return true;
        if (!TryAsInt(rawValue, out int parsed))
            return false;
        value = parsed;
        return true;
    }

    private static bool TryGetBool(GDictionary data, string key, out bool value)
    {
        value = false;
        if (!TryGetRawValue(data, key, out object rawValue))
            return false;
        if (rawValue is Variant variant)
        {
            if (variant.VariantType != Variant.Type.Bool)
                return false;
            value = variant.AsBool();
            return true;
        }
        if (rawValue is bool boolValue)
        {
            value = boolValue;
            return true;
        }
        return false;
    }

    private static bool TryGetArray(GDictionary data, string key, out GArray value)
    {
        value = null;
        if (!TryGetRawValue(data, key, out object rawValue))
            return false;
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Array)
        {
            value = variant.AsGodotArray();
            return true;
        }
        if (rawValue is GArray array)
        {
            value = array;
            return true;
        }
        value = null;
        return false;
    }

    private static bool TryAsDictionary(object rawValue, out GDictionary value)
    {
        if (rawValue is GDictionary dictionary)
        {
            value = dictionary;
            return true;
        }
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Dictionary)
        {
            value = variant.AsGodotDictionary();
            return true;
        }
        value = null;
        return false;
    }

    private static bool TryAsStringName(object rawValue, out StringName value)
    {
        value = default;
        if (rawValue is Variant variant)
        {
            if (variant.VariantType == Variant.Type.StringName)
            {
                value = variant.AsStringName();
                return true;
            }
            if (variant.VariantType == Variant.Type.String)
            {
                value = new StringName(variant.AsString());
                return true;
            }
            return false;
        }
        if (rawValue is StringName stringName)
        {
            value = stringName;
            return true;
        }
        if (rawValue is string stringValue)
        {
            value = new StringName(stringValue);
            return true;
        }
        return false;
    }

    private static bool TryAsInt(object rawValue, out int value)
    {
        value = 0;
        if (rawValue is Variant variant)
        {
            if (variant.VariantType != Variant.Type.Int)
                return false;
            value = variant.AsInt32();
            return true;
        }
        if (rawValue is int intValue)
        {
            value = intValue;
            return true;
        }
        return false;
    }

    private static bool TryGetRawValue(GDictionary data, string key, out object value)
    {
        if (data != null && data.ContainsKey(key))
        {
            value = data[key];
            return true;
        }
        value = null;
        return false;
    }

}
