using Autofac;
using TIG.TotalLink.ServerManager.View;
using TIG.TotalLink.ServerManager.View.Widget;
using TIG.TotalLink.ServerManager.ViewModel;
using TIG.TotalLink.ServerManager.ViewModel.Widget;
using TIG.TotalLink.ServerManager.Window;

namespace TIG.TotalLink.ServerManager
{
    public class ServerManagerModule : Autofac.Module
    {
        #region Overrides

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            // Register components that this module provides
            builder.RegisterType<MainWindow>().SingleInstance();
            builder.RegisterType<MainWindowViewModel>().SingleInstance();
            builder.RegisterType<MainView>().SingleInstance();
            builder.RegisterType<MainViewModel>().SingleInstance();

            builder.RegisterType<ServiceConfigView>().SingleInstance();
            builder.RegisterType<ServiceConfigViewModel>().SingleInstance();

        }

        #endregion
    }
}
