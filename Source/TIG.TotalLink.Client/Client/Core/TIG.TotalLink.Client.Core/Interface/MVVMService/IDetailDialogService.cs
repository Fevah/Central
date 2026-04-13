using System.ComponentModel;
using TIG.TotalLink.Client.Core.Enum;

namespace TIG.TotalLink.Client.Core.Interface.MVVMService
{
    public interface IDetailDialogService
    {
        #region Public Methods

        /// <summary>
        /// Shows the dialog.
        /// </summary>
        /// <param name="editMode">The mode that the dialog is using to edit.</param>
        /// <param name="editObject">The object being edited by this dialog.</param>
        /// <param name="objectTypeName">Specifies an alternative object type name to use, instead of using the type directly from the data object.</param>
        /// <returns>True if the user pressed OK; otherwise false.</returns>
        bool ShowDialog(DetailEditMode editMode, INotifyPropertyChanged editObject, string objectTypeName = null);

        /// <summary>
        /// Shows the dialog.
        /// </summary>
        /// <param name="editObject">The object being edited by this dialog.</param>
        /// <param name="title">The title for the dialog.</param>
        /// <returns>True if the user pressed OK; otherwise false.</returns>
        bool ShowDialog(INotifyPropertyChanged editObject, string title);

        #endregion
    }
}
