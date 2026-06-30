using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GDictionaryArray = Godot.Collections.Array<Godot.Collections.Dictionary>;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

// Partial slice of GameSession — content refresh + validation snapshot + typed def index builders.
// Pure physical split: same class, no behavior change. See GameSession.cs.
public partial class GameSession
{

    private void RefreshProgressionContent()
    {
        if (_progression_content_registry == null)
            return;
        _skillDefinitionIndex = new Dictionary<StringName, SkillDefinition>(
            _progression_content_registry.GetSkillDefinitionsTyped()
        );
        _professionDefIndex = new Dictionary<StringName, ProfessionDef>(
            _progression_content_registry.GetProfessionDefsTyped()
        );
        _profession_defs = ProjectResourceDictionary(_professionDefIndex);
        _achievementDefIndex = new Dictionary<StringName, AchievementDef>(
            _progression_content_registry.GetAchievementDefsTyped()
        );
        _achievement_defs = ProjectAchievementDefPayloadDictionary(_achievementDefIndex);
        _questDefIndex = new Dictionary<StringName, QuestDef>(
            _progression_content_registry.GetQuestDefsTyped()
        );
        _quest_defs = ProjectResourceDictionary(_questDefIndex);
    }

    private void RefreshBattleSpecialProfiles()
    {
        if (_battle_special_profile_registry == null)
            return;
        _battle_special_profile_registry.Rebuild(GetSkillDefinitionsTyped());
    }

    private void RefreshItemContent()
    {
        if (_item_content_registry == null)
            return;
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions = GetSkillDefinitionsTyped();
        Dictionary<StringName, ItemDef> itemDefs = new(_item_content_registry.GetItemDefsTyped());
        foreach (
            var entry in SkillBookItemFactory.BuildGeneratedItemDefs(skillDefinitions, itemDefs)
        )
        {
            itemDefs[entry.Key] = entry.Value;
        }
        _itemDefIndex = new Dictionary<StringName, ItemDef>(itemDefs);
        _item_defs = ProjectResourceDictionary(itemDefs);
    }

    private void RefreshRecipeContent()
    {
        if (_recipe_content_registry == null)
            return;
        _recipe_content_registry.Setup(GetItemDefsTyped());
        _recipeDefIndex = new Dictionary<StringName, RecipeDef>(
            _recipe_content_registry.GetRecipeDefsTyped()
        );
        _recipe_defs = ProjectResourceDictionary(_recipeDefIndex);
    }

    private void RefreshEnemyContent()
    {
        if (_enemy_content_registry == null)
            return;
        _enemyTemplateIndex = new Dictionary<StringName, EnemyTemplateDef>(
            _enemy_content_registry.GetEnemyTemplatesTyped()
        );
        _enemyAiBrainIndex = new Dictionary<StringName, EnemyAiBrainDef>(
            _enemy_content_registry.GetEnemyAiBrainsTyped()
        );
        _wildEncounterRosterIndex = new Dictionary<StringName, WildEncounterRosterDef>(
            _enemy_content_registry.GetWildEncounterRostersTyped()
        );
        _enemy_templates = ProjectResourceDictionary(_enemyTemplateIndex);
        _enemy_ai_brains = ProjectResourceDictionary(_enemyAiBrainIndex);
        _wild_encounter_rosters = ProjectResourceDictionary(_wildEncounterRosterIndex);
    }

    // 把 session 当前的正式内容缓存推进到 GameContentCatalog 自己的 typed 快照里。
    // 在任何会改变 progression / item / recipe / enemy / battle special profile 内容的刷新后调用。
    private void RefreshContentCatalog()
    {
        EnsureGameRoot().GetContentCatalogTyped().Rebuild(this);
    }

    // 回归测试用的显式刷新入口：让测试在直接改动 session 内容缓存后手动重建 catalog 快照，
    // 以验证 catalog getter 不是 session getter 的 live 转发。不要为此暴露 public Godot API。
    internal void RefreshContentCatalogForTests()
    {
        RefreshContentCatalog();
    }

    private void RefreshContentValidationSnapshotState()
    {
        RefreshBattleSpecialProfiles();
        var snapshot = new ContentValidationSnapshotData();
        snapshot.Domains["progression"] = BuildContentValidationDomainSnapshot(
            _progression_content_registry
        );
        snapshot.Domains["battle_special_profile"] = BuildContentValidationDomainSnapshotFromErrors(
            _battle_special_profile_registry?.ValidateTyped()
        );
        snapshot.Domains["item"] = BuildItemContentValidationDomainSnapshot();
        snapshot.Domains["recipe"] = BuildContentValidationDomainSnapshotFromErrors(
            _recipe_content_registry?.ValidateTyped()
        );
        snapshot.Domains["enemy"] = BuildContentValidationDomainSnapshotFromErrors(
            _enemy_content_registry?.ValidateTyped()
        );
        snapshot.Domains["world"] = BuildWorldContentValidationDomainSnapshot();
        snapshot.Domains["quest"] = BuildQuestContentValidationDomainSnapshot();
        _contentValidationSnapshotData = snapshot;
        // 验证刷新会重建 battle special profile registry，需要把新的 snapshot 推进 catalog，
        // 否则运行期再次走验证门时 catalog 的 battle special profile 视图会落后。
        RefreshContentCatalog();
    }

    private static ContentValidationDomainSnapshotData BuildContentValidationDomainSnapshot(
        IValidatableRegistry registry
    )
    {
        return BuildContentValidationDomainSnapshotFromErrors(registry?.ValidateTyped());
    }

    private ContentValidationDomainSnapshotData BuildWorldContentValidationDomainSnapshot()
    {
        if (_world_content_validator == null)
            return new ContentValidationDomainSnapshotData();
        return BuildContentValidationDomainSnapshotFromErrors(
            _world_content_validator.ValidateWorldPresets(_enemy_templates, _wild_encounter_rosters)
        );
    }

    private ContentValidationDomainSnapshotData BuildItemContentValidationDomainSnapshot()
    {
        var errors = new List<string>();
        AppendErrors(errors, _item_content_registry?.ValidateTyped());
        if (
            _item_defs != null
            && _skillDefinitionIndex != null
            && _item_defs.Count > 0
            && _skillDefinitionIndex.Count > 0
        )
        {
            Dictionary<StringName, ItemDef> itemDefs = BuildItemDefIndex(_item_defs);
            IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions =
                GetSkillDefinitionsTyped();
            AppendErrors(errors, SkillBookItemContentValidator.Validate(itemDefs, skillDefinitions));
        }
        return BuildContentValidationDomainSnapshotFromErrors(errors);
    }

    private ContentValidationDomainSnapshotData BuildQuestContentValidationDomainSnapshot()
    {
        var registrationErrors = new List<string>();
        if (_progression_content_registry != null)
        {
            AppendErrors(
                registrationErrors,
                _progression_content_registry.GetQuestRegistrationErrorsTyped()
            );
        }
        Dictionary<StringName, QuestDef> questDefs = BuildQuestDefIndex(_quest_defs);
        Dictionary<StringName, ItemDef> itemDefs = BuildItemDefIndex(_item_defs);
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions =
            GetSkillDefinitionsTyped();
        Dictionary<StringName, EnemyTemplateDef> enemyTemplates = BuildEnemyTemplateIndex(
            _enemy_templates
        );
        return BuildContentValidationDomainSnapshotFromErrors(
            QuestContentValidator.ValidateTyped(
                questDefs,
                itemDefs,
                skillDefinitions,
                enemyTemplates,
                registrationErrors
            )
        );
    }

    private static Dictionary<StringName, QuestDef> BuildQuestDefIndex(GDictionary questDefs)
    {
        var result = new Dictionary<StringName, QuestDef>();
        if (questDefs == null)
            return result;
        foreach (var key in questDefs.Keys)
        {
            if (key.VariantType != Variant.Type.StringName)
                continue;
            QuestDef questDef = questDefs[key].AsGodotObject() as QuestDef;
            if (questDef == null || questDef.quest_id == "")
                continue;
            result[key.AsStringName()] = questDef;
        }
        return result;
    }

    private static Dictionary<StringName, ItemDef> BuildItemDefIndex(GDictionary itemDefs)
    {
        var result = new Dictionary<StringName, ItemDef>();
        if (itemDefs == null)
            return result;
        foreach (Variant key in itemDefs.Keys)
        {
            if (key.VariantType != Variant.Type.StringName)
                continue;
            ItemDef itemDef = itemDefs[key].AsGodotObject() as ItemDef;
            if (itemDef == null || itemDef.item_id == "")
                continue;
            result[key.AsStringName()] = itemDef;
        }
        return result;
    }

    private static Dictionary<StringName, RecipeDef> BuildRecipeDefIndex(GDictionary recipeDefs)
    {
        var result = new Dictionary<StringName, RecipeDef>();
        if (recipeDefs == null)
            return result;
        foreach (Variant key in recipeDefs.Keys)
        {
            if (key.VariantType != Variant.Type.StringName)
                continue;
            RecipeDef recipeDef = recipeDefs[key].AsGodotObject() as RecipeDef;
            if (recipeDef == null || recipeDef.recipe_id == "")
                continue;
            result[key.AsStringName()] = recipeDef;
        }
        return result;
    }

    private static Dictionary<StringName, EnemyTemplateDef> BuildEnemyTemplateIndex(
        GDictionary enemyTemplates
    )
    {
        var result = new Dictionary<StringName, EnemyTemplateDef>();
        if (enemyTemplates == null)
            return result;
        foreach (Variant key in enemyTemplates.Keys)
        {
            if (key.VariantType != Variant.Type.StringName)
                continue;
            EnemyTemplateDef enemyTemplate = enemyTemplates[key].AsGodotObject() as EnemyTemplateDef;
            if (enemyTemplate == null || enemyTemplate.template_id == "")
                continue;
            result[key.AsStringName()] = enemyTemplate;
        }
        return result;
    }

    private static Dictionary<StringName, EnemyAiBrainDef> BuildEnemyAiBrainIndex(
        GDictionary enemyAiBrains
    )
    {
        var result = new Dictionary<StringName, EnemyAiBrainDef>();
        if (enemyAiBrains == null)
            return result;
        foreach (Variant key in enemyAiBrains.Keys)
        {
            if (key.VariantType != Variant.Type.StringName)
                continue;
            EnemyAiBrainDef enemyAiBrain = enemyAiBrains[key].AsGodotObject() as EnemyAiBrainDef;
            if (enemyAiBrain == null || enemyAiBrain.brain_id == "")
                continue;
            result[key.AsStringName()] = enemyAiBrain;
        }
        return result;
    }

    private static Dictionary<StringName, WildEncounterRosterDef> BuildWildEncounterRosterIndex(
        GDictionary wildEncounterRosters
    )
    {
        var result = new Dictionary<StringName, WildEncounterRosterDef>();
        if (wildEncounterRosters == null)
            return result;
        foreach (Variant key in wildEncounterRosters.Keys)
        {
            if (key.VariantType != Variant.Type.StringName)
                continue;
            WildEncounterRosterDef roster = wildEncounterRosters[key].AsGodotObject()
                as WildEncounterRosterDef;
            if (roster == null || roster.profile_id == "")
                continue;
            result[key.AsStringName()] = roster;
        }
        return result;
    }

    private static GDictionary ProjectResourceDictionary<T>(IReadOnlyDictionary<StringName, T> values)
        where T : RefCounted
    {
        var result = new GDictionary();
        if (values == null)
            return RegisterContentProjectionWrapper(
                result,
                $"GameSession.ProjectResourceDictionary:{typeof(T).Name}:empty"
            );
        foreach ((StringName id, T value) in values)
        {
            if (id == default || id == (StringName)"" || value == null)
                continue;
            result[id] = value;
        }
        return RegisterContentProjectionWrapper(
            result,
            $"GameSession.ProjectResourceDictionary:{typeof(T).Name}"
        );
    }

    private static Dictionary<StringName, ProfessionDef> BuildProfessionDefIndex(
        GDictionary professionDefs
    )
    {
        var result = new Dictionary<StringName, ProfessionDef>();
        if (professionDefs == null)
            return result;
        foreach (Variant key in professionDefs.Keys)
        {
            if (key.VariantType != Variant.Type.StringName)
                continue;
            ProfessionDef professionDef = professionDefs[key].AsGodotObject() as ProfessionDef;
            if (professionDef == null || professionDef.profession_id == "")
                continue;
            result[key.AsStringName()] = professionDef;
        }
        return result;
    }

    private static Dictionary<StringName, AchievementDef> BuildAchievementDefIndex(
        GDictionary achievementDefs
    )
    {
        var result = new Dictionary<StringName, AchievementDef>();
        if (achievementDefs == null)
            return result;
        foreach (Variant key in achievementDefs.Keys)
        {
            if (key.VariantType != Variant.Type.StringName)
                continue;
            Variant value = achievementDefs[key];
            if (value.VariantType != Variant.Type.Dictionary)
                continue;
            AchievementDef achievementDef = AchievementDef.FromDictionary(value.AsGodotDictionary());
            if (achievementDef == null || achievementDef.achievement_id == "")
                continue;
            result[key.AsStringName()] = achievementDef;
        }
        return result;
    }

    private static GDictionary ProjectAchievementDefPayloadDictionary(
        IReadOnlyDictionary<StringName, AchievementDef> values
    )
    {
        var result = new GDictionary();
        if (values == null)
            return RegisterContentProjectionWrapper(
                result,
                "GameSession.ProjectAchievementDefPayloadDictionary:empty"
            );
        foreach ((StringName id, AchievementDef value) in values)
        {
            if (id == "" || value == null)
                continue;
            result[id] = value.ToDictionary();
        }
        return RegisterContentProjectionWrapper(
            result,
            "GameSession.ProjectAchievementDefPayloadDictionary"
        );
    }

    private static ContentValidationDomainSnapshotData BuildContentValidationDomainSnapshotFromErrors(
        IEnumerable<string> errors
    )
    {
        var snapshot = new ContentValidationDomainSnapshotData();
        AppendErrors(snapshot.Errors, errors);
        return snapshot;
    }

    private int RequireContentValidationForRuntime(StringName operation_id)
    {
        RefreshContentValidationSnapshotState();
        if (IsContentValidationOk())
            return (int)Error.Ok;
        int errorCount = _contentValidationSnapshotData?.ErrorCount ?? 0;
        PushSessionError(
            "session.content.validation_blocked",
            "GameSession blocked formal runtime entry because content validation failed.",
            Json.Stringify(new GDictionary
            {
                ["operation_id"] = operation_id.ToString(),
                ["error_count"] = errorCount,
            })
        );
        return (int)Error.InvalidData;
    }

    private void ReportContentValidationErrors()
    {
        foreach (string domainId in ContentValidationDomainOrder)
        {
            foreach (
                string validationError in _contentValidationSnapshotData.EnumerateDomainErrors(domainId)
            )
                ReportContentValidationError(domainId, validationError);
        }
    }

    private void ReportContentValidationError(string domain_id, string validation_error)
    {
        switch (domain_id)
        {
            case "progression":
                PushSessionError(
                    "session.content.progression_validation_failed",
                    $"Progression content error: {validation_error}"
                );
                break;
            case "battle_special_profile":
                PushSessionError(
                    "session.content.battle_special_profile_validation_failed",
                    $"Battle special profile content error: {validation_error}"
                );
                break;
            case "item":
                PushSessionError(
                    "session.content.item_validation_failed",
                    $"Item content error: {validation_error}"
                );
                break;
            case "recipe":
                PushSessionError(
                    "session.content.recipe_validation_failed",
                    $"Recipe content error: {validation_error}"
                );
                break;
            case "enemy":
                PushSessionError(
                    "session.content.enemy_validation_failed",
                    $"Enemy content error: {validation_error}"
                );
                break;
            case "world":
                PushSessionError(
                    "session.content.world_validation_failed",
                    $"World content error: {validation_error}"
                );
                break;
            case "quest":
                PushSessionError(
                    "session.content.quest_validation_failed",
                    $"Quest content error: {validation_error}"
                );
                break;
        }
    }
}
