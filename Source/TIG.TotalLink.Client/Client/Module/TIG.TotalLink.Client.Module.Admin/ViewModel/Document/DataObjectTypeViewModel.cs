using System;
using DevExpress.Mvvm.DataAnnotations;
using TIG.TotalLink.Client.Core.ViewModel;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Shared.DataModel.Core;

namespace TIG.TotalLink.Client.Module.Admin.ViewModel.Document
{
    /// <summary>
    /// A read-only viewmodel that describes a data object type (i.e. a persistent or non-persistent data object.)
    /// </summary>
    public class DataObjectTypeViewModel : DataModelTypeViewModelBase
    {
        #region Constructors

        public DataObjectTypeViewModel()
        {
        }

        public DataObjectTypeViewModel(Type dataObjectType)
            : base(dataObjectType)
        {
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// Indicates if the Type is a persistent data object.
        /// (i.e. if it inherits from DataObjectBase.)
        /// </summary>
        public bool IsPersistentDataObject
        {
            get { return typeof(DataObjectBase).IsAssignableFrom(Type); }
        }

        /// <summary>
        /// Indicates if the Type is a local data object.
        /// (i.e. if it inherits from LocalDataObjectBase.)
        /// </summary>
        public bool IsLocalDataObject
        {
            get { return typeof(LocalDataObjectBase).IsAssignableFrom(Type); }
        }

        #endregion


        #region Overrides

        public override string Description
        {
            get
            {
                if (IsPersistentDataObject)
                    return "Persistent data object.";

                if (IsLocalDataObject)
                    return "Local data object.";

                return null;
            }
        }

        public override string Category
        {
            get
            {
                if (IsPersistentDataObject)
                    return "Data Object.";

                if (IsLocalDataObject)
                    return "Local Data Object.";

                return null;
            }
        }

        #endregion


        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<DataObjectTypeViewModel> builder)
        {
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<DataObjectTypeViewModel> builder)
        {
        }

        #endregion
    }
}
