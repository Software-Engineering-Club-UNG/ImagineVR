<#
.SYNOPSIS
  Runs the Unity editor on client/ from the command line and prints the parts of the log that matter.

.EXAMPLE
  # compile only
  ./scripts/unity.ps1

  # one-time project setup (two steps: the second needs the first imported)
  ./scripts/unity.ps1 -Method ImagineVR.EditorTools.Setup.Step1
  ./scripts/unity.ps1 -Method ImagineVR.EditorTools.Setup.Step2

  # export the catalog for the server
  ./scripts/unity.ps1 -Method ImagineVR.EditorTools.Catalog.Export

  # render worlds to PNG files (needs a GPU; stays open until the pictures are saved)
  ./scripts/unity.ps1 -Method ImagineVR.EditorTools.Snapshot.Run -Graphics -NoQuit `
      -Extra '-snapScripts','shared/examples/golf-course.ndjson','-snapOut','client/Snapshots','-snapYaws','0,120,240'

  # build the Quest APK
  ./scripts/unity.ps1 -Method ImagineVR.EditorTools.Builder.BuildAndroid -Target Android -NoQuit -TimeoutSec 3600

.NOTES
  The editor version is read from client/ProjectSettings/ProjectVersion.txt.
  Unity Hub must be running (and signed in) so the editor can find its licence.
#>
param(
    [string]$Method = "",
    [string[]]$Extra = @(),
    [switch]$Graphics,
    [switch]$NoQuit,
    [string]$Target = "",
    [int]$TimeoutSec = 900,
    [string]$Show = ""
)

$repo = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repo "client"
$version = (Select-String -Path (Join-Path $project "ProjectSettings/ProjectVersion.txt") -Pattern '^m_EditorVersion:\s*(\S+)').Matches[0].Groups[1].Value
$unity = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
if (-not (Test-Path $unity)) { Write-Error "Unity $version is not installed at $unity"; exit 2 }

$log = Join-Path $env:TEMP ("imaginevr-unity-" + [Guid]::NewGuid().ToString('N').Substring(0, 8) + ".log")
$argList = @('-batchmode', '-projectPath', "`"$project`"", '-logFile', "`"$log`"")
if (-not $Graphics) { $argList += '-nographics' }
if ($Target) { $argList += @('-buildTarget', $Target) }
if ($Method) { $argList += @('-executeMethod', $Method) }
if (-not $NoQuit) { $argList += '-quit' }
foreach ($e in $Extra) {
    # paths are given relative to the repository root
    $value = $e
    if ($e -notmatch '^-' -and $e -match '[\\/]' -and -not [IO.Path]::IsPathRooted(($e -split ';')[0])) {
        $value = (($e -split ';') | ForEach-Object { Join-Path $repo $_ }) -join ';'
    }
    $argList += "`"$value`""
}

$started = Get-Date
$process = Start-Process -FilePath $unity -ArgumentList $argList -PassThru
if (-not $process.WaitForExit($TimeoutSec * 1000)) {
    Stop-Process -Id $process.Id -Force
    Write-Output "TIMED OUT after $TimeoutSec s (Unity was stopped)"
    $code = 124
} else {
    $code = $process.ExitCode
}
$seconds = [math]::Round(((Get-Date) - $started).TotalSeconds)
Write-Output "unity exit code $code after $seconds s"

if (Test-Path $log) {
    $pattern = 'error CS\d+|warning CS0618|Shader error|Shader warning|^ImagineVR|Snapshot:|Exception|NullReference|Assertion failed|Build (succeeded|failed|completed)|Error building|is not supported|Compilation failed'
    if ($Show) { $pattern += "|$Show" }
    Select-String -Path $log -Pattern $pattern |
        ForEach-Object { $_.Line.Trim() } |
        Where-Object { $_ -notmatch 'Licensing|UnityEditor\.EditorApplication|^\s*at ' } |
        Select-Object -Unique -First 60 |
        ForEach-Object { if ($_.Length -gt 300) { $_.Substring(0, 300) } else { $_ } }
    Write-Output "log: $log"
}
exit $code
