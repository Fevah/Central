using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DevExpress.Xpf.Bars;
using DevExpress.Xpf.Docking;
using DevExpress.Xpf.Docking.Base;
using TIG.TotalLink.Client.Editor.Helper;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Document;

namespace TIG.TotalLink.Client.Module.Admin.View.Document
{
    public partial class WidgetContainerView : UserControl
    {
        #region Dependency Properties

        public static readonly DependencyProperty ActivateDocumentRibbonCommandProperty = DependencyProperty.RegisterAttached(
            "ActivateDocumentRibbonCommand", typeof(ICommand), typeof(WidgetContainerView));
        public static readonly DependencyProperty DocumentModifiedCommandProperty = DependencyProperty.RegisterAttached(
            "DocumentModifiedCommand", typeof(ICommand), typeof(WidgetContainerView));

        /// <summary>
        /// Command to activate the ribbon page for the parent document.
        /// </summary>
        public ICommand ActivateDocumentRibbonCommand
        {
            get { return (ICommand)GetValue(ActivateDocumentRibbonCommandProperty); }
            set { SetValue(ActivateDocumentRibbonCommandProperty, value); }
        }

        /// <summary>
        /// Command to flag that the parent document has been modified.
        /// This should be called when any change occurs to the document layout that requires it to be re-saved.
        /// </summary>
        public ICommand DocumentModifiedCommand
        {
            get { return (ICommand)GetValue(DocumentModifiedCommandProperty); }
            set { SetValue(DocumentModifiedCommandProperty, value); }
        }

        #endregion


        #region Constructors

        public WidgetContainerView()
        {
            InitializeComponent();
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Gets the panel layout.
        /// </summary>
        /// <returns>A Stream containing the panel layout.</returns>
        public Stream GetLayout()
        {
            // Make sure all relevant bindings are up-to-date
            UpdateBindings();

            // Save the layout to a stream
            var panelLayout = new MemoryStream();
            DocumentViewDockLayoutManager.SaveLayoutToStream(panelLayout);
            panelLayout.Seek(0, SeekOrigin.Begin);

            // Return the stream
            return panelLayout;
        }

        /// <summary>
        /// Sets the panel layout.
        /// </summary>
        /// <param name="layout">A Stream containing the panel layout to apply.</param>
        public void SetLayout(Stream layout)
        {
            // Make sure all relevant bindings are up-to-date
            UpdateBindings();

            // Restore the layout
            DocumentViewDockLayoutManager.RestoreLayoutFromStream(layout);
            layout.Dispose();
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Ensures that bindings on all controls within the DockLayoutManager are up-to-date so that the layout is saved with the correct data.
        /// </summary>
        private void UpdateBindings()
        {
            UpdatePanelNamesRecursive(RootLayoutGroup.Items);
        }

        /// <summary>
        /// Updates the BindableNameProperty on all LayoutPanels in the collection supplied.
        /// This is required before saving panels because the Oid may have changed but not been updated to the panel control name yet.
        /// When the layout is restored, layouts are applied by matching the panel control names.
        /// </summary>
        /// <param name="layoutItems">The collection of BaseLayoutItems to process.</param>
        private static void UpdatePanelNamesRecursive(BaseLayoutItemCollection layoutItems)
        {
            // Process all layout items
            foreach (var item in layoutItems)
            {
                // If the item is a LayoutPanel, update the BindableNameProperty
                var layoutPanel = item as LayoutPanel;
                if (layoutPanel != null)
                    BindingHelper.UpdateTarget(layoutPanel, BaseLayoutItem.BindableNameProperty);

                // If the items is a LayoutGroup, process all children of the group
                var layoutGroup = item as LayoutGroup;
                if (layoutGroup != null)
                    UpdatePanelNamesRecursive(layoutGroup.Items);
            }
        }

        #endregion


        #region Events

        /// <summary>
        /// Handles the DocumentViewDockLayoutManager.ShowingMenu event.
        /// </summary>
        /// <param name="sender">Object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void DocumentViewDockLayoutManager_ShowingMenu(object sender, ShowingMenuEventArgs e)
        {
            // Attempt to get the TargetElement as a FrameworkElement
            var element = e.TargetElement as FrameworkElement;
            if (element == null)
                return;

            // Attempt to get the element DataContext as a PanelViewModel
            var panel = element.DataContext as PanelViewModel;
            if (panel == null)
                return;

            // Create new bar items for the menu
            var biEditWidget = new BarButtonItem { Name = "biEditWidget", Content = "Edit Widget", Command = panel.EditPanelCommand };
            InsertBarItemLinkAction.SetItemLinkIndex(biEditWidget, 0);

            var biDeleteWidget = new BarButtonItem { Name = "biDeleteWidget", Content = "Delete Widget", Command = panel.PanelClosedCommand };
            InsertBarItemLinkAction.SetItemLinkIndex(biDeleteWidget, 1);

            var biWidgetSeparator = new BarItemLinkSeparator { Name = "biWidgetSeparator" };
            InsertBarItemLinkAction.SetItemLinkIndex(biWidgetSeparator, 2);

            // Add the bar items to the menu
            e.ActionList.Add(biEditWidget);
            e.ActionList.Add(biDeleteWidget);
            e.ActionList.Add(biWidgetSeparator);
        }

        #endregion
    }
}
