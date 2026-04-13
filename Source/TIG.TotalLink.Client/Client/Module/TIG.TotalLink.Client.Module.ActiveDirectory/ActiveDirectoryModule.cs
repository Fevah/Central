using Autofac;
using TIG.TotalLink.Client.Module.ActiveDirectory.View.Widget;
using TIG.TotalLink.Client.Module.ActiveDirectory.ViewModel.Widget;
using TIG.TotalLink.Shared.Facade.ActiveDirectory;

namespace TIG.TotalLink.Client.Module.ActiveDirectory
{
    public class ActiveDirectoryModule : Autofac.Module
    {
        #region Overrides

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            // Register services that this module provides
            builder.RegisterType<ActiveDirectoryFacade>().As<IActiveDirectoryFacade>().SingleInstance();

            // Register components that this module provides
            builder.RegisterType<ActiveDirectoryUserListView>().InstancePerDependency();
            builder.RegisterType<ActiveDirectoryUserListViewModel>().InstancePerDependency();
        }

        #endregion
    }
}