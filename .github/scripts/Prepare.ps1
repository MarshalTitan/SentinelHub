$ErrorActionPreference = 'Stop'
$dest = "$env:AppData\XIVLauncher\addon\Hooks\dev"
New-Item -ItemType Directory -Path $dest -Force | Out-Null
$zip = Join-Path $env:RUNNER_TEMP 'dalamud-latest.zip'
Invoke-WebRequest 'https://goatcorp.github.io/dalamud-distrib/latest.zip' -OutFile $zip
Expand-Archive -LiteralPath $zip -DestinationPath $dest -Force
New-Item -ItemType Directory -Path '.packages' -Force | Out-Null
$hashes = @{
 'MarshalTitan.SentinelCore.0.4.1.nupkg' = '10b14a37577bd78b528d9bad03848b5cb5c3f6c3f4fa7dff4de966f8e5b34771'
 'MarshalTitan.SentinelCore.UI.0.4.1.nupkg' = '1ec7841311316994d4f71350b499c3f718d36179e1565f0d7316270063c6baea'
}
foreach ($name in $hashes.Keys) {
 $path = Join-Path '.packages' $name
 Invoke-WebRequest "https://github.com/MarshalTitan/SentinelCore/releases/download/v0.4.1.0/$name" -OutFile $path
 if ((Get-FileHash $path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $hashes[$name]) { throw 'Core package hash mismatch' }
}
$font = Join-Path $env:RUNNER_TEMP 'FontAwesomeFreeSolid.otf'
Invoke-WebRequest 'https://raw.githubusercontent.com/goatcorp/DalamudAssets/a26502fb3320b852c3f35c40770f945a8c9f3c50/UIRes/FontAwesomeFreeSolid.otf' -OutFile $font
if ((Get-FileHash $font -Algorithm SHA256).Hash.ToLowerInvariant() -ne '72c42629cf97c33c19dd910185ea6aa95f34f27fb95f84ed15fe74359f910aaa') { throw 'Icon fixture hash mismatch' }
"DALAMUD_ICON_FONT=$font" | Out-File $env:GITHUB_ENV -Append -Encoding utf8
