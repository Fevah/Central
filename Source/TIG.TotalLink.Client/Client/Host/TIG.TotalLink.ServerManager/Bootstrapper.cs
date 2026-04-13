using System.Threading;
using CommandLine;
using MonitoredUndo;
using TIG.TotalLink.Client.Core;
using TIG.TotalLink.Client.Core.StartupWorker.Core;
using TIG.TotalLink.Client.Editor.StartupWorker;
using TIG.TotalLink.Client.Undo.Core;

namespace TIG.TotalLink.ServerManager
{
    public class Bootstrapper : BootstrapperBase
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
            : base(commandLineArgs)
        {
            // Parse the command line arguments
            Parser.Default.ParseArguments(commandLineArgs, _commandLineOptions);

            DelayStartup = _commandLineOptions.DelayStartup;
        }

        #endregion


        #region Overrides

        public override void RunStartup()
        {
            // Replace the default ChangeFactory with our own one that can track data object modifications, and allows us to turn off change tracking
            DefaultChangeFactory.Current = new ChangeFactoryEx();

            base.RunStartup();
        }

        protected override void EnqueueStartupWorkers(StartupWorkerManager startupWorkerManager)
        {
            base.EnqueueStartupWorkers(startupWorkerManager);

            startupWorkerManager.Enqueue(new InitModulesStartupWorker());
        }

        protected override void RunApplication()
        {
            base.RunApplication();

            ShowMainWindow("MainWindow");
        }

        protected override void OnMainWindowLoaded()
        {
            base.OnMainWindowLoaded();

            CloseSplashScreen();
        }

        #endregion
    }
}
