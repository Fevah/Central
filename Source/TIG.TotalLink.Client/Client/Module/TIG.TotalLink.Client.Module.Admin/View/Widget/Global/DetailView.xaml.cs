using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using DevExpress.Xpf.LayoutControl;
using TIG.TotalLink.Client.Core.Helper;
using TIG.TotalLink.Client.Editor.Control.EventArgs;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.Interface;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Widget.Global;

namespace TIG.TotalLink.Client.Module.Admin.View.Widget.Global
{
    [Widget("Detail", "Global", "Displays details of items selected in a grid.")]
    public partial class DetailView : UserControl
    {
        #region Private Fields

        private readonly List<PropertyChangeNotifier> _modifiedNotifiers = new List<PropertyChangeNotifier>();

        #endregion


        #region Constructors

        public DetailView()
        {
            InitializeComponent();
        }

        public DetailView(DetailViewModel viewModel)
            : this()
        {
            DataContext = viewModel;

            // Attempt to get the DataContext as an ISupportLayoutData
            var supportLayout = DataContext as ISupportLayoutData;
            if (supportLayout == null)
                return;

            // Initialize viewmodel delegates
            supportLayout.GetLayout = DataLayoutControl.GetLayout;
            supportLayout.SetLayout = SetLayout;
        }

        #endregion


        #region Protected Properties

        /// <summary>
        /// Indicates if the parent document has been modified.
        /// </summary>
        protected bool IsDocumentModified
        {
            get
            {
                var widget = DataContext as WidgetViewModelBase;
                if (widget == null)
                    return false;

                return widget.IsDocumentModified;
            }
            set
            {
                var widget = DataContext as WidgetViewModelBase;
                if (widget == null)
                    return;

                widget.IsDocumentModified = value;
            }
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Sets the data form layout.
        /// </summary>
        /// <param name="layout">A Stream containing the data form layout to apply.</param>
        private void SetLayout(Stream layout)
        {
            DataLayoutControl.SetLayout(layout);
        }
        
        /// <summary>
        /// Creates a PropertyChangeNotifier to watch the specified property and flag the document as modified when the property changes.
        /// </summary>
        /// <param name="propertySource">The object to watch for property changes on.</param>
        /// <param name="property">The property to watch for changes.</param>
        private void AddModifiedNotifier(DependencyObject propertySource, DependencyProperty property)
        {
            var notifier = new PropertyChangeNotifier(propertySource, property);
            notifier.ValueChanged += ModifiedNotifier_ValueChanged;
            _modifiedNotifiers.Add(notifier);
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Handles the Loaded event for the DetailViewControl.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void DetailViewControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Abort if the modified notifiers are already being handled
            if (_modifiedNotifiers.Count > 0)
                return;

            // Handle DataLayoutControl modifications
            AddModifiedNotifier(DataLayoutControl, DevExpress.Xpf.LayoutControl.DataLayoutControl.AddColonToItemLabelsProperty);
            AddModifiedNotifier(DataLayoutControl, LayoutControl.AllowItemMovingDuringCustomizationProperty);
            AddModifiedNotifier(DataLayoutControl, LayoutControl.AllowItemRenamingDuringCustomizationProperty);
            AddModifiedNotifier(DataLayoutControl, LayoutControl.AllowItemSizingDuringCustomizationProperty);

            DataLayoutControl.LayoutModified += DataLayoutControl_LayoutModified;
        }


        /// <summary>
        /// Handles the Unloaded event for the DetailViewControl.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void DetailViewControl_Unloaded(object sender, RoutedEventArgs e)
        {
            // Stop handling all modified notifiers
            foreach (var modifiedNotifier in _modifiedNotifiers)
            {
                modifiedNotifier.ValueChanged -= ModifiedNotifier_ValueChanged;
                modifiedNotifier.Dispose();
            }
            _modifiedNotifiers.Clear();

            DataLayoutControl.LayoutModified -= DataLayoutControl_LayoutModified;
        }

        /// <summary>
        /// Handles the ValueChanged event for all dependency properties which should flag the document as modified.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void ModifiedNotifier_ValueChanged(object sender, PropertyChangeNotifierEventArgs e)
        {
            // Flag the document as modified
            IsDocumentModified = true;
        }

        /// <summary>
        /// Handles the DataLayoutControlEx.LayoutModified event.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void DataLayoutControl_LayoutModified(object sender, LayoutModifiedEventArgs e)
        {
            //// Ignore the change if IsVisible changed to false
            //// (because we will get a bunch of them when the CurrentItem is changed)
            //if (e != null && e.ChangeType == LayoutModifiedEventArgs.LayoutChangeTypes.PropertyChange && e.PropertyName == "IsVisble" && !(bool)e.NewPropertyValue)
            //    return;

            // Flag the document as modified
            IsDocumentModified = true;
        }
        
        #endregion
    }
}
