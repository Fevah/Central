using System.Windows.Input;
using TIG.TotalLink.Client.Editor.Core.Editor;

namespace TIG.TotalLink.Client.Editor.Definition
{
    public class CheckedListBoxEditorDefinition : EditorDefinitionBase
    {
        #region Private Fields

        private string _itemsSourcePropertyName;
        private ICommand _mouseEnterItemCommand;
        private ICommand _mouseLeaveItemCommand;

        #endregion


        #region Commands

        /// <summary>
        /// Command that will be executed when the mouse enters an item in the list.
        /// This command can include a parameter of the type of the items in the list, and the item the mouse has entered will be passed in.
        /// </summary>
        public ICommand MouseEnterItemCommand
        {
            get { return _mouseEnterItemCommand; }
            set { SetProperty(ref _mouseEnterItemCommand, value, () => MouseEnterItemCommand); }
        }

        /// <summary>
        /// Command that will be executed when the mouse leaves an item in the list.
        /// This command can include a parameter of the type of the items in the list, and the item the mouse has left will be passed in.
        /// </summary>
        public ICommand MouseLeaveItemCommand
        {
            get { return _mouseLeaveItemCommand; }
            set { SetProperty(ref _mouseLeaveItemCommand, value, () => MouseLeaveItemCommand); }
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The name of the property that the ItemsSource should be collected from.
        /// </summary>
        public string ItemsSourcePropertyName
        {
            get { return _itemsSourcePropertyName; }
            set { SetProperty(ref _itemsSourcePropertyName, value, () => ItemsSourcePropertyName); }
        }

        #endregion
    }
}
