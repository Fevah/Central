using System;
using DevExpress.Mvvm;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Document;

namespace TIG.TotalLink.Client.Module.Admin.Helper
{
    public class WidgetModelInit : BindableBase
    {
        #region Public Enums

        public enum InitModes
        {
            DisplayParent,
            DisplayChild
        }

        #endregion


        #region Private Fields

        private readonly DocumentViewModel _parentDocument;
        private InitModes _initMode;
        private string _childPropertyName;

        #endregion


        #region Constructors

        public WidgetModelInit(Type dataModelType, DocumentViewModel parentDocument)
        {
            DataModelType = dataModelType;
            _parentDocument = parentDocument;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The type of data model that is initialized by this initializer.
        /// </summary>
        public Type DataModelType { get; private set; }

        /// <summary>
        /// The mode used to initialize the DataModelType.
        /// </summary>
        public InitModes InitMode
        {
            get { return _initMode; }
            set { SetProperty(ref _initMode, value, () => InitMode, () => _parentDocument.IsModified = true); }
        }

        /// <summary>
        /// The name of the property to display when InitMode = DisplayChild
        /// </summary>
        public string ChildPropertyName
        {
            get { return _childPropertyName; }
            set { SetProperty(ref _childPropertyName, value, () => ChildPropertyName, () => _parentDocument.IsModified = true); }
        }

        /// <summary>
        /// Indicates if this initializer is empty.
        /// (i.e. No ChildPropertyName has been set)
        /// </summary>
        public bool IsEmpty
        {
            get { return string.IsNullOrWhiteSpace(ChildPropertyName); }
        }

        #endregion
    }
}
