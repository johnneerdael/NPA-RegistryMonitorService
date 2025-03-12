# Registry Monitor Service

A Windows service that monitors NetSkope NPA Tunnel registry changes and responds to them accordingly.

## Overview

Registry Monitor Service watches for changes to specific registry keys related to NetSkope NPA Tunnel and executes a PowerShell script to handle these changes.

## Prerequisites

- Windows OS (Windows 10/11 or Windows Server 2016+)
- .NET Framework 4.7.2 or higher
- PowerShell 5.1 or higher
- Administrator rights for installation

## Manual Installation

1. Download the latest release
2. Extract the ZIP file to a temporary location
3. Open PowerShell as Administrator
4. Navigate to the extracted folder
5. Run the installation script:
```powershell
.\Install.ps1
```

By default, this installs the service to %ProgramFiles%\RegistryMonitor\. To specify a custom install path:
```powershell
.\Install.ps1 -InstallPath "C:\CustomPath"
```

## Uninstallation

To uninstall the service manually:

1. Open PowerShell as Administrator
2. Stop and remove the service:
```powershell
Stop-Service -Name RegistryMonitorService -Force
sc.exe delete RegistryMonitorService
Remove-Item -Path "$env:ProgramFiles\RegistryMonitor" -Recurse -Force
```

## Custom Action Scripts

The service can execute custom scripts when the NetSkope NPA Tunnel status changes. This is useful for performing actions like restarting network services when the connection status changes.

### Setting Up Custom Scripts

1. Create a script file in the %ProgramFiles%\RegistryMonitor\Scripts\ directory (created during installation)
2. Edit the ChangeHandler.ps1 file to uncomment and modify the script execution sections

### Example: Restart NLA Service When Connection Changes

1. Create a script to restart the Network Location Awareness service when disconnected:
```powershell
# filepath: %ProgramFiles%\RegistryMonitor\Scripts\OnDisconnected.ps1
# Log start of script execution
$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
$logFile = Join-Path -Path $env:ProgramFiles -ChildPath "RegistryMonitor\Logs\nla_restart.log"
"[$timestamp] Restarting NLA service after NetSkope disconnection..." | Out-File -Append $logFile

try {
    # Wait a few seconds to ensure network transition is complete
    Start-Sleep -Seconds 5
    
    # Restart the Network Location Awareness service
    Restart-Service -Name "NlaSvc" -Force
    
    "[$timestamp] NLA service restarted successfully" | Out-File -Append $logFile
} catch {
    "[$timestamp] Error restarting NLA service: $_" | Out-File -Append $logFile
}
```
2. Modify the ChangeHandler.ps1 file to execute your custom script:
```powershell
# Find this section in ChangeHandler.ps1 and modify as shown:
# Optional: Run specific scripts based on status
try {
    if ($statusValue -eq "Connected") {
        # Example using environment variables for script paths
        # $connectedScript = Join-Path -Path $programFiles -ChildPath "RegistryMonitor\Scripts\OnConnected.ps1"
        # & $connectedScript
    }
    elseif ($statusValue -eq "Disconnected") {
        # Execute the NLA restart script when disconnected
        $disconnectedScript = Join-Path -Path $programFiles -ChildPath "RegistryMonitor\Scripts\OnDisconnected.ps1"
        & $disconnectedScript
    }
}
catch {
    "[$timestamp] Error executing status-specific script: $_" | Out-File -Append $logFile
}
```

### Other Custom Script Examples
You can create various scripts to handle network changes:
- Clear DNS cache: ipconfig /flushdns
- Reset network adapter: Restart-NetAdapter -Name "Ethernet"
- Update proxy settings
- Notify users via pop-up messages

## Deployment via MDM Systems

### Microsoft Intune

1. Create a Win32 App:
  - Use the (Microsoft Win32 Content Prep Tool[https://github.com/Microsoft/Microsoft-Win32-Content-Prep-Tool]) to create an .intunewin file:
  ```powershell
  IntuneWinAppUtil.exe -c "<extracted_folder_path>" -s "Install.ps1" -o "<output_folder_path>"
  ```
2. Add to Intune:
  - In Intune portal, go to Apps > All Apps > Add
  - Select Windows app (Win32) as the app type
  - Upload the .intunewin file
  - Fill in app information (name, description, publisher)
3. Installation Command:
```powershell
powershell.exe -ExecutionPolicy Bypass -File Install.ps1
```
4. Uninstallation Command:
```powershell
powershell.exe -ExecutionPolicy Bypass -Command "Stop-Service -Name RegistryMonitorService -Force; sc.exe delete RegistryMonitorService; Remove-Item -Path '$env:ProgramFiles\RegistryMonitor' -Recurse -Force"
```
5. Detection Rule:
- Set up a detection rule using the registry or file path:
  - Rule type: File
  - Path: %ProgramFiles%\RegistryMonitor
  - File or folder: RegistryMonitorService.exe
  - Detection method: File or folder exists

### JAMF Pro

1. Package Creation:
  - Create a ZIP archive containing all files
  - Upload to JAMF Admin as a package
2. Create a Policy:
  - Go to Computers > Policies > New
  - Configure the general settings
  - Under Packages, add your uploaded package
  - Add a script with these commands:
  ```powershell
  cd /tmp/RegistryMonitorService
  powershell.exe -ExecutionPolicy Bypass -File Install.ps1
  ```
3. Scoping:
  - Scope the policy to appropriate computers
  - Set execution frequency (typically "Once per computer")

### System Center Configuration Manager (SCCM)

1. Create Application:
  - In the SCCM console, go to Software Library > Application Management > Applications
  - Click Create Application
  - Choose Manually specify the application information
2. Deployment Type:
  - Create a Script Installer deployment type
  - Installation program:
  ```powershell
  powershell.exe -ExecutionPolicy Bypass -File ".\Install.ps1"
  ```
  - Uninstall program:
  ```powershell
  powershell.exe -ExecutionPolicy Bypass -Command "Stop-Service -Name RegistryMonitorService -Force; sc.exe delete RegistryMonitorService; Remove-Item -Path "$env:ProgramFiles\RegistryMonitor" -Recurse -Force"
  ```
3. Detection Method:
  - Configure a detection method to look for the service installation:
    - Setting Type: File
    - Path: %ProgramFiles%\RegistryMonitor
    - File or folder: RegistryMonitorService.exe
    - Detection method: File or folder exists

### AirWatch/Workspace ONE

1. Create a Package:
  - Package all files into a ZIP archive
2. Upload to Console:
  - Go to Apps & Books > Applications > Native > Add Application
  - Select Windows as the platform
3. Configure Install Commands:
  - Install Command:
  ```powershell
  powershell.exe -ExecutionPolicy Bypass -File "%~dp0Install.ps1"
  ```
  - Uninstall Command:
  ```powershell
  powershell.exe -ExecutionPolicy Bypass -Command "Stop-Service -Name RegistryMonitorService -Force; sc.exe delete RegistryMonitorService; Remove-Item -Path \"$env:ProgramFiles\RegistryMonitor\" -Recurse -Force"
  ```

## Kandji

1. Create Package:
  - Package all files into a ZIP archive containing the Install.ps1 script and all required files
2. Upload to Kandji:
  - In the Kandji web console, navigate to Library > Custom Apps
  - Click + Add App
  - Select Windows as the platform
  - Upload the ZIP package
  - Complete the app metadata (name, description, etc.)
3. Configure Installation:
  - Install Command:
  ```powershell
  powershell.exe -ExecutionPolicy Bypass -File "Install.ps1"
  ```
  - Uninstall Command:
  ```powershell
  powershell.exe -ExecutionPolicy Bypass -Command "Stop-Service -Name RegistryMonitorService -Force; sc.exe delete RegistryMonitorService; Remove-Item -Path \"$env:ProgramFiles\RegistryMonitor\" -Recurse -Force"
  ```
4. Configure Detection:
  - Detection Type: File Exists
  - Path: %ProgramFiles%\RegistryMonitor\RegistryMonitorService.exe
5. Deployment:
  - Add the app to the appropriate Blueprint(s)
  - Choose deployment settings (Auto-install or Self Service)
  - Save and publish changes to devices

## Troubleshooting
- Check the Windows Event Viewer under Application logs for events from "Registry Monitor Service"
- Logs are stored in %ProgramFiles%\RegistryMonitor\Logs\
- For installation issues, run the Install.ps1 script with the -Verbose parameter
- Common issues:
  - Service fails to start: Check if .NET Framework is installed and updated
  - Access denied errors: Ensure you're running installation with admin rights
  - Script execution policy: If scripts fail to run, temporarily set execution policy with 
  ```powershell
  Set-ExecutionPolicy -ExecutionPolicy Bypass -Scope Process
  ```


## License
### MIT License

Copyright (c) 2025

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

### Support
For support, please open an issue in the GitHub repository.