using System.Collections.Generic;
using TIG.TotalLink.Shared.DataModel.Core.Helper;

namespace TIG.TotalLink.Shared.DataModel.Core.Interface
{
    /// <summary>
    /// Indicates that a data object supports storing temporary alias values when it is cloned or copied.
    /// </summary>
    public interface IAliasedDataObject
    {
        #region Public Properties

        /// <summary>
        /// A dictionary which stores temporary values for aliases on cloned or copied data objects.
        /// </summary>
        Dictionary<string, object> AliasValues { get; }

        #endregion


        #region Public Methods

        /// <summary>
        /// Indicates if a temporary alias value is stored for the specified alias.
        /// </summary>
        /// <param name="alias">The AliasFieldMapping to search for.</param>
        /// <returns>True if a temporary value is stored for the specified alias; otherwise false.</returns>
        bool ContainsAliasValue(AliasedFieldMapping alias);

        #endregion
    }
}
