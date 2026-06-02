using System;
using Godot;
using Godot.Collections;

[GlobalClass]
public partial class BattleRuntimeLootResolver : RefCounted
{
    private static readonly StringName StatusBlackStarBrandElite = "black_star_brand_elite";
    private static readonly StringName StatusCrownBreakBrokenFang = "crown_break_broken_fang";
    private static readonly StringName StatusCrownBreakBrokenHand = "crown_break_broken_hand";
    private static readonly StringName StatusCrownBreakBlindedEye = "crown_break_blinded_eye";
    private static readonly StringName StatusDoomSentenceVerdict = "doom_sentence_verdict";
    private static readonly StringName FortuneMarkTargetStatId = "fortune_mark_target";
    private static readonly StringName BossTargetStatId = "boss_target";
    private const int CalamityPerShard = 2;
    private const int DoomSentenceRefundCalamity = 5;
    private static readonly string[] DropDefinitionRequiredFields = new[]
    {
        "drop_entry_id",
        "drop_type",
        "item_id",
        "quantity",
    };

    private WeakReference<BattleRuntimeModule> _runtimeRef;

    private BattleRuntimeModule _runtime
    {
        get => ResolveWeakRef(_runtimeRef);
        set => _runtimeRef = value != null ? new WeakReference<BattleRuntimeModule>(value) : null;
    }

    private static BattleRuntimeModule ResolveWeakRef(WeakReference<BattleRuntimeModule> weakRef)
    {
        if (
            weakRef == null
            || !weakRef.TryGetTarget(out BattleRuntimeModule target)
        )
            return null;
        return target;
    }

    public void Setup(BattleRuntimeModule runtime)
    {
        _runtime = runtime;
    }

    public new void Dispose()
    {
        _runtime = null;
    }

    public void CollectDefeatedUnitLoot(
        BattleUnitState unitState,
        BattleUnitState killerUnit = null
    )
    {
        _CollectDefeatedUnitLoot(unitState, killerUnit);
    }

    public BattleResolutionResult BuildBattleResolutionResult()
    {
        return _BuildBattleResolutionResult();
    }

    public void setup(BattleRuntimeModule runtime) => Setup(runtime);

    public void dispose() => Dispose();

    public void collect_defeated_unit_loot(
        BattleUnitState unit_state,
        BattleUnitState killer_unit = null
    ) => CollectDefeatedUnitLoot(unit_state, killer_unit);

    public BattleResolutionResult build_battle_resolution_result() => BuildBattleResolutionResult();

    private bool _IsEliteOrBossTarget(BattleUnitState unitState)
    {
        return BattleExecutionRules.IsEliteOrBossTarget(unitState);
    }

    private void _CollectDefeatedUnitLoot(
        BattleUnitState unitState,
        BattleUnitState killerUnit = null
    )
    {
        if (unitState == null || unitState.is_alive || unitState.faction_id == "player")
            return;
        var defeatedUnitId = ProgressionDataUtils.to_string_name(unitState.unit_id);
        if (defeatedUnitId == "" || _runtime == null)
            return;
        var lootedIds = _runtime._looted_defeated_unit_ids;
        if (lootedIds.ContainsKey(defeatedUnitId))
            return;
        lootedIds[defeatedUnitId] = true;
        var enemyTemplate = _ResolveEnemyTemplateForUnit(unitState);
        if (enemyTemplate == null)
            return;
        var dropLuck = _ResolveDropLuckForKillerUnit(killerUnit);
        foreach (
            Dictionary lootEntry in _BuildDefeatedUnitLootEntries(
                unitState,
                enemyTemplate,
                dropLuck
            )
        )
        {
            _runtime._active_loot_entries.Add(lootEntry.Duplicate(true));
        }
    }

    private EnemyTemplateDef _ResolveEnemyTemplateForUnit(BattleUnitState unitState)
    {
        if (unitState == null || _runtime == null)
            return null;
        var templateId = ProgressionDataUtils.to_string_name(unitState.enemy_template_id);
        if (templateId == "")
            return null;
        var enemyTemplates = _runtime._enemy_templates;
        if (enemyTemplates == null || enemyTemplates.Count == 0)
            return null;
        if (enemyTemplates.ContainsKey(templateId))
            return enemyTemplates[templateId].As<EnemyTemplateDef>();
        string templateIdText = templateId.ToString();
        return enemyTemplates.ContainsKey(templateIdText)
            ? enemyTemplates[templateIdText].As<EnemyTemplateDef>()
            : null;
    }

    private Godot.Collections.Array<Dictionary> _BuildDefeatedUnitLootEntries(
        BattleUnitState unitState,
        EnemyTemplateDef enemyTemplate,
        int dropLuck
    )
    {
        var lootEntries = new Godot.Collections.Array<Dictionary>();
        if (unitState == null || enemyTemplate == null)
            return lootEntries;
        var sourceLabel = !string.IsNullOrEmpty(unitState.display_name)
            ? unitState.display_name
            : unitState.unit_id.ToString();
        var normalizedDropLuck = Mathf.Clamp(dropLuck, -6, 5);
        var dropEntries = new Godot.Collections.Array();
        foreach (var entry in enemyTemplate.get_drop_entries_resolved())
            dropEntries.Add(entry);
        foreach (var dropEntryValue in dropEntries)
        {
            var dropEntryData = dropEntryValue.AsGodotDictionary();
            var parsedDropEntry = _ParseDropDefinition(dropEntryData);
            if (parsedDropEntry.Count == 0)
                return new Godot.Collections.Array<Dictionary>();
            var dropEntryId = (StringName)parsedDropEntry["drop_entry_id"];
            var dropType = (StringName)parsedDropEntry["drop_type"];
            var itemId = (StringName)parsedDropEntry["item_id"];
            var quantity = (int)parsedDropEntry["quantity"];
            if (dropType == BattleLootConstants.DROP_TYPE_RANDOM_EQUIPMENT())
            {
                var equipmentDropService = _runtime._equipment_drop_service;
                if (equipmentDropService != null)
                {
                    var rolledInstances = equipmentDropService
                        .RollItemInstances(itemId, quantity, normalizedDropLuck);
                    for (
                        int instanceIndex = 0;
                        instanceIndex < rolledInstances.Count;
                        instanceIndex++
                    )
                    {
                        var lootEntry = _BuildEquipmentInstanceLootEntry(
                            BattleLootConstants.SOURCE_KIND_ENEMY_UNIT(),
                            unitState.unit_id,
                            sourceLabel,
                            $"{unitState.unit_id}_{dropEntryId}_{instanceIndex + 1}",
                            rolledInstances[instanceIndex]
                        );
                        if (lootEntry.Count > 0)
                            lootEntries.Add(lootEntry);
                    }
                    continue;
                }
                var fallbackEntry = _BuildFormalLootEntry(
                    BattleLootConstants.SOURCE_KIND_ENEMY_UNIT(),
                    unitState.unit_id,
                    sourceLabel,
                    $"{(unitState.enemy_template_id != "" ? unitState.enemy_template_id : unitState.unit_id)}_{dropEntryId}",
                    itemId,
                    quantity
                );
                if (fallbackEntry.Count == 0)
                    continue;
                fallbackEntry["drop_type"] = BattleLootConstants
                    .DROP_TYPE_RANDOM_EQUIPMENT()
                    .ToString();
                fallbackEntry["drop_luck"] = normalizedDropLuck;
                lootEntries.Add(fallbackEntry);
                continue;
            }
            var fixedEntry = _BuildFormalLootEntry(
                BattleLootConstants.SOURCE_KIND_ENEMY_UNIT(),
                unitState.unit_id,
                sourceLabel,
                $"{(unitState.enemy_template_id != "" ? unitState.enemy_template_id : unitState.unit_id)}_{dropEntryId}",
                itemId,
                quantity
            );
            if (fixedEntry.Count > 0)
                lootEntries.Add(fixedEntry);
        }
        return lootEntries;
    }

    private Dictionary _ParseDropDefinition(Dictionary entryData)
    {
        if (entryData.ContainsKey("drop_id"))
            return new Dictionary();
        if (entryData.Count != DropDefinitionRequiredFields.Length)
            return new Dictionary();
        foreach (var fieldName in DropDefinitionRequiredFields)
        {
            if (!entryData.ContainsKey(fieldName))
                return new Dictionary();
        }
        var dropEntryId = _StrictStringNameValue(entryData["drop_entry_id"]);
        var dropType = _StrictStringNameValue(entryData["drop_type"]);
        var itemId = _StrictStringNameValue(entryData["item_id"]);
        if (dropEntryId == "" || itemId == "")
            return new Dictionary();
        if (
            dropType != BattleLootConstants.DROP_TYPE_ITEM()
            && dropType != BattleLootConstants.DROP_TYPE_RANDOM_EQUIPMENT()
        )
            return new Dictionary();
        var quantity = entryData["quantity"].AsInt32();
        if (quantity <= 0)
            return new Dictionary();
        return new Dictionary
        {
            ["drop_entry_id"] = dropEntryId,
            ["drop_type"] = dropType,
            ["item_id"] = itemId,
            ["quantity"] = quantity,
        };
    }

    private StringName _StrictStringNameValue(object rawValue) =>
        NormalizeStrictStringNameText(rawValue?.ToString() ?? "");

    private StringName NormalizeStrictStringNameText(string rawText)
    {
        var text = rawText.StripEdges();
        if (string.IsNullOrEmpty(text))
            return "";
        return text;
    }

    private Dictionary _BuildEquipmentInstanceLootEntry(
        StringName dropSourceKind,
        StringName dropSourceId,
        string dropSourceLabel,
        string dropEntrySuffix,
        EquipmentInstanceState rolledInstance
    )
    {
        var equipmentInstanceData = rolledInstance?.to_dict() ?? new Dictionary();
        var itemId = ProgressionDataUtils.to_string_name(
            equipmentInstanceData.GetValueOrDefault("item_id", "")
        );
        if (equipmentInstanceData.Count == 0 || itemId == "")
            return new Dictionary();
        if (
            ProgressionDataUtils.to_string_name(
                equipmentInstanceData.GetValueOrDefault("instance_id", "")
            ) == ""
        )
        {
            var allocatedInstanceId = _AllocateEquipmentInstanceId();
            if (allocatedInstanceId == "")
                return new Dictionary();
            equipmentInstanceData["instance_id"] = allocatedInstanceId.ToString();
        }
        var sourceLabel = dropSourceLabel.StripEdges();
        if (string.IsNullOrEmpty(sourceLabel))
            sourceLabel = dropSourceId.ToString();
        var entrySuffix = dropEntrySuffix.StripEdges();
        if (string.IsNullOrEmpty(entrySuffix))
            entrySuffix = "equipment_instance";
        return new Dictionary
        {
            ["drop_type"] = BattleLootConstants.DROP_TYPE_EQUIPMENT_INSTANCE().ToString(),
            ["drop_source_kind"] = dropSourceKind.ToString(),
            ["drop_source_id"] = dropSourceId.ToString(),
            ["drop_source_label"] = sourceLabel,
            ["drop_entry_id"] = $"{dropSourceKind}_{dropSourceId}_{entrySuffix}",
            ["item_id"] = itemId.ToString(),
            ["quantity"] = 1,
            ["equipment_instance"] = equipmentInstanceData,
        };
    }

    private int _ResolveDropLuckForKillerUnit(BattleUnitState killerUnit)
    {
        if (killerUnit == null || killerUnit.source_member_id == "" || _runtime == null)
            return 0;
        if (_runtime.GetCharacterGatewayTyped() is not CharacterManagementModule characterGateway)
            return 0;
        PartyMemberState memberState = characterGateway.get_member_state(killerUnit.source_member_id);
        if (memberState == null)
            return 0;
        return Mathf.Clamp(memberState.get_effective_luck(), -6, 5);
    }

    private BattleResolutionResult _BuildBattleResolutionResult()
    {
        var resolutionResult = new BattleResolutionResult();
        if (_runtime == null)
            return resolutionResult;
        var state = _runtime._state;
        if (state == null)
            return resolutionResult;
        resolutionResult.battle_id = state.battle_id;
        resolutionResult.seed = state.seed;
        resolutionResult.world_coord = state.world_coord;
        resolutionResult.encounter_anchor_id = state.encounter_anchor_id;
        resolutionResult.terrain_profile_id = state.terrain_profile_id;
        resolutionResult.winner_faction_id = state.winner_faction_id;
        resolutionResult.encounter_resolution = _ResolveEncounterResolution();
        if (resolutionResult.winner_faction_id == "player")
        {
            resolutionResult.set_loot_entries(_BuildPlayerVictoryLootEntries());
            resolutionResult.party_resource_commit = _BuildBattlePartyResourceCommit();
        }
        else
        {
            resolutionResult.set_loot_entries(new Godot.Collections.Array());
            resolutionResult.party_resource_commit = new Dictionary();
        }
        resolutionResult.set_pending_character_rewards(
            _runtime._pending_post_battle_character_rewards
        );
        return resolutionResult;
    }

    private Godot.Collections.Array _BuildPlayerVictoryLootEntries()
    {
        var lootEntries = new Godot.Collections.Array();
        if (_runtime == null)
            return lootEntries;
        foreach (var lootEntryValue in _runtime._active_loot_entries)
        {
            var victoryEntry = _PreparePlayerVictoryLootEntry(
                lootEntryValue.AsGodotDictionary()
            );
            if (victoryEntry.Count > 0)
                lootEntries.Add(victoryEntry);
        }
        lootEntries.AddRange(_BuildStatusRewardLootEntries());
        lootEntries.AddRange(_BuildCalamityConversionLootEntries());
        return lootEntries;
    }

    private Dictionary _PreparePlayerVictoryLootEntry(Dictionary lootEntryData)
    {
        var entry = lootEntryData.Duplicate(true);
        var dropType = ProgressionDataUtils.to_string_name(
            entry.GetValueOrDefault("drop_type", "")
        );
        if (dropType != BattleLootConstants.DROP_TYPE_EQUIPMENT_INSTANCE())
            return entry;
        var equipmentPayload = entry.GetValueOrDefault(
            "equipment_instance",
            new Dictionary()
        ).AsGodotDictionary().Duplicate(true);
        var instanceId = ProgressionDataUtils.to_string_name(
            equipmentPayload.GetValueOrDefault("instance_id", "")
        );
        if (instanceId == "")
        {
            instanceId = _AllocateEquipmentInstanceId();
            if (instanceId == "")
                return new Dictionary();
            equipmentPayload["instance_id"] = instanceId.ToString();
        }
        entry["equipment_instance"] = equipmentPayload;
        return entry;
    }

    private StringName _AllocateEquipmentInstanceId()
    {
        return _runtime != null ? _runtime.allocate_equipment_instance_id() : "";
    }

    private Dictionary _BuildBattlePartyResourceCommit()
    {
        var returnedCalamity = _GetDoomSentenceRefundCalamityTotal();
        var unusedCalamity = _GetTotalUnusedCalamity();
        var convertedShards = _CalculateCalamityConversionShardCount();
        if (returnedCalamity <= 0 && unusedCalamity <= 0 && convertedShards <= 0)
            return new Dictionary();
        return new Dictionary
        {
            ["unused_calamity"] = unusedCalamity,
            ["returned_calamity"] = returnedCalamity,
            ["converted_calamity_shards"] = convertedShards,
        };
    }

    private Godot.Collections.Array _BuildStatusRewardLootEntries()
    {
        var lootEntries = new Godot.Collections.Array();
        foreach (var defeatedUnitValue in _GetDefeatedEnemyUnits())
        {
            var defeatedUnit = defeatedUnitValue.As<BattleUnitState>();
            if (_ShouldGrantStatusCalamityShard(defeatedUnit))
            {
                lootEntries.Add(
                    _BuildFormalLootEntry(
                        BattleLootConstants.SOURCE_KIND_FATE_STATUS_DROP(),
                        defeatedUnit.unit_id,
                        !string.IsNullOrEmpty(defeatedUnit.display_name)
                            ? defeatedUnit.display_name
                            : defeatedUnit.unit_id.ToString(),
                        "status_calamity_shard",
                        BattleLootConstants.ITEM_CALAMITY_SHARD(),
                        1
                    )
                );
            }
            if (_ShouldGrantBlackCrownCore(defeatedUnit))
            {
                lootEntries.Add(
                    _BuildFormalLootEntry(
                        BattleLootConstants.SOURCE_KIND_FATE_STATUS_DROP(),
                        defeatedUnit.unit_id,
                        !string.IsNullOrEmpty(defeatedUnit.display_name)
                            ? defeatedUnit.display_name
                            : defeatedUnit.unit_id.ToString(),
                        "doom_sentence_black_crown_core",
                        BattleLootConstants.ITEM_BLACK_CROWN_CORE(),
                        1
                    )
                );
            }
        }
        return lootEntries;
    }

    private Godot.Collections.Array _BuildCalamityConversionLootEntries()
    {
        var shardCount = _CalculateCalamityConversionShardCount();
        if (shardCount <= 0)
            return new Godot.Collections.Array();
        var battleSourceId = _BattleHasEliteOrBossEnemy()
            ? BattleLootConstants.SOURCE_ID_ELITE_BOSS_BATTLE()
            : BattleLootConstants.SOURCE_ID_ORDINARY_BATTLE();
        var battleSourceLabel =
            battleSourceId == BattleLootConstants.SOURCE_ID_ELITE_BOSS_BATTLE()
                ? "elite/boss 战未消耗 calamity 结算"
                : "普通战未消耗 calamity 结算";
        return new Godot.Collections.Array
        {
            _BuildFormalLootEntry(
                BattleLootConstants.SOURCE_KIND_CALAMITY_CONVERSION(),
                battleSourceId,
                battleSourceLabel,
                "calamity_conversion",
                BattleLootConstants.ITEM_CALAMITY_SHARD(),
                shardCount
            ),
        };
    }

    private int _CalculateCalamityConversionShardCount()
    {
        var totalCalamity = _GetTotalUnusedCalamity() + _GetDoomSentenceRefundCalamityTotal();
        return Mathf.Max((int)(totalCalamity / CalamityPerShard), 0);
    }

    private int _GetTotalUnusedCalamity()
    {
        if (_runtime == null)
            return 0;
        var totalCalamity = 0;
        foreach (var calamityValue in _runtime.calamity_by_member_id.Values)
        {
            totalCalamity += Mathf.Max((int)calamityValue, 0);
        }
        return totalCalamity;
    }

    private int _GetDoomSentenceRefundCalamityTotal()
    {
        var refundTotal = 0;
        foreach (var defeatedUnitValue in _GetDefeatedEnemyUnits())
        {
            var defeatedUnit = defeatedUnitValue.As<BattleUnitState>();
            if (_ShouldGrantBlackCrownCore(defeatedUnit))
                refundTotal += DoomSentenceRefundCalamity;
        }
        return refundTotal;
    }

    private Godot.Collections.Array _GetDefeatedEnemyUnits()
    {
        var defeatedUnits = new Godot.Collections.Array();
        if (_runtime == null)
            return defeatedUnits;
        var state = _runtime._state;
        if (state == null)
            return defeatedUnits;
        foreach (var enemyUnitId in state.enemy_unit_ids)
        {
            state.TryGetUnitTyped(enemyUnitId, out BattleUnitState unitState);
            if (unitState == null || unitState.is_alive)
                continue;
            defeatedUnits.Add(unitState);
        }
        return defeatedUnits;
    }

    private bool _ShouldGrantStatusCalamityShard(BattleUnitState unitState)
    {
        return _IsEliteOrBossTarget(unitState)
            && (
                unitState.has_status_effect(StatusBlackStarBrandElite)
                || _HasCrownBreakSeal(unitState)
            );
    }

    private bool _ShouldGrantBlackCrownCore(BattleUnitState unitState)
    {
        return _IsBossTarget(unitState) && unitState.has_status_effect(StatusDoomSentenceVerdict);
    }

    private bool _HasCrownBreakSeal(BattleUnitState unitState)
    {
        return unitState != null
            && (
                unitState.has_status_effect(StatusCrownBreakBrokenFang)
                || unitState.has_status_effect(StatusCrownBreakBrokenHand)
                || unitState.has_status_effect(StatusCrownBreakBlindedEye)
            );
    }

    private bool _IsBossTarget(BattleUnitState unitState)
    {
        return BattleExecutionRules.IsBossTarget(unitState);
    }

    private bool _BattleHasEliteOrBossEnemy()
    {
        if (_runtime == null)
            return false;
        var state = _runtime._state;
        if (state == null)
            return false;
        foreach (var enemyUnitId in state.enemy_unit_ids)
        {
            state.TryGetUnitTyped(enemyUnitId, out BattleUnitState unitState);
            if (_IsEliteOrBossTarget(unitState))
                return true;
        }
        return false;
    }

    private Dictionary _BuildFormalLootEntry(
        StringName dropSourceKind,
        StringName dropSourceId,
        string dropSourceLabel,
        string dropEntrySuffix,
        StringName itemId,
        int quantity
    )
    {
        var normalizedSourceKind = ProgressionDataUtils.to_string_name(dropSourceKind);
        var normalizedSourceId = ProgressionDataUtils.to_string_name(dropSourceId);
        var normalizedItemId = ProgressionDataUtils.to_string_name(itemId);
        var normalizedQuantity = Mathf.Max(quantity, 0);
        if (
            normalizedSourceKind == ""
            || normalizedSourceId == ""
            || normalizedItemId == ""
            || normalizedQuantity <= 0
        )
            return new Dictionary();
        var sourceLabel = dropSourceLabel.StripEdges();
        if (string.IsNullOrEmpty(sourceLabel))
            sourceLabel = normalizedSourceId.ToString();
        var entrySuffix = dropEntrySuffix.StripEdges();
        if (string.IsNullOrEmpty(entrySuffix))
            entrySuffix = "drop";
        return new Dictionary
        {
            ["drop_type"] = "item",
            ["drop_source_kind"] = normalizedSourceKind.ToString(),
            ["drop_source_id"] = normalizedSourceId.ToString(),
            ["drop_source_label"] = sourceLabel,
            ["drop_entry_id"] = $"{normalizedSourceKind}_{normalizedSourceId}_{entrySuffix}",
            ["item_id"] = normalizedItemId.ToString(),
            ["quantity"] = normalizedQuantity,
        };
    }

    private StringName _ResolveEncounterResolution()
    {
        if (_runtime == null)
            return "";
        var state = _runtime._state;
        if (state == null)
            return "";
        if (state.winner_faction_id == "player")
            return "player_victory";
        if (state.winner_faction_id == "hostile")
            return "hostile_victory";
        if (state.winner_faction_id == "draw")
            return "draw";
        return "resolved";
    }
}
