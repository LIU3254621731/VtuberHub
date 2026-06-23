#requires -version 5.1
$ErrorActionPreference = 'Stop'

# Paths
$workspace = 'c:\Users\32546\Desktop\VtuberHub'
$testDir = Join-Path $workspace 'test'
$testSrc = Join-Path $testDir 'mediapipe-test.cpp'
$mediapipeSrcDir = Join-Path $workspace 'mediapipe\src\bridge'
$handDllCpp = Join-Path $mediapipeSrcDir 'MediapipeHandTrackingDll.cpp'
$holisticDllCpp = Join-Path $mediapipeSrcDir 'MediapipeHolisticTrackingDll.cpp'
$dynLoaderCpp = Join-Path $mediapipeSrcDir 'DynamicModuleLoader.cpp'
$outExe = Join-Path $testDir 'mediapipe-test.exe'

$opencvInclude = Join-Path $workspace 'OpenCV\include'
$opencvLibRelease = Join-Path $workspace 'OpenCV\lib\x64\Release'
$mediapipeDllDir = Join-Path $workspace 'mediapipe\dll'

# Validate sources
$srcs = @($testSrc, $handDllCpp, $holisticDllCpp, $dynLoaderCpp)
foreach ($s in $srcs) { if (-not (Test-Path $s)) { throw "Source file not found: $s" } }

# Find vcvars64.bat
$vcvarsCandidates = @(
    'C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat',
    'C:\Program Files\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat',
    'C:\Program Files (x86)\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat',
    'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat'
)
$vcvars64 = $vcvarsCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $vcvars64) { throw 'vcvars64.bat not found. Please install VS 2022 C++ tools.' }
Write-Host "Using vcvars64: $vcvars64" -ForegroundColor Yellow

# Build command
$compileArgs = @(
    '/EHsc',
    "`"$testSrc`"",
    "`"$handDllCpp`"",
    "`"$holisticDllCpp`"",
    "`"$dynLoaderCpp`"",
    "/I`"$opencvInclude`"",
    "/I`"$mediapipeSrcDir`"",
    '/Fe:mediapipe-test.exe',
    '/link',
    "/LIBPATH:`"$opencvLibRelease`"",
    'opencv_world3410.lib'
)
$clCmd = 'cl ' + ($compileArgs -join ' ')

Push-Location $testDir
try {
    $cmdArgs = @('/c', "call `"$vcvars64`" && $clCmd")
    Write-Host ("cmd " + ($cmdArgs -join ' ')) -ForegroundColor DarkCyan
    Start-Process -FilePath 'cmd' -ArgumentList $cmdArgs -Wait -NoNewWindow
} finally {
    Pop-Location
}

# Copy runtime DLLs and models next to exe
$runtimeFiles = @(
    'MediapipeHolisticTracking.dll',
    'Mediapipe_Hand_Tracking.dll',
    'holistic_tracking_cpu.pbtxt',
    'hand_tracking_desktop_live.pbtxt',
    'opencv_world3410.dll',
    'opencv_world3410d.dll',
    'opencv_ffmpeg3410_64.dll'
)
foreach ($f in $runtimeFiles) {
    $src = Join-Path $mediapipeDllDir $f
    if (Test-Path $src) { Copy-Item -Force $src $testDir }
}

# Ensure mediapipe modules (models/assets) are available relative to CWD
$modulesSrc = Join-Path $workspace 'mediapipe\mediapipe\modules'
$modulesDest = Join-Path $testDir 'mediapipe\modules'
if (Test-Path $modulesSrc) {
    Write-Host "Syncing mediapipe modules to test folder..." -ForegroundColor Yellow
    New-Item -ItemType Directory -Force -Path (Split-Path $modulesDest) | Out-Null
    robocopy "$modulesSrc" "$modulesDest" /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
}
if (-not (Test-Path $outExe)) { throw "Build failed: $outExe not found" }

Write-Host 'Launching mediapipe-test.exe...' -ForegroundColor Cyan
Push-Location $testDir
try {
    & .\mediapipe-test.exe
} finally {
    Pop-Location
}