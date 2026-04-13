using System.Windows;
using System.Windows.Threading;

namespace TIG.TotalLink.IisAdmin
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Create an instance of the Bootstrapper and run it
            var bootstrapper = new Bootstrapper(e.Args);
            bootstrapper.Run();
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(e.Exception.ToString(), "Unhandled Error", MessageBoxButton.OK, MessageBoxImage.Stop);
        }
    }
}
