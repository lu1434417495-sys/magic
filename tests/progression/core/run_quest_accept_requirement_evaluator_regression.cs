using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_quest_accept_requirement_evaluator_regression : LifecycleTestSceneTree
{
	private readonly TestHarness _test = new();

	public override void _Initialize()
	{
		RunAfterProcessStartup(Run);
	}

	private void Run()
	{
		TestNoRequirementsAccepts();
		TestQuestCompletedRequirement();
		TestQuestActiveRequirement();
		TestQuestNotCompletedRequirement();
		TestMultipleRequirements();
		TestMissingQuestId();
		TestUnknownRequirementType();
		TestDisplayNameResolution();
		TestResultFields();

		RequestTestExit(_test.Finish("Quest accept requirement evaluator regression"));
	}

	private void TestNoRequirementsAccepts()
	{
		PartyState partyState = new();
		QuestDefinition questDef = BuildQuestDef("no_req_quest", "无需求任务");
		QuestAcceptRequirementEvaluator evaluator = new();
		QuestAcceptContext context = BuildContext(partyState, new Dictionary<StringName, QuestDefinition>());

		QuestAcceptAvailabilityResult result = evaluator.Evaluate(questDef, context);
		_test.True(result.CanAccept, "无 accept_requirements 时应允许接取。");
	}

	private void TestQuestCompletedRequirement()
	{
		PartyState partyState = new();
		QuestDefinition targetQuest = BuildQuestDef(
			"target_quest",
			"目标任务",
			new GDictionary
			{
				["requirement_type"] = "quest_completed",
				["quest_id"] = "pre_req"
			}
		);
		QuestDefinition preReqQuest = BuildQuestDef("pre_req", "前置任务");
		var questDefs = new Dictionary<StringName, QuestDefinition>
		{
			["target_quest"] = targetQuest,
			["pre_req"] = preReqQuest,
		};
		QuestAcceptRequirementEvaluator evaluator = new();
		QuestAcceptContext context = BuildContext(partyState, questDefs);

		QuestAcceptAvailabilityResult resultBefore = evaluator.Evaluate(targetQuest, context);
		_test.False(resultBefore.CanAccept, "前置任务未完成时应拒绝接取。");
		_test.Eq(
			resultBefore.LockReasonId,
			(StringName)"quest_not_completed",
			"前置任务未完成时的锁定原因应为 quest_not_completed。"
		);

		partyState.completed_quest_ids.Add("pre_req");
		QuestAcceptAvailabilityResult resultAfter = evaluator.Evaluate(targetQuest, context);
		_test.True(resultAfter.CanAccept, "前置任务已完成后应允许接取。");
	}

	private void TestQuestActiveRequirement()
	{
		PartyState partyState = new();
		QuestDefinition targetQuest = BuildQuestDef(
			"target_quest",
			"目标任务",
			new GDictionary
			{
				["requirement_type"] = "quest_active",
				["quest_id"] = "pre_req"
			}
		);
		QuestDefinition preReqQuest = BuildQuestDef("pre_req", "前置任务");
		var questDefs = new Dictionary<StringName, QuestDefinition>
		{
			["target_quest"] = targetQuest,
			["pre_req"] = preReqQuest,
		};
		QuestAcceptRequirementEvaluator evaluator = new();
		QuestAcceptContext context = BuildContext(partyState, questDefs);

		QuestAcceptAvailabilityResult resultBefore = evaluator.Evaluate(targetQuest, context);
		_test.False(resultBefore.CanAccept, "前置任务未激活时应拒绝接取。");
		_test.Eq(
			resultBefore.LockReasonId,
			(StringName)"quest_not_active",
			"前置任务未激活时的锁定原因应为 quest_not_active。"
		);

		partyState.SetActiveQuestState(new QuestState { quest_id = "pre_req" });
		QuestAcceptAvailabilityResult resultAfter = evaluator.Evaluate(targetQuest, context);
		_test.True(resultAfter.CanAccept, "前置任务已激活时应允许接取。");
	}

	private void TestQuestNotCompletedRequirement()
	{
		PartyState partyState = new();
		QuestDefinition targetQuest = BuildQuestDef(
			"target_quest",
			"目标任务",
			new GDictionary
			{
				["requirement_type"] = "quest_not_completed",
				["quest_id"] = "pre_req"
			}
		);
		QuestDefinition preReqQuest = BuildQuestDef("pre_req", "前置任务");
		var questDefs = new Dictionary<StringName, QuestDefinition>
		{
			["target_quest"] = targetQuest,
			["pre_req"] = preReqQuest,
		};
		QuestAcceptRequirementEvaluator evaluator = new();
		QuestAcceptContext context = BuildContext(partyState, questDefs);

		QuestAcceptAvailabilityResult resultBefore = evaluator.Evaluate(targetQuest, context);
		_test.True(resultBefore.CanAccept, "前置任务未完成时应允许接取。");

		partyState.completed_quest_ids.Add("pre_req");
		QuestAcceptAvailabilityResult resultAfter = evaluator.Evaluate(targetQuest, context);
		_test.False(resultAfter.CanAccept, "前置任务已完成后应拒绝接取。");
		_test.Eq(
			resultAfter.LockReasonId,
			(StringName)"quest_already_completed",
			"前置任务已完成后锁定原因应为 quest_already_completed。"
		);
	}

	private void TestMultipleRequirements()
	{
		PartyState partyState = new();
		QuestDefinition targetQuest = BuildQuestDef(
			"target_quest",
			"目标任务",
			new GDictionary
			{
				["requirement_type"] = "quest_completed",
				["quest_id"] = "pre_req_a"
			},
			new GDictionary
			{
				["requirement_type"] = "quest_active",
				["quest_id"] = "pre_req_b"
			}
		);
		var questDefs = new Dictionary<StringName, QuestDefinition>
		{
			["target_quest"] = targetQuest,
			["pre_req_a"] = BuildQuestDef("pre_req_a", "前置任务 A"),
			["pre_req_b"] = BuildQuestDef("pre_req_b", "前置任务 B"),
		};
		QuestAcceptRequirementEvaluator evaluator = new();
		QuestAcceptContext context = BuildContext(partyState, questDefs);

		partyState.completed_quest_ids.Add("pre_req_a");
		_test.False(
			evaluator.Evaluate(targetQuest, context).CanAccept,
			"仅满足部分多需求时仍应拒绝。"
		);

		partyState.SetActiveQuestState(new QuestState { quest_id = "pre_req_b" });
		_test.True(
			evaluator.Evaluate(targetQuest, context).CanAccept,
			"满足全部多需求时应允许接取。"
		);
	}

	private void TestMissingQuestId()
	{
		PartyState partyState = new();
		QuestDefinition targetQuest = BuildQuestDef(
			"target_quest",
			"目标任务",
			new GDictionary
			{
				["requirement_type"] = "quest_completed"
			}
		);
		QuestAcceptRequirementEvaluator evaluator = new();
		QuestAcceptContext context = BuildContext(partyState, new Dictionary<StringName, QuestDefinition>());

		QuestAcceptAvailabilityResult result = evaluator.Evaluate(targetQuest, context);
		_test.False(result.CanAccept, "缺少 quest_id 时应拒绝接取。");
		_test.Eq(
			result.LockReasonId,
			(StringName)"missing_quest_id",
			"缺少 quest_id 时锁定原因应为 missing_quest_id。"
		);
	}

	private void TestUnknownRequirementType()
	{
		PartyState partyState = new();
		QuestDefinition targetQuest = BuildQuestDef(
			"target_quest",
			"目标任务",
			new GDictionary
			{
				["requirement_type"] = "unknown_type",
				["quest_id"] = "pre_req"
			}
		);
		QuestAcceptRequirementEvaluator evaluator = new();
		QuestAcceptContext context = BuildContext(partyState, new Dictionary<StringName, QuestDefinition>());

		QuestAcceptAvailabilityResult result = evaluator.Evaluate(targetQuest, context);
		_test.False(result.CanAccept, "未知需求类型时应拒绝接取。");
		_test.Eq(
			result.LockReasonId,
			(StringName)"unknown_requirement",
			"未知需求类型时锁定原因应为 unknown_requirement。"
		);
	}

	private void TestDisplayNameResolution()
	{
		PartyState partyState = new();
		QuestDefinition targetQuest = BuildQuestDef(
			"target_quest",
			"目标任务",
			new GDictionary
			{
				["requirement_type"] = "quest_completed",
				["quest_id"] = "known_pre_req"
			}
		);
		QuestDefinition knownPreReq = BuildQuestDef("known_pre_req", "已知前置任务");
		var questDefs = new Dictionary<StringName, QuestDefinition>
		{
			["target_quest"] = targetQuest,
			["known_pre_req"] = knownPreReq,
		};
		QuestAcceptRequirementEvaluator evaluator = new();
		QuestAcceptContext context = BuildContext(partyState, questDefs);

		QuestAcceptAvailabilityResult result = evaluator.Evaluate(targetQuest, context);
		_test.False(result.CanAccept, "未完成的已知前置任务应拒绝接取。");
		_test.True(
			result.DisabledReason.Contains("已知前置任务"),
			"拒绝原因应使用 QuestDef 的 display_name。"
		);

		QuestDefinition targetWithUnknown = BuildQuestDef(
			"target_unknown",
			"目标未知任务",
			new GDictionary
			{
				["requirement_type"] = "quest_completed",
				["quest_id"] = "unknown_pre_req"
			}
		);
		QuestAcceptAvailabilityResult unknownResult = evaluator.Evaluate(
			targetWithUnknown,
			BuildContext(partyState, questDefs)
		);
		_test.False(unknownResult.CanAccept, "未完成的未知前置任务应拒绝接取。");
		_test.True(
			unknownResult.DisabledReason.Contains("unknown_pre_req"),
			"未知前置任务的拒绝原因应回退到 quest_id。"
		);
	}

	private void TestResultFields()
	{
		PartyState partyState = new();
		QuestDefinition targetQuest = BuildQuestDef(
			"target_quest",
			"目标任务",
			new GDictionary
			{
				["requirement_type"] = "quest_completed",
				["quest_id"] = "pre_req"
			}
		);
		QuestAcceptRequirementEvaluator evaluator = new();
		QuestAcceptContext context = BuildContext(partyState, new Dictionary<StringName, QuestDefinition>());

		QuestAcceptAvailabilityResult acceptResult = QuestAcceptAvailabilityResult.Accept();
		_test.True(acceptResult.CanAccept, "Accept 工厂方法应返回 CanAccept=true。");
		_test.Eq(acceptResult.LockReasonId, (StringName)"", "Accept 的 LockReasonId 应为空。");
		_test.Eq(acceptResult.DisabledReason, "", "Accept 的 DisabledReason 应为空。");

		QuestAcceptAvailabilityResult rejectResult = evaluator.Evaluate(targetQuest, context);
		_test.False(rejectResult.CanAccept, "Reject 结果应返回 CanAccept=false。");
		_test.True(
			rejectResult.LockReasonId != (StringName)"",
			"Reject 结果应包含非空 LockReasonId。"
		);
		_test.True(
			!string.IsNullOrEmpty(rejectResult.DisabledReason),
			"Reject 结果应包含非空 DisabledReason。"
		);
	}

	private static QuestDefinition BuildQuestDef(
		StringName questId,
		string displayName,
		params GDictionary[] requirements
	)
	{
		var projectedRequirements = new List<QuestAcceptRequirementDefinition>();
		foreach (GDictionary requirement in requirements)
		{
			projectedRequirements.Add(
				new QuestAcceptRequirementDefinition(
					ReadStringName(requirement, "requirement_type"),
					ReadStringName(requirement, "quest_id")
				)
			);
		}
		return new QuestDefinition(
			questId,
			displayName,
			"",
			"service_contract_board",
			System.Array.Empty<StringName>(),
			projectedRequirements,
			System.Array.Empty<QuestObjectiveDefinition>(),
			System.Array.Empty<QuestRewardDefinition>(),
			false,
			"service_contract_board",
			new[] { new StringName("contract_board") },
			"",
			"",
			"",
			""
		);
	}

	private static QuestAcceptContext BuildContext(
		PartyState partyState,
		IReadOnlyDictionary<StringName, QuestDefinition> questDefs
	)
	{
		return new QuestAcceptContext
		{
			PartyState = partyState,
			WarehouseService = null,
			PartyGold = 0,
			WorldStep = 0,
			SettlementId = "",
			SettlementTier = 0,
			QuestDefs = questDefs,
		};
	}

	private static StringName ReadStringName(GDictionary source, string key)
	{
		if (source == null || !source.ContainsKey(key))
			return "";
		Variant value = source[key];
		return value.VariantType switch
		{
			Variant.Type.String => new StringName(value.AsString()),
			Variant.Type.StringName => value.AsStringName(),
			_ => new StringName(""),
		};
	}
}
