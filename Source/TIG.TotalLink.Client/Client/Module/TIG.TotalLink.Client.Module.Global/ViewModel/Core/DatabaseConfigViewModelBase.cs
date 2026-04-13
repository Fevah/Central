using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Xpo;
using TIG.TotalLink.Client.Core.Command;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Client.Editor.Definition;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Shared.DataModel.Core.Enum;
using TIG.TotalLink.Shared.DataModel.Core.Enum.Admin;
using TIG.TotalLink.Shared.DataModel.Global;
using TIG.TotalLink.Shared.Facade.Core.Helper;
using TIG.TotalLink.Shared.Facade.Global;

namespace TIG.TotalLink.Client.Module.Global.ViewModel.Core
{
    public abstract class DatabaseConfigViewModelBase : LocalDetailViewModelBase
    {
        #region Private Fields

        private XpoProvider _provider;
        private bool _useServer;
        private string _serverName;
        private string _databaseName;
        private string _databaseFile;
        private bool _useIntegratedSecurity;
        private string _userName;
        private string _password;

        protected readonly IGlobalFacade GlobalFacade;
        protected readonly DatabaseDomain DatabaseDomain;

        #endregion


        #region Constructors

        protected DatabaseConfigViewModelBase()
        {
        }

        protected DatabaseConfigViewModelBase(DatabaseDomain databaseDomain, IGlobalFacade globalFacade)
            : this()
        {
            DatabaseDomain = databaseDomain;
            GlobalFacade = globalFacade;

            // Initialize commands
            TestDatabaseCommand = new AsyncCommandEx<bool>(OnUpdateDatabaseExecuteAsync, OnUpdateDatabaseCanExecute);
            UpdateDatabaseCommand = TestDatabaseCommand;
            PurgeDatabaseCommand = new AsyncCommandEx(OnPurgeDatabaseExecuteAsync, OnPurgeDatabaseCanExecute);
        }

        #endregion


        #region Commands

        /// <summary>
        /// Command to test the database.
        /// </summary>
        [WidgetCommand("Test", "Database", RibbonItemType.ButtonItem, "Test the database.", CommandParameter = false)]
        [Display(AutoGenerateField = false)]
        public ICommand TestDatabaseCommand { get; private set; }

        /// <summary>
        /// Command to update the database.
        /// </summary>
        [WidgetCommand("Update", "Database", RibbonItemType.ButtonItem, "Update the database.", CommandParameter = true)]
        [Display(AutoGenerateField = false)]
        public ICommand UpdateDatabaseCommand { get; private set; }

        /// <summary>
        /// Command to purge deleted objects from the database.
        /// </summary>
        [WidgetCommand("Purge", "Database", RibbonItemType.ButtonItem, "Purge deleted items from the database.")]
        [Display(AutoGenerateField = false)]
        public ICommand PurgeDatabaseCommand { get; private set; }

        // TODO : ShowXpoProviderDetailsCommand?

        #endregion


        #region Public Properties

        /// <summary>
        /// The XpoProvider to use for database connectivity.
        /// </summary>
        [Display(Order = 0)]
        public virtual XpoProvider Provider
        {
            get { return _provider; }
            set
            {
                SetProperty(ref _provider, value, () => Provider, async () =>
                {
                    await GlobalFacade.SetDatabaseSettingAsync(DatabaseDomain, "Provider", Provider.Oid.ToString());

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
        public virtual bool UseServer
        {
            get { return _useServer; }
            set
            {
                SetProperty(ref _useServer, value, () => UseServer, async () =>
                {
                    await GlobalFacade.SetDatabaseSettingAsync(DatabaseDomain, "UseServer", UseServer.ToString());
                });
            }
        }

        /// <summary>
        /// The name of the server to connect to when using server mode.
        /// </summary>
        [Display(Order = 2)]
        public virtual string ServerName
        {
            get { return _serverName; }
            set
            {
                SetProperty(ref _serverName, value, () => ServerName, async () =>
                {
                    await GlobalFacade.SetDatabaseSettingAsync(DatabaseDomain, "ServerName", ServerName);
                });
            }
        }

        /// <summary>
        /// The name of the database to connect to when using server mode.
        /// </summary>
        [Display(Order = 3)]
        public virtual string DatabaseName
        {
            get { return _databaseName; }
            set
            {
                SetProperty(ref _databaseName, value, () => DatabaseName, async () =>
                {
                    await GlobalFacade.SetDatabaseSettingAsync(DatabaseDomain, "DatabaseName", DatabaseName);
                });
            }
        }

        /// <summary>
        /// The name of the database to connect to when using server mode.
        /// </summary>
        [Display(Order = 4)]
        public virtual string DatabaseFile
        {
            get { return _databaseFile; }
            set
            {
                SetProperty(ref _databaseFile, value, () => DatabaseFile, async () =>
                {
                    await GlobalFacade.SetDatabaseSettingAsync(DatabaseDomain, "DatabaseFile", DatabaseFile);
                });
            }
        }

        /// <summary>
        /// Indicates if integrated security should be used when connecting to the database.
        /// </summary>
        [Display(Order = 5)]
        public virtual bool UseIntegratedSecurity
        {
            get { return _useIntegratedSecurity; }
            set
            {
                SetProperty(ref _useIntegratedSecurity, value, () => UseIntegratedSecurity, async () =>
                {
                    await GlobalFacade.SetDatabaseSettingAsync(DatabaseDomain, "UseIntegratedSecurity", UseIntegratedSecurity.ToString());
                });
            }
        }

        /// <summary>
        /// The name of the user to use when connecting to the database without integrated security.
        /// </summary>
        [Display(Order = 6)]
        public virtual string UserName
        {
            get { return _userName; }
            set
            {
                SetProperty(ref _userName, value, () => UserName, async () =>
                {
                    await GlobalFacade.SetDatabaseSettingAsync(DatabaseDomain, "UserName", UserName);
                });
            }
        }

        /// <summary>
        /// The password to use when connecting to the database without integrated security.
        /// </summary>
        [Display(Order = 7)]
        public virtual string Password
        {
            get { return _password; }
            set
            {
                SetProperty(ref _password, value, () => Password, async () =>
                {
                    await GlobalFacade.SetDatabaseSettingAsync(DatabaseDomain, "Password", Password);
                });
            }
        }

        #endregion


        #region Private Properties

        /// <summary>
        /// Indicates if a database command can be executed, based on whether any related operations are in progress.
        /// </summary>
        private bool CanExecuteDatabaseCommand
        {
            get
            {
                return GlobalFacade.IsMethodConnected && Provider != null &&
                    !(
                        (((AsyncCommandEx<bool>)TestDatabaseCommand).IsExecuting) ||
                        (((AsyncCommandEx<bool>)UpdateDatabaseCommand).IsExecuting) ||
                        (((AsyncCommandEx)PurgeDatabaseCommand).IsExecuting)
                    );
            }
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Loads all settings from the database into the viewmodel.
        /// </summary>
        protected virtual void LoadSettings()
        {
            // Initialize settings
            Guid providerId;
            if (Guid.TryParse(GlobalFacade.GetDatabaseSetting(DatabaseDomain, "Provider"), out providerId))
            {
                _provider = (GlobalFacade.ExecuteQuery(uow =>
                    uow.Query<XpoProvider>().Where(p => p.Oid == providerId)
                )).FirstOrDefault();
            }

            bool useServer;
            if (bool.TryParse(GlobalFacade.GetDatabaseSetting(DatabaseDomain, "UseServer"), out useServer))
                _useServer = useServer;

            _serverName = GlobalFacade.GetDatabaseSetting(DatabaseDomain, "ServerName");
            _databaseName = GlobalFacade.GetDatabaseSetting(DatabaseDomain, "DatabaseName");
            _databaseFile = GlobalFacade.GetDatabaseSetting(DatabaseDomain, "DatabaseFile");

            bool useIntegratedSecurity;
            if (bool.TryParse(GlobalFacade.GetDatabaseSetting(DatabaseDomain, "UseIntegratedSecurity"), out useIntegratedSecurity))
                _useIntegratedSecurity = useIntegratedSecurity;

            _userName = GlobalFacade.GetDatabaseSetting(DatabaseDomain, "UserName");
            _password = GlobalFacade.GetDatabaseSetting(DatabaseDomain, "Password");

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


        #region Event Handlers

        /// <summary>
        /// Execute method for the TestDatabaseCommand and UpdateDatabaseCommand.
        /// </summary>
        protected virtual async Task OnUpdateDatabaseExecuteAsync(bool performUpdate)
        {
            try
            {
                await GlobalFacade.UpdateDatabaseAsync(DatabaseDomain, performUpdate);

                if (performUpdate)
                {
                    await GlobalFacade.PopulateDataStoreAsync(DatabaseDomain);
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
                MessageBoxService.Show(string.Format("{0} database failed!\r\n\r\n{1}", mode, serviceException.Message), string.Format("{0} Database", mode), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// CanExecute method for the TestDatabaseCommand and UpdateDatabaseCommand.
        /// </summary>
        protected virtual bool OnUpdateDatabaseCanExecute(bool performUpdate)
        {
            return CanExecuteWidgetCommand && CanExecuteDatabaseCommand;
        }

        /// <summary>
        /// Execute method for the PurgeDatabaseCommand.
        /// </summary>
        protected virtual async Task OnPurgeDatabaseExecuteAsync()
        {
            try
            {
                var result = await GlobalFacade.PurgeDatabaseAsync(DatabaseDomain);
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
        /// CanExecute method for the PurgeDatabaseCommand.
        /// </summary>
        protected virtual bool OnPurgeDatabaseCanExecute()
        {
            return CanExecuteWidgetCommand && CanExecuteDatabaseCommand;
        }

        #endregion


        #region Overrides

        protected override void OnWidgetLoaded(EventArgs e)
        {
            base.OnWidgetLoaded(e);

            AddStartupTask(() =>
            {
                // Attempt to connect to the GlobalFacade
                ConnectToFacade(GlobalFacade);

                // Initialize the settings
                LoadSettings();
            });
        }

        #endregion


        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<DatabaseConfigViewModelBase> builder)
        {
            builder.DataFormLayout()
                .ContainsProperty(p => p.Provider)
                .GroupBox("Connection")
                    .ContainsProperty(p => p.UseServer)
                    .ContainsProperty(p => p.ServerName)
                    .ContainsProperty(p => p.DatabaseName)
                    .ContainsProperty(p => p.DatabaseFile)
                .EndGroup()
                .GroupBox("Authentication")
                    .ContainsProperty(p => p.UseIntegratedSecurity)
                    .ContainsProperty(p => p.UserName)
                    .ContainsProperty(p => p.Password);
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<DatabaseConfigViewModelBase> builder)
        {
            builder.Condition(i => i != null && i.Provider != null && i.Provider.IsServerBased && i.Provider.IsFileBased)
                .ContainsProperty(p => p.Provider)
                .AffectsPropertyEnabled(p => p.UseServer);

            builder.Condition(i => i != null && i.Provider != null && i.UseServer && i.Provider.IsServerBased)
                .ContainsProperty(p => p.Provider)
                .ContainsProperty(p => p.UseServer)
                .AffectsPropertyEnabled(p => p.ServerName)
                .AffectsPropertyEnabled(p => p.DatabaseName);

            builder.Condition(i => i != null && i.Provider != null && !i.UseServer && i.Provider.IsFileBased)
                .ContainsProperty(p => p.Provider)
                .ContainsProperty(p => p.UseServer)
                .AffectsPropertyEnabled(p => p.DatabaseFile);

            builder.Condition(i => i != null && i.Provider != null && i.Provider.HasIntegratedSecurity)
                .ContainsProperty(p => p.Provider)
                .AffectsPropertyEnabled(p => p.UseIntegratedSecurity);

            builder.Condition(i => i != null && i.Provider != null && !i.UseIntegratedSecurity && i.Provider.HasUserName)
                .ContainsProperty(p => p.Provider)
                .ContainsProperty(p => p.UseIntegratedSecurity)
                .AffectsPropertyEnabled(p => p.UserName);

            builder.Condition(i => i != null && i.Provider != null && !i.UseIntegratedSecurity && i.Provider.HasPassword)
                .ContainsProperty(p => p.Provider)
                .ContainsProperty(p => p.UseIntegratedSecurity)
                .AffectsPropertyEnabled(p => p.Password);

            builder.Property(p => p.Password).ReplaceEditor(new PasswordEditorDefinition());

            var lookupEditor = new LookUpEditorDefinition()
            {
                EntityType = typeof(XpoProvider),
                DisplayMember = "Name"
            };
            builder.Property(p => p.Provider).ReplaceEditor(lookupEditor);

            // TODO : FileName editor for DatabaseFile property
        }

        #endregion
    }
}
