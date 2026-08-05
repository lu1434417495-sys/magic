[CmdletBinding()]
param(
	[Parameter(Mandatory = $true, Position = 0)]
	[string[]]$Artifact,

	[string]$RepositoryRoot,

	[ValidateSet("text", "json")]
	[string]$Format = "text",

	[ValidateRange(1, 2000)]
	[int]$MaxClaimCandidates = 200,

	[ValidateRange(1, 2147483647)]
	[int]$LineStart = 1,

	[ValidateRange(0, 2147483647)]
	[int]$LineEnd = 0
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)

if ($LineEnd -ne 0 -and $LineEnd -lt $LineStart) {
	throw "LineEnd must be zero or greater than or equal to LineStart."
}

function Get-FullRepositoryPath {
	param(
		[string]$Root,
		[string]$Path
	)

	$candidate = if ([IO.Path]::IsPathRooted($Path)) {
		$Path
	} else {
		Join-Path $Root $Path
	}

	$fullPath = [IO.Path]::GetFullPath($candidate)
	$rootPrefix = $Root.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
	if (-not $fullPath.Equals($Root, [StringComparison]::OrdinalIgnoreCase) -and
		-not $fullPath.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
		throw "Artifact path escapes repository root: $Path"
	}

	return $fullPath
}

function Get-RelativeRepositoryPath {
	param(
		[string]$Root,
		[string]$FullPath
	)

	if ($FullPath.Equals($Root, [StringComparison]::OrdinalIgnoreCase)) {
		return "."
	}

	$rootPrefix = $Root.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
	return $FullPath.Substring($rootPrefix.Length).Replace('\', '/')
}

function Get-DocumentClass {
	param([string]$RelativePath)

	switch -Regex ($RelativePath) {
		'^docs/design/' { return "current-design" }
		'^docs/proposals/' { return "proposal" }
		'^docs/content/' { return "content-guidance" }
		'^docs/reviews/' { return "point-in-time-review" }
		'^docs/discussions/' { return "discussion" }
		'^docs/archive/' { return "archive" }
		default { return "other" }
	}
}

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
	$detectedRoot = (& git -C $PSScriptRoot rev-parse --show-toplevel 2>$null)
	if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($detectedRoot)) {
		throw "Unable to locate the Git repository root. Pass -RepositoryRoot explicitly."
	}
	$RepositoryRoot = $detectedRoot.Trim()
}

$repositoryFullPath = [IO.Path]::GetFullPath($RepositoryRoot)
if (-not (Test-Path -LiteralPath $repositoryFullPath -PathType Container)) {
	throw "Repository root does not exist: $repositoryFullPath"
}

$artifactFiles = @{}
foreach ($artifactInput in $Artifact) {
	$artifactFullPath = Get-FullRepositoryPath -Root $repositoryFullPath -Path $artifactInput
	if (Test-Path -LiteralPath $artifactFullPath -PathType Container) {
		Get-ChildItem -LiteralPath $artifactFullPath -Recurse -File |
			Where-Object { $_.Extension -in @(".md", ".txt", ".html") } |
			ForEach-Object { $artifactFiles[$_.FullName] = $_.FullName }
	} elseif (Test-Path -LiteralPath $artifactFullPath -PathType Leaf) {
		$artifactFiles[$artifactFullPath] = $artifactFullPath
	} else {
		throw "Artifact does not exist: $artifactInput"
	}
}

$orderedArtifacts = @($artifactFiles.Values | Sort-Object)
if ($orderedArtifacts.Count -eq 0) {
	throw "No reviewable .md, .txt, or .html artifacts were found."
}

$pathPattern = '(?i)(?<path>(?:scripts|tests|docs|scenes|data|assets|tools|\.github|\.codex)/[A-Za-z0-9_./*?\-]+|project\.godot|magic\.csproj)'
$claimPattern = '(?i)(?:必须|应当|应该|将会|将由|已经|已实现|已完成|支持|覆盖|负责|边界|唯一|不得|must|should|shall|will|implemented|landed|supports?|covers?|owner|boundary)'
$claimCandidates = [Collections.Generic.List[object]]::new()
$explicitPathMap = @{}
$artifactSummaries = [Collections.Generic.List[object]]::new()
$claimTotal = 0

foreach ($artifactFullPath in $orderedArtifacts) {
	$relativeArtifact = Get-RelativeRepositoryPath -Root $repositoryFullPath -FullPath $artifactFullPath
	$lines = @(Get-Content -LiteralPath $artifactFullPath -Encoding UTF8)
	$headings = [Collections.Generic.List[object]]::new()

	for ($lineIndex = 0; $lineIndex -lt $lines.Count; $lineIndex++) {
		$line = [string]$lines[$lineIndex]
		$lineNumber = $lineIndex + 1
		$inSelectedRange = (
			$lineNumber -ge $LineStart -and
			($LineEnd -eq 0 -or $lineNumber -le $LineEnd)
		)
		if ($line -match '^\s*#{1,6}\s+\S') {
			$headings.Add([ordered]@{
				line = $lineNumber
				text = $line.Trim()
			})
		}

		if ($inSelectedRange) {
			foreach ($pathMatch in [regex]::Matches($line, $pathPattern)) {
				$pathValue = $pathMatch.Groups["path"].Value.TrimEnd('.', ':')
				$explicitPathMap[$pathValue] = $pathValue
			}
		}

		$isTableRow = $line -match '^\s*\|.+\|\s*$'
		$isTableSeparator = $line -match '^\s*\|(?:\s*:?-{3,}:?\s*\|)+\s*$'
		$isTableHeader = (
			$isTableRow -and
			$lineIndex + 1 -lt $lines.Count -and
			([string]$lines[$lineIndex + 1]) -match '^\s*\|(?:\s*:?-{3,}:?\s*\|)+\s*$'
		)
		$isMaterialTableRow = $isTableRow -and -not $isTableSeparator -and -not $isTableHeader
		if ($inSelectedRange -and ($line -match $claimPattern -or $isMaterialTableRow)) {
			$claimTotal++
			if ($claimCandidates.Count -lt $MaxClaimCandidates) {
				$claimCandidates.Add([ordered]@{
					id = "C{0:D3}" -f $claimTotal
					artifact = $relativeArtifact
					line = $lineNumber
					kind = if ($isMaterialTableRow) { "table-row" } else { "prose" }
					text = $line.Trim()
				})
			}
		}
	}

	$artifactSummaries.Add([ordered]@{
		path = $relativeArtifact
		document_class = Get-DocumentClass -RelativePath $relativeArtifact
		line_count = $lines.Count
		headings = @($headings)
	})
}

$explicitPaths = [Collections.Generic.List[object]]::new()
foreach ($pathValue in @($explicitPathMap.Values | Sort-Object)) {
	$hasWildcard = [Management.Automation.WildcardPattern]::ContainsWildcardCharacters($pathValue)
	$fullReference = Join-Path $repositoryFullPath $pathValue
	$exists = if ($hasWildcard) {
		@(Get-ChildItem -Path $fullReference -ErrorAction SilentlyContinue).Count -gt 0
	} else {
		Test-Path -LiteralPath $fullReference
	}
	$explicitPaths.Add([ordered]@{
		path = $pathValue
		exists = [bool]$exists
		source = "artifact"
	})
}

$contextPath = Join-Path $repositoryFullPath "docs/design/project_context_units.md"
$matchedUnits = @{}
if (Test-Path -LiteralPath $contextPath -PathType Leaf) {
	$contextLines = @(Get-Content -LiteralPath $contextPath -Encoding UTF8)
	$currentUnit = ""
	for ($lineIndex = 0; $lineIndex -lt $contextLines.Count; $lineIndex++) {
		$line = [string]$contextLines[$lineIndex]
		if ($line -match '^###\s+(CU-\d+\s+.+)$') {
			$currentUnit = $Matches[1].Trim()
		}
		if ([string]::IsNullOrWhiteSpace($currentUnit)) {
			continue
		}
		foreach ($pathValue in $explicitPathMap.Values) {
			$probe = ($pathValue -split '[*?]')[0].TrimEnd('/')
			if ($probe.Length -ge 4 -and $line.IndexOf($probe, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
				$matchedUnits[$currentUnit] = $currentUnit
			}
		}
	}
}

$readSetMap = @{}
foreach ($basePath in @("AGENTS.md", "docs/design/project_context_units.md", "docs/design/README.md")) {
	$readSetMap[$basePath] = [ordered]@{
		path = $basePath
		exists = Test-Path -LiteralPath (Join-Path $repositoryFullPath $basePath)
		source = "baseline"
	}
}
foreach ($pathEntry in $explicitPaths) {
	$readSetMap[$pathEntry.path] = $pathEntry
}

$warnings = [Collections.Generic.List[string]]::new()
if ($matchedUnits.Count -eq 0) {
	$warnings.Add("No context unit was matched from explicit repository paths; select the owning CU manually.")
}
if ($claimTotal -gt $claimCandidates.Count) {
	$warnings.Add("Claim candidates were truncated: showing $($claimCandidates.Count) of $claimTotal. Raise -MaxClaimCandidates to inspect more.")
}
foreach ($pathEntry in $explicitPaths) {
	if (-not $pathEntry.exists) {
		$warnings.Add("Referenced path was not found in the current checkout: $($pathEntry.path)")
	}
}

$report = [ordered]@{
	repository_root = $repositoryFullPath
	selected_line_range = [ordered]@{
		start = $LineStart
		end = if ($LineEnd -eq 0) { $null } else { $LineEnd }
	}
	artifacts = @($artifactSummaries)
	context_units = @($matchedUnits.Values | Sort-Object)
	claim_candidate_count = $claimTotal
	claim_candidates = @($claimCandidates)
	explicit_paths = @($explicitPaths)
	recommended_read_set = @($readSetMap.Values | Sort-Object { $_["path"] })
	warnings = @($warnings)
}

if ($Format -eq "json") {
	$report | ConvertTo-Json -Depth 8
	return
}

Write-Output "Repository: $($report.repository_root)"
Write-Output "Selected line range: $LineStart-$(
	if ($LineEnd -eq 0) { 'EOF' } else { $LineEnd }
)"
Write-Output ""
Write-Output "Artifacts:"
foreach ($entry in $report.artifacts) {
	Write-Output "  [$($entry.document_class)] $($entry.path) ($($entry.line_count) lines)"
}
Write-Output ""
Write-Output "Context units:"
if ($report.context_units.Count -eq 0) {
	Write-Output "  (none inferred)"
} else {
	foreach ($unit in $report.context_units) {
		Write-Output "  $unit"
	}
}
Write-Output ""
Write-Output "Claim candidates: $($report.claim_candidate_count)"
foreach ($claim in $report.claim_candidates) {
	Write-Output "  [$($claim.id)/$($claim.kind)] $($claim.artifact):$($claim.line) $($claim.text)"
}
Write-Output ""
Write-Output "Recommended read set:"
foreach ($entry in $report.recommended_read_set) {
	$state = if ($entry.exists) { "exists" } else { "missing" }
	Write-Output "  [$($entry.source)/$state] $($entry.path)"
}
if ($report.warnings.Count -gt 0) {
	Write-Output ""
	Write-Output "Warnings:"
	foreach ($warning in $report.warnings) {
		Write-Output "  $warning"
	}
}
