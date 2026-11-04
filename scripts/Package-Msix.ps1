param([Parameter(Mandatory)][string]$IdentityName,[Parameter(Mandatory)][string]$Publisher,[Parameter(Mandatory)][string]$PublisherDisplayName,[Parameter(Mandatory)][string]$MakeAppxPath)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(!(Test-Path -LiteralPath $MakeAppxPath)){throw 'Indica el MakeAppx.exe del Windows SDK instalado.'}
if(!(Test-Path -LiteralPath (Join-Path $root 'artifacts/app/EditorPDF.exe'))){throw 'Ejecuta Build.ps1 primero.'}
$stage=Join-Path $root ('artifacts/msix-stage-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $stage -Force | Out-Null
Copy-Item (Join-Path $root 'artifacts/app/*') $stage -Recurse
$template=Get-Content (Join-Path $root 'packaging/AppxManifest.template.xml') -Raw
$template=$template.Replace('__IDENTITY__',[System.Security.SecurityElement]::Escape($IdentityName)).Replace('__PUBLISHER__',[System.Security.SecurityElement]::Escape($Publisher)).Replace('__DISPLAY_PUBLISHER__',[System.Security.SecurityElement]::Escape($PublisherDisplayName))
Set-Content -LiteralPath (Join-Path $stage 'AppxManifest.xml') -Value $template -Encoding utf8
Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory (Join-Path $stage 'Assets') -Force | Out-Null
foreach($item in @(@('StoreLogo.png',50),@('Square44x44Logo.png',44),@('Square150x150Logo.png',150))){$size=[int]$item[1];$bmp=[System.Drawing.Bitmap]::new($size,$size);$g=[System.Drawing.Graphics]::FromImage($bmp);try{$g.Clear([System.Drawing.Color]::FromArgb(35,94,188));$g.SmoothingMode='AntiAlias';$g.FillRectangle([System.Drawing.Brushes]::White,[single]($size*.25),[single]($size*.17),[single]($size*.5),[single]($size*.66));$pen=[System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(35,94,188),[single]($size*.035));try{foreach($line in @(0.36,0.49,0.62)){$g.DrawLine($pen,[single]($size*.34),[single]($size*$line),[single]($size*.66),[single]($size*$line))}}finally{$pen.Dispose()};$bmp.Save((Join-Path $stage ('Assets/'+$item[0])),[System.Drawing.Imaging.ImageFormat]::Png)}finally{$g.Dispose();$bmp.Dispose()}}
$output=Join-Path $root ('artifacts/EditorPDF-0.1.1-'+[guid]::NewGuid().ToString('N').Substring(0,8)+'.msix')
& $MakeAppxPath pack /d $stage /p $output
if($LASTEXITCODE -ne 0){throw 'MakeAppx ha fallado.'}
Write-Output "MSIX sin firmar: $output. No se ha instalado ni publicado."
