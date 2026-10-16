$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Path '.fixtures' -Force | Out-Null
Invoke-WebRequest 'https://raw.githubusercontent.com/MarshalTitan/SentinelHUD/bcc21e44c83fac60c14b1318eddc0a58d7ab4bf9/src/SentinelHUD.Core/QuestConvenienceHandoff.cs' -OutFile '.fixtures/QuestConvenienceHandoff.cs'
$baseline = '758068f68a76d72660fb6e2740f22d592130fdd7'
foreach ($name in @('QuestConvenienceService','CutsceneSkipConfirmationPolicy','QuestRewardSelectionPolicy')) {
 $sourcePath = if ($name -eq 'QuestConvenienceService') { 'src/SentinelHUD/Services' } else { 'src/SentinelHUD.Core' }
 $target = Join-Path $env:RUNNER_TEMP "$name-accepted.cs"
 Invoke-WebRequest "https://raw.githubusercontent.com/MarshalTitan/SentinelHUD/$baseline/$sourcePath/$name.cs" -OutFile $target
 $expected = (Get-Content $target -Raw).Replace("`r`n","`n")
 if ($name -eq 'QuestConvenienceService') {
  $expected = $expected.Replace("using SentinelHUD.Core;`n",'').Replace('namespace SentinelHUD.Services;','namespace SentinelHub.Utilities.Quest;').Replace('ConvenienceConfiguration','QuestConvenienceOptions')
 } else { $expected = $expected.Replace('namespace SentinelHUD.Core;','namespace SentinelHub.Utilities.Quest;') }
 $actual = (Get-Content "Utilities/Quest/$name.cs" -Raw).Replace("`r`n","`n")
 if ($actual.Trim() -cne $expected.Trim()) { throw "Accepted native/policy source changed unexpectedly: $name" }
}
Write-Host 'PASS exact accepted native dialogue/cutscene/reward source and policies; namespace/config adapter only.'
