using Autofac;
using TIG.IntegrationServer.Plugin.Core.ConverterPlugin.Interface;

namespace TIG.IntegrationServer.Plugin.Converter.CalculateConverter
{
    public class CalculateConverterModule : Module
    {
        #region Overrides

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.RegisterType<CalculateConverter>()
                .Named<IConverterPlugin>("Calc")
                .InstancePerLifetimeScope();
        }

        #endregion
    }
}