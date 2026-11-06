param([string]$DotnetPath='dotnet',[string]$PythonPath='python')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE='1'
$run=Join-Path $root ('artifacts/tests-'+(Get-Date -Format yyyyMMdd-HHmmss)+'-'+[guid]::NewGuid().ToString('N').Substring(0,6))
function Check {if($LASTEXITCODE -ne 0){throw "Comprobación fallida: $LASTEXITCODE"}}
Push-Location $root
try{
 & $PythonPath scripts/fixtures.py (Join-Path $run 'fixtures');Check
 & $PythonPath scripts/inspect-native.py vendor/bin/pdfium.dll;Check
 & $DotnetPath publish tests/EditorPdf.Tests -c Release -r win-x64 --self-contained true -p:RestoreLockedMode=true -o artifacts/test-engine;Check
 & $DotnetPath publish tests/EditorPdf.UiTests -c Release -r win-x64 --self-contained true -p:RestoreLockedMode=true -o artifacts/test-ui;Check
 & $DotnetPath publish tests/NetworkGuard -c Release -r win-x64 --self-contained true -p:RestoreLockedMode=true -o artifacts/network-guard;Check
 $guard=Join-Path $root 'artifacts/network-guard/NetworkGuard.exe'
 & $guard $guard --canary-child
 if($LASTEXITCODE -ne 2){throw 'Falló el control positivo de red.'}
 & $guard (Join-Path $root 'artifacts/test-engine/EditorPdf.Tests.exe') $run;Check
 & $PythonPath scripts/verify-pdfs.py $run;Check
 & $guard (Join-Path $root 'artifacts/test-ui/EditorPdf.UiTests.exe') (Join-Path $run 'ui') (Join-Path $run 'fixtures/externo.pdf');Check
 & $PythonPath scripts/verify-pdfs.py --composition (Join-Path $run 'ui/ui-merge-composer.pdf');Check
 & $PythonPath scripts/verify-pdfs.py --reduction (Join-Path $run 'fixtures/escaneo.pdf') (Join-Path $run 'ui/ui-reduced.pdf');Check
 Write-Output "Pruebas y capturas: $run"
}finally{Pop-Location}
