using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DevExpress.Mvvm;
using DevExpress.Xpf.Bars;
using DevExpress.Xpf.Core.Native;
using DevExpress.Xpf.Ribbon;
using MonitoredUndo;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Document;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Ribbon;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Ribbon.Core;
using TIG.TotalLink.Client.ViewModel;
using TIG.TotalLink.Shared.DataModel.Core.Helper;

namespace TIG.TotalLink.Client.View
{
    public partial class MainView : UserControl
    {
        #region Private Fields

        private bool _initialized;
        private bool _allowPopupMenu = true;
        private bool _selectedRibbonPageViewModelChanging;

        #endregion


        #region Constructors

        public MainView()
        {
            InitializeComponent();
        }

        public MainView(MainViewModel viewModel)
            : this()
        {
            DataContext = viewModel;

            // Handle events
            viewModel.PropertyChanged += MainViewModel_PropertyChanged;
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Finds the view model represented by the specified RibbonPage.
        /// This view model may be a RibbonPageViewModel (persistent pages), DocumentViewModel (widget commands) or PanelViewModel (ribbon defined in widget).
        /// </summary>
        /// <param name="ribbonPage">The RibbonPage to find the view model of.</param>
        /// <returns>The view model represented by the specified RibbonPage.</returns>
        private ViewModelBase GetSelectedRibbonPageViewModel(RibbonPage ribbonPage)
        {
            // Abort if the page is null
            if (ribbonPage == null)
                return null;

            // Return the page DataContext as a ViewModelBase
            return ribbonPage.DataContext as ViewModelBase;
        }

        /// <summary>
        /// Selects a RibbonPage based on the view model it represents.
        /// </summary>
        /// <param name="viewModel">
        /// The view model of the page to select.
        /// This view model may belong to a RibbonPageViewModel (persistent pages), DocumentViewModel (widget commands) or PanelViewModel (ribbon defined in widget).
        /// </param>
        /// <returns>The RibbonPage which represents the specified view model.</returns>
        private RibbonPage GetRibbonPageByViewModel(ViewModelBase viewModel)
        {
            // If the view model is null, return the first page in the default category
            if (viewModel == null)
            {
                var defaultCategory = RibbonControl.ActualCategories.OfType<RibbonDefaultPageCategory>().FirstOrDefault();
                if (defaultCategory == null)
                    return null;

                return defaultCategory.GetFirstSelectablePage();
            }

            // Get the Oid of the view model we are trying to find
            var targetOid = GetPageViewModelOid(viewModel);
            if (targetOid == null)
                return null;

            // Get the Type of the view model we are trying to find
            var targetType = viewModel.GetType();

            // Search all existing pages to find one which matches targetType and targetOid
            foreach (var category in RibbonControl.ActualCategories)
            {
                foreach (var page in category.ActualPages)
                {
                    var pageViewModel = page.DataContext as ViewModelBase;
                    if (pageViewModel == null || pageViewModel.GetType() != targetType)
                        continue;

                    var pageOid = GetPageViewModelOid(pageViewModel);
                    if (pageOid == targetOid)
                        return page;
                }
            }

            // Return null if no match was found
            return null;
        }

        /// <summary>
        /// Gets the Oid of the supplied page view model.
        /// </summary>
        /// <param name="viewModel">The view model to collect the Oid from.</param>
        /// <returns>The Oid of the supplied page view model.</returns>
        private Guid? GetPageViewModelOid(ViewModelBase viewModel)
        {
            // ABort if the view model is null
            if (viewModel == null)
                return null;

            // Attempt to get the Oid from the different view model types that the page may contain
            Guid? oid = null;
            TypeSwitch.On(viewModel.GetType())
                .Case<RibbonPageViewModel>(() => oid = ((RibbonPageViewModel)viewModel).DataObject.Oid)
                .Case<DocumentViewModel>(() => oid = ((DocumentViewModel)viewModel).Oid)
                .Case<WidgetViewModelBase>(() => oid = ((PanelViewModel)((ISupportParentViewModel)viewModel).ParentViewModel).Oid);

            // Return the Oid
            return oid;
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Handles the Loaded event.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_initialized)
                return;

            _initialized = true;

            // Get the MainViewModel
            var mainViewModel = DataContext as MainViewModel;
            if (mainViewModel == null)
                return;

            // Initialize the MainViewModel
            mainViewModel.Initialize();

            // Select the first page in the default category
            RibbonControl.SelectedPage = GetRibbonPageByViewModel(null);
        }

        /// <summary>
        /// Handles the Opening event for the ribbon popup menu.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void RibbonPopupMenu_Opening(object sender, CancelEventArgs e)
        {
            // Cancel this popup if the user didn't click somewhere on the RibbonControl
            if (!_allowPopupMenu)
            {
                e.Cancel = true;
                _allowPopupMenu = true;
            }
        }

        /// <summary>
        /// Handles the RibbonControl.PreviewMouseRightButtonDown event.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void RibbonControl_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Get the ribbon control
            var ribbonControl = (RibbonControl)sender;

            // Get the MainViewModel
            var mainViewModel = DataContext as MainViewModel;
            if (mainViewModel == null)
                return;

            // Get the original source control that raised the event
            var sourceControl = e.OriginalSource as DependencyObject;
            if (sourceControl == null)
                return;

            // If the user didn't right click somewhere on the RibbonControl, clear all the stored context items and disallow the next popup menu
            // (e.g. When right clicking in the backstage view)
            var clickedRibbonControl = sourceControl.FindElementByTypeInParents<RibbonControl>(this);
            if (clickedRibbonControl == null)
            {
                mainViewModel.RibbonContextMenuCategory = null;
                mainViewModel.RibbonContextMenuPage = null;
                mainViewModel.RibbonContextMenuGroup = null;
                mainViewModel.RibbonContextMenuItem = null;
                _allowPopupMenu = false;
            }

            // Find the targeted RibbonPageCategoryHeaderControl or RibbonApplicationButtonControl
            var categoryHeaderControl = LayoutHelper.FindParentObject<RibbonPageCategoryHeaderControl>(sourceControl);
            var applicationButtonControl = LayoutHelper.FindParentObject<RibbonApplicationButtonControl>(sourceControl);
            RibbonPageCategoryBase category = null;
            if (categoryHeaderControl != null)
            {
                // If a category was found, store it
                category = categoryHeaderControl.Category;
            }
            else
            {
                // If no category was found, and no application button was found, find the selected category
                if (applicationButtonControl == null)
                    category = ribbonControl.Categories.FirstOrDefault(c => c.IsSelected);
            }

            // If we don't have a category yet, find the default category
            if (category == null || !(category.DataContext is RibbonCategoryViewModelBase))
                category = ribbonControl.Categories.FirstOrDefault(c => c.IsDefault);

            // If the category is not a local object, assign it to the MainViewModel
            var categoryViewModel = (category != null ? category.DataContext as RibbonCategoryViewModelBase : null);
            if (categoryViewModel != null && !categoryViewModel.DataObject.IsLocalOnly)
                mainViewModel.RibbonContextMenuCategory = categoryViewModel;
            else
                mainViewModel.RibbonContextMenuCategory = null;

            // Find the targeted RibbonPageHeaderControl
            var pageHeaderControl = LayoutHelper.FindParentObject<RibbonPageHeaderControl>(sourceControl);
            if (pageHeaderControl != null)
            {
                // If a page was found, and it is not a local object, assign it to the MainViewModel
                var pageViewModel = pageHeaderControl.Page.DataContext as RibbonPageViewModel;
                if (pageViewModel != null && !pageViewModel.DataObject.IsLocalOnly)
                    mainViewModel.RibbonContextMenuPage = pageViewModel;
                else
                    mainViewModel.RibbonContextMenuPage = null;
            }
            else
            {
                // If no page was found, assign the selected page to the MainViewModel if is not a local object
                var pageViewModel = (ribbonControl.SelectedPage != null ? ribbonControl.SelectedPage.DataContext as RibbonPageViewModel : null);
                if (pageViewModel != null && !pageViewModel.DataObject.IsLocalOnly)
                    mainViewModel.RibbonContextMenuPage = pageViewModel;
                else
                    mainViewModel.RibbonContextMenuPage = null;
            }

            // Find the targeted RibbonPageGroupControl
            var groupControl = LayoutHelper.FindParentObject<RibbonPageGroupControl>(sourceControl);
            if (groupControl != null)
            {
                // If a group was found, and it is not a local object, assign it to the MainViewModel
                var groupViewModel = groupControl.PageGroup.DataContext as RibbonGroupViewModel;
                if (groupViewModel != null && !groupViewModel.DataObject.IsLocalOnly)
                    mainViewModel.RibbonContextMenuGroup = groupViewModel;
                else
                    mainViewModel.RibbonContextMenuGroup = null;
            }
            else
            {
                // If no group was found, assign a null group to the MainViewModel
                mainViewModel.RibbonContextMenuGroup = null;
            }

            // Find the targeted RibbonPageGroupControl
            var itemControl = LayoutHelper.FindParentObject<BarButtonItemLinkControl>(sourceControl);
            if (itemControl != null)
            {
                // If an item was found, and it is not a local object, assign it to the MainViewModel
                var itemViewModel = itemControl.DataContext as RibbonItemViewModelBase;
                if (itemViewModel != null && !itemViewModel.DataObject.IsLocalOnly)
                    mainViewModel.RibbonContextMenuItem = itemViewModel;
                else
                    mainViewModel.RibbonContextMenuItem = null;
            }
            else
            {
                // If no item was found, assign a null item to the MainViewModel
                mainViewModel.RibbonContextMenuItem = null;
            }
        }

        /// <summary>
        /// Handles the RibbonControl.SelectedPageChanged event.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void RibbonControl_SelectedPageChanged(object sender, RibbonPropertyChangedEventArgs e)
        {
            // Get the MainViewModel
            var mainViewModel = DataContext as MainViewModel;
            if (mainViewModel == null)
                return;

            // Apply the SelectedRibbonPageOid
            _selectedRibbonPageViewModelChanging = true;
            mainViewModel.SelectedRibbonPageViewModel = GetSelectedRibbonPageViewModel(e.NewValue as RibbonPage);
            _selectedRibbonPageViewModelChanging = false;
        }

        /// <summary>
        /// Handles the MainViewModel.PropertyChanged event.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void MainViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Abort if the SelectedRibbonPageViewModel is being changed by this view, or some other property has changed
            if (_selectedRibbonPageViewModelChanging || e.PropertyName != "SelectedRibbonPageViewModel")
                return;

            // Get the MainViewModel
            var mainViewModel = DataContext as MainViewModel;
            if (mainViewModel == null)
                return;

            // Select the page
            RibbonControl.SelectedPage = GetRibbonPageByViewModel(mainViewModel.SelectedRibbonPageViewModel);
        }

        /// <summary>
        /// Handles the UndoList.PreviewMouseRightButtonDown event.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void UndoList_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Get the MainViewModel
            var mainViewModel = DataContext as MainViewModel;
            if (mainViewModel == null)
                return;

            // Assign the targeted change set to the MainViewModel
            mainViewModel.SelectedChangeSet = ((FrameworkElement)e.OriginalSource).DataContext as ChangeSet;
        }

        #endregion
    }
}
