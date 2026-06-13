using System.Collections;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class CharacterProgressionDelta : RefCounted
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

    public Godot.Collections.Array<GDictionary> mastery_changes
    {
        get => BuildMasteryChangeArray(_masteryChanges);
        set => SetMasteryChanges(value);
    }

    public GStringNameArray unlocked_achievement_ids
    {
        get => BuildStringNameArray(_unlockedAchievementIds);
        set => SetUnlockedAchievementIds(value);
    }

    public Godot.Collections.Array<GDictionary> knowledge_changes
    {
        get => BuildKnowledgeChangeArray(_knowledgeChanges);
        set => SetKnowledgeChanges(value);
    }

    public Godot.Collections.Array<GDictionary> attribute_changes
    {
        get => BuildAttributeChangeArray(_attributeChanges);
        set => SetAttributeChanges(value);
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

    public void SetMasteryChanges(IEnumerable values)
    {
        SetMasteryChangeEntries(_masteryChanges, values);
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

    public void SetKnowledgeChanges(IEnumerable values)
    {
        SetKnowledgeChangeEntries(_knowledgeChanges, values);
    }

    public void AddKnowledgeChange(CharacterKnowledgeChangeFact change)
    {
        AddKnowledgeChangeEntry(_knowledgeChanges, change);
    }

    public void AppendKnowledgeChanges(IEnumerable<CharacterKnowledgeChangeFact> values)
    {
        AppendKnowledgeChangeEntries(_knowledgeChanges, values);
    }

    public void SetAttributeChanges(IEnumerable values)
    {
        SetAttributeChangeEntries(_attributeChanges, values);
    }

    public void AddAttributeChange(CharacterAttributeChangeFact change)
    {
        AddAttributeChangeEntry(_attributeChanges, change);
    }

    public void AppendAttributeChanges(IEnumerable<CharacterAttributeChangeFact> values)
    {
        AppendAttributeChangeEntries(_attributeChanges, values);
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

    private static void SetMasteryChangeEntries(
        List<CharacterMasteryChangeFact> target,
        IEnumerable values
    )
    {
        target.Clear();
        if (values == null)
        {
            return;
        }
        foreach (object value in values)
        {
            if (value is CharacterMasteryChangeFact fact)
            {
                AddMasteryChangeEntry(target, fact);
            }
            else if (value is GDictionary dict)
            {
                AddMasteryChangeEntry(target, CharacterMasteryChangeFact.FromDictionary(dict));
            }
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

    private static void SetKnowledgeChangeEntries(
        List<CharacterKnowledgeChangeFact> target,
        IEnumerable values
    )
    {
        target.Clear();
        if (values == null)
        {
            return;
        }
        foreach (object value in values)
        {
            if (value is CharacterKnowledgeChangeFact fact)
            {
                AddKnowledgeChangeEntry(target, fact);
            }
            else if (value is GDictionary dict)
            {
                AddKnowledgeChangeEntry(target, CharacterKnowledgeChangeFact.FromDictionary(dict));
            }
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

    private static void SetAttributeChangeEntries(
        List<CharacterAttributeChangeFact> target,
        IEnumerable values
    )
    {
        target.Clear();
        if (values == null)
        {
            return;
        }
        foreach (object value in values)
        {
            if (value is CharacterAttributeChangeFact fact)
            {
                AddAttributeChangeEntry(target, fact);
            }
            else if (value is GDictionary dict)
            {
                AddAttributeChangeEntry(target, CharacterAttributeChangeFact.FromDictionary(dict));
            }
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

    private static void SetDictionaryEntries(List<GDictionary> target, IEnumerable values)
    {
        target.Clear();
        if (values == null)
        {
            return;
        }
        foreach (object value in values)
        {
            if (value is GDictionary dict)
            {
                AddDictionaryEntry(target, dict);
            }
        }
    }

    private static void AddDictionaryEntry(List<GDictionary> target, GDictionary value)
    {
        if (value == null || value.Count == 0)
        {
            return;
        }
        target.Add((GDictionary)value.Duplicate(true));
    }

    private static void AppendDictionaryEntries(List<GDictionary> target, IEnumerable<GDictionary> values)
    {
        if (values == null)
        {
            return;
        }
        foreach (GDictionary value in values)
        {
            AddDictionaryEntry(target, value);
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

    private GArray BuildPendingProfessionChoicesArray()
    {
        var result = new GArray();
        foreach (PendingProfessionChoice choice in _pendingProfessionChoices)
        {
            result.Add(choice?.DuplicateState());
        }
        return result;
    }

    private static Godot.Collections.Array<GDictionary> BuildDictionaryArray(
        IEnumerable<GDictionary> values
    )
    {
        var result = new Godot.Collections.Array<GDictionary>();
        foreach (GDictionary value in values)
        {
            result.Add(value != null ? (GDictionary)value.Duplicate(true) : new GDictionary());
        }
        return result;
    }

    private static Godot.Collections.Array<GDictionary> BuildMasteryChangeArray(
        IEnumerable<CharacterMasteryChangeFact> values
    )
    {
        var result = new Godot.Collections.Array<GDictionary>();
        foreach (CharacterMasteryChangeFact value in values)
        {
            result.Add(value?.ToDictionary() ?? new GDictionary());
        }
        return result;
    }

    private static Godot.Collections.Array<GDictionary> BuildKnowledgeChangeArray(
        IEnumerable<CharacterKnowledgeChangeFact> values
    )
    {
        var result = new Godot.Collections.Array<GDictionary>();
        foreach (CharacterKnowledgeChangeFact value in values)
        {
            result.Add(value?.ToDictionary() ?? new GDictionary());
        }
        return result;
    }

    private static Godot.Collections.Array<GDictionary> BuildAttributeChangeArray(
        IEnumerable<CharacterAttributeChangeFact> values
    )
    {
        var result = new Godot.Collections.Array<GDictionary>();
        foreach (CharacterAttributeChangeFact value in values)
        {
            result.Add(value?.ToDictionary() ?? new GDictionary());
        }
        return result;
    }
}
