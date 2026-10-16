$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Path '.fixtures' -Force | Out-Null
Invoke-WebRequest 'https://raw.githubusercontent.com/MarshalTitan/SentinelHUD/c86e688d747757e5c6d50aa95a44ce12bed53776/src/SentinelHUD.Core/AntiAfkLease.cs' -OutFile '.fixtures/AntiAfkLease.cs'
$old = Join-Path $env:RUNNER_TEMP 'Hub-0.1-icon-audit.zip'
Invoke-WebRequest 'https://github.com/MarshalTitan/SentinelHub/releases/download/v0.1.0.0/SentinelHub.zip' -OutFile $old
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead($old)
try {
 $entry = $zip.Entries | Where-Object FullName -eq 'SentinelHub.json'
 $reader = [IO.StreamReader]::new($entry.Open())
 try { $manifest = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
 if ([string]::IsNullOrWhiteSpace($manifest.IconUrl)) { Write-Host 'ICON AUDIT: installed 0.1 manifest is missing IconUrl.' }
 else { Write-Host "ICON AUDIT: installed 0.1 IconUrl=$($manifest.IconUrl)" }
} finally { $zip.Dispose() }
$icon = Join-Path $env:RUNNER_TEMP 'public-Hub-icon.png'
Invoke-WebRequest 'https://raw.githubusercontent.com/MarshalTitan/SentinelHub/main/assets/icon.png' -OutFile $icon
$bytes = [IO.File]::ReadAllBytes($icon)
if ([Convert]::ToHexString([byte[]]$bytes[0..7]) -ne '89504E470D0A1A0A') { throw 'Public icon is not a valid PNG' }
Write-Host 'Verified public Hub icon PNG signature.'
