using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Threading;
using System.Runtime.InteropServices;

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
                    }
                    catch (Exception ex)
                    {
                        EventLog.WriteEntry("Registry Monitor Service",
                            $"Error reading registry value: {ex.Message}",
                            EventLogEntryType.Error);
                        currentStatus = "Error";
                    }

                    // Execute the PowerShell script
                    try
                    {
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
                    catch (Exception ex)
                    {
                        EventLog.WriteEntry("Registry Monitor Service", $"Error executing script: {ex.Message}", EventLogEntryType.Error);
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
    }
}