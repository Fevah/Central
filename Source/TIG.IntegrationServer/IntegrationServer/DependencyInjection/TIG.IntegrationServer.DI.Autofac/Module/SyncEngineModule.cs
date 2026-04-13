using System.ComponentModel.Composition;
using Autofac;
using Autofac.Core;
using TIG.IntegrationServer.SyncEngine.Core.Interface;
using TIG.IntegrationServer.SyncEngine.Custom;
using TIG.IntegrationServer.SyncEngine.Custom.Context;
using TIG.IntegrationServer.SyncEngine.Custom.Context.Configuration;
using TIG.IntegrationServer.SyncEngine.Custom.Context.Configuration.Interface;
using TIG.IntegrationServer.SyncEngine.Custom.Context.Dispatcher.Interface;
using TIG.IntegrationServer.SyncEngine.Custom.Context.Dispatchers;
using TIG.IntegrationServer.SyncEngine.Custom.Context.SyncStatus;
using TIG.IntegrationServer.SyncEngine.Custom.TaskBuilder.Interface;
using TIG.IntegrationServer.SyncEngine.Custom.TaskBuilders;

namespace TIG.IntegrationServer.DI.Autofac.Module
{
    [Export("common", typeof(IModule))]
    internal sealed class SyncEngineModule : global::Autofac.Module
    {
        #region Overrides

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.RegisterType<SyncMachine>().As<ISyncMachine>().SingleInstance();
            builder.RegisterType<Configuration>().As<IConfiguration>().SingleInstance();

            builder.RegisterType<Context>().As<IContext>().SingleInstance();
            builder.RegisterType<SyncStatusManager>().As<ISyncStatusManager>().SingleInstance();
            builder.RegisterType<SyncStatusRepository>().As<ISyncStatusRepository>().SingleInstance();
            
            builder.RegisterType<CancellationDispatcher>().As<ICancellationDispatcher>().OwnedByLifetimeScope();
            builder.RegisterType<PauseDispatcher>().As<IPauseDispatcher>().OwnedByLifetimeScope();

            builder.RegisterType<SyncEntityBundleTaskBuilder>().As<ISyncEntityBundleTaskBuilder>().OwnedByLifetimeScope();
            builder.RegisterType<SyncInstanceBundleTaskBuilder>().As<ISyncInstanceBundleTaskBuilder>().OwnedByLifetimeScope();
            builder.RegisterType<SyncInstanceTaskBuilder>().As<ISyncInstanceTaskBuilder>().OwnedByLifetimeScope();
        }

        #endregion
    }
}
