using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GArray = Godot.Collections.Array;
using GBattleUnitArray = System.Collections.Generic.List<BattleUnitState>;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

// Partial slice of BattleRuntimeModule — skill/enemy/item/special-profile content catalog sync + typed getters.
// Pure physical split: same class, no behavior change. See BattleRuntimeModule.cs.
public sealed partial class BattleRuntimeModule
{

    internal IReadOnlyDictionary<StringName, SkillDefinition> GetSkillDefinitionIndexTyped() =>
        _skillDefinitionIndex;

    internal SkillDefinition GetSkillDefinitionTyped(StringName skillId)
    {
        StringName normalizedSkillId = ProgressionDataUtils.to_string_name(skillId);
        return normalizedSkillId != ""
            && _skillDefinitionIndex != null
            && _skillDefinitionIndex.TryGetValue(
                normalizedSkillId,
                out SkillDefinition skillDefinition
            )
            ? skillDefinition
            : null;
    }

    internal void SyncContentCatalogsTyped(
        IReadOnlyDictionary<StringName, ItemDef> itemDefs,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions = null
    )
    {
        IReadOnlyDictionary<StringName, SkillDefinition> catalogSkillDefinitions =
            _skillCatalog?.GetSkillDefinitionsTyped();
        IReadOnlyDictionary<StringName, SkillDefinition> resolvedSkillDefinitions =
            skillDefinitions
            ?? catalogSkillDefinitions;
        ApplySkillDefinitionsTyped(resolvedSkillDefinitions);
        ApplyItemDefsTyped(itemDefs);
    }

    internal IReadOnlyDictionary<StringName, EnemyTemplateDef> GetEnemyTemplateIndexTyped() =>
        _enemyTemplateIndex;

    internal GDictionary GetSpecialProfileRegistrySnapshotPayload() =>
        RuntimePlainPayload.ProjectDictionary(
            _special_profile_registry_snapshot,
            "BattleRuntimeModule.GetSpecialProfileRegistrySnapshotPayload"
        );

    internal IBattleSpecialProfileView GetSpecialProfileView() =>
        _special_profile_view ?? BattleSpecialProfileRuntimeView.Empty;

    internal EnemyTemplateDef GetEnemyTemplateTyped(StringName templateId)
    {
        if (IsEmpty(templateId))
        {
            return null;
        }
        return _enemyTemplateIndex.TryGetValue(templateId, out EnemyTemplateDef template)
            ? template
            : null;
    }

    internal void ReplaceEnemyTemplatesTyped(
        IReadOnlyDictionary<StringName, EnemyTemplateDef> enemyTemplates
    )
    {
        ApplyEnemyTemplatesTyped(enemyTemplates);
    }

    private void ApplySkillDefinitionsTyped(
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions
    )
    {
        _runtime_services.ClearRuntimeBindings();
        _skillDefinitionIndex.Clear();
        if (skillDefinitions == null || skillDefinitions.Count == 0)
        {
            return;
        }
        foreach ((StringName skillId, SkillDefinition skillDefinition) in skillDefinitions)
        {
            if (skillId == "" || skillDefinition == null || skillDefinition.SkillId == "")
            {
                continue;
            }
            _skillDefinitionIndex[skillDefinition.SkillId] = skillDefinition;
        }
    }

    private void ApplyEnemyTemplatesTyped(
        IReadOnlyDictionary<StringName, EnemyTemplateDef> enemyTemplates
    )
    {
        _enemyTemplateIndex.Clear();
        if (enemyTemplates == null || enemyTemplates.Count == 0)
        {
            return;
        }
        foreach ((StringName templateId, EnemyTemplateDef template) in enemyTemplates)
        {
            if (templateId == "" || template == null || template.template_id == "")
            {
                continue;
            }
            _enemyTemplateIndex[template.template_id] = template;
        }
    }

    private void ApplyEnemyAiBrainsTyped(
        IReadOnlyDictionary<StringName, EnemyAiBrainDef> enemyAiBrains
    )
    {
        _enemyAiBrainIndex.Clear();
        if (enemyAiBrains == null || enemyAiBrains.Count == 0)
        {
            return;
        }
        foreach ((StringName brainId, EnemyAiBrainDef brain) in enemyAiBrains)
        {
            if (brainId == "" || brain == null || brain.brain_id == "")
            {
                continue;
            }
            _enemyAiBrainIndex[brain.brain_id] = brain;
        }
    }

    private void ReplaceSpecialProfileRegistrySnapshot(GDictionary snapshot)
    {
        _special_profile_registry_snapshot.Clear();
        if (snapshot == null)
        {
            return;
        }

        Dictionary<string, object> normalized =
            RuntimePlainPayload.NormalizeDictionary(
                snapshot,
                "BattleRuntimeModule.special_profile_registry_snapshot"
            );
        foreach (KeyValuePair<string, object> entry in normalized)
        {
            _special_profile_registry_snapshot[entry.Key] = entry.Value;
        }
    }

    internal void ReplaceEnemyAiBrainsTyped(
        IReadOnlyDictionary<StringName, EnemyAiBrainDef> enemyAiBrains
    )
    {
        ApplyEnemyAiBrainsTyped(enemyAiBrains);
        _ai_service.Setup(_enemyAiBrainIndex, _damage_resolver);
    }

    private void ApplyItemDefsTyped(IReadOnlyDictionary<StringName, ItemDef> itemDefs)
    {
        _itemDefIndex.Clear();
        if (itemDefs == null || itemDefs.Count == 0)
        {
            return;
        }
        foreach ((StringName itemId, ItemDef itemDef) in itemDefs)
        {
            if (itemId == "" || itemDef == null || itemDef.item_id == "")
            {
                continue;
            }
            _itemDefIndex[itemDef.item_id] = itemDef;
        }
    }

    private EnemyAiBrainDef GetEnemyAiBrainTyped(StringName brainId)
    {
        if (IsEmpty(brainId))
        {
            return null;
        }
        return _enemyAiBrainIndex.TryGetValue(brainId, out EnemyAiBrainDef brain)
            ? brain
            : null;
    }

    internal IReadOnlyDictionary<StringName, EnemyAiBrainDef> GetEnemyAiBrainIndexTyped() =>
        _enemyAiBrainIndex;

    internal IReadOnlyDictionary<StringName, ItemDef> GetItemDefIndexTyped() => _itemDefIndex;

    internal void _append_special_profile_gate_block(
        BattleEventBatch batch,
        BattleSpecialProfileGateResult gate_result
    )
    {
        if (batch == null)
            return;
        string message = "该禁咒配置未通过校验，暂时无法施放。";
        if (gate_result != null && !string.IsNullOrEmpty(gate_result.PlayerMessage))
            message = gate_result.PlayerMessage;
        if (string.IsNullOrEmpty(message))
            message = "该禁咒配置未通过校验，暂时无法施放。";
        batch.AddLogLine(message);
    }

    internal Dictionary<StringName, ItemDef> BuildItemDefIndexSnapshotTyped()
    {
        return new Dictionary<StringName, ItemDef>(_itemDefIndex);
    }

    internal int GetMinBattleSurfaceHeight() => MIN_BATTLE_SURFACE_HEIGHT;

    internal Dictionary<StringName, BattleRatingMemberStats> GetBattleRatingStatsTyped() =>
        _battleRatingStatsByMemberId;

    internal GDictionary get_battle_rating_stats()
    {
        return BattleRatingProjection.ProjectStatsMap(_battleRatingStatsByMemberId);
    }

    internal BattleRatingSystem GetBattleRatingSystem() => _battle_rating_system;

    internal List<PendingCharacterReward> GetPendingPostBattleCharacterRewards() =>
        _pendingPostBattleCharacterRewards;

    internal int GetDoomSentenceRefundCalamityTotal()
    {
        _ensure_sidecars_ready();
        return _loot_resolver?.GetDoomSentenceRefundCalamityTotal() ?? 0;
    }
}
