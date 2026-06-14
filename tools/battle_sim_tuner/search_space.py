"""Tunable parameter space for the mage_controller brain.

Each ParamSpec is one override-patch axis (per-brain action param) plus search
bounds. All of these are brain/action params (patchable per-brain via override
patches), so tuning one faction's mage does not touch the other side.
"""

from __future__ import annotations

from .evaluator import ParamSpec

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


# Full BattleAiScoreProfile weight set as per-faction patches (needs the engine's
# per-faction score-profile support). Tuning these for ONE faction vs a baseline
# opponent is only meaningful in an arena where the weights have gradient — e.g. a
# multi-unit faction makes friendly-fire / heal / target-count weights matter; a
# single-unit faction leaves many of them as free-drift dimensions.
# (name, lo, hi) — bounds bracket the shipped default.
_SCORE_WEIGHTS = [
    ("damage_weight", 0, 40),
    ("heal_weight", 0, 40),
    ("status_weight", 0, 80),
    ("terrain_weight", 0, 60),
    ("height_weight", 0, 48),
    ("lethal_target_weight", 0, 1500),
    ("lethal_threat_target_weight", 0, 2500),
    ("target_count_weight", 0, 160),
    ("friendly_fire_damage_weight", 0, 120),
    ("friendly_fire_target_weight", 0, 800),
    ("friendly_control_target_weight", 0, 1000),
    ("friendly_lethal_target_weight", 0, 15000),
    ("ap_cost_weight", 0, 100),
    ("mp_cost_weight", 0, 80),
    ("stamina_cost_weight", 0, 40),
    ("aura_cost_weight", 0, 120),
    ("cooldown_weight", 0, 48),
    ("movement_cost_weight", 0, 80),
    ("position_base_score", 0, 200),
    ("position_distance_step", 0, 20),
    ("position_undershoot_penalty", 0, 60),
    ("position_overshoot_penalty", 0, 60),
    ("threat_healer_bias_basis_points", 0, 5000),
    ("threat_control_bias_basis_points", 0, 3000),
    ("threat_ranged_bias_basis_points", 0, 3000),
    ("threat_range_step_bias_basis_points", 0, 1000),
    ("threat_multiplier_cap_basis_points", 5000, 30000),
    ("meteor_high_priority_threat_multiplier_bp", 8000, 20000),
    ("meteor_high_priority_damage_hp_percent", 0, 100),
    ("meteor_high_priority_target_priority_score", 0, 800),
    ("meteor_top_threat_rank", 1, 5),
    ("meteor_friendly_fire_soft_expected_hp_percent", 0, 100),
    ("meteor_friendly_fire_hard_expected_hp_percent", 0, 100),
    ("meteor_friendly_fire_hard_worst_case_hp_percent", 0, 100),
]

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


def score_weight_space(faction: str = "hostile") -> list[ParamSpec]:
    """BattleAiScoreProfile scalar weight space patched onto one faction's profile."""
    return [
        ParamSpec(
            name,
            {"target_type": "faction_ai_score_profile", "target_id": faction, "path": name},
            lo,
            hi,
        )
        for name, lo, hi in _SCORE_WEIGHTS
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
    "movement_cost_weight": 18, "position_base_score": 60, "position_distance_step": 4,
    "position_undershoot_penalty": 15, "position_overshoot_penalty": 12,
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
