using System;
using Autofac;
using TIG.IntegrationServer.Plugin.Core.AgentPlugin;
using TIG.IntegrationServer.Plugin.Core.AgentPlugin.Interface;

namespace TIG.IntegrationServer.Plugin.Agent.ECommerceAgent
{
    public class ECommerceAgentModule : Module
    {
        #region Overrides

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);
            var key = new Guid("{9D4F6047-15FE-4BB7-BA3B-0B65C3C476C8}");

            builder.RegisterType<EcommerceAgentPlugin>()
                .Keyed<IAgentPlugin>(key)
                .InstancePerLifetimeScope();
        }

        #endregion
    }
}