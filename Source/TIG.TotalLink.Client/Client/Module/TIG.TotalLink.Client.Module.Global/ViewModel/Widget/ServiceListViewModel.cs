using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using DevExpress.Mvvm;
using DevExpress.Mvvm.Native;
using TIG.TotalLink.Client.Core.Extension;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Client.Editor.Wrapper.Editor;
using TIG.TotalLink.Client.IisAdmin;
using TIG.TotalLink.Client.IisAdmin.Helper;
using TIG.TotalLink.Client.IisAdmin.Provider;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Shared.DataModel.Core.Enum.Admin;

namespace TIG.TotalLink.Client.Module.Global.ViewModel.Widget
{
    public class ServiceListViewModel : WidgetViewModelBase
    {
        #region Private Fields

        private readonly IIisAdminProvider _iisAdminProvider;
        private ObservableCollection<IisSite> _itemsSource;
        private IisSite _currentItem;
        private readonly ObservableCollection<IisSite> _selectedItems = new ObservableCollection<IisSite>();

        #endregion


        #region Constructors

        public ServiceListViewModel()
        {
        }

        public ServiceListViewModel(IIisAdminProvider iisAdminProvider)
        {
            // Store services
            _iisAdminProvider = iisAdminProvider;

            // Initialize commands
            RefreshCommand = new DelegateCommand(OnRefreshExecute);
            StartSiteCommand = new DelegateCommand<bool>(OnConfigureSiteExecute, OnConfigureSiteCanExecute);
            StopSiteCommand = StartSiteCommand;
            RestartSiteCommand = new DelegateCommand(OnRestartSiteExecute, OnRestartSiteCanExecute);
            StartAllCommand = new DelegateCommand<bool>(OnConfigureAllExecute, OnConfigureAllCanExecute);
            StopAllCommand = StartAllCommand;
            RestartAllCommand = new DelegateCommand(OnRestartAllExecute, OnRestartAllCanExecute);

            // Initialize the grid
            PopulateColumns();
        }

        #endregion


        #region Commands

        /// <summary>
        /// Command to refresh the service list.
        /// </summary>
        [WidgetCommand("Refresh", "List", RibbonItemType.ButtonItem, "Refresh the list.")]
        public ICommand RefreshCommand { get; private set; }

        /// <summary>
        /// Command to start a site.
        /// </summary>
        [WidgetCommand("Start", "Selection", RibbonItemType.ButtonItem, "Start the selected services.", CommandParameter = true)]
        public ICommand StartSiteCommand { get; private set; }

        /// <summary>
        /// Command to stop a site.
        /// </summary>
        [WidgetCommand("Stop", "Selection", RibbonItemType.ButtonItem, "Stop the selected services.", CommandParameter = false)]
        public ICommand StopSiteCommand { get; private set; }

        /// <summary>
        /// Command to restart a site.
        /// </summary>
        [WidgetCommand("Restart", "Selection", RibbonItemType.ButtonItem, "Restart the selected services.")]
        public ICommand RestartSiteCommand { get; private set; }

        /// <summary>
        /// Command to start all sites.
        /// </summary>
        [WidgetCommand("Start", "All", RibbonItemType.ButtonItem, "Start all services.", CommandParameter = true)]
        public ICommand StartAllCommand { get; private set; }

        /// <summary>
        /// Command to stop all sites.
        /// </summary>
        [WidgetCommand("Stop", "All", RibbonItemType.ButtonItem, "Stop all services.", CommandParameter = false)]
        public ICommand StopAllCommand { get; private set; }

        /// <summary>
        /// Command to restart all sites.
        /// </summary>
        [WidgetCommand("Restart", "All", RibbonItemType.ButtonItem, "Restart all services.")]
        public ICommand RestartAllCommand { get; private set; }

        #endregion


        #region Public Properties

        /// <summary>
        /// The primary source of items for this list.
        /// </summary>
        public ObservableCollection<IisSite> ItemsSource
        {
            get { return _itemsSource; }
            protected set { SetProperty(ref _itemsSource, value, () => ItemsSource); }
        }

        /// <summary>
        /// All selected items.
        /// </summary>
        public ObservableCollection<IisSite> SelectedItems
        {
            get { return _selectedItems; }
        }

        /// <summary>
        /// Columns that represent all the fields that are available on the type this list displays.
        /// </summary>
        public ObservableCollection<GridColumnWrapper> Columns { get; private set; }

        /// <summary>
        /// The item that is currently focused.
        /// </summary>
        public IisSite CurrentItem
        {
            get { return _currentItem; }
            set { SetProperty(ref _currentItem, value, () => CurrentItem); }
        }

        #endregion


        #region Private Properties

        /// <summary>
        /// Indicates if site operations can be performed on selected sites.
        /// </summary>
        private bool CanExecuteSelectedSiteCommand
        {
            get { return (SelectedItems != null && SelectedItems.Count > 0 && !Debugger.IsAttached); }
        }

        /// <summary>
        /// Indicates if site operations can be performed on all sites.
        /// </summary>
        private bool CanExecuteAllSiteCommand
        {
            get { return (ItemsSource != null && ItemsSource.Count > 0); }
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
            foreach (var property in typeof(IisSite).GetVisibleProperties(LayoutType.Table))
            {
                columns.Add(new GridColumnWrapper(typeof(IisSite), property));
            }

            // Call the EditorMetadataBuilder to allow extended editor customisation
            EditorMetadataBuilder.Build<IisSite>(columns);

            // Store the columns
            Columns = columns;
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Refreshes the service list.
        /// </summary>
        public void Refresh()
        {
            try
            {
                // Attempt to collect the site list
                ItemsSource = _iisAdminProvider.GetSites();
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("{0}\r\n\r\nMake sure that IIS is installed on the local PC.  This is required even if you are only deploying to IIS Express.\r\n\r\n{1}", ex.Message, (ex.InnerException != null ? ex.InnerException.Message : null)));
            }
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Execute method for the RefreshCommand.
        /// </summary>
        private void OnRefreshExecute()
        {
            try
            {
                Refresh();
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    MessageBoxService.Show(ex.Message, "Server Manager", MessageBoxButton.OK, MessageBoxImage.Exclamation)
                ));
            }
        }

        /// <summary>
        /// Execute method for the StartSiteCommand and StopSiteCommand.
        /// </summary>
        private void OnConfigureSiteExecute(bool isStart)
        {
            // Process all selected sites
            foreach (var iisSite in SelectedItems)
            {
                try
                {
                    // Attempt to configure the site
                    _iisAdminProvider.ConfigureSite(iisSite, isStart);
                }
                catch (Exception ex)
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        MessageBoxService.Show(string.Format("{0}\r\n\r\nMake sure that IIS is installed on the local PC.  This is required even if you are only deploying to IIS Express.\r\n\r\n{1}", ex.Message, (ex.InnerException != null ? ex.InnerException.Message : null)), "Server Manager", MessageBoxButton.OK, MessageBoxImage.Exclamation)
                    ));
                }
            }

            OnRefreshExecute();
        }

        /// <summary>
        /// CanExecute method for the StartSiteCommand and StopSiteCommand.
        /// </summary>
        private bool OnConfigureSiteCanExecute(bool isStart)
        {
            return CanExecuteSelectedSiteCommand;
        }

        /// <summary>
        /// Execute method for the RestartSiteCommand.
        /// </summary>
        private void OnRestartSiteExecute()
        {
            // Process all selected sites
            foreach (var iisSite in SelectedItems)
            {
                try
                {
                    // Attempt to configure the site
                    _iisAdminProvider.RestartSite(iisSite);
                }
                catch (Exception ex)
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        MessageBoxService.Show(string.Format("{0}\r\n\r\nMake sure that IIS is installed on the local PC.  This is required even if you are only deploying to IIS Express.\r\n\r\n{1}", ex.Message, (ex.InnerException != null ? ex.InnerException.Message : null)), "Server Manager", MessageBoxButton.OK, MessageBoxImage.Exclamation)
                    ));
                }
            }

            OnRefreshExecute();
        }

        /// <summary>
        /// CanExecute method for the RestartSiteCommand.
        /// </summary>
        private bool OnRestartSiteCanExecute()
        {
            return CanExecuteSelectedSiteCommand;
        }

        /// <summary>
        /// Execute method for the StartAllCommand and StopAllCommand.
        /// </summary>
        private void OnConfigureAllExecute(bool isStart)
        {
            try
            {
                if (Debugger.IsAttached)
                {
                    // Attempt to configure all sites (IIS Express)
                    if (isStart)
                    {
                        foreach (var iisSite in ItemsSource)
                        {
                            IisExpressHelper.StartIisExpressSite(iisSite.Name);
                        }
                    }
                    else
                    {
                        IisExpressHelper.StopIIsExpress();
                    }
                }
                else
                {
                    // Attempt to configure all sites (IIS)
                    foreach (var iisSite in ItemsSource)
                    {
                        _iisAdminProvider.ConfigureSite(iisSite, isStart);
                    }
                }
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    MessageBoxService.Show(string.Format("{0}\r\n\r\nMake sure that IIS is installed on the local PC.  This is required even if you are only deploying to IIS Express.\r\n\r\n{1}", ex.Message, (ex.InnerException != null ? ex.InnerException.Message : null)), "Server Manager", MessageBoxButton.OK, MessageBoxImage.Exclamation)
                ));
            }

            OnRefreshExecute();
        }

        /// <summary>
        /// CanExecute method for the StartAllCommand and StopAllCommand.
        /// </summary>
        private bool OnConfigureAllCanExecute(bool isStart)
        {
            return CanExecuteAllSiteCommand;
        }
        
        /// <summary>
        /// Execute method for the RestartAllCommand.
        /// </summary>
        private void OnRestartAllExecute()
        {
            try
            {
                if (Debugger.IsAttached)
                {
                    // Attempt to restart all sites (IIS Express)
                    IisExpressHelper.StopIIsExpress();

                    foreach (var iisSite in ItemsSource)
                    {
                        IisExpressHelper.StartIisExpressSite(iisSite.Name);
                    }
                }
                else
                {
                    // Attempt to restart all sites (IIS)
                    foreach (var iisSite in ItemsSource)
                    {
                        _iisAdminProvider.RestartSite(iisSite);
                    }
                }
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    MessageBoxService.Show(string.Format("{0}\r\n\r\nMake sure that IIS is installed on the local PC.  This is required even if you are only deploying to IIS Express.\r\n\r\n{1}", ex.Message, (ex.InnerException != null ? ex.InnerException.Message : null)), "Server Manager", MessageBoxButton.OK, MessageBoxImage.Exclamation)
                ));
            }

            OnRefreshExecute();
        }

        /// <summary>
        /// CanExecute method for the RestartAllCommand.
        /// </summary>
        private bool OnRestartAllCanExecute()
        {
            return CanExecuteAllSiteCommand;
        }

        #endregion


        #region Overrides

        protected override void OnWidgetLoaded(EventArgs e)
        {
            base.OnWidgetLoaded(e);

            AddStartupTask(Refresh);
        }

        #endregion
    }
}
