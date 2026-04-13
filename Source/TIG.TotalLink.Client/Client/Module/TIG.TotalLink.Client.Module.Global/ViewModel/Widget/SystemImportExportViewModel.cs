using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Xml;
using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using TIG.TotalLink.Client.Core.Command;
using TIG.TotalLink.Client.Core.Extension;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Client.Editor.Definition;
using TIG.TotalLink.Client.Editor.Extension;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.Message;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Client.Module.Global.Helper;
using TIG.TotalLink.Client.Module.Global.Properties;
using TIG.TotalLink.Shared.DataModel.Core.Enum;
using TIG.TotalLink.Shared.DataModel.Core.Enum.Admin;
using TIG.TotalLink.Shared.Facade.Core.Helper;
using TIG.TotalLink.Shared.Facade.Global;

namespace TIG.TotalLink.Client.Module.Global.ViewModel.Widget
{
    public class SystemImportExportViewModel : LocalDetailViewModelBase
    {
        #region Public Enums

        public enum ImportExportModes
        {
            None,
            Import,
            Export
        }

        #endregion


        #region Private Fields

        private readonly IGlobalFacade _globalFacade;
        private DatabaseDomain _databaseDomain = DatabaseDomain.Main;
        private string _path;
        private List<TableTreeItem> _allTables;
        private ObservableCollection<TableTreeItem> _tables;
        private string _importExportStatus;
        private bool _isImportExportActive;
        private ImportExportModes _importExportMode;
        private int _totalTableCount;
        private int _completedTableCount;
        private CancellationTokenSource _importExportCancellation;
        private CancellationTokenSource _refreshCancellation;
        private bool _importExportIsCancelling;
        private bool _loadingSettings;
        private bool _autoCheckingItems;
        private byte[] _pathErrorImage;
        private string _pathErrorMessage;
        private bool _isWaitIndicatorVisible;

        #endregion


        #region Constructors

        public SystemImportExportViewModel()
        {
        }

        public SystemImportExportViewModel(IGlobalFacade globalFacade)
        {
            // Store services
            _globalFacade = globalFacade;

            // Initialize commands
            RefreshCommand = new DelegateCommand(OnRefreshExecute, OnRefreshCanExecute);
            ImportTablesCommand = new AsyncCommandEx(OnImportTablesExecuteAsync, OnImportTablesCanExecute);
            ExportTablesCommand = new AsyncCommandEx(OnExportTablesExecuteAsync, OnExportTablesCanExecute);
            CancelImportExportCommand = new DelegateCommand(OnCancelImportExportExecute, OnCancelImportExportCanExecute);

            // Initialize the PathErrorImage
            PathErrorImage = new BitmapImage(new Uri("pack://application:,,,/TIG.TotalLink.Client.Module.Global;component/Image/16x16/Error.png", UriKind.Absolute)).GetBytes();
        }

        #endregion


        #region Commands

        /// <summary>
        /// Command to refresh the table list.
        /// </summary>
        [WidgetCommand("Refresh", "List", RibbonItemType.ButtonItem, "Refresh the table list.")]
        public ICommand RefreshCommand { get; private set; }

        /// <summary>
        /// Command to import the selected tables.
        /// </summary>
        [WidgetCommand("Import", "Selection", RibbonItemType.ButtonItem, "Import the selected tables.")]
        public ICommand ImportTablesCommand { get; private set; }

        /// <summary>
        /// Command to export the selected tables.
        /// </summary>
        [WidgetCommand("Export", "Selection", RibbonItemType.ButtonItem, "Export the selected tables.")]
        public ICommand ExportTablesCommand { get; private set; }

        /// <summary>
        /// Command to cancel the current import or export.
        /// </summary>
        public ICommand CancelImportExportCommand { get; private set; }

        #endregion


        #region Public Properties

        /// <summary>
        /// The database to import from and export to.
        /// </summary>
        public DatabaseDomain DatabaseDomain
        {
            get { return _databaseDomain; }
            set
            {
                SetProperty(ref _databaseDomain, value, () => DatabaseDomain, () =>
                {
                    if (_loadingSettings)
                        return;

                    Refresh(true);
                });
            }
        }

        /// <summary>
        /// The path to import from and export to.
        /// </summary>
        public string Path
        {
            get { return _path; }
            set
            {
                SetProperty(ref _path, value, () => Path, () =>
                {
                    if (_loadingSettings)
                        return;

                    Refresh();
                });
            }
        }

        /// <summary>
        /// The image to be displayed next to the PathErrorMessage.
        /// </summary>
        public byte[] PathErrorImage
        {
            get { return _pathErrorImage; }
            private set { SetProperty(ref _pathErrorImage, value, () => PathErrorImage); }
        }

        /// <summary>
        /// The current error message on the Path property.
        /// </summary>
        public string PathErrorMessage
        {
            get { return _pathErrorMessage; }
            set { SetProperty(ref _pathErrorMessage, value, () => PathErrorMessage); }
        }

        /// <summary>
        /// A label with additional notes about the DatabaseDomain.
        /// </summary>
        public string DatabaseDomainLabel
        {
            get { return null; }
        }

        /// <summary>
        /// A label with additional notes about the Path.
        /// </summary>
        public string PathLabel
        {
            get { return "(Path is relative to the database server.)"; }
        }

        /// <summary>
        /// The tables that can be selected for import/export.
        /// </summary>
        public ObservableCollection<TableTreeItem> Tables
        {
            get { return _tables; }
            private set { SetProperty(ref _tables, value, () => Tables); }
        }

        /// <summary>
        /// The tables that have been selected for import/export.
        /// </summary>
        public List<TableTreeItem> SelectedTables
        {
            get { return Tables != null ? Tables.Where(t => t.IsChecked).ToList() : new List<TableTreeItem>(); }
        }

        /// <summary>
        /// A string describing the current status of the import/export.
        /// </summary>
        public string ImportExportStatus
        {
            get { return _importExportStatus; }
            private set { SetProperty(ref _importExportStatus, value, () => ImportExportStatus); }
        }

        /// <summary>
        /// Indicates whether an import/export is currently in progress.
        /// </summary>
        public bool IsImportExportActive
        {
            get { return _isImportExportActive; }
            private set { SetProperty(ref _isImportExportActive, value, () => IsImportExportActive); }
        }

        /// <summary>
        /// Indicates whether an export is currently in progress.
        /// </summary>
        public ImportExportModes ImportExportMode
        {
            get { return _importExportMode; }
            private set { SetProperty(ref _importExportMode, value, () => ImportExportMode); }
        }

        /// <summary>
        /// The total number of tables that will be imported/exported.
        /// </summary>
        public int TotalTableCount
        {
            get { return _totalTableCount; }
            private set { SetProperty(ref _totalTableCount, value, () => TotalTableCount); }
        }

        /// <summary>
        /// The number of tables that have been imported/exported so far.
        /// </summary>
        public int CompletedTableCount
        {
            get { return _completedTableCount; }
            private set { SetProperty(ref _completedTableCount, value, () => CompletedTableCount); }
        }

        /// <summary>
        /// Indicates whether an import/export is currently being cancelled.
        /// </summary>
        public bool IsImportExportCancelling
        {
            get { return _importExportIsCancelling; }
            private set { SetProperty(ref _importExportIsCancelling, value, () => IsImportExportCancelling); }
        }

        /// <summary>
        /// Indicates whether the treeview should display the wait indicator.
        /// </summary>
        public bool IsWaitIndicatorVisible
        {
            get { return _isWaitIndicatorVisible; }
            private set { SetProperty(ref _isWaitIndicatorVisible, value, () => IsWaitIndicatorVisible); }
        }

        #endregion


        #region Private Properties

        /// <summary>
        /// Indicates if import/export operations can be performed on selected tables.
        /// </summary>
        private bool CanExecuteImportExportCommand
        {
            get
            {
                return _globalFacade.IsMethodConnected && _refreshCancellation == null &&
                    !(
                        (((AsyncCommandEx)ImportTablesCommand).IsExecuting) ||
                        (((AsyncCommandEx)ExportTablesCommand).IsExecuting)
                    );
            }
        }

        #endregion


        #region Private Methods

        private void LoadSettings()
        {
            _loadingSettings = true;

            DatabaseDomain databaseDomain;
            if (Enum.TryParse(Settings.Default.ImportExportDatabase, out databaseDomain))
                DatabaseDomain = databaseDomain;

            Path = Settings.Default.ImportExportPath;

            _loadingSettings = false;
        }

        private void SaveSettings()
        {
            Settings.Default.ImportExportDatabase = DatabaseDomain.ToString();
            Settings.Default.ImportExportPath = Path;
            Settings.Default.Save();
        }

        /// <summary>
        /// Imports or Exports tables.
        /// </summary>
        /// <param name="tables">The tables to export.</param>
        /// <param name="mode">The import/export mode that will be executed.</param>
        /// <param name="cancellationToken">A token for cancellation.</param>
        /// <returns>A boolean flag indicating if the import/export was accepted.</returns>
        private async Task<bool> ImportExportTablesAsync(List<TableTreeItem> tables, ImportExportModes mode, CancellationToken cancellationToken)
        {
            // Collect details about the database
            string databasePath;
            bool useServer;
            bool.TryParse(_globalFacade.GetDatabaseSetting(DatabaseDomain, "UseServer"), out useServer);
            if (useServer)
            {
                var serverName = _globalFacade.GetDatabaseSetting(DatabaseDomain, "ServerName");
                var databaseName = _globalFacade.GetDatabaseSetting(DatabaseDomain, "DatabaseName");
                databasePath = string.Format("{0}.{1}", serverName, databaseName);
            }
            else
            {
                var databaseFile = _globalFacade.GetDatabaseSetting(DatabaseDomain, "DatabaseFile");
                databasePath = string.Format("\"{0}\"", databaseFile);
            }

            // Build a warning message describing what is about to happen
            var direction = (mode == ImportExportModes.Import ? "into" : "from");
            var warningMessageBuilder = new StringBuilder(string.Format("Warning!\r\n\r\nYou are about to {0} {1} {2} {3} the database\r\n{4}\r\n", mode.ToString().ToLower(), tables.Count, tables.Count.Pluralize("table"), direction, databasePath));

            if (mode == ImportExportModes.Import)
            {
                warningMessageBuilder.AppendFormat("\r\nNote that all existing data will be deleted from the {0} before the new data is imported.\r\n", tables.Count.Pluralize("table"));
                var missingTableCount = tables.Count(t => string.IsNullOrWhiteSpace(t.ExistingFileVersion));
                if (missingTableCount > 0)
                    warningMessageBuilder.AppendFormat("Also, files for {0} {1} do not exist in the import path so {2} will remain empty.\r\n", missingTableCount, missingTableCount.Pluralize("table"), (missingTableCount > 1 ? "they" : "it"));
            }
            else
            {
                var existingTableCount = tables.Count(t => !string.IsNullOrWhiteSpace(t.ExistingFileVersion));
                if (existingTableCount > 0)
                    warningMessageBuilder.AppendFormat("\r\nNote that files for {0} {1} already exist in the export path and will be overwritten.\r\n", existingTableCount, existingTableCount.Pluralize("table"));
            }

            warningMessageBuilder.Append("\r\nAre you sure you wish to continue?");

            // Show the warning message
            if (MessageBoxService.Show(warningMessageBuilder.ToString(), mode.ToString(), MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                return false;

            // Start the import/export
            ImportExportMode = mode;
            LogStart();

            // If the mode is import, empty the target tables first
            if (mode == ImportExportModes.Import)
            {
                // Only process if cancellation has not been requested
                if (!cancellationToken.IsCancellationRequested)
                {
                    LogMessage("Cleaning target tables...");
                    try
                    {
                        await _globalFacade.EmptyTablesAsync(DatabaseDomain, tables.OrderByDescending(t => t.Id).Select(t => t.TableName).ToArray()).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        // Log the error
                        var serviceException = new ServiceExceptionHelper(ex);
                        LogMessage(string.Format("ERROR: Cleaning target tables.\r\n{0}", serviceException.Message));

                        LogFail();
                        return true;
                    }
                }
            }

            // Always import/export the User table first
            tables.Insert(0, new TableTreeItem()
            {
                TypeName = "TIG.TotalLink.Shared.DataModel.Admin.User",
                TableName = "User"
            });

            // Initialize the import/export counters
            ImportExportStatus = "Estimating time remaining...";
            TotalTableCount = tables.Count;
            var successCount = 0;
            var failCount = 0;
            var tableStartTime = DateTime.Now;
            var totalDuration = new TimeSpan();

            foreach (var table in tables.OrderBy(t => t.Id))
            {
                // Abort if cancellation has been requested
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    LogMessage(string.Format("{0}ing table {1}...", mode, table.TableName));
                    if (mode == ImportExportModes.Import)
                    {
                        // Call the server to perform the import
                        await _globalFacade.ImportTableAsync(DatabaseDomain, Path, table.TypeName).ConfigureAwait(false);
                    }
                    else
                    {
                        // Call the server to perform the export
                        await _globalFacade.ExportTableAsync(DatabaseDomain, Path, table.TypeName).ConfigureAwait(false);
                    }

                    // Increment the successCount
                    successCount++;
                }
                catch (Exception ex)
                {
                    // Increment the failCount
                    failCount++;

                    // Log the error
                    var serviceException = new ServiceExceptionHelper(ex);
                    LogMessage(string.Format("ERROR: {0}ing table {1}.\r\n{2}", mode, table.TableName, serviceException.Message));
                }

                // Calculate the time this table took to process and add it to the total time
                var tableEndTime = DateTime.Now;
                var tableDuration = tableEndTime - tableStartTime;
                tableStartTime = tableEndTime;
                totalDuration += tableDuration;

                // Update counts
                CompletedTableCount++;

                // Calculate and display the time remaining
                var averageReleaseTicks = Math.Round((double)totalDuration.Ticks / (double)CompletedTableCount);
                var remainingDuration = new TimeSpan((long)Math.Round((double)(TotalTableCount - CompletedTableCount) * averageReleaseTicks));
                ImportExportStatus = string.Format("About {0} remaining", remainingDuration.Format());
            }

            // Log the number of tables processed
            LogMessage(string.Format("{0}ed {1:N0} {2} in {3}.", ImportExportMode, tables.Count, tables.Count.Pluralize("table"), totalDuration.Format()));

            // Log the count of successful and failed tables
            LogMessage(string.Format("Successful = {0:N0}", successCount));
            LogMessage(string.Format("Failed = {0:N0}", failCount));

            if (cancellationToken.IsCancellationRequested) // If cancellation was requested, log it
                LogCancelled();
            else if (failCount == 0) // Log success if there were no failures
                LogSuccess();
            else // Otherwise it must be a fail
                LogFail();

            return true;
        }

        /// <summary>
        /// Sends a message to the log widget.
        /// </summary>
        /// <param name="message">The message to send.</param>
        private void LogMessage(string message)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                SendDocumentMessage(new AppendLogMessage(this, message))
                ));
        }

        /// <summary>
        /// Sends a message to the log widget to indicate that the import/export has started.
        /// </summary>
        private void LogStart()
        {
            CompletedTableCount = 0;
            TotalTableCount = 0;
            IsImportExportActive = true;
            ImportExportStatus = "Preparing...";

            LogMessage(string.Format("*** {0} START ***", ImportExportMode.ToString().ToUpper()));
        }

        /// <summary>
        /// Sends a message to the log widget to indicate that the import/export was successful.
        /// </summary>
        private void LogSuccess()
        {
            IsImportExportActive = false;
            CompletedTableCount = 0;
            TotalTableCount = 0;

            LogMessage(string.Format("*** {0} SUCCESSFUL ***", ImportExportMode.ToString().ToUpper()));
        }

        /// <summary>
        /// Sends a message to the log widget to indicate that the upload has failed.
        /// </summary>
        private void LogFail()
        {
            IsImportExportActive = false;
            CompletedTableCount = 0;
            TotalTableCount = 0;

            LogMessage(string.Format("*** {0} FAILED ***", ImportExportMode.ToString().ToUpper()));
        }

        /// <summary>
        /// Sends a message to the log widget to indicate that the upload was cancelled.
        /// </summary>
        private void LogCancelled()
        {
            IsImportExportActive = false;
            IsImportExportCancelling = false;
            CompletedTableCount = 0;
            TotalTableCount = 0;

            LogMessage(string.Format("*** {0} CANCELLED ***", ImportExportMode.ToString().ToUpper()));
        }

        /// <summary>
        /// Loads information about import/export tables from an xml file.
        /// </summary>
        private void LoadTablesFromXml()
        {
            // Get a stream that refers to the resource
            var resource =
                Application.GetResourceStream(
                    new Uri("pack://application:,,,/TIG.TotalLink.ServerManager;component/Data/ImportExport.xml"));
            if (resource == null)
                return;

            // Load the resource into an XmlDocument
            var doc = new XmlDocument();
            using (var stream = resource.Stream)
            {
                doc.Load(stream);
            }

            // Get the root element of the XmlDocument
            var rootElement = doc.DocumentElement;
            if (rootElement == null)
                return;

            // Recursively load all elements
            _allTables = new List<TableTreeItem>();
            var nextId = 0;
            var databaseDomain = DatabaseDomain.Main;
            ProcessChildElementsRecursive(ref nextId, ref databaseDomain, _allTables, rootElement, null);
        }

        /// <summary>
        /// Processes elements in the Import/Export xml file.
        /// </summary>
        /// <param name="nextId">The nextId for new TableTreeItems.</param>
        /// <param name="databaseDomain">The current DatabaseDomain for TableTreeItems.</param>
        /// <param name="tables">The list that TableTreeItems will be added to.</param>
        /// <param name="element">The xml element to process.</param>
        /// <param name="parent">The parent TableTreeItem that new TableTreeItems will become children of.</param>
        private void ProcessChildElementsRecursive(ref int nextId, ref DatabaseDomain databaseDomain,
            List<TableTreeItem> tables, XmlElement element, TableTreeItem parent)
        {
            foreach (var childElement in element.ChildNodes.OfType<XmlElement>())
            {
                switch (childElement.Name)
                {
                    case "Database":
                        databaseDomain = childElement.GetEnumAttribute<DatabaseDomain>("Domain");
                        ProcessChildElementsRecursive(ref nextId, ref databaseDomain, tables, childElement, parent);
                        break;

                    case "Table":
                        var tableTreeItem = new TableTreeItem()
                        {
                            Id = nextId++,
                            ParentId = (parent != null ? (int?)parent.Id : null),
                            TypeName = childElement.GetAttribute("TypeName"),
                            TableName = childElement.GetAttribute("TableName"),
                            DatabaseDomain = databaseDomain
                        };
                        tableTreeItem.PropertyChanged += TableTreeItem_PropertyChanged;
                        tables.Add(tableTreeItem);
                        ProcessChildElementsRecursive(ref nextId, ref databaseDomain, tables, childElement,
                            tableTreeItem);
                        break;
                }
            }
        }

        /// <summary>
        /// Starts an asynchronous task to refresh the table information.
        /// </summary>
        /// <param name="localOnly">Indicates if the refresh is local only.</param>
        private void Refresh(bool localOnly = false)
        {
            // Cancel any refresh that is already in progress
            if (_refreshCancellation != null)
                _refreshCancellation.Cancel();

            // Create a cancellation token
            _refreshCancellation = new CancellationTokenSource();
            var cancellationToken = _refreshCancellation.Token;

            // Start the refresh
            IsWaitIndicatorVisible = true;
            Task.Run(async () => await RefreshAsync(localOnly, cancellationToken), cancellationToken)
                // ReSharper disable once MethodSupportsCancellation
                .ContinueWith(t =>
                {
                    _refreshCancellation = null;
                    Application.Current.Dispatcher.Invoke(CommandManager.InvalidateRequerySuggested);
                    IsWaitIndicatorVisible = false;
                });
        }

        /// <summary>
        /// Asynchronously refreshes the table information.
        /// </summary>
        /// <param name="localOnly">Indicates if the refresh is local only.</param>
        /// <param name="cancellationToken">A token for cancellation.</param>
        private async Task RefreshAsync(bool localOnly, CancellationToken cancellationToken)
        {
            PathErrorMessage = null;

            try
            {
                // Set the list of tables shown in the treeview to be a subset of _allTables which match the current DatabaseDomain
                var tables = new ObservableCollection<TableTreeItem>(_allTables.Where(t => t.DatabaseDomain == DatabaseDomain));
                Tables = tables;

                // If the refresh is local only, stop here
                if (localOnly)
                    return;

                // Clear the ExistingFileVersion on all tables
                foreach (var tableTreeItem in _allTables)
                {
                    // Abort if cancellation has been requested
                    if (cancellationToken.IsCancellationRequested)
                        return;

                    tableTreeItem.ExistingFileVersion = null;
                }

                try
                {
                    // Abort if cancellation has been requested
                    if (cancellationToken.IsCancellationRequested)
                        return;

                    // Call the server to get a list of export files
                    var listExportFilesResult = await _globalFacade.ListExportFilesAsync(Path).ConfigureAwait(false);

                    // Process the results
                    foreach (var exportFileResult in listExportFilesResult.ExportFiles)
                    {
                        // Abort if cancellation has been requested
                        if (cancellationToken.IsCancellationRequested)
                            return;

                        // Store the correct version on the table
                        var tableTreeItem = _allTables.FirstOrDefault(t => t.TableName == exportFileResult.TableName);
                        if (tableTreeItem != null)
                            tableTreeItem.ExistingFileVersion = exportFileResult.Version;
                    }
                }
                catch (Exception ex)
                {
                    var serviceException = new ServiceExceptionHelper(ex);
                    PathErrorMessage = serviceException.Message;
                }
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    MessageBoxService.Show(
                        string.Format("Error refreshing import/export tables.\r\n\r\n{0}", ex.Message), "Server Manager",
                        MessageBoxButton.OK, MessageBoxImage.Exclamation)
                    ));
            }
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Execute method for the RefreshCommand.
        /// </summary>
        private void OnRefreshExecute()
        {
            Refresh();
        }

        /// <summary>
        /// CanExecute method for the RefreshCommand.
        /// </summary>
        private bool OnRefreshCanExecute()
        {
            return CanExecuteWidgetCommand && CanExecuteImportExportCommand;
        }

        /// <summary>
        /// Execute method for the ImportTablesCommand.
        /// </summary>
        private async Task OnImportTablesExecuteAsync()
        {
            // Create a cancellation token
            _importExportCancellation = new CancellationTokenSource();
            var cancellationToken = _importExportCancellation.Token;

            // Perform the export
            var accepted = false;
            try
            {
                accepted = await ImportExportTablesAsync(new List<TableTreeItem>(SelectedTables), ImportExportModes.Import, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Ignore cancellation errors
            }

            // Clear the cancellation token
            _importExportCancellation = null;

            if (accepted)
            {
                // Save settings
                SaveSettings();
            }
        }

        /// <summary>
        /// CanExecute method for the ImportTablesCommand.
        /// </summary>
        private bool OnImportTablesCanExecute()
        {
            return CanExecuteWidgetCommand && CanExecuteImportExportCommand && SelectedTables.Count > 0;
        }

        /// <summary>
        /// Execute method for the ExportTablesCommand.
        /// </summary>
        private async Task OnExportTablesExecuteAsync()
        {
            // Create a cancellation token
            _importExportCancellation = new CancellationTokenSource();
            var cancellationToken = _importExportCancellation.Token;

            // Perform the export
            var accepted = false;
            try
            {
                accepted = await ImportExportTablesAsync(new List<TableTreeItem>(SelectedTables), ImportExportModes.Export, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Ignore cancellation errors
            }

            // Clear the cancellation token
            _importExportCancellation = null;

            if (accepted)
            {
                // Save settings
                SaveSettings();

                // Refresh the table list
                Refresh();
            }
        }

        /// <summary>
        /// CanExecute method for the ExportTablesCommand.
        /// </summary>
        private bool OnExportTablesCanExecute()
        {
            return CanExecuteWidgetCommand && CanExecuteImportExportCommand && SelectedTables.Count > 0;
        }

        /// <summary>
        /// Execute method for the CancelImportExportCommand.
        /// </summary>
        private void OnCancelImportExportExecute()
        {
            if (MessageBoxService.Show(
                string.Format("Warning: If you cancel the {0}, tables that have already been processed will not be rolled back!\r\n\r\nAre you sure?", ImportExportMode),
                string.Format("Cancel {0}", ImportExportMode), MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                return;

            IsImportExportCancelling = true;
            ImportExportStatus = "Cancelling...";
            _importExportCancellation.Cancel();
        }

        /// <summary>
        /// CanExecute method for the CancelImportExportCommand.
        /// </summary>
        private bool OnCancelImportExportCanExecute()
        {
            return (IsImportExportActive && !IsImportExportCancelling && _importExportCancellation != null);
        }

        /// <summary>
        /// Handles the PropertyChanged event for each TableTreeItem
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TableTreeItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Abort if we are already auto checking items
            if (_autoCheckingItems)
                return;

            // Attempt to get the sender as a TableTreeItem
            var tableTreeItem = sender as TableTreeItem;
            if (tableTreeItem == null)
                return;

            // If the IsChecked property has changed...
            if (e.PropertyName == "IsChecked")
            {
                _autoCheckingItems = true;

                // Set the check state of all child items to match this item
                CheckAllChildrenRecursive(tableTreeItem, tableTreeItem.IsChecked);

                // If the item was checked, check all parents
                if (tableTreeItem.IsChecked)
                    CheckAllParentsRecursive(tableTreeItem, true);

                _autoCheckingItems = false;
            }
        }

        /// <summary>
        /// Recursively checks or unchecks all child TableTreeItems.
        /// </summary>
        /// <param name="parentItem">The TableTreeItem to modify children of.</param>
        /// <param name="isChecked">Indicates if children should be checked or unchecked.</param>
        private void CheckAllChildrenRecursive(TableTreeItem parentItem, bool isChecked)
        {
            foreach (var childItem in _allTables.Where(t => t.ParentId == parentItem.Id))
            {
                childItem.IsChecked = isChecked;
                CheckAllChildrenRecursive(childItem, isChecked);
            }
        }

        /// <summary>
        /// Recursively checks or unchecks all parent TableTreeItems.
        /// </summary>
        /// <param name="childItem">The TableTreeItem to modify parents of.</param>
        /// <param name="isChecked">Indicates if parents should be checked or unchecked.</param>
        private void CheckAllParentsRecursive(TableTreeItem childItem, bool isChecked)
        {
            while (childItem.ParentId.HasValue)
            {
                var parentItem = _allTables.First(t => t.Id == childItem.ParentId.Value);
                parentItem.IsChecked = isChecked;
                childItem = parentItem;
            }
        }

        #endregion


        #region Overrides

        protected override void OnWidgetLoaded(EventArgs e)
        {
            base.OnWidgetLoaded(e);

            AddStartupTask(() =>
            {
                // Attempt to connect to the GlobalFacade
                ConnectToFacade(_globalFacade);

                // Initialize the settings
                LoadSettings();
                LoadTablesFromXml();
                Refresh();
            });
        }

        protected override void OnWidgetClosed(EventArgs e)
        {
            base.OnWidgetClosed(e);

            // Save settings
            SaveSettings();

            // Stop handling events
            if (_allTables != null)
            {
                foreach (var tableTreeItem in _allTables)
                {
                    tableTreeItem.PropertyChanged -= TableTreeItem_PropertyChanged;
                }
            }
        }

        #endregion


        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<SystemImportExportViewModel> builder)
        {
            builder.DataFormLayout()
                .Group("Database", Orientation.Horizontal)
                    .ContainsProperty(p => p.DatabaseDomain)
                    .ContainsProperty(p => p.DatabaseDomainLabel)
                .EndGroup()
                .Group("Path", Orientation.Horizontal)
                    .ContainsProperty(p => p.Path)
                    .ContainsProperty(p => p.PathLabel)
                .EndGroup()
                .Group("PathError", Orientation.Horizontal)
                    .ContainsProperty(p => p.PathErrorImage)
                    .ContainsProperty(p => p.PathErrorMessage);

            builder.Property(p => p.DatabaseDomain).DisplayName("Database");
            builder.Property(p => p.Path).DisplayName("Import/Export Path");

            builder.Property(p => p.PathErrorImage).AutoGenerated();
            builder.Property(p => p.SelectedTables).NotAutoGenerated();
            builder.Property(p => p.ImportExportStatus).NotAutoGenerated();
            builder.Property(p => p.IsImportExportActive).NotAutoGenerated();
            builder.Property(p => p.ImportExportMode).NotAutoGenerated();
            builder.Property(p => p.TotalTableCount).NotAutoGenerated();
            builder.Property(p => p.CompletedTableCount).NotAutoGenerated();
            builder.Property(p => p.IsImportExportCancelling).NotAutoGenerated();
            builder.Property(p => p.IsWaitIndicatorVisible).NotAutoGenerated();
            builder.Property(p => p.RefreshCommand).NotAutoGenerated();
            builder.Property(p => p.ImportTablesCommand).NotAutoGenerated();
            builder.Property(p => p.ExportTablesCommand).NotAutoGenerated();
            builder.Property(p => p.CancelImportExportCommand).NotAutoGenerated();
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<SystemImportExportViewModel> builder)
        {
            builder.Condition(context => context != null && !string.IsNullOrWhiteSpace(context.PathErrorMessage))
                .ContainsProperty(p => p.PathErrorMessage)
                .AffectsPropertyVisibility(p => p.PathErrorImage);

            builder.Property(p => p.DatabaseDomain)
                .ReplaceEditor(new ComboEditorDefinition(typeof(DatabaseDomain)));

            builder.Property(p => p.DatabaseDomainLabel)
                .ReplaceEditor(new LabelEditorDefinition())
                .HideLabel()
                .ControlWidth(210);

            builder.Property(p => p.PathLabel)
                .ReplaceEditor(new LabelEditorDefinition())
                .HideLabel()
                .ControlWidth(210);

            builder.Property(p => p.PathErrorImage)
                .ReplaceEditor(new ImageEditorDefinition() { ShowBorder = false })
                .HideLabel()
                .ControlWidth(16);

            builder.Property(p => p.PathErrorMessage)
                .ReplaceEditor(new LabelEditorDefinition())
                .HideLabel();
        }

        #endregion
    }
}
