#requires -Version 7
<#
.SYNOPSIS
Builds the release archives: the plugin alone, and the plugin with BepInEx.

.DESCRIPTION
Writes two files into dist/:

- XSOverlayKeyboardIme-<version>.zip carries the plugin and its licences.
- XSOverlayKeyboardIme-<version>+BepInEx.zip adds BepInEx 5.4.23.6 x64.

Both extract into the XSOverlay folder, and the plugin lands in the same
BepInEx/plugins/XSOverlayKeyboardIme/ folder.
#>
[CmdletBinding()]
param(
    # Build configuration of the plugin and the native library.
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    # XSOverlay folder. Defaults to the GameDir the plugin project builds against.
    [string]$GameDir,

    # Folder that receives the archives, relative to the repository root.
    [string]$OutputDir = 'dist',

    # Reuses the existing build output instead of rebuilding it.
    [switch]$SkipBuild,

    # BepInEx release archive to pack. Defaults to the cached download, fetched when missing.
    [string]$BepInExArchive
)

$ErrorActionPreference = 'Stop'

# Pinned: the bundle must carry the exact loader the plugin was verified on.
$bepinexVersion = '5.4.23.6'
$bepinexUrl = "https://github.com/BepInEx/BepInEx/releases/download/v$bepinexVersion/BepInEx_win_x64_$bepinexVersion.zip"
$bepinexSha256 = '6c57a714a892efb4e9e1fef63d608a3391f30ec984031c465404e4ee381a0755'

$repo = Split-Path -Parent $PSScriptRoot
$pluginName = 'XSOverlayKeyboardIme'
$pluginProject = Join-Path $repo "$pluginName/$pluginName.csproj"
$pluginFolder = "BepInEx/plugins/$pluginName"

function Assert-Step($name, $exitCode) {
    if ($exitCode -ne 0) {
        throw "$name failed ($exitCode)"
    }
}

function Resolve-RepositoryPath($path) {
    if ([IO.Path]::IsPathRooted($path)) {
        return $path
    }

    return Join-Path $repo $path
}

function Get-ProjectProperty($name) {
    $match = Select-Xml -Path $pluginProject -XPath "Project/PropertyGroup/$name" | Select-Object -First 1
    if (-not $match) {
        throw "no <$name> in $pluginProject"
    }

    return $match.Node.InnerText
}

function Resolve-BepInExArchive($given) {
    $archive = if ($given) { Resolve-RepositoryPath $given } else { Join-Path $repo "temp/downloads/BepInEx_win_x64_$bepinexVersion.zip" }

    if (Test-Path $archive) {
        Write-Host "using $archive"
    }
    else {
        Write-Host "downloading $bepinexUrl"
        New-Item -ItemType Directory -Force (Split-Path -Parent $archive) | Out-Null
        $download = @{ Uri = $bepinexUrl; OutFile = $archive; TimeoutSec = 120 }
        if ($env:HTTPS_PROXY) {
            $download.Proxy = $env:HTTPS_PROXY
        }
        Invoke-WebRequest @download
    }

    $hash = (Get-FileHash $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($hash -ne $bepinexSha256) {
        throw "$archive is not BepInEx $bepinexVersion for win x64; its SHA256 is $hash"
    }

    return $archive
}

# The plugin loads the native library from its own folder, so both files travel together.
# The licences travel with them: IngameIME.dll is distributed under the LGPL 2.1.
function Copy-PluginFiles($destination) {
    New-Item -ItemType Directory -Force $destination | Out-Null
    Copy-Item $pluginDll, $nativeDll -Destination $destination
    Copy-Item (Join-Path $repo 'LICENSE') -Destination $destination
    Copy-Item (Join-Path $repo 'THIRD-PARTY-NOTICES.md') -Destination $destination
    Copy-Item (Join-Path $repo 'native/LICENSE-LGPL-2.1.txt') -Destination (Join-Path $destination 'LICENSE-LGPL-2.1.txt')
}

function New-Archive($staging, $zipPath, $required) {
    if (Test-Path $zipPath) {
        Remove-Item $zipPath
    }
    [IO.Compression.ZipFile]::CreateFromDirectory($staging, $zipPath, [IO.Compression.CompressionLevel]::Optimal, $false)

    # Read the archive back: a package is only usable when every entry survived packing.
    $entryNames = @()
    $zip = [IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        $entryNames = $zip.Entries | ForEach-Object { $_.FullName }
    }
    finally {
        $zip.Dispose()
    }

    foreach ($name in $required) {
        if ($entryNames -notcontains $name) {
            throw "$zipPath has no $name"
        }
    }
    if ($entryNames | Where-Object { $_ -like '*\*' }) {
        throw "$zipPath holds backslash entry names, which some extractors misread"
    }

    Write-Host ("packed {0} ({1:N0} KiB, {2} files)" -f $zipPath, ((Get-Item $zipPath).Length / 1KB), $entryNames.Count)
}

function Reset-Staging($path) {
    if (Test-Path $path) {
        Remove-Item $path -Recurse -Force
    }

    return $path
}

# The page bundle is embedded in the plugin DLL, so it is a build input.
if (-not $SkipBuild) {
    # One default, kept in the project that applies it.
    if (-not $GameDir) {
        $GameDir = Get-ProjectProperty 'GameDir'
    }

    $managed = Join-Path $GameDir 'XSOverlay_Data/Managed'
    if (-not (Test-Path $managed)) {
        throw "no XSOverlay managed assemblies in $managed"
    }

    & pnpm --dir (Join-Path $repo 'web') build
    Assert-Step 'the page script build' $LASTEXITCODE

    & pwsh -NoProfile -File (Join-Path $repo 'native/build.ps1') -Configuration $Configuration
    Assert-Step 'the native library build' $LASTEXITCODE

    & dotnet build $pluginProject -c $Configuration "-p:GameDir=$GameDir"
    Assert-Step 'the plugin build' $LASTEXITCODE
}
$pluginDll = Join-Path $repo "$pluginName/bin/$Configuration/netstandard2.1/$pluginName.dll"
$nativeDll = Join-Path $repo "native/build/$Configuration/IngameIME.dll"

foreach ($artifact in $pluginDll, $nativeDll) {
    if (-not (Test-Path $artifact)) {
        throw "$artifact is missing; drop -SkipBuild or build it first"
    }
}

$version = Get-ProjectProperty 'Version'
$archive = Resolve-BepInExArchive $BepInExArchive

$outputRoot = Resolve-RepositoryPath $OutputDir
New-Item -ItemType Directory -Force $outputRoot | Out-Null

$packaged = @(
    "$pluginFolder/$pluginName.dll",
    "$pluginFolder/IngameIME.dll",
    "$pluginFolder/LICENSE",
    "$pluginFolder/LICENSE-LGPL-2.1.txt",
    "$pluginFolder/THIRD-PARTY-NOTICES.md"
)

# Plugin only: a user who already runs BepInEx needs these files alone.
$staging = Reset-Staging (Join-Path $outputRoot 'staging-plugin')
Copy-PluginFiles (Join-Path $staging $pluginFolder)
New-Archive $staging (Join-Path $outputRoot "$pluginName-$version.zip") $packaged
Remove-Item $staging -Recurse -Force

# With the loader: everything the game folder needs, for a user who has no BepInEx.
$staging = Reset-Staging (Join-Path $outputRoot 'staging-bundle')
Expand-Archive -Path $archive -DestinationPath $staging
Copy-PluginFiles (Join-Path $staging $pluginFolder)
New-Archive $staging (Join-Path $outputRoot "$pluginName-$version+BepInEx.zip") ($packaged + @(
        'winhttp.dll',
        'doorstop_config.ini',
        '.doorstop_version',
        'BepInEx/core/BepInEx.dll',
        'BepInEx/core/BepInEx.Preloader.dll'
    ))
Remove-Item $staging -Recurse -Force

Write-Host ''
Write-Host 'extract either archive into the XSOverlay folder (Steam > XSOverlay > Manage > Browse local files)'
Write-Host "the plugin lands in $pluginFolder"
