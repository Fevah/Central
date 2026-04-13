using System.ComponentModel.DataAnnotations;
using System.Linq;
using DevExpress.Mvvm;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Document;

namespace TIG.TotalLink.Client.Module.Admin.ViewModel.Core
{
    public abstract class DocumentDataModelBase : ViewModelBase
    {
        #region Private Fields

        private readonly DocumentDataModelAttribute _documentDataModelAttribute;
        private bool _isEmpty = true;
        private bool _isRootModel;

        #endregion


        #region Constructors

        protected DocumentDataModelBase()
        {
            _documentDataModelAttribute = (DocumentDataModelAttribute)GetType().GetCustomAttributes(typeof(DocumentDataModelAttribute), true).Single();
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// Indicates if this data model is empty (i.e. it is a template model only)
        /// </summary>
        [Display(AutoGenerateField = false)]
        public bool IsEmpty
        {
            get { return _isEmpty; }
            set { SetProperty(ref _isEmpty, value, () => IsEmpty); }
        }

        /// <summary>
        /// Returns a display title for this object.
        /// </summary>
        [Display(AutoGenerateField = false)]
        public virtual string Title
        {
            get { return ToString(); }
        }

        #endregion


        #region Protected Properties

        protected bool IsRootModel
        {
            get { return _isRootModel; }
            private set { SetProperty(ref _isRootModel, value, () => IsRootModel); }
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Allows document data models to populate themselves with sample data when they are being sent to a document which acts as a template.
        /// Persistent entities created within this method should be created in the default session (i.e. no session should be specified in the constructor parameters).
        /// </summary>
        public virtual void GenerateSampleData()
        {
        }

        #endregion


        #region Protected Methods

        /// <summary>
        /// Flags that the DocumentId may have changed.
        /// </summary>
        protected void UpdateDocumentId()
        {
            // Abort if this is not the root model
            if (!IsRootModel)
                return;

            // Call the DocumentViewModel to update its title
            var documentViewModel = (DocumentViewModel)((ISupportParentViewModel)this).ParentViewModel;
            documentViewModel.UpdateDocumentId();
        }
        
        #endregion


        #region Overrides

        protected override void OnParentViewModelChanged(object parentViewModel)
        {
            base.OnParentViewModelChanged(parentViewModel);

            if (parentViewModel != null)
            {
                // The parent view model has been assigned

                // Determine if this is the root model
                IsRootModel = parentViewModel is DocumentViewModel;
            }
        }

        /// <summary>
        /// Returns a string representing this object.
        /// Note that this will be used to generate the DocumentId so it must return a unique value based on the primary object being viewed.
        /// </summary>
        /// <returns>A string representing this object.</returns>
        public override string ToString()
        {
            return (IsEmpty ? string.Format("{0} (Template)", _documentDataModelAttribute.Name) : _documentDataModelAttribute.Name);
        }

        #endregion
    }
}
