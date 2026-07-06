[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$OutputPath = "artifacts/nuget",
    [switch]$ChangedOnly,
    [string]$BaseRef,
    [switch]$NoBuild,
    [switch]$Push,
    [string]$NuGetSource,
    [string]$ApiKey,
    [switch]$SkipDuplicate = $true,
    [switch]$DryRun,
    [switch]$VsOutputEncoding,
    [string]$DotNetVerbosity = 'quiet'
)

$ErrorActionPreference = 'Stop'

if ($VsOutputEncoding) {
    $encoding = [System.Text.Encoding]::Default
    [Console]::OutputEncoding = $encoding
    $OutputEncoding = $encoding
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir
$slnPath = Join-Path $repoRoot "Kwy.slnx"
$outputFullPath = Join-Path $repoRoot $OutputPath

$debugPackageProperties = @(
    '-p:DebugType=portable',
    '-p:IncludeSymbols=true',
    '-p:SymbolPackageFormat=snupkg',
    '-p:PublishRepositoryUrl=true',
    '-p:EmbedUntrackedSources=true'
)

function Write-Step([string]$message) {
    Write-Host "`n==> $message" -ForegroundColor Cyan
}

function Get-ProjectName([string]$projectPath) {
    return [System.IO.Path]::GetFileNameWithoutExtension($projectPath)
}

function Get-RelativePath([string]$path) {
    $root = [System.IO.Path]::GetFullPath($repoRoot).TrimEnd([char[]]@('\','/')) + [System.IO.Path]::DirectorySeparatorChar
    $full = [System.IO.Path]::GetFullPath($path)
    $rootUri = New-Object System.Uri($root)
    $fullUri = New-Object System.Uri($full)
    return [System.Uri]::UnescapeDataString($rootUri.MakeRelativeUri($fullUri).ToString()).Replace('\', '/')
}

function Read-ProjectXml([string]$path) {
    [xml](Get-Content -LiteralPath $path -Raw)
}

function Test-IsExplicitFalse($value) {
    return $value -and $value.ToString().Equals("false", [StringComparison]::OrdinalIgnoreCase)
}

function Test-IsPackableProject([System.IO.FileInfo]$projectFile) {
    $name = Get-ProjectName $projectFile.FullName
    if ($name -notlike "Kwy.*") { return $false }
    if ($name -eq "Kwy.Packaging") { return $false }
    if ($name -like "*.Tests") { return $false }
    if ($name -like "*.Benchmarks") { return $false }
    if ($name -like "KwyTemplate.*") { return $false }
    if ($name -eq "KwyAppDemo") { return $false }

    $xml = Read-ProjectXml $projectFile.FullName
    $isPackable = @($xml.Project.PropertyGroup | ForEach-Object { $_.IsPackable } | Where-Object { $_ }) | Select-Object -First 1
    if (Test-IsExplicitFalse $isPackable) { return $false }
    return $true
}

function Get-ProjectReferences([string]$projectPath) {
    $projectDir = Split-Path -Parent $projectPath
    $xml = Read-ProjectXml $projectPath
    $refs = New-Object System.Collections.Generic.List[string]
    foreach ($itemGroup in @($xml.Project.ItemGroup)) {
        foreach ($ref in @($itemGroup.ProjectReference)) {
            $include = $ref.Include
            if ([string]::IsNullOrWhiteSpace($include)) { continue }
            $resolved = [System.IO.Path]::GetFullPath((Join-Path $projectDir $include))
            $refs.Add($resolved)
        }
    }
    return $refs
}

function Get-DefaultBaseRef {
    try {
        $tag = (& git -C $repoRoot describe --tags --abbrev=0 2>$null).Trim()
        if (-not [string]::IsNullOrWhiteSpace($tag)) { return $tag }
    }
    catch { }

    try {
        & git -C $repoRoot rev-parse --verify HEAD~1 1>$null 2>$null
        if ($LASTEXITCODE -eq 0) { return "HEAD~1" }
    }
    catch { }

    return $null
}

function Get-ChangedFiles([string]$base) {
    $files = New-Object System.Collections.Generic.HashSet[string]([StringComparer]::OrdinalIgnoreCase)

    if (-not [string]::IsNullOrWhiteSpace($base)) {
        foreach ($line in & git -C $repoRoot diff --name-only "$base..HEAD") {
            if (-not [string]::IsNullOrWhiteSpace($line)) { [void]$files.Add($line.Replace('\\', '/')) }
        }
    }

    foreach ($line in & git -C $repoRoot diff --name-only) {
        if (-not [string]::IsNullOrWhiteSpace($line)) { [void]$files.Add($line.Replace('\\', '/')) }
    }

    foreach ($line in & git -C $repoRoot diff --name-only --cached) {
        if (-not [string]::IsNullOrWhiteSpace($line)) { [void]$files.Add($line.Replace('\\', '/')) }
    }

    return @($files)
}

Write-Step "Discovering packable projects"
$allProjects = Get-ChildItem -LiteralPath $repoRoot -Recurse -Filter "*.csproj" |
    Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }

$packableProjects = @($allProjects | Where-Object { Test-IsPackableProject $_ })
if ($packableProjects.Count -eq 0) {
    Write-Host "No packable Kwy projects found." -ForegroundColor Yellow
    exit 0
}

$projectByPath = @{}
foreach ($project in $allProjects) {
    $projectByPath[$project.FullName] = $project
}

$packableByPath = @{}
foreach ($project in $packableProjects) {
    $packableByPath[$project.FullName] = $project
}

$referencesByProject = @{}
$referencedByProject = @{}
foreach ($project in $allProjects) {
    $refs = @(Get-ProjectReferences $project.FullName)
    $referencesByProject[$project.FullName] = $refs
    foreach ($ref in $refs) {
        if (-not $referencedByProject.ContainsKey($ref)) {
            $referencedByProject[$ref] = New-Object System.Collections.Generic.List[string]
        }
        $referencedByProject[$ref].Add($project.FullName)
    }
}

$projectsToPack = New-Object System.Collections.Generic.HashSet[string]([StringComparer]::OrdinalIgnoreCase)

if ($ChangedOnly) {
    if ([string]::IsNullOrWhiteSpace($BaseRef)) {
        $BaseRef = Get-DefaultBaseRef
    }

    if ([string]::IsNullOrWhiteSpace($BaseRef)) {
        Write-Host "No git base reference found. Falling back to all packable projects." -ForegroundColor Yellow
        foreach ($project in $packableProjects) { [void]$projectsToPack.Add($project.FullName) }
    }
    else {
        Write-Step "Detecting changed projects from $BaseRef"
        $changedFiles = @(Get-ChangedFiles $BaseRef)
        if ($changedFiles.Count -eq 0) {
            Write-Host "No changed files detected. Nothing to pack." -ForegroundColor Yellow
            exit 0
        }

        $repoWideChanged = $false
        $changedProjects = New-Object System.Collections.Generic.HashSet[string]([StringComparer]::OrdinalIgnoreCase)

        foreach ($file in $changedFiles) {
            if ($file -match '^(Directory\..*\.props|Directory\..*\.targets|NuGet\.config|global\.json|Kwy\.slnx)') {
                $repoWideChanged = $true
                continue
            }

            $absoluteFile = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $file))
            $owner = $allProjects |
                Where-Object { $absoluteFile.StartsWith((Split-Path -Parent $_.FullName), [StringComparison]::OrdinalIgnoreCase) } |
                Sort-Object { (Split-Path -Parent $_.FullName).Length } -Descending |
                Select-Object -First 1

            if ($owner) { [void]$changedProjects.Add($owner.FullName) }
        }

        if ($repoWideChanged) {
            foreach ($project in $packableProjects) { [void]$projectsToPack.Add($project.FullName) }
        }
        else {
            $queue = New-Object System.Collections.Generic.Queue[string]
            foreach ($projectPath in $changedProjects) { $queue.Enqueue($projectPath) }

            $visited = New-Object System.Collections.Generic.HashSet[string]([StringComparer]::OrdinalIgnoreCase)
            while ($queue.Count -gt 0) {
                $current = $queue.Dequeue()
                if (-not $visited.Add($current)) { continue }

                if ($packableByPath.ContainsKey($current)) { [void]$projectsToPack.Add($current) }

                if ($referencedByProject.ContainsKey($current)) {
                    foreach ($dependent in $referencedByProject[$current]) {
                        $queue.Enqueue($dependent)
                    }
                }
            }
        }
    }
}
else {
    foreach ($project in $packableProjects) { [void]$projectsToPack.Add($project.FullName) }
}

$orderedProjects = @($projectsToPack | Sort-Object)
if ($orderedProjects.Count -eq 0) {
    Write-Host "No affected packable projects found." -ForegroundColor Yellow
    exit 0
}

Write-Host "Projects to pack:" -ForegroundColor Green
foreach ($projectPath in $orderedProjects) {
    Write-Host "  - $(Get-RelativePath $projectPath)"
}

if ($DryRun) {
    Write-Host "Dry run completed. No build, pack or push was executed." -ForegroundColor Yellow
    exit 0
}

New-Item -ItemType Directory -Force -Path $outputFullPath | Out-Null

if (-not $NoBuild) {
    Write-Step "Building solution ($Configuration)"
    dotnet build $slnPath -c $Configuration -m:1 -p:RunKwyPackaging=false -p:GeneratePackageOnBuild=false -p:NuGetAudit=false "-p:NoWarn=NU1900;NU5128" @debugPackageProperties -v $DotNetVerbosity
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Step "Packing NuGet packages"
foreach ($projectPath in $orderedProjects) {
    Write-Host "Packing $(Get-RelativePath $projectPath)..." -ForegroundColor Cyan
    dotnet pack $projectPath -c $Configuration --no-build -o $outputFullPath -p:Authors=Kwy -p:GeneratePackageOnBuild=false -p:NuGetAudit=false "-p:NoWarn=NU1900;NU5128" @debugPackageProperties -v $DotNetVerbosity
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

if ($Push) {
    if ([string]::IsNullOrWhiteSpace($NuGetSource)) {
        throw "-NuGetSource is required when -Push is specified."
    }

    Write-Step "Pushing packages"
    $pushArgs = @("nuget", "push", (Join-Path $outputFullPath "*.nupkg"), "--source", $NuGetSource)
    if (-not [string]::IsNullOrWhiteSpace($ApiKey)) { $pushArgs += @("--api-key", $ApiKey) }
    if ($SkipDuplicate) { $pushArgs += "--skip-duplicate" }
    & dotnet @pushArgs
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Step "Done"
Write-Host "NuGet output: $outputFullPath" -ForegroundColor Green







