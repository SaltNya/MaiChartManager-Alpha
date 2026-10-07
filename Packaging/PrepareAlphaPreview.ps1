param([Parameter(Mandatory=$true)][string]$MajdataApp)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$taskSource=[IO.Path]::GetFullPath($MajdataApp)
$taskPreview=Join-Path $taskRoot 'MaiChartManager/AlphaPreview'
$taskEditor=Join-Path $taskSource 'MajdataEdit/MajdataEdit.dll'
$taskViewer=Join-Path $taskSource 'MajdataView'
if(!(Test-Path -LiteralPath $taskViewer)){$taskViewer=Join-Path $taskSource 'MajdataViewApp'}
if(!(Test-Path -LiteralPath (Join-Path $taskViewer 'MajdataView.exe'))){throw 'MajdataView folder not found'}
if((Get-FileHash -LiteralPath $taskEditor).Hash -ne 'FCB8FC53547FACDB0498083D74FF6ABAF40896A26A854BEC20C11FD92D79A71B'){throw 'Unsupported MajdataEdit parser version'}
if((Get-FileHash -LiteralPath (Join-Path $taskViewer 'MajdataView_Data/Managed/Assembly-CSharp.dll')).Hash -ne 'E12CFF1CE8C4AC89693604248F443A9EFAA43F942DF9462CA5F2CD9531C59028'){throw 'Unsupported MajdataView version'}
New-Item -ItemType Directory -Path (Join-Path $taskPreview 'Viewer'),(Join-Path $taskPreview 'Parser'),(Join-Path $taskPreview 'Bridge') -Force|Out-Null
Copy-Item -Path (Join-Path $taskViewer '*') -Destination (Join-Path $taskPreview 'Viewer') -Recurse -Force
foreach($taskName in @('MajdataEdit.dll','Bass.Net.dll','DiscordRPC.dll','ICSharpCode.AvalonEdit.dll','Newtonsoft.Json.dll','TagLibSharp.dll','WPFLocalizeExtension.dll','XAMLMarkupExtensions.dll')){
 Copy-Item -LiteralPath (Join-Path (Join-Path $taskSource 'MajdataEdit') $taskName) -Destination (Join-Path $taskPreview 'Parser') -Force
}
$taskSfx=Join-Path $taskPreview 'SFX'
New-Item -ItemType Directory -Path $taskSfx -Force|Out-Null
foreach($taskName in @('answer','judge','judge_ex','slide','break','judge_break','touch','hanabi')) {
 Copy-Item -LiteralPath (Join-Path $taskSource ('MajdataEdit/SFX/'+$taskName+'.wav')) -Destination $taskSfx -Force
}
dotnet build (Join-Path $taskRoot 'SinmaiAlpha.PreviewPlayer/SinmaiAlpha.PreviewPlayer.csproj') -c Release
if($LASTEXITCODE -ne 0){throw 'Preview audio build failed'}
dotnet run --project (Join-Path $taskRoot 'SinmaiAlpha.PreviewPatch') -c Release -- (Join-Path $taskPreview 'Viewer/MajdataView_Data/Managed/Assembly-CSharp.dll') (Join-Path $taskRoot 'SinmaiAlpha.PreviewPlayer/bin/Release/netstandard2.1/SinmaiAlpha.PreviewPlayer.dll')
if($LASTEXITCODE -ne 0){throw 'Preview runtime patch failed'}
dotnet publish (Join-Path $taskRoot 'SinmaiAlpha.PreviewBridge/SinmaiAlpha.PreviewBridge.csproj') -c Release -r win-x64 --self-contained true -o (Join-Path $taskPreview 'Bridge')
if($LASTEXITCODE -ne 0){throw 'Preview parser bridge publish failed'}
$taskUiSource=Join-Path $PSScriptRoot 'StandardPreviewUI'
if(!(Test-Path -LiteralPath (Join-Path $taskUiSource 'controls.json'))){throw 'Original chart-browser UI resources not found'}
$taskUi=Join-Path $taskPreview 'UI'
New-Item -ItemType Directory -Path $taskUi -Force|Out-Null
Copy-Item -Path (Join-Path $taskUiSource '*') -Destination $taskUi -Recurse -Force
Write-Output ('Alpha preview prepared: '+$taskPreview)
