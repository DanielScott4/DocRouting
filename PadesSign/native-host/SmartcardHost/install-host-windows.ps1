# Run as Administrator to register the native messaging host in the registry.
$manifestPath = (Resolve-Path "manifest.json").Path
$regKey = "HKCU:\Software\Google\Chrome\NativeMessagingHosts\com.yourorg.padessign"
New-Item -Force -Path $regKey | Out-Null
Set-ItemProperty -Path $regKey -Name "(Default)" -Value $manifestPath
Write-Host "Native messaging host registered: $manifestPath"