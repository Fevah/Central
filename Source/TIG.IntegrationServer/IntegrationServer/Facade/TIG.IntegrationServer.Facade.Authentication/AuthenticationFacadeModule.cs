using Autofac;
using TIG.TotalLink.Shared.Facade.Authentication;

namespace TIG.IntegrationServer.Facade.Authentication
{
    public class AuthenticationFacadeModule : Module
    {
        #region Overrides

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.RegisterType<AuthenticationFacade>().As<IAuthenticationFacade>().SingleInstance();
        }

        #endregion
    }
}
