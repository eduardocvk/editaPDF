param([string]$DotnetPath='dotnet',[switch]$Offline,[string]$AppDirectory='artifacts/app')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$app=Join-Path $root $AppDirectory
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE='1'
$lock=Get-Content (Join-Path $root 'vendor/dependencies.lock.json') -Raw | ConvertFrom-Json
foreach($entry in @(@('vendor/bin/pdfium.dll',$lock.pdfium.dllSha256),@('assets/NotoSans-Regular.ttf',$lock.font.sha256))){if((Get-FileHash -LiteralPath (Join-Path $root $entry[0]) -Algorithm SHA256).Hash -ne $entry[1]){throw "Dependencia modificada: $($entry[0])"}}
Push-Location $root
try{
 $arguments=@('publish','src/EditorPdf.App','-c','Release','-r','win-x64','--self-contained','true','-p:RestoreLockedMode=true','-p:DebugType=None','-p:DebugSymbols=false','-o',$app)
 if($Offline){$arguments+=@('--source',(Join-Path $root 'vendor/nuget-empty'));New-Item -ItemType Directory (Join-Path $root 'vendor/nuget-empty') -Force | Out-Null}
 & $DotnetPath @arguments
 if($LASTEXITCODE -ne 0){throw 'La compilación ha fallado.'}
 Copy-Item README.md -Destination $app
 New-Item -ItemType Directory (Join-Path $app 'docs') -Force | Out-Null
 Copy-Item docs/*.md -Destination (Join-Path $app 'docs')
 Add-Type -AssemblyName System.IO.Compression
 $sourceZip=Join-Path $app 'Source.zip'
 $zipStream=[System.IO.File]::Open($sourceZip,[System.IO.FileMode]::Create)
 $zip=[System.IO.Compression.ZipArchive]::new($zipStream,[System.IO.Compression.ZipArchiveMode]::Create)
 try{
  $files=@(Get-ChildItem -LiteralPath $root -File | Where-Object Name -NotIn @('.DS_Store'))
  foreach($directory in @('src','tests','scripts','packaging','assets','vendor','licenses','docs')){$files+=Get-ChildItem -LiteralPath (Join-Path $root $directory) -File -Recurse | Where-Object {$_.FullName -notmatch '[\\/](bin|obj)[\\/]' -or $_.FullName -match '[\\/]vendor[\\/]bin[\\/]'}}
  foreach($file in $files){if($file.Extension -eq '.tgz'){continue};$relative=[System.IO.Path]::GetRelativePath($root,$file.FullName).Replace('\','/');[System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$file.FullName,$relative,[System.IO.Compression.CompressionLevel]::Optimal)|Out-Null}
 }finally{$zip.Dispose()}
 Write-Output (Join-Path $app 'EditorPDF.exe')
}finally{Pop-Location}
