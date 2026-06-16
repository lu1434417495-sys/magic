using System.Collections.Generic;
using Godot;

public readonly struct MisfortuneForgeGuidanceItemEntry
{
    public MisfortuneForgeGuidanceItemEntry(StringName itemId, int quantity)
    {
        ItemId = ProgressionDataUtils.to_string_name(itemId);
        Quantity = Mathf.Max(quantity, 0);
    }

    public StringName ItemId { get; }
    public int Quantity { get; }
    public bool IsValid => ItemId != "" && Quantity > 0;
}

public sealed class MisfortuneForgeGuidanceInput
{
    private readonly List<MisfortuneForgeGuidanceItemEntry> _removedEntries;
    private readonly List<MisfortuneForgeGuidanceItemEntry> _addedEntries;

    public MisfortuneForgeGuidanceInput(
        bool success,
        IEnumerable<MisfortuneForgeGuidanceItemEntry> removedEntries = null,
        IEnumerable<MisfortuneForgeGuidanceItemEntry> addedEntries = null,
        StringName outputItemId = default
    )
    {
        Success = success;
        _removedEntries = NormalizeEntries(removedEntries);
        _addedEntries = NormalizeEntries(addedEntries);
        OutputItemId = ProgressionDataUtils.to_string_name(outputItemId);
    }

    public static MisfortuneForgeGuidanceInput Empty { get; } = new(false);
    public bool Success { get; }
    public IReadOnlyList<MisfortuneForgeGuidanceItemEntry> RemovedEntries => _removedEntries;
    public IReadOnlyList<MisfortuneForgeGuidanceItemEntry> AddedEntries => _addedEntries;
    public StringName OutputItemId { get; }

    private static List<MisfortuneForgeGuidanceItemEntry> NormalizeEntries(
        IEnumerable<MisfortuneForgeGuidanceItemEntry> entries
    )
    {
        var result = new List<MisfortuneForgeGuidanceItemEntry>();
        if (entries == null)
            return result;
        foreach (var entry in entries)
        {
            if (entry.IsValid)
                result.Add(entry);
        }
        return result;
    }
}

internal class MisfortuneGuidanceService
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
    private static readonly IReadOnlyDictionary<StringName, ItemDef> EmptyItemDefs =
        new Dictionary<StringName, ItemDef>();

    private IBattleRuntimeCharacterGateway _characterGateway;
    private BattleRuntimeModule _battleRuntimeGateway;

    internal void Setup(
        IBattleRuntimeCharacterGateway characterGateway = null,
        BattleRuntimeModule battleRuntimeGateway = null
    )
    {
        _characterGateway = characterGateway;
        _battleRuntimeGateway = battleRuntimeGateway;
    }

    private void BindBattleRuntimeGateway(BattleRuntimeModule battleRuntimeGateway = null)
    {
        _battleRuntimeGateway = battleRuntimeGateway;
    }

    internal void Dispose()
    {
        _characterGateway = null;
        _battleRuntimeGateway = null;
    }

    internal List<StringName> HandleBattleResolution(
        BattleState battleState,
        BattleResolutionResult battleResolutionResult
    )
    {
        var unlockedIds = new List<StringName>();
        var partyState = GetPartyState();
        if (partyState == null || battleState == null || battleResolutionResult == null)
            return unlockedIds;
        if (battleResolutionResult.winner_faction_id != "player")
            return unlockedIds;

        MarkExaltedReadyFlags(battleResolutionResult);

        foreach (var enemyUnitId in battleState.enemy_unit_ids)
        {
            var defeatedUnit = battleState.GetUnit(enemyUnitId);
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

    public List<StringName> HandleForgeResult(
        StringName memberId,
        MisfortuneForgeGuidanceInput result,
        IReadOnlyDictionary<StringName, ItemDef> itemDefs = null
    )
    {
        return HandleForgeResultCore(
            memberId,
            result ?? MisfortuneForgeGuidanceInput.Empty,
            itemDefs ?? EmptyItemDefs
        );
    }

    private List<StringName> HandleForgeResultCore(
        StringName memberId,
        MisfortuneForgeGuidanceInput result,
        IReadOnlyDictionary<StringName, ItemDef> itemDefs
    )
    {
        var unlockedIds = new List<StringName>();
        var normalizedMemberId = ProgressionDataUtils.to_string_name(memberId);
        if (normalizedMemberId == "")
            return unlockedIds;
        if (result == null || !result.Success)
            return unlockedIds;
        if (!HasExaltedReadyFlag(normalizedMemberId))
            return unlockedIds;

        var memberState = GetMemberState(normalizedMemberId);
        if (!IsMisfortuneDevotee(memberState))
            return unlockedIds;
        if (!ForgeResultUsesFixedMaterial(result))
            return unlockedIds;
        if (!ForgeResultOutputsDarkEquipment(result, itemDefs ?? EmptyItemDefs))
            return unlockedIds;
        if (UnlockAchievement(normalizedMemberId, AchievementGuidanceExalted))
            AppendUniqueStringName(unlockedIds, AchievementGuidanceExalted);
        ClearExaltedReadyFlags(new[] { normalizedMemberId });
        return unlockedIds;
    }

    public void ClearExaltedReadyFlags(IEnumerable<StringName> memberIds = null)
    {
        var partyState = GetPartyState();
        if (partyState == null)
            return;

        var normalizedMemberIds = NormalizeMemberIds(memberIds);
        if (memberIds == null || normalizedMemberIds.Count == 0)
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
                partyState.ClearFateRunFlag(flagId);
            }
            return;
        }

        foreach (var memberId in normalizedMemberIds)
            partyState.ClearFateRunFlag(BuildExaltedReadyFlagId(memberId));
    }

    private void MarkExaltedReadyFlags(BattleResolutionResult battleResolutionResult)
    {
        if (!HasCalamityConversionShardLoot(battleResolutionResult))
            return;
        var partyState = GetPartyState();
        if (partyState == null)
            return;
        var calamityByMemberId =
            _battleRuntimeGateway?.GetCalamityByMemberIdSnapshot()
            ?? new Dictionary<StringName, int>();
        foreach (var entry in calamityByMemberId)
        {
            var memberId = ProgressionDataUtils.to_string_name(entry.Key);
            if (memberId == "")
                continue;
            if (Mathf.Max(entry.Value, 0) <= 0)
                continue;
            partyState.SetFateRunFlag(BuildExaltedReadyFlagId(memberId), true);
        }
    }

    private static bool HasCalamityConversionShardLoot(BattleResolutionResult battleResolutionResult)
    {
        if (battleResolutionResult?.loot_entries == null)
            return false;
        StringName calamityShardId = BattleLootIds.ToStringName(BattleLootSpecialItemKind.CalamityShard);
        foreach (BattleLootEntry lootEntry in battleResolutionResult.loot_entries)
        {
            if (
                lootEntry != null
                && lootEntry.SourceKind == BattleLootSourceKind.CalamityConversion
                && lootEntry.ItemId == calamityShardId
                && lootEntry.Quantity > 0
            )
                return true;
        }
        return false;
    }

    private bool MemberHadDevoutAdversity(StringName memberId)
    {
        return HasMisfortuneReason(memberId, CalamityReasonCriticalFail)
            || HasMisfortuneReason(memberId, CalamityReasonStrongDebuff);
    }

    private bool HasMisfortuneReason(StringName memberId, StringName reasonId)
    {
        return _battleRuntimeGateway?.HasMisfortuneReason(memberId, reasonId) ?? false;
    }

    private StringName ResolveEliteSealSourceMemberId(
        BattleState battleState,
        BattleUnitState defeatedUnit
    )
    {
        if (battleState == null || defeatedUnit == null || !IsEliteOrBossTarget(defeatedUnit))
            return "";
        var statusIds = new[]
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
        var effectState = targetUnit.GetStatusEffect(statusId);
        if (effectState == null || effectState.source_unit_id == "")
            return "";
        var sourceUnit = battleState.GetUnit(effectState.source_unit_id);
        return sourceUnit != null
            ? ProgressionDataUtils.to_string_name(sourceUnit.source_member_id)
            : "";
    }

    private bool ForgeResultUsesFixedMaterial(MisfortuneForgeGuidanceInput result)
    {
        foreach (var entry in result.RemovedEntries)
        {
            BattleLootSpecialItemKind itemKind = BattleLootIds.ToSpecialItemKind(entry.ItemId);
            if (
                itemKind == BattleLootSpecialItemKind.CalamityShard
                || itemKind == BattleLootSpecialItemKind.BlackCrownCore
            )
                return true;
        }
        return false;
    }

    private bool ForgeResultOutputsDarkEquipment(
        MisfortuneForgeGuidanceInput result,
        IReadOnlyDictionary<StringName, ItemDef> itemDefs
    )
    {
        var outputItemId = ResolveForgeOutputItemId(result);
        if (outputItemId == "")
            return false;
        var itemDef = GetItemDef(itemDefs, outputItemId);
        if (itemDef == null || !itemDef.IsEquipment())
            return false;
        var tags = itemDef.GetTagsTyped();
        foreach (var tag in tags)
        {
            if (tag == "dark" || tag == "misfortune" || tag == "doom")
                return true;
        }
        var groups = itemDef.GetCraftingGroupsTyped();
        foreach (var group in groups)
        {
            if (group == "misfortune" || group == "dark")
                return true;
        }
        return false;
    }

    private StringName ResolveForgeOutputItemId(MisfortuneForgeGuidanceInput result)
    {
        if (result.OutputItemId != "")
            return result.OutputItemId;
        foreach (var entry in result.AddedEntries)
        {
            if (entry.ItemId != "")
                return entry.ItemId;
        }
        return "";
    }

    private ItemDef GetItemDef(
        IReadOnlyDictionary<StringName, ItemDef> itemDefs,
        StringName itemId
    )
    {
        if (itemDefs == null || itemDefs.Count == 0 || itemId == "")
            return null;
        return itemDefs.TryGetValue(itemId, out ItemDef itemDef) ? itemDef : null;
    }

    private bool UnlockAchievement(StringName memberId, StringName achievementId)
    {
        if (_characterGateway == null || memberId == "" || achievementId == "")
            return false;
        return (_characterGateway as CharacterManagementModule)?.UnlockAchievement(
            memberId,
            achievementId,
            BuildSummaryText(achievementId)
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
        return _characterGateway?.GetPartyState();
    }

    private PartyMemberState GetMemberState(StringName memberId)
    {
        if (_characterGateway == null || memberId == "")
            return null;
        return _characterGateway.GetMemberState(memberId);
    }

    private bool IsDoomMarked(PartyMemberState memberState)
    {
        if (memberState == null || memberState.progression == null)
            return false;
        var unitBaseAttributes = memberState.progression.unit_base_attributes;
        if (unitBaseAttributes == null)
            return false;
        return unitBaseAttributes.GetAttributeValue(DoomMarkedStatId) > 0;
    }

    private bool IsMisfortuneDevotee(PartyMemberState memberState)
    {
        if (memberState == null || memberState.progression == null)
            return false;
        var unitBaseAttributes = memberState.progression.unit_base_attributes;
        if (unitBaseAttributes == null)
            return false;
        return unitBaseAttributes.GetAttributeValue(DoomAuthorityStatId) > 0;
    }

    private bool HasExaltedReadyFlag(StringName memberId)
    {
        var partyState = GetPartyState();
        if (partyState == null || memberId == "")
            return false;
        return partyState.HasFateRunFlag(BuildExaltedReadyFlagId(memberId));
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
        return unitState.attribute_snapshot.GetValue(BossTargetStatId) > 0
            || unitState.attribute_snapshot.GetValue(FortuneMarkTargetStatId)
                > 0;
    }

    private bool IsBossTarget(BattleUnitState unitState)
    {
        if (unitState == null || unitState.attribute_snapshot == null)
            return false;
        return unitState.attribute_snapshot.GetValue(BossTargetStatId) > 0;
    }

    private void AppendUniqueStringName(List<StringName> values, StringName value)
    {
        if (value != "" && !values.Contains(value))
            values.Add(value);
    }

    private static List<StringName> NormalizeMemberIds(IEnumerable<StringName> memberIds)
    {
        var result = new List<StringName>();
        if (memberIds == null)
            return result;
        foreach (var memberId in memberIds)
        {
            var normalizedMemberId = ProgressionDataUtils.to_string_name(memberId);
            if (normalizedMemberId != "" && !result.Contains(normalizedMemberId))
                result.Add(normalizedMemberId);
        }
        return result;
    }
}
