param([string]$Version='1.2.4',[string]$AppDirectory='artifacts/app')
$ErrorActionPreference='Stop'
if($Version -notmatch '^\d+\.\d+\.\d+$'){throw 'Versión no válida.'}
$root=Split-Path $PSScriptRoot -Parent
$app=Join-Path $root $AppDirectory
foreach($file in @('EditorPDF.exe','EditorPDF.dll','pdfium.dll','Source.zip','assets/NotoSans-Regular.ttf','LICENSE.txt','NOTICE.txt')){if(!(Test-Path -LiteralPath (Join-Path $app $file))){throw "Falta $file. Ejecuta Build.ps1 primero."}}
$output=Join-Path $root "artifacts/EditorPDF-$Version-win-x64.zip"
if(Test-Path -LiteralPath $output){throw 'El ZIP ya existe. Conserva las versiones publicadas; utiliza un destino o una versión nuevos.'}
Add-Type -AssemblyName System.IO.Compression
[System.IO.Compression.ZipFile]::CreateFromDirectory($app,$output,[System.IO.Compression.CompressionLevel]::Optimal,$false)
$hash=(Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash.ToLowerInvariant()
[System.IO.File]::WriteAllText($output+'.sha256',"$hash  $([System.IO.Path]::GetFileName($output))`n",[System.Text.UTF8Encoding]::new($false))
Write-Output $output
Write-Output "SHA-256: $hash"
