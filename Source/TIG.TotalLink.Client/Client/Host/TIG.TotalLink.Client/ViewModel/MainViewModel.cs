using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using AutoMapper;
using DevExpress.Mvvm;
using DevExpress.Xpo;
using MonitoredUndo;
using TIG.TotalLink.Client.Core.AppContext;
using TIG.TotalLink.Client.Core.Command;
using TIG.TotalLink.Client.Core.Enum;
using TIG.TotalLink.Client.Core.Helper;
using TIG.TotalLink.Client.Core.Interface.MVVMService;
using TIG.TotalLink.Client.Core.Message;
using TIG.TotalLink.Client.Module.Admin.ViewModel;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Backstage.Item;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Ribbon;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Ribbon.Category;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Ribbon.Core;
using TIG.TotalLink.Client.Undo.AppContext;
using TIG.TotalLink.Client.Undo.Extension;
using TIG.TotalLink.Shared.DataModel.Admin;
using TIG.TotalLink.Shared.DataModel.Core.Extension;
using TIG.TotalLink.Shared.Facade.Admin;
using TIG.TotalLink.Shared.Facade.Core.Helper;
using TIG.TotalLink.Shared.Facade.Global;

namespace TIG.TotalLink.Client.ViewModel
{
    public class MainViewModel : MainViewModelBase
    {
        #region Private Fields

        private readonly IAdminFacade _adminFacade;
        private readonly IGlobalFacade _globalFacade;
        private ChangeSet _selectedChangeSet;
        private RibbonCategoryViewModelBase _ribbonContextMenuCategory;
        private RibbonPageViewModel _ribbonContextMenuPage;
        private RibbonGroupViewModel _ribbonContextMenuGroup;
        private RibbonItemViewModelBase _ribbonContextMenuItem;
        private ViewModelBase _selectedRibbonPageViewModel;

        #endregion


        #region Constructors

        public MainViewModel()
        {
            AppContextViewModel.Instance.GetDetailDialogService = GetService<IDetailDialogService>;
            AppContextViewModel.Instance.GetMessageBoxService = GetService<IMessageBoxService>;
        }

        public MainViewModel(IAdminFacade adminFacade, IGlobalFacade globalFacade)
            : this()
        {
            // Store services
            _adminFacade = adminFacade;
            _globalFacade = globalFacade;

            // Initialize commands
            UndoCommand = new DelegateCommand(OnUndoExecute, OnUndoCanExecute);
            RedoCommand = new DelegateCommand(OnRedoExecute, OnRedoCanExecute);
            //ViewChangeSetCommand = new DelegateCommand(OnViewChangeSetExecute, OnViewChangeSetCanExecute);
            //ExecuteSelectedUndoCommand = new DelegateCommand<PopupControlContainer>(OnMouseLeftButtonDownUndoListExecute);
            //ClearUndoSelectionCommand = new DelegateCommand(OnMouseLeaveUndoListExecute);
            //ChangeUndoSelectionCommand = new DelegateCommand<int?>(OnMouseMoveUndoListExecute);
            AddRibbonCategoryCommand = new AsyncCommandEx(OnAddRibbonCategoryExecuteAsync, OnAddRibbonCategoryCanExecute);
            EditRibbonCategoryCommand = new AsyncCommandEx(OnEditRibbonCategoryExecuteAsync, OnEditRibbonCategoryCanExecute);
            DeleteRibbonCategoryCommand = new AsyncCommandEx(OnDeleteRibbonCategoryExecuteAsync, OnDeleteRibbonCategoryCanExecute);
            AddRibbonPageCommand = new AsyncCommandEx(OnAddRibbonPageExecuteAsync, OnAddRibbonPageCanExecute);
            EditRibbonPageCommand = new AsyncCommandEx(OnEditRibbonPageExecuteAsync, OnEditRibbonPageCanExecute);
            DeleteRibbonPageCommand = new AsyncCommandEx(OnDeleteRibbonPageExecuteAsync, OnDeleteRibbonPageCanExecute);
            AddRibbonGroupCommand = new AsyncCommandEx(OnAddRibbonGroupExecuteAsync, OnAddRibbonGroupCanExecute);
            EditRibbonGroupCommand = new AsyncCommandEx(OnEditRibbonGroupExecuteAsync, OnEditRibbonGroupCanExecute);
            DeleteRibbonGroupCommand = new AsyncCommandEx(OnDeleteRibbonGroupExecuteAsync, OnDeleteRibbonGroupCanExecute);
            AddRibbonItemCommand = new AsyncCommandEx(OnAddRibbonItemExecuteAsync, OnAddRibbonItemCanExecute);
            EditRibbonItemCommand = new AsyncCommandEx(OnEditRibbonItemExecuteAsync, OnEditRibbonItemCanExecute);
            DeleteRibbonItemCommand = new AsyncCommandEx(OnDeleteRibbonItemExecuteAsync, OnDeleteRibbonItemCanExecute);
        }

        #endregion


        #region Mvvm Services

        private IMessageBoxService MessageBoxService { get { return GetService<IMessageBoxService>(); } }

        private IDetailDialogService DetailDialogService { get { return GetService<IDetailDialogService>(); } }

        #endregion


        #region Commands

        /// <summary>
        /// Command to undo the last change on the stack.
        /// </summary>
        public ICommand UndoCommand { get; private set; }

        /// <summary>
        /// Command to redo the next change on the stack.
        /// </summary>
        public ICommand RedoCommand { get; private set; }

        /// <summary>
        /// Command to show contents of the selected change set.
        /// </summary>
        public ICommand ViewChangeSetCommand { get; private set; }

        /// <summary>
        /// Command to undo based the selection in the undo listbox.
        /// </summary>
        public ICommand ExecuteSelectedUndoCommand { get; private set; }

        /// <summary>
        /// Command to clear the selection while mouse left undo listbox.
        /// </summary>
        public ICommand ClearUndoSelectionCommand { get; private set; }

        /// <summary>
        /// Command to select items while mouse moved in the undo listbox.
        /// </summary>
        public ICommand ChangeUndoSelectionCommand { get; private set; }

        /// <summary>
        /// Command to add a new category to the ribbon.
        /// </summary>
        public ICommand AddRibbonCategoryCommand { get; private set; }

        /// <summary>
        /// Command to edit a category on the ribbon.
        /// </summary>
        public ICommand EditRibbonCategoryCommand { get; private set; }

        /// <summary>
        /// Command to delete a category from the ribbon.
        /// </summary>
        public ICommand DeleteRibbonCategoryCommand { get; private set; }

        /// <summary>
        /// Command to add a new page to the ribbon.
        /// </summary>
        public ICommand AddRibbonPageCommand { get; private set; }

        /// <summary>
        /// Command to edit a page on the ribbon.
        /// </summary>
        public ICommand EditRibbonPageCommand { get; private set; }

        /// <summary>
        /// Command to delete a page from the ribbon.
        /// </summary>
        public ICommand DeleteRibbonPageCommand { get; private set; }

        /// <summary>
        /// Command to add a new group to the ribbon.
        /// </summary>
        public ICommand AddRibbonGroupCommand { get; private set; }

        /// <summary>
        /// Command to edit a group on the ribbon.
        /// </summary>
        public ICommand EditRibbonGroupCommand { get; private set; }

        /// <summary>
        /// Command to delete a group from the ribbon.
        /// </summary>
        public ICommand DeleteRibbonGroupCommand { get; private set; }

        /// <summary>
        /// Command to add a new item to the ribbon.
        /// </summary>
        public ICommand AddRibbonItemCommand { get; private set; }

        /// <summary>
        /// Command to edit a item on the ribbon.
        /// </summary>
        public ICommand EditRibbonItemCommand { get; private set; }

        /// <summary>
        /// Command to delete a item from the ribbon.
        /// </summary>
        public ICommand DeleteRibbonItemCommand { get; private set; }

        #endregion


        #region Public Properties

        /// <summary>
        /// The stack of changes that can be undone.
        /// </summary>
        public ICollectionView UndoStack { get; private set; }

        /// <summary>
        /// The stack of changes that can be redone.
        /// </summary>
        public ICollectionView RedoStack { get; private set; }

        /// <summary>
        /// The change set that was selected in the undo list.
        /// </summary>
        public ChangeSet SelectedChangeSet
        {
            get { return _selectedChangeSet; }
            set { SetProperty(ref _selectedChangeSet, value, () => SelectedChangeSet); }
        }

        /// <summary>
        /// The category that was targeted when the ribbon context menu was displayed.
        /// </summary>
        public RibbonCategoryViewModelBase RibbonContextMenuCategory
        {
            get { return _ribbonContextMenuCategory; }
            set { SetProperty(ref _ribbonContextMenuCategory, value, () => RibbonContextMenuCategory); }
        }

        /// <summary>
        /// The page that was targeted when the ribbon context menu was displayed.
        /// </summary>
        public RibbonPageViewModel RibbonContextMenuPage
        {
            get { return _ribbonContextMenuPage; }
            set { SetProperty(ref _ribbonContextMenuPage, value, () => RibbonContextMenuPage); }
        }

        /// <summary>
        /// The group that was targeted when the ribbon context menu was displayed.
        /// </summary>
        public RibbonGroupViewModel RibbonContextMenuGroup
        {
            get { return _ribbonContextMenuGroup; }
            set { SetProperty(ref _ribbonContextMenuGroup, value, () => RibbonContextMenuGroup); }
        }

        /// <summary>
        /// The item that was targeted when the ribbon context menu was displayed.
        /// </summary>
        public RibbonItemViewModelBase RibbonContextMenuItem
        {
            get { return _ribbonContextMenuItem; }
            set { SetProperty(ref _ribbonContextMenuItem, value, () => RibbonContextMenuItem); }
        }

        /// <summary>
        /// The view model represented by the currently selected ribbon page.
        /// This may be a RibbonPageViewModel (persistent pages), DocumentViewModel (widget commands) or PanelViewModel (ribbon defined in widget).
        /// </summary>
        public ViewModelBase SelectedRibbonPageViewModel
        {
            get { return _selectedRibbonPageViewModel; }
            set { SetProperty(ref _selectedRibbonPageViewModel, value, () => SelectedRibbonPageViewModel); }
        }

        /// <summary>
        /// Indicates if a ribbon command can be executed, based on whether any related operations are in progress.
        /// </summary>
        public bool CanExecuteRibbonCommand
        {
            get
            {
                return !(
                       ((AsyncCommandEx)AddRibbonCategoryCommand).IsExecuting
                    || ((AsyncCommandEx)EditRibbonCategoryCommand).IsExecuting
                    || ((AsyncCommandEx)DeleteRibbonCategoryCommand).IsExecuting
                    || ((AsyncCommandEx)AddRibbonPageCommand).IsExecuting
                    || ((AsyncCommandEx)EditRibbonPageCommand).IsExecuting
                    || ((AsyncCommandEx)DeleteRibbonPageCommand).IsExecuting
                    || ((AsyncCommandEx)AddRibbonGroupCommand).IsExecuting
                    || ((AsyncCommandEx)EditRibbonGroupCommand).IsExecuting
                    || ((AsyncCommandEx)DeleteRibbonGroupCommand).IsExecuting
                    || ((AsyncCommandEx)AddRibbonItemCommand).IsExecuting
                    || ((AsyncCommandEx)EditRibbonItemCommand).IsExecuting
                    || ((AsyncCommandEx)DeleteRibbonItemCommand).IsExecuting
                    || AppUndoRootViewModel.Instance.UndoRoot.IsUndoingOrRedoing
                    );
            }
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Called by the MainView after it is loaded to initialize the MainViewModel.
        /// </summary>
        public void Initialize()
        {
            // Connect to the admin facade
            try
            {
                _adminFacade.Connect();
            }
            catch (Exception ex)
            {
                var serviceException = new ServiceExceptionHelper(ex);
                MessageBoxService.Show(string.Format("Failed to connect to AdminFacade!\r\n\r\n{0}", serviceException.HasException ? serviceException.ToString() : ex.ToString()), AppContextViewModel.Instance.ApplicationTitle, MessageBoxButton.OK, MessageBoxImage.Stop);
                Application.Current.Shutdown();
            }

            // Connect to the global facade
            try
            {
                _globalFacade.Connect();
            }
            catch (Exception ex)
            {
                var serviceException = new ServiceExceptionHelper(ex);
                MessageBoxService.Show(string.Format("Failed to connect to GlobalFacade!\r\n\r\n{0}", serviceException.HasException ? serviceException.ToString() : ex.ToString()), AppContextViewModel.Instance.ApplicationTitle, MessageBoxButton.OK, MessageBoxImage.Stop);
                Application.Current.Shutdown();
            }

            // Initialize messages
            Messenger.Default.Register<EntityChangedMessage>(this, OnEntityChangedMessage);

            // Initialize the undo and redo stacks
            var undoRoot = AppUndoRootViewModel.Instance.UndoRoot;
            UndoStack = CollectionViewSource.GetDefaultView(undoRoot.UndoStack);
            RedoStack = CollectionViewSource.GetDefaultView(undoRoot.RedoStack);
            undoRoot.UndoStackChanged += UndoRoot_UndoStackChanged;
            undoRoot.RedoStackChanged += UndoRoot_RedoStackChanged;

            // Initialize the global settings, backstage view and ribbon
            InitializeGlobalSettings();
            InitializeBackstage();
            RefreshRibbon();

            // Turn on undo tracking
            AppUndoRootViewModel.Instance.TrackUndo = true;
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Reads a number of global settings and stores them in the AppContextViewModel for easy access.
        /// </summary>
        private void InitializeGlobalSettings()
        {
            AppContextViewModel.Instance.SystemCode = Convert.ToInt32(_globalFacade.GetSetting("SystemCode"));
            AppContextViewModel.Instance.ReferenceValueFormat = _globalFacade.GetSetting("ReferenceValueFormat");
            AppContextViewModel.Instance.ReferenceDisplayFormat = _globalFacade.GetSetting("ReferenceDisplayFormat");
            AppContextViewModel.Instance.ReferenceDisplayClean = _globalFacade.GetSetting("ReferenceDisplayClean");
        }

        /// <summary>
        /// Initialize the backstage view.
        /// </summary>
        private void InitializeBackstage()
        {
            BackstageItems.Add(new BackstageTabItemViewModel("New Document", "WidgetCardView"));
            BackstageItems.Add(new BackstageTabItemViewModel("Theme", "ThemeGalleryView"));
            BackstageItems.Add(new BackstageButtonItemViewModel("Close", new DelegateCommand(OnCloseApplicationExecute)));
        }

        /// <summary>
        /// Refreshes the ribbon.
        /// </summary>
        private void RefreshRibbon()
        {
            // Store the currently selected ribbon page
            var selectedPageViewModel = SelectedRibbonPageViewModel;

            try
            {
                _adminFacade.ExecuteUnitOfWork(uow =>
                {
                    // Load the ribbon categories
                    var categories = uow.Query<RibbonCategory>();

                    // Pre-fetch all related entities
                    uow.PreFetch(categories, "RibbonPages.RibbonGroups.RibbonItems");

                    // Map the categories to viewmodels
                    Mapper.Map(categories, RibbonCategories);
                });
            }
            catch (Exception ex)
            {
                var serviceException = new ServiceExceptionHelper(ex);
                MessageBoxService.Show(string.Format("Failed to load ribbon structure!\r\n\r\n{0}", serviceException.Message), AppContextViewModel.Instance.ApplicationTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // Re-select the stored ribbon page
            try
            {
                Application.Current.Dispatcher.Invoke(new Action(() => SelectedRibbonPageViewModel = selectedPageViewModel), DispatcherPriority.Background);
            }
            catch (Exception)
            {
                // TODO : Sometimes throws exception at startup
            }
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Close the application.
        /// </summary>
        private void OnCloseApplicationExecute()
        {
            Application.Current.Shutdown();
        }

        /// <summary>
        /// Execute method for the UndoCommand.
        /// </summary>
        private void OnUndoExecute()
        {
            AppUndoRootViewModel.Instance.UndoRoot.Undo();
        }

        /// <summary>
        /// CanExecute method for the UndoCommand.
        /// </summary>
        private bool OnUndoCanExecute()
        {
            var undoRoot = AppUndoRootViewModel.Instance.UndoRoot;
            return undoRoot.CanUndo && !undoRoot.IsUndoingOrRedoing && !undoRoot.IsInBatch;
        }

        /// <summary>
        /// Execute method for the RedoCommand.
        /// </summary>
        private void OnRedoExecute()
        {
            AppUndoRootViewModel.Instance.UndoRoot.Redo();
        }

        /// <summary>
        /// CanExecute method for the RedoCommand.
        /// </summary>
        private bool OnRedoCanExecute()
        {
            var undoRoot = AppUndoRootViewModel.Instance.UndoRoot;
            return undoRoot.CanRedo && !undoRoot.IsUndoingOrRedoing && !undoRoot.IsInBatch;
        }

        /// <summary>
        /// Execute method for the AddRibbonCategoryCommand.
        /// </summary>
        private async Task OnAddRibbonCategoryExecuteAsync()
        {
            // Show a dialog to configure the new ribbon category
            await _adminFacade.ExecuteUnitOfWorkAsync(uow =>
            {
                uow.StartUiTracking(this);
                return DetailDialogService.ShowDialog(DetailEditMode.Add, new RibbonCategory(uow));
            });
        }

        /// <summary>
        /// CanExecute method for the AddRibbonCategoryCommand.
        /// </summary>
        private bool OnAddRibbonCategoryCanExecute()
        {
            return (RibbonContextMenuCategory != null && CanExecuteRibbonCommand);
        }

        /// <summary>
        /// Execute method for the EditRibbonCategoryCommand.
        /// </summary>
        private async Task OnEditRibbonCategoryExecuteAsync()
        {
            // Show a dialog to edit the ribbon category
            await _adminFacade.ExecuteUnitOfWorkAsync(uow =>
            {
                uow.StartUiTracking(this);
                return DetailDialogService.ShowDialog(DetailEditMode.Edit, uow.GetDataObject(RibbonContextMenuCategory.DataObject));
            });
        }

        /// <summary>
        /// CanExecute method for the EditRibbonCategoryCommand.
        /// </summary>
        private bool OnEditRibbonCategoryCanExecute()
        {
            return (RibbonContextMenuCategory != null && !(RibbonContextMenuCategory is RibbonDefaultCategoryViewModel) && CanExecuteRibbonCommand);
        }

        /// <summary>
        /// Execute method for the DeleteRibbonCategoryCommand.
        /// </summary>
        private async Task OnDeleteRibbonCategoryExecuteAsync()
        {
            // Show a warning before deleting the category
            if (MessageBoxService.Show(ActionMessageHelper.GetWarningMessage(RibbonContextMenuCategory.DataObject, "delete"), ActionMessageHelper.GetTitle(RibbonContextMenuCategory.DataObject, "delete"), MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                // Delete the category
                await RibbonContextMenuCategory.DataObject.DeleteDataObjectAsync();
            }
        }

        /// <summary>
        /// CanExecute method for the DeleteRibbonCategoryCommand.
        /// </summary>
        private bool OnDeleteRibbonCategoryCanExecute()
        {
            return (RibbonContextMenuCategory != null && !(RibbonContextMenuCategory is RibbonDefaultCategoryViewModel) && CanExecuteRibbonCommand);
        }

        /// <summary>
        /// Execute method for the AddRibbonPageCommand.
        /// </summary>
        private async Task OnAddRibbonPageExecuteAsync()
        {
            // Show a dialog to configure the new ribbon page
            await _adminFacade.ExecuteUnitOfWorkAsync(uow =>
            {
                uow.StartUiTracking(this);
                return DetailDialogService.ShowDialog(DetailEditMode.Add, new RibbonPage(uow) { RibbonCategory = uow.GetDataObject(RibbonContextMenuCategory.DataObject) });
            });
        }

        /// <summary>
        /// CanExecute method for the AddRibbonPageCommand.
        /// </summary>
        private bool OnAddRibbonPageCanExecute()
        {
            return (RibbonContextMenuCategory != null && CanExecuteRibbonCommand);
        }

        /// <summary>
        /// Execute method for the EditRibbonPageCommand.
        /// </summary>
        private async Task OnEditRibbonPageExecuteAsync()
        {
            // Show a dialog to edit the ribbon page
            await _adminFacade.ExecuteUnitOfWorkAsync(uow =>
            {
                uow.StartUiTracking(this);
                return DetailDialogService.ShowDialog(DetailEditMode.Edit, uow.GetDataObject(RibbonContextMenuPage.DataObject));
            });
        }

        /// <summary>
        /// CanExecute method for the EditRibbonPageCommand.
        /// </summary>
        private bool OnEditRibbonPageCanExecute()
        {
            return (RibbonContextMenuPage != null && CanExecuteRibbonCommand);
        }

        /// <summary>
        /// Execute method for the DeleteRibbonPageCommand.
        /// </summary>
        private async Task OnDeleteRibbonPageExecuteAsync()
        {
            // Show a warning before deleting the page
            if (MessageBoxService.Show(ActionMessageHelper.GetWarningMessage(RibbonContextMenuPage.DataObject, "delete"), ActionMessageHelper.GetTitle(RibbonContextMenuPage.DataObject, "delete"), MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                // Delete the page
                await RibbonContextMenuPage.DataObject.DeleteDataObjectAsync();
            }
        }

        /// <summary>
        /// CanExecute method for the DeleteRibbonPageCommand.
        /// </summary>
        private bool OnDeleteRibbonPageCanExecute()
        {
            return (RibbonContextMenuPage != null && CanExecuteRibbonCommand);
        }

        /// <summary>
        /// Execute method for the AddRibbonGroupCommand.
        /// </summary>
        private async Task OnAddRibbonGroupExecuteAsync()
        {
            // Show a dialog to configure the new ribbon group
            await _adminFacade.ExecuteUnitOfWorkAsync(uow =>
            {
                uow.StartUiTracking(this);
                return DetailDialogService.ShowDialog(DetailEditMode.Add, new RibbonGroup(uow) { RibbonPage = uow.GetDataObject(RibbonContextMenuPage.DataObject) });
            });
        }

        /// <summary>
        /// CanExecute method for the AddRibbonGroupCommand.
        /// </summary>
        private bool OnAddRibbonGroupCanExecute()
        {
            return (RibbonContextMenuPage != null && CanExecuteRibbonCommand);
        }

        /// <summary>
        /// Execute method for the EditRibbonGroupCommand.
        /// </summary>
        private async Task OnEditRibbonGroupExecuteAsync()
        {
            // Show a dialog to edit the ribbon group
            await _adminFacade.ExecuteUnitOfWorkAsync(uow =>
            {
                uow.StartUiTracking(this);
                return DetailDialogService.ShowDialog(DetailEditMode.Edit, uow.GetDataObject(RibbonContextMenuGroup.DataObject));
            });
        }

        /// <summary>
        /// CanExecute method for the EditRibbonGroupCommand.
        /// </summary>
        private bool OnEditRibbonGroupCanExecute()
        {
            return (RibbonContextMenuGroup != null && CanExecuteRibbonCommand);
        }

        /// <summary>
        /// Execute method for the DeleteRibbonGroupCommand.
        /// </summary>
        private async Task OnDeleteRibbonGroupExecuteAsync()
        {
            // Show a warning before deleting the group
            if (MessageBoxService.Show(ActionMessageHelper.GetWarningMessage(RibbonContextMenuGroup.DataObject, "delete"), ActionMessageHelper.GetTitle(RibbonContextMenuGroup.DataObject, "delete"), MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                // Delete the group
                await RibbonContextMenuGroup.DataObject.DeleteDataObjectAsync();
            }
        }

        /// <summary>
        /// CanExecute method for the DeleteRibbonGroupCommand.
        /// </summary>
        private bool OnDeleteRibbonGroupCanExecute()
        {
            return (RibbonContextMenuGroup != null && CanExecuteRibbonCommand);
        }

        /// <summary>
        /// Execute method for the AddRibbonItemCommand.
        /// </summary>
        private async Task OnAddRibbonItemExecuteAsync()
        {
            // Show a dialog to configure the new ribbon item
            await _adminFacade.ExecuteUnitOfWorkAsync(uow =>
            {
                uow.StartUiTracking(this);
                return DetailDialogService.ShowDialog(DetailEditMode.Add, new RibbonItem(uow) { RibbonGroup = uow.GetDataObject(RibbonContextMenuGroup.DataObject) });
            });
        }

        /// <summary>
        /// CanExecute method for the AddRibbonItemCommand.
        /// </summary>
        private bool OnAddRibbonItemCanExecute()
        {
            return (RibbonContextMenuGroup != null && CanExecuteRibbonCommand);
        }

        /// <summary>
        /// Execute method for the EditRibbonItemCommand.
        /// </summary>
        private async Task OnEditRibbonItemExecuteAsync()
        {
            // Show a dialog to edit the ribbon item
            await _adminFacade.ExecuteUnitOfWorkAsync(uow =>
            {
                uow.StartUiTracking(this);
                return DetailDialogService.ShowDialog(DetailEditMode.Edit, uow.GetDataObject(RibbonContextMenuItem.DataObject));
            });
        }

        /// <summary>
        /// CanExecute method for the EditRibbonItemCommand.
        /// </summary>
        private bool OnEditRibbonItemCanExecute()
        {
            return (RibbonContextMenuItem != null && CanExecuteRibbonCommand);
        }

        /// <summary>
        /// Execute method for the DeleteRibbonItemCommand.
        /// </summary>
        private async Task OnDeleteRibbonItemExecuteAsync()
        {
            // Show a warning before deleting the item
            if (MessageBoxService.Show(ActionMessageHelper.GetWarningMessage(RibbonContextMenuItem.DataObject, "delete"), ActionMessageHelper.GetTitle(RibbonContextMenuItem.DataObject, "delete"), MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                // Delete the item
                await RibbonContextMenuItem.DataObject.DeleteDataObjectAsync();
            }
        }

        /// <summary>
        /// CanExecute method for the DeleteRibbonItemCommand.
        /// </summary>
        private bool OnDeleteRibbonItemCanExecute()
        {
            return (RibbonContextMenuItem != null && CanExecuteRibbonCommand);
        }

        /// <summary>
        /// Handler for the EntityChangedMessage.
        /// </summary>
        /// <param name="message">The message that triggered this method.</param>
        private void OnEntityChangedMessage(EntityChangedMessage message)
        {
            // If the message contains any ribbon component types, refresh the ribbon
            if (message.ContainsEntitiesOfType(typeof(RibbonCategory), typeof(RibbonPage), typeof(RibbonGroup), typeof(RibbonItem)))
                RefreshRibbon();
        }

        /// <summary>
        /// Handles the UndoRoot.UndoStackChanged event.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void UndoRoot_UndoStackChanged(object sender, EventArgs e)
        {
            //// Set the height and width of undo list
            //UndoLabelVisible = UndoStack.SourceCollection.Cast<ChangeSet>().Count() != 0;
            //UndoLabelContent = "Cancel";
            //RaisePropertyChanged(() => UndoLabelContent);
            //RaisePropertyChanged(() => UndoLabelVisible);

            // Since the undo stack changes may come from an async task, we need to use the dispatcher to refresh the undo stack
            Application.Current.Dispatcher.BeginInvoke(new Action(() => UndoStack.Refresh()));
        }

        /// <summary>
        /// Handles the UndoRoot.RedoStackChanged event.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void UndoRoot_RedoStackChanged(object sender, EventArgs e)
        {
            // Since the undo stack changes may come from an async task, we need to use the dispatcher to refresh the redo stack
            Application.Current.Dispatcher.BeginInvoke(new Action(() => RedoStack.Refresh()));
        }

        #endregion
    }
}
