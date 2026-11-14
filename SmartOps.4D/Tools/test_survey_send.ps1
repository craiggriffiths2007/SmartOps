param(
    [string]$FourDPath = 'D:\4D\4D 21 R4\4D\4D.exe',
    [ValidateSet('SurveySend', 'Booking', 'FittingRecovery', 'CompanyCode', 'Reservations', 'WebBooking', 'ErrorLog', 'FollowOnSurvey', 'AppVersion', 'MediaOverwrite', 'AgentToken', 'QuestionDraft', 'SurveyReopen', 'AccountSettings')][string]$Suite = 'SurveySend',
    [string]$TestDirectory = (Join-Path ([IO.Path]::GetTempPath()) ('SmartOps-survey-test-' + [Guid]::NewGuid().ToString('N')))
)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $FourDPath -PathType Leaf)) { throw "4D executable not found: $FourDPath" }
if (Test-Path -LiteralPath $TestDirectory) { throw 'TestDirectory must be a new directory; never use an existing project or data directory.' }
$projectRoot = Split-Path -Parent $PSScriptRoot
New-Item -ItemType Directory -Path $TestDirectory | Out-Null
New-Item -ItemType Directory -Path (Join-Path $TestDirectory 'Project') | Out-Null
# Copy source/resources only. Never open or copy the running data/media files.
Copy-Item -LiteralPath (Join-Path $projectRoot 'Project\Sources') -Destination (Join-Path $TestDirectory 'Project') -Recurse
Copy-Item -LiteralPath (Join-Path $projectRoot 'Project\smartops4d.4DProject') -Destination (Join-Path $TestDirectory 'Project')
Copy-Item -LiteralPath (Join-Path $projectRoot 'Resources') -Destination $TestDirectory -Recurse
$methodName = 'GO_Test' + $Suite
$resultName = switch ($Suite) {
    'AccountSettings' { 'account-settings-test-result.json' }
    'SurveyReopen' { 'survey-reopen-test-result.json' }
    'QuestionDraft' { 'question-draft-test-result.json' }
    'AgentToken' { 'agent-token-test-result.json' }
    'MediaOverwrite' { 'media-overwrite-test-result.json' }
    'AppVersion' { 'app-version-test-result.json' }
    'FollowOnSurvey' { 'follow-on-survey-test-result.json' }
    'ErrorLog' { 'error-log-test-result.json' }
    'WebBooking' { 'web-booking-test-result.json' }
    'Reservations' { 'reservations-test-result.json' }
    'CompanyCode' { 'company-code-test-result.json' }
    'Booking' { 'booking-test-result.json' }
    'FittingRecovery' { 'fitting-recovery-test-result.json' }
    default { 'survey-test-result.json' }
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('Tests\' + $methodName + '.4dm')) -Destination (Join-Path $TestDirectory 'Project\Sources\Methods')
$arguments = @(
    '--project', ('"' + (Join-Path $TestDirectory 'Project\smartops4d.4DProject') + '"'),
    '--data', ('"' + (Join-Path $TestDirectory 'test.4DD') + '"'),
    '--create-data', '--headless', '--skip-onstartup', '--startup-method', $methodName
)
$process = Start-Process -FilePath $FourDPath -ArgumentList $arguments -WindowStyle Hidden -PassThru `
    -RedirectStandardOutput (Join-Path $TestDirectory 'stdout.txt') -RedirectStandardError (Join-Path $TestDirectory 'stderr.txt')
if (-not $process.WaitForExit(60000)) {
    Stop-Process -Id $process.Id
    throw "Isolated 4D test timed out. Inspect $TestDirectory"
}
$resultPath = Join-Path $TestDirectory $resultName
if (-not (Test-Path -LiteralPath $resultPath)) { throw "4D did not produce test results. Inspect $TestDirectory" }
$result = Get-Content -Raw -LiteralPath $resultPath | ConvertFrom-Json
$result | ConvertTo-Json -Depth 12
if (-not $result.success) { throw "Native 4D $Suite tests failed. Inspect $resultPath" }
Write-Output "PASS: native 4D $Suite tests. Test files retained at $TestDirectory"
