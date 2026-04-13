using Autofac;
using TIG.TotalLink.Client.Module.Global.View.Widget;
using TIG.TotalLink.Client.Module.Global.ViewModel.Widget;
using TIG.TotalLink.Shared.Facade.Global;

namespace TIG.TotalLink.Client.Module.Global
{
    public class GlobalModule : Autofac.Module
    {
        #region Overrides

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            // Register services that this module provides
            builder.RegisterType<GlobalFacade>().As<IGlobalFacade>().SingleInstance();

            // Register components that this module provides
            builder.RegisterType<XpoProviderListView>().InstancePerDependency();
            builder.RegisterType<XpoProviderListViewModel>().InstancePerDependency();
            builder.RegisterType<MainDatabaseView>().InstancePerDependency();
            builder.RegisterType<MainDatabaseViewModel>().InstancePerDependency();
            builder.RegisterType<ServiceListView>().InstancePerDependency();
            builder.RegisterType<ServiceListViewModel>().InstancePerDependency();
            builder.RegisterType<ReferenceConfigView>().InstancePerDependency();
            builder.RegisterType<ReferenceConfigViewModel>().InstancePerDependency();
            builder.RegisterType<SystemImportExportView>().InstancePerDependency();
            builder.RegisterType<SystemImportExportViewModel>().InstancePerDependency();
        }

        #endregion
    }
}
