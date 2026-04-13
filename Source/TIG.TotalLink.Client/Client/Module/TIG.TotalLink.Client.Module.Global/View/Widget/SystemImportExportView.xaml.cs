using System.Collections;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using DevExpress.Xpf.Grid.TreeList;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.Enum;
using TIG.TotalLink.Client.Module.Global.Helper;
using TIG.TotalLink.Client.Module.Global.ViewModel.Widget;

namespace TIG.TotalLink.Client.Module.Global.View.Widget
{
    [HideWidget(HostTypes.Client)]
    [Widget("System Import/Export", "Server", "Allows bulk importing and exporting.")]
    public partial class SystemImportExportView : UserControl
    {
        #region Dependency Properties

        public static readonly DependencyProperty IsWaitIndicatorVisibleProperty = DependencyProperty.RegisterAttached(
            "IsWaitIndicatorVisible", typeof(bool), typeof(SystemImportExportView), new FrameworkPropertyMetadata((s, e) => ((SystemImportExportView)s).OnIsWaitIndicatorVisibleChanged(e)) { DefaultUpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });

        public static readonly DependencyProperty AreAllCheckedProperty = DependencyProperty.RegisterAttached(
            "AreAllChecked", typeof(bool?), typeof(SystemImportExportView), new FrameworkPropertyMetadata((s, e) => ((SystemImportExportView)s).OnAreAllSelectedChanged(e)) { DefaultValue = false, DefaultUpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });

        /// <summary>
        /// Indicates if the tree view is displaying the wait indicator.
        /// </summary>
        public bool IsWaitIndicatorVisible
        {
            get { return (bool)GetValue(IsWaitIndicatorVisibleProperty); }
            set { SetValue(IsWaitIndicatorVisibleProperty, value); }
        }

        /// <summary>
        /// Indicates if all items in the tree are checked.
        /// </summary>
        public bool? AreAllChecked
        {
            get { return (bool?)GetValue(AreAllCheckedProperty); }
            set { SetValue(AreAllCheckedProperty, value); }
        }

        #endregion


        #region Private Fields

        private readonly PropertyInfo _waitIndicatorProperty;
        private bool _updatingSelection;

        #endregion


        #region Constructors

        public SystemImportExportView()
        {
            InitializeComponent();

            // Cache the IsWaitIndicatorVisible property on the TreeView
            _waitIndicatorProperty = TreeListView.GetType().GetProperty("IsWaitIndicatorVisible");
        }

        public SystemImportExportView(SystemImportExportViewModel viewModel)
            : this()
        {
            DataContext = viewModel;

            SetBinding(IsWaitIndicatorVisibleProperty, "IsWaitIndicatorVisible");
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Called when the IsWaitIndicatorVisible property changes
        /// </summary>
        /// <param name="e">Event arguments</param>
        private void OnIsWaitIndicatorVisibleChanged(DependencyPropertyChangedEventArgs e)
        {
            _waitIndicatorProperty.SetValue(TreeListView, e.NewValue);
        }

        /// <summary>
        /// Called when the AreAllSelected property changes
        /// </summary>
        /// <param name="e">Event arguments</param>
        private void OnAreAllSelectedChanged(DependencyPropertyChangedEventArgs e)
        {
            // Abort if the selection is already being updated
            if (_updatingSelection)
                return;

            _updatingSelection = true;

            // If the header checkbox was set to Indeterminate, set it to False
            if (!AreAllChecked.HasValue)
                AreAllChecked = false;

            // If the header checkbox changed from Indeterminate to False, set it to True
            if (!((bool?)e.OldValue).HasValue && (bool?)e.NewValue == false)
                AreAllChecked = true;

            // Update IsChecked on all items to match AreAllChecked
            foreach (var treeItem in (ObservableCollection<TableTreeItem>)TreeListControl.ItemsSource)
                treeItem.IsChecked = AreAllChecked.Value;

            _updatingSelection = false;
        }

        /// <summary>
        /// Handles the NodeCheckStateChanged event on the TreeListView.
        /// </summary>
        /// <param name="sender">Object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void TreeListView_NodeCheckStateChanged(object sender, TreeListNodeEventArgs e)
        {
            // Abort if the selection is already being updated
            if (_updatingSelection)
                return;

            _updatingSelection = true;

            // Update AreAllChecked by comparing the total item count to the checked item count
            var itemsSource = (ObservableCollection<TableTreeItem>)TreeListControl.ItemsSource;
            var itemsCount = itemsSource.Count;
            var checkedCount = itemsSource.Count(t => t.IsChecked);
            if (itemsCount == checkedCount)
                AreAllChecked = true;
            else
                AreAllChecked = (checkedCount == 0 ? (bool?)false : null);

            _updatingSelection = false;
        }

        #endregion
    }
}
