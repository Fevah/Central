using System;
using TIG.IntegrationServer.Helper;
using TIG.IntegrationServer.Service;
using Topshelf;

namespace TIG.IntegrationServer
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            try
            {
                // Create a Topshelf Host to contain the service
                var serviceHost = HostFactory.New(x =>
                {
                    // Set service information
                    x.SetServiceName("TIGIntegrationService");
                    x.SetDescription("TIG Integration Service");
                    x.SetDisplayName("TIG Integration Service");

                    // Specify SyncEngineService as the main service instance
                    x.Service<SyncEngineService>();

                    // Run Service as a local system user
                    x.RunAsLocalSystem();

                    // Service can be shutdown and paused
                    x.EnableShutdown();
                    x.EnablePauseAndContinue();

                    // Set service recovery behavior
                    x.EnableServiceRecovery(r =>
                    {
                        r.RestartComputer(5, "TIG Integration Service restarting!");
                        r.RestartService(0);

                        r.OnCrashOnly();
                        r.SetResetPeriod(1);
                    });
                });

                // Run the service
                serviceHost.Run();
            }
            catch (Exception ex)
            {
                // Process the exception to return more useful information
                var serviceException = new ServiceExceptionHelper(ex);

                // Pause output if any error occurs during initialization
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine("Error initializing service host!");
                Console.WriteLine();
                Console.WriteLine(serviceException.Message);
                Console.WriteLine();
                Console.WriteLine("Press any key to close.");
                Console.ReadKey();
            }
        }
    }
}
