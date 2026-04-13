using System;
using Autofac;
using TIG.IntegrationServer.Plugin.Core.ChangeTrackerPlugin;
using TIG.IntegrationServer.Plugin.Core.ChangeTrackerPlugin.Interface;

namespace TIG.IntegrationServer.Plugin.ChangeTracker.SqlServerChangeTracker
{
    public class SqlServerChangeTrackerModule : Module
    {
        #region Overrides

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);
            var key = new Guid("{15680198-1B76-459D-87EF-93A6DFFB7E21}");

            builder.RegisterType<SqlServerChangeTrackerPlugin>()
                .Keyed<IChangeTrackerPlugin>(key)
                .InstancePerLifetimeScope();
        }

        #endregion
    }
}