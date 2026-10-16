param([string]$PackagePath, [string]$ExpectedVersion)
$ErrorActionPreference = 'Stop'
if (-not $ExpectedVersion) { [xml]$project = Get-Content SentinelHub.csproj -Raw; $ExpectedVersion = [string]$project.Project.PropertyGroup.Version }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead((Resolve-Path $PackagePath))
try {
 $entries = @($archive.Entries)
 $names = @($entries | ForEach-Object { $_.FullName.Replace('\','/') })
 foreach ($name in @('SentinelHub.dll','SentinelHub.json','SentinelHub.deps.json','SentinelCore.dll','SentinelCore.UI.dll','assets/icon.png')) {
  if ($names -notcontains $name) { throw "Package missing $name" }
 }
 if (@($names | Select-Object -Unique).Count -ne $names.Count) { throw 'Duplicate archive paths' }
 if ($names | Where-Object { $_ -match '(^|/)(obj|bin)/|\.nupkg$|\.cs$|(^|/)\.\./' }) { throw 'Unexpected build/source content in package' }
 $manifest = $entries | Where-Object FullName -eq 'SentinelHub.json' | Select-Object -First 1
 $reader = [IO.StreamReader]::new($manifest.Open())
 try { $m = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
 if ($m.InternalName -ne 'SentinelHub' -or $m.AssemblyVersion -ne $ExpectedVersion -or $m.DalamudApiLevel -ne 15) { throw 'Manifest identity/version/API mismatch' }
 if ($m.IconUrl -ne 'https://raw.githubusercontent.com/MarshalTitan/SentinelHub/main/assets/icon.png') { throw 'Installed manifest is missing its correct IconUrl' }
 if ($m.RepoUrl -ne 'https://github.com/MarshalTitan/SentinelHub') { throw 'Installed manifest RepoUrl mismatch' }
 foreach ($consumer in @('SRankSentinel','PvPSentinel','SentinelHUD','ClassySentinel','SentinelProfiles','SentinelRelay','SentinelHunts')) {
  if ($names -contains "$consumer.dll") { throw 'Hub must not bundle or require consumer assemblies' }
 }
 foreach ($dll in @('SentinelCore','SentinelCore.UI')) {
  $pkg = [IO.Compression.ZipFile]::OpenRead((Resolve-Path ".packages/MarshalTitan.$dll.0.4.1.nupkg"))
  try {
   $source = @($pkg.Entries | Where-Object { $_.FullName -like "lib/*/$dll.dll" })
   if ($source.Count -ne 1) { throw 'Unexpected Core package library layout' }
   $actual = $entries | Where-Object FullName -eq "$dll.dll" | Select-Object -First 1
   $sha = [Security.Cryptography.SHA256]::Create()
   $stream = $source[0].Open(); try { $expected = [Convert]::ToHexString($sha.ComputeHash($stream)) } finally { $stream.Dispose() }
   $stream = $actual.Open(); try { $found = [Convert]::ToHexString($sha.ComputeHash($stream)) } finally { $stream.Dispose() }
   if ($expected -ne $found) { throw "Bundled $dll differs from published pinned package" }
  } finally { $pkg.Dispose() }
 }
} finally { $archive.Dispose() }
Write-Host 'Validated Hub identity, version, package paths and exact bundled Core libraries.'
