[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = '1.0.0',

    [ValidateSet('win-x64')]
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$projectPath = Join-Path $repositoryRoot 'src\HDecompressHelper\HDecompressHelper.csproj'
$installerScript = Join-Path $repositoryRoot 'installer\HNovaArchive.iss'
$publishDirectory = Join-Path $repositoryRoot "artifacts\publish\$Runtime"
$installerDirectory = Join-Path $repositoryRoot 'artifacts\installer'

function Reset-BuildDirectory([string]$Path) {
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $workspacePrefix = $repositoryRoot + [System.IO.Path]::DirectorySeparatorChar
    if (-not $fullPath.StartsWith($workspacePrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean a directory outside the repository: $fullPath"
    }

    if ([System.IO.Directory]::Exists($fullPath)) {
        [System.IO.Directory]::Delete($fullPath, $true)
    }

    [System.IO.Directory]::CreateDirectory($fullPath) | Out-Null
}

$innoCompilerCandidates = @(
    'C:\Program Files\Inno Setup 7\ISCC.exe',
    'C:\Program Files (x86)\Inno Setup 7\ISCC.exe',
    'C:\Program Files\Inno Setup 6\ISCC.exe',
    'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
)
$innoCompiler = $innoCompilerCandidates |
    Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
    Select-Object -First 1
if ($null -eq $innoCompiler) {
    $innoCommand = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($null -ne $innoCommand) {
        $innoCompiler = $innoCommand.Source
    }
}

if ([string]::IsNullOrWhiteSpace($innoCompiler)) {
    throw 'Inno Setup compiler ISCC.exe was not found. Install Inno Setup 6 or 7 first.'
}

Reset-BuildDirectory $publishDirectory
Reset-BuildDirectory $installerDirectory

Write-Host "Publishing the $Runtime self-contained single-file app..."
& dotnet publish $projectPath `
    --configuration Release `
    --runtime $Runtime `
    --self-contained true `
    --output $publishDirectory `
    -p:Version=$Version `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$publishedExecutable = Join-Path $publishDirectory 'HDecompressHelper.exe'
if (-not (Test-Path -LiteralPath $publishedExecutable -PathType Leaf)) {
    throw "The published executable is missing: $publishedExecutable"
}

Write-Host 'Compiling the Inno Setup installer...'
& $innoCompiler `
    "/DMyAppVersion=$Version" `
    "/DPublishDir=$publishDirectory" `
    "/DOutputDir=$installerDirectory" `
    $installerScript
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compilation failed with exit code $LASTEXITCODE."
}

$installerPath = Join-Path $installerDirectory "HNovaArchive-$Version-win-x64-Setup.exe"
if (-not (Test-Path -LiteralPath $installerPath -PathType Leaf)) {
    throw "The generated installer was not found: $installerPath"
}

$installer = Get-Item -LiteralPath $installerPath
$hash = Get-FileHash -LiteralPath $installerPath -Algorithm SHA256
Write-Host ''
Write-Host "Installer: $($installer.FullName)"
Write-Host "Size: $([Math]::Round($installer.Length / 1MB, 2)) MB"
Write-Host "SHA256: $($hash.Hash)"
