using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using DevExpress.Mvvm;
using DevExpress.Mvvm.Native;
using TIG.TotalLink.Client.Core.Command;
using TIG.TotalLink.Client.Core.Extension;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Client.Editor.Wrapper.Editor;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.Message;
using TIG.TotalLink.Client.Module.Admin.Uploader.Core;
using TIG.TotalLink.Shared.DataModel.Core.Enum.Admin;

namespace TIG.TotalLink.Client.Module.Admin.ViewModel.Core
{
    [SendsDocumentMessage(typeof(AppendLogMessage))]
    public abstract class UploaderViewModelBase<T> : WidgetViewModelBase //, ISupportLayoutData
        where T : UploaderDataModelBase, new()
    {
        #region Private Fields

        //private static byte[] _defaultLayout;
        private string _uploadStatus;
        private bool _isUploadActive;
        private int _totalRowCount;
        private int _completedRowCount;
        private CancellationTokenSource _uploadCancellation;
        private bool _isCancelling;

        #endregion


        #region Constructors

        protected UploaderViewModelBase()
        {
            UploadBatchSize = 100;

            // Initialize collections
            ItemsSource = new ObservableCollection<T>();
            SelectedItems = new ObservableCollection<T>();

            // Initialize commands
            UploadAllCommand = new AsyncCommandEx(OnUploadAllExecuteAsync, OnUploadAllCanExecute);
            UploadSelectedCommand = new AsyncCommandEx(OnUploadSelectedExecuteAsync, OnUploadSelectedCanExecute);
            CancelUploadCommand = new DelegateCommand(OnCancelUploadExecute, OnCancelUploadCanExecute);

            // Populate the columns
            PopulateColumns();
        }

        #endregion


        #region Commands

        /// <summary>
        /// Command to upload all rows.
        /// </summary>
        [WidgetCommand("Upload All", "Upload", RibbonItemType.ButtonItem, "Upload all rows.")]
        public ICommand UploadAllCommand { get; private set; }

        /// <summary>
        /// Command to upload selected rows.
        /// </summary>
        [WidgetCommand("Upload Selected", "Upload", RibbonItemType.ButtonItem, "Upload the selected rows.")]
        public ICommand UploadSelectedCommand { get; private set; }

        /// <summary>
        /// Command to cancel the current upload.
        /// </summary>
        public ICommand CancelUploadCommand { get; private set; }

        #endregion


        #region Public Properties

        /// <summary>
        /// All columns that this uploader displays.
        /// </summary>
        public ObservableCollection<GridColumnWrapper> Columns { get; private set; }

        /// <summary>
        /// The source of items.
        /// </summary>
        public ObservableCollection<T> ItemsSource { get; private set; }

        /// <summary>
        /// All items that are currently selected.
        /// </summary>
        public ObservableCollection<T> SelectedItems { get; private set; }

        /// <summary>
        /// A string describing the current status of the upload.
        /// </summary>
        public string UploadStatus
        {
            get { return _uploadStatus; }
            private set { SetProperty(ref _uploadStatus, value, () => UploadStatus); }
        }

        /// <summary>
        /// Indicates whether an upload is currently in progress or not.
        /// </summary>
        public bool IsUploadActive
        {
            get { return _isUploadActive; }
            private set { SetProperty(ref _isUploadActive, value, () => IsUploadActive); }
        }

        /// <summary>
        /// The total number of rows that will be uploaded.
        /// </summary>
        public int TotalRowCount
        {
            get { return _totalRowCount; }
            private set { SetProperty(ref _totalRowCount, value, () => TotalRowCount); }
        }

        /// <summary>
        /// The number of rows that have been uploaded so far.
        /// </summary>
        public int CompletedRowCount
        {
            get { return _completedRowCount; }
            private set { SetProperty(ref _completedRowCount, value, () => CompletedRowCount); }
        }

        /// <summary>
        /// Indicates whether an upload is currently being cancelled.
        /// </summary>
        public bool IsCancelling
        {
            get { return _isCancelling; }
            private set { SetProperty(ref _isCancelling, value, () => IsCancelling); }
        }

        #endregion


        #region Protected Properties

        /// <summary>
        /// The number of rows to cache before writing a batch.
        /// </summary>
        protected int UploadBatchSize { get; set; }

        #endregion


        #region Private Properties

        /// <summary>
        /// Indicates if an uploader command can be executed, based on whether any related operations are in progress.
        /// </summary>
        private bool CanExecuteUploaderCommand
        {
            get
            {
                return !(
                    ((AsyncCommandEx)UploadAllCommand).IsExecuting ||
                    ((AsyncCommandEx)UploadSelectedCommand).IsExecuting
                );
            }
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Creates columns for all properties on the data model.
        /// </summary>
        private void PopulateColumns()
        {
            // Create column wrappers for each visible property on the entity being displayed
            var columns = new ObservableCollection<GridColumnWrapper>();
            foreach (var propertyWrapper in typeof(T).GetVisibleAndAliasedProperties(LayoutType.Table))
            {
                columns.Add(new GridColumnWrapper(propertyWrapper));
            }

            // Call the EditorMetadataBuilder to allow extended editor customisation
            EditorMetadataBuilder.Build<T>(columns);

            // Store the columns
            Columns = columns;
        }

        /// <summary>
        /// Uploads data.
        /// </summary>
        /// <param name="data">The data to upload.</param>
        /// <param name="cancellationToken">A token for cancellation.</param>
        private void UploadData(List<T> data, CancellationToken cancellationToken)
        {
            LogStart();

            // Get all items that have not already been uploaded
            var unprocessedData = data.Where(d => d.UploadResult == UploaderResult.None || d.UploadResult == UploaderResult.Failed || d.UploadResult == UploaderResult.Cancelled).ToList();
            TotalRowCount = unprocessedData.Count;

            // Log a message if some rows have already been uploaded
            if (unprocessedData.Count < data.Count)
            {
                var processedCount = data.Count - unprocessedData.Count;
                LogMessage(string.Format("{0:N0} {1} {2} already been uploaded.", processedCount, processedCount.Pluralize("row"), (processedCount == 1 ? "has" : "have")));
            }

            // Initialize the uploader
            UploadStatus = "Initializing...";
            LogMessage("Initializing...");
            try
            {
                InitializeUpload();
            }
            catch (Exception ex)
            {
                LogMessage(string.Format("ERROR!  Initialization failed!  {0}", ex.Message));
                LogFail();
                return;
            }

            // Upload the unprocessed rows
            LogMessage(string.Format("Uploading {0:N0} {1}...", unprocessedData.Count, unprocessedData.Count.Pluralize("row")));
            foreach (var dataModel in unprocessedData)
            {
                dataModel.UploadResult = UploaderResult.Waiting;
            }

            UploadStatus = "Estimating time remaining...";

            var batchRows = new List<T>();
            var batchStartTime = DateTime.Now;
            var totalDuration = new TimeSpan();
            DateTime batchEndTime;
            TimeSpan batchDuration;

            foreach (var dataModel in unprocessedData)
            {
                // Abort if cancellation has been requested
                if (cancellationToken.IsCancellationRequested)
                    break;

                // Upload the row
                dataModel.UploadResult = UploaderResult.Uploading;
                try
                {
                    UploadRow(dataModel);
                    batchRows.Add(dataModel);
                }
                catch (Exception ex)
                {
                    dataModel.UploadResult = UploaderResult.Failed;
                    dataModel.UploadException = ex;
                }

                // When the batch is full, write them
                if (batchRows.Count >= UploadBatchSize)
                {
                    WriteBatchInternal(batchRows);

                    // Calculate the time this batch took to execute and add it to the total time
                    batchEndTime = DateTime.Now;
                    batchDuration = batchEndTime - batchStartTime;
                    batchStartTime = batchEndTime;
                    totalDuration += batchDuration;
                    Debug.WriteLine("Batch Duration = {0} sec", Math.Round(batchDuration.TotalSeconds, 2));

                    // Update row counts
                    CompletedRowCount += batchRows.Count;
                    batchRows.Clear();

                    // Calculate and display the time remaining
                    var averageBatchTicks = Math.Round((double)totalDuration.Ticks / ((double)CompletedRowCount / (double)UploadBatchSize));
                    var remainingDuration = new TimeSpan((long)Math.Round(((double)(TotalRowCount - CompletedRowCount) / (double)UploadBatchSize) * averageBatchTicks));
                    UploadStatus = string.Format("About {0} remaining", remainingDuration.Format());
                }
            }

            // If there are any remaining items in the batch, and cancellation has not been requested, write them
            if (batchRows.Count > 0 && !cancellationToken.IsCancellationRequested)
            {
                WriteBatchInternal(batchRows);

                // Update row counts
                CompletedRowCount += batchRows.Count;
            }

            // Calculate the time the last batch took to execute and add it to the total time
            batchEndTime = DateTime.Now;
            batchDuration = batchEndTime - batchStartTime;
            totalDuration += batchDuration;

            // Log the number of rows uploaded
            LogMessage(string.Format("Processed {0:N0} {1} in {2}.", CompletedRowCount, CompletedRowCount.Pluralize("row"), totalDuration.Format()));

            // Calculate and log the count of successful and failed rows
            var successCount = unprocessedData.Count(d => d.UploadResult == UploaderResult.Successful);
            var failCount = unprocessedData.Count(d => d.UploadResult == UploaderResult.Failed);
            var unknownCount = unprocessedData.Count(d => d.UploadResult == UploaderResult.Unknown);
            LogMessage(string.Format("Successful = {0:N0}", successCount));
            LogMessage(string.Format("Failed = {0:N0}", failCount));
            LogMessage(string.Format("Unknown = {0:N0}", unknownCount));

            // If cancellation was requested, log it and mark all remaining items as cancelled
            if (cancellationToken.IsCancellationRequested)
            {
                foreach (var dataModel in unprocessedData.Where(d => d.UploadResult == UploaderResult.Waiting || d.UploadResult == UploaderResult.Uploading))
                {
                    dataModel.UploadResult = UploaderResult.Cancelled;
                }

                LogCancelled();
                return;
            }

            // Log success if at least one row was successful
            if (successCount > 0)
            {
                LogSuccess();
                return;
            }

            LogFail();
        }

        /// <summary>
        /// Writes a batch of rows to the data source and processes the result.
        /// </summary>
        /// <param name="batchRows">A list of the rows that are being written.</param>
        private void WriteBatchInternal(List<T> batchRows)
        {
            try
            {
                // Write the batch
                WriteBatch();

                // Mark all rows in the batch as successful
                foreach (var dataModel in batchRows)
                {
                    dataModel.UploadResult = UploaderResult.Successful;
                }
            }
            catch (Exception ex)
            {
                // If the batch failed, mark each row with the error
                foreach (var dataModel in batchRows)
                {
                    dataModel.UploadResult = UploaderResult.Failed;
                    dataModel.UploadException = ex;
                }
            }
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
        /// Sends a message to the log widget to indicate that the upload has started.
        /// </summary>
        private void LogStart()
        {
            CompletedRowCount = 0;
            TotalRowCount = 0;
            IsUploadActive = true;
            UploadStatus = "Preparing...";

            LogMessage("*** UPLOAD START ***");
        }

        /// <summary>
        /// Sends a message to the log widget to indicate that the upload was successful.
        /// </summary>
        private void LogSuccess()
        {
            IsUploadActive = false;
            CompletedRowCount = 0;
            TotalRowCount = 0;

            LogMessage("*** UPLOAD SUCCESSFUL ***");
        }

        /// <summary>
        /// Sends a message to the log widget to indicate that the upload has failed.
        /// </summary>
        private void LogFail()
        {
            IsUploadActive = false;
            CompletedRowCount = 0;
            TotalRowCount = 0;

            LogMessage("*** UPLOAD FAILED ***");
        }

        /// <summary>
        /// Sends a message to the log widget to indicate that the upload was cancelled.
        /// </summary>
        private void LogCancelled()
        {
            IsUploadActive = false;
            IsCancelling = false;
            CompletedRowCount = 0;
            TotalRowCount = 0;

            LogMessage("*** UPLOAD CANCELLED ***");
        }

        #endregion


        #region Protected Methods

        /// <summary>
        /// Initializes the uploader.
        /// Inheriters should override this and perform setup tasks such as creating a UnitOfWork, and finding or creating common lookup entities.
        /// </summary>
        protected virtual void InitializeUpload()
        {
        }

        /// <summary>
        /// Uploads one row.
        /// Inheriters should override this and create entities based on the supplied uploader data model.
        /// </summary>
        /// <param name="dataModel">The uploader data model which contains data to create entities from.</param>
        protected virtual void UploadRow(T dataModel)
        {
        }

        /// <summary>
        /// Writes a batch of rows to the data source.
        /// Inheriters should override this and call CommitChangesAsync on any UnitOfWork where new entities were created.
        /// </summary>
        protected virtual void WriteBatch()
        {
        }

        /// <summary>
        /// Finalizes the upload.
        /// Inheriters should override this to perform any cleanup required such as disposing the UnitOfWork, and call EntityChangedMessage.Send with the Oids of each entity that has been added.
        /// </summary>
        protected virtual void FinalizeUpload()
        {
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Handler for the ClearUploaderDataMessage.
        /// </summary>
        /// <param name="message">The message that triggered this method.</param>
        private void OnClearUploaderDataMessage(ClearUploaderDataMessage message)
        {
            ItemsSource.Clear();
        }

        /// <summary>
        /// Handler for the AddUploaderDataMessage.
        /// </summary>
        /// <param name="message">The message that triggered this method.</param>
        private void OnAddUploaderDataMessage(AddUploaderDataMessage message)
        {
            foreach (var dataModel in message.Data.OfType<T>())
            {
                ItemsSource.Add(dataModel);
            }
        }

        /// <summary>
        /// Execute method for the UploadAllCommand.
        /// </summary>
        private async Task OnUploadAllExecuteAsync()
        {
            // Create a cancellation token
            _uploadCancellation = new CancellationTokenSource();
            var cancellationToken = _uploadCancellation.Token;

            // Perform the upload
            await Task.Run(() => UploadData(ItemsSource.ToList(), cancellationToken), cancellationToken);

            // Clear the cancellation token
            _uploadCancellation = null;

            // Finalize the upload
            FinalizeUpload();
        }

        /// <summary>
        /// CanExecute method for the UploadAllCommand.
        /// </summary>
        private bool OnUploadAllCanExecute()
        {
            return CanExecuteUploaderCommand && CanExecuteWidgetCommand && ItemsSource.Count > 0;
        }

        /// <summary>
        /// Execute method for the UploadSelectedCommand.
        /// </summary>
        private async Task OnUploadSelectedExecuteAsync()
        {
            // Create a cancellation token
            _uploadCancellation = new CancellationTokenSource();
            var cancellationToken = _uploadCancellation.Token;

            // Perform the upload
            await Task.Run(() => UploadData(SelectedItems.ToList(), cancellationToken), cancellationToken);

            // Clear the cancellation token
            _uploadCancellation = null;

            // Finalize the upload
            FinalizeUpload();
        }

        /// <summary>
        /// CanExecute method for the UploadSelectedCommand.
        /// </summary>
        private bool OnUploadSelectedCanExecute()
        {
            return CanExecuteUploaderCommand && CanExecuteWidgetCommand && SelectedItems.Count > 0;
        }

        /// <summary>
        /// Execute method for the CancelUploadCommand.
        /// </summary>
        private void OnCancelUploadExecute()
        {
            if (MessageBoxService.Show(
                "Warning: If you cancel the upload, rows that have already been processed will not be rolled back!\r\n\r\nAre you sure?",
                "Cancel Upload", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                return;

            IsCancelling = true;
            UploadStatus = "Cancelling...";
            _uploadCancellation.Cancel();
        }

        /// <summary>
        /// CanExecute method for the CancelUploadCommand.
        /// </summary>
        private bool OnCancelUploadCanExecute()
        {
            return (IsUploadActive && !IsCancelling && _uploadCancellation != null);
        }

        #endregion


        #region Overrides

        //public override void StoreWidgetData()
        //{
        //    base.StoreWidgetData();

        //    // Store the grid layout
        //    SetWidgetData(KeyedData.Scopes.Panel, KeyedData.GroupKeys.GridLayout, GetType().FullName, GetLayout());
        //}

        //protected override void OnWidgetLoaded()
        //{
        //    base.OnWidgetLoaded();

        //    // Store the default grid layout
        //    if (_defaultLayout == null)
        //        _defaultLayout = ((MemoryStream)GetLayout()).ToArray();

        //    // Restore the grid layout
        //    SetLayout(GetWidgetData(KeyedData.Scopes.Panel, KeyedData.GroupKeys.GridLayout, GetType().FullName));
        //}

        protected override void OnRegisterDocumentMessages(object documentId)
        {
            base.OnRegisterDocumentMessages(documentId);

            var uploaderDataMessageToken = string.Format("{0}|{1}", documentId, typeof(T).FullName);
            RegisterDocumentMessage<ClearUploaderDataMessage>(uploaderDataMessageToken, OnClearUploaderDataMessage);
            RegisterDocumentMessage<AddUploaderDataMessage>(uploaderDataMessageToken, OnAddUploaderDataMessage);
        }

        protected override void OnUnregisterDocumentMessages(object documentId)
        {
            base.OnUnregisterDocumentMessages(documentId);

            var uploaderDataMessageToken = string.Format("{0}|{1}", documentId, typeof(T).FullName);
            UnregisterDocumentMessage<ClearUploaderDataMessage>(uploaderDataMessageToken);
            UnregisterDocumentMessage<AddUploaderDataMessage>(uploaderDataMessageToken);
        }

        #endregion


        //#region ISupportLayoutData

        //public GetLayoutDelegate GetLayout { get; set; }

        //public SetLayoutDelegate SetLayout { get; set; }

        //public void ApplyDefaultLayout()
        //{
        //    SetLayout(new MemoryStream(_defaultLayout));
        //}

        //public void ApplySavedLayout()
        //{
        //    // Attempt to get and restore the last saved layout
        //    // If no saved layout was found, we will restore the default layout instead
        //    var savedLayout = GetWidgetData(KeyedData.Scopes.Panel, KeyedData.GroupKeys.GridLayout, GetType().FullName);
        //    if (savedLayout != null)
        //        SetLayout(savedLayout);
        //    else
        //        ApplyDefaultLayout();
        //}

        //#endregion
    }
}
