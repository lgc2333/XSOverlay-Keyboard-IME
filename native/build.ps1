#requires -Version 7
<#
.SYNOPSIS
Builds the native library the plugin loads: IngameIME.dll.

.DESCRIPTION
The library lands in native/build/<Configuration>. The plugin's deploy target
copies it next to the plugin DLL. See ADR 0023.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    # Removes the build tree first, so the configure step runs from scratch.
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'

$native = $PSScriptRoot
$build = Join-Path $native 'build'
$repo = Split-Path -Parent $native

function Get-VisualStudioPath {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
    if (-not (Test-Path $vswhere)) {
        return $null
    }

    return (& $vswhere -latest -products * -property installationPath) | Select-Object -First 1
}

function Resolve-CMake($visualStudio) {
    $command = Get-Command cmake -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    # Visual Studio ships CMake, so a build host does not need a second copy.
    if ($visualStudio) {
        $candidate = Join-Path $visualStudio 'Common7/IDE/CommonExtensions/Microsoft/CMake/CMake/bin/cmake.exe'
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    throw 'cmake is not installed. Install CMake, or the Visual Studio C++ workload.'
}

function Resolve-Ninja($visualStudio) {
    $command = Get-Command ninja -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    if ($visualStudio) {
        $candidate = Join-Path $visualStudio 'Common7/IDE/CommonExtensions/Microsoft/CMake/Ninja/ninja.exe'
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    throw 'ninja is not installed. Install Ninja, or the Visual Studio CMake workload.'
}

# The compiler is only on PATH inside a developer prompt, so its environment is
# imported here. Ninja needs it as well as the compiler itself.
function Import-BuildEnvironment($visualStudio) {
    $vcvars = Join-Path $visualStudio 'VC/Auxiliary/Build/vcvars64.bat'
    if (-not (Test-Path $vcvars)) {
        throw "the C++ toolset is missing from $visualStudio; install the C++ workload"
    }

    $lines = & cmd.exe /c "call `"$vcvars`" >nul && set"
    if ($LASTEXITCODE -ne 0) {
        throw "vcvars64 failed ($LASTEXITCODE)"
    }

    foreach ($line in $lines) {
        $split = $line.Split('=', 2)
        if ($split.Count -eq 2 -and $split[0]) {
            Set-Item -Path "env:$($split[0])" -Value $split[1]
        }
    }
}

$visualStudio = Get-VisualStudioPath
$cmake = Resolve-CMake $visualStudio
$ninja = Resolve-Ninja $visualStudio
Import-BuildEnvironment $visualStudio

# The upstream repositories are submodules. Nothing recurses: IngameIME_Win32
# refers to its own copy of IngameIME-Common through an SSH URL, while this
# build uses the sibling submodule.
$missing = @()
foreach ($path in 'native/IngameIME_Win32', 'native/IngameIME-Common') {
    if (-not (Test-Path (Join-Path $repo "$path/.git"))) {
        $missing += $path
    }
}

if ($missing.Count -gt 0) {
    Write-Host "fetching submodules: $($missing -join ', ')"
    git -C $repo submodule update --init -- @missing
    if ($LASTEXITCODE -ne 0) {
        throw "git submodule update failed ($LASTEXITCODE)"
    }
}

if ($Clean -and (Test-Path $build)) {
    Remove-Item -Recurse -Force $build
}

# Every -D argument is quoted: PowerShell does not expand a variable inside an
# unquoted token that starts with -D.
& $cmake -S $native -B $build -G Ninja "-DCMAKE_BUILD_TYPE=$Configuration" "-DCMAKE_MAKE_PROGRAM=$ninja"
if ($LASTEXITCODE -ne 0) {
    throw "cmake configure failed ($LASTEXITCODE)"
}

& $cmake --build $build
if ($LASTEXITCODE -ne 0) {
    throw "cmake build failed ($LASTEXITCODE)"
}

$output = Join-Path $build $Configuration
$produced = Get-ChildItem $output -Filter '*.dll' -ErrorAction SilentlyContinue
if (-not $produced) {
    throw "no DLL was produced in $output"
}

Write-Host ''
Write-Host "built into $output"
foreach ($file in $produced) {
    Write-Host "  $($file.Name)  $($file.Length) bytes"
}
