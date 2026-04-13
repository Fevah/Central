using Autofac;
using TIG.IntegrationServer.Plugin.Core.ConverterPlugin.Interface;

namespace TIG.IntegrationServer.Plugin.Converter.AutoGenerateConverter
{
    public class AutoGenerateConverterModule : Module
    {
        #region Overrides

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.RegisterType<AutoGenerateConverter>()
                .Named<IConverterPlugin>("AutoGenerate")
                .InstancePerLifetimeScope();
        }

        #endregion
    }
}