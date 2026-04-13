using System;
using System.Diagnostics;
using System.ServiceModel;
using System.Timers;
using System.Windows;
using TIG.TotalLink.Client.IisAdmin;

namespace TIG.TotalLink.IisAdmin
{
    public class Bootstrapper
    {
        #region Private Fields

        private readonly CommandLineOptions _commandLineOptions = new CommandLineOptions();

        #endregion


        #region Constructors

        /// <summary>
        /// Public constructor.
        /// </summary>
        /// <param name="commandLineArgs">Arguments that were passed on the command line.</param>
        public Bootstrapper(string[] commandLineArgs)
        {
            // Parse the command line arguments
            CommandLine.Parser.Default.ParseArguments(commandLineArgs, _commandLineOptions);
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Begins initialization of the application.
        /// </summary>
        public void Run()
        {
            // Close the application immediately if no parent process id was specified (unless a debugger is attached)
            if (!Debugger.IsAttached)
            {
                if (_commandLineOptions.ParentProcessId == 0)
                    Application.Current.Shutdown();
            }

            // Tell the admin class to manage IIS Express if the command line switch was set
            if (_commandLineOptions.Express)
                IisAdmin.IsExpress = true;

            // Start the WCF service host
            var host = new ServiceHost(typeof(IisAdmin), new Uri("net.pipe://localhost"));
            host.AddServiceEndpoint(typeof(IIisAdmin), new NetNamedPipeBinding(), "TIG.TotalLink.IisAdmin");
            host.Open();

            // Setup a timer to poll the parent process (unless a debugger is attached)
            if (!Debugger.IsAttached)
            {
                var pollParentTimer = new Timer(5000);
                pollParentTimer.Elapsed += PollParentTimer_Elapsed;
                pollParentTimer.Start();
            }
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Handles the PollParentTimer.Elapsed event.
        /// </summary>
        /// <param name="sender">Object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void PollParentTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            // Close the application if the parent process has closed
            try
            {
                Process.GetProcessById(_commandLineOptions.ParentProcessId);
            }
            catch (Exception)
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(Application.Current.Shutdown));
            }
        }

        #endregion
    }
}
