[CmdletBinding()]
param([string]$Executable = (Join-Path $PSScriptRoot '..\artifacts\release\win-x64\WinmailExtractor.exe'))
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$exe = [IO.Path]::GetFullPath($Executable)
$runDirectory = Join-Path $workspace ('artifacts\portable-smoke\日本語 & (portable)-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $runDirectory | Out-Null
$portableExe = Join-Path $runDirectory 'WinmailExtractor.exe'
Copy-Item -LiteralPath $exe -Destination $portableExe
$sample = Join-Path $workspace 'artifacts\verification-final\sample-winmail.dat'
$invalid = Join-Path $workspace 'artifacts\verification-final\invalid.dat'
if (!(Test-Path -LiteralPath $sample) -or !(Test-Path -LiteralPath $invalid)) { throw 'Run WinmailExtractor.Verify first.' }
$cases = @(
    @{ Name = 'no-arguments'; Args = @() },
    @{ Name = 'tnef-argument'; Args = @($sample) },
    @{ Name = 'invalid-dat'; Args = @($invalid) },
    @{ Name = 'missing-file'; Args = @((Join-Path $runDirectory 'missing.dat')) },
    @{ Name = 'multiple-arguments'; Args = @($sample, $invalid) }
)
$results = @()
foreach ($case in $cases) {
    $start = [Diagnostics.ProcessStartInfo]::new($portableExe)
    $start.UseShellExecute = $false
    $start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    $start.WorkingDirectory = $runDirectory
    $start.Environment['DOTNET_ROOT'] = (Join-Path $runDirectory 'no-runtime-installed')
    $start.Environment['DOTNET_ROOT_X64'] = $start.Environment['DOTNET_ROOT']
    $start.Environment['DOTNET_MULTILEVEL_LOOKUP'] = '0'
    $start.Environment['DOTNET_HOST_TRACE'] = '1'
    $trace = Join-Path $workspace ('artifacts\portable-smoke\' + $case.Name + '-host.txt')
    $start.Environment['DOTNET_HOST_TRACEFILE'] = $trace
    foreach ($argument in $case.Args) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($start)
    try {
        $idle = $process.WaitForInputIdle(20000)
        Start-Sleep -Milliseconds 750
        $alive = !$process.HasExited
        if (!$idle -or !$alive) { throw "Portable launch failed: $($case.Name)" }
        $results += [pscustomobject]@{ Case = $case.Name; InputIdle = $idle; Running = $alive; Trace = $trace }
    }
    finally {
        if (!$process.HasExited) {
            $closed = $process.CloseMainWindow()
            if (!$closed -or !$process.WaitForExit(5000)) { $process.Kill($true); $process.WaitForExit() }
        }
        $process.Dispose()
    }
}
$results | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $workspace 'artifacts\portable-smoke\results.json') -Encoding utf8
$results | Format-Table Case,InputIdle,Running
Write-Host "Isolated single EXE: $portableExe"
Write-Host 'This launch test does not verify Explorer integration or a genuinely runtime-free Windows installation.'
