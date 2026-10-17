[CmdletBinding()]
param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'artifacts\release\win-x64'))
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
Push-Location $PSScriptRoot
try {
    dotnet restore WinmailExtractor.sln --locked-mode --disable-parallel -m:1 -nr:false
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
    dotnet build WinmailExtractor.sln -c Release --no-restore -m:1 -nr:false
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    dotnet test tests\WinmailExtractor.Tests -c Release --no-build --no-restore --logger 'trx;LogFileName=release-tests.trx' --results-directory artifacts\test-results
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
    # No cleanup of user paths. Refuse to publish into a nonempty directory.
    $target = [IO.Path]::GetFullPath($OutputDirectory)
    if ((Test-Path -LiteralPath $target) -and (Get-ChildItem -LiteralPath $target -Force | Select-Object -First 1)) {
        throw "Output directory is not empty. Choose a fresh -OutputDirectory: $target"
    }
    dotnet publish src\WinmailExtractor -c Release -r win-x64 --self-contained true -p:RuntimeFrameworkVersion=10.0.13 -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:PublishTrimmed=false -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true -p:NuGetLockFilePath=packages.publish.lock.json -p:RestoreLockedMode=true -o $target -m:1 -nr:false
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    $files = @(Get-ChildItem -LiteralPath $target -File -Recurse)
    if ($files.Count -ne 1 -or $files[0].Name -ne 'WinmailExtractor.exe') { throw 'Publish output is not a single EXE.' }
    $hash = Get-FileHash -LiteralPath $files[0].FullName -Algorithm SHA256
    Write-Host "Published: $($files[0].FullName)"
    Write-Host "SHA256: $($hash.Hash)"
}
finally { Pop-Location }
