using Autofac;
using TIG.IntegrationServer.Common.ServiceConfiguration;
using TIG.TotalLink.Shared.Facade.Core.Configuration;

namespace TIG.IntegrationServer.Common
{
    public class CommonModule : Module
    {
        #region Overrides

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.RegisterType<TotalLinkServiceConfiguration>().As<IServiceConfiguration>().SingleInstance();
        }

        #endregion
    }
}