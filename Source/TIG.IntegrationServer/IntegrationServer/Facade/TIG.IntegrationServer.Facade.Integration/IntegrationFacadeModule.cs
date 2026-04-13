using Autofac;
using TIG.IntegrationServer.Common.Configuration;
using TIG.IntegrationServer.Common.ServiceConfiguration;
using TIG.TotalLink.Shared.Facade.Authentication;
using TIG.TotalLink.Shared.Facade.Core;
using TIG.TotalLink.Shared.Facade.Core.Configuration;
using TIG.TotalLink.Shared.Facade.Integration;

namespace TIG.IntegrationServer.Facade.Integration
{
    public class IntegrationFacadeModule : Module
    {
        #region Overrides

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            // Register services that this module provides
            builder.Register(context =>
            {
                var autenticationFacade = context.Resolve<IAuthenticationFacade>();
                autenticationFacade.Connect(ServiceTypes.Method);
                var authenticationSettings = IntegrationServiceSection.Instance.IntegrationServiceSettings.AuthenticationSettings;
                var token = autenticationFacade.Login(authenticationSettings.LoginName, authenticationSettings.Password);
                return new IntegrationFacade(new TotalLinkServiceConfiguration(token));
            }).As<IIntegrationFacade>().SingleInstance();

            builder.RegisterType<TotalLinkServiceConfiguration>().As<IServiceConfiguration>().SingleInstance();
        }

        #endregion
    }
}