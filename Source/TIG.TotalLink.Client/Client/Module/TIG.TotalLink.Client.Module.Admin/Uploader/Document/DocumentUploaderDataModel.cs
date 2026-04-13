using DevExpress.Mvvm.DataAnnotations;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Client.Module.Admin.Uploader.Core;
using TIG.TotalLink.Shared.DataModel.Core.Enum.Admin;

namespace TIG.TotalLink.Client.Module.Admin.Uploader.Document
{
    public class DocumentUploaderDataModel : UploaderDataModelBase
    {
        #region Private Fields

        private string _category;
        private string _page;
        private string _group;
        private string _item;
        private string _description;
        private string _document;
        private DocumentView _documentView;
        private string _documentAction;
        private string _widgetName;
        private string _widgetView;
        private string _widgetGroup;

        #endregion


        #region Public Properties

        public string Category
        {
            get { return _category; }
            set { SetProperty(ref _category, value, () => Category); }
        }

        public string Page
        {
            get { return _page; }
            set { SetProperty(ref _page, value, () => Page); }
        }

        public string Group
        {
            get { return _group; }
            set { SetProperty(ref _group, value, () => Group); }
        }

        public string Item
        {
            get { return _item; }
            set { SetProperty(ref _item, value, () => Item); }
        }

        public string Description
        {
            get { return _description; }
            set { SetProperty(ref _description, value, () => Description); }
        }

        public string Document
        {
            get { return _document; }
            set { SetProperty(ref _document, value, () => Document); }
        }

        public DocumentView DocumentView
        {
            get { return _documentView; }
            set { SetProperty(ref _documentView, value, () => DocumentView); }
        }

        public string DocumentAction
        {
            get { return _documentAction; }
            set { SetProperty(ref _documentAction, value, () => DocumentAction); }
        }

        public string WidgetName
        {
            get { return _widgetName; }
            set { SetProperty(ref _widgetName, value, () => WidgetName); }
        }

        public string WidgetView
        {
            get { return _widgetView; }
            set { SetProperty(ref _widgetView, value, () => WidgetView); }
        }

        public string WidgetGroup
        {
            get { return _widgetGroup; }
            set { SetProperty(ref _widgetGroup, value, () => WidgetGroup); }
        }

        #endregion


        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<DocumentUploaderDataModel> builder)
        {
            builder.Property(p => p.Category)
                .ReadOnly();
            builder.Property(p => p.Page)
                .ReadOnly();
            builder.Property(p => p.Group)
                .ReadOnly();
            builder.Property(p => p.Item)
                .ReadOnly();
            builder.Property(p => p.Description)
                .ReadOnly();
            builder.Property(p => p.Document)
                .ReadOnly();
            builder.Property(p => p.DocumentAction)
                .ReadOnly();
            builder.Property(p => p.WidgetName)
                .ReadOnly();
            builder.Property(p => p.WidgetView)
                .ReadOnly();
            builder.Property(p => p.WidgetGroup)
                .ReadOnly();
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<DocumentUploaderDataModel> builder)
        {
            builder.Property(p => p.Description).UnlimitedLength();
        }

        #endregion
    }
}
