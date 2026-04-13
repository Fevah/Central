using System;
using System.Linq;
using System.Windows.Input;
using DevExpress.Mvvm;
using TIG.TotalLink.Client.Core.Enum;
using TIG.TotalLink.Client.Core.Interface.BackgroundService;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Client.Module.Repository.BackgroundService;
using TIG.TotalLink.Shared.DataModel.Core.Enum.Admin;

namespace TIG.TotalLink.Client.Module.Repository.ViewModel.Widget
{
    public class SyncControlViewModel : ListViewModelBase<SyncEntityBase>
    {
        #region Private Properties

        private readonly ISyncBackgroundService _syncBackgroundService;

        #endregion


        #region Constructors

        /// <summary>
        /// Default Constructor
        /// </summary>
        public SyncControlViewModel()
        {
            PauseSyncCommand = new DelegateCommand(OnPauseExcute, OnPauseCanExcute);
            StopSyncCommand = new DelegateCommand(OnStopExcute, OnStopCanExcute);
            StartSyncCommand = new DelegateCommand(OnStartExcute, OnStartCanExcute);
        }

        /// <summary>
        /// Constructor with service
        /// </summary>
        /// <param name="syncBackgroundService">Sync background service</param>
        public SyncControlViewModel(ISyncBackgroundService syncBackgroundService)
            : this()
        {
            _syncBackgroundService = syncBackgroundService;
        }

        #endregion


        #region Overrides

        protected override void OnWidgetLoaded(EventArgs e)
        {
            base.OnWidgetLoaded(e);

            AddStartupTask(() =>
            {
                // Initialize the data source
                ItemsSource = _syncBackgroundService.SyncEntities;
            });
        }

        #endregion


        #region Commands

        /// <summary>
        /// Command to add a new item to the list.
        /// </summary>
        [WidgetCommand("Pause", "Sync", RibbonItemType.ButtonItem, "Pause selected sync item.")]
        public virtual ICommand PauseSyncCommand { get; private set; }

        /// <summary>
        /// Command to add a new item to the list.
        /// </summary>
        [WidgetCommand("Stop", "Sync", RibbonItemType.ButtonItem, "Stop selected sync item.")]
        public virtual ICommand StopSyncCommand { get; private set; }

        /// <summary>
        /// Command to delete all selected items from the list.
        /// </summary>
        [WidgetCommand("Start", "Sync", RibbonItemType.ButtonItem, "Start selected sync item.")]
        public virtual ICommand StartSyncCommand { get; private set; }

        public override ICommand AddCommand { get { return null; } }

        public override ICommand DeleteCommand { get { return null; } }

        public override ICommand RefreshCommand { get { return null; } }

        #endregion


        #region Event handlers

        /// <summary>
        /// Execute method for the UndoCommand.
        /// </summary>
        private void OnPauseExcute()
        {
            foreach (var syncItem in SelectedItems.Where(syncItem => syncItem.Status == SyncStatus.Sync))
            {
                syncItem.Pause();
            }
        }

        /// <summary>
        /// CanExecute method for the UndoCommand.
        /// </summary>
        private bool OnPauseCanExcute()
        {
            return SelectedItems.Any(p => p.Status == SyncStatus.Sync)
                && CanExecuteWidgetCommand;
        }

        /// <summary>
        /// Execute method for the UndoCommand.
        /// </summary>
        private void OnStopExcute()
        {
            foreach (var syncItem in SelectedItems.Where(syncItem => syncItem.Status == SyncStatus.Sync))
            {
                syncItem.Stop();
            }
        }

        /// <summary>
        /// CanExecute method for the UndoCommand.
        /// </summary>
        private bool OnStopCanExcute()
        {
            return SelectedItems.Any(p => p.Status == SyncStatus.Sync)
                && CanExecuteWidgetCommand;
        }

        /// <summary>
        /// Execute method for the UndoCommand.
        /// </summary>
        private void OnStartExcute()
        {
            foreach (var syncItem in SelectedItems.Where(syncItem => syncItem.Status != SyncStatus.Sync))
            {
                syncItem.Start();
            }
        }

        /// <summary>
        /// CanExecute method for the UndoCommand.
        /// </summary>
        private bool OnStartCanExcute()
        {
            return SelectedItems.Any(p => p.Status != SyncStatus.Sync)
                && CanExecuteWidgetCommand;
        }

        #endregion
    }
}