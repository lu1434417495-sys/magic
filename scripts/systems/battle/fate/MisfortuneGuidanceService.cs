using Godot;
using Godot.Collections;

[GlobalClass]
public partial class MisfortuneGuidanceService : RefCounted
{
    private static readonly StringName AchievementGuidanceTrue = "misfortune_guidance_true";
    private static readonly StringName AchievementGuidanceDevout = "misfortune_guidance_devout";
    private static readonly StringName AchievementGuidanceExalted = "misfortune_guidance_exalted";
    private static readonly StringName AchievementGuidanceBlessed = "misfortune_guidance_blessed";

    private static readonly StringName DoomMarkedStatId = "doom_marked";
    private static readonly StringName DoomAuthorityStatId = "doom_authority";
    private static readonly StringName FortuneMarkTargetStatId = "fortune_mark_target";
    private static readonly StringName BossTargetStatId = "boss_target";
    private static readonly StringName StatusBlackStarBrandElite = "black_star_brand_elite";
    private static readonly StringName StatusCrownBreakBrokenFang = "crown_break_broken_fang";
    private static readonly StringName StatusCrownBreakBrokenHand = "crown_break_broken_hand";
    private static readonly StringName StatusCrownBreakBlindedEye = "crown_break_blinded_eye";
    private static readonly StringName StatusDoomSentenceVerdict = "doom_sentence_verdict";
    private static readonly StringName ExaltedReadyFlagPrefix =
        "misfortune_guidance_exalted_ready:";
    private static readonly StringName CalamityReasonCriticalFail = "critical_fail";
    private static readonly StringName CalamityReasonStrongDebuff = "strong_debuff";

    private IBattleRuntimeCharacterGateway _characterGateway;
    private BattleRuntimeModule _battleRuntimeGateway;

    public static StringName ACHIEVEMENT_GUIDANCE_TRUE_ID() => AchievementGuidanceTrue;

    public static StringName ACHIEVEMENT_GUIDANCE_DEVOUT_ID() => AchievementGuidanceDevout;

    public static StringName ACHIEVEMENT_GUIDANCE_EXALTED_ID() => AchievementGuidanceExalted;

    public static StringName ACHIEVEMENT_GUIDANCE_BLESSED_ID() => AchievementGuidanceBlessed;

    public void Setup(IBattleRuntimeCharacterGateway characterGateway = null, BattleRuntimeModule battleRuntimeGateway = null)
    {
        _characterGateway = characterGateway;
        _battleRuntimeGateway = battleRuntimeGateway;
    }

    public void setup(
        IBattleRuntimeCharacterGateway character_gateway = null,
        BattleRuntimeModule battle_runtime_gateway = null
    )
    {
        Setup(character_gateway, battle_runtime_gateway);
    }

    public void BindBattleRuntimeGateway(BattleRuntimeModule battleRuntimeGateway = null)
    {
        _battleRuntimeGateway = battleRuntimeGateway;
    }

    public void bind_battle_runtime_gateway(GodotObject battle_runtime_gateway = null)
    {
        BindBattleRuntimeGateway(battle_runtime_gateway as BattleRuntimeModule);
    }

    public new void Dispose()
    {
        _characterGateway = null;
        _battleRuntimeGateway = null;
    }

    public void dispose()
    {
        _characterGateway = null;
        _battleRuntimeGateway = null;
    }

    public Array<StringName> HandleBattleResolution(
        BattleState battleState,
        BattleResolutionResult battleResolutionResult
    )
    {
        var unlockedIds = new Array<StringName>();
        var partyState = GetPartyState();
        if (partyState == null || battleState == null || battleResolutionResult == null)
            return unlockedIds;
        if (battleResolutionResult.winner_faction_id != "player")
            return unlockedIds;

        MarkExaltedReadyFlags(battleResolutionResult);

        foreach (var enemyUnitId in battleState.enemy_unit_ids)
        {
            var defeatedUnit = battleState.units[enemyUnitId].As<BattleUnitState>();
            if (defeatedUnit == null || defeatedUnit.is_alive)
                continue;

            var sealedMemberId = ResolveEliteSealSourceMemberId(battleState, defeatedUnit);
            if (sealedMemberId != "")
            {
                var sealedMemberState = GetMemberState(sealedMemberId);
                if (
                    IsDoomMarked(sealedMemberState)
                    && UnlockAchievement(sealedMemberId, AchievementGuidanceTrue)
                )
                    AppendUniqueStringName(unlockedIds, AchievementGuidanceTrue);
                if (
                    IsMisfortuneDevotee(sealedMemberState)
                    && MemberHadDevoutAdversity(sealedMemberId)
                    && UnlockAchievement(sealedMemberId, AchievementGuidanceDevout)
                )
                    AppendUniqueStringName(unlockedIds, AchievementGuidanceDevout);
            }

            var verdictMemberId = ResolveStatusSourceMemberId(
                battleState,
                defeatedUnit,
                StatusDoomSentenceVerdict
            );
            if (verdictMemberId == "" || !IsBossTarget(defeatedUnit))
                continue;
            if (
                IsMisfortuneDevotee(GetMemberState(verdictMemberId))
                && UnlockAchievement(verdictMemberId, AchievementGuidanceBlessed)
            )
                AppendUniqueStringName(unlockedIds, AchievementGuidanceBlessed);
        }

        return unlockedIds;
    }

    public Array<StringName> handle_battle_resolution(
        BattleState battle_state,
        BattleResolutionResult battle_resolution_result
    )
    {
        return HandleBattleResolution(battle_state, battle_resolution_result);
    }

    public Array<StringName> HandleForgeResult(
        StringName memberId,
        Dictionary result,
        Dictionary itemDefs = null
    )
    {
        return HandleForgeResultCore(
            memberId,
            DictBool(result, "success", false),
            DictDictionary(result, "inventory_delta"),
            DictDictionary(result, "service_side_effects"),
            itemDefs ?? new Dictionary()
        );
    }

    public Array<StringName> HandleForgeResult(
        StringName memberId,
        SettlementServiceResult result,
        Dictionary itemDefs = null
    )
    {
        return HandleForgeResultCore(
            memberId,
            result?.Success ?? false,
            result?.InventoryDelta ?? new Dictionary(),
            result?.ServiceSideEffects ?? new Dictionary(),
            itemDefs ?? new Dictionary()
        );
    }

    private Array<StringName> HandleForgeResultCore(
        StringName memberId,
        bool success,
        Dictionary inventoryDelta,
        Dictionary serviceSideEffects,
        Dictionary itemDefs
    )
    {
        var unlockedIds = new Array<StringName>();
        var normalizedMemberId = ProgressionDataUtils.to_string_name(memberId);
        if (normalizedMemberId == "")
            return unlockedIds;
        if (!success)
            return unlockedIds;
        if (!HasExaltedReadyFlag(normalizedMemberId))
            return unlockedIds;

        var memberState = GetMemberState(normalizedMemberId);
        if (!IsMisfortuneDevotee(memberState))
            return unlockedIds;
        if (!ForgeResultUsesFixedMaterial(inventoryDelta))
            return unlockedIds;
        if (!ForgeResultOutputsDarkEquipment(inventoryDelta, serviceSideEffects, itemDefs))
            return unlockedIds;
        if (UnlockAchievement(normalizedMemberId, AchievementGuidanceExalted))
            AppendUniqueStringName(unlockedIds, AchievementGuidanceExalted);
        ClearExaltedReadyFlags(new Godot.Collections.Array { normalizedMemberId });
        return unlockedIds;
    }

    public Array<StringName> handle_forge_result(
        StringName member_id,
        Dictionary result,
        Dictionary item_defs = null
    )
    {
        return HandleForgeResult(member_id, result, item_defs);
    }

    public void ClearExaltedReadyFlags(Godot.Collections.Array memberIds = null)
    {
        var partyState = GetPartyState();
        if (partyState == null)
            return;
        if (memberIds == null || memberIds.Count == 0)
        {
            var fateFlags = partyState.fate_run_flags;
            foreach (var key in fateFlags.Keys)
            {
                var flagId = ProgressionDataUtils.to_string_name(key);
                if (flagId == "")
                    continue;
                var flagStr = flagId.ToString();
                if (!flagStr.StartsWith(ExaltedReadyFlagPrefix))
                    continue;
                partyState.clear_fate_run_flag(flagId);
            }
            return;
        }
        foreach (var memberId in memberIds)
            partyState.clear_fate_run_flag(BuildExaltedReadyFlagId(memberId.AsStringName()));
    }

    public void clear_exalted_ready_flags(Godot.Collections.Array member_ids = null)
    {
        ClearExaltedReadyFlags(member_ids);
    }

    private void MarkExaltedReadyFlags(BattleResolutionResult battleResolutionResult)
    {
        if (battleResolutionResult == null)
            return;
        var convertedShards = battleResolutionResult
            .party_resource_commit
            .GetValueOrDefault("converted_calamity_shards", 0)
            .AsInt32();
        if (convertedShards <= 0)
            return;
        var partyState = GetPartyState();
        if (partyState == null)
            return;
        var calamityByMemberId = GetCalamityByMemberId();
        foreach (var memberKey in calamityByMemberId.Keys)
        {
            var memberId = ProgressionDataUtils.to_string_name(memberKey);
            if (memberId == "")
                continue;
            var value = calamityByMemberId.GetValueOrDefault(memberKey, 0).AsInt32();
            if (Mathf.Max(value, 0) <= 0)
                continue;
            partyState.set_fate_run_flag(BuildExaltedReadyFlagId(memberId), true);
        }
    }

    private bool MemberHadDevoutAdversity(StringName memberId)
    {
        return HasMisfortuneReason(memberId, CalamityReasonCriticalFail)
            || HasMisfortuneReason(memberId, CalamityReasonStrongDebuff);
    }

    private bool HasMisfortuneReason(StringName memberId, StringName reasonId)
    {
        return _battleRuntimeGateway?.has_misfortune_reason(memberId, reasonId) ?? false;
    }

    private Dictionary GetCalamityByMemberId()
    {
        return _battleRuntimeGateway?.get_calamity_by_member_id()?.Duplicate(true) ?? new Dictionary();
    }

    private StringName ResolveEliteSealSourceMemberId(
        BattleState battleState,
        BattleUnitState defeatedUnit
    )
    {
        if (battleState == null || defeatedUnit == null || !IsEliteOrBossTarget(defeatedUnit))
            return "";
        var statusIds = new Array<StringName>
        {
            StatusCrownBreakBrokenFang,
            StatusCrownBreakBrokenHand,
            StatusCrownBreakBlindedEye,
            StatusBlackStarBrandElite,
        };
        foreach (var statusId in statusIds)
        {
            var sourceMemberId = ResolveStatusSourceMemberId(battleState, defeatedUnit, statusId);
            if (sourceMemberId != "")
                return sourceMemberId;
        }
        return "";
    }

    private StringName ResolveStatusSourceMemberId(
        BattleState battleState,
        BattleUnitState targetUnit,
        StringName statusId
    )
    {
        if (battleState == null || targetUnit == null || statusId == "")
            return "";
        var effectState = targetUnit.get_status_effect(statusId);
        if (effectState == null || effectState.source_unit_id == "")
            return "";
        var sourceUnit = battleState.units[effectState.source_unit_id].As<BattleUnitState>();
        return sourceUnit != null
            ? ProgressionDataUtils.to_string_name(sourceUnit.source_member_id)
            : "";
    }

    private bool ForgeResultUsesFixedMaterial(Dictionary inventoryDelta)
    {
        var removedEntries = inventoryDelta
            .GetValueOrDefault("removed_entries", new Godot.Collections.Array())
            .AsGodotArray();
        foreach (var entryValue in removedEntries)
        {
            if (entryValue.VariantType != Variant.Type.Dictionary)
                continue;
            var entry = entryValue.AsGodotDictionary();
            var itemId = ProgressionDataUtils.to_string_name(
                entry.GetValueOrDefault("item_id", "")
            );
            if (
                itemId == BattleLootConstants.ITEM_CALAMITY_SHARD()
                || itemId == BattleLootConstants.ITEM_BLACK_CROWN_CORE()
            )
                return true;
        }
        return false;
    }

    private bool ForgeResultOutputsDarkEquipment(
        Dictionary inventoryDelta,
        Dictionary serviceSideEffects,
        Dictionary itemDefs)
    {
        var outputItemId = ResolveForgeOutputItemId(inventoryDelta, serviceSideEffects);
        if (outputItemId == "")
            return false;
        var itemDef = GetItemDef(itemDefs, outputItemId);
        if (itemDef == null || !itemDef.is_equipment())
            return false;
        var tags = itemDef.get_tags();
        foreach (var tag in tags)
        {
            if (tag == "dark" || tag == "misfortune" || tag == "doom")
                return true;
        }
        var groups = itemDef.get_crafting_groups();
        foreach (var group in groups)
        {
            if (group == "misfortune" || group == "dark")
                return true;
        }
        return false;
    }

    private StringName ResolveForgeOutputItemId(
        Dictionary inventoryDelta,
        Dictionary serviceSideEffects)
    {
        if (serviceSideEffects != null && serviceSideEffects.Count > 0)
        {
            var outputFromSideEffects = ProgressionDataUtils.to_string_name(
                serviceSideEffects.GetValueOrDefault("output_item_id", "")
            );
            if (outputFromSideEffects != "")
                return outputFromSideEffects;
        }
        if (inventoryDelta == null || inventoryDelta.Count == 0)
            return "";
        var addedEntries = inventoryDelta
            .GetValueOrDefault("added_entries", new Godot.Collections.Array())
            .AsGodotArray();
        foreach (var entryValue in addedEntries)
        {
            if (entryValue.VariantType != Variant.Type.Dictionary)
                continue;
            var entry = entryValue.AsGodotDictionary();
            var itemId = ProgressionDataUtils.to_string_name(
                entry.GetValueOrDefault("item_id", "")
            );
            if (itemId != "")
                return itemId;
        }
        return "";
    }

    private ItemDef GetItemDef(Dictionary itemDefs, StringName itemId)
    {
        if (itemDefs.Count == 0 || itemId == "")
            return null;
        var itemDef = GetItemDefByStringNameKey(itemDefs, itemId);
        if (itemDef != null && itemDef is ItemDef)
            return (ItemDef)itemDef;
        return null;
    }

    private GodotObject GetItemDefByStringNameKey(Dictionary itemDefs, StringName itemId)
    {
        if (itemId == "")
            return null;
        foreach (var key in itemDefs.Keys)
        {
            if (key.VariantType != Variant.Type.StringName)
                continue;
            if (key.AsStringName() == itemId)
                return itemDefs[key].AsGodotObject();
        }
        return null;
    }

    private bool UnlockAchievement(StringName memberId, StringName achievementId)
    {
        if (_characterGateway == null || memberId == "" || achievementId == "")
            return false;
        return (_characterGateway as CharacterManagementModule)?.unlock_achievement(
            memberId,
            achievementId,
            new Dictionary { ["summary_text"] = BuildSummaryText(achievementId) }
        ) ?? false;
    }

    private string BuildSummaryText(StringName achievementId)
    {
        if (achievementId == AchievementGuidanceTrue)
            return "黑冕第一次确认这名角色能把厄运压成封印。";
        if (achievementId == AchievementGuidanceDevout)
            return "吃下坏事之后仍能封喉，才算真正懂得 Misfortune。";
        if (achievementId == AchievementGuidanceExalted)
            return "这名角色开始把灾厄余烬锻成真正属于黑冕的装备。";
        if (achievementId == AchievementGuidanceBlessed)
            return "一次宣判击杀，让黑冕认定了最终的裁决资格。";
        return "";
    }

    private PartyState GetPartyState()
    {
        return _characterGateway?.get_party_state();
    }

    private PartyMemberState GetMemberState(StringName memberId)
    {
        if (_characterGateway == null || memberId == "")
            return null;
        return _characterGateway.get_member_state(memberId);
    }

    private bool IsDoomMarked(PartyMemberState memberState)
    {
        if (memberState == null || memberState.progression == null)
            return false;
        var unitBaseAttributes = memberState.progression.unit_base_attributes;
        if (unitBaseAttributes == null)
            return false;
        return unitBaseAttributes.get_attribute_value(DoomMarkedStatId) > 0;
    }

    private bool IsMisfortuneDevotee(PartyMemberState memberState)
    {
        if (memberState == null || memberState.progression == null)
            return false;
        var unitBaseAttributes = memberState.progression.unit_base_attributes;
        if (unitBaseAttributes == null)
            return false;
        return unitBaseAttributes.get_attribute_value(DoomAuthorityStatId) > 0;
    }

    private bool HasExaltedReadyFlag(StringName memberId)
    {
        var partyState = GetPartyState();
        if (partyState == null || memberId == "")
            return false;
        return partyState.has_fate_run_flag(BuildExaltedReadyFlagId(memberId));
    }

    private StringName BuildExaltedReadyFlagId(StringName memberId)
    {
        return ProgressionDataUtils.to_string_name(
            string.Format("{0}{1}", ExaltedReadyFlagPrefix, memberId)
        );
    }

    private bool IsEliteOrBossTarget(BattleUnitState unitState)
    {
        if (unitState == null || unitState.attribute_snapshot == null)
            return false;
        return unitState.attribute_snapshot.get_value(BossTargetStatId) > 0
            || unitState.attribute_snapshot.get_value(FortuneMarkTargetStatId)
                > 0;
    }

    private bool IsBossTarget(BattleUnitState unitState)
    {
        if (unitState == null || unitState.attribute_snapshot == null)
            return false;
        return unitState.attribute_snapshot.get_value(BossTargetStatId) > 0;
    }

    private void AppendUniqueStringName(Array<StringName> values, StringName value)
    {
        if (value != "" && !values.Contains(value))
            values.Add(value);
    }

    private static bool DictBool(Dictionary dictionary, string key, bool fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        Variant value = dictionary[key];
        return value.VariantType == Variant.Type.Bool ? value.AsBool() : fallback;
    }

    private static Dictionary DictDictionary(Dictionary dictionary, string key)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return new Dictionary();
        Variant value = dictionary[key];
        return value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary()
            : new Dictionary();
    }
}
