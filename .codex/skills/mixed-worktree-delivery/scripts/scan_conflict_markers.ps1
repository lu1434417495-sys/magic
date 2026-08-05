[CmdletBinding()]
param(
	[string]$RepoPath = ".",

	[ValidateSet("Changed", "All")]
	[string]$Scope = "Changed",

	[ValidateSet("Text", "Json")]
	[string]$Format = "Text"
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "_git_readonly.ps1")

try {
	$root = Resolve-RepositoryRoot -RepositoryPath $RepoPath
	$null = Assert-ReadableGitMetadata -RepositoryRoot $root

	if ($Scope -eq "All") {
		$tracked = Invoke-ReadOnlyGit -RepositoryPath $root -Arguments @("-c", "core.quotepath=false", "ls-files", "--cached", "--others", "--exclude-standard")
		[string[]]$candidates = @($tracked.Output | Sort-Object -Unique)
	}
	else {
		$unstaged = Invoke-ReadOnlyGit -RepositoryPath $root -Arguments @("-c", "core.quotepath=false", "diff", "--name-only", "--diff-filter=ACMRTUXB")
		$staged = Invoke-ReadOnlyGit -RepositoryPath $root -Arguments @("-c", "core.quotepath=false", "diff", "--cached", "--name-only", "--diff-filter=ACMRTUXB")
		$untracked = Invoke-ReadOnlyGit -RepositoryPath $root -Arguments @("-c", "core.quotepath=false", "ls-files", "--others", "--exclude-standard")
		[string[]]$candidates = @($unstaged.Output + $staged.Output + $untracked.Output | Sort-Object -Unique)
	}

	$findings = [System.Collections.Generic.List[object]]::new()
	$scannedCount = 0
	$binaryCount = 0
	foreach ($relativePath in $candidates) {
		if ([string]::IsNullOrWhiteSpace($relativePath)) {
			continue
		}

		$fullPath = Resolve-RepositoryFile -RepositoryRoot $root -RelativePath $relativePath
		if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
			continue
		}
		if (Test-ProbablyBinaryFile -Path $fullPath) {
			$binaryCount++
			continue
		}

		$scannedCount++
		$lineNumber = 0
		foreach ($line in [System.IO.File]::ReadLines($fullPath)) {
			$lineNumber++
			if ($line -match "^(<{7}(?:\s.*)?|={7}\s*|>{7}(?:\s.*)?)$") {
				$findings.Add([pscustomobject]@{
					path = ($relativePath -replace "\\", "/")
					line = $lineNumber
					text = $line.Trim()
				})
			}
		}
	}

	$result = [ordered]@{
		repository_root = $root
		scope = $Scope
		candidate_count = $candidates.Count
		scanned_text_file_count = $scannedCount
		skipped_binary_count = $binaryCount
		findings = @($findings)
	}

	if ($Format -eq "Json") {
		$result | ConvertTo-Json -Depth 4
	}
	else {
		Write-Output "Scanned $scannedCount text files ($Scope scope); skipped $binaryCount binary files."
		if ($findings.Count -eq 0) {
			Write-Output "No embedded Git conflict markers found."
		}
		else {
			$findings | ForEach-Object { Write-Output "$($_.path):$($_.line): $($_.text)" }
		}
	}

	if ($findings.Count -gt 0) {
		exit 1
	}
	exit 0
}
catch {
	Write-Error $_
	exit 2
}
