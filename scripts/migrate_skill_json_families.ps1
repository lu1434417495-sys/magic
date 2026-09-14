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

    $templates = [ordered]@{}
    foreach ($key in @('max_level', 'non_core_max_level', 'growth_tier')) {
        $groups = @{}
        foreach ($entry in $entries) {
            if (-not $entry.Contains($key)) { continue }
            $serialized = $entry[$key] | ConvertTo-Json -Compress -Depth 20
            if (-not $groups.Contains($serialized)) { $groups[$serialized] = @() }
            $groups[$serialized] += $entry
        }
        foreach ($serialized in $groups.Keys) {
            $members = @($groups[$serialized])
            if ($members.Count -lt 2) { continue }
            $suffix = ($serialized -replace '[^A-Za-z0-9]+', '_').Trim('_').ToLowerInvariant()
            $templateName = "${key}_${suffix}"
            $templates[$templateName] = [ordered]@{ $key = $members[0][$key] }
            foreach ($entry in $members) {
                if (-not $entry.Contains('template')) {
                    $entry['template'] = $templateName
                }
            }
        }
    }
    if ($templates.Count -eq 0) { continue }
    $document.templates = $templates
    Write-JsonDocument $file.FullName $document
}
