using Autofac;
using TIG.IntegrationServer.Plugin.Core.ConverterPlugin.Interface;

namespace TIG.IntegrationServer.Plugin.Converter.PropertyConverter
{
    public class PropertyConverterModule : Module
    {
        #region Overrides

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.RegisterType<PropertyConverter>()
                .Named<IConverterPlugin>("Property")
                .InstancePerLifetimeScope();
        }

        #endregion
    }
}