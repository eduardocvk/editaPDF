param([Parameter(Mandatory)][string]$IdentityName,[Parameter(Mandatory)][string]$Publisher,[Parameter(Mandatory)][string]$PublisherDisplayName,[Parameter(Mandatory)][string]$MakeAppxPath,[string]$AppDirectory='artifacts/app')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$app=Join-Path $root $AppDirectory
if($IdentityName -notmatch '^[A-Za-z0-9.-]{3,50}$' -or !$Publisher.StartsWith('CN=') -or [string]::IsNullOrWhiteSpace($PublisherDisplayName)){throw 'Identidad de Store incompleta o inválida.'}
if(!(Test-Path -LiteralPath $MakeAppxPath)){throw 'Indica el MakeAppx.exe del Windows SDK instalado.'}
if(!(Test-Path -LiteralPath (Join-Path $app 'EditorPDF.exe'))){throw 'Ejecuta Build.ps1 primero.'}
$stage=Join-Path $root ('artifacts/msix-stage-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $stage -Force | Out-Null
Copy-Item (Join-Path $app '*') $stage -Recurse
$template=Get-Content (Join-Path $root 'packaging/AppxManifest.template.xml') -Raw
$template=$template.Replace('__IDENTITY__',[System.Security.SecurityElement]::Escape($IdentityName)).Replace('__PUBLISHER__',[System.Security.SecurityElement]::Escape($Publisher)).Replace('__DISPLAY_PUBLISHER__',[System.Security.SecurityElement]::Escape($PublisherDisplayName))
Set-Content -LiteralPath (Join-Path $stage 'AppxManifest.xml') -Value $template -Encoding utf8
New-Item -ItemType Directory (Join-Path $stage 'Assets') -Force | Out-Null
foreach($name in @('StoreLogo.png','Square44x44Logo.png','Square150x150Logo.png')){Copy-Item -LiteralPath (Join-Path $root ('assets/'+$name)) -Destination (Join-Path $stage ('Assets/'+$name))}
$output=Join-Path $root 'artifacts/EditaPDF-1.2.4-x64.msix'
if(Test-Path -LiteralPath $output){throw 'Ya existe el paquete. No se reemplazan artefactos anteriores.'}
& $MakeAppxPath pack /d $stage /p $output
if($LASTEXITCODE -ne 0){throw 'MakeAppx ha fallado.'}
$hash=(Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText($output+'.sha256',"$hash  $([IO.Path]::GetFileName($output))`n")
Write-Output "MSIX sin firmar: $output. No se ha instalado ni publicado."
