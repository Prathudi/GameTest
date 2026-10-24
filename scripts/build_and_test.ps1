# Builds everything and runs every test that does not need a game window.
# Usage: powershell -ExecutionPolicy Bypass -File scripts\build_and_test.ps1 [-Full] [-GameExe path]
#   -Full     also run the real game test suite (needs a desktop; opens game windows)
#   -GameExe  the game to use with -Full (default: build\game\Release\game.exe)
param(
    [switch]$Full,
    [string]$GameExe = "build\game\Release\game.exe"
)

# Work from the repository root, wherever the script is started from.
Set-Location (Split-Path $PSScriptRoot -Parent)

$steps = New-Object System.Collections.Generic.List[object]
$total = [System.Diagnostics.Stopwatch]::StartNew()

function Show-Summary {
    Write-Host ""
    Write-Host "==== Summary ====" -ForegroundColor Cyan
    foreach ($s in $steps) {
        $mark = if ($s.Ok) { "OK    " } else { "FAILED" }
        Write-Host ("{0}  {1}  ({2:0.0} s)  {3}" -f $mark, $s.Name, $s.Seconds, $s.Detail)
    }
    Write-Host ("Total: {0:0.0} s" -f $total.Elapsed.TotalSeconds)
}

# Runs one step. If it fails, prints the summary and stops the whole script with exit code 1.
function Invoke-Step {
    param([string]$Name, [scriptblock]$Action)
    Write-Host ""
    Write-Host "==== $Name ====" -ForegroundColor Cyan
    $clock = [System.Diagnostics.Stopwatch]::StartNew()
    $ok = $true
    $detail = ""
    $global:LASTEXITCODE = 0
    try {
        & $Action
        if ($LASTEXITCODE -ne 0) { $ok = $false; $detail = "exit code $LASTEXITCODE" }
    }
    catch {
        $ok = $false
        $detail = $_.Exception.Message
    }
    $steps.Add([pscustomobject]@{ Name = $Name; Ok = $ok; Seconds = $clock.Elapsed.TotalSeconds; Detail = $detail })
    if (-not $ok) {
        Show-Summary
        Write-Host ""
        Write-Host "STOPPED: '$Name' failed ($detail). Later steps were not run." -ForegroundColor Red
        exit 1
    }
}

Invoke-Step "Check tools" {
    foreach ($tool in "cmake", "dotnet") {
        if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) {
            throw "'$tool' was not found on PATH"
        }
    }
    $v = cmake --version
    $v[0]
    "dotnet " + (dotnet --version)
}

Invoke-Step "Configure C++ (CMake)" {
    if (Test-Path "build\CMakeCache.txt") {
        "Reusing the existing build folder."
    }
    else {
        cmake -S . -B build
    }
}

Invoke-Step "Build C++ (Release)" { cmake --build build --config Release }

Invoke-Step "C++ tests (CTest)" { ctest --test-dir build -C Release --output-on-failure }

Invoke-Step "Build runner (C#)" { dotnet build runner\GameTest.Runner -c Release }

Invoke-Step "Runner unit tests (C#)" { dotnet test runner\GameTest.Runner.Tests -c Release }

if ($Full) {
    Invoke-Step "Game test suite (needs a desktop)" {
        if (-not (Test-Path $GameExe)) { throw "game not found: $GameExe" }
        dotnet run --no-build --project runner\GameTest.Runner -c Release -- run-all tests\json $GameExe
    }
}

Show-Summary
Write-Host ""
Write-Host "ALL STEPS PASSED" -ForegroundColor Green
exit 0
