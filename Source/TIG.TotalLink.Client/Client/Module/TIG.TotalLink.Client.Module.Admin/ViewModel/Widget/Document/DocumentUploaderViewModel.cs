using System;
using System.Linq;
using DevExpress.Xpo;
using TIG.TotalLink.Client.Module.Admin.Uploader.Document;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Client.Undo.Extension;
using TIG.TotalLink.Shared.DataModel.Admin;
using TIG.TotalLink.Shared.DataModel.Core.Enum.Admin;
using TIG.TotalLink.Shared.Facade.Admin;

namespace TIG.TotalLink.Client.Module.Admin.ViewModel.Widget.Document
{
    public class DocumentUploaderViewModel : UploaderViewModelBase<DocumentUploaderDataModel>
    {
        #region Private Fields

        private readonly IAdminFacade _adminFacade;
        private UnitOfWork _unitOfWork;

        #endregion


        #region Constructors

        public DocumentUploaderViewModel()
        {
        }

        public DocumentUploaderViewModel(IAdminFacade adminFacade)
            : this()
        {
            // Store services
            _adminFacade = adminFacade;
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Finds or creates a Document.
        /// </summary>
        /// <param name="dataModel">The data model containing source values.</param>
        /// <returns>An existing Document if one was found; otherwise returns a new one.</returns>
        private Shared.DataModel.Admin.Document FindOrCreateDocument(DocumentUploaderDataModel dataModel)
        {
            // Abort if the document name is empty
            if (string.IsNullOrWhiteSpace(dataModel.Document))
                return null;

            // Attempt to find an existing document
            var document = _unitOfWork.QueryInTransaction<Shared.DataModel.Admin.Document>().FirstOrDefault(d => d.Name == dataModel.Document);
            if (document != null)
                return document;

            // If no document was found, create a new one
            document = new Shared.DataModel.Admin.Document(_unitOfWork)
            {
                Oid = Guid.NewGuid(),
                Name = dataModel.Document,
                View = dataModel.DocumentView
            };
            return document;
        }

        /// <summary>
        /// Finds or creates a DocumentAction.
        /// </summary>
        /// <param name="dataModel">The data model containing source values.</param>
        /// <param name="document">The Document that this DocumentAction refers to.</param>
        /// <returns>An existing DocumentAction if one was found; otherwise returns a new one.</returns>
        private DocumentAction FindOrCreateDocumentAction(DocumentUploaderDataModel dataModel, Shared.DataModel.Admin.Document document)
        {
            // Abort if the document action name is empty
            if (string.IsNullOrWhiteSpace(dataModel.DocumentAction))
                return null;

            // Error if the document is null
            if (document == null)
                throw new ApplicationException("Document is required when Document Action is included.");

            // Attempt to find an existing document action
            var documentAction = _unitOfWork.QueryInTransaction<DocumentAction>().FirstOrDefault(d => d.Name == dataModel.DocumentAction);
            if (documentAction != null)
                return documentAction;

            // If no document action was found, create a new one
            documentAction = new DocumentAction(_unitOfWork)
            {
                Oid = Guid.NewGuid(),
                Name = dataModel.DocumentAction,
                Document = document
            };
            return documentAction;
        }

        /// <summary>
        /// Finds or creates a PanelGroup.
        /// </summary>
        /// <param name="dataModel">The data model containing source values.</param>
        /// <param name="document">The parent Document for this PanelGroup.</param>
        /// <returns>An existing PanelGroup if one was found; otherwise returns a new one.</returns>
        private PanelGroup FindOrCreatePanelGroup(DocumentUploaderDataModel dataModel, Shared.DataModel.Admin.Document document)
        {
            // Abort if the widget group is empty
            if (string.IsNullOrWhiteSpace(dataModel.WidgetGroup))
                return null;

            // Error if the document is null
            if (document == null)
                throw new ApplicationException("Document is required when Widget details are included.");

            // Attempt to find an existing panel group
            var panelGroup = _unitOfWork.QueryInTransaction<PanelGroup>().FirstOrDefault(p => p.Document.Oid == document.Oid && p.Name == dataModel.WidgetGroup);
            if (panelGroup != null)
                return panelGroup;

            // If no panel group was found, create a new one
            panelGroup = new PanelGroup(_unitOfWork)
            {
                Oid = Guid.NewGuid(),
                Name = dataModel.WidgetGroup,
                Document = document
            };
            return panelGroup;
        }

        /// <summary>
        /// Finds or creates a Panel.
        /// </summary>
        /// <param name="dataModel">The data model containing source values.</param>
        /// <param name="document">The parent Document for this Panel.</param>
        /// <param name="panelGroup">The parent PanelGroup for this Panel.</param>
        /// <returns>An existing Panel if one was found; otherwise returns a new one.</returns>
        private Panel FindOrCreatePanel(DocumentUploaderDataModel dataModel, Shared.DataModel.Admin.Document document, PanelGroup panelGroup)
        {
            // Abort if the widget name is empty
            if (string.IsNullOrWhiteSpace(dataModel.WidgetName))
                return null;

            // Error if the document is null
            if (document == null)
                throw new ApplicationException("Document is required when Widget details are included.");

            // Attempt to find an existing panel
            var panel = _unitOfWork.QueryInTransaction<Panel>().FirstOrDefault(p => p.Document.Oid == document.Oid && p.Name == dataModel.WidgetName && ((panelGroup == null && p.PanelGroup == null) || (panelGroup != null && p.PanelGroup != null && p.PanelGroup.Oid == panelGroup.Oid)));
            if (panel != null)
                return panel;

            // If no panel was found, create a new one
            panel = new Panel(_unitOfWork)
            {
                Oid = Guid.NewGuid(),
                Name = dataModel.WidgetName,
                ViewName = dataModel.WidgetView,
                Document = document,
                PanelGroup = panelGroup
            };
            return panel;
        }

        /// <summary>
        /// Finds or creates a RibbonCategory.
        /// </summary>
        /// <param name="dataModel">The data model containing source values.</param>
        /// <returns>An existing RibbonCategory if one was found; otherwise returns a new one.</returns>
        private RibbonCategory FindOrCreateCategory(DocumentUploaderDataModel dataModel)
        {
            // Abort if the category name is empty
            if (string.IsNullOrWhiteSpace(dataModel.Category))
                return null;

            // Attempt to find an existing category
            var category = dataModel.Category == "(Default)"
                ? _unitOfWork.QueryInTransaction<RibbonCategory>().FirstOrDefault(c => c.IsDefault)
                : _unitOfWork.QueryInTransaction<RibbonCategory>().FirstOrDefault(c => c.Name == dataModel.Category);
            if (category != null)
                return category;

            // If no category was found, create a new one
            category = new RibbonCategory(_unitOfWork)
            {
                Oid = Guid.NewGuid(),
                Name = dataModel.Category,
                IsDefault = (dataModel.Category == "(Default)")
            };
            return category;
        }

        /// <summary>
        /// Finds or creates a RibbonPage.
        /// </summary>
        /// <param name="dataModel">The data model containing source values.</param>
        /// <param name="category">The parent RibbonCategory for this RibbonPage.</param>
        /// <returns>An existing RibbonPage if one was found; otherwise returns a new one.</returns>
        private RibbonPage FindOrCreatePage(DocumentUploaderDataModel dataModel, RibbonCategory category)
        {
            // Abort if the page name is empty
            if (string.IsNullOrWhiteSpace(dataModel.Page))
                return null;

            // Error if the category is null
            if (category == null)
                throw new ApplicationException("Category is required when Page is included.");

            // Attempt to find an existing page
            var page = _unitOfWork.QueryInTransaction<RibbonPage>().FirstOrDefault(p => p.RibbonCategory.Oid == category.Oid && p.Name == dataModel.Page);
            if (page != null)
                return page;

            // If no page was found, create a new one
            page = new RibbonPage(_unitOfWork)
            {
                Oid = Guid.NewGuid(),
                Name = dataModel.Page,
                RibbonCategory = category
            };
            return page;
        }

        /// <summary>
        /// Finds or creates a RibbonGroup.
        /// </summary>
        /// <param name="dataModel">The data model containing source values.</param>
        /// <param name="page">The parent RibbonPage for this RibbonGroup.</param>
        /// <returns>An existing RibbonGroup if one was found; otherwise returns a new one.</returns>
        private RibbonGroup FindOrCreateGroup(DocumentUploaderDataModel dataModel, RibbonPage page)
        {
            // Abort if the group name is empty
            if (string.IsNullOrWhiteSpace(dataModel.Group))
                return null;

            // Error if the page is null
            if (page == null)
                throw new ApplicationException("Page is required when Group is included.");

            // Attempt to find an existing group
            var group = _unitOfWork.QueryInTransaction<RibbonGroup>().FirstOrDefault(g => g.RibbonPage.Oid == page.Oid && g.Name == dataModel.Group);
            if (group != null)
                return group;

            // If no group was found, create a new one
            group = new RibbonGroup(_unitOfWork)
            {
                Oid = Guid.NewGuid(),
                Name = dataModel.Group,
                ShowCaptionButton = false,
                RibbonPage = page
            };
            return group;
        }

        /// <summary>
        /// Finds or creates a RibbonItem.
        /// </summary>
        /// <param name="dataModel">The data model containing source values.</param>
        /// <param name="group">The parent RibbonGroup for this RibbonItem.</param>
        /// <param name="document">The Document that this item should open.</param>
        /// <returns>An existing RibbonItem if one was found; otherwise returns a new one.</returns>
        private RibbonItem FindOrCreateItem(DocumentUploaderDataModel dataModel, RibbonGroup group, Shared.DataModel.Admin.Document document)
        {
            // Abort if the item name is empty
            if (string.IsNullOrWhiteSpace(dataModel.Item))
                return null;

            // Error if the group is null
            if (group == null)
                throw new ApplicationException("Group is required when Item is included.");

            // Attempt to find an existing group
            var item = _unitOfWork.QueryInTransaction<RibbonItem>().FirstOrDefault(i => i.RibbonGroup.Oid == group.Oid && i.Name == dataModel.Item);
            if (item != null)
                return item;

            // If no item was found, create a new one
            item = new RibbonItem(_unitOfWork)
            {
                Oid = Guid.NewGuid(),
                Name = dataModel.Item,
                Description = dataModel.Description,
                ItemType = RibbonItemType.ButtonItem,
                CommandType = CommandType.ShowDocument,
                CommandParameter = (document != null ? document.Oid.ToString() : null),
                RibbonGroup = group
            };
            return item;
        }

        #endregion


        #region Overrides

        protected override void OnWidgetLoaded(EventArgs e)
        {
            base.OnWidgetLoaded(e);

            AddStartupTask(() =>
            {
                // Attempt to connect to the AdminFacade
                ConnectToFacade(_adminFacade);
            });
        }

        protected override void InitializeUpload()
        {
            base.InitializeUpload();

            // Create a UnitOfWork and start notification tracking
            _unitOfWork = _adminFacade.CreateUnitOfWork();
            _unitOfWork.StartUiTracking(this, true, false, true, false);
        }

        protected override void UploadRow(DocumentUploaderDataModel dataModel)
        {
            base.UploadRow(dataModel);

            // Create the document structure
            var document = FindOrCreateDocument(dataModel);
            var documentAction = FindOrCreateDocumentAction(dataModel, document);
            var panelGroup = FindOrCreatePanelGroup(dataModel, document);
            var panel = FindOrCreatePanel(dataModel, document, panelGroup);

            // Create the ribbon structure
            var ribbonCategory = FindOrCreateCategory(dataModel);
            var ribbonPage = FindOrCreatePage(dataModel, ribbonCategory);
            var ribbonGroup = FindOrCreateGroup(dataModel, ribbonPage);
            var ribbonItem = FindOrCreateItem(dataModel, ribbonGroup, document);
        }

        protected override void WriteBatch()
        {
            base.WriteBatch();

            // Commit the UnitOfWork
            _unitOfWork.CommitChanges();
        }

        protected override void FinalizeUpload()
        {
            base.FinalizeUpload();

            // Dispose the UnitOfWork
            try
            {
                _unitOfWork.Dispose();
            }
            catch (Exception)
            {
                // Ignore dispose exceptions
            }
        }

        #endregion
    }
}
