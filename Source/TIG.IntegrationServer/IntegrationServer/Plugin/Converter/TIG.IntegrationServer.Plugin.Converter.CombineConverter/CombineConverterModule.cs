using Autofac;
using TIG.IntegrationServer.Plugin.Core.ConverterPlugin.Interface;

namespace TIG.IntegrationServer.Plugin.Converter.CombineConverter
{
    public class CombineConverterModule : Module
    {
        #region Overrides

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.RegisterType<CombineConverter>()
                .Named<IConverterPlugin>("Combine")
                .InstancePerLifetimeScope();
        }

        #endregion
    }
}