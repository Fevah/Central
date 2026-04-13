using Autofac;
using TIG.IntegrationServer.Plugin.Core.ConverterPlugin.Interface;

namespace TIG.IntegrationServer.Plugin.Converter.TextConverter
{
    public class ConstantConverterModule : Module
    {
        #region Overrides

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.RegisterType<ConstantConverter>()
                .Named<IConverterPlugin>("Constant")
                .InstancePerLifetimeScope();
        }

        #endregion
    }
}