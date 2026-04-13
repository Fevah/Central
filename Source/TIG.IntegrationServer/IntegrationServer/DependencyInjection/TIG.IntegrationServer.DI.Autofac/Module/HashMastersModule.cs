using System.ComponentModel.Composition;
using Autofac;
using Autofac.Core;
using TIG.IntegrationServer.Security.Cryptography;
using TIG.IntegrationServer.Security.Cryptography.MD5;
using TIG.IntegrationServer.Security.Cryptography.SHA512;

namespace TIG.IntegrationServer.DI.Autofac.Module
{
    [Export("common", typeof(IModule))]
    internal class HashMastersModule : global::Autofac.Module
    {
        #region Overrides

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            // Register type by Name
            builder.RegisterType<Md5HashMaster>().Named<IHashMaster>("MD5");
            builder.RegisterType<Sha512HashMaster>().Named<IHashMaster>("SHA512");

            // Register type by self
            builder.RegisterType<Md5HashMaster>().AsSelf();
            builder.RegisterType<Sha512HashMaster>().AsSelf();
        }

        #endregion
    }
}
