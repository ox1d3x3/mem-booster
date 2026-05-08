$ErrorActionPreference = 'SilentlyContinue'
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$outRoot = Join-Path $env:TEMP "Mem-Booster-diagnostics-$stamp"
$zip = Join-Path ([Environment]::GetFolderPath('Desktop')) "Mem-Booster-diagnostics-$stamp.zip"
$appData = Join-Path $env:APPDATA 'Mem-Booster'

Remove-Item $outRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $outRoot | Out-Null

$info = @()
$info += "Mem-Booster Diagnostics"
$info += "Collected: $(Get-Date -Format o)"
$info += "User: $env:USERNAME"
$info += "Computer: $env:COMPUTERNAME"
$info += "OS: $((Get-CimInstance Win32_OperatingSystem).Caption) $((Get-CimInstance Win32_OperatingSystem).Version)"
$info += "AppData: $appData"
$info | Set-Content -Path (Join-Path $outRoot 'app-info.txt') -Encoding UTF8

Get-Process | Select-Object Name,Id,SI,CPU,PM,WS,Path | Sort-Object Name | Export-Csv -NoTypeInformation -Path (Join-Path $outRoot 'running-processes.csv')

if (Test-Path $appData) {
    Get-ChildItem $appData -File -ErrorAction SilentlyContinue | Copy-Item -Destination $outRoot -Force
    if (Test-Path (Join-Path $appData 'logs')) {
        Copy-Item (Join-Path $appData 'logs') -Destination (Join-Path $outRoot 'logs') -Recurse -Force
    }
}

Compress-Archive -Path (Join-Path $outRoot '*') -DestinationPath $zip -Force
Remove-Item $outRoot -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "Diagnostics ZIP created: $zip"
explorer.exe /select,"$zip"
