using Autofac;
using TIG.IntegrationServer.Plugin.Core.ConverterPlugin.Interface;

namespace TIG.IntegrationServer.Plugin.Converter.SplitConverter
{
    public class SplitConverterModule : Module
    {
        #region Overrides

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.RegisterType<SplitConverter>()
                .Named<IConverterPlugin>("Split")
                .InstancePerLifetimeScope();
        }

        #endregion
    }
}