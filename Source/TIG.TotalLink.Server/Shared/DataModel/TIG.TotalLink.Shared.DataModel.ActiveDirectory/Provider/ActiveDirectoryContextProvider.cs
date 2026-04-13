using System.DirectoryServices.Protocols;
using System.Net;
using LinqToLdap;
using LinqToLdap.Mapping;
using TIG.TotalLink.Server.Core.Configuration;

namespace TIG.TotalLink.Shared.DataModel.ActiveDirectory.Provider
{
    public class ActiveDirectoryContextProvider
    {
        #region Constructors

        public ActiveDirectoryContextProvider()
        {
            InitializeContext();
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// Active directory context.
        /// </summary>
        public IDirectoryContext Context { get; private set; }

        #endregion


        #region Private Methods

        /// <summary>
        /// Initialize active directory context.
        /// </summary>
        private void InitializeContext()
        {
            var config = ActiveDirectoryConfigurationSection.Instance;
            var configuration = new LdapConfiguration();
            var userMap = new AttributeClassMap<ActiveDirectoryUser>();

            // Set user mapping from web config.
            configuration.AddMapping(userMap, string.Format("CN=Users,{0}", config.Root));

            // Build configuration
            configuration.ConfigurePooledFactory(config.Domain)
                .AuthenticateBy(AuthType.Basic)
                .AuthenticateAs(new NetworkCredential(
                    config.User,
                    config.Password))
                .UsePort(389)
                .ProtocolVersion(3);

            // Create the context
            Context = configuration.CreateContext();
        }

        #endregion
    }
}