param(
    [string]$Status = "Unknown"
)

# Use environment variables for paths
$programFiles = $env:ProgramFiles
$programData = $env:ProgramData

$logPath = Join-Path -Path $programFiles -ChildPath "RegistryMonitor\Logs"
$logFile = Join-Path -Path $logPath -ChildPath "netskope_status.log"

# Create log directory if it doesn't exist
if (!(Test-Path $logPath)) {
    New-Item -ItemType Directory -Path $logPath -Force | Out-Null
}

# Create NetSkopeStatus directory if it doesn't exist
$statusDir = Join-Path -Path $programData -ChildPath "NetSkopeStatus"
if (!(Test-Path $statusDir)) {
    New-Item -ItemType Directory -Path $statusDir -Force | Out-Null
}

# Log the event
$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
"[$timestamp] NetSkope NPA Tunnel status change detected" | Out-File -Append $logFile
"[$timestamp] Status received from service: $Status" | Out-File -Append $logFile

# Process the status value passed from service
$statusValue = $Status

# Take action based on connection status
switch ($statusValue) {
    "Connected" {
        "[$timestamp] NetSkope NPA Tunnel is now connected" | Out-File -Append $logFile
        "Connected" | Out-File -FilePath (Join-Path -Path $statusDir -ChildPath "status.txt") -Force
    }
    "Disconnected" {
        "[$timestamp] NetSkope NPA Tunnel is now disconnected" | Out-File -Append $logFile
        "Disconnected" | Out-File -FilePath (Join-Path -Path $statusDir -ChildPath "status.txt") -Force
    }
    "Error" {
        "[$timestamp] Error detected in NetSkope NPA Tunnel status" | Out-File -Append $logFile
        "Error" | Out-File -FilePath "$statusDir\status.txt" -Force
    }
    default {
        "[$timestamp] Unknown status: $statusValue" | Out-File -Append $logFile
        "Unknown" | Out-File -FilePath "$statusDir\status.txt" -Force
    }
}

# Optional: Run specific scripts based on status
try {
    if ($statusValue -eq "Connected") {
        # Example using environment variables for script paths
        # $connectedScript = Join-Path -Path $programFiles -ChildPath "RegistryMonitor\Scripts\OnConnected.ps1"
        # & $connectedScript
    }
    elseif ($statusValue -eq "Disconnected") {
        # Example using environment variables for script paths
        # $connectedScript = Join-Path -Path $programFiles -ChildPath "RegistryMonitor\Scripts\OnDisconnected.ps1"
        # & $disconnectedScript
    }
}
catch {
    "[$timestamp] Error executing status-specific script: $_" | Out-File -Append $logFile
}


# Log completion
"[$timestamp] Status change processing completed" | Out-File -Append $logFile