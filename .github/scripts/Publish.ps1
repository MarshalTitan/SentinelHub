$ErrorActionPreference = 'Stop'
$version = '0.1.0.0'
$tag = "v$version"
$repo = $env:GITHUB_REPOSITORY
./.github/scripts/Validate-Package.ps1 -PackagePath release/latest.zip -ExpectedVersion $version
# Existing assets are immutable. Never use upload --clobber.
gh release view $tag --repo $repo *> $null
$exists = $LASTEXITCODE -eq 0
$global:LASTEXITCODE = 0
if ($exists) { throw 'Release already exists; increment the version instead of replacing assets.' }
Copy-Item release/latest.zip release/SentinelHub.zip
gh release create $tag release/SentinelHub.zip --repo $repo --target $env:GITHUB_SHA --title "Sentinel Hub $version beta" --notes-file docs/RELEASE_NOTES.md --prerelease
if ($LASTEXITCODE -ne 0) { throw 'Release creation failed' }
$public = Join-Path $env:RUNNER_TEMP 'public-Hub.zip'
$url = "https://github.com/$repo/releases/download/$tag/SentinelHub.zip"
for ($attempt = 1; $attempt -le 12; $attempt++) {
 try { Invoke-WebRequest "${url}?sentinel=$env:GITHUB_RUN_ID-$attempt" -OutFile $public; break }
 catch { if ($attempt -eq 12) { throw }; Start-Sleep -Seconds 5 }
}
./.github/scripts/Validate-Package.ps1 -PackagePath $public -ExpectedVersion $version
if ((Get-FileHash $public).Hash -ne (Get-FileHash release/latest.zip).Hash) { throw 'Public ZIP differs from validated CI artifact' }
if (-not (Test-Path repo.json)) {
 $entry = [ordered]@{
  Author='MTitan'; Name='Sentinel Hub'; InternalName='SentinelHub'; AssemblyVersion=$version; TestingAssemblyVersion=$version
  Description='Optional ecosystem control center: inspect installed Sentinel plugins, dependencies and sanitized Hub diagnostics. Every consumer remains independent.'
  ApplicableVersion='any'; RepoUrl="https://github.com/$repo"; Tags=@('sentinel','utility','diagnostics')
  DalamudApiLevel=15; TestingDalamudApiLevel=15; LoadRequiredState=0; LoadSync=$true
  Punchline='Optional Sentinel ecosystem control center.'
  IconUrl="https://raw.githubusercontent.com/$repo/main/assets/icon.png"
  DownloadLinkInstall=$url; DownloadLinkUpdate=$url; DownloadLinkTesting=$url
  LastUpdate=[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
 }
 $json = ConvertTo-Json -InputObject @($entry) -Depth 10
 [IO.File]::WriteAllText((Join-Path $PWD 'repo.json'), $json + "`n")
 $request = @{ message="Bootstrap validated Sentinel Hub $version child manifest [skip ci]"; branch='main'; content=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($json + "`n")) } | ConvertTo-Json
 $request | gh api --method PUT "repos/$repo/contents/repo.json" --input -
 if ($LASTEXITCODE -ne 0) { throw 'First child manifest creation failed' }
}
