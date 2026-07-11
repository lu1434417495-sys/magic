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
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions = null,
        IReadOnlyDictionary<StringName, TraitDefinition> traitDefs = null,
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> equipmentAbilityBindings = null,
        IReadOnlyDictionary<StringName, BarrierProfileDefinition> barrierProfileDefinitions = null
    )
    {
        BeginContentCatalogRebind();
        IReadOnlyDictionary<StringName, SkillDefinition> catalogSkillDefinitions =
            _skillCatalog?.GetSkillDefinitionsTyped();
        IReadOnlyDictionary<StringName, SkillDefinition> resolvedSkillDefinitions =
            skillDefinitions
            ?? catalogSkillDefinitions;
        ApplySkillDefinitionsTyped(resolvedSkillDefinitions);
        ApplyItemDefsTyped(itemDefs);
        ApplyTraitDefsTyped(traitDefs);
        ApplyEquipmentAbilityBindingsTyped(equipmentAbilityBindings);
        ApplyBarrierProfileDefinitionsTyped(barrierProfileDefinitions);
        CompleteContentCatalogRebind();
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
        BeginContentCatalogRebind();
        ApplyEnemyTemplatesTyped(enemyTemplates);
        CompleteContentCatalogRebind();
    }

    private void ApplySkillDefinitionsTyped(
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions
    )
    {
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
        BeginContentCatalogRebind();
        ApplyEnemyAiBrainsTyped(enemyAiBrains);
        _ai_service.Setup(_enemyAiBrainIndex, _damage_resolver);
        CompleteContentCatalogRebind();
    }

    private void BeginContentCatalogRebind()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Exception firstFailure = null;
        RunTeardownStep(ref firstFailure, _runtime_services.ClearRuntimeBindings);
        RunTeardownStep(ref firstFailure, ClearAiActionPlans);
        if (firstFailure != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(firstFailure).Throw();
        }
    }

    private void CompleteContentCatalogRebind()
    {
        if (_state == null)
        {
            return;
        }
        try
        {
            _build_ai_action_plans();
        }
        catch
        {
            Exception cleanupFailure = null;
            RunTeardownStep(ref cleanupFailure, _runtime_services.ClearRuntimeBindings);
            RunTeardownStep(ref cleanupFailure, ClearAiActionPlans);
            throw;
        }
    }

    private void ClearContentCatalogBorrowers()
    {
        _characterGateway = null;
        _skillCatalog = null;
        _skillDefinitionIndex.Clear();
        _traitDefIndex.Clear();
        _barrierProfileIndex.Clear();
        _equipmentAbilityBindingIndex.Clear();
        _itemDefIndex.Clear();
        _enemyTemplateIndex.Clear();
        _enemyAiBrainIndex.Clear();
        _special_profile_registry_snapshot.Clear();
        _special_profile_view = BattleSpecialProfileRuntimeView.Empty;
        _special_profile_gate = null;
        _encounter_builder = null;
        _equipment_drop_service = null;
        _equipment_instance_id_allocator = null;
    }

    internal bool HasContentCatalogBorrowers =>
        _characterGateway != null
        || _skillCatalog != null
        || _skillDefinitionIndex.Count != 0
        || _traitDefIndex.Count != 0
        || _barrierProfileIndex.Count != 0
        || _equipmentAbilityBindingIndex.Count != 0
        || _itemDefIndex.Count != 0
        || _enemyTemplateIndex.Count != 0
        || _enemyAiBrainIndex.Count != 0
        || _special_profile_registry_snapshot.Count != 0
        || !ReferenceEquals(_special_profile_view, BattleSpecialProfileRuntimeView.Empty)
        || _special_profile_gate != null
        || _encounter_builder != null
        || _equipment_drop_service != null
        || _equipment_instance_id_allocator != null;

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

    private void ApplyTraitDefsTyped(IReadOnlyDictionary<StringName, TraitDefinition> traitDefs)
    {
        _traitDefIndex.Clear();
        if (traitDefs == null || traitDefs.Count == 0)
        {
            return;
        }
        foreach ((StringName traitId, TraitDefinition traitDef) in traitDefs)
        {
            if (traitId == "" || traitDef == null || traitDef.TraitId == "")
            {
                continue;
            }
            _traitDefIndex[traitDef.TraitId] = traitDef;
        }
    }

    private void ApplyBarrierProfileDefinitionsTyped(
        IReadOnlyDictionary<StringName, BarrierProfileDefinition> profileDefinitions
    )
    {
        _barrierProfileIndex.Clear();
        if (profileDefinitions == null || profileDefinitions.Count == 0)
        {
            return;
        }
        foreach (
            (StringName profileId, BarrierProfileDefinition profileDefinition) in profileDefinitions
        )
        {
            if (
                profileId == ""
                || profileDefinition == null
                || profileDefinition.ProfileId == ""
            )
            {
                continue;
            }
            _barrierProfileIndex[profileDefinition.ProfileId] = profileDefinition;
        }
    }

    private void ApplyEquipmentAbilityBindingsTyped(
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> bindings
    )
    {
        _equipmentAbilityBindingIndex.Clear();
        if (bindings == null || bindings.Count == 0)
        {
            return;
        }
        foreach ((StringName bindingId, EquipmentAbilityBindingDefinition binding) in bindings)
        {
            if (bindingId == "" || binding == null || binding.BindingId == "")
            {
                continue;
            }
            _equipmentAbilityBindingIndex[binding.BindingId] = binding;
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

    internal IReadOnlyDictionary<StringName, TraitDefinition> GetTraitDefIndexTyped() => _traitDefIndex;

    internal IReadOnlyDictionary<StringName, BarrierProfileDefinition> GetBarrierProfileIndexTyped() =>
        _barrierProfileIndex;

    internal IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> GetEquipmentAbilityBindingIndexTyped() =>
        _equipmentAbilityBindingIndex;

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

    internal Dictionary<StringName, TraitDefinition> BuildTraitDefIndexSnapshotTyped()
    {
        return new Dictionary<StringName, TraitDefinition>(_traitDefIndex);
    }

    internal Dictionary<StringName, EquipmentAbilityBindingDefinition> BuildEquipmentAbilityBindingIndexSnapshotTyped()
    {
        return new Dictionary<StringName, EquipmentAbilityBindingDefinition>(
            _equipmentAbilityBindingIndex
        );
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
