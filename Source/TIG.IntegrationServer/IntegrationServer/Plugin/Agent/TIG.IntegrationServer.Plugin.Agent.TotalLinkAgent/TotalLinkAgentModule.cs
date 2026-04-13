using System;
using Autofac;
using TIG.IntegrationServer.Plugin.Core.AgentPlugin.Interface;

namespace TIG.IntegrationServer.Plugin.Agent.TotalLinkAgent
{
    public class TotalLinkAgentModule : Module
    {
        #region Overrides

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);
            var key = new Guid("{4050D6DF-DE42-4C90-B535-73EAF4AB9960}");

            builder.RegisterType<TotalLinkAgentPlugin>()
                .Keyed<IAgentPlugin>(key)
                .InstancePerLifetimeScope();
        }

        #endregion
    }
}