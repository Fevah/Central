using System;
using Autofac;
using TIG.IntegrationServer.Plugin.Core.MapperPlugin.Interface;

namespace TIG.IntegrationServer.Plugin.Mapper.DictionaryEntityMapper
{
    public class DictionaryEntityMapperModule : Module
    {
        #region Overrides

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);
            var key = new Guid("{EBC8AECA-8521-4A9E-A4C0-C3711E829BCC}");

            builder.RegisterType<DictionaryEntityMapper>()
                .Keyed<IFieldMapperPlugin>(key)
                .InstancePerLifetimeScope();
        }

        #endregion
    }
}