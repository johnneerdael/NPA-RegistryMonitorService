using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;  // Add this for Task
using System.Runtime.InteropServices;
using System.Net.NetworkInformation;
using System.Management; // Add reference to System.Management.dll in your project

namespace RegistryMonitorService
{
    public partial class RegistryMonitorService : ServiceBase
    {
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern int RegNotifyChangeKeyValue(
            IntPtr hKey,
            bool bWatchSubtree,
            RegistryNotifyFilter dwNotifyFilter,
            IntPtr hEvent,
            bool fAsynchronous);

        [Flags]
        private enum RegistryNotifyFilter
        {
            Name = 1,
            Attributes = 2,
            LastSet = 4,
            Security = 8
        }


        private Thread monitorThread;
        private bool stopMonitoring = false;
        private const string RegPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\NetSkope\NpaTunnel";
        private const string RegValue = "NpaStatus";
        private const int RetryIntervalSeconds = 30;

        // Use environment variables for paths
        private readonly string scriptPath;

        public RegistryMonitorService()
        {
            InitializeComponent();

            // Get program files path using environment variables
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            scriptPath = Path.Combine(programFiles, "RegistryMonitor", "Scripts", "ChangeHandler.ps1");
        }

        protected override void OnStart(string[] args)
        {
            stopMonitoring = false;
            monitorThread = new Thread(new ThreadStart(MonitorRegistry));
            monitorThread.Start();
        }

        protected override void OnStop()
        {
            stopMonitoring = true;
            monitorThread.Join(1000);
        }

        private void MonitorRegistry()
        {
            EventLog.WriteEntry("Registry Monitor Service", "Starting NetSkope NPA Tunnel status monitoring", EventLogEntryType.Information);

            while (!stopMonitoring)
            {
                RegistryKey regKey = null;
                try
                {
                    // Use the direct approach that we know works
                    string subKeyPath = @"SOFTWARE\NetSkope\NpaTunnel";
                    
                    // Use RegistryKey.OpenBaseKey with specific view
                    RegistryKey baseKey = RegistryKey.OpenBaseKey(
                        RegistryHive.LocalMachine, 
                        Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Default);
                    regKey = baseKey.OpenSubKey(subKeyPath, false);
                    
                    if (regKey == null)
                    {
                        EventLog.WriteEntry("Registry Monitor Service",
                            $"Registry key not found: {subKeyPath}. Retrying in {RetryIntervalSeconds} seconds.",
                            EventLogEntryType.Warning);

                        // Wait and retry
                        Thread.Sleep(RetryIntervalSeconds * 1000);
                        continue;
                    }

                    // Wait for registry changes
                    RegNotifyChangeKeyValue(
                        regKey.Handle.DangerousGetHandle(),
                        true,
                        RegistryNotifyFilter.Name | RegistryNotifyFilter.LastSet,
                        IntPtr.Zero,
                        false);

                    // Read the current value after change is detected
                    string currentStatus = string.Empty;
                    try
                    {
                        currentStatus = regKey.GetValue(RegValue)?.ToString() ?? "Unknown";
                        EventLog.WriteEntry("Registry Monitor Service",
                            $"Registry change detected. Current status: {currentStatus}",
                            EventLogEntryType.Information);
                        
                        // Trigger NLA reevaluation directly from the service
                        TriggerNetworkReevaluation(currentStatus);  // KEEP THIS ONE
                    }
                    catch (Exception)  // Remove 'ex' if not using it/ Remove 'ex' if not using it
                    {
                        // Error handling
                    }

                    // Execute the PowerShell script
                    try
                    {
                        // PowerShell script execution remains intact
                        ProcessStartInfo startInfo = new ProcessStartInfo
                        {
                            FileName = "powershell.exe",
                            Arguments = $"-ExecutionPolicy Bypass -File \"{scriptPath}\" -Status \"{currentStatus}\"",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };

                        using (Process process = new Process())
                        {
                            process.StartInfo = startInfo;
                            process.StartInfo.RedirectStandardOutput = true;
                            process.StartInfo.RedirectStandardError = true;
                            process.OutputDataReceived += (sender, e) => 
                            {
                                if (!string.IsNullOrEmpty(e.Data))
                                    EventLog.WriteEntry("Registry Monitor Service", $"Script output: {e.Data}", EventLogEntryType.Information);
                            };
                            process.ErrorDataReceived += (sender, e) => 
                            {
                                if (!string.IsNullOrEmpty(e.Data))
                                    EventLog.WriteEntry("Registry Monitor Service", $"Script error: {e.Data}", EventLogEntryType.Error);
                            };
                            process.Start();
                            process.BeginOutputReadLine();
                            process.BeginErrorReadLine();
                            process.WaitForExit();
                        }
                    }
                    catch (Exception)  // Remove 'ex' if not using it/ Remove 'ex' if not using it
                    {
                        // Error handling
                    }
                }
                catch (Exception ex)
                {
                    EventLog.WriteEntry("Registry Monitor Service",
                        $"Error monitoring registry: {ex.Message}. Retrying in {RetryIntervalSeconds} seconds.",
                        EventLogEntryType.Error);
                    Thread.Sleep(RetryIntervalSeconds * 1000);
                }
                finally
                {
                    regKey?.Close();
                }
            }

            EventLog.WriteEntry("Registry Monitor Service", "NetSkope NPA Tunnel status monitoring stopped", EventLogEntryType.Information);
        }

        // Add these methods to the RegistryMonitorService class
        public void TestStartupAndStop(string[] args)
        {
            OnStart(args);
        }

        public void TestStop()
        {
            OnStop();
        }

        // Add this method to your RegistryMonitorService class
        private void TriggerNetworkReevaluation(string currentStatus)
        {
            try
            {
                // Only trigger NLA reevaluation when status changes to specific values
                if (currentStatus == "Disconnected" || currentStatus == "Connected")
                {
                    EventLog.WriteEntry("Registry Monitor Service",
                        $"Triggering NLA reevaluation due to '{currentStatus}' status change",
                        EventLogEntryType.Information);

                    // Add detection for Windows versions
                    Version osVersion = Environment.OSVersion.Version;
                    bool isWindows10OrNewer = (osVersion.Major >= 10);
                    
                    if (isWindows10OrNewer)
                    {
                        // For Windows 10/11, we can also try the NetworkInformation API first
                        // as it's more reliable on newer Windows versions
                        try
                        {
                            // This forces a refresh of network information
                            var profiles = NetworkInterface.GetAllNetworkInterfaces();  // Changed from NetworkInformation.GetAllNetworkInterfaces()
                            NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
                            // Unregister after a delay to prevent memory leaks
                            Task.Delay(5000).ContinueWith(_ => 
                            {
                                try { NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged; } 
                                catch { /* Ignore */ }
                            });
                        }
                        catch 
                        {
                            // Fall back to WMI method if this fails
                        }
                    }

                    // Your existing WMI code remains unchanged - it works on Windows 10/11
                    NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();
                    bool refreshedAdapter = false;

                    foreach (NetworkInterface adapter in adapters)
                    {
                        // Only process active Ethernet or WiFi adapters
                        if (adapter.OperationalStatus == OperationalStatus.Up && 
                            (adapter.NetworkInterfaceType == NetworkInterfaceType.Ethernet || 
                             adapter.NetworkInterfaceType == NetworkInterfaceType.Wireless80211))
                        {
                            EventLog.WriteEntry("Registry Monitor Service",
                                $"Found active adapter: {adapter.Name}",
                                EventLogEntryType.Information);

                            try
                            {
                                // Use WMI to gently refresh the adapter (less disruptive than disable/enable)
                                string adapterName = adapter.Name;
                                
                                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                                    $"SELECT * FROM Win32_NetworkAdapter WHERE NetConnectionID = '{adapterName}'"))
                                {
                                    foreach (ManagementObject obj in searcher.Get())
                                    {
                                        try {
                                            // Instead of using SetPowerState directly, try this approach:
                                            // 1. First check if we can disable/enable (more reliable)
                                            if ((bool)obj["NetEnabled"]) 
                                            {
                                                // Log that we're attempting to refresh
                                                EventLog.WriteEntry("Registry Monitor Service",
                                                    $"Attempting to refresh adapter: {adapterName} (DeviceID: {obj["DeviceID"]})",
                                                    EventLogEntryType.Information);
                                                    
                                                // Method 1: Try to disable and re-enable the adapter
                                                obj.InvokeMethod("Disable", null);
                                                Thread.Sleep(100);
                                                obj.InvokeMethod("Enable", null);
                                                
                                                EventLog.WriteEntry("Registry Monitor Service",
                                                    $"Successfully triggered NLA reevaluation on {adapterName}",
                                                    EventLogEntryType.Information);
                                                    
                                                refreshedAdapter = true;
                                            }
                                            // If that doesn't work, fall back to the NetworkChange event
                                            else {
                                                // Method 2: Use network change notification
                                                NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
                                                EventLog.WriteEntry("Registry Monitor Service",
                                                    $"Using NetworkChange event for adapter: {adapterName}",
                                                    EventLogEntryType.Information);
                                                refreshedAdapter = true;
                                            }
                                        }
                                        catch (Exception methodEx) {
                                            EventLog.WriteEntry("Registry Monitor Service",
                                                $"Error with network adapter methods: {methodEx.Message}",
                                                EventLogEntryType.Warning);
                                        }
                                                                
                                        break; // Just process one adapter
                                    }
                                }
                            }
                            catch (Exception wmiEx)
                            {
                                EventLog.WriteEntry("Registry Monitor Service",
                                    $"Error using WMI to refresh adapter: {wmiEx.Message}",
                                    EventLogEntryType.Warning);
                                    
                                // Try the alternative method if WMI fails
                                TriggerNetworkStackRefresh();
                                refreshedAdapter = true; // Consider this handled
                            }
                            
                            if (refreshedAdapter)
                                break;
                        }
                    }
                    
                    if (!refreshedAdapter)
                    {
                        EventLog.WriteEntry("Registry Monitor Service",
                            "Could not find suitable network adapter for NLA reevaluation",
                            EventLogEntryType.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry("Registry Monitor Service",
                    $"Error during NLA reevaluation: {ex.Message}",
                    EventLogEntryType.Error);
            }
        }

        private void OnNetworkAddressChanged(object sender, EventArgs e)
        {
            EventLog.WriteEntry("Registry Monitor Service", 
                "Network address change triggered", 
                EventLogEntryType.Information);
        }

        // Add this as an alternative method to your TriggerNetworkReevaluation method
        private void TriggerNetworkStackRefresh()
        {
            try
            {
                // This is a gentler approach that uses network events
                NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
                
                // Use DNS refresh as trigger mechanism (works reliably)
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "ipconfig.exe",
                    Arguments = "/flushdns",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                
                using (Process process = Process.Start(psi))
                {
                    process.WaitForExit();
                    EventLog.WriteEntry("Registry Monitor Service",
                        "Using DNS flush to trigger network stack refresh",
                        EventLogEntryType.Information);
                }
                
                // Unregister after a delay
                Task.Delay(5000).ContinueWith(_ => {
                    try { NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged; }
                    catch { /* Ignore */ }
                });
                
                return; // Success
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry("Registry Monitor Service",
                    $"Error during network stack refresh: {ex.Message}",
                    EventLogEntryType.Error);
            }
        }
    }
}