[CmdletBinding()]
param([string]$SboxRoot = "D:\SteamLibrary\steamapps\common\sbox", [int]$TimeoutSec = 480, [switch]$Clean)
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$scratch = Join-Path $env:TEMP "adaptive-director-editor-rig\scratch"
$sbproj = Join-Path $scratch "adaptive_director_gate.sbproj"
$libDir = Join-Path $scratch "Libraries\local.adaptive_director"
$resultPath = Join-Path $PSScriptRoot "editor_gate_result.json"
$sboxExe = Join-Path $SboxRoot "sbox-dev.exe"
$template = Join-Path $SboxRoot "templates\game.minimal"
function Fail([int]$code, [string]$message) { Write-Host "RESULT: $message" -ForegroundColor Red; exit $code }
if (-not (Test-Path $sboxExe)) { Fail 2 "sbox-dev.exe not found at $sboxExe" }
if ($Clean -and (Test-Path $scratch)) {
    $resolvedScratch = (Resolve-Path -LiteralPath $scratch).Path
    $resolvedTemp = (Resolve-Path -LiteralPath $env:TEMP).Path.TrimEnd([IO.Path]::DirectorySeparatorChar)
    if (-not $resolvedScratch.StartsWith($resolvedTemp + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { Fail 2 "Refusing to clean a scratch directory outside the system temp directory: $resolvedScratch" }
    if (Test-Path -LiteralPath $libDir) { [IO.Directory]::Delete($libDir, $false) }
    Remove-Item -LiteralPath $resolvedScratch -Recurse -Force
}
if (-not (Test-Path $sbproj)) {
    New-Item -ItemType Directory -Force $scratch | Out-Null
    Copy-Item (Join-Path $template "Assets") (Join-Path $scratch "Assets") -Recurse -Force
    Copy-Item (Join-Path $template "Code") (Join-Path $scratch "Code") -Recurse -Force
    Copy-Item (Join-Path $template "Editor") (Join-Path $scratch "Editor") -Recurse -Force
    $project = Get-Content (Join-Path $template "`$ident.sbproj") -Raw
    $project = $project -replace '"Title":\s*"[^"]*"', '"Title": "Adaptive Director Gate"'
    $project = $project -replace '"Ident":\s*"[^"]*"', '"Ident": "adaptive_director_gate"'
    [IO.File]::WriteAllText($sbproj, $project)
}
New-Item -ItemType Directory -Force (Join-Path $scratch "Libraries") | Out-Null
if (-not (Test-Path $libDir)) { New-Item -ItemType Junction -Path $libDir -Value $repoRoot | Out-Null }
Remove-Item $resultPath -Force -ErrorAction SilentlyContinue
Set-Content "$resultPath.arm" (Get-Date -Format o) -Encoding ascii
$env:ADAPTIVE_DIRECTOR_GATE_RESULT = $resultPath
try { $process = Start-Process -FilePath $sboxExe -ArgumentList @("-project", "`"$sbproj`"") -WorkingDirectory $SboxRoot -WindowStyle Hidden -PassThru }
finally { Remove-Item Env:ADAPTIVE_DIRECTOR_GATE_RESULT -ErrorAction SilentlyContinue }
$deadline = (Get-Date).AddSeconds($TimeoutSec); $complete = $false
while ((Get-Date) -lt $deadline) {
    if (Test-Path $resultPath) { try { $json = Get-Content $resultPath -Raw | ConvertFrom-Json; if ($json.Completed) { $complete = $true; break } } catch {} }
    if ($process.HasExited) { break }; Start-Sleep 2
}
if ($complete -and -not $process.HasExited) { $process.WaitForExit(20000) | Out-Null }
if (-not $process.HasExited) { taskkill /PID $process.Id /T /F | Out-Null }
if (-not (Test-Path $resultPath)) { Fail 2 "No result; inspect $SboxRoot\logs\sbox-dev.log for compiler or whitelist errors." }
$raw = Get-Content $resultPath -Raw; Write-Host $raw
$result = $raw | ConvertFrom-Json
if ($result.Completed -and $result.Passed) { Write-Host "RESULT: PASS - compiled and executed inside sbox-dev.exe." -ForegroundColor Green; exit 0 }
Fail 1 "Editor gate failed."
