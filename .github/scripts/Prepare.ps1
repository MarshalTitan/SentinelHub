$ErrorActionPreference = 'Stop'
$dest = "$env:AppData\XIVLauncher\addon\Hooks\dev"
New-Item -ItemType Directory -Path $dest -Force | Out-Null
$zip = Join-Path $env:RUNNER_TEMP 'dalamud-latest.zip'
Invoke-WebRequest 'https://goatcorp.github.io/dalamud-distrib/latest.zip' -OutFile $zip
Expand-Archive -LiteralPath $zip -DestinationPath $dest -Force
New-Item -ItemType Directory -Path '.packages' -Force | Out-Null
$hashes = @{
 'MarshalTitan.SentinelCore.0.4.1.2.nupkg' = '82057104351fb05bbcbc579a62a3373ce7f17b46a9f39b06e872c72262c4f949'
 'MarshalTitan.SentinelCore.UI.0.4.1.2.nupkg' = 'ee2aec0d063eb1ee1da815de58c58bb2c8ff2199ce395c118c1c3976606a4c1d'
}
foreach ($name in $hashes.Keys) {
 $path = Join-Path '.packages' $name
 Invoke-WebRequest "https://github.com/MarshalTitan/SentinelCore/releases/download/v0.4.1.2/$name" -OutFile $path
 if ((Get-FileHash $path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $hashes[$name]) { throw 'Core package hash mismatch' }
}
$font = Join-Path $env:RUNNER_TEMP 'FontAwesomeFreeSolid.otf'
Invoke-WebRequest 'https://raw.githubusercontent.com/goatcorp/DalamudAssets/a26502fb3320b852c3f35c40770f945a8c9f3c50/UIRes/FontAwesomeFreeSolid.otf' -OutFile $font
if ((Get-FileHash $font -Algorithm SHA256).Hash.ToLowerInvariant() -ne '72c42629cf97c33c19dd910185ea6aa95f34f27fb95f84ed15fe74359f910aaa') { throw 'Icon fixture hash mismatch' }
"DALAMUD_ICON_FONT=$font" | Out-File $env:GITHUB_ENV -Append -Encoding utf8
