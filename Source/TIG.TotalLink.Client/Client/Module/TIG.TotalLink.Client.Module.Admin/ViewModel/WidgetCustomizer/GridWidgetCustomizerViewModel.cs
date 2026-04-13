using System.ComponentModel.DataAnnotations;
using System.Windows;
using System.Windows.Input;
using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Xpf.Core.Native;
using DevExpress.Xpf.Grid;
using DevExpress.Xpf.LayoutControl;
using TIG.TotalLink.Client.Core.Helper;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Client.Editor.Control;
using TIG.TotalLink.Client.Editor.Definition;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.Enum.Grid;
using TIG.TotalLink.Client.Module.Admin.Interface;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;

namespace TIG.TotalLink.Client.Module.Admin.ViewModel.WidgetCustomizer
{
    [WidgetCustomizer("Grid", 200)]
    public class GridWidgetCustomizerViewModel : WidgetCustomizerViewModelBase
    {
        #region Private Fields

        private readonly GridControlEx _gridControl;
        private readonly ISupportLayoutData _widget;
        private object _currentItem;
        private readonly PropertyChangeNotifier _searchPanelNotifier;
        private readonly PropertyChangeNotifier _groupPanelNotifier;

        #endregion


        #region Constructors

        public GridWidgetCustomizerViewModel()
        {
        }

        public GridWidgetCustomizerViewModel(GridControlEx gridControl, ISupportLayoutData widget)
            : this()
        {
            // Display this viewmodel in the DataLayoutControl
            CurrentItem = this;

            // Initialize properties
            _gridControl = gridControl;
            _widget = widget;

            // Initialize commands
            RestoreDefaultLayoutCommand = new DelegateCommand(OnRestoreDefaultLayoutExecute);
            RestoreSavedLayoutCommand = new DelegateCommand(OnRestoreSavedLayoutExecute);

            // Handle property change events on the TableView
            var tableView = gridControl.View as TableView;
            if (tableView != null)
            {
                _searchPanelNotifier = new PropertyChangeNotifier(tableView, DataViewBase.ActualShowSearchPanelProperty);
                _searchPanelNotifier.ValueChanged += ShowSearchPanel_ValueChanged;

                _groupPanelNotifier = new PropertyChangeNotifier(tableView, GridViewBase.ShowGroupPanelProperty);
                _groupPanelNotifier.ValueChanged += ShowGroupPanel_ValueChanged;
            }
        }

        #endregion


        #region Commands

        /// <summary>
        /// Command to restore the default grid layout.
        /// </summary>
        public ICommand RestoreDefaultLayoutCommand { get; private set; }

        /// <summary>
        /// Command to restore the last saved grid layout.
        /// </summary>
        public ICommand RestoreSavedLayoutCommand { get; private set; }

        #endregion


        #region Public Properties

        /// <summary>
        /// The object being displayed in the DataLayoutControl.
        /// This will automatically be initialized to contain a reference to this viewmodel.
        /// </summary>
        [Display(AutoGenerateField = false)]
        public object CurrentItem
        {
            get { return _currentItem; }
            set { SetProperty(ref _currentItem, value, () => CurrentItem); }
        }

        /// <summary>
        /// Indicates if the loading panel is visible.  Always false.
        /// Since the LocalDetailView is usually used directly in a widget, it contains a WidgetLoadingPanelView which will attempt to bind to this property.
        /// Therefore we include this property definition to avoid binding errors.
        /// </summary>
        [Display(AutoGenerateField = false)]
        public bool IsLoadingPanelVisible
        {
            get { return false; }
        }

        /// <summary>
        /// Indicates if the group panel is visible.
        /// </summary>
        public bool ShowGroupPanel
        {
            get
            {
                var gridView = _gridControl.View as GridViewBase;
                if (gridView == null)
                    return false;

                return gridView.ShowGroupPanel;
            }
            set
            {
                var gridView = _gridControl.View as GridViewBase;
                if (gridView == null)
                    return;

                gridView.ShowGroupPanel = value;
            }
        }

        /// <summary>
        /// Indicates if the indicator column is visible.
        /// </summary>
        public bool ShowIndicator
        {
            get
            {
                var tableView = _gridControl.View as TableView;
                if (tableView == null)
                    return false;

                return tableView.ShowIndicator;
            }
            set
            {
                var tableView = _gridControl.View as TableView;
                if (tableView == null)
                    return;

                tableView.ShowIndicator = value;
            }
        }

        /// <summary>
        /// Indicates if columns will always fit the width of the grid.
        /// </summary>
        public bool AutoWidth
        {
            get
            {
                var tableView = _gridControl.View as TableView;
                if (tableView == null)
                    return false;

                return tableView.AutoWidth;
            }
            set
            {
                var tableView = _gridControl.View as TableView;
                if (tableView == null)
                    return;

                tableView.AutoWidth = value;
            }
        }

        /// <summary>
        /// Indicates if the user can modify column sorting.
        /// </summary>
        public bool AllowSorting
        {
            get { return _gridControl.View.AllowSorting; }
            set { _gridControl.View.AllowSorting = value; }
        }

        /// <summary>
        /// Indicates if the user can modify column grouping.
        /// </summary>
        public bool AllowGrouping
        {
            get
            {
                var gridView = _gridControl.View as GridViewBase;
                if (gridView == null)
                    return false;

                return gridView.AllowGrouping;
            }
            set
            {
                var gridView = _gridControl.View as GridViewBase;
                if (gridView == null)
                    return;

                gridView.AllowGrouping = value;
            }
        }

        /// <summary>
        /// Indicates if the user can move columns.
        /// </summary>
        public bool AllowColumnMoving
        {
            get { return _gridControl.View.AllowColumnMoving; }
            set { _gridControl.View.AllowColumnMoving = value; }
        }

        /// <summary>
        /// Indicates if the user can resize columns.
        /// </summary>
        public bool AllowResizing
        {
            get
            {
                var tableView = _gridControl.View as TableView;
                if (tableView == null)
                    return false;

                return tableView.AllowResizing;
            }
            set
            {
                var tableView = _gridControl.View as TableView;
                if (tableView == null)
                    return;

                tableView.AllowResizing = value;
            }
        }

        /// <summary>
        /// Indicates if the user can use the Best Fit option.
        /// </summary>
        public bool AllowBestFit
        {
            get
            {
                var tableView = _gridControl.View as TableView;
                if (tableView == null)
                    return false;

                return tableView.AllowBestFit;
            }
            set
            {
                var tableView = _gridControl.View as TableView;
                if (tableView == null)
                    return;

                tableView.AllowBestFit = value;
            }
        }

        /// <summary>
        /// Indicates if the user can modify column filters.
        /// </summary>
        public bool AllowColumnFiltering
        {
            get { return _gridControl.View.AllowColumnFiltering; }
            set { _gridControl.View.AllowColumnFiltering = value; }
        }

        /// <summary>
        /// Indicates if even rows are displayed with a different background color.
        /// </summary>
        public bool UseEvenRowBackground
        {
            get
            {
                var tableView = _gridControl.View as TableView;
                if (tableView == null)
                    return false;

                return tableView.UseEvenRowBackground;
            }
            set
            {
                var tableView = _gridControl.View as TableView;
                if (tableView == null)
                    return;

                tableView.UseEvenRowBackground = value;
            }
        }

        /// <summary>
        /// Indicates if vertical lines are visible.
        /// </summary>
        public bool ShowVerticalLines
        {
            get
            {
                var tableView = _gridControl.View as TableView;
                if (tableView == null)
                    return false;

                return tableView.ShowVerticalLines;
            }
            set
            {
                var tableView = _gridControl.View as TableView;
                if (tableView == null)
                    return;

                tableView.ShowVerticalLines = value;
            }
        }

        /// <summary>
        /// Indicates if horizontal lines are visible.
        /// </summary>
        public bool ShowHorizontalLines
        {
            get
            {
                var tableView = _gridControl.View as TableView;
                if (tableView == null)
                    return false;

                return tableView.ShowHorizontalLines;
            }
            set
            {
                var tableView = _gridControl.View as TableView;
                if (tableView == null)
                    return;

                tableView.ShowHorizontalLines = value;
            }
        }

        /// <summary>
        /// Indicates if the column headers are visible.
        /// </summary>
        public bool ShowColumnHeaders
        {
            get { return _gridControl.View.ShowColumnHeaders; }
            set { _gridControl.View.ShowColumnHeaders = value; }
        }

        /// <summary>
        /// Indicates if the search panel is visible.
        /// </summary>
        public bool ShowSearchPanel
        {
            get
            {
                var tableView = _gridControl.View as TableViewEx;
                if (tableView == null)
                    return false;

                return tableView.IsSearchPanelVisible;
            }
            set
            {
                var tableView = _gridControl.View as TableViewEx;
                if (tableView == null)
                    return;

                tableView.IsSearchPanelVisible = value;
            }
        }

        /// <summary>
        /// Indicates if the auto filter row is visible.
        /// </summary>
        public bool ShowAutoFilterRow
        {
            get
            {
                var tableView = _gridControl.View as TableView;
                if (tableView == null)
                    return false;

                return tableView.ShowAutoFilterRow;
            }
            set
            {
                var tableView = _gridControl.View as TableView;
                if (tableView == null)
                    return;

                tableView.ShowAutoFilterRow = value;
            }
        }

        /// <summary>
        /// Indicates if the checkbox selector column is visible.
        /// </summary>
        public bool ShowCheckBoxSelectorColumn
        {
            get
            {
                var tableView = _gridControl.View as TableView;
                if (tableView == null)
                    return false;

                return tableView.ShowCheckBoxSelectorColumn;
            }
            set
            {
                var tableView = _gridControl.View as TableView;
                if (tableView == null)
                    return;

                tableView.ShowCheckBoxSelectorColumn = value;
            }
        }

        /// <summary>
        /// Indicates what type of total summaries are displayed.
        /// </summary>
        public SummaryType SummaryType
        {
            get
            {
                var tableView = _gridControl.View as TableView;
                if (tableView == null)
                    return SummaryType.None;

                if (tableView.ShowFixedTotalSummary)
                    return SummaryType.Fixed;

                if (tableView.ShowTotalSummary)
                    return SummaryType.Column;

                return SummaryType.None;
            }
            set
            {
                var tableView = _gridControl.View as TableView;
                if (tableView == null)
                    return;

                tableView.ShowFixedTotalSummary = (value == SummaryType.Fixed);
                tableView.ShowTotalSummary = (value == SummaryType.Column);
            }
        }

        /// <summary>
        /// Indicates if the list widget should display a dialog box when adding new items.
        /// </summary>
        public bool UseAddDialog
        {
            get
            {
                var tableView = _gridControl.View as TableViewEx;
                if (tableView != null)
                    return tableView.UseAddDialog;

                //var treeView = _gridControl.View as TreeListViewEx;
                //if (treeView != null)
                //    return treeView.UseAddDialog;

                return false;
            }
            set
            {
                var tableView = _gridControl.View as TableViewEx;
                if (tableView != null)
                    tableView.UseAddDialog = value;

                //var treeView = _gridControl.View as TreeListViewEx;
                //if (treeView != null)
                //    treeView.UseAddDialog = value;
            }
        }

        /// <summary>
        /// Indicates if the grid allows multiple items to be selected at once.
        /// </summary>
        public bool MultiSelect
        {
            get
            {
                return (_gridControl.SelectionMode == MultiSelectMode.Row);
            }
            set
            {
                _gridControl.SelectionMode = (value ? MultiSelectMode.Row : MultiSelectMode.None);
            }
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Execute method for the RestoreDefaultLayout command.
        /// </summary>
        private void OnRestoreDefaultLayoutExecute()
        {
            _widget.ApplyDefaultLayout();
            RefreshAllProperties();
        }

        /// <summary>
        /// Execute method for the RestoreSavedLayout command.
        /// </summary>
        private void OnRestoreSavedLayoutExecute()
        {
            _widget.ApplySavedLayout();
            RefreshAllProperties();
        }

        /// <summary>
        /// Handles the PropertyChanged event for the TableView.ActualShowSearchPanel property.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void ShowSearchPanel_ValueChanged(object sender, PropertyChangeNotifierEventArgs e)
        {
            RaisePropertyChanged(() => ShowSearchPanel);
        }

        /// <summary>
        /// Handles the PropertyChanged event for the TableView.ShowGroupPanel property.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void ShowGroupPanel_ValueChanged(object sender, PropertyChangeNotifierEventArgs e)
        {
            RaisePropertyChanged(() => ShowGroupPanel);
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Refreshes all property bindings.
        /// </summary>
        private void RefreshAllProperties()
        {
            RaisePropertyChanged(() => ShowIndicator);
            RaisePropertyChanged(() => AutoWidth);
            RaisePropertyChanged(() => AllowSorting);
            RaisePropertyChanged(() => AllowGrouping);
            RaisePropertyChanged(() => AllowColumnMoving);
            RaisePropertyChanged(() => AllowResizing);
            RaisePropertyChanged(() => AllowBestFit);
            RaisePropertyChanged(() => AllowColumnFiltering);
            RaisePropertyChanged(() => UseEvenRowBackground);
            RaisePropertyChanged(() => ShowVerticalLines);
            RaisePropertyChanged(() => ShowHorizontalLines);
            RaisePropertyChanged(() => ShowColumnHeaders);
            RaisePropertyChanged(() => ShowAutoFilterRow);
            RaisePropertyChanged(() => ShowCheckBoxSelectorColumn);
            RaisePropertyChanged(() => SummaryType);
            RaisePropertyChanged(() => UseAddDialog);
            RaisePropertyChanged(() => MultiSelect);
        }

        #endregion


        #region Overrides

        public new static WidgetCustomizerViewModelBase CreateCustomizer(FrameworkElement content, WidgetViewModelBase widget)
        {
            // Attempt to find a GridControlEx within the content
            var gridControl = LayoutHelper.FindElementByType<GridControlEx>(content);
            if (gridControl == null)
                return null;

            // Return a new GridWidgetCustomizerViewModel
            return new GridWidgetCustomizerViewModel(gridControl, widget as ISupportLayoutData);
        }

        public override void OnWidgetClosed()
        {
            base.OnWidgetClosed();

            // Stop handling property change events on the TableView
            var tableView = _gridControl.View as TableView;
            if (tableView != null)
            {
                _searchPanelNotifier.ValueChanged -= ShowSearchPanel_ValueChanged;
                _searchPanelNotifier.Dispose();

                _groupPanelNotifier.ValueChanged -= ShowGroupPanel_ValueChanged;
                _groupPanelNotifier.Dispose();
            }
        }

        #endregion


        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<GridWidgetCustomizerViewModel> builder)
        {
            builder.DataFormLayout()
                .GroupBox("Layout")
                    .ContainsProperty(p => p.RestoreDefaultLayoutCommand)
                    .ContainsProperty(p => p.RestoreSavedLayoutCommand)
                .EndGroup()
                .GroupBox("Elements")
                    .ContainsProperty(p => p.ShowSearchPanel)
                    .ContainsProperty(p => p.ShowGroupPanel)
                    .ContainsProperty(p => p.ShowColumnHeaders)
                    .ContainsProperty(p => p.ShowAutoFilterRow)
                    .ContainsProperty(p => p.ShowIndicator)
                    .ContainsProperty(p => p.ShowCheckBoxSelectorColumn)
                    .ContainsProperty(p => p.ShowHorizontalLines)
                    .ContainsProperty(p => p.ShowVerticalLines)
                    .ContainsProperty(p => p.SummaryType)
                .EndGroup()
                .GroupBox("Features")
                    .ContainsProperty(p => p.MultiSelect)
                    .ContainsProperty(p => p.UseAddDialog)
                    .ContainsProperty(p => p.AutoWidth)
                    .ContainsProperty(p => p.UseEvenRowBackground)
                    .ContainsProperty(p => p.AllowSorting)
                    .ContainsProperty(p => p.AllowGrouping)
                    .ContainsProperty(p => p.AllowColumnFiltering)
                    .ContainsProperty(p => p.AllowColumnMoving)
                    .ContainsProperty(p => p.AllowResizing)
                    .ContainsProperty(p => p.AllowBestFit);

            builder.Property(p => p.RestoreDefaultLayoutCommand)
                .DisplayName("Restore Default")
                .Description("Restore the default layout for this grid.");
            builder.Property(p => p.RestoreSavedLayoutCommand)
                .DisplayName("Restore Saved")
                .Description("Restore the last saved layout for this grid.");

            builder.Property(p => p.ShowSearchPanel)
                .DisplayName("Search Panel")
                .Description("Show a panel at the top of the grid for searching.\n(This panel can also be displayed by pressing CTRL+F.)");
            builder.Property(p => p.ShowGroupPanel)
                .DisplayName("Group Panel")
                .Description("Show a panel at the top of the grid for grouping columns.");
            builder.Property(p => p.ShowColumnHeaders)
                .DisplayName("Column Headers")
                .Description("Show a row at the top of the grid containing column names.");
            builder.Property(p => p.ShowAutoFilterRow)
                .DisplayName("Auto Filter Row")
                .Description("Show a row at the top of the grid which allows quick filtering.");
            builder.Property(p => p.ShowIndicator)
                .DisplayName("Indicator Column")
                .Description("Show a thin column at the left of the grid to assist with selecting rows.");
            builder.Property(p => p.ShowCheckBoxSelectorColumn)
                .DisplayName("Checkbox Selector Column")
                .Description("Show a column at the left of the grid which allows rows to be selected via checkboxes.");
            builder.Property(p => p.ShowHorizontalLines)
                .DisplayName("Horizontal Lines")
                .Description("Show horizontal lines between rows.");
            builder.Property(p => p.ShowVerticalLines)
                .DisplayName("Vertical Lines")
                .Description("Show vertical lines between columns.");
            builder.Property(p => p.SummaryType)
                .Description("Show a row at the bottom of the grid where calculated summaries can be displayed.");

            builder.Property(p => p.UseAddDialog)
                .DisplayName("Multi-Select")
                .Description("Allow multiple rows to be selected.");
            builder.Property(p => p.UseAddDialog)
                .DisplayName("Use Add Dialog")
                .Description("If this value is True, when Add is pressed a dialog will be displayed to populate values and the item will not be saved until OK is pressed.\nIf this value is False, when Add is pressed a new item will be created and saved immediately containing default values for all fields.");
            builder.Property(p => p.AutoWidth)
                .DisplayName("Fit Columns To Width")
                .Description("Resize all columns so they fit within the visble width of the grid.");
            builder.Property(p => p.UseEvenRowBackground)
                .DisplayName("Alternate Row Colour")
                .Description("Use a different background colour for all even numbered rows.");
            builder.Property(p => p.AllowSorting)
                .DisplayName("Allow Column Sorting")
                .Description("Allow column sorting to be modified.");
            builder.Property(p => p.AllowGrouping)
                .DisplayName("Allow Column Grouping")
                .Description("Allow column grouping to be modified.");
            builder.Property(p => p.AllowColumnFiltering)
                .DisplayName("Allow Column Filtering")
                .Description("Allow column filtering to be modified.");
            builder.Property(p => p.AllowColumnMoving)
                .DisplayName("Allow Column Moving")
                .Description("Allow columns to be moved.");
            builder.Property(p => p.AllowResizing)
                .DisplayName("Allow Column Resizing")
                .Description("Allow columns to be resized.");
            builder.Property(p => p.AllowBestFit)
                .DisplayName("Allow Best Fit")
                .Description("Allow the Best Fit option to be applied to columns.");
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<GridWidgetCustomizerViewModel> builder)
        {
            builder.Property(p => p.RestoreDefaultLayoutCommand)
                .HideLabel();
            builder.Property(p => p.RestoreSavedLayoutCommand)
                .HideLabel();

            builder.Property(p => p.SummaryType)
                .LabelPosition(LayoutItemLabelPosition.Top)
                .ReplaceEditor(new OptionEditorDefinition(typeof(SummaryType)));
        }

        #endregion
    }
}
