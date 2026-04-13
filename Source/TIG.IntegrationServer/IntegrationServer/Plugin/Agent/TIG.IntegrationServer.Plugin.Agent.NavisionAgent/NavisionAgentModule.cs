using System;
using Autofac;
using TIG.IntegrationServer.Plugin.Core.AgentPlugin.Interface;

namespace TIG.IntegrationServer.Plugin.Agent.NavisionAgent
{
    public class NavisionAgentModule : Module
    {
        #region Overrides

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);
            var key = new Guid("{6F0BBCED-8A4D-43E9-9304-0FC335025330}");

            builder.RegisterType<NavAgentPlugin>()
                .Keyed<IAgentPlugin>(key)
                .InstancePerLifetimeScope();
        }

        #endregion
    }
}