using System;
using System.Collections.Generic;
using Godot;

internal sealed record BattleFateEventPayload
{
    private readonly List<StringName> _attackerStrongAttackDebuffIds = new();

    private BattleFateEventPayload(
        StringName eventType,
        StringName battleId,
        StringName attackerMemberId,
        StringName attackerId,
        StringName defenderId,
        int critGateDie,
        bool isDisadvantage,
        bool defenderIsEliteOrBoss,
        bool attackerLowHpHardship,
        IEnumerable<StringName> attackerStrongAttackDebuffIds,
        int? hiddenLuckAtBirth
    )
    {
        EventType = Normalize(eventType);
        BattleId = Normalize(battleId);
        AttackerMemberId = Normalize(attackerMemberId);
        AttackerId = Normalize(attackerId);
        DefenderId = Normalize(defenderId);
        CritGateDie = Math.Max(critGateDie, 0);
        IsDisadvantage = isDisadvantage;
        DefenderIsEliteOrBoss = defenderIsEliteOrBoss;
        AttackerLowHpHardship = attackerLowHpHardship;
        HiddenLuckAtBirth = hiddenLuckAtBirth;

        if (attackerStrongAttackDebuffIds == null)
            return;
        foreach (StringName debuffId in attackerStrongAttackDebuffIds)
        {
            StringName normalizedDebuffId = Normalize(debuffId);
            if (normalizedDebuffId != "" && !_attackerStrongAttackDebuffIds.Contains(normalizedDebuffId))
                _attackerStrongAttackDebuffIds.Add(normalizedDebuffId);
        }
    }

    public StringName EventType { get; }
    public StringName BattleId { get; }
    public StringName AttackerMemberId { get; }
    public StringName AttackerId { get; }
    public StringName DefenderId { get; }
    public int CritGateDie { get; }
    public bool IsDisadvantage { get; }
    public bool DefenderIsEliteOrBoss { get; }
    public bool AttackerLowHpHardship { get; }
    public IReadOnlyList<StringName> AttackerStrongAttackDebuffIds =>
        _attackerStrongAttackDebuffIds;
    public int? HiddenLuckAtBirth { get; }

    public static BattleFateEventPayload Empty(StringName eventType = default) =>
        Create(eventType);

    public static BattleFateEventPayload Create(
        StringName eventType,
        StringName battleId = default,
        StringName attackerMemberId = default,
        StringName attackerId = default,
        StringName defenderId = default,
        int critGateDie = 0,
        bool isDisadvantage = false,
        bool defenderIsEliteOrBoss = false,
        bool attackerLowHpHardship = false,
        IEnumerable<StringName> attackerStrongAttackDebuffIds = null,
        int? hiddenLuckAtBirth = null
    ) =>
        new(
            eventType,
            battleId,
            attackerMemberId,
            attackerId,
            defenderId,
            critGateDie,
            isDisadvantage,
            defenderIsEliteOrBoss,
            attackerLowHpHardship,
            attackerStrongAttackDebuffIds,
            hiddenLuckAtBirth
        );

    public static BattleFateEventPayload CriticalFail(
        StringName battleId,
        StringName attackerMemberId,
        StringName attackerId = default,
        StringName defenderId = default,
        int? hiddenLuckAtBirth = null
    ) =>
        Create(
            "critical_fail",
            battleId,
            attackerMemberId,
            attackerId,
            defenderId,
            hiddenLuckAtBirth: hiddenLuckAtBirth
        );

    public BattleFateEventPayload WithEventType(StringName eventType) =>
        Create(
            eventType,
            BattleId,
            AttackerMemberId,
            AttackerId,
            DefenderId,
            CritGateDie,
            IsDisadvantage,
            DefenderIsEliteOrBoss,
            AttackerLowHpHardship,
            _attackerStrongAttackDebuffIds,
            HiddenLuckAtBirth
        );

    public FortuneCriticalEventPayload ToFortuneCriticalPayload() =>
        new(BattleId, AttackerMemberId, AttackerId, DefenderId, CritGateDie, IsDisadvantage);

    public FortunaGuidanceEventPayload ToFortunaGuidancePayload() =>
        new(
            EventType,
            BattleId,
            AttackerMemberId,
            DefenderIsEliteOrBoss,
            AttackerLowHpHardship,
            _attackerStrongAttackDebuffIds
        );

    public LowLuckFateEventPayload ToLowLuckPayload() =>
        new(
            BattleId,
            AttackerMemberId,
            AttackerLowHpHardship,
            _attackerStrongAttackDebuffIds,
            HiddenLuckAtBirth
        );

    private static StringName Normalize(StringName value) =>
        ProgressionDataUtils.to_string_name(value);
}

internal readonly record struct FortuneCriticalEventPayload(
    StringName BattleId,
    StringName AttackerMemberId,
    StringName AttackerId,
    StringName DefenderId,
    int CritGateDie,
    bool IsDisadvantage
)
{
    public FortuneMarkEventInput ToFortuneMarkEventInput() =>
        new()
        {
            BattleId = ProgressionDataUtils.to_string_name(BattleId),
            AttackerMemberId = ProgressionDataUtils.to_string_name(AttackerMemberId),
            AttackerId = ProgressionDataUtils.to_string_name(AttackerId),
            DefenderId = ProgressionDataUtils.to_string_name(DefenderId),
            CritGateDie = Math.Max(CritGateDie, 0),
            IsDisadvantage = IsDisadvantage,
        };
}

internal readonly record struct FortunaGuidanceEventPayload(
    StringName EventType,
    StringName BattleId,
    StringName AttackerMemberId,
    bool DefenderIsEliteOrBoss,
    bool AttackerLowHpHardship,
    IReadOnlyList<StringName> AttackerStrongAttackDebuffIds
)
{
    public FortunaGuidanceEventInput ToInput() =>
        new()
        {
            EventType = ProgressionDataUtils.to_string_name(EventType),
            BattleId = ProgressionDataUtils.to_string_name(BattleId),
            AttackerMemberId = ProgressionDataUtils.to_string_name(AttackerMemberId),
            DefenderIsEliteOrBoss = DefenderIsEliteOrBoss,
            AttackerLowHpHardship = AttackerLowHpHardship,
            AttackerStrongAttackDebuffIds =
                AttackerStrongAttackDebuffIds ?? Array.Empty<StringName>(),
        };
}
