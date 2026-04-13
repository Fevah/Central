using Autofac;
using TIG.TotalLink.Client.Module.Integration.View.Widget;
using TIG.TotalLink.Client.Module.Integration.ViewModel.Widget;
using TIG.TotalLink.Shared.Facade.Integration;

namespace TIG.TotalLink.Client.Module.Integration
{
    public class IntegrationModule : Autofac.Module
    {
        #region Overrides

        /// <summary>
        /// Register services that this module provides.
        /// </summary>
        /// <param name="builder">Container builder.</param>
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            // Register services that this module provides
            builder.RegisterType<IntegrationFacade>().As<IIntegrationFacade>().SingleInstance();

            // Register components that this module provides
            builder.RegisterType<SyncEntityBundleListView>().InstancePerDependency();
            builder.RegisterType<SyncEntityBundleListViewModel>().InstancePerDependency();
            builder.RegisterType<SyncEntityListView>().InstancePerDependency();
            builder.RegisterType<SyncEntityListViewModel>().InstancePerDependency();
            builder.RegisterType<SyncEntityMapListView>().InstancePerDependency();
            builder.RegisterType<SyncEntityMapListViewModel>().InstancePerDependency();
        }

        #endregion
    }
}
