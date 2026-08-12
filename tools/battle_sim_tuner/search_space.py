"""Tunable parameter space for the mage_controller brain.

Each ParamSpec is one override-patch axis (per-brain action param) plus search
bounds. All of these are brain/action params (patchable per-brain via override
patches), so tuning one faction's mage does not touch the other side.
"""

from __future__ import annotations

from .evaluator import ParamSpec
from .score_weight_catalog import load_score_weight_specs

_BRAIN = "mage_controller"
_GAIN = "min_survival_margin_gain_to_escape"


def _action(action_id: str, path: str) -> dict:
    return {"target_type": "action", "brain_id": _BRAIN, "action_id": action_id, "path": path}


# 8-dimensional space mixing the survival-commit thresholds (the decisive levers)
# with the distance bands the mage positions and fires from.
MAGE_SPACE = [
    ParamSpec("blink_gain", _action("mage_blink_escape", _GAIN), -20, 20),
    ParamSpec("surv_gain", _action("mage_survival_position", _GAIN), -20, 20),
    ParamSpec("blink_safe", _action("mage_blink_escape", "minimum_safe_distance"), 1, 8),
    ParamSpec("blink_bonus", _action("mage_blink_escape", "desired_max_distance_bonus"), 0, 6),
    ParamSpec("surv_min", _action("mage_survival_position", "desired_min_distance"), 1, 8),
    ParamSpec("surv_max", _action("mage_survival_position", "desired_max_distance"), 1, 10),
    ParamSpec("fire_min", _action("mage_fireball_cluster", "desired_min_distance"), 1, 8),
    ParamSpec("fire_max", _action("mage_fireball_cluster", "desired_max_distance"), 1, 10),
]


# Full BattleAiScoreProfile weight set as per-faction patches. The JSON catalog
# is the cross-language contract consumed by both this Python tuner and the C#
# BattleSim override validation.
_SCORE_WEIGHTS = tuple(
    (spec.path, spec.minimum, spec.maximum)
    for spec in load_score_weight_specs()
)

SCORE_ACTION_BASE_DEFAULTS = {
    "skill": 0,
    "move": 20,
    "retreat": 35,
    "wait": -40,
}

SCORE_BUCKET_PRIORITY_DEFAULTS = {
    "mist_support": 120,
    "mist_control": 110,
    "mist_offense": 100,
    "frontline_guard": 130,
    "harrier_pressure": 100,
    "charge_open": 100,
    "archer_survival": 150,
    "archer_positioning": 110,
    "archer_pressure": 90,
}


# Score params that only carry gradient when a mage is on the field: MP (mana)
# reserve/cost, the meteor-swarm targeting band, and chain-lightning target weight.
# In a mage-free roster (e.g. mixed_6v12_two_archer = 4 sword + 2 archer, no MP user)
# these are zero-gradient free-drift dimensions; dropping them shrinks the search
# space the surrogate / CMA-ES has to cover. NOTE: `aura_*` is intentionally NOT here
# — aura is a warrior/archer ultimate resource, not mage-specific.
MAGE_PARAMS = frozenset({
    "mp_cost_weight",
    "mp_reserve_floor_bp",
    "mp_reserve_pressure_weight",
    "mp_reserve_breach_penalty",
    "chain_enemy_target_weight",
    "meteor_high_priority_threat_multiplier_bp",
    "meteor_high_priority_damage_hp_percent",
    "meteor_high_priority_target_priority_score",
    "meteor_top_threat_rank",
    "meteor_friendly_fire_soft_expected_hp_percent",
    "meteor_friendly_fire_hard_expected_hp_percent",
    "meteor_friendly_fire_hard_worst_case_hp_percent",
})

# Named drop-groups resolvable from the CLI (--drop-params <name>). Comma lists of
# raw param names are also accepted by the callers.
DROP_GROUPS = {"mage": MAGE_PARAMS}


def resolve_drop_params(spec: str | None) -> set[str]:
    """Expand a --drop-params value (named group or comma list) to param names."""
    if not spec:
        return set()
    out: set[str] = set()
    for token in spec.split(","):
        token = token.strip()
        if not token:
            continue
        out |= set(DROP_GROUPS.get(token.lower(), {token}))
    return out


def score_weight_space(
    faction: str = "hostile", drop: "set[str] | None" = None
) -> list[ParamSpec]:
    """BattleAiScoreProfile scalar weight space patched onto one faction's profile.

    `drop` removes the named params from the search; the engine then keeps them at
    their shipped defaults (neutral), so frozen mage params do not change behaviour.
    """
    drop = drop or set()
    return [
        ParamSpec(
            name,
            {"target_type": "faction_ai_score_profile", "target_id": faction, "path": name},
            lo,
            hi,
        )
        for name, lo, hi in _SCORE_WEIGHTS
        if name not in drop
    ]


# Shipped BattleAiScoreProfile scalar defaults — start CMA here (the values are
# already tuned), not at the bound midpoints, so the search refines instead of
# recovering from junk.
SCORE_DEFAULTS = {
    "damage_weight": 10, "heal_weight": 8, "status_weight": 25, "terrain_weight": 15,
    "height_weight": 12, "lethal_target_weight": 500, "lethal_threat_target_weight": 900,
    "target_count_weight": 40, "friendly_fire_damage_weight": 35,
    "friendly_fire_target_weight": 250, "friendly_control_target_weight": 350,
    "friendly_lethal_target_weight": 5000, "ap_cost_weight": 25, "mp_cost_weight": 15,
    "stamina_cost_weight": 2, "aura_cost_weight": 35, "cooldown_weight": 8,
    "delayed_resolution_cost_per_5_tu": 1,
    "movement_cost_weight": 18,
    "mp_reserve_floor_bp": 0, "mp_reserve_pressure_weight": 0,
    "mp_reserve_breach_penalty": 0, "stamina_reserve_floor_bp": 0,
    "stamina_reserve_pressure_weight": 0, "stamina_reserve_breach_penalty": 0,
    "aura_reserve_floor_bp": 0, "aura_reserve_pressure_weight": 0,
    "aura_reserve_breach_penalty": 0, "resource_conservation_weight": 100,
    "position_base_score": 60, "position_distance_step": 4,
    "position_undershoot_penalty": 15, "position_overshoot_penalty": 12,
    "survival_margin_gain_weight": 0, "post_action_threat_damage_weight": 0,
    "post_action_threat_count_weight": 0, "lethal_survival_risk_penalty": 0,
    "incoming_threat_relief_weight": 0, "low_hp_urgency_threshold_bp": 0,
    "low_hp_urgency_weight": 0, "execute_target_hp_threshold_bp": 0,
    "execute_bonus_weight": 0, "overkill_damage_penalty_weight": 0,
    "role_threat_min_effective_range": 4, "role_threat_distance_window": 4,
    "role_threat_max_approach_distance": 7, "role_threat_max_contact_range": 2,
    "role_threat_in_range_score_step": 10, "enemy_target_count_weight": 0,
    "chain_enemy_target_weight": 0, "focus_fire_wounded_target_weight": 0,
    "hit_rate_reliability_weight": 0, "save_reliable_damage_weight": 0,
    "shield_absorbed_weight": 2, "control_weight": 0, "ground_control_weight": 0,
    "status_redundancy_penalty": 0, "position_objective_weight": 100,
    "safe_distance_adherence_weight": 0,
    "threat_healer_bias_basis_points": 1500, "threat_control_bias_basis_points": 500,
    "threat_ranged_bias_basis_points": 800, "threat_range_step_bias_basis_points": 200,
    "threat_multiplier_cap_basis_points": 15000,
    "meteor_high_priority_threat_multiplier_bp": 11000,
    "meteor_high_priority_damage_hp_percent": 35,
    "meteor_high_priority_target_priority_score": 250, "meteor_top_threat_rank": 1,
    "meteor_friendly_fire_soft_expected_hp_percent": 10,
    "meteor_friendly_fire_hard_expected_hp_percent": 25,
    "meteor_friendly_fire_hard_worst_case_hp_percent": 50,
    "default_bucket_priority": 0,
}

SCORE_PROFILE_DEFAULTS = {
    **SCORE_DEFAULTS,
    "meteor_friendly_fire_profile": "default",
    "action_base_scores": SCORE_ACTION_BASE_DEFAULTS,
    "bucket_priorities": SCORE_BUCKET_PRIORITY_DEFAULTS,
}
