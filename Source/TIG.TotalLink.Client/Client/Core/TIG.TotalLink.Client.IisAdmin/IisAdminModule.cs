using Autofac;
using TIG.TotalLink.Client.IisAdmin.Provider;

namespace TIG.TotalLink.Client.IisAdmin
{
    public class IisAdminModule : Autofac.Module
    {
        #region Overrides

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            // Register services that this module provides
            builder.RegisterType<IisAdminProvider>().As<IIisAdminProvider>().SingleInstance();
        }

        #endregion
    }
}
