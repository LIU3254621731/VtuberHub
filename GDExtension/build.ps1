param(
  [string]$GodotCppDir = "$PSScriptRoot/godot-cpp",
  [string]$BuildType = "Release",
  [string]$Generator = "Visual Studio 17 2022",
  [string]$Arch = "x64"
)

$ErrorActionPreference = 'Stop'

Write-Host "=== VtuberHub GDExtension Build ===" -ForegroundColor Cyan

# Ensure godot-cpp exists
if (!(Test-Path $GodotCppDir)) {
  Write-Host "Cloning godot-cpp into $GodotCppDir" -ForegroundColor Yellow
  git clone https://github.com/godotengine/godot-cpp "$GodotCppDir"
}

# Build godot-cpp if missing lib for requested config
$buildTypeToken = if ($BuildType -match 'Debug') { 'debug' } else { 'release' }
$libCandidates = Get-ChildItem -Recurse -Filter *.lib -Path "$GodotCppDir" |
  Where-Object { $_.Name -match 'godot-cpp' -and $_.Name -match "template_$buildTypeToken" }
if (-not $libCandidates) {
  Write-Host "Building godot-cpp ($BuildType)" -ForegroundColor Yellow
  pushd $GodotCppDir
  if (!(Test-Path build)) { mkdir build | Out-Null }
  pushd build
  # Configure once (safe to run again)
  cmake .. -G "$Generator" -A $Arch
  cmake --build . --config $BuildType --target godot-cpp
  popd
  popd
}

# Build bridge
$bridgeDir = Join-Path $PSScriptRoot 'GDExtension/bridge'
if (!(Test-Path $bridgeDir)) { $bridgeDir = Join-Path $PSScriptRoot 'bridge' }
if (!(Test-Path $bridgeDir)) { throw "Bridge directory not found" }

$buildDir = Join-Path $bridgeDir 'build'
if (!(Test-Path $buildDir)) { mkdir $buildDir | Out-Null }
pushd $buildDir
cmake .. -G "$Generator" -A $Arch -DCMAKE_BUILD_TYPE=$BuildType -DGODOT_CPP_DIR="$GodotCppDir"
cmake --build . --config $BuildType --target vtuberhub_bridge
popd

Write-Host "Build complete. DLL at: $buildDir/bin/vtuberhub_bridge.windows.dll" -ForegroundColor Green