using Autofac;
using TIG.TotalLink.Client.View;
using TIG.TotalLink.Client.ViewModel;
using TIG.TotalLink.Client.Window;
using TIG.TotalLink.Shared.Facade.Authentication;

namespace TIG.TotalLink.Client
{
    public class ClientModule : Autofac.Module
    {
        #region Overrides

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            // Register services that this module provides
            builder.RegisterType<AuthenticationFacade>().As<IAuthenticationFacade>().SingleInstance();

            // Register components that this module provides
            builder.RegisterType<LoginWindow>().SingleInstance();
            builder.RegisterType<LoginWindowViewModel>().SingleInstance();
            builder.RegisterType<LoginView>().SingleInstance();
            builder.RegisterType<LoginViewModel>().SingleInstance();
            builder.RegisterType<MainWindow>().SingleInstance();
            builder.RegisterType<MainWindowViewModel>().SingleInstance();
            builder.RegisterType<MainView>().SingleInstance();
            builder.RegisterType<MainViewModel>().SingleInstance();
        }

        #endregion
    }
}
