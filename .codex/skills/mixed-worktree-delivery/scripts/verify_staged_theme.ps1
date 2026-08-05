[CmdletBinding()]
param(
	[string]$RepoPath = ".",

	[Parameter(Mandatory = $true)]
	[ValidateNotNullOrEmpty()]
	[string]$Theme,

	[string[]]$AllowedPath = @(),

	[string[]]$ForbiddenPath = @(".godot/**", ".tmp_battle_sim/**"),

	[switch]$FailOnUntrackedSources,

	[switch]$FailOnOverlap,

	[ValidateSet("Text", "Json")]
	[string]$Format = "Text"
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "_git_readonly.ps1")

function Test-MatchesAnyPattern {
	param(
		[string]$Path,
		[string[]]$Patterns
	)
	foreach ($pattern in $Patterns) {
		if ($Path -like $pattern) {
			return $true
		}
	}
	return $false
}

try {
	$root = Resolve-RepositoryRoot -RepositoryPath $RepoPath
	$null = Assert-ReadableGitMetadata -RepositoryRoot $root

	$stagedResult = Invoke-ReadOnlyGit -RepositoryPath $root -Arguments @("-c", "core.quotepath=false", "diff", "--cached", "--name-only", "--diff-filter=ACMRTUXB")
	[string[]]$stagedPaths = @($stagedResult.Output | Sort-Object -Unique)
	$stagedStatus = Invoke-ReadOnlyGit -RepositoryPath $root -Arguments @("-c", "core.quotepath=false", "diff", "--cached", "--name-status", "--no-renames")
	$unstagedResult = Invoke-ReadOnlyGit -RepositoryPath $root -Arguments @("-c", "core.quotepath=false", "diff", "--name-only", "--diff-filter=ACMRTUXB")
	[string[]]$unstagedPaths = @($unstagedResult.Output | Sort-Object -Unique)
	$unmergedResult = Invoke-ReadOnlyGit -RepositoryPath $root -Arguments @("-c", "core.quotepath=false", "diff", "--name-only", "--diff-filter=U")
	$diffCheck = Invoke-ReadOnlyGit -RepositoryPath $root -Arguments @("diff", "--cached", "--check") -AllowFailure
	$untrackedResult = Invoke-ReadOnlyGit -RepositoryPath $root -Arguments @("-c", "core.quotepath=false", "ls-files", "--others", "--exclude-standard")
	[string[]]$untrackedSources = @($untrackedResult.Output | Where-Object {
		[System.IO.Path]::GetExtension($_).ToLowerInvariant() -in @(".cs", ".gd", ".js", ".mjs", ".ts", ".tsx")
	})

	$violations = [System.Collections.Generic.List[string]]::new()
	if ($stagedPaths.Count -eq 0) {
		$violations.Add("No staged files exist for theme '$Theme'.")
	}
	foreach ($path in $stagedPaths) {
		$normalized = $path -replace "\\", "/"
		if ($AllowedPath.Count -gt 0 -and -not (Test-MatchesAnyPattern -Path $normalized -Patterns $AllowedPath)) {
			$violations.Add("Staged path is outside the allowed theme paths: $normalized")
		}
		if (Test-MatchesAnyPattern -Path $normalized -Patterns $ForbiddenPath) {
			$violations.Add("Staged path matches a forbidden path pattern: $normalized")
		}
	}
	foreach ($path in $unmergedResult.Output) {
		$violations.Add("Unmerged path remains: $path")
	}
	if ($diffCheck.ExitCode -ne 0) {
		$violations.Add("git diff --cached --check failed with exit code $($diffCheck.ExitCode).")
		$diffCheck.Output | ForEach-Object { $violations.Add("staged diff check: $_") }
	}

	$stagedMarkerFindings = [System.Collections.Generic.List[object]]::new()
	foreach ($path in $stagedPaths) {
		$blobResult = Invoke-ReadOnlyGit -RepositoryPath $root -Arguments @("show", ":$path") -AllowFailure
		if ($blobResult.ExitCode -ne 0) {
			continue
		}
		for ($index = 0; $index -lt $blobResult.Output.Count; $index++) {
			$line = $blobResult.Output[$index]
			if ($line -match "^(<{7}(?:\s.*)?|={7}\s*|>{7}(?:\s.*)?)$") {
				$finding = [pscustomobject]@{ path = $path; line = $index + 1; text = $line.Trim() }
				$stagedMarkerFindings.Add($finding)
				$violations.Add("Staged conflict marker: $path`:$($index + 1) $($line.Trim())")
			}
		}
	}

	if ($FailOnUntrackedSources) {
		foreach ($source in $untrackedSources) {
			$violations.Add("Untracked source requires ownership review: $source")
		}
	}

	[string[]]$overlap = @($stagedPaths | Where-Object { $_ -in $unstagedPaths })
	if ($FailOnOverlap) {
		foreach ($path in $overlap) {
			$violations.Add("Staged path also has unstaged changes: $path")
		}
	}
	$result = [ordered]@{
		repository_root = $root
		theme = $Theme
		allowed_path_patterns = @($AllowedPath)
		forbidden_path_patterns = @($ForbiddenPath)
		staged_status = @($stagedStatus.Output)
		staged_paths = $stagedPaths
		staged_unstaged_overlap = $overlap
		fail_on_overlap = [bool]$FailOnOverlap
		untracked_sources = $untrackedSources
		staged_conflict_markers = @($stagedMarkerFindings)
		violations = @($violations)
		valid = $violations.Count -eq 0
	}

	if ($Format -eq "Json") {
		$result | ConvertTo-Json -Depth 5
	}
	else {
		Write-Output "Theme: $Theme"
		Write-Output "Staged files: $($stagedPaths.Count); violations: $($violations.Count)."
		$stagedStatus.Output | ForEach-Object { Write-Output "  staged: $_" }
		$overlap | ForEach-Object { Write-Output "  staged/unstaged overlap: $_" }
		$untrackedSources | ForEach-Object { Write-Output "  untracked source: $_" }
		$violations | ForEach-Object { Write-Output "  violation: $_" }
	}

	if ($violations.Count -gt 0) {
		exit 1
	}
	exit 0
}
catch {
	Write-Error $_
	exit 2
}
