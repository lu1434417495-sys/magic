[CmdletBinding()]
param(
	[string]$RepoPath = ".",

	[string[]]$Extensions = @(".cs", ".gd", ".js", ".mjs", ".ts", ".tsx"),

	[ValidateSet("Text", "Json")]
	[string]$Format = "Text",

	[switch]$FailOnFindings
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "_git_readonly.ps1")

try {
	$root = Resolve-RepositoryRoot -RepositoryPath $RepoPath
	$null = Assert-ReadableGitMetadata -RepositoryRoot $root

	$normalizedExtensions = @($Extensions | ForEach-Object {
		if ($_.StartsWith(".")) { $_.ToLowerInvariant() } else { ".$($_.ToLowerInvariant())" }
	})
	$untrackedResult = Invoke-ReadOnlyGit -RepositoryPath $root -Arguments @("-c", "core.quotepath=false", "ls-files", "--others", "--exclude-standard")
	[string[]]$untrackedSources = @($untrackedResult.Output | Where-Object {
		$normalizedExtensions -contains [System.IO.Path]::GetExtension($_).ToLowerInvariant()
	})

	$trackedResult = Invoke-ReadOnlyGit -RepositoryPath $root -Arguments @("-c", "core.quotepath=false", "ls-files", "--cached")
	$textExtensions = @(
		".cs", ".gd", ".tscn", ".tres", ".godot", ".csproj", ".props", ".targets",
		".json", ".yaml", ".yml", ".xml", ".md", ".txt", ".py", ".ps1",
		".html", ".css", ".js", ".mjs", ".ts", ".tsx"
	)
	$searchableText = [System.Collections.Generic.List[object]]::new()
	[string[]]$searchablePaths = @($trackedResult.Output + $untrackedResult.Output | Sort-Object -Unique)
	foreach ($searchablePath in $searchablePaths) {
		$extension = [System.IO.Path]::GetExtension($searchablePath).ToLowerInvariant()
		if ($textExtensions -notcontains $extension) {
			continue
		}
		$fullPath = Resolve-RepositoryFile -RepositoryRoot $root -RelativePath $searchablePath
		if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
			continue
		}
		$fileInfo = Get-Item -LiteralPath $fullPath
		if ($fileInfo.Length -gt 2MB -or (Test-ProbablyBinaryFile -Path $fullPath)) {
			continue
		}
		$searchableText.Add([pscustomobject]@{
			path = ($searchablePath -replace "\\", "/")
			content = [System.IO.File]::ReadAllText($fullPath)
		})
	}

	$defaultCompileItems = $true
	$projectFiles = Get-ChildItem -LiteralPath $root -Filter "*.csproj" -File
	foreach ($projectFile in $projectFiles) {
		$projectText = [System.IO.File]::ReadAllText($projectFile.FullName)
		if ($projectText -match "(?is)<EnableDefaultCompileItems>\s*false\s*</EnableDefaultCompileItems>") {
			$defaultCompileItems = $false
			break
		}
	}

	$findings = [System.Collections.Generic.List[object]]::new()
	foreach ($sourcePath in $untrackedSources) {
		$relative = $sourcePath -replace "\\", "/"
		$fileName = [System.IO.Path]::GetFileName($relative)
		$symbol = [System.IO.Path]::GetFileNameWithoutExtension($relative)
		$pathReferences = [System.Collections.Generic.List[string]]::new()
		$symbolReferences = [System.Collections.Generic.List[string]]::new()

		foreach ($candidate in $searchableText) {
			if ($candidate.path.Equals($relative, [System.StringComparison]::OrdinalIgnoreCase)) {
				continue
			}
			$content = $candidate.content
			if (
				$content.IndexOf("res://$relative", [System.StringComparison]::OrdinalIgnoreCase) -ge 0 -or
				$content.IndexOf($relative, [System.StringComparison]::OrdinalIgnoreCase) -ge 0 -or
				$content.IndexOf($fileName, [System.StringComparison]::OrdinalIgnoreCase) -ge 0
			) {
				$pathReferences.Add($candidate.path)
				continue
			}
			if ($content -match "(?i)(?<![A-Za-z0-9_])$([regex]::Escape($symbol))(?![A-Za-z0-9_])") {
				$symbolReferences.Add($candidate.path)
			}
		}

		$isImplicitCompileCandidate = (
			[System.IO.Path]::GetExtension($relative).Equals(".cs", [System.StringComparison]::OrdinalIgnoreCase) -and
			$defaultCompileItems
		)
		if ($isImplicitCompileCandidate -or $pathReferences.Count -gt 0 -or $symbolReferences.Count -gt 0) {
			$findings.Add([pscustomobject]@{
				path = $relative
				implicit_csharp_compile_candidate = $isImplicitCompileCandidate
				path_referenced_by = @($pathReferences | Sort-Object -Unique)
				symbol_referenced_by = @($symbolReferences | Sort-Object -Unique)
			})
		}
	}

	$result = [ordered]@{
		repository_root = $root
		untracked_source_count = $untrackedSources.Count
		default_csharp_compile_items = $defaultCompileItems
		finding_count = $findings.Count
		findings = @($findings)
	}

	if ($Format -eq "Json") {
		$result | ConvertTo-Json -Depth 5
	}
	else {
		Write-Output "Untracked source files: $($untrackedSources.Count); dependency findings: $($findings.Count)."
		foreach ($finding in $findings) {
			Write-Output ""
			Write-Output $finding.path
			Write-Output "  implicit C# compile candidate: $($finding.implicit_csharp_compile_candidate)"
			if ($finding.path_referenced_by.Count -gt 0) {
				Write-Output "  path references:"
				$finding.path_referenced_by | ForEach-Object { Write-Output "    $_" }
			}
			if ($finding.symbol_referenced_by.Count -gt 0) {
				Write-Output "  symbol references:"
				$finding.symbol_referenced_by | ForEach-Object { Write-Output "    $_" }
			}
		}
	}

	if ($FailOnFindings -and $findings.Count -gt 0) {
		exit 1
	}
	exit 0
}
catch {
	Write-Error $_
	exit 2
}
