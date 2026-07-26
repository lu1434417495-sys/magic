using Godot;

internal static class ContingencyContractRules
{
    private static readonly string[] SimpleTriggerFields = { "type", "subject", "timing" };
    private static readonly string[] HpBelowPercentTriggerFields =
    {
        "type",
        "subject",
        "percent",
        "crossing_only",
        "timing",
    };
    private static readonly string[] IncomingDamagePercentTriggerFields =
    {
        "type",
        "subject",
        "damage_percent",
        "damage_basis",
        "damage_amount_mode",
        "timing",
    };
    private static readonly string[] EnemyEnterRadiusTriggerFields =
    {
        "type",
        "center",
        "radius",
        "radius_metric",
        "source_team",
        "timing",
    };
    private static readonly string[] StatusAppliedTriggerFields =
    {
        "type",
        "subject",
        "status_tags",
        "application_match",
        "timing",
    };
    private static readonly string[] AffectedBySpellTriggerFields =
    {
        "type",
        "subject",
        "source_team",
        "spell_match",
        "timing",
    };
    private static readonly string[] SimpleTargetResolverFields = { "type" };
    private static readonly string[] EmptyCellTargetResolverFields =
    {
        "type",
        "preference",
        "max_distance",
    };

    internal static ContingencyTriggerKind ToTriggerKind(StringName type)
    {
        if (type == "combat_started")
            return ContingencyTriggerKind.CombatStarted;
        if (type == "hp_below_percent")
            return ContingencyTriggerKind.HpBelowPercent;
        if (type == "incoming_damage_percent")
            return ContingencyTriggerKind.IncomingDamagePercent;
        if (type == "fatal_damage_incoming")
            return ContingencyTriggerKind.FatalDamageIncoming;
        if (type == "status_applied")
            return ContingencyTriggerKind.StatusApplied;
        if (type == "enemy_enter_radius")
            return ContingencyTriggerKind.EnemyEnterRadius;
        if (type == "affected_by_spell")
            return ContingencyTriggerKind.AffectedBySpell;
        if (type == "owner_turn_started")
            return ContingencyTriggerKind.OwnerTurnStarted;
        return ContingencyTriggerKind.Unknown;
    }

    internal static StringName ToTriggerType(ContingencyTriggerKind kind) =>
        kind switch
        {
            ContingencyTriggerKind.CombatStarted => "combat_started",
            ContingencyTriggerKind.HpBelowPercent => "hp_below_percent",
            ContingencyTriggerKind.IncomingDamagePercent => "incoming_damage_percent",
            ContingencyTriggerKind.FatalDamageIncoming => "fatal_damage_incoming",
            ContingencyTriggerKind.StatusApplied => "status_applied",
            ContingencyTriggerKind.EnemyEnterRadius => "enemy_enter_radius",
            ContingencyTriggerKind.AffectedBySpell => "affected_by_spell",
            ContingencyTriggerKind.OwnerTurnStarted => "owner_turn_started",
            _ => new StringName(""),
        };

    internal static ContingencyTimingKind ToTimingKind(StringName timing)
    {
        if (timing == "after_battle_confirmed")
            return ContingencyTimingKind.AfterBattleConfirmed;
        if (timing == "before_spell_effect_resolved")
            return ContingencyTimingKind.BeforeSpellEffectResolved;
        if (timing == "before_damage_resolved")
            return ContingencyTimingKind.BeforeDamageResolved;
        if (timing == "after_hp_changed")
            return ContingencyTimingKind.AfterHpChanged;
        if (timing == "after_status_applied")
            return ContingencyTimingKind.AfterStatusApplied;
        if (timing == "after_position_changed")
            return ContingencyTimingKind.AfterPositionChanged;
        if (timing == "owner_turn_started")
            return ContingencyTimingKind.OwnerTurnStarted;
        return ContingencyTimingKind.Unknown;
    }

    internal static string[] GetTriggerFields(StringName type)
    {
        ContingencyTriggerKind kind = ToTriggerKind(type);
        return kind switch
        {
            ContingencyTriggerKind.CombatStarted
            or ContingencyTriggerKind.FatalDamageIncoming
            or ContingencyTriggerKind.OwnerTurnStarted => SimpleTriggerFields,
            ContingencyTriggerKind.HpBelowPercent => HpBelowPercentTriggerFields,
            ContingencyTriggerKind.IncomingDamagePercent =>
                IncomingDamagePercentTriggerFields,
            ContingencyTriggerKind.EnemyEnterRadius => EnemyEnterRadiusTriggerFields,
            ContingencyTriggerKind.StatusApplied => StatusAppliedTriggerFields,
            ContingencyTriggerKind.AffectedBySpell => AffectedBySpellTriggerFields,
            _ => null,
        };
    }

    internal static ContingencyTargetResolverKind ToTargetResolverKind(StringName type)
    {
        if (type == "self")
            return ContingencyTargetResolverKind.Self;
        if (type == "trigger_source")
            return ContingencyTargetResolverKind.TriggerSource;
        if (type == "trigger_target")
            return ContingencyTargetResolverKind.TriggerTarget;
        if (type == "nearest_enemy_to_owner")
            return ContingencyTargetResolverKind.NearestEnemyToOwner;
        if (type == "nearest_enemy_to_trigger_cell")
            return ContingencyTargetResolverKind.NearestEnemyToTriggerCell;
        if (type == "owner_centered_area")
            return ContingencyTargetResolverKind.OwnerCenteredArea;
        if (type == "attacker_cell")
            return ContingencyTargetResolverKind.AttackerCell;
        if (type == "empty_cell_near_owner")
            return ContingencyTargetResolverKind.EmptyCellNearOwner;
        return ContingencyTargetResolverKind.Unknown;
    }

    internal static StringName ToTargetResolverType(ContingencyTargetResolverKind kind) =>
        kind switch
        {
            ContingencyTargetResolverKind.Self => "self",
            ContingencyTargetResolverKind.TriggerSource => "trigger_source",
            ContingencyTargetResolverKind.TriggerTarget => "trigger_target",
            ContingencyTargetResolverKind.NearestEnemyToOwner => "nearest_enemy_to_owner",
            ContingencyTargetResolverKind.NearestEnemyToTriggerCell =>
                "nearest_enemy_to_trigger_cell",
            ContingencyTargetResolverKind.OwnerCenteredArea => "owner_centered_area",
            ContingencyTargetResolverKind.AttackerCell => "attacker_cell",
            ContingencyTargetResolverKind.EmptyCellNearOwner => "empty_cell_near_owner",
            _ => new StringName(""),
        };

    internal static string[] GetTargetResolverFields(ContingencyTargetResolverKind kind) =>
        kind == ContingencyTargetResolverKind.EmptyCellNearOwner
            ? EmptyCellTargetResolverFields
            : kind == ContingencyTargetResolverKind.Unknown
                ? null
                : SimpleTargetResolverFields;

    internal static ContingencyEmptyCellPreferenceKind ToEmptyCellPreferenceKind(
        StringName preference
    )
    {
        if (preference == "away_from_trigger_source")
            return ContingencyEmptyCellPreferenceKind.AwayFromTriggerSource;
        if (preference == "safe_cell")
            return ContingencyEmptyCellPreferenceKind.SafeCell;
        return ContingencyEmptyCellPreferenceKind.Unknown;
    }
}
