using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using DevExpress.Mvvm;
using DevExpress.Xpo;
using TIG.TotalLink.Shared.DataModel.Core.Helper;
using TIG.TotalLink.Shared.DataModel.Core.Interface;

namespace TIG.TotalLink.Client.Core.ViewModel
{
    /// <summary>
    /// Base class for viewmodels that will be displayed in a grid.
    /// </summary>
    public abstract class LocalDataObjectBase : BindableBase, IAliasedDataObject
    {
        #region Private Fields

        private readonly Dictionary<string, object> _aliasValues = new Dictionary<string, object>();

        #endregion


        #region IAliasedDataObject

        /// <summary>
        /// A dictionary which stores temporary values for aliases on cloned or copied data objects.
        /// </summary>
        [NonPersistent]
        [Display(AutoGenerateField = false)]
        public Dictionary<string, object> AliasValues
        {
            get { return _aliasValues; }
        }

        /// <summary>
        /// Indicates if a temporary alias value is stored for the specified alias.
        /// </summary>
        /// <param name="alias">The AliasFieldMapping to search for.</param>
        /// <returns>True if a temporary value is stored for the specified alias; otherwise false.</returns>
        public bool ContainsAliasValue(AliasedFieldMapping alias)
        {
            return alias != null && AliasValues.ContainsKey(alias.AliasFieldName);
        }

        #endregion
    }
}
