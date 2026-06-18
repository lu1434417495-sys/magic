using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;

internal class BattleRuntimeLootResolver
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
    private readonly struct ParsedDropDefinition
    {
        public ParsedDropDefinition(
            StringName dropEntryId,
            BattleLootDropKind dropKind,
            StringName dropType,
            StringName itemId,
            int quantity
        )
        {
            DropEntryId = dropEntryId;
            DropKind = dropKind;
            DropType = dropType;
            ItemId = itemId;
            Quantity = quantity;
        }

        public StringName DropEntryId { get; }
        public BattleLootDropKind DropKind { get; }
        public StringName DropType { get; }
        public StringName ItemId { get; }
        public int Quantity { get; }
        public bool IsValid => DropEntryId != "" && ItemId != "" && Quantity > 0;
    }

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

    internal void Setup(BattleRuntimeModule runtime)
    {
        _runtime = runtime;
    }

    internal void Dispose()
    {
        _runtime = null;
    }

    internal void CollectDefeatedUnitLoot(
        BattleUnitState unitState,
        BattleUnitState killerUnit = null
    )
    {
        _CollectDefeatedUnitLoot(unitState, killerUnit);
    }

    internal BattleResolutionResult BuildBattleResolutionResult()
    {
        return _BuildBattleResolutionResult();
    }
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
        if (lootedIds.Contains(defeatedUnitId))
            return;
        lootedIds.Add(defeatedUnitId);
        var enemyTemplate = _ResolveEnemyTemplateForUnit(unitState);
        if (enemyTemplate == null)
            return;
        var dropLuck = _ResolveDropLuckForKillerUnit(killerUnit);
        foreach (
            BattleLootEntry lootEntry in _BuildDefeatedUnitLootEntries(
                unitState,
                enemyTemplate,
                dropLuck
            )
        )
        {
            _runtime._active_loot_entries.Add(lootEntry.Duplicate());
        }
    }

    private EnemyTemplateDef _ResolveEnemyTemplateForUnit(BattleUnitState unitState)
    {
        if (unitState == null || _runtime == null)
            return null;
        var templateId = ProgressionDataUtils.to_string_name(unitState.enemy_template_id);
        if (templateId == "")
            return null;
        return _runtime.GetEnemyTemplateTyped(templateId);
    }

    private List<BattleLootEntry> _BuildDefeatedUnitLootEntries(
        BattleUnitState unitState,
        EnemyTemplateDef enemyTemplate,
        int dropLuck
    )
    {
        var lootEntries = new List<BattleLootEntry>();
        if (unitState == null || enemyTemplate == null)
            return lootEntries;
        var sourceLabel = !string.IsNullOrEmpty(unitState.display_name)
            ? unitState.display_name
            : unitState.unit_id.ToString();
        var normalizedDropLuck = Mathf.Clamp(dropLuck, -6, 5);
        foreach (DropEntryDef dropEntry in enemyTemplate.drop_entries)
        {
            ParsedDropDefinition parsedDropEntry = _ParseDropDefinition(dropEntry);
            if (!parsedDropEntry.IsValid)
                continue;
            StringName dropEntryId = parsedDropEntry.DropEntryId;
            BattleLootDropKind dropKind = parsedDropEntry.DropKind;
            StringName itemId = parsedDropEntry.ItemId;
            int quantity = parsedDropEntry.Quantity;
            if (dropKind == BattleLootDropKind.RandomEquipment)
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
                            BattleLootSourceKind.EnemyUnit,
                            unitState.unit_id,
                            sourceLabel,
                            $"{unitState.unit_id}_{dropEntryId}_{instanceIndex + 1}",
                            rolledInstances[instanceIndex]
                        );
                        if (lootEntry != null)
                            lootEntries.Add(lootEntry);
                    }
                    continue;
                }
                var fallbackEntry = _BuildFormalRandomEquipmentLootEntry(
                    BattleLootSourceKind.EnemyUnit,
                    unitState.unit_id,
                    sourceLabel,
                    $"{(unitState.enemy_template_id != "" ? unitState.enemy_template_id : unitState.unit_id)}_{dropEntryId}",
                    itemId,
                    quantity,
                    normalizedDropLuck
                );
                if (fallbackEntry == null)
                    continue;
                lootEntries.Add(fallbackEntry);
                continue;
            }
            var fixedEntry = _BuildFormalLootEntry(
                BattleLootSourceKind.EnemyUnit,
                unitState.unit_id,
                sourceLabel,
                $"{(unitState.enemy_template_id != "" ? unitState.enemy_template_id : unitState.unit_id)}_{dropEntryId}",
                itemId,
                quantity
            );
            if (fixedEntry != null)
                lootEntries.Add(fixedEntry);
        }
        return lootEntries;
    }

    private ParsedDropDefinition _ParseDropDefinition(DropEntryDef entryData)
    {
        if (entryData == null)
            return default;
        StringName dropEntryId = ProgressionDataUtils.to_string_name(entryData.drop_entry_id);
        StringName dropType = ProgressionDataUtils.to_string_name(entryData.drop_type);
        StringName itemId = ProgressionDataUtils.to_string_name(entryData.item_id);
        if (dropEntryId == "" || itemId == "")
            return default;
        BattleLootDropKind dropKind = BattleLootIds.ToDropKind(dropType);
        if (dropKind != BattleLootDropKind.Item && dropKind != BattleLootDropKind.RandomEquipment)
            return default;
        int quantity = entryData.quantity;
        if (quantity <= 0)
            return default;
        return new ParsedDropDefinition(dropEntryId, dropKind, dropType, itemId, quantity);
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

    private BattleLootEntry _BuildEquipmentInstanceLootEntry(
        BattleLootSourceKind dropSourceKind,
        StringName dropSourceId,
        string dropSourceLabel,
        string dropEntrySuffix,
        EquipmentInstanceState rolledInstance
    )
    {
        var equipmentInstance = NormalizeTransientEquipmentInstance(rolledInstance);
        if (equipmentInstance == null || equipmentInstance.item_id == "")
            return null;
        if (equipmentInstance.instance_id == "")
        {
            var allocatedInstanceId = _AllocateEquipmentInstanceId();
            if (allocatedInstanceId == "")
                return null;
            equipmentInstance.instance_id = allocatedInstanceId;
        }
        var sourceLabel = dropSourceLabel.StripEdges();
        if (string.IsNullOrEmpty(sourceLabel))
            sourceLabel = dropSourceId.ToString();
        var entrySuffix = dropEntrySuffix.StripEdges();
        if (string.IsNullOrEmpty(entrySuffix))
            entrySuffix = "equipment_instance";
        return BattleLootEntry.CreateEquipmentInstance(
            dropSourceKind,
            dropSourceId,
            sourceLabel,
            $"{BattleLootIds.ToStringName(dropSourceKind)}_{dropSourceId}_{entrySuffix}",
            equipmentInstance
        );
    }

    private static EquipmentInstanceState NormalizeTransientEquipmentInstance(
        EquipmentInstanceState instance
    )
    {
        EquipmentInstanceState equipmentInstance = instance?.DuplicateState();
        if (equipmentInstance == null || equipmentInstance.item_id == "")
            return null;
        if (!EquipmentInstanceState.IsValidRarity(equipmentInstance.rarity))
            return null;
        if (
            !EquipmentDurabilityRules.IsValidCurrentDurability(
                equipmentInstance.current_durability,
                equipmentInstance.rarity
            )
        )
            return null;
        return equipmentInstance;
    }

    private int _ResolveDropLuckForKillerUnit(BattleUnitState killerUnit)
    {
        if (killerUnit == null || killerUnit.source_member_id == "" || _runtime == null)
            return 0;
        if (_runtime.GetCharacterGatewayTyped() is not CharacterManagementModule characterGateway)
            return 0;
        PartyMemberState memberState = characterGateway.GetMemberState(killerUnit.source_member_id);
        if (memberState == null)
            return 0;
        return Mathf.Clamp(memberState.GetEffectiveLuck(), -6, 5);
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
            resolutionResult.SetLootEntries(_BuildPlayerVictoryLootEntries());
            return resolutionResult;
        }
        resolutionResult.SetLootEntries(System.Array.Empty<BattleLootEntry>());
        return resolutionResult;
    }

    private List<BattleLootEntry> _BuildPlayerVictoryLootEntries()
    {
        var lootEntries = new List<BattleLootEntry>();
        if (_runtime == null)
            return lootEntries;
        foreach (BattleLootEntry lootEntry in _runtime._active_loot_entries)
        {
            BattleLootEntry victoryEntry = lootEntry?.Duplicate();
            if (victoryEntry != null)
                lootEntries.Add(victoryEntry);
        }
        lootEntries.AddRange(_BuildStatusRewardLootEntries());
        lootEntries.AddRange(_BuildCalamityConversionLootEntries());
        return lootEntries;
    }

    private StringName _AllocateEquipmentInstanceId()
    {
        return _runtime != null ? _runtime.AllocateEquipmentInstanceId() : "";
    }

    private List<BattleLootEntry> _BuildStatusRewardLootEntries()
    {
        var lootEntries = new List<BattleLootEntry>();
        foreach (var defeatedUnitValue in _GetDefeatedEnemyUnits())
        {
            var defeatedUnit = defeatedUnitValue.As<BattleUnitState>();
            if (_ShouldGrantStatusCalamityShard(defeatedUnit))
            {
                lootEntries.Add(
                    _BuildFormalLootEntry(
                        BattleLootSourceKind.FateStatusDrop,
                        defeatedUnit.unit_id,
                        !string.IsNullOrEmpty(defeatedUnit.display_name)
                            ? defeatedUnit.display_name
                            : defeatedUnit.unit_id.ToString(),
                        "status_calamity_shard",
                        BattleLootIds.ToStringName(BattleLootSpecialItemKind.CalamityShard),
                        1
                    )
                );
            }
            if (_ShouldGrantBlackCrownCore(defeatedUnit))
            {
                lootEntries.Add(
                    _BuildFormalLootEntry(
                        BattleLootSourceKind.FateStatusDrop,
                        defeatedUnit.unit_id,
                        !string.IsNullOrEmpty(defeatedUnit.display_name)
                            ? defeatedUnit.display_name
                            : defeatedUnit.unit_id.ToString(),
                        "doom_sentence_black_crown_core",
                        BattleLootIds.ToStringName(BattleLootSpecialItemKind.BlackCrownCore),
                        1
                    )
                );
            }
        }
        return lootEntries;
    }

    private List<BattleLootEntry> _BuildCalamityConversionLootEntries()
    {
        var shardCount = _CalculateCalamityConversionShardCount();
        if (shardCount <= 0)
            return new List<BattleLootEntry>();
        BattleLootSourceIdKind battleSourceIdKind = _BattleHasEliteOrBossEnemy()
            ? BattleLootSourceIdKind.EliteBossBattle
            : BattleLootSourceIdKind.OrdinaryBattle;
        var battleSourceId = BattleLootIds.ToStringName(battleSourceIdKind);
        var battleSourceLabel =
            battleSourceIdKind == BattleLootSourceIdKind.EliteBossBattle
                ? "elite/boss 战未消耗 calamity 结算"
                : "普通战未消耗 calamity 结算";
        return new List<BattleLootEntry>
        {
            _BuildFormalLootEntry(
                BattleLootSourceKind.CalamityConversion,
                battleSourceId,
                battleSourceLabel,
                "calamity_conversion",
                BattleLootIds.ToStringName(BattleLootSpecialItemKind.CalamityShard),
                shardCount
            ),
        };
    }

    private int _CalculateCalamityConversionShardCount()
    {
        var totalCalamity =
            _CalculateCalamityAvailableForShardConversion()
            + _GetDoomSentenceRefundCalamityTotal();
        return Mathf.Max((int)(totalCalamity / CalamityPerShard), 0);
    }

    private int _CalculateCalamityAvailableForShardConversion()
    {
        if (_runtime == null)
            return 0;
        var totalCalamity = 0;
        foreach (var calamityValue in _runtime.calamity_by_member_id.Values)
        {
            totalCalamity += Mathf.Max(calamityValue, 0);
        }
        return totalCalamity;
    }

    internal int GetDoomSentenceRefundCalamityTotal() => _GetDoomSentenceRefundCalamityTotal();

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
                unitState.HasStatusEffect(StatusBlackStarBrandElite)
                || _HasCrownBreakSeal(unitState)
            );
    }

    private bool _ShouldGrantBlackCrownCore(BattleUnitState unitState)
    {
        return _IsBossTarget(unitState) && unitState.HasStatusEffect(StatusDoomSentenceVerdict);
    }

    private bool _HasCrownBreakSeal(BattleUnitState unitState)
    {
        return unitState != null
            && (
                unitState.HasStatusEffect(StatusCrownBreakBrokenFang)
                || unitState.HasStatusEffect(StatusCrownBreakBrokenHand)
                || unitState.HasStatusEffect(StatusCrownBreakBlindedEye)
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

    private BattleLootEntry _BuildFormalLootEntry(
        BattleLootSourceKind dropSourceKind,
        StringName dropSourceId,
        string dropSourceLabel,
        string dropEntrySuffix,
        StringName itemId,
        int quantity
    )
    {
        var normalizedSourceId = ProgressionDataUtils.to_string_name(dropSourceId);
        var normalizedItemId = ProgressionDataUtils.to_string_name(itemId);
        var normalizedQuantity = Mathf.Max(quantity, 0);
        if (
            dropSourceKind == BattleLootSourceKind.Unknown
            || normalizedSourceId == ""
            || normalizedItemId == ""
            || normalizedQuantity <= 0
        )
            return null;
        var sourceLabel = dropSourceLabel.StripEdges();
        if (string.IsNullOrEmpty(sourceLabel))
            sourceLabel = normalizedSourceId.ToString();
        var entrySuffix = dropEntrySuffix.StripEdges();
        if (string.IsNullOrEmpty(entrySuffix))
            entrySuffix = "drop";
        return BattleLootEntry.CreateItem(
            dropSourceKind,
            normalizedSourceId,
            sourceLabel,
            $"{BattleLootIds.ToStringName(dropSourceKind)}_{normalizedSourceId}_{entrySuffix}",
            normalizedItemId,
            normalizedQuantity
        );
    }

    private BattleLootEntry _BuildFormalRandomEquipmentLootEntry(
        BattleLootSourceKind dropSourceKind,
        StringName dropSourceId,
        string dropSourceLabel,
        string dropEntrySuffix,
        StringName itemId,
        int quantity,
        int dropLuck
    )
    {
        return BattleLootEntry.CreateRandomEquipment(
            dropSourceKind,
            dropSourceId,
            dropSourceLabel,
            $"{BattleLootIds.ToStringName(dropSourceKind)}_{dropSourceId}_{dropEntrySuffix}",
            itemId,
            quantity,
            dropLuck
        );
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
