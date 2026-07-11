using System;
using System.Collections.Generic;
using Godot;

public sealed class MisfortuneBlackOmenHookPayload
{
    public StringName MemberId { get; init; } = "";
    public bool EncounterWon { get; init; }
    public bool DefeatedEliteOrBoss { get; init; }
    public bool BossEncounter { get; init; }
    public bool MemberSurvived { get; init; }
    public bool? HasCursedRelic { get; init; }
    public bool? HasBossCurse { get; init; }
    public IReadOnlyList<StringName> BossCurseStatusIds { get; init; } = Array.Empty<StringName>();
    public IReadOnlyList<StringName> PathTags { get; init; } = Array.Empty<StringName>();
}

public sealed class MisfortuneBlackOmenResult
{
    public bool Ok { get; init; }
    public StringName HookId { get; init; } = "";
    public StringName MemberId { get; init; } = "";
    public bool ConditionsMet { get; init; }
    public bool Granted { get; init; }
    public bool AlreadyMarked { get; init; }
    public int DoomMarked { get; init; }
    public string ErrorCode { get; init; } = "";
}

internal enum MisfortuneBlackOmenHookKind
{
    Unknown = 0,
    CursedRelicEliteOrBossVictory,
    BossCurseSurvivalVictory,
    DeadRoadLanternBlackOmenPath,
}

public sealed class MisfortuneBlackOmenService
{
    internal static readonly StringName DOOM_MARKED_STAT_ID = "doom_marked";
    private static readonly StringName HOOK_CURSED_RELIC_ELITE_OR_BOSS_VICTORY =
        "cursed_relic_elite_or_boss_victory";
    private static readonly StringName HOOK_BOSS_CURSE_SURVIVAL_VICTORY =
        "boss_curse_survival_victory";
    private static readonly StringName HOOK_DEAD_ROAD_LANTERN_BLACK_OMEN_PATH =
        "dead_road_lantern_black_omen_path";

    private static readonly StringName[] CursedRelicRequiredTags = { "cursed", "relic" };

    private CharacterManagementModule _characterGateway;
    private readonly Dictionary<StringName, ItemDefinition> _itemDefinitions = new();

    internal static StringName ToStringName(MisfortuneBlackOmenHookKind kind)
    {
        return kind switch
        {
            MisfortuneBlackOmenHookKind.CursedRelicEliteOrBossVictory =>
                HOOK_CURSED_RELIC_ELITE_OR_BOSS_VICTORY,
            MisfortuneBlackOmenHookKind.BossCurseSurvivalVictory =>
                HOOK_BOSS_CURSE_SURVIVAL_VICTORY,
            MisfortuneBlackOmenHookKind.DeadRoadLanternBlackOmenPath =>
                HOOK_DEAD_ROAD_LANTERN_BLACK_OMEN_PATH,
            _ => "",
        };
    }

    public void Setup(
        CharacterManagementModule characterGateway = null,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions = null
    )
    {
        _characterGateway = characterGateway;
        _itemDefinitions.Clear();
        if (itemDefinitions == null)
            return;

        foreach (var (itemId, itemDefinition) in itemDefinitions)
        {
            if (itemId != "" && itemDefinition != null)
                _itemDefinitions[itemId] = itemDefinition;
        }
    }

    public void Dispose()
    {
        _characterGateway = null;
        _itemDefinitions.Clear();
    }

    public MisfortuneBlackOmenResult TryRunHook(
        StringName hookId,
        MisfortuneBlackOmenHookPayload payload = null
    )
    {
        payload ??= new MisfortuneBlackOmenHookPayload();
        if (hookId == HOOK_CURSED_RELIC_ELITE_OR_BOSS_VICTORY)
            return TryGrantCursedRelicEliteOrBossVictory(payload);
        if (hookId == HOOK_BOSS_CURSE_SURVIVAL_VICTORY)
            return TryGrantBossCurseSurvivalVictory(payload);
        if (hookId == HOOK_DEAD_ROAD_LANTERN_BLACK_OMEN_PATH)
            return TryGrantDeadRoadLanternBlackOmenPath(payload);
        return BuildResult(payload.MemberId, hookId, errorCode: "unknown_hook_id");
    }

    public MisfortuneBlackOmenResult GrantDoomMark(StringName memberId, StringName sourceId)
    {
        if (memberId == "" || sourceId == "")
            return BuildResult(memberId, sourceId, errorCode: "invalid_request");

        var memberState = GetMemberState(memberId);
        if (
            memberState == null
            || memberState.progression == null
            || GetUnitBaseAttributes(memberState) == null
        )
        {
            return BuildResult(memberId, sourceId, errorCode: "member_not_found");
        }

        int currentValue = GetDoomMarkedValue(memberState);
        if (currentValue >= 1)
        {
            return BuildResult(
                memberId,
                sourceId,
                ok: true,
                conditionsMet: true,
                alreadyMarked: true,
                doomMarked: currentValue
            );
        }

        GetUnitBaseAttributes(memberState).SetAttributeValue(DOOM_MARKED_STAT_ID, 1);
        return BuildResult(
            memberId,
            sourceId,
            ok: true,
            conditionsMet: true,
            granted: true,
            doomMarked: 1
        );
    }

    private MisfortuneBlackOmenResult TryGrantCursedRelicEliteOrBossVictory(
        MisfortuneBlackOmenHookPayload payload
    )
    {
        var memberId = payload.MemberId;
        var memberState = GetMemberState(memberId);
        if (memberId == "")
            return BuildResult(memberId, HOOK_CURSED_RELIC_ELITE_OR_BOSS_VICTORY, errorCode: "invalid_request");
        if (memberState == null)
            return BuildResult(memberId, HOOK_CURSED_RELIC_ELITE_OR_BOSS_VICTORY, errorCode: "member_not_found");

        bool conditionsMet =
            payload.EncounterWon
            && payload.DefeatedEliteOrBoss
            && HasCursedRelic(memberState, payload);
        if (!conditionsMet)
        {
            return BuildResult(
                memberId,
                HOOK_CURSED_RELIC_ELITE_OR_BOSS_VICTORY,
                ok: true,
                doomMarked: GetDoomMarkedValue(memberState),
                errorCode: "conditions_not_met"
            );
        }
        return GrantDoomMark(memberId, HOOK_CURSED_RELIC_ELITE_OR_BOSS_VICTORY);
    }

    private MisfortuneBlackOmenResult TryGrantBossCurseSurvivalVictory(
        MisfortuneBlackOmenHookPayload payload
    )
    {
        var memberId = payload.MemberId;
        var memberState = GetMemberState(memberId);
        if (memberId == "")
            return BuildResult(memberId, HOOK_BOSS_CURSE_SURVIVAL_VICTORY, errorCode: "invalid_request");
        if (memberState == null)
            return BuildResult(memberId, HOOK_BOSS_CURSE_SURVIVAL_VICTORY, errorCode: "member_not_found");

        bool conditionsMet =
            payload.EncounterWon
            && payload.BossEncounter
            && payload.MemberSurvived
            && HasBossCurse(payload);
        if (!conditionsMet)
        {
            return BuildResult(
                memberId,
                HOOK_BOSS_CURSE_SURVIVAL_VICTORY,
                ok: true,
                doomMarked: GetDoomMarkedValue(memberState),
                errorCode: "conditions_not_met"
            );
        }
        return GrantDoomMark(memberId, HOOK_BOSS_CURSE_SURVIVAL_VICTORY);
    }

    private MisfortuneBlackOmenResult TryGrantDeadRoadLanternBlackOmenPath(
        MisfortuneBlackOmenHookPayload payload
    )
    {
        var memberId = payload.MemberId;
        var memberState = GetMemberState(memberId);
        if (memberId == "")
            return BuildResult(memberId, HOOK_DEAD_ROAD_LANTERN_BLACK_OMEN_PATH, errorCode: "invalid_request");
        if (memberState == null)
            return BuildResult(memberId, HOOK_DEAD_ROAD_LANTERN_BLACK_OMEN_PATH, errorCode: "member_not_found");

        bool conditionsMet =
            MemberHasEquippedItem(memberState, LowLuckRelicRules.ToStringName(LowLuckRelicItemKind.DeadRoadLantern))
            && ContainsId(payload.PathTags, LowLuckRelicRules.ToStringName(LowLuckPathTagKind.BlackOmen));
        if (!conditionsMet)
        {
            return BuildResult(
                memberId,
                HOOK_DEAD_ROAD_LANTERN_BLACK_OMEN_PATH,
                ok: true,
                doomMarked: GetDoomMarkedValue(memberState),
                errorCode: "conditions_not_met"
            );
        }
        return GrantDoomMark(memberId, HOOK_DEAD_ROAD_LANTERN_BLACK_OMEN_PATH);
    }

    private bool HasCursedRelic(
        PartyMemberState memberState,
        MisfortuneBlackOmenHookPayload payload
    )
    {
        if (payload.HasCursedRelic.HasValue)
            return payload.HasCursedRelic.Value;
        if (memberState?.equipment_state == null || _itemDefinitions.Count == 0)
            return false;

        foreach (var slotId in memberState.equipment_state.GetEntrySlotIdsTyped())
        {
            var entry = memberState.equipment_state.GetEntry(slotId);
            var itemId = entry?.item_id ?? new StringName("");
            if (
                entry == null
                || itemId == ""
                || !_itemDefinitions.TryGetValue(
                    itemId,
                    out ItemDefinition itemDefinition
                )
            )
                continue;
            if (HasAllTags(itemDefinition, CursedRelicRequiredTags))
                return true;
        }
        return false;
    }

    private static bool HasBossCurse(MisfortuneBlackOmenHookPayload payload)
    {
        if (payload.HasBossCurse.HasValue)
            return payload.HasBossCurse.Value;
        return payload.BossCurseStatusIds != null && payload.BossCurseStatusIds.Count > 0;
    }

    private static bool MemberHasEquippedItem(PartyMemberState memberState, StringName itemId)
    {
        if (memberState?.equipment_state == null || itemId == "")
            return false;

        foreach (var slotId in memberState.equipment_state.GetEntrySlotIdsTyped())
        {
            var equippedItemId = ProgressionDataUtils.to_string_name(
                memberState.equipment_state.GetEquippedItemId(slotId)
            );
            if (equippedItemId == itemId)
                return true;
        }
        return false;
    }

    private static bool HasAllTags(
        ItemDefinition itemDefinition,
        IReadOnlyList<StringName> requiredTags
    )
    {
        if (itemDefinition == null || requiredTags == null || requiredTags.Count == 0)
            return false;

        var itemTags = itemDefinition.GetTagsTyped();
        foreach (var requiredTag in requiredTags)
        {
            if (!itemTags.Contains(requiredTag))
                return false;
        }
        return true;
    }

    private PartyMemberState GetMemberState(StringName memberId)
    {
        if (_characterGateway == null || memberId == "")
            return null;
        return _characterGateway.GetMemberState(memberId);
    }

    private int GetDoomMarkedValue(PartyMemberState memberState)
    {
        var attributes = GetUnitBaseAttributes(memberState);
        return attributes?.GetAttributeValue(DOOM_MARKED_STAT_ID) ?? 0;
    }

    private static UnitBaseAttributes GetUnitBaseAttributes(PartyMemberState memberState) =>
        memberState?.progression?.unit_base_attributes;

    private MisfortuneBlackOmenResult BuildResult(
        StringName memberId,
        StringName sourceId,
        bool ok = false,
        bool conditionsMet = false,
        bool granted = false,
        bool alreadyMarked = false,
        int? doomMarked = null,
        string errorCode = ""
    )
    {
        return new MisfortuneBlackOmenResult
        {
            Ok = ok,
            HookId = sourceId,
            MemberId = memberId,
            ConditionsMet = conditionsMet,
            Granted = granted,
            AlreadyMarked = alreadyMarked,
            DoomMarked = doomMarked ?? GetDoomMarkedValue(GetMemberState(memberId)),
            ErrorCode = errorCode ?? "",
        };
    }

    private static bool ContainsId(IReadOnlyList<StringName> values, StringName expected)
    {
        if (values == null || expected == "")
            return false;
        foreach (StringName value in values)
        {
            if (value == expected)
                return true;
        }
        return false;
    }
}
