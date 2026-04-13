using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using DevExpress.Xpf.Core;
using TIG.TotalLink.Shared.Facade.Core.Helper;

namespace TIG.TotalLink.Client
{
    public partial class App : Application
    {
        #region Overrides

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

#if !DEBUG || TEST
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
#endif
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            // Create an instance of the Bootstrapper and run it
            var bootstrapper = new Bootstrapper(e.Args);
            bootstrapper.RunStartup();
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Handles the unobserved task exception event.
        /// This event is a last chance to handle task exception
        /// It is trigger when GC work, so don't let system do this, it will be cause performance issue.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            // Set exception to observed, otherwise application will crash.
            e.SetObserved();

            // Display the error
            // TODO: [Bo] In future, we will record this issue on file or system event, not just throw them to end user.
            var serviceException = new ServiceExceptionHelper(e.Exception);
            Current.Dispatcher.BeginInvoke(DispatcherPriority.SystemIdle, (Action)(() =>
            {
                DXMessageBox.Show(serviceException.ToString(), "Unhandled Error", MessageBoxButton.OK, MessageBoxImage.Stop);
            }));
        }

#if !DEBUG || TEST
        /// <summary>
        /// Handles the unhandled exception of application domain.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            // Display the error
            // TODO: [Bo] In future, we will record this issue on file or system event, not just throw them to end user.
            var exception = e.ExceptionObject as Exception;
            var serviceException = new ServiceExceptionHelper(exception);
            DXMessageBox.Show(serviceException.HasException ? serviceException.ToString() : e.ExceptionObject.ToString(), "Unhandled Error", MessageBoxButton.OK, MessageBoxImage.Stop);
        }
#endif

        /// <summary>
        /// Handles the unhandled exception of main threading.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
#if !DEBUG || TEST
            // Display the error
            // TODO: [Bo] In future, we will record this issue on file or system event, not just throw them to end user.
            var serviceException = new ServiceExceptionHelper(e.Exception);
            DXMessageBox.Show(serviceException.ToString(), "Unhandled Error", MessageBoxButton.OK, MessageBoxImage.Stop);

            // Set exception to handled, otherwise application will crash.
            e.Handled = true;
#endif
        }

        #endregion
    }
}
