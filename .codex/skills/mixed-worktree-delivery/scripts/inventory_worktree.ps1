[CmdletBinding()]
param(
	[string]$RepoPath = ".",

	[ValidateSet("Text", "Json")]
	[string]$Format = "Text"
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "_git_readonly.ps1")

try {
	$root = Resolve-RepositoryRoot -RepositoryPath $RepoPath
	$indexLockPath = Assert-ReadableGitMetadata -RepositoryRoot $root

	$branchResult = Invoke-ReadOnlyGit -RepositoryPath $root -Arguments @("symbolic-ref", "--short", "-q", "HEAD") -AllowFailure
	$headResult = Invoke-ReadOnlyGit -RepositoryPath $root -Arguments @("rev-parse", "--verify", "HEAD") -AllowFailure
	$upstreamResult = Invoke-ReadOnlyGit -RepositoryPath $root -Arguments @("rev-parse", "--abbrev-ref", "--symbolic-full-name", "@{upstream}") -AllowFailure
	$gitDirectoryResult = Invoke-ReadOnlyGit -RepositoryPath $root -Arguments @("rev-parse", "--absolute-git-dir")
	$statusResult = Invoke-ReadOnlyGit -RepositoryPath $root -Arguments @("-c", "core.quotepath=false", "status", "--short", "--branch", "--untracked-files=all")
	$unstagedResult = Invoke-ReadOnlyGit -RepositoryPath $root -Arguments @("-c", "core.quotepath=false", "diff", "--name-status", "--no-renames")
	$stagedResult = Invoke-ReadOnlyGit -RepositoryPath $root -Arguments @("-c", "core.quotepath=false", "diff", "--cached", "--name-status", "--no-renames")
	$untrackedResult = Invoke-ReadOnlyGit -RepositoryPath $root -Arguments @("-c", "core.quotepath=false", "ls-files", "--others", "--exclude-standard")
	$unmergedResult = Invoke-ReadOnlyGit -RepositoryPath $root -Arguments @("-c", "core.quotepath=false", "diff", "--name-only", "--diff-filter=U")
	$unstagedCheck = Invoke-ReadOnlyGit -RepositoryPath $root -Arguments @("diff", "--check") -AllowFailure
	$stagedCheck = Invoke-ReadOnlyGit -RepositoryPath $root -Arguments @("diff", "--cached", "--check") -AllowFailure

	$generatedPattern = "(?i)(^|/)(\.godot|\.tmp_battle_sim|coverage|TestResults|artifacts?|captures?|reports?)(/|$)|\.(tmp|bak|user)$"
	[string[]]$generatedUntracked = @($untrackedResult.Output | Where-Object { $_ -replace "\\", "/" -match $generatedPattern })
	[string[]]$otherUntracked = @($untrackedResult.Output | Where-Object { $_ -notin $generatedUntracked })

	$inventory = [ordered]@{
		repository_root = $root
		git_directory = $gitDirectoryResult.Output[-1]
		index_lock_path = $indexLockPath
		index_lock_present = $false
		git_metadata_readable = $true
		git_metadata_writable = "unverified"
		branch = if ($branchResult.ExitCode -eq 0 -and $branchResult.Output.Count -gt 0) { $branchResult.Output[-1] } else { "(detached)" }
		head = if ($headResult.ExitCode -eq 0 -and $headResult.Output.Count -gt 0) { $headResult.Output[-1] } else { $null }
		upstream = if ($upstreamResult.ExitCode -eq 0 -and $upstreamResult.Output.Count -gt 0) { $upstreamResult.Output[-1] } else { $null }
		status = @($statusResult.Output)
		unstaged = @($unstagedResult.Output)
		staged = @($stagedResult.Output)
		untracked = @($untrackedResult.Output)
		generated_untracked = $generatedUntracked
		other_untracked = $otherUntracked
		unmerged = @($unmergedResult.Output)
		git_warnings = @(@(
			$statusResult.ErrorOutput +
			$unstagedResult.ErrorOutput +
			$stagedResult.ErrorOutput +
			$untrackedResult.ErrorOutput +
			$unmergedResult.ErrorOutput +
			$unstagedCheck.ErrorOutput +
			$stagedCheck.ErrorOutput
		) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique)
		unstaged_diff_check = [ordered]@{ exit_code = $unstagedCheck.ExitCode; output = @($unstagedCheck.Output) }
		staged_diff_check = [ordered]@{ exit_code = $stagedCheck.ExitCode; output = @($stagedCheck.Output) }
	}

	if ($Format -eq "Json") {
		$inventory | ConvertTo-Json -Depth 5
		exit 0
	}

	Write-Output "Repository: $($inventory.repository_root)"
	Write-Output "Git directory: $($inventory.git_directory)"
	Write-Output "Branch: $($inventory.branch)"
	Write-Output "HEAD: $($inventory.head)"
	Write-Output "Upstream: $($inventory.upstream)"
	Write-Output "Index lock: absent ($($inventory.index_lock_path))"
	Write-Output "Git metadata: readable; writability unverified"
	foreach ($section in @(
		@("Status", $inventory.status),
		@("Unstaged name-status", $inventory.unstaged),
		@("Staged name-status", $inventory.staged),
		@("Untracked", $inventory.untracked),
		@("Generated-looking untracked", $inventory.generated_untracked),
		@("Unmerged", $inventory.unmerged),
		@("Git warnings", $inventory.git_warnings)
	)) {
		Write-Output ""
		Write-Output "$($section[0]):"
		if ($section[1].Count -eq 0) {
			Write-Output "  (none)"
		}
		else {
			$section[1] | ForEach-Object { Write-Output "  $_" }
		}
	}

	Write-Output ""
	Write-Output "Diff check: unstaged=$($unstagedCheck.ExitCode), staged=$($stagedCheck.ExitCode)"
	$unstagedCheck.Output | ForEach-Object { Write-Output "  unstaged: $_" }
	$stagedCheck.Output | ForEach-Object { Write-Output "  staged: $_" }
}
catch {
	Write-Error $_
	exit 2
}
