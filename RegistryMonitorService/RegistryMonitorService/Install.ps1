[CmdletBinding()]
param(
    [Parameter(Mandatory=$false)]
    [string]$InstallPath = $env:ProgramFiles
)

# Define paths using environment variables
$serviceName = "RegistryMonitorService"
$serviceDisplayName = "Registry Monitor Service"
$serviceDescription = "Monitors NetSkope NPA Tunnel registry changes"

$baseDir = Join-Path -Path $InstallPath -ChildPath "RegistryMonitor"
$scriptsDir = Join-Path -Path $baseDir -ChildPath "Scripts"
$logsDir = Join-Path -Path $baseDir -ChildPath "Logs"
$serviceExePath = Join-Path -Path $baseDir -ChildPath "$serviceName.exe"

# Create directories
Write-Host "Creating installation directories..."
New-Item -ItemType Directory -Force -Path $baseDir | Out-Null
New-Item -ItemType Directory -Force -Path $scriptsDir | Out-Null
New-Item -ItemType Directory -Force -Path $logsDir | Out-Null

# Create NetSkopeStatus directory in ProgramData
$statusDir = Join-Path -Path $env:ProgramData -ChildPath "NetSkopeStatus"
New-Item -ItemType Directory -Force -Path $statusDir | Out-Null

# Copy files
Write-Host "Copying service executable..."
Copy-Item -Path "$PSScriptRoot\$serviceName.exe" -Destination $serviceExePath -Force

Write-Host "Copying PowerShell scripts..."
Copy-Item -Path "$PSScriptRoot\ChangeHandler.ps1" -Destination $scriptsDir -Force

# Create event log source if it doesn't exist
if (-not [System.Diagnostics.EventLog]::SourceExists($serviceDisplayName)) {
    Write-Host "Creating event log source..."
    New-EventLog -LogName Application -Source $serviceDisplayName
}

# Stop and remove the service if it exists
$existingService = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($existingService) {
    Write-Host "Stopping and removing existing service..."
    Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    sc.exe delete $serviceName
    Start-Sleep -Seconds 2
}

# Install the service
Write-Host "Installing service..."
sc.exe create $serviceName binPath= `"$serviceExePath`" start= auto DisplayName= `"$serviceDisplayName`"
sc.exe description $serviceName `"$serviceDescription`"

# Set recovery options (restart on failure)
Write-Host "Configuring service recovery options..."
sc.exe failure $serviceName reset= 86400 actions= restart/60000/restart/60000/restart/60000

# Start the service
Write-Host "Starting service..."
Start-Service -Name $serviceName

Write-Host "Installation completed successfully."