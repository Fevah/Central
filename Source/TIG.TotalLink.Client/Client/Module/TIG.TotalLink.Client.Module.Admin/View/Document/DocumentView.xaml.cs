using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Controls;
using DevExpress.Mvvm;
using DevExpress.Xpf.Core.Native;
using TIG.TotalLink.Client.Core.Extension;
using TIG.TotalLink.Client.Core.ViewModel;
using TIG.TotalLink.Client.Module.Admin.Helper;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Document;

namespace TIG.TotalLink.Client.Module.Admin.View.Document
{
    public partial class DocumentView : UserControl
    {
        #region Constructors

        public DocumentView()
        {
            InitializeComponent();
        }

        public DocumentView(DocumentViewModel viewModel)
            : this()
        {
            DataContext = viewModel;

            // Initialize viewmodel delegates
            viewModel.GetLayout = GetLayout;
            viewModel.SetLayout = SetLayout;

            // Initialize commands
            viewModel.ActivateDocumentRibbonCommand = new DelegateCommand(OnActivateDocumentRibbonExecute);
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Activates the ribbon page for this document.
        /// </summary>
        private void ActivateDocumentRibbon()
        {
            // Attempt to get the DocumentViewModel
            var documentViewModel = DataContext as DocumentViewModel;
            if (documentViewModel == null)
                return;

            // Abort if the document is still loading
            if (documentViewModel.IsLoadingPanelVisible)
                return;

            // Find the root ribbon control
            var rootRibbonControl = RibbonControl;
            while (rootRibbonControl.ActualMergedParent != null)
                rootRibbonControl = rootRibbonControl.ActualMergedParent;

            // Abort if the currently selected page exists within this document or its widgets
            if (rootRibbonControl.SelectedPage != null)
            {
                var selectedView = LayoutHelper.FindParentObject<DocumentView>(rootRibbonControl.SelectedPage.Ribbon);
                if (ReferenceEquals(selectedView, this))
                    return;
            }

            // Select the DocumentRibbonPage
            rootRibbonControl.SelectedPage = DocumentRibbonPage;
        }

        /// <summary>
        /// Gets the panel layout.
        /// </summary>
        /// <returns>A Stream containing the panel layout.</returns>
        private Stream GetLayout()
        {
            // Create a dictionary to contain all child panel layouts
            var layouts = new Dictionary<Guid, string>();

            // Process each child element to collect all layouts
            LayoutHelper.ForEachElement(this, e =>
            {
                // Attempt to get the element as a WidgetContainerView
                var widgetContainer = e as WidgetContainerView;
                if (widgetContainer == null)
                    return;

                // Attempt to get the WidgetContainerView DataContext as an EntityViewModelBase
                var entityViewModel = widgetContainer.DataContext as EntityViewModelBase;
                if (entityViewModel == null)
                    return;

                // Attempt to get the Oid from the EntityViewModelBase to use as the layout key
                var layoutKey = entityViewModel.DataObjectAsBase.Oid;
                if (layoutKey == Guid.Empty)
                    return;

                // Attempt to get the layout from the WidgetContainerView
                var layout = widgetContainer.GetLayout();
                if (layout == null)
                    return;

                // Add the layout to the dictionary
                layouts.Add(layoutKey, layout.GetAsUtf8String());
            });

            // Return the layouts serialized into a stream
            return DocumentLayoutHelper.ConvertToStream(layouts);
        }

        /// <summary>
        /// Sets the panel layout.
        /// </summary>
        /// <param name="layout">A Stream containing the panel layout to apply.</param>
        private void SetLayout(Stream layout)
        {
            // Get the layouts in a dictionary, and abort if it is null
            var layouts = DocumentLayoutHelper.ConvertFromStream(layout);
            if (layouts == null)
                return;

            // Process each child element to apply all layouts
            LayoutHelper.ForEachElement(this, e =>
            {
                // Attempt to get the element as a WidgetContainerView
                var widgetContainer = e as WidgetContainerView;
                if (widgetContainer == null)
                    return;

                // Attempt to get the WidgetContainerView DataContext as an EntityViewModelBase
                var entityViewModel = widgetContainer.DataContext as EntityViewModelBase;
                if (entityViewModel == null)
                    return;

                // Attempt to get the Oid from the EntityViewModelBase to use as the layout key
                var layoutKey = entityViewModel.Oid;
                if (layoutKey == Guid.Empty)
                    return;

                // Attempt to find the layout for this layout key
                string layoutString;
                if (!layouts.TryGetValue(layoutKey, out layoutString))
                    return;

                // Copy the layout string into a stream and apply it
                var layoutStream = new MemoryStream();
                layoutStream.SetAsUtf8String(layoutString);
                widgetContainer.SetLayout(layoutStream);
            });
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Execute method for the ActivateDocumentRibbonCommand.
        /// </summary>
        private void OnActivateDocumentRibbonExecute()
        {
            ActivateDocumentRibbon();
        }

        #endregion
    }
}
