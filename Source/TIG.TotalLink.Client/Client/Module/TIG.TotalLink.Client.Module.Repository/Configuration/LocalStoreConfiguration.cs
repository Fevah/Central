using System;
using System.Linq;
using DevExpress.Xpo;
using TIG.TotalLink.Client.Core.AppContext;
using TIG.TotalLink.Client.Module.Repository.Properties;
using TIG.TotalLink.Shared.DataModel.Global;
using TIG.TotalLink.Shared.Facade.Core.Configuration;
using TIG.TotalLink.Shared.Facade.Global;
using TIG.TotalLink.Shared.Xpo.Core.Helper;

namespace TIG.TotalLink.Client.Module.Repository.Configuration
{
    public class LocalStoreConfiguration : ILocalStoreConfiguration
    {
        #region Private Properites

        private readonly IGlobalFacade _globalFacade;
        private string _connection;

        #endregion

        public LocalStoreConfiguration(IGlobalFacade globalFacade)
        {
            _globalFacade = globalFacade;
            Load();
        }

        /// <summary>
        /// Load the configuration.
        /// </summary>
        public void Load()
        {
            ProviderId = Settings.Default.ProviderId;
            ServerName = Settings.Default.ServerName;
            DatabaseFile = Settings.Default.DatabaseFile;
            UseIntegratedSecurity = Settings.Default.UseIntegratedSecurity;
            UserName = Settings.Default.UserName;
            Password = Settings.Default.Password;
            UseServer = Settings.Default.UseServer;
            DatabaseName = Settings.Default.DatabaseName;
        }

        public string GetConnection()
        {
            if (!string.IsNullOrWhiteSpace(_connection))
                return _connection;

            var providerId = Settings.Default.ProviderId;

            if (!_globalFacade.IsConnected)
                _globalFacade.Connect();

            var provider = _globalFacade.ExecuteQuery(uow =>
                uow.Query<XpoProvider>().Where(p => p.Oid == providerId)
            ).FirstOrDefault();

            if (provider == null)
                return string.Empty;
            return ServiceHelper.GetConnectionString(provider.Name, provider.HasUserName, provider.HasPassword,
                UseServer,
                ServerName,
                DatabaseName,
                DatabaseFile,
                UseIntegratedSecurity,
                UserName,
                Password);
        }

        #region Public Properites

        public Guid ProviderId { get; private set; }

        public string ServerName { get; private set; }

        public string DatabaseFile { get; private set; }

        public bool UseIntegratedSecurity { get; private set; }

        public string UserName { get; private set; }

        public string Password { get; private set; }

        public bool UseServer { get; private set; }

        public string DatabaseName { get; private set; }

        #endregion
    }
}