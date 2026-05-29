extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const BattleSessionFacade = preload("res://scripts/systems/game_runtime/BattleSessionFacade.cs")
const CharacterProgressionDelta = preload("res://scripts/systems/progression/CharacterProgressionDelta.cs")
const PartyMemberState = preload("res://scripts/player/progression/PartyMemberState.cs")
const PartyState = preload("res://scripts/player/progression/PartyState.cs")
const PendingProfessionChoice = preload("res://scripts/player/progression/PendingProfessionChoice.cs")
const ProfessionDef = preload("res://scripts/player/progression/ProfessionDef.cs")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_promotion_prompt_filters_invalid_candidates()

	if _failures.is_empty():
		print("Battle session promotion prompt regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Battle session promotion prompt regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_promotion_prompt_filters_invalid_candidates() -> void:
	var runtime := _FakeRuntime.new()
	runtime.party_state = _build_party_state()
	runtime.game_session = _FakeGameSession.new({
		&"warrior": _build_profession(&"warrior", "Warrior"),
		&"cleric": _build_profession(&"cleric", "Cleric"),
	})
	var facade := BattleSessionFacade.new()
	facade.setup(runtime)

	var pending_choice := PendingProfessionChoice.new()
	pending_choice.candidate_profession_ids = [&"warrior", &"rogue", &"mage", &"cleric"]
	pending_choice.set_target_rank(&"warrior", 1)
	pending_choice.set_target_rank(&"cleric", 0)
	var delta := CharacterProgressionDelta.new()
	delta.member_id = &"hero"
	delta.needs_promotion_modal = true
	delta.pending_profession_choices.append(pending_choice)

	var prompt := facade.build_promotion_prompt(delta, "确认后将在战斗中立即生效。")
	var choices: Array = prompt.get("choices", [])
	_assert_eq(choices.size(), 1, "Prompt should expose only candidates with a known profession and positive target rank.")
	_assert_eq(String(choices[0].get("profession_id", "")), "warrior", "Prompt should keep the valid warrior candidate.")
	_assert_eq(String(prompt.get("member_name", "")), "Hero", "Prompt should still include the member display name.")


func _build_party_state() -> PartyState:
	var party_state := PartyState.new()
	var member := PartyMemberState.new()
	member.member_id = &"hero"
	member.display_name = "Hero"
	party_state.set_member_state(member)
	return party_state


func _build_profession(profession_id: StringName, display_name: String) -> ProfessionDef:
	var profession := ProfessionDef.new()
	profession.profession_id = profession_id
	profession.display_name = display_name
	return profession


class _FakeGameSession extends RefCounted:
	var profession_defs: Dictionary = {}


	func _init(defs: Dictionary) -> void:
		profession_defs = defs


	func get_profession_defs() -> Dictionary:
		return profession_defs


class _FakeRuntime extends RefCounted:
	var party_state: PartyState = null
	var game_session = null


	func get_party_state():
		return party_state


	func get_game_session():
		return game_session


func _assert_eq(actual, expected, message: String) -> void:
	if actual == expected:
		return
	_test.fail("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
