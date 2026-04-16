[CmdletBinding()]
param(
	[string]$RepoRoot = ".",
	[string]$StoryId = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Push-Location $RepoRoot
try {
	Write-Host "Running Ralph checks for story: $StoryId"
	& godot --headless --script tests/battle_runtime/run_battle_runtime_smoke.gd
	& godot --headless --script tests/progression/run_progression_tests.gd
	& godot --headless --script tests/warehouse/run_party_warehouse_regression.gd
}
finally {
	Pop-Location
}
