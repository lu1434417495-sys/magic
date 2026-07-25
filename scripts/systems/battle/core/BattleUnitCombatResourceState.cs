using System;

internal readonly record struct BattleUnitCombatResourceValues(
    int Hp,
    int Mp,
    int Stamina,
    int Aura,
    int Ap,
    int MovePoints,
    int StaminaRecoveryProgress,
    bool IsAlive
)
{
    internal static BattleUnitCombatResourceValues Default =>
        new(
            0,
            0,
            0,
            0,
            0,
            BattleUnitState.DefaultMovePointsPerTurn,
            0,
            true
        );
}

internal readonly record struct BattleUnitCombatResourceReadView(
    bool OwnerPresent,
    BattleUnitCombatResourceValues Values
)
{
    internal static BattleUnitCombatResourceReadView MissingOwner =>
        new(false, BattleUnitCombatResourceValues.Default);

    internal static BattleUnitCombatResourceReadView Present(
        BattleUnitCombatResourceValues values
    ) =>
        new(true, values);
}

internal readonly record struct BattleUnitCombatResourceSnapshot(
    bool OwnerPresent,
    BattleUnitCombatResourceValues Values
)
{
    internal static BattleUnitCombatResourceSnapshot MissingOwner =>
        new(false, BattleUnitCombatResourceValues.Default);

    internal static BattleUnitCombatResourceSnapshot Present(
        BattleUnitCombatResourceValues values
    ) =>
        new(true, values);
}

internal sealed class BattleUnitCombatResourceState
{
    private BattleUnitCombatResourceValues _values =
        BattleUnitCombatResourceValues.Default;

    internal BattleUnitCombatResourceReadView GetReadView() =>
        BattleUnitCombatResourceReadView.Present(_values);

    internal int GetCurrentHp() => _values.Hp;

    internal int GetCurrentMp() => _values.Mp;

    internal int GetCurrentStamina() => _values.Stamina;

    internal int GetCurrentAura() => _values.Aura;

    internal int GetCurrentAp() => _values.Ap;

    internal int GetCurrentMovePoints() => _values.MovePoints;

    internal int GetStaminaRecoveryProgress() =>
        _values.StaminaRecoveryProgress;

    internal bool IsAlive() => _values.IsAlive;

    internal void SetCurrentHp(int value)
    {
        int hp = Math.Max(value, 0);
        _values = _values with
        {
            Hp = hp,
            IsAlive = hp > 0,
        };
    }

    internal void SetCurrentHpClamped(int value, int hpMax)
    {
        int normalizedMax = Math.Max(hpMax, 0);
        SetCurrentHp(
            normalizedMax > 0
                ? Math.Clamp(value, 0, normalizedMax)
                : 0
        );
    }

    internal int ApplyHpDamage(int damage)
    {
        int previousHp = _values.Hp;
        SetCurrentHp(_values.Hp - Math.Max(damage, 0));
        return previousHp - _values.Hp;
    }

    internal int ApplyHealing(int amount, int hpMax)
    {
        int previousHp = _values.Hp;
        SetCurrentHpClamped(
            _values.Hp + Math.Max(amount, 0),
            hpMax
        );
        return _values.Hp - previousHp;
    }

    internal void MarkDead()
    {
        _values = _values with
        {
            Hp = 0,
            IsAlive = false,
        };
    }

    internal void ReviveWithHp(int hp, int hpMax)
    {
        int normalizedMax = Math.Max(hpMax, 1);
        SetCurrentHpClamped(Math.Max(hp, 1), normalizedMax);
    }

    internal void SetCurrentMp(int value)
    {
        _values = _values with { Mp = Math.Max(value, 0) };
    }

    internal void SetCurrentStamina(int value)
    {
        _values = _values with { Stamina = Math.Max(value, 0) };
    }

    internal void SetCurrentAura(int value)
    {
        _values = _values with { Aura = Math.Max(value, 0) };
    }

    internal void SetCurrentAp(int value)
    {
        _values = _values with { Ap = Math.Max(value, 0) };
    }

    internal void SetCurrentMovePoints(int value)
    {
        _values = _values with
        {
            MovePoints = Math.Max(value, 0),
        };
    }

    internal void SetAllNormalized(
        int hp,
        int mp,
        int stamina,
        int aura,
        int ap,
        int movePoints
    )
    {
        int normalizedHp = Math.Max(hp, 0);
        _values = new BattleUnitCombatResourceValues(
            normalizedHp,
            Math.Max(mp, 0),
            Math.Max(stamina, 0),
            Math.Max(aura, 0),
            Math.Max(ap, 0),
            Math.Max(movePoints, 0),
            _values.StaminaRecoveryProgress,
            normalizedHp > 0
        );
    }

    internal void RestoreProjectionNormalized(
        int hp,
        int mp,
        int stamina,
        int aura,
        int ap,
        int movePoints,
        bool alive
    )
    {
        _values = new BattleUnitCombatResourceValues(
            Math.Max(hp, 0),
            Math.Max(mp, 0),
            Math.Max(stamina, 0),
            Math.Max(aura, 0),
            Math.Max(ap, 0),
            Math.Max(movePoints, 0),
            _values.StaminaRecoveryProgress,
            alive
        );
    }

    internal void ClampToCaps(BattleResourceCaps caps)
    {
        int hp = ClampResource(_values.Hp, caps.HpMax);
        _values = new BattleUnitCombatResourceValues(
            hp,
            ClampResource(_values.Mp, caps.MpMax),
            ClampResource(_values.Stamina, caps.StaminaMax),
            ClampResource(_values.Aura, caps.AuraMax),
            ClampResource(_values.Ap, caps.ApMax),
            ClampResource(_values.MovePoints, caps.MovePointMax),
            _values.StaminaRecoveryProgress,
            hp > 0
        );
    }

    internal bool ApplyStaminaRecovery(
        int tickCount,
        int staminaMax,
        int progressGainPerTick,
        int progressDenominator
    )
    {
        if (tickCount <= 0 || progressDenominator <= 0)
            return false;

        if (staminaMax <= 0)
        {
            if (
                _values.Stamina != 0
                || _values.StaminaRecoveryProgress != 0
            )
            {
                _values = _values with
                {
                    Stamina = 0,
                    StaminaRecoveryProgress = 0,
                };
                return true;
            }
            return false;
        }

        if (_values.Stamina >= staminaMax)
        {
            bool changed =
                _values.Stamina != staminaMax
                || _values.StaminaRecoveryProgress != 0;
            if (changed)
            {
                _values = _values with
                {
                    Stamina = staminaMax,
                    StaminaRecoveryProgress = 0,
                };
            }
            return changed;
        }

        bool recoveredAny = false;
        for (int index = 0; index < tickCount; index++)
        {
            int progress =
                _values.StaminaRecoveryProgress
                + progressGainPerTick;
            int recovered = progress / progressDenominator;
            _values = _values with
            {
                StaminaRecoveryProgress = progress,
            };
            if (recovered <= 0)
                continue;

            SetCurrentStamina(
                Math.Min(_values.Stamina + recovered, staminaMax)
            );
            _values = _values with
            {
                StaminaRecoveryProgress =
                    _values.StaminaRecoveryProgress
                    % progressDenominator,
            };
            recoveredAny = true;
            if (_values.Stamina >= staminaMax)
            {
                _values = _values with
                {
                    Stamina = staminaMax,
                    StaminaRecoveryProgress = 0,
                };
                break;
            }
        }
        return recoveredAny;
    }

    internal void SpendSkillCosts(
        SkillCostTransaction costs,
        bool includeAp
    )
    {
        if (costs == null)
        {
            return;
        }
        if (includeAp)
            SetCurrentAp(_values.Ap - costs.ApCost);
        SetCurrentMp(_values.Mp - costs.MpCost);
        SetCurrentStamina(_values.Stamina - costs.StaminaCost);
        SetCurrentAura(_values.Aura - costs.AuraCost);
    }

    internal void RefundSkillCosts(
        SkillCostTransaction costs,
        BattleResourceCaps caps
    )
    {
        if (costs == null)
        {
            return;
        }
        RefundSkillResources(
            costs.MpCost,
            costs.StaminaCost,
            costs.AuraCost,
            caps
        );
    }

    internal void RefundSkillResources(
        int mp,
        int stamina,
        int aura,
        BattleResourceCaps caps
    )
    {
        SetCurrentMp(
            Math.Min(
                _values.Mp + Math.Max(mp, 0),
                Math.Max(caps.MpMax, 0)
            )
        );
        SetCurrentStamina(
            Math.Min(
                _values.Stamina + Math.Max(stamina, 0),
                Math.Max(caps.StaminaMax, 0)
            )
        );
        SetCurrentAura(
            Math.Min(
                _values.Aura + Math.Max(aura, 0),
                Math.Max(caps.AuraMax, 0)
            )
        );
    }

    internal BattleUnitCombatResourceSnapshot CaptureRaw() =>
        BattleUnitCombatResourceSnapshot.Present(_values);

    internal void RestoreRaw(
        BattleUnitCombatResourceSnapshot snapshot
    )
    {
        _values = snapshot.Values;
    }

    internal BattleUnitCombatResourceState DuplicateState() =>
        FromRaw(CaptureRaw());

    internal static BattleUnitCombatResourceState FromRaw(
        BattleUnitCombatResourceSnapshot snapshot
    )
    {
        var result = new BattleUnitCombatResourceState();
        result.RestoreRaw(snapshot);
        return result;
    }

    private static int ClampResource(int value, int maxValue)
    {
        int normalizedMax = Math.Max(maxValue, 0);
        return normalizedMax > 0
            ? Math.Clamp(value, 0, normalizedMax)
            : 0;
    }
}
