param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Aggregate', 'Templates')]
    [string]$Mode
)

$ErrorActionPreference = 'Stop'
$repoRoot = (git --no-optional-locks rev-parse --show-toplevel).Trim()
$skillRoot = Join-Path $repoRoot 'data/configs/json/skills'
$resolvedSkillRoot = [IO.Path]::GetFullPath($skillRoot)
$expectedSkillRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'data/configs/json/skills'))
if ($resolvedSkillRoot -ne $expectedSkillRoot -or -not (Test-Path -LiteralPath $resolvedSkillRoot -PathType Container)) {
    throw "Refusing unexpected skill JSON root: $resolvedSkillRoot"
}

function Write-JsonDocument([string]$Path, [hashtable]$Document) {
    $json = $Document | ConvertTo-Json -Depth 100
    [IO.File]::WriteAllText($Path, $json + "`n", [Text.UTF8Encoding]::new($false))
}

if ($Mode -eq 'Aggregate') {
    $sourceFiles = @(Get-ChildItem -LiteralPath $resolvedSkillRoot -File -Filter '*.json' | Sort-Object Name)
    $entries = @()
    foreach ($file in $sourceFiles) {
        $document = Get-Content -Raw -LiteralPath $file.FullName | ConvertFrom-Json -AsHashtable
        if ($document.domain -ne 'skills' -or $document.entries.Count -eq 0) {
            throw "Expected canonical skill entries in $($file.Name)"
        }
        $entries += @($document.entries)
    }

    foreach ($file in $sourceFiles) {
        Remove-Item -LiteralPath $file.FullName
    }

    $buckets = [ordered]@{}
    foreach ($entry in $entries) {
        $id = [string]$entry.skill_id
        $prefix = if ($id.StartsWith('warrior_')) { 'warrior' }
            elseif ($id.StartsWith('mage_')) { 'mage' }
            elseif ($id.StartsWith('archer_')) { 'archer' }
            elseif ($id.StartsWith('weapon_')) { 'weapon' }
            else { 'misc' }
        if (-not $buckets.Contains($prefix)) { $buckets[$prefix] = @() }
        $buckets[$prefix] += $entry
    }

    foreach ($prefix in $buckets.Keys) {
        $orderedEntries = @($buckets[$prefix] | Sort-Object { [string]$_.skill_id })
        $partCount = [Math]::Ceiling($orderedEntries.Count / 30.0)
        while ($partCount -gt 1 -and [Math]::Floor($orderedEntries.Count / $partCount) -lt 20) {
            $partCount -= 1
        }
        $offset = 0
        for ($part = 1; $part -le $partCount; $part += 1) {
            $remaining = $orderedEntries.Count - $offset
            $remainingParts = $partCount - $part + 1
            $count = [Math]::Ceiling($remaining / $remainingParts)
            $chunk = @($orderedEntries[$offset..($offset + $count - 1)])
            $family = '{0}_{1:d2}' -f $prefix, $part
            Write-JsonDocument (Join-Path $resolvedSkillRoot "$family.json") ([ordered]@{
                schema = 1
                domain = 'skills'
                family = $family
                templates = [ordered]@{}
                entries = $chunk
            })
            $offset += $count
        }
    }
    exit 0
}

$documents = @(Get-ChildItem -LiteralPath $resolvedSkillRoot -File -Filter '*.json' | Sort-Object Name)
foreach ($file in $documents) {
    $document = Get-Content -Raw -LiteralPath $file.FullName | ConvertFrom-Json -AsHashtable
    $entries = @($document.entries)
    if ($entries.Count -eq 0) { continue }

    $template = [ordered]@{}
    foreach ($key in @('skill_type', 'learn_source')) {
        if (-not $entries[0].Contains($key)) { continue }
        $candidate = $entries[0][$key]
        $candidateJson = $candidate | ConvertTo-Json -Compress -Depth 20
        $shared = $true
        foreach ($entry in $entries) {
            if (-not $entry.Contains($key) -or (($entry[$key] | ConvertTo-Json -Compress -Depth 20) -ne $candidateJson)) {
                $shared = $false
                break
            }
        }
        if ($shared) { $template[$key] = $candidate }
    }
    if ($template.Count -eq 0) { continue }

    $templateName = 'family_base'
    foreach ($entry in $entries) {
        $entry.template = $templateName
    }
    $document.templates = [ordered]@{ $templateName = $template }
    Write-JsonDocument $file.FullName $document
}
