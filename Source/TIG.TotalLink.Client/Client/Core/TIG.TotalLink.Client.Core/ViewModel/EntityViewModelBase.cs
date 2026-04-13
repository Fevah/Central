using System;
using DevExpress.Mvvm;
using TIG.TotalLink.Shared.DataModel.Core;

namespace TIG.TotalLink.Client.Core.ViewModel
{
    /// <summary>
    /// Base classs for viewmodels which wrap a DataObjectBase.
    /// </summary>
    public abstract class EntityViewModelBase : ViewModelBase
    {
        #region Protected Fields

        protected DataObjectBase _dataObject;

        #endregion


        #region Public Properties

        /// <summary>
        /// The DataObject as a DataObjectBase.
        /// </summary>
        public DataObjectBase DataObjectAsBase
        {
            get { return _dataObject; }
        }

        /// <summary>
        /// The Oid of this entity.
        /// </summary>
        public virtual Guid Oid
        {
            get
            {
                if (DataObjectAsBase == null)
                    return Guid.Empty;

                return DataObjectAsBase.Oid;
            }
        }

        #endregion
    }
}
