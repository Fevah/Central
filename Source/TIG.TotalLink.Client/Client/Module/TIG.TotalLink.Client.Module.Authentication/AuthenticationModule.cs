using Autofac;
using TIG.TotalLink.Shared.Facade.Authentication;

namespace TIG.TotalLink.Client.Module.Authentication
{
    public class AdminModule : Autofac.Module
    {
        #region Overrides

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            // Register services that this module provides
            builder.RegisterType<AuthenticationFacade>().As<IAuthenticationFacade>().SingleInstance();
        }

        #endregion
    }
}
