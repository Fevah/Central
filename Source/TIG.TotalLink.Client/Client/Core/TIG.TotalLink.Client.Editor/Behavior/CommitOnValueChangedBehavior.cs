using System;
using System.Windows;
using System.Windows.Threading;
using DevExpress.Xpf.Grid;
using DevExpress.Xpf.Grid.TreeList;

// https://www.devexpress.com/Support/Center/Example/Details/E4155

namespace TIG.TotalLink.Client.Editor.Behavior
{
    /// <summary>
    /// Apply this behaviour to a GridColumn to force it to update the source binding when the value is changed.
    /// </summary>
    public class CommitOnValueChangedBehavior
    {
        #region Dependency Properties

        public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached("Enabled", typeof(bool), typeof(CommitOnValueChangedBehavior), new PropertyMetadata(EnabledPropertyChanged));

        /// <summary>
        /// Gets the Enabled property.
        /// </summary>
        /// <param name="element">The GridColumnBase that the property will be collected from.</param>
        /// <returns>The value of the Enabled property.</returns>
        public static bool GetEnabled(GridColumnBase element)
        {
            return (bool)element.GetValue(EnabledProperty);
        }

        /// <summary>
        /// Sets the Enabled property.
        /// </summary>
        /// <param name="element">The GridColumnBase that the property will be applied to.</param>
        /// <param name="value">The new value for the property.</param>
        public static void SetEnabled(GridColumnBase element, bool value)
        {
            element.SetValue(EnabledProperty, value);
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Handles the PropertyChanged event for the Enabled property.
        /// </summary>
        /// <param name="dp">The DependencyObject that contains the property.</param>
        /// <param name="e">Event arguments.</param>
        private static void EnabledPropertyChanged(DependencyObject dp, DependencyPropertyChangedEventArgs e)
        {
            var col = dp as GridColumnBase;
            if (col.View == null)
                Dispatcher.CurrentDispatcher.BeginInvoke(new Action<GridColumnBase, bool>(ToggleCellValueChanging), col, (bool)e.NewValue);
            else
                ToggleCellValueChanging(col, (bool)e.NewValue);
        }

        /// <summary>
        /// Handles CellValueChanging for columns.
        /// </summary>
        /// <param name="col">The column to handle events on.</param>
        /// <param name="subscribe">Indicates if events should be subscribed or unsubscribed.</param>
        private static void ToggleCellValueChanging(GridColumnBase col, bool subscribe)
        {
            if (!(col.View is DataViewBase))
                return;

            if (subscribe)
            {
                if (col.View is TreeListView)
                    ((TreeListView)col.View).CellValueChanging += TreeCellValueChanging;
                else
                    ((GridViewBase)col.View).CellValueChanging += GridCellValueChanging;
            }
            else
            {
                if (col.View is TreeListView)
                    ((TreeListView)col.View).CellValueChanging -= TreeCellValueChanging;
                else
                    ((GridViewBase)col.View).CellValueChanging -= GridCellValueChanging;
            }
        }

        /// <summary>
        /// Handles CellValueChanging for TreeView columns.
        /// </summary>
        /// <param name="sender">Object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private static void TreeCellValueChanging(object sender, TreeListCellValueChangedEventArgs e)
        {
            if ((bool)e.Column.GetValue(EnabledProperty))
            {
                ((DataViewBase)sender).PostEditor();
                e.Handled = true;
            }
        }

        /// <summary>
        /// Handles CellValueChanging for Grid columns.
        /// </summary>
        /// <param name="sender">Object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private static void GridCellValueChanging(object sender, CellValueChangedEventArgs e)
        {
            if ((bool)e.Column.GetValue(EnabledProperty))
            {
                ((DataViewBase)sender).PostEditor();
                e.Handled = true;
            }
        }

        #endregion
    }
}
