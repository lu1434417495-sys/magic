Set-StrictMode -Version Latest

function Invoke-ReadOnlyGit {
	param(
		[Parameter(Mandatory = $true)]
		[string]$RepositoryPath,

		[Parameter(Mandatory = $true)]
		[string[]]$Arguments,

		[switch]$AllowFailure
	)

	$startInfo = [System.Diagnostics.ProcessStartInfo]::new()
	$startInfo.FileName = "git"
	$startInfo.UseShellExecute = $false
	$startInfo.CreateNoWindow = $true
	$startInfo.RedirectStandardOutput = $true
	$startInfo.RedirectStandardError = $true
	$startInfo.Environment["GIT_OPTIONAL_LOCKS"] = "0"
	$startInfo.ArgumentList.Add("--no-optional-locks")
	$startInfo.ArgumentList.Add("-C")
	$startInfo.ArgumentList.Add($RepositoryPath)
	foreach ($argument in $Arguments) {
		$startInfo.ArgumentList.Add($argument)
	}

	$process = [System.Diagnostics.Process]::new()
	$process.StartInfo = $startInfo
	try {
		if (-not $process.Start()) {
			throw "Failed to start Git."
		}
		$stdoutTask = $process.StandardOutput.ReadToEndAsync()
		$stderrTask = $process.StandardError.ReadToEndAsync()
		$process.WaitForExit()
		$stdout = $stdoutTask.GetAwaiter().GetResult()
		$stderr = $stderrTask.GetAwaiter().GetResult()
		$exitCode = $process.ExitCode
	}
	finally {
		$process.Dispose()
	}

	$trimmedStdout = $stdout.TrimEnd([char[]]"`r`n")
	$trimmedStderr = $stderr.TrimEnd([char[]]"`r`n")
	[string[]]$output = @()
	if (-not [string]::IsNullOrEmpty($trimmedStdout)) {
		$output = @($trimmedStdout -split "\r?\n")
	}
	[string[]]$errorOutput = @()
	if (-not [string]::IsNullOrEmpty($trimmedStderr)) {
		$errorOutput = @($trimmedStderr -split "\r?\n")
	}
	$combined = @($output + $errorOutput) -join [Environment]::NewLine
	$errorText = $errorOutput -join [Environment]::NewLine
	$metadataFailurePattern = "(?i)(permission denied|access is denied|unable to create .*index\.lock|index\.lock.*(?:permission|denied))"
	if ($errorText -match $metadataFailurePattern) {
		throw "[GIT_METADATA_PERMISSION] Git metadata access failed. Stop Git write operations. $combined"
	}

	if ($exitCode -ne 0 -and -not $AllowFailure) {
		throw "Git command failed with exit code ${exitCode}: git $($Arguments -join ' ')`n$combined"
	}

	return [pscustomobject]@{
		ExitCode = $exitCode
		Output = $output
		ErrorOutput = $errorOutput
	}
}

function Resolve-RepositoryRoot {
	param(
		[Parameter(Mandatory = $true)]
		[string]$RepositoryPath
	)

	$resolvedInput = (Resolve-Path -LiteralPath $RepositoryPath -ErrorAction Stop).ProviderPath
	if (-not (Test-Path -LiteralPath $resolvedInput -PathType Container)) {
		throw "Repository path is not a directory: $resolvedInput"
	}

	$result = Invoke-ReadOnlyGit -RepositoryPath $resolvedInput -Arguments @("rev-parse", "--show-toplevel")
	if ($result.Output.Count -eq 0) {
		throw "Git did not return a repository root for: $resolvedInput"
	}

	$root = [System.IO.Path]::GetFullPath($result.Output[-1].Trim())
	if (-not (Test-Path -LiteralPath $root -PathType Container)) {
		throw "Resolved Git root does not exist: $root"
	}

	return $root
}

function Assert-ReadableGitMetadata {
	param(
		[Parameter(Mandatory = $true)]
		[string]$RepositoryRoot
	)

	$lockResult = Invoke-ReadOnlyGit -RepositoryPath $RepositoryRoot -Arguments @("rev-parse", "--git-path", "index.lock")
	if ($lockResult.Output.Count -eq 0) {
		throw "Git did not return an index-lock path."
	}

	$lockPath = $lockResult.Output[-1].Trim()
	if (-not [System.IO.Path]::IsPathRooted($lockPath)) {
		$lockPath = Join-Path $RepositoryRoot $lockPath
	}
	$lockPath = [System.IO.Path]::GetFullPath($lockPath)

	try {
		$metadataDirectory = Split-Path -Parent $lockPath
		$null = Get-Item -LiteralPath $metadataDirectory -Force -ErrorAction Stop
		if (Test-Path -LiteralPath $lockPath -ErrorAction Stop) {
			throw "[INDEX_LOCK] Git index lock exists at '$lockPath'. Stop Git write operations and identify the owning process."
		}
	}
	catch [System.UnauthorizedAccessException] {
		throw "[GIT_METADATA_PERMISSION] Cannot read Git metadata at '$metadataDirectory'. Stop Git write operations. $($_.Exception.Message)"
	}

	return $lockPath
}

function Resolve-RepositoryFile {
	param(
		[Parameter(Mandatory = $true)]
		[string]$RepositoryRoot,

		[Parameter(Mandatory = $true)]
		[string]$RelativePath
	)

	$rootWithSeparator = $RepositoryRoot.TrimEnd(
		[System.IO.Path]::DirectorySeparatorChar,
		[System.IO.Path]::AltDirectorySeparatorChar
	) + [System.IO.Path]::DirectorySeparatorChar
	$fullPath = [System.IO.Path]::GetFullPath((Join-Path $RepositoryRoot $RelativePath))
	if (-not $fullPath.StartsWith($rootWithSeparator, [System.StringComparison]::OrdinalIgnoreCase)) {
		throw "Git path escapes the repository root: $RelativePath"
	}

	return $fullPath
}

function Test-ProbablyBinaryFile {
	param(
		[Parameter(Mandatory = $true)]
		[string]$Path
	)

	$stream = [System.IO.File]::OpenRead($Path)
	try {
		$buffer = New-Object byte[] 8192
		$count = $stream.Read($buffer, 0, $buffer.Length)
		for ($index = 0; $index -lt $count; $index++) {
			if ($buffer[$index] -eq 0) {
				return $true
			}
		}
	}
	finally {
		$stream.Dispose()
	}

	return $false
}
