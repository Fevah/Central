using Autofac;
using TIG.TotalLink.Client.Core.Interface.BackgroundService;
using TIG.TotalLink.Client.Module.Repository.BackgroundService;
using TIG.TotalLink.Client.Module.Repository.Configuration;
using TIG.TotalLink.Client.Module.Repository.View.Widget;
using TIG.TotalLink.Client.Module.Repository.ViewModel.Widget;
using TIG.TotalLink.Shared.Facade.Core.Configuration;
using TIG.TotalLink.Shared.Facade.Repository;

namespace TIG.TotalLink.Client.Module.Repository
{
    public class RepositoryModule : Autofac.Module
    {
        #region Overrides

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.RegisterType<LocalStoreConfiguration>().As<ILocalStoreConfiguration>().SingleInstance();
            builder.RegisterType<SyncBackgroundService>().As<ISyncBackgroundService>().SingleInstance();

            // Register services that this module provides
            builder.RegisterType<RepositoryFacade>().As<IRepositoryFacade>().SingleInstance();

            // Register components that this module provides
            builder.RegisterType<RepositoryDatabaseView>().InstancePerDependency();
            builder.RegisterType<RepositoryDatabaseViewModel>().InstancePerDependency();

            builder.RegisterType<RepositoryDataStoreView>().InstancePerDependency();
            builder.RegisterType<RepositoryDataStoreViewModel>().InstancePerDependency();

            builder.RegisterType<RepositoryLocalDatabaseView>().InstancePerDependency();
            builder.RegisterType<RepositoryLocalDatabaseViewModel>().InstancePerDependency();

            builder.RegisterType<RepositoryFileListView>().InstancePerDependency();
            builder.RegisterType<RepositoryFileListViewModel>().InstancePerDependency();

            builder.RegisterType<SyncControlView>().InstancePerDependency();
            builder.RegisterType<SyncControlViewModel>().InstancePerDependency();

            builder.RegisterType<Uploader>().InstancePerDependency();
            builder.RegisterType<Downloader>().InstancePerDependency();
        }

        #endregion
    }
}
