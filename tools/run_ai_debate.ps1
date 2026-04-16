[CmdletBinding(DefaultParameterSetName = "InlineQuestion")]
param(
	[Parameter(Mandatory = $true, Position = 0, ParameterSetName = "InlineQuestion")]
	[string]$Question,

	[Parameter(Mandatory = $true, ParameterSetName = "QuestionFile")]
	[string]$QuestionFile,

	[ValidateRange(1, 8)]
	[int]$Rounds = 3,

	[string]$WorkingDirectory = ".",
	[string]$OutputRoot = ".tmp\ai-debate",
	[string]$CodexModel = "gpt-5.4",
	[string]$CodexSandboxMode = "",
	[string]$ClaudeModel = "sonnet",

	[ValidateRange(30, 3600)]
	[int]$CodexTimeoutSeconds = 600,

	[ValidateRange(30, 3600)]
	[int]$ClaudeTimeoutSeconds = 600,

	[ValidateSet("auto", "workspace", "context")]
	[string]$ToolScope = "auto",

	[string[]]$ClaudeTools = @("Read", "Glob", "Grep"),
	[string]$ClaudeGitBashPath = "",
	[switch]$ClaudeBareMode,

	[ValidateSet("codex", "claude")]
	[string]$FinalSynthesizer = "codex",

	[string[]]$ContextPath = @()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

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

function Read-TextFile {
	param([Parameter(Mandatory = $true)][string]$Path)

	return (Get-Content -LiteralPath $Path -Raw).Trim()
}

function Write-TextFile {
	param(
		[Parameter(Mandatory = $true)][string]$Path,
		[Parameter(Mandatory = $true)][string]$Content
	)

	$parent = Split-Path -Parent $Path
	if (-not [string]::IsNullOrWhiteSpace($parent)) {
		New-Item -ItemType Directory -Path $parent -Force | Out-Null
	}

	Set-Content -LiteralPath $Path -Value $Content -Encoding utf8
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

function Get-PowerShellExecutablePath {
	$currentProcess = Get-Process -Id $PID -ErrorAction Stop
	if (-not [string]::IsNullOrWhiteSpace([string]$currentProcess.Path)) {
		return [string]$currentProcess.Path
	}

	foreach ($candidate in @(
		(Join-Path $PSHOME "pwsh.exe"),
		(Join-Path $PSHOME "powershell.exe")
	)) {
		if (Test-Path -LiteralPath $candidate) {
			return $candidate
		}
	}

	throw "Unable to resolve the current PowerShell executable path."
}

function Expand-Template {
	param(
		[Parameter(Mandatory = $true)][string]$Template,
		[Parameter(Mandatory = $true)][hashtable]$Values
	)

	$result = $Template
	foreach ($key in $Values.Keys) {
		$token = "{{{{{0}}}}}" -f $key
		$result = $result.Replace($token, [string]$Values[$key])
	}
	return $result
}

function Get-SectionText {
	param(
		[string]$Content,
		[string]$Fallback
	)

	if ([string]::IsNullOrWhiteSpace($Content)) {
		return $Fallback
	}

	return $Content.Trim()
}

function Get-ToolScopePaths {
	param(
		[Parameter(Mandatory = $true)][string]$WorkspaceRoot,
		[string[]]$AdditionalContextPaths = @(),
		[Parameter(Mandatory = $true)][string]$ScopeMode
	)

	$normalizedContextPaths = @()
	foreach ($path in $AdditionalContextPaths) {
		if (-not [string]::IsNullOrWhiteSpace($path)) {
			$normalizedContextPaths += $path
		}
	}
	$normalizedContextPaths = @($normalizedContextPaths | Select-Object -Unique)

	switch ($ScopeMode) {
		"context" {
			if ($normalizedContextPaths.Count -eq 0) {
				throw "ToolScope 'context' requires at least one ContextPath."
			}
			return $normalizedContextPaths
		}
		"workspace" {
			return @(@($WorkspaceRoot) + $normalizedContextPaths | Select-Object -Unique)
		}
		default {
			if ($normalizedContextPaths.Count -gt 0) {
				return $normalizedContextPaths
			}
			return @($WorkspaceRoot)
		}
	}
}

function Get-ToolAccessPaths {
	param(
		[Parameter(Mandatory = $true)][string]$WorkspaceRoot,
		[Parameter(Mandatory = $true)][string[]]$ToolScopePaths
	)

	$accessPaths = New-Object System.Collections.Generic.List[string]
	$seen = @{}

	foreach ($path in $ToolScopePaths) {
		if ([string]::IsNullOrWhiteSpace($path)) {
			continue
		}

		$accessPath = $path
		if (Test-Path -LiteralPath $path -PathType Leaf) {
			$accessPath = Split-Path -Parent $path
		}

		if ([string]::IsNullOrWhiteSpace($accessPath)) {
			$accessPath = $WorkspaceRoot
		}

		$accessPath = Resolve-AbsolutePath -Path $accessPath
		if ($seen.ContainsKey($accessPath)) {
			continue
		}

		$seen[$accessPath] = $true
		$accessPaths.Add($accessPath)
	}

	if ($accessPaths.Count -eq 0) {
		$accessPaths.Add($WorkspaceRoot)
	}

	return @($accessPaths)
}

function New-ScopedWorkingDirectory {
	param(
		[Parameter(Mandatory = $true)][string]$OutputDirectory,
		[Parameter(Mandatory = $true)][string[]]$ToolScopePaths
	)

	$scopeRoot = Join-Path $OutputDirectory "_tool-scope"
	New-Item -ItemType Directory -Path $scopeRoot -Force | Out-Null

	$readmeLines = New-Object System.Collections.Generic.List[string]
	$readmeLines.Add("Restricted tool workspace for AI debate subprocesses.")
	$readmeLines.Add("")
	$readmeLines.Add("Allowed repo paths:")
	foreach ($path in $ToolScopePaths) {
		$readmeLines.Add("- $path")
	}

	Write-TextFile -Path (Join-Path $scopeRoot "README.md") -Content ($readmeLines -join [Environment]::NewLine)
	return $scopeRoot
}

function Test-PathWithinRoot {
	param(
		[Parameter(Mandatory = $true)][string]$Path,
		[Parameter(Mandatory = $true)][string]$Root
	)

	$resolvedRoot = Resolve-AbsolutePath -Path $Root
	$candidatePath = Resolve-AbsolutePath -Path $Path
	if (Test-Path -LiteralPath $candidatePath -PathType Leaf) {
		$candidatePath = Split-Path -Parent $candidatePath
	}
	if ([string]::IsNullOrWhiteSpace($candidatePath)) {
		$candidatePath = $resolvedRoot
	}

	if ($candidatePath.Equals($resolvedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
		return $true
	}

	$rootPrefix = $resolvedRoot.TrimEnd([char[]]@('\', '/')) + [System.IO.Path]::DirectorySeparatorChar
	return $candidatePath.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)
}

function Get-ToolWorkingDirectory {
	param(
		[Parameter(Mandatory = $true)][string]$WorkspaceRoot,
		[Parameter(Mandatory = $true)][string[]]$ToolScopePaths,
		[Parameter(Mandatory = $true)][string]$OutputDirectory
	)

	$allPathsWithinWorkspace = $true
	foreach ($path in $ToolScopePaths) {
		if (-not (Test-PathWithinRoot -Path $path -Root $WorkspaceRoot)) {
			$allPathsWithinWorkspace = $false
			break
		}
	}

	if ($allPathsWithinWorkspace) {
		return $WorkspaceRoot
	}

	return (New-ScopedWorkingDirectory -OutputDirectory $OutputDirectory -ToolScopePaths $ToolScopePaths)
}

function Resolve-ClaudeBashPath {
	param([string]$PreferredPath = "")

	$candidates = New-Object System.Collections.Generic.List[string]
	if (-not [string]::IsNullOrWhiteSpace($PreferredPath)) {
		$candidates.Add($PreferredPath)
	}
	if (-not [string]::IsNullOrWhiteSpace($env:CLAUDE_CODE_GIT_BASH_PATH)) {
		$candidates.Add($env:CLAUDE_CODE_GIT_BASH_PATH)
	}

	foreach ($candidate in @(
		"C:\Program Files\Git\bin\bash.exe",
		"C:\Program Files\Git\usr\bin\bash.exe",
		"D:\Git\bin\bash.exe",
		"D:\Git\usr\bin\bash.exe",
		"E:\Git\bin\bash.exe",
		"E:\Git\usr\bin\bash.exe"
	)) {
		$candidates.Add($candidate)
	}

	$bashCommand = Get-Command bash -ErrorAction SilentlyContinue
	if ($bashCommand -ne $null) {
		foreach ($candidate in @($bashCommand.Source, $bashCommand.Path, $bashCommand.Definition)) {
			if (-not [string]::IsNullOrWhiteSpace([string]$candidate)) {
				$candidates.Add([string]$candidate)
			}
		}
	}

	foreach ($candidate in $candidates) {
		if ([string]::IsNullOrWhiteSpace($candidate)) {
			continue
		}
		$resolved = Resolve-AbsolutePath -Path $candidate -AllowMissing
		if (Test-Path -LiteralPath $resolved -PathType Leaf) {
			return $resolved
		}
	}

	return ""
}

function Initialize-ClaudeEnvironment {
	param([string]$PreferredBashPath = "")

	$bashPath = Resolve-ClaudeBashPath -PreferredPath $PreferredBashPath
	if ([string]::IsNullOrWhiteSpace($bashPath)) {
		throw "Claude requires CLAUDE_CODE_GIT_BASH_PATH, but no usable bash executable was found."
	}

	$env:CLAUDE_CODE_GIT_BASH_PATH = $bashPath
	return $bashPath
}

function Get-ClaudeAuthStatus {
	param(
		[Parameter(Mandatory = $true)][string]$ClaudeCommandPath,
		[Parameter(Mandatory = $true)][string]$WorkingDirectory
	)

	$output = (& $ClaudeCommandPath auth status 2>$null | Out-String).Trim()
	if ([string]::IsNullOrWhiteSpace($output)) {
		throw "Claude auth status returned no output."
	}

	try {
		return ($output | ConvertFrom-Json -ErrorAction Stop)
	}
	catch {
		throw "Claude auth status returned invalid JSON."
	}
}

function Quote-ProcessArgument {
	param([AllowNull()][string]$Value)

	if ($null -eq $Value -or $Value.Length -eq 0) {
		return '""'
	}

	if ($Value -notmatch '[\s"]') {
		return $Value
	}

	$escaped = $Value -replace '(\\*)"', '$1$1\"'
	$escaped = $escaped -replace '(\\+)$', '$1$1'
	return '"' + $escaped + '"'
}

function Format-CommandLine {
	param(
		[Parameter(Mandatory = $true)][string]$FilePath,
		[Parameter(Mandatory = $true)][string[]]$Arguments
	)

	$parts = New-Object System.Collections.Generic.List[string]
	$parts.Add((Quote-ProcessArgument -Value $FilePath))
	foreach ($argument in $Arguments) {
		$parts.Add((Quote-ProcessArgument -Value $argument))
	}

	return ($parts -join " ")
}

function Read-SharedTextFile {
	param([Parameter(Mandatory = $true)][string]$Path)

	if (-not (Test-Path -LiteralPath $Path)) {
		return ""
	}

	$fileStream = $null
	$reader = $null
	try {
		$fileStream = [System.IO.File]::Open($Path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
		$reader = New-Object System.IO.StreamReader($fileStream, $true)
		return $reader.ReadToEnd()
	}
	catch {
		return ""
	}
	finally {
		if ($reader -ne $null) {
			$reader.Dispose()
		}
		elseif ($fileStream -ne $null) {
			$fileStream.Dispose()
		}
	}
}

function Write-StreamChunk {
	param(
		[Parameter(Mandatory = $true)][string]$Chunk,
		[Parameter(Mandatory = $true)][string]$Prefix,
		[Parameter(Mandatory = $true)][string]$LogPath,
		[Parameter(Mandatory = $true)][ref]$PendingFragment
	)

	if ($Chunk.Length -eq 0) {
		return
	}

	$buffer = $PendingFragment.Value + $Chunk
	if ($buffer.Length -eq 0) {
		return
	}

	$endsWithNewline = $buffer.EndsWith("`n") -or $buffer.EndsWith("`r")
	$parts = $buffer -split "`r?`n", -1
	$completeLineCount = $parts.Length
	if (-not $endsWithNewline) {
		$completeLineCount -= 1
	}

	for ($index = 0; $index -lt $completeLineCount; $index++) {
		$line = $parts[$index]
		if ($line.Length -eq 0) {
			continue
		}

		$prefixedLine = ("[{0}] {1}" -f $Prefix, $line)
		Add-Content -LiteralPath $LogPath -Value $prefixedLine -Encoding utf8
		Write-Host $prefixedLine
	}

	if ($endsWithNewline) {
		$PendingFragment.Value = ""
	}
	else {
		$PendingFragment.Value = $parts[-1]
	}
}

function Flush-RedirectedStream {
	param(
		[Parameter(Mandatory = $true)][string]$SourcePath,
		[Parameter(Mandatory = $true)][string]$Prefix,
		[Parameter(Mandatory = $true)][string]$LogPath,
		[Parameter(Mandatory = $true)][ref]$LastText,
		[Parameter(Mandatory = $true)][ref]$PendingFragment
	)

	if (-not (Test-Path -LiteralPath $SourcePath)) {
		return
	}

	$currentText = Read-SharedTextFile -Path $SourcePath
	if ($null -eq $currentText) {
		$currentText = ""
	}

	$chunk = ""
	if ($currentText.Length -lt $LastText.Value.Length) {
		$PendingFragment.Value = ""
		$chunk = $currentText
	}
	elseif ($currentText.Length -gt $LastText.Value.Length) {
		$chunk = $currentText.Substring($LastText.Value.Length)
	}

	$LastText.Value = $currentText
	if ($chunk.Length -gt 0) {
		Write-StreamChunk -Chunk $chunk -Prefix $Prefix -LogPath $LogPath -PendingFragment $PendingFragment
	}
}

function Flush-RedirectedStreamTail {
	param(
		[Parameter(Mandatory = $true)][string]$Prefix,
		[Parameter(Mandatory = $true)][string]$LogPath,
		[Parameter(Mandatory = $true)][ref]$PendingFragment
	)

	if ([string]::IsNullOrEmpty([string]$PendingFragment.Value)) {
		return
	}

	$prefixedLine = ("[{0}] {1}" -f $Prefix, $PendingFragment.Value)
	Add-Content -LiteralPath $LogPath -Value $prefixedLine -Encoding utf8
	Write-Host $prefixedLine
	$PendingFragment.Value = ""
}

function Get-RunnerScriptPath {
	param([Parameter(Mandatory = $true)][string]$OutputDirectory)

	$runnerPath = Join-Path $OutputDirectory "_invoke_ai_cli.ps1"
	if (-not (Test-Path -LiteralPath $runnerPath)) {
		$runnerScript = @'
[CmdletBinding()]
param(
	[Parameter(Mandatory = $true)][string]$CommandPath,
	[Parameter(Mandatory = $true)][string]$PromptPath,
	[Parameter(Mandatory = $true)][string]$ArgumentsPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$commandArgs = @()
if (Test-Path -LiteralPath $ArgumentsPath) {
	$payload = Get-Content -LiteralPath $ArgumentsPath -Raw | ConvertFrom-Json
	if ($null -ne $payload -and $null -ne $payload.arguments) {
		foreach ($argument in $payload.arguments) {
			$commandArgs += [string]$argument
		}
	}
}

$promptText = Get-Content -LiteralPath $PromptPath -Raw
$promptText | & $CommandPath @commandArgs
exit $LASTEXITCODE
'@
		Write-TextFile -Path $runnerPath -Content $runnerScript
	}

	return $runnerPath
}

function Invoke-LoggedProcess {
	param(
		[Parameter(Mandatory = $true)][string]$FilePath,
		[Parameter(Mandatory = $true)][string[]]$Arguments,
		[Parameter(Mandatory = $true)][string]$WorkingDirectory,
		[Parameter(Mandatory = $true)][string]$LogPath,
		[Parameter(Mandatory = $true)][int]$TimeoutSeconds,
		[string[]]$HeaderLines = @()
	)

	$logLines = New-Object System.Collections.Generic.List[string]
	foreach ($line in $HeaderLines) {
		$logLines.Add($line)
	}
	$logLines.Add(("started_at: {0}" -f (Get-Date).ToString("o")))
	$logLines.Add(("working_directory: {0}" -f $WorkingDirectory))
	$logLines.Add(("timeout_seconds: {0}" -f $TimeoutSeconds))
	$logLines.Add(("command: {0}" -f (Format-CommandLine -FilePath $FilePath -Arguments $Arguments)))
	$logLines.Add("")
	Write-TextFile -Path $LogPath -Content ($logLines -join [Environment]::NewLine)

	$stdoutPath = Join-Path ([System.IO.Path]::GetTempPath()) ([System.Guid]::NewGuid().ToString() + ".stdout.log")
	$stderrPath = Join-Path ([System.IO.Path]::GetTempPath()) ([System.Guid]::NewGuid().ToString() + ".stderr.log")
	$process = $null
	$timedOut = $false
	$exitCode = -1
	$stdoutText = ""
	$stderrText = ""
	$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
	$stdoutObservedText = ""
	$stderrObservedText = ""
	$stdoutPendingFragment = ""
	$stderrPendingFragment = ""

	try {
		$process = Start-Process -FilePath $FilePath -ArgumentList $Arguments -WorkingDirectory $WorkingDirectory -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath -NoNewWindow -PassThru
		if ($process -eq $null) {
			throw "Failed to start process: $FilePath"
		}

		while (-not $process.WaitForExit(250)) {
			Flush-RedirectedStream -SourcePath $stdoutPath -Prefix "stdout" -LogPath $LogPath -LastText ([ref]$stdoutObservedText) -PendingFragment ([ref]$stdoutPendingFragment)
			Flush-RedirectedStream -SourcePath $stderrPath -Prefix "stderr" -LogPath $LogPath -LastText ([ref]$stderrObservedText) -PendingFragment ([ref]$stderrPendingFragment)
			if ($stopwatch.Elapsed.TotalSeconds -ge $TimeoutSeconds) {
				$timedOut = $true
				Add-Content -LiteralPath $LogPath -Value ("[system] Process timed out after {0} seconds. Killing process tree..." -f $TimeoutSeconds) -Encoding utf8
				try {
					$process.Kill($true)
				}
				catch {
					try {
						$process.Kill()
					}
					catch {
					}
				}
				break
			}
		}

		$process.WaitForExit()
		Flush-RedirectedStream -SourcePath $stdoutPath -Prefix "stdout" -LogPath $LogPath -LastText ([ref]$stdoutObservedText) -PendingFragment ([ref]$stdoutPendingFragment)
		Flush-RedirectedStream -SourcePath $stderrPath -Prefix "stderr" -LogPath $LogPath -LastText ([ref]$stderrObservedText) -PendingFragment ([ref]$stderrPendingFragment)
		Flush-RedirectedStreamTail -Prefix "stdout" -LogPath $LogPath -PendingFragment ([ref]$stdoutPendingFragment)
		Flush-RedirectedStreamTail -Prefix "stderr" -LogPath $LogPath -PendingFragment ([ref]$stderrPendingFragment)
		$exitCode = $process.ExitCode
	}
	catch {
		Add-Content -LiteralPath $LogPath -Value ("[system] Process launch failed: {0}" -f $_.Exception.Message) -Encoding utf8
		throw
	}
	finally {
		$stopwatch.Stop()
		if (Test-Path -LiteralPath $stdoutPath) {
			$stdoutText = Read-SharedTextFile -Path $stdoutPath
			Remove-Item -LiteralPath $stdoutPath -Force -ErrorAction SilentlyContinue
		}
		if (Test-Path -LiteralPath $stderrPath) {
			$stderrText = Read-SharedTextFile -Path $stderrPath
			Remove-Item -LiteralPath $stderrPath -Force -ErrorAction SilentlyContinue
		}

		if ($process -ne $null) {
			Add-Content -LiteralPath $LogPath -Value ("[system] Exit code: {0}" -f $exitCode) -Encoding utf8
			$process.Dispose()
		}
		else {
			Add-Content -LiteralPath $LogPath -Value "[system] Exit code: (process failed to start)" -Encoding utf8
		}

		Add-Content -LiteralPath $LogPath -Value ("[system] Duration ms: {0}" -f $stopwatch.ElapsedMilliseconds) -Encoding utf8
	}

	if ($timedOut) {
		throw "Command timed out after $TimeoutSeconds seconds. See $LogPath"
	}

	return [pscustomobject]@{
		ExitCode = $exitCode
		StdOut = $stdoutText
		StdErr = $stderrText
		DurationMs = $stopwatch.ElapsedMilliseconds
	}
}

function Invoke-ToolCommand {
	param(
		[Parameter(Mandatory = $true)][string]$CommandPath,
		[Parameter(Mandatory = $true)][string[]]$Arguments,
		[Parameter(Mandatory = $true)][string]$PromptPath,
		[Parameter(Mandatory = $true)][string]$OutputDirectory,
		[Parameter(Mandatory = $true)][string]$ToolWorkingDirectory,
		[Parameter(Mandatory = $true)][string]$LogPath,
		[Parameter(Mandatory = $true)][int]$TimeoutSeconds,
		[string[]]$HeaderLines = @()
	)

	$runnerPath = Get-RunnerScriptPath -OutputDirectory $OutputDirectory
	$argumentsPath = Join-Path $OutputDirectory ("{0}.args.json" -f [System.IO.Path]::GetFileNameWithoutExtension($LogPath))
	Write-TextFile -Path $argumentsPath -Content (([ordered]@{ arguments = @($Arguments) } | ConvertTo-Json -Depth 5))

	$runnerArguments = @(
		"-NoProfile",
		"-ExecutionPolicy", "Bypass",
		"-File", $runnerPath,
		"-CommandPath", $CommandPath,
		"-PromptPath", $PromptPath,
		"-ArgumentsPath", $argumentsPath
	)

	return (Invoke-LoggedProcess -FilePath $script:PowerShellCommandPath -Arguments $runnerArguments -WorkingDirectory $ToolWorkingDirectory -LogPath $LogPath -TimeoutSeconds $TimeoutSeconds -HeaderLines $HeaderLines)
}

function Invoke-CodexRound {
	param(
		[Parameter(Mandatory = $true)][string]$PromptText,
		[Parameter(Mandatory = $true)][string]$RoundLabel,
		[Parameter(Mandatory = $true)][string]$OutputDirectory,
		[Parameter(Mandatory = $true)][string]$Model,
		[Parameter(Mandatory = $true)][string]$ToolWorkingDirectory,
		[Parameter(Mandatory = $true)][string[]]$ToolAccessPaths,
		[Parameter(Mandatory = $true)][int]$TimeoutSeconds
	)

	$promptPath = Join-Path $OutputDirectory ("{0}-codex-prompt.md" -f $RoundLabel)
	$messagePath = Join-Path $OutputDirectory ("{0}-codex.txt" -f $RoundLabel)
	$logPath = Join-Path $OutputDirectory ("{0}-codex-cli.log" -f $RoundLabel)
	Write-TextFile -Path $promptPath -Content $PromptText

	$args = @(
		"-a", "never",
		"exec",
		"--ephemeral",
		"--color", "never",
		"--skip-git-repo-check",
		"-C", $ToolWorkingDirectory,
		"-m", $Model,
		"-o", $messagePath
	)

	if (-not [string]::IsNullOrWhiteSpace($script:CodexSandboxMode)) {
		$args += "-s"
		$args += $script:CodexSandboxMode
	}

	foreach ($path in $ToolAccessPaths) {
		if ($path -ne $ToolWorkingDirectory) {
			$args += "--add-dir"
			$args += $path
		}
	}

	$args += "-"

	$headerLines = @(
		"provider: codex",
		("model: {0}" -f $Model),
		("sandbox: {0}" -f $script:CodexSandboxModeLabel),
		("tool_working_directory: {0}" -f $ToolWorkingDirectory),
		"tool_access_paths:"
	) + ($ToolAccessPaths | ForEach-Object { "- $_" })

	$result = Invoke-ToolCommand -CommandPath $script:CodexCommandPath -Arguments $args -PromptPath $promptPath -OutputDirectory $OutputDirectory -ToolWorkingDirectory $ToolWorkingDirectory -LogPath $logPath -TimeoutSeconds $TimeoutSeconds -HeaderLines $headerLines

	if ($result.ExitCode -ne 0) {
		throw "Codex failed during $RoundLabel. See $logPath"
	}

	if (-not (Test-Path -LiteralPath $messagePath)) {
		throw "Codex completed without writing the final message during $RoundLabel. See $logPath"
	}

	return (Read-TextFile -Path $messagePath)
}

function Invoke-ClaudeRound {
	param(
		[Parameter(Mandatory = $true)][string]$PromptText,
		[Parameter(Mandatory = $true)][string]$RoundLabel,
		[Parameter(Mandatory = $true)][string]$OutputDirectory,
		[Parameter(Mandatory = $true)][string]$Model,
		[Parameter(Mandatory = $true)][string]$ToolWorkingDirectory,
		[Parameter(Mandatory = $true)][string[]]$ToolAccessPaths,
		[Parameter(Mandatory = $true)][int]$TimeoutSeconds
	)

	$promptPath = Join-Path $OutputDirectory ("{0}-claude-prompt.md" -f $RoundLabel)
	$jsonPath = Join-Path $OutputDirectory ("{0}-claude.json" -f $RoundLabel)
	$messagePath = Join-Path $OutputDirectory ("{0}-claude.txt" -f $RoundLabel)
	$logPath = Join-Path $OutputDirectory ("{0}-claude-cli.log" -f $RoundLabel)
	Write-TextFile -Path $promptPath -Content $PromptText

	$args = @(
		"-p",
		"--disable-slash-commands",
		"--output-format", "json",
		"--no-session-persistence",
		"--permission-mode", "dontAsk",
		"--tools", ($script:ClaudeAllowedTools -join ","),
		"--model", $Model
	)

	if ($script:ClaudeBareModeEnabled) {
		$args += "--bare"
	}

	foreach ($path in $ToolAccessPaths) {
		$args += "--add-dir"
		$args += $path
	}

	$headerLines = @(
		"provider: claude",
		("model: {0}" -f $Model),
		("allowed_tools: {0}" -f ($script:ClaudeAllowedTools -join ", ")),
		("claude_bare_mode: {0}" -f $script:ClaudeBareModeEnabled),
		("claude_git_bash_path: {0}" -f $script:ClaudeBashPath),
		("tool_working_directory: {0}" -f $ToolWorkingDirectory),
		"tool_access_paths:"
	) + ($ToolAccessPaths | ForEach-Object { "- $_" })

	$result = Invoke-ToolCommand -CommandPath $script:ClaudeCommandPath -Arguments $args -PromptPath $promptPath -OutputDirectory $OutputDirectory -ToolWorkingDirectory $ToolWorkingDirectory -LogPath $logPath -TimeoutSeconds $TimeoutSeconds -HeaderLines $headerLines
	$jsonText = $result.StdOut.Trim()
	Write-TextFile -Path $jsonPath -Content $jsonText

	if ($result.ExitCode -ne 0) {
		throw "Claude failed during $RoundLabel. See $logPath"
	}

	try {
		$payload = $jsonText | ConvertFrom-Json -ErrorAction Stop
	}
	catch {
		throw "Claude returned invalid JSON during $RoundLabel. See $logPath"
	}

	if ($payload.is_error) {
		$errorMessage = [string]$payload.result
		if ([string]::IsNullOrWhiteSpace($errorMessage)) {
			$errorMessage = "Claude returned an error payload."
		}
		throw ("Claude returned an error during {0}: {1}. See {2}" -f $RoundLabel, $errorMessage, $logPath)
	}

	$message = [string]$payload.result
	Write-TextFile -Path $messagePath -Content $message
	return $message.Trim()
}

Ensure-CommandExists -Name "codex"
Ensure-CommandExists -Name "claude"

if ($ClaudeTools.Count -eq 0) {
	throw "ClaudeTools cannot be empty."
}

$script:PowerShellCommandPath = Get-PowerShellExecutablePath
$script:CodexCommandPath = Get-ResolvedCommandPath -Name "codex"
$script:ClaudeCommandPath = Get-ResolvedCommandPath -Name "claude"
$script:CodexSandboxMode = $CodexSandboxMode.Trim()
$script:CodexSandboxModeLabel = if ([string]::IsNullOrWhiteSpace($script:CodexSandboxMode)) { "(config/default)" } else { $script:CodexSandboxMode }
$script:ClaudeAllowedTools = @($ClaudeTools)
$script:ClaudeBareModeEnabled = $ClaudeBareMode.IsPresent
$script:ClaudeBashPath = Initialize-ClaudeEnvironment -PreferredBashPath $ClaudeGitBashPath

$workspaceRoot = Resolve-AbsolutePath -Path $WorkingDirectory
$outputRootPath = Resolve-AbsolutePath -Path $OutputRoot -AllowMissing
$contextPaths = @()
foreach ($path in $ContextPath) {
	$contextPaths += Resolve-AbsolutePath -Path $path
}

$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-AbsolutePath -Path (Join-Path $scriptPath "..\..")
$roundTemplatePath = Join-Path $repoRoot "prompts\ai_debate_round_prompt.md"
$finalTemplatePath = Join-Path $repoRoot "prompts\ai_debate_final_prompt.md"
$roundTemplate = Read-TextFile -Path $roundTemplatePath
$finalTemplate = Read-TextFile -Path $finalTemplatePath

$questionText = $Question
if ($PSCmdlet.ParameterSetName -eq "QuestionFile") {
	$questionText = Read-TextFile -Path (Resolve-AbsolutePath -Path $QuestionFile)
}

if ([string]::IsNullOrWhiteSpace($questionText)) {
	throw "Question cannot be empty."
}

$runDirectory = Join-Path $outputRootPath (Get-Date -Format "yyyyMMdd-HHmmss")
New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null

$toolScopePaths = Get-ToolScopePaths -WorkspaceRoot $workspaceRoot -AdditionalContextPaths $contextPaths -ScopeMode $ToolScope
$toolAccessPaths = Get-ToolAccessPaths -WorkspaceRoot $workspaceRoot -ToolScopePaths $toolScopePaths
$toolWorkingDirectory = Get-ToolWorkingDirectory -WorkspaceRoot $workspaceRoot -ToolScopePaths $toolScopePaths -OutputDirectory $runDirectory
$claudeAuthStatus = Get-ClaudeAuthStatus -ClaudeCommandPath $script:ClaudeCommandPath -WorkingDirectory $workspaceRoot

$metadata = [ordered]@{
	question = $questionText.Trim()
	rounds = $Rounds
	final_synthesizer = $FinalSynthesizer
	working_directory = $workspaceRoot
	context_paths = $contextPaths
	codex_model = $CodexModel
	codex_sandbox_mode = $script:CodexSandboxModeLabel
	claude_model = $ClaudeModel
	codex_timeout_seconds = $CodexTimeoutSeconds
	claude_timeout_seconds = $ClaudeTimeoutSeconds
	tool_scope = $ToolScope
	tool_scope_paths = $toolScopePaths
	tool_access_paths = $toolAccessPaths
	tool_working_directory = $toolWorkingDirectory
	claude_tools = $script:ClaudeAllowedTools
	claude_bare_mode = $script:ClaudeBareModeEnabled
	claude_git_bash_path = $script:ClaudeBashPath
	claude_logged_in = [bool]$claudeAuthStatus.loggedIn
	claude_auth_method = [string]$claudeAuthStatus.authMethod
	started_at = (Get-Date).ToString("o")
}
Write-TextFile -Path (Join-Path $runDirectory "run.json") -Content ($metadata | ConvertTo-Json -Depth 5)
Write-TextFile -Path (Join-Path $runDirectory "question.md") -Content $questionText.Trim()

$contextSummary = if ($contextPaths.Count -gt 0) { $contextPaths -join ", " } else { "(none)" }
$codexPrevious = ""
$claudePrevious = ""
$roundSummaries = New-Object System.Collections.Generic.List[string]

for ($round = 1; $round -le $Rounds; $round++) {
	$roundLabel = "round-{0:D2}" -f $round
	Write-Host ("[{0}/{1}] Codex debating..." -f $round, $Rounds)

	$codexPrompt = Expand-Template -Template $roundTemplate -Values @{
		SELF_NAME = "Codex"
		OPPONENT_NAME = "Claude"
		WORKDIR = $workspaceRoot
		ROUND_INDEX = $round
		ROUND_COUNT = $Rounds
		CONTEXT_PATHS = $contextSummary
		QUESTION = $questionText.Trim()
		SELF_POSITION = (Get-SectionText -Content $codexPrevious -Fallback "No prior position yet.")
		OPPONENT_POSITION = (Get-SectionText -Content $claudePrevious -Fallback "No counterpart position yet.")
	}

	$codexResponse = Invoke-CodexRound -PromptText $codexPrompt -RoundLabel $roundLabel -OutputDirectory $runDirectory -Model $CodexModel -ToolWorkingDirectory $toolWorkingDirectory -ToolAccessPaths $toolAccessPaths -TimeoutSeconds $CodexTimeoutSeconds
	$codexPrevious = $codexResponse

	Write-Host ("[{0}/{1}] Claude debating..." -f $round, $Rounds)

	$claudePrompt = Expand-Template -Template $roundTemplate -Values @{
		SELF_NAME = "Claude"
		OPPONENT_NAME = "Codex"
		WORKDIR = $workspaceRoot
		ROUND_INDEX = $round
		ROUND_COUNT = $Rounds
		CONTEXT_PATHS = $contextSummary
		QUESTION = $questionText.Trim()
		SELF_POSITION = (Get-SectionText -Content $claudePrevious -Fallback "No prior position yet.")
		OPPONENT_POSITION = $codexResponse
	}

	$claudeResponse = Invoke-ClaudeRound -PromptText $claudePrompt -RoundLabel $roundLabel -OutputDirectory $runDirectory -Model $ClaudeModel -ToolWorkingDirectory $toolWorkingDirectory -ToolAccessPaths $toolAccessPaths -TimeoutSeconds $ClaudeTimeoutSeconds
	$claudePrevious = $claudeResponse

	$roundSummaries.Add(@"
## Round $round

### Codex
$codexResponse

### Claude
$claudeResponse
"@.Trim())
}

$transcript = $roundSummaries -join ([Environment]::NewLine + [Environment]::NewLine)
Write-TextFile -Path (Join-Path $runDirectory "transcript.md") -Content $transcript

$finalPrompt = Expand-Template -Template $finalTemplate -Values @{
	SYNTHESIZER_NAME = if ($FinalSynthesizer -eq "codex") { "Codex" } else { "Claude" }
	WORKDIR = $workspaceRoot
	QUESTION = $questionText.Trim()
	TRANSCRIPT = $transcript
}

Write-Host ("[final] {0} synthesizing..." -f ([cultureinfo]::InvariantCulture.TextInfo.ToTitleCase($FinalSynthesizer)))

$finalText = if ($FinalSynthesizer -eq "codex") {
	Invoke-CodexRound -PromptText $finalPrompt -RoundLabel "final" -OutputDirectory $runDirectory -Model $CodexModel -ToolWorkingDirectory $toolWorkingDirectory -ToolAccessPaths $toolAccessPaths -TimeoutSeconds $CodexTimeoutSeconds
}
else {
	Invoke-ClaudeRound -PromptText $finalPrompt -RoundLabel "final" -OutputDirectory $runDirectory -Model $ClaudeModel -ToolWorkingDirectory $toolWorkingDirectory -ToolAccessPaths $toolAccessPaths -TimeoutSeconds $ClaudeTimeoutSeconds
}

$summary = @"
# AI Debate Summary

## Question
$($questionText.Trim())

## Final Answer
$finalText

## Transcript
$transcript
"@.Trim()

Write-TextFile -Path (Join-Path $runDirectory "summary.md") -Content $summary
Write-Host ("Debate complete. Output: {0}" -f $runDirectory)
