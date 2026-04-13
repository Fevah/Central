using Autofac;
using TIG.IntegrationServer.Plugin.Core.ConverterPlugin.Interface;

namespace TIG.IntegrationServer.Plugin.Converter.EntityConverter
{
    public class EntityConverterModule : Module
    {
        #region Overrides

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.RegisterType<EntityConverter>().Named<IConverterPlugin>("Entity").InstancePerLifetimeScope();
        }

        #endregion
    }
}