using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictArray = Godot.Collections.Array<Godot.Collections.Dictionary>;
using GIntArray = Godot.Collections.Array<int>;
using GStringArray = Godot.Collections.Array<string>;

/// <summary>
/// 单阶段命中预览数据。
/// </summary>
public readonly record struct AttackPreviewStage
{
	public readonly int HitRatePercent;
	public readonly int SuccessRatePercent;
	public readonly int BaseHitRatePercent;
	public readonly int RequiredRoll;
	public readonly int DisplayRequiredRoll;
	public readonly string PreviewText;

	public AttackPreviewStage(
		int hitRatePercent,
		int successRatePercent,
		int baseHitRatePercent,
		int requiredRoll,
		int displayRequiredRoll,
		string previewText
	)
	{
		HitRatePercent = hitRatePercent;
		SuccessRatePercent = successRatePercent;
		BaseHitRatePercent = baseHitRatePercent;
		RequiredRoll = requiredRoll;
		DisplayRequiredRoll = displayRequiredRoll;
		PreviewText = previewText;
	}
}

/// <summary>
/// 攻击命中预览的强类型表示。
/// 替代原 GDictionary 承载的 build_skill_attack_preview /
/// build_repeat_attack_preview / build_force_hit_no_crit_attack_preview 结果。
/// </summary>
[GlobalClass]
public partial class AttackPreviewData : RefCounted
{
	private readonly List<BattleAttackRollModifierSpec> _attackRollModifierBreakdown = new();

	public string SummaryText { get; set; } = "";

	public List<AttackPreviewStage> Stages { get; set; } = new();

	public int HitRatePercent { get; set; }
	public int SuccessRatePercent { get; set; }
	public int BaseHitRatePercent { get; set; }

	public bool ForceHitNoCrit { get; set; }
	public bool CritLocked { get; set; }

	// 仅多段攻击有效
	public int BaseAttackBonus { get; set; }
	public int FollowUpAttackPenalty { get; set; }

	internal IReadOnlyList<BattleAttackRollModifierSpec> AttackRollModifierBreakdownTyped =>
		_attackRollModifierBreakdown;

	// BattleAttackCheckPolicyService 追加的 modifier 分解信息。公开属性只做 Godot 边界投影。
	public GDictArray AttackRollModifierBreakdown => BuildAttackRollModifierBreakdownPayload(
		_attackRollModifierBreakdown
	);

	// Special profile 来源标记（如陨星雨等自定义预览）
	public string Source { get; set; } = "";

	public bool IsEmpty => Stages.Count == 0 && SuccessRatePercent == 0 && BaseHitRatePercent == 0 && string.IsNullOrEmpty(SummaryText);
	public int StageCount => Stages.Count;

	public void SetAttackRollModifierBreakdown(
		IEnumerable<BattleAttackRollModifierSpec> specs
	)
	{
		_attackRollModifierBreakdown.Clear();
		if (specs == null)
		{
			return;
		}
		foreach (BattleAttackRollModifierSpec spec in specs)
		{
			if (spec != null)
			{
				_attackRollModifierBreakdown.Add(spec.Clone());
			}
		}
	}

	public void SetAttackRollModifierBreakdownPayload(GArray payload)
	{
		_attackRollModifierBreakdown.Clear();
		if (payload == null)
		{
			return;
		}
		foreach (Variant item in payload)
		{
			if (item.VariantType != Variant.Type.Dictionary)
			{
				continue;
			}
			AddAttackRollModifierBreakdownPayloadDictionary(item.AsGodotDictionary());
		}
	}

	public void SetAttackRollModifierBreakdownPayload(GDictArray payload)
	{
		_attackRollModifierBreakdown.Clear();
		if (payload == null)
		{
			return;
		}
		foreach (Godot.Collections.Dictionary item in payload)
		{
			AddAttackRollModifierBreakdownPayloadDictionary(item);
		}
	}

	public GDictArray BuildAttackRollModifierBreakdownPayload()
	{
		return BuildAttackRollModifierBreakdownPayload(_attackRollModifierBreakdown);
	}

	public static GDictArray BuildAttackRollModifierBreakdownPayload(
		IEnumerable<BattleAttackRollModifierSpec> specs
	)
	{
		var payloads = new GDictArray();
		foreach (
			BattleAttackRollModifierSpec spec in specs
				?? System.Array.Empty<BattleAttackRollModifierSpec>()
		)
		{
			if (spec == null)
			{
				continue;
			}
			payloads.Add(spec.ToDictionaryWithEffectiveModifierDelta(spec.modifier_delta));
		}
		return payloads;
	}

	private void AddAttackRollModifierBreakdownPayloadDictionary(
		Godot.Collections.Dictionary payload
	)
	{
		BattleAttackRollModifierSpec spec =
			BattleAttackRollModifierSpec.FromPartialDictionary(payload);
		if (spec != null)
		{
			_attackRollModifierBreakdown.Add(spec);
		}
	}

	// ---- 便捷属性：按字段名返回数组，减少现有调用者的改动量 ----

	public GIntArray StageHitRates
	{
		get
		{
			var result = new GIntArray();
			foreach (var stage in Stages)
				result.Add(stage.HitRatePercent);
			return result;
		}
	}

	public GIntArray StageSuccessRates
	{
		get
		{
			var result = new GIntArray();
			foreach (var stage in Stages)
				result.Add(stage.SuccessRatePercent);
			return result;
		}
	}

	public GIntArray StageBaseHitRates
	{
		get
		{
			var result = new GIntArray();
			foreach (var stage in Stages)
				result.Add(stage.BaseHitRatePercent);
			return result;
		}
	}

	public GIntArray StageRequiredRolls
	{
		get
		{
			var result = new GIntArray();
			foreach (var stage in Stages)
				result.Add(stage.DisplayRequiredRoll);
			return result;
		}
	}

	public GStringArray StagePreviewTexts
	{
		get
		{
			var result = new GStringArray();
			foreach (var stage in Stages)
				result.Add(stage.PreviewText);
			return result;
		}
	}
}
