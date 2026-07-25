using System;
using Godot;

internal readonly record struct BattleUnitShieldSnapshot(
    int CurrentHp,
    int MaxHp,
    int Duration,
    StringName Family,
    StringName SourceUnitId,
    StringName SourceSkillId
)
{
    internal static BattleUnitShieldSnapshot MissingOwner =>
        new(0, 0, 0, default, default, default);
}

internal readonly record struct BattleUnitShieldDrainResult(
    int ActualDrain,
    bool Depleted
);

internal sealed class BattleUnitShieldState
{
    private int _currentHp;
    private int _maxHp;
    private int _duration = -1;
    private StringName _family = "";
    private StringName _sourceUnitId = "";
    private StringName _sourceSkillId = "";

    internal BattleUnitShieldSnapshot CaptureRaw() =>
        new(
            _currentHp,
            _maxHp,
            _duration,
            _family,
            _sourceUnitId,
            _sourceSkillId
        );

    internal BattleUnitShieldSnapshot CaptureCanonical()
    {
        Normalize();
        return CaptureRaw();
    }

    internal bool HasActiveShield() =>
        _currentHp > 0 && _maxHp > 0 && _duration > 0;

    internal void ReplaceAndNormalize(BattleUnitShieldSnapshot snapshot)
    {
        RestoreRaw(snapshot);
        Normalize();
    }

    internal void SetCurrentHpAndNormalize(int currentHp)
    {
        _currentHp = currentHp;
        Normalize();
    }

    internal bool AdvanceDuration(int elapsedTu)
    {
        if (elapsedTu <= 0 || !HasActiveShield())
        {
            return false;
        }
        if (elapsedTu >= _duration)
        {
            Clear();
            return true;
        }
        _duration -= elapsedTu;
        return true;
    }

    internal BattleUnitShieldDrainResult DrainCurrentHp(int requestedDrain)
    {
        Normalize();
        int actualDrain = Math.Min(Math.Max(requestedDrain, 0), _currentHp);
        _currentHp = Math.Max(_currentHp - actualDrain, 0);
        bool depleted = actualDrain > 0 && _currentHp <= 0;
        if (depleted)
        {
            Clear();
        }
        else
        {
            Normalize();
        }
        return new BattleUnitShieldDrainResult(actualDrain, depleted);
    }

    internal void Clear()
    {
        _currentHp = 0;
        _maxHp = 0;
        _duration = -1;
        _family = "";
        _sourceUnitId = "";
        _sourceSkillId = "";
    }

    internal void Normalize()
    {
        if (_currentHp <= 0 || _maxHp <= 0 || _duration <= 0)
        {
            Clear();
            return;
        }
        _maxHp = Math.Max(_maxHp, 1);
        _currentHp = Math.Clamp(_currentHp, 0, _maxHp);
        if (_currentHp <= 0)
        {
            Clear();
        }
    }

    internal BattleUnitShieldState DuplicateState()
    {
        var duplicate = new BattleUnitShieldState();
        duplicate.RestoreRaw(CaptureRaw());
        return duplicate;
    }

    internal void RestoreRaw(BattleUnitShieldSnapshot snapshot)
    {
        _currentHp = snapshot.CurrentHp;
        _maxHp = snapshot.MaxHp;
        _duration = snapshot.Duration;
        _family = snapshot.Family;
        _sourceUnitId = snapshot.SourceUnitId;
        _sourceSkillId = snapshot.SourceSkillId;
    }

    internal static BattleUnitShieldState FromRaw(BattleUnitShieldSnapshot snapshot)
    {
        var result = new BattleUnitShieldState();
        result.RestoreRaw(snapshot);
        return result;
    }
}
