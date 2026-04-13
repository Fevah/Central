using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Xpo;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Client.Module.Global.ViewModel.Core;
using TIG.TotalLink.Client.Module.Repository.DataModel;
using TIG.TotalLink.Client.Module.Repository.Properties;
using TIG.TotalLink.Shared.DataModel.Core.Enum;
using TIG.TotalLink.Shared.DataModel.Global;
using TIG.TotalLink.Shared.Facade.Core.Helper;
using TIG.TotalLink.Shared.Facade.Global;
using TIG.TotalLink.Shared.Xpo.Core.Helper;

namespace TIG.TotalLink.Client.Module.Repository.ViewModel.Widget
{
    public class RepositoryLocalDatabaseViewModel : DatabaseConfigViewModelBase
    {
        #region Private Properties

        private XpoProvider _provider;
        private bool _useServer;
        private string _serverName;
        private string _databaseName;
        private string _databaseFile;
        private bool _useIntegratedSecurity;
        private string _userName;
        private string _password;
        private static Assembly[] _repositoryStoreAssemblies;

        #endregion


        #region Constructors

        public RepositoryLocalDatabaseViewModel()
        {
        }

        public RepositoryLocalDatabaseViewModel(IGlobalFacade globalFacade)
            : base(DatabaseDomain.Repository, globalFacade)
        {
            _repositoryStoreAssemblies = new[]
            {
                typeof(FileData).Assembly,
                typeof(XPObjectType).Assembly
            };
        }

        #endregion


        #region Overrides

        /// <summary>
        /// Execute method for the TestDatabaseCommand and UpdateDatabaseCommand.
        /// </summary>
        protected override async Task OnUpdateDatabaseExecuteAsync(bool performUpdate)
        {
            try
            {
                // Get the connection string for the specified database domain
                var connectionString = GetConnectionString();

                await Task.Run(() =>
                {
                    ServiceHelper.UpdateDatabase(performUpdate, connectionString, _repositoryStoreAssemblies, false);
                });

                if (performUpdate)
                {
                    await Task.Run(() =>
                    {
                        ServiceHelper.PopulateDataStore(connectionString, _repositoryStoreAssemblies);
                    });

                    MessageBoxService.Show("Database updated successfully.", "Update Database", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBoxService.Show("Database is up-to-date.", "Test Database", MessageBoxButton.OK, MessageBoxImage.Information);
                }

            }
            catch (Exception ex)
            {
                var mode = (performUpdate ? "Update" : "Test");
                var serviceException = new ServiceExceptionHelper(ex);
                MessageBoxService.Show(
                    string.Format("{0} database failed!\r\n\r\n{1}", mode, serviceException.Message),
                    string.Format("{0} Database", mode), MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// Execute method for the PurgeDatabaseCommand.
        /// </summary>
        protected override async Task OnPurgeDatabaseExecuteAsync()
        {
            try
            {
                var result = await Task.Run(() =>
                {
                    // Get the connection string for the specified database domain
                    var connectionString = GetConnectionString();

                    return ServiceHelper.PurgeDatabase(connectionString, _repositoryStoreAssemblies);
                });

                MessageBoxService.Show(
                    string.Format(
                        "Purge successful.\r\n\r\nProcessed: {0}\r\nPurged: {1}\r\nNon Purged: {2}\r\nReferenced By Non Deleted Object: {3}",
                        result.Processed, result.Purged, result.NonPurged, result.ReferencedByNonDeletedObjects),
                    "Purge Deleted Objects", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                var serviceException = new ServiceExceptionHelper(ex);
                MessageBoxService.Show(string.Format("Purge failed!\r\n\r\n{0}", serviceException.Message), "Purge Database", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Loads all settings from the database into the viewmodel.
        /// </summary>
        protected override void LoadSettings()
        {
            // Initialize settings
            var providerId = Settings.Default.ProviderId;
            _provider = GlobalFacade.ExecuteQuery(uow =>
                uow.Query<XpoProvider>().Where(p => p.Oid == providerId)
            ).FirstOrDefault();

            _useServer = Settings.Default.UseServer;

            _serverName = Settings.Default.ServerName;
            _databaseName = Settings.Default.DatabaseName;
            _databaseFile = Settings.Default.DatabaseFile;

            _useIntegratedSecurity = Settings.Default.UseIntegratedSecurity;

            _userName = Settings.Default.UserName;
            _password = Settings.Default.Password;

            // Raise PropertyChanged events
            RaisePropertyChanged(() => Provider);
            RaisePropertyChanged(() => UseServer);
            RaisePropertyChanged(() => ServerName);
            RaisePropertyChanged(() => DatabaseFile);
            RaisePropertyChanged(() => DatabaseName);
            RaisePropertyChanged(() => UserName);
            RaisePropertyChanged(() => Password);
            RaisePropertyChanged(() => UseIntegratedSecurity);
        }


        #endregion


        #region Private Methods

        /// <summary>
        /// Gets a connection string.
        /// </summary>
        /// <returns>The connection string.</returns>
        private string GetConnectionString()
        {
            var providerId = Settings.Default.ProviderId;
            var provider = GlobalFacade.ExecuteQuery(uow =>
                uow.Query<XpoProvider>().Where(p => p.Oid == providerId)
            ).FirstOrDefault();

            if (provider == null)
                return string.Empty;
            return ServiceHelper.GetConnectionString(provider.Name, provider.HasUserName, provider.HasPassword,
                Settings.Default.UseServer,
                Settings.Default.ServerName,
                Settings.Default.DatabaseName,
                Settings.Default.DatabaseFile,
                Settings.Default.UseIntegratedSecurity,
                Settings.Default.UserName,
                Settings.Default.Password);
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The XpoProvider to use for database connectivity.
        /// </summary>
        [Display(Order = 0)]
        public override XpoProvider Provider
        {
            get { return _provider; }
            set
            {
                SetProperty(ref _provider, value, () => Provider, () =>
                {
                    Settings.Default.ProviderId = Provider.Oid;
                    Settings.Default.Save();

                    // If the new provider doesn't support both server and file modes, make sure UseServer reflects the available mode
                    if (!(_provider.IsServerBased && _provider.IsFileBased))
                        UseServer = _provider.IsServerBased;

                    // If the new provider doesn't support integrated security, make sure UseIntegratedSecurity is set to false
                    if (!_provider.HasIntegratedSecurity)
                        UseIntegratedSecurity = false;
                });
            }
        }

        /// <summary>
        /// Indicates if a server connection should be used when the provider supports both server and file modes.
        /// </summary>
        [Display(Order = 1)]
        public override bool UseServer
        {
            get { return _useServer; }
            set
            {
                SetProperty(ref _useServer, value, () => UseServer, () =>
                {
                    Settings.Default.UseServer = UseServer;
                    Settings.Default.Save();
                });
            }
        }

        /// <summary>
        /// The name of the server to connect to when using server mode.
        /// </summary>
        [Display(Order = 2)]
        public override string ServerName
        {
            get { return _serverName; }
            set
            {
                SetProperty(ref _serverName, value, () => ServerName, () =>
                {
                    Settings.Default.ServerName = ServerName;
                    Settings.Default.Save();
                });
            }
        }

        /// <summary>
        /// The name of the database to connect to when using server mode.
        /// </summary>
        [Display(Order = 3)]
        public override string DatabaseName
        {
            get { return _databaseName; }
            set
            {
                SetProperty(ref _databaseName, value, () => DatabaseName, () =>
                {
                    Settings.Default.DatabaseName = DatabaseName;
                    Settings.Default.Save();
                });
            }
        }

        /// <summary>
        /// The name of the database to connect to when using server mode.
        /// </summary>
        [Display(Order = 4)]
        public override string DatabaseFile
        {
            get { return _databaseFile; }
            set
            {
                SetProperty(ref _databaseFile, value, () => DatabaseFile, () =>
                {
                    Settings.Default.DatabaseFile = DatabaseFile;
                    Settings.Default.Save();
                });
            }
        }

        /// <summary>
        /// Indicates if integrated security should be used when connecting to the database.
        /// </summary>
        [Display(Order = 5)]
        public override bool UseIntegratedSecurity
        {
            get { return _useIntegratedSecurity; }
            set
            {
                SetProperty(ref _useIntegratedSecurity, value, () => UseIntegratedSecurity, () =>
                {
                    Settings.Default.UseIntegratedSecurity = UseIntegratedSecurity;
                    Settings.Default.Save();
                });
            }
        }

        /// <summary>
        /// The name of the user to use when connecting to the database without integrated security.
        /// </summary>
        [Display(Order = 6)]
        public override string UserName
        {
            get { return _userName; }
            set
            {
                SetProperty(ref _userName, value, () => UserName, () =>
                {
                    Settings.Default.UserName = UserName;
                    Settings.Default.Save();
                });
            }
        }

        /// <summary>
        /// The password to use when connecting to the database without integrated security.
        /// </summary>
        [Display(Order = 7)]
        public override string Password
        {
            get { return _password; }
            set
            {
                SetProperty(ref _password, value, () => Password, () =>
                {
                    Settings.Default.Password = Password;
                    Settings.Default.Save();
                });
            }
        }

        #endregion


        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<RepositoryLocalDatabaseViewModel> builder)
        {
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<RepositoryLocalDatabaseViewModel> builder)
        {
        }

        #endregion
    }
}
