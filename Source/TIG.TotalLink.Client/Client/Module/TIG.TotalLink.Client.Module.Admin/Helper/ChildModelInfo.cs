using System.ComponentModel;
using System.Reflection;

namespace TIG.TotalLink.Client.Module.Admin.Helper
{
    /// <summary>
    /// Helper class used by DetailViewModel which stores information about child models found via ModelInitializers.
    /// </summary>
    public class ChildModelInfo
    {
        /// <summary>
        /// The parent item that the child item was collected from.
        /// </summary>
        public INotifyPropertyChanged ParentItem { get; set; }
        
        /// <summary>
        /// The child item found.
        /// </summary>
        public INotifyPropertyChanged ChildItem { get; set; }

        /// <summary>
        /// The property on the parent item that the child item was collected from.
        /// </summary>
        public PropertyInfo ChildProperty { get; set; }
    }
}
