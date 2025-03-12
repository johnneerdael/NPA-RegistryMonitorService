$serviceName = "RegistryMonitorService"
$installDir = Join-Path -Path $env:ProgramFiles -ChildPath "RegistryMonitor"

$serviceExists = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
$dirExists = Test-Path $installDir

if ($serviceExists -and $dirExists) {
    Write-Output "Registry Monitor Service is installed."
    exit 0
} else {
    Write-Output "Registry Monitor Service is not installed."
    exit 1
}