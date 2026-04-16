[CmdletBinding()]
param(
	[ValidateRange(1, 1000)]
	[int]$MaxIterations = 10,

	[string]$WorkingDirectory = ".",
	[string]$StateDirectory = ".ralph",
	[string]$StoryId = "",
	[string]$CodexModel = "",

	[ValidateSet("read-only", "workspace-write", "danger-full-access")]
	[string]$CodexSandboxMode = "workspace-write",

	[switch]$AllowDirtyWorktree,
	[switch]$NoCommit,
	[switch]$SkipChecks,
	[switch]$PersistCodexSessions,
	[switch]$ContinueAfterFailure
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$script:DefaultRalphSkills = @(
	"godot-master",
	"algorithm-design"
)

function Resolve-AbsolutePath {
	param(
		[Parameter(Mandatory = $true)]
		[string]$Path,

		[switch]$AllowMissing
	)

	$candidate = $Path
	if (-not [System.IO.Path]::IsPathRooted($candidate)) {
		$candidate = Join-Path (Get-Location).Path $candidate
	}

	$fullPath = [System.IO.Path]::GetFullPath($candidate)
	if (-not $AllowMissing -and -not (Test-Path -LiteralPath $fullPath)) {
		throw "Path does not exist: $Path"
	}

	return $fullPath
}

function Ensure-CommandExists {
	param([Parameter(Mandatory = $true)][string]$Name)

	$command = Get-Command $Name -ErrorAction SilentlyContinue
	if ($null -eq $command) {
		throw "Required command is not available: $Name"
	}
}

function Get-PreferredPowerShellCommand {
	foreach ($candidate in @("pwsh", "powershell")) {
		$command = Get-Command $candidate -ErrorAction SilentlyContinue
		if ($null -ne $command) {
			return $candidate
		}
	}

	throw "Neither pwsh nor powershell is available."
}

function Get-ResolvedCommandPath {
	param([Parameter(Mandatory = $true)][string]$Name)

	$commands = @(Get-Command $Name -All -ErrorAction Stop)
	foreach ($preferredType in @("Application", "ExternalScript")) {
		foreach ($command in $commands) {
			if ([string]$command.CommandType -ne $preferredType) {
				continue
			}
			foreach ($candidate in @($command.Source, $command.Path, $command.Definition)) {
				if (-not [string]::IsNullOrWhiteSpace([string]$candidate)) {
					return [string]$candidate
				}
			}
		}
	}

	throw "Unable to resolve command path: $Name"
}

function Read-JsonFile {
	param([Parameter(Mandatory = $true)][string]$Path)

	$raw = Get-Content -LiteralPath $Path -Raw
	if ([string]::IsNullOrWhiteSpace($raw)) {
		throw "JSON file is empty: $Path"
	}

	return $raw | ConvertFrom-Json
}

function Write-JsonFile {
	param(
		[Parameter(Mandatory = $true)]$Data,
		[Parameter(Mandatory = $true)][string]$Path
	)

	$parent = Split-Path -Parent $Path
	if (-not [string]::IsNullOrWhiteSpace($parent)) {
		New-Item -ItemType Directory -Path $parent -Force | Out-Null
	}

	$json = $Data | ConvertTo-Json -Depth 100
	Set-Content -LiteralPath $Path -Value $json -Encoding utf8
}

function Ensure-TextFile {
	param(
		[Parameter(Mandatory = $true)][string]$Path,
		[string]$DefaultContent = ""
	)

	$parent = Split-Path -Parent $Path
	if (-not [string]::IsNullOrWhiteSpace($parent)) {
		New-Item -ItemType Directory -Path $parent -Force | Out-Null
	}

	if (-not (Test-Path -LiteralPath $Path)) {
		Set-Content -LiteralPath $Path -Value $DefaultContent -Encoding utf8
	}
}

function Set-PropertyValue {
	param(
		[Parameter(Mandatory = $true)]$Object,
		[Parameter(Mandatory = $true)][string]$Name,
		$Value
	)

	$property = $Object.PSObject.Properties[$Name]
	if ($null -eq $property) {
		$Object | Add-Member -NotePropertyName $Name -NotePropertyValue $Value
	}
	else {
		$property.Value = $Value
	}
}

function Get-RepoRoot {
	param([Parameter(Mandatory = $true)][string]$Directory)

	$resolvedDirectory = Resolve-AbsolutePath -Path $Directory
	$repoRoot = & git -C $resolvedDirectory rev-parse --show-toplevel
	if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($repoRoot)) {
		throw "Failed to resolve git repository root from: $Directory"
	}

	return $repoRoot.Trim()
}

function Get-RunTimestamp {
	return (Get-Date).ToString("yyyy-MM-ddTHH:mm:ssK")
}

function Get-StatePaths {
	param(
		[Parameter(Mandatory = $true)][string]$RepoRoot,
		[Parameter(Mandatory = $true)][string]$StateDirectoryName
	)

	$stateRoot = Resolve-AbsolutePath -Path (Join-Path $RepoRoot $StateDirectoryName) -AllowMissing
	$runsRoot = Join-Path $stateRoot "runs"
	New-Item -ItemType Directory -Path $runsRoot -Force | Out-Null

	return @{
		StateRoot = $stateRoot
		RunsRoot = $runsRoot
		Prd = Join-Path $stateRoot "prd.json"
		Checks = Join-Path $stateRoot "checks.ps1"
		Progress = Join-Path $stateRoot "progress.md"
		Guardrails = Join-Path $stateRoot "guardrails.md"
		OutputSchema = Join-Path $stateRoot "output_schema.json"
	}
}

function Assert-LoopFilesExist {
	param([Parameter(Mandatory = $true)]$Paths)

	foreach ($requiredPath in @(
		$Paths.Prd,
		$Paths.Checks,
		$Paths.Progress,
		$Paths.Guardrails,
		$Paths.OutputSchema
	)) {
		if (-not (Test-Path -LiteralPath $requiredPath)) {
			throw "Missing Ralph file: $requiredPath"
		}
	}
}

function Get-DirtyWorktreeEntries {
	param([Parameter(Mandatory = $true)][string]$RepoRoot)

	$entries = & git -C $RepoRoot status --porcelain
	if ($LASTEXITCODE -ne 0) {
		throw "Failed to read git worktree status."
	}

	return @($entries | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

function Get-CurrentBranchName {
	param([Parameter(Mandatory = $true)][string]$RepoRoot)

	$branch = & git -C $RepoRoot branch --show-current
	if ($LASTEXITCODE -ne 0) {
		throw "Failed to read current branch name."
	}

	return $branch.Trim()
}

function Ensure-LoopBranch {
	param(
		[Parameter(Mandatory = $true)][string]$RepoRoot,
		$State
	)

	$branchName = ""
	if ($null -ne $State.PSObject.Properties["branchName"]) {
		$branchName = [string]$State.branchName
	}
	if ([string]::IsNullOrWhiteSpace($branchName)) {
		return
	}

	$currentBranch = Get-CurrentBranchName -RepoRoot $RepoRoot
	if ($currentBranch -eq $branchName) {
		return
	}

	& git -C $RepoRoot checkout -B $branchName | Out-Null
	if ($LASTEXITCODE -ne 0) {
		throw "Failed to create or switch branch: $branchName"
	}
}

function Get-MaxAttemptsPerStory {
	param($State)

	if ($null -ne $State.PSObject.Properties["maxAttemptsPerStory"]) {
		return [int]$State.maxAttemptsPerStory
	}
	return 3
}

function Get-CommitPrefix {
	param($State)

	if ($null -ne $State.PSObject.Properties["commitPrefix"]) {
		$value = [string]$State.commitPrefix
		if (-not [string]::IsNullOrWhiteSpace($value)) {
			return $value.Trim()
		}
	}
	return "chore(ralph):"
}

function Select-Story {
	param(
		$State,
		[string]$RequestedStoryId
	)

	$stories = @($State.userStories)
	if ([string]::IsNullOrWhiteSpace($RequestedStoryId)) {
		return $stories | Where-Object { $_.status -eq "open" } | Select-Object -First 1
	}

	return $stories | Where-Object { $_.id -eq $RequestedStoryId -and ($_.status -eq "open" -or $_.status -eq "in_progress") } | Select-Object -First 1
}

function Format-StoryJson {
	param($Story)

	return ($Story | ConvertTo-Json -Depth 50)
}

function Update-StoryForAttempt {
	param(
		$Story,
		[string]$RunId
	)

	$timestamp = Get-RunTimestamp
	$attemptCount = 0
	if ($null -ne $Story.PSObject.Properties["attemptCount"]) {
		$attemptCount = [int]$Story.attemptCount
	}
	$attemptCount += 1

	Set-PropertyValue -Object $Story -Name "status" -Value "in_progress"
	Set-PropertyValue -Object $Story -Name "attemptCount" -Value $attemptCount
	Set-PropertyValue -Object $Story -Name "lastAttemptAt" -Value $timestamp
	Set-PropertyValue -Object $Story -Name "lastRunId" -Value $RunId
	if ($null -eq $Story.PSObject.Properties["startedAt"] -or [string]::IsNullOrWhiteSpace([string]$Story.startedAt)) {
		Set-PropertyValue -Object $Story -Name "startedAt" -Value $timestamp
	}
}

function Mark-StoryDone {
	param(
		$Story,
		[string]$ChecksRun,
		[string]$Learnings
	)

	Set-PropertyValue -Object $Story -Name "status" -Value "done"
	Set-PropertyValue -Object $Story -Name "completedAt" -Value (Get-RunTimestamp)
	Set-PropertyValue -Object $Story -Name "lastFailure" -Value ""
	Set-PropertyValue -Object $Story -Name "lastChecksRun" -Value $ChecksRun
	Set-PropertyValue -Object $Story -Name "lastLearnings" -Value $Learnings
}

function Mark-StoryFailed {
	param(
		$Story,
		[int]$MaxAttempts,
		[string]$FailureText,
		[string]$ChecksRun,
		[string]$Learnings
	)

	$attemptCount = 0
	if ($null -ne $Story.PSObject.Properties["attemptCount"]) {
		$attemptCount = [int]$Story.attemptCount
	}
	$newStatus = if ($attemptCount -ge $MaxAttempts) { "blocked" } else { "open" }

	Set-PropertyValue -Object $Story -Name "status" -Value $newStatus
	Set-PropertyValue -Object $Story -Name "lastFailure" -Value $FailureText
	Set-PropertyValue -Object $Story -Name "lastChecksRun" -Value $ChecksRun
	Set-PropertyValue -Object $Story -Name "lastLearnings" -Value $Learnings
}

function Append-MarkdownLog {
	param(
		[Parameter(Mandatory = $true)][string]$Path,
		[Parameter(Mandatory = $true)][string]$Body
	)

	Add-Content -LiteralPath $Path -Value "`n$Body" -Encoding utf8
}

function Append-ProgressEntry {
	param(
		[Parameter(Mandatory = $true)][string]$Path,
		$Story,
		[Parameter(Mandatory = $true)][string]$Status,
		[Parameter(Mandatory = $true)][string]$Note
	)

	$body = @"
## $(Get-RunTimestamp) | $($Story.id) | $Status
title: $($Story.title)
$Note
"@
	Append-MarkdownLog -Path $Path -Body $body
}

function Append-GuardrailEntry {
	param(
		[Parameter(Mandatory = $true)][string]$Path,
		$Story,
		[Parameter(Mandatory = $true)][string]$FailureText,
		[Parameter(Mandatory = $true)][string]$RunId
	)

	$body = @"
- $(Get-RunTimestamp) | $($Story.id) | run=$RunId
  failure: $FailureText
"@
	Append-MarkdownLog -Path $Path -Body $body
}

function Render-Prompt {
	param(
		$Story,
		[Parameter(Mandatory = $true)][string]$RepoRoot,
		[Parameter(Mandatory = $true)]$Paths
	)

	$skillLines = ($script:DefaultRalphSkills | ForEach-Object { "- `$" + $_ }) -join "`r`n"
	$storyJson = Format-StoryJson -Story $Story
	return @"
You are Codex running inside this repository's Ralph loop.

Repository root:
$RepoRoot

You must work on exactly one story this iteration.

Mandatory repo rules:
- Read docs/design/project_context_units.md before planning or editing.
- Read the nearest AGENTS.md instructions and follow them.
- Do not edit $($Paths.Prd); the outer loop owns story state.
- Update docs/design/project_context_units.md only if runtime relationships, ownership boundaries, or recommended read sets truly changed.
- Keep scope to the minimum complete slice that satisfies the story.
- Prefer existing tests and existing project commands over inventing new ones.

Project memory files:
- $($Paths.Progress)
- $($Paths.Guardrails)
- NEXTACTION.md

Use the following repository-default skills for this run:
$skillLines

Current story JSON:
$storyJson

Before you finish:
- Summarize what changed.
- Summarize what checks you ran yourself, if any.
- Summarize any durable learning the outer loop should keep.

Return JSON matching the provided schema with:
- result: done | blocked
- changed: short summary
- checks_run: short summary
- learnings: short summary
"@
}

function Invoke-CodexIteration {
	param(
		[Parameter(Mandatory = $true)][string]$PromptText,
		[Parameter(Mandatory = $true)][string]$RepoRoot,
		[Parameter(Mandatory = $true)]$Paths,
		[Parameter(Mandatory = $true)][string]$RunId,
		[AllowEmptyString()][string]$Model = "",
		[Parameter(Mandatory = $true)][string]$SandboxMode,
		[switch]$PersistSessions
	)

	$runPrefix = Join-Path $Paths.RunsRoot $RunId
	$promptPath = "${runPrefix}.prompt.md"
	$eventsPath = "${runPrefix}.events.jsonl"
	$stderrPath = "${runPrefix}.stderr.log"
	$finalPath = "${runPrefix}.final.json"

	Set-Content -LiteralPath $promptPath -Value $PromptText -Encoding utf8

	$args = @(
		"--cd", $RepoRoot,
		"--sandbox", $SandboxMode,
		"--json",
		"--output-schema", $Paths.OutputSchema,
		"-o", $finalPath
	)

	if (-not [string]::IsNullOrWhiteSpace($Model)) {
		$args = @("exec", "-m", $Model) + $args
	}
	else {
		$args = @("exec") + $args
	}

	if (-not $PersistSessions) {
		$args += "--ephemeral"
	}

	$args += "-"

	foreach ($path in @($eventsPath, $stderrPath)) {
		if (Test-Path -LiteralPath $path) {
			Remove-Item -LiteralPath $path -Force
		}
	}

	$codexCommandPath = Get-ResolvedCommandPath -Name "codex"
	$process = Start-Process `
		-FilePath $codexCommandPath `
		-ArgumentList $args `
		-WorkingDirectory $RepoRoot `
		-RedirectStandardInput $promptPath `
		-RedirectStandardOutput $eventsPath `
		-RedirectStandardError $stderrPath `
		-Wait `
		-PassThru

	$exitCode = [int]$process.ExitCode

	return @{
		ExitCode = $exitCode
		PromptPath = $promptPath
		EventsPath = $eventsPath
		StderrPath = $stderrPath
		FinalPath = $finalPath
	}
}

function Read-CodexResult {
	param(
		[Parameter(Mandatory = $true)][string]$FinalPath
	)

	if (-not (Test-Path -LiteralPath $FinalPath)) {
		throw "Codex final message file not found: $FinalPath"
	}

	$raw = Get-Content -LiteralPath $FinalPath -Raw
	if ([string]::IsNullOrWhiteSpace($raw)) {
		throw "Codex final message file is empty: $FinalPath"
	}

	return $raw | ConvertFrom-Json
}

function Normalize-CodexErrorText {
	param([string]$Text)

	if ([string]::IsNullOrWhiteSpace($Text)) {
		return ""
	}

	$candidate = $Text.Trim()
	try {
		$parsed = $candidate | ConvertFrom-Json -ErrorAction Stop
	}
	catch {
		return $candidate
	}

	if ($null -ne $parsed -and $null -ne $parsed.PSObject.Properties["error"]) {
		$nestedError = $parsed.error
		if ($null -ne $nestedError -and $null -ne $nestedError.PSObject.Properties["message"]) {
			$nestedMessage = [string]$nestedError.message
			if (-not [string]::IsNullOrWhiteSpace($nestedMessage)) {
				return Normalize-CodexErrorText -Text $nestedMessage
			}
		}
	}

	if ($null -ne $parsed -and $null -ne $parsed.PSObject.Properties["message"]) {
		$message = [string]$parsed.message
		if (-not [string]::IsNullOrWhiteSpace($message)) {
			return Normalize-CodexErrorText -Text $message
		}
	}

	return $candidate
}

function Get-CodexFailureSummary {
	param(
		[Parameter(Mandatory = $true)][string]$EventsPath,
		[Parameter(Mandatory = $true)][string]$StderrPath
	)

	$messages = New-Object System.Collections.Generic.List[string]

	if (Test-Path -LiteralPath $EventsPath) {
		foreach ($line in Get-Content -LiteralPath $EventsPath) {
			if ([string]::IsNullOrWhiteSpace($line)) {
				continue
			}

			try {
				$event = $line | ConvertFrom-Json -ErrorAction Stop
			}
			catch {
				continue
			}

			$rawMessage = ""
			if ($event.type -eq "error" -and $null -ne $event.PSObject.Properties["message"]) {
				$rawMessage = [string]$event.message
			}
			elseif ($event.type -eq "turn.failed" -and $null -ne $event.PSObject.Properties["error"]) {
				$eventError = $event.error
				if ($null -ne $eventError -and $null -ne $eventError.PSObject.Properties["message"]) {
					$rawMessage = [string]$eventError.message
				}
			}

			$normalized = Normalize-CodexErrorText -Text $rawMessage
			if (-not [string]::IsNullOrWhiteSpace($normalized) -and -not $messages.Contains($normalized)) {
				$messages.Add($normalized)
			}
		}
	}

	if (Test-Path -LiteralPath $StderrPath) {
		$stderrRaw = Get-Content -LiteralPath $StderrPath -Raw
		$stderrText = Normalize-CodexErrorText -Text $stderrRaw
		if (-not [string]::IsNullOrWhiteSpace($stderrText) -and -not $messages.Contains($stderrText)) {
			$messages.Add($stderrText)
		}
	}

	if ($messages.Count -gt 0) {
		return ($messages -join " | ")
	}

	return "No structured Codex error details found. events: $EventsPath"
}

function Invoke-Checks {
	param(
		[Parameter(Mandatory = $true)]$Paths,
		[Parameter(Mandatory = $true)][string]$RepoRoot,
		[Parameter(Mandatory = $true)]$Story,
		[Parameter(Mandatory = $true)][string]$RunId,
		[Parameter(Mandatory = $true)][string]$PowerShellCommand
	)

	$checkLogPath = Join-Path $Paths.RunsRoot "${RunId}.checks.log"
	& $PowerShellCommand -ExecutionPolicy Bypass -File $Paths.Checks -RepoRoot $RepoRoot -StoryId ([string]$Story.id) 1> $checkLogPath 2>&1
	$exitCode = $LASTEXITCODE

	return @{
		ExitCode = $exitCode
		LogPath = $checkLogPath
	}
}

function Commit-IfNeeded {
	param(
		[Parameter(Mandatory = $true)][string]$RepoRoot,
		[Parameter(Mandatory = $true)][string]$Message
	)

	& git -C $RepoRoot add -A | Out-Null
	if ($LASTEXITCODE -ne 0) {
		throw "git add failed."
	}

	& git -C $RepoRoot diff --cached --quiet
	$hasNoStagedChanges = ($LASTEXITCODE -eq 0)
	if ($hasNoStagedChanges) {
		return $false
	}

	& git -C $RepoRoot commit -m $Message
	if ($LASTEXITCODE -ne 0) {
		throw "git commit failed."
	}

	return $true
}

Ensure-CommandExists -Name "git"
Ensure-CommandExists -Name "codex"
$powerShellCommand = Get-PreferredPowerShellCommand

$repoRoot = Get-RepoRoot -Directory $WorkingDirectory
$paths = Get-StatePaths -RepoRoot $repoRoot -StateDirectoryName $StateDirectory
Assert-LoopFilesExist -Paths $paths
Ensure-TextFile -Path $paths.Progress -DefaultContent "# Ralph Progress"
Ensure-TextFile -Path $paths.Guardrails -DefaultContent "# Ralph Guardrails"

if (-not $AllowDirtyWorktree) {
	$dirtyEntries = Get-DirtyWorktreeEntries -RepoRoot $repoRoot
	if ($dirtyEntries.Count -gt 0) {
		throw "Worktree must be clean before starting Ralph loop. Use -AllowDirtyWorktree to override."
	}
}

$state = Read-JsonFile -Path $paths.Prd
$maxAttemptsPerStory = Get-MaxAttemptsPerStory -State $state
$commitPrefix = Get-CommitPrefix -State $state

for ($iteration = 1; $iteration -le $MaxIterations; $iteration++) {
	$state = Read-JsonFile -Path $paths.Prd
	$story = Select-Story -State $state -RequestedStoryId $StoryId

	if ($null -eq $story) {
		Write-Host "No eligible story found. Ralph loop finished."
		exit 0
	}

	Ensure-LoopBranch -RepoRoot $repoRoot -State $state

	$runId = "{0}-{1}" -f (Get-Date -Format "yyyyMMdd-HHmmss"), $story.id
	Update-StoryForAttempt -Story $story -RunId $runId
	Write-JsonFile -Data $state -Path $paths.Prd

	Write-Host ("[{0}/{1}] story={2} title={3}" -f $iteration, $MaxIterations, $story.id, $story.title)

	$promptText = Render-Prompt -Story $story -RepoRoot $repoRoot -Paths $paths
	$codexRun = Invoke-CodexIteration -PromptText $promptText -RepoRoot $repoRoot -Paths $paths -RunId $runId -Model $CodexModel -SandboxMode $CodexSandboxMode -PersistSessions:$PersistCodexSessions

	if ([int]$codexRun.ExitCode -ne 0) {
		$codexErrorSummary = Get-CodexFailureSummary -EventsPath $codexRun.EventsPath -StderrPath $codexRun.StderrPath
		$failureText = "Codex exec failed: $codexErrorSummary"
		Mark-StoryFailed -Story $story -MaxAttempts $maxAttemptsPerStory -FailureText $failureText -ChecksRun "" -Learnings ""
		Write-JsonFile -Data $state -Path $paths.Prd
		Append-ProgressEntry -Path $paths.Progress -Story $story -Status "agent_failed" -Note $failureText
		Append-GuardrailEntry -Path $paths.Guardrails -Story $story -FailureText $failureText -RunId $runId
		Write-Warning $failureText
		if (-not $ContinueAfterFailure) {
			exit 1
		}
		continue
	}

	$codexResult = Read-CodexResult -FinalPath $codexRun.FinalPath
	$changedSummary = [string]$codexResult.changed
	$checksSummary = [string]$codexResult.checks_run
	$learningsSummary = [string]$codexResult.learnings
	$resultState = [string]$codexResult.result

	if ($resultState -ne "done") {
		$failureText = if ([string]::IsNullOrWhiteSpace($changedSummary)) {
			"Codex returned blocked."
		}
		else {
			"Codex returned blocked: $changedSummary"
		}
		Mark-StoryFailed -Story $story -MaxAttempts $maxAttemptsPerStory -FailureText $failureText -ChecksRun $checksSummary -Learnings $learningsSummary
		Write-JsonFile -Data $state -Path $paths.Prd
		Append-ProgressEntry -Path $paths.Progress -Story $story -Status "blocked" -Note $failureText
		Append-GuardrailEntry -Path $paths.Guardrails -Story $story -FailureText $failureText -RunId $runId
		Write-Warning $failureText
		if (-not $ContinueAfterFailure) {
			exit 1
		}
		continue
	}

	if (-not $SkipChecks) {
		$checkRun = Invoke-Checks -Paths $paths -RepoRoot $repoRoot -Story $story -RunId $runId -PowerShellCommand $powerShellCommand
		if ([int]$checkRun.ExitCode -ne 0) {
			$failureText = "Checks failed. log: $($checkRun.LogPath)"
			Mark-StoryFailed -Story $story -MaxAttempts $maxAttemptsPerStory -FailureText $failureText -ChecksRun $checkRun.LogPath -Learnings $learningsSummary
			Write-JsonFile -Data $state -Path $paths.Prd
			Append-ProgressEntry -Path $paths.Progress -Story $story -Status "checks_failed" -Note $failureText
			Append-GuardrailEntry -Path $paths.Guardrails -Story $story -FailureText $failureText -RunId $runId
			Write-Warning $failureText
			if (-not $ContinueAfterFailure) {
				exit 1
			}
			continue
		}
		$checksSummary = if ([string]::IsNullOrWhiteSpace($checksSummary)) { $checkRun.LogPath } else { $checksSummary }
	}
	else {
		$checksSummary = if ([string]::IsNullOrWhiteSpace($checksSummary)) { "Skipped outer-loop checks." } else { $checksSummary }
	}

	Mark-StoryDone -Story $story -ChecksRun $checksSummary -Learnings $learningsSummary
	Write-JsonFile -Data $state -Path $paths.Prd
	Append-ProgressEntry -Path $paths.Progress -Story $story -Status "done" -Note $changedSummary

	if (-not $NoCommit) {
		$commitMessage = "{0} {1} {2}" -f $commitPrefix, $story.id, $story.title
		$didCommit = Commit-IfNeeded -RepoRoot $repoRoot -Message $commitMessage.Trim()
		if ($didCommit) {
			Write-Host "Committed: $commitMessage"
		}
		else {
			Write-Host "Story completed with no staged diff to commit."
		}
	}

	if (-not [string]::IsNullOrWhiteSpace($StoryId)) {
		Write-Host "Requested story completed."
		exit 0
	}
}

Write-Host "Reached max iterations: $MaxIterations"
