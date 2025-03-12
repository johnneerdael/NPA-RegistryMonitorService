using System;
using System.ServiceProcess;
using System.Diagnostics;
using System.IO;

namespace RegistryMonitorService
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            // First, handle the Windows Service mode - this should always work
            if (!Environment.UserInteractive || (args.Length > 0 && args[0].ToLower() == "service"))
            {
                // Running as a Windows service - no console operations allowed
                ServiceBase[] ServicesToRun = new ServiceBase[] { new RegistryMonitorService() };
                ServiceBase.Run(ServicesToRun);
                return; // Exit immediately after service run
            }

            // If we get here, we're definitely in interactive console mode
            try
            {
                var service = new RegistryMonitorService();
                
                // Use command-line arguments to determine action
                if (args.Length > 0)
                {
                    string command = args[0].ToLower();
                    
                    switch (command)
                    {
                        case "install":
                            Console.WriteLine("Installing service...");
                            // Add installation code if needed
                            break;
                            
                        case "start":
                            // Start the service manually
                            Console.WriteLine("Starting service in console mode for debugging...");
                            service.TestStartupAndStop(args);
                            Console.WriteLine("Service started. Press Enter to stop...");
                            
                            // Use Console.ReadLine() instead of ReadKey for better reliability
                            Console.ReadLine();
                            
                            Console.WriteLine("Stopping service...");
                            service.TestStop();
                            Console.WriteLine("Service stopped.");
                            break;
                            
                        default:
                            ShowUsage();
                            break;
                    }
                }
                else
                {
                    ShowUsage();
                }
            }
            catch (Exception ex)
            {
                // Log the exception to a file for troubleshooting
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
                File.AppendAllText(logPath, $"{DateTime.Now}: {ex.Message}\r\n{ex.StackTrace}\r\n\r\n");
                
                // If we can access the console, show the error
                try
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    Console.WriteLine("Details have been logged to error.log");
                    Console.WriteLine("Press Enter to exit...");
                    Console.ReadLine();
                }
                catch
                {
                    // If we can't access the console, just exit
                }
            }
        }
        
        private static void ShowUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  RegistryMonitorService.exe install - Install the service");
            Console.WriteLine("  RegistryMonitorService.exe start - Start the service in console mode");
            Console.WriteLine("  RegistryMonitorService.exe service - Run as Windows service (internal use)");
            Console.WriteLine("Press Enter to exit...");
            Console.ReadLine();
        }
    }
}