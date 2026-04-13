using System.ComponentModel;

namespace TIG.TotalLink.Client.Core.Descriptor
{
    /// <summary>
    /// A wrapper for PropertyDescriptorCollection which also tracks whether aliases have been generated or not.
    /// </summary>
    public class CachedPropertyDescriptorCollection
    {
        #region Public Properties

        /// <summary>
        /// The cached PropertyDescriptors.
        /// </summary>
        public PropertyDescriptorCollection Properties { get; set; }

        /// <summary>
        /// Indicates if alises have been generated and added to the Properties collection yet.
        /// </summary>
        public bool AreAliasesGenerated { get; set; }

        #endregion
    }
}
